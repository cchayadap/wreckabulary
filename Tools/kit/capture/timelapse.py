"""timelapse.py: desktop timelapse in Python (Pillow ImageGrab, no ffmpeg).

    uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py start [--out <dir>] [--interval-sec 10]
        [--max-frames 0] [--max-width 1920] [--quality 80] [--min-free-gb 10] [--max-errors 20] [--workspace <W>] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py stop   [--workspace <W>] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py status [--workspace <W>] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py once   [--out <dir>] [--max-width 1920] [--quality 80] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py run    [same options as start]   (foreground; Ctrl+C stops)

Why it exists: on the kit's build machine Windows Defender AMSI refused to load desktop_timelapse.ps1
(ScriptContainedMaliciousContent). This is a separate implementation in Python, not a way around that verdict: the
owner says yes before its first start. It follows the same contract, so either tool can stop or report the other's
session:
- Frames go to <out>/<YYYYMMDDTHHMMSSZ>/<YYYYMMDDTHHMMSSZ>.jpg, UTC only; --out defaults to <W>/Recordings/timelapse.
  A second session in the same second gets a _2 suffix.
- `start` runs the capture in a separate worker with its own minimized console window titled
  "GSK desktop timelapse: RECORDING ...", so the owner can always see that the screen is being recorded.
- The worker writes .harness/timelapse.pid itself: line 1 PID, line 2 process start UTC, line 3 session folder,
  line 4 image name, line 5 a claim token (desktop_timelapse.ps1 reads lines 1-4). Starts are serialised by
  .harness/timelapse.start.lock (broken after 60 s). A start while a verified recorder runs exits 75.
- `stop` checks that the PID's start time (within 2 s) and image still match the file while holding a process handle,
  so the PID cannot be reused meanwhile. It withdraws the PID file; the worker notices within a second, writes
  _session.json (reason "stopped") and exits. Only after 15 s is it killed, by PID tree (taskkill /PID /T /F), never by
  name. A dead or reused PID means a stale file: it is deleted and nothing is killed. Unreadable process facts mean
  nothing is killed and the exit is 75 (CONTRACTS.md section 1.6).
- The worker also ends after --max-frames, after --max-errors capture errors in a row (a locked screen, for
  example), when free disk drops below --min-free-gb (GiB), on Ctrl+C, or when its console window is closed. Each
  session folder gets _session.json {schema, tool, start_utc, end_utc, frames, engine, interval_sec, reason, pid}.
- Exit codes: 0 ok, 1 capture or start failed, 2 usage, 3 no kit workspace, 75 running, starting or unverifiable.
- Compile without ffmpeg: frames_to_gif.py --in <session dir> --out <gif>; contact_sheet.py --in <session dir>
  --out <png> --select every:30.
Full process checks are implemented for Windows; on Linux they use /proc. Elsewhere `stop` reports 75 and asks for the
worker's window to be closed.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import secrets
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import capture_common as cc  # noqa: E402

PID_REL = Path(".harness") / "timelapse.pid"
LOCK_REL = Path(".harness") / "timelapse.start.lock"
DEFAULT_OUT_REL = Path("Recordings") / "timelapse"
STOP_GRACE_S = 15
CLAIM_WAIT_S = 20
LOCK_STALE_S = 60
PID_MATCH_S = 2.0
IS_WIN = os.name == "nt"
CREATE_BREAKAWAY_FROM_JOB = 0x01000000
BREAKAWAY_WARNING = ("job breakaway refused: the recorder may stop when `uv run` exits; "
                     "check `timelapse.py status` in a few seconds")


# ------------------------------------------------------------------ small helpers

def stamp(d: dt.datetime) -> str:
    return d.astimezone(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def iso(d: dt.datetime) -> str:
    return d.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def write_text_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    tmp.write_text(text, encoding="utf-8", newline="\n")
    os.replace(tmp, path)


def free_gib(folder: Path) -> float:
    """Free space on the volume that holds `folder` (or its nearest existing parent), in GiB."""
    d = Path(folder).resolve()
    while not d.exists() and d.parent != d:
        d = d.parent
    try:
        return shutil.disk_usage(d).free / 1024 ** 3
    except OSError:
        return float("inf")  # a share without usage figures: keep recording


def new_session_dir(root: Path) -> Path:
    base = stamp(cc.now_utc())
    d, i = root / base, 2
    while d.exists():
        d, i = root / f"{base}_{i}", i + 1
    d.mkdir(parents=True)
    return d


def frame_path(session: Path) -> Path:
    base = stamp(cc.now_utc())
    p, i = session / f"{base}.jpg", 2
    while p.exists():
        p, i = session / f"{base}_{i}.jpg", i + 1
    return p


def count_frames(session: Path | None) -> int | None:
    if not session or not session.is_dir():
        return None
    return sum(1 for p in session.glob("*.jpg") if not p.name.startswith("."))


# ------------------------------------------------------------------ process facts (the §1.6 rule)

if IS_WIN:
    import ctypes
    from ctypes import wintypes

    _k32 = ctypes.WinDLL("kernel32", use_last_error=True)
    _k32.OpenProcess.restype = wintypes.HANDLE
    _k32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    _k32.CloseHandle.argtypes = [wintypes.HANDLE]
    _k32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    _k32.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
    _k32.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR,
                                                ctypes.POINTER(wintypes.DWORD)]
    _k32.WaitForSingleObject.restype = wintypes.DWORD
    _k32.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    _k32.GetCurrentProcess.restype = wintypes.HANDLE
    SYNCHRONIZE, QUERY_LIMITED = 0x00100000, 0x1000
    STILL_ACTIVE, WAIT_OBJECT_0 = 259, 0
    ERROR_INVALID_PARAMETER = 87


def _filetime_utc(ft) -> dt.datetime:
    ticks = (ft.dwHighDateTime << 32) | ft.dwLowDateTime  # 100 ns since 1601-01-01 UTC
    return dt.datetime(1601, 1, 1, tzinfo=dt.timezone.utc) + dt.timedelta(microseconds=ticks // 10)


class ProcHandle:
    """An open handle to a process (Windows) or a PID (elsewhere). While a Windows handle is open the PID cannot be
    given to another process, so checks, the wait and a fallback kill all concern the same process."""

    def __init__(self, pid: int):
        self.pid, self.h = pid, None
        self.state, self.start, self.image, self.reason = "unknown", None, None, None
        if IS_WIN:
            self._open_win()
        else:
            self._open_posix()

    def _open_win(self):
        h = _k32.OpenProcess(SYNCHRONIZE | QUERY_LIMITED, False, self.pid)
        if not h:
            err = ctypes.get_last_error()
            self.state = "dead" if err == ERROR_INVALID_PARAMETER else "unknown"
            self.reason = "process is gone" if self.state == "dead" else f"cannot open the process (error {err})"
            return
        self.h = h
        code = wintypes.DWORD()
        if _k32.GetExitCodeProcess(h, ctypes.byref(code)) and code.value != STILL_ACTIVE:
            self.state, self.reason = "dead", "process is gone"
            return
        c, e, k, u = (wintypes.FILETIME() for _ in range(4))
        buf, n = ctypes.create_unicode_buffer(1024), wintypes.DWORD(1024)
        if not _k32.GetProcessTimes(h, ctypes.byref(c), ctypes.byref(e), ctypes.byref(k), ctypes.byref(u)):
            self.reason = "cannot read the process start time"
            return
        self.start = _filetime_utc(c)
        if _k32.QueryFullProcessImageNameW(h, 0, buf, ctypes.byref(n)):
            self.image = Path(buf.value).stem
        self.state = "alive"

    def _open_posix(self):
        try:
            os.kill(self.pid, 0)
        except ProcessLookupError:
            self.state, self.reason = "dead", "process is gone"
            return
        except PermissionError:
            pass
        try:
            stat = Path(f"/proc/{self.pid}/stat").read_text()
            ticks = int(stat.rsplit(")", 1)[1].split()[19])
            btime = next(int(line.split()[1]) for line in Path("/proc/stat").read_text().splitlines()
                         if line.startswith("btime"))
            self.start = dt.datetime.fromtimestamp(btime + ticks / os.sysconf("SC_CLK_TCK"), dt.timezone.utc)
            self.image = Path(f"/proc/{self.pid}/comm").read_text().strip()
            self.state = "alive"
        except (OSError, ValueError, IndexError, StopIteration):
            self.reason = "cannot read the process start time on this platform"

    def wait(self, seconds: float) -> bool:
        """True when the process has exited within `seconds`."""
        if IS_WIN and self.h:
            return _k32.WaitForSingleObject(self.h, int(seconds * 1000)) == WAIT_OBJECT_0
        end = time.monotonic() + seconds
        while time.monotonic() < end:
            try:
                os.kill(self.pid, 0)
            except ProcessLookupError:
                return True
            time.sleep(0.2)
        return False

    def close(self):
        if IS_WIN and self.h:
            _k32.CloseHandle(self.h)
            self.h = None


def self_facts() -> tuple[int, dt.datetime, str]:
    """This process's PID, start time (UTC) and image name, as a stop would read them."""
    p = ProcHandle(os.getpid())
    try:
        start = p.start or cc.now_utc()
        return os.getpid(), start, p.image or Path(sys.executable).stem
    finally:
        p.close()


