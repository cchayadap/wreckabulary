"""Rig a humanoid mesh through the public Make-It-Animatable (MIA) Hugging Face Space, failing closed on any drift.

Not a Blender script. Run with uv (the pins are the versions the research lane verified; keyring reads HF_TOKEN from
Windows Credential Manager):
  uv run --python 3.12 --with gradio_client==2.7.1 --with keyring==25.7.0 Tools/kit/blender/mia_autorig.py \
      --in body_apose.glb --out body_rigged.fbx --result ArtSource/rigs/<id>/mia.json [--asset-id <id>] [--json]
Exit codes: 0 ok; 1 refused (the Space changed: sha or parameter labels); 2 usage; 3 fallback (ZeroGPU quota,
HTTP 5xx, network, timeout, Space not callable, gradio_client missing, no output), or infra/error (assetgen missing
for --asset-id, rig_route=mia not recorded, an unexpected error): read `status`. A fallback means: use the paid route
(Tripo v3 or Meshy) or the donor route (donor_fit.py). It never retries in a loop: one call per run.

Facts (research_blender_rigging.md 2.4 and its fact-check, ARCHITECTURE 3.9 and critic C-20):
  - the /pipeline parameters are unnamed and positional (param_2 ... param_13), so the call is only safe on the
    Space revision whose labels were read: sha cf2d235e067d478985e2c077f209b341af0fe1a6;
  - param_11 ("Animation File") defaults to a server path, so it is passed as None;
  - server defaults give 22 mixamorig: bones (No Fingers), T-pose rest (Reset to Rest);
  - ZeroGPU quota per day: 2 GPU-min anonymous (about 2 rigs), 5 with a free HF_TOKEN, 40 with PRO;
  - the mesh is uploaded to a third party; the licence of MIA output is UNKNOWN (U-14): prototype only. The
    ship validator blocks assets whose asset.json says rig_route=mia, which --asset-id records through assetgen;
    a run with --asset-id that cannot record it is not ok.
  - a SLEEPING Space is callable: connecting wakes it (gradio_client 2.7.1), then the labels are pinned.
HF_TOKEN comes from Windows Credential Manager (service 'game-studio-kit', user 'HF_TOKEN': Python keyring, or
advapi32 CredReadW when keyring is not installed), then the environment. Only the storage method is reported.
"""
import argparse
import concurrent.futures
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import traceback
import urllib.error
import urllib.request

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import riglogic  # noqa: E402

SPACE_ID = "jasongzy/Make-It-Animatable"
PINNED_SHA = "cf2d235e067d478985e2c077f209b341af0fe1a6"
SPACE_API_URL = "https://huggingface.co/api/spaces/" + SPACE_ID
INFO_URL = "https://jasongzy-make-it-animatable.hf.space/gradio_api/info"
API_NAME = "/pipeline"
INPUT_PARAM = "progress"
PINNED_LABELS = {
    "param_2": "No Fingers",
    "param_3": "Input Rest Pose",
    "param_10": "Reset to Rest",
    "param_11": "Animation File",
}
GRADIO_CLIENT_PIN = "2.7.1"
KEYRING_PIN = "25.7.0"
INVOCATION = ("uv run --python 3.12 --with gradio_client==%s --with keyring==%s Tools/kit/blender/mia_autorig.py"
              % (GRADIO_CLIENT_PIN, KEYRING_PIN))
KEYRING_SERVICE = "game-studio-kit"
LICENSE_NOTE = "U-14: MIA output licence undecided (model trained on Mixamo data); prototype only, re-rig before ship"
USER_AGENT = "game-studio-kit/mia_autorig"
ASSET_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$")
# Space runtime stages (huggingface_hub SpaceStage). gradio_client 2.7.1 refuses NO_APP_FILE, CONFIG_ERROR,
# BUILD_ERROR, RUNTIME_ERROR and PAUSED, waits (unbounded) on BUILDING, and wakes SLEEPING on connect.
DEAD_STAGES = ("PAUSED", "STOPPED", "NO_APP_FILE", "BUILDING", "DELETING")
WAKE_STAGES = ("SLEEPING", "APP_STARTING")
FALLBACK_ROUTES = ["tripo_v3 (paid, free Rig Check first)", "meshy (paid)", "donor_fit.py (experimental)"]
ASSETGEN_TIMEOUT_S = 180

