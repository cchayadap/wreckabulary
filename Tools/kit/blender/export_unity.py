"""Export a character or prop for Unity with fixed settings, then prove the file by re-importing a copy of it.

Run: node Tools/kit/blender/bl_run.mjs export_unity.py -- --in <model> --out <file.fbx|.glb> [--format fbx|glb]
       [--preset character|prop] [--verify-dir <dir>] [--no-verify] [--fidelity-mm 0.5] [--in-place]
Presets (blkit.FBX_PRESETS / GLB_PRESETS; checked against Blender 5.2.2 operator defaults, extract_animation.md 2.11):
  character FBX: armature + skinned meshes only, no leaf bones, every action baked as its own take (NLA off,
    simplify 0), FBX_SCALE_UNITS, -Z forward / Y up, textures copied next to the file. The armature object is
    renamed 'Armature' so every take is 'Armature|<clip>', which the Unity postprocessor strips.
  prop FBX: meshes and empties, FBX_SCALE_ALL + bake_space_transform (no -90 deg X in Unity), no animation.
  GLB: Y up, ACTIONS animation mode, 4 influences, images as authored.
Verification (extract_animation.md 2.12, 'copied-folder reimport'): the exported file and the files the export
wrote (the model, and for FBX its <stem>.fbm/ textures; nothing else in the folder) are copied to a fresh folder,
imported into an empty scene, and compared with the source: bone and clip names, clip durations, skinned mesh
count, unweighted vertices, influences, height and axes (bounding box), textures resolved inside the copy or
embedded, texture bytes equal to the source's packed bytes, and bone positions sampled at the start, middle and end
of every clip.
"""
import hashlib
import math
import os
import shutil
import sys
import tempfile
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402

import blkit  # noqa: E402

ARMATURE_NAME = "Armature"
SKIP_IMAGES = {"Render Result", "Viewer Node"}


def add_args(p):
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--format", choices=["fbx", "glb"])
    p.add_argument("--preset", choices=["character", "prop"], help="default: character when there is an armature")
    p.add_argument("--verify-dir", help="where the reimport copy goes (default: a temp folder, removed afterwards)")
    p.add_argument("--no-verify", action="store_true")
    p.add_argument("--keep-names", action="store_true", help="do not rename the armature object to 'Armature'")
    p.add_argument("--deform-only", action="store_true", help="FBX: export deform bones only")
    p.add_argument("--fidelity-mm", type=float, default=0.5, help="max bone position error on reimport")
    blkit.add_in_place_arg(p)


def clip_key(name):
    """'Armature|Walk' -> 'walk'; glTF may also append '_Armature'."""
    n = name.split("|")[-1]
    for suffix in ("_" + ARMATURE_NAME, "_armature"):
        if n.endswith(suffix):
            n = n[: -len(suffix)]
    return n.lower()


action_paths = blkit.action_paths


def armature_actions(arm):
    """Actions that animate this armature's bones (or are empty)."""
    out = []
    for act in bpy.data.actions:
        paths = action_paths(act)
        if any(p.startswith("pose.bones") for p in paths) or not paths:
            out.append(act)
    return out


def _channel(path):
    """'pose.bones["X"].rotation_euler' -> ('X', 'rotation_euler'), else None."""
    if not path.startswith('pose.bones["'):
        return None
    end = path.find('"]', 12)
    if end < 0 or path[end + 2:end + 3] != ".":
        return None
    return path[12:end], path[end + 3:]


def key_neutral_channels(arm, actions):
    """Key rest values into every clip for channels that other clips animate but it does not. Without this the
    exporter bakes whatever pose the previous clip left on unkeyed bones into this clip (the Overlord finger-pose
    leak, extract_animation.md 2.6). Only this export session changes; the source file is not saved."""
    per_act = {act.name: {c for c in map(_channel, action_paths(act)) if c} for act in actions}
    union = set().union(*per_act.values()) if per_act else set()
    added = 0
    for act in actions:
        missing = sorted(union - per_act[act.name])
        if not missing:
            continue
        start = blkit.action_range(act)[0]
        with blkit.solo_action(arm, act):
            for bone, prop in missing:
                pb = arm.pose.bones.get(bone)
                if pb is None or prop not in ("location", "rotation_quaternion", "rotation_euler",
                                              "rotation_axis_angle", "scale"):
                    continue
                pb.keyframe_insert(prop, frame=start)
                added += 1
    return added


