"""The output allowlist (ARCHITECTURE §3.7 improvement 2; extract_generation §4 rule 5, §8.1 defects 1 and 6).

Only values under the endpoint's `output_keys` (dotted paths such as images, model_mesh, model_urls.glb) are outputs.
Any URL that also appears in the request, in uploaded refs, or anywhere in an input-looking part of the result is
excluded: WoO recorded uploaded references as outputs in 126 job records. Duplicates are dropped, and at most one mesh
format is kept (GLB, unless another format is asked for). After download, runner.py also drops any file whose sha256
equals a reference image.
"""

import urllib.parse

from .errors import Refused
from .util import URL_RE

MESH_EXTS = ("glb", "gltf", "fbx", "obj", "usdz", "stl", "3mf", "blend")
INPUT_KEYS = {"input", "inputs", "request", "arguments", "params", "parameters", "payload"}


def collect_urls(obj, out=None):
    out = set() if out is None else out
    if isinstance(obj, str):
        s = obj.strip()
        if s.startswith(("http://", "https://")):
            out.add(s)
        elif s.startswith("data:"):
            out.add(s)
        else:
            for m in URL_RE.findall(s):
                out.add(m)
    elif isinstance(obj, dict):
        for v in obj.values():
            collect_urls(v, out)
    elif isinstance(obj, (list, tuple)):
        for v in obj:
            collect_urls(v, out)
    return out


def input_urls_in_result(result):
    """URLs under input-like keys of the result itself (providers echo the request)."""
    out = set()

    def walk(o):
        if isinstance(o, dict):
            for k, v in o.items():
                if str(k).lower() in INPUT_KEYS or str(k).lower().endswith(("_input", "input_urls", "image_urls_in")):
                    collect_urls(v, out)
                else:
                    walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)

    walk(result)
    return out


def get_path(obj, dotted):
    cur = [obj]
    for part in dotted.split("."):
        nxt = []
        for c in cur:
            if isinstance(c, dict) and part in c:
                nxt.append(c[part])
            elif isinstance(c, list):
                for item in c:
                    if isinstance(item, dict) and part in item:
                        nxt.append(item[part])
        cur = nxt
        if not cur:
            return []
    return cur


def _urls_of(value, key):
    """Yield (url, hint) from a string, a {url} dict, a list, or a {format: url} map."""
    if isinstance(value, str):
        if value.startswith(("http://", "https://", "data:")):
            yield value, key
    elif isinstance(value, dict):
        if isinstance(value.get("url"), str):
            yield value["url"], value.get("file_name") or value.get("content_type") or key
        else:
            for fmt, v in value.items():
                if isinstance(v, str) and v.startswith(("http://", "https://", "data:")):
                    yield v, f"{key}.{fmt}"
    elif isinstance(value, list):
        for v in value:
            yield from _urls_of(v, key)


def _mesh_fmt(url, hint):
    for s in (str(hint or "").lower(), url.split("?", 1)[0].lower()):
        for ext in MESH_EXTS:
            if s.endswith("." + ext) or s.endswith("/" + ext) or s == ext:
                return ext
    return None


def extract(result, output_keys, exclude=(), keep_mesh=("glb",), host_ok=None, allow_data=True):
    """[{url, key}] in allowlist order, inputs excluded, deduplicated, one mesh format.

    host_ok (url → bool) and allow_data=False are used when the result JSON came from the agent (MCP settle): an output
    must then be an https URL on the provider's CDN, so an arbitrary file cannot be laundered in as provider output."""
    excluded = set(exclude) | input_urls_in_result(result)
    seen, out = set(), []
    for key in output_keys:
        for value in get_path(result, key):
            for url, hint in _urls_of(value, key):
                if url in excluded or url in seen:
                    continue
                if url.startswith("data:") and not allow_data:
                    raise Refused(f"result key {key} holds an inline data: URI; provider outputs on this path must be provider CDN URLs", reason="output_host")
                if host_ok is not None and not url.startswith("data:") and not host_ok(url):
                    host = urllib.parse.urlsplit(url).hostname
                    raise Refused(f"result key {key} names an output on {host}, which is not a known CDN host for this provider", reason="output_host")
                seen.add(url)
                out.append({"url": url, "key": key, "mesh": _mesh_fmt(url, hint)})
    meshes = [o for o in out if o["mesh"]]
    if len(meshes) > 1:
        keep = None
        for fmt in keep_mesh:
            keep = next((o for o in meshes if o["mesh"] == fmt), None)
            if keep:
                break
        keep = keep or meshes[0]
        out = [o for o in out if not o["mesh"] or o is keep]
    return [{"url": o["url"], "key": o["key"]} for o in out]
