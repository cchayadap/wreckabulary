"""SOFA visual pilot from original Wreckabulary bytes; no runtime replacement."""

import argparse
import hashlib
import html
import json
import math
from pathlib import Path
import shutil
import sys
import time

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import numpy as np


SOURCE_SHA = "dfaca31fdc676de5d2d7fac74a4b6b26cbadea62bbd523f0739cbf7411705def"
VIEWS = ("three-quarter", "front", "side", "rear", "detail", "clay", "scale")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def collection(name):
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def move_to(obj, target):
    for old in tuple(obj.users_collection):
        old.objects.unlink(obj)
    target.objects.link(obj)


def import_original(path, target):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    result = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    for obj in result:
        move_to(obj, target)
    return result


def bounds(objects):
    points = [o.matrix_world @ v.co for o in objects if o.type == "MESH" for v in o.data.vertices]
    return (Vector([min(p[i] for p in points) for i in range(3)]),
            Vector([max(p[i] for p in points) for i in range(3)]))


def component_faces(mesh):
    keys = [tuple(round(c, 6) for c in v.co) for v in mesh.vertices]
    parent = {}

    def find(key):
        parent.setdefault(key, key)
        while parent[key] != key:
            parent[key] = parent[parent[key]]
            key = parent[key]
        return key

    for face in mesh.polygons:
        points = [(face.material_index, keys[i]) for i in face.vertices]
        first = find(points[0])
        for point in points[1:]:
            parent[find(point)] = first
    groups = {}
    for face in mesh.polygons:
        key = find((face.material_index, keys[face.vertices[0]]))
        groups.setdefault(key, []).append(face)
    return list(groups.values())


def make_part(source, faces, target, material):
    old = source.data
    indices = sorted({i for face in faces for i in face.vertices})
    mapping = {old_index: new_index for new_index, old_index in enumerate(indices)}
    matrix = source.matrix_world
    normal_matrix = matrix.to_3x3().inverted().transposed()
    mesh = bpy.data.meshes.new("OriginalPartGeometry")
    mesh.from_pydata([matrix @ old.vertices[i].co for i in indices], [],
                     [[mapping[i] for i in face.vertices] for face in faces])
    mesh.materials.append(material)
    source_loops = [i for face in faces for i in face.loop_indices]
    for layer in old.uv_layers:
        uv = mesh.uv_layers.new(name=layer.name)
        for new_loop, old_loop in enumerate(source_loops):
            uv.data[new_loop].uv = layer.data[old_loop].uv
    for face in mesh.polygons:
        face.use_smooth = True
    normals = [(normal_matrix @ old.corner_normals[i].vector).normalized() for i in source_loops]
    mesh.normals_split_custom_set(normals)
    mesh.update()
    obj = bpy.data.objects.new("OriginalPart", mesh)
    target.objects.link(obj)
    return obj


def identify(obj, original_material):
    lo, hi = bounds([obj])
    center = (lo + hi) / 2
    side = "Left" if center.x < 0 else "Right"
    if original_material.startswith("green_"):
        if hi.z < .38:
            return "UpholsteredBase"
        if hi.x - lo.x > 1:
            return "OuterBack"
        if abs(center.x) > .6:
            return side + "Arm"
        return side + ("SeatCushion" if center.z < .5 else "BackCushion")
    if original_material.startswith("cream_"):
        return side + "OriginalSeatWelt"
    if original_material.startswith("wood_dark_"):
        return side + "OriginalBackButton"
    return side + ("FrontFoot" if center.y < 0 else "RearFoot")