def tree_kill(pid: int) -> None:
    """The only kill in this tool: by PID tree, never by name."""
    if IS_WIN:
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True, check=False)
    else:
        try:
            os.kill(pid, 9)
        except OSError:
            pass


# ------------------------------------------------------------------ PID file and start lock

def read_pid_file(path: Path) -> dict | None:
    """None when absent; busy=True when it stays unreadable; valid=True when lines 1-2 parse."""
    lines = None
    for _ in range(5):
        if not path.is_file():
            return None
        try:
            lines = [ln.strip() for ln in path.read_text(encoding="utf-8-sig").splitlines()]
            break
        except FileNotFoundError:
            return None
        except OSError:
            time.sleep(0.05)
    if lines is None:
        return {"valid": False, "busy": True}
    info = {"valid": False, "busy": False, "raw": " | ".join(lines)}
    if len(lines) >= 2 and lines[0].isdigit() and int(lines[0]) > 0:
        start = cc.parse_utc(lines[1])
        if start is not None:
            info.update(valid=True, pid=int(lines[0]), start=start,
                        session=lines[2] if len(lines) > 2 and lines[2] else None,
                        image=lines[3] if len(lines) > 3 and lines[3] else None,
                        token=lines[4] if len(lines) > 4 and lines[4] else None)
    return info