def whole_range(act):
    """The action's range on whole frames (exporters bake whole frames)."""
    a, b = blkit.action_range(act)
    fa = int(round(a))
    return fa, max(fa, int(math.floor(b + 1e-3)))


def sample_clips(arm, fps):
    """{clip key: {'name', 'duration_s', 'samples': [(frame offset, {bone: head})]}} at start, middle and end."""
    clips = {}
    for act in armature_actions(arm):
        fa, fb = whole_range(act)
        samples = []
        with blkit.solo_action(arm, act):
            for f in sorted({fa, (fa + fb) // 2, fb}):
                blkit.set_frame(f)
                samples.append((f - fa, blkit.bone_world_heads(arm)))
        clips[clip_key(act.name)] = {"name": act.name, "duration_s": round((fb - fa) / fps, 4), "samples": samples,
                                     "frame_range": [fa, fb]}
    return clips


def snap_ranges(actions):
    """Give actions whose range ends a hair off a whole frame (glTF imports at another fps) an exact manual range,
    so the FBX exporter does not drop the last frame."""
    n = 0
    for act in actions:
        a, b = blkit.action_range(act)
        ra, rb = round(a), round(b)
        if (abs(a - ra) > 1e-6 or abs(b - rb) > 1e-6) and abs(a - ra) < 0.01 and abs(b - rb) < 0.01:
            act.use_frame_range = True
            act.frame_start, act.frame_end = float(ra), float(rb)
            n += 1
    return n


def image_report(copy_dir=None):
    out = []
    for img in bpy.data.images:
        if img.name in SKIP_IMAGES or img.source not in ("FILE", "GENERATED", "TILED"):
            continue
        path = bpy.path.abspath(img.filepath) if img.filepath else ""
        entry = {"name": img.name, "packed": img.packed_file is not None, "size": list(img.size),
                 "path": path or None}
        if copy_dir is not None:
            inside = bool(path) and os.path.normcase(os.path.abspath(path)).startswith(os.path.normcase(os.path.abspath(copy_dir)))
            entry["resolved"] = entry["packed"] or (inside and os.path.isfile(path))
        out.append(entry)
    return out


def scene_facts(fps):
    arms = blkit.armatures()
    mesh_objs = blkit.meshes()
    skinned = [m for m in mesh_objs if blkit.armature_of(m) is not None]
    facts = {"armatures": len(arms), "meshes": len(mesh_objs), "skinned_meshes": len(skinned),
             "tris": blkit.tri_count(mesh_objs), "materials": len({s.material.name for m in mesh_objs
                                                                   for s in m.material_slots if s.material})}
    with blkit.rest_pose(arms):
        lo, hi = blkit.world_bbox(mesh_objs)
    facts["bbox_min"] = None if lo is None else [round(float(v), 5) for v in lo]
    facts["bbox_max"] = None if hi is None else [round(float(v), 5) for v in hi]
    facts["size"] = None if lo is None else [round(float(v), 5) for v in (hi - lo)]
    if arms:
        arm = arms[0]
        facts["bones"] = sorted(b.name for b in arm.data.bones)
        stats = blkit.weight_stats(skinned) if skinned else {"unweighted": 0, "max_influences": 0}
        facts["unweighted"] = stats["unweighted"]
        facts["max_influences"] = stats["max_influences"]
        facts["_clips"] = sample_clips(arm, fps)
        with blkit.rest_pose([arm]):
            facts["_rest"] = blkit.bone_world_heads(arm)
    return facts


MAGIC = ((b"\x89PNG", ".png"), (b"\xff\xd8", ".jpg"), (b"GIF8", ".gif"), (b"BM", ".bmp"))


def material_images(objs):
    imgs = set()
    for o in objs:
        for slot in getattr(o, "material_slots", []):
            mat = slot.material
            if mat is None or mat.node_tree is None:
                continue
            for n in mat.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image is not None:
                    imgs.add(n.image)
    return sorted(imgs, key=lambda i: i.name)


def externalise_images(objs, out, warnings):
    """FBX points at texture files. Images that live only inside the source (packed, e.g. from a GLB, or generated)
    are written first to <out stem>.fbm/, the folder the exporter copies textures to and Unity reads, with packed
    bytes written unchanged (no re-encode). The material is then pointed at a fresh image loaded from that file
    (user_remap). Nothing is unpacked, so no file is written to or read from the working directory.
    Returns [{name, path, sha256, bytes}] for the verify step."""
    out_dir = os.path.dirname(os.path.abspath(out))
    folder = os.path.join(out_dir, os.path.splitext(os.path.basename(out))[0] + ".fbm")
    written = []
    for img in material_images(objs):
        path = bpy.path.abspath(img.filepath) if img.filepath else ""
        if img.packed_file is None and path and os.path.isfile(path):
            continue
        os.makedirs(folder, exist_ok=True)
        stem = "".join(c if (c.isalnum() or c in "-_.") else "_" for c in os.path.splitext(img.name)[0]) or "texture"
        if img.packed_file is not None:
            data = bytes(img.packed_file.data)
            ext = next((e for m, e in MAGIC if data.startswith(m)), None)
            if ext is None:
                ext = ".webp" if data[8:12] == b"WEBP" else ".bin"
                warnings.append("texture %s is %s, which Unity does not import; convert it to PNG" % (img.name, ext))
            dst = os.path.join(folder, stem + ext)
            with open(dst, "wb") as fh:
                fh.write(data)
        else:
            dst = os.path.join(folder, stem + ".png")
            img.file_format = "PNG"
            img.save(filepath=dst)
            with open(dst, "rb") as fh:
                data = fh.read()
        fresh = bpy.data.images.load(dst, check_existing=False)
        name = img.name
        img.user_remap(fresh)
        img.name = name + ".gsk_source"
        fresh.name = name
        written.append({"name": name, "path": dst, "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)})
    return written


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def compare(src, dst, fps_src, fps_dst, fidelity_m, preset):
    failures, detail = [], {}
    for key in ("armatures", "skinned_meshes"):
        if src.get(key) != dst.get(key):
            failures.append({"check": key, "detail": "source %s, copy %s" % (src.get(key), dst.get(key))})
    if src.get("size") and dst.get("size"):
        s, d = np.array(src["size"]), np.array(dst["size"])
        rel = np.abs(d - s) / np.maximum(s, 1e-3)
        detail["size_rel_err"] = [round(float(v), 5) for v in rel]
        if float(rel.max()) > 0.01:
            failures.append({"check": "scale_axes", "detail": "bounding box %s became %s" % (src["size"], dst["size"])})
        detail["scale"] = round(float(d[2] / s[2]), 5) if s[2] > 1e-6 else None
    if src.get("bones") is not None:
        missing = sorted(set(src["bones"]) - set(dst.get("bones") or []))
        if missing:
            failures.append({"check": "bones", "detail": "%d bones lost, e.g. %s" % (len(missing), missing[:5])})
        if dst.get("unweighted"):
            failures.append({"check": "unweighted", "detail": "%d unweighted vertices after reimport" % dst["unweighted"]})
        if (dst.get("max_influences") or 0) > 4:
            failures.append({"check": "max_influences", "detail": str(dst["max_influences"])})
        errs = []
        for k, a in (src.get("_rest") or {}).items():
            b = (dst.get("_rest") or {}).get(k)
            if b is not None:
                errs.append((a - b).length)
        sc, dc = src.get("_clips") or {}, dst.get("_clips") or {}
        lost = sorted(k for k in sc if k not in dc)
        if lost and preset == "character":
            failures.append({"check": "clips", "detail": "clips lost: %s" % lost})
        durations = []
        for k, c in sc.items():
            if k not in dc:
                continue
            durations.append({"clip": k, "source_s": c["duration_s"], "copy_s": dc[k]["duration_s"]})
            if abs(c["duration_s"] - dc[k]["duration_s"]) > 1.0 / max(fps_src, 1) + 1e-4:
                failures.append({"check": "clip_duration", "detail": "%s %.4f s became %.4f s" % (k, c["duration_s"], dc[k]["duration_s"])})
            copy_samples = dict(dc[k]["samples"])
            for off, ha in c["samples"]:
                hb = copy_samples.get(off)
                if hb is None:
                    continue
                for name, pa in ha.items():
                    pb = hb.get(name)
                    if pb is not None:
                        errs.append((pa - pb).length)
        detail["clips"] = durations
        detail["max_bone_err_m"] = round(max(errs), 7) if errs else None
        if errs and max(errs) > fidelity_m:
            failures.append({"check": "fidelity", "detail": "bone position error %.5f m > %.5f m" % (max(errs), fidelity_m)})
    return failures, detail


def verify(out, written, verify_dir, fps_src, src, fidelity_m, preset, texture_src=()):
    own_tmp = verify_dir is None
    base = verify_dir or tempfile.mkdtemp(prefix="gsk_verify_")
    copy_dir = os.path.join(os.path.abspath(base), "copy_%d" % int(time.time() * 1000))
    os.makedirs(copy_dir, exist_ok=True)
    out_dir = os.path.dirname(os.path.abspath(out))
    copied = []
    for p in written:
        rel = os.path.relpath(p, out_dir)
        dst = os.path.join(copy_dir, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(p, dst)
        copied.append(rel.replace("\\", "/"))
    blkit.reset_scene()
    if abs(fps_src - round(fps_src)) < 1e-6:
        bpy.context.scene.render.fps = int(round(fps_src))
        bpy.context.scene.render.fps_base = 1.0
    blkit.import_model(os.path.join(copy_dir, os.path.basename(out)))
    fps_dst = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    dst = scene_facts(fps_dst)
    failures, detail = compare(src, dst, fps_src, fps_dst, fidelity_m, preset)
    images = image_report(copy_dir)
    bad = [i["name"] for i in images if not i.get("resolved")]
    if bad:
        failures.append({"check": "textures", "detail": "not inside the copy and not embedded: %s" % bad[:10]})
    bytes_check = []
    for t in texture_src:
        rel = os.path.relpath(t["path"], out_dir)
        cp = os.path.join(copy_dir, rel)
        same = os.path.isfile(cp) and sha256_file(cp) == t["sha256"]
        bytes_check.append({"name": t["name"], "file": rel.replace(os.sep, "/"), "sha256": t["sha256"], "same": same})
        if not same:
            failures.append({"check": "texture_bytes", "detail": "%s in the copy differs from the source image" % rel})
    rep = {"ok": not failures, "copy_dir": None if own_tmp else copy_dir, "copied": copied, "failures": failures,
           "textures": images, "texture_bytes": bytes_check,
           "copy": {k: v for k, v in dst.items() if not k.startswith("_")}}
    rep.update(detail)
    if own_tmp:
        shutil.rmtree(base, ignore_errors=True)
    return rep


def main(args, res):
    warnings = []
    fmt = args.format or os.path.splitext(args.out)[1].lower().lstrip(".")
    if fmt not in ("fbx", "glb"):
        raise blkit.UsageError("--out must end in .fbx or .glb (or pass --format)")
    if os.path.splitext(args.out)[1].lower() != "." + fmt:
        raise blkit.UsageError("--out %s does not match --format %s" % (args.out, fmt))
    if not args.fidelity_mm > 0:
        raise blkit.UsageError("--fidelity-mm must be positive")
    blkit.refuse_out_is_input(args.out, [args.inp], args.in_place)
    blkit.reset_scene()
    blkit.import_model(args.inp, warnings)
    sc = bpy.context.scene
    fps = sc.render.fps / sc.render.fps_base
    arms = blkit.armatures()
    preset = args.preset or ("character" if arms else "prop")
    mesh_objs = blkit.meshes()
    if preset == "character":
        if len(arms) != 1:
            raise blkit.Refused("armature_count", "character export needs exactly one armature, found %d" % len(arms))
        arm = arms[0]
        if not args.keep_names and arm.name != ARMATURE_NAME:
            clash = bpy.data.objects.get(ARMATURE_NAME)
            if clash is not None and clash is not arm:
                clash.name = ARMATURE_NAME + "_obj"
            arm.name = ARMATURE_NAME
        objs = [arm] + [m for m in mesh_objs if blkit.armature_of(m) is arm or blkit.is_bone_parented(m)]
        loose = [m.name for m in mesh_objs if m not in objs]
        if loose:
            warnings.append("meshes not attached to the rig are left out: %s" % loose[:10])
        acts = armature_actions(arm)
        snapped = snap_ranges(acts)
        if snapped:
            warnings.append("snapped %d clip ranges to whole frames" % snapped)
        added = key_neutral_channels(arm, acts) if len(acts) > 1 else 0
        res["neutral_keys_added"] = added
        if added:
            warnings.append("keyed %d rest channels into clips that did not animate them (stops pose leaks between takes)" % added)
        if arm.animation_data is not None:
            arm.animation_data.action = None
        blkit.reset_pose(arm)
    else:
        objs = list(mesh_objs) + [o for o in sc.objects if o.type == "EMPTY"]
        if not mesh_objs:
            raise blkit.Refused("no_mesh", "nothing to export")
    src = scene_facts(fps)
    out = os.path.abspath(args.out)
    out_dir = os.path.dirname(out)
    os.makedirs(out_dir, exist_ok=True)
    texture_src = externalise_images(objs, out, warnings) if fmt == "fbx" else []
    overrides = {"use_armature_deform_only": True} if args.deform_only else None
    written = []
    settings = blkit.export_model(out, objs, preset, warnings, fbx_overrides=overrides, written=written)
    written = sorted(set(written) | {t["path"] for t in texture_src} | {out})
    if not os.path.isfile(out):
        raise RuntimeError("exporter wrote no file")
    res.update(input=os.path.abspath(args.inp), out=out, format=fmt, preset=preset,
               settings={k: (sorted(v) if isinstance(v, set) else v) for k, v in settings.items()},
               files=[os.path.relpath(p, out_dir).replace("\\", "/") for p in written],
               axes={"forward": "-Z", "up": "Y"} if fmt == "fbx" else {"forward": "+Z", "up": "+Y"},
               take_prefix=(ARMATURE_NAME + "|") if (fmt == "fbx" and preset == "character") else None,
               clips=[{"name": c["name"], "duration_s": c["duration_s"]} for c in (src.get("_clips") or {}).values()],
               source={k: v for k, v in src.items() if not k.startswith("_")}, fps=fps)
    if args.no_verify:
        res.update(scale=None, verify=None, warnings=warnings + ["--no-verify: the file was not re-imported"], ok=True)
        return
    rep = verify(out, written, args.verify_dir, fps, src, args.fidelity_mm / 1000.0, preset, texture_src)
    res.update(verify=rep, scale=rep.get("scale"), warnings=warnings, ok=rep["ok"])
    if not rep["ok"]:
        res["error"] = "reimport check failed: " + "; ".join(f["check"] for f in rep["failures"])


if __name__ == "__main__":
    blkit.run_script("export_unity.py", __doc__, add_args, main)
