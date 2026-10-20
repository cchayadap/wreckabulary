"""The rig gate: check a rigged GLB/FBX/.blend against a rig schema and the kit thresholds.

Run: node Tools/kit/blender/bl_run.mjs inspect_rig.py --result ArtSource/rigs/<asset_id>/rig_gate.json -- \
       --in <file> --schema mixamo_22 [--height-range 1.6,1.9]
PASS needs: one armature; every required schema bone; zero unweighted vertices on skinned meshes; at most
max_influences (4) per vertex; every schema `weighted` bone carrying weight; the schema rest pose (T: upper arm
within +-10 deg of horizontal); the height range when given; feet on the ground (min Z about 0).
The operator's {'FINISHED'} is never evidence: only these measurements are.
"""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import blkit  # noqa: E402
import riglogic  # noqa: E402


def add_args(p):
    p.add_argument("--in", dest="inp", required=True, help="rigged model: .glb, .gltf, .fbx or .blend")
    p.add_argument("--schema", required=True, help="rig_schemas/<x>.json, a bare name (mixamo_22), or 'any'")
    p.add_argument("--height-range", help="min,max in metres, e.g. 1.6,1.9")
    p.add_argument("--max-influences", type=int, help="override the schema limit (default 4)")
    p.add_argument("--tpose-tol-deg", type=float, default=riglogic.T_POSE_TOL_DEG)
    p.add_argument("--ground-tol", type=float, help="allowed |min Z| in metres (default max(0.01, 1%% of height))")
    p.add_argument("--no-ground-check", action="store_true", help="skip the feet-at-zero check")
    p.add_argument("--emit-schema", help="also write a schema JSON built from this rig (for identical-rig families)")


def upper_arm_elevation(arm, schema, side):
    """Elevation of shoulder joint -> elbow joint (child head), which is independent of bone roll and axis."""
    pair = (schema.get("upper_arm") or {}).get(side)
    names = pair or (["LeftArm", "LeftForeArm"] if side == "left" else ["RightArm", "RightForeArm"])
    fallbacks = {
        "left": [["upperarm_l", "lowerarm_l"], ["upper_arm.L", "forearm.L"], ["LeftUpperArm", "LeftLowerArm"]],
        "right": [["upperarm_r", "lowerarm_r"], ["upper_arm.R", "forearm.R"], ["RightUpperArm", "RightLowerArm"]],
    }[side]
    for up_name, low_name in [names] + fallbacks:
        up = blkit.find_bone(arm, up_name)
        if up is None:
            continue
        low = blkit.find_bone(arm, low_name)
        mw = arm.matrix_world
        a = mw @ up.head_local
        b = mw @ (low.head_local if low is not None else up.tail_local)
        return riglogic.elevation_deg(tuple(b - a)), up.name
    return None, None


def emit_schema(path, arm, stats):
    bones = [b.name for b in arm.data.bones]
    weighted = [b for b in bones if stats["per_bone_verts"].get(b, 0) > 0]
    blkit.write_json(path, {
        "schema": 1, "name": os.path.splitext(os.path.basename(path))[0], "match": "exact", "rest_pose": "any",
        "max_influences": 4, "bones": bones, "required": bones, "weighted": weighted, "count": len(bones),
        "note": "emitted by inspect_rig.py --emit-schema",
    })


