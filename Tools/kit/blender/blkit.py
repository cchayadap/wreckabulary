"""Shared helpers for the kit's headless Blender 5.x scripts. Run every script through bl_run.mjs.

Script contract (CONTRACTS.md 4.7): arguments after `--` are parsed with argparse, `--result <path>` is required,
one JSON object with an "ok" field is written there atomically, one scene per process, and no GUI Blender is touched.
A crash writes {"ok": false, "error", "traceback"} and re-raises, so `--python-exit-code 1` makes Blender exit 1.
A usage error writes {"ok": false, "usage": true, ...} and exits 2. A policy refusal is {"ok": false, "refused":
"<code>", "error": "<message>"} (refused is the code string; the message is in error).
Exports go to a temporary name first and replace the target in one rename, so a killed run never leaves a
half-written model (or a half-rewritten input) behind.
"""
import argparse
import json
import math
import os
import shutil
import sys
import tempfile
import time
import traceback

import bpy
import numpy as np
from mathutils import Vector

RESULT_MARK = "__RESULT__"
EPS_WEIGHT = 1e-4


class UsageError(Exception):
    pass


class Refused(Exception):
    """A deliberate refusal (bad input for the method, a safety rule). Not a crash."""

    def __init__(self, code, message, **extra):
        super().__init__(message)
        self.code = code
        self.extra = extra


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        raise UsageError(message)


def script_argv(argv=None):
    argv = sys.argv if argv is None else argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def make_parser(prog, description):
    p = _Parser(prog=prog, description=description, add_help=False)
    p.add_argument("--result", required=True, help="where the JSON result is written")
    return p


def _peek_result(argv):
    for i, a in enumerate(argv):
        if a == "--result" and i + 1 < len(argv):
            return argv[i + 1]
        if a.startswith("--result="):
            return a.split("=", 1)[1]
    return None


def jsonable(x):
    """Make a value JSON-safe: Vectors to lists, NaN/inf to None, numpy scalars to Python numbers."""
    if isinstance(x, dict):
        return {str(k): jsonable(v) for k, v in x.items()}
    if isinstance(x, (list, tuple, set)):
        return [jsonable(v) for v in x]
    if isinstance(x, Vector):
        return [jsonable(float(v)) for v in x]
    if isinstance(x, np.ndarray):
        return jsonable(x.tolist())
    if isinstance(x, np.generic):
        return jsonable(x.item())
    if isinstance(x, float):
        return x if math.isfinite(x) else None
    return x


