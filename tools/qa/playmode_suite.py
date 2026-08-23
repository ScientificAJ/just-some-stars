#!/usr/bin/env python3
"""Run every project PlayMode fixture in its own Unity process.

Unity Test Framework 1.6 can discover the complete project suite while writing
an invalid zero-test aggregate. Single-fixture processes are reliable, so this
runner validates a committed fixture manifest against the source tree, runs
each fixture independently, validates each NUnit report, and writes one
fail-closed JSON summary.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Sequence


DEFAULT_ASSEMBLY = "JustSomeStars.PlayModeTests"
DEFAULT_SOURCE_DIRECTORY = Path("Assets/_JustSomeStars/Tests/PlayMode")
DEFAULT_MANIFEST = Path("tools/qa/playmode-fixtures.txt")
DEFAULT_OUTPUT_DIRECTORY = Path("Builds/TestResults/playmode-suite")
DEFAULT_LOG_DIRECTORY = Path("Builds/Logs/playmode-suite")
FIXTURE_NAME = re.compile(r"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+$")
TEST_ATTRIBUTE = re.compile(r"\[(?:Test|TestCase|UnityTest)\b")
NAMESPACE = re.compile(r"\bnamespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)")


class FixtureManifestError(RuntimeError):
    """The committed fixture manifest and source tree disagree."""


class ReportValidationError(RuntimeError):
    """A Unity NUnit result is missing, malformed, empty, or unsuccessful."""


class SuiteExecutionError(RuntimeError):
    """A fixture process could not complete successfully."""


@dataclass(frozen=True)
class FixtureResult:
    fixture: str
    result: str
    total: int
    passed: int
    durationSeconds: float
    report: str
    log: str
    unityExitCode: int


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _required_nonnegative_count(element: ET.Element, name: str) -> int:
    raw = element.attrib.get(name)
    if raw is None:
        raise ReportValidationError(f"Unity test report has no {name!r} count")
    try:
        value = int(raw)
    except ValueError as error:
        raise ReportValidationError(
            f"Unity test report has invalid {name!r} count: {raw!r}"
        ) from error
    if value < 0:
        raise ReportValidationError(
            f"Unity test report has negative {name!r} count: {value}"
        )
    return value


def discover_test_fixtures(source_directory: Path) -> tuple[str, ...]:
    """Discover convention-bound top-level PlayMode fixtures from source."""
    source_directory = source_directory.resolve()
    if not source_directory.is_dir():
        raise FixtureManifestError(
            f"PlayMode source directory does not exist: {source_directory}"
        )

    fixtures: list[str] = []
    for source_path in sorted(source_directory.rglob("*.cs")):
        text = source_path.read_text(encoding="utf-8")
        if not TEST_ATTRIBUTE.search(text):
            continue
        if not source_path.name.endswith("Tests.cs"):
            raise FixtureManifestError(
                "PlayMode source contains tests outside the required *Tests.cs "
                f"fixture convention: {source_path}"
            )
        namespace_matches = NAMESPACE.findall(text)
        if len(set(namespace_matches)) != 1:
            raise FixtureManifestError(
                f"expected exactly one namespace in PlayMode fixture: {source_path}"
            )
        class_name = source_path.stem
        class_pattern = re.compile(
            rf"\bpublic\s+sealed\s+class\s+{re.escape(class_name)}\b"
        )
        if len(class_pattern.findall(text)) != 1:
            raise FixtureManifestError(
                "PlayMode fixture must contain exactly one matching public sealed "
                f"class {class_name}: {source_path}"
            )
        fixtures.append(f"{namespace_matches[0]}.{class_name}")

    if not fixtures:
        raise FixtureManifestError(
            f"no PlayMode test fixtures were discovered in {source_directory}"
        )
    if len(fixtures) != len(set(fixtures)):
        raise FixtureManifestError("duplicate PlayMode fixture names were discovered")
    return tuple(sorted(fixtures))


def load_fixture_manifest(manifest_path: Path) -> tuple[str, ...]:
    manifest_path = manifest_path.resolve()
    if not manifest_path.is_file():
        raise FixtureManifestError(f"fixture manifest does not exist: {manifest_path}")
    fixtures = tuple(
        line.strip()
        for line in manifest_path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    )
    if not fixtures:
        raise FixtureManifestError(f"fixture manifest is empty: {manifest_path}")
    invalid = [fixture for fixture in fixtures if not FIXTURE_NAME.fullmatch(fixture)]
    if invalid:
        raise FixtureManifestError(
            f"fixture manifest contains invalid fully qualified names: {invalid}"
        )
    if len(fixtures) != len(set(fixtures)):
        raise FixtureManifestError("fixture manifest contains duplicate entries")
    if fixtures != tuple(sorted(fixtures)):
        raise FixtureManifestError("fixture manifest entries must be sorted")
    return fixtures


def validate_fixture_manifest(
    declared: Sequence[str], discovered: Sequence[str]
) -> None:
    declared_set = set(declared)
    discovered_set = set(discovered)
    missing = sorted(discovered_set - declared_set)
    stale = sorted(declared_set - discovered_set)
    if missing or stale:
        details: list[str] = []
        if missing:
            details.append(f"missing from manifest: {missing}")
        if stale:
            details.append(f"declared but not discovered: {stale}")
        raise FixtureManifestError("; ".join(details))
    if tuple(declared) != tuple(discovered):
        raise FixtureManifestError(
            "fixture manifest and discovery order differ; both must be sorted"
        )


def parse_unity_report(report_path: Path, expected_fixture: str) -> FixtureResult:
    report_path = report_path.resolve()
    if not report_path.is_file() or report_path.stat().st_size == 0:
        raise ReportValidationError(f"missing or empty Unity report: {report_path}")
    try:
        root = ET.parse(report_path).getroot()
    except ET.ParseError as error:
        raise ReportValidationError(
            f"malformed Unity report {report_path}: {error}"
        ) from error
    if _local_name(root.tag) != "test-run":
        raise ReportValidationError(
            f"unexpected Unity report root {_local_name(root.tag)!r}: {report_path}"
        )

    testcasecount = _required_nonnegative_count(root, "testcasecount")
    total = _required_nonnegative_count(root, "total")
    passed = _required_nonnegative_count(root, "passed")
    failed = _required_nonnegative_count(root, "failed")
    skipped = _required_nonnegative_count(root, "skipped")
    inconclusive = _required_nonnegative_count(root, "inconclusive")
    if total == 0:
        raise ReportValidationError(
            f"Unity report selected zero tests for {expected_fixture}"
        )
    if root.attrib.get("result") != "Passed":
        raise ReportValidationError(
            f"Unity report result was {root.attrib.get('result')!r}, not 'Passed'"
        )
    if (testcasecount, passed) != (total, total):
        raise ReportValidationError(
            "Unity selected-test counts disagree: "
            f"testcasecount={testcasecount}, total={total}, passed={passed}"
        )
    if (failed, skipped, inconclusive) != (0, 0, 0):
        raise ReportValidationError(
            "Unity report is not clean: "
            f"failed={failed}, skipped={skipped}, inconclusive={inconclusive}"
        )

    fixture_suites = [
        element
        for element in root.iter()
        if _local_name(element.tag) == "test-suite"
        and element.attrib.get("type") == "TestFixture"
    ]
    matching_suites = [
        element
        for element in fixture_suites
        if element.attrib.get("fullname") == expected_fixture
    ]
    if len(matching_suites) != 1 or len(fixture_suites) != 1:
        actual = sorted(
            element.attrib.get("fullname", "<missing>") for element in fixture_suites
        )
        raise ReportValidationError(
            f"expected only fixture {expected_fixture!r}, found {actual}"
        )
    fixture_suite = matching_suites[0]
    fixture_total = _required_nonnegative_count(fixture_suite, "total")
    fixture_passed = _required_nonnegative_count(fixture_suite, "passed")
    if fixture_suite.attrib.get("result") != "Passed":
        raise ReportValidationError(
            f"fixture {expected_fixture} result was not Passed"
        )
    if (fixture_total, fixture_passed) != (total, total):
        raise ReportValidationError(
            f"fixture/root count mismatch for {expected_fixture}: "
            f"fixture={fixture_total}/{fixture_passed}, root={total}/{passed}"
        )
    case_elements = [
        element for element in root.iter() if _local_name(element.tag) == "test-case"
    ]
    if len(case_elements) != total:
        raise ReportValidationError(
            f"expected {total} concrete test cases, found {len(case_elements)}"
        )
    wrong_cases = [
        element.attrib.get("fullname", "<missing>")
        for element in case_elements
        if not element.attrib.get("fullname", "").startswith(expected_fixture + ".")
        or element.attrib.get("result") != "Passed"
    ]
    if wrong_cases:
        raise ReportValidationError(
            f"report contains unexpected or unsuccessful cases: {wrong_cases}"
        )
    try:
        duration = float(root.attrib.get("duration", "0"))
    except ValueError as error:
        raise ReportValidationError("Unity report duration is invalid") from error
    return FixtureResult(
        fixture=expected_fixture,
        result="Passed",
        total=total,
        passed=passed,
        durationSeconds=duration,
        report=str(report_path),
        log="",
        unityExitCode=0,
    )


def _artifact_stem(fixture: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", fixture).replace(".", "_")


def _write_summary(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.replace(temporary, path)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-editor", required=True, type=Path)
    parser.add_argument("--project-path", default=Path.cwd(), type=Path)
    parser.add_argument("--source-directory", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--output-directory", type=Path)
    parser.add_argument("--log-directory", type=Path)
    parser.add_argument("--assembly-name", default=DEFAULT_ASSEMBLY)
    parser.add_argument("--timeout-seconds", type=int, default=1200)
    parser.add_argument(
        "--keep-going",
        action="store_true",
        help="run remaining fixtures after a failure; default is fail-fast",
    )
    return parser


def _resolved_path(project: Path, value: Path | None, default: Path) -> Path:
    candidate = default if value is None else value
    return candidate.resolve() if candidate.is_absolute() else (project / candidate).resolve()


def run_from_args(arguments: argparse.Namespace) -> int:
    project = arguments.project_path.resolve()
    unity_editor = arguments.unity_editor.resolve()
    if not project.is_dir():
        raise SuiteExecutionError(f"project path does not exist: {project}")
    source = _resolved_path(project, arguments.source_directory, DEFAULT_SOURCE_DIRECTORY)
    manifest = _resolved_path(project, arguments.manifest, DEFAULT_MANIFEST)
    output = _resolved_path(project, arguments.output_directory, DEFAULT_OUTPUT_DIRECTORY)
    logs = _resolved_path(project, arguments.log_directory, DEFAULT_LOG_DIRECTORY)
    output.mkdir(parents=True, exist_ok=True)
    summary_path = output / "summary.json"
    _write_summary(
        summary_path,
        {
            "schemaVersion": 1,
            "result": "Running",
            "phase": "preflight",
            "fixtureCount": 0,
            "completedFixtureCount": 0,
            "total": 0,
            "passed": 0,
            "failedFixtureCount": 0,
            "fixtures": [],
            "failures": [],
        },
    )
    try:
        if not unity_editor.is_file() or not os.access(unity_editor, os.X_OK):
            raise SuiteExecutionError(f"Unity editor is not executable: {unity_editor}")
        if arguments.timeout_seconds <= 0:
            raise SuiteExecutionError("timeout must be a positive number of seconds")
        declared = load_fixture_manifest(manifest)
        discovered = discover_test_fixtures(source)
        validate_fixture_manifest(declared, discovered)
    except (FixtureManifestError, SuiteExecutionError) as error:
        _write_summary(
            summary_path,
            {
                "schemaVersion": 1,
                "result": "Failed",
                "phase": "preflight",
                "error": str(error),
                "fixtureCount": 0,
                "completedFixtureCount": 0,
                "total": 0,
                "passed": 0,
                "failedFixtureCount": 1,
                "fixtures": [],
                "failures": [{"phase": "preflight", "error": str(error)}],
            },
        )
        raise
    logs.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    completed: list[FixtureResult] = []
    failures: list[dict[str, object]] = []

    for fixture in declared:
        stem = _artifact_stem(fixture)
        report_path = output / f"{stem}.xml"
        log_path = logs / f"{stem}.log"
        report_path.unlink(missing_ok=True)
        log_path.unlink(missing_ok=True)
        command = [
            str(unity_editor),
            "-batchmode",
            "-nographics",
            "-buildTarget",
            "Android",
            "-projectPath",
            str(project),
            "-runTests",
            "-testPlatform",
            "playmode",
            "-assemblyNames",
            arguments.assembly_name,
            "-testFilter",
            fixture,
            "-testResults",
            str(report_path),
            "-logFile",
            str(log_path),
        ]
        print(f"[JSS QA] PlayMode fixture: {fixture}", flush=True)
        try:
            process = subprocess.run(
                command,
                cwd=project,
                check=False,
                timeout=arguments.timeout_seconds,
            )
            if process.returncode != 0:
                raise SuiteExecutionError(
                    f"Unity exited {process.returncode} for {fixture}; log: {log_path}"
                )
            parsed = parse_unity_report(report_path, fixture)
            completed.append(
                FixtureResult(
                    fixture=parsed.fixture,
                    result=parsed.result,
                    total=parsed.total,
                    passed=parsed.passed,
                    durationSeconds=parsed.durationSeconds,
                    report=str(report_path),
                    log=str(log_path),
                    unityExitCode=process.returncode,
                )
            )
        except (OSError, subprocess.TimeoutExpired, SuiteExecutionError, ReportValidationError) as error:
            failures.append(
                {
                    "fixture": fixture,
                    "error": str(error),
                    "report": str(report_path),
                    "log": str(log_path),
                }
            )
            if not arguments.keep_going:
                break

    elapsed = time.monotonic() - started
    passed_total = sum(result.passed for result in completed)
    selected_total = sum(result.total for result in completed)
    success = not failures and len(completed) == len(declared)
    summary: dict[str, object] = {
        "schemaVersion": 1,
        "result": "Passed" if success else "Failed",
        "phase": "complete" if success else "execution",
        "fixtureCount": len(declared),
        "completedFixtureCount": len(completed),
        "total": selected_total,
        "passed": passed_total,
        "failedFixtureCount": len(failures),
        "elapsedSeconds": elapsed,
        "assemblyName": arguments.assembly_name,
        "fixtures": [asdict(result) for result in completed],
        "failures": failures,
    }
    _write_summary(summary_path, summary)
    if not success:
        for failure in failures:
            print(
                f"[JSS QA] FAILED {failure['fixture']}: {failure['error']}",
                file=sys.stderr,
            )
        print(f"[JSS QA] Partial summary: {summary_path}", file=sys.stderr)
        return 1
    print(
        f"[JSS QA] Passed {passed_total}/{selected_total} tests across "
        f"{len(completed)} isolated fixtures. Summary: {summary_path}",
        flush=True,
    )
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = _build_parser()
    try:
        return run_from_args(parser.parse_args(argv))
    except (FixtureManifestError, SuiteExecutionError) as error:
        print(f"[JSS QA] {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
