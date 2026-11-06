#!/usr/bin/env python3
"""Build a deterministic tar.gz from regular files in a source directory."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import os
import stat
import tempfile
import tarfile
from pathlib import Path


MAX_GZIP_MTIME = 0xFFFFFFFF


def validate_epoch(epoch: int) -> int:
    if isinstance(epoch, bool) or not isinstance(epoch, int):
        raise ValueError("SOURCE_DATE_EPOCH must be an integer")
    if epoch < 0 or epoch > MAX_GZIP_MTIME:
        raise ValueError(f"SOURCE_DATE_EPOCH must be between 0 and {MAX_GZIP_MTIME}")
    return epoch


def collect_files(source: Path) -> list[tuple[Path, str, os.stat_result]]:
    files: list[tuple[Path, str, os.stat_result]] = []
    for root_text, dirnames, filenames in os.walk(source, topdown=True, followlinks=False):
        root = Path(root_text)
        dirnames.sort()
        filenames.sort()
        for dirname in dirnames:
            candidate = root / dirname
            if candidate.is_symlink() or not candidate.is_dir():
                raise ValueError(f"source contains a non-directory path: {candidate}")
        for filename in filenames:
            candidate = root / filename
            metadata = candidate.lstat()
            if not stat.S_ISREG(metadata.st_mode):
                raise ValueError(f"source contains a non-regular file: {candidate}")
            relative_name = candidate.relative_to(source).as_posix()
            files.append((candidate, relative_name, metadata))
    files.sort(key=lambda item: item[1])
    return files


def _identity(metadata: os.stat_result) -> tuple[int, int, int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_size,
        metadata.st_mtime_ns,
        stat.S_IFMT(metadata.st_mode),
        stat.S_IMODE(metadata.st_mode),
    )


def archive_mode(source_mode: int) -> int:
    """Map source execute bits to a stable archive mode."""
    return 0o755 if source_mode & 0o111 else 0o644


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def pack_archive(source: Path, output: Path, epoch: int, force: bool = False) -> str:
    epoch = validate_epoch(epoch)
    source = source.resolve(strict=True)
    if not source.is_dir():
        raise ValueError(f"source must be a directory: {source}")

    output = output.resolve(strict=False)
    try:
        output.relative_to(source)
    except ValueError:
        pass
    else:
        raise ValueError("output must be outside the source tree")
    if output.exists() and not force:
        raise FileExistsError(f"output already exists (use --force to replace): {output}")
    if output.exists() and output.is_dir():
        raise IsADirectoryError(f"output is a directory: {output}")

    members = collect_files(source)
    output.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{output.name}.", suffix=".tmp", dir=output.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(fd, "wb") as raw_output:
            with gzip.GzipFile(
                filename="",
                fileobj=raw_output,
                mode="wb",
                compresslevel=9,
                mtime=epoch,
            ) as compressed:
                with tarfile.open(
                    fileobj=compressed,
                    mode="w",
                    format=tarfile.PAX_FORMAT,
                ) as archive:
                    for path, relative_name, original_metadata in members:
                        info = tarfile.TarInfo(relative_name)
                        info.type = tarfile.REGTYPE
                        info.size = original_metadata.st_size
                        info.mtime = epoch
                        info.uid = 0
                        info.gid = 0
                        info.uname = ""
                        info.gname = ""
                        info.mode = archive_mode(original_metadata.st_mode)
                        with path.open("rb") as file_stream:
                            opened_metadata = os.fstat(file_stream.fileno())
                            if _identity(opened_metadata) != _identity(original_metadata):
                                raise RuntimeError(f"source changed before reading: {path}")
                            archive.addfile(info, file_stream)
                        current_metadata = path.stat()
                        if _identity(current_metadata) != _identity(original_metadata):
                            raise RuntimeError(f"source changed while packaging: {path}")

        if force:
            os.replace(temporary_path, output)
        else:
            # A same-directory hard link publishes the finished file without
            # replacing a destination that appeared after the initial check.
            os.link(temporary_path, output)
            temporary_path.unlink()
        return sha256_file(output)
    finally:
        try:
            temporary_path.unlink()
        except FileNotFoundError:
            pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="directory tree to archive")
    parser.add_argument("--output", required=True, type=Path, help="destination .tar.gz file")
    parser.add_argument(
        "--source-date-epoch",
        type=int,
        help="fixed Unix timestamp; defaults to the SOURCE_DATE_EPOCH environment variable",
    )
    parser.add_argument("--force", action="store_true", help="replace an existing output")
    args = parser.parse_args()

    epoch = args.source_date_epoch
    if epoch is None:
        raw_epoch = os.environ.get("SOURCE_DATE_EPOCH")
        if raw_epoch is None:
            parser.error("provide --source-date-epoch or set SOURCE_DATE_EPOCH")
        try:
            if not raw_epoch.strip().isdigit():
                raise ValueError
            epoch = int(raw_epoch, 10)
        except ValueError:
            parser.error("SOURCE_DATE_EPOCH must be a non-negative integer")
    try:
        args.source_date_epoch = validate_epoch(epoch)
    except ValueError as error:
        parser.error(str(error))
    return args


def main() -> int:
    args = parse_args()
    try:
        digest = pack_archive(
            args.source,
            args.output,
            args.source_date_epoch,
            force=args.force,
        )
    except (OSError, RuntimeError, ValueError) as error:
        print(f"ERROR: {error}")
        return 2
    print(f"PASS: {args.output} sha256={digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