def remove_file(path: Path) -> bool:
    for _ in range(20):
        try:
            path.unlink()
            return True
        except FileNotFoundError:
            return True
        except OSError:
            time.sleep(0.1)
    return not path.exists()


def facts_mismatch(info: dict, p: ProcHandle) -> str | None:
    delta = abs((p.start - info["start"]).total_seconds())
    if delta > PID_MATCH_S:
        return f"PID {info['pid']} now belongs to another process (start differs by {delta:.0f} s)"
    if info.get("image") and p.image and info["image"].lower() != p.image.lower():
        return f"PID {info['pid']} now belongs to {p.image}, not {info['image']}"
    return None


def get_state(pid_path: Path, keep_handle: bool = False) -> dict:
    """running | stale | unknown | none, plus the parsed file (and an open handle when keep_handle and running)."""
    f = read_pid_file(pid_path)
    if f is None:
        return {"state": "none", "file": None}
    if f["busy"]:
        return {"state": "unknown", "file": f, "reason": "the PID file stays locked by another process"}
    if not f["valid"]:
        return {"state": "stale", "file": f, "reason": "unreadable pid file"}
    p = ProcHandle(f["pid"])
    try:
        if p.state == "dead":
            return {"state": "stale", "file": f, "reason": p.reason}
        if p.state == "unknown":
            return {"state": "unknown", "file": f, "reason": p.reason}
        why = facts_mismatch(f, p)
        if why:
            return {"state": "stale", "file": f, "reason": why}
        res = {"state": "running", "file": f}
        if keep_handle:
            res["proc"], p = p, None
        return res
    finally:
        if p is not None:
            p.close()