EXIT_OK, EXIT_REFUSED, EXIT_USAGE, EXIT_FALLBACK = 0, 1, 2, 3
EXIT_INFRA = 3


class Refused(Exception):
    def __init__(self, reason, message, **extra):
        super().__init__(message)
        self.reason, self.extra = reason, extra


class Fallback(Exception):
    def __init__(self, reason, message, **extra):
        super().__init__(message)
        self.reason, self.extra = reason, extra


class Infra(Exception):
    def __init__(self, reason, message, **extra):
        super().__init__(message)
        self.reason, self.extra = reason, extra


class UsageError(Exception):
    pass


class ResultPathError(UsageError):
    """--result names a file the run must not write (it is not a kit result, or it is an input)."""


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        raise UsageError(message)


class ProcResult:
    def __init__(self, returncode, stdout="", stderr="", timed_out=False):
        self.returncode, self.stdout, self.stderr, self.timed_out = returncode, stdout, stderr, timed_out


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def present(data):
    """Path fields as workspace-relative POSIX inside a kit workspace, else absolute POSIX (CONTRACTS 1.1)."""
    return riglogic.present_paths(data, riglogic.find_workspace())


def write_json(path, data):
    path = os.path.abspath(path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(present(data), indent=2) + "\n")
    os.replace(tmp, path)


def same_path(a, b):
    a, b = os.path.abspath(a), os.path.abspath(b)
    if os.path.normcase(a) == os.path.normcase(b):
        return True
    try:
        return os.path.exists(a) and os.path.exists(b) and os.path.samefile(a, b)
    except OSError:
        return False


def check_result_path(path):
    """--result must be a .json file that is new or an earlier kit result ({"ok": ...}); it is never another file."""
    if not path.lower().endswith(".json"):
        raise ResultPathError("--result must be a .json file: %s" % path)
    if os.path.isdir(path):
        raise ResultPathError("--result is a folder: %s" % path)
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8-sig") as fh:
                old = json.load(fh)
        except (OSError, ValueError):
            old = None
        if not (isinstance(old, dict) and "ok" in old):
            raise ResultPathError("--result %s exists and is not a kit result file (no \"ok\" field); refusing to "
                             "overwrite it" % path)


# ---------------------------------------------------------------------------------------------- seams (tests patch)

def fetch_json(url, timeout=30):
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def credman_read(service, username):
    """Read a generic credential from Windows Credential Manager with advapi32.CredReadW (stdlib ctypes), where
    Python keyring stores it: target '<service>' when its user name matches, else '<username>@<service>'.
    Returns the secret or None when there is none; raises OSError when the store cannot be read."""
    if os.name != "nt":
        raise OSError("Credential Manager exists only on Windows")
    import ctypes
    from ctypes import wintypes

    class CREDENTIALW(ctypes.Structure):
        _fields_ = [("Flags", wintypes.DWORD), ("Type", wintypes.DWORD), ("TargetName", wintypes.LPWSTR),
                    ("Comment", wintypes.LPWSTR), ("LastWritten", wintypes.FILETIME),
                    ("CredentialBlobSize", wintypes.DWORD), ("CredentialBlob", ctypes.POINTER(ctypes.c_ubyte)),
                    ("Persist", wintypes.DWORD), ("AttributeCount", wintypes.DWORD), ("Attributes", ctypes.c_void_p),
                    ("TargetAlias", wintypes.LPWSTR), ("UserName", wintypes.LPWSTR)]

    advapi = ctypes.WinDLL("advapi32", use_last_error=True)
    cred_read = advapi.CredReadW
    cred_read.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(ctypes.POINTER(CREDENTIALW))]
    cred_read.restype = wintypes.BOOL
    cred_free = advapi.CredFree
    cred_free.argtypes = [ctypes.c_void_p]
    cred_free.restype = None
    cred_type_generic, error_not_found = 1, 1168

    def read(target):
        ptr = ctypes.POINTER(CREDENTIALW)()
        if not cred_read(target, cred_type_generic, 0, ctypes.byref(ptr)):
            err = ctypes.get_last_error()
            if err == error_not_found:
                return None
            raise OSError(err, "CredReadW failed")
        try:
            c = ptr.contents
            blob = ctypes.string_at(c.CredentialBlob, c.CredentialBlobSize) if c.CredentialBlobSize else b""
            return c.UserName, blob
        finally:
            cred_free(ptr)

    hit = read(service)
    if hit is None or (username and hit[0] != username):
        hit = read("%s@%s" % (username, service))
    if hit is None:
        return None
    blob = hit[1]
    try:
        return blob.decode("utf-16-le") if len(blob) % 2 == 0 else blob.decode("utf-8")
    except UnicodeDecodeError:
        return blob.decode("utf-8", errors="replace")


