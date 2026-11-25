import argparse
import hashlib
import json
import math
import re
import struct
import sys
import tempfile
import zlib
from array import array
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

REPO = Path(__file__).resolve().parents[2]
PROJECT = REPO / "Assets/_Project"
MODELS = PROJECT / "Art/Collections/HouseItemsV1/Models"
PREFABS = PROJECT / "Prefabs/Equipment/HouseItemsV1"
LIBRARY = PROJECT / "Materials/Library"
MATERIALS_JSON = PROJECT / "Data/Generated/materials.json"
CLASSIC = PROJECT / "Art/Imported/Items"
MANIFEST = REPO / "ArtSource/Collections/HouseItemsV1/manifest.json"
SCRIPT = "Tools/AssetPipeline/rebuild_house_items.py"
WORDS = ("PILLOW", "RACK", "MIRROR", "SPEAKER", "BENCH", "TOASTER", "BEAR")
REFERENCES = ("CHAIR", "SOFA", "BOOK", "LAMP", "CAKE")
ROOT_TRANSFORM = "-8679921383154817045"
AXES = ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign", "UnitScaleFactor")
SWAP = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))
AXIS_TURN = {"Z": (0.0, 0.0, 0.0), "Y": (-math.pi / 2, 0.0, 0.0), "X": (0.0, math.pi / 2, 0.0)}
FIT_WARNING = 0.05
BOUNDS_TOLERANCE = 2e-4
PREVIEW_SIZE = 384
PARTS = []
MATS = {}
PENDING = {}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out")
    p.add_argument("--in-place", action="store_true")
    p.add_argument("--report", required=True)
    p.add_argument("--preview")
    p.add_argument("--only", default=",".join(WORDS))
    p.add_argument("--inspect", action="store_true")
    p.add_argument("--samples", type=int, default=24)
    p.add_argument("--pending", action="append", default=[])
    args = p.parse_args(argv)
    if bool(args.out) == args.in_place:
        p.error("pass exactly one of --out DIR or --in-place")
    if args.in_place and args.pending:
        p.error("--pending colours are for staged builds; an in-place build needs every material in the library")
    for item in args.pending:
        name, values = item.split("=", 1)
        r, g, b, roughness = (float(v) for v in values.split(","))
        PENDING[name] = {"linear": (r, g, b), "alpha": 1.0, "metallic": 0.0, "smoothness": 1.0 - roughness, "source": "pending"}
    args.words = [w.strip().upper() for w in args.only.split(",") if w.strip()]
    unknown = [w for w in args.words if w not in WORDS]
    if unknown:
        p.error("unknown words: " + ", ".join(unknown))
    return args


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
    if name in PENDING:
        return PENDING[name]
    raise RuntimeError(f"{name} is not a Classic library material (no {path.name}, no materials.json entry)")


def prefab_transform(word):
    text = (PREFABS / f"{word}.prefab").read_text(encoding="utf-8")
    values = {}
    for m in re.finditer(r"target: \{fileID: (-?\d+),[^}]*\}\s*propertyPath: (\S+)\s*value: (\S*)", text):
        if m.group(1) == ROOT_TRANSFORM and m.group(3):
            values[m.group(2)] = float(m.group(3))
    scale = [values[f"m_LocalScale.{a}"] for a in "xyz"]
    if max(scale) - min(scale) > 1e-5:
        raise RuntimeError(f"{word}: prefab scale is not uniform {scale}")
    position = Vector([values[f"m_LocalPosition.{a}"] for a in "xyz"])
    q = [values[f"m_LocalRotation.{a}"] for a in "xyzw"]
    return scale[0], position, Quaternion((q[3], q[0], q[1], q[2])).to_matrix()


def import_scale(path):
    text = Path(str(path) + ".meta").read_text(encoding="utf-8")
    if not re.search(r"^\s+useFileScale: 1\s*$", text, re.M) or not re.search(r"^\s+bakeAxisConversion: 1\s*$", text, re.M):
        raise RuntimeError(f"{path.name}: importer must use file scale and baked axis conversion")
    return float(re.search(r"^\s+globalScale: ([0-9.eE+-]+)", text, re.M).group(1))


def manifest():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def target_size(entries, word):
    size = next(e["size"] for e in entries if e["word"] == word)
    return Vector((size[0], size[2], size[1]))


