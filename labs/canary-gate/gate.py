"""Evaluate a canary metrics snapshot and return promote, hold, or rollback."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any


def _metrics(document: dict[str, Any], name: str) -> tuple[int, int, float]:
    value = document.get(name)
    if not isinstance(value, dict):
        raise ValueError(f"{name} must be an object")
    requests = value.get("requests")
    errors = value.get("errors")
    p95_ms = value.get("p95_ms")
    if isinstance(requests, bool) or not isinstance(requests, int) or requests <= 0:
        raise ValueError(f"{name}.requests must be a positive integer")
    if isinstance(errors, bool) or not isinstance(errors, int) or errors < 0 or errors > requests:
        raise ValueError(f"{name}.errors must be an integer between zero and requests")
    if (
        isinstance(p95_ms, bool)
        or not isinstance(p95_ms, (int, float))
        or not math.isfinite(p95_ms)
        or p95_ms <= 0
    ):
        raise ValueError(f"{name}.p95_ms must be positive")
    return requests, errors, float(p95_ms)


def evaluate(
    document: dict[str, Any],
    *,
    min_requests: int = 100,
    max_error_rate: float = 0.01,
    max_error_delta: float = 0.005,
    max_p95_ms: float = 500.0,
    max_latency_ratio: float = 1.5,
) -> dict[str, Any]:
    thresholds = (max_error_rate, max_error_delta, max_p95_ms, max_latency_ratio)
    if (
        min_requests <= 0
        or not all(math.isfinite(value) for value in thresholds)
        or not 0 < max_error_rate <= 1
        or not 0 <= max_error_delta <= 1
        or max_p95_ms <= 0
        or max_latency_ratio < 1
    ):
        raise ValueError("thresholds must be positive; deltas cannot be negative and latency ratio must be >= 1")
    base_requests, base_errors, base_p95 = _metrics(document, "baseline")
    canary_requests, canary_errors, canary_p95 = _metrics(document, "canary")
    base_rate = base_errors / base_requests
    canary_rate = canary_errors / canary_requests
    error_limit = min(max_error_rate, base_rate + max_error_delta)
    latency_limit = min(max_p95_ms, base_p95 * max_latency_ratio)

    reasons: list[str] = []
    if canary_rate > error_limit:
        reasons.append(f"canary error rate {canary_rate:.4%} exceeds {error_limit:.4%}")
    if canary_p95 > latency_limit:
        reasons.append(f"canary p95 {canary_p95:g} ms exceeds {latency_limit:g} ms")
    if reasons:
        decision = "rollback"
    elif min(base_requests, canary_requests) < min_requests:
        decision = "hold"
        reasons.append(f"need at least {min_requests} requests in baseline and canary")
    else:
        decision = "promote"
        reasons.append("error-rate and p95 limits pass with sufficient samples")

    return {
        "decision": decision,
        "baseline": {"requests": base_requests, "error_rate": base_rate, "p95_ms": base_p95},
        "canary": {"requests": canary_requests, "error_rate": canary_rate, "p95_ms": canary_p95},
        "limits": {"error_rate": error_limit, "p95_ms": latency_limit, "min_requests": min_requests},
        "reasons": reasons,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("metrics", type=Path)
    parser.add_argument("--min-requests", type=int, default=100)
    parser.add_argument("--max-error-rate", type=float, default=0.01)
    parser.add_argument("--max-error-delta", type=float, default=0.005)
    parser.add_argument("--max-p95-ms", type=float, default=500.0)
    parser.add_argument("--max-latency-ratio", type=float, default=1.5)
    args = parser.parse_args(argv)
    try:
        result = evaluate(
            json.loads(args.metrics.read_text(encoding="utf-8")),
            min_requests=args.min_requests,
            max_error_rate=args.max_error_rate,
            max_error_delta=args.max_error_delta,
            max_p95_ms=args.max_p95_ms,
            max_latency_ratio=args.max_latency_ratio,
        )
    except (OSError, json.JSONDecodeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
