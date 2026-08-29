import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageFilter


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
GENERATOR = REPOSITORY_ROOT / "tools" / "sprites" / "create_captain_package.py"
MASTER = (
    REPOSITORY_ROOT
    / "Assets/_JustSomeStars/Art/2D/Characters/Captain/Source/References"
    / "captain-right-facing-family-master-v2.png"
)
PACKAGE_ROOT = (
    REPOSITORY_ROOT
    / "Assets/_JustSomeStars/Art/2D/Characters/Captain"
)
CLIP_ORDER = [
    {"id": "idle"},
    {"id": "run"},
    {"id": "turn"},
    {"id": "jump"},
    {"id": "land"},
    {"id": "climb"},
    {"id": "scan"},
    {"id": "interact"},
]


class CaptainSpritePackageTests(unittest.TestCase):
    def test_published_package_has_real_palette_motion_family_and_anchor_pixels(self):
        manifest = json.loads(
            (PACKAGE_ROOT / "captain-sprite-package.json").read_text(
                encoding="utf-8"
            )
        )
        modules = {
            (
                publication["family"],
                publication["facing"],
                publication["category"],
            ): publication
            for publication in manifest["modulePublications"]
        }

        channel_bounds = [False, False, False, False]
        for publication in manifest["palettePublications"]:
            with Image.open(PACKAGE_ROOT / publication["path"]) as image:
                channels = image.convert("RGBA").split()
                channel_bounds = [
                    prior or channel.getbbox() is not None
                    for prior, channel in zip(channel_bounds, channels)
                ]
        self.assertEqual(
            channel_bounds,
            [True, True, True, True],
            "Skin, hair, suit and Signal palette channels must all own pixels.",
        )

        for publication in manifest["modulePublications"]:
            with Image.open(PACKAGE_ROOT / publication["path"]) as page:
                self.assertGreaterEqual(page.width, 2048)
                self.assertGreaterEqual(page.height, 1536)
            module_manifest = json.loads(
                (PACKAGE_ROOT / publication["manifestPath"]).read_text(
                    encoding="utf-8"
                )
            )
            self.assertGreaterEqual(module_manifest["optionAtlasSize"][0], 512)
            self.assertGreaterEqual(module_manifest["optionAtlasSize"][1], 768)

        with tempfile.TemporaryDirectory(prefix="jss-captain-integrity-") as temporary:
            report = Path(temporary) / "preflight.json"
            result = self._run(
                "--preflight-master",
                str(MASTER),
                "--write-report",
                str(report),
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            families = json.loads(report.read_text(encoding="utf-8"))["families"]
        ratios = {}
        for family in families:
            left, top, right, bottom = family["alphaBoundsPixels"]
            ratios[family["id"]] = (right - left) / (bottom - top)
        self.assertGreaterEqual(ratios["Compact"], ratios["Average"] * 1.05)
        self.assertGreaterEqual(ratios["TallBroad"], ratios["Average"] * 1.10)

        for family in ("compact", "average", "tallbroad"):
            motion_root = PACKAGE_ROOT / "Evidence/MotionPreviews"
            turn = self._frames(motion_root / f"captain-{family}-right-turn.webp")
            turn_widths = [frame.getchannel("A").getbbox()[2] -
                           frame.getchannel("A").getbbox()[0] for frame in turn]
            self.assertGreaterEqual(min(turn_widths), max(turn_widths) * 0.85)

            jump = self._frames(motion_root / f"captain-{family}-right-jump.webp")
            jump_bounds = [frame.getchannel("A").getbbox() for frame in jump]
            self.assertLessEqual(jump_bounds[-1][3], jump_bounds[0][3] - 25)
            self.assertLessEqual(
                jump_bounds[0][3] - jump_bounds[0][1],
                (jump_bounds[1][3] - jump_bounds[1][1]) * 0.95,
            )

            land = self._frames(motion_root / f"captain-{family}-right-land.webp")
            land_bounds = [frame.getchannel("A").getbbox() for frame in land]
            self.assertLessEqual(land_bounds[0][3], land_bounds[2][3] - 20)
            self.assertLessEqual(
                land_bounds[2][3] - land_bounds[2][1],
                (land_bounds[3][3] - land_bounds[3][1]) * 0.94,
            )

            climb = self._frames(motion_root / f"captain-{family}-right-climb.webp")
            for motion_frames in (jump, land, climb):
                for frame in motion_frames:
                    left, top, right, bottom = frame.getchannel("A").getbbox()
                    self.assertGreaterEqual(left, 2)
                    self.assertGreaterEqual(top, 2)
                    self.assertLessEqual(right, 254)
                    self.assertLessEqual(bottom, 382)

        for publication in manifest["publications"]:
            atlas_manifest = json.loads(
                (PACKAGE_ROOT / publication["atlasManifestPath"]).read_text(
                    encoding="utf-8"
                )
            )
            with Image.open(PACKAGE_ROOT / publication["atlasPath"]) as opened:
                atlas = opened.convert("RGBA")
            for clip_row, required_clip in ((3, "jump"), (4, "land")):
                clip = next(
                    item for item in atlas_manifest["clips"]
                    if f".{required_clip}." in item["id"]
                )
                frames = [
                    atlas.crop((
                        frame_index * 128,
                        clip_row * 192,
                        (frame_index + 1) * 128,
                        (clip_row + 1) * 192,
                    ))
                    for frame_index in range(len(clip["frames"]))
                ]
                self.assertTrue(
                    all(frame.getchannel("A").getbbox() for frame in frames),
                    f"{publication['id']} must remain present in {required_clip}.",
                )
                if publication["layerId"] in (
                        "head-hair", "backpack-equipment"):
                    alpha_areas = [
                        sum(
                            alpha * count
                            for alpha, count in enumerate(
                                frame.getchannel("A").histogram()
                            )
                        )
                        for frame in frames
                    ]
                    self.assertGreaterEqual(
                        min(alpha_areas),
                        max(alpha_areas) * 0.88,
                        f"{publication['id']} {required_clip} must articulate "
                        "without globally rescaling identity/equipment.",
                    )
            climb_clip = next(
                clip for clip in atlas_manifest["clips"]
                if ".climb." in clip["id"]
            )
            climb_anchors = [
                {
                    anchor["id"]: anchor["runtimePixels"]
                    for anchor in frame["anchors"]
                }
                for frame in climb_clip["frames"]
            ]
            for anchor_id, minimum_vertical_travel in (
                    ("LeftHand", 18.0),
                    ("RightHand", 18.0),
                    ("LeftFoot", 8.0),
                    ("RightFoot", 8.0)):
                vertical = [anchors[anchor_id][1] for anchors in climb_anchors]
                self.assertGreaterEqual(
                    max(vertical) - min(vertical),
                    minimum_vertical_travel,
                    f"{publication['id']} {anchor_id} must visibly travel.",
                )
            hand_peaks = [
                max(
                    range(len(climb_anchors)),
                    key=lambda index: climb_anchors[index][anchor_id][1],
                )
                for anchor_id in ("LeftHand", "RightHand")
            ]
            peak_separation = (
                hand_peaks[0] - hand_peaks[1]
            ) % len(climb_anchors)
            self.assertIn(
                peak_separation,
                (3, 4, 5),
                f"{publication['id']} climb hands must alternate reach peaks.",
            )

            with Image.open(PACKAGE_ROOT / publication["atlasPath"]) as atlas:
                atlas = atlas.convert("RGBA")
                climb_frames = [
                    atlas.crop((
                        frame_index * 128,
                        5 * 192,
                        (frame_index + 1) * 128,
                        6 * 192,
                    ))
                    for frame_index in range(8)
                ]
            self.assertGreaterEqual(
                len({frame.tobytes() for frame in climb_frames}),
                4,
                f"{publication['id']} climb layer must visibly animate.",
            )

            if publication["layerId"] == "body-base":
                run_clip = next(
                    clip for clip in atlas_manifest["clips"]
                    if ".run." in clip["id"]
                )
                boots_publication = modules[(
                    publication["family"],
                    publication["facing"],
                    "boots",
                )]
                with Image.open(
                        PACKAGE_ROOT / boots_publication["path"]) as opened:
                    boots_page = opened.convert("RGBA")
                for frame_index, frame in enumerate(run_clip["frames"]):
                    anchors = {
                        anchor["id"]: anchor["runtimePixels"]
                        for anchor in frame["anchors"]
                    }
                    foot_distance = (
                        (anchors["LeftFoot"][0] -
                         anchors["RightFoot"][0]) ** 2 +
                        (anchors["LeftFoot"][1] -
                         anchors["RightFoot"][1]) ** 2
                    ) ** 0.5
                    self.assertGreaterEqual(
                        foot_distance,
                        18.0,
                        f"{publication['id']} run frame {frame_index} must "
                        "keep both feet visibly separated at runtime scale "
                        "through horizontal stride or passing-pose lift.",
                    )
                    body_frame = atlas.crop((
                        frame_index * 128,
                        192,
                        (frame_index + 1) * 128,
                        384,
                    ))
                    body_components = self._significant_alpha_components(
                        body_frame
                    )
                    self.assertEqual(
                        len(body_components),
                        1,
                        f"{publication['id']} run frame {frame_index} must "
                        "keep both animated legs connected to the pelvis; "
                        f"observed component sizes {body_components}.",
                    )
                    default_boot_frame = boots_page.crop((
                        frame_index * 64,
                        96,
                        (frame_index + 1) * 64,
                        192,
                    )).resize((128, 192), Image.Resampling.NEAREST)
                    body_support = body_frame.getchannel("A").point(
                        lambda alpha: 255 if alpha >= 32 else 0
                    ).filter(ImageFilter.MaxFilter(9))
                    boot_alpha = default_boot_frame.getchannel("A").point(
                        lambda alpha: 255 if alpha >= 32 else 0
                    )
                    boot_pixels = sum(
                        1
                        for y in range(192)
                        for x in range(128)
                        if boot_alpha.getpixel((x, y))
                    )
                    supported_boot_pixels = sum(
                        1
                        for y in range(192)
                        for x in range(128)
                        if boot_alpha.getpixel((x, y)) and
                        body_support.getpixel((x, y))
                    )
                    self.assertGreater(boot_pixels, 0, publication["id"])
                    self.assertGreaterEqual(
                        supported_boot_pixels / boot_pixels,
                        0.94,
                        f"{publication['id']} run frame {frame_index} must "
                        "keep the published boot overlay registered to both "
                        "visible lower legs instead of drawing across empty "
                        "pixels and making a shin disappear.",
                    )
                run_row = next(
                    row for row in publication["sourceRows"]
                    if row["clipId"] == "run"
                )
                with Image.open(PACKAGE_ROOT / run_row["path"]) as opened:
                    run_strip = opened.convert("RGBA")
                for frame_index in (0, 4):
                    frame = run_strip.crop((
                        frame_index * 256,
                        0,
                        (frame_index + 1) * 256,
                        384,
                    ))
                    alpha = frame.getchannel("A")
                    bounds = alpha.getbbox()
                    self.assertIsNotNone(bounds, publication["id"])
                    lower_top = round(
                        bounds[1] + (bounds[3] - bounds[1]) * 0.55
                    )
                    visible_columns = []
                    for x in range(256):
                        visible = sum(
                            1
                            for y in range(lower_top, bounds[3])
                            if alpha.getpixel((x, y)) >= 32
                        )
                        if visible >= 3:
                            visible_columns.append(x)
                    lower_span = max(visible_columns) - min(visible_columns) + 1
                    frame_span = bounds[2] - bounds[0]
                    self.assertGreaterEqual(
                        lower_span / frame_span,
                        0.95,
                        f"{publication['id']} run contact frame {frame_index} "
                        "must preserve a broad two-leg stride silhouette at "
                        "mobile scale instead of collapsing into one limb.",
                    )
            for clip in atlas_manifest["clips"]:
                for frame in clip["frames"]:
                    for anchor in frame["anchors"]:
                        x, y = anchor["runtimePixels"]
                        self.assertGreaterEqual(x, 0.0)
                        self.assertLessEqual(x, 128.0)
                        self.assertGreaterEqual(y, 0.0)
                        self.assertLessEqual(y, 192.0)

    def test_contract_declares_exact_launch_matrix_and_event_semantics(self):
        with tempfile.TemporaryDirectory(prefix="jss-captain-contract-") as temporary:
            output = Path(temporary) / "contract.json"
            result = self._run("--write-contract", str(output))

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            contract = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(contract["schemaVersion"], 1)
            self.assertEqual(contract["cadenceFps"], 12)
            self.assertEqual(
                contract["families"],
                {
                    "Compact": {
                        "heightMeters": 1.46,
                        "heightPixels": 146,
                        "silhouetteWidthScale": 1.0,
                    },
                    "Average": {
                        "heightMeters": 1.56,
                        "heightPixels": 156,
                        "silhouetteWidthScale": 1.0,
                    },
                    "TallBroad": {
                        "heightMeters": 1.66,
                        "heightPixels": 166,
                        "silhouetteWidthScale": 1.0,
                    },
                },
            )
            self.assertEqual(contract["facings"], ["right", "left"])
            self.assertEqual(
                {
                    clip["id"]: (clip["frameCount"], clip["loopMode"])
                    for clip in contract["clips"]
                },
                {
                    "idle": (4, "Loop"),
                    "run": (8, "Loop"),
                    "turn": (4, "Once"),
                    "jump": (6, "HoldLast"),
                    "land": (4, "Once"),
                    "climb": (8, "Loop"),
                    "scan": (8, "Once"),
                    "interact": (6, "Once"),
                },
            )
            self.assertEqual(contract["compositeClipCount"], 48)
            self.assertEqual(contract["compositeFrameCount"], 288)
            scan = next(clip for clip in contract["clips"] if clip["id"] == "scan")
            self.assertEqual(
                scan["events"],
                {
                    "1": ["ToolAttach:scan-tool-attach"],
                    "4": [
                        "Interaction:scan-commit",
                        "Audio:scan-audio",
                        "Vfx:scan-vfx",
                    ],
                    "6": ["ToolDetach:scan-tool-detach"],
                },
            )

    def test_contract_caps_layers_and_covers_every_approved_launch_option(self):
        with tempfile.TemporaryDirectory(prefix="jss-captain-contract-") as temporary:
            output = Path(temporary) / "contract.json"
            result = self._run("--write-contract", str(output))
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            contract = json.loads(output.read_text(encoding="utf-8"))

            self.assertEqual(
                contract["layers"],
                [
                    "body-base",
                    "head-hair",
                    "silhouette-costume",
                    "backpack-equipment",
                    "foreground-hand-tool",
                ],
            )
            self.assertEqual(contract["proofSourceRowCount"], 240)
            self.assertEqual(contract["publicationCount"], 30)
            catalog = contract["catalog"]
            self.assertEqual(len(catalog["facePresets"]), 6)
            self.assertEqual(len(catalog["skinSwatches"]), 8)
            self.assertEqual(len(catalog["eyeShapes"]), 6)
            self.assertEqual(len(catalog["irisColors"]), 6)
            self.assertEqual(len(catalog["hairShapes"]), 8)
            self.assertEqual(len(catalog["hairColors"]), 9)
            self.assertEqual(len(catalog["suitColorways"]), 6)
            self.assertEqual(len(catalog["patches"]), 6)
            self.assertEqual(len(catalog["accessories"]), 5)
            self.assertEqual(len(catalog["gloves"]), 3)
            self.assertEqual(len(catalog["boots"]), 3)
            self.assertEqual(len(catalog["helmets"]), 3)
            self.assertEqual(len(catalog["backpacks"]), 3)
            self.assertEqual(
                catalog["signalStates"],
                ["dormant", "active-cyan", "resonance-violet"],
            )
            self.assertEqual(
                contract["anchors"],
                [
                    "Root",
                    "LeftFoot",
                    "RightFoot",
                    "LeftHand",
                    "RightHand",
                    "HelmetRing",
                    "BackpackSocket",
                    "Belt",
                    "LeftWrist",
                    "RightWrist",
                    "LeftBootTop",
                    "RightBootTop",
                    "ActiveTool",
                    "StowedTool",
                ],
            )

    def test_master_preflight_binds_exact_authority_and_family_extraction(self):
        with tempfile.TemporaryDirectory(prefix="jss-captain-preflight-") as temporary:
            report = Path(temporary) / "preflight.json"
            result = self._run(
                "--preflight-master",
                str(MASTER),
                "--write-report",
                str(report),
            )

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            payload = json.loads(report.read_text(encoding="utf-8"))
            self.assertEqual(
                payload["masterSha256"],
                "41425b612a0897fb7ea0dcc9c3bbb7fb9a9aa10ac5424a5b898901bc0c95c54e",
            )
            self.assertEqual(payload["sourceSize"], [1672, 941])
            self.assertEqual(payload["familyCount"], 3)
            self.assertEqual(
                [family["id"] for family in payload["families"]],
                ["Compact", "Average", "TallBroad"],
            )
            self.assertTrue(all(family["isolated"] for family in payload["families"]))
            self.assertTrue(all(family["facesRight"] for family in payload["families"]))
            self.assertTrue(payload["transparentCorners"])

    def test_average_right_preview_build_has_all_five_layers_and_eight_motions(self):
        with tempfile.TemporaryDirectory(prefix="jss-captain-preview-") as temporary:
            output = Path(temporary) / "preview"
            result = self._run(
                "--build-preview",
                "--preflight-master",
                str(MASTER),
                "--output",
                str(output),
                "--family",
                "Average",
                "--facing",
                "right",
            )

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            manifest = json.loads(
                (output / "captain-average-right-preview.json")
                .read_text(encoding="utf-8")
            )
            self.assertEqual(manifest["family"], "Average")
            self.assertEqual(manifest["facing"], "right")
            self.assertEqual(manifest["layerCount"], 5)
            self.assertEqual(manifest["sourceRowCount"], 40)
            self.assertEqual(manifest["runtimeRowCount"], 40)
            self.assertEqual(len(manifest["motionPreviews"]), 8)
            self.assertEqual(
                [entry["clipId"] for entry in manifest["motionPreviews"]],
                [clip["id"] for clip in CLIP_ORDER],
            )
            for entry in manifest["rows"]:
                source_path = output / entry["sourcePath"]
                runtime_path = output / entry["runtimePath"]
                with Image.open(source_path) as source:
                    self.assertEqual(
                        source.size,
                        (entry["frameCount"] * 256, 384),
                    )
                with Image.open(runtime_path) as runtime:
                    self.assertEqual(
                        runtime.size,
                        (entry["frameCount"] * 128, 192),
                    )
            with Image.open(output / manifest["compositeContactSheet"]) as contact:
                self.assertGreaterEqual(contact.width, 1024)
                self.assertGreaterEqual(contact.height, 8 * 192)

    def test_full_package_publishes_exact_family_facing_layer_matrix(self):
        with tempfile.TemporaryDirectory(prefix="jss-captain-package-") as temporary:
            output = Path(temporary) / "package"
            preserved_atlas = (
                output / "Atlases" / "Compact" / "right" /
                "captain-compact-right-body-base.spriteatlas"
            )
            preserved_atlas.parent.mkdir(parents=True)
            preserved_atlas.write_text("unity-owned-atlas\n", encoding="utf-8")
            preserved_meta = preserved_atlas.with_suffix(".spriteatlas.meta")
            preserved_meta.write_text("guid: preserved\n", encoding="utf-8")
            result = self._run(
                "--build-package",
                "--preflight-master",
                str(MASTER),
                "--output",
                str(output),
            )

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual(
                preserved_atlas.read_text(encoding="utf-8"),
                "unity-owned-atlas\n",
            )
            self.assertEqual(
                preserved_meta.read_text(encoding="utf-8"),
                "guid: preserved\n",
            )
            manifest_path = output / "captain-sprite-package.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.assertEqual(manifest["schemaVersion"], 1)
            self.assertEqual(manifest["familyCount"], 3)
            self.assertEqual(manifest["facingCount"], 2)
            self.assertEqual(manifest["layerCount"], 5)
            self.assertEqual(manifest["publicationCount"], 30)
            self.assertEqual(manifest["proofSourceRowCount"], 240)
            self.assertEqual(manifest["paletteMaskRowCount"], 240)
            self.assertEqual(len(manifest["palettePublications"]), 30)
            self.assertEqual(manifest["modulePublicationCount"], 66)
            self.assertEqual(len(manifest["modulePublications"]), 66)
            self.assertEqual(manifest["moduleOptionFrameCount"], 15264)
            self.assertEqual(manifest["compositeClipCount"], 48)
            self.assertEqual(manifest["compositeFrameCount"], 288)
            self.assertEqual(len(manifest["publications"]), 30)
            self.assertEqual(len(manifest["motionPreviews"]), 48)
            self.assertEqual(len(manifest["contactSheets"]), 6)
            self.assertEqual(len(manifest["rawPublicationSheets"]), 30)
            self.assertEqual(len(manifest["authorityHashes"]), 8)
            self.assertTrue(
                (output / manifest["customizationMatrix"]["path"]).is_file()
            )
            self.assertTrue(
                (output / manifest["attachmentMatrix"]["path"]).is_file()
            )
            self.assertEqual(manifest["anchors"], [
                "Root", "LeftFoot", "RightFoot", "LeftHand", "RightHand",
                "HelmetRing", "BackpackSocket", "Belt", "LeftWrist",
                "RightWrist", "LeftBootTop", "RightBootTop", "ActiveTool",
                "StowedTool",
            ])

            observed = {
                (entry["family"], entry["facing"], entry["layerId"])
                for entry in manifest["publications"]
            }
            expected = {
                (family, facing, layer)
                for family in ("Compact", "Average", "TallBroad")
                for facing in ("right", "left")
                for layer in (
                    "body-base", "head-hair", "silhouette-costume",
                    "backpack-equipment", "foreground-hand-tool",
                )
            }
            self.assertEqual(observed, expected)
            for entry in manifest["publications"]:
                self.assertEqual(len(entry["sourceRows"]), 8)
                for row in entry["sourceRows"]:
                    self.assertTrue((output / row["paletteMaskPath"]).is_file())
                atlas_path = output / entry["atlasPath"]
                atlas_manifest_path = output / entry["atlasManifestPath"]
                atlas_hash_path = output / entry["atlasManifestHashPath"]
                with Image.open(atlas_path) as atlas:
                    self.assertLessEqual(atlas.width, 2048)
                    self.assertLessEqual(atlas.height, 2048)
                    self.assertEqual(atlas.size, (1024, 1536))
                self.assertTrue(atlas_manifest_path.is_file())
                self.assertTrue(atlas_hash_path.is_file())
                atlas_manifest = json.loads(
                    atlas_manifest_path.read_text(encoding="utf-8")
                )
                self.assertTrue(all(
                    len(frame["anchors"]) == 14
                    for clip in atlas_manifest["clips"]
                    for frame in clip["frames"]
                ))
            for entry in manifest["contactSheets"]:
                with Image.open(output / entry["path"]) as contact:
                    self.assertEqual(contact.size, (1024, 1536))
            for entry in manifest["palettePublications"]:
                with Image.open(output / entry["path"]) as palette:
                    self.assertEqual(palette.size, (1024, 1536))
            for entry in manifest["modulePublications"]:
                with Image.open(output / entry["path"]) as module_page:
                    self.assertEqual(module_page.size, (2048, 1536))
                    self.assertIsNotNone(module_page.getchannel("A").getbbox())
                self.assertEqual(len(entry["options"]), len(
                    manifest["catalog"][entry["category"]]
                ))
                self.assertTrue(output.joinpath(entry["manifestPath"]).is_file())
                self.assertTrue(all(
                    option["clipCount"] == 8 and option["frameCount"] == 48
                    for option in entry["options"]
                ))

            average_right = [
                entry for entry in manifest["modulePublications"]
                if entry["family"] == "Average" and entry["facing"] == "right"
            ]
            self.assertEqual(len(average_right), 11)
            self.assertEqual(
                len({entry["sha256"] for entry in average_right}),
                len(average_right),
            )

            body_publications = [
                entry for entry in manifest["publications"]
                if entry["layerId"] == "body-base" and entry["facing"] == "right"
            ]
            widths = {}
            for entry in body_publications:
                atlas_manifest = json.loads(
                    output.joinpath(entry["atlasManifestPath"]).read_text(
                        encoding="utf-8"
                    )
                )
                frame = atlas_manifest["clips"][0]["frames"][0]
                widths[entry["family"]] = (
                    frame["alphaBoundsPixels"][2] -
                    frame["alphaBoundsPixels"][0]
                )
            self.assertLess(widths["Compact"], widths["TallBroad"])

            turn = next(
                item for item in manifest["motionPreviews"]
                if item["family"] == "Average" and item["facing"] == "right" and
                item["clipId"] == "turn"
            )
            climb = next(
                item for item in manifest["motionPreviews"]
                if item["family"] == "Average" and item["facing"] == "right" and
                item["clipId"] == "climb"
            )
            self.assertNotEqual(turn["sha256"], climb["sha256"])

            average_manifest = json.loads(
                output.joinpath(
                    "Atlases/Average/right/"
                    "captain-average-right-body-base.sprite-manifest.json"
                ).read_text(encoding="utf-8")
            )
            turn_clip = next(
                clip for clip in average_manifest["clips"]
                if ".turn." in clip["id"]
            )
            turn_widths = [
                frame["alphaBoundsPixels"][2] - frame["alphaBoundsPixels"][0]
                for frame in turn_clip["frames"]
            ]
            self.assertGreaterEqual(min(turn_widths), max(turn_widths) * 0.85)
            self.assertEqual(
                [frame["contacts"] for frame in turn_clip["frames"]],
                [
                    ["LeftFoot", "RightFoot"],
                    ["RightFoot"],
                    ["RightFoot"],
                    ["LeftFoot", "RightFoot"],
                ],
            )
            climb_clip = next(
                clip for clip in average_manifest["clips"]
                if ".climb." in clip["id"]
            )
            climb_events = [
                event["id"]
                for frame in climb_clip["frames"]
                for event in frame["events"]
                if event["kind"] == "FootContact"
            ]
            self.assertEqual(climb_events, ["step-right", "step-left"])
            for frame_index in (1, 2, 3):
                self.assertIn(
                    "RightFoot",
                    climb_clip["frames"][frame_index]["contacts"],
                )
            for frame_index in (5, 6, 7):
                self.assertIn(
                    "LeftFoot",
                    climb_clip["frames"][frame_index]["contacts"],
                )

    def _run(self, *arguments):
        return subprocess.run(
            [sys.executable, str(GENERATOR), *arguments],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    @staticmethod
    def _frames(path):
        frames = []
        with Image.open(path) as image:
            for frame_index in range(image.n_frames):
                image.seek(frame_index)
                frames.append(image.convert("RGBA"))
        return frames

    @staticmethod
    def _significant_alpha_components(image, threshold=32, minimum_pixels=8):
        alpha = image.getchannel("A")
        pixels = alpha.load()
        width, height = alpha.size
        visited = set()
        components = []
        for y in range(height):
            for x in range(width):
                if (x, y) in visited or pixels[x, y] < threshold:
                    continue
                pending = [(x, y)]
                visited.add((x, y))
                count = 0
                while pending:
                    current_x, current_y = pending.pop()
                    count += 1
                    for offset_y in (-1, 0, 1):
                        for offset_x in (-1, 0, 1):
                            neighbour = (
                                current_x + offset_x,
                                current_y + offset_y,
                            )
                            if (
                                    0 <= neighbour[0] < width and
                                    0 <= neighbour[1] < height and
                                    neighbour not in visited and
                                    pixels[neighbour[0], neighbour[1]] >= threshold):
                                visited.add(neighbour)
                                pending.append(neighbour)
                if count >= minimum_pixels:
                    components.append(count)
        return sorted(components, reverse=True)


if __name__ == "__main__":
    unittest.main()
