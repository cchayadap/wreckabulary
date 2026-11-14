"""Build-07 selects the true original candidate grip; reviewed build-06 LOD detail and all geometry acceptance retained. Run only in a fresh, root-scheduled Blender process."""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import shutil
import sys
import time

sys.dont_write_bytecode = True
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import fan_geometry as geometry
import fan_yoke as yoke
import fan_lod_bounds as lod_bounds
import fan_grip

V2_BLEND_SHA = "73000d2546a714b8c3939e66d2f3563c02a0d2f702318d7870a2a3355e6f5e21"
V2_SCRIPT_SHA = "39349fc8b450a61a2aa3a288e5a50c3a10d7886d777c990b51c84fd802dd4f86"
V2_RECEIPT_SHA = "777c7a47d0590de0e7e2d0ff4148f5f67ce90c84e8e8a5428ce4ba278beb6e10"
V2_GLB_SHA = "031ecbdc3972b4ef7f7935e7c7d13a0999e05447412f3d66a9d20cbc77b98ce8"
TILT_MIN, TILT_MAX, TILT_STEP = -10, 10, 2
BLADE_MARGIN = 0.006
MIN_BLADE_GAP = 0.003
MIN_HUB_GAP = 0.0005
MIN_TILT_SAMPLE_GAP = 0.001
VARIANTS = {"original": "OriginalItem", "vivid-v2": "V2Reference", "construction-v3": "CandidateItem", "lod1": "CandidateLOD1"}
VIEWS = ("three-quarter", "front", "side", "rear", "detail", "dial")
CORE = None
BUILD01_SCRIPT_SHA = "b4ae4ee8e14914bc4372326708195751002b5ef1088ef096a5d7627b59e41ff0"
REPAIR_EVIDENCE = {
    'BuildNotes/completion-20261027/assets-build06/build_fan_v3.py': 'be7a7b0e99ca82c95dc8e1c6140b868281d70b6b8542de5294fbe2867b0e477e',
    'BuildNotes/completion-20261027/assets-build06/manifest.json': 'f977c990e618da3f3eaaa9c8a84e1de5823be9f420cd66373558f43faa8b57b8',
    'BuildNotes/completion-20261027/assets-build06/lod-detail.patch': 'a89c295cbf8d1ff744cbf4b2da49165ef7e9b1871f4f5693a25463776ede599d',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/Source/FAN.original.glb': '67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/build-receipt.json': 'b24b93fd69102956b47df68a7ecaab67c0aa1e6d76a33521ba440e1f586275a9',
    'Assets/_Project/Data/Config/items.json': '79363c8f1f14e7aca553718b552468d34b3140fe4ba88688fd4ded8330cf2416',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-rear-construction-v3.png': 'be3c088c43288e58e2e2593ea753578c2649400df5e7f445eea02dec06b0c93f',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-rear-construction-v3.json': 'f3ca6288b8225fc3d78bcf81b3d62ecc42f84d83134acda16b5db10b32f23cd7',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-rear-lod1.png': '8f2103850078326634b187fe6757870ef002e07f2b5b2fced0c5b99b23d56f60',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-rear-lod1.json': 'd2580200b52982f14a55c621b3d4ac3ba00ca08b745e155dfd3b3358ed7884e1',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-detail-construction-v3.png': '836d79d272a40c8d2bb3525d73ad79b4f3b8493f91436f601ce147c90577fc91',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-detail-construction-v3.json': '604923ecfa5317dfea293839111169af5004e3c137b43d72ca6815864f937a1a',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-detail-lod1.png': '1df817ba5603de637102dfd79cfc199d5f308a792ba97df6435b77c86a8e10a6',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-detail-lod1.json': '808e09203a1a71a3e1036ba79bd31c3bcddebf3a1d8ba6290dcfbd6192701276',
    'BuildNotes/completion-20261027/assets-build05/build_fan_v3.py': 'f0627d8e073238a09a3218e5baf473abc1298a102360f8b503e1105be7dae9d3',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/build-receipt.json': '634be56b5bfac47208b4072a5e6883395a45a50e2334dbc9642a6c654356e896',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/clearance-probe.json': '613df4fe1fd3c77634ed4a7f063f7fa0ad0d2ef37cd00be59bcf1bd9ba99dd0b',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/lod-probe.json': '608d4b576363559c47052e05bad58bcbd6451db6d6221873ea3ee4b6d489a436',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/FAN-original-based-pilot-v3.blend': '04e746eefc2827ec1abeef1f22106e57261632ed047cfd328316049535f0c8d6',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Exports/FAN-construction-v3-LOD1.fbx': '040ebac2c1b26c50689fd0d708c920cdd6aafbc260e9c90dc9957c4ce9ad5152',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Exports/FAN-construction-v3-LOD1.glb': '63a2eb2edc5ba243714057793680c628c33bed121205f149202ee7f79120d0d4',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Exports/FAN-construction-v3.fbx': 'e2ceea795099d9af0ab78122de8489cefeda32235ac363ea3570da21c9e1a15f',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Exports/FAN-construction-v3.glb': '945f1304d8a4a98e53ec6fe52dfbe69c3c109b0dc1df55c50b432737d9ec48b3',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-front-construction-v3.png': '815a801d79ab53e69a2fbf3bf891a76ac1d0824f6c6f4aed951e129765d86377',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-front-construction-v3.json': '76276d1fe3567b59bb3b15bc0e62e142d189991cf80ebb18d976f4e29e3d9253',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-front-lod1.png': 'af4ad2843932f096f5028b715b1dab4e9fe06b0a8630649252b99849857a7152',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-front-lod1.json': 'f29e1724f221e4f90dd43e839ab238fee10053b87dc242c37a1fc82b6c8302e4',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-side-construction-v3.png': '12ec5277241a707e6351c471203279d50261800f118049c0915f88a393d19a15',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-side-construction-v3.json': '35fb6cf4e7e7a23f4568c4d11512f92f8a24ad491489d40f6cc45b9efd91d20b',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-side-lod1.png': 'e2228791bd14456059e6bbc6db1fe4d2f7a273ee5af7673adb3c51230b113917',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-05/Previews/FAN-side-lod1.json': '2162720d817da0c24eb659f542a5ef802cbf4456129ce23da699ac7f689bd6ce',
    'BuildNotes/completion-20261027/assets-build04/build_fan_v3.py': '86065b092426b2f49245ab0a55792ba511859df417d24e6b09347b02c914d9e2',
    'BuildNotes/completion-20261027/assets-build06/inspect-fan04-glb.mjs': '8d4e735cfeb8ae0df11970cd38bada81b5c02c20c819c0e550e749c0804d4db1',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/build-receipt.json': 'eb64ab427e29137abbdec1eb35a2b4e12e0d6739dd54303df582e48107d0a322',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/clearance-probe.json': '613df4fe1fd3c77634ed4a7f063f7fa0ad0d2ef37cd00be59bcf1bd9ba99dd0b',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Exports/FAN-construction-v3-LOD1.fbx': 'a03ddee38ea1f970d9d88e3fca865ba725284449da1020b6db6092b1e02e0624',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Exports/FAN-construction-v3-LOD1.glb': '4d49c65bfe9d113cd911c532d943cc31c48f33c22b7f35e98e4a40f297016a45',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Exports/FAN-construction-v3.fbx': '6413cbcaf4a4576e11c49e7ce5f674b309fb058ceaf0d89078cc84e0743f2e6d',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Exports/FAN-construction-v3.glb': '31e23c4c6892324427017defcbcdaadcf30fe8038543533060e702b74aff0b7f',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/FAN-original-based-pilot-v3.blend': 'ddcdbc8b95c28633288181f1a9b21ec7313f4498c49cb98e6e43ac1802545045',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/lod-probe.json': '608d4b576363559c47052e05bad58bcbd6451db6d6221873ea3ee4b6d489a436',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Source/build_fan_v2.py': '39349fc8b450a61a2aa3a288e5a50c3a10d7886d777c990b51c84fd802dd4f86',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Source/build_fan_v3.py': '86065b092426b2f49245ab0a55792ba511859df417d24e6b09347b02c914d9e2',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Source/fan_geometry.py': '1099611865b7a55cd5f53cdc5a11028c83f36aece8a7747a87af5db39c53829e',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Source/fan_lod_bounds.py': '81b6032dc4935eb049de3a9d746c353fde446e6d86dac1122b5f5568e4316736',
    'ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-04/Source/fan_yoke.py': 'ffb2e30482be0fa6168634958f84e3ade56b2fa99c7e5ab5d5cd502c68a1b98a',
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-03/build-receipt.json": "4916fa4a68d81804958cc8871a1a6522862d68d2f08c3dddaee94f81e145b5c2",
    "BuildNotes/completion-20261027/assets-draft/candidate/build_fan_v3.py": "7780f071a53e1a96d9ce1688e484456fb65def73fd670d63442740e4b4b8e0e4",
    "BuildNotes/asset-expansion/fan-v3/repair-draft/build_fan_v3.py": "4598cbf854c38b10694c332d6881e3d56f606f224ef046129be654e5f6089390",
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-02/build-receipt.json": "48178d9b2def4de09edb4dbd323d71b7c74c976a9aa4aed7d5fbec03ea607a83",
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-02/clearance-probe.json": "613df4fe1fd3c77634ed4a7f063f7fa0ad0d2ef37cd00be59bcf1bd9ba99dd0b",
    "BuildNotes/asset-expansion/fan-v3/build_fan_v3.py": BUILD01_SCRIPT_SHA,
    "BuildNotes/asset-expansion/fan-v3/diagnostic-01/receipt.json": "677096c5ca76dcfa79eab684d1741e2b048ce43a6a5b65baf3e10c5fff518bd8",
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-01/FAN-v3-diagnostic-only.blend": "438ce6524f2eda0e491928a06819d0cafa48062df17d5b76638ab6546c05be26",
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-01/clearance-probe.json": "c01a58a6f0757839cfe07c74f997972fa337f17795061731302a1e1576f701b6",
    "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-01/build-receipt.json": "3a2264e8bdaeea83c7c1b10577472565bd7c02ee291e67fb7021408e94aa1507",
}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_new(path, value):
    with Path(path).open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def require(name):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError("Reviewed source object missing: " + name)
    return obj