def keyring_get(name):
    """(value or None, how): how is 'keyring' (Python keyring read it), 'credman' (keyring is not installed, so
    CredReadW read the same store) or 'unavailable' (neither could read it). The value is never printed."""
    try:
        import keyring  # uv run --with keyring
    except ImportError:
        keyring = None
    if keyring is not None:
        try:
            return keyring.get_password(KEYRING_SERVICE, name), "keyring"
        except Exception:
            pass
    try:
        return credman_read(KEYRING_SERVICE, name), "credman"
    except Exception:
        return None, "unavailable"


def make_client(token):
    from gradio_client import Client
    try:
        return Client(SPACE_ID, token=token, httpx_kwargs={"timeout": 300}, verbose=False)
    except TypeError:
        return Client(SPACE_ID, token=token)


def handle_file(path):
    from gradio_client import handle_file as hf
    return hf(path)


def tree_kill(pid):
    """Kill a process and its descendants by PID (CONTRACTS 1.6), never by name."""
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], stdin=subprocess.DEVNULL, capture_output=True,
                       timeout=60)
    else:
        try:
            os.kill(pid, 9)
        except OSError:
            pass


def run_process(cmd, cwd, timeout=ASSETGEN_TIMEOUT_S):
    """Run a command (argv list, or a finished cmd.exe line) with stdin closed; on timeout kill its whole tree."""
    p = subprocess.Popen(cmd, cwd=cwd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                         text=True, encoding="utf-8", errors="replace")
    try:
        out, err = p.communicate(timeout=timeout)
        return ProcResult(p.returncode, out, err, False)
    except subprocess.TimeoutExpired:
        tree_kill(p.pid)
        try:
            out, err = p.communicate(timeout=15)
        except Exception:
            out, err = "", ""
        return ProcResult(None, out, err, True)


# ---------------------------------------------------------------------------------------------- logic

def read_token():
    """(token or None, storage): storage is keyring | env | missing, or keyring_unavailable when nothing could read
    Credential Manager and the environment has no HF_TOKEN either. The value is never printed."""
    v, how = keyring_get("HF_TOKEN")
    if v:
        return v, "keyring"
    v = os.environ.get("HF_TOKEN")
    if v:
        return v, "env"
    return None, ("keyring_unavailable" if how == "unavailable" else "missing")


def stage_action(stage):
    """'call', 'wake' (connect first, which wakes it) or 'fallback' for a Space runtime stage."""
    s = str(stage or "UNKNOWN").upper()
    if s in DEAD_STAGES or s.endswith("_ERROR"):
        return "fallback"
    if s in WAKE_STAGES:
        return "wake"
    return "call"


