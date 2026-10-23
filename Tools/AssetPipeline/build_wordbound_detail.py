import argparse
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


WIDTH, HEIGHT, CELL = 512, 256, 64
ROWS = ("oiled wood", "woven textile", "glazed ceramic", "painted surface")


def load_builder(path):
    spec = importlib.util.spec_from_file_location("wordbound_original_builder", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def surface(u, v, row):
    if row == 0:
        grain = math.sin(u * 72 + 2.6 * math.sin(v * 9) + .6 * math.sin(v * 23))
        pores = math.sin(u * 210 + v * 8) * math.sin(v * 60)
        return 1 + .065 * grain + .018 * pores, .45 + .05 * grain, .0013 * grain
    if row == 1:
        warp = math.sin(u * math.tau * 15)
        weft = math.sin(v * math.tau * 15)
        weave = warp * weft
        return 1 + .035 * weave, .83 + .025 * weave, .00075 * weave
    if row == 2:
        glaze = math.sin(u * 17 + v * 11) * math.sin(v * 23 - u * 7)
        return 1 + .009 * glaze, .21 + .012 * glaze, .00008 * glaze
    enamel = math.sin(u * 63 + v * 37) * math.sin(v * 57 - u * 19)
    return 1 + .012 * enamel, .38 + .018 * enamel, .00015 * enamel


def save_image(output, name, pixels, data=False):
    image = bpy.data.images.new(name, width=WIDTH, height=HEIGHT, alpha=True)
    image.colorspace_settings.name = "Non-Color" if data else "sRGB"
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(output / "Exports" / (name + ".png"))
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def materials(base, output):
    colors, masks, normals, roughness = [], [], [], []
    for y in range(HEIGHT):
        row, py = divmod(y, CELL)
        v = (py + .5) / CELL
        for x in range(WIDTH):
            col, px = divmod(x, CELL)
            u = (px + .5) / CELL
            variation, rough, height = surface(u, v, row)
            color = base.colour(base.PALETTE[col])
            colors.extend([max(0, min(1, channel * variation)) for channel in color] + [1])
            masks.extend([0, 0, 0, 1 - rough])
            roughness.extend([rough, rough, rough, 1])
            du = (surface(u + 1 / CELL, v, row)[2] - height) * CELL
            dv = (surface(u, v + 1 / CELL, row)[2] - height) * CELL
            normal = Vector((-du, -dv, 1)).normalized()
            normals.extend([component * .5 + .5 for component in normal] + [1])
    atlas = save_image(output, "WordboundPvPPalette", colors)
    mask = save_image(output, "WordboundPvPMask", masks, True)
    normal = save_image(output, "WordboundPvPNormal", normals, True)
    rough = save_image(output, "WordboundPvPRoughness", roughness, True)
    paint = base.material("WordboundPvPPaint", "FFFFFF", .5)
    tree = paint.node_tree
    shader = tree.nodes["Principled BSDF"]
    for image, target in ((atlas, "Base Color"), (rough, "Roughness")):
        node = tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        tree.links.new(node.outputs["Color"], shader.inputs[target])
    texture = tree.nodes.new("ShaderNodeTexImage")
    texture.image = normal
    bump = tree.nodes.new("ShaderNodeNormalMap")
    tree.links.new(texture.outputs["Color"], bump.inputs["Color"])
    tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    base.PAINT = paint
    base.BRASS = base.material("WordboundPvPBrass", "D6AE62", .30, .72)
    return atlas, mask, normal, rough


def material_row(item, name):
    text = name.lower()
    if "oak" in text or "lower_shelf" in text or "rear_handle" in text or "join" in text:
        return 0
    if item == "PLATE" or "ceramic" in text or item == "CLOCK" and "dial" in text:
        return 2
    if any(word in text for word in ("grip", "wrap", "binding", "padded", "bristle", "seam", "stitch", "piping", "tuft")):
        return 1
    if item == "SOFA" and any(word in text for word in ("seat", "back", "arm")):
        return 1
    if item == "BED" and any(word in text for word in ("quilt", "pillow", "mattress", "band")):
        return 1
    return 3


def remap_uv(base, root, item):
    for obj in base.descendants(root):
        if obj.type != "MESH" or obj.data.materials[0] == base.BRASS:
            continue
        uv = obj.data.uv_layers.active
        previous = [loop.uv.copy() for loop in uv.data]
        column = min(7, max(0, int(sum(value.x for value in previous) / len(previous) * 8)))
        row = material_row(item, obj.name)
        lows = [min(v.co[axis] for v in obj.data.vertices) for axis in range(3)]
        spans = [max(v.co[axis] for v in obj.data.vertices) - lows[axis] for axis in range(3)]
        spread = max(value.x for value in previous) - min(value.x for value in previous)
        for polygon in obj.data.polygons:
            normal_axis = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
            axes = sorted((axis for axis in range(3) if axis != normal_axis), key=lambda axis: spans[axis])
            for index in polygon.loop_indices:
                if spread > .001 and "lathe" not in obj.name.lower():
                    u = (previous[index].x * 8 - column - .12) / .76
                    v = (previous[index].y - .12) / .76
                else:
                    co = obj.data.vertices[obj.data.loops[index].vertex_index].co
                    u = (co[axes[0]] - lows[axes[0]]) / max(.00001, spans[axes[0]])
                    v = (co[axes[1]] - lows[axes[1]]) / max(.00001, spans[axes[1]])
                uv.data[index].uv = ((column + .08 + .84 * max(0, min(1, u))) / 8,
                                    (row + .08 + .84 * max(0, min(1, v))) / 4)
        obj["surface_family"] = ROWS[row]


def details(base, item):
    if item == "BROOM":
        for i in range(10):
            for side in (-1, 1):
                x = (i - 4.5) * .028 + side * .006
                base.rod("Bristle_fibre", (x, .041, .012), (x, .041, .112), .0012, base.LINEN, sectors=6)
    elif item == "HAMMER":
        for x in (-.095, .095):
            base.torus("Padded_head_seam", (x, 0, .5475), .087, .0014, base.LINEN,
                       rotation=(0, math.pi / 2, 0), segments=24, sides=5)
    elif item == "SPEAR":
        for x in (-.028, .028):
            base.sphere("Socket_rivet", (x, .035, 1.035), (.004, .003, .004), metal=True, segments=8, rings=4)
    elif item == "SHIELD":
        for z in (.215, .365):
            base.box("Rear_leather_binding", (0, -.112, z), (.047, .043, .022), base.WALNUT, .004)
    elif item == "PLATE":
        base.torus("Ceramic_foot_ring", (0, 0, .007), .083, .003, base.CREAM, segments=40, sides=5)
    elif item == "TABLE":
        for x in (-.183, .183):
            base.rod("Oak_plank_join", (x, -.279, .734), (x, .279, .734), .0012, base.WALNUT, sectors=6)
        for y in (-.278, .278):
            base.box("Oak_apron", (0, y, .567), (.98, .032, .075), base.OAK, .007)
    elif item == "SOFA":
        for x in (-.43, 0, .43):
            for z in (.60, .76):
                base.sphere("Tuft_button", (x, -.129, z), (.008, .004, .008), base.TERRA, segments=10, rings=5)
            base.rod("Seat_front_stitch", (x - .15, .343, .39), (x + .15, .343, .39), .0014, base.LINEN, sectors=6)
    elif item == "BED":
        for y in (-.20, .10, .38):
            base.box("Quilt_cross_seam", (0, y, .571), (.81, .004, .003), base.LINEN, .001)
        base.rod("Pillow_edge_seam", (-.30, -.335, .55), (.30, -.335, .55), .0015, base.LINEN, sectors=6)
    elif item == "STOOL":
        base.torus("Seat_piping", (0, 0, .427), .252, .0025, base.LINEN, segments=32, sides=6)
    elif item == "FAN":
        for radius in (.12, .20):
            base.torus("Inner_safety_grille", (0, .076, .56), radius, .003, metal=True,
                       rotation=(math.pi / 2, 0, 0), segments=32, sides=6)
        for x in (-.045, -.015, .015, .045):
            base.box("Motor_vent", (x, -.205, .56), (.009, .002, .075), base.DARK, .001)
    elif item == "CLOCK":
        for i in range(60):
            if i % 5 == 0:
                continue
            angle = i * math.tau / 60
            base.box("Minute_mark", (math.sin(angle) * .198, .071, .249 + math.cos(angle) * .198),
                     (.003, .003, .009), base.DARK, 0, rotation=(0, angle, 0))


def record(base, output, path):
    return {"path": str(path.relative_to(output)).replace("\\", "/"), "sha256": base.sha256(path)}


def mesh_measurements(base, root):
    meshes = [obj for obj in base.descendants(root) if obj.type == "MESH"]
    points = [root.matrix_world.inverted() @ obj.matrix_world @ vertex.co
              for obj in meshes for vertex in obj.data.vertices]
    return meshes, points


def verify_vertex_positions(before, after, item):
    if len(before) != len(after):
        raise RuntimeError(item + ": grouping changed vertex count")
    tree = KDTree(len(before))
    for index, point in enumerate(before):
        tree.insert(point, index)
    tree.balance()
    matched = set()
    maximum_error = 0
    for point in after:
        candidates = sorted(tree.find_range(point, .00001), key=lambda candidate: candidate[2])
        match = next((candidate for candidate in candidates if candidate[1] not in matched), None)
        if match is None:
            raise RuntimeError(item + ": grouping changed a vertex position beyond 0.01 mm")
        matched.add(match[1])
        maximum_error = max(maximum_error, match[2])
    return maximum_error


def grouped_export(base, root, output, spec):
    objects = base.descendants(root)
    names = {obj: obj.name for obj in objects}
    source_meshes, source_points = mesh_measurements(base, root)
    copies = {}
    temporary_meshes = []
    temporary = bpy.data.collections.new("Wordbound temporary delivery")
    bpy.context.scene.collection.children.link(temporary)
    try:
        for obj in objects:
            obj.name = names[obj] + "__EditableSource"
        for obj in objects:
            copy = obj.copy()
            if obj.type == "MESH":
                copy.data = obj.data.copy()
                temporary_meshes.append(copy.data)
            copy.name = names[obj]
            temporary.objects.link(copy)
            copies[obj] = copy
        for obj, copy in copies.items():
            copy.parent = copies.get(obj.parent)
        delivery = copies[root]
        bpy.context.view_layer.update()
        groups = {}
        for obj in copies.values():
            if obj.type == "MESH":
                if len(obj.data.materials) != 1:
                    raise RuntimeError("Each editable component must have one material before grouping")
                groups.setdefault((obj.parent, obj.data.materials[0]), []).append(obj)
        for (parent, material), members in groups.items():
            bpy.ops.object.select_all(action="DESELECT")
            for obj in members:
                obj.select_set(True)
            bpy.context.view_layer.objects.active = members[0]
            if len(members) > 1:
                bpy.ops.object.join()
            members[0].name = parent.name + "_" + material.name + "Mesh"
        bpy.context.view_layer.update()
        delivery_meshes, delivery_points = mesh_measurements(base, delivery)
        vertex_error = verify_vertex_positions(source_points, delivery_points, spec["id"])
        measured = base.inspect_model(delivery, spec)
        measured["max_grouping_vertex_error_metres"] = vertex_error
        measured["editable_mesh_count"] = len(source_meshes)
        measured["export_mesh_count"] = len(delivery_meshes)
        measured["material_draw_count"] = sum(len(obj.data.materials) for obj in delivery_meshes)
        measured["export_pivots"] = [{"name": obj.name, "matrix_world": [value for row in obj.matrix_world for value in row]}
                                     for obj in base.descendants(delivery) if obj.type == "EMPTY"]
        measured["exports"] = base.export(delivery, output, spec)
        return measured
    finally:
        for obj in list(temporary.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(temporary)
        for mesh in temporary_meshes:
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        for obj, name in names.items():
            obj.name = name


def build(base, source, output):
    if output.exists():
        raise RuntimeError("Choose a new revision directory; build never overwrites an existing revision")
    output.mkdir(parents=True)
    for name in ("Source", "Exports", "Previews", "Icons"):
        (output / name).mkdir()
    for name in ("prompts.json", "provenance.json"):
        shutil.copy2(source / name, output / name)
    shutil.copytree(source / "Concepts", output / "Concepts")
    prompts = json.loads((source / "prompts.json").read_text(encoding="utf-8"))["entries"]
    old = json.loads((source / "manifest.json").read_text(encoding="utf-8"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials(base, output)
    entries = []
    for spec in prompts:
        root = base.empty(spec["id"])
        base.ACTIVE = root
        base.BUILDERS[spec["id"]]()
        details(base, spec["id"])
        remap_uv(base, root, spec["id"])
        base.normalize(root, spec)
        root["art_revision"] = "DetailV2"
        entry = grouped_export(base, root, output, spec)
        entry["concept_sha256"] = base.sha256(output / spec["destination"])
        entry["previews"] = []
        entries.append(entry)
        collection = bpy.data.collections.new(spec["id"] + " — editable components")
        bpy.context.scene.collection.children.link(collection)
        for obj in base.descendants(root):
            for previous in list(obj.users_collection):
                previous.objects.unlink(obj)
            collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for entry in entries:
        root = bpy.data.objects[entry["id"]]
        for obj in base.descendants(root):
            obj.hide_set(entry["id"] != "BAT")
    bpy.data.objects["BAT"].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["BAT"]
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "Source/WordboundPvP.blend"), compress=True)
    manifest = {
        "collection": "WordboundPvP", "revision": "DetailV2", "status": "exports-produced-awaiting-validation",
        "blender": bpy.app.version_string, "generator": str(Path(__file__).name),
        "generator_sha256": base.sha256(__file__), "base_generator_sha256": base.sha256(base.__file__),
        "prior_source_sha256": old["source"]["sha256"],
        "provenance": "Original editable geometry revision and procedural material sources; unchanged original concept bytes. No external provider.",
        "delivery_optimization": "Merge meshes only within the same parent pivot and material; editable source components remain separate.",
        "source": record(base, output, output / "Source/WordboundPvP.blend"),
        "palette": record(base, output, output / "Exports/WordboundPvPPalette.png"),
        "detail_maps": {"normal": record(base, output, output / "Exports/WordboundPvPNormal.png"),
                        "mask": record(base, output, output / "Exports/WordboundPvPMask.png"),
                        "roughness": record(base, output, output / "Exports/WordboundPvPRoughness.png")},
        "material_contract": {"size": [WIDTH, HEIGHT], "rows_bottom_to_top": ROWS,
                              "base_color": "sRGB", "normal": "linear tangent-space +Y",
                              "mask": "linear; R metallic (0), A smoothness (1 - roughness)",
                              "roughness": "linear grayscale for Blender/glTF"},
        "native_unity_validation": "NOT RUN", "human_acceptance": "NOT RUN", "assets": entries}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def verify(base, output):
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    checks = []
    for entry in manifest["assets"]:
        for format_name in ("fbx", "glb"):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            file = output / entry["exports"][format_name]["path"]
            errors = []
            if base.sha256(file) != entry["exports"][format_name]["sha256"]:
                errors.append("file hash differs")
            if format_name == "fbx":
                bpy.ops.import_scene.fbx(filepath=str(file))
            else:
                bpy.ops.import_scene.gltf(filepath=str(file))
            bpy.context.view_layer.update()
            objects = list(bpy.context.scene.objects)
            meshes = [obj for obj in objects if obj.type == "MESH"]
            triangles = 0
            for obj in meshes:
                obj.data.calc_loop_triangles()
                triangles += len(obj.data.loop_triangles)
                if any(triangle.area < 1e-12 for triangle in obj.data.loop_triangles):
                    errors.append("degenerate triangle")
            points = [obj.matrix_world @ v.co for obj in meshes for v in obj.data.vertices]
            low = [min(point[i] for point in points) for i in range(3)]
            high = [max(point[i] for point in points) for i in range(3)]
            bounds_error = max(abs(got - want) for actual, expected in
                               ((low, entry["bounds_blender_min"]), (high, entry["bounds_blender_max"]))
                               for got, want in zip(actual, expected))
            if bounds_error > .001 or triangles != entry["triangles"]:
                errors.append("bounds or triangle count differs")
            if len(meshes) != entry["export_mesh_count"]:
                errors.append("optimized mesh count differs")
            draw_count = sum(len(obj.data.materials) for obj in meshes)
            if draw_count != entry["material_draw_count"]:
                errors.append("material draw count differs")
            pivot_error = 0
            for pivot in entry["export_pivots"]:
                name = "Grip_R" if pivot["name"] == entry["id"] + "_Grip_R" else pivot["name"]
                obj = next((obj for obj in objects if obj.name == name), None)
                if not obj or obj.type != "EMPTY":
                    errors.append("missing authored pivot: " + name)
                    continue
                actual = [value for row in obj.matrix_world for value in row]
                pivot_error = max(pivot_error, max(abs(a - b) for a, b in zip(actual, pivot["matrix_world"])))
            if pivot_error > .001:
                errors.append("authored pivot transform drift")
            marker = next((obj for obj in objects if obj.name == "Grip_R"), None)
            expected = entry["grip_unity_metres"]
            if not marker or (marker.matrix_world.translation - Vector((expected[0], expected[2], expected[1]))).length > .001:
                errors.append("Grip_R missing or moved")
            materials = {slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}
            if len(materials) != 2 or any(not obj.data.uv_layers for obj in meshes):
                errors.append("material or UV contract differs")
            images = [node.image for material in bpy.data.materials if material.use_nodes
                      for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image]
            if not images or any(max(image.size) > 512 or min(image.size) < 1 for image in images):
                errors.append("material texture missing or oversized")
            if any(obj.type in {"CAMERA", "LIGHT"} for obj in objects):
                errors.append("embedded camera or light")
            checks.append({"id": entry["id"], "format": format_name, "passed": not errors,
                           "triangles": triangles, "meshes": len(meshes), "material_draw_count": draw_count,
                           "max_bounds_error_metres": bounds_error, "max_pivot_matrix_error": pivot_error,
                           "materials": sorted(materials), "errors": errors})
            print("WORDBOUND_CHECK " + json.dumps(checks[-1]), flush=True)
    report = {"blender": bpy.app.version_string, "passed": all(check["passed"] for check in checks),
              "formats": ["fbx", "glb"], "native_unity_validation": "NOT RUN", "checks": checks}
    (output / "export-verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not report["passed"]:
        raise RuntimeError("Wordbound detail export validation failed")


def render(base, output, selected):
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    source = output / manifest["source"]["path"]
    if base.sha256(source) != manifest["source"]["sha256"]:
        raise RuntimeError("Editable source differs from the production manifest")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    roots = [bpy.data.objects[entry["id"]] for entry in manifest["assets"]]
    if selected and set(selected) - {root.name for root in roots}:
        raise RuntimeError("Unknown requested asset")
    camera = base.studio()
    bpy.context.scene.render.threads = 2
    for root in roots:
        for obj in base.descendants(root):
            obj.hide_set(False)
            obj.hide_render = True
    for root in roots:
        if selected and root.name not in selected:
            continue
        for obj in base.descendants(root):
            obj.hide_render = False
        base.render(root, camera, output, root.name)
        for obj in base.descendants(root):
            obj.hide_render = True
    for entry in manifest["assets"]:
        entry["previews"] = base.preview_records(output, entry["id"])
    if all(len(entry["previews"]) == 4 for entry in manifest["assets"]):
        manifest["status"] = "source-exports-and-previews-produced"
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("build", "verify", "render"))
    parser.add_argument("--base-builder", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--items", nargs="*")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    base = load_builder(args.base_builder.resolve())
    source, output = args.source.resolve(), args.output.resolve()
    if source == output or source in output.parents and output.name != "DetailV2":
        raise RuntimeError("The detail revision must be a separate directory")
    if args.command == "build":
        build(base, source, output)
    elif args.command == "verify":
        verify(base, output)
    else:
        render(base, output, args.items)


if __name__ == "__main__":
    main()