def world_points(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def triangles(objects):
    result = []
    for obj in objects:
        points = world_points(obj)
        obj.data.calc_loop_triangles()
        result.extend((obj.name, [points[i] for i in face.vertices]) for face in obj.data.loop_triangles)
    return result


def remap_vertices(obj, operation):
    inverse = obj.matrix_world.inverted()
    obj.data = obj.data.copy()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ operation(obj.matrix_world @ vertex.co)
    obj.data.update()


def material(role):
    return bpy.data.materials["FAN_V2_" + role.title()]


def new_mesh(name, points, faces, surface, target, parent, smooth=False):
    mesh = bpy.data.meshes.new(name + "_EditableGeometry")
    mesh.from_pydata(points, [], faces)
    mesh.materials.append(surface)
    mesh.update()
    for face in mesh.polygons:
        face.use_smooth = smooth and len(face.vertices) == 4
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    CORE.parent_preserve_world(obj, parent)
    return obj


def beams(name, segments, radius, target, parent, sides=12):
    points, faces = [], []
    for start, end in segments:
        start, end = Vector(start), Vector(end)
        direction = (end - start).normalized()
        helper = Vector((0, 0, 1)) if abs(direction.z) < .9 else Vector((1, 0, 0))
        u, v = direction.cross(helper).normalized(), None
        v = direction.cross(u).normalized()
        offset = len(points)
        for position in (start, end):
            points.extend(position + radius * (u * math.cos(math.tau * i / sides) + v * math.sin(math.tau * i / sides)) for i in range(sides))
        faces.extend((offset + i, offset + (i + 1) % sides, offset + sides + (i + 1) % sides, offset + sides + i) for i in range(sides))
        faces.extend((tuple(offset + i for i in reversed(range(sides))), tuple(offset + sides + i for i in range(sides))))
    return new_mesh(name, points, faces, material("metal"), target, parent, True)


def remove(name):
    obj = require(name)
    if obj.name not in bpy.data.collections["CandidateItem"].objects:
        raise RuntimeError("Removal escaped candidate collection")
    bpy.data.objects.remove(obj, do_unlink=True)


def copy_reference(target):
    group = CORE.collection("V2Reference")
    for obj in CORE.meshes(target):
        clone = obj.copy()
        clone.data = obj.data.copy()
        group.objects.link(clone)
        clone.parent = None
        clone.matrix_world = obj.matrix_world.copy()
        clone.name = "V2_" + obj.name


def repair_fixed_yoke():
    expected = {
        "FAN_TiltYokeCrossbar": ((-.075, .063, .454), (.075, .081, .472)),
        "FAN_LeftTiltSupport": ((-.084, .063, .463), (-.066, .081, .56)),
        "FAN_RightTiltSupport": ((.066, .063, .463), (.084, .081, .56)),
    }
    records = []
    for name, bounds in expected.items():
        obj = require(name)
        before = CORE.bounds([obj])
        if any(abs(before[i][j] - bounds[i][j]) > 1e-6 for i in range(2) for j in range(3)):
            raise RuntimeError("Fixed yoke source differs from inspected geometry: " + name)
        if obj.parent != require("FAN_V3_Root") or len(obj.data.materials) != 1 or obj.data.materials[0] != material("metal"):
            raise RuntimeError("Fixed yoke parent/material identity changed: " + name)
        counts = (len(obj.data.vertices), len(obj.data.polygons))
        original_mesh = obj.data
        operation = yoke.shift_crossbar if name == "FAN_TiltYokeCrossbar" else yoke.aim_support
        expected_points = [Vector(operation(point)) for point in world_points(obj)]
        obj.matrix_world = Matrix(yoke.affine_rows(operation)) @ obj.matrix_world
        bpy.context.view_layer.update()
        if obj.data != original_mesh or counts != (len(obj.data.vertices), len(obj.data.polygons)):
            raise RuntimeError("Fixed yoke repair changed source mesh data")
        position_error = max((actual - expected).length for actual, expected in zip(world_points(obj), expected_points))
        if position_error > 1e-6:
            raise RuntimeError("Object transform did not preserve the reviewed yoke geometry")
        obj["build02_support_repair"] = "Crossbar center Y .108; original .009 m support radius retained; upper axle centers unchanged."
        records.append({"part": name, "before_bounds": before, "after_bounds": CORE.bounds([obj]),
                        "vertices": counts[0], "polygons": counts[1], "material": obj.data.materials[0].name,
                        "parent": obj.parent.name, "mesh_data_unchanged": True,
                        "world_point_max_error_metres": position_error,
                        "transform_method": "Object affine transform; existing custom normals and UV data retained in mesh space."})
    bar_low = CORE.bounds([require("FAN_TiltYokeCrossbar")])[0][1]
    envelope = []
    for name in ("FAN_RearGuardSpoke_9", "FAN_RearGuardSpoke_11"):
        low, high = yoke.swept_box(CORE.bounds([require(name)]), TILT_MIN, TILT_MAX)
        gap = bar_low - high[1]
        if gap < .012:
            raise RuntimeError("Repaired crossbar lacks expected full-range spoke Y separation")
        envelope.append({"moving_part": name, "swept_aabb_metres": (low, high),
                         "crossbar_y_gap_lower_bound_metres": gap})
    return {"crossbar_rear_shift_metres": yoke.NEW_Y - yoke.OLD_Y, "parts": records,
            "upper_axle_centers_unchanged": ((-.075, .072, .56), (.075, .072, .56)),
            "crossbar_center": (0, yoke.NEW_Y, yoke.BOTTOM_Z), "source_radius_preserved_metres": .009,
            "continuous_spoke_crossbar_separation": envelope,
            "limit": "AABB axis separation covers these two spokes versus the crossbar over the full tilt interval; remaining mesh pairs still require the unchanged native checks."}


def construction(target):
    root, tilt, rotor = require("FAN_V2_Root"), require("FAN_TiltPivot_X"), require("FAN_RotorPivot_Y")
    root.name = "FAN_V3_Root"
    root["held_scale_from_original_config"] = .4385
    root["editing"] = "Rotate the named tilt/rotor/dial controls. LOD0 components retain separate meshes. No gameplay controller installed."
    copy_reference(target)
    before_bounds = CORE.bounds(CORE.meshes(target))
    rim = require("FAN_BlueGuardRim_02")
    aperture = min(geometry.radial_bounds(face)[0] for _, face in triangles([rim]))
    target_radius = aperture - BLADE_MARGIN
    blades = [require("FAN_OriginalBlade_" + value) for value in ("08", "09", "10")]
    old_radius = max(math.hypot(p.x, p.z - .56) for obj in blades for p in world_points(obj))
    factor = target_radius / old_radius
    if not .78 <= factor <= .90:
        raise RuntimeError("Blade fit exceeds reviewed radial repair range")
    for blade in blades:
        remap_vertices(blade, lambda p: Vector((p.x * factor, p.y, .56 + (p.z - .56) * factor)))
        blade["radial_fit_factor_v3"] = factor
    remove("FAN_OriginalFrontGrille_07")
    segments = []
    for i in range(6):
        direction = Vector((math.cos(math.tau * i / 6), 0, math.sin(math.tau * i / 6)))
        center = Vector((0, -.06, .56))
        segments.append((center + direction * .060, center + direction * .210))
    spoke = beams("FAN_CapturedFrontSpokes", segments, .003, target, tilt)
    spoke["joint"] = "Six capped rods end on the centreline of the existing .060/.210 support rings; no protruding tips."
    endpoint_checks = []
    for point_pair in segments:
        for point, expected in zip(point_pair, (.060, .210)):
            actual = math.hypot(point.x, point.z - .56)
            endpoint_checks.append(abs(actual - expected))
    if max(endpoint_checks) > 1e-7:
        raise RuntimeError("Front support endpoint mismatch")
    for index in range(2, 13, 2):
        remove("FAN_RearGuardSpoke_" + str(index))
        remove("FAN_RearGuardMount_" + str(index))
    stand = require("FAN_OriginalStand_03")
    low, high = CORE.bounds([stand])
    stand_top = before_bounds[0][2] + .275
    remap_vertices(stand, lambda p: Vector((p.x, p.y, low[2] + (p.z - low[2]) * (stand_top - low[2]) / (high[2] - low[2]))))
    remove("FAN_StandToTiltNeck")
    neck_radius, rear_y = .012, before_bounds[1][1] - .012
    neck_points = ((0, 0, stand_top), (0, rear_y, stand_top), (0, rear_y, .43), (0, yoke.NEW_Y, yoke.BOTTOM_Z))
    neck = beams("FAN_RearOffsetSupport", list(zip(neck_points, neck_points[1:])), neck_radius, target, root, sides=16)
    neck["construction"] = "Fixed rear-offset neck clears the ring/rotor; shared endpoints overlap as rigid joints. Not a collider."
    yoke_report = repair_fixed_yoke()
    tilt["control"] = "Local X, -10 to +10 degrees. Sampled mesh checks are reported separately from continuous certification."
    tilt["minimum_degrees"], tilt["maximum_degrees"] = TILT_MIN, TILT_MAX
    tilt.rotation_mode = rotor.rotation_mode = "XYZ"
    constraint = tilt.constraints.new("LIMIT_ROTATION")
    constraint.name = "AuthoringTiltStops"
    constraint.owner_space = "LOCAL"
    constraint.use_limit_x = constraint.use_limit_y = constraint.use_limit_z = True
    constraint.min_x, constraint.max_x = math.radians(TILT_MIN), math.radians(TILT_MAX)
    constraint.min_y = constraint.max_y = constraint.min_z = constraint.max_z = 0
    rotor["control"] = "Local Y full rotation; rigid mechanical part, no skin/skeleton or gameplay speed attached."
    bpy.context.view_layer.update()
    CORE.assert_bounds(CORE.bounds(CORE.meshes(target)), before_bounds)
    return {"front_spokes": 6, "rear_spoke_mount_pairs": 6, "front_endpoint_max_error_metres": max(endpoint_checks),
            "aperture_min_triangle_radius": aperture, "old_blade_max_radius": old_radius,
            "new_blade_max_radius": target_radius, "blade_radial_factor": factor,
            "stand_original_top": high[2], "stand_new_top": stand_top, "neck_radius": neck_radius,
            "neck_control_points": neck_points, "fixed_yoke_repair": yoke_report,
            "held_scale": .4385, "outer_bounds_unchanged": before_bounds}


def paint_and_dial(target):
    root = require("FAN_V3_Root")
    lip = material("blue").copy()
    lip.name = "FAN_V3_EnamelRecess"
    srgb = [int("1C4F9C"[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    color = (*[CORE.linear_channel(value) for value in srgb], 1)
    lip.diffuse_color = color
    shader = lip.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = .55
    assigned = {}
    for name in ("FAN_BlueBase_01", "FAN_BlueGuardRim_02"):
        obj = require(name)
        obj.data = obj.data.copy()
        obj.data.materials.append(lip)
        count = 0
        for face in obj.data.polygons:
            center = obj.matrix_world @ face.center
            selected = center.z < .012 if name == "FAN_BlueBase_01" else math.hypot(center.x, center.z - .56) < .230
            if selected:
                face.material_index = len(obj.data.materials) - 1
                count += 1
        if not count:
            raise RuntimeError("No enamel lip faces selected")
        assigned[name] = count
    dial = require("FAN_BaseSpeedDial")
    origin = Vector((0, -.052, .054))
    remap_vertices(dial, lambda p: Vector((origin.x + (p.x - origin.x) * 1.4, origin.y + (p.y - origin.y) * 1.4, p.z)))
    control = CORE.empty("FAN_DialPivot_Z", target, origin, root)
    CORE.parent_preserve_world(dial, control)
    control["control"] = "Local Z, decorative setting indicator. No interactable/runtime state is connected."
    remove("FAN_DialIndicator")
    top = CORE.bounds([dial])[1][2] + .0003
    new_mesh("FAN_DialPaintPointer", [(-.0012, -.069, top), (.0012, -.069, top), (.0012, -.056, top), (-.0012, -.056, top)],
             [(0, 1, 2, 3)], material("recess"), target, control)
    points, faces = [], []
    base_z = CORE.bounds([require("FAN_BlueBase_01")])[1][2] + .00025
    for degrees in (210, 270, 330):
        direction = Vector((math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0))
        tangent = Vector((-direction.y, direction.x, 0)) * .0018
        a, b = Vector((0, -.052, base_z)) + direction * .026, Vector((0, -.052, base_z)) + direction * .034
        offset = len(points)
        points.extend((a - tangent, b - tangent, b + tangent, a + tangent))
        faces.append(tuple(offset + i for i in range(4)))
    ticks = new_mesh("FAN_PaintedSpeedTicks", points, faces, material("gold"), target, root)
    ticks["purpose"] = "Three paint marks relate to the dial pointer; no labels or promised UI interaction."
    points, faces = [], []
    for i in range(3):
        x, y = .065 + i * .012, -.065 + i * .006
        offset = len(points)
        points.extend(((x, y, base_z), (x + .006, y, base_z), (x + .021, y + .025, base_z), (x + .015, y + .025, base_z)))
        faces.append(tuple(offset + j for j in range(4)))
    new_mesh("FAN_AirflowPaintBars", points, faces, material("ivory"), target, root)
    return {"identity": "V2 blue/ivory/gold retained; #1C4F9C enamel recess on existing faces", "enamel_face_counts": assigned,
            "dial_radius_factor": 1.4, "paint_triangles": 14,
            "limit": "Flat material inlays, not a painted texture/PBR bake. Thin overlay depth and readability require native all-angle review."}


def bvh(objects):
    points, faces = [], []
    for obj in objects:
        offset = len(points)
        points.extend(world_points(obj))
        obj.data.calc_loop_triangles()
        faces.extend(tuple(offset + i for i in face.vertices) for face in obj.data.loop_triangles)
    return BVHTree.FromPolygons(points, faces, all_triangles=True), points


def clearance(target):
    rotor, tilt = require("FAN_RotorPivot_Y"), require("FAN_TiltPivot_X")
    rotor_parts = [obj for obj in CORE.meshes(target) if obj.parent == rotor]
    stationary = [obj for obj in CORE.meshes(target) if obj.parent != rotor]
    blades = [obj for obj in rotor_parts if "OriginalBlade" in obj.name]
    blade_vertices = [p for obj in blades for p in world_points(obj)]
    radius = max(math.hypot(p.x, p.z - .56) for p in blade_vertices)
    low, high = min(p.y for p in blade_vertices), max(p.y for p in blade_vertices)
    stationary_bounds = [(name, geometry.triangle_cylindrical_bounds(face)) for name, face in triangles(stationary)]
    blade_gap, blade_against = min((geometry.cylinder_bounds_lower_bound(radius, low, high, face), name) for name, face in stationary_bounds)
    hub_profile = geometry.swept_axial_profile([face for _, face in triangles([require("FAN_OriginalRotorCap_11")])])
    hub_gap, hub_against = min((geometry.cylinder_bounds_lower_bound(r, a, b, face), name)
                               for name, face in stationary_bounds for r, a, b in hub_profile)
    fixed = [obj for obj in stationary if obj.parent != tilt and "TiltAxle" not in obj.name and "TiltFastener" not in obj.name]
    head = [obj for obj in stationary if obj.parent == tilt] + rotor_parts
    samples = []
    original_tilt, original_rotor = tilt.rotation_euler.copy(), rotor.rotation_euler.copy()
    try:
        fixed_tree, fixed_points = bvh(fixed)
        for degrees in range(TILT_MIN, TILT_MAX + 1, TILT_STEP):
            tilt.rotation_euler.x = math.radians(degrees)
            bpy.context.view_layer.update()
            head_tree, head_points = bvh(head)
            overlaps = len(head_tree.overlap(fixed_tree))
            distances = [fixed_tree.find_nearest(point)[3] for point in head_points]
            distances += [head_tree.find_nearest(point)[3] for point in fixed_points]
            samples.append({"tilt_degrees": degrees, "surface_triangle_overlap_pairs": overlaps,
                            "minimum_sampled_vertex_surface_distance": min(distance for distance in distances if distance is not None)})
    finally:
        tilt.rotation_euler, rotor.rotation_euler = original_tilt, original_rotor
        bpy.context.view_layer.update()
    passed = blade_gap >= MIN_BLADE_GAP and hub_gap >= MIN_HUB_GAP and all(row["surface_triangle_overlap_pairs"] == 0 and row["minimum_sampled_vertex_surface_distance"] >= MIN_TILT_SAMPLE_GAP for row in samples)
    return {"status": "PASS" if passed else "FAIL", "continuous_blade_solid_cylinder_lower_bound": blade_gap,
            "blade_nearest_part": blade_against, "continuous_hub_slab_envelope_lower_bound": hub_gap,
            "hub_nearest_part": hub_against, "thresholds_metres": {"blade": MIN_BLADE_GAP, "hub": MIN_HUB_GAP, "tilt_sample": MIN_TILT_SAMPLE_GAP},
            "tilt_samples": samples, "intentional_mates_excluded_from_tilt_check": ["FAN_LeftTiltAxle", "FAN_RightTiltAxle", "FAN_LeftTiltFastener", "FAN_RightTiltFastener"],
            "limits": ["Rotor blade/hub envelopes conservatively cover every Y rotation in the neutral head frame; they test stationary source triangles, not physics colliders.",
                       "Tilt checks are discrete surface-overlap and vertex-to-surface measurements, not continuous minimum triangle distance, containment proof or all combined rotor/tilt poses.",
                       "The motor shaft enters the motor shell intentionally; bearing friction, skinning and rigidbody physics are not certified.",
                       "No collision meshes, mass, online timing or runtime animation are produced."]}


def build_lod(target, output):
    group = CORE.collection("CandidateLOD1")
    source = CORE.meshes(target)
    analysis = lod_bounds.analyze_parts({obj.name: world_points(obj) for obj in source})
    protected = {"FAN_BlueBase_01", "FAN_BlueGuardRim_02", "FAN_OriginalBlade_08", "FAN_OriginalBlade_09", "FAN_OriginalBlade_10", "FAN_OriginalRotorCap_11"}
    protected.update(analysis["protected_boundary_meshes"])
    detail_protected = {"FAN_OriginalStand_03", "FAN_OriginalFrontGrille_04", "FAN_OriginalFrontGrille_05", "FAN_OriginalFrontGrille_06",
                        "FAN_TiltYokeCrossbar", "FAN_LeftTiltSupport", "FAN_RightTiltSupport", "FAN_RearGuardInnerSupport"}
    protected.update(detail_protected)
    report = {"status": "IN PROGRESS", "source_extrema": analysis, "protected_parts": sorted(protected),
              "detail_protected_parts": sorted(detail_protected), "reductions": []}
    groups = {}
    try:
        missing = detail_protected.difference(obj.name for obj in source)
        if missing:
            raise RuntimeError("Required original LOD detail missing: " + ", ".join(sorted(missing)))
        for obj in source:
            clone = obj.copy()
            clone.data = obj.data.copy()
            group.objects.link(clone)
            clone.name = "LOD1_" + obj.name
            clone.matrix_world = obj.matrix_world.copy()
            clone.data.calc_loop_triangles()
            before = len(clone.data.loop_triangles)
            row = {"part": obj.name, "before": before, "before_bounds": lod_bounds.bounds(world_points(clone)),
                   "protected_boundary": obj.name in analysis["protected_boundary_meshes"],
                   "protected_detail": obj.name in detail_protected, "simplified": False}
            if obj.name not in protected and before > 48:
                backup = clone.data.copy()
                try:
                    bpy.ops.object.select_all(action="DESELECT")
                    clone.select_set(True)
                    bpy.context.view_layer.objects.active = clone
                    modifier = clone.modifiers.new("LOD1_StaticSimplification", "DECIMATE")
                    modifier.ratio, modifier.use_collapse_triangulate = .30, True
                    bpy.ops.object.modifier_apply(modifier=modifier.name)
                    bpy.context.view_layer.update()
                    row["attempted_bounds"] = lod_bounds.bounds(world_points(clone))
                    outside = lod_bounds.first_outside(world_points(clone), analysis["bounds"])
                    if outside:
                        discarded = clone.data
                        clone.data = backup
                        if discarded.users == 0:
                            bpy.data.meshes.remove(discarded)
                        row["restored_after_envelope_escape"] = outside
                    else:
                        row["simplified"] = True
                finally:
                    if backup.users == 0:
                        bpy.data.meshes.remove(backup)
            clone.data.calc_loop_triangles()
            row.update(after=len(clone.data.loop_triangles), after_bounds=lod_bounds.bounds(world_points(clone)))
            report["reductions"].append(row)
            key = (clone.parent.name if clone.parent else "", tuple(mat.name for mat in clone.data.materials))
            groups.setdefault(key, []).append(clone)
        report["before_join_extrema"] = lod_bounds.analyze_parts({obj.name: world_points(obj) for obj in CORE.meshes(group)})
        for index, objects in enumerate(groups.values()):
            bpy.ops.object.select_all(action="DESELECT")
            for obj in objects:
                obj.select_set(True)
            bpy.context.view_layer.objects.active = objects[0]
            bpy.ops.object.join()
            objects[0].name = "FAN_LOD1_Group_" + str(index + 1).zfill(2)
        bpy.context.view_layer.update()
        report["after_join_extrema"] = lod_bounds.analyze_parts({obj.name: world_points(obj) for obj in CORE.meshes(group)})
        report["bounds_comparison"] = lod_bounds.comparison(report["after_join_extrema"]["bounds"], analysis["bounds"])
        geometry_report = CORE.geometry_report(CORE.meshes(group))
        base = CORE.geometry_report(CORE.meshes(target))
        report["geometry"] = geometry_report
        report["triangle_ratio"] = geometry_report["triangles"] / base["triangles"]
        if geometry_report["triangles"] > base["triangles"] * .65 or geometry_report["mesh_count"] > 20:
            raise RuntimeError("LOD1 missed bounded triangle/renderer target")
        CORE.assert_bounds(CORE.bounds(CORE.meshes(group)), CORE.bounds(CORE.meshes(target)), .00001)
        report["status"] = "PASS"
        report["limit"] = "Owned boundary-preserving LOD; no Unity LODGroup, motion, visual or measured draw-call acceptance."
        return group, report
    except Exception as error:
        report["status"], report["error"] = "FAIL", str(error)
        raise
    finally:
        write_new(output / "lod-probe.json", report)


def show(variant):
    visible = VARIANTS[variant]
    for name in (*VARIANTS.values(), "ConstructionReferenceV1", "HeldOriginal", "HeldCandidate", "OriginalAvatarContext"):
        group = bpy.data.collections.get(name)
        if group:
            group.hide_render = group.hide_viewport = name != visible
    bpy.context.view_layer.update()


def export_variant(group, path):
    show("construction-v3" if group.name == "CandidateItem" else "lod1")
    bpy.ops.object.select_all(action="DESELECT")
    controls = list(bpy.data.collections["FANControls"].objects)
    for obj in [*CORE.meshes(group), *controls]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = require("FAN_V3_Root")
    if path.suffix == ".glb":
        bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True, export_animations=False, export_yup=True, export_normals=True)
    else:
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"}, bake_anim=False,
                                 apply_unit_scale=True, axis_forward="-Z", axis_up="Y", path_mode="AUTO", use_mesh_modifiers=True)
        if not path.read_bytes().startswith(b"Kaydara FBX Binary"):
            raise RuntimeError("FBX signature missing")


def load_core(source):
    path = source / "Source/build_fan_v2.py"
    if digest(path) != V2_SCRIPT_SHA:
        raise RuntimeError("V2 helper script changed")
    spec = importlib.util.spec_from_file_location("reviewed_fan_v2_helpers", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def reimport(path, expected_bounds, expected_mesh_parents, expected_grip):
    record = CORE.reimport(path, expected_bounds)
    named = {obj.name.split(".")[0]: obj for obj in bpy.context.scene.objects}
    for child, parent in (("FAN_TiltPivot_X", "FAN_V3_Root"), ("FAN_RotorPivot_Y", "FAN_TiltPivot_X"),
                          ("FAN_DialPivot_Z", "FAN_V3_Root"), ("Grip_R", "FAN_V3_Root")):
        obj = named.get(child)
        if obj is None or obj.parent is None or obj.parent.name.split(".")[0] != parent:
            raise RuntimeError("Export lost rigid control relationship: " + child)
    record["v3_control_parentage_found"] = True
    imported_meshes = {name: obj.parent.name.split(".")[0] if obj.parent else None
                       for name, obj in named.items() if obj.type == "MESH"}
    if imported_meshes != expected_mesh_parents:
        raise RuntimeError("Export changed selected mesh membership or rigid-parent assignments")
    record["mesh_rigid_parentage"] = imported_meshes
    grips = [obj for obj in bpy.context.scene.objects if obj.name.split(".")[0] == "Grip_R"]
    if len(grips) != 1:
        raise RuntimeError("Export changed the unique original grip identity")
    record["original_grip_matrix"] = fan_grip.check_matrix(
        [list(row) for row in grips[0].matrix_world], expected_grip, .000001)
    record["control_limit"] = "Blender authoring constraints/custom properties are not a Unity controller or guaranteed exchange-format limit metadata."
    return record



def prepare_original_grip(target, contract):
    expected = contract["blender_world_matrix_float32"]
    matches = [obj for obj in target.objects if obj.name.split(".")[0] == "Grip_R"]
    if len(matches) != 1:
        raise RuntimeError("Expected one candidate-collection grip; global reference markers are not eligible")
    grip = matches[0]
    if grip.type != "EMPTY" or len(grip.children) != 0 or grip.parent != require("FAN_V2_Root"):
        raise RuntimeError("Candidate grip has unexpected type, children or rigid parent")
    before = [list(row) for row in grip.matrix_world]
    inherited = fan_grip.check_matrix(before, expected, .000001)
    prior_name = grip.name
    renamed_reference = None
    collision = bpy.data.objects.get("Grip_R")
    if collision is not None and collision != grip:
        reference = bpy.data.collections.get("OriginalItem")
        alias = "FAN_OriginalReference_Grip_R"
        if reference is None or collision.name not in reference.all_objects or collision.type != "EMPTY" or bpy.data.objects.get(alias) is not None:
            raise RuntimeError("Canonical grip name is occupied by an unexpected object")
        reference_matrix = [list(row) for row in collision.matrix_world]
        collision.name = alias
        bpy.context.view_layer.update()
        if collision.name != alias or reference_matrix != [list(row) for row in collision.matrix_world]:
            raise RuntimeError("Reference marker naming changed its original transform")
        renamed_reference = {"old_name": "Grip_R", "new_name": alias,
                             "world_matrix_preserved": reference_matrix}
    grip.name = "Grip_R"
    if grip.name != "Grip_R":
        raise RuntimeError("Candidate grip could not obtain its canonical export name")
    grip.matrix_world = Matrix(expected)
    bpy.context.view_layer.update()
    exact = fan_grip.check_matrix([list(row) for row in grip.matrix_world], expected)
    return grip, {"selected_collection": target.name, "candidate_name_before": prior_name,
                  "candidate_name_after": grip.name, "inherited_source_comparison": inherited,
                  "original_reference_rename": renamed_reference, "exact_original_matrix": exact,
                  "limit": "Only the candidate empty is normalized to the original native float32 matrix; no mesh, material or reference transform is moved."}


def build(args):
    global CORE
    expected = {"FAN-original-based-pilot-v2.blend": V2_BLEND_SHA, "build-receipt.json": V2_RECEIPT_SHA, "Exports/FAN-vivid-v2.glb": V2_GLB_SHA, "Source/build_fan_v2.py": V2_SCRIPT_SHA}
    for name, value in expected.items():
        if digest(args.v2 / name) != value:
            raise RuntimeError("Reviewed V2 input changed: " + name)
    for name, value in REPAIR_EVIDENCE.items():
        if not (args.root / name).is_file() or digest(args.root / name) != value:
            raise RuntimeError("Failed build/diagnostic evidence changed: " + name)
    inputs = {str(path.relative_to(args.root)): digest(path) for path in args.v2.rglob("*") if path.is_file()}
    inputs.update(REPAIR_EVIDENCE)
    if args.output.exists():
        raise RuntimeError("Refusing to overwrite V3 output")
    CORE = load_core(args.v2)
    receipt_v2 = json.loads((args.v2 / "build-receipt.json").read_text(encoding="utf-8"))
    original_source = args.root / "ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1"
    grip_contract = fan_grip.original_contract((original_source / "Source/FAN.original.glb").read_bytes(),
        json.loads((args.root / "Assets/_Project/Data/Config/items.json").read_text(encoding="utf-8")),
        json.loads((original_source / "build-receipt.json").read_text(encoding="utf-8")))
    args.output.mkdir(parents=True)
    report = {"status": "IN PROGRESS", "script_sha256": digest(__file__), "helper_sha256": V2_SCRIPT_SHA,
              "geometry_helper_sha256": digest(Path(__file__).with_name("fan_geometry.py")), "source_hashes": inputs,
              "repair_helper_sha256": digest(Path(__file__).with_name("fan_yoke.py")),
              "grip_helper_sha256": digest(Path(__file__).with_name("fan_grip.py")), "original_grip_contract": grip_contract,
              "repair_revision": "build-07 collection-scoped original grip contract; reviewed build-06 LOD and all geometry acceptance retained",
              "lod_bounds_helper_sha256": digest(Path(__file__).with_name("fan_lod_bounds.py")), "build01_script_sha256": BUILD01_SCRIPT_SHA}
    try:
        bpy.ops.wm.open_mainfile(filepath=str(args.v2 / "FAN-original-based-pilot-v2.blend"))
        if bpy.app.debug_value == 100 or bpy.context.scene.unit_settings.scale_length != 1.0:
            raise RuntimeError("Original grip contract requires standard glTF axis conversion and metre scene units")
        bpy.context.view_layer.update()
        target = bpy.data.collections["CandidateItem"]
        report["geometry_before"] = CORE.geometry_report(CORE.meshes(target))
        grip, report["original_grip_selection"] = prepare_original_grip(target, grip_contract)
        grip_original_parent = grip.parent.name if grip.parent else None
        grip_world_before = grip.matrix_world.copy()
        grip_before = [list(row) for row in grip_world_before]
        report["construction"] = construction(target)
        report["paint"] = paint_and_dial(target)
        report["clearance"] = clearance(target)
        write_new(args.output / "clearance-probe.json", report["clearance"])
        if report["clearance"]["status"] != "PASS":
            bpy.ops.wm.save_as_mainfile(filepath=str(args.output / "FAN-v3-diagnostic-only.blend"))
            raise RuntimeError("Construction clearance probe failed; inspect preserved probe before changing design")
        try:
            lod, report["lod1"] = build_lod(target, args.output)
        except Exception:
            bpy.ops.wm.save_as_mainfile(filepath=str(args.output / "FAN-v3-lod-diagnostic-only.blend"))
            raise
        report["geometry_after"] = CORE.geometry_report(CORE.meshes(target))
        if report["geometry_after"]["triangles"] > report["geometry_before"]["triangles"]:
            raise RuntimeError("V3 adds net polygon cost")
        CORE.assert_bounds(CORE.bounds(CORE.meshes(target)), receipt_v2["original_bounds_blender_metres"])
        grip = require("Grip_R")
        if grip_before != [list(row) for row in grip.matrix_world]:
            raise RuntimeError("Original grip changed before rigid parenting")
        grip.parent = require("FAN_V3_Root")
        grip.matrix_world = grip_world_before.copy()
        bpy.context.view_layer.update()
        report["grip_parentage"] = {
            "original_parent": grip_original_parent, "candidate_parent": grip.parent.name,
            "world_matrix_before": grip_before, "world_matrix_after": [list(row) for row in grip.matrix_world],
            "exact_world_matrix_preserved": grip_before == [list(row) for row in grip.matrix_world],
            "limit": "Rigid authoring parent only; exchange-format reimports must still preserve this relationship."
        }
        if grip_before != [list(row) for row in require("Grip_R").matrix_world]:
            raise RuntimeError("Original grip changed")
        for name in ("Source", "Exports", "Previews", "Motion"):
            (args.output / name).mkdir()
        for path in (Path(__file__), Path(__file__).with_name("fan_geometry.py"), Path(__file__).with_name("fan_yoke.py"), Path(__file__).with_name("fan_lod_bounds.py"), Path(__file__).with_name("fan_grip.py"), args.v2 / "Source/build_fan_v2.py"):
            shutil.copy2(path, args.output / "Source" / path.name)
        controls = CORE.collection("FANControls")
        for name in ("FAN_V3_Root", "FAN_TiltPivot_X", "FAN_RotorPivot_Y", "FAN_DialPivot_Z", "Grip_R"):
            obj = require(name)
            controls.objects.link(obj)
            if obj.name in target.objects:
                target.objects.unlink(obj)
            elif name != "Grip_R":
                raise RuntimeError("Expected candidate control membership: " + name)
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device, scene.cycles.samples = "CPU", args.samples
        scene.render.threads_mode, scene.render.threads = "FIXED", args.threads
        show("construction-v3")
        blend = args.output / "FAN-original-based-pilot-v3.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        exports, mesh_parentage = [], {}
        for group, name in ((target, "FAN-construction-v3"), (lod, "FAN-construction-v3-LOD1")):
            for suffix in (".glb", ".fbx"):
                path = args.output / "Exports" / (name + suffix)
                export_variant(group, path)
                exports.append(path)
                mesh_parentage[path.name] = {obj.name: obj.parent.name if obj.parent else None for obj in CORE.meshes(group)}
        checks = {path.name: reimport(path, receipt_v2["original_bounds_blender_metres"], mesh_parentage[path.name], grip_contract["blender_world_matrix_float32"]) for path in exports}
        report.update(status="SOURCE CANDIDATE; NATIVE VISUAL/UNITY/PHYSICS ACCEPTANCE PENDING", utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), blender=bpy.app.version_string,
                      blend_sha256=digest(blend), original_bounds_blender_metres=receipt_v2["original_bounds_blender_metres"], held_scale_unchanged=.4385,
                      grip_world_matrix_blender=grip_before, export_reimport_checks=checks,
                      exports={path.name: {"sha256": digest(path), "bytes": path.stat().st_size} for path in exports})
    except Exception as error:
        report.update(status="FAIL", error=str(error))
        raise
    finally:
        changed = [name for name, value in inputs.items() if not (args.root / name).is_file() or digest(args.root / name) != value]
        report["source_preservation"] = "PASS" if not changed else "FAIL"
        report["changed_inputs"] = changed
        write_new(args.output / "build-receipt.json", report)
        if changed:
            raise RuntimeError("V2/failed-build/diagnostic input preservation failed")


def render(args):
    global CORE
    CORE = load_core(args.v2)
    receipt = json.loads((args.output / "build-receipt.json").read_text(encoding="utf-8"))
    blend = args.output / "FAN-original-based-pilot-v3.blend"
    if receipt.get("source_preservation") != "PASS" or digest(blend) != receipt.get("blend_sha256"):
        raise RuntimeError("Saved candidate differs from receipt")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    scene.cycles.samples = args.samples
    scene.render.threads_mode, scene.render.threads = "FIXED", args.threads
    scene.render.resolution_x = scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    low, high = [Vector(point) for point in receipt["original_bounds_blender_metres"]]
    views = [(view, variant, 0, 0) for view in args.views for variant in VARIANTS] if args.stage == "render" else [("motion", "construction-v3", degrees, TILT_MAX * math.sin(math.radians(degrees))) for degrees in range(0, 361, 30)]
    for view, variant, spin, tilt in views:
        center = Vector((0, .065, .56)) if view == "detail" else Vector((0, -.03, .045)) if view == "dial" else (low + high) / 2
        offset = {"front": (0, -8, .4), "side": (8, 0, .4), "rear": (2.3, 3.6, 2.3), "detail": (2.2, 3, 1.8), "dial": (1, -2, 3)}.get(view, (2.3, -3.6, 2.3))
        scene.camera.location = center + Vector(offset)
        scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera.data.ortho_scale = .32 if view == "detail" else .25 if view == "dial" else max(high - low) * 1.28
        require("FAN_RotorPivot_Y").rotation_euler.y = math.radians(spin)
        require("FAN_TiltPivot_X").rotation_euler.x = math.radians(tilt)
        show(variant)
        path = args.output / ("Motion" if view == "motion" else "Previews") / ("FAN-" + view + "-" + variant + ("-" + str(spin).zfill(3) if view == "motion" else "") + ".png")
        meta = path.with_suffix(".json")
        record = {**CORE.settings_record(scene, args, view, receipt), "variant": variant, "rotor_degrees": spin, "tilt_degrees": tilt, "v3_script_sha256": digest(__file__)}
        if path.exists() or meta.exists():
            raise RuntimeError("Refusing to overwrite an existing/incomplete preview: " + str(path))
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        write_new(meta, {**record, "sha256": digest(path)})
    if digest(blend) != receipt["blend_sha256"]:
        raise RuntimeError("Render modified saved source")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--stage", choices=("build", "render", "motion"), required=True)
    parser.add_argument("--views", default=",".join(VIEWS))
    parser.add_argument("--size", type=int, default=640)
    parser.add_argument("--samples", type=int, default=16)
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--run-name", default="build-07")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    args.root = args.project_root.resolve()
    if not (args.root / "ProjectSettings/ProjectVersion.txt").is_file():
        raise RuntimeError("Expected actual Unity checkout")
    parent = args.root / "ArtSource/Collections/WordboundPvP"
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,39}", args.run_name):
        raise ValueError("Run name must be a simple lowercase basename")
    family = parent / "OriginalBasedPilotFANV3"
    args.v2, args.output = parent / "OriginalBasedPilotFANV2", family / args.run_name
    if family.resolve().parent != parent.resolve() or args.output.resolve().parent != family.resolve() or args.v2.resolve().parent != parent.resolve():
        raise RuntimeError("Source/output alias escapes approved ArtSource parent")
    args.views = args.views.split(",")
    if len(set(args.views)) != len(args.views) or any(view not in VIEWS for view in args.views):
        raise ValueError("Unknown/repeated view")
    if not 320 <= args.size <= 960 or not 16 <= args.samples <= 64 or not 1 <= args.threads <= 4:
        raise ValueError("Unbounded render settings")
    (build if args.stage == "build" else render)(args)


if __name__ == "__main__":
    main()
