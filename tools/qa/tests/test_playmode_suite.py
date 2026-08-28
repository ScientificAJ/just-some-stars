from __future__ import annotations

import json
import stat
import tempfile
import textwrap
import unittest
from pathlib import Path

from tools.qa.playmode_suite import (
    FixtureManifestError,
    ReportValidationError,
    discover_test_fixtures,
    load_fixture_manifest,
    main,
    parse_unity_report,
    validate_fixture_manifest,
)


class PlayModeSuiteTests(unittest.TestCase):
    def test_manifest_must_exactly_match_discovered_test_fixtures(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "Assets" / "Tests" / "PlayMode"
            source.mkdir(parents=True)
            (source / "AlphaTests.cs").write_text(
                textwrap.dedent(
                    """
                    using NUnit.Framework;
                    namespace Example.Tests.PlayMode
                    {
                        public sealed class AlphaTests
                        {
                            [Test]
                            public void Passes() { }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            (source / "BetaTests.cs").write_text(
                textwrap.dedent(
                    """
                    using UnityEngine.TestTools;
                    namespace Example.Tests.PlayMode
                    {
                        public sealed class BetaTests
                        {
                            [UnityTest]
                            public System.Collections.IEnumerator Passes()
                            {
                                yield break;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            manifest = root / "fixtures.txt"
            manifest.write_text(
                "Example.Tests.PlayMode.AlphaTests\n"
                "Example.Tests.PlayMode.BetaTests\n",
                encoding="utf-8",
            )

            discovered = discover_test_fixtures(source)
            declared = load_fixture_manifest(manifest)

            self.assertEqual(
                discovered,
                (
                    "Example.Tests.PlayMode.AlphaTests",
                    "Example.Tests.PlayMode.BetaTests",
                ),
            )
            validate_fixture_manifest(declared, discovered)

    def test_manifest_rejects_an_omitted_discovered_fixture(self) -> None:
        with self.assertRaisesRegex(FixtureManifestError, "missing from manifest"):
            validate_fixture_manifest(
                ("Example.Tests.PlayMode.AlphaTests",),
                (
                    "Example.Tests.PlayMode.AlphaTests",
                    "Example.Tests.PlayMode.BetaTests",
                ),
            )

    def test_discovery_recurses_into_playmode_subdirectories(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            source = Path(temporary_directory) / "PlayMode"
            nested = source / "Feature"
            nested.mkdir(parents=True)
            (nested / "NestedTests.cs").write_text(
                textwrap.dedent(
                    """
                    using NUnit.Framework;
                    namespace Example.Tests.PlayMode.Feature
                    {
                        public sealed class NestedTests
                        {
                            [Test]
                            public void Passes() { }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            self.assertEqual(
                discover_test_fixtures(source),
                ("Example.Tests.PlayMode.Feature.NestedTests",),
            )

    def test_report_rejects_zero_selected_tests_even_when_unity_says_passed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            report = Path(temporary_directory) / "zero.xml"
            report.write_text(
                '<test-run result="Passed" testcasecount="90" total="0" '
                'passed="0" failed="0" skipped="0" inconclusive="0" />',
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ReportValidationError, "selected zero tests"):
                parse_unity_report(
                    report,
                    "Example.Tests.PlayMode.AlphaTests",
                )

    def test_runner_executes_each_fixture_in_its_own_process_and_aggregates(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "Assets" / "Tests" / "PlayMode"
            source.mkdir(parents=True)
            fixtures = (
                "Example.Tests.PlayMode.AlphaTests",
                "Example.Tests.PlayMode.BetaTests",
            )
            for fixture in fixtures:
                class_name = fixture.rsplit(".", 1)[-1]
                (source / f"{class_name}.cs").write_text(
                    textwrap.dedent(
                        f"""
                        using NUnit.Framework;
                        namespace Example.Tests.PlayMode
                        {{
                            public sealed class {class_name}
                            {{
                                [Test]
                                public void Passes() {{ }}
                            }}
                        }}
                        """
                    ),
                    encoding="utf-8",
                )
            manifest = root / "fixtures.txt"
            manifest.write_text("\n".join(fixtures) + "\n", encoding="utf-8")
            invocation_log = root / "invocations.jsonl"
            fake_unity = root / "fake-unity"
            fake_unity.write_text(
                textwrap.dedent(
                    f"""\
                    #!/usr/bin/env python3
                    import json
                    import sys
                    from pathlib import Path
                    args = sys.argv[1:]
                    fixture = args[args.index('-testFilter') + 1]
                    report = Path(args[args.index('-testResults') + 1])
                    log = Path(args[args.index('-logFile') + 1])
                    report.parent.mkdir(parents=True, exist_ok=True)
                    log.parent.mkdir(parents=True, exist_ok=True)
                    report.write_text(
                        '<test-run result="Passed" testcasecount="1" total="1" '
                        'passed="1" failed="0" skipped="0" inconclusive="0">'
                        '<test-suite type="TestFixture" fullname="' + fixture + '" '
                        'result="Passed" total="1" passed="1" failed="0" '
                        'skipped="0" inconclusive="0">'
                        '<test-case fullname="' + fixture + '.Passes" result="Passed" />'
                        '</test-suite></test-run>',
                        encoding='utf-8',
                    )
                    log.write_text('fake Unity completed\\n', encoding='utf-8')
                    with Path({str(invocation_log)!r}).open('a', encoding='utf-8') as stream:
                        stream.write(json.dumps(args) + '\\n')
                    """
                ),
                encoding="utf-8",
            )
            fake_unity.chmod(fake_unity.stat().st_mode | stat.S_IXUSR)
            output = root / "Builds" / "TestResults" / "suite"
            exit_code = main(
                [
                    "--unity-editor",
                    str(fake_unity),
                    "--project-path",
                    str(root),
                    "--source-directory",
                    str(source),
                    "--manifest",
                    str(manifest),
                    "--output-directory",
                    str(output),
                    "--timeout-seconds",
                    "10",
                ]
            )

            self.assertEqual(exit_code, 0)
            summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual(summary["result"], "Passed")
            self.assertEqual(summary["fixtureCount"], 2)
            self.assertEqual(summary["total"], 2)
            self.assertEqual(summary["passed"], 2)
            invocations = [
                json.loads(line)
                for line in invocation_log.read_text(encoding="utf-8").splitlines()
            ]
            self.assertEqual(len(invocations), 2)
            self.assertEqual(
                [args[args.index("-testFilter") + 1] for args in invocations],
                list(fixtures),
            )
            self.assertTrue(all("-quit" not in args for args in invocations))

    def test_runner_preserves_graphics_for_the_pixel_render_fixture(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "Assets" / "Tests" / "PlayMode"
            source.mkdir(parents=True)
            fixtures = (
                "JustSomeStars.Tests.PlayMode.LayeredCharacterRendererTests",
                "JustSomeStars.Tests.PlayMode.SurfaceMotor2DTests",
            )
            for fixture in fixtures:
                class_name = fixture.rsplit(".", 1)[-1]
                (source / f"{class_name}.cs").write_text(
                    textwrap.dedent(
                        f"""
                        using NUnit.Framework;
                        namespace JustSomeStars.Tests.PlayMode
                        {{
                            public sealed class {class_name}
                            {{
                                [Test]
                                public void Passes() {{ }}
                            }}
                        }}
                        """
                    ),
                    encoding="utf-8",
                )
            manifest = root / "fixtures.txt"
            manifest.write_text("\n".join(fixtures) + "\n", encoding="utf-8")
            invocation_log = root / "invocations.jsonl"
            fake_unity = root / "fake-unity"
            fake_unity.write_text(
                textwrap.dedent(
                    f"""\
                    #!/usr/bin/env python3
                    import json
                    import sys
                    from pathlib import Path
                    args = sys.argv[1:]
                    fixture = args[args.index('-testFilter') + 1]
                    report = Path(args[args.index('-testResults') + 1])
                    log = Path(args[args.index('-logFile') + 1])
                    report.parent.mkdir(parents=True, exist_ok=True)
                    log.parent.mkdir(parents=True, exist_ok=True)
                    report.write_text(
                        '<test-run result="Passed" testcasecount="1" total="1" '
                        'passed="1" failed="0" skipped="0" inconclusive="0">'
                        '<test-suite type="TestFixture" fullname="' + fixture + '" '
                        'result="Passed" total="1" passed="1" failed="0" '
                        'skipped="0" inconclusive="0">'
                        '<test-case fullname="' + fixture + '.Passes" result="Passed" />'
                        '</test-suite></test-run>',
                        encoding='utf-8',
                    )
                    log.write_text('fake Unity completed\\n', encoding='utf-8')
                    with Path({str(invocation_log)!r}).open('a', encoding='utf-8') as stream:
                        stream.write(json.dumps(args) + '\\n')
                    """
                ),
                encoding="utf-8",
            )
            fake_unity.chmod(fake_unity.stat().st_mode | stat.S_IXUSR)

            exit_code = main(
                [
                    "--unity-editor",
                    str(fake_unity),
                    "--project-path",
                    str(root),
                    "--source-directory",
                    str(source),
                    "--manifest",
                    str(manifest),
                    "--output-directory",
                    str(root / "results"),
                    "--log-directory",
                    str(root / "logs"),
                ]
            )

            self.assertEqual(exit_code, 0)
            invocations = {
                args[args.index("-testFilter") + 1]: args
                for args in (
                    json.loads(line)
                    for line in invocation_log.read_text(
                        encoding="utf-8"
                    ).splitlines()
                )
            }
            self.assertNotIn(
                "-nographics",
                invocations[
                    "JustSomeStars.Tests.PlayMode.LayeredCharacterRendererTests"
                ],
            )
            self.assertIn(
                "-nographics",
                invocations[
                    "JustSomeStars.Tests.PlayMode.SurfaceMotor2DTests"
                ],
            )

    def test_preflight_failure_replaces_a_stale_passing_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            output = root / "Builds" / "TestResults" / "suite"
            output.mkdir(parents=True)
            summary_path = output / "summary.json"
            summary_path.write_text(
                '{"schemaVersion": 1, "result": "Passed"}\n',
                encoding="utf-8",
            )

            exit_code = main(
                [
                    "--unity-editor",
                    str(root / "missing-unity"),
                    "--project-path",
                    str(root),
                    "--output-directory",
                    str(output),
                ]
            )

            self.assertEqual(exit_code, 1)
            summary = json.loads(summary_path.read_text(encoding="utf-8"))
            self.assertEqual(summary["result"], "Failed")
            self.assertEqual(summary["phase"], "preflight")
            self.assertIn("not executable", summary["error"])


if __name__ == "__main__":
    unittest.main()
