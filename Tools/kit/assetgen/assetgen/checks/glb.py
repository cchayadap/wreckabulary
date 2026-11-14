"""GLB mesh check (pure Python + Pillow for texture sizes).

Numbers instead of eyeballing (ARCHITECTURE §3.7 improvement 5): triangles, world-space bounds (node transforms
applied to each POSITION accessor's min/max box), height in metres (glTF is Y-up, 1 unit = 1 m), pivot at bottom-centre
(min Y ≈ 0, centre X/Z ≈ 0), materials with metallic > 0 (glTF's default metallicFactor is 1.0), texture sizes, missing
external textures, skins and animations. Budgets come from style_lock class_budgets[<class>] or flags. Skinned meshes
are measured in their rest pose. To fix a pivot, run Tools/kit/blender/center_glb_bottom.py (blender-rig-animate).
"""

import io
import json
import math
import struct
from pathlib import Path

from ..sniff import sniff, sniff_file
from . import fail, new_result

CT = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}
JSON_CHUNK, BIN_CHUNK = 0x4E4F534A, 0x004E4942


class GlbError(Exception):
    pass


def parse_glb(data):
    if len(data) < 20:
        raise GlbError("file too short for a GLB header")
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise GlbError("not a GLB (magic bytes)")
    if version != 2:
        raise GlbError(f"glTF version {version} (need 2)")
    off, gltf, binc = 12, None, None
    while off + 8 <= min(len(data), length):
        clen, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8: off + 8 + clen]
        if ctype == JSON_CHUNK and gltf is None:
            gltf = json.loads(chunk.decode("utf-8"))
        elif ctype == BIN_CHUNK and binc is None:
            binc = chunk
        off += 8 + clen
    if gltf is None:
        raise GlbError("no JSON chunk")
    return gltf, binc or b""


def _mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def _identity():
    return [[1.0 if i == j else 0.0 for j in range(4)] for i in range(4)]


