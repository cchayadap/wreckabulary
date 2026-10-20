"""Shared helpers for the Tools/kit/anim Python tools (stdlib only; CONTRACTS.md §1).

Exit codes, the pinned clock (GSK_NOW), workspace discovery (Tools/kit/kit.json), W-relative POSIX paths, sha256,
the one-line --json result (also for unexpected exceptions, §1.4), RFC 4180 CSV appends under the same `<file>.lock`
protocol as lib/locks.mjs (§1.6: O_EXCL create, stale breaking serialised by `<lock>.break`), and licence rows through
the only licence writer, `node Tools/kit/ship/license_row.mjs add --from-json -` (looked up with `get --path`).
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import http.client
import json
import os
import secrets
import shutil
import socket
import subprocess
import sys
import time

EXIT_OK, EXIT_FAIL, EXIT_USAGE, EXIT_INFRA, EXIT_BLOCKED = 0, 1, 2, 3, 75
KIT_JSON_REL = os.path.join("Tools", "kit", "kit.json")
LICENSE_ROW_REL = os.path.join("Tools", "kit", "ship", "license_row.mjs")
LICENCE_COLUMNS = [
    "path", "sha256", "source_kind", "source_url", "licence_id", "licence_url", "author", "attribution_text",
    "retrieved_at_utc", "account_or_plan", "provider", "generator_model", "job_id", "ai_generated", "commercial_ok",
    "training_ban", "redistribution", "ships", "notes",
]
MOTION_COLUMNS = ["clip_path", "body", "source_kind", "source_url", "licence_id", "route", "fps", "loop",
                  "retrieved_at_utc", "sha256", "notes"]


class CliFail(Exception):
    """An error that carries its exit code (and optional extra result fields)."""

    def __init__(self, message, code=EXIT_FAIL, extra=None):
        super().__init__(message)
        self.code = code
        self.extra = extra or {}


def now_utc():
    """The kit clock: GSK_NOW (ISO 8601) when set and valid, else the real UTC time."""
    pinned = os.environ.get("GSK_NOW")
    if pinned:
        try:
            return _dt.datetime.fromisoformat(pinned.replace("Z", "+00:00")).astimezone(_dt.timezone.utc)
        except ValueError:
            pass
    return _dt.datetime.now(_dt.timezone.utc)


def utc_iso(d=None):
    d = d or now_utc()
    return d.astimezone(_dt.timezone.utc).replace(microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


def utc_stamp(d=None):
    return utc_iso(d).replace("-", "").replace(":", "")


def parse_utc(s):
    return _dt.datetime.fromisoformat(str(s).replace("Z", "+00:00")).astimezone(_dt.timezone.utc)


def find_workspace(start=None):
    """First ancestor (inclusive) of start that holds Tools/kit/kit.json, or None."""
    d = os.path.abspath(start or os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd())
    while True:
        if os.path.isfile(os.path.join(d, KIT_JSON_REL)):
            return d
        up = os.path.dirname(d)
        if up == d:
            return None
        d = up


def unity_root(ws):
    """The Unity project folder of a workspace (kit.json unity_subdir, default the workspace itself)."""
    sub = "."
    try:
        with open(os.path.join(ws, KIT_JSON_REL), encoding="utf-8-sig") as fh:
            sub = str(json.load(fh).get("unity_subdir") or ".")
    except (OSError, ValueError, AttributeError):
        pass
    return os.path.abspath(ws if sub in (".", "") else os.path.join(ws, sub))


def is_inside(child, parent):
    c = os.path.normcase(os.path.abspath(child))
    p = os.path.normcase(os.path.abspath(parent))
    return c == p or c.startswith(p.rstrip("\\/") + os.sep)


def rel_posix(base, path):
    """POSIX path of `path` relative to base (or the absolute POSIX path when it is outside base)."""
    if base and is_inside(path, base):
        r = os.path.relpath(os.path.abspath(path), os.path.abspath(base))
        return "." if r == "." else r.replace("\\", "/")
    return os.path.abspath(path).replace("\\", "/")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def emit(result, as_json, text_lines):
    if as_json:
        sys.stdout.write(json.dumps(result, ensure_ascii=False) + "\n")
    else:
        sys.stdout.write("\n".join(text_lines) + "\n")
    sys.stdout.flush()


def note(*parts):
    sys.stderr.write(" ".join(str(p) for p in parts) + "\n")


INFRA_ERRORS = (OSError, subprocess.TimeoutExpired, http.client.HTTPException)


def run_main(main):
    """Run main(argv) -> exit code; map CliFail, usage errors and unexpected exceptions; under --json always print one
    {ok:false, exit, error} line (CONTRACTS §1.4). Environment failures (I/O, timeouts, HTTP) exit 3, anything else 1."""
    argv = sys.argv[1:]
    for stream in (sys.stdout, sys.stderr):  # a Windows console or pipe may default to cp1252
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    def fail_line(code, message, extra=None):
        if "--json" in argv:
            sys.stdout.write(json.dumps({"ok": False, "exit": code, "error": message, **(extra or {})}, ensure_ascii=False) + "\n")
        sys.stderr.write(f"error: {message}\n")

    try:
        code = main(argv)
    except CliFail as e:
        fail_line(e.code, str(e), e.extra)
        code = e.code
    except SystemExit as e:  # argparse usage errors exit 2
        code = e.code if isinstance(e.code, int) else EXIT_USAGE
        if code == EXIT_USAGE and "--json" in argv:
            sys.stdout.write(json.dumps({"ok": False, "exit": EXIT_USAGE, "error": "usage error"}) + "\n")
    except KeyboardInterrupt:
        fail_line(EXIT_INFRA, "interrupted")
        code = EXIT_INFRA
    except Exception as e:  # noqa: BLE001 - the contract is one JSON line, whatever went wrong
        code = EXIT_INFRA if isinstance(e, INFRA_ERRORS) else EXIT_FAIL
        fail_line(code, f"{type(e).__name__}: {e}", {"unexpected": True})
    sys.stdout.flush()
    sys.exit(code)


def write_json_atomic(path, value):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    tmp = f"{path}.tmp-{os.getpid()}-{secrets.token_hex(3)}"
    with open(tmp, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
    for i in range(9):
        try:
            os.replace(tmp, path)
            return
        except PermissionError:
            if i == 8:
                raise
            time.sleep(0.025 * (i + 1))


# ---------- CSV (same quoting as lib/csv.mjs csvField) ----------

def csv_field(v):
    s = "" if v is None else str(v)
    if any(ch in s for ch in '",\r\n') or (s and (s[0].isspace() or s[-1].isspace())):
        return '"' + s.replace('"', '""') + '"'
    return s


def csv_line(values):
    return ",".join(csv_field(v) for v in values)


def parse_csv_line(line):
    """Parse one header line (no embedded newlines)."""
    out, field, q, i = [], "", False, 0
    while i < len(line):
        c = line[i]
        if q:
            if c == '"':
                if i + 1 < len(line) and line[i + 1] == '"':
                    field += '"'
                    i += 2
                    continue
                q = False
            else:
                field += c
        elif c == '"' and field == "":
            q = True
        elif c == ",":
            out.append(field)
            field = ""
        else:
            field += c
        i += 1
    out.append(field)
    return out


def _pid_alive(pid):
    """True when a process with this PID exists. Never signals it (os.kill on Windows would terminate the process)."""
    if not isinstance(pid, int) or pid <= 0:
        return False
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.OpenProcess.restype = wintypes.HANDLE
        kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
        kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
        h = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not h:
            return ctypes.get_last_error() == 5  # access denied: it exists
        try:
            code = wintypes.DWORD()
            kernel32.GetExitCodeProcess(h, ctypes.byref(code))
            return code.value == 259  # STILL_ACTIVE
        finally:
            kernel32.CloseHandle(h)
    try:
        os.kill(pid, 0)
        return True
    except PermissionError:
        return True
    except OSError:
        return False


def _process_start_epoch(pid):
    """Start time (epoch seconds) of a live process, or None when it cannot be read (Windows: GetProcessTimes)."""
    if os.name != "nt" or not isinstance(pid, int) or pid <= 0:
        return None
    import ctypes
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    h = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not h:
        return None
    try:
        t = [wintypes.FILETIME() for _ in range(4)]
        if not kernel32.GetProcessTimes(h, *[ctypes.byref(x) for x in t]):
            return None
        ft = (t[0].dwHighDateTime << 32) | t[0].dwLowDateTime
        return (ft - 116444736000000000) / 1e7
    finally:
        kernel32.CloseHandle(h)


LOCK_UNREADABLE_STALE_S = 600.0
BREAK_STALE_S = 10.0
PID_START_TOLERANCE_S = 2.0


def _read_holder(path):
    try:
        with open(path, encoding="utf-8") as fh:
            h = json.load(fh)
        return h if isinstance(h, dict) else None
    except (OSError, ValueError):
        return None


def _create_excl(path, info):
    """'ok' | 'exists' | 'busy' (Windows reports a file being deleted as a permission error: held, retry)."""
    try:
        fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        return "exists"
    except PermissionError:
        return "busy"
    with os.fdopen(fd, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(info))
    return "ok"


def _unlink_retry(path):
    for i in range(10):
        try:
            os.remove(path)
            return True
        except FileNotFoundError:
            return False
        except PermissionError:
            time.sleep(0.02 * (i + 1))
    return False


def _same_holder(a, b):
    if a is None and b is None:
        return True
    return isinstance(a, dict) and isinstance(b, dict) and a.get("token") == b.get("token") and a.get("pid") == b.get("pid")


_SELF_START = []


def _self_start_utc():
    """This process's start time as the lockHolder records it (lets lib/locks.mjs detect a reused PID), or None."""
    if not _SELF_START:
        t = _process_start_epoch(os.getpid())
        _SELF_START.append(utc_iso(_dt.datetime.fromtimestamp(t, _dt.timezone.utc)) if t else None)
    return _SELF_START[0]