def export_matrix(word):
    s, t, r = prefab_transform(word)
    g = import_scale(MODELS / f"{word}.fbx")
    linear = (1.0 / (g * s)) * (SWAP @ r.transposed() @ SWAP)
    offset = -(1.0 / (g * s)) * (SWAP @ r.transposed() @ t)
    m = linear.to_4x4()
    m.translation = offset
    return m


def to_prefab(word):
    s, t, r = prefab_transform(word)
    g = import_scale(MODELS / f"{word}.fbx")
    return lambda f: t + s * (r @ Vector((g * f[0], g * f[1], -g * f[2])))


def box_of(points):
    lo = Vector((min(p[0] for p in points), min(p[1] for p in points), min(p[2] for p in points)))
    hi = Vector((max(p[0] for p in points), max(p[1] for p in points), max(p[2] for p in points)))
    return lo, hi


def rounded(v, n=5):
    return [round(x, n) for x in v]


def bounds_check(word, lo, hi, size_unity):
    size = hi - lo
    errors = []
    for i, axis in enumerate("xyz"):
        if abs(size[i] - size_unity[i]) > BOUNDS_TOLERANCE:
            errors.append(f"{word}: Unity size {axis} {size[i]:.5f} vs manifest {size_unity[i]:.5f}")
    if abs((lo.x + hi.x) / 2) > BOUNDS_TOLERANCE or abs((lo.z + hi.z) / 2) > BOUNDS_TOLERANCE or abs(lo.y) > BOUNDS_TOLERANCE:
        errors.append(f"{word}: not bottom-centred, min {rounded(lo)} max {rounded(hi)}")
    return errors


def read_ascii_fbx(path):
    text = path.read_text(encoding="latin1")
    values = [float(x) for x in re.search(r"Vertices: \*\d+ \{\s*a: ([^}]*)\}", text).group(1).split(",")]
    index = [int(x) for x in re.search(r"PolygonVertexIndex: \*\d+ \{\s*a: ([^}]*)\}", text).group(1).split(",")]
    settings = {k: float(v) for k, v in re.findall(r'P: "(' + "|".join(AXES) + r')", "[^"]*", "[^"]*", "[^"]*",\s*([-0-9.eE+]+)', text)}
    points = [tuple(values[i:i + 3]) for i in range(0, len(values), 3)]
    return {"settings": settings, "points": points, "polygons": polygons(index),
            "materials": re.findall(r'Material: \d+, "Material::([^"]+)"', text),
            "models": [list(m) for m in re.findall(r'Model: \d+, "Model::([^"]+)", "(\w+)"', text)]}


def polygons(index):
    out, current = [], []
    for i in index:
        if i < 0:
            current.append(-i - 1)
            out.append(current)
            current = []
        else:
            current.append(i)
    return out


def area_and_tris(points, polys):
    area, tris = 0.0, 0
    for poly in polys:
        a = Vector(points[poly[0]])
        for k in range(1, len(poly) - 1):
            area += (Vector(points[poly[k]]) - a).cross(Vector(points[poly[k + 1]]) - a).length / 2
            tris += 1
    return area, tris


def binary_nodes(data, pos, end, version):
    nodes = []
    wide = version >= 7500
    while pos < end:
        if wide:
            end_offset, count, length = struct.unpack_from("<QQQ", data, pos)
            pos += 24
        else:
            end_offset, count, length = struct.unpack_from("<III", data, pos)
            pos += 12
        name_length = data[pos]
        pos += 1
        if end_offset == 0:
            break
        name = data[pos:pos + name_length].decode("latin1")
        pos += name_length
        props, cursor = [], pos
        for _ in range(count):
            kind = chr(data[cursor])
            cursor += 1
            if kind in "YCIFDL":
                fmt, size = {"Y": ("<h", 2), "C": ("<?", 1), "I": ("<i", 4), "F": ("<f", 4), "D": ("<d", 8), "L": ("<q", 8)}[kind]
                props.append(struct.unpack_from(fmt, data, cursor)[0])
                cursor += size
            elif kind in "fdlib":
                n, encoding, compressed = struct.unpack_from("<III", data, cursor)
                cursor += 12
                raw = data[cursor:cursor + compressed]
                cursor += compressed
                if encoding == 1:
                    raw = zlib.decompress(raw)
                props.append(list(struct.unpack("<%d%s" % (n, {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}[kind]), raw)))
            elif kind in "SR":
                n = struct.unpack_from("<I", data, cursor)[0]
                cursor += 4
                raw = data[cursor:cursor + n]
                cursor += n
                props.append(raw.decode("latin1") if kind == "S" else raw)
            else:
                raise RuntimeError(f"unknown FBX property type {kind!r}")
        pos += length
        children = binary_nodes(data, pos, end_offset - (25 if wide else 13), version) if pos < end_offset else []
        nodes.append((name, props, children))
        pos = end_offset
    return nodes


