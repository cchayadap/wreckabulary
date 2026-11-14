"""mixamo_intake.py: take Mixamo clips that a human downloaded under their own Adobe ID (stdlib only).

    uv run --python 3.12 Tools/kit/anim/mixamo_intake.py --inbox _Inbox/Mixamo --out <Assets dir> [--research-metadata-only] [--json]
    uv run --python 3.12 Tools/kit/anim/mixamo_intake.py --register-in-place <Assets file or folder> --retrieved-at <UTC> [--json]

CONTRACTS.md §4.5; research_motion.md §2, §9, §10. Mixamo clips may ship only when the studio's own Adobe ID downloaded
them from mixamo.com, embedded in the game and never as raw FBX. The agent never signs in, never types a password and
never downloads from mixamo.com or any mirror: the human drops files into the inbox, and this tool
1. validates each FBX: FBX header, a mixamorig skeleton with the 22 core bones, at least one take with a length, the
   frame rate (--fps, default 30) and "Without Skin" (a mesh only warns);
2. copies it into --out (never overwrites a different file; the copy goes through a dot-prefixed temp name that Unity
   does not import), then makes sure Docs/motion_sources.csv and Docs/licenses.csv have a row for it
   (source_kind=own_account_download, licence_id=Mixamo-Adobe, redistribution=embedded_only, training_ban=true); the
   licence row goes through license_row.mjs and is looked up first, so a re-run writes a row that an earlier run failed
   to write;
3. moves the inbox file to <inbox>/processed/ only once both rows exist. Rejected files stay in the inbox with reasons.
The inbox must be outside the Unity project's Assets, Packages and ProjectSettings, and must hold no .meta files:
moving an imported asset away from its .meta would give it a new GUID and break every reference to it. Clips that are
already in the project are registered where they are with --register-in-place <path> --retrieved-at <UTC> (the owner
states when they downloaded them; a file time is not a download time): rows are written, nothing is copied or moved.
--research-metadata-only writes nothing: it lists what the inbox files contain, and with --metadata-csv <local file>
[--query <text>] looks clip names up in a local index so a human knows what to download. It never fetches anything,
and never from the Hugging Face mirror (a research mirror with no licence chain for a shipped game).
Additive flags: --body <name>, --fps <n>, --loop <true|false>, --project <W>, --license-row <path>, --no-license-row,
--account-label <text> (a label such as "studio Adobe ID", never an email or password), --register-in-place <path>,
--retrieved-at <UTC>.
"""
from __future__ import annotations

import argparse
import csv
import datetime as _dt
import os
import re
import shutil
import sys

sys.dont_write_bytecode = True  # keep Tools/kit/anim free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbxmeta  # noqa: E402
import gsk_anim_common as C  # noqa: E402

MIXAMO_URL = "https://www.mixamo.com/"
MIXAMO_TERMS = "https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html"
DENY = re.compile(r"huggingface\.co/datasets/Linzhan|Mixamo-Animations-Characters", re.I)
UNITY_TOP = ("Assets", "Packages", "ProjectSettings")


def validate(path, want_fps):
    """(meta, reasons[], warnings[]) for one inbox file."""
    reasons, warnings = [], []
    if not path.lower().endswith(".fbx"):
        return None, ["not an .fbx file (unzip Mixamo packs into the inbox first)"], []
    try:
        meta = fbxmeta.probe_fbx(path)
    except (fbxmeta.FbxError, OSError) as e:
        return None, [f"unreadable FBX: {e}"], []
    if not meta["mixamo_prefix"]:
        reasons.append("no mixamorig skeleton: not a Mixamo download")
    elif meta["mixamo_core_missing"]:
        reasons.append("Mixamo skeleton is missing core bones: " + ", ".join(meta["mixamo_core_missing"]))
    if not meta["takes"] or not meta["duration_s"]:
        reasons.append("no animation take with a length (character-only file?)")
    if meta["fps"] is None:
        reasons.append("frame rate unknown")
    elif abs(meta["fps"] - want_fps) > 0.01:
        reasons.append(f"{meta['fps']:g} fps, expected {want_fps:g}: download again at {want_fps:g} fps")
    if meta["has_mesh"]:
        warnings.append('contains a mesh: download clips "Without Skin" (the raw Adobe character must not ship on its own)')
    if meta["kind"] == "ascii":
        warnings.append("ASCII FBX: Mixamo exports binary, so check where this file came from")
    return meta, reasons, warnings


def clean_name(name):
    base = re.sub(r"[^\w .()+-]", "_", name).strip(" .")
    return base or "clip.fbx"


