"""BELT builder: editable original-style leather belt with a gold frame buckle, 8-bone strap rig and review renders.

blender --background --factory-startup --python-exit-code 1 --python build_belt.py -- \
    --project-root <checkout> --stage build|render --run-name build-01 [--size 640] [--samples 16] [--threads 2]

Each run writes a new ArtSource/Collections/OriginalStyleBELTV1/<run-name>/ folder and never overwrites earlier runs.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import math
import os
import re
import shutil
import struct
import sys
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import belt_geometry as g  # noqa: E402

FAMILY = "ArtSource/Collections/OriginalStyleBELTV1"
TEX_W, TEX_H = 512, 256
TRI_CAP = 5500


# ---------------------------------------------------------------- files

def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def write_png(path, rgb):
    """8-bit RGB or RGBA PNG from a (h, w, c) float array in 0..1, row 0 at the top."""
    if os.path.exists(path):
        raise FileExistsError(path)
    a = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w, c = a.shape
    raw = b"".join(b"\x00" + a[y].tobytes() for y in range(h))
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6 if c == 4 else 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def write_json(path, data):
    if os.path.exists(path):
        raise FileExistsError(path)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, sort_keys=False)
        f.write("\n")


# ---------------------------------------------------------------- textures

def value_noise(w, h, cell, seed):
    rng = np.random.default_rng(seed)
    gw, gh = w // cell + 2, h // cell + 2
    grid = rng.random((gh, gw))
    ys, xs = np.mgrid[0:h, 0:w]
    fx, fy = xs / cell, ys / cell
    x0, y0 = fx.astype(int), fy.astype(int)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a = grid[y0 % gh, x0 % gw]; b = grid[y0 % gh, (x0 + 1) % gw]
    c = grid[(y0 + 1) % gh, x0 % gw]; d = grid[(y0 + 1) % gh, (x0 + 1) % gw]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def tileable_noise(w, h, seed):
    """Fractal grain that wraps horizontally and vertically inside each 256x256 tile."""
    out = np.zeros((h, w))
    amp = 1.0
    for octave, cell in enumerate((32, 16, 8, 4)):
        n = value_noise(w + cell * 2, h + cell * 2, cell, seed + octave)[:h, :w]
        out += amp * n
        amp *= 0.55
    out = (out - out.min()) / (out.max() - out.min() + 1e-9)
    return out


def leather_layers():
    """Shared masks in atlas pixels: grain, edge paint, stitch dashes, creases. Row 0 is the image top (V = 1)."""
    h, w = TEX_H, TEX_W
    v, u = np.mgrid[0:h, 0:w]
    U = (u + 0.5) / w
    V = 1.0 - (v + 0.5) / h
    grain = tileable_noise(w, h, 11)
    fine = tileable_noise(w, h, 29)
    strap = U < 0.5
    edge = strap & ((U < 0.06) | (U > 0.44))
    stitch_u = (0.085, 0.415)
    pitch = 1.0 / 24.0
    dash = ((V % pitch) / pitch) < 0.55
    stitch = np.zeros((h, w), bool)
    crease = np.zeros((h, w))
    for su in stitch_u:
        du = np.abs(U - su) * w
        stitch |= strap & (du < 1.6) & dash
        crease += strap * np.exp(-((np.abs(U - (su + (0.025 if su < 0.25 else -0.025))) * w) ** 2) / 2.0)
    grip = (U >= 0.5) & (U < 0.75)
    lace = (U >= 0.75) & (V < 0.5)
    keeper = (U >= 0.75) & (V >= 0.5)
    wrap_lines = grip & ((((U - 0.5) * 4 * 24 + V * 6) % 1.0) < 0.12)
    braid = lace & ((((U - 0.75) * 4 * 10 + V * 40) % 1.0) < 0.25)
    keeper_edge = keeper & ((((V - 0.5) * 2) < 0.12) | (((V - 0.5) * 2) > 0.88))
    return dict(grain=grain, fine=fine, edge=edge, stitch=stitch, crease=np.clip(crease, 0, 1), grip=grip, lace=lace,
                keeper=keeper, wrap_lines=wrap_lines, braid=braid, keeper_edge=keeper_edge, strap=strap)


def leather_base(skin, layers):
    spec = g.LEATHER[skin]
    base = np.array(g.hex_rgb(spec["base"]))
    edge = np.array(g.hex_rgb(spec["edge"]))
    stitch = np.array(g.hex_rgb(spec["stitch"]))
    h, w = TEX_H, TEX_W
    img = np.broadcast_to(base, (h, w, 3)).copy()
    shade = 0.92 + 0.12 * layers["grain"] + 0.04 * (layers["fine"] - 0.5)
    img *= shade[..., None]
    img[layers["edge"]] = edge * (0.95 + 0.08 * layers["fine"][layers["edge"]][..., None])
    img *= (1.0 - 0.10 * layers["crease"])[..., None]
    img[layers["grip"]] *= 0.86
    img[layers["wrap_lines"]] *= 0.82
    img[layers["keeper_edge"]] = edge
    img[layers["lace"]] = stitch * (0.93 + 0.07 * layers["fine"][layers["lace"]][..., None])
    img[layers["braid"]] *= 0.88
    img[layers["stitch"]] = stitch
    return np.clip(img, 0, 1)


def leather_normal(layers, strength=2.2):
    height = 0.35 * layers["grain"] + 0.15 * layers["fine"]
    height -= 0.9 * layers["crease"]
    height -= 0.6 * layers["stitch"]
    height -= 0.5 * layers["wrap_lines"] + 0.4 * layers["braid"]
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, 1, axis=0) - np.roll(height, -1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, -dy * strength, np.ones_like(height)
    n = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / n * 0.5 + 0.5, ny / n * 0.5 + 0.5, nz / n * 0.5 + 0.5], axis=-1)


# ---------------------------------------------------------------- scene helpers

def link(obj, collection=None):
    (collection or bpy.context.scene.collection).objects.link(obj)
    return obj


def mesh_object(name, verts, faces, uvs=None, mat_index=0, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    if uvs is not None:
        by_vertex = [dict(zip(f, uv)) for f, uv in zip(faces, uvs)]
        layer = me.uv_layers.new(name="UVMap")
        for poly in me.polygons:
            for li, vi in zip(poly.loop_indices, poly.vertices):
                layer.data[li].uv = by_vertex[poly.index][vi]
    for poly in me.polygons:
        poly.material_index = mat_index
        poly.use_smooth = smooth
    obj = bpy.data.objects.new(name, me)
    obj["belt_material"] = mat_index
    return obj


def sweep(name, path, section, uv_of, closed_section=True, cap_start=False, cap_end=False, mat_index=0):
    """Sweep a section [(sx, sy, tag)] along a 3D path of frames [(origin, x_axis, normal)]."""
    verts, faces, uvs = [], [], []
    n = len(section)
    for ring, (origin, xa, na) in enumerate(path):
        for sx, sy, _ in section:
            verts.append(tuple(origin + xa * sx + na * sy))
    span = n if closed_section else n - 1
    for r in range(len(path) - 1):
        for i in range(span):
            j = (i + 1) % n
            a, b, c, d = r * n + i, r * n + j, (r + 1) * n + j, (r + 1) * n + i
            faces.append((a, b, c, d))
            uvs.append((uv_of(r, i), uv_of(r, j), uv_of(r + 1, j), uv_of(r + 1, i)))
    if cap_start:
        faces.append(tuple(reversed(range(n))))
        uvs.append(tuple(uv_of(0, i) for i in reversed(range(n))))
    if cap_end:
        base = (len(path) - 1) * n
        faces.append(tuple(base + i for i in range(n)))
        uvs.append(tuple(uv_of(len(path) - 1, i) for i in range(n)))
    return mesh_object(name, verts, faces, uvs, mat_index)


def lathe(name, profile, sectors, mat_index, uv=(0.0, 0.0), z_offset=0.0, cap_bottom=True):
    """Revolve [(radius, z)] about +Z."""
    verts, faces, uvs = [], [], []
    for k in range(sectors):
        a = 2 * math.pi * k / sectors
        for r, z in profile:
            verts.append((r * math.cos(a), r * math.sin(a), z + z_offset))
    m = len(profile)
    for k in range(sectors):
        k2 = (k + 1) % sectors
        for i in range(m - 1):
            faces.append((k * m + i, k2 * m + i, k2 * m + i + 1, k * m + i + 1))
            uvs.append((uv, uv, uv, uv))
    if cap_bottom:
        faces.append(tuple(k * m for k in reversed(range(sectors))))
        uvs.append(tuple(uv for _ in range(sectors)))
    return mesh_object(name, verts, faces, uvs, mat_index)


def torus(name, centre, major, minor, seg_major, seg_minor, mat_index, axis="Z", uv=(0.0, 0.0)):
    verts, faces = [], []
    for i in range(seg_major):
        a = 2 * math.pi * i / seg_major
        for j in range(seg_minor):
            b = 2 * math.pi * j / seg_minor
            r = major + minor * math.cos(b)
            p = Vector((r * math.cos(a), r * math.sin(a), minor * math.sin(b)))
            if axis == "Y":
                p = Vector((p.x, p.z, p.y))
            verts.append(tuple(Vector(centre) + p))
    for i in range(seg_major):
        for j in range(seg_minor):
            i2, j2 = (i + 1) % seg_major, (j + 1) % seg_minor
            faces.append((i * seg_minor + j, i2 * seg_minor + j, i2 * seg_minor + j2, i * seg_minor + j2))
    return mesh_object(name, verts, faces, [(uv,) * 4 for _ in faces], mat_index)


def tube_along(name, points, radii, sides, mat_index, closed=False, uv=(0.0, 0.0)):
    """Round tube through 3D points; radii per point. Uses parallel-transport frames."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    frames = []
    prev_x = None
    for i in range(n):
        if closed:
            t = (pts[(i + 1) % n] - pts[(i - 1) % n]).normalized()
        else:
            t = (pts[min(n - 1, i + 1)] - pts[max(0, i - 1)]).normalized()
        if prev_x is None:
            ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
            xa = t.cross(ref).normalized()
        else:
            xa = (prev_x - t * prev_x.dot(t)).normalized()
        ya = t.cross(xa).normalized()
        frames.append((xa, ya))
        prev_x = xa
    verts, faces = [], []
    for i, (xa, ya) in enumerate(frames):
        for k in range(sides):
            a = 2 * math.pi * k / sides
            verts.append(tuple(pts[i] + (xa * math.cos(a) + ya * math.sin(a)) * radii[i]))
    rings = n if closed else n - 1
    for i in range(rings):
        i2 = (i + 1) % n
        for k in range(sides):
            k2 = (k + 1) % sides
            faces.append((i * sides + k, i * sides + k2, i2 * sides + k2, i2 * sides + k))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((n - 1) * sides + k for k in range(sides)))
    return mesh_object(name, verts, faces, [(uv,) * len(f) for f in faces], mat_index)