def write_json(path, data):
    """Atomic JSON write (temp file + rename); creates the folder. Path fields become workspace-relative POSIX
    (CONTRACTS 1.1) when the working directory is inside a kit workspace, else absolute POSIX."""
    import riglogic
    path = os.path.abspath(path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = riglogic.present_paths(jsonable(data), riglogic.find_workspace())
    text = json.dumps(data, indent=2, allow_nan=False) + "\n"
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    os.replace(tmp, path)


def run_script(prog, description, add_args, main):
    """Parse args, run main(args, res), write the result. main fills `res` (a dict) and may raise Refused."""
    argv = script_argv()
    result_path = _peek_result(argv)
    parser = make_parser(prog, description)
    add_args(parser)
    t0 = time.time()
    res = {"ok": False, "script": prog, "blender": bpy.app.version_string}
    try:
        args = parser.parse_args(argv)
    except UsageError as e:
        res.update(usage=True, error="usage: %s" % e)
        if result_path:
            write_json(result_path, res)
        print(RESULT_MARK + json.dumps(jsonable(res)))
        sys.stderr.write("usage: %s\n" % e)
        sys.exit(2)
    try:
        main(args, res)
    except UsageError as e:
        res.update(ok=False, usage=True, error="usage: %s" % e, secs=round(time.time() - t0, 2))
        write_json(args.result, res)
        sys.stderr.write("usage: %s\n" % e)
        sys.exit(2)
    except Refused as e:
        res.update(ok=False, refused=e.code, error=str(e))
        res.update(jsonable(e.extra))
    except Exception as e:
        res.update(ok=False, error="%s: %s" % (type(e).__name__, e), traceback=traceback.format_exc()[-4000:])
        res["secs"] = round(time.time() - t0, 2)
        write_json(args.result, res)
        raise
    res["secs"] = round(time.time() - t0, 2)
    write_json(args.result, res)
    print(RESULT_MARK + json.dumps(jsonable({k: res[k] for k in ("ok", "script", "secs") if k in res})))


# ---------------------------------------------------------------------------------------------------------------------
# Operators and I/O

def op_exists(module, name):
    """5.x-safe operator check. hasattr() on bpy.ops is always True, so ask for the RNA type instead."""
    try:
        getattr(getattr(bpy.ops, module), name).get_rna_type()
        return True
    except Exception:
        return False


def op_props(module, name):
    try:
        return set(getattr(getattr(bpy.ops, module), name).get_rna_type().properties.keys())
    except Exception:
        return set()


def call_op(module, name, warnings=None, **kwargs):
    """Call an operator with only the keyword arguments it knows; list dropped keys in `warnings`."""
    known = op_props(module, name)
    use = {k: v for k, v in kwargs.items() if k in known}
    dropped = sorted(set(kwargs) - set(use))
    if dropped and warnings is not None:
        warnings.append("%s.%s ignores unknown options: %s" % (module, name, ", ".join(dropped)))
    return getattr(getattr(bpy.ops, module), name)(**use)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


MODEL_EXTS = (".glb", ".gltf", ".fbx", ".obj", ".stl", ".blend")


def key_frames():
    """Every keyframe position of every action (5.x layered actions, through the slots' channelbags)."""
    from bpy_extras import anim_utils
    frames = set()
    for act in bpy.data.actions:
        for slot in act.slots:
            cb = anim_utils.action_get_channelbag_for_slot(act, slot)
            if cb is None:
                continue
            for fc in cb.fcurves:
                frames.update(round(k.co[0], 4) for k in fc.keyframe_points)
    return sorted(frames)


def import_model(path, warnings=None, match_fps=True):
    """Load a model into the (empty) scene. .blend opens the file. Returns the scene objects.
    glTF stores seconds, and the importer places keys at the scene's frame rate, so a 30 fps clip imported at the
    default 24 fps lands on fractional frames (0.8, 1.6, ...) and FBX baking then drops a frame. With match_fps the
    rate the keys were sampled at is detected and the file is imported again at that rate."""
    path = os.path.abspath(path)
    if not os.path.isfile(path):
        raise FileNotFoundError("input not found: %s" % path)
    ext = os.path.splitext(path)[1].lower()
    objs = _import_once(path, ext)
    if match_fps and ext in (".glb", ".gltf") and bpy.data.actions:
        import riglogic
        sc = bpy.context.scene
        scene_fps = int(round(sc.render.fps / sc.render.fps_base))
        fps = riglogic.detect_fps(key_frames(), scene_fps)
        if fps is not None and fps != scene_fps:
            reset_scene()
            bpy.context.scene.render.fps = int(fps)
            bpy.context.scene.render.fps_base = 1.0
            objs = _import_once(path, ext)
            if warnings is not None:
                warnings.append("animation was sampled at %d fps; imported at %d fps (scene default %d)" % (fps, fps, scene_fps))
    return objs


def _import_once(path, ext):
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        if op_exists("wm", "fbx_import"):
            bpy.ops.wm.fbx_import(filepath=path)
        else:
            bpy.ops.import_scene.fbx(filepath=path)  # b5-ok: pre-5.0 fallback only
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext == ".stl":
        bpy.ops.wm.stl_import(filepath=path)
    elif ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=path)
    else:
        raise ValueError("unsupported input type %s (use %s)" % (ext, ", ".join(MODEL_EXTS)))
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    return list(bpy.context.scene.objects)


# Fixed export settings. Keys a Blender build does not know are dropped and reported (call_op).
FBX_PRESETS = {
    "character": dict(
        use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, bake_anim_step=1.0,
        apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        global_scale=1.0, apply_unit_scale=True, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        use_armature_deform_only=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", path_mode="COPY", embed_textures=False, use_custom_props=False,
    ),
    "prop": dict(
        use_selection=True, object_types={"MESH", "EMPTY"}, add_leaf_bones=False, bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
        global_scale=1.0, apply_unit_scale=True, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        path_mode="COPY", embed_textures=False, use_custom_props=False,
    ),
}

GLB_PRESETS = {
    "character": dict(
        export_format="GLB", use_selection=True, export_yup=True, export_apply=False, export_animations=True,
        export_animation_mode="ACTIONS", export_skins=True, export_influence_nb=4, export_all_influences=False,
        export_morph=True, export_image_format="AUTO", export_materials="EXPORT", export_cameras=False,
        export_lights=False, export_extras=False,
    ),
    "prop": dict(
        export_format="GLB", use_selection=True, export_yup=True, export_apply=True, export_animations=False,
        export_skins=False, export_morph=False, export_image_format="AUTO", export_materials="EXPORT",
        export_cameras=False, export_lights=False, export_extras=False,
    ),
}


