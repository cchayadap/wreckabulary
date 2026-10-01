"""Re-import every FBX that build_assets.py wrote and check it against build_report.json.

Run headless after build_assets.py (never alongside another heavy job):
  blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/verify_assets.py -- --repo <Unity project root>

Checks per file: it re-imports, triangle count is unchanged, bounds match within 1 mm,
no camera or light, item files keep their Grip_R marker, every material is in the
library, and the avatar keeps all bones and clips. Writes verify_report.json next to
build_report.json and exits non-zero on any failure.
"""
import argparse
import json
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
BOUNDS_TOLERANCE_M = 0.001


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--repo", required=True)
    p.add_argument("--selection", default=os.path.join(HERE, "selection.json"))
    return p.parse_args(argv)


def unity_bounds(meshes):
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    for obj in meshes:
        mw = obj.matrix_world
        for v in obj.data.vertices:
            w = mw @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    # Same mapping as build_assets.to_unity_space: Blender (x, y, z) lands at Unity (x, z, y).
    return [lo.x, lo.z, lo.y], [hi.x, hi.z, hi.y]


def check(entry, repo, library_names):
    problems = []
    path = entry["output"]
    if not os.path.isabs(path):
        path = os.path.join(repo, path)
    if not os.path.exists(path) or os.path.getsize(path) == 0:
        return {"name": entry["name"], "kind": entry["kind"], "problems": ["missing or empty FBX"]}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = list(bpy.data.objects)
    meshes = [o for o in objs if o.type == "MESH"]
    tris = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        tris += len(obj.data.loop_triangles)
    if tris != entry["triangles"]:
        problems.append(f"triangles {tris} != {entry['triangles']}")
    if any(o.type in {"CAMERA", "LIGHT"} for o in objs):
        problems.append("contains a camera or light")
    lo, hi = unity_bounds(meshes) if meshes else ([0] * 3, [0] * 3)
    for got, want, label in ((lo, entry["bounds_min"], "min"), (hi, entry["bounds_max"], "max")):
        if any(abs(g - w) > BOUNDS_TOLERANCE_M for g, w in zip(got, want)):
            problems.append(f"bounds {label} {[round(g, 4) for g in got]} != {want}")
    if entry["kind"] == "item" and not any(o.name.startswith("Grip_R") for o in objs):
        problems.append("item has no Grip_R marker")
    authored = entry.get("authored_size_xyz")
    if authored:
        size = [h - l for l, h in zip(lo, hi)]
        if any(abs(s - a) > 0.02 for s, a in zip(size, authored)):
            problems.append(f"size {[round(s, 3) for s in size]} differs from authored {authored} by over 2 cm")
    materials = {s.material.name for o in meshes for s in o.material_slots if s.material}
    unknown = sorted(m for m in materials if m not in library_names)
    if unknown:
        problems.append(f"materials missing from materials.json: {unknown}")
    result = {"name": entry["name"], "kind": entry["kind"], "triangles": tris, "problems": problems}
    if entry["kind"] == "avatar":
        rigs = [o for o in objs if o.type == "ARMATURE"]
        bones = sorted(b.name for r in rigs for b in r.data.bones)
        if bones != sorted(entry["bones"]):
            problems.append(f"bones differ: got {len(bones)}, want {len(entry['bones'])}")
        clip_names = sorted(a.name.split("|")[-1] for a in bpy.data.actions)
        want_clips = sorted(entry["action_frames"])
        missing = sorted(set(want_clips) - set(clip_names))
        if missing:
            problems.append(f"clips missing after round trip: {missing}")
        result["clips"] = clip_names
        result["meshes"] = sorted(o.name for o in meshes)
    return result


def main():
    args = parse_args()
    with open(args.selection, encoding="utf-8") as f:
        sel = json.load(f)
    data_dir = os.path.join(args.repo, sel["output"]["data"])
    with open(os.path.join(data_dir, "build_report.json"), encoding="utf-8") as f:
        report = json.load(f)
    with open(os.path.join(data_dir, "materials.json"), encoding="utf-8") as f:
        library_names = {m["name"] for m in json.load(f)["materials"]}
    results = [check(entry, args.repo, library_names) for entry in report["files"]]
    failed = [r for r in results if r["problems"]]
    out = {"checked": len(results), "failed": len(failed), "results": results}
    with open(os.path.join(data_dir, "verify_report.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("VERIFY_RESULT " + json.dumps({"checked": len(results), "failed": len(failed),
                                         "first_failures": failed[:5]}))
    if failed:
        sys.exit(1)


main()
