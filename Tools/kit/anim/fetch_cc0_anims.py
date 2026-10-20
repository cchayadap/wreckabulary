"""fetch_cc0_anims.py: fetch licence-clean humanoid clip packs with pinned hashes and receipts (stdlib only).

    uv run --python 3.12 Tools/kit/anim/fetch_cc0_anims.py --manifest <json> --out <dir> [--json]

CONTRACTS.md §4.5; research_motion.md §3, §9, §10 and its fact-check; research_ship.md R6.
- Each manifest pack pins a URL and (once known) a sha256. A pack with an empty sha256 is pinned on its first fetch in
  <out>/downloads.json (trust on first use: copy the hash into the manifest after a human check); after that, and
  whenever the manifest carries a sha256, a different hash exits 1 and nothing is overwritten. A size in the manifest is
  checked too. Every redirect hop must stay on https (plain http only for loopback test servers).
- "manual": true packs (itch builds need a browser click) are never downloaded: a human puts the zip in --inbox
  (default _Inbox/CC0) and the script verifies, copies and records it. Once a pack has a receipt and its file is in
  --out, the inbox is no longer needed.
- "stale": true packs (older OpenGameArt copies of the itch builds; their bone names differ) are skipped unless
  --allow-stale is given or --only names them.
- The receipt is written as soon as a file is in place (so its hash stays pinned, even when a later step fails). The
  later steps are idempotent and are re-checked on every run, including for unchanged packs: a licence row is written
  when `license_row.mjs get --path` does not find one, and --extract unpacks again when <out>/<id>/.gsk_extracted.json
  records another sha256.
- --extract unpacks into a hidden sibling folder and renames it into place only when every member was written: no path
  traversal, no links, at most 20000 members and a bounded unpacked size (the declared sizes and the bytes actually
  written are both counted).
- Quaternius packs obtained on or after 2026-08-28 are QAL-1.0 with redistribution=embedded_only; a CC0 claim for them is
  refused (the licence in force when the asset was obtained governs it).
- Refused sources: the Hugging Face Mixamo mirror, HY-Motion, and anything that is not https.
Additive flags: --only <id,…>, --inbox <dir>, --extract, --allow-stale, --project <W>, --license-row <path>,
--no-license-row, --timeout <s>.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import http.client
import json
import os
import re
import shutil
import sys
import urllib.parse
import urllib.request
import zipfile

sys.dont_write_bytecode = True  # keep Tools/kit/anim free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gsk_anim_common as C  # noqa: E402

USER_AGENT = "game-studio-kit/fetch_cc0_anims (licence-pinned asset fetch)"
QAL_EFFECTIVE = _dt.datetime(2026, 8, 28, tzinfo=_dt.timezone.utc)
DENY_URL = [re.compile(r"huggingface\.co/datasets/Linzhan", re.I), re.compile(r"hunyuan-motion", re.I)]
LOOPBACK = {"127.0.0.1", "localhost", "::1"}
MAX_BYTES = 1024 * 1024 * 1024
MAX_MEMBERS = 20000
MAX_EXTRACT_BYTES = 4 * 1024 * 1024 * 1024
EXTRACT_RATIO = 50
MIN_EXTRACT_CAP = 64 * 1024 * 1024
ALLOWED_KIND = {"cc0_download", "library_download"}
ALLOWED_REDIST = {"allowed", "embedded_only", "forbidden"}
MARKER = ".gsk_extracted.json"


def is_quaternius(pack):
    blob = " ".join(str(pack.get(k, "")) for k in ("author", "url", "title", "file"))
    return bool(re.search(r"quaternius|universal_animation_library", blob, re.I))


def validate_manifest(m):
    errs = []
    if not isinstance(m, dict) or m.get("schema") != 1 or not isinstance(m.get("packs"), list):
        raise C.CliFail("manifest: expected {schema: 1, packs: [...]}", C.EXIT_USAGE)
    ids = set()
    for i, p in enumerate(m["packs"]):
        where = f"packs[{i}]"
        if not isinstance(p, dict):
            errs.append(f"{where} is not an object")
            continue
        pid = p.get("id")
        if not isinstance(pid, str) or not re.fullmatch(r"[a-z0-9][a-z0-9_.-]*", pid or ""):
            errs.append(f"{where}.id must be a lower-case slug")
        elif pid in ids:
            errs.append(f"duplicate id {pid}")
        ids.add(pid)
        for k in ("url", "licence_id", "licence_url", "author"):
            if not isinstance(p.get(k), str) or not p.get(k):
                errs.append(f"{where}.{k} is required")
        sha = p.get("sha256", "")
        if not isinstance(sha, str) or (sha and not re.fullmatch(r"[0-9a-f]{64}", sha)):
            errs.append(f"{where}.sha256 must be empty or 64 lower-case hex")
        if p.get("source_kind", "cc0_download") not in ALLOWED_KIND:
            errs.append(f"{where}.source_kind must be one of {sorted(ALLOWED_KIND)}")
        if p.get("redistribution", "embedded_only") not in ALLOWED_REDIST:
            errs.append(f"{where}.redistribution must be one of {sorted(ALLOWED_REDIST)}")
        if "stale" in p and not isinstance(p["stale"], bool):
            errs.append(f"{where}.stale must be true or false")
        if p.get("manual"):
            if not (p.get("file_regex") or p.get("file")):
                errs.append(f"{where}: a manual pack needs file_regex or file")
        else:
            f = p.get("file") or os.path.basename(urllib.parse.urlparse(p.get("url", "")).path)
            if not f or "/" in f or "\\" in f or f.startswith("."):
                errs.append(f"{where}.file must be a plain file name")
        if "bytes" in p and (not isinstance(p["bytes"], int) or p["bytes"] <= 0):
            errs.append(f"{where}.bytes must be a positive integer")
    if errs:
        raise C.CliFail("manifest invalid: " + "; ".join(errs), C.EXIT_USAGE)
    return m["packs"]


def url_allowed(url):
    u = urllib.parse.urlparse(url)
    return u.scheme == "https" or (u.scheme == "http" and u.hostname in LOOPBACK)


def policy_refusal(pack, retrieved):
    """Reason a pack may not be fetched or recorded as declared, or None."""
    url = pack["url"]
    for rx in DENY_URL:
        if rx.search(url):
            return f"denylisted source {url} (research mirror / non-shippable motion)"
    u = urllib.parse.urlparse(url)
    if not pack.get("manual"):
        if u.scheme == "http" and u.hostname not in LOOPBACK:
            return "plain http is refused (https only)"
        if u.scheme not in ("https", "http"):
            return f"unsupported URL scheme {u.scheme!r}"
    if is_quaternius(pack) and retrieved >= QAL_EFFECTIVE:
        if pack["licence_id"] != "QAL-1.0":
            return "Quaternius assets obtained on or after 2026-08-28 are QAL-1.0, not " + pack["licence_id"]
        if pack.get("redistribution", "embedded_only") != "embedded_only":
            return "QAL-1.0 assets need redistribution=embedded_only"
    if re.search(r"(^|[^a-z])nc([^a-z]|$)", pack["licence_id"], re.I):
        return f"non-commercial licence {pack['licence_id']}"
    return None


def load_receipts(path):
    if not os.path.exists(path):
        return {"schema": 1, "files": []}
    try:
        with open(path, encoding="utf-8-sig") as fh:
            r = json.load(fh)
        if isinstance(r, dict) and isinstance(r.get("files"), list):
            return r
    except (OSError, ValueError):
        pass
    raise C.CliFail(f"receipts file is corrupt: {path} (fix or move it; it pins the hashes)", C.EXIT_FAIL)


class _HttpsOnlyRedirect(urllib.request.HTTPRedirectHandler):
    """Refuse a redirect hop that leaves https before it is requested (a plain-http hop could swap the payload)."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if not url_allowed(newurl):
            raise C.CliFail(f"redirect to a non-https URL refused: {newurl}", C.EXIT_FAIL)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


