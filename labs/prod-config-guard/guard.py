#!/usr/bin/env python3
"""Fail-closed policy check for production Helm values."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any

import yaml
from yaml.constructor import ConstructorError


class UniqueKeyLoader(yaml.SafeLoader):
    """SafeLoader variant that rejects duplicate keys instead of silently taking the last value."""


def _construct_unique_mapping(loader: UniqueKeyLoader, node: yaml.MappingNode, deep: bool = False) -> dict:
    # Expand YAML merge keys first. If a merge and an explicit key collide, reject the ambiguity.
    loader.flatten_mapping(node)
    seen: set[Any] = set()
    for key_node, _ in node.value:
        key = loader.construct_object(key_node, deep=deep)
        try:
            hash(key)
        except TypeError as exc:
            raise ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                "found an unhashable mapping key",
                key_node.start_mark,
            ) from exc
        if key in seen:
            raise ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                f"found duplicate key {key!r}",
                key_node.start_mark,
            )
        seen.add(key)
    return yaml.SafeLoader.construct_mapping(loader, node, deep=deep)


UniqueKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG,
    _construct_unique_mapping,
)


class InputError(Exception):
    """Input file could not be read or parsed safely."""


def load_values(path: Path) -> Any:
    try:
        content = path.read_text(encoding="utf-8-sig")
        return yaml.load(content, Loader=UniqueKeyLoader)
    except (OSError, UnicodeError, yaml.YAMLError) as exc:
        raise InputError(str(exc)) from exc


def validate_values(document: Any) -> list[str]:
    """Return violations; an absent fault key is equivalent to the safe default."""
    if not isinstance(document, dict):
        return ["document root must be a YAML mapping"]

    faults = document.get("faults", {})
    if not isinstance(faults, dict):
        return ["faults must be a mapping when present"]

    violations: list[str] = []
    for key in ("errorRate", "latencyMs"):
        if key not in faults:
            continue
        value = faults[key]
        path = f"faults.{key}"
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            violations.append(f"{path} must be numeric 0 or be omitted (got {value!r})")
        elif value != 0:
            violations.append(f"{path} must be numeric 0 or be omitted (got {value!r})")
    return violations


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Reject unsafe or ambiguous fault-injection settings in production values."
    )
    parser.add_argument("values_file", type=Path, help="production YAML values file to validate")
    args = parser.parse_args(argv)

    try:
        document = load_values(args.values_file)
    except InputError as exc:
        print(f"ERROR: cannot safely read {args.values_file}: {exc}", file=sys.stderr)
        return 2

    violations = validate_values(document)
    if violations:
        for violation in violations:
            print(f"BLOCK: {violation}", file=sys.stderr)
        return 1

    print("PASS: production fault injection is disabled or absent.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
