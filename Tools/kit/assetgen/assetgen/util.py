"""Time, hashing and file helpers shared by every command (CONTRACTS.md §1.2, §1.3)."""

import datetime as _dt
import hashlib
import json
import os
import re
import secrets
import time
from pathlib import Path

UTC = _dt.timezone.utc
_ISO_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$")


def now():
    """UTC now at second precision; GSK_NOW (ISO) pins it for tests."""
    pinned = os.environ.get("GSK_NOW")
    if pinned:
        return parse_utc(pinned)
    return _dt.datetime.now(UTC).replace(microsecond=0)


def parse_utc(s):
    s = str(s).strip()
    if s.endswith("Z"):
        s = s[:-1] + "+00:00"
    d = _dt.datetime.fromisoformat(s)
    if d.tzinfo is None:
        d = d.replace(tzinfo=UTC)
    return d.astimezone(UTC).replace(microsecond=0)


def utc_iso(d=None):
    d = d or now()
    return d.astimezone(UTC).strftime("%Y-%m-%dT%H:%M:%SZ")


def file_stamp(d=None):
    d = d or now()
    return d.astimezone(UTC).strftime("%Y%m%dT%H%M%SZ")


def is_utc_iso(s):
    return isinstance(s, str) and bool(_ISO_RE.match(s))


def parse_since(spec):
    """'2d', '12h', '30m' or an ISO time → datetime."""
    m = re.fullmatch(r"(\d+)\s*([dhm])", str(spec).strip())
    if m:
        n, unit = int(m.group(1)), m.group(2)
        delta = {"d": _dt.timedelta(days=n), "h": _dt.timedelta(hours=n), "m": _dt.timedelta(minutes=n)}[unit]
        return now() - delta
    return parse_utc(spec)


def rand_suffix(n=6):
    alphabet = "abcdefghijklmnopqrstuvwxyz0123456789"
    return "".join(secrets.choice(alphabet) for _ in range(n))


def sha256_bytes(b):
    return hashlib.sha256(b).hexdigest()


def sha256_text(s):
    return sha256_bytes(s.encode("utf-8"))


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def decode_text(data, name="input"):
    """UTF-8 with or without BOM, or UTF-16 with a BOM (Windows PowerShell 5.1 `>` and Out-File write UTF-16LE).
    Anything else is a usage error that names the file (Set-Content writes the ANSI code page)."""
    from .errors import UsageError

    if data.startswith(b"\xef\xbb\xbf"):
        data = data[3:]
    elif data.startswith((b"\xff\xfe", b"\xfe\xff")):
        try:
            return data.decode("utf-16").replace("\r\n", "\n")
        except UnicodeDecodeError:
            raise UsageError(f"{name} has a UTF-16 byte-order mark but is not valid UTF-16 (save it as UTF-8)")
    try:
        return data.decode("utf-8").replace("\r\n", "\n")
    except UnicodeDecodeError:
        raise UsageError(f"{name} is not UTF-8 (save it as UTF-8; Windows PowerShell Set-Content writes the ANSI code page)")


def read_text(path):
    """UTF-8 with or without BOM, or UTF-16 with a BOM; CRLF becomes LF."""
    return decode_text(Path(path).read_bytes(), str(path))


def read_json(path):
    return json.loads(read_text(path))


def read_json_or(path, default):
    try:
        return read_json(path)
    except FileNotFoundError:
        return default


def _replace_retry(src, dst):
    # Windows refuses to replace a file another process holds open for a moment (readers, antivirus): retry briefly.
    for i in range(40):
        try:
            os.replace(src, dst)
            return
        except PermissionError:
            if i == 39:
                raise
            time.sleep(0.05)