# ---------------------------------------------------------------- parts

def strap_part(name, with_holes=True):
    section = g.rounded_rect(g.STRAP_W, g.STRAP_T, g.STRAP_R)
    path2 = g.strap_path()
    lengths = g.arc_lengths(path2)
    frames = []
    for (y, z), (ty, tz), (ny, nz) in g.path_frames(path2):
        frames.append((Vector((0, y, z)), Vector((1, 0, 0)), Vector((0, ny, nz)) * -1))
    s0 = g.STRAP_Z0

    def uv_of(r, i):
        x, _, a = section[i]
        return (g.section_u(x, a), (s0 + lengths[r]) / 0.150)

    obj = sweep(name, frames, section, uv_of, cap_end=True, mat_index=0)
    if with_holes:
        cutters = []
        for k, hz in enumerate(g.HOLES_Z):
            c = tube_along(f"_hole{k}", [(0, -0.05, hz), (0, 0.05, hz)], [g.HOLE_D / 2] * 2, 12, 0)
            link(c)
            cutters.append(c)
        link(obj)
        for c in cutters:
            mod = obj.modifiers.new(c.name, "BOOLEAN")
            mod.operation = "DIFFERENCE"
            mod.object = c
            try:
                mod.solver = "EXACT"
            except TypeError:
                pass
        dg = bpy.context.evaluated_depsgraph_get()
        applied = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
        old = obj.data
        obj.modifiers.clear()
        obj.data = applied
        bpy.data.meshes.remove(old)
        for c in cutters:
            bpy.data.objects.remove(c)
        bpy.context.scene.collection.objects.unlink(obj)
        applied.name = name
    return obj


