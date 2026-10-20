"""fetch-cc0 (Poly Haven, ambientCG, Kenney) and fetch-refs (sha256-pinned reference downloads).

- Poly Haven: GET https://api.polyhaven.com/info/<id> and /files/<id>; every file has url, md5 and size, which are
  verified. API ToS §2.4 requires a User-Agent that names the software; assetgen sends game-studio-kit-assetgen/<ver>.
  Resolutions are read from the keys (some HDRIs go to 16k) [V research_generation_apis R9]. Assets are CC0.
- ambientCG: GET https://ambientcg.com/api/v2/full_json?id=<id>&include=downloadData, then the zip for the attribute
  <RES>-<FMT> (e.g. 2K-JPG). No checksum is published, so size is checked and sha256 recorded. CC0.
- Kenney: the pack page https://kenney.nl/assets/<slug> links the zip; only kenney.nl hosts are accepted. CC0,
  attribution optional, never use Kenney's logo.
Every file gets a licence row through Tools/kit/ship/license_row.mjs; without that writer nothing is downloaded.
"""

import os
import re
import urllib.parse
from pathlib import Path

from . import licence, net
from .errors import InfraError, Refused, UsageError
from .sniff import sniff_file
from .util import file_stamp, is_inside, read_json, sha256_file, utc_iso

POLYHAVEN_API = "https://api.polyhaven.com"
AMBIENTCG_API = "https://ambientcg.com/api/v2/full_json"
KENNEY_PAGE = "https://kenney.nl/assets/"
BUNDLE_KEYS = {"blend", "gltf", "fbx", "usd", "mtlx", "tonemapped", "backplates", "colorchart"}
ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$")


def _safe_rel(rel):
    rel = rel.replace("\\", "/")
    if rel.startswith("/") or re.match(r"^[A-Za-z]:", rel) or any(p in ("..", "") for p in rel.split("/")):
        raise Refused(f"refusing an unsafe relative path from the source: {rel!r}", reason="unsafe_path")
    return rel


def _md5(path):
    import hashlib

    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _existing(dest, md5):
    return {"file": dest, "sha256": sha256_file(dest), "bytes": dest.stat().st_size, "kind": sniff_file(dest)[0], "md5_ok": bool(md5) or None, "skipped": True}


def _get_named(url, dest, md5=None, size=None, *, pin_sha256=None, replace=False, backup=None, label=None):
    """Download to an exact file name from a trusted CC0 host; the magic bytes must not contradict the extension.

    An existing file is never overwritten silently: identical content is skipped, different content (an owner edit) is
    refused with exists_differs unless replace is set, and then the old file is moved to `backup(dest)` first."""
    dest = Path(dest)
    if dest.is_file() and ((md5 and _md5(dest) == str(md5).lower()) or (not md5 and size and dest.stat().st_size == int(size))):
        return _existing(dest, md5)
    info = net.download(url, dest.parent, "." + dest.stem + ".dl", expected_md5=md5, expected_size=size)
    want_ext = dest.suffix.lower().lstrip(".")
    got = info["ext"]
    if got not in ("bin",) and want_ext and got != want_ext and not (got == "jpg" and want_ext == "jpeg"):
        if not (want_ext in ("gltf", "bin", "blend", "mtlx", "txt", "usda") and got == "bin"):
            info["file"].unlink(missing_ok=True)
            raise Refused(f"{dest.name}: the content is {info['kind']}, not .{want_ext}", reason="type_mismatch")
    if pin_sha256 and info["sha256"] != pin_sha256.lower():
        info["file"].unlink(missing_ok=True)
        raise Refused(f"{dest.name}: sha256 differs from the pinned value", reason="sha_mismatch")
    if dest.is_file():
        if sha256_file(dest) == info["sha256"]:
            info["file"].unlink(missing_ok=True)
            return _existing(dest, md5)
        if not replace:
            info["file"].unlink(missing_ok=True)
            raise Refused(f"{label or dest.name} already exists with different content (edited?); nothing was overwritten. "
                          f"Pass --replace to move the old file to .harness/backups/cc0/ and replace it.", reason="exists_differs")
        backup(dest)
        info["replaced"] = True
    else:
        info["created"] = True
    os.replace(info["file"], dest)
    info["file"] = dest
    return info


