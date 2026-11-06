"""Create, verify, and restore a content-only directory backup."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Any


MANIFEST = ".backup-manifest.json"


def canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def inventory(root: Path, *, exclude_manifest: bool = False) -> list[dict[str, Any]]:
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise ValueError(f"not a directory: {root}")
    files: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            raise ValueError(f"symlinks are not supported: {relative}")
        if path.is_dir():
            continue
        if not path.is_file():
            raise ValueError(f"non-regular file is not supported: {relative}")
        if exclude_manifest and relative == MANIFEST:
            continue
        data = path.read_bytes()
        files.append({"path": relative, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    return files


def _inside(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def _copy_inventory(source: Path, destination: Path, files: list[dict[str, Any]]) -> None:
    for record in files:
        relative = Path(*record["path"].split("/"))
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / relative, target)


def _read_manifest(backup: Path) -> list[dict[str, Any]]:
    try:
        manifest = json.loads((backup / MANIFEST).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"cannot read backup manifest: {error}") from error
    if not isinstance(manifest, dict) or manifest.get("version") != 1 or not isinstance(manifest.get("files"), list):
        raise ValueError("unsupported or malformed backup manifest")
    return manifest["files"]


def verify_backup(backup: Path) -> tuple[int, str]:
    backup = backup.resolve(strict=True)
    expected = _read_manifest(backup)
    actual = inventory(backup, exclude_manifest=True)
    if expected != actual:
        raise ValueError("backup contents do not match the manifest")
    digest = hashlib.sha256(canonical_json(expected)).hexdigest()
    return len(actual), digest


def _new_destination(destination: Path) -> Path:
    requested = destination
    if requested.exists() or requested.is_symlink():
        raise FileExistsError(f"destination already exists: {requested}")
    destination = requested.resolve()
    if destination.exists() or destination.is_symlink():
        raise FileExistsError(f"destination resolves to an existing path: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    return destination


def create_backup(source: Path, destination: Path) -> tuple[int, str]:
    source = source.resolve(strict=True)
    if not source.is_dir():
        raise ValueError(f"source is not a directory: {source}")
    if (source / MANIFEST).exists():
        raise ValueError(f"source uses reserved filename {MANIFEST}")
    files = inventory(source)
    destination = _new_destination(destination)
    if _inside(destination, source):
        raise ValueError("backup destination must be outside the source tree")
    digest = hashlib.sha256(canonical_json(files)).hexdigest()

    with tempfile.TemporaryDirectory(prefix="backup-stage-", dir=destination.parent) as temporary:
        stage = Path(temporary) / "backup"
        stage.mkdir()
        _copy_inventory(source, stage, files)
        if inventory(source) != files:
            raise RuntimeError("source changed while the backup was being copied")
        (stage / MANIFEST).write_bytes(canonical_json({"version": 1, "files": files}) + b"\n")
        if inventory(stage, exclude_manifest=True) != files:
            raise RuntimeError("staged backup failed its content verification")
        if destination.exists() or destination.is_symlink():
            raise FileExistsError(f"destination appeared during backup: {destination}")
        stage.rename(destination)
    return len(files), digest


def restore_backup(backup: Path, destination: Path) -> tuple[int, str]:
    backup = backup.resolve(strict=True)
    count, digest = verify_backup(backup)
    files = _read_manifest(backup)
    destination = _new_destination(destination)
    with tempfile.TemporaryDirectory(prefix="restore-stage-", dir=destination.parent) as temporary:
        stage = Path(temporary) / "restored"
        stage.mkdir()
        _copy_inventory(backup, stage, files)
        shutil.copyfile(backup / MANIFEST, stage / MANIFEST)
        if inventory(stage, exclude_manifest=True) != files:
            raise RuntimeError("restored files failed their content verification")
        if destination.exists() or destination.is_symlink():
            raise FileExistsError(f"destination appeared during restore: {destination}")
        stage.rename(destination)
    return count, digest


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create", help="copy a source directory and record file hashes")
    create.add_argument("--source", type=Path, required=True)
    create.add_argument("--destination", type=Path, required=True)
    verify = commands.add_parser("verify", help="compare backup files to the embedded manifest")
    verify.add_argument("--backup", type=Path, required=True)
    restore = commands.add_parser("restore", help="verify a backup and restore it to a new directory")
    restore.add_argument("--backup", type=Path, required=True)
    restore.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "create":
            count, digest = create_backup(args.source, args.destination)
            print(f"created backup: files={count} inventory_sha256={digest}")
        elif args.command == "verify":
            count, digest = verify_backup(args.backup)
            print(f"PASS: backup verified: files={count} inventory_sha256={digest}")
        else:
            count, digest = restore_backup(args.backup, args.destination)
            print(f"PASS: restored and verified: files={count} inventory_sha256={digest}")
    except (OSError, ValueError, RuntimeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