_OPENER = urllib.request.build_opener(_HttpsOnlyRedirect)


def download(url, dest_tmp, timeout):
    """Stream url to dest_tmp; returns (bytes, sha256, final_url)."""
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    h = hashlib.sha256()
    n = 0
    with _OPENER.open(req, timeout=timeout) as resp, open(dest_tmp, "wb") as out:
        final = resp.geturl()
        if not url_allowed(final):
            raise C.CliFail(f"redirected to a non-https URL: {final}", C.EXIT_FAIL)
        while True:
            chunk = resp.read(1 << 16)
            if not chunk:
                break
            n += len(chunk)
            if n > MAX_BYTES:
                raise C.CliFail("download larger than 1 GiB", C.EXIT_FAIL)
            h.update(chunk)
            out.write(chunk)
    return n, h.hexdigest(), final


def extract_cap(zip_bytes):
    """Largest unpacked size accepted for an archive of this size."""
    return min(MAX_EXTRACT_BYTES, max(EXTRACT_RATIO * zip_bytes, MIN_EXTRACT_CAP))


def safe_extract(zip_path, dest, cap=None):
    """Extract a zip into dest without path traversal, links or unbounded output; returns the member count.

    Every name is checked before anything is written. The member count and the declared unpacked size are bounded, and
    the bytes actually written are counted too (headers can lie)."""
    root = os.path.abspath(dest)
    cap = extract_cap(os.path.getsize(zip_path)) if cap is None else cap
    with zipfile.ZipFile(zip_path) as z:
        infos = z.infolist()
        if len(infos) > MAX_MEMBERS:
            raise C.CliFail(f"zip has {len(infos)} members (limit {MAX_MEMBERS}); refusing to extract", C.EXIT_FAIL)
        declared = sum(i.file_size for i in infos)
        if declared > cap:
            raise C.CliFail(f"zip would unpack to {declared} bytes (limit {cap}); refusing to extract", C.EXIT_FAIL)
        plan = []
        for info in infos:
            name = info.filename.replace("\\", "/")
            if name.startswith("/") or re.match(r"^[A-Za-z]:", name) or ".." in name.split("/"):
                raise C.CliFail(f"unsafe path in zip: {info.filename}", C.EXIT_FAIL)
            if (info.external_attr >> 16) & 0o170000 == 0o120000:
                raise C.CliFail(f"symlink in zip: {info.filename}", C.EXIT_FAIL)
            target = os.path.abspath(os.path.join(root, name))
            if not C.is_inside(target, root):
                raise C.CliFail(f"unsafe path in zip: {info.filename}", C.EXIT_FAIL)
            plan.append((info, name, target))
        count = 0
        written = 0
        for info, name, target in plan:
            if name.endswith("/"):
                os.makedirs(target, exist_ok=True)
                continue
            os.makedirs(os.path.dirname(target), exist_ok=True)
            member = 0
            with z.open(info) as src, open(target, "wb") as out:
                while True:
                    chunk = src.read(1 << 16)
                    if not chunk:
                        break
                    member += len(chunk)
                    written += len(chunk)
                    if member > info.file_size:
                        raise C.CliFail(f"zip member {info.filename} is larger than its header says; refusing", C.EXIT_FAIL)
                    if written > cap:
                        raise C.CliFail(f"unpacked size passed the limit of {cap} bytes; refusing", C.EXIT_FAIL)
                    out.write(chunk)
            count += 1
    return count