def enter_lock(path: Path) -> bool:
    path.parent.mkdir(parents=True, exist_ok=True)
    for _ in range(2):
        try:
            fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(f"{os.getpid()} {iso(dt.datetime.now(dt.timezone.utc))}\n")
            return True
        except FileExistsError:
            try:
                age = time.time() - path.stat().st_mtime
            except OSError:
                continue
            if age <= LOCK_STALE_S:
                return False
            remove_file(path)  # a start that crashed more than a minute ago
        except OSError:
            return False
    return False


def still_owner(pid_path: Path, strict: bool = False) -> bool:
    """Loose: an unreadable file counts as still ours for this moment (the front end may be writing it)."""
    f = read_pid_file(pid_path)
    if f is None:
        return False
    if f["busy"]:
        return not strict
    return f["valid"] and f["pid"] == os.getpid()


# ------------------------------------------------------------------ capture

def grab_frame(path: Path, max_width: int, quality: int) -> tuple[int, int]:
    from PIL import Image, ImageGrab  # noqa: PLC0415

    img = ImageGrab.grab(all_screens=True)
    if img.mode != "RGB":
        img = img.convert("RGB")
    if max_width > 0 and img.width > max_width:
        img = img.resize((max_width, max(1, round(img.height * max_width / img.width))), Image.Resampling.LANCZOS)
    cc.save_image_atomic(img, path, format="JPEG", quality=quality)
    return img.width, img.height


def set_console_title(text: str) -> None:
    if IS_WIN:
        try:
            ctypes.windll.kernel32.SetConsoleTitleW(text)
        except OSError:
            pass


class Recorder:
    """The worker loop. finish() is idempotent, so Ctrl+C, a closed window and a normal end all leave one
    _session.json and never delete a PID file that names another process."""

    def __init__(self, args, ws: Path | None, pid_path: Path | None, session: Path):
        self.args, self.ws, self.pid_path, self.session = args, ws, pid_path, session
        self.frames, self.errors, self.started = 0, 0, cc.now_utc()
        self._done, self._lock = False, threading.Lock()

    def finish(self, reason: str) -> None:
        with self._lock:
            if self._done:
                return
            self._done = True
        summary = {"schema": 1, "tool": "timelapse.py", "start_utc": iso(self.started), "end_utc": iso(cc.now_utc()),
                   "frames": self.frames, "engine": "pillow", "interval_sec": self.args.interval_sec,
                   "reason": reason, "pid": os.getpid()}
        try:
            write_text_atomic(self.session / "_session.json", json.dumps(summary, indent=2) + "\n")
        except OSError:
            pass
        if self.pid_path is not None and still_owner(self.pid_path, strict=True):
            remove_file(self.pid_path)

    def owner(self) -> bool:
        return self.pid_path is None or still_owner(self.pid_path)

    def loop(self) -> str:
        a = self.args
        err_log = self.session / "_errors.log"
        due = time.monotonic()  # a fixed cadence: frame N is due at start + N x interval, whatever a capture costs
        while True:
            if not self.owner():
                return "stopped"
            if self.frames % 30 == 0 and free_gib(self.session) < a.min_free_gb:
                return "low_disk"
            try:
                grab_frame(frame_path(self.session), a.max_width, a.quality)
                self.frames += 1
                self.errors = 0
            except Exception as e:  # noqa: BLE001 - a locked screen raises OSError; count it and keep going
                self.errors += 1
                with open(err_log, "a", encoding="utf-8", newline="\n") as fh:
                    fh.write(f"{iso(cc.now_utc())} {type(e).__name__}: {e}\n")
                if self.errors >= a.max_errors:
                    return "capture_errors"
            if a.max_frames > 0 and self.frames >= a.max_frames:
                return "max_frames"
            due = max(due + a.interval_sec, time.monotonic())  # after a slow capture: skip ahead, never burst
            while time.monotonic() < due:
                time.sleep(min(0.5, max(0.0, due - time.monotonic())))
                if not self.owner():
                    return "stopped"