def check_space():
    """Pin the Space's git sha and judge its runtime stage. Raises Refused or Fallback."""
    try:
        space = fetch_json(SPACE_API_URL)
    except urllib.error.HTTPError as e:
        raise Fallback("server_5xx" if e.code >= 500 else "space_api_http_%d" % e.code, "Space API returned HTTP %d" % e.code)
    except Exception as e:
        raise Fallback("network", "cannot reach the Hugging Face API: %s" % type(e).__name__)
    sha = space.get("sha")
    if sha != PINNED_SHA:
        raise Refused("sha_mismatch", "the Space is at %s, not the pinned %s: its unnamed parameters may have moved; "
                      "read /gradio_api/info, re-verify one call, then update the pin" % (sha, PINNED_SHA), space_sha=sha)
    stage = ((space.get("runtime") or {}).get("stage")) or "UNKNOWN"
    action = stage_action(stage)
    if action == "fallback":
        raise Fallback("space_not_running", "the Space is %s" % stage, space_sha=sha, space_stage=stage)
    return {"space_sha": sha, "space_stage": stage, "stage_action": action}


def check_labels():
    """Pin the labels of the positional /pipeline parameters. Raises Refused or Fallback."""
    try:
        info = fetch_json(INFO_URL)
    except urllib.error.HTTPError as e:
        raise Fallback("server_5xx" if e.code >= 500 else "info_http_%d" % e.code, "gradio_api/info returned HTTP %d" % e.code)
    except Exception as e:
        raise Fallback("network", "cannot read gradio_api/info: %s" % type(e).__name__)
    ep = (info.get("named_endpoints") or {}).get(API_NAME)
    if not ep:
        raise Refused("endpoint_missing", "the Space has no %s endpoint" % API_NAME)
    params = ep.get("parameters") or []
    labels = {p.get("parameter_name"): p.get("label") for p in params}
    if INPUT_PARAM not in labels:
        raise Refused("input_param_missing", "no '%s' input parameter on %s" % (INPUT_PARAM, API_NAME), labels=labels)
    changed = {k: {"pinned": v, "live": labels.get(k)} for k, v in PINNED_LABELS.items() if labels.get(k) != v}
    if changed:
        raise Refused("labels_changed", "parameter labels differ from the pinned ones: %s" % sorted(changed), labels_changed=changed)
    return {"labels": {k: labels[k] for k in PINNED_LABELS}}


def preflight():
    """Both pins, for a Space that is already running (the order rig() uses unless the Space must be woken)."""
    out = check_space()
    out.update(check_labels())
    return out


def classify_error(exc):
    """Map a call failure to a fallback reason. Everything is a fallback: the run never loops."""
    text = "%s: %s" % (type(exc).__name__, exc)
    low = text.lower()
    status = getattr(exc, "status_code", None) or getattr(getattr(exc, "response", None), "status_code", None)
    if "zerogpu" in low or "quota" in low:
        return "quota"
    if isinstance(status, int) and status >= 500:
        return "server_5xx"
    if any(s in low for s in (" 500", " 502", " 503", " 504", "internal server error", "bad gateway",
                              "service unavailable", "gateway timeout")):
        return "server_5xx"
    if any(s in low for s in ("connect", "timeout", "timed out", "network", "name resolution", "getaddrinfo")):
        return "network"
    return "app_error"


def _job_done(job):
    try:
        return bool(job.done())
    except Exception:
        return False


def is_job_timeout(exc, job):
    """True when job.result(timeout) gave up waiting (the job still runs), not when the job itself failed."""
    return isinstance(exc, (TimeoutError, concurrent.futures.TimeoutError)) and job is not None and not _job_done(job)


def release(job, client):
    """Cancel an unfinished job (it would keep spending ZeroGPU quota) and close the client; never raises."""
    if job is not None and not _job_done(job):
        try:
            job.cancel()
        except Exception:
            pass
    if client is not None:
        try:
            close = getattr(client, "close", None)
            if callable(close):
                close()
        except Exception:
            pass