def read_marker(folder):
    try:
        with open(os.path.join(folder, MARKER), encoding="utf-8") as fh:
            j = json.load(fh)
        return j if isinstance(j, dict) else None
    except (OSError, ValueError):
        return None


def extract_pack(zip_path, out, pack_id, sha, iso):
    """Unpack zip_path into <out>/<pack_id>/ unless its marker already records sha.

    Returns (member count or None when already done, warning or None). The archive goes into a hidden sibling folder
    first and is renamed into place only when complete, so a failure never leaves partial files in <out>/<pack_id>/.
    A previous complete extraction (it has a marker) is replaced; a folder without a marker is kept aside and reported."""
    dest = os.path.join(out, pack_id)
    if (read_marker(dest) or {}).get("sha256") == sha:
        return None, None
    tmp = os.path.join(out, f".{pack_id}.extract-{os.getpid()}")
    shutil.rmtree(tmp, ignore_errors=True)
    warning = None
    try:
        count = safe_extract(zip_path, tmp)
        C.write_json_atomic(os.path.join(tmp, MARKER), {"schema": 1, "id": pack_id, "sha256": sha, "utc": iso, "files": count})
        if os.path.exists(dest):
            complete = read_marker(dest) is not None
            aside = os.path.join(out, f".{pack_id}.replaced-{C.utc_stamp()}-{os.getpid()}")
            os.replace(dest, aside)
            try:
                os.replace(tmp, dest)
            except OSError:
                os.replace(aside, dest)
                raise
            if complete:
                shutil.rmtree(aside, ignore_errors=True)
            else:
                warning = f"the previous {pack_id}/ folder had no extraction marker (partial or hand-made): kept at {os.path.basename(aside)}; delete it after a look"
        else:
            os.replace(tmp, dest)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return count, warning


