"""Material-first FAN comparison; isolated source outputs, never a Unity import."""

import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import struct
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector


ORIGINAL_SHA = "67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7"
BLEND_SHA = "5e89db51071535ba3fa0aa8741121fe54f98ced6c5b048fdc3f62fa308e6f381"
VIEWS = ("three-quarter", "front", "side", "rear", "detail")
VARIANTS = {"original": "OriginalItem", "construction-v1": "ConstructionReferenceV1", "vivid-v2": "CandidateItem"}
SWATCHES = {
    "blue": ("2362C3", 0.0, 0.42),
    "metal": ("99AAB5", 0.9, 0.28),
    "ivory": ("F0D99F", 0.0, 0.5),
    "gold": ("EAB44B", 0.85, 0.28),
    "recess": ("202A34", 0.0, 0.76),
}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def meshes(collection):
    return [obj for obj in collection.all_objects if obj.type == "MESH"]


def bounds(objects):
    vertices = [obj.matrix_world @ vertex.co for obj in objects if obj.type == "MESH" for vertex in obj.data.vertices]
    if not vertices:
        raise RuntimeError("No mesh vertices")
    return [[min(v[axis] for v in vertices) for axis in range(3)], [max(v[axis] for v in vertices) for axis in range(3)]]


def assert_bounds(actual, expected, tolerance=0.00001):
    error = max(abs(actual[row][axis] - expected[row][axis]) for row in range(2) for axis in range(3))
    if error > tolerance:
        raise RuntimeError("Bounds changed by " + str(error) + " metres")
    return error