def output_files(job):
    found = []

    def walk(x):
        if isinstance(x, str):
            if os.path.isfile(x):
                found.append(x)
        elif isinstance(x, dict):
            for v in x.values():
                walk(v)
        elif isinstance(x, (list, tuple)):
            for v in x:
                walk(v)
        else:
            p = getattr(x, "path", None) or getattr(x, "name", None)
            if isinstance(p, str) and os.path.isfile(p):
                found.append(p)

    try:
        for o in job.outputs():
            walk(o)
    except Exception:
        pass
    seen, out = set(), []
    for p in found:
        k = os.path.normcase(os.path.abspath(p))
        if k not in seen:
            seen.add(k)
            out.append(p)
    return out


CMD_META = re.compile(r'([()\][%!^"`<>&|;, *?])')


def cmd_escape_arg(arg):
    """One argument for a cmd.exe line run with /d /s /c (the lib/proc.mjs resolveSpawn rules)."""
    a = str(arg)
    if re.search(r'["\r\n]', a):
        raise UsageError("cannot pass %r through cmd.exe safely" % a[:80])
    a = re.sub(r"(\\*)$", lambda m: m.group(1) * 2, a)
    return CMD_META.sub(r"^\1", '"%s"' % a)


def cmd_line(wrapper, args, comspec=None):
    """The full command line that runs a .cmd wrapper through cmd.exe with every metacharacter escaped."""
    if re.search(r'["\r\n]', wrapper):
        raise UsageError("the assetgen wrapper path cannot pass through cmd.exe safely")
    line = " ".join([CMD_META.sub(r"^\1", os.path.normpath(wrapper))] + [cmd_escape_arg(a) for a in args])
    return '"%s" /d /s /c "%s"' % (comspec or os.environ.get("ComSpec", "cmd.exe"), line)


def assetgen_route(cwd):
    """How to run assetgen from here: {'kind': 'argv'|'cmd'|None, ...}. The direct uv route (no cmd.exe) first."""
    ws = riglogic.find_workspace(cwd)
    if ws is None:
        return {"kind": None, "reason": "kit not installed (no Tools/kit/kit.json above %s)" % cwd}
    proj = os.path.join(ws, "Tools", "kit", "assetgen")
    uv = shutil.which("uv")
    if uv and os.path.isfile(os.path.join(proj, "pyproject.toml")):
        return {"kind": "argv", "argv": [uv, "run", "--project", proj, "assetgen"], "ws": ws}
    wrapper = os.path.join(ws, "Tools", "kit", "bin", "assetgen.cmd" if os.name == "nt" else "assetgen")
    if os.path.isfile(wrapper):
        return {"kind": "cmd" if os.name == "nt" else "argv", "argv": [wrapper], "wrapper": wrapper, "ws": ws}
    return {"kind": None, "reason": "assetgen not installed (no Tools/kit/assetgen project and no Tools/kit/bin wrapper)"}


def asset_set(asset_id, cwd, route=None):
    """Record rig_route=mia in asset.json through assetgen (the only writer of asset.json)."""
    if not ASSET_ID_RE.match(asset_id or ""):
        raise UsageError("--asset-id must match %s" % ASSET_ID_RE.pattern)
    route = route or assetgen_route(cwd)
    if route["kind"] is None:
        return {"recorded": False, "reason": route["reason"]}
    args = ["asset-set", asset_id, "rig_route=mia", "--json"]
    cmd = cmd_line(route["wrapper"], args) if route["kind"] == "cmd" else route["argv"] + args
    try:
        r = run_process(cmd, route["ws"])
    except Exception as e:
        return {"recorded": False, "reason": "assetgen failed to start: %s" % type(e).__name__}
    rep = {"recorded": r.returncode == 0, "exit": r.returncode, "via": route["kind"]}
    if getattr(r, "timed_out", False):
        rep.update(recorded=False, reason="assetgen timed out after %ds; process tree killed" % ASSETGEN_TIMEOUT_S)
    return rep