def _polyhaven_plan(a):
    info = net.get_json(f"{POLYHAVEN_API}/info/{a.id}")
    files = net.get_json(f"{POLYHAVEN_API}/files/{a.id}")
    if not isinstance(info, dict) or not isinstance(files, dict) or not info.get("name"):
        raise UsageError(f"Poly Haven has no asset {a.id!r}")
    authors = ", ".join(sorted((info.get("authors") or {}).keys())) or "Poly Haven"
    res = (a.res or "4k").lower()
    kind = {0: "hdri", 1: "texture", 2: "model"}.get(info.get("type"), "unknown")
    plan = []
    if kind == "hdri":
        fmt = (a.format or "hdr").lower()
        levels = files.get("hdri") or {}
        if res not in levels:
            raise UsageError(f"{a.id}: resolution {res} not offered; available: {', '.join(sorted(levels))}")
        f = levels[res].get(fmt)
        if not f:
            raise UsageError(f"{a.id}: format {fmt} not offered at {res}; available: {', '.join(sorted(levels[res]))}")
        plan.append({"url": f["url"], "name": f"{a.id}_{res}.{fmt}", "md5": f.get("md5"), "size": f.get("size")})
    elif kind == "texture":
        fmt = (a.format or "jpg").lower()
        want = {m.strip().lower() for m in (a.maps or "").split(",") if m.strip()}
        for mname, levels in sorted(files.items()):
            if mname.lower() in BUNDLE_KEYS or not isinstance(levels, dict):
                continue
            if want and mname.lower() not in want:
                continue
            f = (levels.get(res) or {}).get(fmt) or (levels.get(res) or {}).get("png")
            if not f or "url" not in f:
                continue
            ext = f["url"].split("?", 1)[0].rsplit(".", 1)[-1].lower()
            plan.append({"url": f["url"], "name": f"{a.id}_{mname}_{res}.{ext}", "md5": f.get("md5"), "size": f.get("size")})
        if not plan:
            raise UsageError(f"{a.id}: no texture maps at {res}/{fmt}")
    elif kind == "model":
        g = ((files.get("gltf") or {}).get(res) or {}).get("gltf")
        if not g:
            raise UsageError(f"{a.id}: no glTF at {res}; available: {', '.join(sorted(files.get('gltf') or {}))}")
        main = _safe_rel(g["url"].split("?", 1)[0].rsplit("/", 1)[-1])
        plan.append({"url": g["url"], "name": main, "md5": g.get("md5"), "size": g.get("size")})
        for rel, f in sorted((g.get("include") or {}).items()):
            plan.append({"url": f["url"], "name": _safe_rel(rel), "md5": f.get("md5"), "size": f.get("size")})
    else:
        raise UsageError(f"{a.id}: unknown Poly Haven asset type {info.get('type')!r}")
    return plan, authors, f"https://polyhaven.com/a/{a.id}", kind


def _ambientcg_plan(a):
    j = net.get_json(f"{AMBIENTCG_API}?id={urllib.parse.quote(a.id)}&include=downloadData")
    assets = (j or {}).get("foundAssets") or []
    asset = next((x for x in assets if x.get("assetId") == a.id), assets[0] if assets else None)
    if not asset:
        raise UsageError(f"ambientCG has no asset {a.id!r}")
    downloads = (((asset.get("downloadFolders") or {}).get("default") or {}).get("downloadFiletypeCategories") or {}).get("zip", {}).get("downloads") or []
    attr = f"{(a.res or '2k').upper()}-{(a.format or 'jpg').upper()}"
    d = next((x for x in downloads if str(x.get("attribute", "")).upper() == attr), None)
    if not d:
        raise UsageError(f"{a.id}: {attr} not offered; available: {', '.join(sorted(str(x.get('attribute')) for x in downloads))}")
    host = urllib.parse.urlsplit(d["downloadLink"]).hostname or ""
    if not (host == "ambientcg.com" or host.endswith(".ambientcg.com")):
        raise Refused(f"ambientCG download link points to {host}", reason="unsafe_host")
    name = _safe_rel(d.get("fileName") or f"{a.id}_{attr}.zip")
    return [{"url": d["downloadLink"], "name": name, "md5": None, "size": d.get("size")}], "ambientCG", f"https://ambientcg.com/view?id={a.id}", "material"


