#!/usr/bin/env python3
"""Bind the final Task 12 Stage 3 runtime evidence to the Captain package."""

from __future__ import annotations

import hashlib
import json
import os
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CAPTAIN_ROOT = (
    REPOSITORY_ROOT
    / "Assets/_JustSomeStars/Art/2D/Characters/Captain"
)
PACKAGE_MANIFEST = CAPTAIN_ROOT / "captain-sprite-package.json"
TRACKED_EVIDENCE = (
    CAPTAIN_ROOT / "Evidence/captain-stage3-runtime-evidence.json"
)
BUILD_EVIDENCE = (
    REPOSITORY_ROOT
    / "Builds/VisualEvidence/task12-stage3-final"
    / "captain-stage3-evidence-manifest.json"
)

CAPTURE_ROOT = Path("Builds/VisualEvidence/task12-stage3-final")
CAPTURE_STEMS = (
    "compact-run",
    "average-custom-scan",
    "tallbroad-climb",
)
CAPTURE_LOGS = tuple(
    Path("Builds/Logs")
    / f"task12-stage3-final-{stem}-capture-post-critic.log"
    for stem in CAPTURE_STEMS
)
FOCUSED_RESULTS = (
    (
        Path(
            "Builds/TestResults/"
            "task12-stage3-post-critic-editmode-green.xml"
        ),
        Path("Builds/Logs/task12-stage3-post-critic-editmode-green.log"),
        4,
    ),
    (
        Path(
            "Builds/TestResults/"
            "task12-stage3-post-critic-playmode-green.xml"
        ),
        Path("Builds/Logs/task12-stage3-post-critic-playmode-green.log"),
        3,
    ),
)
PYTHON_RESULT = Path(
    "Builds/Logs/task12-stage3-python-post-critic-green.log"
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _relative(path: Path) -> str:
    return path.relative_to(REPOSITORY_ROOT).as_posix()


def _require_file(relative: Path) -> Path:
    path = REPOSITORY_ROOT / relative
    if not path.is_file() or path.stat().st_size <= 0:
        raise RuntimeError(f"Required evidence is missing or empty: {relative}")
    return path


def _artifact(relative: Path, kind: str) -> dict[str, object]:
    path = _require_file(relative)
    return {
        "kind": kind,
        "path": relative.as_posix(),
        "bytes": path.stat().st_size,
        "sha256": _sha256(path),
    }


def _validate_test_result(relative: Path, expected_count: int) -> None:
    path = _require_file(relative)
    root = ET.parse(path).getroot()
    result = root.attrib.get("result")
    total = int(root.attrib.get("total", "-1"))
    passed = int(root.attrib.get("passed", "-1"))
    failed = int(root.attrib.get("failed", "-1"))
    skipped = int(root.attrib.get("skipped", "0"))
    inconclusive = int(root.attrib.get("inconclusive", "0"))
    if (
        result != "Passed"
        or total != expected_count
        or passed != expected_count
        or failed != 0
        or skipped != 0
        or inconclusive != 0
    ):
        raise RuntimeError(
            f"Focused result is not exact {expected_count}/{expected_count}: "
            f"{relative} ({root.attrib})"
        )


def _canonical_json_sha(payload: dict[str, object]) -> str:
    encoded = json.dumps(
        payload,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _write_json_atomic(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=path.name + ".",
        suffix=".tmp",
        dir=path.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, indent=2, ensure_ascii=False, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _collect_artifacts() -> list[dict[str, object]]:
    artifacts: list[dict[str, object]] = []
    for stem in CAPTURE_STEMS:
        artifacts.append(_artifact(CAPTURE_ROOT / f"{stem}.png", "runtime-capture"))
        artifacts.append(_artifact(CAPTURE_ROOT / f"{stem}.webp", "runtime-motion"))
        artifacts.append(
            _artifact(CAPTURE_ROOT / f"{stem}-sequence-contact.png", "motion-contact-sheet")
        )
        sequence = CAPTURE_ROOT / f"{stem}-sequence"
        for frame_index in range(8):
            artifacts.append(
                _artifact(
                    sequence / f"captain-frame-{frame_index:02d}.png",
                    "runtime-sequence-frame",
                )
            )
        artifacts.append(_artifact(sequence / "sequence-contract.txt", "sequence-contract"))

    artifacts.append(
        _artifact(CAPTURE_ROOT / "stage3-final-three-captures.png", "capture-montage")
    )
    artifacts.append(
        _artifact(CAPTURE_ROOT / "family-lineup.png", "same-scale-family-lineup")
    )
    for relative in CAPTURE_LOGS:
        artifacts.append(_artifact(relative, "unity-capture-log"))
    artifacts.append(_artifact(PYTHON_RESULT, "python-focused-test-log"))
    for result, log, _ in FOCUSED_RESULTS:
        artifacts.append(_artifact(result, "unity-focused-test-result"))
        artifacts.append(_artifact(log, "unity-focused-test-log"))
    return artifacts


def main() -> int:
    if not PACKAGE_MANIFEST.is_file():
        raise RuntimeError(f"Captain package manifest is missing: {PACKAGE_MANIFEST}")
    for result, _, expected_count in FOCUSED_RESULTS:
        _validate_test_result(result, expected_count)
    python_log = _require_file(PYTHON_RESULT).read_text(encoding="utf-8")
    if "Ran 6 tests" not in python_log or "OK" not in python_log:
        raise RuntimeError("Python focused evidence is not an exact successful 6-test run")

    package = json.loads(PACKAGE_MANIFEST.read_text(encoding="utf-8"))
    package_without_evidence = dict(package)
    package_without_evidence.pop("runtimeEvidenceManifest", None)
    package_contract_sha = _canonical_json_sha(package_without_evidence)
    artifacts = _collect_artifacts()
    if sum(item["kind"] == "runtime-capture" for item in artifacts) != 3:
        raise RuntimeError("Expected exactly three runtime captures")
    if sum(item["kind"] == "runtime-motion" for item in artifacts) != 3:
        raise RuntimeError("Expected exactly three runtime motion files")
    if sum(item["kind"] == "runtime-sequence-frame" for item in artifacts) != 24:
        raise RuntimeError("Expected exactly twenty-four runtime sequence frames")

    tracked_payload: dict[str, object] = {
        "schemaVersion": 1,
        "stage": "Task 12 Stage 3 - Captain families and modular runtime",
        "packageManifestPath": _relative(PACKAGE_MANIFEST),
        "packageContractSha256": package_contract_sha,
        "authorityHashes": package_without_evidence["authorityHashes"],
        "captureCount": 3,
        "motionCount": 3,
        "sequenceFrameCount": 24,
        "focusedTestCounts": {"python": 6, "editMode": 4, "playMode": 3},
        "artifacts": artifacts,
    }
    _write_json_atomic(TRACKED_EVIDENCE, tracked_payload)
    tracked_sha = _sha256(TRACKED_EVIDENCE)

    package["runtimeEvidenceManifest"] = {
        "path": _relative(TRACKED_EVIDENCE),
        "sha256": tracked_sha,
        "packageContractSha256": package_contract_sha,
        "captureCount": 3,
        "motionCount": 3,
        "sequenceFrameCount": 24,
    }
    _write_json_atomic(PACKAGE_MANIFEST, package)

    build_payload = {
        "schemaVersion": 1,
        "trackedManifest": {
            "path": _relative(TRACKED_EVIDENCE),
            "sha256": tracked_sha,
        },
        "packageManifest": {
            "path": _relative(PACKAGE_MANIFEST),
            "sha256": _sha256(PACKAGE_MANIFEST),
            "contractSha256": package_contract_sha,
        },
        "artifacts": artifacts,
    }
    _write_json_atomic(BUILD_EVIDENCE, build_payload)

    stored_package = json.loads(PACKAGE_MANIFEST.read_text(encoding="utf-8"))
    stored_link = stored_package.get("runtimeEvidenceManifest", {})
    if stored_link.get("sha256") != _sha256(TRACKED_EVIDENCE):
        raise RuntimeError("Captain package evidence link failed post-write validation")
    stored_tracked = json.loads(TRACKED_EVIDENCE.read_text(encoding="utf-8"))
    if stored_tracked != tracked_payload:
        raise RuntimeError("Tracked Captain evidence changed during publication")
    print(
        "Captain Stage 3 evidence bound: "
        f"{len(artifacts)} artifacts, tracked SHA-256 {tracked_sha}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
