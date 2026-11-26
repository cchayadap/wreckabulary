import json
import math
import re
import tempfile
from array import array
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

REPO = Path(__file__).resolve().parents[2]
PROJECT = REPO / "Assets/_Project"
LIBRARY = PROJECT / "Materials/Library"
MATERIALS_JSON = PROJECT / "Data/Generated/materials.json"
AXIS_TURN = {"Z": (0.0, 0.0, 0.0), "Y": (-math.pi / 2, 0.0, 0.0), "X": (0.0, math.pi / 2, 0.0)}
PREVIEW_SIZE = 384
PARTS = []
MATS = {}

def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4



def library_spec(name):
    path = LIBRARY / (name + ".mat")
    if path.exists():
        text = path.read_text(encoding="utf-8")
        colour = re.search(r"- _BaseColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}", text)
        metallic = re.search(r"- _Metallic: (\S+)", text)
        smoothness = re.search(r"- _Smoothness: (\S+)", text)
        rgb = [srgb_to_linear(float(colour.group(i))) for i in (1, 2, 3)]
        return {"linear": tuple(rgb), "alpha": float(colour.group(4)), "metallic": float(metallic.group(1)),
                "smoothness": float(smoothness.group(1)), "source": str(path.relative_to(REPO)).replace("\\", "/")}
    data = json.loads(MATERIALS_JSON.read_text(encoding="utf-8"))
    for spec in data["materials"]:
        if spec["name"] == name:
            c = spec["baseColor"]
            return {"linear": tuple(c[:3]), "alpha": c[3], "metallic": spec["metallic"], "smoothness": 1.0 - spec["roughness"],
                    "source": "materials.json"}
    raise RuntimeError(f"{name} is not a Classic library material (no {path.name}, no materials.json entry)")


def box_of(points):
    lo = Vector((min(p[0] for p in points), min(p[1] for p in points), min(p[2] for p in points)))
    hi = Vector((max(p[0] for p in points), max(p[1] for p in points), max(p[2] for p in points)))
    return lo, hi


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PARTS.clear()
    MATS.clear()


def material(name):
    if name in MATS:
        return MATS[name]
    spec = library_spec(name)
    mat = bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except (AttributeError, TypeError):
        pass
    rgba = (*spec["linear"], spec["alpha"])
    mat.diffuse_color = rgba
    mat.metallic = spec["metallic"]
    mat.roughness = 1.0 - spec["smoothness"]
    tree = mat.node_tree
    shader = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if tree else None
    if shader:
        shader.inputs["Base Color"].default_value = rgba
        shader.inputs["Metallic"].default_value = spec["metallic"]
        shader.inputs["Roughness"].default_value = 1.0 - spec["smoothness"]
        shader.inputs["Alpha"].default_value = spec["alpha"]
    if spec["alpha"] < 0.999:
        for attr, value in (("blend_method", "BLEND"), ("surface_render_method", "BLENDED")):
            try:
                setattr(mat, attr, value)
            except (AttributeError, TypeError):
                pass
    MATS[name] = mat
    return mat


