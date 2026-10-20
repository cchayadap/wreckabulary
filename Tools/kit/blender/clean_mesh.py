"""Clean a generated mesh before rigging or engine import: weld, drop loose and degenerate geometry and debris
islands, make normals consistent, and optionally decimate to a ratio or a triangle budget.

Run: node Tools/kit/blender/bl_run.mjs clean_mesh.py -- --in <model> --out <file.glb|.fbx|.blend>
       [--decimate <ratio 0-1>] [--target-tris N] [--merge <m>] [--min-island-verts N] [--apply-transforms]
       [--in-place]
Generated meshes arrive as many islands with duplicate seam vertices; heat weighting and voxel proxies behave
better on welded, consistent geometry (research_blender_rigging.md 2.2-2.3). Budgets: about 10-20k triangles
per character (extract_animation.md 2.2). UVs, materials and vertex groups are kept; meshes with shape keys are
welded but not decimated (a decimate would drop the keys), and that is reported.
"""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402

import blkit  # noqa: E402


def add_args(p):
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--decimate", type=float, help="keep this fraction of triangles (0 < r <= 1)")
    p.add_argument("--target-tris", type=int, help="decimate to about this many triangles in total")
    p.add_argument("--merge", type=float, default=1e-4, help="weld distance in metres (default 0.0001)")
    p.add_argument("--min-island-verts", type=int, default=0, help="delete islands with fewer vertices")
    p.add_argument("--apply-transforms", action="store_true", help="bake object transforms into static meshes")
    blkit.add_in_place_arg(p)


def islands(bm):
    bm.verts.ensure_lookup_table()
    seen = set()
    out = []
    for v in bm.verts:
        if v.index in seen:
            continue
        comp = []
        stack = [v]
        while stack:
            x = stack.pop()
            if x.index in seen:
                continue
            seen.add(x.index)
            comp.append(x)
            stack.extend(e.other_vert(x) for e in x.link_edges)
        out.append(comp)
    return out


def measure(bm):
    return {"verts": len(bm.verts), "tris": sum(len(f.verts) - 2 for f in bm.faces), "islands": len(islands(bm)),
            "non_manifold_edges": sum(1 for e in bm.edges if not e.is_manifold),
            "loose_verts": sum(1 for v in bm.verts if not v.link_edges),
            "loose_edges": sum(1 for e in bm.edges if not e.link_faces)}


def clean(ob, args):
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    before = measure(bm)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=args.merge)
    bmesh.ops.dissolve_degenerate(bm, dist=min(args.merge, 1e-6), edges=bm.edges[:])
    loose_e = [e for e in bm.edges if not e.link_faces]
    if loose_e:
        bmesh.ops.delete(bm, geom=loose_e, context="EDGES")
    loose_v = [v for v in bm.verts if not v.link_edges]
    if loose_v:
        bmesh.ops.delete(bm, geom=loose_v, context="VERTS")
    removed_islands = 0
    if args.min_island_verts > 0:
        small = [c for c in islands(bm) if len(c) < args.min_island_verts]
        doomed = [v for c in small for v in c]
        if doomed:
            bmesh.ops.delete(bm, geom=doomed, context="VERTS")
        removed_islands = len(small)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    after = measure(bm)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return before, after, removed_islands


def decimate(ob, ratio):
    mod = ob.modifiers.new("GSK_Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = max(0.0, min(1.0, ratio))
    mod.use_collapse_triangulate = True
    # every modifier above this one would be baked in too, so bake only when it is the only modifier
    others = [m for m in ob.modifiers if m != mod and m.type != "ARMATURE"]
    if others:
        ob.modifiers.remove(mod)
        return "skipped: other modifiers %s" % [m.name for m in others]
    saved = [(m, m.show_viewport) for m in ob.modifiers if m.type == "ARMATURE"]
    for m, _ in saved:
        m.show_viewport = False
    dg = bpy.context.evaluated_depsgraph_get()
    new = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    for m, vis in saved:
        m.show_viewport = vis
    ob.modifiers.remove(mod)
    old = ob.data
    ob.data = new
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return None


def main(args, res):
    warnings = []
    if args.decimate is not None and not (0 < args.decimate <= 1):
        raise blkit.UsageError("--decimate must be in (0, 1]")
    if args.target_tris is not None and args.target_tris <= 0:
        raise blkit.UsageError("--target-tris must be positive")
    if not args.merge >= 0 or args.min_island_verts < 0:
        raise blkit.UsageError("--merge and --min-island-verts must not be negative")
    blkit.refuse_out_is_input(args.out, [args.inp], args.in_place)
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    mesh_objs = blkit.meshes()
    if not mesh_objs:
        raise blkit.Refused("no_mesh", "no mesh in %s" % args.inp)
    if args.apply_transforms:
        static = [o for o in mesh_objs if blkit.armature_of(o) is None and o.data.shape_keys is None]
        if static:
            blkit.select_only(static)
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    tris_before = blkit.tri_count(mesh_objs)
    per = []
    for ob in mesh_objs:
        if ob.data.users > 1:
            ob.data = ob.data.copy()
        before, after, removed = clean(ob, args)
        per.append({"object": ob.name, "before": before, "after_clean": after, "removed_islands": removed})
    ratio = args.decimate
    tris_clean = blkit.tri_count(mesh_objs)
    if args.target_tris is not None and tris_clean > 0:
        ratio = min(1.0, args.target_tris / float(tris_clean))
    if ratio is not None and ratio < 1.0:
        for ob, row in zip(mesh_objs, per):
            if ob.data.shape_keys is not None:
                row["decimate"] = "skipped: shape keys"
                warnings.append("%s has shape keys; not decimated" % ob.name)
                continue
            note = decimate(ob, ratio)
            row["decimate"] = note or ratio
            if note:
                warnings.append("%s: decimate %s" % (ob.name, note))
    tris_after = blkit.tri_count(mesh_objs)
    arms = blkit.armatures()
    objs = list(mesh_objs) + arms
    blkit.export_model(args.out, objs, "character" if arms else "prop", warnings)
    res.update(input=os.path.abspath(args.inp), out=os.path.abspath(args.out), tris_before=tris_before,
               tris_clean=tris_clean, tris_after=tris_after, ratio=ratio, objects=per, warnings=warnings,
               ok=os.path.isfile(args.out))


if __name__ == "__main__":
    blkit.run_script("clean_mesh.py", __doc__, add_args, main)