def lock_state(path, holder):
    """'held' | 'stale' | 'gone' for the lock at path whose holder was read as `holder` (CONTRACTS §1.6). Stale, on the
    same host only: the PID is dead; or it is alive but is another process (it started at another time than the holder
    recorded, or after the lock file was written: Windows reuses PIDs); or the file is unreadable and older than 10 min."""
    try:
        st = os.stat(path)
    except FileNotFoundError:
        return "gone"
    except OSError:
        return "held"
    if not isinstance(holder, dict) or not isinstance(holder.get("pid"), int):
        return "stale" if time.time() - st.st_mtime > LOCK_UNREADABLE_STALE_S else "held"
    if holder.get("host") != socket.gethostname():
        return "held"
    pid = holder["pid"]
    if not _pid_alive(pid):
        return "stale"
    if pid == os.getpid():
        return "held"
    start = _process_start_epoch(pid)
    if start is None:
        return "held"
    recorded = holder.get("process_start_utc")
    if recorded:
        try:
            if abs(start - parse_utc(recorded).timestamp()) > PID_START_TOLERANCE_S:
                return "stale"
        except ValueError:
            pass
    if start > st.st_mtime + PID_START_TOLERANCE_S:
        return "stale"
    return "held"


class FileLock:
    """`<file>.lock` created with O_EXCL and a lockHolder JSON; the same protocol as lib/locks.mjs (CONTRACTS §1.6), so
    Python and node writers exclude each other. A stale lock is broken only under the `<lock>.break` O_EXCL mutex, after
    re-reading the holder under it: two contenders that both saw the same stale holder never both take the lock."""

    def __init__(self, path, purpose, wait_s=30.0):
        self.path = path
        self.purpose = purpose
        self.wait_s = wait_s
        self.token = f"{os.getpid()}-{int(time.time() * 1000)}-{secrets.token_hex(4)}"

    def _holder(self, purpose, token):
        return {"pid": os.getpid(), "host": socket.gethostname(), "utc": utc_iso(_dt.datetime.now(_dt.timezone.utc)),
                "purpose": purpose, "token": token, "process_start_utc": _self_start_utc(), "image": sys.executable}

    def __enter__(self):
        deadline = time.monotonic() + self.wait_s
        while True:
            r = _create_excl(self.path, self._holder(self.purpose, self.token))
            if r == "ok":
                return self
            if r == "exists":
                seen = _read_holder(self.path)
                state = lock_state(self.path, seen)
                if state == "gone" or (state == "stale" and self._break_stale(seen)):
                    continue
            if time.monotonic() > deadline:
                raise CliFail(f"lock held: {self.path} ({json.dumps(_read_holder(self.path))})", EXIT_BLOCKED)
            time.sleep(0.05)

    def _break_stale(self, seen):
        """Remove a stale lock under the .break mutex; True when the caller should retry the create at once."""
        brk = self.path + ".break"
        token = f"{os.getpid()}-{int(time.time() * 1000)}-{secrets.token_hex(4)}"
        r = _create_excl(brk, self._holder("break", token))
        if r != "ok":
            if r == "exists":  # a breaker that died leaves .break behind: clear it once it is clearly old
                try:
                    if time.time() - os.stat(brk).st_mtime > BREAK_STALE_S:
                        _unlink_retry(brk)
                except OSError:
                    pass
            return False
        try:
            now = _read_holder(self.path)
            if not _same_holder(seen, now):
                return True  # released or broken by someone else meanwhile: retry the create
            state = lock_state(self.path, now)
            if state == "gone":
                return True
            if state != "stale":
                return False
            _unlink_retry(self.path)
            return True
        finally:
            if (_read_holder(brk) or {}).get("token") == token:
                _unlink_retry(brk)

    def __exit__(self, *exc):
        h = _read_holder(self.path)
        if h and h.get("token") == self.token:  # removed after the handle is closed (Windows cannot delete an open file)
            _unlink_retry(self.path)
        return False


