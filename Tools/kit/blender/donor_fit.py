"""EXPERIMENTAL (U-23): fit a donor skeleton to an unrigged mesh by cross-section landmarks.

Run: node Tools/kit/blender/bl_run.mjs donor_fit.py -- --experimental --in <mesh> --donor <rigged file> --out <file>
       [--mode auto|humanoid|bbox] [--samples 40000] [--seed 7]
Refuses without --experimental: automatic donor fitting is not verified yet (ARCHITECTURE U-23: bake-off on three
meshes pending; fallbacks are Make-It-Animatable, paid rigging, or a hand fit of about 2 h). Every output must go
through proxy_weights.py, then inspect_rig.py, and a reviewer must look at the joints (the result lists them).

Method (research_blender_rigging.md 4.2). The target stands on Z = 0 facing -Y in T or A pose, arms clear of the
body, legs apart:
  - sample the surface; slice it along Z;
  - crotch = top of the run of slices, from the feet up, with an empty band at the body's centre line;
  - shoulder = middle of the run of slices where the centre cluster is much wider than the waist (arms join);
  - neck = narrowest centre slice between the shoulders and the top; arm axis = main axis of each arm's points;
  - donor joints map piecewise-linearly between sole, crotch, shoulder and top; spine and head joints snap to the
    centre-slice centroid, leg joints to their leg's centroid, arm joints along the arm axis to the arm's slab
    centroid; other bones (fingers, toes, props) follow their parent at the height ratio.
humanoid mode needs Mixamo-style names (Hips, Spine, Neck, Head, LeftArm, LeftUpLeg...); other donors (Mesh2Motion
creatures) use bbox mode: the donor is scaled per axis onto the target's box, which is only a starting point.
The result reports inside_fraction (joints inside the mesh) and the landmarks; ok needs inside_fraction >= 0.75.
"""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import blkit  # noqa: E402
import proxy_weights  # noqa: E402
import riglogic  # noqa: E402

BINS = 160
MIN_INSIDE = 0.75


def add_args(p):
    p.add_argument("--experimental", action="store_true", help="required: this route is not verified (U-23)")
    p.add_argument("--in", dest="inp", required=True, help="unrigged target mesh")
    p.add_argument("--donor", required=True, help="file with the donor armature (its meshes are ignored)")
    p.add_argument("--out", required=True, help=".blend (recommended), .fbx or .glb")
    p.add_argument("--mode", choices=["auto", "humanoid", "bbox"], default="auto")
    p.add_argument("--samples", type=int, default=40000)
    p.add_argument("--seed", type=int, default=7)
    blkit.add_in_place_arg(p)


# --------------------------------------------------------------------------------------------------- geometry

