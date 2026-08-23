from __future__ import annotations

import json
import stat
import subprocess
import sys
import tempfile
import textwrap
import time
import unittest
from pathlib import Path

import tools.qa.device_inspector_session as inspector_module
from tools.qa.device_inspector_session import (
    InspectorSessionError,
    begin_session,
    end_session,
    require_session,
    run_guarded,
)


class DeviceInspectorSessionTests(unittest.TestCase):
    def test_active_argent_session_blocks_limrun_uiautomator(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            state = Path(temporary_directory) / "active-inspector.json"

            begin_session(state, "argent", "instance-123", "serial-456")

            with self.assertRaisesRegex(InspectorSessionError, "already active"):
                begin_session(
                    state,
                    "limrun-uiautomator",
                    "instance-123",
                    "serial-456",
                )
            active = require_session(state, "argent", "instance-123", "serial-456")
            self.assertEqual(active.backend, "argent")

    def test_session_end_requires_the_exact_backend_instance_and_serial(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            state = Path(temporary_directory) / "active-inspector.json"
            begin_session(state, "argent", "instance-123", "serial-456")

            with self.assertRaisesRegex(InspectorSessionError, "does not match"):
                end_session(
                    state,
                    "argent",
                    "different-instance",
                    "serial-456",
                )
            self.assertTrue(state.exists())

            end_session(state, "argent", "instance-123", "serial-456")
            self.assertFalse(state.exists())

    def test_malformed_state_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            state = Path(temporary_directory) / "active-inspector.json"
            state.write_text(json.dumps({"backend": "argent"}), encoding="utf-8")

            with self.assertRaisesRegex(InspectorSessionError, "malformed"):
                require_session(state, "argent", "instance-123", "serial-456")

    def test_competing_processes_cannot_claim_different_backends(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            state = Path(temporary_directory) / "active-inspector.json"
            script = Path(inspector_module.__file__).resolve()
            base = [
                sys.executable,
                str(script),
                "--state",
                str(state),
                "begin",
                "--instance-id",
                "instance-123",
                "--serial",
                "serial-456",
            ]
            argent = subprocess.Popen(
                base + ["--backend", "argent"],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            limrun = subprocess.Popen(
                base + ["--backend", "limrun-uiautomator"],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )

            argent.communicate(timeout=5)
            limrun.communicate(timeout=5)

            self.assertEqual(sorted((argent.returncode, limrun.returncode)), [0, 1])
            active = json.loads(state.read_text(encoding="utf-8"))
            self.assertIn(active["backend"], ("argent", "limrun-uiautomator"))

    def test_guarded_run_holds_lock_and_rejects_wrong_executable(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            state = root / "active-inspector.json"
            begin_session(state, "argent", "instance-123", "serial-456")
            with self.assertRaisesRegex(InspectorSessionError, "requires executable"):
                run_guarded(
                    state,
                    "argent",
                    "instance-123",
                    "serial-456",
                    ["/bin/true"],
                )

            started = root / "started"
            release = root / "release"
            fake_argent = root / "argent"
            fake_argent.write_text(
                textwrap.dedent(
                    f"""\
                    #!{sys.executable}
                    import time
                    from pathlib import Path
                    Path({str(started)!r}).touch()
                    release = Path({str(release)!r})
                    while not release.exists():
                        time.sleep(0.01)
                    """
                ),
                encoding="utf-8",
            )
            fake_argent.chmod(fake_argent.stat().st_mode | stat.S_IXUSR)
            script = Path(inspector_module.__file__).resolve()
            identity = [
                "--backend",
                "argent",
                "--instance-id",
                "instance-123",
                "--serial",
                "serial-456",
            ]
            guarded = subprocess.Popen(
                [
                    sys.executable,
                    str(script),
                    "--state",
                    str(state),
                    "run",
                    *identity,
                    "--",
                    str(fake_argent),
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            deadline = time.monotonic() + 5
            while not started.exists() and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertTrue(started.exists(), "guarded command never started")

            ending = subprocess.Popen(
                [
                    sys.executable,
                    str(script),
                    "--state",
                    str(state),
                    "end",
                    *identity,
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            time.sleep(0.1)
            self.assertIsNone(ending.poll(), "end did not wait for the guarded run")

            release.touch()
            guarded.communicate(timeout=5)
            ending.communicate(timeout=5)
            self.assertEqual(guarded.returncode, 0)
            self.assertEqual(ending.returncode, 0)
            self.assertFalse(state.exists())


if __name__ == "__main__":
    unittest.main()