def child(node, name):
    return next((c for c in node[2] if c[0] == name), None)


def properties70(node):
    block = child(node, "Properties70")
    return {c[1][0]: c[1][4:] for c in block[2] if c[0] == "P"} if block else {}


def read_binary_fbx(path):
    data = Path(path).read_bytes()
    if not data.startswith(b"Kaydara FBX Binary"):
        raise RuntimeError(f"{path} is not a binary FBX")
    version = struct.unpack_from("<I", data, 23)[0]
    top = {n[0]: n for n in binary_nodes(data, 27, len(data), version)}
    settings = {k: float(v[0]) for k, v in properties70(top["GlobalSettings"]).items() if k in AXES}
    models, geometries, materials = {}, {}, {}
    for node in top["Objects"][2]:
        label = node[1][1].split("\x00")[0] if len(node[1]) > 1 else ""
        if node[0] == "Model":
            models[node[1][0]] = {"name": label, "type": node[1][2], "props": properties70(node)}
        elif node[0] == "Geometry":
            values = child(node, "Vertices")[1][0]
            points = [tuple(values[i:i + 3]) for i in range(0, len(values), 3)]
            geometries[node[1][0]] = {"points": points, "polygons": polygons(child(node, "PolygonVertexIndex")[1][0])}
        elif node[0] == "Material":
            materials[node[1][0]] = label
    parents = {}
    for c in top["Connections"][2]:
        if c[0] == "C" and c[1][0] == "OO":
            parents.setdefault(c[1][1], []).append(c[1][2])
    return {"settings": settings, "models": models, "geometries": geometries, "materials": materials, "parents": parents}


def identity_transform(props):
    checks = {"Lcl Translation": 0.0, "Lcl Rotation": 0.0, "Lcl Scaling": 1.0, "PreRotation": 0.0, "PostRotation": 0.0,
              "GeometricTranslation": 0.0, "GeometricRotation": 0.0, "GeometricScaling": 1.0, "RotationOffset": 0.0, "ScalingOffset": 0.0}
    return [k for k, want in checks.items() if k in props and any(abs(float(v) - want) > 1e-5 for v in props[k][:3])]


def verify_fbx(path, word, size_unity, expected_materials, kenney_settings):
    fbx = read_binary_fbx(path)
    errors = []
    for key in AXES:
        if abs(fbx["settings"].get(key, float("nan")) - kenney_settings.get(key, float("nan"))) > 1e-6:
            errors.append(f"{word}: GlobalSettings {key} {fbx['settings'].get(key)} differs from the Kenney source {kenney_settings.get(key)}")
    roots = [i for i, m in fbx["models"].items() if 0 in fbx["parents"].get(i, [])]
    meshes = [i for i, m in fbx["models"].items() if m["type"] == "Mesh"]
    if len(roots) != 1 or fbx["models"][roots[0]]["type"] != "Null" or fbx["models"][roots[0]]["name"] != word:
        errors.append(f"{word}: expected one root Null named {word}, found {[fbx['models'][i]['name'] for i in roots]}")
    if len(meshes) != 1 or (roots and roots[0] not in fbx["parents"].get(meshes[0], [])):
        errors.append(f"{word}: expected one mesh under the root, found {len(meshes)}")
    for i, m in fbx["models"].items():
        moved = identity_transform(m["props"])
        if moved:
            errors.append(f"{word}: node {m['name']} carries a transform {moved}; the prefab override would discard it")
    points = [p for g in fbx["geometries"].values() for p in g["points"]]
    prefab = to_prefab(word)
    lo, hi = box_of([prefab(p) for p in points])
    errors += bounds_check(word, lo, hi, size_unity)
    names = sorted(fbx["materials"].values())
    if names != sorted(expected_materials):
        errors.append(f"{word}: FBX materials {names} differ from the built slots {sorted(expected_materials)}")
    tris = sum(len(p) - 2 for g in fbx["geometries"].values() for p in g["polygons"])
    return {"unityMin": rounded(lo), "unityMax": rounded(hi), "materials": names, "triangles": tris,
            "settings": fbx["settings"], "nodes": [[m["name"], m["type"]] for m in fbx["models"].values()]}, errors


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