def bake(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
    old = obj.data
    obj.modifiers.clear()
    obj.data = mesh
    bpy.data.meshes.remove(old)


def emit(name, bm, mats, smooth=True, sharp=None, bevel=None, weighted=False):
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    bm.normal_update()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for m in mats:
        mesh.materials.append(material(m))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    if sharp is not None:
        mesh.set_sharp_from_angle(angle=math.radians(sharp))
    if bevel:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width, mod.segments = bevel
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(35)
        mod.profile = 0.5
        mod.use_clamp_overlap = True
    if weighted:
        mod = obj.modifiers.new("weighted", "WEIGHTED_NORMAL")
        mod.mode = "FACE_AREA"
        mod.keep_sharp = True
    if obj.modifiers:
        bake(obj)
    PARTS.append(obj)
    return obj


def place(bm, at=(0, 0, 0), rotation=(0, 0, 0), scale=None):
    if scale is not None:
        bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    if any(rotation):
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Euler(rotation, "XYZ").to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    return bm


def box(name, mat, size, at, bevel=0.01, segments=3, rotation=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    place(bm, at, rotation, size)
    return emit(name, bm, mat, bevel=(bevel, segments) if bevel else None, weighted=bool(bevel))


def ellipsoid(name, mat, at, radii, segments=16, rings=8, rotation=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    place(bm, at, rotation, radii)
    return emit(name, bm, mat)


def cylinder(name, mat, radius, depth, at, axis="Z", segments=24, bevel=0.004, top=None, scale=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=segments, radius1=radius,
                          radius2=radius if top is None else top, depth=depth)
    if scale is not None:
        bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    place(bm, at, AXIS_TURN[axis])
    return emit(name, bm, mat, sharp=None if bevel else 40, bevel=(bevel, 2) if bevel else None, weighted=bool(bevel))


def torus(name, mat, major, minor, at, axis="Z", major_segments=32, minor_segments=8):
    rx, ry = (major, major) if isinstance(major, (int, float)) else major
    bm = bmesh.new()
    rings = []
    for i in range(major_segments):
        a = 2 * math.pi * i / major_segments
        ring = []
        for j in range(minor_segments):
            b = 2 * math.pi * j / minor_segments
            ring.append(bm.verts.new(((rx + minor * math.cos(b)) * math.cos(a), (ry + minor * math.cos(b)) * math.sin(a), minor * math.sin(b))))
        rings.append(ring)
    for i in range(major_segments):
        for j in range(minor_segments):
            n, m = (i + 1) % major_segments, (j + 1) % minor_segments
            bm.faces.new((rings[i][j], rings[n][j], rings[n][m], rings[i][m]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    place(bm, at, AXIS_TURN[axis])
    return emit(name, bm, mat)


def lathe(name, mat, profile, at=(0, 0, 0), axis="Z", segments=32, sharp=None):
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r < 1e-7:
            rings.append([bm.verts.new((0.0, 0.0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segments), r * math.sin(2 * math.pi * i / segments), z)) for i in range(segments)])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(segments):
            k = (i + 1) % segments
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[k]))
            elif len(b) == 1:
                bm.faces.new((a[i], a[k], b[0]))
            else:
                bm.faces.new((a[i], a[k], b[k], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    place(bm, at, AXIS_TURN[axis])
    return emit(name, bm, mat, sharp=sharp)


def frames(points, closed, reference):
    count = len(points)
    tangents = []
    for i in range(count):
        a = points[(i - 1) % count] if closed or i > 0 else points[i]
        b = points[(i + 1) % count] if closed or i < count - 1 else points[i]
        tangents.append((b - a).normalized())
    normal = (reference - reference.dot(tangents[0]) * tangents[0]).normalized()
    out = []
    for t in tangents:
        normal = (normal - normal.dot(t) * t).normalized()
        out.append((t, normal, t.cross(normal)))
    return out


def tube(name, mat, points, radius, sides=10, closed=False, reference=Vector((0, 0, 1))):
    points = [Vector(p) for p in points]
    bm = bmesh.new()
    rings = []
    for p, (t, n, b) in zip(points, frames(points, closed, reference)):
        rings.append([bm.verts.new(p + radius * (math.cos(2 * math.pi * j / sides) * n + math.sin(2 * math.pi * j / sides) * b)) for j in range(sides)])
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for a, c in pairs:
        for j in range(sides):
            k = (j + 1) % sides
            bm.faces.new((a[j], a[k], c[k], c[j]))
    if not closed:
        for ring, centre in ((rings[0], points[0]), (rings[-1], points[-1])):
            hub = bm.verts.new(centre)
            for j in range(sides):
                bm.faces.new((ring[j], ring[(j + 1) % sides], hub))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat)


def spline(points, steps=4):
    pts = [Vector(p) for p in points]
    out = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for s in range(steps):
            t = s / steps
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t))
    out.append(pts[-1])
    return out


def rounded_rect(width, height, bottom, top, z0=0.0, steps=8):
    hw = width / 2
    corners = ((hw - bottom, z0 + bottom, bottom, -90), (hw - top, z0 + height - top, top, 0),
               (-hw + top, z0 + height - top, top, 90), (-hw + bottom, z0 + bottom, bottom, 180))
    out = []
    for cx, cz, r, start in corners:
        for s in range(steps + 1):
            a = math.radians(start + 90 * s / steps)
            out.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return out


def slab(name, mats, outline, y0, y1, bevel=None):
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    bm = bmesh.new()
    front = [bm.verts.new((x, y1, z)) for x, z in outline]
    back = [bm.verts.new((x, y0, z)) for x, z in outline]
    caps = [bm.faces.new(front), bm.faces.new(list(reversed(back)))]
    for i in range(len(outline)):
        k = (i + 1) % len(outline)
        bm.faces.new((back[i], back[k], front[k], front[i])).material_index = 0
    for cap in caps:
        cap.material_index = len(mats) - 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="BEAUTY")
    return emit(name, bm, mats, bevel=bevel, weighted=bool(bevel))