def research_metadata(a, ws, inbox):
    files = []
    if os.path.isdir(inbox):
        for n in sorted(os.listdir(inbox)):
            p = os.path.join(inbox, n)
            if not os.path.isfile(p):
                continue
            meta, reasons, warnings = validate(p, a.fps)
            files.append({"inbox": C.rel_posix(ws, p), "would_accept": not reasons, "reasons": reasons, "warnings": warnings,
                          **({k: meta[k] for k in ("kind", "fps", "duration_s", "frames", "mixamo_bones", "has_mesh")} if meta else {}),
                          **({"takes": [t["name"] for t in meta["takes"]]} if meta else {})})
    matches = []
    if a.metadata_csv:
        if DENY.search(a.metadata_csv):
            raise C.CliFail("refusing a Hugging Face mirror reference: use a local index file", C.EXIT_USAGE)
        if not os.path.isfile(a.metadata_csv):
            raise C.CliFail(f"--metadata-csv not found: {a.metadata_csv}", C.EXIT_USAGE)
        with open(a.metadata_csv, encoding="utf-8-sig", newline="") as fh:
            rows = list(csv.DictReader(fh))
        q = (a.query or "").lower()
        for r in rows:
            blob = " ".join(str(v) for v in r.values()).lower()
            if not q or q in blob:
                matches.append({k: r[k] for k in list(r)[:6]})
            if len(matches) >= 200:
                break
    return {"ok": True, "exit": C.EXIT_OK, "mode": "research_metadata_only", "writes": [], "files": files, "matches": matches,
            "note": "nothing was downloaded or copied; a human downloads chosen clips from mixamo.com under the studio's own Adobe ID"}


def refuse_unity_inbox(ws, inbox):
    """Exit 2 when the inbox is inside the Unity project's asset folders or holds .meta files (imported assets)."""
    U = C.unity_root(ws)
    for top in UNITY_TOP:
        if C.is_inside(inbox, os.path.join(U, top)):
            raise C.CliFail(f"--inbox {C.rel_posix(ws, inbox)} is inside the Unity project ({top}/): moving imported clips away "
                            "from their .meta files gives them new GUIDs and breaks every reference; to add rows for clips already "
                            "in the project use --register-in-place <path> --retrieved-at <UTC>", C.EXIT_USAGE)
    if os.path.isdir(inbox):
        metas = sorted(n for n in os.listdir(inbox) if n.lower().endswith(".meta") and os.path.isfile(os.path.join(inbox, n)))
        if metas:
            raise C.CliFail(f"the inbox holds Unity .meta files ({', '.join(metas[:3])}): it looks like a folder Unity imports; "
                            "nothing was taken. Use --register-in-place for clips already in the project", C.EXIT_USAGE)


def rows_for(entry, a, sha, meta, retrieved, notes):
    motion = {
        "clip_path": entry["dest"], "body": a.body, "source_kind": "own_account_download", "source_url": MIXAMO_URL,
        "licence_id": "Mixamo-Adobe", "route": "mixamo_own", "fps": f"{meta['fps']:g}", "loop": a.loop or "",
        "retrieved_at_utc": retrieved, "sha256": sha,
        "notes": f"{notes}; takes {', '.join(entry['takes'])}; {meta['duration_s']:g} s",
    }
    licence = {
        "path": entry["dest"], "sha256": sha, "source_kind": "own_account_download", "source_url": MIXAMO_URL,
        "licence_id": "Mixamo-Adobe", "licence_url": MIXAMO_TERMS, "author": "Adobe (Mixamo)", "attribution_text": "",
        "retrieved_at_utc": retrieved, "account_or_plan": "own_adobe_id", "provider": "mixamo", "generator_model": "",
        "job_id": "", "ai_generated": "false", "commercial_ok": "true", "training_ban": "true",
        "redistribution": "embedded_only", "ships": "true", "notes": notes,
    }
    return motion, licence


def ensure_rows(entry, motion_csv, motion, licence, tool, ws):
    """Write whichever of the two rows is missing. Returns True when both rows exist (or licence rows are skipped)."""
    if any(r.get("clip_path") == entry["dest"] for r in C.csv_rows(motion_csv)):
        entry["motion_row"] = "exists"
    else:
        C.csv_append(motion_csv, C.MOTION_COLUMNS, motion)
        entry["motion_row"] = "written"
    if not tool:
        entry["licence_row"] = "skipped"
        entry["licence_row_json"] = licence
        return True
    status, detail = C.ensure_licence_row(tool, ws, licence)
    entry["licence_row"] = status
    if status == "failed":
        entry.update(status="licence_row_failed", reasons=[detail])
        return False
    return True


