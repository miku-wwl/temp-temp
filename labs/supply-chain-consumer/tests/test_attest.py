from __future__ import annotations

import base64
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives import serialization

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from attest import (
    DSSE_PAYLOAD_TYPE,
    SPDX_PREDICATE_TYPE,
    STATEMENT_TYPE,
    canonical_json,
    dsse_pae,
    signed_envelope,
    source_digest,
    verify_attestation,
)


class SupplyChainConsumerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="sbom-consumer-")
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()
        (self.source / "requirements.txt").write_text("requests==2.31.0\n", encoding="utf-8")
        self.private = Ed25519PrivateKey.generate()
        self.public_path = self.root / "trusted.pub"
        self.public_path.write_bytes(
            self.private.public_key().public_bytes(
                serialization.Encoding.PEM,
                serialization.PublicFormat.SubjectPublicKeyInfo,
            )
        )
        self.sbom = {
            "spdxVersion": "SPDX-2.3",
            "packages": [{"name": "requests", "versionInfo": "2.31.0"}],
        }
        self.statement = {
            "_type": STATEMENT_TYPE,
            "subject": [{"name": "fixture-app", "digest": {"sha256": source_digest(self.source)}}],
            "predicateType": SPDX_PREDICATE_TYPE,
            "predicate": self.sbom,
        }
        self.envelope_path = self.root / "attestation.json"
        self.write_envelope(signed_envelope(self.statement, self.private))

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_envelope(self, envelope: dict) -> None:
        self.envelope_path.write_bytes(canonical_json(envelope))

    def verify(self, **kwargs):
        return verify_attestation(
            self.envelope_path,
            self.public_path,
            self.source,
            "fixture-app",
            **kwargs,
        )

    def test_accepts_valid_signature_subject_and_required_package(self) -> None:
        digest, packages, _ = self.verify(expected_package="requests==2.31.0")
        self.assertEqual(digest, source_digest(self.source))
        self.assertEqual(packages, 1)

    def test_rejects_payload_tampering(self) -> None:
        envelope = json.loads(self.envelope_path.read_text(encoding="utf-8"))
        payload = json.loads(base64.b64decode(envelope["payload"]))
        payload["subject"][0]["name"] = "attacker-app"
        envelope["payload"] = base64.b64encode(canonical_json(payload)).decode("ascii")
        self.write_envelope(envelope)
        with self.assertRaisesRegex(ValueError, "signature verification failed"):
            self.verify()

    def test_rejects_wrong_trusted_key(self) -> None:
        other = Ed25519PrivateKey.generate().public_key().public_bytes(
            serialization.Encoding.PEM,
            serialization.PublicFormat.SubjectPublicKeyInfo,
        )
        self.public_path.write_bytes(other)
        with self.assertRaisesRegex(ValueError, "no DSSE signature matches"):
            self.verify()

    def test_rejects_changed_source_tree(self) -> None:
        (self.source / "app.py").write_text("changed\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "digest does not match"):
            self.verify()

    def test_rejects_wrong_subject_policy(self) -> None:
        with self.assertRaisesRegex(ValueError, "subject name does not match"):
            verify_attestation(self.envelope_path, self.public_path, self.source, "other-app")

    def test_rejects_required_package_absent_from_signed_sbom(self) -> None:
        with self.assertRaisesRegex(ValueError, "required package not found"):
            self.verify(expected_package="flask==3.0.0")

    def test_rejects_detached_sbom_that_differs_from_signed_predicate(self) -> None:
        detached = self.root / "detached.json"
        detached.write_text(json.dumps({"spdxVersion": "SPDX-2.3", "packages": []}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "does not match the signed predicate"):
            self.verify(sbom_path=detached)

    def test_rejects_wrong_payload_type_even_with_valid_signature(self) -> None:
        envelope = signed_envelope(self.statement, self.private)
        envelope["payloadType"] = "text/plain"
        self.write_envelope(envelope)
        with self.assertRaisesRegex(ValueError, "unexpected DSSE payloadType"):
            self.verify()

    def test_source_digest_tracks_content_but_not_mtime(self) -> None:
        path = self.source / "requirements.txt"
        first = source_digest(self.source)
        os.utime(path, (1_700_000_000, 1_700_000_000))
        self.assertEqual(first, source_digest(self.source))
        path.write_text("requests==2.32.0\n", encoding="utf-8")
        self.assertNotEqual(first, source_digest(self.source))

    def test_dsse_pae_length_prefixes_are_byte_lengths(self) -> None:
        payload_type = "类型"
        payload = "内容".encode("utf-8")
        expected = f"DSSEv1 {len(payload_type.encode())} {payload_type} {len(payload)} ".encode() + payload
        self.assertEqual(dsse_pae(payload_type, payload), expected)

    @unittest.skipIf(os.name == "nt", "Windows symlink creation may require elevated policy")
    def test_source_digest_rejects_symlinks(self) -> None:
        target = self.source / "requirements.txt"
        link = self.source / "link.txt"
        os.symlink(target, link)
        with self.assertRaisesRegex(ValueError, "symlink"):
            source_digest(self.source)


if __name__ == "__main__":
    unittest.main()