def ring(name, mat, outer, inner, y0, y1, bevel=None):
    bm = bmesh.new()
    of = [bm.verts.new((x, y1, z)) for x, z in outer]
    ob = [bm.verts.new((x, y0, z)) for x, z in outer]
    inf = [bm.verts.new((x, y1, z)) for x, z in inner]
    inb = [bm.verts.new((x, y0, z)) for x, z in inner]
    for i in range(len(outer)):
        k = (i + 1) % len(outer)
        bm.faces.new((of[i], of[k], inf[k], inf[i]))
        bm.faces.new((ob[k], ob[i], inb[i], inb[k]))
        bm.faces.new((ob[i], ob[k], of[k], of[i]))
        bm.faces.new((inf[i], inf[k], inb[k], inb[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat, bevel=bevel, weighted=bool(bevel))


def sheet(name, mat, outline, y):
    bm = bmesh.new()
    face = bm.faces.new([bm.verts.new((x, y, z)) for x, z in outline])
    bm.normal_update()
    if face.normal.y < 0:
        bmesh.ops.reverse_faces(bm, faces=[face])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="BEAUTY")
    return emit(name, bm, mat, smooth=False)


def streak(cx, cz, length, width, angle):
    d = (math.cos(angle), math.sin(angle))
    n = (-d[1], d[0])
    return [(cx + sx * length / 2 * d[0] + sy * width / 2 * n[0], cz + sx * length / 2 * d[1] + sy * width / 2 * n[1])
            for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]


def join(name):
    target = PARTS[0]
    for o in bpy.context.scene.objects:
        o.select_set(o in PARTS)
    bpy.context.view_layer.objects.active = target
    with bpy.context.temp_override(active_object=target, object=target, selected_objects=list(PARTS), selected_editable_objects=list(PARTS)):
        bpy.ops.object.join()
    target.name = name + "_Build"
    target.data.name = name + "_Build"
    PARTS[:] = [target]
    return target


def mesh_bounds(obj):
    return box_of([obj.matrix_world @ v.co for v in obj.data.vertices])


def box_uv(obj):
    mesh = obj.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    layer = mesh.uv_layers.new(name="UVMap")
    lo, hi = mesh_bounds(obj)
    extent = max(hi - lo)
    for poly in mesh.polygons:
        axis = max(range(3), key=lambda i: abs(poly.normal[i]))
        u, v = ((1, 2), (0, 2), (0, 1))[axis]
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            layer.data[li].uv = ((co[u] - lo[u]) / extent, (co[v] - lo[v]) / extent)


def fit(obj, size):
    lo, hi = mesh_bounds(obj)
    extent = hi - lo
    factors = [size[i] / extent[i] for i in range(3)]
    centre = (lo + hi) / 2
    obj.data.transform(Matrix.Diagonal((*factors, 1.0)) @ Matrix.Translation((-centre.x, -centre.y, -lo.z)))
    obj.data.update()
    return factors


def stats(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    area = sum(p.area for p in mesh.polygons)
    return {"triangles": len(mesh.loop_triangles), "vertices": len(mesh.vertices), "area": round(area, 4),
            "trianglesPerM2": round(len(mesh.loop_triangles) / area, 1), "materials": [m.name for m in mesh.materials]}


def move(obj, rotation=(0, 0, 0), at=(0, 0, 0)):
    obj.data.transform(Matrix.Translation(at) @ Euler(rotation, "XYZ").to_matrix().to_4x4())
    obj.data.update()
    return obj


def prism(name, mats, outline, z0, z1, bevel=None):
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    bm = bmesh.new()
    low = [bm.verts.new((x, y, z0)) for x, y in outline]
    high = [bm.verts.new((x, y, z1)) for x, y in outline]
    caps = [bm.faces.new(high), bm.faces.new(list(reversed(low)))]
    for i in range(len(outline)):
        k = (i + 1) % len(outline)
        bm.faces.new((low[i], low[k], high[k], high[i])).material_index = 0
    for cap in caps:
        cap.material_index = len(mats) - 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="BEAUTY")
    return emit(name, bm, mats, bevel=bevel, weighted=bool(bevel))


def fan_badge(name, mats, outline, centre, place):
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    bm = bmesh.new()
    layers = []
    for layer in (0, 1):
        hub = bm.verts.new(place(centre[0], centre[1], layer))
        ring = [bm.verts.new(place(u, v, layer)) for u, v in outline]
        layers.append((hub, ring))
    count = len(outline)
    for layer, (hub, ring) in enumerate(layers):
        for i in range(count):
            k = (i + 1) % count
            face = bm.faces.new((hub, ring[i], ring[k]) if layer else (hub, ring[k], ring[i]))
            face.material_index = len(mats) - 1
    low, high = layers[0][1], layers[1][1]
    for i in range(count):
        k = (i + 1) % count
        bm.faces.new((low[i], low[k], high[k], high[i])).material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mats, sharp=30)


