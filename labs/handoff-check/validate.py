"""Validate an on-call handoff bundle and hash-linked evidence files."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from datetime import datetime
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any


SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


def _required_text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be a non-empty string")
    return value.strip()


def _linked_file(root: Path, value: Any, field: str) -> Path:
    raw = _required_text(value, field)
    normalized = raw.replace("\\", "/")
    relative = PurePosixPath(normalized)
    if relative.is_absolute() or PureWindowsPath(raw).is_absolute() or PureWindowsPath(raw).drive:
        raise ValueError(f"{field} must be a relative path inside the bundle root")
    if any(part in ("..", "") for part in relative.parts):
        raise ValueError(f"{field} must not traverse outside the bundle root")
    candidate = root
    for part in relative.parts:
        candidate = candidate / part
        if candidate.is_symlink():
            raise ValueError(f"{field} must not use symlinks")
    try:
        resolved = candidate.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"{field} does not exist: {raw}") from error
    if not resolved.is_relative_to(root) or not resolved.is_file():
        raise ValueError(f"{field} must resolve to a file inside the bundle root")
    return resolved


def validate_bundle(bundle_path: Path, root: Path) -> dict[str, Any]:
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise ValueError("bundle root must be a directory")
    bundle = _linked_file(root, str(bundle_path), "bundle")
    try:
        document = json.loads(bundle.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise ValueError(f"bundle JSON is invalid: {error}") from error
    if not isinstance(document, dict):
        raise ValueError("bundle must be a JSON object")

    incident_id = _required_text(document.get("incident_id"), "incident_id")
    service = _required_text(document.get("service"), "service")
    owner = _required_text(document.get("owner"), "owner")
    opened_at = _required_text(document.get("opened_at"), "opened_at")
    try:
        timestamp = datetime.fromisoformat(opened_at.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError("opened_at must be an ISO-8601 timestamp") from error
    if timestamp.tzinfo is None:
        raise ValueError("opened_at must include a timezone")

    alerts = document.get("alerts")
    if not isinstance(alerts, list) or not alerts:
        raise ValueError("alerts must be a non-empty list")
    seen: set[str] = set()
    checked: list[dict[str, str]] = []
    for index, alert in enumerate(alerts):
        if not isinstance(alert, dict):
            raise ValueError(f"alerts[{index}] must be an object")
        name = _required_text(alert.get("name"), f"alerts[{index}].name")
        if name in seen:
            raise ValueError(f"duplicate alert name: {name}")
        seen.add(name)
        runbook = _linked_file(root, alert.get("runbook"), f"alerts[{index}].runbook")
        evidence = _linked_file(root, alert.get("evidence"), f"alerts[{index}].evidence")
        expected = alert.get("sha256")
        if not isinstance(expected, str) or not SHA256_RE.fullmatch(expected):
            raise ValueError(f"alerts[{index}].sha256 must be a lowercase SHA-256 hex digest")
        actual = hashlib.sha256(evidence.read_bytes()).hexdigest()
        if actual != expected:
            raise ValueError(f"evidence hash mismatch for alert {name}")
        checked.append({"name": name, "runbook": runbook.relative_to(root).as_posix(), "evidence": evidence.relative_to(root).as_posix(), "sha256": actual})

    return {
        "status": "PASS",
        "incident_id": incident_id,
        "service": service,
        "owner": owner,
        "opened_at": opened_at,
        "alerts_checked": len(checked),
        "references": checked,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True, help="bundle file path relative to --root")
    parser.add_argument("--root", type=Path, required=True, help="trusted local bundle root")
    args = parser.parse_args(argv)
    try:
        result = validate_bundle(args.bundle, args.root)
    except (OSError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