def licence_for(pack, rel_path, sha, retrieved_iso, host, ships="false"):
    cc0 = pack["licence_id"] == "CC0-1.0"
    return {
        "path": rel_path,
        "sha256": sha,
        "source_kind": pack.get("source_kind", "cc0_download"),
        "source_url": pack["url"],
        "licence_id": pack["licence_id"],
        "licence_url": pack["licence_url"],
        "author": pack["author"],
        "attribution_text": pack.get("attribution", ""),
        "retrieved_at_utc": retrieved_iso,
        "account_or_plan": "",
        "provider": host,
        "generator_model": "",
        "job_id": "",
        "ai_generated": "false",
        "commercial_ok": "true",
        "training_ban": "false" if cc0 else "unknown",
        "redistribution": pack.get("redistribution", "embedded_only"),
        "ships": ships,
        "notes": " ".join(x for x in (f"pack {pack['id']}", "(stale mirror copy)" if pack.get("stale") else "", pack.get("notes", "")) if x),
    }


def find_inbox_file(inbox, pack):
    if not os.path.isdir(inbox):
        return None, "inbox folder does not exist"
    names = sorted(n for n in os.listdir(inbox) if os.path.isfile(os.path.join(inbox, n)))
    if pack.get("file"):
        hits = [n for n in names if n == pack["file"]]
    else:
        rx = re.compile(pack["file_regex"])
        hits = [n for n in names if rx.search(n)]
    if not hits:
        return None, "not in the inbox yet"
    if len(hits) > 1:
        return None, "more than one inbox file matches: " + ", ".join(hits)
    return os.path.join(inbox, hits[0]), None


def save_receipts(ctx):
    C.write_json_atomic(ctx["receipts_path"], ctx["receipts"])


def record_receipt(ctx, pack, fname, rel, nbytes, sha, final_url, iso, pinned_by):
    ctx["receipts"]["files"] = [r for r in ctx["receipts"]["files"] if r.get("id") != pack["id"]] + [{
        "id": pack["id"], "url": pack["url"], "final_url": final_url, "file": fname, "path": rel, "bytes": nbytes,
        "sha256": sha, "licence_id": pack["licence_id"], "retrieved_at_utc": iso, "pinned_by": pinned_by,
    }]
    save_receipts(ctx)  # at once: a later failure (or crash) must not lose the pin of a file already in place


def after_steps(pack, ctx, res, final, sha, iso):
    """Idempotent steps for a pack whose file is in place: its licence row, and with --extract the unpacked folder and
    the folder's licence row. Run on every run, so a step that failed before is retried."""
    host = urllib.parse.urlparse(pack["url"]).hostname or ""
    res["licence_rows"] = []

    def row_step(row):
        if not ctx["tool"]:
            res["licence_rows"].append({"path": row["path"], "status": "skipped", "row": row})
            return
        status, detail = C.ensure_licence_row(ctx["tool"], ctx["ws"], row)
        res["licence_rows"].append({"path": row["path"], "status": status, **({"detail": detail} if detail else {})})
        if status == "failed":
            res["status"] = "licence_row_failed"

    row_step(licence_for(pack, res["path"], sha, iso, host))
    if ctx["extract"]:
        try:
            count, warning = extract_pack(final, ctx["out"], pack["id"], sha, iso)
        except (C.CliFail, zipfile.BadZipFile, OSError, EOFError) as e:
            res.update(status="failed", reason=f"extract: {e}")
            return res
        if count is not None:
            res["extracted_files"] = count
        if warning:
            res["warning"] = " ".join(x for x in (res.get("warning"), warning) if x)
        res["extracted_to"] = C.rel_posix(ctx["ws"], os.path.join(ctx["out"], pack["id"])) + "/"
        row_step(licence_for(pack, res["extracted_to"], "", iso, host))
    return res


