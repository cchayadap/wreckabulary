"""O_EXCL file locks and N-slot pools, compatible with lib/locks.mjs (CONTRACTS.md §1.6).

A lock file holds lockHolder JSON {pid, host, utc, purpose, token, process_start_utc, image}. It is stale (same host
only) when its PID is dead, when the PID now belongs to another process (start time or image differ, checked once the
lock is older than 15 s), or when it is unreadable and older than 10 minutes. Breaking is serialised by <lock>.break.
Never kills anything. Note: os.kill(pid, 0) terminates a process on Windows, so liveness uses OpenProcess.
"""

import contextlib
import datetime as _dt
import json
import os
import socket
import sys
import time
from pathlib import Path

from .errors import Blocked
from .util import UTC, rand_suffix, utc_iso

BUSY_ERRNOS = {13, 16}  # EACCES, EBUSY
BREAK_STALE_S = 10.0
_IS_WIN = os.name == "nt"

if _IS_WIN:
    import ctypes
    from ctypes import wintypes

    _k32 = ctypes.WinDLL("kernel32", use_last_error=True)
    _k32.OpenProcess.restype = wintypes.HANDLE
    _k32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    _k32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    _k32.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
    _k32.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    _k32.CloseHandle.argtypes = [wintypes.HANDLE]
    _k32.GetCurrentProcess.restype = wintypes.HANDLE
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    STILL_ACTIVE = 259
    ERROR_ACCESS_DENIED = 5

    def _filetime_ms(ft):
        v = (ft.dwHighDateTime << 32) | ft.dwLowDateTime
        return v / 10000.0 - 11644473600000.0

    def _proc_facts(handle):
        c, e, k, u = (wintypes.FILETIME() for _ in range(4))
        start = None
        if _k32.GetProcessTimes(handle, ctypes.byref(c), ctypes.byref(e), ctypes.byref(k), ctypes.byref(u)):
            start = _filetime_ms(c)
        buf = ctypes.create_unicode_buffer(1024)
        size = wintypes.DWORD(1024)
        image = buf.value if _k32.QueryFullProcessImageNameW(handle, 0, buf, ctypes.byref(size)) else None
        return start, image


def is_pid_alive(pid):
    if not isinstance(pid, int) or pid <= 0:
        return False
    if pid == os.getpid():
        return True
    if _IS_WIN:
        h = _k32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        if not h:
            return ctypes.get_last_error() == ERROR_ACCESS_DENIED
        try:
            code = wintypes.DWORD()
            if _k32.GetExitCodeProcess(h, ctypes.byref(code)):
                return code.value == STILL_ACTIVE
            return True
        finally:
            _k32.CloseHandle(h)
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def process_info(pid):
    """{start_ms, image} or None when the facts cannot be read."""
    if _IS_WIN:
        h = _k32.GetCurrentProcess() if pid == os.getpid() else _k32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        if not h:
            return None
        try:
            start, image = _proc_facts(h)
        finally:
            if pid != os.getpid():
                _k32.CloseHandle(h)
        if start is None:
            return None
        return {"start_ms": start, "image": image}
    try:
        with open(f"/proc/{pid}/stat", "rb") as f:
            fields = f.read().rsplit(b")", 1)[1].split()
        ticks = int(fields[19])
        with open("/proc/stat", "rb") as f:
            btime = next(int(l.split()[1]) for l in f if l.startswith(b"btime"))
        start = (btime + ticks / os.sysconf("SC_CLK_TCK")) * 1000.0
        image = os.readlink(f"/proc/{pid}/exe")
        return {"start_ms": start, "image": image}
    except Exception:
        return None


_SELF = None


def _self_facts():
    global _SELF
    if _SELF is None:
        info = process_info(os.getpid()) or {}
        start = info.get("start_ms") or (time.time() * 1000.0)
        _SELF = {"start_utc": utc_iso(_dt.datetime.fromtimestamp(start / 1000.0, UTC).replace(microsecond=0)), "image": info.get("image") or sys.executable}
    return _SELF


def pid_matches(pid, start_utc=None, image=None, not_after_ms=None, tol_ms=2000):
    if not is_pid_alive(pid):
        return "dead"
    info = process_info(pid)
    if not info:
        return "unknown" if is_pid_alive(pid) else "dead"
    if start_utc:
        try:
            from .util import parse_utc

            exp = parse_utc(start_utc).timestamp() * 1000.0
            if abs(info["start_ms"] - exp) > tol_ms:
                return "mismatch"
        except Exception:
            pass
    if not_after_ms is not None and info["start_ms"] > not_after_ms + tol_ms:
        return "mismatch"
    base = lambda p: str(p).replace("\\", "/").rsplit("/", 1)[-1].lower()
    if image and info.get("image") and base(image) != base(info["image"]):
        return "mismatch"
    return "match"


def _holder(purpose, token):
    facts = _self_facts()
    return {
        "pid": os.getpid(),
        "host": socket.gethostname(),
        "utc": utc_iso(_dt.datetime.now(UTC).replace(microsecond=0)),
        "purpose": purpose or "",
        "token": token,
        "process_start_utc": facts["start_utc"],
        "image": facts["image"],
    }