def rig(args, res):
    inp = os.path.abspath(args.inp)
    out = os.path.abspath(args.out)
    ext = os.path.splitext(out)[1].lower()
    if not os.path.isfile(inp):
        raise UsageError("--in not found: %s" % inp)
    if ext not in (".fbx", ".glb"):
        raise UsageError("--out must end in .fbx or .glb")
    if same_path(inp, out):
        raise UsageError("--out is the --in file; write the rigged model to a new file")
    for p in (inp, out):
        if args.result and same_path(p, args.result):
            raise ResultPathError("--result must not be the --in or --out file")
    route = None
    if args.asset_id is not None:
        if not ASSET_ID_RE.match(args.asset_id):
            raise UsageError("--asset-id must match %s (letters, digits, '_', '.', '-')" % ASSET_ID_RE.pattern)
        route = assetgen_route(os.getcwd())
        if route["kind"] is None:
            raise Infra("assetgen_missing", "--asset-id needs assetgen to record rig_route=mia (%s); nothing was "
                        "uploaded. Install the kit, or drop --asset-id and record the route by hand" % route["reason"])
    try:
        os.makedirs(os.path.dirname(out), exist_ok=True)
    except OSError as e:
        raise UsageError("cannot create the --out folder %s: %s" % (os.path.dirname(out), type(e).__name__))
    token, storage = read_token()
    res.update(space=SPACE_ID, api=API_NAME, pinned_sha=PINNED_SHA, token_storage=storage, input=inp,
               input_sha256=sha256_file(inp), uploaded_to="huggingface.co/spaces/" + SPACE_ID, license=LICENSE_NOTE)
    if storage == "missing":
        res.setdefault("notes", []).append("no HF_TOKEN: anonymous ZeroGPU quota is about 2 rigs a day")
    elif storage == "keyring_unavailable":
        res.setdefault("notes", []).append("Credential Manager could not be read (run with --with keyring==%s); no "
                                           "HF_TOKEN in the environment either: anonymous quota" % KEYRING_PIN)
    res.update(check_space())
    waking = res["stage_action"] == "wake"
    if not waking:
        res.update(check_labels())
    try:
        client = make_client(token)
    except ImportError:
        raise Fallback("gradio_client_missing", "gradio_client is not installed: run through '%s'" % INVOCATION)
    except Exception as e:
        raise Fallback(classify_error(e), "cannot connect to the Space: %s" % type(e).__name__)
    job = None
    try:
        if waking:
            res.update(check_labels())
            res.setdefault("notes", []).append("the Space was %s; connecting woke it" % res["space_stage"])
        res["calls"] = 1
        try:
            job = client.submit(**{INPUT_PARAM: handle_file(inp)}, param_11=None, api_name=API_NAME)
            job.result(timeout=args.timeout)
        except Exception as e:
            if is_job_timeout(e, job):
                raise Fallback("timeout", "the Space job did not finish within %gs; it was cancelled" % args.timeout)
            reason = classify_error(e)
            raise Fallback(reason, "the Space call failed (%s): %s" % (reason, str(e)[:300] or type(e).__name__))
        files = output_files(job)
        res["outputs"] = [os.path.basename(p) for p in files]
        pick = riglogic.pick_output(files, ext, os.path.splitext(os.path.basename(inp))[0])
        if pick is None:
            raise Fallback("no_output", "the Space returned no %s model" % ext, outputs=res["outputs"])
        keep = os.path.splitext(out)[0] + "_mia_outputs"
        os.makedirs(keep, exist_ok=True)
        for p in files:
            shutil.copy2(p, os.path.join(keep, os.path.basename(p)))
        tmp = os.path.join(os.path.dirname(out), ".%s.gsk-tmp" % os.path.basename(out))
        shutil.copy2(pick, tmp)
        os.replace(tmp, out)  # one rename: --out is never half-written
    finally:
        release(job, client)
    res.update(out=out, out_sha256=sha256_file(out), outputs_dir=keep, picked=os.path.basename(pick))
    prov = {"schema": 1, "route": "mia", "space": SPACE_ID, "space_sha": res.get("space_sha"), "api": API_NAME,
            "params": {"param_11": None, "others": "server defaults (22 bones, T-pose rest)"},
            "gradio_client": GRADIO_CLIENT_PIN, "input": os.path.basename(inp), "input_sha256": res["input_sha256"],
            "output": os.path.basename(out), "output_sha256": res["out_sha256"], "utc": utc_now(),
            "license": LICENSE_NOTE, "invocation": INVOCATION,
            "next": "inspect_rig.py --schema mixamo_22 (rig gate), then Unity Humanoid import"}
    prov_path = out + ".provenance.json"
    write_json(prov_path, prov)
    res["provenance_file"] = prov_path
    if args.asset_id:
        res["asset_set"] = asset_set(args.asset_id, os.getcwd(), route)
        if not res["asset_set"]["recorded"]:
            raise Infra("ok_unrecorded", "the rig was made but rig_route=mia was NOT recorded, so the ship validator "
                        "cannot block it: run assetgen asset-set %s rig_route=mia before using the model" % args.asset_id)
    res["status"] = "ok"
    res["ok"] = True