def select_only(objs):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]


def same_path(a, b):
    """Two paths name the same file (case-insensitive on Windows; samefile catches links and 8.3 names)."""
    a, b = os.path.abspath(a), os.path.abspath(b)
    if os.path.normcase(a) == os.path.normcase(b):
        return True
    try:
        return os.path.exists(a) and os.path.exists(b) and os.path.samefile(a, b)
    except OSError:
        return False


def add_in_place_arg(p):
    p.add_argument("--in-place", action="store_true", help="allow --out to be the input (replaced in one rename)")


def refuse_out_is_input(out, inputs, in_place=False):
    """Refuse (out_is_input) an --out that names an input file or folder unless --in-place was given."""
    for i in inputs:
        if i and same_path(out, i):
            if in_place:
                return True
            raise Refused("out_is_input", "--out %s is the input %s: write to a new path, or pass --in-place to replace "
                          "it" % (out, i))
    return False


def _move_staged(stage, out_dir, main_name):
    """Move every file the exporter wrote under `stage` to the same relative place in out_dir (textures first, the
    model last). Returns the final paths."""
    moved, last = [], None
    for root, _dirs, names in os.walk(stage):
        rel = os.path.relpath(root, stage)
        dst_root = out_dir if rel == "." else os.path.join(out_dir, rel)
        os.makedirs(dst_root, exist_ok=True)
        for n in names:
            if rel == "." and n == main_name:
                last = (os.path.join(root, n), os.path.join(dst_root, n))
                continue
            os.replace(os.path.join(root, n), os.path.join(dst_root, n))
            moved.append(os.path.join(dst_root, n))
    if last is not None:
        os.replace(*last)
        moved.append(last[1])
    return moved


def export_model(path, objs, preset="prop", warnings=None, fbx_overrides=None, written=None):
    """Export objs with the kit preset to .fbx, .glb, .gltf or .blend (.blend saves a copy of the whole file).
    The exporter writes into a hidden staging folder next to the target (FBX textures land in <stem>.fbm/ there, so
    their relative paths stay right); the files then replace the targets by rename, the model last. A .blend is
    saved under a temporary name in the target folder (its relative paths depend on the folder). The final paths
    of every file written are appended to `written` when given."""
    path = os.path.abspath(path)
    out_dir = os.path.dirname(path)
    os.makedirs(out_dir, exist_ok=True)
    ext = os.path.splitext(path)[1].lower()
    name = os.path.basename(path)
    select_only(objs)
    if ext == ".blend":
        tmp = os.path.join(out_dir, ".%s.gsk-tmp-%d.blend" % (os.path.splitext(name)[0], os.getpid()))
        try:
            bpy.ops.wm.save_as_mainfile(filepath=tmp, copy=True)
            os.replace(tmp, path)
        finally:
            for leftover in (tmp, tmp + "1"):
                if os.path.isfile(leftover):
                    os.remove(leftover)
        if written is not None:
            written.append(path)
        return {"copy": True}
    if ext not in (".fbx", ".glb", ".gltf"):
        raise ValueError("unsupported output type %s (use .fbx, .glb, .gltf or .blend)" % ext)
    stage = tempfile.mkdtemp(prefix=".gsk-tmp-", dir=out_dir)
    try:
        staged = os.path.join(stage, name)
        if ext == ".fbx":
            opts = dict(FBX_PRESETS[preset])
            opts.update(fbx_overrides or {})
            call_op("export_scene", "fbx", warnings, filepath=staged, **opts)
        else:
            opts = dict(GLB_PRESETS[preset])
            if ext == ".gltf":
                opts["export_format"] = "GLTF_SEPARATE"
            call_op("export_scene", "gltf", warnings, filepath=staged, **opts)
        if not os.path.isfile(staged):
            raise RuntimeError("the exporter wrote no %s file" % ext)
        moved = _move_staged(stage, out_dir, name)
    finally:
        shutil.rmtree(stage, ignore_errors=True)
    if written is not None:
        written.extend(moved)
    return opts


# ---------------------------------------------------------------------------------------------------------------------
# Scene queries

def armatures():
    return [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]


def custom_shapes():
    shapes = set()
    for a in armatures():
        for pb in a.pose.bones:
            if pb.custom_shape is not None:
                shapes.add(pb.custom_shape.name)
    return shapes