def ellipse(rx, rz, cx=0.0, cz=0.0, steps=48):
    return [(cx + rx * math.cos(2 * math.pi * i / steps), cz + rz * math.sin(2 * math.pi * i / steps)) for i in range(steps)]


def star(points, outer, inner, turn=math.pi / 2):
    out = []
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        a = turn + math.pi * i / points
        out.append((r * math.cos(a), r * math.sin(a)))
    return out


def lobed(name, mat, at, radii, lobes=0, amount=0.0, dimple=0.0, segments=40, rings=20):
    bm = bmesh.new()
    top = bm.verts.new((at[0], at[1], at[2] + radii[2] - dimple))
    bottom = bm.verts.new((at[0], at[1], at[2] - radii[2] + dimple * 0.5))
    grid = []
    for j in range(1, rings):
        v = math.pi * j / rings
        row = []
        for i in range(segments):
            u = 2 * math.pi * i / segments
            swell = 1 + amount * math.cos(lobes * u) * math.sin(v) ** 2
            sink = dimple * (math.exp(-(v / 0.45) ** 2) - 0.5 * math.exp(-((math.pi - v) / 0.45) ** 2))
            row.append(bm.verts.new((at[0] + radii[0] * swell * math.sin(v) * math.cos(u), at[1] + radii[1] * swell * math.sin(v) * math.sin(u),
                                     at[2] + radii[2] * math.cos(v) - sink)))
        grid.append(row)
    for i in range(segments):
        k = (i + 1) % segments
        bm.faces.new((top, grid[0][i], grid[0][k]))
        bm.faces.new((bottom, grid[-1][k], grid[-1][i]))
        for j in range(len(grid) - 1):
            bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][k], grid[j][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat)