def fbx_files(root):
    if os.path.isfile(root):
        return [root]
    out = []
    for d, dirs, files in os.walk(root):
        dirs[:] = sorted(x for x in dirs if not x.startswith("."))
        out += [os.path.join(d, f) for f in sorted(files) if f.lower().endswith(".fbx")]
    return out


def register_in_place(a, ws, tool, motion_csv):
    target = os.path.abspath(os.path.join(ws, a.register_in_place) if not os.path.isabs(a.register_in_place) else a.register_in_place)
    if not C.is_inside(target, ws) or not os.path.exists(target):
        raise C.CliFail("--register-in-place must name an existing file or folder inside the workspace", C.EXIT_USAGE)
    if not a.retrieved_at:
        raise C.CliFail("--register-in-place needs --retrieved-at <UTC ISO time> (when the owner downloaded the clips from mixamo.com); "
                        "a file time is not a download time", C.EXIT_USAGE)
    try:
        retrieved = C.utc_iso(C.parse_utc(a.retrieved_at))
    except ValueError:
        raise C.CliFail(f"--retrieved-at {a.retrieved_at!r} is not an ISO 8601 time", C.EXIT_USAGE)
    files = []
    for f in fbx_files(target):
        entry = {"path": C.rel_posix(ws, f), "status": "", "reasons": [], "warnings": []}
        files.append(entry)
        meta, reasons, warnings = validate(f, a.fps)
        entry["warnings"] = warnings
        if reasons:
            entry.update(status="rejected", reasons=reasons)
            continue
        sha = C.sha256_file(f)
        entry.update(dest=entry["path"], sha256=sha, fps=meta["fps"], duration_s=meta["duration_s"], takes=[t["name"] for t in meta["takes"]])
        notes = (f"registered in place; downloaded by the owner from mixamo.com under the {a.account_label}; "
                 "retrieval time stated by the owner; raw FBX never public")
        motion, licence = rows_for(entry, a, sha, meta, retrieved, notes)
        if ensure_rows(entry, motion_csv, motion, licence, tool, ws):
            entry["status"] = "registered"
    return files


