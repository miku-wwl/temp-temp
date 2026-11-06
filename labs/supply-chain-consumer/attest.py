"""Create and verify a small local Syft SPDX SBOM attestation."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey, Ed25519PublicKey


STATEMENT_TYPE = "https://in-toto.io/Statement/v1"
SPDX_PREDICATE_TYPE = "https://spdx.dev/Document/v2.3"
DSSE_PAYLOAD_TYPE = "application/vnd.in-toto+json"


def canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def source_digest(source: Path) -> str:
    """Hash sorted relative paths and file bytes; reject links and special files."""
    root = source.resolve(strict=True)
    if not root.is_dir():
        raise ValueError(f"source is not a directory: {root}")
    entries: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if path.is_symlink():
            raise ValueError(f"source contains a symlink: {path.relative_to(root).as_posix()}")
        if path.is_dir():
            continue
        if not path.is_file():
            raise ValueError(f"source contains a non-regular file: {path.relative_to(root).as_posix()}")
        content = path.read_bytes()
        entries.append(
            {
                "path": path.relative_to(root).as_posix(),
                "size": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        )
    return hashlib.sha256(canonical_json(entries)).hexdigest()


def dsse_pae(payload_type: str, payload: bytes) -> bytes:
    """Return DSSE v1 Pre-Authentication Encoding for the exact payload bytes."""
    encoded_type = payload_type.encode("utf-8")
    return (
        b"DSSEv1 "
        + str(len(encoded_type)).encode("ascii")
        + b" "
        + encoded_type
        + b" "
        + str(len(payload)).encode("ascii")
        + b" "
        + payload
    )


def public_key_id(public_key: Ed25519PublicKey) -> str:
    der = public_key.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    return hashlib.sha256(der).hexdigest()


def statement_for(source: Path, subject_name: str, sbom: dict[str, Any]) -> dict[str, Any]:
    if sbom.get("spdxVersion") != "SPDX-2.3" or not isinstance(sbom.get("packages"), list):
        raise ValueError("Syft output is not a SPDX-2.3 document with a packages list")
    return {
        "_type": STATEMENT_TYPE,
        "subject": [{"name": subject_name, "digest": {"sha256": source_digest(source)}}],
        "predicateType": SPDX_PREDICATE_TYPE,
        "predicate": sbom,
    }


def signed_envelope(statement: dict[str, Any], private_key: Ed25519PrivateKey) -> dict[str, Any]:
    payload = canonical_json(statement)
    public_key = private_key.public_key()
    return {
        "payloadType": DSSE_PAYLOAD_TYPE,
        "payload": base64.b64encode(payload).decode("ascii"),
        "signatures": [
            {
                "keyid": public_key_id(public_key),
                "sig": base64.b64encode(private_key.sign(dsse_pae(DSSE_PAYLOAD_TYPE, payload))).decode("ascii"),
            }
        ],
    }


def _load_public_key(path: Path) -> Ed25519PublicKey:
    key = serialization.load_pem_public_key(path.read_bytes())
    if not isinstance(key, Ed25519PublicKey):
        raise ValueError("trusted key must be Ed25519")
    return key


def _inside(child: Path, parent: Path) -> bool:
    try:
        child.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def _write_output(path: Path, data: bytes, force: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not force and path.exists():
        raise FileExistsError(f"refusing to overwrite existing output: {path}")
    if force:
        path.write_bytes(data)
    else:
        with path.open("xb") as stream:
            stream.write(data)


def create_attestation(
    source: Path,
    subject_name: str,
    source_version: str,
    syft: str,
    private_key_path: Path,
    sbom_path: Path,
    envelope_path: Path,
    force: bool = False,
) -> tuple[str, int, str]:
    source = source.resolve(strict=True)
    for output in (sbom_path, envelope_path):
        if _inside(output, source):
            raise ValueError("SBOM and envelope outputs must be outside the source tree")
    if sbom_path.resolve() == envelope_path.resolve():
        raise ValueError("SBOM and envelope outputs must be different paths")
    if not force and (sbom_path.exists() or envelope_path.exists()):
        raise FileExistsError("an output already exists; choose new paths or pass --force")

    before = source_digest(source)
    with tempfile.TemporaryDirectory(prefix="syft-spdx-") as temporary:
        generated_sbom = Path(temporary) / "sbom.spdx.json"
        command = [
            syft,
            "scan",
            f"dir:{source}",
            "-o",
            f"spdx-json={generated_sbom}",
            "--source-name",
            subject_name,
            "--source-version",
            source_version,
            "--quiet",
        ]
        result = subprocess.run(command, capture_output=True, text=True, check=False)
        if result.returncode:
            raise RuntimeError(f"Syft failed ({result.returncode}): {result.stderr.strip()}")
        sbom_bytes = generated_sbom.read_bytes()
    after = source_digest(source)
    if before != after:
        raise RuntimeError("source changed while Syft was scanning it")

    sbom = json.loads(sbom_bytes)
    statement = statement_for(source, subject_name, sbom)
    private_key = serialization.load_pem_private_key(private_key_path.read_bytes(), password=None)
    if not isinstance(private_key, Ed25519PrivateKey):
        raise ValueError("signing key must be Ed25519")
    envelope = signed_envelope(statement, private_key)
    _write_output(sbom_path, sbom_bytes, force)
    _write_output(envelope_path, canonical_json(envelope) + b"\n", force)
    return before, len(sbom["packages"]), envelope["signatures"][0]["keyid"]


def verify_attestation(
    envelope_path: Path,
    public_key_path: Path,
    source: Path,
    expected_subject: str,
    expected_package: str | None = None,
    sbom_path: Path | None = None,
) -> tuple[str, int, str]:
    public_key = _load_public_key(public_key_path)
    key_id = public_key_id(public_key)
    envelope = json.loads(envelope_path.read_text(encoding="utf-8"))
    if envelope.get("payloadType") != DSSE_PAYLOAD_TYPE:
        raise ValueError("unexpected DSSE payloadType")
    try:
        payload = base64.b64decode(envelope["payload"], validate=True)
    except (KeyError, ValueError) as error:
        raise ValueError("invalid DSSE payload encoding") from error
    signatures = envelope.get("signatures")
    if not isinstance(signatures, list) or any(not isinstance(signature, dict) for signature in signatures):
        raise ValueError("DSSE signatures must be a list of objects")
    matches = [signature for signature in signatures if signature.get("keyid") == key_id]
    if not matches:
        raise ValueError("no DSSE signature matches the trusted key")
    valid = False
    for signature in matches:
        try:
            public_key.verify(
                base64.b64decode(signature["sig"], validate=True),
                dsse_pae(DSSE_PAYLOAD_TYPE, payload),
            )
            valid = True
            break
        except (InvalidSignature, KeyError, ValueError, TypeError):
            continue
    if not valid:
        raise ValueError("DSSE signature verification failed")

    statement = json.loads(payload)
    if statement.get("_type") != STATEMENT_TYPE or statement.get("predicateType") != SPDX_PREDICATE_TYPE:
        raise ValueError("unexpected in-toto statement or predicate type")
    subjects = statement.get("subject")
    if not isinstance(subjects, list) or len(subjects) != 1:
        raise ValueError("statement must contain exactly one subject")
    subject = subjects[0]
    if subject.get("name") != expected_subject:
        raise ValueError("attested subject name does not match policy")
    actual_digest = source_digest(source)
    if subject.get("digest", {}).get("sha256") != actual_digest:
        raise ValueError("attested subject digest does not match source tree")
    sbom = statement.get("predicate")
    if not isinstance(sbom, dict) or sbom.get("spdxVersion") != "SPDX-2.3":
        raise ValueError("signed predicate is not SPDX-2.3")
    packages = sbom.get("packages")
    if not isinstance(packages, list):
        raise ValueError("SPDX packages must be a list")
    if sbom_path is not None:
        supplied_sbom = json.loads(sbom_path.read_text(encoding="utf-8"))
        if supplied_sbom != sbom:
            raise ValueError("detached SBOM does not match the signed predicate")
    if expected_package:
        name, separator, version = expected_package.partition("==")
        if not separator or not name or not version:
            raise ValueError("expected package must use name==version syntax")
        if not any(item.get("name") == name and item.get("versionInfo") == version for item in packages):
            raise ValueError(f"required package not found in signed SBOM: {expected_package}")
    return actual_digest, len(packages), key_id


def _keygen(private_path: Path, public_path: Path, force: bool) -> None:
    if private_path.resolve() == public_path.resolve():
        raise ValueError("private and public key paths must be different")
    if not force and (private_path.exists() or public_path.exists()):
        raise FileExistsError("refusing to overwrite an existing key")
    key = Ed25519PrivateKey.generate()
    private_bytes = key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.PKCS8,
        serialization.NoEncryption(),
    )
    public_bytes = key.public_key().public_bytes(
        serialization.Encoding.PEM,
        serialization.PublicFormat.SubjectPublicKeyInfo,
    )
    _write_output(private_path, private_bytes, force)
    _write_output(public_path, public_bytes, force)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    keygen = commands.add_parser("keygen", help="create a local Ed25519 demonstration key pair")
    keygen.add_argument("--private-key", type=Path, required=True)
    keygen.add_argument("--public-key", type=Path, required=True)
    keygen.add_argument("--force", action="store_true")

    create = commands.add_parser("create", help="scan a source directory and sign its SPDX SBOM")
    create.add_argument("--source", type=Path, required=True)
    create.add_argument("--subject-name", required=True)
    create.add_argument("--source-version", required=True)
    create.add_argument("--syft", default="syft", help="Syft executable path (default: syft on PATH)")
    create.add_argument("--private-key", type=Path, required=True)
    create.add_argument("--sbom", type=Path, required=True)
    create.add_argument("--envelope", type=Path, required=True)
    create.add_argument("--force", action="store_true")

    verify = commands.add_parser("verify", help="verify a DSSE signature and enforce local consumer policy")
    verify.add_argument("--source", type=Path, required=True)
    verify.add_argument("--subject-name", required=True)
    verify.add_argument("--envelope", type=Path, required=True)
    verify.add_argument("--public-key", type=Path, required=True)
    verify.add_argument("--sbom", type=Path)
    verify.add_argument("--expected-package")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "keygen":
            _keygen(args.private_key, args.public_key, args.force)
            print("created local Ed25519 key pair; keep the private key outside the repository")
        elif args.command == "create":
            digest, package_count, key_id = create_attestation(
                args.source,
                args.subject_name,
                args.source_version,
                args.syft,
                args.private_key,
                args.sbom,
                args.envelope,
                args.force,
            )
            print(f"created signed SPDX-2.3 SBOM: packages={package_count} source_sha256={digest} keyid={key_id}")
        else:
            digest, package_count, key_id = verify_attestation(
                args.envelope,
                args.public_key,
                args.source,
                args.subject_name,
                args.expected_package,
                args.sbom,
            )
            print(f"PASS: signed SBOM accepted: packages={package_count} source_sha256={digest} keyid={key_id}")
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
