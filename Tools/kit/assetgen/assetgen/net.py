"""HTTP for providers and downloads: HTTPS only, keys only to pinned hosts, bounded retries, scrubbed errors.

- A provider key is attached only when the request host is in AUTH_HOSTS for that provider, so an edited base_url or a
  redirect can never carry the key elsewhere. Redirects are re-validated and drop the Authorization header.
- POST (submit) is retried only on HTTP 429, which means "not accepted". A timeout or a dropped connection after sending
  raises NoResponse: the caller records submission_unknown and never resubmits (WoO "131 credits without a job id").
- Downloads stream to <stem>.part with a size cap and an overall deadline, verify md5/size when the source gives them,
  and are renamed by sniffed magic bytes (fal_job.py defects 8 and 9).
- Test mode: GSK_ASSETGEN_URL_MAP = {"https://real.origin": "http://127.0.0.1:<port>/prefix", ...}. Only loopback
  targets are accepted, and keys.lookup refuses non-'test-' keys while it is set.
"""

import base64
import hashlib
import ipaddress
import json
import os
import socket
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

from . import USER_AGENT
from .errors import InfraError
from .sniff import sniff
from .util import rand_suffix, scrub_urls

AUTH_HOSTS = {
    "fal": {"queue.fal.run", "fal.run", "api.fal.ai", "rest.alpha.fal.ai"},
    "meshy": {"api.meshy.ai"},
    "tripo": {"openapi.tripo3d.ai", "api.tripo3d.ai"},
    "blockade": {"backend.blockadelabs.com"},
    "recraft": {"external.api.recraft.ai"},
}
RETRY_STATUS_GET = {429, 500, 502, 503, 504}
MAX_DOWNLOAD_BYTES = 512 * 1024 * 1024


class HttpError(Exception):
    def __init__(self, status, message):
        super().__init__(f"HTTP {status}: {message}")
        self.status = status
        self.message = message


class NoResponse(Exception):
    """The request may or may not have reached the server (timeout, reset). Never resubmit after this."""


def test_mode():
    return bool(os.environ.get("GSK_ASSETGEN_URL_MAP"))


def _is_loopback_host(host):
    if not host:
        return False
    h = host.strip("[]").lower()
    if h in ("localhost",) or h.endswith(".localhost"):
        return True
    try:
        return ipaddress.ip_address(h).is_loopback
    except ValueError:
        return False


def _is_private_literal(host):
    h = (host or "").strip("[]").lower()
    if _is_loopback_host(h) or h in ("0.0.0.0", "::"):
        return True
    try:
        ip = ipaddress.ip_address(h)
        return ip.is_private or ip.is_link_local or ip.is_reserved or ip.is_unspecified
    except ValueError:
        return False


_MAP_CACHE = (None, [])


def _url_map():
    global _MAP_CACHE
    raw = os.environ.get("GSK_ASSETGEN_URL_MAP")
    if not raw:
        return []
    if _MAP_CACHE[0] == raw:
        return _MAP_CACHE[1]
    try:
        m = json.loads(raw)
    except json.JSONDecodeError as e:
        raise InfraError(f"GSK_ASSETGEN_URL_MAP is not JSON: {e}")
    pairs = []
    for src, dst in m.items():
        host = urllib.parse.urlsplit(dst).hostname
        if not _is_loopback_host(host):
            raise InfraError("GSK_ASSETGEN_URL_MAP may only map to loopback targets (test mode)")
        pairs.append((src.rstrip("/"), dst.rstrip("/")))
    pairs.sort(key=lambda p: -len(p[0]))
    _MAP_CACHE = (raw, pairs)
    return pairs


def resolve(url):
    for src, dst in _url_map():
        if url == src or url.startswith(src + "/") or url.startswith(src + "?"):
            return dst + url[len(src):], True
    return url, False


def check_url(url, mapped=False):
    parts = urllib.parse.urlsplit(url)
    if parts.username or parts.password:
        raise InfraError("refusing a URL that carries credentials")
    if mapped:
        if parts.scheme not in ("http", "https") or not _is_loopback_host(parts.hostname):
            raise InfraError("mapped test URL must be loopback http(s)")
        return
    if parts.scheme != "https":
        raise InfraError(f"refusing non-HTTPS URL ({parts.scheme}://{parts.hostname})")
    if _is_private_literal(parts.hostname):
        raise InfraError(f"refusing a loopback or private address ({parts.hostname})")


