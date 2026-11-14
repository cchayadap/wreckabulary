"""Move a GLB's pivot to its bottom centre: the lowest point on the ground (glTF/Unity Y = 0) and the footprint
centred on X/Z = 0. Engine-neutral.

Run: node Tools/kit/blender/bl_run.mjs center_glb_bottom.py -- --in <file.glb|dir> --out <file.glb|dir>
       [--mode bottom|center|ground] [--in-place]
Rewritten from WoO's center_glb_bottom.py idea (it worked on Blender 5.2.2, extract_generation.md 8.4), with its
limits fixed:
  - bounds come from the evaluated mesh in rest pose, so skinned and posed assets measure right (WoO used the
    rest bound_box of meshes only);
  - only root objects move, so rigs, children and skinning stay intact; no transform is applied;
  - a JSON report per file (bounding box before and after, triangles, textures with sizes), and ok=false with a
    non-zero bl_run exit when any file fails (WoO skipped mesh-less files silently and always exited 0);
  - export settings are the kit's GLB settings (images kept in their authored format).
Modes: bottom (default) = ground and centre the footprint; center = the whole box centred on the origin;
ground = only drop to Y = 0.
--out may not be --in (file or folder) unless --in-place is given. Every file is written under a temporary name and
renamed into place, and in folder mode the result JSON is rewritten after each file ("partial": true until the end),
so a run killed by the bl_run timeout shows which files it already changed.
"""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import blkit  # noqa: E402


def add_args(p):
    p.add_argument("--in", dest="inp", required=True, help="a .glb/.gltf file or a folder of them")
    p.add_argument("--out", required=True, help="output file, or folder when --in is a folder")
    p.add_argument("--mode", choices=["bottom", "center", "ground"], default="bottom")
    blkit.add_in_place_arg(p)


def texture_list():
    out = []
    for img in bpy.data.images:
        if img.name in ("Render Result", "Viewer Node"):
            continue
        out.append({"name": img.name, "size": list(img.size), "packed": img.packed_file is not None,
                    "bytes": img.packed_file.size if img.packed_file is not None else None})
    return out


def gl_box(lo, hi):
    """Blender box -> glTF-convention min/max ([x, y_up, z])."""
    a, b = blkit.blender_to_gltf(lo), blkit.blender_to_gltf(hi)
    return [round(min(a[i], b[i]), 6) for i in range(3)], [round(max(a[i], b[i]), 6) for i in range(3)]


def process(src, dst, mode):
    warnings = []
    blkit.reset_scene()
    blkit.import_model(src, warnings)
    mesh_objs = blkit.meshes()
    rep = {"in": os.path.abspath(src), "out": os.path.abspath(dst), "ok": False}
    if not mesh_objs:
        rep["error"] = "no mesh in file"
        return rep
    arms = blkit.armatures()
    with blkit.rest_pose(arms):
        lo, hi = blkit.world_bbox(mesh_objs)
    if lo is None:
        rep["error"] = "meshes have no vertices"
        return rep
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    if mode == "bottom":
        delta = Vector((-cx, -cy, -lo[2]))
    elif mode == "center":
        delta = Vector((-cx, -cy, -(lo[2] + hi[2]) / 2))
    else:
        delta = Vector((0.0, 0.0, -lo[2]))
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    for o in roots:
        o.matrix_world.translation = o.matrix_world.translation + delta
    bpy.context.view_layer.update()
    with blkit.rest_pose(arms):
        lo2, hi2 = blkit.world_bbox(mesh_objs)
    before, after = gl_box(lo, hi), gl_box(lo2, hi2)
    keyed = sorted({act.name for act in bpy.data.actions for p in blkit.action_paths(act) if p in ("location", "delta_location")})
    if keyed:
        warnings.append("clips key object location (%s): those keys override the move; re-root them" % ", ".join(keyed[:5]))
    blkit.export_model(dst, list(bpy.context.scene.objects), "character", warnings)  # keeps skins, morphs, clips
    rep.update(ok=os.path.isfile(dst), tris=blkit.tri_count(mesh_objs), roots=[o.name for o in roots],
               bbox_before=before, bbox_after=after, moved=[round(float(v), 6) for v in blkit.blender_to_gltf(delta)],
               min_y=after[0][1], center_xz=[round((after[0][0] + after[1][0]) / 2, 6), round((after[0][2] + after[1][2]) / 2, 6)],
               textures=texture_list(), warnings=warnings)
    return rep


def main(args, res):
    src, dst = os.path.abspath(args.inp), os.path.abspath(args.out)
    if not os.path.exists(src):
        raise blkit.UsageError("--in not found: %s" % src)
    blkit.refuse_out_is_input(dst, [src], args.in_place)
    if os.path.isdir(src):
        if os.path.isfile(dst):
            raise blkit.UsageError("--out must be a folder when --in is a folder")
        names = sorted(n for n in os.listdir(src) if n.lower().endswith((".glb", ".gltf")))
        if not names:
            raise blkit.Refused("no_input", "no .glb/.gltf files in %s" % src)
        os.makedirs(dst, exist_ok=True)
        pairs = [(os.path.join(src, n), os.path.join(dst, os.path.splitext(n)[0] + ".glb")) for n in names]
    else:
        if not dst.lower().endswith(".glb"):
            raise blkit.UsageError("--out must be a .glb file when --in is a file")
        pairs = [(src, dst)]
    files = []
    res["mode"] = args.mode
    for s, d in pairs:
        try:
            files.append(process(s, d, args.mode))
        except Exception as e:
            files.append({"in": s, "out": d, "ok": False, "error": "%s: %s" % (type(e).__name__, e)})
        if len(pairs) > 1:
            blkit.write_json(args.result, dict(res, ok=False, partial=True, done=len(files), total=len(pairs),
                                               files=files, error="partial: %d of %d files done" % (len(files), len(pairs))))
    res["files"] = files
    if len(files) == 1:
        for k in ("min_y", "center_xz", "bbox_before", "bbox_after", "tris", "textures", "out", "warnings", "error"):
            if k in files[0]:
                res[k] = files[0][k]
    res["failed"] = [f["in"] for f in files if not f["ok"]]
    res["ok"] = not res["failed"]


if __name__ == "__main__":
    blkit.run_script("center_glb_bottom.py", __doc__, add_args, main)
