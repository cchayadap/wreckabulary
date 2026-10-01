"""Convert the chosen source-pack GLBs into Unity-ready FBX files and a material library.

Run headless, as the only heavy job on the machine:
  blender -b --factory-startup --python Tools/AssetPipeline/build_assets.py -- \
      --packs <folder holding vault-v2/ and quality-pass-01/> --repo <Unity project root> \
      [--only items,letters,environment,vfx,avatar]

Outputs (paths come from selection.json):
  <art>/Items/<WORD>.fbx, <art>/Letters/Tile_<L>.fbx, <art>/Environment/<id>.fbx,
  <art>/VFX/<id>.fbx, <art>/Avatar/Avatar.fbx, <art>/Textures/*.png
  <data>/materials.json   material library read by the Unity importer (MaterialLibraryBuilder)
  <data>/build_report.json per-file facts that verify_assets.py checks the FBX files against

Materials are read from the glTF JSON (the authored values), not from Blender's shader
nodes. The FBX files carry material names only; Unity binds them to library materials.
"""
import argparse
import hashlib
import json
import os
import re
import struct
import sys
import tempfile
import time

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from gltf_read import Glb  # noqa: E402

SKIN_SUFFIX = re.compile(r"^(?P<family>.+)_(?P<skin>Classic|Candy|Arcade)$")
DUP_SUFFIX = re.compile(r"^(?P<base>.+)\.\d{3}$")
FLOOR_TOLERANCE_M = 0.0005


# --------------------------------------------------------------------------- args


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--packs", required=True)
    p.add_argument("--repo", required=True)
    p.add_argument("--only", default="items,letters,environment,vfx,avatar")
    p.add_argument("--selection", default=os.path.join(HERE, "selection.json"))
    return p.parse_args(argv)


# --------------------------------------------------------------------------- material library


def png_size(data):
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", data[16:24])


def safe_name(name):
    stem = os.path.splitext(name)[0]
    return re.sub(r"[^A-Za-z0-9_-]", "_", stem) or "texture"


def close(a, b, tol=1e-4):
    if isinstance(a, (list, tuple)):
        return len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))
    return abs(a - b) <= tol


class Library:
    """Materials keyed by name, plus the textures they use (deduplicated)."""

    PARAMS = ("baseColor", "metallic", "roughness", "emissive")

    def __init__(self, max_size):
        self.max_size = max_size
        self.materials = {}
        self.textures = {}
        self.conflicts = []

    def add_glb(self, glb, owner):
        for m in glb.materials():
            entry = {k: v for k, v in m.items() if not k.startswith("_")}
            entry["baseMap"] = self._texture(glb, m["_baseImage"])
            entry["normalMap"] = self._texture(glb, m["_normalImage"])
            entry["emissionMap"] = self._texture(glb, m["_emissiveImage"])
            match = SKIN_SUFFIX.match(entry["name"])
            entry["family"] = match.group("family") if match else entry["name"]
            entry["skin"] = match.group("skin") if match else None
            known = self.materials.get(entry["name"])
            if known is None:
                entry["sources"] = [owner]
                self.materials[entry["name"]] = entry
                continue
            if owner not in known["sources"]:
                known["sources"].append(owner)
            diffs = [k for k in self.PARAMS if not close(known[k], entry[k])]
            diffs += [k for k in ("alphaMode", "baseMap", "normalMap", "emissionMap") if known[k] != entry[k]]
            if diffs:
                self.conflicts.append({"material": entry["name"], "kept": known["sources"][0],
                                       "ignored": owner, "fields": diffs})

    def _texture(self, glb, image_index):
        if image_index is None:
            return None
        data = glb.image_bytes(image_index)
        name = safe_name(glb.image_name(image_index))
        size = png_size(data)
        digest = hashlib.sha256(data).hexdigest()
        known = self.textures.get(name)
        if known is None or known["sha256"] == digest:
            if known is None:
                self.textures[name] = {"bytes": data, "size": size, "sha256": digest, "source": glb.path}
            return name
        # Same name, different pixels. The avatar packs ship one image at 256 px and 1024 px:
        # keep the larger copy. Anything else is a real clash and gets its own name.
        if size and known["size"] and size != known["size"]:
            if size[0] * size[1] > known["size"][0] * known["size"][1]:
                self.textures[name] = {"bytes": data, "size": size, "sha256": digest, "source": glb.path}
            return name
        alt = f"{name}_{digest[:8]}"
        self.textures.setdefault(alt, {"bytes": data, "size": size, "sha256": digest, "source": glb.path})
        return alt

    def write(self, art_dir, data_dir, art_rel):
        tex_dir = os.path.join(art_dir, "Textures")
        os.makedirs(tex_dir, exist_ok=True)
        written = {}
        for name, tex in sorted(self.textures.items()):
            dest = os.path.join(tex_dir, name + ".png")
            size = tex["size"]
            if size and max(size) <= self.max_size:
                with open(dest, "wb") as f:
                    f.write(tex["bytes"])
                final = size
            else:
                final = downscale_png(tex["bytes"], dest, self.max_size)
            written[name] = {"file": f"{art_rel}/Textures/{name}.png", "size": list(final),
                             "source_size": list(size) if size else None}
        materials = []
        for name in sorted(self.materials):
            m = dict(self.materials[name])
            for key in ("baseMap", "normalMap", "emissionMap"):
                if m[key]:
                    m[key] = written[m[key]]["file"]
            materials.append(m)
        payload = {"generator": "Tools/AssetPipeline/build_assets.py", "materials": materials,
                   "textures": written, "conflicts": self.conflicts}
        with open(os.path.join(data_dir, "materials.json"), "w", encoding="utf-8") as f:
            json.dump(payload, f, indent=1)
        return payload