class _SafeRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        target = urllib.parse.urlsplit(newurl)
        if test_mode() and _is_loopback_host(target.hostname):
            check_url(newurl, mapped=True)
        else:
            mapped_url, mapped = resolve(newurl)
            check_url(mapped_url, mapped=mapped)
            newurl = mapped_url
        new = super().redirect_request(req, fp, code, msg, headers, newurl)
        if new is not None:
            old_host = urllib.parse.urlsplit(req.full_url).hostname
            if urllib.parse.urlsplit(new.full_url).hostname != old_host:
                for h in list(new.headers):
                    if h.lower() == "authorization":
                        del new.headers[h]
                new.unredirected_hdrs.pop("Authorization", None)
        return new


_OPENER = urllib.request.build_opener(_SafeRedirects)


def _backoff(i, retry_after=None):
    scale = float(os.environ.get("GSK_ASSETGEN_BACKOFF_MS", "1000")) / 1000.0
    if retry_after:
        try:
            return min(60.0, float(retry_after)) * min(1.0, scale if scale > 0 else 0)
        except ValueError:
            pass
    return min(30.0, scale * (2 ** i))


def _auth_header(logical_url, auth):
    if not auth:
        return None
    provider, value = auth
    host = urllib.parse.urlsplit(logical_url).hostname or ""
    if host.lower() not in AUTH_HOSTS.get(provider, set()):
        raise InfraError(f"refusing to send the {provider} key to {host}: not a pinned {provider} host", reason="auth_host")
    return value


def request(method, url, *, headers=None, json_body=None, data=None, auth=None, timeout=60, retries=None):
    """(status, headers, body bytes). Raises HttpError, NoResponse or InfraError."""
    target, mapped = resolve(url)
    check_url(target, mapped)
    hdr = {"User-Agent": USER_AGENT, "Accept": "application/json"}
    hdr.update(headers or {})
    auth_value = _auth_header(url, auth)
    if auth_value:
        hdr["Authorization"] = auth_value
    if json_body is not None:
        data = json.dumps(json_body).encode("utf-8")
        hdr["Content-Type"] = "application/json"
    is_get = method.upper() in ("GET", "HEAD")
    max_tries = retries if retries is not None else 4
    last = None
    for i in range(max_tries):
        req = urllib.request.Request(target, data=data, method=method.upper(), headers=hdr)
        try:
            with _OPENER.open(req, timeout=timeout) as resp:
                return resp.status, dict(resp.headers), resp.read()
        except urllib.error.HTTPError as e:
            body = b""
            try:
                body = e.read()
            except Exception:
                pass
            retryable = (e.code in RETRY_STATUS_GET) if is_get else (e.code == 429)
            last = HttpError(e.code, scrub_urls(body.decode("utf-8", "replace")))
            if retryable and i + 1 < max_tries:
                time.sleep(_backoff(i, e.headers.get("Retry-After") if e.headers else None))
                continue
            raise last
        except (urllib.error.URLError, socket.timeout, TimeoutError, ConnectionError, OSError) as e:
            if isinstance(e, urllib.error.URLError) and isinstance(getattr(e, "reason", None), InfraError):
                raise e.reason
            if not is_get:
                raise NoResponse(scrub_urls(f"{type(e).__name__}: {e}"))
            last = e
            if i + 1 < max_tries:
                time.sleep(_backoff(i))
                continue
            raise InfraError(f"network error after {max_tries} tries: {scrub_urls(str(e))}", reason="network")
    raise InfraError(f"request failed: {scrub_urls(str(last))}", reason="network")


def get_json(url, **kw):
    status, _, body = request("GET", url, **kw)
    try:
        return json.loads(body.decode("utf-8") or "null")
    except json.JSONDecodeError:
        raise InfraError(f"non-JSON response from {urllib.parse.urlsplit(url).hostname}", reason="bad_response")


def post_json(url, obj, **kw):
    status, _, body = request("POST", url, json_body=obj, **kw)
    try:
        return json.loads(body.decode("utf-8") or "null")
    except json.JSONDecodeError:
        raise NoResponse(f"non-JSON response to a submit from {urllib.parse.urlsplit(url).hostname}")


def get_text(url, **kw):
    kw.setdefault("headers", {"Accept": "text/html,application/xhtml+xml"})
    _, _, body = request("GET", url, **kw)
    return body.decode("utf-8", "replace")