def transition_part():
    rect = g.rounded_rect(g.STRAP_W, g.STRAP_T, g.STRAP_R)
    z0, z1 = g.GRIP_Z1, g.STRAP_Z0
    path, rings = [], 4
    for k in range(rings):
        t = k / (rings - 1)
        path.append(t)

    section_rings = []
    for t in path:
        s = g.smooth(t)
        ring = []
        for x, y, a in rect:
            ang = math.atan2(y, x)
            cx, cy = g.GRIP_R * math.cos(ang), g.GRIP_R * math.sin(ang)
            ring.append((cx + (x - cx) * s, cy + (y - cy) * s, a))
        section_rings.append(ring)
    verts, faces, uvs = [], [], []
    n = len(rect)
    for k, ring in enumerate(section_rings):
        z = z0 + (z1 - z0) * path[k]
        for x, y, _ in ring:
            verts.append((x, y, z))
    for k in range(rings - 1):
        for i in range(n):
            j = (i + 1) % n
            faces.append((k * n + i, k * n + j, (k + 1) * n + j, (k + 1) * n + i))
            def uv(kk, ii):
                x, _, a = rect[ii]
                return (g.section_u(x, a), (z0 + (z1 - z0) * path[kk]) / 0.150)
            uvs.append((uv(k, i), uv(k, j), uv(k + 1, j), uv(k + 1, i)))
    return mesh_object("BELT_Transition", verts, faces, uvs, 0)


def grip_roll():
    sectors, rings = 24, 12
    verts, faces, uvs = [], [], []
    for r in range(rings):
        z = g.GRIP_Z0 + (g.GRIP_Z1 - g.GRIP_Z0) * r / (rings - 1)
        for k in range(sectors + 1):
            a = 2 * math.pi * k / sectors
            verts.append((g.GRIP_R * math.cos(a), g.GRIP_R * math.sin(a), z))
    w = sectors + 1
    for r in range(rings - 1):
        for k in range(sectors):
            faces.append((r * w + k, r * w + k + 1, (r + 1) * w + k + 1, (r + 1) * w + k))
            def uv(rr, kk):
                return (0.5 + 0.25 * kk / sectors, 0.02 + 0.96 * rr / (rings - 1))
            uvs.append((uv(r, k), uv(r, k + 1), uv(r + 1, k + 1), uv(r + 1, k)))
    obj = mesh_object("BELT_Grip_roll", verts, faces, uvs, 0)
    return obj


def lace_wrap():
    turns, per_turn = 4, 16
    z0, z1 = g.GRIP_Z0 + 0.025, g.GRIP_Z1 - 0.02
    pts, n = [], turns * per_turn
    for i in range(n + 1):
        t = i / n
        a = 2 * math.pi * turns * t
        r = g.GRIP_R + 0.0025
        pts.append((r * math.cos(a), r * math.sin(a), z0 + (z1 - z0) * t))
    obj = tube_along("BELT_Grip_wrap", pts, [0.0032] * len(pts), 4, 0, uv=(0.86, 0.25))
    return obj


def keeper(name, z0, z1):
    inner_w, inner_t = g.STRAP_W + 0.002, g.STRAP_T + 0.002
    band = 0.005
    ring_in = g.rounded_rect(inner_w, inner_t, 0.006)
    ring_out = g.rounded_rect(inner_w + 2 * band, inner_t + 2 * band, 0.006 + band)
    verts, faces, uvs = [], [], []
    n = len(ring_in)
    layers = [(ring_out, z0), (ring_out, z1), (ring_in, z1), (ring_in, z0)]
    for ring, z in layers:
        for x, y, _ in ring:
            verts.append((x, y, z))
    for L in range(4):
        L2 = (L + 1) % 4
        for i in range(n):
            j = (i + 1) % n
            faces.append((L * n + i, L * n + j, L2 * n + j, L2 * n + i))
            vv = (0.5 + 0.5 * L / 4, 0.5 + 0.5 * L2 / 4)
            uvs.append(((0.76 + 0.22 * i / n, vv[0]), (0.76 + 0.22 * (i + 1) / n, vv[0]),
                        (0.76 + 0.22 * (i + 1) / n, vv[1]), (0.76 + 0.22 * i / n, vv[1])))
    return mesh_object(name, verts, faces, uvs, 0)


