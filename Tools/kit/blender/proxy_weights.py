"""Repair skin weights with a voxel proxy plus a weight transfer, for meshes where heat weights fail silently.

Run: node Tools/kit/blender/bl_run.mjs proxy_weights.py -- --in <rig.blend|fbx|glb> --out <file> --voxel <m|auto>
       [--rigid <object or vertex group>=<bone>]... [--exclude-bones <glob>[,<glob>]]... [--max-cells 400]
       [--limit 4] [--in-place]
Method (research_blender_rigging.md 2.3, verified at 0 unweighted vertices on a 14,807-vertex generated mesh):
  1. measure the narrowest gap between limb parts (surfaces that face each other across a gap and belong to bones
     at least 4 steps apart in the hierarchy, e.g. knee to knee, forearm to hip, finger to finger); refuse a voxel
     size >= that gap, because the proxy would fuse the limbs and weights would bleed across. --rigid parts and
     --exclude-bones bones (e.g. '*Hand?*' for fingers) are left out of this search. A voxel below max dimension /
     --max-cells is too fine; when the gap itself is below that floor no voxel works (gap_below_voxel_floor);
  2. voxel-remesh a copy of the body (without the rigid parts) into one closed proxy; refuse if the proxy bridges
     bones that the mesh keeps apart (excluded bones are not judged);
  3. heat-weight the proxy (ARMATURE_AUTO); fill any proxy vertex heat left empty from the nearest bones;
  4. give every original vertex the interpolated weights of its nearest proxy triangle, keep 4, normalise;
     vertices of excluded bones take 100% of their nearest bone;
  5. rigid parts (teeth, armour, props) get 100% on one deform bone; a separate unskinned mesh named by --rigid is
     skinned to the armature;
  6. measure: zero unweighted vertices or ok=false. The operator's {'FINISHED'} is never trusted.
Every mesh of the input is written to --out: bone-parented props as they are, and unskinned meshes not named by
--rigid unchanged (they are listed, and the rig gate fails them).
The pose must be T or A with the limbs apart. Only vertex groups named after deform bones are replaced.
"""
import math
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import blkit  # noqa: E402
import riglogic  # noqa: E402

FAR_HOPS = 4


def add_args(p):
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True, help=".blend, .fbx or .glb")
    p.add_argument("--voxel", required=True, help="proxy voxel size in metres, or 'auto'")
    p.add_argument("--rigid", action="append", default=[], metavar="NAME=BONE",
                   help="object or vertex group NAME gets 100%% weight on BONE (repeatable)")
    p.add_argument("--limit", type=int, default=4, help="max influences per vertex")
    p.add_argument("--exclude-bones", action="append", default=[], metavar="GLOB",
                   help="deform bones (glob, case-insensitive, e.g. '*Hand?*') kept out of the gap search and the "
                        "fusion check; their vertices take 100%% of the nearest bone (repeatable or comma-separated)")
    p.add_argument("--max-cells", type=int, default=riglogic.DEFAULT_MAX_CELLS,
                   help="the smallest voxel is max dimension / this (default %d)" % riglogic.DEFAULT_MAX_CELLS)
    blkit.add_in_place_arg(p)


def parse_voxel(text):
    """None for 'auto', else a positive size in metres; anything else is a usage error."""
    if str(text).strip().lower() == "auto":
        return None
    try:
        v = float(text)
    except ValueError:
        raise blkit.UsageError("--voxel must be a size in metres or 'auto', got %r" % text)
    if not (v > 0 and math.isfinite(v)):
        raise blkit.UsageError("--voxel must be a positive number of metres, got %r" % text)
    return v


def split_rigid(spec):
    if "=" not in spec or not spec.split("=", 1)[0] or not spec.split("=", 1)[1]:
        raise blkit.UsageError("--rigid expects NAME=BONE, got %r" % spec)
    return tuple(spec.split("=", 1))


def nearest_bone_labels(points, heads, tails, chunk=20000):
    """Index of the nearest bone segment for each point, and that distance."""
    d = tails - heads
    l2 = np.maximum((d * d).sum(axis=1), 1e-12)
    labels = np.empty(len(points), dtype=np.int64)
    dists = np.empty(len(points), dtype=np.float64)
    for s in range(0, len(points), chunk):
        p = points[s:s + chunk]
        rel = p[:, None, :] - heads[None, :, :]
        t = np.clip((rel * d[None]).sum(axis=2) / l2[None], 0.0, 1.0)
        closest = heads[None] + t[..., None] * d[None]
        dd = ((p[:, None, :] - closest) ** 2).sum(axis=2)
        labels[s:s + chunk] = dd.argmin(axis=1)
        dists[s:s + chunk] = np.sqrt(dd.min(axis=1))
    return labels, dists