def gathered(name, mat, profile, folds, amount, segments=48, sharp=None):
    bm = bmesh.new()
    top = max(z for _, z in profile)
    rings = []
    for r, z in profile:
        if r < 1e-7:
            rings.append([bm.verts.new((0.0, 0.0, z))])
            continue
        weight = amount * (z / top) ** 2
        rings.append([bm.verts.new((r * (1 + weight * math.cos(folds * 2 * math.pi * i / segments)) * math.cos(2 * math.pi * i / segments),
                                    r * (1 + weight * math.cos(folds * 2 * math.pi * i / segments)) * math.sin(2 * math.pi * i / segments), z))
                      for i in range(segments)])
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            k = (i + 1) % segments
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[k]))
            elif len(b) == 1:
                bm.faces.new((a[i], a[k], b[0]))
            else:
                bm.faces.new((a[i], a[k], b[k], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat, sharp=sharp)


def taper(name, mat, points, radii, sides=14, reference=Vector((0, 0, 1))):
    points = [Vector(p) for p in points]
    bm = bmesh.new()
    rings = []
    for p, r, (t, n, b) in zip(points, radii, frames(points, False, reference)):
        rings.append([bm.verts.new(p + r * (math.cos(2 * math.pi * j / sides) * n + math.sin(2 * math.pi * j / sides) * b)) for j in range(sides)])
    for a, c in zip(rings, rings[1:]):
        for j in range(sides):
            k = (j + 1) % sides
            bm.faces.new((a[j], a[k], c[k], c[j]))
    for ring_, centre in ((rings[0], points[0]), (rings[-1], points[-1])):
        hub = bm.verts.new(centre)
        for j in range(sides):
            bm.faces.new((ring_[j], ring_[(j + 1) % sides], hub))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat)


def interpolate(values, count):
    out = []
    for i in range(count):
        t = i / (count - 1) * (len(values) - 1)
        k = min(int(t), len(values) - 2)
        out.append(values[k] + (values[k + 1] - values[k]) * (t - k))
    return out