def _kenney_plan(a):
    page = f"{KENNEY_PAGE}{a.id}"
    url = a.url
    if not url:
        html = net.get_text(page)
        links = [urllib.parse.urljoin(page, m) for m in re.findall(r"href=[\"']([^\"']+?\.zip)(?:\?[^\"']*)?[\"']", html, re.I)]
        links = [u for u in links if (urllib.parse.urlsplit(u).hostname or "").endswith("kenney.nl")]
        if not links:
            raise UsageError(f"no kenney.nl zip link on {page}; pass --url <zip url from that page>")
        url = links[0]
    host = urllib.parse.urlsplit(url).hostname or ""
    if not (host == "kenney.nl" or host.endswith(".kenney.nl")):
        raise Refused(f"Kenney downloads must come from kenney.nl (got {host})", reason="unsafe_host")
    name = _safe_rel(urllib.parse.unquote(url.split("?", 1)[0].rsplit("/", 1)[-1]) or f"{a.id}.zip")
    return [{"url": url, "name": name, "md5": None, "size": None, "sha256": a.sha256}], "Kenney (www.kenney.nl)", page, "pack"


def fetch_cc0(ctx, a):
    L = ctx.layout
    if a.source not in ("polyhaven", "ambientcg", "kenney"):
        raise UsageError("--source must be polyhaven, ambientcg or kenney")
    if not a.id or not ID_RE.match(a.id):
        raise UsageError("--id must be the source's asset id or slug (letters, digits, _ . -)")
    if not a.dry_run and not licence.writer_available(L):
        raise InfraError("Tools/kit/ship/license_row.mjs (and node) are needed to record the licence row; nothing was downloaded", reason="licence_writer_missing")
    # --out is W-relative and must stay inside W, outside kit and harness files (CONTRACTS §1.4: nothing is written
    # outside W without a prompt). Every destination and licence path is computed before anything is downloaded.
    out_dir = L.output_target(a.out, "--out") if a.out else (L.cc0 / a.source / a.id).resolve()
    plan, author, source_url, kind = {"polyhaven": _polyhaven_plan, "ambientcg": _ambientcg_plan, "kenney": _kenney_plan}[a.source](a)
    dests = []
    for p in plan:
        dest = (out_dir / p["name"]).resolve()
        if not is_inside(dest, out_dir) or L.protected(dest):
            raise Refused(f"refusing a destination outside --out: {p['name']!r}", reason="unsafe_path")
        dests.append((p, dest, L.rel(dest)))
    if a.dry_run:
        return {"ok": True, "dry_run": True, "source": a.source, "id": a.id, "kind": kind, "author": author, "source_url": source_url,
                "out": L.rel(out_dir), "files": [{"name": p["name"], "path": rel, "size": p.get("size"), "md5": p.get("md5"), "exists": dest.is_file()} for p, dest, rel in dests]}
    stamp = file_stamp()

    def backup(dest):
        target = L.backups / "cc0" / stamp / L.rel(dest)
        target.parent.mkdir(parents=True, exist_ok=True)
        os.replace(dest, target)

    files, rows, created = [], [], []
    try:
        for p, dest, rel in dests:
            info = _get_named(p["url"], dest, p.get("md5"), p.get("size"), pin_sha256=p.get("sha256"), replace=bool(getattr(a, "replace", False)),
                              backup=backup, label=rel)
            if info.get("created"):
                created.append(dest)
            files.append({"path": rel, "sha256": info["sha256"], "bytes": info["bytes"], "kind": info["kind"], "md5_ok": info.get("md5_ok"),
                          "skipped": bool(info.get("skipped")), "replaced": bool(info.get("replaced"))})
    except Exception:
        for f in created:  # never leave CC0 files without their licence rows
            f.unlink(missing_ok=True)
        raise
    retrieved = utc_iso()
    note = {"kenney": "CC0; attribution optional; never use the Kenney logo", "polyhaven": "CC0; no attribution required", "ambientcg": "CC0"}[a.source]
    for f in files:
        row = licence.cc0_row(f["path"], f["sha256"], a.source, source_url, author, retrieved, bool(a.ships), note + ("" if f["md5_ok"] else "; no upstream checksum, sha256 recorded"))
        licence.write_row(L, row, replace=True)
        rows.append(f["path"])
    return {"ok": True, "source": a.source, "id": a.id, "kind": kind, "author": author, "source_url": source_url, "out": L.rel(out_dir), "files": files, "licence_rows": rows}