def main(args, res):
    warnings = []
    try:
        schema = riglogic.load_schema(args.schema)
        height_range = riglogic.parse_range(args.height_range) if args.height_range else None
    except (ValueError, OSError) as e:
        raise blkit.UsageError(str(e))
    if args.max_influences is not None and args.max_influences < 1:
        raise blkit.UsageError("--max-influences must be at least 1")
    if args.ground_tol is not None and not args.ground_tol >= 0:
        raise blkit.UsageError("--ground-tol must be >= 0")
    limit = args.max_influences or int(schema.get("max_influences", 4))
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    arms = blkit.armatures()
    mesh_objs = blkit.meshes()
    skinned = [m for m in mesh_objs if blkit.armature_of(m) is not None]
    bone_parented = [m.name for m in mesh_objs if blkit.is_bone_parented(m)]
    loose = [m.name for m in mesh_objs if m not in skinned and not blkit.is_bone_parented(m)]
    failures = []
    res.update(input=os.path.abspath(args.inp), schema=schema["name"], schema_path=schema["path"],
               armatures=len(arms), meshes=len(mesh_objs), skinned_meshes=len(skinned),
               bone_parented_meshes=bone_parented, unskinned_meshes=loose)
    if len(arms) != 1:
        failures.append({"check": "armature_count", "detail": "expected 1 armature, found %d" % len(arms)})
    if not skinned:
        failures.append({"check": "skinned_mesh", "detail": "no mesh is deformed by an armature"})
    if loose and arms:
        failures.append({"check": "unskinned_mesh", "detail": "meshes that neither skin to nor follow a bone: %s" % loose[:10]})
    arm = arms[0] if arms else None
    bone_names = [b.name for b in arm.data.bones] if arm else []
    m = riglogic.match_bones(schema, bone_names)
    res.update(bones=len(bone_names), bone_names=bone_names, missing_bones=m["missing"], extra_bones=m["extra"][:200],
               namespaces=m["namespaces"], deform_bones=len(blkit.deform_bone_names(arm)) if arm else 0)
    if m["missing"]:
        failures.append({"check": "schema_bones", "detail": "%d required bones missing" % len(m["missing"])})

    stats = blkit.weight_stats(skinned, limit) if skinned else {"verts": 0, "unweighted": 0, "max_influences": 0,
                                                                 "verts_over_limit": 0, "per_bone_verts": {}}
    res.update(verts=stats["verts"], unweighted=stats["unweighted"], max_influences=stats["max_influences"],
               verts_over_limit=stats["verts_over_limit"], influence_limit=limit)
    if stats["unweighted"]:
        failures.append({"check": "unweighted", "detail": "%d vertices have no deform weight" % stats["unweighted"]})
    if stats["max_influences"] > limit:
        failures.append({"check": "max_influences", "detail": "%d vertices exceed %d influences" % (stats["verts_over_limit"], limit)})
    optional = {m["mapping"].get(b, b) for b in schema.get("optional_weight", [])}
    no_weight = riglogic.unweighted_schema_bones(schema, m["mapping"], stats["per_bone_verts"])
    res["unweighted_required_bones"] = no_weight
    if no_weight:
        failures.append({"check": "bone_weighted", "detail": "schema bones with no vertex: %s" % no_weight[:10]})
    other_empty = [b for b, n in stats["per_bone_verts"].items()
                   if n == 0 and b not in no_weight and b not in optional and not riglogic.is_leaf_name(b)]
    if other_empty:
        warnings.append("deform bones with no weighted vertex: %s" % sorted(other_empty)[:20])

    rest = {"left": None, "right": None}
    if arm is not None:
        for side in ("left", "right"):
            elev, used = upper_arm_elevation(arm, schema, side)
            rest[side] = {"elevation_deg": None if elev is None else round(elev, 2), "bone": used}
    elevs = [r["elevation_deg"] for r in rest.values() if r and r["elevation_deg"] is not None]
    worst = max(elevs, key=abs) if elevs else None
    rest_pose = riglogic.classify_rest(worst, args.tpose_tol_deg) if elevs else "n/a"
    res.update(rest_pose=rest_pose, upper_arm_elevation_deg=worst, rest_detail=rest, rest_expected=schema["rest_pose"])
    want = schema["rest_pose"]
    if want in ("T", "A") and rest_pose != want:
        failures.append({"check": "rest_pose", "detail": "expected %s-pose rest, measured %s (upper arm %s deg)" % (want, rest_pose, worst)})

    with blkit.rest_pose(arms):
        lo, hi = blkit.world_bbox(skinned or mesh_objs)
    if lo is not None:
        height = float(hi[2] - lo[2])
        res.update(height_m=round(height, 4), min_z=round(float(lo[2]), 4),
                   bbox_min=[round(float(v), 4) for v in lo], bbox_max=[round(float(v), 4) for v in hi])
        if height_range and not (height_range[0] <= height <= height_range[1]):
            failures.append({"check": "height", "detail": "height %.3f m outside %s" % (height, list(height_range))})
        tol = args.ground_tol if args.ground_tol is not None else max(0.01, 0.01 * height)
        res["ground_tol"] = tol
        if not args.no_ground_check and abs(float(lo[2])) > tol:
            failures.append({"check": "grounded", "detail": "min Z %.4f m, expected 0 +- %.3f (run center_glb_bottom.py)" % (float(lo[2]), tol)})
    else:
        res.update(height_m=None, min_z=None)
        failures.append({"check": "geometry", "detail": "no mesh vertices"})

    if args.emit_schema and arm is not None:
        emit_schema(args.emit_schema, arm, stats)
        res["emitted_schema"] = os.path.abspath(args.emit_schema)
    res["failures"] = failures
    res["warnings"] = warnings
    res["verdict"] = riglogic.gate_verdict(failures)
    res["ok"] = res["verdict"] == "PASS"


if __name__ == "__main__":
    blkit.run_script("inspect_rig.py", __doc__, add_args, main)
