"""fetch_cc0_audio.py: fetch CC0 sounds from Freesound or Kenney with throttling, receipts and licence rows.

    uv run --project Tools/kit/audio Tools/kit/audio/fetch_cc0_audio.py --source freesound --id <sound id> --out <dir>
        [--original] [--receipts <dir>] [--ships] [--no-licence] [--json]
    uv run --project Tools/kit/audio Tools/kit/audio/fetch_cc0_audio.py --source freesound --search "<query>" [--max 15]
    uv run --project Tools/kit/audio Tools/kit/audio/fetch_cc0_audio.py --source kenney --id <pack slug> --out <dir>

Rules (research_generation_apis.md §4.4, §6 and fact-check R6):
- Freesound: CC0 only. Search uses the filter license:"Creative Commons 0"; every fetch re-reads the sound's licence
  and refuses anything that is not CC0 (exit 1). Token auth (header, never the URL) for metadata and HQ previews;
  original files need an OAuth2 bearer token (FREESOUND_OAUTH_TOKEN, a human step) and use the stricter bucket.
- Rate limits: at most 1 request/s (general limit 60/min, 2,000/day) and 1 original download per 2 s (30/min,
  500/day), enforced across processes by a lock and a state file (.harness/fetch_cc0_audio.json).
- Keys: FREESOUND_API_KEY from Windows Credential Manager (keyring service game-studio-kit) first, then env. Only the
  storage method is ever printed. The API host is fixed (no override): http_get refuses to send an Authorization
  header anywhere but https://freesound.org, and drops it on any redirect that changes host or scheme.
- Kenney: the pack page must state CC0; the zip must come from https://kenney.nl (or a subdomain); only audio files
  and licence texts are extracted, with zip-slip protection and caps on total size, member count, compression ratio
  and free disk space (checked before anything is written).
- Every fetch writes a receipt (Docs/audio/receipts/<source>_<id>.json) and one licence row per file through
  Tools/kit/ship/license_row.mjs.
Exit codes: 0 ok, 1 refused (not CC0, not found, bad content), 2 usage, 3 INFRA (key, network, quota, disk), 75 lock
held.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import os
import re
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audio_common as ac  # noqa: E402

TOOL_VERSION = "1.0"
USER_AGENT = f"game-studio-kit-fetch_cc0_audio/{TOOL_VERSION}"
CC0_URL = "https://creativecommons.org/publicdomain/zero/1.0/"
FREESOUND_API = "https://freesound.org/apiv2"
AUTH_HOSTS = frozenset({"freesound.org"})  # the only host that ever receives the Freesound key or OAuth token
LOOPBACK = frozenset({"127.0.0.1", "localhost", "::1"})
KENNEY_BASE = "https://kenney.nl"
KENNEY_HOST = "kenney.nl"
AUDIO_EXTS = {".wav", ".ogg", ".mp3", ".flac", ".aif", ".aiff"}
LICENCE_NAMES = re.compile(r"^(licen[cs]e|readme)[^/]*\.txt$", re.I)
MAX_AUDIO_BYTES = 200 * 1024 * 1024
MAX_ZIP_BYTES = 800 * 1024 * 1024
MAX_PAGE_BYTES = 4 * 1024 * 1024
MAX_EXTRACT_BYTES = 2 * 1024 ** 3  # total uncompressed size of the members a Kenney pack may write
MAX_MEMBERS = 5000  # extracted members per pack
MAX_ZIP_ENTRIES = 50000  # entries of any kind in the zip directory
MAX_RATIO = 200  # uncompressed/compressed per member; real audio packs sit far below this
DISK_MARGIN_BYTES = 10 * 1024 ** 3  # free space that must remain after extraction
BUCKETS = {  # interval seconds, daily cap (kept a little under the provider limit)
    "freesound": (1.0, 1990),
    "freesound_original": (2.0, 490),
    "kenney": (1.0, None),
}


# ---------------------------------------------------------------- throttle (cross-process)

class Throttle:
    """Serialises requests per bucket through `<state>.lock` and records the last request time and a per-UTC-day
    count in the state file. The lock is held while sleeping, so parallel processes queue instead of bursting."""

    def __init__(self, state_path: Path, clock=time.time, sleep=time.sleep, lock_wait_s: float = 120.0):
        self.state_path, self.clock, self.sleep, self.lock_wait_s = state_path, clock, sleep, lock_wait_s

    def _lock(self):
        lock = self.state_path.with_name(self.state_path.name + ".lock")
        lock.parent.mkdir(parents=True, exist_ok=True)
        deadline = time.monotonic() + self.lock_wait_s
        while True:
            try:
                fd = os.open(str(lock), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.write(fd, f"{os.getpid()} {ac.utc_iso()}\n".encode())
                os.close(fd)
                return lock
            except FileExistsError:
                try:
                    if time.time() - lock.stat().st_mtime > 60:
                        lock.unlink(missing_ok=True)
                        continue
                except OSError:
                    pass
                if time.monotonic() > deadline:
                    raise ac.ToolError(f"throttle lock held: {lock}", ac.EXIT_BLOCKED)
                time.sleep(0.1)
            except PermissionError:
                if time.monotonic() > deadline:
                    raise ac.ToolError(f"throttle lock busy: {lock}", ac.EXIT_BLOCKED) from None
                time.sleep(0.1)

    @staticmethod
    def clean_state(raw, now: float) -> dict:
        """The state file as a well-formed dict whatever it held: a non-dict becomes {schema:1}; each known bucket
        becomes {day:str, count:int>=0, last:finite float, never after `now`} (a clock stepped back or a copied
        file must not make the next request sleep for hours while holding the lock)."""
        state = raw if isinstance(raw, dict) else {}
        state["schema"] = 1
        for name in BUCKETS:
            b = state.get(name)
            b = dict(b) if isinstance(b, dict) else {}
            day = b.get("day")
            b["day"] = day if isinstance(day, str) else ""
            count = b.get("count")
            ok_count = isinstance(count, (int, float)) and not isinstance(count, bool) and math.isfinite(count) \
                and count >= 0
            b["count"] = int(count) if ok_count else 0
            try:
                last = float(b.get("last", 0.0))
            except (TypeError, ValueError):
                last = 0.0
            if not math.isfinite(last) or last < 0:
                last = 0.0
            b["last"] = min(last, now)
            state[name] = b
        return state

    def wait(self, *buckets: str) -> None:
        lock = self._lock()
        try:
            try:
                raw = json.loads(self.state_path.read_text(encoding="utf-8"))
            except (OSError, UnicodeDecodeError, json.JSONDecodeError):
                raw = None
            state = self.clean_state(raw, self.clock())
            day = ac.now_utc().strftime("%Y-%m-%d")
            delay = 0.0
            for name in buckets:
                interval, cap = BUCKETS[name]
                b = state[name]
                if b["day"] != day:
                    b["day"], b["count"] = day, 0
                if cap is not None and b["count"] >= cap:
                    raise ac.ToolError(f"{name}: daily request budget ({cap}) used up for {day} UTC; try tomorrow",
                                       ac.EXIT_INFRA, quota=name)
                delay = max(delay, min(interval, b["last"] + interval - self.clock()))
            if delay > 0:
                self.sleep(delay)
            t = self.clock()
            for name in buckets:
                state[name]["last"] = t
                state[name]["count"] += 1
            ac.write_json_atomic(self.state_path, state)
        finally:
            lock.unlink(missing_ok=True)


# ---------------------------------------------------------------- HTTP

class _DropAuthOnHostChange(urllib.request.HTTPRedirectHandler):
    """Follows redirects only to https (or loopback http) and drops Authorization whenever the scheme or host changes
    or the target is not an auth host."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        check_url(newurl)
        new = super().redirect_request(req, fp, code, msg, headers, newurl)
        if new is None:
            return new
        old_p, new_p = urllib.parse.urlsplit(req.full_url), urllib.parse.urlsplit(newurl)
        if (old_p.scheme, old_p.netloc) != (new_p.scheme, new_p.netloc) or not auth_allowed(newurl):
            for h in list(new.headers):
                if h.lower() == "authorization":
                    del new.headers[h]
            for h in list(new.unredirected_hdrs):
                if h.lower() == "authorization":
                    del new.unredirected_hdrs[h]
        return new