REF_KINDS = ("png", "jpeg", "webp")
REFS_PREFIX = "artsource/_refs/"


def _ref_dest(L, raw):
    """A manifest path lands under ArtSource/_refs/ only (a leading ArtSource/_refs/ is accepted). No segment may start
    with '.', so .harness/, .claude/ and .mcp.json can never be reached, and nothing else in W can be overwritten."""
    rel = _safe_rel(raw)
    if rel.lower().startswith(REFS_PREFIX):
        rel = rel[len(REFS_PREFIX):]
    if not rel or any(seg.startswith(".") for seg in rel.split("/")):
        raise Refused(f"refusing reference path {raw!r}: no hidden files or folders", reason="unsafe_path")
    root = L.refs.resolve()
    dest = (L.refs / rel).resolve()
    if not is_inside(root, L.ws) or not is_inside(dest, root) or L.protected(dest):
        raise Refused(f"reference path {raw!r} leaves ArtSource/_refs/", reason="unsafe_path")
    return dest


def fetch_refs(ctx, a):
    """Manifest {schema:1, refs:[{url, path, sha256?, credit, take}]}: private style references, never shipped. Every
    path is checked before anything is downloaded; files land under ArtSource/_refs/ and must be png, jpeg or webp."""
    L = ctx.layout
    man = read_json(L.input_file(a.manifest, "--manifest", 1 << 20))
    refs = man.get("refs") if isinstance(man, dict) else None
    if not isinstance(refs, list):
        raise UsageError("manifest must be {schema:1, refs:[{url, path, sha256?, credit, take}]}")
    planned = [(r, _ref_dest(L, str(r.get("path", "")))) for r in refs]
    out, failures = [], []
    for r, dest in planned:
        rel = L.rel(dest)
        pin = (r.get("sha256") or "").lower() or None
        if dest.is_file():
            have = sha256_file(dest)
            if pin and have != pin:
                failures.append({"path": rel, "message": "existing file differs from the pinned sha256 (never overwritten)"})
                continue
            out.append({"path": rel, "sha256": have, "skipped": True})
            continue
        if a.dry_run:
            out.append({"path": rel, "url_host": urllib.parse.urlsplit(r.get("url", "")).hostname, "planned": True})
            continue
        try:
            info = net.download(r["url"], dest.parent, "." + dest.stem + ".dl", max_bytes=64 * 1024 * 1024)
        except Exception as e:
            failures.append({"path": rel, "message": f"{type(e).__name__}: {getattr(e, 'message', str(e))[:200]}"})
            continue
        if info["kind"] not in REF_KINDS:
            info["file"].unlink(missing_ok=True)
            failures.append({"path": rel, "message": f"content is {info['kind']}, not an image (png, jpeg, webp); deleted"})
            continue
        if pin and info["sha256"] != pin:
            info["file"].unlink(missing_ok=True)
            failures.append({"path": rel, "message": "sha256 differs from the pin"})
            continue
        os.replace(info["file"], dest)
        out.append({"path": rel, "sha256": info["sha256"], "kind": info["kind"], "credit": r.get("credit", ""), "take": r.get("take", "")})
    return {"ok": not failures, "refs": out, "failures": failures, "note": "references are private style inputs under ArtSource/_refs/; they never ship"}
