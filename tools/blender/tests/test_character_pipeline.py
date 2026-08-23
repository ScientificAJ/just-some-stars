import hashlib
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

import bmesh
import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[3]
BLENDER_TOOLS = PROJECT_ROOT / "tools" / "blender"
if str(BLENDER_TOOLS) not in sys.path:
    sys.path.insert(0, str(BLENDER_TOOLS))

import export_unity_fbx
import generate_lods
import jss_scene_setup
import validate_character


class CharacterPipelineTests(unittest.TestCase):
    def setUp(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.artifact_root = PROJECT_ROOT / "Builds" / "Task11" / "PythonTests"
        self.artifact_root.mkdir(parents=True, exist_ok=True)
        self.temp_directory = Path(
            tempfile.mkdtemp(prefix="case-", dir=self.artifact_root)
        )

    def tearDown(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        shutil.rmtree(self.temp_directory, ignore_errors=True)

    def test_scene_setup_creates_meter_scale_metadata_and_lod_collections(self):
        jss_scene_setup.configure_scene(
            asset_name="task11-primitive",
            rig_kind="Generic",
            expected_bones=("Root", "Hips", "Spine"),
        )

        scene = bpy.context.scene
        self.assertEqual("METRIC", scene.unit_settings.system)
        self.assertEqual(1.0, scene.unit_settings.scale_length)
        self.assertEqual("METERS", scene.unit_settings.length_unit)
        self.assertEqual(30, scene.render.fps)
        self.assertEqual("task11-primitive", scene["jss_asset_name"])
        self.assertEqual("Generic", scene["jss_rig_kind"])
        self.assertEqual(
            ["Hips", "Root", "Spine"],
            json.loads(scene["jss_expected_bones"]),
        )
        self.assertEqual(
            {"JSS_EXPORT", "JSS_LOD0", "JSS_LOD1", "JSS_LOD2"},
            {
                name
                for name in bpy.data.collections.keys()
                if name.startswith("JSS_")
            },
        )

    def test_validator_accepts_complete_primitive_contract(self):
        self._create_valid_fixture()

        result = validate_character.validate_scene()

        self.assertTrue(result.is_valid, result.format_issues())
        self.assertEqual([], result.issues)

    def test_validator_rejects_unapplied_scale(self):
        fixture = self._create_valid_fixture()
        fixture["lod0"].scale.x = 1.25

        self._assert_issue("UNAPPLIED_SCALE")

    def test_validator_rejects_non_manifold_hero_mesh(self):
        fixture = self._create_valid_fixture()
        mesh = fixture["lod0"].data
        editable = bmesh.new()
        editable.from_mesh(mesh)
        editable.faces.ensure_lookup_table()
        editable.faces.remove(editable.faces[0])
        editable.to_mesh(mesh)
        editable.free()
        mesh.update()

        self._assert_issue("NON_MANIFOLD_HERO_MESH")

    def test_validator_rejects_unnamed_material(self):
        fixture = self._create_valid_fixture()
        fixture["lod0"].data.materials[0].name = "Material"

        self._assert_issue("UNNAMED_MATERIAL")

    def test_validator_rejects_unexpected_bone(self):
        fixture = self._create_valid_fixture()
        self._add_edit_bone(fixture["rig"], "Unexpected", (0, 0, 1.2), (0, 0, 1.4))

        self._assert_issue("UNEXPECTED_BONE")

    def test_validator_rejects_missing_lod_collection(self):
        self._create_valid_fixture()
        bpy.data.collections.remove(bpy.data.collections["JSS_LOD2"])

        self._assert_issue("MISSING_LOD_COLLECTION")

    def test_validator_rejects_lod_collection_outside_export_graph(self):
        self._create_valid_fixture()
        export = bpy.data.collections["JSS_EXPORT"]
        lod2 = bpy.data.collections["JSS_LOD2"]
        export.children.unlink(lod2)
        bpy.context.scene.collection.children.link(lod2)

        self._assert_issue("INVALID_LOD_COLLECTION_PARENT")

    def test_validator_rejects_invalid_object_prefix(self):
        fixture = self._create_valid_fixture()
        fixture["lod0"].name = "Body_LOD0"

        self._assert_issue("INVALID_OBJECT_PREFIX")

    def test_lod_generation_is_idempotent_and_preserves_rig_material_and_transform(self):
        fixture = self._create_valid_fixture(generate=False)

        first = generate_lods.generate_lods(lod1_ratio=0.5, lod2_ratio=0.2)
        second = generate_lods.generate_lods(lod1_ratio=0.5, lod2_ratio=0.2)

        self.assertEqual(first, second)
        self.assertGreater(first["JSS_LOD0"], first["JSS_LOD1"])
        self.assertGreater(first["JSS_LOD1"], first["JSS_LOD2"])
        for index in (1, 2):
            generated = bpy.data.objects[f"CHR_Task11Primitive_LOD{index}"]
            self.assertEqual((1.0, 1.0, 1.0), tuple(generated.scale))
            self.assertIs(
                fixture["material"],
                generated.data.materials[0],
            )
            modifiers = [modifier for modifier in generated.modifiers if modifier.type == "ARMATURE"]
            self.assertEqual(1, len(modifiers))
            self.assertIs(fixture["rig"], modifiers[0].object)
            self.assertEqual(["Root"], [group.name for group in generated.vertex_groups])
            self.assertEqual(
                [f"JSS_LOD{index}"],
                sorted(collection.name for collection in generated.users_collection),
            )

    def test_export_publishes_fbx_and_schema_versioned_report(self):
        self._create_valid_fixture()
        source_path = self.temp_directory / "task11-primitive.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(source_path))

        report = export_unity_fbx.export_character(
            output_directory=self.temp_directory / "Export",
            basename="task11-primitive",
        )

        fbx_path = self.temp_directory / "Export" / "task11-primitive.fbx"
        report_path = (
            self.temp_directory
            / "Export"
            / "task11-primitive.jss-character.json"
        )
        self.assertTrue(fbx_path.is_file())
        self.assertTrue(report_path.is_file())
        self.assertEqual(report, json.loads(report_path.read_text(encoding="utf-8")))
        self.assertEqual(1, report["schemaVersion"])
        self.assertEqual("METERS", report["units"]["lengthUnit"])
        self.assertEqual(1.0, report["units"]["metersPerUnit"])
        self.assertEqual("-Z", report["axes"]["forward"])
        self.assertEqual("Y", report["axes"]["up"])
        self.assertEqual("Generic", report["rigKind"])
        self.assertEqual(["Hips", "Root", "Spine"], report["bones"])
        self.assertEqual(["JSS_LOD0", "JSS_LOD1", "JSS_LOD2"], list(report["lods"]))
        self.assertGreater(report["lods"]["JSS_LOD0"]["triangles"], report["lods"]["JSS_LOD1"]["triangles"])
        self.assertGreater(report["lods"]["JSS_LOD1"]["triangles"], report["lods"]["JSS_LOD2"]["triangles"])
        self.assertEqual("Root", report["rootMotion"]["bone"])
        self.assertAlmostEqual(1.0, report["rootMotion"]["distanceMeters"], places=4)
        self.assertEqual("SOCKET_Forward", report["forwardMarker"])
        self.assertEqual(["MAT_Task11Canvas"], report["materials"])
        self.assertTrue(report["validation"]["isValid"])
        self.assertEqual([], report["validation"]["issues"])
        self.assertEqual(
            hashlib.sha256(fbx_path.read_bytes()).hexdigest(),
            report["fbxSha256"],
        )
        self.assertEqual([], list((self.temp_directory / "Export").glob("*.tmp*")))

    def test_failed_export_removes_stale_success_outputs(self):
        fixture = self._create_valid_fixture()
        source_path = self.temp_directory / "task11-primitive.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
        export_dir = self.temp_directory / "Export"
        export_unity_fbx.export_character(
            output_directory=export_dir,
            basename="task11-primitive",
        )
        fixture["lod0"].scale.x = 2.0
        bpy.ops.wm.save_as_mainfile(filepath=str(source_path))

        with self.assertRaises(validate_character.CharacterValidationError):
            export_unity_fbx.export_character(
                output_directory=export_dir,
                basename="task11-primitive",
            )

        self.assertFalse((export_dir / "task11-primitive.fbx").exists())
        self.assertFalse(
            (export_dir / "task11-primitive.jss-character.json").exists()
        )
        self.assertEqual([], list(export_dir.glob("*.tmp*")))

    def test_dirty_source_export_removes_stale_success_outputs(self):
        fixture = self._create_valid_fixture()
        source_path = self.temp_directory / "task11-primitive.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
        export_dir = self.temp_directory / "Export"
        export_dir.mkdir(parents=True, exist_ok=True)
        fbx_path = export_dir / "task11-primitive.fbx"
        report_path = export_dir / "task11-primitive.jss-character.json"
        fbx_path.write_bytes(b"stale-fbx")
        report_path.write_text("stale-report", encoding="utf-8")
        bpy.context.view_layer.objects.active = fixture["lod0"]
        fixture["lod0"].select_set(True)
        bpy.ops.transform.translate(value=(0.25, 0.0, 0.0))
        bpy.ops.ed.undo_push(message="Task 11 dirty-source test")
        self.assertTrue(bpy.data.is_dirty)

        with self.assertRaisesRegex(RuntimeError, "Save the .blend after"):
            export_unity_fbx.export_character(
                output_directory=export_dir,
                basename="task11-primitive",
            )

        self.assertFalse(fbx_path.exists())
        self.assertFalse(report_path.exists())
        self.assertEqual([], list(export_dir.glob("*.tmp*")))

    def _create_valid_fixture(self, generate=True):
        jss_scene_setup.configure_scene(
            asset_name="task11-primitive",
            rig_kind="Generic",
            expected_bones=("Root", "Hips", "Spine"),
        )
        export_collection = bpy.data.collections["JSS_EXPORT"]
        lod0_collection = bpy.data.collections["JSS_LOD0"]

        armature_data = bpy.data.armatures.new("RIG_Task11Primitive")
        rig = bpy.data.objects.new("RIG_Task11Primitive", armature_data)
        export_collection.objects.link(rig)
        self._add_edit_bone(rig, "Root", (0, 0, 0), (0, 0, 0.4))
        self._add_edit_bone(
            rig,
            "Hips",
            (0, 0, 0.4),
            (0, 0, 0.8),
            parent="Root",
        )
        self._add_edit_bone(
            rig,
            "Spine",
            (0, 0, 0.8),
            (0, 0, 1.2),
            parent="Hips",
        )

        material = bpy.data.materials.new("MAT_Task11Canvas")
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=32,
            ring_count=16,
            location=(0.0, 0.0, 0.8),
            scale=(0.3, 0.2, 0.8),
        )
        lod0 = bpy.context.active_object
        lod0.name = "CHR_Task11Primitive_LOD0"
        self._move_to_collection(lod0, lod0_collection)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        lod0.data.materials.append(material)
        root_weights = lod0.vertex_groups.new(name="Root")
        root_weights.add(
            list(range(len(lod0.data.vertices))),
            1.0,
            "REPLACE",
        )
        modifier = lod0.modifiers.new("Armature", "ARMATURE")
        modifier.object = rig

        forward = bpy.data.objects.new("SOCKET_Forward", None)
        forward.location = (0.0, -0.5, 0.8)
        forward.parent = rig
        export_collection.objects.link(forward)

        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        bpy.ops.object.mode_set(mode="POSE")
        root = rig.pose.bones["Root"]
        root.location = (0.0, 0.0, 0.0)
        root.keyframe_insert(data_path="location", frame=1)
        root.location = (0.0, -1.0, 0.0)
        root.keyframe_insert(data_path="location", frame=31)
        bpy.ops.object.mode_set(mode="OBJECT")
        if rig.animation_data and rig.animation_data.action:
            rig.animation_data.action.name = "ACT_Task11RootMotion"

        if generate:
            generate_lods.generate_lods(lod1_ratio=0.5, lod2_ratio=0.2)
        return {
            "rig": rig,
            "lod0": lod0,
            "material": material,
            "forward": forward,
        }

    @staticmethod
    def _add_edit_bone(rig, name, head, tail, parent=None):
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bone = rig.data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = rig.data.edit_bones[parent]
        bpy.ops.object.mode_set(mode="OBJECT")
        return bone

    @staticmethod
    def _move_to_collection(obj, target):
        for collection in tuple(obj.users_collection):
            collection.objects.unlink(obj)
        target.objects.link(obj)

    def _assert_issue(self, code):
        result = validate_character.validate_scene()
        self.assertFalse(result.is_valid)
        self.assertIn(code, [issue.code for issue in result.issues])


def publish_roundtrip_fixture():
    """Create the Task 11 Generic primitive fixture in the canonical asset tree."""
    case = CharacterPipelineTests(methodName="test_validator_accepts_complete_primitive_contract")
    case.setUp()
    try:
        case._create_valid_fixture()
        source_path = (
            PROJECT_ROOT
            / "Assets"
            / "_JustSomeStars"
            / "Art"
            / "Characters"
            / "Source"
            / "Fixtures"
            / "task11-primitive.blend"
        )
        export_directory = (
            PROJECT_ROOT
            / "Assets"
            / "_JustSomeStars"
            / "Art"
            / "Characters"
            / "Export"
            / "Fixtures"
        )
        source_path.parent.mkdir(parents=True, exist_ok=True)
        export_directory.mkdir(parents=True, exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
        report = export_unity_fbx.export_character(
            output_directory=export_directory,
            basename="task11-primitive",
        )
        print(json.dumps(report, indent=2, sort_keys=False))
    finally:
        case.tearDown()


if __name__ == "__main__":
    unittest.main(argv=[__file__], verbosity=2)