def downscale_png(data, dest, max_size):
    """Resize with Blender's image API (no extra Python packages needed)."""
    fd, tmp = tempfile.mkstemp(suffix=".png")
    os.close(fd)
    try:
        with open(tmp, "wb") as f:
            f.write(data)
        img = bpy.data.images.load(tmp)
        w, h = img.size
        scale = max_size / float(max(w, h))
        nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
        img.scale(nw, nh)
        img.filepath_raw = dest
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        return (nw, nh)
    finally:
        os.remove(tmp)


# --------------------------------------------------------------------------- scene helpers


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def remove_cameras_and_lights():
    removed = 0
    for obj in list(bpy.data.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1
    return removed


def strip_animation():
    for obj in bpy.data.objects:
        if obj.animation_data:
            obj.animation_data_clear()
        data = getattr(obj, "data", None)
        keys = getattr(data, "shape_keys", None) if data is not None else None
        if keys is not None and keys.animation_data:
            keys.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def restore_rest_transforms(glb):
    """Put objects back to the authored node transforms.

    The importer evaluates the first frame of any object clip (V2 items start their
    Assemble_Preview clip at ~22% scale), and clearing the animation keeps that pose.
    glTF is Y-up and Blender Z-up, so (x, y, z) maps to (x, -z, y).
    """
    for node in glb.json.get("nodes", []):
        obj = bpy.data.objects.get(node.get("name", ""))
        if obj is None or obj.type == "ARMATURE" or "matrix" in node:
            continue
        t = node.get("translation", [0.0, 0.0, 0.0])
        r = node.get("rotation", [0.0, 0.0, 0.0, 1.0])
        s = node.get("scale", [1.0, 1.0, 1.0])
        obj.location = (t[0], -t[2], t[1])
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = (r[3], r[0], -r[2], r[1])
        obj.scale = (s[0], s[2], s[1])
    bpy.context.view_layer.update()


def mesh_objects():
    return [o for o in bpy.data.objects if o.type == "MESH"]


def world_bounds(objs):
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    for obj in objs:
        mw = obj.matrix_world
        for v in obj.data.vertices:
            w = mw @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def triangle_count(objs):
    total = 0
    for obj in objs:
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def snap_to_floor():
    """Move root objects so the lowest vertex sits on z = 0 (Blender is Z-up)."""
    meshes = mesh_objects()
    lo, _ = world_bounds(meshes)
    dz = -lo.z
    if abs(dz) <= FLOOR_TOLERANCE_M:
        return 0.0
    for obj in bpy.data.objects:
        if obj.parent is None:
            obj.location.z += dz
    bpy.context.view_layer.update()
    return dz


def to_unity_space(v):
    """Where Blender (x, y, z) lands in Unity: (x, z, y).

    Measured, not assumed: Unity reads the "-Z forward" these files declare and turns them to
    face its own +Z, which puts Blender +Y at Unity +Z. See face_unity_forward.
    """
    return [round(v.x, 5), round(v.z, 5), round(v.y, 5)]


# A half turn about Blender's Z (up) axis, written out so it's exact.
HALF_TURN_Z = Matrix(((-1, 0, 0, 0), (0, -1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))


def face_unity_forward():
    """Turn the scene a half turn about Z, so models face Unity's +Z.

    The packs follow glTF, where a model's front is +Z; in Blender that is -Y. Unity maps
    Blender +Y to +Z (see to_unity_space), so without this every model faces backwards.
    Root objects are turned about the origin; children follow.
    """
    for obj in bpy.data.objects:
        if obj.parent is None:
            obj.matrix_world = HALF_TURN_Z @ obj.matrix_world
    bpy.context.view_layer.update()


def describe(kind, name, source, output):
    meshes = mesh_objects()
    lo, hi = world_bounds(meshes)
    return {
        "kind": kind,
        "name": name,
        "source": source.replace("\\", "/"),
        "output": output.replace("\\", "/"),
        "triangles": triangle_count(meshes),
        "meshes": sorted(o.name for o in meshes),
        "materials": sorted({s.material.name for o in meshes for s in o.material_slots if s.material}),
        "empties": sorted(o.name for o in bpy.data.objects if o.type == "EMPTY"),
        "bounds_min": to_unity_space(lo),
        "bounds_max": to_unity_space(hi),
        "actions": sorted(a.name for a in bpy.data.actions),
    }


def export_fbx(path, animated):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"EMPTY", "MESH", "ARMATURE"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=False,
        use_custom_props=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=animated,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=animated,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
        embed_textures=False,
    )
    return os.path.getsize(path)


# --------------------------------------------------------------------------- static jobs


def run_static(kind, name, src, out_path, library, snap, extra_material_glbs=()):
    reset_scene()
    glb = Glb(src)
    library.add_glb(glb, f"{kind}/{name}")
    for extra in extra_material_glbs:
        library.add_glb(Glb(extra), f"{kind}/{name}")
    import_glb(src)
    removed = remove_cameras_and_lights()
    strip_animation()
    restore_rest_transforms(glb)
    dz = snap_to_floor() if snap else 0.0
    face_unity_forward()
    report = describe(kind, name, src, out_path)
    report["removed_cameras_lights"] = removed
    report["floor_snap_m"] = round(dz, 5)
    # Items record their authored size (x, depth, height in the Z-up source); verify compares.
    for node in glb.mesh_nodes():
        dims = (node.get("extras") or {}).get("world_dimensions_xyz_m")
        if dims:
            report["authored_size_xyz"] = [dims[0], dims[2], dims[1]]
            break
    report["bytes"] = export_fbx(out_path, animated=False)
    return report


# --------------------------------------------------------------------------- avatar


def strip_zero_shape_keys(obj, tol=1e-6):
    keys = obj.data.shape_keys
    if keys is None:
        return 0
    basis = keys.reference_key
    removed = 0
    for block in list(keys.key_blocks):
        if block == basis:
            continue
        if all((a.co - b.co).length < tol for a, b in zip(block.data, basis.data)):
            obj.shape_key_remove(block)
            removed += 1
    keys = obj.data.shape_keys
    if keys is not None and len(keys.key_blocks) == 1:
        obj.shape_key_clear()
    return removed


def remove_bone_display_shapes():
    """The glTF importer adds display-only shape meshes ("Icosphere") for bones. Drop them."""
    shapes = {pb.custom_shape for o in bpy.data.objects if o.type == "ARMATURE"
              for pb in o.pose.bones if pb.custom_shape}
    for o in bpy.data.objects:
        if o.type == "ARMATURE":
            for pb in o.pose.bones:
                pb.custom_shape = None
    for shape in shapes:
        bpy.data.objects.remove(shape, do_unlink=True)
    return len(shapes)


def run_avatar(sel, packs, art_dir, art_rel, library):
    reset_scene()
    # The source clips are authored at 30 fps; import at 30 so keyframes land on whole frames.
    bpy.context.scene.render.fps = sel["avatar"].get("fps", 30)
    bpy.context.scene.render.fps_base = 1.0
    base_src = os.path.join(packs, sel["packs"][sel["avatar"]["base"]["source"]], sel["avatar"]["base"]["file"])
    library.add_glb(Glb(base_src), "avatar/base")
    import_glb(base_src)
    remove_bone_display_shapes()
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"expected one armature in {base_src}, found {len(armatures)}")
    rig = armatures[0]
    base_actions = set(bpy.data.actions)
    notes = []

    mod_cfg = sel["avatar"]["modules"]
    mod_dir = os.path.join(packs, sel["packs"][mod_cfg["source"]], mod_cfg["folder"])
    for module in mod_cfg["names"]:
        src = os.path.join(mod_dir, module + ".glb")
        library.add_glb(Glb(src), f"avatar/{module}")
        known_materials = set(bpy.data.materials)
        known_actions = set(bpy.data.actions)
        new_names = [o.name for o in import_glb(src)]
        remove_bone_display_shapes()
        new_objs = [bpy.data.objects[n] for n in new_names if n in bpy.data.objects]
        new_rigs = [o for o in new_objs if o.type == "ARMATURE"]
        new_meshes = [o for o in new_objs if o.type == "MESH"]
        bone_names = {b.name for b in rig.data.bones}
        for mesh in new_meshes:
            missing = {g.name for g in mesh.vertex_groups} - bone_names
            if missing:
                raise RuntimeError(f"{module}: vertex groups with no matching bone: {sorted(missing)}")
            world = mesh.matrix_world.copy()
            mesh.parent = rig
            mesh.matrix_world = world
            for mod in mesh.modifiers:
                if mod.type == "ARMATURE":
                    mod.object = rig
            # Imported duplicates come in as "fabric_main.001": point them at the base material.
            for slot in mesh.material_slots:
                mat = slot.material
                match = DUP_SUFFIX.match(mat.name) if mat else None
                if match and mat not in known_materials and match.group("base") in bpy.data.materials:
                    slot.material = bpy.data.materials[match.group("base")]
        for obj in new_rigs:
            for child in obj.children:
                if child not in new_meshes:
                    bpy.data.objects.remove(child, do_unlink=True)
            bpy.data.objects.remove(obj, do_unlink=True)
        for action in set(bpy.data.actions) - known_actions:
            bpy.data.actions.remove(action)
        notes.append(f"{module}: {len(new_meshes)} mesh(es) rebound to {rig.name}")

    removed_keys = {o.name: strip_zero_shape_keys(o) for o in mesh_objects()}
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)
    remove_cameras_and_lights()
    if set(bpy.data.actions) != base_actions:
        raise RuntimeError("module import left extra actions behind")
    stray = [o.name for o in mesh_objects() if not o.name.startswith("SK_")]
    if stray:
        raise RuntimeError(f"unexpected non-avatar meshes: {stray}")

    out_path = os.path.join(art_dir, "Avatar", sel["avatar"]["output_name"] + ".fbx")
    face_unity_forward()
    report = describe("avatar", sel["avatar"]["output_name"], base_src, out_path)
    report["bones"] = [b.name for b in rig.data.bones]
    report["action_frames"] = {a.name: [round(a.frame_range[0], 2), round(a.frame_range[1], 2)]
                               for a in bpy.data.actions}
    report["zero_shape_keys_removed"] = removed_keys
    report["notes"] = notes
    report["bytes"] = export_fbx(out_path, animated=True)
    return report