def end_cap():
    profile = [(0.024, 0.0), (0.032, 0.003), (0.0355, 0.010), (0.036, 0.020), (0.036, 0.034), (0.033, 0.040), (0.0305, 0.040)]
    return lathe("BELT_End_cap", profile, 24, 1, cap_bottom=True)


def collar():
    return torus("BELT_Collar", (0, 0, g.GRIP_Z1), g.GRIP_R + 0.0015, 0.0045, 20, 5, 1)


def buckle_frame():
    cz = (g.FRAME_Z0 + g.FRAME_Z1) / 2
    hw = g.FRAME_W / 2 - g.FRAME_TUBE
    hh = (g.FRAME_Z1 - g.FRAME_Z0) / 2 - g.FRAME_TUBE
    r = 0.012
    pts = []
    for ox, oz, a0 in ((hw - r, hh - r, 0), (-(hw - r), hh - r, 90), (-(hw - r), -(hh - r), 180), (hw - r, -(hh - r), 270)):
        for k in range(7):
            a = math.radians(a0 + 90 * k / 6)
            pts.append((ox + r * math.cos(a), 0.0, cz + oz + r * math.sin(a)))
    return tube_along("BELT_Buckle_frame", pts, [g.FRAME_TUBE] * len(pts), 8, 1, closed=True)


def buckle_prong():
    z0 = g.BAR_Z
    z1 = g.FRAME_Z1 - g.FRAME_TUBE
    pts, radii = [], []
    for i in range(7):
        t = i / 6
        y = -(g.FRAME_TUBE + 0.002) * math.sin(math.pi * min(1.0, t * 1.15)) - 0.004 * t
        pts.append((0.0, y, z0 + (z1 - z0 + 0.004) * t))
        radii.append(0.0055 - 0.0025 * t)
    return tube_along("BELT_Buckle_prong", pts, radii, 6, 1)


def rivets():
    objs = []
    z = 1.080
    y = -(g.FOLD_R * g.smooth((z - g.SHIFT_Z0) / (g.SHIFT_Z1 - g.SHIFT_Z0))) - g.STRAP_T / 2
    for k, x in enumerate((-0.018, 0.018)):
        prof = [(0.0012, 0.0034), (0.0035, 0.0028), (0.0055, 0.0012), (0.0062, 0.0)]
        o = lathe(f"BELT_Rivet_{k}", prof, 10, 1, cap_bottom=True)
        o.data.transform(Matrix.Rotation(math.radians(90), 4, "X"))
        o.data.transform(Matrix.Translation((x, y + 0.0004, z)))
        objs.append(o)
    return objs


# ---------------------------------------------------------------- materials

def make_materials(tex_dir):
    mats = {}
    for skin, spec in g.LEATHER.items():
        m = bpy.data.materials.new(f"leather_{skin}")
        try:
            m.use_nodes = True
        except Exception:
            pass
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        img = bpy.data.images.load(os.path.join(tex_dir, f"leather_{skin}_BaseColor.png"))
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        nimg = bpy.data.images.load(os.path.join(tex_dir, "leather_NormalGL.png"))
        nimg.colorspace_settings.name = "Non-Color"
        ntex = nt.nodes.new("ShaderNodeTexImage"); ntex.image = nimg
        nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.inputs["Strength"].default_value = 0.8
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
        bsdf.inputs["Roughness"].default_value = spec["roughness"]
        bsdf.inputs["Metallic"].default_value = 0.0
        m.diffuse_color = (*[c ** 2.2 for c in g.hex_rgb(spec["base"])], 1)
        m.use_fake_user = True
        mats[skin] = m
    gold = bpy.data.materials.new("gold_Classic")
    try:
        gold.use_nodes = True
    except Exception:
        pass
    b = gold.node_tree.nodes.get("Principled BSDF")
    lin = [c ** 2.2 for c in g.hex_rgb(g.GOLD["base"])]
    b.inputs["Base Color"].default_value = (*lin, 1)
    b.inputs["Metallic"].default_value = g.GOLD["metallic"]
    b.inputs["Roughness"].default_value = g.GOLD["roughness"]
    gold.diffuse_color = (*lin, 1)
    mats["gold"] = gold
    return mats


# ---------------------------------------------------------------- rig

def build_rig(parent):
    arm = bpy.data.armatures.new("BELT_Rig")
    rig = link(bpy.data.objects.new("BELT_Rig", arm))
    rig.parent = parent
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    prev = None
    for name, z0, z1 in g.BONES:
        eb = arm.edit_bones.new(name)
        eb.head = (0, 0, z0)
        eb.tail = (0, 0, z1)
        eb.align_roll(Vector((0, -1, 0)))
        if prev:
            eb.parent = prev
            eb.use_connect = abs(prev.tail.z - z0) < 1e-9
        prev = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        if pb.name not in ("Belt_Root", "Belt_Tip"):
            c = pb.constraints.new("LIMIT_ROTATION")
            c.use_limit_x = c.use_limit_y = c.use_limit_z = True
            lim = math.radians(g.JOINT_LIMIT_DEG)
            c.min_x, c.max_x = -lim, lim
            c.min_y, c.max_y = math.radians(-10), math.radians(10)
            c.min_z, c.max_z = math.radians(-8), math.radians(8)
            c.owner_space = "LOCAL"
    rig.select_set(False)
    return rig


