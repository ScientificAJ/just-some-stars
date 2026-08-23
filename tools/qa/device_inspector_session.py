#!/usr/bin/env python3
"""Enforce one UI-inspection backend per paid Android device session."""

from __future__ import annotations

import argparse
import fcntl
import json
import os
import subprocess
import sys
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterator, Sequence


VALID_BACKENDS = ("argent", "limrun-uiautomator")
DEFAULT_STATE = Path("Builds/DeviceSessions/active-inspector.json")


class InspectorSessionError(RuntimeError):
    """Inspector ownership is missing, malformed, or conflicts."""


@dataclass(frozen=True)
class InspectorSession:
    backend: str
    instance_id: str
    serial: str
    started_at_utc: str

    def to_json(self) -> dict[str, object]:
        return {
            "schemaVersion": 1,
            "backend": self.backend,
            "instanceId": self.instance_id,
            "serial": self.serial,
            "startedAtUtc": self.started_at_utc,
        }


def _validate_identity(backend: str, instance_id: str, serial: str) -> None:
    if backend not in VALID_BACKENDS:
        raise InspectorSessionError(
            f"unsupported inspector backend {backend!r}; expected {VALID_BACKENDS}"
        )
    for label, value in (("instance ID", instance_id), ("serial", serial)):
        if not value or value.strip() != value or any(character.isspace() for character in value):
            raise InspectorSessionError(f"{label} must be nonempty and contain no whitespace")


def _lock_path(state_path: Path) -> Path:
    return state_path.with_name(state_path.name + ".lock")


@contextmanager
def _locked(state_path: Path, exclusive: bool) -> Iterator[None]:
    state_path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = _lock_path(state_path)
    with lock_path.open("a+", encoding="utf-8") as lock_stream:
        fcntl.flock(lock_stream.fileno(), fcntl.LOCK_EX if exclusive else fcntl.LOCK_SH)
        try:
            yield
        finally:
            fcntl.flock(lock_stream.fileno(), fcntl.LOCK_UN)


