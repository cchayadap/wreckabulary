"""Editable, original-scale cosmetic variants. Run only in a fresh output folder."""

import argparse
from array import array
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector


SOURCE_HASHES = {
    "BAT": "f79c6a1465e6b1627953dc4baab9ef7a83b0cabd57b5c3ba8dbee11fc027ab15",
    "SHIELD": "c52b3c7c23f5cdd664bba168e11b1c803bfed57aee9cdd6a4b049f0fb58f5552",
}
BUDGETS = {"BAT": (3000, 1200), "SHIELD": (1200, 600)}
MATERIAL_NAMES = {"PackingClub_Surface", "PackingClub_Wrap"}
EXPECTED_EXPORTS = {(item, lod, kind) for item in SOURCE_HASHES for lod in (0, 1) for kind in ("glb", "fbx")}
PALETTE = (
    (0.82, 0.69, 0.48, 1),
    (0.70, 0.54, 0.34, 1),
    (0.94, 0.88, 0.73, 1),
    (0.30, 0.57, 0.76, 1),
    (0.44, 0.31, 0.19, 1),
)
HALF_TURN = Matrix.Rotation(math.pi, 4, "Z")
VIEWS = {"three-quarter": (2.3, -3.6, 2.3), "front": (0, -5, 0),
         "side": (5, 0, 0), "rear": (2.3, 3.6, 2.3)}


def utc():
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def file_record(path, output):
    return {"path": path.relative_to(output).as_posix(), "sha256": digest(path), "bytes": path.stat().st_size}


def collection(name):
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def bounds(objects, transform=None):
    transform = transform or Matrix.Identity(4)
    points = [transform @ obj.matrix_world @ vertex.co for obj in objects
              if obj.type == "MESH" for vertex in obj.data.vertices]
    if not points:
        raise RuntimeError("No mesh vertices to measure")
    return tuple(Vector([operation(point[axis] for point in points) for axis in range(3)])
                 for operation in (min, max))


def canonical_vector(point):
    return [round(point.x, 7), round(point.z, 7), round(-point.y, 7)]


def canonical_bounds(low, high):
    return [[round(low.x, 7), round(low.z, 7), round(-high.y, 7)],
            [round(high.x, 7), round(high.z, 7), round(-low.y, 7)]]


def new_empty(name, target, parent=None):
    obj = bpy.data.objects.new(name, None)
    target.objects.link(obj)
    obj.parent = parent
    obj.empty_display_size = 0.025
    return obj


