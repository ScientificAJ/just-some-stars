import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageChops, ImageOps


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
GENERATOR = REPOSITORY_ROOT / "tools" / "sprites" / "create_crew_package.py"
REFERENCE_ROOT = (
    REPOSITORY_ROOT
    / "Assets/_JustSomeStars/Art/Characters/References"
)
SOURCE_ROOT = (
    REPOSITORY_ROOT
    / "Assets/_JustSomeStars/Art/2D/Characters"
)
CHARACTERS = {
    "Mira": ("mira", 1.54, "7d7ebbbe494ba1899e5d0f4549e5f363ac58a00e75fadf8c25524989524787a5"),
    "Juno": ("juno", 1.48, "b46e416d4ac0901efdf9dbcd5eff244e8f930f64e117fd5a8c753053930da722"),
    "Kai": ("kai", 1.62, "d53aa52b5cdefaa63988394a94f92e4a3471fd0d48c66f69aa4eeb2227ee8396"),
    "Bea": ("bea", 1.51, "28927ab81d62f07deb7fc7a327d19e4fe60f4fd054f98a1a6cccc481eb4ba0e0"),
    "Ori": ("ori", 0.68, "16e0b936ff7b93b3ef28a1f4bab862c7a78981e2a3c765cb6ea09e05527b22ce"),
}
CLIPS = {
    "idle": 4,
    "run": 8,
    "turn": 4,
    "jump": 6,
    "land": 4,
    "climb": 8,
    "scan": 8,
    "interact": 6,
}
EXPRESSIONS = [
    "neutral", "happy", "curious", "worried", "afraid",
    "surprised", "determined", "sad", "blink", "speaking",
]


