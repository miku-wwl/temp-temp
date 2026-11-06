from __future__ import annotations

import hashlib
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate import validate_bundle


class HandoffValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="handoff-check-")
        self.root = Path(self.temporary.name)
        (self.root / "runbooks").mkdir()
        (self.root / "evidence").mkdir()
        (self.root / "runbooks" / "alert.md").write_text("follow the runbook\n", encoding="utf-8")
        self.evidence = self.root / "evidence" / "alert.json"
        self.evidence.write_text('{"sample":true}\n', encoding="utf-8")
        self.bundle_path = self.root / "handoff.json"
        self.write_bundle()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def document(self) -> dict:
        return {
            "incident_id": "INC-2026-0001",
            "service": "checkout",
            "owner": "primary-oncall",
            "opened_at": "2026-11-07T00:00:00Z",
            "alerts": [
                {
                    "name": "High5xxRate",
                    "runbook": "runbooks/alert.md",
                    "evidence": "evidence/alert.json",
                    "sha256": hashlib.sha256(self.evidence.read_bytes()).hexdigest(),
                }
            ],
        }

    def write_bundle(self, document: dict | None = None) -> None:
        self.bundle_path.write_text(json.dumps(document or self.document()), encoding="utf-8")

    def test_accepts_complete_bundle_with_matching_evidence_hash(self) -> None:
        result = validate_bundle(Path("handoff.json"), self.root)
        self.assertEqual(result["status"], "PASS")
        self.assertEqual(result["alerts_checked"], 1)

    def test_rejects_changed_evidence(self) -> None:
        self.evidence.write_text("changed\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "evidence hash mismatch"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_missing_runbook(self) -> None:
        doc = self.document()
        doc["alerts"][0]["runbook"] = "runbooks/missing.md"
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "does not exist"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_path_traversal(self) -> None:
        doc = self.document()
        doc["alerts"][0]["evidence"] = "../../outside.json"
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "must not traverse"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_absolute_path(self) -> None:
        doc = self.document()
        doc["alerts"][0]["runbook"] = str(self.root / "runbooks" / "alert.md")
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "relative path"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_missing_owner(self) -> None:
        doc = self.document()
        doc["owner"] = "  "
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "owner must be a non-empty"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_timezone_free_timestamp(self) -> None:
        doc = self.document()
        doc["opened_at"] = "2026-11-07T00:00:00"
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "must include a timezone"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_duplicate_alert_names(self) -> None:
        doc = self.document()
        doc["alerts"].append(dict(doc["alerts"][0]))
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "duplicate alert name"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_bad_checksum_format(self) -> None:
        doc = self.document()
        doc["alerts"][0]["sha256"] = "not-a-digest"
        self.write_bundle(doc)
        with self.assertRaisesRegex(ValueError, "lowercase SHA-256"):
            validate_bundle(Path("handoff.json"), self.root)

    def test_rejects_symlinked_runbook(self) -> None:
        path = self.root / "runbooks" / "alert.md"
        target = self.root / "runbooks" / "real.md"
        target.write_text("runbook\n", encoding="utf-8")
        path.unlink()
        try:
            os.symlink(target, path)
        except (OSError, NotImplementedError):
            self.skipTest("symlink creation is not permitted")
        with self.assertRaisesRegex(ValueError, "must not use symlinks"):
            validate_bundle(Path("handoff.json"), self.root)


if __name__ == "__main__":
    unittest.main()