def install_close_handler(rec: Recorder):
    """Closing the console window (or logoff/shutdown) still writes _session.json and releases the PID file."""
    if not IS_WIN:
        return None
    handler_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.DWORD)

    def handler(event):
        if event in (2, 5, 6):  # CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT, CTRL_SHUTDOWN_EVENT
            rec.finish("window_closed")
        return False  # let the default handling (Ctrl+C -> KeyboardInterrupt, close -> exit) continue

    cb = handler_type(handler)
    ctypes.windll.kernel32.SetConsoleCtrlHandler(cb, True)
    return cb  # keep a reference, or the callback is collected


# ------------------------------------------------------------------ commands

def need_ws(args) -> Path:
    ws = cc.find_workspace(args.workspace) if args.workspace else cc.find_workspace()
    if ws is None:
        raise cc.ToolError("no kit workspace (Tools/kit/kit.json) found; run inside a project or pass --workspace",
                           cc.EXIT_INFRA)
    return ws


def out_root(args, ws: Path | None) -> Path:
    if args.out:
        return Path(args.out).resolve()
    if ws is None:
        raise cc.ToolError("no kit workspace (Tools/kit/kit.json) found; pass --out", cc.EXIT_INFRA)
    return ws / DEFAULT_OUT_REL


def check_options(args) -> None:
    if args.max_width < 0 or not 10 <= args.quality <= 100:
        raise cc.UsageError("--max-width >= 0 (0: full size), --quality 10-100")
    if args.cmd in ("start", "run"):
        if not 1 <= args.interval_sec <= 86400:
            raise cc.UsageError("--interval-sec must be 1-86400")
        if args.max_frames < 0 or args.max_errors < 1 or args.min_free_gb < 0:
            raise cc.UsageError("--max-frames >= 0, --max-errors >= 1, --min-free-gb >= 0")


def cmd_once(args) -> tuple[dict, int, list[str]]:
    check_options(args)
    ws = cc.find_workspace(args.workspace) if args.workspace else cc.find_workspace()
    folder = Path(args.out).resolve() if args.out else out_root(args, ws) / "_once"
    folder.mkdir(parents=True, exist_ok=True)
    f = frame_path(folder)
    try:
        w, h = grab_frame(f, args.max_width, args.quality)
    except Exception as e:  # noqa: BLE001
        raise cc.ToolError(f"capture failed: {type(e).__name__}: {e}", cc.EXIT_FAIL) from e
    rel = cc.rel_path(f, ws)
    return {"ok": True, "action": "once", "file": rel, "size": [w, h], "engine": "pillow"}, cc.EXIT_OK, \
        [f"wrote {rel} ({w}x{h})"]


def cmd_status(args) -> tuple[dict, int, list[str]]:
    ws = need_ws(args)
    pid_path = ws / PID_REL
    st = get_state(pid_path)
    status = "not_running" if st["state"] == "none" else st["state"]
    r = {"ok": True, "action": "status", "status": status, "pid_file": cc.rel_path(pid_path, ws)}
    f = st.get("file")
    if f and f.get("valid"):
        session = Path(f["session"]) if f.get("session") else None
        r.update(pid=f["pid"], process_start_utc=iso(f["start"]), image=f.get("image"),
                 session_dir=cc.rel_path(session, ws) if session else None, frames=count_frames(session))
    if st.get("reason"):
        r["reason"] = st["reason"]
    return r, cc.EXIT_OK, [f"timelapse: {status}" + (f" (PID {r['pid']}, {r.get('frames')} frames)" if 'pid' in r and status == 'running' else "")]