def read_holder(lock_path):
    try:
        with open(lock_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def _create(lock_path, info):
    try:
        fd = os.open(str(lock_path), os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_BINARY", 0))
    except FileExistsError:
        return "exists"
    except PermissionError:
        return "busy"
    except OSError as e:
        if e.errno in BUSY_ERRNOS:
            return "busy"
        raise
    try:
        os.write(fd, json.dumps(info).encode("utf-8"))
    except Exception:
        os.close(fd)
        with contextlib.suppress(Exception):
            os.unlink(lock_path)
        raise
    os.close(fd)
    return "ok"


def _unlink_retry(p):
    for _ in range(10):
        try:
            os.unlink(p)
            return True
        except FileNotFoundError:
            return False
        except PermissionError:
            time.sleep(0.02)
        except OSError as e:
            if e.errno not in BUSY_ERRNOS:
                return False
            time.sleep(0.02)
    return False


def _state(lock_path, holder, stale_s=600.0, pid_check_after_s=15.0):
    try:
        st = os.stat(lock_path)
    except FileNotFoundError:
        return "gone"
    except OSError:
        return "held"
    age = time.time() - st.st_mtime
    if not holder or not isinstance(holder.get("pid"), int):
        return "stale" if age > stale_s else "held"
    if holder.get("host") != socket.gethostname():
        return "held"
    if not is_pid_alive(holder["pid"]):
        return "stale"
    if holder["pid"] != os.getpid() and age < pid_check_after_s:
        return "held"
    m = pid_matches(holder["pid"], holder.get("process_start_utc"), holder.get("image"), st.st_mtime * 1000.0)
    return "stale" if m in ("mismatch", "dead") else "held"


def _same(a, b):
    if not a and not b:
        return True
    return bool(a) and bool(b) and a.get("token") == b.get("token") and a.get("pid") == b.get("pid")


def _break_stale(lock_path, seen):
    brk = Path(str(lock_path) + ".break")
    token = f"{os.getpid()}-{rand_suffix(8)}"
    r = _create(brk, _holder("break", token))
    if r != "ok":
        if r == "exists":
            with contextlib.suppress(Exception):
                if time.time() - os.stat(brk).st_mtime > BREAK_STALE_S:
                    _unlink_retry(brk)
        return False
    try:
        cur = read_holder(lock_path)
        if not _same(seen, cur):
            return True
        state = _state(lock_path, cur)
        if state == "gone":
            return True
        if state != "stale":
            return False
        _unlink_retry(lock_path)
        return True
    finally:
        h = read_holder(brk)
        if h and h.get("token") == token:
            _unlink_retry(brk)


class Lock:
    def __init__(self, path, token):
        self.path = Path(path)
        self.token = token

    def release(self):
        h = None
        for _ in range(5):
            h = read_holder(self.path)
            if h is not None or not self.path.exists():
                break
            time.sleep(0.01)
        if not h or h.get("token") != self.token:
            return False
        return _unlink_retry(self.path)


def is_held(lock_path):
    """True while a live process (or an unreadable, recent holder) holds the lock. Never breaks it."""
    lock_path = Path(lock_path)
    if not lock_path.exists():
        return False
    return _state(lock_path, read_holder(lock_path)) == "held"


def try_lock(lock_path, purpose=""):
    lock_path = Path(lock_path)
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    for _ in range(3):
        token = f"{os.getpid()}-{int(time.time() * 1000)}-{rand_suffix(8)}"
        r = _create(lock_path, _holder(purpose, token))
        if r == "ok":
            return Lock(lock_path, token)
        if r == "busy":
            return None
        holder = read_holder(lock_path)
        state = _state(lock_path, holder)
        if state == "gone":
            continue
        if state == "held" or not _break_stale(lock_path, holder):
            return None
    return None


def acquire(lock_path, purpose="", wait_s=30.0, poll_s=0.05):
    t0 = time.monotonic()
    while True:
        lk = try_lock(lock_path, purpose)
        if lk:
            return lk
        if time.monotonic() - t0 >= wait_s:
            holder = read_holder(lock_path) or {}
            raise Blocked(f"lock wait expired after {wait_s:.0f} s: {Path(lock_path).name} held by pid {holder.get('pid')} ({holder.get('purpose', '')})", reason="lock_timeout")
        time.sleep(poll_s)


@contextlib.contextmanager
def locked(lock_path, purpose="", wait_s=30.0):
    lk = acquire(lock_path, purpose, wait_s)
    try:
        yield lk
    finally:
        lk.release()


def acquire_slot(pool_dir, prefix, n, purpose="", wait_s=1800.0, poll_s=0.5):
    """First free slot of <pool_dir>/<prefix>-0..n-1.lock (per-provider semaphore). Raises Blocked on timeout."""
    t0 = time.monotonic()
    while True:
        for i in range(max(1, n)):
            lk = try_lock(Path(pool_dir) / f"{prefix}-{i}.lock", purpose)
            if lk:
                lk.slot = i
                return lk
        if time.monotonic() - t0 >= wait_s:
            raise Blocked(f"all {n} '{prefix}' concurrency slots stayed busy for {wait_s:.0f} s", reason="pool_timeout")
        time.sleep(poll_s)