def build_bear(size):
    fur, cream, red, dark = "plush_Classic", "cream_Classic", "red_Classic", "dark_Classic"
    ellipsoid("body", fur, (0, -0.075, 0.135), (0.097, 0.108, 0.125), 20, 12)
    ellipsoid("belly", cream, (0, 0.0, 0.125), (0.064, 0.042, 0.08), 16, 8)
    ellipsoid("tail", fur, (0, -0.19, 0.08), (0.032, 0.032, 0.03), 12, 6)
    ellipsoid("head", fur, (0, -0.045, 0.315), (0.104, 0.098, 0.1), 20, 12)
    ellipsoid("muzzle", cream, (0, 0.035, 0.29), (0.052, 0.04, 0.038), 16, 8)
    ellipsoid("nose", dark, (0, 0.073, 0.305), (0.02, 0.012, 0.014), 12, 6)
    ellipsoid("knot", red, (0, 0.02, 0.223), (0.016, 0.012, 0.016), 12, 6)
    torus("collar", red, (0.068, 0.076), 0.011, (0, -0.07, 0.222), "Z", 24, 8)
    for side in (-1, 1):
        ellipsoid("heart_lobe", red, (side * 0.011, 0.039, 0.148), (0.013, 0.006, 0.013), 12, 6)
    ellipsoid("heart_tip", red, (0, 0.039, 0.134), (0.011, 0.006, 0.014), 12, 6, (0, math.radians(45), 0))
    for side in (-1, 1):
        ellipsoid("ear", fur, (side * 0.07, -0.06, 0.41), (0.04, 0.024, 0.04), 14, 7)
        ellipsoid("inner_ear", cream, (side * 0.07, -0.042, 0.407), (0.025, 0.008, 0.025), 12, 6)
        ellipsoid("eye", dark, (side * 0.037, 0.038, 0.345), (0.0125, 0.0125, 0.0125), 10, 5)
        ellipsoid("arm", fur, (side * 0.088, -0.02, 0.165), (0.032, 0.04, 0.072), 14, 7, (math.radians(25), math.radians(-side * 15), 0))
        ellipsoid("leg", fur, (side * 0.056, 0.04, 0.052), (0.05, 0.085, 0.052), 14, 7)
        ellipsoid("foot", fur, (side * 0.058, 0.13, 0.068), (0.047, 0.038, 0.06), 14, 7)
        ellipsoid("pad", cream, (side * 0.058, 0.166, 0.07), (0.033, 0.009, 0.043), 12, 6)
        ellipsoid("loop", red, (side * 0.034, 0.015, 0.226), (0.032, 0.013, 0.021), 12, 6, (0, math.radians(-side * 14), 0))
        ellipsoid("ribbon", red, (side * 0.015, 0.017, 0.198), (0.01, 0.007, 0.024), 10, 5, (0, math.radians(-side * 22), 0))


def build_bench(size):
    frame, plank = "green_Classic", "wood_dark_Classic"
    for side in (-1, 1):
        x = side * 0.51
        box("front_leg", frame, (0.06, 0.06, 0.4), (x, 0.258, 0.2), 0.012)
        box("back_leg", frame, (0.06, 0.06, 0.5238), (x, -0.258, 0.2619), 0.012)
        box("rail", frame, (0.055, 0.56, 0.05), (x, 0.0, 0.255), 0.01)
        box("stretcher", frame, (0.045, 0.5, 0.035), (x, 0.0, 0.075), 0.01)
        box("arm", frame, (0.075, 0.54, 0.035), (side * 0.5125, 0.01, 0.4175), 0.012)
    for y in (0.205, 0.09, -0.025, -0.14):
        box("seat", plank, (0.98, 0.105, 0.035), (0, y, 0.2975), 0.01)
    box("brace", plank, (0.94, 0.04, 0.035), (0, 0.03, 0.262), 0.008)
    box("back_low", plank, (0.98, 0.03, 0.08), (0, -0.248, 0.372), 0.01, rotation=(math.radians(10), 0, 0))
    box("back_high", plank, (0.98, 0.03, 0.08), (0, -0.262, 0.468), 0.01, rotation=(math.radians(10), 0, 0))