_OPENER = urllib.request.build_opener(_DropAuthOnHostChange)


def check_url(url: str) -> None:
    parts = urllib.parse.urlsplit(url)
    local = parts.hostname in LOOPBACK
    if parts.scheme != "https" and not (parts.scheme == "http" and local):
        raise ac.ToolError(f"refusing non-https URL {parts.scheme}://{parts.hostname}", ac.EXIT_FAIL)


def auth_allowed(url: str) -> bool:
    """True only for https://<auth host> (loopback http counts as https, which matters only when a test adds a
    loopback host to AUTH_HOSTS)."""
    parts = urllib.parse.urlsplit(url)
    host = (parts.hostname or "").lower()
    return host in AUTH_HOSTS and (parts.scheme == "https" or (parts.scheme == "http" and host in LOOPBACK))


def http_get(url: str, headers: dict | None = None, max_bytes: int = MAX_PAGE_BYTES, timeout: float = 60.0) -> tuple[bytes, str]:
    """GET → (body, final url). Maps HTTP errors to kit exit codes; never includes headers in messages. Refuses to send
    an Authorization header to any URL but https://freesound.org (AUTH_HOSTS)."""
    check_url(url)
    if any(k.lower() == "authorization" for k in (headers or {})) and not auth_allowed(url):
        parts = urllib.parse.urlsplit(url)
        raise ac.ToolError(f"refusing to send credentials to {parts.scheme}://{parts.hostname} (keys go only to "
                           f"https://{', https://'.join(sorted(AUTH_HOSTS))})", ac.EXIT_FAIL, reason="auth_host")
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, **(headers or {})})
    safe = urllib.parse.urlsplit(url)._replace(query="").geturl()
    try:
        with _OPENER.open(req, timeout=timeout) as r:
            buf = io.BytesIO()
            while True:
                chunk = r.read(1 << 16)
                if not chunk:
                    break
                buf.write(chunk)
                if buf.tell() > max_bytes:
                    raise ac.ToolError(f"{safe}: response larger than {max_bytes} bytes", ac.EXIT_FAIL)
            return buf.getvalue(), r.geturl()
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            raise ac.ToolError(f"{safe}: HTTP {e.code} (key missing, wrong or not allowed)", ac.EXIT_INFRA, http=e.code) from None
        if e.code == 404:
            raise ac.ToolError(f"{safe}: not found", ac.EXIT_FAIL, http=404) from None
        if e.code == 429:
            raise ac.ToolError(f"{safe}: rate limited (HTTP 429); wait and retry", ac.EXIT_INFRA, http=429,
                               retry_after=e.headers.get("Retry-After") if e.headers else None) from None
        raise ac.ToolError(f"{safe}: HTTP {e.code}", ac.EXIT_INFRA, http=e.code) from None
    except (urllib.error.URLError, TimeoutError, OSError) as e:
        raise ac.ToolError(f"{safe}: network error: {getattr(e, 'reason', e)}", ac.EXIT_INFRA) from None