def make_materials(output):
    image = bpy.data.images.new("PackingClubPalette", width=512, height=512, alpha=True)
    image.colorspace_settings.name = "sRGB"
    pixels = []
    for _ in range(512):
        for x in range(512):
            pixels.extend(PALETTE[min(len(PALETTE) - 1, x * len(PALETTE) // 512)])
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(output / "Exports" / "PackingClubPalette.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    materials = []
    for name, roughness in (("PackingClub_Surface", 0.68), ("PackingClub_Wrap", 0.86)):
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        nodes, links = material.node_tree.nodes, material.node_tree.links
        shader = nodes.get("Principled BSDF")
        shader.inputs["Roughness"].default_value = roughness
        shader.inputs["Metallic"].default_value = 0
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.interpolation = "Closest"
        links.new(texture.outputs["Color"], shader.inputs["Base Color"])
        material.use_backface_culling = True
        materials.append(material)
    return materials


def mesh_part(name, vertices, faces, target, parent, materials, swatches, wrap=False):
    mesh = bpy.data.meshes.new(name + "Geometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(materials[1 if wrap else 0])
    uv = mesh.uv_layers.new(name="PaletteUV")
    for face in mesh.polygons:
        swatch = swatches[face.index] if isinstance(swatches, list) else swatches
        for index in face.loop_indices:
            uv.data[index].uv = ((swatch + 0.5) / len(PALETTE), 0.5)
        face.use_smooth = len(face.vertices) == 4
    obj = bpy.data.objects.new(name, mesh)
    target.objects.link(obj)
    obj.parent = parent
    return obj


def ring_mesh(rings, segments, axis="Z"):
    vertices, faces = [], []
    for height, rx, ry, exponent in rings:
        for i in range(segments):
            angle = i * math.tau / segments
            c, s = math.cos(angle), math.sin(angle)
            x = rx * math.copysign(abs(c) ** (2 / exponent), c)
            y = ry * math.copysign(abs(s) ** (2 / exponent), s)
            vertices.append((x, y, height) if axis == "Z" else (x, height, y + 0.285))
    for ring in range(len(rings) - 1):
        for i in range(segments):
            a = ring * segments + i
            b = ring * segments + (i + 1) % segments
            faces.append((a, b, b + segments, a + segments))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(rings) - 1) * segments + i for i in range(segments)))
    return vertices, faces


def tube(path, width, thickness):
    vertices, faces = [], []
    for index, point in enumerate(path):
        tangent = Vector(path[min(index + 1, len(path) - 1)]) - Vector(path[max(0, index - 1)])
        tangent.normalize()
        normal = Vector((0, -tangent.z, tangent.y)).normalized()
        for x, sign in ((-width / 2, -1), (width / 2, -1), (width / 2, 1), (-width / 2, 1)):
            vertices.append(Vector(point) + Vector((x, 0, 0)) + normal * thickness * sign)
    for ring in range(len(path) - 1):
        for i in range(4):
            a, b = ring * 4 + i, ring * 4 + (i + 1) % 4
            faces.append((a, b, b + 4, a + 4))
    faces.extend(((3, 2, 1, 0), tuple((len(path) - 1) * 4 + i for i in range(4))))
    return vertices, faces


def bat(target, root, materials, lod):
    segments = 32 if lod == 0 else 16
    rings = [(0.025, .035, .035, 2), (.055, .025, .025, 2), (.26, .024, .024, 2),
             (.34, .033, .033, 2.3), (.47, .050, .050, 3.2), (.68, .072, .072, 4),
             (.90, .086, .086, 4), (.965, .086, .086, 4), (1.003, .077, .077, 3.4),
             (1.018, .063, .063, 3)]
    vertices, faces = ring_mesh(rings, segments)
    swatches = []
    for ring in range(len(rings) - 1):
        for i in range(segments):
            angle = (i + .5) * math.tau / segments
            swatches.append(1 if ring >= 3 and abs(math.sin(angle)) < .4 else 0)
    swatches.extend((0, 0))
    mesh_part("BarrelAndHandle", vertices, faces, target, root, materials, swatches)
    knob = [(0, .023, .023, 2), (.008, .033, .033, 2), (.019, .038, .038, 2),
            (.028, .036, .036, 2), (.042, .028, .028, 2)]
    mesh_part("RoundedKnob", *ring_mesh(knob, segments), target, root, materials, 0)
    steps = 84 if lod == 0 else 56
    vertices, faces = [], []
    for i in range(steps + 1):
        angle = math.tau * 7 * i / steps
        z = .065 + .19 * i / steps
        for radius, height in ((.0253, -.004), (.0288, -.004), (.0288, .004), (.0253, .004)):
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z + height))
    for i in range(steps):
        for side in range(4):
            a, b = i * 4 + side, i * 4 + (side + 1) % 4
            faces.append((a, b, b + 4, a + 4))
    faces.extend(((3, 2, 1, 0), tuple(steps * 4 + i for i in range(4))))
    mesh_part("ContinuousCreamWrap", vertices, faces, target, root, materials, 2, wrap=True)