def build_mirror(size):
    gold, glass, white, wood = "gold_Classic", "glass_Classic", "white_Classic", "wood_dark_Classic"
    cylinder("foot", wood, 0.1, 0.045, (0, 0, 0.0225), "Z", 40, bevel=0.012, scale=(1.45, 1.0, 1.0))
    for side in (-1, 1):
        cylinder("post", gold, 0.012, 0.04, (side * 0.08, 0, 0.06), "Z", 16, bevel=0.003)
    opening = rounded_rect(0.336, 0.44, 0.012, 0.12, 0.11)
    ring("frame", gold, rounded_rect(0.416, 0.52, 0.05, 0.16, 0.07), opening, -0.022, 0.022, bevel=(0.006, 2))
    ellipsoid("crest", gold, (0, 0, 0.578), (0.032, 0.02, 0.022), 16, 8)
    backing = rounded_rect(0.35, 0.454, 0.02, 0.127, 0.103)
    slab("backing", white, backing, 0.003, 0.008)
    sheet("glass", glass, opening, 0.0105)
    sheet("glint", white, streak(-0.07, 0.4, 0.17, 0.03, math.radians(45)), 0.012)
    sheet("glint", white, streak(0.005, 0.385, 0.1, 0.018, math.radians(45)), 0.012)
    slab("back_panel", wood, backing, -0.016, -0.004)
    ring("back_trim", gold, rounded_rect(0.3, 0.4, 0.01, 0.1, 0.13), rounded_rect(0.284, 0.384, 0.004, 0.092, 0.138), -0.019, -0.015)