def _read_state_unlocked(state_path: Path) -> InspectorSession:
    if not state_path.is_file():
        raise InspectorSessionError(f"no active inspector session: {state_path}")
    try:
        payload = json.loads(state_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise InspectorSessionError(f"inspector state is malformed: {state_path}") from error
    expected_keys = {
        "schemaVersion",
        "backend",
        "instanceId",
        "serial",
        "startedAtUtc",
    }
    if not isinstance(payload, dict) or set(payload) != expected_keys:
        raise InspectorSessionError(f"inspector state is malformed: {state_path}")
    if payload.get("schemaVersion") != 1:
        raise InspectorSessionError(f"inspector state is malformed: {state_path}")
    values = (payload.get("backend"), payload.get("instanceId"), payload.get("serial"))
    if not all(isinstance(value, str) for value in values):
        raise InspectorSessionError(f"inspector state is malformed: {state_path}")
    started_at = payload.get("startedAtUtc")
    if not isinstance(started_at, str) or not started_at:
        raise InspectorSessionError(f"inspector state is malformed: {state_path}")
    backend, instance_id, serial = values
    _validate_identity(backend, instance_id, serial)
    return InspectorSession(backend, instance_id, serial, started_at)


def _require_match(
    session: InspectorSession,
    backend: str,
    instance_id: str,
    serial: str,
) -> None:
    requested = (backend, instance_id, serial)
    active = (session.backend, session.instance_id, session.serial)
    if requested != active:
        raise InspectorSessionError(
            "requested inspector identity does not match the active session: "
            f"active={active!r}, requested={requested!r}"
        )


def begin_session(
    state_path: Path,
    backend: str,
    instance_id: str,
    serial: str,
) -> InspectorSession:
    state_path = state_path.resolve()
    _validate_identity(backend, instance_id, serial)
    with _locked(state_path, exclusive=True):
        if state_path.exists():
            active = _read_state_unlocked(state_path)
            raise InspectorSessionError(
                "an inspector session is already active: "
                f"backend={active.backend}, instance={active.instance_id}, "
                f"serial={active.serial}"
            )
        session = InspectorSession(
            backend=backend,
            instance_id=instance_id,
            serial=serial,
            started_at_utc=datetime.now(timezone.utc).isoformat(),
        )
        temporary = state_path.with_name(state_path.name + ".tmp")
        temporary.write_text(
            json.dumps(session.to_json(), indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        temporary.chmod(0o600)
        os.replace(temporary, state_path)
        return session


def require_session(
    state_path: Path,
    backend: str,
    instance_id: str,
    serial: str,
) -> InspectorSession:
    state_path = state_path.resolve()
    _validate_identity(backend, instance_id, serial)
    with _locked(state_path, exclusive=False):
        session = _read_state_unlocked(state_path)
        _require_match(session, backend, instance_id, serial)
        return session


def end_session(
    state_path: Path,
    backend: str,
    instance_id: str,
    serial: str,
) -> InspectorSession:
    state_path = state_path.resolve()
    _validate_identity(backend, instance_id, serial)
    with _locked(state_path, exclusive=True):
        session = _read_state_unlocked(state_path)
        _require_match(session, backend, instance_id, serial)
        state_path.unlink()
        return session


def run_guarded(
    state_path: Path,
    backend: str,
    instance_id: str,
    serial: str,
    command: Sequence[str],
) -> int:
    if not command:
        raise InspectorSessionError("guarded run requires a command")
    executable = Path(command[0]).name
    expected_executable = "argent" if backend == "argent" else "lim"
    if executable != expected_executable:
        raise InspectorSessionError(
            f"backend {backend!r} requires executable {expected_executable!r}, "
            f"not {executable!r}"
        )
    state_path = state_path.resolve()
    _validate_identity(backend, instance_id, serial)
    with _locked(state_path, exclusive=False):
        session = _read_state_unlocked(state_path)
        _require_match(session, backend, instance_id, serial)
        try:
            return subprocess.run(command, check=False).returncode
        except OSError as error:
            raise InspectorSessionError(f"could not run guarded command: {error}") from error


def _add_identity_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--backend", required=True, choices=VALID_BACKENDS)
    parser.add_argument("--instance-id", required=True)
    parser.add_argument("--serial", required=True)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--state", type=Path, default=DEFAULT_STATE)
    subparsers = parser.add_subparsers(dest="action", required=True)
    for action in ("begin", "require", "end"):
        subparser = subparsers.add_parser(action)
        _add_identity_arguments(subparser)
    run_parser = subparsers.add_parser("run")
    _add_identity_arguments(run_parser)
    run_parser.add_argument("command", nargs=argparse.REMAINDER)
    subparsers.add_parser("status")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.action == "begin":
            session = begin_session(
                arguments.state,
                arguments.backend,
                arguments.instance_id,
                arguments.serial,
            )
            print(json.dumps(session.to_json(), sort_keys=True))
            return 0
        if arguments.action == "require":
            session = require_session(
                arguments.state,
                arguments.backend,
                arguments.instance_id,
                arguments.serial,
            )
            print(json.dumps(session.to_json(), sort_keys=True))
            return 0
        if arguments.action == "end":
            session = end_session(
                arguments.state,
                arguments.backend,
                arguments.instance_id,
                arguments.serial,
            )
            print(json.dumps(session.to_json(), sort_keys=True))
            return 0
        if arguments.action == "run":
            command = list(arguments.command)
            if command and command[0] == "--":
                command = command[1:]
            return run_guarded(
                arguments.state,
                arguments.backend,
                arguments.instance_id,
                arguments.serial,
                command,
            )
        state_path = arguments.state.resolve()
        with _locked(state_path, exclusive=False):
            session = _read_state_unlocked(state_path)
        print(json.dumps(session.to_json(), sort_keys=True))
        return 0
    except InspectorSessionError as error:
        print(f"[JSS QA] {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