# ---------------------------------------------------------------- helpers

def sniff(data: bytes) -> str | None:
    if data[:4] == b"RIFF" and data[8:12] == b"WAVE":
        return "wav"
    if data[:4] == b"OggS":
        return "ogg"
    if data[:4] == b"fLaC":
        return "flac"
    if data[:3] == b"ID3" or (len(data) > 1 and data[0] == 0xFF and (data[1] & 0xE0) == 0xE0):
        return "mp3"
    if data[:4] == b"FORM" and data[8:12] in (b"AIFF", b"AIFC"):
        return "aiff"
    if data[:4] == b"PK\x03\x04":
        return "zip"
    return None


def slug(text: str, n: int = 40) -> str:
    s = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return (s[:n].rstrip("-")) or "sound"


def is_cc0(licence: str) -> bool:
    v = (licence or "").strip().lower()
    return "publicdomain/zero" in v or v in ("creative commons 0", "cc0", "cc0 1.0", "cc0-1.0")


def default_receipts(ws: Path | None, out: Path) -> Path:
    return (ws / "Docs" / "audio" / "receipts") if ws else (out / "_receipts")


def state_path(ws: Path | None, out: Path) -> Path:
    return (ws / ".harness" / "fetch_cc0_audio.json") if ws else (out / ".fetch_cc0_audio.json")