def process(pack, ctx):
    """Fetch or take one pack. Returns the per-pack result dict."""
    retrieved = C.now_utc()
    res = {"id": pack["id"], "status": "", "manual": bool(pack.get("manual")), "licence_id": pack["licence_id"]}
    why = policy_refusal(pack, retrieved)
    if why:
        res.update(status="refused", reason=why)
        return res
    out = ctx["out"]
    prior = next((r for r in ctx["receipts"]["files"] if r.get("id") == pack["id"]), None)
    pinned = pack.get("sha256") or (prior or {}).get("sha256") or ""
    pin_source = "manifest" if pack.get("sha256") else ("receipt" if prior else "")
    manual = bool(pack.get("manual"))

    # A pack with a receipt whose file is still in --out needs neither the network nor the inbox again.
    if prior and prior.get("file") and os.path.isfile(os.path.join(out, prior["file"])):
        final = os.path.join(out, prior["file"])
        res["path"] = C.rel_posix(ctx["ws"], final)
        have = C.sha256_file(final)
        if pinned and have != pinned:
            res.update(status="hash_changed", sha256=have, expected=pinned, reason="the file on disk differs from the pinned hash; nothing was overwritten")
            return res
        if manual:
            src, _ = find_inbox_file(ctx["inbox"], pack)
            if src and C.sha256_file(src) != have:
                res.update(status="hash_changed", sha256=C.sha256_file(src), expected=have,
                           reason=f"{os.path.basename(src)} in the inbox differs from the pinned pack (a newer build?); nothing was overwritten: move the old pack and its receipt aside to take it")
                return res
        if prior.get("sha256") == have:
            res.update(status="unchanged", sha256=have, bytes=os.path.getsize(final))
            return after_steps(pack, ctx, res, final, have, prior.get("retrieved_at_utc") or C.utc_iso(retrieved))

    tmp = None
    if manual:
        src, why = find_inbox_file(ctx["inbox"], pack)
        if not src:
            res.update(status="waiting", reason=f"{why}: a human downloads it from {pack['url']} into {C.rel_posix(ctx['ws'], ctx['inbox'])}")
            return res
        fname = os.path.basename(src)
    else:
        fname = pack.get("file") or os.path.basename(urllib.parse.urlparse(pack["url"]).path)
    final = os.path.join(out, fname)
    res["path"] = C.rel_posix(ctx["ws"], final)
    if os.path.exists(final) and pinned:
        have = C.sha256_file(final)
        if have != pinned:
            res.update(status="hash_changed", sha256=have, expected=pinned, reason="the file on disk differs from the pinned hash; nothing was overwritten")
            return res
    try:
        if manual:
            nbytes = os.path.getsize(src)
            sha = C.sha256_file(src)
            final_url = pack["url"]
        else:
            tmp = os.path.join(out, f".{fname}.part-{os.getpid()}")
            nbytes, sha, final_url = download(pack["url"], tmp, ctx["timeout"])
        if pack.get("bytes") and nbytes != pack["bytes"]:
            res.update(status="size_mismatch", bytes=nbytes, expected_bytes=pack["bytes"], sha256=sha, reason="size differs from the manifest; nothing was written")
            return res
        if pinned and sha != pinned:
            res.update(status="hash_changed", sha256=sha, expected=pinned, reason=f"sha256 differs from the {pin_source} pin; nothing was written")
            return res
        if os.path.exists(final):
            if C.sha256_file(final) != sha:
                res.update(status="hash_changed", sha256=sha, reason="a different file already sits at the target; nothing was overwritten")
                return res
        elif manual:
            part = os.path.join(out, f".{fname}.part-{os.getpid()}")
            shutil.copyfile(src, part)
            os.replace(part, final)
        else:
            os.replace(tmp, final)
            tmp = None
    except C.CliFail as e:
        res.update(status="failed", reason=str(e))
        return res
    except (OSError, ValueError, http.client.HTTPException) as e:
        res.update(status="failed", reason=f"{type(e).__name__}: {e}")
        return res
    finally:
        for leftover in (tmp, os.path.join(out, f".{fname}.part-{os.getpid()}")):
            if leftover and os.path.exists(leftover):
                os.remove(leftover)
    iso = C.utc_iso(retrieved)
    res.update(status="fetched" if not manual else "taken_from_inbox", bytes=nbytes, sha256=sha,
               pin="first_fetch" if not pinned else pin_source, retrieved_at_utc=iso)
    if not pinned:
        res["warning"] = f"no pinned sha256: pinned {sha} on first fetch (trust on first use); copy it into the manifest after a human check"
    record_receipt(ctx, pack, fname, res["path"], nbytes, sha, final_url, iso, "first_fetch" if not pinned else pin_source)
    return after_steps(pack, ctx, res, final, sha, iso)