def meshes():
    """Scene mesh objects, without bone display shapes (the glTF importer adds one)."""
    shapes = custom_shapes()
    return [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name not in shapes]


def armature_of(obj):
    for m in obj.modifiers:
        if m.type == "ARMATURE" and m.object is not None:
            return m.object
    if obj.parent is not None and obj.parent.type == "ARMATURE" and obj.parent_type == "ARMATURE":
        return obj.parent
    return None


def is_bone_parented(obj):
    return obj.parent is not None and obj.parent.type == "ARMATURE" and obj.parent_type == "BONE"


def deform_bone_names(arm):
    return [b.name for b in arm.data.bones if b.use_deform]


def tri_count(objs):
    n = 0
    for o in objs:
        if o.type == "MESH":
            n += sum(max(0, len(p.vertices) - 2) for p in o.data.polygons)
    return n


class rest_pose:
    """Context manager: put armatures in rest position while measuring, restore afterwards."""

    def __init__(self, arms):
        self.arms = list(arms)
        self.saved = []

    def __enter__(self):
        for a in self.arms:
            self.saved.append((a, a.data.pose_position))
            a.data.pose_position = "REST"
        bpy.context.view_layer.update()
        return self

    def __exit__(self, *exc):
        for a, p in self.saved:
            a.data.pose_position = p
        bpy.context.view_layer.update()
        return False


def world_coords(obj, depsgraph=None, evaluated=True):
    """World-space vertex coordinates (N x 3 float64). evaluated=True includes modifiers such as the armature."""
    if evaluated:
        dg = depsgraph or bpy.context.evaluated_depsgraph_get()
        oe = obj.evaluated_get(dg)
        me = oe.to_mesh()
        mw = oe.matrix_world
    else:
        oe = None
        me = obj.data
        mw = obj.matrix_world
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3).astype(np.float64)
    m = np.array(mw, dtype=np.float64)
    out = co @ m[:3, :3].T + m[:3, 3]
    if oe is not None:
        oe.to_mesh_clear()
    return out


def world_bbox(objs, evaluated=True):
    """(min, max) numpy arrays over the objects' vertices, or (None, None)."""
    dg = bpy.context.evaluated_depsgraph_get()
    lo = hi = None
    for o in objs:
        if o.type != "MESH":
            continue
        pts = world_coords(o, dg, evaluated)
        if len(pts) == 0:
            continue
        a, b = pts.min(axis=0), pts.max(axis=0)
        lo = a if lo is None else np.minimum(lo, a)
        hi = b if hi is None else np.maximum(hi, b)
    return lo, hi


def blender_to_gltf(v):
    """Blender (x, y, z) Z-up to glTF/Unity-style (x, y, z) Y-up with +Z forward: (x, z, -y)."""
    return [float(v[0]), float(v[2]), float(-v[1])]


def weight_stats(mesh_objs, limit=4, deform_names=None):
    """Weights over skinned meshes: unweighted verts, max influences, verts over the limit, weight per deform bone."""
    total = unweighted = over = maxinf = 0
    per_bone = {}
    for m in mesh_objs:
        arm = armature_of(m)
        names = set(deform_names if deform_names is not None else (deform_bone_names(arm) if arm else []))
        idx = {vg.index: vg.name for vg in m.vertex_groups if vg.name in names}
        for n in names:
            per_bone.setdefault(n, 0)
        for v in m.data.vertices:
            k = 0
            for g in v.groups:
                name = idx.get(g.group)
                if name is not None and g.weight > EPS_WEIGHT:
                    k += 1
                    per_bone[name] = per_bone.get(name, 0) + 1
            total += 1
            if k == 0:
                unweighted += 1
            if k > limit:
                over += 1
            maxinf = max(maxinf, k)
    return {
        "verts": total, "unweighted": unweighted, "max_influences": maxinf, "verts_over_limit": over,
        "per_bone_verts": per_bone,
    }


def strip_ns(name):
    """'mixamorig:LeftArm' -> 'LeftArm' (FBX namespaces); other names unchanged."""
    return name.split(":")[-1]


def find_bone(arm, *candidates):
    """First bone whose name, or name without namespace, matches a candidate (case-insensitive)."""
    table = {}
    for b in arm.data.bones:
        table.setdefault(b.name.lower(), b)
        table.setdefault(strip_ns(b.name).lower(), b)
    for c in candidates:
        b = table.get(c.lower())
        if b is not None:
            return b
    return None