def shield(target, root, materials, lod, original_bounds):
    segments = 48 if lod == 0 else 24
    sections = [(-.047, .05), (-.046, .88), (-.043, .92), (-.043, .975),
                (-.032, 1), (.008, 1), (.020, .965), (.021, .91), (.021, .05)]
    rings = [(depth, .23 * scale, .285 * scale, 2) for depth, scale in sections]
    vertices, faces = ring_mesh(rings, segments, axis="Y")
    swatches = [0 if 2 <= ring <= 6 else 3 for ring in range(len(rings) - 1) for _ in range(segments)]
    swatches.extend((3, 3))
    mesh_part("OvalBoardAndTimberRim", vertices, faces, target, root, materials, swatches)
    vertices = []
    for depth in (-.044, original_bounds[0].y):
        for i in range(10):
            angle = math.pi / 2 + i * math.pi / 5
            radius = .084 if i % 2 == 0 else .038
            vertices.append((radius * math.cos(angle), depth, .31 + radius * math.sin(angle)))
    faces = [(i, (i + 1) % 10, (i + 1) % 10 + 10, i + 10) for i in range(10)]
    faces.extend((tuple(reversed(range(10))), tuple(range(10, 20))))
    mesh_part("InsetCreamStar", vertices, faces, target, root, materials, 2, wrap=True)
    steps = 16 if lod == 0 else 8
    path = [(0, .016, .365)]
    path.extend((0, .035 + .075 * math.sin(i * math.pi / steps), .29 + .075 * math.cos(i * math.pi / steps))
                for i in range(steps + 1))
    path.append((0, .016, .215))
    mesh_part("BentBackStrap", *tube(path, .060, original_bounds[1].y - .110),
              target, root, materials, 4, wrap=True)


def inspect(objects, budget, expected_bounds, expected_grip):
    objects = list(objects)
    low, high = bounds(objects)
    errors, parts, material_names = [], [], set()
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
        volume = bm.calc_volume(signed=True)
        bm.free()
        bad_triangles = sum(triangle.area < 1e-12 for triangle in mesh.loop_triangles)
        slots = list(mesh.materials)
        used_slots = {face.material_index for face in mesh.polygons}
        valid_slots = bool(slots) and bool(used_slots) and all(material is not None for material in slots) and all(
            0 <= index < len(slots) for index in used_slots)
        if not valid_slots:
            errors.append(obj.name + ": missing material slot or invalid face material index")
        part_materials = sorted({slots[index].name for index in used_slots}) if valid_slots else []
        if valid_slots and (len(slots) > 2 or len(part_materials) != len(slots) or used_slots != set(range(len(slots))) or
                            not set(part_materials).issubset(MATERIAL_NAMES)):
            errors.append(obj.name + ": unused or unexpected material identity")
        material_names.update(part_materials)
        if nonmanifold or volume <= 0 or bad_triangles or not mesh.uv_layers:
            errors.append(obj.name + ": closed positive-volume mesh / nondegenerate triangles / UV validation failed")
        parts.append({"name": obj.name, "triangles": len(mesh.loop_triangles), "vertices": len(mesh.vertices),
                      "nonmanifold_edges": nonmanifold, "degenerate_triangles": bad_triangles,
                      "signed_volume": volume, "materials": part_materials})
    triangles = sum(part["triangles"] for part in parts)
    error = max(abs(actual[i] - expected[i]) for actual, expected in zip((low, high), expected_bounds) for i in range(3))
    if error > .00001:
        errors.append("Original envelope drift: " + str(error))
    if triangles <= 0 or triangles > budget or material_names != MATERIAL_NAMES:
        errors.append("Triangle budget or exact assigned material identities invalid")
    grip = next((obj for obj in objects if obj.get("packing_club_socket") == "Grip_R"), None)
    if grip is None or max(abs(grip.matrix_world[r][c] - expected_grip[r][c]) for r in range(4) for c in range(4)) > .00001:
        errors.append("Original composed grip matrix changed")
    if errors:
        raise RuntimeError("; ".join(errors))
    return {"triangles": triangles, "triangle_budget": budget, "materials": sorted(material_names),
            "parts": parts, "bounds_blender": [list(low), list(high)], "bounds_game_metres": canonical_bounds(low, high),
            "grip_game_metres": canonical_vector(grip.matrix_world.translation),
            "grip_matrix_blender": [list(row) for row in grip.matrix_world], "max_envelope_error_metres": error}