# ---------------------------------------------------------------- Freesound

def freesound_api() -> str:
    return FREESOUND_API


def freesound_key() -> tuple[str, str]:
    key, storage = ac.get_secret("FREESOUND_API_KEY")
    if not key:
        raise ac.ToolError("FREESOUND_API_KEY missing: store it in Windows Credential Manager (keyring service "
                           "game-studio-kit) or the user environment (a human step)", ac.EXIT_INFRA, key_storage=storage)
    return key, storage


def freesound_search(query: str, max_n: int, throttle: Throttle) -> dict:
    key, storage = freesound_key()
    params = urllib.parse.urlencode({
        "query": query, "filter": 'license:"Creative Commons 0"', "page_size": max(1, min(max_n, 150)),
        "fields": "id,name,license,username,duration,samplerate,channels,tags",
    })
    throttle.wait("freesound")
    body, _ = http_get(f"{freesound_api()}/search/?{params}", {"Authorization": f"Token {key}"})
    data = json.loads(body.decode("utf-8"))
    results = [{"id": r.get("id"), "name": r.get("name"), "username": r.get("username"),
                "duration_s": r.get("duration"), "samplerate": r.get("samplerate"), "licence": r.get("license")}
               for r in data.get("results", []) if is_cc0(str(r.get("license", "")))]
    return {"ok": True, "source": "freesound", "query": query, "count": data.get("count"), "results": results,
            "key_storage": storage}


def fetch_freesound(sound_id: str, out: Path, args, ws: Path | None, throttle: Throttle) -> dict:
    if not re.fullmatch(r"\d{1,12}", sound_id):
        raise ac.UsageError("--id for freesound is the numeric sound id")
    key, storage = freesound_key()
    fields = "id,name,license,username,url,previews,type,duration,samplerate,channels,filesize"
    throttle.wait("freesound")
    body, _ = http_get(f"{freesound_api()}/sounds/{sound_id}/?fields={fields}", {"Authorization": f"Token {key}"})
    meta = json.loads(body.decode("utf-8"))
    licence = str(meta.get("license", ""))
    if not is_cc0(licence):
        raise ac.ToolError(f"freesound {sound_id} is not CC0 ({licence or 'no licence'}); the kit fetches CC0 only",
                           ac.EXIT_FAIL, reason="not_cc0", licence=licence)
    name = str(meta.get("name") or f"freesound_{sound_id}")
    if args.original:
        token, _ = ac.get_secret("FREESOUND_OAUTH_TOKEN")
        if not token:
            raise ac.ToolError("--original needs an OAuth2 token (FREESOUND_OAUTH_TOKEN, a human step); "
                               "drop --original to fetch the HQ preview", ac.EXIT_INFRA)
        throttle.wait("freesound", "freesound_original")
        data, final = http_get(f"{freesound_api()}/sounds/{sound_id}/download/", {"Authorization": f"Bearer {token}"},
                               max_bytes=MAX_AUDIO_BYTES, timeout=300)
        variant = "original"
    else:
        previews = meta.get("previews") or {}
        variant = next((k for k in ("preview-hq-ogg", "preview-hq-mp3") if previews.get(k)), None)
        if variant is None:
            raise ac.ToolError(f"freesound {sound_id}: no HQ preview in the API response", ac.EXIT_FAIL)
        throttle.wait("freesound")
        data, final = http_get(previews[variant], max_bytes=MAX_AUDIO_BYTES, timeout=300)  # CDN: no key sent
    kind = sniff(data)
    if kind not in ("wav", "ogg", "mp3", "flac", "aiff"):
        raise ac.ToolError(f"freesound {sound_id}: download is not audio ({kind or 'unknown bytes'})", ac.EXIT_FAIL)
    file = out / f"freesound_{sound_id}_{slug(name)}.{kind}"
    ac.write_bytes_atomic(file, data)
    sha = ac.sha256_file(file)
    user = str(meta.get("username") or "")
    page = str(meta.get("url") or f"https://freesound.org/s/{sound_id}/")
    retrieved = ac.utc_iso()
    receipts = Path(args.receipts) if args.receipts else default_receipts(ws, out)
    receipt_path = receipts / f"freesound_{sound_id}.json"
    receipt = {
        "schema": 1, "source": "freesound", "id": sound_id, "name": name, "username": user, "licence_raw": licence,
        "licence_id": "CC0-1.0", "page_url": page, "variant": variant,
        "download_url": urllib.parse.urlsplit(final)._replace(query="").geturl(), "retrieved_at_utc": retrieved,
        "file": ac.rel_path(file, ws), "sha256": sha, "bytes": len(data), "tool": f"fetch_cc0_audio.py {TOOL_VERSION}",
        "api_meta": {k: meta.get(k) for k in ("type", "duration", "samplerate", "channels", "filesize")},
    }
    ac.write_json_atomic(receipt_path, receipt)
    row = {
        "source_kind": "cc0_download", "source_url": page, "licence_id": "CC0-1.0", "licence_url": CC0_URL,
        "author": user, "retrieved_at_utc": retrieved, "provider": "freesound", "ai_generated": "false",
        "commercial_ok": "true", "training_ban": "false", "redistribution": "allowed",
        "ships": "true" if args.ships else "false",
        "notes": f"freesound {sound_id} '{name[:60]}' ({variant}); receipt {ac.rel_path(receipt_path, ws)}",
    }
    lic = ac.write_licence_row(file, row, ws, enabled=not args.no_licence)
    return {"ok": True, "source": "freesound", "id": sound_id, "file": ac.rel_path(file, ws), "sha256": sha,
            "bytes": len(data), "variant": variant, "licence_id": "CC0-1.0", "author": user,
            "receipt": ac.rel_path(receipt_path, ws), "licence_row": lic, "key_storage": storage}