def csv_append(path, header, row):
    """Append one row under <path>.lock; create the file with the header; refuse a different header."""
    extra = [k for k in row if k not in header]
    if extra:
        raise CliFail(f"csv: unknown column(s) {', '.join(extra)}")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with FileLock(path + ".lock", "csv append"):
        if not os.path.exists(path) or os.path.getsize(path) == 0:
            with open(path, "w", encoding="utf-8", newline="") as fh:
                fh.write(csv_line(header) + "\n")
        else:
            with open(path, "r", encoding="utf-8-sig", newline="") as fh:
                text = fh.read()
            first = text.split("\n", 1)[0].rstrip("\r")
            if parse_csv_line(first) != list(header):
                raise CliFail(f"csv: header mismatch in {path}")
            if text and not text.endswith("\n"):
                with open(path, "a", encoding="utf-8", newline="") as fh:
                    fh.write("\n")
        with open(path, "a", encoding="utf-8", newline="") as fh:
            fh.write(csv_line([row.get(h, "") for h in header]) + "\n")


def csv_rows(path):
    """Rows of a small CSV as dicts ([] when missing). Uses the stdlib reader (RFC 4180)."""
    import csv

    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


# ---------- licence rows ----------

def license_row_tool(ws, override=None):
    """Path of license_row.mjs, or None when it is not installed."""
    p = os.path.abspath(override) if override else (os.path.join(ws, LICENSE_ROW_REL) if ws else None)
    return p if p and os.path.isfile(p) else None


