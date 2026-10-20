"""Render an animation clip as a frame sheet (rows = views, columns = evenly spaced frames) and measure the pose
on every frame, so reviewers see motion and numbers instead of one still.

Run: node Tools/kit/blender/bl_run.mjs anim_sheet.py -- --in <rigged model> --out <sheet.png>
       [--action <name>] [--views front,side,back] [--frames 8] [--cell 256] [--prop-bone <bone>] [--keep-cells]
Views: front, side (+X), left (-X), back, top. Layout: row r = views[r], column c = frame c (see result `layout`).
Metrics (per frame, and the worst over the clip), from extract_animation.md 7.1 and the 4.3 threshold sheet:
  sole_min_z_m            lowest deformed vertex (ground contact; negative = sinking below the ground)
  spine_lean_deg          pelvis-to-neck pitch relative to the rest pose (+ = forward, the character faces -Y)
  head_pitch_deg          head bone pitch relative to the rest pose
  root_travel_m           horizontal travel of the hips from the first frame (in-place clips should stay near 0)
  loop_seam_err_m         largest bone-head gap between the first and last frame (loops should be near 0)
  prop_head_dist_m        with --prop-bone: distance from the prop bone to the head (weapon over face)
The metrics also go to <sheet>.metrics.json. Workbench renders headless here; Cycles CPU is the fallback.
"""
import math
import os
import shutil
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402

import blkit  # noqa: E402

PELVIS = ("Hips", "pelvis", "hips", "root_pelvis")
NECK = ("Neck", "neck_01", "neck", "Spine2", "spine_03")
HEAD = ("Head", "head")


def add_args(p):
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True, help="sheet PNG")
    p.add_argument("--action", help="clip name (default: the first clip of the armature)")
    p.add_argument("--views", default="front,side,back")
    p.add_argument("--frames", type=int, default=8)
    p.add_argument("--cell", type=int, default=256, help="pixels per cell")
    p.add_argument("--prop-bone")
    p.add_argument("--shading", choices=["solid", "texture"], default="solid")
    p.add_argument("--keep-cells", action="store_true")


def pitch_deg(vec):
    """Pitch of a vector in the sagittal plane: 0 = straight up, + = leaning toward -Y (the character's front)."""
    _, y, z = vec
    return math.degrees(math.atan2(-y, z))


def pick_action(arm, name):
    acts = list(bpy.data.actions)
    if name:
        for a in acts:
            if a.name == name or a.name.split("|")[-1] == name:
                return a
        raise blkit.Refused("no_action", "no clip named %r (have: %s)" % (name, [a.name for a in acts]))
    if arm.animation_data is not None and arm.animation_data.action is not None:
        return arm.animation_data.action
    posed = [a for a in acts if any(p.startswith("pose.bones") for p in blkit.action_paths(a))]
    if not posed:
        raise blkit.Refused("no_action", "the file has no clip that animates the armature")
    return posed[0]


def frame_metrics(arm, mesh_objs, bones, rest, prop):
    mw = arm.matrix_world
    lo, _ = blkit.world_bbox(mesh_objs)
    out = {"sole_min_z_m": None if lo is None else round(float(lo[2]), 5)}
    pb = {k: arm.pose.bones[b.name] if b is not None else None for k, b in bones.items()}
    if pb["pelvis"] is not None and pb["neck"] is not None:
        v = (mw @ pb["neck"].head) - (mw @ pb["pelvis"].head)
        out["spine_lean_deg"] = round(pitch_deg(v) - rest["spine"], 3)
    if pb["head"] is not None:
        v = (mw @ pb["head"].tail) - (mw @ pb["head"].head)
        out["head_pitch_deg"] = round(pitch_deg(v) - rest["head"], 3)
    if pb["pelvis"] is not None:
        h = mw @ pb["pelvis"].head
        out["_pelvis"] = (float(h.x), float(h.y))
    if prop is not None and pb["head"] is not None:
        out["prop_head_dist_m"] = round(float(((mw @ arm.pose.bones[prop.name].head) - (mw @ pb["head"].head)).length), 5)
    return out