def main(argv):
    ap = argparse.ArgumentParser(prog="mixamo_intake.py", description=__doc__.split("\n")[0])
    ap.add_argument("--inbox", default=os.path.join("_Inbox", "Mixamo"))
    ap.add_argument("--out")
    ap.add_argument("--research-metadata-only", action="store_true")
    ap.add_argument("--metadata-csv")
    ap.add_argument("--query")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--body", default="humanoid")
    ap.add_argument("--fps", type=float, default=30.0)
    ap.add_argument("--loop", choices=["true", "false"])
    ap.add_argument("--project")
    ap.add_argument("--license-row")
    ap.add_argument("--no-license-row", action="store_true")
    ap.add_argument("--account-label", default="studio's own Adobe ID")
    ap.add_argument("--register-in-place")
    ap.add_argument("--retrieved-at")
    a = ap.parse_args(argv)

    ws = os.path.abspath(a.project) if a.project else C.find_workspace(os.getcwd())
    base = ws or os.getcwd()
    inbox = os.path.abspath(os.path.join(base, a.inbox) if not os.path.isabs(a.inbox) else a.inbox)
    if DENY.search(a.inbox):
        raise C.CliFail("the Hugging Face Mixamo mirror is not an intake source", C.EXIT_USAGE)
    if re.search(r"@|password", a.account_label, re.I):
        raise C.CliFail("--account-label is a label such as 'studio Adobe ID', never an email address or password", C.EXIT_USAGE)

    if a.research_metadata_only:
        res = research_metadata(a, ws, inbox)
        text = [f"{'ok ' if f['would_accept'] else 'NO '} {f['inbox']}  {'; '.join(f['reasons'])}" for f in res["files"]]
        text += [", ".join(f"{k}={v}" for k, v in m.items()) for m in res["matches"]]
        text.append(f"research metadata only: {len(res['files'])} inbox file(s), {len(res['matches'])} index match(es); nothing written")
        C.emit(res, a.json, text)
        return C.EXIT_OK

    if not ws:
        raise C.CliFail("no kit workspace found (Tools/kit/kit.json); pass --project <W>", C.EXIT_INFRA)
    if not a.register_in_place and not a.out:
        raise C.CliFail("--out <Assets dir> is required (or use --research-metadata-only or --register-in-place)", C.EXIT_USAGE)
    out = None
    if not a.register_in_place:
        out = os.path.abspath(os.path.join(ws, a.out) if not os.path.isabs(a.out) else a.out)
        if not C.is_inside(out, ws):
            raise C.CliFail("--out must be inside the workspace", C.EXIT_USAGE)
        if C.is_inside(out, inbox):
            raise C.CliFail("--out must not be inside the inbox", C.EXIT_USAGE)
        refuse_unity_inbox(ws, inbox)
    tool = None
    if not a.no_license_row:
        tool = C.license_row_tool(ws, a.license_row)
        if not tool:
            raise C.CliFail("license_row.mjs not found (install the kit, or pass --license-row or --no-license-row); nothing was taken", C.EXIT_INFRA)
        if not shutil.which("node"):
            raise C.CliFail("node not found on PATH; nothing was taken", C.EXIT_INFRA)
    motion_csv = os.path.join(ws, "Docs", "motion_sources.csv")

    if a.register_in_place:
        files = register_in_place(a, ws, tool, motion_csv)
        bad = [f for f in files if f["status"] in ("rejected", "licence_row_failed")]
        ok = not bad and bool(files)
        result = {"ok": ok, "exit": C.EXIT_OK if ok else C.EXIT_FAIL, "mode": "register_in_place",
                  "motion_csv": C.rel_posix(ws, motion_csv), "files": files}
        text = [f"{f['status']:<18} {f['path']}  {'; '.join(f['reasons'] or f['warnings'])}".rstrip() for f in files]
        text.append(("PASS" if ok else "FAIL") + f": {len(files)} file(s) registered in place, {len(bad)} rejected or failed"
                    + ("" if files else " (no .fbx found)"))
        C.emit(result, a.json, text)
        return result["exit"]

    processed = os.path.join(inbox, "processed")
    files = []
    names = sorted(n for n in os.listdir(inbox) if os.path.isfile(os.path.join(inbox, n))) if os.path.isdir(inbox) else []
    for n in names:
        src = os.path.join(inbox, n)
        entry = {"inbox": C.rel_posix(ws, src), "status": "", "reasons": [], "warnings": []}
        files.append(entry)
        meta, reasons, warnings = validate(src, a.fps)
        entry["warnings"] = warnings
        if reasons:
            entry.update(status="rejected", reasons=reasons)
            continue
        sha = C.sha256_file(src)
        dest = os.path.join(out, clean_name(n))
        entry.update(sha256=sha, dest=C.rel_posix(ws, dest), fps=meta["fps"], duration_s=meta["duration_s"],
                     frames=meta["frames"], mixamo_bones=meta["mixamo_bones"], takes=[t["name"] for t in meta["takes"]])
        if os.path.exists(dest):
            if C.sha256_file(dest) != sha:
                entry.update(status="rejected", reasons=[f"a different file already exists at {entry['dest']}; nothing was overwritten"])
                continue
            entry["status"] = "duplicate"
        else:
            os.makedirs(out, exist_ok=True)
            tmp = os.path.join(out, f".{os.path.basename(dest)}.part-{os.getpid()}")  # dot-prefixed: Unity never imports it
            try:
                shutil.copyfile(src, tmp)
                os.replace(tmp, dest)
            finally:
                if os.path.exists(tmp):
                    os.remove(tmp)
            entry["status"] = "accepted"
        # the human downloaded it when the browser wrote the inbox file: its mtime is the retrieval time
        retrieved = C.utc_iso(_dt.datetime.fromtimestamp(os.path.getmtime(src), _dt.timezone.utc))
        notes = f"inbox file {n}; downloaded by the owner from mixamo.com under the {a.account_label}; raw FBX never public"
        motion, licence = rows_for(entry, a, sha, meta, retrieved, notes)
        if not ensure_rows(entry, motion_csv, motion, licence, tool, ws):
            continue  # the file stays in the inbox: the next run finds the copy as a duplicate and retries the row
        os.makedirs(processed, exist_ok=True)
        moved = os.path.join(processed, f"{C.utc_stamp()}-{n}")
        try:
            os.replace(src, moved)
            entry["moved_to"] = C.rel_posix(ws, moved)
        except OSError as e:
            entry["warnings"] = entry["warnings"] + [f"could not move the inbox file to processed/ ({type(e).__name__}: {e}); the next run finds it as a duplicate"]
    bad = [f for f in files if f["status"] in ("rejected", "licence_row_failed")]
    ok = not bad
    result = {"ok": ok, "exit": C.EXIT_OK if ok else C.EXIT_FAIL, "mode": "intake", "inbox": C.rel_posix(ws, inbox),
              "out": C.rel_posix(ws, out), "motion_csv": C.rel_posix(ws, motion_csv), "files": files}
    text = [f"{f['status']:<18} {f['inbox']}  {'; '.join(f['reasons'] or f['warnings'])}".rstrip() for f in files]
    text.append(("PASS" if ok else "FAIL") + f": {len(files)} file(s), {len(bad)} rejected or failed")
    C.emit(result, a.json, text)
    return result["exit"]


if __name__ == "__main__":
    C.run_main(main)