def build_pillow(size):
    body, trim = "blue_Classic", "cream_Classic"
    width, depth, height = size
    pipe, pinch, n = 0.015, 0.1, 18
    a, b, c = width / 2 - pipe, height / 2 - pipe, height / 2
    ts = [math.sin(math.pi / 2 * (-1 + 2 * i / n)) for i in range(n + 1)]

    def edge(u, v):
        return a * u * (1 - pinch * (1 - v * v)), c + b * v * (1 - pinch * (1 - u * u))

    def lift(u, v):
        return (max(0.0, 1 - u * u) ** 0.55) * (max(0.0, 1 - v * v) ** 0.55) * (1 - 0.22 * math.exp(-(u * u + v * v) / 0.035))

    half = depth / 2 / max(lift(u, v) for u in ts for v in ts)
    bm = bmesh.new()
    shared = {}
    for s in (1, -1):
        grid = {}
        for i, u in enumerate(ts):
            for j, v in enumerate(ts):
                rim = i in (0, n) or j in (0, n)
                if rim and (i, j) in shared:
                    grid[i, j] = shared[i, j]
                    continue
                x, z = edge(u, v)
                grid[i, j] = bm.verts.new((x, s * half * lift(u, v), z))
                if rim:
                    shared[i, j] = grid[i, j]
        for i in range(n):
            for j in range(n):
                quad = (grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1])
                bm.faces.new(quad if s > 0 else tuple(reversed(quad)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    emit("cushion", bm, body, sharp=70)
    loop = [(i, 0) for i in range(n + 1)] + [(n, j) for j in range(1, n + 1)] + [(i, n) for i in range(n - 1, -1, -1)] + [(0, j) for j in range(n - 1, 0, -1)]
    tube("piping", trim, [(edge(ts[i], ts[j])[0], 0.0, edge(ts[i], ts[j])[1]) for i, j in loop], pipe, 10, True, Vector((0, 1, 0)))
    for s in (1, -1):
        ellipsoid("button", trim, (0, s * half * lift(0.0, 0.0), c), (0.024, 0.009, 0.024), 14, 7)


def build_rack(size):
    wood, gold = "wood_dark_Classic", "gold_Classic"
    lathe("base", wood, [(0, 0), (0.21, 0), (0.22, 0.01), (0.222, 0.028), (0.212, 0.044), (0.18, 0.064), (0.13, 0.084),
                         (0.08, 0.104), (0.055, 0.118), (0, 0.118)], segments=36, sharp=50)
    cylinder("collar", gold, 0.048, 0.03, (0, 0, 0.128), "Z", 24, bevel=0.006)
    cylinder("pole", wood, 0.034, 1.37, (0, 0, 0.815), "Z", 20, bevel=0)
    torus("band", gold, 0.036, 0.008, (0, 0, 0.95), "Z", 24, 6)
    for z in (0.22, 0.42):
        torus("band", gold, 0.036, 0.006, (0, 0, z), "Z", 24, 6)
    cylinder("hub", gold, 0.05, 0.055, (0, 0, 1.33), "Z", 24, bevel=0.008)
    cylinder("hub_low", gold, 0.046, 0.045, (0, 0, 1.12), "Z", 24, bevel=0.006)
    upper = [(0.035, 1.32), (0.1, 1.335), (0.165, 1.37), (0.21, 1.42), (0.235, 1.468), (0.243, 1.5)]
    lower = [(0.035, 1.115), (0.08, 1.125), (0.125, 1.155), (0.155, 1.195), (0.165, 1.228)]
    for k in range(4):
        for path, angle, radius, ball, tip in ((upper, k * 90, 0.016, 0.032, (0.2514, 1.522)), (lower, 45 + k * 90, 0.014, 0.027, (0.167, 1.25))):
            d = Vector((math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0))
            tube("hook", wood, spline([(r * d.x, r * d.y, z) for r, z in path], 3), radius, 8)
            ellipsoid("tip", gold, (tip[0] * d.x, tip[0] * d.y, tip[1]), (ball, ball, ball), 12, 6)
    cylinder("neck", wood, 0.034, 0.02, (0, 0, 1.51), "Z", 20, bevel=0, top=0.024)
    ellipsoid("finial", gold, (0, 0, 1.555), (0.045, 0.045, 0.045), 16, 8)


def build_speaker(size):
    body, metal, cone, wood = "dark_Classic", "metal_Classic", "red_Classic", "wood_dark_Classic"
    y0, front, back = -0.0097, 0.0753, -0.0947
    box("plinth", wood, (0.2093, 0.19, 0.03), (0, y0, 0.015), 0.008)
    box("cabinet", body, (0.196, 0.17, 0.854), (0, y0, 0.457), 0.016)
    for z in (0.255, 0.53):
        cylinder("baffle", body, 0.078, 0.012, (0, front + 0.006, z), "Y", 32, bevel=0.003)
        torus("rim", metal, 0.072, 0.011, (0, front + 0.018, z), "Y", 24, 6)
        lathe("cone", cone, [(0, 0), (0.022, 0.003), (0.045, 0.01), (0.066, 0.018), (0.066, 0), (0, 0)], (0, front + 0.009, z), "Y", 24)
        ellipsoid("cap", metal, (0, front + 0.013, z), (0.024, 0.011, 0.024), 16, 8)
    torus("tweeter_rim", metal, 0.03, 0.007, (0, front + 0.008, 0.745), "Y", 24, 6)
    ellipsoid("power_light", cone, (0.062, front + 0.003, 0.84), (0.009, 0.004, 0.009), 12, 6)
    ellipsoid("tweeter", cone, (0, front + 0.004, 0.745), (0.024, 0.012, 0.024), 16, 8)
    torus("port", metal, 0.024, 0.006, (0, front + 0.006, 0.1), "Y", 24, 6)
    torus("top_rim", metal, 0.07, 0.008, (0, y0, 0.892), "Z", 24, 6)
    lathe("top_cone", cone, [(0, 0), (0.02, 0.002), (0.04, 0.007), (0.064, 0.014), (0.064, 0), (0, 0)], (0, y0, 0.885), "Z", 24)
    ellipsoid("top_cap", metal, (0, y0, 0.888), (0.022, 0.022, 0.009), 16, 8)
    box("terminal", metal, (0.07, 0.004, 0.05), (0, back - 0.002, 0.2), 0.0015)
    for x, mat in ((-0.017, cone), (0.017, body)):
        cylinder("post", mat, 0.0075, 0.01, (x, back - 0.005, 0.2), "Y", 16, bevel=0.002)
    torus("back_port", metal, 0.026, 0.006, (0, back - 0.003, 0.62), "Y", 24, 6)


def build_toaster(size):
    shell, metal, slot, face, crust = "red_Classic", "metal_Classic", "dark_Classic", "wood_light_Classic", "wood_dark_Classic"
    box("base", metal, (0.29, 0.1596, 0.014), (0, 0, 0.007), 0.006)
    box("shell", shell, (0.27, 0.146, 0.148), (0, 0, 0.088), 0.035, segments=4)
    right = [(0.068, 0.0), (0.068, 0.068), (0.074, 0.078), (0.075, 0.088), (0.068, 0.097), (0.055, 0.101), (0.035, 0.104), (0.015, 0.105)]
    bread = right + [(-x, z) for x, z in reversed(right)]
    for side in (-1, 1):
        y = side * 0.034
        box("strip", metal, (0.19, 0.006, 0.014), (0, side * 0.073, 0.06), 0.003)
        box("rim", metal, (0.2, 0.036, 0.008), (0, y, 0.162), 0.004)
        box("slot", slot, (0.18, 0.016, 0.004), (0, y, 0.1655), 0.0015)
        slab("toast", [crust, face], [(x, z + 0.1025) for x, z in bread], y - 0.008, y + 0.008, bevel=(0.003, 2))
    box("lever_plate", metal, (0.004, 0.018, 0.08), (0.1352, 0, 0.09), 0.0015)
    box("lever", slot, (0.016, 0.03, 0.016), (0.142, 0, 0.12), 0.005)
    cylinder("dial", metal, 0.017, 0.01, (-0.14, 0, 0.09), "X", 24, bevel=0.002)
    cylinder("knob", slot, 0.011, 0.006, (-0.147, 0, 0.09), "X", 20, bevel=0.0015)


BUILDERS = {"BEAR": build_bear, "BENCH": build_bench, "MIRROR": build_mirror, "PILLOW": build_pillow,
            "RACK": build_rack, "SPEAKER": build_speaker, "TOASTER": build_toaster}


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


def export(obj, word, path):
    root = bpy.data.objects.new(word, None)
    bpy.context.scene.collection.objects.link(root)
    data = obj.data.copy()
    data.name = word + "_Mesh"
    data.transform(export_matrix(word))
    data.update()
    copy = bpy.data.objects.new(word + "_Mesh", data)
    bpy.context.scene.collection.objects.link(copy)
    copy.parent = root
    for o in bpy.context.scene.objects:
        o.select_set(o in (root, copy))
    bpy.context.view_layer.objects.active = root
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    bpy.data.objects.remove(copy)
    bpy.data.objects.remove(root)
    bpy.data.meshes.remove(data)
    blob = path.read_bytes()
    return {"path": str(path).replace("\\", "/"), "bytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()}


def reimport(path, word, size_unity):
    reset()
    bpy.ops.import_scene.fbx(filepath=str(path))
    g = import_scale(MODELS / f"{word}.fbx")
    s, t, r = prefab_transform(word)
    points = [t + s * (r @ (g * (SWAP @ (o.matrix_world @ v.co)))) for o in bpy.context.scene.objects if o.type == "MESH" for v in o.data.vertices]
    lo, hi = box_of(points)
    return {"unityMin": rounded(lo), "unityMax": rounded(hi)}, bounds_check(word, lo, hi, size_unity)


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


def inspect(entries):
    report = {"kenney": {}, "classic": {}}
    for word in WORDS:
        source = MODELS / f"{word}.fbx"
        if not source.read_bytes().startswith(b"; FBX"):
            report["kenney"][word] = {"note": "already rebuilt (binary FBX)"}
            continue
        fbx = read_ascii_fbx(source)
        prefab = to_prefab(word)
        mapped = [prefab(p) for p in fbx["points"]]
        lo, hi = box_of(mapped)
        area, tris = area_and_tris(mapped, fbx["polygons"])
        size = target_size(entries, word)
        report["kenney"][word] = {"models": fbx["models"], "materials": fbx["materials"], "triangles": tris, "area": round(area, 4),
                                  "trianglesPerM2": round(tris / area, 1), "settings": fbx["settings"], "unityMin": rounded(lo), "unityMax": rounded(hi),
                                  "mappingErrors": bounds_check(word, lo, hi, (size.x, size.z, size.y)), "prefabTransform": [round(prefab_transform(word)[0], 6)]}
    for name in REFERENCES:
        path = CLASSIC / f"{name}.fbx"
        reset()
        bpy.ops.import_scene.fbx(filepath=str(path))
        g = import_scale(path)
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        points = [g * (SWAP @ (o.matrix_world @ v.co)) for o in meshes for v in o.data.vertices]
        lo, hi = box_of(points)
        tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
        area = sum(p.area * (o.matrix_world.to_scale().x ** 2) for o in meshes for p in o.data.polygons) * g * g
        fbx = read_binary_fbx(path)
        report["classic"][name] = {"objects": [[o.name, o.type] for o in bpy.context.scene.objects], "materials": sorted(fbx["materials"].values()),
                                   "triangles": tris, "area": round(area, 4), "trianglesPerM2": round(tris / area, 1),
                                   "unityMin": rounded(lo), "unityMax": rounded(hi), "settings": fbx["settings"]}
    return report


def kenney_settings(word):
    source = MODELS / f"{word}.fbx"
    data = source.read_bytes()
    if data.startswith(b"; FBX"):
        return read_ascii_fbx(source)["settings"]
    return read_binary_fbx(source)["settings"]


def record_manifest(results):
    raw = MANIFEST.read_bytes().decode("utf-8")
    newline = "\r\n" if "\r\n" in raw else "\n"
    data = json.loads(raw)
    for entry in data["entries"]:
        result = results.get(entry["word"])
        if result:
            entry["rebuilt"] = {"script": SCRIPT, "bytes": result["bytes"], "sha256": result["sha256"]}
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    MANIFEST.write_bytes(text.replace("\n", newline).encode("utf-8"))


def main():
    args = parse_args()
    entries = manifest()["entries"]
    report = {"script": SCRIPT, "blender": bpy.app.version_string, "mode": "in-place" if args.in_place else "staged", "models": {}, "errors": [], "warnings": []}
    if args.inspect:
        report["inspect"] = inspect(entries)
        for word, item in report["inspect"]["kenney"].items():
            report["errors"] += item.get("mappingErrors", [])
    settings = {word: kenney_settings(word) for word in args.words}
    out_dir = MODELS if args.in_place else Path(args.out)
    written = {}
    for word in args.words:
        reset()
        size = target_size(entries, word)
        size_unity = (size.x, size.z, size.y)
        BUILDERS[word](size)
        obj = join(word)
        box_uv(obj)
        factors = fit(obj, size)
        item = {"fitFactors": [round(f, 4) for f in factors], **stats(obj)}
        if any(abs(f - 1) > FIT_WARNING for f in factors):
            report["warnings"].append(f"{word}: fit factors {item['fitFactors']} exceed {FIT_WARNING:.0%}")
        if args.preview:
            item["preview"] = preview(obj, word, args.preview, args.samples)
        target = out_dir / f"{word}.fbx"
        item["fbx"] = export(obj, word, target)
        check, errors = verify_fbx(target, word, size_unity, item["materials"], settings[word])
        item["fbxCheck"] = check
        report["errors"] += errors
        again, errors = reimport(target, word, size_unity)
        item["reimport"] = again
        report["errors"] += errors
        report["models"][word] = item
        written[word] = item["fbx"]
        print(f"HOUSE_ITEM {word} tris={item['triangles']} fit={item['fitFactors']} materials={item['materials']}", flush=True)
    if args.in_place and not report["errors"]:
        record_manifest(written)
        report["manifest"] = str(MANIFEST.relative_to(REPO)).replace("\\", "/")
    report["status"] = "OK" if not report["errors"] else "FAIL"
    Path(args.report).parent.mkdir(parents=True, exist_ok=True)
    Path(args.report).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("HOUSE_ITEMS_REBUILD " + json.dumps({"status": report["status"], "models": list(report["models"]), "errors": report["errors"][:20],
                                               "warnings": report["warnings"], "report": args.report}), flush=True)
    if report["errors"]:
        raise RuntimeError(f"{len(report['errors'])} check(s) failed; see {args.report}")


main()