def fabric_material(source, name):
    material = source.copy()
    material.name = name
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .76
    shader.inputs["Sheen Weight"].default_value = .18
    shader.inputs["Sheen Roughness"].default_value = .55
    coordinates = nodes.new("ShaderNodeTexCoord")
    coordinates.label = "Physical scale: editable mesh coordinates are metres"
    texture = nodes.new("ShaderNodeTexNoise")
    texture.name = "SubmillimetreFabric"
    texture.inputs["Scale"].default_value = 720
    texture.inputs["Detail"].default_value = 1
    texture.inputs["Roughness"].default_value = .35
    links.new(coordinates.outputs["Object"], texture.inputs["Vector"])
    bump = nodes.new("ShaderNodeBump")
    bump.name = "RestrainedFabricRelief"
    bump.inputs["Strength"].default_value = .22
    bump.inputs["Distance"].default_value = .00022
    links.new(texture.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return material


def cushion_frame(obj):
    points = np.array([tuple(v.co) for v in obj.data.vertices])
    center = points.mean(axis=0)
    _, axes = np.linalg.eigh(np.cov((points - center).T))
    horizontal = Vector(axes[:, max(range(3), key=lambda i: abs(axes[0, i]))])
    horizontal *= 1 if horizontal.x > 0 else -1
    vertical = Vector(axes[:, max(range(3), key=lambda i: abs(axes[2, i]))])
    vertical *= 1 if vertical.z > 0 else -1
    normal = horizontal.cross(vertical).normalized()
    center = Vector(center)
    projections = [(Vector(p) - center) for p in points]
    half = [max(abs(p.dot(axis)) for p in projections) for axis in (horizontal, vertical, normal)]
    return center, horizontal, vertical, normal, half


def back_welt(obj, target, material):
    center, horizontal, vertical, normal, half = cushion_frame(obj)
    mesh = obj.data
    surface = BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],
                                   [list(face.vertices) for face in mesh.polygons])
    radius = .0013
    plane_center = center + normal * (half[2] * .55)
    reach = max(half[0], half[1]) * 2 + .02
    section = []
    for i in range(256):
        angle = math.tau * i / 256
        radial = (horizontal * (half[0] * math.cos(angle)) + vertical * (half[1] * math.sin(angle))).normalized()
        hit, _, _, _ = surface.ray_cast(plane_center + radial * reach, -radial, reach * 2)
        if hit is None:
            raise RuntimeError("Cushion panel seam missed actual surface: " + obj.name)
        section.append(hit)
    projected = np.array([((p-plane_center).dot(horizontal), (p-plane_center).dot(vertical)) for p in section])
    section_center = (projected.min(axis=0) + projected.max(axis=0)) / 2
    projected -= section_center
    plane_center += horizontal * float(section_center[0]) + vertical * float(section_center[1])
    width, height = np.abs(projected).max(axis=0)
    candidates = np.linspace(.002, min(width, height) * .95, 600)
    best_radius, best_error, best_residual = None, float("inf"), None
    for corner_radius in candidates:
        delta = np.abs(projected) - np.array((width-corner_radius, height-corner_radius))
        residual = np.linalg.norm(np.maximum(delta, 0), axis=1) + np.minimum(np.maximum(delta[:, 0], delta[:, 1]), 0) - corner_radius
        error = float(np.mean(residual * residual))
        if error < best_error:
            best_radius, best_error, best_residual = float(corner_radius), error, float(np.max(np.abs(residual)))
    values = []
    corners = ((width-best_radius, height-best_radius, 0), (-width+best_radius, height-best_radius, 90),
               (-width+best_radius, -height+best_radius, 180), (width-best_radius, -height+best_radius, 270))
    for index, (x, z, start) in enumerate(corners):
        for i in range(65):
            angle = math.radians(start + i * 90 / 64)
            outward = horizontal * math.cos(angle) + vertical * math.sin(angle)
            values.append(plane_center + horizontal * float(x) + vertical * float(z) + outward * (best_radius + radius * .30))
        end = values[-1]
        nx, nz, nstart = corners[(index+1) % 4]
        next_angle = math.radians(nstart)
        next_outward = horizontal * math.cos(next_angle) + vertical * math.sin(next_angle)
        next_start = plane_center + horizontal * float(nx) + vertical * float(nz) + next_outward * (best_radius + radius * .30)
        for i in range(1, 64):
            values.append(end.lerp(next_start, i / 64))
    turns = []
    for i, point in enumerate(values):
        incoming = point-values[i-1]
        outgoing = values[(i+1) % len(values)]-point
        turns.append(math.degrees(incoming.angle(outgoing)))
    if max(turns) > 1.5:
        raise RuntimeError("Fitted upholstery seam has a visible angular change: " + obj.name + " " + str(max(turns)))
    contact = []
    for i, point in enumerate(values):
        next_point = values[(i + 1) % len(values)]
        for fraction in (0, .25, .5, .75):
            probe = point.lerp(next_point, fraction)
            nearest, surface_normal, _, distance = surface.find_nearest(probe)
            if nearest is None:
                raise RuntimeError("Cannot verify upholstery seam contact: " + obj.name)
            signed_distance = distance if (probe-nearest).dot(surface_normal) >= 0 else -distance
            if abs(signed_distance) > radius * .75:
                raise RuntimeError("Upholstery seam floats or is excessively buried: " + obj.name + " " + str(signed_distance))
            contact.append(signed_distance)
    curve = bpy.data.curves.new(obj.name + "_WeltGeometry", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    spline.points.add(len(values) - 1)
    for point, value in zip(spline.points, values):
        point.co = (*value, 1)
    spline.use_cyclic_u = True
    curve.materials.append(material)
    welt = bpy.data.objects.new(obj.name.replace("Cushion", "Welt"), curve)
    target.objects.link(welt)
    welt["construction"] = "1.3 mm matching-fabric welt with smooth fitted side-band section and strict actual-surface contact; original cushion geometry retained"
    audit = {"part": welt.name, "radius_metres": radius, "surface_section_depth_fraction": .55,
             "curve_points": len(values), "contact_probes": len(contact),
             "measured_section_points": len(section), "fitted_corner_radius_metres": best_radius,
             "fit_maximum_2d_residual_metres": best_residual,
             "maximum_polyline_turn_degrees": max(turns),
             "min_signed_surface_distance_metres": min(contact),
             "max_signed_surface_distance_metres": max(contact),
             "maximum_allowed_absolute_distance_metres": radius * .75}
    return audit


def studio():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.view_settings.view_transform = "AgX"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("OriginalBased_NeutralStudio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (.78, .82, .85, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = .4
    scene.world = world
    for name, location, energy, size, color in (
        ("Key", (-3,-4,5),500,4,(1,.88,.73)),
        ("Fill", (4,-1,4),300,3.5,(.76,.85,1)),
        ("Rim", (0,4,4),400,3,(1,.92,.78))):
        light = bpy.data.lights.new(name, "AREA")
        light.energy, light.size, light.color = energy, size, color
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0,0,.45)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    floor = bpy.data.materials.new("NeutralStudioFloor")
    floor.use_nodes = True
    shader = floor.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (.67,.65,.58,1)
    shader.inputs["Roughness"].default_value = .96
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.005))
    bpy.context.object.name = "NeutralStudioFloor"
    bpy.context.object.data.materials.append(floor)
    camera = bpy.data.cameras.new("MatchedCamera")
    obj = bpy.data.objects.new("MatchedCamera", camera)
    scene.collection.objects.link(obj)
    scene.camera = obj
    camera.type = "ORTHO"
    clay = bpy.data.materials.new("NeutralClay")
    clay.diffuse_color = (.55,.55,.55,1)
    clay.use_nodes = True
    clay.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.55,.55,.55,1)
    clay.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = .7