class CrewSpritePackageTests(unittest.TestCase):
    def test_contract_pins_bespoke_identity_motion_and_mechanical_ori(self):
        with tempfile.TemporaryDirectory(prefix="jss-crew-contract-") as temporary:
            contract_path = Path(temporary) / "contract.json"
            result = self._run("--write-contract", str(contract_path))

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            contract = json.loads(contract_path.read_text(encoding="utf-8"))
            self.assertEqual(contract["schemaVersion"], 1)
            self.assertEqual(contract["cadenceFps"], 12)
            self.assertEqual(contract["facings"], ["right", "left"])
            self.assertEqual(contract["clips"], CLIPS)
            self.assertEqual(contract["expressions"], EXPRESSIONS)
            self.assertEqual(contract["speechShapes"], 6)
            self.assertEqual(set(contract["characters"]), set(CHARACTERS))
            self.assertEqual(contract["characters"]["Ori"]["rigKind"], "Mechanical")
            self.assertEqual(
                contract["characters"]["Ori"]["dimensionsMeters"],
                [0.54, 0.68, 0.58],
            )
            for name, (_, height, reference_hash) in CHARACTERS.items():
                identity = contract["characters"][name]
                self.assertEqual(identity["heightMeters"], height)
                self.assertEqual(identity["referenceSha256"], reference_hash)
                self.assertTrue(identity["roleMotionId"])
                self.assertTrue(identity["anchors"])

    def test_build_publishes_every_character_facing_clip_face_and_anchor(self):
        with tempfile.TemporaryDirectory(prefix="jss-crew-package-") as temporary:
            output = Path(temporary) / "Crew"
            result = self._run(
                "--build",
                "--reference-root", str(REFERENCE_ROOT),
                "--source-root", str(SOURCE_ROOT),
                "--output", str(output),
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

            alpha_fingerprints = {}
            semantic_issues = []
            for name, (character_id, height, reference_hash) in CHARACTERS.items():
                root = output / name
                package = json.loads(
                    (root / f"{character_id}-sprite-package.json").read_text(
                        encoding="utf-8"
                    )
                )
                self.assertEqual(package["characterId"], character_id)
                self.assertEqual(package["approvedHeightMeters"], height)
                self.assertEqual(package["authority"]["referenceSha256"], reference_hash)
                self.assertEqual(
                    {"right", "left"},
                    set(package["authority"]["climbActorOnlySha256"]),
                    f"{name} must pin two approved actor-only climb sources.",
                )
                if name == "Bea":
                    self.assertEqual(
                        {"right", "left"},
                        set(package["authority"]["scanActorOnlySha256"]),
                        "Bea must pin two approved full-body scan sources.",
                    )
                self.assertEqual(package["publicationCount"], 3)
                self.assertEqual(len(package["evidence"]), 13)
                evidence_paths = {entry["path"] for entry in package["evidence"]}
                self.assertIn(
                    f"Evidence/{character_id}-live-anchors-right.webp",
                    evidence_paths,
                )
                self.assertIn(
                    f"Evidence/{character_id}-live-anchors-left.webp",
                    evidence_paths,
                )
                self.assertNotIn(
                    f"Evidence/{character_id}-attachment-anchor-matrix.png",
                    evidence_paths,
                )
                source_contract_path = root / package["authority"]["sourceContractPath"]
                self.assertEqual(
                    self._sha256(source_contract_path),
                    package["authority"]["sourceContractSha256"],
                )
                source_contract = json.loads(
                    source_contract_path.read_text(encoding="utf-8")
                )
                self.assertEqual(
                    source_contract["rightMotionSheetSha256"],
                    package["authority"]["rightMotionSheetSha256"],
                )
                self.assertEqual(
                    source_contract["leftMotionSheetSha256"],
                    package["authority"]["leftMotionSheetSha256"],
                )

                facing_atlases = {}
                for facing in ("right", "left"):
                    manifest_path = (
                        root / "Atlases" / facing /
                        f"{character_id}-{facing}.sprite-manifest.json"
                    )
                    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                    self.assertEqual(
                        [clip["id"] for clip in manifest["clips"]],
                        [f"{character_id}.{clip}.{facing}" for clip in CLIPS],
                    )
                    published_frames = {}
                    for clip in manifest["clips"]:
                        clip_id = clip["id"].split(".")[1]
                        self.assertEqual(len(clip["frames"]), CLIPS[clip_id])
                        if clip_id == "climb":
                            self.assertEqual(clip["loopMode"], "Once")
                            self.assertEqual(
                                clip["sceneGeometryPolicy"],
                                "external-only",
                            )
                        source_strip = Image.open(root / clip["sourceStrip"]).convert(
                            "RGBA"
                        )
                        frame_fingerprints = []
                        published_frames[clip_id] = []
                        for frame in clip["frames"]:
                            self.assertEqual(
                                {anchor["id"] for anchor in frame["anchors"]},
                                set(package["anchors"]),
                            )
                            source_frame = source_strip.crop((
                                frame["index"] * 256,
                                0,
                                (frame["index"] + 1) * 256,
                                384,
                            ))
                            alpha = source_frame.getchannel("A")
                            published_frames[clip_id].append(source_frame)
                            bounds = alpha.getbbox()
                            self.assertIsNotNone(bounds)
                            self.assertGreaterEqual(bounds[2] - bounds[0], 18)
                            self.assertGreaterEqual(bounds[3] - bounds[1], 34)
                            frame_fingerprints.append(
                                hashlib.sha256(alpha.tobytes()).hexdigest()
                            )
                            for anchor in frame["anchors"]:
                                self.assertEqual(
                                    anchor["semanticBasis"],
                                    "authored-frame-v1",
                                )
                                self.assertIsInstance(
                                    anchor["isAuthoredVisible"],
                                    bool,
                                )
                                self.assertEqual(
                                    len(anchor["semanticRegionNormalized"]),
                                    4,
                                )
                                region = anchor["semanticRegionNormalized"]
                                normalized_x = anchor["sourcePixels"][0] / 256.0
                                normalized_y = (
                                    384.0 - anchor["sourcePixels"][1]
                                ) / 384.0
                                self.assertGreaterEqual(normalized_x, region[0])
                                self.assertLessEqual(normalized_x, region[2])
                                self.assertGreaterEqual(normalized_y, region[1])
                                self.assertLessEqual(normalized_y, region[3])
                                x = round(anchor["sourcePixels"][0])
                                y = round(384 - anchor["sourcePixels"][1])
                                self.assertGreaterEqual(
                                    alpha.getpixel((x, y)),
                                    36,
                                    f"{clip['id']} frame {frame['index']} "
                                    f"anchor {anchor['id']} is detached from pixels.",
                                )
                            if clip_id == "climb":
                                self.assertEqual(
                                    frame["authoredPoseRole"],
                                    "actor-only",
                                )
                            if clip_id == "jump" and frame["index"] > 0:
                                self.assertFalse(
                                    set(frame["contacts"])
                                    & {
                                        "LeftFoot", "RightFoot",
                                        "LeftWheelContact", "RightWheelContact",
                                    },
                                    f"{clip['id']} frame {frame['index']} is airborne.",
                                )
                            if clip_id == "scan":
                                event_kinds = {
                                    event["kind"] for event in frame["events"]
                                }
                                self.assertNotIn("ToolAttach", event_kinds)
                                self.assertNotIn("ToolDetach", event_kinds)
                                active_tool = next(
                                    anchor for anchor in frame["anchors"]
                                    if anchor["id"] == "ActiveTool"
                                )
                                self.assertTrue(active_tool["isAuthoredVisible"])
                        if clip_id not in ("idle", "land"):
                            self.assertGreaterEqual(
                                len(set(frame_fingerprints)),
                                max(3, len(frame_fingerprints) - 2),
                                f"{clip['id']} lacks authored motion variation.",
                            )

                    jump_fingerprints = {
                        hashlib.sha256(
                            frame.getchannel("A").tobytes()
                        ).hexdigest()
                        for frame in published_frames["jump"]
                    }
                    climb_fingerprints = [
                        hashlib.sha256(
                            frame.getchannel("A").tobytes()
                        ).hexdigest()
                        for frame in published_frames["climb"]
                    ]
                    if not jump_fingerprints.isdisjoint(climb_fingerprints):
                        semantic_issues.append(
                            f"{character_id}.{facing} climb reuses jump silhouettes."
                        )
                    climb_pose_signatures = {
                        self._normalized_alpha_signature(frame)
                        for frame in published_frames["climb"]
                    }
                    if len(climb_pose_signatures) < 6:
                        semantic_issues.append(
                            f"{character_id}.{facing} climb has only "
                            f"{len(climb_pose_signatures)} distinct actor poses; "
                            "translation is not authored ascent."
                        )
                    climb_centers = []
                    for frame in published_frames["climb"]:
                        bounds = frame.getchannel("A").getbbox()
                        self.assertIsNotNone(bounds)
                        climb_centers.append((bounds[1] + bounds[3]) / 2.0)
                    if min(climb_centers[3:6]) > climb_centers[0] - 16.0:
                        semantic_issues.append(
                            f"{character_id}.{facing} climb never gains height."
                        )
                    climb = next(
                        clip for clip in manifest["clips"]
                        if clip["id"] == f"{character_id}.climb.{facing}"
                    )
                    climb_commits = [
                        frame["index"]
                        for frame in climb["frames"]
                        if any(
                            event["id"] == "climb-commit"
                            for event in frame["events"]
                        )
                    ]
                    if climb_commits != [5]:
                        semantic_issues.append(
                            f"{character_id}.{facing} climb commit is "
                            f"{climb_commits}, expected [5]."
                        )
                    grip_ids = (
                        {"LeftGripper", "RightGripper"}
                        if name == "Ori"
                        else {"LeftHand", "RightHand"}
                    )
                    for frame in climb["frames"][2:6]:
                        if not set(frame["contacts"]) & grip_ids:
                            semantic_issues.append(
                                f"{climb['id']} frame {frame['index']} "
                                "has no grip contact."
                            )

                    scan_fingerprints = {
                        hashlib.sha256(
                            frame.getchannel("A").tobytes()
                        ).hexdigest()
                        for frame in published_frames["scan"]
                    }
                    interact_fingerprints = {
                        hashlib.sha256(
                            frame.getchannel("A").tobytes()
                        ).hexdigest()
                        for frame in published_frames["interact"]
                    }
                    if not scan_fingerprints.isdisjoint(interact_fingerprints):
                        semantic_issues.append(
                            f"{character_id}.{facing} interact reuses scan frames."
                        )

                    idle_bounds = [
                        frame.getchannel("A").getbbox()
                        for frame in published_frames["idle"]
                    ]
                    if name == "Juno":
                        interact_bounds = [
                            frame.getchannel("A").getbbox()
                            for frame in published_frames["interact"]
                        ]
                        idle_width = max(
                            bounds[2] - bounds[0] for bounds in idle_bounds
                        )
                        idle_height = max(
                            bounds[3] - bounds[1] for bounds in idle_bounds
                        )
                        self.assertLessEqual(
                            max(bounds[2] - bounds[0] for bounds in interact_bounds),
                            idle_width * 1.80,
                            "Juno interact changes apparent character scale.",
                        )
                        self.assertLessEqual(
                            max(bounds[3] - bounds[1] for bounds in interact_bounds),
                            idle_height * 0.80,
                            "Juno's crouched interact must read shorter than idle.",
                        )
                    if name == "Bea":
                        scan_bounds = [
                            frame.getchannel("A").getbbox()
                            for frame in published_frames["scan"]
                        ]
                        idle_height = max(
                            bounds[3] - bounds[1] for bounds in idle_bounds
                        )
                        self.assertGreaterEqual(
                            min(bounds[3] - bounds[1] for bounds in scan_bounds),
                            idle_height * 0.88,
                            "Bea scan frames crop away her lower body.",
                        )

                    interact = next(
                        clip for clip in manifest["clips"]
                        if clip["id"] == f"{character_id}.interact.{facing}"
                    )
                    leading_contact = (
                        "RightGripper" if facing == "right" else "LeftGripper"
                    ) if name == "Ori" else (
                        "RightHand" if facing == "right" else "LeftHand"
                    )
                    owns_external_target = name in ("Mira", "Juno", "Ori")
                    if owns_external_target and (
                        interact["sceneGeometryPolicy"] != "external-only"
                    ):
                        semantic_issues.append(
                            f"{interact['id']} does not keep its scene target external."
                        )
                    for frame in interact["frames"]:
                        if owns_external_target and (
                            frame["authoredPoseRole"]
                            != "actor-with-approved-equipment"
                        ):
                            semantic_issues.append(
                                f"{interact['id']} frame {frame['index']} owns "
                                "non-character interaction geometry."
                            )
                        if owns_external_target and (
                            frame["contactAuthority"]
                            != "external-scene-resolver"
                        ):
                            semantic_issues.append(
                                f"{interact['id']} frame {frame['index']} does not "
                                "resolve contact against scene geometry."
                            )
                        if (
                            owns_external_target
                            and 1 <= frame["index"] <= 4
                            and leading_contact not in frame["contacts"]
                        ):
                            semantic_issues.append(
                                f"{interact['id']} frame {frame['index']} lacks the "
                                "visible interaction contact."
                            )
                    if owns_external_target:
                        source_hashes = package["authority"][
                            "interactionActorOnlySha256"
                        ]
                        self.assertEqual({"right", "left"}, set(source_hashes))
                        source_path = (
                            root / "Source" /
                            f"{character_id}-{facing}-interact-actor-only-v2.png"
                        )
                        self.assertEqual(
                            source_hashes[facing],
                            hashlib.sha256(source_path.read_bytes()).hexdigest(),
                        )

                    if name == "Ori":
                        for frame in climb["frames"]:
                            anchors = {
                                anchor["id"]: anchor
                                for anchor in frame["anchors"]
                            }
                            for gripper_id in ("LeftGripper", "RightGripper"):
                                front_gripper = (
                                    "RightGripper"
                                    if facing == "right"
                                    else "LeftGripper"
                                )
                                expected_visible = (
                                    2 <= frame["index"] <= 6
                                    and gripper_id == front_gripper
                                )
                                if (
                                    anchors[gripper_id]["isAuthoredVisible"]
                                    != expected_visible
                                ):
                                    semantic_issues.append(
                                        f"{climb['id']} frame {frame['index']} "
                                        f"{gripper_id} visibility does not match "
                                        f"the visible side-on gripper ({expected_visible})."
                                    )
                        for frame in interact["frames"]:
                            anchors = {
                                anchor["id"]: anchor
                                for anchor in frame["anchors"]
                            }
                            front_gripper = (
                                "RightGripper"
                                if facing == "right"
                                else "LeftGripper"
                            )
                            rear_gripper = (
                                "LeftGripper"
                                if facing == "right"
                                else "RightGripper"
                            )
                            if not anchors[front_gripper]["isAuthoredVisible"]:
                                semantic_issues.append(
                                    f"{interact['id']} frame {frame['index']} hides "
                                    "its drawn gripper."
                                )
                            if anchors[rear_gripper]["isAuthoredVisible"]:
                                semantic_issues.append(
                                    f"{interact['id']} frame {frame['index']} invents "
                                    "a hidden rear gripper."
                                )
                            for scanner_id in ("SecondaryScanner", "ActiveTool"):
                                if anchors[scanner_id]["isAuthoredVisible"]:
                                    semantic_issues.append(
                                        f"{interact['id']} frame {frame['index']} "
                                        f"mislabels the gripper as {scanner_id}."
                                    )
                    land = next(
                        clip for clip in manifest["clips"]
                        if clip["id"] == f"{character_id}.land.{facing}"
                    )
                    self.assertFalse(land["frames"][0]["contacts"])
                    self.assertTrue(land["frames"][-1]["contacts"])
                    self.assertTrue(
                        any(frame["contacts"] for frame in land["frames"][1:]),
                        f"{land['id']} never establishes a visible landing contact.",
                    )
                    atlas_path = root / "Atlases" / facing / f"{character_id}-{facing}.png"
                    with Image.open(atlas_path) as opened:
                        atlas = opened.convert("RGBA")
                    self.assertIsNotNone(atlas.getchannel("A").getbbox())
                    facing_atlases[facing] = atlas

                mirrored_right = ImageOps.mirror(facing_atlases["right"])
                self.assertIsNotNone(
                    ImageChops.difference(
                        mirrored_right,
                        facing_atlases["left"],
                    ).getbbox(),
                    f"{name} left motion must be independently authored, not mirrored.",
                )

                neutral = json.loads(
                    (root / "Atlases/neutral" /
                     f"{character_id}-face-speech.sprite-manifest.json").read_text(
                        encoding="utf-8"
                    )
                )
                self.assertEqual(
                    [clip["id"] for clip in neutral["clips"]],
                    [f"{character_id}.expression.{item}" for item in EXPRESSIONS]
                    + [f"{character_id}.speech.{index}" for index in range(6)],
                )
                with Image.open(root / "Source" / f"{character_id}-right-master.png") as opened:
                    master = opened.convert("RGBA")
                alpha_fingerprints[name] = hashlib.sha256(
                    master.getchannel("A").tobytes()
                ).hexdigest()

            self.assertEqual(
                len(set(alpha_fingerprints.values())),
                len(CHARACTERS),
                "Crew sources must be genuinely bespoke silhouettes, not recolors.",
            )
            self.assertEqual([], semantic_issues, "\n".join(semantic_issues))

    def test_invalid_authority_fails_closed_and_cannot_leave_stale_success(self):
        with tempfile.TemporaryDirectory(prefix="jss-crew-fail-closed-") as temporary:
            temporary = Path(temporary)
            references = temporary / "References"
            output = temporary / "Crew"
            references.mkdir()
            for source in REFERENCE_ROOT.glob("*.png"):
                (references / source.name).write_bytes(source.read_bytes())
            result = self._run(
                "--build",
                "--reference-root", str(references),
                "--source-root", str(SOURCE_ROOT),
                "--output", str(output),
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertTrue(output.exists())

            tampered = references / "mira.png"
            tampered.write_bytes(tampered.read_bytes() + b"tampered")
            result = self._run(
                "--build",
                "--reference-root", str(references),
                "--source-root", str(SOURCE_ROOT),
                "--output", str(output),
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse(output.exists())
            self.assertFalse(Path(str(output) + ".staging").exists())

    def _run(self, *arguments):
        return subprocess.run(
            [sys.executable, str(GENERATOR), *arguments],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )

    @staticmethod
    def _normalized_alpha_signature(frame):
        alpha = frame.getchannel("A")
        bounds = alpha.getbbox()
        if bounds is None:
            return "empty"
        crop = alpha.crop(bounds)
        payload = (
            crop.width.to_bytes(2, "big")
            + crop.height.to_bytes(2, "big")
            + crop.tobytes()
        )
        return hashlib.sha256(payload).hexdigest()

    @staticmethod
    def _sha256(path):
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
