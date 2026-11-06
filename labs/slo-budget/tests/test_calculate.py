from __future__ import annotations

import math
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from calculate import calculate


class SloBudgetTests(unittest.TestCase):
    def test_excludes_client_errors_from_availability_sli(self) -> None:
        result = calculate({"status_counts": {"200": 9990, "500": 10, "404": 500}})
        self.assertEqual(result["total_requests"], 10_500)
        self.assertEqual(result["sli_requests"], 10_000)
        self.assertEqual(result["excluded_requests"], 500)
        self.assertAlmostEqual(result["availability"], 0.999)
        self.assertEqual(result["error_budget_remaining_percent"], 0.0)

    def test_reports_half_of_error_budget_consumed(self) -> None:
        result = calculate({"status_counts": {"200": 9995, "500": 5}})
        self.assertTrue(result["slo_met"])
        self.assertAlmostEqual(result["error_budget_burn_percent"], 50.0)
        self.assertAlmostEqual(result["error_budget_remaining_percent"], 50.0)

    def test_reports_exhausted_budget_below_target(self) -> None:
        result = calculate({"status_counts": {"200": 9985, "500": 15}})
        self.assertFalse(result["slo_met"])
        self.assertAlmostEqual(result["error_budget_burn_percent"], 150.0)
        self.assertAlmostEqual(result["error_budget_remaining_percent"], -50.0)

    def test_accepts_three_xx_as_success(self) -> None:
        result = calculate({"status_counts": {"302": 999, "500": 1}}, target=0.999)
        self.assertTrue(result["slo_met"])

    def test_rejects_boolean_and_negative_counts(self) -> None:
        for count in (True, -1, 1.5):
            with self.subTest(count=count), self.assertRaisesRegex(ValueError, "non-negative integer"):
                calculate({"status_counts": {"500": count}})

    def test_rejects_invalid_status_codes(self) -> None:
        for status in ("99", "600", "0200", "bad"):
            with self.subTest(status=status), self.assertRaisesRegex(ValueError, "invalid HTTP status"):
                calculate({"status_counts": {status: 1}})

    def test_rejects_no_availability_eligible_requests(self) -> None:
        with self.assertRaisesRegex(ValueError, "no 2xx/3xx or 5xx"):
            calculate({"status_counts": {"404": 5}})

    def test_rejects_non_finite_or_out_of_range_target(self) -> None:
        for target in (math.nan, 0, 1, 1.1):
            with self.subTest(target=target), self.assertRaisesRegex(ValueError, "finite fraction"):
                calculate({"status_counts": {"200": 1}}, target=target)


if __name__ == "__main__":
    unittest.main()