def cmd_stop(args) -> tuple[dict, int, list[str]]:
    ws = need_ws(args)
    pid_path = ws / PID_REL
    pid_rel = cc.rel_path(pid_path, ws)
    st = get_state(pid_path, keep_handle=True)
    if st["state"] == "none":
        return {"ok": True, "action": "stop", "status": "not_running", "pid_file": pid_rel}, cc.EXIT_OK, \
            ["timelapse: not running"]
    f = st["file"]
    if st["state"] == "stale":
        remove_file(pid_path)
        r = {"ok": True, "action": "stop", "status": "stale", "reason": st["reason"], "killed": False, "pid_file": pid_rel}
        if f.get("valid"):
            r["pid"] = f["pid"]
        return r, cc.EXIT_OK, [f"timelapse: stale pid file removed ({st['reason']}); nothing killed"]
    if st["state"] == "unknown":
        raise cc.ToolError(f"cannot verify PID {f.get('pid')} ({st['reason']}); nothing killed", cc.EXIT_BLOCKED,
                           action="stop", status="unknown", killed=False, pid=f.get("pid"))
    proc: ProcHandle = st["proc"]
    try:
        remove_file(pid_path)  # 1) graceful: the worker sees it no longer owns the file and finishes
        graceful = proc.wait(STOP_GRACE_S)
        forced = False
        if not graceful:  # 2) a hung capture: kill its tree; the open handle means this PID is still the worker
            forced = True
            tree_kill(f["pid"])
            proc.wait(10)
        alive = not proc.wait(0)
    finally:
        proc.close()
    if alive:
        raise cc.ToolError(f"PID {f['pid']} did not exit after taskkill", cc.EXIT_FAIL, action="stop",
                           status="kill_failed", pid=f["pid"])
    session = Path(f["session"]) if f.get("session") else None
    sj = session / "_session.json" if session else None
    r = {"ok": True, "action": "stop", "status": "stopped", "graceful": graceful, "killed": forced, "pid": f["pid"],
         "session_dir": cc.rel_path(session, ws) if session else None,
         "session_json": cc.rel_path(sj, ws) if sj and sj.is_file() else None,
         "frames": count_frames(session), "pid_file": pid_rel}
    text = [f"timelapse: stopped PID {f['pid']}" + (f" (killed after {STOP_GRACE_S} s)" if forced else ""),
            f"frames: {r['frames']} in {r['session_dir']}"]
    return r, cc.EXIT_OK, text


def worker_argv(args, session: Path, ws: Path, token: str) -> list[str]:
    return [sys.executable, str(Path(__file__).resolve()), "run", "--session", str(session), "--claim", token,
            "--workspace", str(ws), "--interval-sec", str(args.interval_sec), "--max-frames", str(args.max_frames),
            "--max-width", str(args.max_width), "--quality", str(args.quality), "--min-free-gb", repr(args.min_free_gb),
            "--max-errors", str(args.max_errors)]


def spawn_worker(argv: list[str], cwd: Path) -> tuple[subprocess.Popen, bool]:
    """Returns (process, breakaway_refused)."""
    if not IS_WIN:
        return subprocess.Popen(argv, cwd=cwd, stdin=subprocess.DEVNULL, start_new_session=True), False
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 7  # SW_SHOWMINNOACTIVE: visible in the taskbar, does not steal focus
    base = subprocess.CREATE_NEW_CONSOLE
    try:  # leave uv's job object, or the recorder dies when `uv run` returns
        return subprocess.Popen(argv, cwd=cwd, creationflags=base | CREATE_BREAKAWAY_FROM_JOB, startupinfo=si), False
    except OSError:
        return subprocess.Popen(argv, cwd=cwd, creationflags=base, startupinfo=si), True


