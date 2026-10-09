"""Author a separate Generic-rig emote; never rewrite the source Avatar FBX."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import author_clips as core

FPS, LAST_FRAME, NAME = 30, 72, "WinterShuffle"
SIDES = ("L", "R")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def vec(v):
    return [round(float(x), 6) for x in v]


def paths(armature):
    result = {}
    for bone in armature.data.bones:
        chain = [bone.name]
        parent = bone.parent
        while parent:
            chain.append(parent.name)
            parent = parent.parent
        result[bone.name] = "/".join(reversed(chain))
    return result


def fk(rig, local, locations):
    points, rotations = {}, {}
    for name in rig.names:
        parent = rig.parent[name]
        parent_q = rotations.get(parent, Quaternion())
        offset = rig.rest[name] - rig.rest[parent] if parent else rig.rest[name]
        points[name] = points.get(parent, Vector()) + parent_q @ (offset + locations.get(name, Vector()))
        rotations[name] = parent_q @ local.get(name, Quaternion())
    return points, rotations


def solve_arm(rig, local, locations, side, hand):
    points, rotations = fk(rig, local, locations)
    upper, lower, wrist = (name + "_" + side for name in ("upper_arm", "forearm", "hand"))
    shoulder = points[upper]
    rest_upper = rig.rest[lower] - rig.rest[upper]
    rest_lower = rig.rest[wrist] - rig.rest[lower]
    l1, l2 = rest_upper.length, rest_lower.length
    delta = hand - shoulder
    reach = (l1 + l2) * .9995
    miss = max(0.0, delta.length - reach)
    distance = min(reach, max(delta.length, abs(l1 - l2) + .0001))
    axis = delta.normalized()
    sign = 1 if side == "L" else -1
    pole = Vector((sign, -.5, -.15))
    bend = (pole - axis * pole.dot(axis)).normalized()
    along = (l1*l1 - l2*l2 + distance*distance) / (2*distance)
    elbow = shoulder + axis * along + bend * math.sqrt(max(0, l1*l1 - along*along))
    wrist_point = shoulder + axis * distance
    normal = axis.cross(bend).normalized()
    rest_normal = rest_upper.cross(rest_lower).normalized()
    upper_q = core._align(rest_upper, rest_normal, elbow - shoulder, normal)
    lower_q = core._align(rest_lower, rest_normal, wrist_point - elbow, normal)
    local[upper] = rotations[rig.parent[upper]].inverted() @ upper_q
    local[lower] = upper_q.inverted() @ lower_q
    local[wrist] = Quaternion()
    return miss


def dance_keys():
    neutral = core.pose(shift=(0, -.004, 0))
    def step(x=0, drop=.022, left=(0, 0, 0, 0), right=(0, 0, 0, 0), lean=0):
        return core.pose(pelvis=(1, 0, lean), spine=(0, 0, -lean*.65), chest=(-2, 0, -lean*.35),
            head=(-8, -lean*.6, 0), shift=(x, -drop, 0), foot_l=left, foot_r=right)
    return [(0, neutral, "lin"),
            (5, step(-.014, .023, lean=2), "io"),
            (10, step(.010, .020, left=(.022, .035, 0, 0), lean=-2), "io"),
            (15, step(.028, .016, left=(.040, 0, 0, 0), lean=-3), "io"),
            (22, step(.032, .031, left=(.040, 0, 0, 0), lean=-2), "io"),
            (26, step(.025, .016, left=(.040, 0, 0, 0), lean=-2), "io"),
            (31, step(.008, .022, left=(.020, .028, 0, 0)), "io"),
            (35, step(0, .025), "io"),
            (40, step(-.010, .020, right=(-.022, .035, 0, 0), lean=2), "io"),
            (45, step(-.028, .016, right=(-.040, 0, 0, 0), lean=3), "io"),
            (50, step(-.032, .031, right=(-.040, 0, 0, 0), lean=2), "io"),
            (54, step(-.025, .016, right=(-.040, 0, 0, 0), lean=2), "io"),
            (59, step(-.008, .022, right=(-.020, .028, 0, 0)), "io"),
            (64, step(0, .016), "io"), (72, neutral, "io")]


def lerp_keys(frame, keys):
    for (a, av), (b, bv) in zip(keys, keys[1:]):
        if a <= frame <= b:
            return av + (bv-av) * core.smoothstep(a, b, frame)
    return keys[-1][1]


def sample(rig, frame):
    local, shift, foot_miss = core.keyed(rig, dance_keys(), frame)
    locations = {"pelvis": shift}
    raised = core.smoothstep(3, 16, frame) * (1-core.smoothstep(56, 70, frame))
    openness = lerp_keys(frame, [(0, 1), (17, 1), (22, 0), (26, 1), (45, 1), (50, 0), (54, 1), (72, 1)])
    baseline = {name: q.copy() for name, q in local.items()}
    arm_miss = 0
    for side in SIDES:
        sign = 1 if side == "L" else -1
        locations["clavicle_" + side] = Vector((0, .050 * raised, 0))
        target = Vector((shift.x + sign * (.050 + .100 * openness), .630 + shift.y + .010 * (1-openness), .177-.030*openness))
        arm_miss = max(arm_miss, solve_arm(rig, local, locations, side, target))
        for part in ("upper_arm", "forearm", "hand"):
            name = part + "_" + side
            local[name] = baseline.get(name, Quaternion()).slerp(local[name], raised)
    return local, locations, foot_miss, arm_miss * raised


def set_pose(rig, frame):
    local, locations, foot_miss, arm_miss = sample(rig, frame)
    for name in rig.names:
        bone = rig.obj.pose.bones[name]
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = local.get(name, Quaternion())
        bone.location = locations.get(name, Vector())
        bone.scale = (1, 1, 1)
    return foot_miss, arm_miss


def author(rig):
    action = bpy.data.actions.new(NAME)
    action.use_fake_user = True
    rig.obj.animation_data_create().action = action
    previous = {}
    for frame in range(LAST_FRAME+1):
        set_pose(rig, frame)
        for name in rig.names:
            bone = rig.obj.pose.bones[name]
            q = bone.rotation_quaternion.copy()
            if name in previous and previous[name].dot(q) < 0: q = -q
            previous[name] = q.copy()
            bone.rotation_quaternion = q
            for path in ("rotation_quaternion", "location", "scale"):
                bone.keyframe_insert(path, frame=frame, group=name)
    for curve in core._curves(action):
        for key in curve.keyframe_points: key.interpolation = "LINEAR"
    return action


def evaluated_points(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    result = [evaluated.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
    return result


def validate(rig):
    checks, sampled = [], {}
    boots = bpy.data.objects["SK_Boots"]
    names = ("root", "pelvis", "head", "hand_L", "hand_R", "shin_L", "shin_R", "foot_L", "foot_R")
    for frame in range(LAST_FRAME+1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        pts = {name: rig.obj.matrix_world @ rig.obj.pose.bones[name].matrix.translation for name in names}
        sole = min(p.z for p in evaluated_points(boots))
        foot_miss, arm_miss = sample(rig, frame)[2:]
        checks.append({"frame": frame, "root": vec(pts["root"]), "sole_min_m": round(sole, 6),
                       "leg_ik_error_m": round(foot_miss, 6), "arm_ik_error_m": round(arm_miss, 6),
                       "wrist_gap_m": round((pts["hand_L"]-pts["hand_R"]).length, 6)})
        if frame in (0, 10, 17, 22, 26, 40, 45, 50, 54, 64, 72):
            sampled[str(frame)] = {name: vec(point) for name, point in pts.items()}
    root_drift = max(Vector(c["root"]).length for c in checks)
    floor_error = max(abs(c["sole_min_m"]-rig.ground) for c in checks)
    leg_error = max(c["leg_ik_error_m"] for c in checks)
    arm_error = max(c["arm_ik_error_m"] for c in checks)
    neutral_error = max((Vector(sampled["0"][n])-Vector(sampled["72"][n])).length for n in names)
    report = {"root_drift_m": root_drift, "support_foot_floor_error_m": floor_error,
              "max_leg_ik_error_m": leg_error, "max_arm_ik_error_m": arm_error,
              "neutral_return_error_m": neutral_error, "samples_world_blender_z_up": sampled, "frames": checks}
    if root_drift > .0001 or floor_error > .004 or leg_error > .004 or arm_error > .004 or neutral_error > .0001:
        raise RuntimeError("Motion verification failed: " + json.dumps({k:v for k,v in report.items() if not isinstance(v, (list, dict))}))
    return report


def export(armature, path):
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"ARMATURE"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=False, armature_nodetype="NULL", bake_anim=True,
        bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1, bake_anim_simplify_factor=0)


def roundtrip(path, expected_paths, expected_samples):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    if paths(armature) != expected_paths: raise RuntimeError("Roundtrip bone hierarchy differs")
    if len(bpy.data.actions) != 1: raise RuntimeError("Export must contain exactly one skeleton action")
    if any(obj.type != "ARMATURE" for obj in bpy.data.objects): raise RuntimeError("Skeleton export contains unexpected objects")
    action = bpy.data.actions[0]
    start, end = action.frame_range
    if abs((end-start)/FPS - 2.4) > .0001: raise RuntimeError("Roundtrip clip duration changed")
    worst = 0
    for frame, points in expected_samples.items():
        bpy.context.scene.frame_set(int(round(start))+int(frame))
        bpy.context.view_layer.update()
        for name, expected in points.items():
            observed = armature.matrix_world @ armature.pose.bones[name].matrix.translation
            worst = max(worst, (observed-Vector(expected)).length)
    if worst > .0002: raise RuntimeError(f"Roundtrip joint error {worst:.6f} m")
    return {"armature": armature.name, "bones": len(armature.data.bones), "actions": [action.name],
            "frames": [start, end], "duration_seconds": (end-start)/FPS, "max_joint_error_m": worst}


def preview(outdir):
    bpy.ops.wm.open_mainfile(filepath=str(outdir / (NAME + ".blend")))
    scene = bpy.context.scene
    engines = scene.render.bl_rna.properties["engine"].enum_items.keys()
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 32
    scene.render.resolution_x = 720; scene.render.resolution_y = 900; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if not scene.world: scene.world = bpy.data.worlds.new("Preview world")
    scene.world.color = (.22, .22, .22)
    scene.view_settings.view_transform = "AgX"
    project = Path(__file__).resolve().parents[2]
    specs = json.loads((project / "Assets/_Project/Data/Generated/materials.json").read_text(encoding="utf-8"))["materials"]
    for spec in specs:
        material = bpy.data.materials.get(spec["name"])
        if not material: continue
        material.use_nodes = True
        material.node_tree.nodes.clear()
        shader = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        shader.inputs["Base Color"].default_value = spec["baseColor"]
        shader.inputs["Roughness"].default_value = spec["roughness"]
        shader.inputs["Metallic"].default_value = spec["metallic"]
        base_map = spec.get("baseMap")
        if base_map and (project / base_map).is_file():
            texture = material.node_tree.nodes.new("ShaderNodeTexImage")
            texture.image = bpy.data.images.load(str(project / base_map), check_existing=True)
            multiply = material.node_tree.nodes.new("ShaderNodeMixRGB")
            multiply.blend_type = "MULTIPLY"
            multiply.inputs[0].default_value = 1
            multiply.inputs[2].default_value = spec["baseColor"]
            material.node_tree.links.new(texture.outputs["Color"], multiply.inputs[1])
            material.node_tree.links.new(multiply.outputs["Color"], shader.inputs["Base Color"])
        output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
        material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    for obj in bpy.data.objects:
        if obj.type == "MESH": obj.hide_render = obj.name in ("SK_Crewneck", "SK_Cap", "SK_Glasses")
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.002))
    floor = bpy.context.object
    floor.name = "Preview floor"
    mat = bpy.data.materials.new("Preview oat floor"); mat.diffuse_color = (.58, .47, .32, 1)
    floor.data.materials.append(mat)
    for name, location, power, size in (("Key", (-1.5, 2, 2.5), 110, 2), ("Fill", (2, 1, 1.5), 65, 2), ("Rim", (0, -1.5, 2), 80, 1.5)):
        light = bpy.data.lights.new(name, "AREA"); light.energy = power; light.shape = "DISK"; light.size = size
        obj = bpy.data.objects.new(name, light); scene.collection.objects.link(obj); obj.location = location
        obj.rotation_euler = (Vector((0, 0, .6))-obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("Preview camera", bpy.data.cameras.new("Preview camera")); scene.collection.objects.link(camera)
    camera.location = (-.6, 2.5, 1.22)
    camera.rotation_euler = (Vector((0, 0, .53))-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"; camera.data.ortho_scale = 1.35; scene.camera = camera
    renders = []
    for frame in (0, 10, 17, 22, 40, 50, 72):
        scene.frame_set(frame)
        scene.render.filepath = str(outdir / f"WinterShuffle-{frame:02}.png")
        bpy.ops.render.render(write_still=True)
        renders.append(Path(scene.render.filepath).name)
    return renders


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--render", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    source, outdir = Path(args.source).resolve(), Path(args.output).resolve()
    project = Path(__file__).resolve().parents[2]
    if (project / "Assets") == outdir or (project / "Assets") in outdir.parents:
        raise RuntimeError("Winter authoring must stay outside Assets")
    outdir.mkdir(parents=True, exist_ok=True)
    source_sha = sha(source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    for image in bpy.data.images:
        filename = Path(image.filepath.replace("\\", "/")).name
        texture = project / "Assets/_Project/Art/Imported/Textures" / filename
        if texture.is_file():
            image.filepath = str(texture)
            image.reload()
            image.pack()
    for obj in bpy.data.objects:
        obj.animation_data_clear()
        if obj.type == "MESH" and obj.data.shape_keys:
            obj.data.shape_keys.animation_data_clear()
            for key in obj.data.shape_keys.key_blocks: key.value = 0
    for action in list(bpy.data.actions): bpy.data.actions.remove(action)
    armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    for bone in armature.pose.bones:
        bone.location = (0, 0, 0); bone.rotation_mode = "QUATERNION"; bone.rotation_quaternion = Quaternion(); bone.scale = (1, 1, 1)
    rig = core.Rig(armature)
    if len(rig.names) != 22: raise RuntimeError("Expected existing 22-bone Generic rig")
    scene = bpy.context.scene
    scene.name = NAME
    scene.render.fps = FPS; scene.render.fps_base = 1; scene.frame_start = 0; scene.frame_end = LAST_FRAME
    action = author(rig)
    report = {"name": NAME, "version": 1, "status": "authored-not-unity-imported", "source_avatar": str(source.relative_to(project)),
        "source_avatar_sha256": source_sha, "generator": str(Path(__file__).resolve().relative_to(project)),
        "generator_sha256": sha(__file__), "blender": bpy.app.version_string, "fps": FPS, "duration_seconds": 2.4,
        "authoring": "Original local keyframes and two-bone IK; existing avatar geometry/rig is reference only.",
        "design": "Two side steps, soft knee bounce, two raised front-of-face mitten claps, planted neutral return.",
        "rig_adaptation": "Rig arms are 0.241m long from 0.549m shoulders; head top is 0.988m. Raised front claps replace unreachable overhead contact without stretching arms.",
        "clap_frames": [22, 50], "armature": armature.name, "bone_paths_relative_to_armature": paths(armature),
        "motion": validate(rig)}
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=str(outdir / (NAME + ".blend")))
    fbx = outdir / (NAME + ".fbx")
    export(armature, fbx)
    report["roundtrip"] = roundtrip(fbx, report["bone_paths_relative_to_armature"], report["motion"]["samples_world_blender_z_up"])
    report["preview_materials"] = "Existing generated material colours and base textures; Blender lighting is only a pose preview, not the native Unity look."
    if args.render: report["previews"] = preview(outdir)
    if sha(source) != source_sha: raise RuntimeError("Original Avatar FBX changed during authoring")
    report["files"] = {p.name: {"sha256": sha(p), "bytes": p.stat().st_size} for p in outdir.iterdir() if p.suffix in (".fbx", ".blend", ".png")}
    (outdir / "manifest.json").write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8")
    print("WINTER_EMOTE="+json.dumps({"status":report["status"], "roundtrip":report["roundtrip"], "output":str(outdir)}))


if __name__ == "__main__": main()
