"""Report the Blender build and the API facts the kit's scripts depend on (the 5.x breaks), without loading a file.

Run: node Tools/kit/blender/bl_run.mjs probe_api.py
ok is true when the build is 5.x with the layered-action API, the C++ FBX importer and the glTF/FBX exporters;
`has` lists each fact (research_blender_rigging.md 1.5, verified on 5.2.2). Operators are checked through their
RNA type, because hasattr() on bpy.ops is always True in 5.x.
"""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import addon_utils  # noqa: E402
import bpy  # noqa: E402

import blkit  # noqa: E402


def add_args(p):
    pass


def main(args, res):
    has = {
        "action_fcurves": "fcurves" in bpy.types.Action.bl_rna.properties,
        "action_slots": "slots" in bpy.types.Action.bl_rna.properties,
        "action_layers": "layers" in bpy.types.Action.bl_rna.properties,
        "wm_fbx_import": blkit.op_exists("wm", "fbx_import"),
        "import_scene_fbx": blkit.op_exists("import_scene", "fbx"),
        "export_scene_fbx": blkit.op_exists("export_scene", "fbx"),
        "import_scene_gltf": blkit.op_exists("import_scene", "gltf"),
        "export_scene_gltf": blkit.op_exists("export_scene", "gltf"),
        "collada": blkit.op_exists("wm", "collada_export"),  # b5-ok: probes the removal
        "hasattr_ops_always_true": hasattr(bpy.ops.wm, "gsk_no_such_operator_xyz"),  # b5-ok: probes the trap
        "scene_compositing_node_group": "compositing_node_group" in bpy.types.Scene.bl_rna.properties,
        "eevee_next_enum": "BLENDER_EEVEE_NEXT" in  # b5-ok: probes the rename
             [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items],
    }
    try:
        from bpy_extras import anim_utils
        has["channelbag_helpers"] = sorted(n for n in dir(anim_utils) if "channelbag" in n)
    except Exception as e:
        has["channelbag_helpers"] = "error: %s" % e
    try:
        import gpu
        has["gpu_init"] = hasattr(gpu, "init")
    except Exception:
        has["gpu_init"] = False
    mods = {m.__name__ for m in addon_utils.modules()}
    has["addons"] = {n: (n in mods) for n in ("rigify", "io_scene_fbx", "io_scene_gltf2")}
    want_fbx = ["add_leaf_bones", "bake_anim_use_all_actions", "bake_anim_use_nla_strips", "bake_anim_simplify_factor",
                "apply_scale_options", "bake_space_transform", "axis_forward", "axis_up", "use_armature_deform_only",
                "path_mode", "embed_textures"]
    fbx = blkit.op_props("export_scene", "fbx")
    has["fbx_export_options_missing"] = [k for k in want_fbx if k not in fbx]
    gl = blkit.op_props("export_scene", "gltf")
    has["gltf_export_options_missing"] = [k for k in ("export_animation_mode", "export_influence_nb", "export_yup",
                                                        "export_image_format", "export_skins") if k not in gl]
    ver = bpy.app.version
    res.update(version=bpy.app.version_string, version_tuple=list(ver), python=sys.version.split()[0],
               background=bpy.app.background, factory_startup=bpy.app.factory_startup, has=has)
    problems = []
    if ver[0] < 5:
        problems.append("Blender %s is older than 5.0; the kit scripts target 5.x" % bpy.app.version_string)
    if has["action_fcurves"]:
        problems.append("Action.fcurves present: not a 5.x API")
    if not has["wm_fbx_import"] or not has["export_scene_fbx"] or not has["export_scene_gltf"]:
        problems.append("an importer or exporter is missing")
    if has["fbx_export_options_missing"] or has["gltf_export_options_missing"]:
        problems.append("export options the kit presets use are missing")
    res["problems"] = problems
    res["ok"] = not problems


if __name__ == "__main__":
    blkit.run_script("probe_api.py", __doc__, add_args, main)
