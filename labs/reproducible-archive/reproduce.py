#!/usr/bin/env python3
"""Run the baseline and normalized archive comparison on the checked-in fixture."""

from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


LAB = Path(__file__).resolve().parent
FIXTURE = LAB / "sample" / "source"
EPOCH = 1_700_000_000


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command: list[str]) -> None:
    print("$ " + subprocess.list2cmdline(command))
    subprocess.run(command, check=True)


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="reproducible-archive-") as temporary:
        root = Path(temporary)
        source = root / "source"
        shutil.copytree(FIXTURE, source)
        app = source / "app.py"
        readme = source / "README.md"
        for index, path in enumerate((app, readme)):
            os.utime(path, (EPOCH + index * 10, EPOCH + index * 10))

        naive_one = root / "naive-one.tar.gz"
        naive_two = root / "naive-two.tar.gz"
        stable_one = root / "stable-one.tar.gz"
        stable_two = root / "stable-two.tar.gz"
        changed = root / "changed-content.tar.gz"

        run([sys.executable, str(LAB / "naive_pack.py"), "--source", str(source), "--output", str(naive_one)])
        os.utime(app, (EPOCH + 120, EPOCH + 120))
        run([sys.executable, str(LAB / "naive_pack.py"), "--source", str(source), "--output", str(naive_two)])
        naive_hash_one, naive_hash_two = digest(naive_one), digest(naive_two)

        run([sys.executable, str(LAB / "pack.py"), "--source", str(source), "--output", str(stable_one), "--source-date-epoch", str(EPOCH)])
        os.utime(app, (EPOCH + 240, EPOCH + 240))
        run([sys.executable, str(LAB / "pack.py"), "--source", str(source), "--output", str(stable_two), "--source-date-epoch", str(EPOCH)])
        stable_hash_one, stable_hash_two = digest(stable_one), digest(stable_two)

        app.write_text(app.read_text(encoding="utf-8") + "# content change\n", encoding="utf-8")
        run([sys.executable, str(LAB / "pack.py"), "--source", str(source), "--output", str(changed), "--source-date-epoch", str(EPOCH)])
        changed_hash = digest(changed)

        print(f"naive.sha256.1={naive_hash_one}")
        print(f"naive.sha256.2={naive_hash_two}")
        print(f"naive.equal={naive_hash_one == naive_hash_two}")
        print(f"normalized.sha256.1={stable_hash_one}")
        print(f"normalized.sha256.2={stable_hash_two}")
        print(f"normalized.equal={stable_hash_one == stable_hash_two}")
        print(f"content-change.sha256={changed_hash}")
        print(f"content-change.differs={changed_hash != stable_hash_two}")

        if naive_hash_one == naive_hash_two:
            raise SystemExit("FAIL: metadata-sensitive baseline unexpectedly matched")
        if stable_hash_one != stable_hash_two:
            raise SystemExit("FAIL: normalized archives did not match")
        if changed_hash == stable_hash_two:
            raise SystemExit("FAIL: changing content did not change the digest")
        print("PASS: baseline drift, normalized repeatability, and content sensitivity reproduced.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
