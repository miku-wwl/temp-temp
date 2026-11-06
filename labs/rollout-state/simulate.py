"""Simulate a maxUnavailable rolling replacement using batch readiness counts."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


def _integer(value: Any, name: str, minimum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise ValueError(f"{name} must be an integer >= {minimum}")
    return value


def simulate(document: dict[str, Any]) -> dict[str, Any]:
    desired = _integer(document.get("desired"), "desired", 1)
    max_unavailable = _integer(document.get("max_unavailable"), "max_unavailable", 1)
    if max_unavailable > desired:
        raise ValueError("max_unavailable cannot exceed desired replicas")
    ready_counts = document.get("batch_ready_counts")
    if not isinstance(ready_counts, list):
        raise ValueError("batch_ready_counts must be a list")

    updated = 0
    min_available = desired
    events: list[dict[str, int]] = []
    for batch_number, ready_value in enumerate(ready_counts, start=1):
        remove = min(max_unavailable, desired - updated)
        ready = _integer(ready_value, f"batch_ready_counts[{batch_number - 1}]", 0)
        if ready > remove:
            raise ValueError(f"batch {batch_number} cannot report more ready pods than removed replicas")
        available_after_removal = desired - remove
        available_after_readiness = available_after_removal + ready
        min_available = min(min_available, available_after_removal, available_after_readiness)
        events.append(
            {
                "batch": batch_number,
                "old_removed": remove,
                "new_ready": ready,
                "available_after_removal": available_after_removal,
                "available_after_readiness": available_after_readiness,
            }
        )
        if ready < remove:
            return {
                "state": "rolled_back",
                "desired": desired,
                "max_unavailable": max_unavailable,
                "available_replicas": desired,
                "updated_replicas": 0,
                "min_available_seen": min_available,
                "events": events,
                "reason": f"batch {batch_number} did not reach readiness; old replicas restored",
            }
        updated += ready
        if updated == desired:
            return {
                "state": "complete",
                "desired": desired,
                "max_unavailable": max_unavailable,
                "available_replicas": desired,
                "updated_replicas": updated,
                "min_available_seen": min_available,
                "events": events,
                "reason": "all replicas reached readiness",
            }

    return {
        "state": "progressing",
        "desired": desired,
        "max_unavailable": max_unavailable,
        "available_replicas": desired,
        "updated_replicas": updated,
        "min_available_seen": min_available,
        "events": events,
        "reason": "readiness batches ended before all replicas were updated",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scenario", type=Path, help="JSON file with desired replicas and readiness batches")
    args = parser.parse_args(argv)
    try:
        result = simulate(json.loads(args.scenario.read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