def add_scale_context(source_root):
    target = collection("ScaleReference_OriginalGameAssets")
    art = source_root / "Web/public/art"
    for path, offset in ((art / "items/TABLE.glb", (0,-.9,0)), (art / "avatar.glb", (1.15,-.12,0))):
        objects = import_original(path, target)
        for obj in objects:
            if obj.parent is None:
                obj.location += Vector(offset)
        if path.name == "avatar.glb":
            wardrobe = json.loads((art / "manifest.json").read_text(encoding="utf-8"))["wardrobe"]
            defaults = {"SK_Head"} | {p["mesh"] for p in wardrobe["pieces"] if p["id"] in wardrobe["default"]["pieces"].values()}
            for obj in objects:
                if obj.type == "MESH" and obj.name.split(".")[0] not in defaults:
                    obj.hide_render = True
                if obj.type == "ARMATURE" and obj.animation_data:
                    for track in obj.animation_data.nla_tracks:
                        track.mute = True
                    obj.animation_data.action = next((a for a in bpy.data.actions if a.name == "Idle"), None)
    target.hide_render = True
    target.hide_viewport = True
    bpy.context.scene.frame_set(1)


def build(args, output):
    if output.exists():
        raise RuntimeError("Refusing to overwrite pilot destination; choose a new revision")
    source = args.source_root / "Web/public/art/items/SOFA.glb"
    if digest(source) != SOURCE_SHA:
        raise RuntimeError("Original SOFA bytes differ from inspected original game reference")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    original = collection("Original_SOFA_Unmodified")
    original_objects = import_original(source, original)
    original_bounds = bounds(original_objects)
    grip = next(obj for obj in original_objects if obj.name.startswith("Grip_R"))
    original_grip = grip.matrix_world.copy()
    candidate = collection("Candidate_SOFA_OriginalBasedPilot")
    material_copies, parts, geometry_record = {}, [], []
    for source_obj in original_objects:
        if source_obj.type != "MESH":
            continue
        for faces in component_faces(source_obj.data):
            original_material = source_obj.data.materials[faces[0].material_index]
            if original_material.name not in material_copies:
                if original_material.name.startswith(("green_", "cream_")):
                    material = fabric_material(original_material, "Pilot_" + original_material.name)
                else:
                    material = original_material.copy()
                    material.name = "Pilot_" + original_material.name
                material_copies[original_material.name] = material
            part = make_part(source_obj, faces, candidate, material_copies[original_material.name])
            part.name = "SOFA_" + identify(part, original_material.name)
            part.data.name = part.name + "_EditableGeometry"
            part["provenance"] = "Derived from unmodified original Wreckabulary SOFA.glb " + SOURCE_SHA
            lo, hi = bounds([part])
            geometry_record.append({"part": part.name, "source_vertices": len(part.data.vertices),
                                    "source_faces": len(part.data.polygons), "bounds": [list(lo), list(hi)]})
            parts.append(part)
    if len(parts) != 16 or sum("Cushion" in p.name for p in parts) != 4:
        raise RuntimeError("Unexpected original SOFA components; inspect before modifying")
    seam_contacts = []
    for part in parts:
        if "BackCushion" in part.name:
            seam_contacts.append(back_welt(part, candidate, part.data.materials[0]))
    candidate_grip = bpy.data.objects.new("SOFA_Grip_R_OriginalTransform", None)
    candidate.objects.link(candidate_grip)
    candidate_grip.matrix_world = original_grip
    candidate_grip.empty_display_size = .04
    candidate_bounds = bounds(parts)
    for expected, actual in zip(original_bounds, candidate_bounds):
        if (expected - actual).length > .000001:
            raise RuntimeError("Candidate primary geometry changed original overall bounds")
    studio()
    add_scale_context(args.source_root)
    original.hide_render, original.hide_viewport = True, True
    bpy.context.view_layer.update()
    output.mkdir(parents=True)
    (output / "Source").mkdir()
    (output / "Previews").mkdir()
    shutil.copy2(source, output / "Source/SOFA.original.glb")
    shutil.copy2(__file__, output / "Source/build_wordbound_original_pilot.py")
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "SOFA-original-based-pilot.blend"))
    manifest = {
        "status": "VISUAL PILOT — NOT APPROVED, NOT IMPORTED INTO UNITY",
        "source_path": "Web/public/art/items/SOFA.glb", "source_sha256": SOURCE_SHA,
        "build_script_sha256": digest(__file__), "blender": bpy.app.version_string,
        "source_bounds_blender_metres": [list(v) for v in original_bounds],
        "candidate_primary_bounds_blender_metres": [list(v) for v in candidate_bounds],
        "grip_world_matrix_blender": [list(row) for row in original_grip],
        "original_components": geometry_record,
        "primary_geometry_policy": "All original component positions, faces, UVs and transformed split normals retained; no cushion subdivision or shape replacement",
        "seam_contact_checks": seam_contacts,
        "candidate_mesh_parts": len(parts), "candidate_curve_welts": 2,
        "candidate_mesh_triangles": sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),
        "materials": [m.name for m in material_copies.values()],
        "limitations": ["Seam construction and material quality await actual rendered review", "Procedural fabric relief is editable Blender material; no baked Unity maps or delivery exports yet", "Static avatar/table scale scene is not a gameplay capture", "No runtime replacement, budget acceptance, owner acceptance or production readiness claimed"],
        "blend_sha256": digest(output / "SOFA-original-based-pilot.blend"),
    }
    write_json(output / "build-receipt.json", manifest)
    print("ORIGINAL_PILOT_BUILT " + str(output), flush=True)