def export_model(target, item, lod, output):
    source_scene = bpy.context.scene
    scene = bpy.data.scenes.new("PackingClub export")
    bpy.context.window.scene = scene
    export_root = bpy.data.objects.new(item + "_PackingClub", None)
    scene.collection.objects.link(export_root)
    meshes = []
    for obj in target.all_objects:
        if obj.type != "MESH" and obj.get("packing_club_socket") != "Grip_R":
            continue
        copy = obj.copy()
        if obj.type == "MESH":
            copy.data = obj.data.copy()
            meshes.append(copy)
        else:
            copy.name = "Grip_R"
        scene.collection.objects.link(copy)
        copy.parent = export_root
        copy.matrix_local = obj.matrix_world.copy()
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    bpy.context.object.name = item + "_PackingClubMesh"
    for obj in scene.objects:
        obj.select_set(True)
    suffix = "" if lod == 0 else "_LOD1"
    glb = output / "Exports" / (item + suffix + ".glb")
    fbx = output / "Exports" / (item + suffix + ".fbx")
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", use_selection=True,
                              export_yup=True, export_apply=True, export_animations=False,
                              export_cameras=False, export_lights=False, export_extras=True)
    export_root.matrix_world = HALF_TURN
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             use_tspace=False, use_custom_props=True, add_leaf_bones=False, bake_anim=False,
                             path_mode="RELATIVE", embed_textures=False)
    bpy.context.window.scene = source_scene
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(scene)
    return {"glb": file_record(glb, output), "fbx": file_record(fbx, output)}