def look(camera, target, direction, distance):
    camera.location = target + direction.normalized() * distance
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def preview(obj, word, folder, samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 4
    scene.render.resolution_x = scene.render.resolution_y = PREVIEW_SIZE
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Preview")
    try:
        world.use_nodes = True
    except (AttributeError, TypeError):
        pass
    background = world.node_tree.nodes.get("Background") if world.node_tree else None
    if background:
        background.inputs[0].default_value = (0.147, 0.171, 0.196, 1.0)
        background.inputs[1].default_value = 1.0
    world.color = (0.147, 0.171, 0.196)
    scene.world = world
    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    camera.data.type = "ORTHO"
    scene.collection.objects.link(camera)
    scene.camera = camera
    sun = bpy.data.objects.new("PreviewSun", bpy.data.lights.new("PreviewSun", "SUN"))
    sun.data.energy = 3.2
    scene.collection.objects.link(sun)
    lo, hi = mesh_bounds(obj)
    centre = (lo + hi) / 2
    corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    elevation = math.radians(35)
    views = [Vector((0, 1, 0)), Vector((0, -1, 0)),
             Vector((-math.cos(elevation) * math.sqrt(0.5), math.cos(elevation) * math.sqrt(0.5), math.sin(elevation))), Vector((0, -22, 30))]
    shots = []
    with tempfile.TemporaryDirectory() as tmp:
        for k, direction in enumerate(views):
            look(camera, centre, direction, (hi - lo).length * 3 + 1)
            bpy.context.view_layer.update()
            inverse = camera.matrix_world.inverted()
            spans = [inverse @ c for c in corners]
            camera.data.ortho_scale = 1.15 * 2 * max(max(abs(p.x), abs(p.y)) for p in spans)
            heading = Vector((direction.x, direction.y, 0)).normalized()
            heading = Matrix.Rotation(math.radians(-32), 3, "Z") @ heading
            light = (math.cos(math.radians(48)) * heading + Vector((0, 0, math.sin(math.radians(48))))).normalized()
            sun.rotation_euler = (-light).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = str(Path(tmp) / f"{k}.png")
            bpy.ops.render.render(write_still=True)
            shots.append(Path(tmp) / f"{k}.png")
        out = Path(folder) / f"{word}.png"
        compose(shots, out)
    bpy.data.objects.remove(camera)
    bpy.data.objects.remove(sun)
    return str(out).replace("\\", "/")


def compose(paths, out):
    n = PREVIEW_SIZE
    sheet_pixels = array("f", [0.0]) * (4 * n * n * 4)
    for k, path in enumerate(paths):
        image = bpy.data.images.load(str(path))
        pixels = array("f", [0.0]) * (n * n * 4)
        image.pixels.foreach_get(pixels)
        column, row = k % 2, 1 - k // 2
        for y in range(n):
            start = ((row * n + y) * 2 * n + column * n) * 4
            sheet_pixels[start:start + n * 4] = pixels[y * n * 4:(y + 1) * n * 4]
        bpy.data.images.remove(image)
    image = bpy.data.images.new("Sheet", 2 * n, 2 * n, alpha=False)
    image.pixels.foreach_set(sheet_pixels)
    out.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(out)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def densify(profile, steps=3):
    points = spline([(r, z, 0.0) for r, z in profile], steps)
    last = len(points) - 1
    return [(points[i][0] if i in (0, last) else max(points[i][0], 1e-3), points[i][1]) for i in range(len(points))]


def pumpkin(name, mat, at, radii, lobes, groove, dimple=0.0, segments=48, rings=24):
    bm = bmesh.new()
    top = bm.verts.new((at[0], at[1], at[2] + radii[2] - dimple))
    bottom = bm.verts.new((at[0], at[1], at[2] - radii[2] + dimple * 0.4))
    grid = []
    for j in range(1, rings):
        v = math.pi * j / rings
        crown = math.sin(v) * (1 - v / math.pi) ** 1.5
        sink = dimple * (math.exp(-(v / 0.4) ** 2) - 0.4 * math.exp(-((math.pi - v) / 0.4) ** 2))
        row = []
        for i in range(segments):
            u = 2 * math.pi * i / segments
            swell = 1 - groove * crown * (0.5 - 0.5 * math.cos(lobes * u))
            row.append(bm.verts.new((at[0] + radii[0] * swell * math.sin(v) * math.cos(u), at[1] + radii[1] * swell * math.sin(v) * math.sin(u),
                                     at[2] + radii[2] * math.cos(v) - sink)))
        grid.append(row)
    for i in range(segments):
        k = (i + 1) % segments
        bm.faces.new((top, grid[0][i], grid[0][k]))
        bm.faces.new((bottom, grid[-1][k], grid[-1][i]))
        for j in range(len(grid) - 1):
            bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][k], grid[j][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return emit(name, bm, mat)


def banded(name, mats, profile, at=(0, 0, 0), axis="Z", segments=48, period=6, stripe=2, wave=0.0, waves=2.0, sharp=None, twist=0.0):
    bm = bmesh.new()
    low, high = min(z for _, z in profile), max(z for _, z in profile)
    rings = []
    for r, z in profile:
        if r < 1e-7:
            rings.append([bm.verts.new((0.0, 0.0, z))])
            continue
        shift = wave * math.sin(2 * math.pi * waves * (z - low) / (high - low)) + twist * (z - low) / (high - low)
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segments + shift), r * math.sin(2 * math.pi * i / segments + shift), z))
                      for i in range(segments)])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(segments):
            k = (i + 1) % segments
            if len(a) == 1:
                face = bm.faces.new((a[0], b[i], b[k]))
            elif len(b) == 1:
                face = bm.faces.new((a[i], a[k], b[0]))
            else:
                face = bm.faces.new((a[i], a[k], b[k], b[i]))
            face.material_index = 1 if i % period < stripe else 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    place(bm, at, AXIS_TURN[axis])
    return emit(name, bm, mats, sharp=sharp)
