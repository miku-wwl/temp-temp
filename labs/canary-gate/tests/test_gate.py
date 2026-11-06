from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate import evaluate


class CanaryGateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.good = {
            "baseline": {"requests": 1000, "errors": 2, "p95_ms": 180},
            "canary": {"requests": 250, "errors": 1, "p95_ms": 210},
        }

    def test_promotes_when_samples_and_metrics_pass(self) -> None:
        self.assertEqual(evaluate(self.good)["decision"], "promote")

    def test_holds_when_sample_count_is_too_low(self) -> None:
        self.good["canary"]["requests"] = 40
        self.good["canary"]["errors"] = 0
        self.assertEqual(evaluate(self.good)["decision"], "hold")

    def test_rolls_back_on_error_rate_limit(self) -> None:
        self.good["canary"].update(requests=250, errors=5)
        self.assertEqual(evaluate(self.good)["decision"], "rollback")

    def test_rolls_back_on_latency_regression(self) -> None:
        self.good["canary"]["p95_ms"] = 271
        self.assertEqual(evaluate(self.good)["decision"], "rollback")

    def test_observed_breach_rolls_back_even_with_few_samples(self) -> None:
        self.good["canary"].update(requests=20, errors=2)
        self.assertEqual(evaluate(self.good)["decision"], "rollback")

    def test_rejects_impossible_error_count(self) -> None:
        self.good["canary"]["errors"] = 251
        with self.assertRaisesRegex(ValueError, "between zero and requests"):
            evaluate(self.good)

    def test_rejects_boolean_request_count(self) -> None:
        self.good["baseline"]["requests"] = True
        with self.assertRaisesRegex(ValueError, "positive integer"):
            evaluate(self.good)

    def test_rejects_invalid_thresholds(self) -> None:
        with self.assertRaisesRegex(ValueError, "thresholds must be positive"):
            evaluate(self.good, max_latency_ratio=0.9)

    def test_rejects_non_finite_latency(self) -> None:
        self.good["canary"]["p95_ms"] = float("nan")
        with self.assertRaisesRegex(ValueError, "p95_ms must be positive"):
            evaluate(self.good)

    def test_rejects_non_finite_threshold(self) -> None:
        with self.assertRaisesRegex(ValueError, "thresholds must be positive"):
            evaluate(self.good, max_error_rate=float("nan"))


if __name__ == "__main__":
    unittest.main()