def world_triangles(objs):
    tris = []
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        n = len(me.vertices)
        co = np.empty(n * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        m = np.array(o.matrix_world, dtype=np.float64)
        co = co.reshape(n, 3).astype(np.float64) @ m[:3, :3].T + m[:3, 3]
        lt = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
        me.loop_triangles.foreach_get("vertices", lt)
        tris.append(co[lt.reshape(-1, 3)])
    return np.concatenate(tris) if tris else np.zeros((0, 3, 3))


def sample_surface(tris, n, seed):
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    if area.sum() <= 0:
        raise blkit.Refused("no_area", "the target mesh has no surface area")
    rng = np.random.default_rng(seed)
    idx = rng.choice(len(tris), size=n, p=area / area.sum())
    r1, r2 = np.sqrt(rng.random(n)), rng.random(n)
    return (1 - r1)[:, None] * a[idx] + (r1 * (1 - r2))[:, None] * b[idx] + (r1 * r2)[:, None] * c[idx]


def clusters_1d(xs, gap):
    return riglogic.split_clusters(list(map(float, xs)), gap)


def centre_cluster(xs, x0, gap):
    """(lo, hi) of the 1D cluster holding x0, or None when x0 sits in an empty band."""
    for lo, hi, _ in clusters_1d(xs, gap):
        if lo - gap * 0.5 <= x0 <= hi + gap * 0.5:
            return lo, hi
    return None


def landmarks(pts):
    z = pts[:, 2]
    zmin, zmax = float(z.min()), float(z.max())
    h = zmax - zmin
    x0 = float(np.median(pts[:, 0]))
    gap = 0.012 * h
    edges = np.linspace(zmin, zmax, BINS + 1)
    idx = np.clip(np.digitize(z, edges) - 1, 0, BINS - 1)
    bins = [pts[idx == i] for i in range(BINS)]
    mid = (edges[:-1] + edges[1:]) / 2
    split = [len(b) > 0 and centre_cluster(b[:, 0], x0, gap) is None for b in bins]
    crotch_i = None
    i = 0
    while i < BINS and not split[i] and mid[i] < zmin + 0.15 * h:
        i += 1
    while i < BINS and split[i] and mid[i] < zmin + 0.7 * h:
        crotch_i = i
        i += 1
    if crotch_i is None:
        raise blkit.Refused("no_leg_split", "no leg split found: the legs must stand apart (T or A pose)")
    crotch = float(edges[crotch_i + 1])
    half = []
    for b in bins:
        cc = centre_cluster(b[:, 0], x0, gap) if len(b) else None
        half.append(None if cc is None else max(abs(cc[0] - x0), abs(cc[1] - x0)))
    upper = h - (crotch - zmin)
    waist = [half[i] for i in range(BINS) if half[i] is not None and crotch + 0.1 * upper <= mid[i] <= crotch + 0.3 * upper]
    if not waist:
        raise blkit.Refused("no_torso", "no torso slices above the crotch")
    wt = float(np.median(waist))
    wide = [i for i in range(BINS) if half[i] is not None and mid[i] > crotch + 0.3 * upper and half[i] > 1.6 * wt]
    if not wide:
        raise blkit.Refused("no_arms", "no arm junction found: the arms must stand clear of the body")
    run = [wide[-1]]
    for i in reversed(wide[:-1]):
        if i == run[-1] - 1:
            run.append(i)
        else:
            break
    shoulder = float((mid[run[-1]] + mid[run[0]]) / 2)
    necks = [(half[i], i) for i in range(BINS) if half[i] is not None and shoulder + 0.02 * h < mid[i] < zmax - 0.03 * h]
    neck = float(mid[min(necks)[1]]) if necks else shoulder + 0.4 * (zmax - shoulder)
    return {"sole": zmin, "top": zmax, "height": h, "x0": x0, "crotch": crotch, "shoulder": shoulder, "neck": neck,
            "torso_half_width": wt, "gap": gap}


def slice_centroid(pts, z, band, xsel=None):
    m = np.abs(pts[:, 2] - z) < band
    if xsel is not None:
        m &= xsel(pts[:, 0])
    sel = pts[m]
    return None if len(sel) == 0 else sel.mean(axis=0)


def leg_centroid(pts, z, band, lm, side):
    m = np.abs(pts[:, 2] - z) < band
    sel = pts[m]
    if len(sel) == 0:
        return None
    cls = clusters_1d(sel[:, 0], lm["gap"])
    want = [c for c in cls if (c[0] + c[1]) / 2 > lm["x0"]] if side > 0 else [c for c in cls if (c[0] + c[1]) / 2 < lm["x0"]]
    if not want:
        return None
    lo, hi, _ = min(want, key=lambda c: abs((c[0] + c[1]) / 2 - lm["x0"]))
    pick = sel[(sel[:, 0] >= lo - 1e-9) & (sel[:, 0] <= hi + 1e-9)]
    return pick.mean(axis=0)


def arm_axis(pts, lm, side):
    """Root point, tip point and unit axis of one arm from its points (PCA)."""
    rel = (pts[:, 0] - lm["x0"]) * side
    m = (rel > lm["torso_half_width"] * 1.25) & (pts[:, 2] > lm["crotch"] + 0.25 * (lm["shoulder"] - lm["crotch"]))
    arm = pts[m]
    if len(arm) < 20:
        raise blkit.Refused("no_arm_points", "too few points on the %s arm" % ("left" if side > 0 else "right"))
    c = arm.mean(axis=0)
    _, _, vt = np.linalg.svd(arm - c, full_matrices=False)
    axis = vt[0] * (1 if vt[0][0] * side > 0 else -1)
    t = (arm - c) @ axis
    band = max(0.02 * lm["height"], (t.max() - t.min()) * 0.03)
    root = arm[t < t.min() + band].mean(axis=0)
    tip = arm[t > t.max() - band].mean(axis=0)
    excess = (root[0] - lm["x0"]) * side - lm["torso_half_width"]
    if excess > 0:  # the selection starts outside the torso; the shoulder joint sits at the torso edge
        root = root - axis * (excess / max(abs(axis[0]), 0.2))
    return root, tip, axis, arm


def arm_slab_centroid(arm, point, axis, band):
    t = (arm - point) @ axis
    sel = arm[np.abs(t) < band]
    return point if len(sel) == 0 else sel.mean(axis=0)


# --------------------------------------------------------------------------------------------------- donor

def piecewise(pairs):
    pairs = sorted(pairs)
    xs = [p[0] for p in pairs]
    ys = [p[1] for p in pairs]

    def f(v):
        if v <= xs[0]:
            return ys[0] + (v - xs[0]) * ((ys[1] - ys[0]) / max(xs[1] - xs[0], 1e-9))
        if v >= xs[-1]:
            return ys[-1] + (v - xs[-1]) * ((ys[-1] - ys[-2]) / max(xs[-1] - xs[-2], 1e-9))
        for i in range(len(xs) - 1):
            if xs[i] <= v <= xs[i + 1]:
                t = (v - xs[i]) / max(xs[i + 1] - xs[i], 1e-9)
                return ys[i] + t * (ys[i + 1] - ys[i])
        return v

    return f


def donor_table(arm):
    mw = arm.matrix_world
    return {b.name: {"head": np.array(mw @ b.head_local), "tail": np.array(mw @ b.tail_local),
                     "parent": b.parent.name if b.parent else None,
                     "children": [c.name for c in b.children]} for b in arm.data.bones}


def order_bones(table):
    out, seen = [], set()

    def visit(n):
        if n in seen:
            return
        p = table[n]["parent"]
        if p is not None:
            visit(p)
        seen.add(n)
        out.append(n)

    for n in table:
        visit(n)
    return out


ROLE_NAMES = {
    "hips": ("Hips", "pelvis"), "spine": ("Spine",), "spine1": ("Spine1",), "spine2": ("Spine2",),
    "neck": ("Neck",), "head": ("Head",),
}


def roles(arm):
    r = {}
    for k, names in ROLE_NAMES.items():
        b = blkit.find_bone(arm, *names)
        if b is not None:
            r[b.name] = k
    for side, s in (("Left", 1), ("Right", -1)):
        for part in ("Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"):
            b = blkit.find_bone(arm, side + part)
            if b is not None:
                r[b.name] = (part.lower(), s)
    return r


def fit_humanoid(arm, table, pts, lm, donor_lm=None):
    rl = roles(arm)
    need = {"hips", "neck", "head"}
    have = {v for v in rl.values() if isinstance(v, str)}
    arms_ok = all(any(v == (p, s) for v in rl.values()) for p in ("arm", "forearm", "hand", "upleg", "leg", "foot") for s in (1, -1))
    if not need <= have or not arms_ok:
        raise blkit.Refused("not_humanoid_donor", "humanoid mode needs Mixamo-style bone names (Hips, Neck, Head, "
                            "Left/Right Arm, ForeArm, Hand, UpLeg, Leg, Foot); use --mode bbox")
    by_role = {v: k for k, v in rl.items()}
    d = lambda role, key="head": table[by_role[role]][key]
    if donor_lm is not None:  # the donor's own mesh, measured the same way as the target: like maps to like
        d_sole, d_crotch, d_shoulder, d_top = donor_lm["sole"], donor_lm["crotch"], donor_lm["shoulder"], donor_lm["top"]
    else:  # bones only: the hip joints stand in for the crotch
        d_sole = min(min(table[n]["head"][2], table[n]["tail"][2]) for n in table)
        d_crotch = (d(("upleg", 1))[2] + d(("upleg", -1))[2]) / 2
        d_shoulder = (d(("arm", 1))[2] + d(("arm", -1))[2]) / 2
        d_top = max(max(table[n]["head"][2], table[n]["tail"][2]) for n in table)
    zmap = piecewise([(d_sole, lm["sole"]), (d_crotch, lm["crotch"]), (d_shoulder, lm["shoulder"]), (d_top, lm["top"])])
    scale = lm["height"] / max(d_top - d_sole, 1e-6)
    band = 0.02 * lm["height"]
    x0 = lm["x0"]
    centre = lambda x: np.abs(x - x0) <= lm["torso_half_width"] * 1.2
    new = {}
    turn = {}  # bone -> rotation (3x3) carried by bones below a re-aimed arm (fingers follow the hand)
    for s in (1, -1):
        root, tip, axis, arm_pts = arm_axis(pts, lm, s)
        chain = [by_role[("arm", s)], by_role[("forearm", s)], by_role[("hand", s)]]
        joints = [table[chain[0]]["head"], table[chain[1]]["head"], table[chain[2]]["head"], table[chain[2]]["tail"]]
        lens = np.cumsum([0.0] + [float(np.linalg.norm(joints[i + 1] - joints[i])) for i in range(3)])
        frac = lens / max(lens[-1], 1e-9)
        pos = [root + f * (tip - root) for f in frac]
        snapped = [arm_slab_centroid(arm_pts, p, axis, band) for p in pos[:3]] + [pos[3]]
        for i, n in enumerate(chain):
            new[n] = {"head": snapped[i], "tail": snapped[i + 1]}
        donor_dir = Vector((joints[3] - joints[0]).tolist()).normalized()
        rot = np.array(donor_dir.rotation_difference(Vector(axis.tolist())).to_matrix())
        turn[chain[2]] = rot
        sh = by_role.get(("shoulder", s))
        if sh is not None:
            head = table[sh]["head"]
            z = zmap(head[2])
            c = slice_centroid(pts, z, band, centre)
            new[sh] = {"head": np.array([x0 + (head[0] - d("hips")[0]) * scale, c[1] if c is not None else head[1], z]),
                       "tail": snapped[0]}
    for n in order_bones(table):
        if n in new:
            continue
        role = rl.get(n)
        head = table[n]["head"]
        par = table[n]["parent"]
        if par in turn:
            turn[n] = turn[par]
        z = zmap(head[2])
        if isinstance(role, str):  # spine, neck, head: centre-slice centroid
            c = slice_centroid(pts, z, band, centre)
            new[n] = {"head": np.array([c[0], c[1], z]) if c is not None else np.array([x0, head[1] * scale, z])}
        elif isinstance(role, tuple) and role[0] in ("upleg", "leg", "foot"):
            c = leg_centroid(pts, min(max(z, lm["sole"] + band), lm["crotch"] - 2 * band), band, lm, role[1])
            fallback = np.array([x0 + (head[0] - x0) * scale, head[1] * scale, z])
            new[n] = {"head": np.array([c[0], c[1], max(z, lm["sole"])]) if c is not None else fallback}
        elif par in new:  # toes, fingers, extras: keep the donor offset from the parent, scaled (and turned)
            off = (head - table[par]["head"]) * scale
            if n in turn:
                off = turn[n] @ off
            new[n] = {"head": new[par]["head"] + off}
        else:
            new[n] = {"head": np.array([x0 + (head[0] - x0) * scale, head[1] * scale, z])}
    for n in new:
        if "tail" in new[n]:
            continue
        kids = [k for k in table[n]["children"] if np.linalg.norm(table[k]["head"] - table[n]["tail"]) < 1e-4 and k in new]
        if len(kids) == 1:
            new[n]["tail"] = new[kids[0]]["head"]
        else:
            off = (table[n]["tail"] - table[n]["head"]) * scale
            new[n]["tail"] = new[n]["head"] + (turn[n] @ off if n in turn else off)
    return new, {"scale": scale, "donor": {"sole": d_sole, "crotch": d_crotch, "shoulder": d_shoulder, "top": d_top}}


def fit_bbox(table, pts):
    heads = np.array([v["head"] for v in table.values()] + [v["tail"] for v in table.values()])
    dlo, dhi = heads.min(axis=0), heads.max(axis=0)
    tlo, thi = pts.min(axis=0), pts.max(axis=0)
    s = (thi - tlo) / np.maximum(dhi - dlo, 1e-6)
    mp = lambda p: tlo + (p - dlo) * s
    return {n: {"head": mp(v["head"]), "tail": mp(v["tail"])} for n, v in table.items()}, {"scale_xyz": s.tolist()}


def apply_fit(arm, new):
    blkit.select_only([arm])
    bpy.ops.object.mode_set(mode="EDIT")
    inv = arm.matrix_world.inverted()
    for eb in arm.data.edit_bones:
        f = new.get(eb.name)
        if f is None:
            continue
        eb.use_connect = False
    for eb in arm.data.edit_bones:
        f = new.get(eb.name)
        if f is None:
            continue
        h, t = Vector(f["head"].tolist()), Vector(f["tail"].tolist())
        if (t - h).length < 1e-4:
            t = h + Vector((0.0, 0.0, 0.01))
        eb.head, eb.tail = inv @ h, inv @ t
    bpy.ops.object.mode_set(mode="OBJECT")


def inside_report(objs, new, height):
    """Joints inside the body. Generated meshes are overlapping parts, so a nearest-surface test on the raw mesh
    reads inside points as outside; the test runs on a voxel remesh of the union (one closed surface) instead."""
    points, _, _, faces, _ = proxy_weights.gather_body(objs)
    src = proxy_weights.make_mesh_object("GSK_InsideSource", points, faces)
    shell = proxy_weights.voxel_proxy(src, max(height / 150.0, 1e-3))
    bpy.data.objects.remove(src, do_unlink=True)
    bvh = BVHTree.FromObject(shell, bpy.context.evaluated_depsgraph_get())
    bpy.data.objects.remove(shell, do_unlink=True)
    inside, worst = 0, 0.0
    rows = {}
    for n, f in new.items():
        p = Vector(f["head"].tolist())
        loc, normal, _, dist = bvh.find_nearest(p)
        ins = loc is not None and (p - loc).dot(normal) <= 0
        inside += 1 if ins else 0
        if not ins and dist is not None:
            worst = max(worst, dist)
        rows[n] = {"head": [round(float(v), 4) for v in f["head"]], "inside": bool(ins)}
    return inside / max(len(new), 1), worst, rows


def sample_surface_objs(objs, n, seed):
    """Surface samples of evaluated (posed or rest) meshes."""
    dg = bpy.context.evaluated_depsgraph_get()
    tris = []
    for o in objs:
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        me.calc_loop_triangles()
        k = len(me.vertices)
        co = np.empty(k * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        m = np.array(oe.matrix_world, dtype=np.float64)
        co = co.reshape(k, 3).astype(np.float64) @ m[:3, :3].T + m[:3, 3]
        lt = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
        me.loop_triangles.foreach_get("vertices", lt)
        tris.append(co[lt.reshape(-1, 3)])
        oe.to_mesh_clear()
    return sample_surface(np.concatenate(tris), n, seed) if tris else None


def load_donor(path, warnings, samples=20000, seed=7):
    """Import the donor into the current scene and return its armature (donor meshes are deleted)."""
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext == ".blend":
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.objects = list(src.objects)
        for o in dst.objects:
            if o is not None:
                bpy.context.scene.collection.objects.link(o)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        blkit._import_once(path, ".fbx")
    else:
        raise blkit.UsageError("--donor must be .blend, .glb, .gltf or .fbx")
    added = [o for o in bpy.data.objects if o not in before]
    arms = [o for o in added if o.type == "ARMATURE"]
    shapes = blkit.custom_shapes()
    donor_meshes = [o for o in added if o.type == "MESH" and o.name not in shapes]
    arm_obj = arms[0] if len(arms) == 1 else None
    with blkit.rest_pose([arm_obj] if arm_obj is not None else []):
        donor_pts = sample_surface_objs(donor_meshes, samples, seed) if donor_meshes else None
    if len(arms) != 1:
        raise blkit.Refused("donor_armature_count", "the donor file must hold exactly one armature, found %d" % len(arms))
    arm = arms[0]
    for o in added:
        if o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    if arm.parent is not None:
        mat = arm.matrix_world.copy()
        arm.parent = None
        arm.matrix_world = mat
    blkit.select_only([arm])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bpy.data.actions:
        warnings.append("donor clips kept (%d); check them on the fitted rig before use" % len(bpy.data.actions))
    return arm, donor_pts


def main(args, res):
    res["experimental"] = True
    res["unknown"] = "U-23"
    if not args.experimental:
        raise blkit.Refused("experimental", "donor_fit.py is EXPERIMENTAL (U-23): pass --experimental, then gate the "
                            "result with proxy_weights.py and inspect_rig.py and have a reviewer check the joints")
    warnings = []
    if args.samples < 100:
        raise blkit.UsageError("--samples must be at least 100")
    if blkit.same_path(args.out, args.donor):
        raise blkit.Refused("out_is_input", "--out is the --donor file; write the fitted rig to a new path")
    blkit.refuse_out_is_input(args.out, [args.inp], args.in_place)
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    for a in blkit.armatures():
        warnings.append("target armature %s removed" % a.name)
        for m in blkit.meshes():
            for mod in [x for x in m.modifiers if x.type == "ARMATURE" and x.object is a]:
                m.modifiers.remove(mod)
            if m.parent is a:
                mat = m.matrix_world.copy()
                m.parent = None
                m.matrix_world = mat
        bpy.data.objects.remove(a, do_unlink=True)
    targets = blkit.meshes()
    if not targets:
        raise blkit.Refused("no_mesh", "no target mesh")
    arm, donor_pts = load_donor(os.path.abspath(args.donor), warnings, args.samples, args.seed)
    table = donor_table(arm)
    pts = sample_surface(world_triangles(targets), args.samples, args.seed)
    mode = args.mode
    if mode == "auto":
        mode = "humanoid" if blkit.find_bone(arm, "Hips") and blkit.find_bone(arm, "LeftUpLeg") else "bbox"
    if mode == "humanoid":
        lm = landmarks(pts)
        donor_lm = None
        if donor_pts is not None:
            try:
                donor_lm = landmarks(donor_pts)
            except blkit.Refused as e:
                warnings.append("donor mesh landmarks failed (%s); using its bones" % e)
        else:
            warnings.append("the donor file has no mesh; donor landmarks come from its bones")
        new, info = fit_humanoid(arm, table, pts, lm, donor_lm)
        res["landmarks"] = {k: round(float(v), 4) for k, v in lm.items()}
    else:
        new, info = fit_bbox(table, pts)
    apply_fit(arm, new)
    for m in targets:
        mat = m.matrix_world.copy()
        m.parent = arm
        m.matrix_world = mat
        if blkit.armature_of(m) is not arm:
            mod = m.modifiers.new("Armature", "ARMATURE")
            mod.object = arm
    frac, worst, rows = inside_report(targets, new, float(pts[:, 2].max() - pts[:, 2].min()))
    res.update(input=os.path.abspath(args.inp), donor=os.path.abspath(args.donor), mode=mode,
               fit={k: (round(v, 4) if isinstance(v, float) else v) for k, v in info.items() if k != "donor"},
               donor_landmarks={k: round(float(v), 4) for k, v in info.get("donor", {}).items()},
               inside_fraction=round(frac, 3), worst_outside_m=round(worst, 4), joints=rows, bones=len(new))
    blkit.export_model(args.out, [arm] + targets, "character", warnings)
    res["out"] = os.path.abspath(args.out)
    res["next"] = "proxy_weights.py --in <out> --voxel auto, then inspect_rig.py; a reviewer checks the joints"
    if frac < MIN_INSIDE:
        warnings.append("only %.0f%% of joints are inside the mesh" % (frac * 100))
    res["warnings"] = warnings
    res["ok"] = frac >= MIN_INSIDE
    if not res["ok"]:
        res["error"] = "fit rejected: inside_fraction %.2f < %.2f" % (frac, MIN_INSIDE)


if __name__ == "__main__":
    blkit.run_script("donor_fit.py", __doc__, add_args, main)