def main(argv):
    ap = argparse.ArgumentParser(prog="fetch_cc0_anims.py", description=__doc__.split("\n")[0])
    ap.add_argument("--manifest", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--only")
    ap.add_argument("--inbox")
    ap.add_argument("--extract", action="store_true")
    ap.add_argument("--allow-stale", action="store_true")
    ap.add_argument("--project")
    ap.add_argument("--license-row")
    ap.add_argument("--no-license-row", action="store_true")
    ap.add_argument("--timeout", type=float, default=120.0)
    a = ap.parse_args(argv)

    ws = os.path.abspath(a.project) if a.project else C.find_workspace(os.getcwd())
    try:
        with open(a.manifest, encoding="utf-8-sig") as fh:
            manifest = json.load(fh)
    except OSError as e:
        raise C.CliFail(f"manifest not readable: {a.manifest} ({e.strerror})", C.EXIT_USAGE)
    except ValueError as e:
        raise C.CliFail(f"manifest is not valid JSON: {e}", C.EXIT_USAGE)
    packs = validate_manifest(manifest)
    want = None
    if a.only:
        want = [x.strip() for x in a.only.split(",") if x.strip()]
        unknown = [w for w in want if w not in {p["id"] for p in packs}]
        if unknown:
            raise C.CliFail("unknown pack id(s): " + ", ".join(unknown), C.EXIT_USAGE)
        packs = [p for p in packs if p["id"] in want]
    tool = None
    if not a.no_license_row:
        tool = C.license_row_tool(ws, a.license_row)
        if not tool:
            raise C.CliFail("license_row.mjs not found (install the kit, or pass --license-row or --no-license-row); nothing was fetched", C.EXIT_INFRA)
        if not shutil.which("node"):
            raise C.CliFail("node not found on PATH; nothing was fetched", C.EXIT_INFRA)
    out = os.path.abspath(a.out)
    if ws and not C.is_inside(out, ws):
        raise C.CliFail("--out must be inside the workspace (licence rows use workspace-relative paths)", C.EXIT_USAGE)
    os.makedirs(out, exist_ok=True)
    receipts_path = os.path.join(out, "downloads.json")
    ctx = {
        "ws": ws, "out": out, "tool": tool, "timeout": a.timeout, "extract": a.extract,
        "inbox": os.path.abspath(a.inbox) if a.inbox else os.path.join(ws or os.getcwd(), "_Inbox", "CC0"),
        "receipts": load_receipts(receipts_path), "receipts_path": receipts_path,
    }
    results = []
    for p in packs:
        if p.get("stale") and not a.allow_stale and not want:
            results.append({"id": p["id"], "status": "skipped_stale", "manual": bool(p.get("manual")), "licence_id": p["licence_id"],
                            "reason": "stale mirror copy (older build, other bone names): fetched only with --allow-stale or --only " + p["id"]})
            continue
        results.append(process(p, ctx))
    save_receipts(ctx)
    bad = [r for r in results if r["status"] in ("refused", "hash_changed", "size_mismatch", "failed", "licence_row_failed")]
    ok = not bad
    result = {"ok": ok, "exit": C.EXIT_OK if ok else C.EXIT_FAIL, "receipts": C.rel_posix(ws, receipts_path), "files": results}
    text = [f"{r['status']:<18} {r['id']}  {r.get('path', '')}  {r.get('reason', r.get('warning', ''))}".rstrip() for r in results]
    text.append(("PASS" if ok else "FAIL") + f": {len(results)} pack(s), {len(bad)} problem(s)")
    C.emit(result, a.json, text)
    return result["exit"]


if __name__ == "__main__":
    C.run_main(main)