def main(args, res):
    warnings = []
    views = [v.strip() for v in args.views.split(",") if v.strip()]
    bad = [v for v in views if v not in blkit.VIEW_DIRS]
    if bad or not views:
        raise blkit.UsageError("unknown view(s) %s; use %s" % (bad, ",".join(blkit.VIEW_DIRS)))
    if not 1 <= args.frames <= 64 or not 32 <= args.cell <= 2048:
        raise blkit.UsageError("--frames 1-64 and --cell 32-2048")
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    arms = blkit.armatures()
    if len(arms) != 1:
        raise blkit.Refused("armature_count", "need exactly one armature, found %d" % len(arms))
    arm = arms[0]
    mesh_objs = [m for m in blkit.meshes() if blkit.armature_of(m) is arm or blkit.is_bone_parented(m)] or blkit.meshes()
    act = pick_action(arm, args.action)
    bones = {"pelvis": blkit.find_bone(arm, *PELVIS), "neck": blkit.find_bone(arm, *NECK),
             "head": blkit.find_bone(arm, *HEAD)}
    prop = blkit.find_bone(arm, args.prop_bone) if args.prop_bone else None
    if args.prop_bone and prop is None:
        raise blkit.Refused("no_prop_bone", "prop bone %r not found" % args.prop_bone)
    rest = {"spine": 0.0, "head": 0.0}
    with blkit.rest_pose([arm]):
        mw = arm.matrix_world
        if bones["pelvis"] is not None and bones["neck"] is not None:
            rest["spine"] = pitch_deg((mw @ bones["neck"].head_local) - (mw @ bones["pelvis"].head_local))
        if bones["head"] is not None:
            rest["head"] = pitch_deg((mw @ bones["head"].tail_local) - (mw @ bones["head"].head_local))

    sc = bpy.context.scene
    fps = sc.render.fps / sc.render.fps_base
    a, b = blkit.action_range(act)
    n = args.frames
    frames = [a + (b - a) * i / n for i in range(n)] if b > a else [a]
    per_frame = []
    lo_all = hi_all = None
    with blkit.solo_action(arm, act):
        blkit.set_frame(a)
        first = blkit.bone_world_heads(arm)
        blkit.set_frame(b)
        last = blkit.bone_world_heads(arm)
        seam = max(((first[k] - last[k]).length for k in first), default=0.0)
        for f in frames:
            blkit.set_frame(f)
            m = frame_metrics(arm, mesh_objs, bones, rest, prop)
            m["frame"] = round(f, 3)
            per_frame.append(m)
            lo, hi = blkit.world_bbox(mesh_objs)
            if lo is not None:
                lo_all = lo if lo_all is None else np.minimum(lo_all, lo)
                hi_all = hi if hi_all is None else np.maximum(hi_all, hi)
        if lo_all is None:
            raise blkit.Refused("no_geometry", "no mesh vertices to render")
        lo_all = np.minimum(lo_all, [lo_all[0], lo_all[1], 0.0])
        span = float(np.linalg.norm(hi_all - lo_all))
        ground = blkit.add_ground(((lo_all + hi_all) / 2).tolist(), span)
        for o in mesh_objs:
            o.color = (0.82, 0.8, 0.76, 1.0)
        cam = blkit.ensure_camera()
        cells_dir = blkit.cell_dir(args.out, args.keep_cells)
        cells = {}
        engine = None
        for c, f in enumerate(frames):
            blkit.set_frame(f)
            for r, view in enumerate(views):
                blkit.place_camera(cam, view, lo_all, hi_all)
                path = os.path.join(cells_dir, "r%d_c%02d_%s.png" % (r, c, view))
                if engine is None:
                    engine = blkit.render_engine_fallback(args.cell, args.shading, path)
                else:
                    blkit.render_to(path)
                cells[(r, c)] = path
        sheet = blkit.compose_grid(cells, len(views), len(frames), args.cell)
        blkit.save_pixels(args.out, sheet)
        bpy.data.objects.remove(ground, do_unlink=True)
    if not args.keep_cells:
        shutil.rmtree(cells_dir, ignore_errors=True)

    p0 = per_frame[0].get("_pelvis")
    for m in per_frame:
        pv = m.pop("_pelvis", None)
        if p0 is not None and pv is not None:
            m["root_travel_m"] = round(math.hypot(pv[0] - p0[0], pv[1] - p0[1]), 5)

    def vals(key):
        return [m[key] for m in per_frame if m.get(key) is not None]

    def agg(key, fn):
        v = vals(key)
        return round(fn(v), 5) if v else None

    sole_min = agg("sole_min_z_m", min)
    summary = {
        "clip": act.name, "frame_range": [a, b], "fps": fps, "duration_s": round((b - a) / fps, 4),
        "sole_min_z_m": sole_min, "sole_max_z_m": agg("sole_min_z_m", max),
        "ground_penetration_m": round(max(0.0, -(sole_min or 0.0)), 5),
        "max_abs_spine_lean_deg": agg("spine_lean_deg", lambda v: max(abs(x) for x in v)),
        "max_abs_head_pitch_deg": agg("head_pitch_deg", lambda v: max(abs(x) for x in v)),
        "max_root_travel_m": agg("root_travel_m", max),
        "loop_seam_err_m": round(float(seam), 5),
        "min_prop_head_dist_m": agg("prop_head_dist_m", min),
        "bones": {k: (v.name if v is not None else None) for k, v in bones.items()},
    }
    if bones["pelvis"] is None or bones["neck"] is None:
        warnings.append("pelvis or neck bone not found: no spine lean metric")
    metrics = {"summary": summary, "frames": per_frame}
    metrics_path = os.path.splitext(os.path.abspath(args.out))[0] + ".metrics.json"
    blkit.write_json(metrics_path, metrics)
    w = len(frames) * args.cell + (len(frames) + 1) * 2
    h = len(views) * args.cell + (len(views) + 1) * 2
    res.update(input=os.path.abspath(args.inp), out=os.path.abspath(args.out), metrics_file=metrics_path,
               layout={"rows": views, "cols": [m["frame"] for m in per_frame], "cell": args.cell, "gap": 2,
                       "size": [w, h]},
               engine=engine, metrics=summary, warnings=warnings, ok=os.path.isfile(args.out))


if __name__ == "__main__":
    blkit.run_script("anim_sheet.py", __doc__, add_args, main)
