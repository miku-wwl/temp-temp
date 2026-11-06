from __future__ import annotations

import gzip
import hashlib
import os
import struct
import stat
import subprocess
import sys
import tarfile
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pack import archive_mode, pack_archive


LAB = Path(__file__).resolve().parents[1]
PACK_CLI = LAB / "pack.py"
NAIVE_CLI = LAB / "naive_pack.py"
EPOCH = 1_700_000_000


class ReproducibleArchiveTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="archive-tests-")
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        (self.source / "nested").mkdir(parents=True)
        (self.source / "z.txt").write_text("zeta\n", encoding="utf-8")
        (self.source / "nested" / "a.txt").write_text("alpha\n", encoding="utf-8")
        os.utime(self.source / "z.txt", (EPOCH + 1, EPOCH + 1))
        os.utime(self.source / "nested" / "a.txt", (EPOCH + 2, EPOCH + 2))

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def sha256(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def test_normalized_digest_ignores_source_mtime(self) -> None:
        first = self.root / "first.tar.gz"
        second = self.root / "second.tar.gz"
        first_digest = pack_archive(self.source, first, EPOCH)
        os.utime(self.source / "z.txt", (EPOCH + 900, EPOCH + 900))
        second_digest = pack_archive(self.source, second, EPOCH)
        self.assertEqual(first_digest, second_digest)
        self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_member_order_metadata_and_gzip_time_are_normalized(self) -> None:
        archive_path = self.root / "normalized.tar.gz"
        pack_archive(self.source, archive_path, EPOCH)
        raw = archive_path.read_bytes()
        self.assertEqual(struct.unpack("<I", raw[4:8])[0], EPOCH)

        with gzip.open(archive_path, "rb") as compressed:
            with tarfile.open(fileobj=compressed, mode="r:") as archive:
                members = archive.getmembers()
        self.assertEqual([item.name for item in members], ["nested/a.txt", "z.txt"])
        for item in members:
            self.assertEqual(item.mtime, EPOCH)
            self.assertEqual((item.uid, item.gid), (0, 0))
            self.assertEqual((item.uname, item.gname), ("", ""))
            self.assertEqual(item.mode, 0o644)

    def test_content_change_changes_digest(self) -> None:
        first = self.root / "first.tar.gz"
        changed = self.root / "changed.tar.gz"
        pack_archive(self.source, first, EPOCH)
        (self.source / "z.txt").write_text("different content\n", encoding="utf-8")
        pack_archive(self.source, changed, EPOCH)
        self.assertNotEqual(self.sha256(first), self.sha256(changed))

    def test_archive_mode_uses_explicit_posix_execute_bits(self) -> None:
        self.assertEqual(archive_mode(stat.S_IFREG | 0o644), 0o644)
        self.assertEqual(archive_mode(stat.S_IFREG | 0o755), 0o755)

    @unittest.skipIf(os.name == "nt", "Windows chmod does not expose POSIX execute bits")
    def test_executable_bit_is_normalized_in_archive(self) -> None:
        executable = self.source / "run.sh"
        executable.write_text("#!/bin/sh\n", encoding="utf-8")
        executable.chmod(0o755)
        output = self.root / "modes.tar.gz"
        pack_archive(self.source, output, EPOCH)
        with gzip.open(output, "rb") as compressed:
            with tarfile.open(fileobj=compressed, mode="r:") as archive:
                member = archive.getmember("run.sh")
        self.assertEqual(member.mode, 0o755)

    def test_naive_baseline_tracks_source_mtime(self) -> None:
        first, second = self.root / "naive-one.tar.gz", self.root / "naive-two.tar.gz"
        subprocess.run(
            [sys.executable, str(NAIVE_CLI), "--source", str(self.source), "--output", str(first)],
            check=True,
            capture_output=True,
            text=True,
        )
        os.utime(self.source / "z.txt", (EPOCH + 120, EPOCH + 120))
        subprocess.run(
            [sys.executable, str(NAIVE_CLI), "--source", str(self.source), "--output", str(second)],
            check=True,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(self.sha256(first), self.sha256(second))

    @unittest.skipIf(os.name == "nt", "Windows symlink creation may require elevated policy")
    def test_symlinked_file_and_directory_are_rejected(self) -> None:
        file_link = self.source / "file-link.txt"
        os.symlink(self.source / "z.txt", file_link)
        with self.assertRaisesRegex(ValueError, "non-regular file"):
            pack_archive(self.source, self.root / "file-link.tar.gz", EPOCH)
        file_link.unlink()

        directory_link = self.source / "directory-link"
        os.symlink(self.source / "nested", directory_link, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "non-directory path"):
            pack_archive(self.source, self.root / "directory-link.tar.gz", EPOCH)

    def test_output_inside_source_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "outside the source tree"):
            pack_archive(self.source, self.source / "output.tar.gz", EPOCH)

    def test_existing_output_is_preserved_unless_force_is_set(self) -> None:
        output = self.root / "existing.tar.gz"
        output.write_bytes(b"owner data")
        with self.assertRaises(FileExistsError):
            pack_archive(self.source, output, EPOCH)
        self.assertEqual(output.read_bytes(), b"owner data")
        pack_archive(self.source, output, EPOCH, force=True)
        self.assertNotEqual(output.read_bytes(), b"owner data")

    def test_epoch_range_is_checked(self) -> None:
        for invalid in (-1, 0x1_0000_0000):
            with self.subTest(epoch=invalid):
                with self.assertRaises(ValueError):
                    pack_archive(self.source, self.root / f"invalid-{invalid}.tar.gz", invalid)

    def test_cli_requires_epoch_when_environment_is_unset(self) -> None:
        environment = os.environ.copy()
        environment.pop("SOURCE_DATE_EPOCH", None)
        result = subprocess.run(
            [sys.executable, str(PACK_CLI), "--source", str(self.source), "--output", str(self.root / "no-epoch.tar.gz")],
            env=environment,
            capture_output=True,
            text=True,
        )
        self.assertEqual(result.returncode, 2)
        self.assertIn("provide --source-date-epoch or set SOURCE_DATE_EPOCH", result.stderr)

    def test_cli_reads_source_date_epoch_from_environment(self) -> None:
        environment = os.environ.copy()
        environment["SOURCE_DATE_EPOCH"] = str(EPOCH)
        output = self.root / "environment.tar.gz"
        result = subprocess.run(
            [sys.executable, str(PACK_CLI), "--source", str(self.source), "--output", str(output)],
            env=environment,
            capture_output=True,
            text=True,
        )
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        self.assertTrue(output.is_file())
        self.assertIn("sha256=", result.stdout)


if __name__ == "__main__":
    unittest.main()
