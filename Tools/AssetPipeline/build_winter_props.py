"""Author the original Winter House Party meshes; all output stays outside Unity Assets."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = REPO / "ArtSource/Collections/Winter/Props"
PALETTE = ["34785B", "245843", "B84851", "F2DFB2", "99CAD2", "604139", "FAF1D7", "D69D57"]
FIR, DEEP_FIR, CRANBERRY, OAT, ICE, WALNUT, SNOW, GOLD = range(8)


def linear_channel(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def rgb(hex_value):
    return tuple(linear_channel(int(hex_value[i:i + 2], 16) / 255) for i in (0, 2, 4))


def material(name, colour, roughness=.58, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*colour, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    mat.diffuse_color = (*colour, 1)
    return mat


def palette_material(output):
    image = bpy.data.images.new("WinterPalette", width=512, height=64, alpha=False)
    colours = [rgb(value) for value in PALETTE]
    pixels = []
    for y in range(64):
        for x in range(512):
            # Subtle deterministic weave remains inside each padded solid-colour cell.
            grain = 1 + .012 * math.sin(x * 1.7) * math.sin(y * 1.3)
            pixels.extend([min(1, c * grain) for c in colours[x // 64]] + [1])
    image.pixels = pixels
    image.filepath_raw = str(output / "exports/WinterPalette.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    mat = material("WinterPaint", (1, 1, 1), .64)
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    mat.node_tree.links.new(node.outputs["Color"], mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
    return mat


def link(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_size = .06
    obj.parent = parent
    obj.location = location
    return obj


def finish(obj, name, parent, paint, brass, colour, metal=False, smooth=False):
    obj.name = name
    obj.parent = parent
    obj.data.materials.clear()
    obj.data.materials.append(brass if metal else paint)
    if smooth:
        for face in obj.data.polygons:
            face.use_smooth = True
    if not metal:
        uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
        for i, loop in enumerate(uv.data):
            u, v = loop.uv
            loop.uv = ((colour + .12 + .76 * u) / 8, .12 + .76 * v)
    return obj


def lathe(name, parent, profile, paint, brass, colour, sectors=16, metal=False, scallop=0):
    vertices = []
    for ring, (z, radius) in enumerate(profile):
        for j in range(sectors):
            angle = 2 * math.pi * j / sectors
            r = radius * (1 + scallop * math.cos(angle * 5))
            vertices.append((r * math.cos(angle), r * math.sin(angle), z))
    faces = []
    for ring in range(len(profile) - 1):
        for j in range(sectors):
            k = (j + 1) % sectors
            faces.append((ring * sectors + j, ring * sectors + k, (ring + 1) * sectors + k, (ring + 1) * sectors + j))
    faces.extend([tuple(reversed(range(sectors))), tuple((len(profile) - 1) * sectors + j for j in range(sectors))])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="UVMap")
    for face in mesh.polygons:
        for loop_index in face.loop_indices:
            v = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = ((math.atan2(v.y, v.x) / (2 * math.pi)) % 1, (v.z - profile[0][0]) / max(.001, profile[-1][0] - profile[0][0]))
    return finish(obj, name, parent, paint, brass, colour, metal, True)


def sphere(name, parent, at, size, paint, brass, colour, metal=False, ico=False):
    if ico:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=at)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=1, location=at)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, parent, paint, brass, colour, metal, True)


def box(name, parent, at, size, paint, brass, colour, bevel=.012, metal=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Soft crafted edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, name, parent, paint, brass, colour, metal)


def torus(name, parent, at, radius, thickness, paint, brass, colour, metal=False, segments=16, cross=6, scale=(1, 1, 1), rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=cross,
        location=at, major_radius=radius, minor_radius=thickness, rotation=rotation)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, parent, paint, brass, colour, metal, True)


def star(parent, paint, brass):
    outline = []
    for i in range(10):
        angle = math.pi / 2 + math.pi * i / 5
        r = .112 if i % 2 == 0 else .056
        outline.append((r * math.cos(angle), 0, 1.473 + r * math.sin(angle)))
    verts = outline + [(0, -.035, 1.473), (0, .024, 1.473)]
    faces = [(10, i, (i + 1) % 10) for i in range(10)] + [(11, (i + 1) % 10, i) for i in range(10)]
    mesh = bpy.data.meshes.new("Brass star")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Brass star", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("Rounded star tips", "BEVEL")
    modifier.width = .004
    modifier.segments = 2
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, "Brass star", parent, paint, brass, GOLD, True)


def garland(parent, paint, brass):
    segments, cross = 28, 4
    verts, faces = [], []
    for z, radius, height in ((.255, .445, .68), (.635, .328, .58), (.998, .226, .415)):
        offset = len(verts)
        for i in range(segments):
            angle = i / segments * math.pi * 2
            above = .10 + .026 * (1 + math.cos(angle * 3))
            r = radius * (1 - .4 * (above - .07) / (height * .48 - .07))
            r *= 1 + .035 * math.cos(angle * 5)
            radial = Vector((math.cos(angle), math.sin(angle), 0))
            centre = radial * (r + .007) + Vector((0, 0, z + above))
            for j in range(cross):
                a = j * math.pi * 2 / cross
                verts.append(centre + radial * (.006 * math.cos(a)) + Vector((0, 0, .006 * math.sin(a))))
        for i in range(segments):
            for j in range(cross):
                faces.append(tuple(offset + index for index in (i * cross + j, i * cross + (j + 1) % cross,
                    ((i + 1) % segments) * cross + (j + 1) % cross, ((i + 1) % segments) * cross + j)))
    mesh = bpy.data.meshes.new("Brass bead garland")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Brass bead garland", mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, "Brass bead garland", parent, paint, brass, GOLD, True, True)


def tree(paint, brass):
    root = empty("WinterTree")
    lathe("Cranberry planter", root, [(0, .122), (.025, .136), (.185, .17), (.21, .16)], paint, brass, CRANBERRY)
    torus("Oat planter lip", root, (0, 0, .203), .155, .014, paint, brass, OAT)
    lathe("Walnut trunk", root, [(.17, .052), (.43, .052)], paint, brass, WALNUT, sectors=10)
    for label, z, radius, height, colour in [("Lower", .255, .445, .68, FIR), ("Middle", .635, .328, .58, FIR), ("Crown", .998, .226, .415, FIR)]:
        profile = [(z, radius * .77), (z + .025, radius * .96), (z + .07, radius),
            (z + height * .48, radius * .60), (z + height * .84, radius * .19), (z + height, .012)]
        lathe(label + " soft foliage", root, profile, paint, brass, colour, sectors=20, scallop=.035)
    star(root, paint, brass)
    garland(root, paint, brass)
    rings = [(.40, .425, 6), (.79, .296, 4), (1.13, .163, 3)]
    for ring, (height, radius, count) in enumerate(rings):
        for i in range(count):
            angle = math.pi * 2 * (i / count) - math.pi / 2 + ring * .24
            centre = (math.cos(angle) * radius, math.sin(angle) * radius, height)
            sphere("Ornament %d %d" % (ring, i), root, centre, (.034, .034, .041), paint, brass,
                CRANBERRY if i % 2 == 0 else OAT, metal=i % 3 == 2)
    empty("WinterTree_Front", root, (0, -.6, .8))
    return root


def bow(parent, centre, size, paint, brass, colour, metal=False):
    x, y, z = centre
    for side in (-1, 1):
        loop = torus("Puffed ribbon loop", parent, (x + size * .47 * side, y, z), size * .42, size * .10,
            paint, brass, colour, metal, segments=8, cross=4, scale=(1, .55, .63), rotation=(math.pi / 2, 0, side * -.24))
    sphere("Ribbon knot", parent, centre, (size * .23, size * .18, size * .22), paint, brass, colour, metal, ico=True)


def gifts(paint, brass):
    root = empty("WrappedGifts")
    for label, at, size, colour, ribbon, yaw in [
        ("GiftTall", (-.25, .10, 0), (.31, .28, .39), FIR, OAT, -.12),
        ("GiftWide", (.19, .13, 0), (.39, .30, .24), CRANBERRY, OAT, .13),
        ("GiftSmall", (-.015, -.20, 0), (.255, .235, .20), OAT, CRANBERRY, -.04),
    ]:
        parcel = empty(label, root)
        w, d, h = size
        box("Soft wrapped box", parcel, (0, 0, h / 2), size, paint, brass, colour, .018)
        box("Lift-off lid", parcel, (0, 0, h - .002), (w + .016, d + .016, .055), paint, brass, colour, .012)
        box("Cross ribbon X", parcel, (0, 0, h / 2 + .014), (.047, d + .020, h + .041), paint, brass, ribbon, .004)
        box("Cross ribbon Y", parcel, (0, 0, h / 2 + .014), (w + .02, .047, h + .041), paint, brass, ribbon, .004)
        bow(parcel, (0, -.008, h + .043), .09, paint, brass, ribbon, metal=label == "GiftTall")
        parcel.location = at
        parcel.rotation_euler.z = yaw
    empty("WrappedGifts_Front", root, (0, -.5, .2))
    return root


def wreath(paint, brass):
    root = empty("DoorWreath")
    torus("Fir wreath core", root, (0, 0, .325), .245, .047, paint, brass, DEEP_FIR,
        segments=20, cross=6, rotation=(math.pi / 2, 0, 0))
    for i in range(12):
        angle = i * math.pi / 6
        at = (math.cos(angle) * .243, -.013, .325 + math.sin(angle) * .243)
        leaf = sphere("Rounded fir sprig %02d" % i, root, at, (.088, .047, .043), paint, brass, FIR)
        leaf.rotation_euler.y = -angle - .37
    bow(root, (0, -.08, .552), .092, paint, brass, CRANBERRY)
    for side in (-1, 1):
        tail = box("Ribbon tail", root, (side * .034, -.084, .489), (.045, .014, .091), paint, brass, CRANBERRY, .006)
        tail.rotation_euler.y = side * -.22
        bell = lathe("Brass bell", root, [(0, .026), (.007, .032), (.034, .02), (.045, .009)], paint, brass, GOLD, sectors=8, metal=True)
        bell.location = (side * .037, -.082, .445)
    for i, angle in enumerate((-.35, .13, 2.5, 2.9)):
        sphere("Cranberry berry %d" % i, root, (math.cos(angle) * .25, -.064, .325 + math.sin(angle) * .25),
            (.019, .019, .019), paint, brass, CRANBERRY, ico=True)
    empty("DoorWreath_Front", root, (0, -.25, .325))
    empty("WallAnchor", root, (0, .05, .325))
    return root


def descendants(root):
    return [root] + list(root.children_recursive)


def bounds(root):
    bpy.context.view_layer.update()
    points = [root.matrix_world.inverted() @ obj.matrix_world @ vertex.co
        for obj in descendants(root) if obj.type == "MESH" for vertex in obj.data.vertices]
    return Vector([min(p[i] for p in points) for i in range(3)]), Vector([max(p[i] for p in points) for i in range(3)])


def normalize(root, height=None):
    low, high = bounds(root)
    factor = height / (high.z - low.z) if height else 1
    for child in root.children:
        child.location.z -= low.z
        child.location *= factor
        child.scale *= factor
    return bounds(root)


def describe(root, budget):
    meshes = [obj for obj in descendants(root) if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    low, high = bounds(root)
    mats = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    if triangles > budget:
        raise RuntimeError(f"{root.name}: {triangles} triangles exceeds {budget}")
    if len(mats) > 2:
        raise RuntimeError(f"{root.name}: exceeds two materials")
    if abs(low.z) > .0001:
        raise RuntimeError(f"{root.name}: pivot is not grounded")
    return {"name": root.name, "triangles": triangles, "budget": budget, "mesh_count": len(meshes),
        "materials": mats, "bounds_blender_min": list(low), "bounds_blender_max": list(high),
        "size_unity_width_height_depth": [high.x - low.x, high.z - low.z, high.y - low.y],
        "pivot": "root at ground; wreath ground corresponds to bottom edge", "front_marker": root.name + "_Front",
        "parts": [obj.name for obj in root.children if obj.type == "EMPTY"],
        "has_uv": all(bool(obj.data.uv_layers) for obj in meshes if obj.data.materials[0].name == "WinterPaint")}


def export(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
        use_custom_props=True, add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE", embed_textures=False)


def studio():
    collection = bpy.data.collections.new("_Preview Studio — excluded from FBX")
    bpy.context.scene.collection.children.link(collection)
    floor = material("Preview floor only", rgb("E6D3B4"), .86)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.012))
    plane = bpy.context.object
    plane.name = "Preview floor only"
    plane.data.materials.append(floor)
    link(plane, collection)
    world = bpy.data.worlds.new("Winter warm studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (.76, .82, .91, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = .45
    bpy.context.scene.world = world
    for name, at, power, size, colour in [
        ("Key", (-3, -4, 5), 420, 4, (1, .86, .72)),
        ("Fill", (3, -1, 3), 190, 3, (.77, .88, 1)),
        ("Rim", (1, 3, 4), 300, 3, (1, .91, .78)),
    ]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, colour
        obj = bpy.data.objects.new(name, data)
        collection.objects.link(obj)
        obj.location = at
        obj.rotation_euler = (Vector((0, 0, .65)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.cameras.new("Winter preview camera")
    obj = bpy.data.objects.new("Winter preview camera", camera)
    collection.objects.link(obj)
    bpy.context.scene.camera = obj
    camera.type = "ORTHO"
    return obj


def render(path, camera, target, offset, scale, width=1000, height=1000):
    scene = bpy.context.scene
    camera.location = Vector(target) + Vector(offset)
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_exports(output, entries):
    checks = []
    for entry in entries:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(output / entry["file"]))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
        low = [min(point[i] for point in points) for i in range(3)]
        high = [max(point[i] for point in points) for i in range(3)]
        triangles = 0
        for obj in meshes:
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)
        error = max(abs(a - b) for got, expected in ((low, entry["bounds_blender_min"]), (high, entry["bounds_blender_max"]))
            for a, b in zip(got, expected))
        cameras_or_lights = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT"}]
        marker = bpy.data.objects.get(entry["front_marker"])
        image_nodes = [node for mat in bpy.data.materials if mat.use_nodes for node in mat.node_tree.nodes if node.type == "TEX_IMAGE"]
        palette_loaded = any(node.image and list(node.image.size) == [512, 64] for node in image_nodes)
        passed = triangles == entry["triangles"] and error < .001 and not cameras_or_lights and bool(marker) and palette_loaded
        check = {"name": entry["name"], "passed": passed, "triangles": triangles, "max_bounds_error_m": error,
            "palette_loaded": palette_loaded, "front_marker_present": bool(marker), "cameras_or_lights": cameras_or_lights}
        checks.append(check)
        print("WINTER_FBX_CHECK " + json.dumps(check), flush=True)
    (output / "fbx-verification.json").write_text(json.dumps({"blender": bpy.app.version_string,
        "passed": all(check["passed"] for check in checks), "checks": checks}, indent=2) + "\n", encoding="utf-8")
    if not all(check["passed"] for check in checks):
        raise RuntimeError("Winter FBX round-trip verification failed")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    parser.add_argument("--no-render", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    output = Path(args.output).resolve()
    if REPO / "Assets" in output.parents or output == REPO / "Assets":
        raise RuntimeError("Stage this batch outside Unity Assets")
    if args.verify_only:
        verify_exports(output, json.loads((output / "manifest.json").read_text(encoding="utf-8"))["props"])
        return
    (output / "exports").mkdir(parents=True, exist_ok=True)
    (output / "renders").mkdir(exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    paint = palette_material(output)
    brass = material("WinterBrass", rgb("D6AE62"), .31, .72)
    roots = [tree(paint, brass), gifts(paint, brass), wreath(paint, brass)]
    normalize(roots[0], 1.6)
    normalize(roots[1])
    normalize(roots[2], .65)
    reports = []
    for root, budget in zip(roots, (3000, 1500, 1500)):
        collection = bpy.data.collections.new(root.name + " — editable parts")
        scene.collection.children.link(collection)
        for obj in descendants(root):
            link(obj, collection)
        entry = describe(root, budget)
        destination = output / "exports" / (root.name + ".fbx")
        export(root, destination)
        entry["file"] = str(destination.relative_to(output)).replace("\\", "/")
        entry["sha256"] = digest(destination)
        reports.append(entry)
        print("WINTER_PROP " + json.dumps(entry), flush=True)
    camera = studio()
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 6
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "AgX"
    if not args.no_render:
        for root, target, offset, scale in zip(roots, [(0, 0, .8), (0, 0, .24), (0, 0, .325)],
                [(2.7, -5, 2.5), (2.6, -4, 3), (1.2, -5, 1)], [1.94, 1.08, .82]):
            for other in roots:
                for obj in descendants(other):
                    obj.hide_render = other != root
            render(output / "renders" / (root.name + ".png"), camera, target, offset, scale)
    for root in roots:
        for obj in descendants(root):
            obj.hide_render = False
    roots[0].location = (-.58, .18, 0)
    roots[1].location = (.34, -.21, 0)
    roots[2].location = (.60, .28, .46)
    if not args.no_render:
        render(output / "renders/Collection.png", camera, (0, 0, .75), (2.4, -6, 2.3), 2.5, 1400, 1000)
    bpy.ops.object.select_all(action="DESELECT")
    for root in roots:
        root.select_set(True)
    bpy.context.view_layer.objects.active = roots[0]
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "source.blend"))
    manifest = {"collection": "Winter House Party", "generator": "Tools/AssetPipeline/build_winter_props.py",
        "blender": bpy.app.version_string, "provenance": "Original deterministic Blender mesh geometry authored for this project; no provider, image-to-3D conversion or stock source. The separate GPT lobby illustration is not the source of these meshes.",
        "source": "source.blend", "source_sha256": digest(output / "source.blend"),
        "palette": {"file": "exports/WinterPalette.png", "size": [512, 64], "srgb_hex": PALETTE, "sha256": digest(output / "exports/WinterPalette.png")},
        "materials": [{"name": "WinterPaint", "base_map": "WinterPalette.png", "metallic": 0, "roughness": .64},
            {"name": "WinterBrass", "srgb_hex": "D6AE62", "metallic": .72, "roughness": .31}],
        "export": {"axis_forward": "-Z", "axis_up": "Y", "metres": True, "animation": False, "embedded_lights_cameras": False},
        "placement": "All FBX roots start at origin, bottom at 0. Preserve imported FBX axis transforms under a separate placement anchor. Front empty points toward Blender -Y. Wreath WallAnchor identifies the rear wall-contact point; use imported marker direction rather than guessing a face rotation.",
        "native_unity_validation": "NOT RUN; staged outside Assets while root builds current checkpoint", "props": reports}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    verify_exports(output, reports)
    print("WINTER_PROPS_READY " + str(output), flush=True)


if __name__ == "__main__":
    main()
