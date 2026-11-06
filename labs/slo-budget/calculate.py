"""Calculate availability SLI and error-budget burn from HTTP status counts."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any


def calculate(document: dict[str, Any], target: float = 0.999) -> dict[str, Any]:
    if isinstance(target, bool) or not math.isfinite(target) or not 0 < target < 1:
        raise ValueError("SLO target must be a finite fraction between zero and one")
    counts = document.get("status_counts")
    if not isinstance(counts, dict) or not counts:
        raise ValueError("status_counts must be a non-empty object")

    success = errors = total = excluded = 0
    for code_text, count in counts.items():
        try:
            code = int(code_text)
        except (TypeError, ValueError) as error:
            raise ValueError(f"invalid HTTP status code: {code_text!r}") from error
        if str(code) != str(code_text) or not 100 <= code <= 599:
            raise ValueError(f"invalid HTTP status code: {code_text!r}")
        if isinstance(count, bool) or not isinstance(count, int) or count < 0:
            raise ValueError(f"count for HTTP {code} must be a non-negative integer")
        total += count
        if 200 <= code <= 399:
            success += count
        elif 500 <= code <= 599:
            errors += count
        else:
            excluded += count

    sli_requests = success + errors
    if sli_requests == 0:
        raise ValueError("no 2xx/3xx or 5xx requests are available for the availability SLI")
    availability = success / sli_requests
    allowed_error_count = sli_requests * (1 - target)
    burn = errors / allowed_error_count
    return {
        "slo_target": target,
        "slo_met": availability >= target,
        "total_requests": total,
        "sli_requests": sli_requests,
        "excluded_requests": excluded,
        "successful_requests": success,
        "error_requests": errors,
        "availability": availability,
        "allowed_error_count": round(allowed_error_count, 10),
        "error_budget_burn_percent": round(burn * 100, 10),
        "error_budget_remaining_percent": round((1 - burn) * 100, 10),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("counts", type=Path, help="JSON file with a status_counts object")
    parser.add_argument("--target", type=float, default=0.999, help="availability target as a fraction (default: 0.999)")
    args = parser.parse_args(argv)
    try:
        result = calculate(json.loads(args.counts.read_text(encoding="utf-8")), args.target)
    except (OSError, json.JSONDecodeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