# ---------------------------------------------------------------- Kenney

def _same_site(url: str) -> bool:
    """https://kenney.nl or https://<sub>.kenney.nl (fixed; there is no override)."""
    parts = urllib.parse.urlsplit(url)
    h = (parts.hostname or "").lower()
    return parts.scheme == "https" and (h == KENNEY_HOST or h.endswith("." + KENNEY_HOST))


def _free_bytes(path: Path) -> int:
    p = path.resolve()
    while not p.exists() and p.parent != p:
        p = p.parent
    return shutil.disk_usage(str(p)).free


def plan_extraction(zf: zipfile.ZipFile, slug_id: str, dest: Path) -> list[tuple[zipfile.ZipInfo, PurePosixPath]]:
    """The members to extract, after refusing the zip as a whole (nothing written yet) when it is bomb-shaped: too many
    entries or members, a member compressed more than MAX_RATIO:1, more than MAX_EXTRACT_BYTES in total, or less free
    disk space than the total plus DISK_MARGIN_BYTES. The declared sizes bound what zipfile will actually write."""
    infos = zf.infolist()
    if len(infos) > MAX_ZIP_ENTRIES:
        raise ac.ToolError(f"kenney {slug_id}: {len(infos)} zip entries (cap {MAX_ZIP_ENTRIES}); not extracted",
                           ac.EXIT_FAIL, reason="zip_too_many_entries")
    plan, total = [], 0
    for info in infos:
        rel = safe_member(info.filename)
        if rel is None or info.file_size > MAX_AUDIO_BYTES:
            continue
        if info.file_size > MAX_RATIO * max(info.compress_size, 1) and info.file_size > 1024 * 1024:
            raise ac.ToolError(f"kenney {slug_id}: {info.filename} expands {info.file_size // max(info.compress_size, 1)}:1 "
                               f"(cap {MAX_RATIO}:1); not extracted", ac.EXIT_FAIL, reason="zip_ratio")
        total += info.file_size
        plan.append((info, rel))
    if len(plan) > MAX_MEMBERS:
        raise ac.ToolError(f"kenney {slug_id}: {len(plan)} audio/licence members (cap {MAX_MEMBERS}); not extracted",
                           ac.EXIT_FAIL, reason="zip_too_many_members")
    if total > MAX_EXTRACT_BYTES:
        raise ac.ToolError(f"kenney {slug_id}: {total} bytes uncompressed (cap {MAX_EXTRACT_BYTES}); not extracted",
                           ac.EXIT_FAIL, reason="zip_too_large")
    free = _free_bytes(dest)
    if total + DISK_MARGIN_BYTES > free:
        raise ac.ToolError(f"kenney {slug_id}: needs {total} bytes plus a {DISK_MARGIN_BYTES // 1024 ** 3} GB margin, "
                           f"only {free} bytes free; not extracted", ac.EXIT_INFRA, reason="low_disk")
    return plan