# --------------------------------------------------------------------------- main


def main():
    args = parse_args()
    with open(args.selection, encoding="utf-8") as f:
        sel = json.load(f)
    only = set(args.only.split(","))
    packs = os.path.abspath(args.packs)
    repo = os.path.abspath(args.repo)
    art_rel = sel["output"]["art"]
    art_dir = os.path.join(repo, art_rel)
    data_dir = os.path.join(repo, sel["output"]["data"])
    os.makedirs(data_dir, exist_ok=True)
    library = Library(sel["max_texture_size"])
    reports = []
    started = time.time()

    def pack(key):
        return os.path.join(packs, sel["packs"][sel[key]["source"]], sel[key]["folder"])

    if "items" in only:
        root = pack("items")
        for word in sel["items"]["words"]:
            matches = [os.path.join(dp, f) for dp, _, fs in os.walk(root) for f in fs if f == f"{word}_World.glb"]
            if len(matches) != 1:
                raise RuntimeError(f"{word}: expected one world GLB, found {len(matches)}")
            src = matches[0]
            held_dir = os.path.join(os.path.dirname(src), "Held_Skins")
            skins = [os.path.join(held_dir, f"{word}_Held_{s}.glb") for s in sel["items"]["skins"]]
            skins = [s for s in skins if os.path.exists(s)]
            out = os.path.join(art_dir, "Items", f"{word}.fbx")
            reports.append(run_static("item", word, src, out, library, snap=True, extra_material_glbs=skins))
            reports[-1]["held_skins_found"] = len(skins)

    if "letters" in only:
        root = pack("letters")
        for letter in sel["letters"]["letters"]:
            src = os.path.join(root, letter, f"Tile_{letter}_{sel['letters']['skin']}.glb")
            out = os.path.join(art_dir, "Letters", f"Tile_{letter}.fbx")
            reports.append(run_static("letter", letter, src, out, library, snap=True))

    for kind, key, names in (("environment", "environment", "modules"), ("vfx", "vfx", "meshes")):
        if kind not in only:
            continue
        root = pack(key)
        sub = "Environment" if kind == "environment" else "VFX"
        for name in sel[key][names]:
            src = os.path.join(root, name, f"{name}.glb")
            out = os.path.join(art_dir, sub, f"{name}.fbx")
            reports.append(run_static(kind, name, src, out, library, snap=False))

    if "avatar" in only:
        reports.append(run_avatar(sel, packs, art_dir, art_rel, library))

    # Keep machine-specific folders out of the committed report.
    for rep in reports:
        rep["source"] = os.path.relpath(rep["source"], packs).replace("\\", "/")
        rep["output"] = os.path.relpath(rep["output"], repo).replace("\\", "/")

    # Material library last: it may call Blender's image API, which a scene reset would clear.
    reset_scene()
    lib = library.write(art_dir, data_dir, art_rel)
    summary = {
        "generator": "Tools/AssetPipeline/build_assets.py",
        "blender": bpy.app.version_string,
        "only": sorted(only),
        "seconds": round(time.time() - started, 1),
        "files": reports,
        "material_count": len(lib["materials"]),
        "texture_count": len(lib["textures"]),
        "material_conflicts": lib["conflicts"],
    }
    with open(os.path.join(data_dir, "build_report.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    print("BUILD_RESULT " + json.dumps({"files": len(reports), "materials": len(lib["materials"]),
                                        "textures": len(lib["textures"]), "conflicts": len(lib["conflicts"]),
                                        "seconds": summary["seconds"]}))


try:
    main()
except Exception as exc:  # make failures visible to the calling shell
    import traceback
    traceback.print_exc()
    print("BUILD_FAILED " + str(exc))
    sys.exit(1)