def collection(name):
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def parent_preserve_world(obj, parent):
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def empty(name, target, position=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.06
    target.objects.link(obj)
    obj.location = position
    bpy.context.view_layer.update()
    if parent:
        parent_preserve_world(obj, parent)
    return obj


def finite(values):
    return all(math.isfinite(float(value)) for value in values)


def geometry_report(objects):
    rows = []
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        bad_vertices = sum(not finite(vertex.co) for vertex in mesh.vertices)
        bad_normals = sum(not finite(normal.vector) or normal.vector.length < 0.5 for normal in mesh.corner_normals)
        zero_faces = sum(face.area < 1e-12 for face in mesh.polygons)
        bad_uv = sum(not finite(loop.uv) for layer in mesh.uv_layers for loop in layer.data)
        topology = bmesh.new()
        try:
            topology.from_mesh(mesh)
            raw_boundary = sum(edge.is_boundary for edge in topology.edges)
            bmesh.ops.remove_doubles(topology, verts=list(topology.verts), dist=1e-7)
            welded_boundary = sum(edge.is_boundary for edge in topology.edges)
            nonmanifold = sum(not edge.is_manifold for edge in topology.edges)
        finally:
            topology.free()
        row = {"name": obj.name, "vertices": len(mesh.vertices), "triangles": len(mesh.loop_triangles),
               "uv_layers": len(mesh.uv_layers), "nonfinite_vertices": bad_vertices,
               "nonfinite_or_short_corner_normals": bad_normals, "zero_area_faces": zero_faces,
               "nonfinite_uvs": bad_uv, "raw_boundary_edges": raw_boundary,
               "boundary_edges_after_diagnostic_weld": welded_boundary,
               "nonmanifold_edges_after_diagnostic_weld": nonmanifold,
               "world_determinant": obj.matrix_world.to_3x3().determinant()}
        if bad_vertices or bad_normals or bad_uv or zero_faces or row["world_determinant"] <= 0:
            raise RuntimeError("Invalid static geometry: " + json.dumps(row))
        rows.append(row)
    return {"mesh_count": len(rows), "triangles": sum(row["triangles"] for row in rows), "parts": rows,
            "uv_limit": "Missing UV layers are recorded. Constant PBR colors do not require UVs; painted-texture work remains pending.",
            "topology_limit": "Boundary/nonmanifold counts are diagnostic, not collider certification. Diagnostic welding changes no asset."}


def copy_reference(source):
    target = collection("ConstructionReferenceV1")
    for obj in meshes(source):
        clone = obj.copy()
        clone.data = obj.data.copy()
        target.objects.link(clone)
        clone.parent = None
        clone.matrix_world = obj.matrix_world.copy()
        clone.name = "V1_" + obj.name
    return target


def linear_channel(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def make_material(role):
    hex_rgb, metal, roughness = SWATCHES[role]
    srgb = [int(hex_rgb[index:index + 2], 16) / 255 for index in (0, 2, 4)]
    linear = [linear_channel(value) for value in srgb]
    material = bpy.data.materials.new("FAN_V2_" + role.title())
    material.use_nodes = True
    material.diffuse_color = (*linear, 1)
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = roughness
    if role == "blue":
        shader.inputs["Coat Weight"].default_value = 0.12
        shader.inputs["Coat Roughness"].default_value = 0.3
    material["source_srgb_hex"] = "#" + hex_rgb
    material["purpose"] = "Constant-material comparison; no painted texture or procedural-noise claim"
    return material, {"role": role, "srgb_hex": "#" + hex_rgb, "base_color_linear": linear,
                      "metallic": metal, "roughness": roughness,
                      "coat_weight": 0.12 if role == "blue" else 0}


def apply_materials(target):
    materials, records = {}, []
    for role in SWATCHES:
        materials[role], record = make_material(role)
        records.append(record)
    assignments = []
    for obj in meshes(target):
        if len(obj.data.materials) != 1:
            raise RuntimeError("Unexpected material layout: " + obj.name)
        name = obj.data.materials[0].name
        role = "recess" if name.startswith("FAN_MotorVentInterior") else name.removeprefix("FAN_OriginalBased_")
        if role not in materials:
            raise RuntimeError("Unrecognized source material: " + name)
        obj.data = obj.data.copy()
        obj.data.materials[0] = materials[role]
        assignments.append({"part": obj.name, "source_material": name, "new_role": role})
    return {"materials": records, "assignments": assignments,
            "detail_policy": "No geometry added; no extra guard wires, random noise, faux wear or emission."}


def rigid_controls(target):
    objects = meshes(target)
    names = {obj.name for obj in objects}
    required = {"FAN_OriginalBlade_08", "FAN_OriginalBlade_09", "FAN_OriginalBlade_10", "FAN_OriginalRotorCap_11",
                "FAN_ConnectedMotorShaft", "FAN_BlueBase_01", "FAN_BlueGuardRim_02", "FAN_OriginalStand_03"}
    if not required.issubset(names) or len(objects) != 68:
        raise RuntimeError("V1 component identities changed; do not infer a rig")
    before = bounds(objects)
    root = empty("FAN_V2_Root", target)
    tilt = empty("FAN_TiltPivot_X", target, (0, 0.072, 0.56), root)
    rotor = empty("FAN_RotorPivot_Y", target, (0, 0, 0.56), tilt)
    tilt["control"] = "Rotate local X. Neutral pose only validated; operating range and clearance are NOT TESTED."
    rotor["control"] = "Rotate local Y. No animation clip or runtime drive is installed."
    rotor_names = {"FAN_OriginalBlade_08", "FAN_OriginalBlade_09", "FAN_OriginalBlade_10", "FAN_OriginalRotorCap_11", "FAN_ConnectedMotorShaft"}
    base_names = {"FAN_BlueBase_01", "FAN_OriginalStand_03", "FAN_StandToTiltNeck", "FAN_TiltYokeCrossbar",
                  "FAN_LeftTiltSupport", "FAN_LeftTiltAxle", "FAN_LeftTiltFastener",
                  "FAN_RightTiltSupport", "FAN_RightTiltAxle", "FAN_RightTiltFastener",
                  "FAN_BaseSpeedDial", "FAN_DialIndicator"}
    assignments = {"rotor": [], "base": [], "tilt_head": []}
    for obj in objects:
        role = "rotor" if obj.name in rotor_names else ("base" if obj.name in base_names else "tilt_head")
        parent_preserve_world(obj, {"rotor": rotor, "base": root, "tilt_head": tilt}[role])
        assignments[role].append(obj.name)
    marker = bpy.data.objects.get("FAN_Grip_R_OriginalTransform")
    if marker is None or marker.name not in target.objects:
        raise RuntimeError("Original grip marker missing")
    grip_matrix = [list(row) for row in marker.matrix_world]
    parent_preserve_world(marker, root)
    marker.name = "Grip_R"
    bpy.context.view_layer.update()
    assert_bounds(bounds(objects), before)
    return {"type": "Rigid transform hierarchy, not skeletal skinning", "assignments": assignments,
            "grip_world_matrix_blender": grip_matrix,
            "motion_validation": "NOT RUN: no animated clearance, clips, collision or Unity controller has been tested."}


def visibility(variant):
    visible = VARIANTS[variant]
    for name in (*VARIANTS.values(), "HeldOriginal", "HeldCandidate", "OriginalAvatarContext"):
        group = bpy.data.collections.get(name)
        if group:
            group.hide_render = group.hide_viewport = name != visible
    bpy.context.view_layer.update()


def select_candidate():
    visibility("vivid-v2")
    bpy.ops.object.select_all(action="DESELECT")
    objects = list(bpy.data.collections["CandidateItem"].all_objects)
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["FAN_V2_Root"]
    return objects


def glb_manifest(path):
    raw = path.read_bytes()
    if raw[:4] != b"glTF" or struct.unpack_from("<I", raw, 4)[0] != 2 or struct.unpack_from("<I", raw, 8)[0] != len(raw):
        raise RuntimeError("Invalid exported GLB header")
    length, kind = struct.unpack_from("<II", raw, 12)
    if kind != 0x4E4F534A:
        raise RuntimeError("Missing GLB JSON chunk")
    doc = json.loads(raw[20:20 + length])
    return {"meshes": len(doc.get("meshes", [])), "nodes": len(doc.get("nodes", [])),
            "materials": len(doc.get("materials", [])), "animations": len(doc.get("animations", [])),
            "extensions_used": doc.get("extensionsUsed", []), "skins": len(doc.get("skins", []))}


def reimport(path, expected_bounds):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix == ".glb":
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    actual = bounds(objects)
    error = assert_bounds(actual, expected_bounds, tolerance=0.001)
    report = geometry_report(objects)
    names = {obj.name.split(".")[0] for obj in bpy.context.scene.objects}
    for expected in ("Grip_R", "FAN_TiltPivot_X", "FAN_RotorPivot_Y"):
        if expected not in names:
            raise RuntimeError("Export lost authoring control " + expected)
    return {"bounds_blender_metres": actual, "max_bounds_error_metres": error,
            "geometry": report, "rigid_controls_found": True,
            "limit": "Blender reimport, not Unity import/shader/physics verification"}


def build(args):
    out, source = args.output, args.v1
    if out.exists():
        raise RuntimeError("Preserve existing V2 output; build refuses to overwrite " + str(out))
    inputs = [source / "FAN-original-based-pilot.blend", source / "Source/FAN.original.glb",
              source / "build-receipt.json", source / "Source/build_wordbound_tool_pilots.py",
              source / "Source/build_wordbound_original_pilot.py"]
    input_hashes = {str(path.relative_to(args.root)): digest(path) for path in inputs}
    if digest(inputs[0]) != BLEND_SHA or digest(inputs[1]) != ORIGINAL_SHA:
        raise RuntimeError("Reviewed original/V1 source changed")
    receipt_v1 = json.loads(inputs[2].read_text(encoding="utf-8"))
    if receipt_v1["blend_sha256"] != BLEND_SHA or receipt_v1["source_sha256"] != ORIGINAL_SHA:
        raise RuntimeError("V1 receipt does not match reviewed source")
    bpy.ops.wm.open_mainfile(filepath=str(inputs[0]))
    target = bpy.data.collections["CandidateItem"]
    original_bounds = receipt_v1["original_bounds_blender_metres"]
    baseline = geometry_report(meshes(target))
    assert_bounds(bounds(meshes(target)), original_bounds)
    copy_reference(target)
    materials = apply_materials(target)
    controls = rigid_controls(target)
    after = geometry_report(meshes(target))
    if baseline["triangles"] != after["triangles"] or baseline["mesh_count"] != after["mesh_count"]:
        raise RuntimeError("Material/pivot work unexpectedly changed mesh counts")
    assert_bounds(bounds(meshes(target)), original_bounds)
    scene = bpy.context.scene
    scene.frame_set(1)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.threads_mode, scene.render.threads = "FIXED", args.threads
    visibility("vivid-v2")
    out.mkdir(parents=True)
    for name in ("Source", "Exports", "Previews"):
        (out / name).mkdir()
    shutil.copy2(__file__, out / "Source/build_fan_v2.py")
    shutil.copy2(inputs[1], out / "Source/FAN.original.glb")
    bpy.ops.file.pack_all()
    blend = out / "FAN-original-based-pilot-v2.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    select_candidate()
    glb, fbx = out / "Exports/FAN-vivid-v2.glb", out / "Exports/FAN-vivid-v2.fbx"
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", use_selection=True,
                              export_animations=False, export_yup=True, export_normals=True)
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"EMPTY", "MESH"},
                             bake_anim=False, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
                             path_mode="AUTO", use_mesh_modifiers=True)
    if not fbx.read_bytes().startswith(b"Kaydara FBX Binary"):
        raise RuntimeError("FBX export is missing expected binary signature")
    glb_structure = glb_manifest(glb)
    reimports = {path.suffix[1:]: reimport(path, original_bounds) for path in (glb, fbx)}
    for path in inputs:
        if digest(path) != input_hashes[str(path.relative_to(args.root))]:
            raise RuntimeError("Source preservation failed: " + str(path))
    write_json(out / "build-receipt.json", {
        "status": "STATIC SOURCE CANDIDATE; NOT UNITY IMPORTED OR VISUALLY APPROVED",
        "utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "blender": bpy.app.version_string,
        "script_sha256": digest(__file__), "source_hashes": input_hashes, "source_preservation": "PASS",
        "blend_sha256": digest(blend), "exports": {path.name: {"sha256": digest(path), "bytes": path.stat().st_size} for path in (glb, fbx)},
        "original_bounds_blender_metres": original_bounds, "held_scale_unchanged": receipt_v1["held_scale_from_original_config"],
        "geometry_before": baseline, "geometry_after": after, "materials": materials, "rigid_controls": controls,
        "glb_structure": glb_structure, "export_reimport_checks": reimports,
        "limits": ["No new texture painting, skin, skeleton, animation clip, collider or Unity asset was created.",
                   "No added geometry; V1 wire density and component count are unchanged.",
                   "Five material families and 68 mesh parts are not runtime cost approval; no LODs yet.",
                   "No full-rotation clearance, held fit, physics, online or player-build tests.",
                   "FBX may not preserve every coat/PBR setting. Unity material authoring remains required.",
                   "Vividness and appearance require inspecting actual matched images; source checks cannot approve art."]})
    print("FAN_V2_BUILT " + str(out), flush=True)