def write_bytes_atomic(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{os.getpid()}.{rand_suffix(4)}.tmp")
    with open(tmp, "wb") as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    _replace_retry(tmp, path)


def write_text_atomic(path, text):
    if not text.endswith("\n"):
        text += "\n"
    write_bytes_atomic(path, text.encode("utf-8"))


def to_json(obj, indent=2):
    return json.dumps(obj, indent=indent, ensure_ascii=False)


def write_json_atomic(path, obj):
    write_text_atomic(path, to_json(obj))


def create_json_exclusive(path, obj):
    """Create a JSON file only if it does not exist (O_EXCL). Returns False when it already exists."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_BINARY", 0))
    except FileExistsError:
        return False
    try:
        os.write(fd, (to_json(obj) + "\n").encode("utf-8"))
        os.fsync(fd)
    finally:
        os.close(fd)
    return True


def append_jsonl(path, obj):
    """One line, one write call, O_APPEND (CONTRACTS.md §1.3)."""
    line = json.dumps(obj, ensure_ascii=False, separators=(",", ":")) + "\n"
    data = line.encode("utf-8")
    if len(data) > (1 << 20):
        raise ValueError("ledger line over 1 MiB")
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_APPEND | getattr(os, "O_BINARY", 0))
    try:
        os.write(fd, data)
    finally:
        os.close(fd)


def read_jsonl(path, strict=False):
    try:
        text = read_text(path)
    except FileNotFoundError:
        return []
    out = []
    for i, raw in enumerate(text.split("\n"), 1):
        if not raw.strip():
            continue
        try:
            obj = json.loads(raw)
        except json.JSONDecodeError:
            if strict:
                raise ValueError(f"{path}:{i}: not JSON")
            continue
        if isinstance(obj, dict):
            out.append(obj)
        elif strict:
            raise ValueError(f"{path}:{i}: not an object")
    return out


def rel_posix(path, base):
    """Workspace-relative POSIX path (CONTRACTS.md §1.1)."""
    p = Path(os.path.abspath(path))
    b = Path(os.path.abspath(base))
    try:
        return p.relative_to(b).as_posix()
    except ValueError:
        try:
            return os.path.relpath(p, b).replace("\\", "/")
        except ValueError:  # another drive on Windows
            return p.as_posix()


def _norm(p):
    return os.path.normcase(os.path.abspath(str(p))).rstrip("\\/")


def is_inside(child, parent):
    """True when child is parent or below it: case-insensitive on Windows and separator-aware (W2 is not inside W).
    Callers pass resolved paths, so junctions and symlinks are already followed."""
    c, p = _norm(child), _norm(parent)
    return c == p or c.startswith(p + os.sep)


def kit_root():
    """The kit repository root when this package runs from the repo (tests); None once installed in Tools/kit."""
    code = Path(__file__).resolve().parent.parent
    parents = code.parents
    if len(parents) > 4 and parents[3].name == "skills" and parents[2].name == "game-asset-generation":
        return parents[4]
    return None


def throwaway_roots(fixtures=False):
    """Folders a test-mode workspace may live in: the system temp folder and, in the kit repo, _scratch/. With
    fixtures=True also the kit's tests/ folder, where fixture programs (the fake Higgsfield CLI) live."""
    import tempfile

    roots = [Path(tempfile.gettempdir()).resolve()]
    kr = kit_root()
    if kr is not None:
        roots.append((kr / "_scratch").resolve())
        if fixtures:
            roots.append((kr / "tests").resolve())
    return roots


def in_throwaway(path, fixtures=False):
    p = Path(path).resolve()
    return any(is_inside(p, r) for r in throwaway_roots(fixtures))


def glob_match(pattern, text):
    """'*' matches any run of characters (including '/'), '?' one character."""
    rx = "^" + "".join(".*" if c == "*" else "." if c == "?" else re.escape(c) for c in pattern) + "$"
    return re.match(rx, text) is not None


URL_RE = re.compile(r"(?:https?|data):[^\s\"'<>]+", re.I)


def scrub_urls(text, limit=300):
    """Error text never carries signed URLs or long bodies (WoO fal_job.py defect: SDK text can hold signed URLs)."""
    s = URL_RE.sub("<url>", str(text))
    s = re.sub(r"\s+", " ", s).strip()
    return s[:limit]
