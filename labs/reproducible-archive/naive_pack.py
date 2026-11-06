#!/usr/bin/env python3
"""Intentionally naive baseline: preserve host metadata and current gzip time."""

from __future__ import annotations

import argparse
import tarfile
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not args.source.is_dir():
        parser.error(f"source must be a directory: {args.source}")
    if args.output.exists():
        parser.error(f"output already exists: {args.output}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tarfile.open(args.output, mode="w:gz") as archive:
        archive.add(args.source, arcname="source")
    print(f"BASELINE: wrote {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