def _node_tool(tool, ws, args, stdin=None, timeout=120):
    """Run `node <tool> <args>`; returns (exit, last stdout line parsed as JSON or None, detail)."""
    node = shutil.which("node")
    if not node:
        return None, None, "node not found on PATH"
    try:
        r = subprocess.run([node, tool, *args], input=stdin, cwd=ws, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=timeout)
    except subprocess.TimeoutExpired:
        return None, None, f"{os.path.basename(tool)} timed out after {timeout} s"
    except OSError as e:
        return None, None, f"{os.path.basename(tool)} did not start: {e}"
    lines = [x for x in (r.stdout or "").splitlines() if x.strip()]
    parsed = None
    if lines:
        try:
            parsed = json.loads(lines[-1])
        except ValueError:
            parsed = None
    detail = (lines[-1] if lines else "") or (r.stderr or "").strip()[-300:]
    return r.returncode, parsed, detail


def licence_row_exists(tool, ws, path):
    """Does Docs/licenses.csv hold a row for this W-relative path? True / False / None (could not tell). Uses
    `license_row.mjs get --path`, so the writer's own path normalisation (Unity subfolder, case on Windows) applies."""
    code, parsed, _ = _node_tool(tool, ws, ["get", "--path", path, "--json"])
    if isinstance(parsed, dict) and "row" in parsed:
        if isinstance(parsed["row"], dict):
            return True
        if parsed["row"] is None and code == EXIT_FAIL:
            return False
    return None


def licence_row(tool, ws, row):
    """Append one licence row through license_row.mjs. Returns (ok, detail)."""
    full = {k: str(row.get(k, "")) for k in LICENCE_COLUMNS}
    code, _, detail = _node_tool(tool, ws, ["add", "--from-json", "-", "--json"], stdin=json.dumps(full))
    if code == 0:
        return True, "written"
    if code is None:
        return False, detail
    return False, f"license_row.mjs exit {code}: {detail}"


def ensure_licence_row(tool, ws, row):
    """Write the row unless the ledger already covers its path. Returns (status, detail): status is 'exists',
    'written' or 'failed'. A lookup that cannot tell falls through to `add`, which refuses a duplicate path itself."""
    if licence_row_exists(tool, ws, row["path"]):
        return "exists", ""
    ok, detail = licence_row(tool, ws, row)
    return ("written" if ok else "failed"), ("" if ok else detail)