def cmd_start(args) -> tuple[dict, int, list[str]]:
    check_options(args)
    ws = need_ws(args)
    pid_path, lock = ws / PID_REL, ws / LOCK_REL
    if not enter_lock(lock):
        raise cc.ToolError("another timelapse start is in progress; try again in a moment", cc.EXIT_BLOCKED,
                           action="start", status="starting")
    proc, session, started, stale_note, token, breakaway_refused = None, None, False, None, None, False
    try:
        st = get_state(pid_path)
        f = st.get("file") or {}
        if st["state"] == "running":
            raise cc.ToolError(f"a timelapse is already running (PID {f['pid']})", cc.EXIT_BLOCKED, action="start",
                               status="running", pid=f["pid"])
        if st["state"] == "unknown":
            raise cc.ToolError(f"cannot verify PID {f.get('pid')} in {PID_REL.as_posix()}; not starting a second capture",
                               cc.EXIT_BLOCKED, action="start", status="unknown")
        if st["state"] == "stale":
            if not remove_file(pid_path):
                raise cc.ToolError(f"cannot remove the stale {PID_REL.as_posix()}", cc.EXIT_BLOCKED, action="start",
                                   status="unknown")
            stale_note = st["reason"]
        root = out_root(args, ws)
        if free_gib(root) < args.min_free_gb:
            raise cc.ToolError(f"less than {args.min_free_gb} GiB free for {cc.rel_path(root, ws)}", cc.EXIT_FAIL,
                               action="start", status="low_disk")
        session = new_session_dir(root)
        token = secrets.token_hex(8)
        proc, breakaway_refused = spawn_worker(worker_argv(args, session, ws, token), ws)
        deadline = time.monotonic() + CLAIM_WAIT_S
        info = None
        while time.monotonic() < deadline:
            info = read_pid_file(pid_path)
            if info and info.get("valid") and info.get("token") == token:
                break
            if proc.poll() is not None:
                raise cc.ToolError(f"the capture worker exited immediately (exit {proc.returncode})", cc.EXIT_FAIL,
                                   action="start")
            time.sleep(0.1)
        else:
            raise cc.ToolError(f"the capture worker did not claim {PID_REL.as_posix()} within {CLAIM_WAIT_S} s",
                               cc.EXIT_FAIL, action="start")
        started = True
    finally:
        if not started:
            if proc is not None and proc.poll() is None:
                tree_kill(proc.pid)  # Popen still holds the launcher's handle, so this PID is ours
            f2 = read_pid_file(pid_path)
            if token and f2 and f2.get("valid") and f2.get("token") == token:
                remove_file(pid_path)  # our worker claimed it before the start failed
            if session is not None and session.is_dir() and not any(session.iterdir()):
                session.rmdir()
        remove_file(lock)
    r = {"ok": True, "action": "start", "status": "running", "pid": info["pid"], "process_start_utc": iso(info["start"]),
         "image": info.get("image"), "session_dir": cc.rel_path(session, ws), "engine": "pillow",
         "interval_sec": args.interval_sec, "max_frames": args.max_frames, "max_width": args.max_width,
         "pid_file": cc.rel_path(pid_path, ws), "warnings": [BREAKAWAY_WARNING] if breakaway_refused else []}
    if stale_note:
        r["stale_pid_file_removed"] = stale_note
    return r, cc.EXIT_OK, [f"timelapse started: PID {info['pid']}, a frame every {args.interval_sec} s into "
                           f"{r['session_dir']}", *(f"warning: {w}" for w in r["warnings"]),
                           "stop with: timelapse.py stop"]