def render(args, output):
    receipt = json.loads((output / "build-receipt.json").read_text(encoding="utf-8"))
    blend = output / "SOFA-original-based-pilot.blend"
    if digest(blend) != receipt["blend_sha256"]:
        raise RuntimeError("Blend differs from build receipt; preserve edit as a new reviewed revision")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    scene.cycles.samples = args.samples
    scene.render.threads_mode, scene.render.threads = "FIXED", args.threads
    original = bpy.data.collections["Original_SOFA_Unmodified"]
    candidate = bpy.data.collections["Candidate_SOFA_OriginalBasedPilot"]
    context = bpy.data.collections["ScaleReference_OriginalGameAssets"]
    offsets = {"three-quarter": (2.3,-3.6,2.3), "front": (0,-4,.2), "side": (4,0,.2),
               "rear": (2.3,3.6,1.9), "detail": (1,-3,1.2), "clay": (2.3,-3.6,2.3), "scale": (3.35,-6,3)}
    for view in args.views.split(","):
        if view not in VIEWS:
            raise ValueError("Unknown view " + view)
        target = Vector((.25,.02,.6)) if view == "detail" else Vector((0,0,.465))
        if view == "scale":
            target = Vector((.22,-.18,.53))
        scene.camera.location = target + Vector(offsets[view])
        scene.camera.rotation_euler = (target-scene.camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera.data.ortho_scale = .82 if view == "detail" else (3.7 if view == "scale" else 1.95)
        scene.render.resolution_x = args.size
        scene.render.resolution_y = round(args.size * 2 / 3) if view == "scale" else args.size
        scene.render.resolution_percentage = 100
        context.hide_render = view != "scale"
        context.hide_viewport = view != "scale"
        scene.view_layers[0].material_override = bpy.data.materials["NeutralClay"] if view == "clay" else None
        for variant in ("original", "candidate"):
            original.hide_render = original.hide_viewport = variant != "original"
            candidate.hide_render = candidate.hide_viewport = variant != "candidate"
            bpy.context.view_layer.update()
            path = output / "Previews" / ("SOFA-" + view + "-" + variant + ".png")
            metadata = path.with_suffix(".json")
            settings = {"blend_sha256": receipt["blend_sha256"], "render_script_sha256": digest(__file__),
                        "blender": bpy.app.version_string, "variant": variant, "view": view,
                        "camera_matrix": [list(row) for row in scene.camera.matrix_world],
                        "ortho_scale": scene.camera.data.ortho_scale,
                        "resolution": [scene.render.resolution_x, scene.render.resolution_y],
                        "samples": args.samples, "threads": args.threads, "denoising": True,
                        "label": "Static original-game scale reference, not gameplay" if view == "scale" else "Matched neutral studio preview"}
            if path.exists() or metadata.exists():
                if not path.exists() or not metadata.exists():
                    raise RuntimeError("Incomplete render exists; preserve it and use a new revision: " + str(path))
                existing = json.loads(metadata.read_text(encoding="utf-8"))
                changed = [key for key, value in settings.items() if existing.get(key) != value]
                if digest(path) != existing.get("sha256") or changed:
                    raise RuntimeError("Render bytes/settings changed; preserve it and use a new revision: " + str(path) + "; changed settings=" + ",".join(changed))
                print("ORIGINAL_PILOT_EXISTING_VERIFIED " + str(path), flush=True)
                continue
            scene.render.filepath = str(path)
            started = time.monotonic()
            bpy.ops.render.render(write_still=True)
            write_json(metadata, {**settings, "sha256": digest(path), "elapsed_seconds": round(time.monotonic()-started,3)})
            print("ORIGINAL_PILOT_RENDERED " + str(path), flush=True)
    gallery(output)


def gallery(output):
    rows = []
    for view in VIEWS:
        cells = []
        pair_settings = []
        for variant in ("original", "candidate"):
            path = output / "Previews" / ("SOFA-" + view + "-" + variant + ".png")
            meta = path.with_suffix(".json")
            if not path.exists() or not meta.exists():
                cells.append("<td>Not rendered</td>")
                continue
            metadata = json.loads(meta.read_text(encoding="utf-8"))
            if digest(path) != metadata["sha256"]:
                raise RuntimeError("Preview differs from its receipt: " + str(path))
            pair_settings.append({key: value for key, value in metadata.items() if key not in ("variant", "sha256", "elapsed_seconds")})
            relative = "Previews/" + path.name
            cells.append('<td><a href="' + relative + '"><img src="' + relative + '" alt="' + html.escape(view + " " + variant) + '"></a></td>')
        if all("Not rendered" in cell for cell in cells):
            continue
        if len(pair_settings) == 2 and pair_settings[0] != pair_settings[1]:
            raise RuntimeError("Original/candidate preview settings differ for " + view + "; refusing matched-comparison gallery")
        rows.append('<tr><th colspan="2">' + html.escape(view) + '</th></tr><tr>' + ''.join(cells) + '</tr>')
    page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Original Wreckabulary SOFA pilot</title><style>body{font:16px system-ui;background:#eeece5;color:#26352d;max-width:1250px;margin:auto;padding:24px}table{width:100%;border-collapse:collapse}td{width:50%;padding:8px;vertical-align:top}th{text-align:left;padding:20px 8px 4px}img{width:100%;border-radius:8px}a{color:#26634e}p{max-width:900px}</style>
<h1>Original Wreckabulary · SOFA pilot</h1><p>Visual candidate only. Original geometry and palette are the reference. Owner appearance acceptance, Unity import and performance checks remain pending. The scale view is a static studio scene using original game assets.</p>
<p><a href="SOFA-original-based-pilot.blend">Editable Blender scene</a> · <a href="build-receipt.json">Build provenance</a> · <a href="Source/SOFA.original.glb">Unmodified original input</a> · <a href="../../../../Web/public/art/supplied-showcase.png">Original game showcase</a></p>
<table><thead><tr><th>Original game asset</th><th>Original-based pilot</th></tr></thead><tbody>'''
    (output / "PILOT-REVIEW.html").write_text(page + ''.join(rows) + '</tbody></table></html>\n', encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--stage", choices=("build", "render"), required=True)
    parser.add_argument("--views", default="three-quarter,side,clay")
    parser.add_argument("--size", type=int, default=960)
    parser.add_argument("--samples", type=int, default=64)
    parser.add_argument("--threads", type=int, default=6)
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    args.source_root, args.output_root = args.source_root.resolve(), args.output_root.resolve()
    allowed = (args.source_root / "ArtSource/Collections/WordboundPvP").resolve()
    if args.output_root.parent != allowed or not args.output_root.name.startswith("OriginalBasedPilot"):
        raise RuntimeError("Output must be a new OriginalBasedPilot revision directly beneath ArtSource/Collections/WordboundPvP")
    if not 1 <= args.threads <= 6 or not 16 <= args.samples <= 256 or not 320 <= args.size <= 1600:
        raise ValueError("Preview settings exceed bounded pilot limits")
    (build if args.stage == "build" else render)(args, args.output_root)


if __name__ == "__main__":
    main()