def settings_record(scene, args, view, receipt):
    return {"view": view, "blend_sha256": receipt["blend_sha256"], "script_sha256": digest(__file__),
            "camera_matrix": [list(row) for row in scene.camera.matrix_world], "ortho_scale": scene.camera.data.ortho_scale,
            "resolution": [args.size, args.size], "samples": args.samples, "threads": args.threads,
            "view_transform": scene.view_settings.view_transform, "look": scene.view_settings.look,
            "exposure": scene.view_settings.exposure, "gamma": scene.view_settings.gamma,
            "world": scene.world.name, "lights": [{"name": obj.name, "energy": obj.data.energy,
                "color": list(obj.data.color), "matrix": [list(row) for row in obj.matrix_world]} for obj in scene.objects if obj.type == "LIGHT"],
            "label": "Matched offline material/construction comparison; not Unity gameplay"}


def render(args):
    out = args.output
    receipt = json.loads((out / "build-receipt.json").read_text(encoding="utf-8"))
    blend = out / "FAN-original-based-pilot-v2.blend"
    if digest(blend) != receipt["blend_sha256"]:
        raise RuntimeError("Saved source differs from receipt; preserve edited version separately")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    scene.cycles.samples = args.samples
    scene.render.threads_mode, scene.render.threads = "FIXED", args.threads
    scene.render.resolution_x = scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    lo, hi = [Vector(point) for point in receipt["original_bounds_blender_metres"]]
    offsets = {"three-quarter": (2.3, -3.6, 2.3), "front": (0, -8, .4), "side": (8, 0, .4), "rear": (2.3, 3.6, 2.3), "detail": (2.2, 3, 1.8)}
    for view in args.views:
        center = Vector((0, .065, .56)) if view == "detail" else (lo + hi) / 2
        scene.camera.location = center + Vector(offsets[view])
        scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera.data.ortho_scale = .32 if view == "detail" else max(hi - lo) * 1.28
        bpy.context.view_layer.update()
        shared = settings_record(scene, args, view, receipt)
        for variant in VARIANTS:
            visibility(variant)
            path = out / "Previews" / ("FAN-" + view + "-" + variant + ".png")
            meta = path.with_suffix(".json")
            expected = {**shared, "variant": variant}
            if path.exists() or meta.exists():
                if not path.exists() or not meta.exists():
                    raise RuntimeError("Incomplete prior render; preserve before repair: " + str(path))
                previous = json.loads(meta.read_text(encoding="utf-8"))
                if digest(path) != previous.get("sha256") or any(previous.get(key) != value for key, value in expected.items()):
                    raise RuntimeError("Existing preview or settings changed: " + str(path))
                continue
            scene.render.filepath = str(path)
            started = time.monotonic()
            bpy.ops.render.render(write_still=True)
            write_json(meta, {**expected, "sha256": digest(path), "elapsed_seconds": round(time.monotonic() - started, 3)})
            print("FAN_V2_RENDERED " + str(path), flush=True)
    rows = []
    for view in VIEWS:
        cells = []
        for variant in VARIANTS:
            name = "FAN-" + view + "-" + variant + ".png"
            path = out / "Previews" / name
            cells.append('<td><img alt="' + variant + ' ' + view + '" src="Previews/' + name + '"></td>' if path.exists() else "<td>Not rendered</td>")
        rows.append('<tr><th colspan="3">' + view + '</th></tr><tr>' + ''.join(cells) + '</tr>')
    (out / "PILOT-REVIEW.html").write_text('<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>FAN material comparison</title><style>body{font:16px system-ui;background:#19212a;color:#f3f4f6;padding:24px;max-width:1500px;margin:auto}table{width:100%;table-layout:fixed}td{padding:5px}img{width:100%}th{text-align:left;padding-top:18px}</style><h1>FAN · original / construction / vivid paint</h1><p>Offline source candidate. Visual review, motion, Unity import and gameplay checks pending.</p><table><tr><th>Original game geometry</th><th>V1 construction</th><th>V2 material correction</th></tr>' + ''.join(rows) + '</table></html>\n', encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--stage", choices=("build", "render"), required=True)
    parser.add_argument("--views", default=",".join(VIEWS))
    parser.add_argument("--size", type=int, default=640)
    parser.add_argument("--samples", type=int, default=16)
    parser.add_argument("--threads", type=int, default=2)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    args.root = args.project_root.resolve()
    if not (args.root / "ProjectSettings/ProjectVersion.txt").is_file():
        raise RuntimeError("Expected actual Unity project root")
    parent = args.root / "ArtSource/Collections/WordboundPvP"
    args.v1, args.output = parent / "OriginalBasedPilotFANV1", parent / "OriginalBasedPilotFANV2"
    args.views = args.views.split(",")
    if len(set(args.views)) != len(args.views) or any(view not in VIEWS for view in args.views):
        raise ValueError("Unknown or repeated view")
    if not 320 <= args.size <= 960 or not 16 <= args.samples <= 64 or not 1 <= args.threads <= 4:
        raise ValueError("Unbounded CPU preview settings")
    (build if args.stage == "build" else render)(args)


if __name__ == "__main__":
    main()
