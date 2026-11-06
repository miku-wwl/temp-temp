from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from simulate import simulate


class RolloutStateTests(unittest.TestCase):
    def test_completes_when_every_batch_is_ready(self) -> None:
        result = simulate({"desired": 4, "max_unavailable": 1, "batch_ready_counts": [1, 1, 1, 1]})
        self.assertEqual(result["state"], "complete")
        self.assertEqual(result["updated_replicas"], 4)
        self.assertEqual(result["min_available_seen"], 3)

    def test_rolls_back_and_restores_replicas_after_readiness_failure(self) -> None:
        result = simulate({"desired": 4, "max_unavailable": 2, "batch_ready_counts": [2, 1]})
        self.assertEqual(result["state"], "rolled_back")
        self.assertEqual(result["available_replicas"], 4)
        self.assertEqual(result["updated_replicas"], 0)
        self.assertEqual(result["min_available_seen"], 2)

    def test_reports_progressing_when_more_batches_are_needed(self) -> None:
        result = simulate({"desired": 4, "max_unavailable": 2, "batch_ready_counts": [2]})
        self.assertEqual(result["state"], "progressing")
        self.assertEqual(result["updated_replicas"], 2)

    def test_zero_ready_pods_triggers_rollback(self) -> None:
        result = simulate({"desired": 3, "max_unavailable": 1, "batch_ready_counts": [0]})
        self.assertEqual(result["state"], "rolled_back")

    def test_unavailable_bound_is_respected(self) -> None:
        result = simulate({"desired": 6, "max_unavailable": 2, "batch_ready_counts": [2, 2, 2]})
        self.assertGreaterEqual(result["min_available_seen"], 6 - result["max_unavailable"])

    def test_rejects_ready_count_greater_than_batch(self) -> None:
        with self.assertRaisesRegex(ValueError, "more ready pods"):
            simulate({"desired": 3, "max_unavailable": 1, "batch_ready_counts": [2]})

    def test_rejects_boolean_counts(self) -> None:
        with self.assertRaisesRegex(ValueError, "desired must be an integer"):
            simulate({"desired": True, "max_unavailable": 1, "batch_ready_counts": []})

    def test_rejects_invalid_unavailable_limit(self) -> None:
        for value in (0, 4):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "max_unavailable"):
                simulate({"desired": 3, "max_unavailable": value, "batch_ready_counts": []})


if __name__ == "__main__":
    unittest.main()
