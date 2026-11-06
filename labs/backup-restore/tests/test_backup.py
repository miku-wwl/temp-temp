from __future__ import annotations

import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from backup import create_backup, restore_backup, verify_backup


class BackupRestoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="backup-restore-")
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        (self.source / "nested").mkdir(parents=True)
        (self.source / "a.txt").write_text("alpha\n", encoding="utf-8")
        (self.source / "nested" / "b.bin").write_bytes(b"\x00beta\xff")
        self.backup = self.root / "backup"

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_creates_verifies_and_restores_backup(self) -> None:
        count, digest = create_backup(self.source, self.backup)
        self.assertEqual(count, 2)
        self.assertEqual(verify_backup(self.backup), (count, digest))
        restored = self.root / "restored"
        self.assertEqual(restore_backup(self.backup, restored), (count, digest))
        self.assertEqual(verify_backup(restored), (count, digest))
        self.assertEqual((restored / "nested" / "b.bin").read_bytes(), b"\x00beta\xff")

    def test_detects_modified_backup_file(self) -> None:
        create_backup(self.source, self.backup)
        (self.backup / "a.txt").write_text("tampered\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "do not match the manifest"):
            verify_backup(self.backup)

    def test_detects_missing_backup_file(self) -> None:
        create_backup(self.source, self.backup)
        (self.backup / "a.txt").unlink()
        with self.assertRaisesRegex(ValueError, "do not match the manifest"):
            verify_backup(self.backup)

    def test_refuses_to_overwrite_existing_destination(self) -> None:
        self.backup.mkdir()
        marker = self.backup / "keep.txt"
        marker.write_text("owner data", encoding="utf-8")
        with self.assertRaises(FileExistsError):
            create_backup(self.source, self.backup)
        self.assertEqual(marker.read_text(encoding="utf-8"), "owner data")

    def test_restore_refuses_existing_destination(self) -> None:
        create_backup(self.source, self.backup)
        destination = self.root / "existing"
        destination.mkdir()
        marker = destination / "keep.txt"
        marker.write_text("owner data", encoding="utf-8")
        with self.assertRaises(FileExistsError):
            restore_backup(self.backup, destination)
        self.assertEqual(marker.read_text(encoding="utf-8"), "owner data")

    def test_refuses_dangling_destination_symlink(self) -> None:
        link = self.root / "backup-link"
        target = self.root / "do-not-create.txt"
        try:
            os.symlink(target, link)
        except (OSError, NotImplementedError):
            self.skipTest("symlink creation is not permitted")
        with self.assertRaises(FileExistsError):
            create_backup(self.source, link)
        self.assertFalse(target.exists())

    def test_rejects_reserved_manifest_source_file(self) -> None:
        (self.source / ".backup-manifest.json").write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "reserved filename"):
            create_backup(self.source, self.backup)

    def test_rejects_backup_destination_inside_source(self) -> None:
        with self.assertRaisesRegex(ValueError, "outside the source tree"):
            create_backup(self.source, self.source / "backup")

    def test_rejects_malformed_manifest(self) -> None:
        create_backup(self.source, self.backup)
        (self.backup / ".backup-manifest.json").write_text('{"version":1,"files":"bad"}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "malformed backup manifest"):
            verify_backup(self.backup)

    def test_rejects_symlinked_source_file(self) -> None:
        link = self.source / "link.txt"
        try:
            os.symlink(self.source / "a.txt", link)
        except (OSError, NotImplementedError):
            self.skipTest("symlink creation is not permitted")
        with self.assertRaisesRegex(ValueError, "symlinks are not supported"):
            create_backup(self.source, self.backup)


if __name__ == "__main__":
    unittest.main()