def bone_world_heads(arm):
    """{bone name: world position of the posed head} for the current frame."""
    mw = arm.matrix_world
    return {pb.name: (mw @ pb.head).copy() for pb in arm.pose.bones}


# ---------------------------------------------------------------------------------------------------------------------
# Actions (Blender 5.x layered actions)

def assign_action(arm, act):
    """Assign an action and a slot (5.x): keeps the first slot for OBJECT ids when none is picked automatically."""
    if arm.animation_data is None:
        arm.animation_data_create()
    ad = arm.animation_data
    ad.action = act
    if act is not None and getattr(ad, "action_slot", None) is None and len(getattr(act, "slots", [])):
        slots = [s for s in act.slots if getattr(s, "target_id_type", "OBJECT") == "OBJECT"] or list(act.slots)
        try:
            ad.action_slot = slots[0]
        except Exception:
            pass


def reset_pose(arm):
    """Every pose bone to rest (location 0, identity rotation, scale 1), so unkeyed channels cannot carry a pose
    from one clip into the next."""
    for pb in arm.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.rotation_axis_angle = (0.0, 0.0, 1.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


class solo_action:
    """Context manager: mute NLA tracks, reset the pose to rest and assign one action; restore afterwards."""

    def __init__(self, arm, act):
        self.arm, self.act = arm, act

    def __enter__(self):
        if self.arm.animation_data is None:
            self.arm.animation_data_create()
        ad = self.arm.animation_data
        self.saved_action = ad.action
        self.saved_mutes = [(t, t.mute) for t in ad.nla_tracks]
        for t, _ in self.saved_mutes:
            t.mute = True
        ad.action = None
        reset_pose(self.arm)
        assign_action(self.arm, self.act)
        return self

    def __exit__(self, *exc):
        ad = self.arm.animation_data
        for t, m in self.saved_mutes:
            t.mute = m
        try:
            ad.action = self.saved_action
        except Exception:
            pass
        return False


def action_paths(act):
    """Every animated data path of an action (5.x layered actions: through the slots' channelbags)."""
    from bpy_extras import anim_utils
    paths = []
    for slot in act.slots:
        cb = anim_utils.action_get_channelbag_for_slot(act, slot)
        if cb is not None:
            paths.extend(fc.data_path for fc in cb.fcurves)
    return paths


def action_range(act):
    a, b = act.frame_range
    return float(a), float(b)


def set_frame(f):
    """Set a (possibly fractional) frame and update the depsgraph."""
    whole = int(math.floor(f))
    bpy.context.scene.frame_set(whole, subframe=float(f - whole))


# ---------------------------------------------------------------------------------------------------------------------
# Rendering (Workbench works headless on this class of machine; Cycles CPU is the fallback)

VIEW_DIRS = {
    # camera position direction from the target centre, and camera rotation (Euler XYZ) looking back at it
    "front": ((0.0, -1.0, 0.0), (math.pi / 2, 0.0, 0.0)),
    "back": ((0.0, 1.0, 0.0), (math.pi / 2, 0.0, math.pi)),
    "side": ((1.0, 0.0, 0.0), (math.pi / 2, 0.0, math.pi / 2)),
    "left": ((-1.0, 0.0, 0.0), (math.pi / 2, 0.0, -math.pi / 2)),
    "top": ((0.0, 0.0, 1.0), (0.0, 0.0, 0.0)),
}


def setup_render(size, engine="BLENDER_WORKBENCH", shading="solid"):
    sc = bpy.context.scene
    sc.render.engine = engine
    sc.render.resolution_x = int(size)
    sc.render.resolution_y = int(size)
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    fmt = sc.render.image_settings
    if "media_type" in fmt.bl_rna.properties:
        fmt.media_type = "IMAGE"  # 5.x: set before file_format
    fmt.file_format = "PNG"
    fmt.color_mode = "RGB"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("GSK_World")
    sc.world.color = (0.62, 0.64, 0.68)
    if engine == "BLENDER_WORKBENCH":
        sh = sc.display.shading
        sh.light = "STUDIO"
        sh.color_type = "TEXTURE" if shading == "texture" else "OBJECT"
        sh.show_shadows = True
        sh.show_cavity = True
        sh.show_object_outline = True
        sh.object_outline_color = (0.05, 0.05, 0.05)
    elif engine == "CYCLES":
        sc.cycles.samples = 8
        sc.cycles.use_denoising = False
        sc.cycles.device = "CPU"
        sun =bpy.data.objects.new("GSK_Sun", bpy.data.lights.new("GSK_Sun", "SUN"))
        sun.rotation_euler = (0.8, 0.2, 0.6)
        sc.collection.objects.link(sun)
    return sc


def ensure_camera(name="GSK_Cam"):
    sc = bpy.context.scene
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start = 0.01
    sc.camera = cam
    return cam


def add_ground(center, extent, z=0.0, name="GSK_Ground"):
    me = bpy.data.meshes.new(name)
    h = float(extent)
    cx, cy = float(center[0]), float(center[1])
    me.from_pydata([(cx - h, cy - h, z), (cx + h, cy - h, z), (cx + h, cy + h, z), (cx - h, cy + h, z)], [], [(0, 1, 2, 3)])
    ob = bpy.data.objects.new(name, me)
    ob.color = (0.35, 0.36, 0.38, 1.0)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def place_camera(cam, view, lo, hi, margin=1.12):
    """Orthographic camera for a named view, framing the box lo..hi."""
    d, rot = VIEW_DIRS[view]
    lo, hi = np.asarray(lo, dtype=float), np.asarray(hi, dtype=float)
    c = (lo + hi) / 2
    span = hi - lo
    if view in ("front", "back"):
        w, h = span[0], span[2]
    elif view in ("side", "left"):
        w, h = span[1], span[2]
    else:
        w, h = span[0], span[1]
    cam.data.ortho_scale = max(float(w), float(h), 1e-3) * margin
    dist = float(np.linalg.norm(span)) * 2 + 1.0
    cam.location = Vector((c[0] + d[0] * dist, c[1] + d[1] * dist, c[2] + d[2] * dist))
    cam.rotation_euler = rot
    cam.data.clip_end = dist * 3


def place_orbit_camera(cam, lo, hi, angle_deg, elevation_deg, margin=1.15):
    lo, hi = np.asarray(lo, dtype=float), np.asarray(hi, dtype=float)
    c = (lo + hi) / 2
    radius = float(np.linalg.norm(hi - lo)) / 2
    cam.data.ortho_scale = max(radius * 2, 1e-3) * margin
    dist = radius * 4 + 1.0
    a, e = math.radians(angle_deg), math.radians(elevation_deg)
    direction = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = Vector(c.tolist()) + direction * dist
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = dist * 3


def render_to(path):
    sc = bpy.context.scene
    sc.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
    if not os.path.isfile(path):
        raise RuntimeError("render wrote no file: %s" % path)
    return path


def render_engine_fallback(size, shading, try_render_path):
    """Workbench first; if it cannot render headless here, Cycles CPU. Returns the engine used."""
    for engine in ("BLENDER_WORKBENCH", "CYCLES"):
        try:
            setup_render(size, engine, shading)
            render_to(try_render_path)
            return engine
        except Exception:
            continue
    raise RuntimeError("no render engine could render headless (tried Workbench and Cycles)")


def load_pixels(path):
    """(H, W, 4) float32 RGBA with row 0 at the top."""
    img = bpy.data.images.load(os.path.abspath(path), check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1]


def save_pixels(path, arr):
    """Write (H, W, 4) float32 RGBA (row 0 at the top) as PNG."""
    h, w = arr.shape[:2]
    img = bpy.data.images.new("GSK_Sheet", w, h, alpha=False)
    img.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).reshape(-1).astype(np.float32))
    img.filepath_raw = os.path.abspath(path)
    img.file_format = "PNG"
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    img.save()
    bpy.data.images.remove(img)
    return path


def compose_grid(cells, rows, cols, cell, gap=2, background=(0.12, 0.12, 0.12)):
    """cells: {(r, c): path}. Returns an (H, W, 4) array with `gap` pixel dividers."""
    height = rows * cell + (rows + 1) * gap
    width = cols * cell + (cols + 1) * gap
    sheet = np.empty((height, width, 4), dtype=np.float32)
    sheet[:, :, :3] = background
    sheet[:, :, 3] = 1.0
    for (r, c), p in cells.items():
        px = load_pixels(p)
        ph, pw = min(px.shape[0], cell), min(px.shape[1], cell)
        y, x = gap + r * (cell + gap), gap + c * (cell + gap)
        sheet[y:y + ph, x:x + pw] = px[:ph, :pw]
    return sheet


def cell_dir(out_path, keep):
    """Folder for per-cell renders: next to the output when kept, else a temp folder."""
    if keep:
        d = os.path.splitext(os.path.abspath(out_path))[0] + "_cells"
        os.makedirs(d, exist_ok=True)
        return d
    return tempfile.mkdtemp(prefix="gsk_cells_")