def _finish(part, dest_dir, stem, head, total, sha, md5, expected_md5, expected_size):
    if expected_size is not None and int(expected_size) != total:
        part.unlink(missing_ok=True)
        raise InfraError(f"size mismatch for {stem}: expected {expected_size}, got {total}", reason="size_mismatch")
    if expected_md5 and md5.hexdigest().lower() != str(expected_md5).lower():
        part.unlink(missing_ok=True)
        raise InfraError(f"md5 mismatch for {stem}", reason="md5_mismatch")
    kind, ext = sniff(head)
    final = Path(dest_dir) / f"{stem}.{ext}"
    for i in range(40):
        try:
            os.replace(part, final)
            break
        except PermissionError:
            if i == 39:
                raise
            time.sleep(0.05)
    return {"file": final, "sha256": sha.hexdigest(), "bytes": total, "kind": kind, "ext": ext, "md5_ok": bool(expected_md5) or None}


def download(url, dest_dir, stem, *, max_bytes=MAX_DOWNLOAD_BYTES, deadline_s=900, expected_md5=None, expected_size=None, tries=3):
    """Download without auth. Returns {file, sha256, bytes, kind, ext, md5_ok}."""
    dest_dir = Path(dest_dir)
    dest_dir.mkdir(parents=True, exist_ok=True)
    part = dest_dir / f"{stem}.{rand_suffix(4)}.part"
    if url.startswith("data:"):
        header, _, payload = url.partition(",")
        raw = base64.b64decode(payload) if ";base64" in header else urllib.parse.unquote_to_bytes(payload)
        if len(raw) > max_bytes:
            raise InfraError("inline output exceeds the size cap", reason="too_large")
        part.write_bytes(raw)
        return _finish(part, dest_dir, stem, raw[:64], len(raw), hashlib.sha256(raw), hashlib.md5(raw), expected_md5, expected_size)
    target, mapped = resolve(url)
    check_url(target, mapped)
    last = None
    for i in range(tries):
        t0 = time.monotonic()
        sha, md5, total, head = hashlib.sha256(), hashlib.md5(), 0, b""
        req = urllib.request.Request(target, headers={"User-Agent": USER_AGENT, "Accept": "*/*"})
        try:
            with _OPENER.open(req, timeout=60) as resp, open(part, "wb") as out:
                while True:
                    if time.monotonic() - t0 > deadline_s:
                        raise TimeoutError("download deadline passed")
                    chunk = resp.read(1 << 20)
                    if not chunk:
                        break
                    total += len(chunk)
                    if total > max_bytes:
                        raise InfraError(f"download exceeds {max_bytes} bytes", reason="too_large")
                    if len(head) < 64:
                        head += chunk[: 64 - len(head)]
                    sha.update(chunk)
                    md5.update(chunk)
                    out.write(chunk)
            return _finish(part, dest_dir, stem, head, total, sha, md5, expected_md5, expected_size)
        except InfraError:
            part.unlink(missing_ok=True)
            raise
        except urllib.error.HTTPError as e:
            last = HttpError(e.code, "download failed")
            part.unlink(missing_ok=True)
            if e.code not in RETRY_STATUS_GET:
                break
        except (urllib.error.URLError, socket.timeout, TimeoutError, ConnectionError, OSError) as e:
            if isinstance(e, urllib.error.URLError) and isinstance(getattr(e, "reason", None), InfraError):
                part.unlink(missing_ok=True)
                raise e.reason
            last = e
            part.unlink(missing_ok=True)
        if i + 1 < tries:
            time.sleep(_backoff(i))
    raise InfraError(f"download failed after {tries} tries: {scrub_urls(str(last))}", reason="download")


UPLOAD_MIME = {"png": "image/png", "jpeg": "image/jpeg", "webp": "image/webp", "glb": "model/gltf-binary"}


def data_uri(path, mime=None):
    """Only sniffed images and GLB are ever inlined for upload; anything else (a key file, a .env) is refused."""
    raw = Path(path).read_bytes()
    kind, _ = sniff(raw[:64])
    if kind not in UPLOAD_MIME:
        raise InfraError(f"refusing to upload {Path(path).name}: its content is {kind}, not an image or GLB", reason="unsafe_ref")
    mime = mime or UPLOAD_MIME[kind]
    return f"data:{mime};base64," + base64.b64encode(raw).decode("ascii")