def two_nearest_weights(points, heads, tails):
    """Inverse-distance weights over the 2 nearest bones (fallback for proxy vertices heat left empty)."""
    d = tails - heads
    l2 = np.maximum((d * d).sum(axis=1), 1e-12)
    rel = points[:, None, :] - heads[None]
    t = np.clip((rel * d[None]).sum(axis=2) / l2[None], 0.0, 1.0)
    dd = np.sqrt(((points[:, None, :] - (heads[None] + t[..., None] * d[None])) ** 2).sum(axis=2))
    order = np.argsort(dd, axis=1)[:, :2]
    out = np.zeros((len(points), len(heads)))
    for i in range(len(points)):
        a, b = order[i, 0], order[i, 1] if order.shape[1] > 1 else order[i, 0]
        wa, wb = 1.0 / max(dd[i, a], 1e-6), 1.0 / max(dd[i, b], 1e-6)
        out[i, a] += wa / (wa + wb)
        out[i, b] += wb / (wa + wb)
    return out


def gather_body(targets):
    """World-space rest coordinates, normals, edges and faces of all target meshes as one body."""
    pts, nrm, edges, faces, owner = [], [], [], [], []
    base = 0
    for o in targets:
        me = o.data
        n = len(me.vertices)
        co = np.empty(n * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        no = np.empty(n * 3, dtype=np.float32)
        me.vertices.foreach_get("normal", no)
        m = np.array(o.matrix_world, dtype=np.float64)
        pts.append(co.reshape(n, 3).astype(np.float64) @ m[:3, :3].T + m[:3, 3])
        nm = no.reshape(n, 3).astype(np.float64) @ np.linalg.inv(m[:3, :3])
        nm /= np.maximum(np.linalg.norm(nm, axis=1, keepdims=True), 1e-12)
        nrm.append(nm)
        ne = len(me.edges)
        ev = np.empty(ne * 2, dtype=np.int64)
        me.edges.foreach_get("vertices", ev)
        edges.append(ev.reshape(ne, 2) + base)
        faces.extend([[base + i for i in p.vertices] for p in me.polygons])
        owner.append((o, base, n))
        base += n
    return (np.concatenate(pts), np.concatenate(nrm), np.concatenate(edges) if edges else np.zeros((0, 2), np.int64),
            faces, owner)


def far_matrix(names, parents, edge_labels):
    """far[a][b]: bones at least FAR_HOPS apart in the hierarchy whose surfaces the mesh does not join. Two labels
    count as joined when mesh edges link them directly or through one other label (nearest-bone labels are noisy
    near joints, e.g. an armpit can read UpLeg -> Shoulder -> Arm)."""
    dist = riglogic.hierarchy_distances(parents)
    k = len(names)
    adj = [set() for _ in range(k)]
    for a, b in edge_labels:
        adj[int(a)].add(int(b))
        adj[int(b)].add(int(a))
    far = np.zeros((k, k), dtype=bool)
    for i in range(k):
        near = set(adj[i])
        for j in adj[i]:
            near |= adj[j]
        for j in range(k):
            far[i, j] = i != j and j not in near and dist(names[i], names[j]) >= FAR_HOPS
    return far


def _within(pts, box, r):
    lo, hi = box
    return np.all((pts >= lo - r) & (pts <= hi + r), axis=1)


def min_limb_gap(points, normals, labels, far, radius):
    """Narrowest distance between facing surfaces of far-apart bones, searched up to `radius`. Surfaces 'face' when
    both normals are within about 73 deg of the line joining the points (sign ignored: generated meshes flip faces)."""
    k = far.shape[0]
    groups = [np.where(labels == a)[0] for a in range(k)]
    boxes = [(points[g].min(axis=0), points[g].max(axis=0)) if len(g) else None for g in groups]
    best, pair = None, None
    for a in range(k):
        for b in range(a + 1, k):
            if not far[a, b] or boxes[a] is None or boxes[b] is None:
                continue
            sep = np.maximum(0.0, np.maximum(boxes[a][0] - boxes[b][1], boxes[b][0] - boxes[a][1]))
            if float(np.linalg.norm(sep)) > radius:
                continue
            ia = groups[a][_within(points[groups[a]], boxes[b], radius)]
            ib = groups[b][_within(points[groups[b]], boxes[a], radius)]
            if not len(ia) or not len(ib):
                continue
            pb, nb = points[ib], normals[ib]
            step = max(1, int(2_000_000 // max(1, len(ib))))
            for s in range(0, len(ia), step):
                ca = ia[s:s + step]
                d = pb[None, :, :] - points[ca][:, None, :]
                dist = np.sqrt((d * d).sum(axis=2))
                ok = (dist > 1e-9) & (dist <= radius)
                if not ok.any():
                    continue
                v = d / np.maximum(dist, 1e-12)[..., None]
                fa = np.abs((normals[ca][:, None, :] * v).sum(axis=2)) > 0.3
                fb = np.abs((nb[None, :, :] * v).sum(axis=2)) > 0.3
                mask = ok & fa & fb
                if mask.any():
                    m = float(dist[mask].min())
                    if best is None or m < best:
                        best, pair = m, (a, b)
    return best, pair


def make_mesh_object(name, points, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(p) for p in points], [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def voxel_proxy(src, voxel):
    mod = src.modifiers.new("GSK_Remesh", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = voxel
    mod.use_smooth_shade = False
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    src.modifiers.remove(mod)
    ob = bpy.data.objects.new("GSK_WeightProxy", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def heat_weights(proxy, arm):
    blkit.select_only([arm, proxy])
    proxy.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def proxy_weight_matrix(proxy, deform):
    idx = {vg.index: deform.index(vg.name) for vg in proxy.vertex_groups if vg.name in deform}
    w = np.zeros((len(proxy.data.vertices), len(deform)))
    for v in proxy.data.vertices:
        for g in v.groups:
            k = idx.get(g.group)
            if k is not None and g.weight > blkit.EPS_WEIGHT:
                w[v.index, k] = g.weight
    return w


def parse_rigid(specs, arm, targets, deform):
    """[(part name, bone name, [(object, [vertex indices])])]. The bone must be a deform bone."""
    out = []
    for name, bone in specs:
        b = blkit.find_bone(arm, bone)
        if b is None:
            raise blkit.Refused("rigid_bone_missing", "--rigid bone %r is not in the armature" % bone)
        if b.name not in deform:
            raise blkit.Refused("rigid_bone_not_deform", "--rigid bone %r is not a deform bone, so no weight can "
                                "go to it; use a deform bone such as its parent" % b.name)
        hits = []
        for o, _base, n in targets:
            if o.name == name:
                hits.append((o, list(range(n))))
            else:
                vg = o.vertex_groups.get(name)
                if vg is not None:
                    sel = [v.index for v in o.data.vertices for g in v.groups if g.group == vg.index and g.weight > 0.5]
                    if sel:
                        hits.append((o, sel))
        if not hits:
            raise blkit.Refused("rigid_part_missing", "--rigid part %r is neither a mesh object nor a vertex group" % name)
        out.append((name, b.name, hits))
    return out


def main(args, res):
    warnings = []
    requested = parse_voxel(args.voxel)
    if args.limit < 1:
        raise blkit.UsageError("--limit must be at least 1")
    if not 50 <= args.max_cells <= 4000:
        raise blkit.UsageError("--max-cells must be between 50 and 4000")
    excludes = [g.strip() for spec in args.exclude_bones for g in spec.split(",") if g.strip()]
    rigid_specs = [split_rigid(x) for x in args.rigid]
    blkit.refuse_out_is_input(args.out, [args.inp], args.in_place)
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    arms = blkit.armatures()
    if len(arms) != 1:
        raise blkit.Refused("armature_count", "need exactly one armature, found %d" % len(arms))
    arm = arms[0]
    mesh_objs = blkit.meshes()
    targets = [m for m in mesh_objs if blkit.armature_of(m) is arm and not blkit.is_bone_parented(m)]
    if not targets:
        targets = [m for m in mesh_objs if not blkit.is_bone_parented(m)]
    rigid_names = {name for name, _ in rigid_specs}
    for m in mesh_objs:
        if m.name in rigid_names and m not in targets:
            if blkit.is_bone_parented(m):
                warnings.append("%s already follows a bone (bone-parented); --rigid leaves it as it is" % m.name)
            else:
                targets.append(m)  # a separate unskinned part (armour, prop) to skin to one bone
    rigid_specs = [(n, b) for n, b in rigid_specs if not any(m.name == n and blkit.is_bone_parented(m) for m in mesh_objs)]
    if not targets:
        raise blkit.Refused("no_mesh", "no mesh to weight")
    for o in targets:
        if o.data.shape_keys is not None:
            warnings.append("%s has shape keys: weights are transferred to the basis shape" % o.name)
    deform = blkit.deform_bone_names(arm)
    if not deform:
        raise blkit.Refused("no_deform_bones", "the armature has no deform bones")
    bones = [arm.data.bones[n] for n in deform]
    mw = arm.matrix_world
    heads = np.array([tuple(mw @ b.head_local) for b in bones])
    tails = np.array([tuple(mw @ b.tail_local) for b in bones])
    parents = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
    excluded = [k for k, n in enumerate(deform) if riglogic.bone_matches(n, excludes)]
    if excludes and not excluded:
        warnings.append("--exclude-bones %s matched no deform bone" % excludes)

    points, normals, edges, faces, owner = gather_body(targets)
    rigid = parse_rigid(rigid_specs, arm, owner, deform)
    rigid_map = {}
    for name, bone, hits in rigid:
        for o, idxs in hits:
            for i in idxs:
                rigid_map[(o.name, i)] = bone
    rigid_mask = np.zeros(len(points), dtype=bool)
    for o, base, n in owner:
        for i in range(n):
            if (o.name, i) in rigid_map:
                rigid_mask[base + i] = True
    free = ~rigid_mask
    lo, hi = points.min(axis=0), points.max(axis=0)
    max_dim = float((hi - lo).max())
    labels, _ = nearest_bone_labels(points, heads, tails)
    edge_labels = np.unique(np.sort(labels[edges], axis=1), axis=0) if len(edges) else np.zeros((0, 2), np.int64)
    edge_labels = edge_labels[edge_labels[:, 0] != edge_labels[:, 1]] if len(edge_labels) else edge_labels
    far = far_matrix(deform, parents, edge_labels)
    for k in excluded:
        far[k, :] = False
        far[:, k] = False

    auto = requested is None
    radius = max(max_dim / 8.0, (requested or 0.0) * 2.5)
    gap, pair = min_limb_gap(points[free], normals[free], labels[free], far, radius) if free.any() else (None, None)
    gap_pair = None if pair is None else [deform[pair[0]], deform[pair[1]]]
    voxel = riglogic.auto_voxel(max_dim, gap, args.max_cells) if auto else requested
    res.update(input=os.path.abspath(args.inp), voxel_m=round(voxel, 5), voxel_auto=auto,
               voxel_floor_m=round(riglogic.voxel_floor(max_dim, args.max_cells), 5), max_cells=args.max_cells,
               min_limb_gap_m=None if gap is None else round(gap, 5), gap_pair=gap_pair,
               gap_search_radius_m=round(radius, 5), max_dim_m=round(max_dim, 4), verts=int(len(points)),
               meshes=[o.name for o in targets], excluded_bones=[deform[k] for k in excluded],
               rigid_verts=int(rigid_mask.sum()))
    ok, code, msg = riglogic.voxel_decision(voxel, gap, max_dim, args.max_cells, gap_pair)
    if not ok:
        floor = riglogic.voxel_floor(max_dim, args.max_cells)
        suggested = None if code == "gap_below_voxel_floor" or gap is None else round(max(gap * 0.5, floor), 5)
        raise blkit.Refused(code, msg, suggested_voxel_m=suggested)

    w = tris = tco = bvh = None
    if free.any():
        src_faces = [f for f in faces if not all(rigid_mask[i] for i in f)]
        src = make_mesh_object("GSK_ProxySource", points, src_faces)
        proxy = voxel_proxy(src, voxel)
        bpy.data.objects.remove(src, do_unlink=True)
        pn = len(proxy.data.vertices)
        if pn == 0:
            raise blkit.Refused("proxy_empty", "voxel remesh produced no geometry (open or tiny mesh?)")
        pco = np.empty(pn * 3, dtype=np.float32)
        proxy.data.vertices.foreach_get("co", pco)
        pco = pco.reshape(pn, 3).astype(np.float64)
        plabels, _ = nearest_bone_labels(pco, heads, tails)
        pe = np.empty(len(proxy.data.edges) * 2, dtype=np.int64)
        proxy.data.edges.foreach_get("vertices", pe)
        pl = plabels[pe.reshape(-1, 2)]
        bridged = pl[far[pl[:, 0], pl[:, 1]]]
        fused = sorted({tuple(sorted((deform[a], deform[b]))) for a, b in bridged})
        res.update(proxy_verts=pn, fused_pairs=[list(x) for x in fused])
        if fused:
            raise blkit.Refused("proxy_fuses_limbs", "the voxel proxy bridges %d bone pairs the mesh keeps apart (e.g. %s); "
                                "use a smaller voxel, pose the limbs apart, or leave thin parts out with "
                                "--exclude-bones" % (len(fused), " / ".join(fused[0])))

        heat_weights(proxy, arm)
        w = proxy_weight_matrix(proxy, deform)
        empty = np.where(w.sum(axis=1) <= blkit.EPS_WEIGHT)[0]
        res["proxy_heat_unweighted"] = int(len(empty))
        if len(empty):
            w[empty] = two_nearest_weights(pco[empty], heads, tails)
            if len(empty) > 0.05 * pn:
                warnings.append("heat weighting left %d of %d proxy vertices empty; they took nearest-bone weights"
                                % (len(empty), pn))

        bm = bmesh.new()
        bm.from_mesh(proxy.data)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.verts.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
        tris = [tuple(v.index for v in f.verts) for f in bm.faces]
        tco = [v.co.copy() for v in bm.verts]
        bvh = BVHTree.FromBMesh(bm)
        bm.free()
        bpy.data.objects.remove(proxy, do_unlink=True)
    else:
        res.update(proxy_verts=0, fused_pairs=[], proxy_heat_unweighted=0)

    excluded_set = set(excluded)
    for o, base, n in owner:
        for name in deform:
            vg = o.vertex_groups.get(name)
            if vg is not None:
                o.vertex_groups.remove(vg)
        groups = {name: o.vertex_groups.new(name=name) for name in deform}
        for i in range(n):
            bone = rigid_map.get((o.name, i))
            if bone is not None:
                groups[bone].add([i], 1.0, "REPLACE")
                continue
            k = int(labels[base + i])
            if k in excluded_set or bvh is None:
                groups[deform[k]].add([i], 1.0, "REPLACE")
                continue
            loc, _, fi, _ = bvh.find_nearest(Vector(points[base + i]))
            if fi is None:
                groups[deform[k]].add([i], 1.0, "REPLACE")
                continue
            a, b, c = tris[fi]
            u, v, t = riglogic.barycentric(tuple(loc), tuple(tco[a]), tuple(tco[b]), tuple(tco[c]))
            row = u * w[a] + v * w[b] + t * w[c]
            final = riglogic.limit_and_normalise({deform[x]: float(row[x]) for x in np.nonzero(row)[0]}, args.limit)
            if not final:
                final = {deform[k]: 1.0}
            for name, wt in final.items():
                groups[name].add([i], wt, "REPLACE")
        if blkit.armature_of(o) is not arm:
            mod = o.modifiers.new("Armature", "ARMATURE")
            mod.object = arm
        if o.parent is not arm:
            mat = o.matrix_world.copy()
            o.parent = arm
            o.parent_type = "OBJECT"
            o.matrix_world = mat

    stats = blkit.weight_stats(targets, args.limit, deform)
    others = [m for m in blkit.meshes() if m not in targets]
    bone_parented = [m.name for m in others if blkit.is_bone_parented(m)]
    unskinned = [m.name for m in others if not blkit.is_bone_parented(m) and blkit.armature_of(m) is not arm]
    if unskinned:
        warnings.append("meshes left unskinned (the rig gate fails them; name them with --rigid NAME=BONE): %s"
                        % unskinned[:10])
    res.update(unweighted=stats["unweighted"], max_influences=stats["max_influences"],
               verts_over_limit=stats["verts_over_limit"],
               bones_without_weight=sorted(b for b, k in stats["per_bone_verts"].items() if k == 0 and not riglogic.is_leaf_name(b)),
               rigid=[{"part": name, "bone": bone} for name, bone, _ in rigid],
               bone_parented_meshes=bone_parented, unskinned_meshes=unskinned,
               meshes_out=len(targets) + len(others))
    objs = [arm] + targets + others
    res["out"] = os.path.abspath(args.out)
    blkit.export_model(args.out, objs, "character", warnings)
    res["warnings"] = warnings
    res["ok"] = stats["unweighted"] == 0 and stats["max_influences"] <= args.limit
    if not res["ok"]:
        res["error"] = "%d vertices still unweighted after the transfer" % stats["unweighted"]


if __name__ == "__main__":
    blkit.run_script("proxy_weights.py", __doc__, add_args, main)