def node_matrix(node):
    if "matrix" in node:
        m = node["matrix"]
        return [[m[j * 4 + i] for j in range(4)] for i in range(4)]
    tx, ty, tz = node.get("translation", [0, 0, 0])
    qx, qy, qz, qw = node.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = node.get("scale", [1, 1, 1])
    r = [
        [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
        [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
        [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)],
    ]
    return [
        [r[0][0] * sx, r[0][1] * sy, r[0][2] * sz, tx],
        [r[1][0] * sx, r[1][1] * sy, r[1][2] * sz, ty],
        [r[2][0] * sx, r[2][1] * sy, r[2][2] * sz, tz],
        [0.0, 0.0, 0.0, 1.0],
    ]


def _apply(m, p):
    x, y, z = p
    return tuple(m[i][0] * x + m[i][1] * y + m[i][2] * z + m[i][3] for i in range(3))


def read_accessor(gltf, binc, idx):
    acc = gltf["accessors"][idx]
    fmt, size = CT[acc["componentType"]]
    n = NC[acc["type"]]
    if "bufferView" not in acc:
        return [tuple([0] * n)] * acc["count"]
    bv = gltf["bufferViews"][acc["bufferView"]]
    if bv.get("buffer", 0) != 0:
        raise GlbError("external buffers are not supported by this check")
    base = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or size * n
    out = []
    for i in range(acc["count"]):
        out.append(struct.unpack_from("<" + fmt * n, binc, base + i * stride))
    return out


def _minmax(gltf, binc, idx):
    acc = gltf["accessors"][idx]
    if "min" in acc and "max" in acc and len(acc["min"]) >= 3:
        return acc["min"][:3], acc["max"][:3]
    pts = read_accessor(gltf, binc, idx)
    if not pts:
        return None
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def _world_nodes(gltf):
    nodes = gltf.get("nodes", [])
    if gltf.get("scenes"):
        roots = gltf["scenes"][gltf.get("scene", 0)].get("nodes", [])
    else:
        children = {c for n in nodes for c in n.get("children", [])}
        roots = [i for i in range(len(nodes)) if i not in children]
    out, stack, seen = [], [(r, _identity()) for r in roots], set()
    while stack:
        idx, parent = stack.pop()
        if idx in seen or idx >= len(nodes):
            continue
        seen.add(idx)
        world = _mat_mul(parent, node_matrix(nodes[idx]))
        out.append((idx, world))
        for c in nodes[idx].get("children", []):
            stack.append((c, world))
    return out


def _tris(mode, count):
    if mode == 4:
        return count // 3
    if mode in (5, 6):
        return max(0, count - 2)
    return 0


def _image_bytes(gltf, binc, img, base_dir):
    if "bufferView" in img:
        bv = gltf["bufferViews"][img["bufferView"]]
        o = bv.get("byteOffset", 0)
        return binc[o: o + bv["byteLength"]], "embedded"
    uri = img.get("uri", "")
    if uri.startswith("data:"):
        import base64

        return base64.b64decode(uri.split(",", 1)[1]), "data_uri"
    p = (base_dir / uri).resolve()
    if not p.is_file():
        return None, f"missing:{uri}"
    return p.read_bytes(), "external"


def budgets_from(style_lock, opts):
    cls = opts.get("class_budget")
    b = dict(((style_lock or {}).get("class_budgets") or {}).get(cls) or {}) if cls else {}
    for k in ("tris_max", "tex_max"):
        if opts.get(k) is not None:
            b[k] = int(opts[k])
    if opts.get("height_range"):
        lo, hi = (float(v) for v in str(opts["height_range"]).split(","))
        b["height_m"] = [lo, hi]
    return b


def check_mesh(path, opts, style_lock=None):
    res = new_result("mesh", path)
    kind, _ = sniff_file(path)
    res["metrics"]["format"] = kind
    if kind != "glb":
        return fail(res, "format", f"only GLB is measured here (magic bytes say {kind}); convert with Tools/kit/blender first", value=kind)
    data = Path(path).read_bytes()
    try:
        gltf, binc = parse_glb(data)
    except (GlbError, ValueError, KeyError, struct.error) as e:
        return fail(res, "parse", f"GLB does not parse: {e}")
    m = res["metrics"]
    accessors = gltf.get("accessors", [])
    meshes = gltf.get("meshes", [])
    tris = verts = prims = 0
    lo, hi = [math.inf] * 3, [-math.inf] * 3
    no_material = 0
    try:
        for idx, world in _world_nodes(gltf):
            node = gltf["nodes"][idx]
            if "mesh" not in node:
                continue
            for prim in meshes[node["mesh"]].get("primitives", []):
                prims += 1
                pos = prim.get("attributes", {}).get("POSITION")
                if pos is None:
                    continue
                count = accessors[prim["indices"]]["count"] if "indices" in prim else accessors[pos]["count"]
                tris += _tris(prim.get("mode", 4), count)
                verts += accessors[pos]["count"]
                if "material" not in prim:
                    no_material += 1
                mm = _minmax(gltf, binc, pos)
                if not mm:
                    continue
                (x0, y0, z0), (x1, y1, z1) = mm
                for c in [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]:
                    w = _apply(world, c)
                    for i in range(3):
                        lo[i] = min(lo[i], w[i])
                        hi[i] = max(hi[i], w[i])
    except (KeyError, IndexError, struct.error, GlbError) as e:
        return fail(res, "parse", f"GLB structure is broken: {type(e).__name__}: {e}")
    m.update({"meshes": len(meshes), "primitives": prims, "nodes": len(gltf.get("nodes", [])), "triangles": tris, "vertices": verts,
              "primitives_without_material": no_material, "skinned": bool(gltf.get("skins")),
              "joints": sum(len(s.get("joints", [])) for s in gltf.get("skins", [])), "animations": len(gltf.get("animations", []))})
    if tris == 0 or lo[0] == math.inf:
        return fail(res, "geometry", "no triangles in the default scene")
    size = [hi[i] - lo[i] for i in range(3)]
    m["bounds"] = {"min": [round(v, 5) for v in lo], "max": [round(v, 5) for v in hi], "size": [round(v, 5) for v in size]}
    m["height_m"] = round(size[1], 5)
    cx, cz = (lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2
    extent = max(size[0], size[2], 1e-6)
    tol_y = max(float(opts.get("pivot_tol_m", 0.01)), 0.01 * size[1])
    tol_xz = max(float(opts.get("pivot_tol_m", 0.01)), 0.02 * extent)
    pivot_ok = abs(lo[1]) <= tol_y and abs(cx) <= tol_xz and abs(cz) <= tol_xz
    m["pivot"] = {"min_y": round(lo[1], 5), "center_x": round(cx, 5), "center_z": round(cz, 5), "ok": pivot_ok}
    if not pivot_ok and not opts.get("skip_pivot"):
        fail(res, "pivot", "pivot is not at bottom-centre (min Y ≈ 0, centre X/Z ≈ 0); run Tools/kit/blender/center_glb_bottom.py", value=m["pivot"])
    mats = []
    for i, mat in enumerate(gltf.get("materials", [])):
        pbr = mat.get("pbrMetallicRoughness", {})
        metallic = float(pbr.get("metallicFactor", 1.0))
        mats.append({"index": i, "name": mat.get("name", ""), "metallic": metallic, "roughness": float(pbr.get("roughnessFactor", 1.0)),
                     "metallic_texture": "metallicRoughnessTexture" in pbr, "double_sided": bool(mat.get("doubleSided", False))})
    m["materials"] = mats
    metallic_bad = [x for x in mats if x["metallic"] > 0]
    if metallic_bad and not opts.get("allow_metallic"):
        fail(res, "metallic", "materials with metallic > 0 (glTF default is 1.0): stylised assets keep metallic 0 unless whitelisted", value=[x["name"] or x["index"] for x in metallic_bad])
    texs, tex_max_px = [], 0
    base_dir = Path(path).parent
    for i, img in enumerate(gltf.get("images", [])):
        raw, source = _image_bytes(gltf, binc, img, base_dir)
        if raw is None:
            fail(res, "missing_texture", f"image {i} is not in the file and not next to it ({source})")
            texs.append({"index": i, "source": source})
            continue
        entry = {"index": i, "source": source, "bytes": len(raw), "kind": sniff(raw[:64])[0]}
        try:
            from PIL import Image

            with Image.open(io.BytesIO(raw)) as im:
                entry["width"], entry["height"] = im.size
                tex_max_px = max(tex_max_px, max(im.size))
        except Exception:
            entry["unreadable"] = True
            res["warnings"].append(f"texture {i} could not be decoded (webp or KTX2 extension?)")
        texs.append(entry)
    m["textures"] = texs
    m["tex_max_px"] = tex_max_px
    b = budgets_from(style_lock, opts)
    m["budget"] = b
    if b.get("tris_max") is not None and tris > b["tris_max"]:
        fail(res, "tris_max", "triangle count is over the class budget", value=tris, limit=b["tris_max"])
    if b.get("tex_max") is not None and tex_max_px > b["tex_max"]:
        fail(res, "tex_max", "a texture is larger than the class budget", value=tex_max_px, limit=b["tex_max"])
    if b.get("height_m"):
        h0, h1 = b["height_m"]
        if not (h0 <= size[1] <= h1):
            fail(res, "height_m", "height in metres is outside the class range (scale)", value=round(size[1], 4), limit=[h0, h1])
    return res