def safe_member(name: str) -> PurePosixPath | None:
    """Zip member → safe relative path, or None (absolute, drive, '..', or not audio/licence)."""
    n = name.replace("\\", "/")
    if n.startswith("/") or re.match(r"^[A-Za-z]:", n) or n.endswith("/"):
        return None
    p = PurePosixPath(n)
    if any(part in ("..", "") for part in p.parts) or any(part.startswith("__MACOSX") for part in p.parts):
        return None
    if p.suffix.lower() in AUDIO_EXTS or LICENCE_NAMES.match(p.name):
        return p
    return None


def fetch_kenney(slug_id: str, out: Path, args, ws: Path | None, throttle: Throttle) -> dict:
    if not re.fullmatch(r"[a-z0-9]+(-[a-z0-9]+)*", slug_id):
        raise ac.UsageError("--id for kenney is the pack slug from kenney.nl/assets/<slug>, e.g. impact-sounds")
    page_url = f"{KENNEY_BASE}/assets/{slug_id}"
    throttle.wait("kenney")
    html, final_page = http_get(page_url)
    if not _same_site(final_page):
        raise ac.ToolError(f"kenney {slug_id}: the pack page redirected off kenney.nl; not fetched", ac.EXIT_FAIL,
                           reason="off_site")
    text = html.decode("utf-8", "replace")
    if not re.search(r"creative\s*commons\s*(cc)?0|\bcc0\b|publicdomain/zero", text, re.I):
        raise ac.ToolError(f"kenney {slug_id}: the pack page does not state CC0; not fetched", ac.EXIT_FAIL,
                           reason="not_cc0")
    links = [urllib.parse.urljoin(page_url, m) for m in re.findall(r"""href=["']([^"']+?\.zip)(?:\?[^"']*)?["']""", text, re.I)]
    links = [u for u in links if _same_site(u)]
    if not links:
        raise ac.ToolError(f"kenney {slug_id}: no kenney.nl zip link on the page (layout changed?); download it by "
                           "hand and record the licence row", ac.EXIT_INFRA, reason="no_zip_link")
    zip_url = links[0]
    throttle.wait("kenney")
    data, final = http_get(zip_url, max_bytes=MAX_ZIP_BYTES, timeout=600)
    if not _same_site(final):
        raise ac.ToolError(f"kenney {slug_id}: the zip download redirected off kenney.nl; not extracted", ac.EXIT_FAIL,
                           reason="off_site")
    if sniff(data) != "zip":
        raise ac.ToolError(f"kenney {slug_id}: download is not a zip", ac.EXIT_FAIL)
    dest = out / f"kenney_{slug_id}"
    files = []
    try:
        zf = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile as e:
        raise ac.ToolError(f"kenney {slug_id}: bad zip: {e}", ac.EXIT_FAIL) from None
    with zf:
        for info, rel in plan_extraction(zf, slug_id, dest):
            target = dest.joinpath(*rel.parts)
            if not ac.is_inside(target, dest):  # belt and braces after safe_member
                continue
            ac.write_bytes_atomic(target, zf.read(info))
            files.append(target)
    audio = [f for f in files if f.suffix.lower() in AUDIO_EXTS]
    if not audio:
        raise ac.ToolError(f"kenney {slug_id}: the zip holds no audio files", ac.EXIT_FAIL)
    retrieved = ac.utc_iso()
    receipts = Path(args.receipts) if args.receipts else default_receipts(ws, out)
    receipt_path = receipts / f"kenney_{slug_id}.json"
    entries = [{"path": ac.rel_path(f, ws), "sha256": ac.sha256_file(f), "bytes": f.stat().st_size} for f in files]
    receipt = {
        "schema": 1, "source": "kenney", "id": slug_id, "page_url": page_url, "licence_id": "CC0-1.0",
        "zip_url": urllib.parse.urlsplit(final)._replace(query="").geturl(), "zip_sha256": hashlib.sha256(data).hexdigest(),
        "zip_bytes": len(data), "retrieved_at_utc": retrieved, "dir": ac.rel_path(dest, ws), "files": entries,
        "tool": f"fetch_cc0_audio.py {TOOL_VERSION}",
    }
    ac.write_json_atomic(receipt_path, receipt)
    statuses: dict[str, int] = {}
    for f in audio:
        row = {
            "source_kind": "cc0_download", "source_url": page_url, "licence_id": "CC0-1.0", "licence_url": CC0_URL,
            "author": "Kenney (kenney.nl)", "retrieved_at_utc": retrieved, "provider": "kenney",
            "ai_generated": "false", "commercial_ok": "true", "training_ban": "false", "redistribution": "allowed",
            "ships": "true" if args.ships else "false",
            "notes": f"kenney pack {slug_id}; receipt {ac.rel_path(receipt_path, ws)}; do not use the Kenney logo",
        }
        st = ac.write_licence_row(f, row, ws, enabled=not args.no_licence)["status"]
        statuses[st] = statuses.get(st, 0) + 1
    return {"ok": True, "source": "kenney", "id": slug_id, "dir": ac.rel_path(dest, ws), "audio_files": len(audio),
            "files": len(files), "zip_sha256": receipt["zip_sha256"], "receipt": ac.rel_path(receipt_path, ws),
            "licence_rows": statuses}


