"""Render a turntable of a model (camera orbiting the model) as numbered PNG frames, plus an optional contact sheet.

Run: node Tools/kit/blender/bl_run.mjs turntable.py -- --in <model> --out <dir> [--frames 24] [--size 512]
       [--elevation 15] [--sheet <png>] [--cols 8] [--shading solid|texture] [--rest]
Frames are turntable_000.png ... in --out; frame i looks from azimuth 360*i/frames degrees (0 = the front, -Y).
Earlier turntable_NNN.png frames in --out are removed first (only those names), so a folder always holds one run.
The model is framed once for the whole orbit, so its size reads the same in every frame. With --rest the rig is
shown in rest pose (a posed first frame otherwise). Animated GIFs are game-capture-trailer's frames_to_gif.py.
"""
import os
import re
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402

import blkit  # noqa: E402


def add_args(p):
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True, help="output folder")
    p.add_argument("--frames", type=int, default=24)
    p.add_argument("--size", type=int, default=512)
    p.add_argument("--elevation", type=float, default=15.0, help="camera elevation in degrees")
    p.add_argument("--sheet", help="also write a contact sheet PNG")
    p.add_argument("--cols", type=int, default=8)
    p.add_argument("--shading", choices=["solid", "texture"], default="texture")
    p.add_argument("--rest", action="store_true", help="show rigs in rest pose")


FRAME_RE = re.compile(r"^turntable_[0-9]{3}\.png$")


def clear_old_frames(out):
    """Remove earlier runs' frames (turntable_NNN.png only) so globbing the folder never mixes two runs."""
    removed = []
    for n in sorted(os.listdir(out)):
        p = os.path.join(out, n)
        if FRAME_RE.match(n) and os.path.isfile(p):
            os.remove(p)
            removed.append(n)
    return removed


def main(args, res):
    warnings = []
    if not 1 <= args.frames <= 360 or not 32 <= args.size <= 4096:
        raise blkit.UsageError("--frames 1-360 and --size 32-4096")
    if not 1 <= args.cols <= 64:
        raise blkit.UsageError("--cols must be 1-64")
    if os.path.isfile(args.out):
        raise blkit.UsageError("--out must be a folder, not a file: %s" % args.out)
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    mesh_objs = blkit.meshes()
    if not mesh_objs:
        raise blkit.Refused("no_mesh", "nothing to render")
    arms = blkit.armatures()
    if args.rest:
        for a in arms:
            a.data.pose_position = "REST"
        bpy.context.view_layer.update()
    lo, hi = blkit.world_bbox(mesh_objs)
    lo = np.minimum(lo, [lo[0], lo[1], 0.0])
    ground = blkit.add_ground(((lo + hi) / 2).tolist(), float(np.linalg.norm(hi - lo)))
    for o in mesh_objs:
        if tuple(o.color) == (1.0, 1.0, 1.0, 1.0):
            o.color = (0.82, 0.8, 0.76, 1.0)
    cam = blkit.ensure_camera()
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    cleared = clear_old_frames(out)
    if cleared:
        warnings.append("removed %d turntable frames of an earlier run from --out" % len(cleared))
    res["cleared_frames"] = len(cleared)
    frames = []
    engine = None
    for i in range(args.frames):
        angle = 360.0 * i / args.frames
        blkit.place_orbit_camera(cam, lo, hi, angle, args.elevation)
        path = os.path.join(out, "turntable_%03d.png" % i)
        if engine is None:
            engine = blkit.render_engine_fallback(args.size, args.shading, path)
        else:
            blkit.render_to(path)
        frames.append(path)
    bpy.data.objects.remove(ground, do_unlink=True)
    sheet = None
    if args.sheet:
        cols = max(1, min(args.cols, len(frames)))
        rows = (len(frames) + cols - 1) // cols
        cells = {(i // cols, i % cols): p for i, p in enumerate(frames)}
        blkit.save_pixels(args.sheet, blkit.compose_grid(cells, rows, cols, args.size))
        sheet = os.path.abspath(args.sheet)
    res.update(input=os.path.abspath(args.inp), out=out, frames=frames, sheet=sheet, engine=engine,
               elevation_deg=args.elevation, size=args.size, tris=blkit.tri_count(mesh_objs),
               bbox_min=[round(float(v), 4) for v in lo], bbox_max=[round(float(v), 4) for v in hi],
               warnings=warnings, ok=len(frames) == args.frames and all(os.path.isfile(f) for f in frames))


if __name__ == "__main__":
    blkit.run_script("turntable.py", __doc__, add_args, main)
