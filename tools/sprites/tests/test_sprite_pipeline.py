import copy
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
PIPELINE = REPOSITORY_ROOT / "tools" / "sprites" / "prepare_animation_run.py"
FIXTURE_GENERATOR = (
    REPOSITORY_ROOT / "tools" / "sprites" / "create_stage2_primitive.py"
)
FRAME_WIDTH = 64
FRAME_HEIGHT = 96
BASELINE_Y = 89


class SpritePipelineTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="jss-stage2-")
        self.root = Path(self.temporary.name)
        self.source_root = self.root / "source"
        self.output_root = self.root / "published"
        self.source_root.mkdir()
        self.request = self._write_valid_request()

    def tearDown(self):
        self.temporary.cleanup()

    def test_complete_rows_publish_deterministic_atlas_manifest_and_evidence(self):
        first = self._run()
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)

        expected = {
            ".jss-sprite-pipeline-owner.json",
            "primitive-stage2.png",
            "primitive-stage2.sprite-manifest.json",
            "primitive-stage2.sprite-manifest.sha256",
            "primitive-stage2-contact-sheet.png",
            "primitive-stage2-preview.webp",
        }
        self.assertEqual(
            {path.name for path in self.output_root.iterdir()},
            expected,
        )
        first_hashes = {
            path.name: self._sha256(path)
            for path in self.output_root.iterdir()
        }

        manifest_path = self.output_root / "primitive-stage2.sprite-manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertEqual(manifest["schemaVersion"], 1)
        self.assertEqual(manifest["characterId"], "primitive-stage2")
        self.assertEqual(manifest["atlas"]["format"], "PNG")
        self.assertEqual(manifest["atlas"]["width"], 512)
        self.assertEqual(manifest["atlas"]["height"], 192)
        self.assertEqual(
            manifest.get("processing"),
            {
                "alphaThreshold": 8,
                "maximumBaselineCorrectionPixels": 3,
                "maximumInteriorAlphaHolePixels": 0,
                "repairMode": "complete-rows-only",
            },
        )
        self.assertEqual(
            manifest["atlas"]["sha256"],
            self._sha256(self.output_root / "primitive-stage2.png"),
        )
        self.assertEqual(
            [clip["id"] for clip in manifest["clips"]],
            ["primitive.idle.right", "primitive.run.right"],
        )
        self.assertEqual(
            [len(clip["frames"]) for clip in manifest["clips"]],
            [4, 8],
        )
        self.assertEqual(
            [frame["contacts"] for frame in manifest["clips"][1]["frames"]],
            [["LeftFoot"], [], ["RightFoot"], [], ["LeftFoot"], [],
             ["RightFoot"], []],
        )
        self.assertTrue(manifest["validation"]["isValid"])
        self.assertEqual(manifest["validation"]["issues"], [])
        self.assertEqual(
            [
                frame["interiorAlphaHolePixels"]
                for clip in manifest["clips"]
                for frame in clip["frames"]
            ],
            [0] * 12,
        )
        with Image.open(self.output_root / "primitive-stage2.png") as opened:
            atlas = opened.convert("RGBA")
        self.assertEqual(
            sum(1 for pixel in atlas.getdata() if pixel == (0, 224, 255, 255)),
            0,
            "The structural facing marker must not ship in atlas pixels.",
        )
        self.assertEqual(
            self._count_interior_translucent_pixels(atlas),
            0,
            "Painterly detail may not punch translucent flicker into opaque forms.",
        )
        self.assertEqual(
            (self.output_root / "primitive-stage2.sprite-manifest.sha256")
            .read_text(encoding="ascii")
            .strip(),
            self._sha256(manifest_path),
        )

        second = self._run()
        self.assertEqual(second.returncode, 0, second.stdout + second.stderr)
        self.assertEqual(
            {
                path.name: self._sha256(path)
                for path in self.output_root.iterdir()
            },
            first_hashes,
        )
        self.assertFalse((self.output_root.parent / ".primitive-stage2.staging").exists())

    def test_registration_normalizes_small_source_baseline_variation(self):
        request = self._load_request()
        request["clips"][1]["sourceBaselineOffsets"] = [0, 1, 2, 0, 1, 2, 0, 1]
        self._draw_strip(
            self.source_root / "run.png",
            frame_count=8,
            baseline_offsets=request["clips"][1]["sourceBaselineOffsets"],
        )
        self._save_request(request)

        result = self._run()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        manifest = json.loads(
            (self.output_root / "primitive-stage2.sprite-manifest.json")
            .read_text(encoding="utf-8")
        )
        registered = [
            frame["registeredBaselinePixels"]
            for frame in manifest["clips"][1]["frames"]
        ]
        self.assertEqual(registered, [BASELINE_Y] * 8)

    def test_clipped_frame_removes_stale_success_and_transaction(self):
        first = self._run()
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self._draw_strip(
            self.source_root / "run.png",
            frame_count=8,
            touch_left_border_at=3,
        )

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("clipped", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())
        self.assertFalse((self.output_root.parent / ".primitive-stage2.staging").exists())

    def test_invalid_request_removes_owned_stale_success(self):
        first = self._run()
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        request = self._load_request()
        request["schemaVersion"] = 999
        self._save_request(request)

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("schemaversion", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())
        self.assertFalse((self.output_root.parent / ".primitive-stage2.staging").exists())

    def test_output_owned_by_another_character_is_refused_and_preserved(self):
        first = self._run()
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        original_hashes = {
            path.name: self._sha256(path)
            for path in self.output_root.iterdir()
        }
        request = self._load_request()
        request["characterId"] = "other-character"
        self._save_request(request)

        result = self._run()

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("owned by character", (result.stdout + result.stderr).lower())
        self.assertTrue(self.output_root.is_dir())
        self.assertEqual(
            {
                path.name: self._sha256(path)
                for path in self.output_root.iterdir()
            },
            original_hashes,
        )

    def test_large_baseline_excursion_fails_closed(self):
        request = self._load_request()
        request["clips"][1]["sourceBaselineOffsets"] = [0, 0, 0, 9, 0, 0, 0, 0]
        self._draw_strip(
            self.source_root / "run.png",
            frame_count=8,
            baseline_offsets=request["clips"][1]["sourceBaselineOffsets"],
        )
        self._save_request(request)

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("baseline", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_enclosed_transparency_hole_fails_closed(self):
        self._draw_strip(
            self.source_root / "run.png",
            frame_count=8,
            enclosed_hole_at=4,
        )

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("interior alpha hole", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_facing_marker_on_rear_side_fails_closed(self):
        self._draw_strip(
            self.source_root / "run.png",
            frame_count=8,
            reverse_facing_at=5,
        )

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("facing", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_non_alternating_run_contacts_fail_closed(self):
        request = self._load_request()
        request["clips"][1]["contacts"] = [
            ["LeftFoot"], [], ["LeftFoot"], [],
            ["RightFoot"], [], ["RightFoot"], [],
        ]
        self._save_request(request)

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("alternating", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_clip_ids_that_normalize_to_duplicate_sprite_names_fail_closed(self):
        request = self._load_request()
        request["clips"][0]["id"] = "primitive.run-right"
        request["clips"][1]["id"] = "primitive.run_right"
        self._save_request(request)

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("duplicate sprite name", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_partial_frame_repair_is_rejected_before_publication(self):
        request = self._load_request()
        request["repair"] = {
            "mode": "partial-frames",
            "clipId": "primitive.run.right",
            "frameIndices": [3],
        }
        self._save_request(request)

        result = self._run()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("complete-row", (result.stdout + result.stderr).lower())
        self.assertFalse(self.output_root.exists())

    def test_painterly_primitive_generator_produces_two_complete_coherent_rows(self):
        fixture_root = self.root / "fixture"
        result = subprocess.run(
            [
                sys.executable,
                str(FIXTURE_GENERATOR),
                "--root",
                str(fixture_root),
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        request_path = fixture_root / "primitive-stage2-request.json"
        request = json.loads(request_path.read_text(encoding="utf-8"))
        self.assertEqual(
            [clip["id"] for clip in request["clips"]],
            ["primitive.idle.right", "primitive.run.right"],
        )
        with Image.open(fixture_root / "source" / "primitive-stage2-idle.png") as idle:
            self.assertEqual(idle.size, (512, 192))
        with Image.open(fixture_root / "source" / "primitive-stage2-run.png") as run:
            self.assertEqual(run.size, (1024, 192))

        output = fixture_root / "published"
        pipeline = subprocess.run(
            [
                sys.executable,
                str(PIPELINE),
                "--request",
                str(request_path),
                "--output",
                str(output),
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(pipeline.returncode, 0, pipeline.stdout + pipeline.stderr)
        self.assertTrue((output / "primitive-stage2-preview.webp").is_file())

    def test_unowned_output_directory_is_refused_without_removing_contents(self):
        unowned = self.root / "unowned"
        unowned.mkdir()
        sentinel = unowned / "primitive-stage2.png"
        sentinel.write_text("belongs to someone else\n", encoding="utf-8")

        result = subprocess.run(
            [
                sys.executable,
                str(PIPELINE),
                "--request",
                str(self.request),
                "--output",
                str(unowned),
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not owned", (result.stdout + result.stderr).lower())
        self.assertEqual(sentinel.read_text(encoding="utf-8"),
                         "belongs to someone else\n")
        self.assertTrue(self.request.is_file())

    def _write_valid_request(self):
        self._draw_strip(self.source_root / "idle.png", frame_count=4)
        self._draw_strip(self.source_root / "run.png", frame_count=8)
        request = {
            "schemaVersion": 1,
            "characterId": "primitive-stage2",
            "pixelsPerUnit": 64,
            "atlasColumns": 8,
            "frameWidth": FRAME_WIDTH,
            "frameHeight": FRAME_HEIGHT,
            "alphaThreshold": 8,
            "maximumBaselineCorrectionPixels": 3,
            "facingMarker": {"rgba": [0, 224, 255, 255], "minimumPixels": 6},
            "repair": {"mode": "complete-rows-only"},
            "clips": [
                {
                    "id": "primitive.idle.right",
                    "sourceStrip": "source/idle.png",
                    "frameCount": 4,
                    "facing": "Right",
                    "cadenceFps": 12,
                    "loopMode": "Loop",
                    "pivotPixels": [32, 6],
                    "sourceBaselineOffsets": [0, 0, 0, 0],
                    "contacts": [
                        ["LeftFoot", "RightFoot"],
                        ["LeftFoot", "RightFoot"],
                        ["LeftFoot", "RightFoot"],
                        ["LeftFoot", "RightFoot"],
                    ],
                    "events": [[], [], [], []],
                },
                {
                    "id": "primitive.run.right",
                    "sourceStrip": "source/run.png",
                    "frameCount": 8,
                    "facing": "Right",
                    "cadenceFps": 12,
                    "loopMode": "Loop",
                    "pivotPixels": [32, 6],
                    "sourceBaselineOffsets": [0, 0, 0, 0, 0, 0, 0, 0],
                    "contacts": [
                        ["LeftFoot"], [], ["RightFoot"], [],
                        ["LeftFoot"], [], ["RightFoot"], [],
                    ],
                    "events": [
                        [{"id": "step-left", "kind": "FootContact"}],
                        [],
                        [{"id": "step-right", "kind": "FootContact"}],
                        [],
                        [{"id": "step-left", "kind": "FootContact"}],
                        [],
                        [{"id": "step-right", "kind": "FootContact"}],
                        [],
                    ],
                },
            ],
        }
        request_path = self.root / "request.json"
        request_path.write_text(
            json.dumps(request, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        return request_path

    def _draw_strip(
        self,
        path,
        frame_count,
        baseline_offsets=None,
        touch_left_border_at=None,
        enclosed_hole_at=None,
        reverse_facing_at=None,
    ):
        offsets = baseline_offsets or [0] * frame_count
        strip = Image.new("RGBA", (FRAME_WIDTH * frame_count, FRAME_HEIGHT))
        for index in range(frame_count):
            frame = Image.new("RGBA", (FRAME_WIDTH, FRAME_HEIGHT))
            draw = ImageDraw.Draw(frame)
            offset = offsets[index]
            bounce = (index % 4 == 1) - (index % 4 == 3)
            body = (18, 32 - offset + bounce, 45, 73 - offset + bounce)
            draw.ellipse(body, fill=(235, 152, 72, 255), outline=(76, 42, 54, 255), width=2)
            draw.ellipse((22, 12 - offset, 43, 36 - offset),
                         fill=(245, 184, 112, 255), outline=(76, 42, 54, 255), width=2)
            draw.polygon([(20, 67 - offset), (30, 67 - offset),
                          (27, BASELINE_Y - offset), (17, BASELINE_Y - offset)],
                         fill=(42, 83, 120, 255))
            draw.polygon([(34, 67 - offset), (44, 67 - offset),
                          (48, BASELINE_Y - offset), (38, BASELINE_Y - offset)],
                         fill=(52, 104, 142, 255))
            marker_x = 10 if reverse_facing_at == index else 48
            draw.rectangle((marker_x, 27 - offset, marker_x + 3, 30 - offset),
                           fill=(0, 224, 255, 255))
            if touch_left_border_at == index:
                draw.rectangle((0, 50, 8, 60), fill=(235, 152, 72, 255))
            if enclosed_hole_at == index:
                draw.ellipse((27, 45 - offset, 34, 52 - offset), fill=(0, 0, 0, 0))
            strip.alpha_composite(frame, (index * FRAME_WIDTH, 0))
        path.parent.mkdir(parents=True, exist_ok=True)
        strip.save(path, format="PNG", optimize=False)

    def _load_request(self):
        return json.loads(self.request.read_text(encoding="utf-8"))

    def _save_request(self, request):
        self.request.write_text(
            json.dumps(request, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )

    def _run(self):
        return subprocess.run(
            [
                sys.executable,
                str(PIPELINE),
                "--request",
                str(self.request),
                "--output",
                str(self.output_root),
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    @staticmethod
    def _sha256(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()

    @staticmethod
    def _count_interior_translucent_pixels(image):
        alpha = image.getchannel("A")
        pixels = alpha.load()
        count = 0
        for y in range(1, image.height - 1):
            for x in range(1, image.width - 1):
                if not 8 < pixels[x, y] < 250:
                    continue
                if all(
                    pixels[nx, ny] >= 250
                    for nx, ny in (
                        (x - 1, y),
                        (x + 1, y),
                        (x, y - 1),
                        (x, y + 1),
                    )
                ):
                    count += 1
        return count


if __name__ == "__main__":
    unittest.main()
