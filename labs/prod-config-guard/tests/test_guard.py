from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import yaml

LAB = Path(__file__).resolve().parents[1]
ROOT = LAB.parents[1]
sys.path.insert(0, str(LAB))

from guard import InputError, load_values, validate_values  # noqa: E402


class ValidateValuesTests(unittest.TestCase):
    def test_absent_fault_settings_are_safe(self) -> None:
        self.assertEqual(validate_values({"service": {"replicas": 2}}), [])

    def test_numeric_zero_values_are_safe(self) -> None:
        self.assertEqual(
            validate_values({"faults": {"errorRate": 0, "latencyMs": 0.0}}),
            [],
        )

    def test_nonzero_error_rate_is_blocked(self) -> None:
        self.assertIn("faults.errorRate", validate_values({"faults": {"errorRate": 0.02}})[0])

    def test_nonzero_latency_is_blocked(self) -> None:
        self.assertIn("faults.latencyMs", validate_values({"faults": {"latencyMs": 800}})[0])

    def test_boolean_is_not_accepted_as_a_number(self) -> None:
        self.assertIn("numeric 0", validate_values({"faults": {"latencyMs": False}})[0])

    def test_numeric_string_is_not_accepted(self) -> None:
        self.assertIn("numeric 0", validate_values({"faults": {"latencyMs": "0"}})[0])

    def test_nan_is_blocked(self) -> None:
        self.assertIn("numeric 0", validate_values({"faults": {"errorRate": float("nan")}})[0])

    def test_large_integer_is_blocked_without_conversion_error(self) -> None:
        self.assertIn("numeric 0", validate_values({"faults": {"latencyMs": 10**1000}})[0])

    def test_faults_must_be_a_mapping(self) -> None:
        self.assertIn("faults must be a mapping", validate_values({"faults": None})[0])

    def test_root_must_be_a_mapping(self) -> None:
        self.assertIn("root must be a YAML mapping", validate_values(["not", "a", "mapping"])[0])

    def test_dev_fixture_keeps_faults_and_is_not_a_prod_input(self) -> None:
        document = load_values(LAB / "envs" / "dev" / "values.yaml")
        self.assertEqual(document["faults"], {"errorRate": 0.05, "latencyMs": 800})
        self.assertEqual(len(validate_values(document)), 2)

    def test_merged_fault_injection_is_still_evaluated(self) -> None:
        document = load_values(LAB / "fixtures" / "prod-bad-merged-values.yaml")
        self.assertIn("faults.errorRate", validate_values(document)[0])

    def test_merge_overrides_are_rejected_as_ambiguous(self) -> None:
        with self.assertRaises(InputError):
            load_values(LAB / "fixtures" / "prod-ambiguous-merge.yaml")

    def test_duplicate_yaml_keys_are_rejected(self) -> None:
        with self.assertRaises(InputError):
            load_values(LAB / "fixtures" / "prod-duplicate-key.yaml")

    def test_malformed_yaml_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "bad.yaml"
            path.write_text("faults: [\n", encoding="utf-8")
            with self.assertRaises(InputError):
                load_values(path)

    def test_missing_file_is_rejected(self) -> None:
        with self.assertRaises(InputError):
            load_values(LAB / "fixtures" / "does-not-exist.yaml")

    def test_workflow_checks_all_branches_and_only_the_prod_path(self) -> None:
        workflow_path = ROOT / ".github" / "workflows" / "lab-validation.yml"
        workflow = yaml.load(workflow_path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
        events = workflow["on"]
        self.assertEqual(set(events), {"push", "pull_request", "workflow_dispatch"})
        self.assertNotIn("branches", events["push"] or {})
        self.assertNotIn("branches", events["pull_request"] or {})
        run_steps = [step.get("run", "") for step in workflow["jobs"]["production-config-guard"]["steps"]]
        self.assertIn(
            "python labs/prod-config-guard/guard.py labs/prod-config-guard/envs/prod/values.yaml",
            run_steps,
        )


class CliTests(unittest.TestCase):
    def run_guard(self, path: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(LAB / "guard.py"), str(path)],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_cli_accepts_production_values(self) -> None:
        result = self.run_guard(LAB / "envs" / "prod" / "values.yaml")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("PASS:", result.stdout)

    def test_cli_blocks_bad_production_values(self) -> None:
        result = self.run_guard(LAB / "fixtures" / "prod-bad-latency.yaml")
        self.assertEqual(result.returncode, 1)
        self.assertIn("BLOCK: faults.latencyMs", result.stderr)

    def test_cli_reports_parse_error_with_exit_code_two(self) -> None:
        result = self.run_guard(LAB / "fixtures" / "prod-duplicate-key.yaml")
        self.assertEqual(result.returncode, 2)
        self.assertIn("cannot safely read", result.stderr)


if __name__ == "__main__":
    unittest.main()