def cmd_run(args) -> tuple[dict, int, list[str]]:
    """The worker. With --claim it was started by `start` (which holds the start lock and checked the state);
    without it, a person runs it in a terminal and it takes the lock itself."""
    check_options(args)
    ws = cc.find_workspace(args.workspace) if args.workspace else cc.find_workspace()
    pid_path = ws / PID_REL if ws else None
    if args.claim and pid_path is None:
        return {"ok": False, "exit": cc.EXIT_INFRA, "error": "no workspace"}, cc.EXIT_INFRA, ["error: no workspace"]
    session = Path(args.session).resolve() if args.session else new_session_dir(out_root(args, ws))
    session.mkdir(parents=True, exist_ok=True)
    pid, start, image = self_facts()
    line = f"{pid}\n{iso(start)}\n{session}\n{image}\n{args.claim or secrets.token_hex(8)}\n"
    if pid_path is not None:
        if args.claim:
            write_text_atomic(pid_path, line)
        else:
            lock = ws / LOCK_REL
            if not enter_lock(lock):
                raise cc.ToolError("another timelapse start is in progress", cc.EXIT_BLOCKED)
            try:
                st = get_state(pid_path)
                if st["state"] in ("running", "unknown"):
                    raise cc.ToolError(f"a timelapse is already running or unverifiable ({st['state']})", cc.EXIT_BLOCKED)
                if st["state"] == "stale":
                    remove_file(pid_path)
                write_text_atomic(pid_path, line)
            finally:
                remove_file(lock)
    set_console_title(f"GSK desktop timelapse: RECORDING the screen every {args.interval_sec} s "
                      f"(stop: timelapse.py stop, or close this window)")
    print(f"Recording one desktop frame every {args.interval_sec} s into {session}", flush=True)
    print("Stop with: uv run --project Tools/kit/capture Tools/kit/capture/timelapse.py stop  (or close this window)",
          flush=True)
    rec = Recorder(args, ws, pid_path, session)
    _keep = install_close_handler(rec)  # noqa: F841 - the reference keeps the callback alive
    reason = "stopped"
    try:
        reason = rec.loop()
    except KeyboardInterrupt:
        reason = "interrupted"
    except Exception as e:  # noqa: BLE001
        reason = f"error: {type(e).__name__}: {e}"
    finally:
        rec.finish(reason)
    r = {"ok": True, "action": "run", "reason": reason, "frames": rec.frames, "session_dir": cc.rel_path(session, ws)}
    return r, cc.EXIT_OK, [f"timelapse ended ({reason}): {rec.frames} frames in {r['session_dir']}"]


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="timelapse.py", description="desktop timelapse (Pillow ImageGrab, no ffmpeg)")
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("start", "run", "once", "stop", "status"):
        p = sub.add_parser(name)
        p.add_argument("--workspace", help="kit workspace W (default: CLAUDE_PROJECT_DIR or the current folder)")
        p.add_argument("--json", action="store_true", help="print exactly one JSON line on stdout")
        if name == "once":
            p.add_argument("--out", help="folder that receives the one frame; no workspace needed "
                                         "(default W/Recordings/timelapse/_once)")
        if name in ("start", "run"):
            p.add_argument("--out", help="root folder for session folders (default W/Recordings/timelapse)")
        if name in ("start", "run", "once"):
            p.add_argument("--max-width", type=int, default=1920, help="downscale wider frames (0: full size)")
            p.add_argument("--quality", type=int, default=80, help="JPEG quality 10-100")
        if name in ("start", "run"):
            p.add_argument("--interval-sec", type=int, default=10)
            p.add_argument("--max-frames", type=int, default=0, help="stop after N frames (0: no limit)")
            p.add_argument("--min-free-gb", type=float, default=10.0, help="stop below this much free disk (GiB)")
            p.add_argument("--max-errors", type=int, default=20, help="stop after N capture errors in a row")
        if name == "run":
            p.add_argument("--session", help=argparse.SUPPRESS)
            p.add_argument("--claim", help=argparse.SUPPRESS)
    return ap.parse_args(argv)


def main(argv):
    as_json = "--json" in argv
    args = parse_args([a for a in argv if a != "--json"])
    fn = {"start": cmd_start, "run": cmd_run, "once": cmd_once, "stop": cmd_stop, "status": cmd_status}[args.cmd]
    result, code, text = fn(args)
    cc.emit(result, as_json, text)
    return code


if __name__ == "__main__":
    sys.exit(cc.run_main(main))