def studio():
    scene = bpy.context.scene
    world = bpy.data.worlds.new("PackingClub warm studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (.78, .82, .85, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = .4
    scene.world = world
    for name, position, energy, size, color in (("Key", (-3, -4, 5), 500, 4, (1, .88, .73)),
                                               ("Fill", (4, -1, 4), 300, 3.5, (.76, .85, 1)),
                                               ("Rim", (0, 4, 4), 400, 3, (1, .92, .78))):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = energy, size, color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector((0, 0, .5)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("PackingClub camera", bpy.data.cameras.new("PackingClub camera"))
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.render.threads_mode = "FIXED"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "AgX"


def build(args):
    output = args.output
    if output.exists():
        raise RuntimeError("Preserve previous output; choose a new output folder: " + str(output))
    sources = {item: args.repo / "Web/public/art/items" / (item + ".glb") for item in SOURCE_HASHES}
    for item, source in sources.items():
        if digest(source) != SOURCE_HASHES[item]:
            raise RuntimeError("Original reference changed: " + item)
    definitions = json.loads((args.repo / "Assets/_Project/Data/Config/items.json").read_text(encoding="utf-8"))["items"]
    definitions = {row["id"]: row for row in definitions}
    output.mkdir(parents=True)
    for folder in ("Source", "Exports", "Icons", "Previews"):
        (output / folder).mkdir()
    shutil.copy2(__file__, output / "Source" / "packing_club.py")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    materials = make_materials(output)
    records = []
    for item, source in sources.items():
        original = collection(item + "_Original")
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(source))
        objects = [obj for obj in bpy.data.objects if obj not in before]
        bpy.context.view_layer.update()
        original_bounds = bounds(objects)
        original_grip = next(obj.matrix_world.copy() for obj in objects if obj.name.startswith("Grip_R"))
        expected_grip = definitions[item]["grip"]
        if max(abs(a - b) for a, b in zip(canonical_vector(original_grip.translation), expected_grip)) > .00001:
            raise RuntimeError("Composed original grip does not agree with catalogue: " + item)
        for obj in objects:
            for prior in tuple(obj.users_collection):
                prior.objects.unlink(obj)
            original.objects.link(obj)
            obj.name = item + "_Original_" + obj.name
        original.hide_render = original.hide_viewport = True
        copied = output / "Source" / ("original-" + item + ".glb")
        shutil.copy2(source, copied)
        for lod in (0, 1):
            target = collection(item + "_PackingClub_LOD" + str(lod))
            root = new_empty(item + "_PackingClub_LOD" + str(lod), target)
            root["item_id"], root["skin_id"], root["lod"] = item, "PackingClub", lod
            root["collision_owner"] = "canonical gameplay; this hierarchy is render-only"
            if item == "BAT":
                bat(target, root, materials, lod)
            else:
                shield(target, root, materials, lod, original_bounds)
            grip = new_empty(item + "_CandidateGrip_LOD" + str(lod), target, root)
            grip.matrix_world = original_grip
            grip["packing_club_socket"] = "Grip_R"
            bpy.context.view_layer.update()
            record = inspect(target.all_objects, BUDGETS[item][lod], original_bounds, original_grip)
            record.update({"item_id": item, "skin_id": "PackingClub", "lod": lod,
                           "collection": target.name, "original_collection": original.name,
                           "original": file_record(copied, output), "held_scale": definitions[item]["heldScale"],
                           "canonical_gameplay_size": definitions[item]["size"],
                           "exports": export_model(target, item, lod, output)})
            records.append(record)
            target.hide_render = target.hide_viewport = True
    studio()
    bpy.data.collections["BAT_PackingClub_LOD0"].hide_render = False
    bpy.data.collections["BAT_PackingClub_LOD0"].hide_viewport = False
    bpy.context.scene.camera.location = (2.3, -3.6, 2.8)
    bpy.context.scene.camera.rotation_euler = (Vector((0, 0, .509)) - bpy.context.scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera.data.ortho_scale = 1.3
    bpy.ops.file.pack_all()
    blend = output / "Source" / "PackingClub.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    manifest = {"schema": 1, "created_utc": utc(), "collection": "PackingClub", "version": 1,
                "status": "AUTHORED SOURCE; export round-trip, Unity and owner visual acceptance pending",
                "blender": bpy.app.version_string, "script_sha256": digest(__file__),
                "source_blend": file_record(blend, output),
                "palette": file_record(output / "Exports" / "PackingClubPalette.png", output),
                "provenance": {"method": "local parameterized Blender geometry", "provider_jobs": [],
                               "provider_spend": 0, "reference_rights": "Existing owner-provided game assets; no new third-party content"},
                "coordinates": {"source": "Blender metres +Z up, front -Y, right +X",
                                "glb": "glTF metres +Y up, front +Z; original reference basis",
                                "fbx": "Source root gets +180 degrees about Blender Z before axis -Z/Y export, matching build_assets.face_unity_forward",
                                "unity": "Retain imported FBX root transforms. Validate bounds/socket after import, then use canonical fitting."},
                "runtime": {"physics": "unchanged", "lod_switching": "NOT IMPLEMENTED", "unity_import": "NOT RUN",
                            "model_binding": "Additive (itemId, skinId); Classic/Candy/Arcade remain intact"}, "assets": records}
    write_json(output / "manifest.json", manifest)
    print("PACKING_CLUB_BUILT " + str(output), flush=True)


def unique_json_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate manifest key: " + key)
        result[key] = value
    return result


def checked_file(output, record, expected_path):
    if not isinstance(record, dict) or record.get("path") != expected_path:
        raise ValueError("Unexpected manifest file path: " + expected_path)
    path = (output / expected_path).resolve()
    if not path.is_relative_to(output.resolve()) or not path.is_file():
        raise ValueError("Missing or escaping manifest file: " + expected_path)
    if type(record.get("bytes")) is not int or record["bytes"] != path.stat().st_size or digest(path) != record.get("sha256"):
        raise ValueError("File bytes or SHA-256 differ: " + expected_path)
    return path


def image_pixels(image):
    if tuple(image.size) != (512, 512) or image.channels not in (3, 4) or image.colorspace_settings.name != "sRGB":
        raise ValueError("Palette must decode as 512 x 512 sRGB RGB/RGBA")
    channels, remainder = divmod(len(image.pixels), 512 * 512)
    if remainder or channels not in (3, 4):
        raise ValueError("Palette pixel buffer dimensions differ")
    pixels = array("f", [0]) * len(image.pixels)
    image.pixels.foreach_get(pixels)
    if not all(math.isfinite(value) and -.00001 <= value <= 1.00001 for value in pixels):
        raise ValueError("Palette contains invalid pixel values")
    if channels == 3:
        rgba = array("f")
        for offset in range(0, len(pixels), 3):
            rgba.extend(pixels[offset:offset + 3])
            rgba.append(1)
        return rgba
    return pixels


def palette_pixels(path):
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        return image_pixels(image)
    finally:
        bpy.data.images.remove(image)


def inspect_palette_bindings(objects, palette_path, kind):
    expected_pixels = palette_pixels(palette_path)
    materials = {}
    for obj in objects:
        if obj.type == "MESH":
            for face in obj.data.polygons:
                material = obj.data.materials[face.material_index]
                materials[material.name] = material
    if set(materials) != MATERIAL_NAMES:
        raise ValueError("Palette binding requires both assigned collection materials")
    bindings = []
    for name, material in sorted(materials.items()):
        if not material.use_nodes or not material.node_tree:
            raise ValueError(name + ": material has no shader graph")
        outputs = [node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL" and node.is_active_output]
        if len(outputs) != 1 or len(outputs[0].inputs["Surface"].links) != 1:
            raise ValueError(name + ": no unique connected surface output")
        shader = outputs[0].inputs["Surface"].links[0].from_node
        if shader.type != "BSDF_PRINCIPLED" or len(shader.inputs["Base Color"].links) != 1:
            raise ValueError(name + ": expected connected Principled base color")
        link = shader.inputs["Base Color"].links[0]
        texture = link.from_node
        if texture.type != "TEX_IMAGE" or link.from_socket.name != "Color" or texture.image is None:
            raise ValueError(name + ": the base color must use the palette image")
        image = texture.image
        if kind == "fbx":
            reference = Path(bpy.path.abspath(image.filepath, library=image.library)).resolve()
            if reference != palette_path.resolve() or not reference.is_file():
                raise ValueError(name + ": FBX palette does not resolve to the verified external palette")
        actual_pixels = image_pixels(image)
        error = max(abs(actual - expected) for actual, expected in zip(actual_pixels, expected_pixels))
        if error > 1 / 255 + .00001:
            raise ValueError(name + ": decoded palette content differs")
        bindings.append({"material": name, "image_size": [512, 512], "max_decoded_channel_error": error,
                         "decoded_rgba_sha256": hashlib.sha256(actual_pixels.tobytes()).hexdigest(),
                         "binding": "Principled Base Color <- Image Color",
                         "verified_external_reference": kind == "fbx"})
    return bindings


def load_manifest(args):
    manifest = json.loads((args.output / "manifest.json").read_text(encoding="utf-8"), object_pairs_hook=unique_json_object)
    if not isinstance(manifest, dict) or manifest.get("schema") != 1 or manifest.get("collection") != "PackingClub" or manifest.get("version") != 1:
        raise ValueError("Unsupported Packing Club manifest schema/identity")
    records = manifest.get("assets")
    if not isinstance(records, list) or len(records) != 4:
        raise ValueError("Manifest must contain exactly four item/LOD records and eight exports")
    actual_exports = set()
    for record in records:
        if not isinstance(record, dict):
            raise ValueError("Invalid asset record")
        item, lod = record.get("item_id"), record.get("lod")
        if item not in SOURCE_HASHES or type(lod) is not int or lod not in (0, 1) or record.get("skin_id") != "PackingClub":
            raise ValueError("Unexpected item, LOD or skin identity")
        exports = record.get("exports")
        if not isinstance(exports, dict) or set(exports) != {"glb", "fbx"}:
            raise ValueError("Each item/LOD must contain exactly GLB and FBX exports")
        if record.get("triangle_budget") != BUDGETS[item][lod] or type(record.get("triangles")) is not int or not 0 < record["triangles"] <= BUDGETS[item][lod]:
            raise ValueError("Invalid recorded triangle count or budget")
        suffix = "" if lod == 0 else "_LOD1"
        for kind in ("glb", "fbx"):
            key = (item, lod, kind)
            if key in actual_exports:
                raise ValueError("Duplicate item/LOD/export record")
            actual_exports.add(key)
            checked_file(args.output, exports[kind], "Exports/" + item + suffix + "." + kind)
        original = checked_file(args.output, record.get("original"), "Source/original-" + item + ".glb")
        if digest(original) != SOURCE_HASHES[item]:
            raise ValueError("Original reference hash differs: " + item)
    if actual_exports != EXPECTED_EXPORTS:
        raise ValueError("Incomplete BAT/SHIELD x LOD0/LOD1 x GLB/FBX matrix")
    if digest(__file__) != manifest["script_sha256"]:
        raise RuntimeError("Script changed since build; preserve output and build a new revision")
    if digest(args.output / "Source/packing_club.py") != manifest["script_sha256"]:
        raise RuntimeError("Saved generator source changed")
    blend = checked_file(args.output, manifest.get("source_blend"), "Source/PackingClub.blend")
    palette = checked_file(args.output, manifest.get("palette"), "Exports/PackingClubPalette.png")
    palette_pixels(palette)
    return manifest, blend


def verify(args):
    report_path = args.output / "export-verification.json"
    if report_path.exists():
        raise RuntimeError("Preserve the existing verification receipt")
    try:
        manifest, _ = load_manifest(args)
    except Exception as error:
        if args.output.is_dir():
            manifest_path = args.output / "manifest.json"
            write_json(report_path, {"created_utc": utc(), "result": "FAIL", "stage": "manifest_and_palette",
                                    "manifest_sha256": digest(manifest_path) if manifest_path.is_file() else None,
                                    "expected_export_count": 8, "checked_export_count": 0,
                                    "error": str(error), "checks": [], "unity_import": "NOT RUN"})
        raise
    checks = []
    for record in manifest["assets"]:
        for kind, exported in record["exports"].items():
            path = args.output / exported["path"]
            check = {"item_id": record["item_id"], "lod": record["lod"], "format": kind,
                     "export_sha256": exported["sha256"]}
            try:
                if digest(path) != exported["sha256"]:
                    raise RuntimeError("Export hash differs: " + str(path))
                bpy.ops.wm.read_factory_settings(use_empty=True)
                if kind == "fbx":
                    bpy.ops.import_scene.fbx(filepath=str(path), use_custom_normals=True)
                else:
                    bpy.ops.import_scene.gltf(filepath=str(path))
                objects = list(bpy.context.scene.objects)
                if kind == "fbx":
                    for obj in objects:
                        if obj.parent is None:
                            obj.matrix_world = HALF_TURN @ obj.matrix_world
                bpy.context.view_layer.update()
                for obj in objects:
                    if obj.name == "Grip_R":
                        obj["packing_club_socket"] = "Grip_R"
                expected_bounds = tuple(Vector(point) for point in record["bounds_blender"])
                actual = inspect(objects, record["triangle_budget"], expected_bounds, Matrix(record["grip_matrix_blender"]))
                if actual["triangles"] != record["triangles"]:
                    raise RuntimeError("Export triangle count changed: " + str(path))
                if any(obj.type not in {"EMPTY", "MESH"} for obj in objects):
                    raise RuntimeError("Export contains a camera, light or rig: " + str(path))
                if sum(obj.type == "MESH" for obj in objects) != 1:
                    raise RuntimeError("Runtime export must be one combined rigid mesh")
                bindings = inspect_palette_bindings(objects, args.output / "Exports/PackingClubPalette.png", kind)
                check.update({"result": "PASS", "palette_bindings": bindings, **actual})
            except Exception as error:
                check.update({"result": "FAIL", "error": str(error)})
            checks.append(check)
            print("PACKING_CLUB_EXPORT_CHECK " + json.dumps(check), flush=True)
    observed = {(check["item_id"], check["lod"], check["format"]) for check in checks}
    passed = len(checks) == 8 and observed == EXPECTED_EXPORTS and all(check["result"] == "PASS" for check in checks)
    write_json(report_path, {"created_utc": utc(), "manifest_sha256": digest(args.output / "manifest.json"),
                            "blender": bpy.app.version_string, "result": "PASS" if passed else "FAIL", "checks": checks,
                            "expected_export_count": 8, "checked_export_count": len(checks),
                            "verified_palette_sha256": manifest["palette"]["sha256"],
                            "unity_import": "NOT RUN", "visual_acceptance": "PENDING OWNER REVIEW"})
    if not passed:
        raise RuntimeError("Packing Club export round-trip failed; inspect preserved receipt")
    print("PACKING_CLUB_VERIFIED " + str(len(checks)), flush=True)


def render(args):
    manifest, blend = load_manifest(args)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene = bpy.context.scene
    scene.cycles.samples = args.samples
    scene.render.threads = args.threads
    scene.render.resolution_x = scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    all_collections = [bpy.data.collections[name] for record in manifest["assets"]
                       for name in (record["collection"], record["original_collection"])]
    for record in (entry for entry in manifest["assets"] if entry["lod"] == 0):
        low, high = (Vector(value) for value in record["bounds_blender"])
        center = (low + high) / 2
        scene.camera.data.ortho_scale = max(high - low) * 1.28
        for view, offset in VIEWS.items():
            scene.camera.location = center + Vector(offset)
            scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
            bpy.context.view_layer.update()
            for variant, name in (("original", record["original_collection"]), ("candidate", record["collection"])):
                for target in all_collections:
                    target.hide_render = target.hide_viewport = target.name != name
                path = args.output / "Previews" / (record["item_id"] + "-" + view + "-" + variant + ".png")
                metadata = path.with_suffix(".json")
                settings = {"item_id": record["item_id"], "variant": variant, "view": view,
                            "blend_sha256": manifest["source_blend"]["sha256"], "script_sha256": digest(__file__),
                            "camera_matrix": [list(row) for row in scene.camera.matrix_world],
                            "ortho_scale": scene.camera.data.ortho_scale, "resolution": [args.size, args.size],
                            "samples": args.samples, "threads": args.threads, "blender": bpy.app.version_string}
                if path.exists() or metadata.exists():
                    prior = json.loads(metadata.read_text(encoding="utf-8")) if metadata.exists() else {}
                    if not path.exists() or digest(path) != prior.get("sha256") or any(prior.get(k) != v for k, v in settings.items()):
                        raise RuntimeError("Existing render differs; preserve it: " + str(path))
                else:
                    started = time.monotonic()
                    scene.render.filepath = str(path)
                    bpy.ops.render.render(write_still=True)
                    image = bpy.data.images.load(str(path), check_existing=False)
                    alpha = list(image.pixels)[3::4]
                    border = alpha[:args.size] + alpha[-args.size:] + alpha[::args.size] + alpha[args.size - 1::args.size]
                    if min(alpha) > .01 or max(alpha) < .99 or max(border) > .05 or sum(a > .5 for a in alpha) < len(alpha) * .01:
                        raise RuntimeError("Transparent render has missing or clipped content: " + str(path))
                    bpy.data.images.remove(image)
                    write_json(metadata, {**settings, "sha256": digest(path), "created_utc": utc(),
                                          "elapsed_seconds": round(time.monotonic() - started, 3),
                                          "alpha_background_and_content": "PASS", "human_visual_review": "NOT RUN"})
                if variant == "candidate" and view == "three-quarter":
                    icon = args.output / "Icons" / (record["item_id"] + ".png")
                    if icon.exists() and digest(icon) != digest(path):
                        raise RuntimeError("Existing icon differs; preserve it: " + str(icon))
                    if not icon.exists():
                        shutil.copy2(path, icon)
                print("PACKING_CLUB_RENDERED " + str(path), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify", "render"))
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--samples", type=int, default=64)
    parser.add_argument("--threads", type=int, default=6)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    args.repo, args.output = args.repo.resolve(), args.output.resolve()
    allowed = (args.repo / "ArtSource/Collections/PackingClub").resolve()
    if not args.output.is_relative_to(allowed) or args.output == allowed:
        raise ValueError("Output must be a new child folder inside this repository's ArtSource/Collections/PackingClub")
    if not 128 <= args.size <= 1024 or not 1 <= args.samples <= 256 or not 1 <= args.threads <= 16:
        raise ValueError("Render settings exceed bounded local limits")
    {"build": build, "verify": verify, "render": render}[args.mode](args)


if __name__ == "__main__":
    main()