def skin_mesh(obj, rig, rigid=None):
    for name, _, _ in g.BONES:
        obj.vertex_groups.new(name=name)
    groups = {vg.name: vg for vg in obj.vertex_groups}
    for v in obj.data.vertices:
        w = {rigid: 1.0} if rigid else g.weights_at(v.co.z)
        for name, value in w.items():
            if value > 0:
                groups[name].add([v.index], value, "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    obj.parent = rig


def pose(rig, tip_degrees, tip_bone=0.0):
    angles = g.bend_angles(tip_degrees)
    for k, pb in enumerate(rig.pose.bones):
        if pb.name.startswith("Belt_0"):
            pb.rotation_euler = (math.radians(angles[int(pb.name[-1]) - 1]), 0, 0)
        elif pb.name == "Belt_Tip":
            pb.rotation_euler = (math.radians(tip_bone), 0, 0)
        else:
            pb.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- checks

def world_mesh(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    mats = [p.material_index for p in me.polygons]
    tris = sum(len(p) - 2 for p in polys)
    ev.to_mesh_clear()
    return verts, polys, mats, tris


def bounds(verts):
    lo = [min(v[i] for v in verts) for i in range(3)]
    hi = [max(v[i] for v in verts) for i in range(3)]
    return lo, hi


def strap_probe(rig, probe):
    rest_v, polys, _, _ = world_mesh(probe)
    rest_area = [area(rest_v, p) for p in polys]
    n = 16
    rings = len(rest_v) // n
    samples = []
    for tip in list(range(0, 151, 10)) + [-30, -60]:
        pose(rig, tip, tip_bone=min(20, abs(tip) * 0.15))
        v, _, _, _ = world_mesh(probe)
        thick = []
        for r in range(rings):
            back = (v[r * n + 3] + v[r * n + 4]) / 2
            front = (v[r * n + 11] + v[r * n + 12]) / 2
            thick.append((back - front).length / g.STRAP_T)
        stretch = max(area(v, p) / a for p, a in zip(polys, rest_area) if a > 1e-12)
        tree = BVHTree.FromPolygons(v, polys)
        pairs = [pq for pq in tree.overlap(tree) if not set(polys[pq[0]]) & set(polys[pq[1]])]
        samples.append({"tip_degrees": tip, "min_thickness_ratio": round(min(thick), 4),
                        "max_area_stretch": round(stretch, 4), "self_overlap_pairs": len(pairs)})
    pose(rig, 0)
    ok = all(s["min_thickness_ratio"] >= 0.85 and s["max_area_stretch"] <= 1.35 and s["self_overlap_pairs"] == 0 for s in samples)
    return {"status": "PASS" if ok else "FAIL", "thresholds": {"min_thickness_ratio": 0.85, "max_area_stretch": 1.35,
            "self_overlap_pairs": 0}, "samples": samples,
            "limits": ["Strap sweep without holes; rigid gold parts and keepers are not swept here.",
                       "Poses are the runtime's tip-weighted share plus a small buckle tilt; Unity playback is verified separately."]}


def area(v, p):
    a = 0.0
    for i in range(1, len(p) - 1):
        a += ((v[p[i]] - v[p[0]]).cross(v[p[i + 1]] - v[p[0]])).length * 0.5
    return a


# ---------------------------------------------------------------- stages

def stage_build(root, run_dir, run_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex_dir = os.path.join(run_dir, "Textures")
    os.makedirs(tex_dir)
    layers = leather_layers()
    for skin in g.LEATHER:
        write_png(os.path.join(tex_dir, f"leather_{skin}_BaseColor.png"), leather_base(skin, layers))
    write_png(os.path.join(tex_dir, "leather_NormalGL.png"), leather_normal(layers))
    mats = make_materials(tex_dir)

    model = link(bpy.data.objects.new("BELT", None))
    grip = link(bpy.data.objects.new("Grip_R", None))
    grip.empty_display_type = "ARROWS"
    grip.empty_display_size = 0.05
    grip.parent = model
    grip.location = (g.GRIP[0], -g.GRIP[2], g.GRIP[1])
    rig = build_rig(model)

    parts_col = bpy.data.collections.new("BELT_Editable_parts")
    bpy.context.scene.collection.children.link(parts_col)
    leather_parts = [strap_part("BELT_Strap"), transition_part(), grip_roll(), lace_wrap(),
                     keeper("BELT_Keeper_free", *g.KEEPERS[0]), keeper("BELT_Keeper_fixed", *g.KEEPERS[1])]
    gold_parts = [end_cap(), collar(), buckle_frame(), buckle_prong()] + rivets()
    rigid = {"BELT_Grip_roll": "Belt_Root", "BELT_Grip_wrap": "Belt_Root", "BELT_End_cap": "Belt_Root",
             "BELT_Collar": "Belt_Root", "BELT_Keeper_free": "Belt_06", "BELT_Keeper_fixed": "Belt_06",
             "BELT_Buckle_frame": "Belt_Tip", "BELT_Buckle_prong": "Belt_Tip", "BELT_Rivet_0": "Belt_Tip", "BELT_Rivet_1": "Belt_Tip"}
    geometry = {}
    for obj in leather_parts + gold_parts:
        link(obj, parts_col)
        obj.data.materials.append(mats["Classic"] if obj["belt_material"] == 0 else mats["gold"])
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        geometry[obj.name] = {"vertices": len(obj.data.vertices), "triangles": tris,
                              "material": "leather_Classic" if obj["belt_material"] == 0 else "gold_Classic"}

    bm = bmesh.new()
    for obj in leather_parts + gold_parts:
        tmp = obj.data.copy()
        for poly in tmp.polygons:
            poly.material_index = obj["belt_material"]
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    merged_me = bpy.data.meshes.new("BELT")
    bm.to_mesh(merged_me)
    bm.free()
    merged_me.materials.append(mats["Classic"])
    merged_me.materials.append(mats["gold"])
    merged = link(bpy.data.objects.new("BELT_Mesh", merged_me))

    owner = []
    for obj in leather_parts + gold_parts:
        owner += [rigid.get(obj.name)] * len(obj.data.vertices)
    for name, _, _ in g.BONES:
        merged.vertex_groups.new(name=name)
    groups = {vg.name: vg for vg in merged.vertex_groups}
    for v in merged_me.vertices:
        w = {owner[v.index]: 1.0} if owner[v.index] else g.weights_at(v.co.z)
        for name, value in w.items():
            groups[name].add([v.index], value, "REPLACE")
    mod = merged.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    merged.parent = rig
    for obj in leather_parts + gold_parts:
        obj.hide_render = True
        obj.hide_set(True)

    probe = strap_part("_probe_strap", with_holes=False)
    link(probe)
    skin_mesh(probe, rig)

    verts, polys, mat_ids, tris = world_mesh(merged)
    lo, hi = bounds(verts)
    size_blender = [hi[i] - lo[i] for i in range(3)]
    measured_size = [size_blender[0], size_blender[2], size_blender[1]]
    leather_tris = sum(len(p) - 2 for p, m in zip(polys, mat_ids) if m == 0)
    gold_tris = tris - leather_tris
    infl = max(sum(1 for gr in v.groups if gr.weight > 0) for v in merged_me.vertices)
    weight_err = max(abs(sum(gr.weight for gr in v.groups) - 1.0) for v in merged_me.vertices)
    checks = {
        "triangles_total": tris, "triangles_leather": leather_tris, "triangles_gold": gold_tris,
        "triangle_cap": TRI_CAP, "within_budget": tris <= TRI_CAP,
        "materials": [m.name for m in merged_me.materials],
        "bones": [b[0] for b in g.BONES], "max_influences": infl, "max_weight_sum_error": weight_err,
        "bounds_blender_min": lo, "bounds_blender_max": hi,
        "measured_size_unity_xyz": [round(s, 5) for s in measured_size],
        "catalogue_size_unity_xyz": list(g.SIZE),
        "size_error_metres": [round(abs(a - b), 6) for a, b in zip(measured_size, g.SIZE)],
        "floor_min_z": lo[2], "x_centre": (lo[0] + hi[0]) / 2,
        "grip_blender": list(grip.matrix_world.translation),
        "grip_catalogue_unity": list(g.GRIP),
        "grip_error_metres": (grip.matrix_world.translation - Vector((g.GRIP[0], -g.GRIP[2], g.GRIP[1]))).length,
        "held_length_metres": g.SIZE[1] * g.HELD_SCALE,
    }
    probe_result = strap_probe(rig, probe)
    bpy.data.objects.remove(probe)

    export_dir = os.path.join(run_dir, "Exports")
    os.makedirs(export_dir)
    for o in bpy.context.scene.objects:
        o.select_set(o.name in ("BELT", "Grip_R", "BELT_Rig", "BELT_Mesh"))
    fbx = os.path.join(export_dir, "BELT.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"EMPTY", "MESH", "ARMATURE"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=False,
                             use_custom_props=False, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                             armature_nodetype="NULL", use_armature_deform_only=True, bake_anim=False, path_mode="STRIP",
                             embed_textures=False)
    glb = os.path.join(export_dir, "BELT.glb")
    try:
        bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_yup=True,
                                  export_apply=False, export_animations=False, export_skins=True)
    except Exception as error:
        glb = None
        checks["glb_export_error"] = str(error)
    blend = os.path.join(run_dir, "BELT.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend, compress=True)

    reimport = reimport_fbx(fbx, tris)
    status = "PASS" if (checks["within_budget"] and infl <= 2 and weight_err < 1e-5 and checks["grip_error_metres"] < 1e-6
                        and max(checks["size_error_metres"]) <= 0.0015 and abs(lo[2]) < 1e-6 and abs(checks["x_centre"]) < 1e-6
                        and probe_result["status"] == "PASS" and reimport["status"] == "PASS") else "FAIL"
    exports = {os.path.relpath(p, run_dir).replace(os.sep, "/"): {"sha256": sha256(p), "bytes": os.path.getsize(p)}
               for p in [fbx, blend] + ([glb] if glb else []) + [os.path.join(tex_dir, f) for f in sorted(os.listdir(tex_dir))]}
    receipt = {
        "status": status, "run": run_name, "utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "blender": bpy.app.version_string, "script_sha256": sha256(os.path.join(HERE, "build_belt.py")),
        "helper_sha256": {"belt_geometry.py": sha256(os.path.join(HERE, "belt_geometry.py"))},
        "contract": {"size_unity_xyz": list(g.SIZE), "grip_unity_xyz": list(g.GRIP), "held_scale": g.HELD_SCALE,
                     "axes": "Blender +Z = Unity +Y (length); +X = width; front face -Y", "materials": ["leather_Classic", "gold_Classic"],
                     "skins": list(g.LEATHER), "triangle_cap": TRI_CAP},
        "parts": geometry, "checks": checks, "bend_clearance": probe_result, "export_reimport": reimport,
        "exports": exports, "source_preservation": "new run folder; earlier runs untouched",
        "licence": "Own work (procedural geometry and textures generated by this script); no third-party input.",
        "limits": ["Renders and Unity import/holding checks are separate stages and do not follow from this receipt.",
                   "Owner style/feel approval is not implied."],
    }
    write_json(os.path.join(run_dir, "build-receipt.json"), receipt)
    print(json.dumps({"status": status, "triangles": tris, "size": checks["measured_size_unity_xyz"],
                      "probe": probe_result["status"], "reimport": reimport["status"]}))
    return 0 if status == "PASS" else 1


def reimport_fbx(path, expected_tris):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, primary_bone_axis="Y", secondary_bone_axis="X")
    rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    grips = [o for o in bpy.context.scene.objects if o.name == "Grip_R"]
    bones = [b.name for b in rigs[0].data.bones] if rigs else []
    tris = sum(len(p.vertices) - 2 for m in meshes for p in m.data.polygons)
    v, _, _, _ = world_mesh(meshes[0]) if meshes else ([], [], [], 0)
    lo, hi = bounds(v) if v else ([0] * 3, [0] * 3)
    grip = list(grips[0].matrix_world.translation) if grips else None
    expected_grip = (g.GRIP[0], -g.GRIP[2], g.GRIP[1])
    grip_err = (Vector(grip) - Vector(expected_grip)).length if grip else None
    ok = (len(rigs) == 1 and len(meshes) == 1 and len(grips) == 1 and bones == [b[0] for b in g.BONES] and tris == expected_tris
          and grip_err is not None and grip_err < 1e-5)
    return {"status": "PASS" if ok else "FAIL", "armatures": len(rigs), "meshes": len(meshes), "grip_markers": len(grips),
            "bones": bones, "triangles": tris, "bounds_min": lo, "bounds_max": hi, "grip": grip, "grip_error_metres": grip_err,
            "limits": ["Reimported in the same Blender process after a factory reset; Unity import is checked in Unity."]}


# ---------------------------------------------------------------- render

def setup_render(size, samples, threads, transparent=False):
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    try:
        scene.eevee.taa_render_samples = samples
    except Exception:
        pass
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.threads_mode = "FIXED"
    scene.render.threads = threads
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    world = bpy.data.worlds.new("Studio") if not scene.world else scene.world
    scene.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.80, 0.80, 0.78, 1)
    bg.inputs["Strength"].default_value = 0.9
    for name, energy, size_m, loc in (("Key", 160, 1.6, (-1.3, -1.8, 2.2)), ("Fill", 60, 2.2, (1.9, -1.0, 1.0)),
                                      ("Rim", 90, 1.2, (0.7, 1.9, 1.9))):
        light = link(bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA")))
        light.data.energy = energy
        light.data.size = size_m
        light.location = loc
        light.rotation_euler = (Vector((0, 0, 0.6)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def camera(location, target, ortho=None, lens=60):
    cam_data = bpy.data.cameras.new("Cam")
    if ortho:
        cam_data.type = "ORTHO"; cam_data.ortho_scale = ortho
    else:
        cam_data.lens = lens
    cam = link(bpy.data.objects.new("Cam", cam_data))
    cam.location = location
    direction = Vector(target) - Vector(location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    return cam


def render_to(path):
    if os.path.exists(path):
        raise FileExistsError(path)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_rgba(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    bpy.data.images.remove(img)
    return a


def stage_render(root, run_dir, size, samples, threads):
    blend = os.path.join(run_dir, "BELT.blend")
    receipt = json.load(open(os.path.join(run_dir, "build-receipt.json"), encoding="utf-8"))
    before = sha256(blend)
    if before != receipt["exports"]["BELT.blend"]["sha256"]:
        raise RuntimeError("BELT.blend differs from its build receipt")
    bpy.ops.wm.open_mainfile(filepath=blend)
    previews = os.path.join(run_dir, "Previews")
    os.makedirs(previews)
    setup_render(size, samples, threads)
    rig = bpy.data.objects["BELT_Rig"]
    out = {}
    views = {
        "front": ((0, -3, 0.6), (0, 0, 0.6), 1.35),
        "side": ((3, 0, 0.6), (0, 0, 0.6), 1.35),
        "three-quarter": ((1.4, -1.9, 1.1), (0, 0, 0.62), None),
        "buckle": ((0.22, -0.42, 1.24), (0, 0, 1.135), 0.24),
        "grip": ((0.25, -0.5, 0.25), (0, 0, 0.16), 0.42),
        "holes": ((0.05, -0.6, 0.58), (0, 0, 0.56), 0.32),
    }
    for name, (loc, target, ortho) in views.items():
        cam = camera(loc, target, ortho=ortho, lens=55)
        path = os.path.join(previews, f"{name}.png")
        render_to(path)
        bpy.data.objects.remove(cam)
        out[name] = {"sha256": sha256(path), "camera": list(loc), "target": list(target), "ortho": ortho}

    strip = []
    poses = [("rest", 0, 0), ("held-idle", 24, 2), ("windup", 48, 6), ("contact", 6, 0), ("overshoot", -10, -4), ("settle", -2, 0)]
    for name, tip, tip_bone in poses:
        pose(rig, tip, tip_bone)
        cam = camera((3, 0, 0.62), (0, 0, 0.62), ortho=1.45)
        path = os.path.join(previews, f"pose-{name}.png")
        render_to(path)
        bpy.data.objects.remove(cam)
        strip.append(load_rgba(path))
        out[f"pose-{name}"] = {"sha256": sha256(path), "tip_degrees": tip, "buckle_degrees": tip_bone}
    pose(rig, 0)
    sheet = np.concatenate([s[:, :, :3] for s in strip], axis=1)
    write_png(os.path.join(previews, "pose-strip.png"), sheet)
    out["pose-strip"] = {"sha256": sha256(os.path.join(previews, "pose-strip.png")), "order": [p[0] for p in poses]}

    merged = bpy.data.objects["BELT_Mesh"]
    skins = []
    for skin_name in g.LEATHER:
        merged.data.materials[0] = bpy.data.materials[f"leather_{skin_name}"]
        cam = camera((0.9, -2.6, 0.75), (0, 0, 0.62), ortho=1.4)
        path = os.path.join(previews, f"skin-{skin_name}.png")
        render_to(path)
        bpy.data.objects.remove(cam)
        skins.append(load_rgba(path))
        out[f"skin-{skin_name}"] = {"sha256": sha256(path)}
    merged.data.materials[0] = bpy.data.materials["leather_Classic"]
    write_png(os.path.join(previews, "skin-strip.png"), np.concatenate([s[:, :, :3] for s in skins], axis=1))
    out["skin-strip"] = {"sha256": sha256(os.path.join(previews, "skin-strip.png")), "order": list(g.LEATHER)}

    bat = os.path.join(root, "Web/public/art/items/BAT.glb")
    if os.path.exists(bat):
        before_objs = set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=bat)
        imported = [o for o in bpy.context.scene.objects if o not in before_objs]
        for o in imported:
            if o.parent is None:
                o.location.x += 0.32
        cam = camera((0.16, -3.2, 0.6), (0.16, 0, 0.6), ortho=1.45)
        path = os.path.join(previews, "compare-original-BAT.png")
        render_to(path)
        bpy.data.objects.remove(cam)
        out["compare-original-BAT"] = {"sha256": sha256(path), "reference": "Web/public/art/items/BAT.glb (original, unmodified)",
                                       "reference_sha256": sha256(bat)}
        for o in imported:
            bpy.data.objects.remove(o)

    icons = os.path.join(run_dir, "Icons")
    os.makedirs(icons)
    setup_icon = bpy.context.scene
    setup_icon.render.film_transparent = True
    pose(rig, 40, 8)
    cam = camera((1.25, -1.55, 1.05), (0, 0, 0.6), lens=50)
    master = os.path.join(icons, "BELT-512.png")
    bpy.context.scene.render.resolution_x = bpy.context.scene.render.resolution_y = 512
    render_to(master)
    bpy.data.objects.remove(cam)
    pose(rig, 0)
    rgba = load_rgba(master)
    write_png(os.path.join(icons, "BELT-192.png"), downsample(rgba, 192))
    out["icon"] = {"master": sha256(master), "icon_192": sha256(os.path.join(icons, "BELT-192.png"))}

    after = sha256(blend)
    write_json(os.path.join(previews, "render-receipt.json"), {
        "status": "PASS" if after == before else "FAIL", "blend_sha256_before": before, "blend_sha256_after": after,
        "size": size, "samples": samples, "threads": threads, "engine": bpy.context.scene.render.engine,
        "utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "images": out,
        "limits": ["Images must be opened and inspected; this receipt only proves which files were rendered."]})
    print(json.dumps({"rendered": sorted(out)}))
    return 0


def downsample(rgba, size):
    h, w, _ = rgba.shape
    ys = (np.arange(size + 1) * h / size).astype(int)
    xs = (np.arange(size + 1) * w / size).astype(int)
    out = np.zeros((size, size, 4), np.float32)
    for i in range(size):
        for j in range(size):
            block = rgba[ys[i]:ys[i + 1], xs[j]:xs[j + 1]]
            a = block[..., 3:4]
            wsum = a.sum()
            out[i, j, :3] = (block[..., :3] * a).sum(axis=(0, 1)) / wsum if wsum > 0 else 0
            out[i, j, 3] = a.mean()
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--project-root", required=True)
    p.add_argument("--stage", choices=("build", "render"), required=True)
    p.add_argument("--run-name", required=True)
    p.add_argument("--size", type=int, default=640)
    p.add_argument("--samples", type=int, default=16)
    p.add_argument("--threads", type=int, default=2)
    a = p.parse_args(argv)
    root = os.path.abspath(a.project_root)
    if not os.path.isfile(os.path.join(root, "ProjectSettings", "ProjectVersion.txt")):
        raise SystemExit("project root must contain ProjectSettings/ProjectVersion.txt")
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,39}", a.run_name):
        raise SystemExit("run name must match ^[a-z0-9][a-z0-9-]{0,39}$")
    if not (320 <= a.size <= 960 and 8 <= a.samples <= 64 and 1 <= a.threads <= 4):
        raise SystemExit("size 320-960, samples 8-64, threads 1-4")
    family = os.path.join(root, *FAMILY.split("/"))
    run_dir = os.path.join(family, a.run_name)
    if a.stage == "build":
        if os.path.exists(run_dir):
            raise SystemExit(f"{run_dir} exists; choose a new run name")
        os.makedirs(run_dir)
        shutil.copytree(HERE, os.path.join(run_dir, "Source"), ignore=shutil.ignore_patterns("__pycache__"))
        sys.exit(stage_build(root, run_dir, a.run_name))
    sys.exit(stage_render(root, run_dir, a.size, a.samples, a.threads))


if __name__ == "__main__":
    main()