# ---------------------------------------------------------------- CLI

def parse_args(argv):
    ap = argparse.ArgumentParser(prog="fetch_cc0_audio.py", description="fetch CC0 audio (Freesound CC0 / Kenney)")
    ap.add_argument("--source", required=True, choices=("freesound", "kenney"))
    ap.add_argument("--id", help="Freesound sound id, or Kenney pack slug")
    ap.add_argument("--out", help="destination folder")
    ap.add_argument("--search", help="freesound only: list CC0 sounds matching a query (no download)")
    ap.add_argument("--max", type=int, default=15, help="with --search: result count (default 15)")
    ap.add_argument("--original", action="store_true", help="freesound: original file (needs FREESOUND_OAUTH_TOKEN)")
    ap.add_argument("--receipts", help="receipt folder (default Docs/audio/receipts)")
    ap.add_argument("--ships", action="store_true", help="mark the licence rows as shipping")
    ap.add_argument("--no-licence", action="store_true", help="do not write licence rows")
    ap.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def main(argv, throttle: Throttle | None = None):
    args = parse_args(argv)
    ws = ac.find_workspace()
    if args.search:
        if args.source != "freesound":
            raise ac.UsageError("--search works with --source freesound")
        out = Path(args.out) if args.out else Path.cwd()
        result = freesound_search(args.search, args.max, throttle or Throttle(state_path(ws, out)))
        ac.emit(result, args.json, [f"{r['id']:>8}  {r['duration_s']}s  {r['username']}  {r['name']}" for r in result["results"]])
        return ac.EXIT_OK
    if not args.id or not args.out:
        raise ac.UsageError("--id and --out are required (or --search)")
    out = Path(args.out)
    th = throttle or Throttle(state_path(ws, out))
    result = (fetch_freesound if args.source == "freesound" else fetch_kenney)(args.id, out, args, ws, th)
    ac.emit(result, args.json)
    return ac.EXIT_OK


if __name__ == "__main__":
    sys.exit(ac.run_main(main))