def parse(argv):
    p = _Parser(prog="mia_autorig.py", description=__doc__.split("\n")[0], add_help=False)
    p.add_argument("--in", dest="inp", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--result", required=True)
    p.add_argument("--asset-id")
    p.add_argument("--timeout", type=float, default=600.0, help="seconds to wait for the Space job")
    p.add_argument("--json", action="store_true")
    a = p.parse_args(argv)
    if not (a.timeout > 0):
        raise UsageError("--timeout must be a positive number of seconds")
    return a


def peek_result(argv):
    found = None
    for i, a in enumerate(argv):
        if a == "--result" and i + 1 < len(argv):
            found = argv[i + 1]
        elif a.startswith("--result="):
            found = a.split("=", 1)[1]
    return found


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    t0 = time.time()
    res = {"ok": False, "status": None, "reason": None, "script": "mia_autorig.py"}
    result_path = peek_result(argv)
    as_json = "--json" in argv
    code = EXIT_OK
    try:
        if result_path is not None:
            check_result_path(result_path)
            for stale in (result_path, result_path + ".tmp"):
                if os.path.isfile(stale):
                    os.remove(stale)  # an earlier ok:true must never outlive this run
        args = parse(argv)
        rig(args, res)
    except UsageError as e:
        res.update(status="usage", reason="usage", error="usage: %s" % e)
        code = EXIT_USAGE
        if isinstance(e, ResultPathError):
            result_path = None  # never write over a file the guard refused
    except Refused as e:
        res.update(status="refused", reason=e.reason, error=str(e), **e.extra)
        code = EXIT_REFUSED
    except Fallback as e:
        res.update(status="fallback", reason=e.reason, error=str(e), **e.extra)
        res["fallback_routes"] = FALLBACK_ROUTES
        code = EXIT_FALLBACK
    except Infra as e:
        res.update(status="ok_unrecorded" if e.reason == "ok_unrecorded" else "infra", reason=e.reason, error=str(e),
                   **e.extra)
        code = EXIT_INFRA
    except Exception as e:
        res.update(status="error", reason=type(e).__name__, error="%s: %s" % (type(e).__name__, str(e)[:300]),
                   traceback=traceback.format_exc()[-3000:])
        code = EXIT_INFRA
    res["ok"] = code == EXIT_OK and res.get("status") == "ok"
    res["exit"] = code
    res["secs"] = round(time.time() - t0, 2)
    if result_path:
        try:
            write_json(result_path, res)
        except OSError as e:
            res.setdefault("notes", []).append("could not write --result: %s" % type(e).__name__)
    if as_json:
        sys.stdout.write(json.dumps(present(res)) + "\n")
    else:
        sys.stderr.write("%s: %s\n" % (res["status"], res.get("error") or res.get("out")))
    return code


def cli(argv=None):
    """Entry point: exit as soon as the result is out. gradio_client runs jobs on non-daemon executor threads that
    the interpreter would otherwise wait for, so a timed-out job could hold the process until the Space finished."""
    code = main(argv)
    try:
        sys.stdout.flush()
        sys.stderr.flush()
    finally:
        os._exit(code)


if __name__ == "__main__":
    cli()
