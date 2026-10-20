"""Shared helpers for the kit's capture tools (installed to Tools/kit/capture/, a uv project with Pillow).

Conventions (CONTRACTS.md §1): exit codes 0 OK, 1 FAIL, 2 USAGE, 3 INFRA, 75 BLOCKED; with --json stdout holds exactly
one JSON line; printed paths are POSIX and workspace-relative; timestamps are UTC and honour GSK_NOW.
"""
from __future__ import annotations

import datetime as dt
import glob
import json
import os
import sys
from pathlib import Path

EXIT_OK, EXIT_FAIL, EXIT_USAGE, EXIT_INFRA, EXIT_BLOCKED = 0, 1, 2, 3, 75
KIT_JSON_REL = Path("Tools") / "kit" / "kit.json"
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff"}


class UsageError(Exception):
    """Bad arguments (exit 2)."""


class ToolError(Exception):
    def __init__(self, message: str, exit_code: int = EXIT_FAIL, **fields):
        super().__init__(message)
        self.exit_code = exit_code
        self.fields = fields


def now_utc() -> dt.datetime:
    pinned = os.environ.get("GSK_NOW")
    if pinned:
        return dt.datetime.fromisoformat(pinned.replace("Z", "+00:00")).astimezone(dt.timezone.utc)
    return dt.datetime.now(dt.timezone.utc)


def utc_iso(d: dt.datetime | None = None) -> str:
    return (d or now_utc()).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_utc(s: str) -> dt.datetime | None:
    """Strict `YYYY-MM-DDTHH:MM:SSZ` (the kit's only timestamp form), else None."""
    try:
        if not isinstance(s, str) or len(s) != 20 or not s.endswith("Z"):
            return None
        return dt.datetime.strptime(s, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError:
        return None


def find_workspace(start: str | os.PathLike | None = None) -> Path | None:
    base = start or os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    d = Path(base).resolve()
    for cand in [d, *d.parents]:
        if (cand / KIT_JSON_REL).is_file():
            return cand
    return None


def is_inside(child: Path, parent: Path) -> bool:
    try:
        Path(child).resolve().relative_to(Path(parent).resolve())
        return True
    except ValueError:
        return False


def rel_path(p: str | os.PathLike, ws: Path | None = None) -> str:
    ap = Path(p).resolve()
    for base in [b for b in (ws, Path.cwd()) if b is not None]:
        if is_inside(ap, base):
            return ap.relative_to(Path(base).resolve()).as_posix() or "."
    return ap.as_posix()


def expand_inputs(patterns: list[str], exts: set[str] = IMAGE_EXTS) -> tuple[list[Path], list[str]]:
    """Files from paths, folders (their images, not recursive) and globs (`**` allowed), sorted by name within each
    pattern and de-duplicated. Returns (files, patterns that matched nothing)."""
    files, missing, seen = [], [], set()
    for pat in patterns:
        p = Path(pat)
        if any(ch in pat for ch in "*?["):
            hits = [Path(h) for h in glob.glob(pat, recursive=True)]
            hits = [h for h in hits if h.is_file() and h.suffix.lower() in exts]
        elif p.is_dir():
            hits = [h for h in p.iterdir() if h.is_file() and h.suffix.lower() in exts]
        elif p.is_file():
            hits = [p]
        else:
            hits = []
        if not hits:
            missing.append(pat)
        for h in sorted(hits, key=lambda x: x.name.lower()):
            k = str(h.resolve()).lower()
            if k not in seen:
                seen.add(k)
                files.append(h)
    return files, missing


def write_json_atomic(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    tmp.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    os.replace(tmp, path)


def save_image_atomic(img, path: Path, **kw) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.stem}.{os.getpid()}.tmp{path.suffix}")
    img.save(tmp, **kw)
    os.replace(tmp, path)


def emit(result: dict, as_json: bool, text: list[str] | None = None) -> None:
    if as_json:
        sys.stdout.write(json.dumps(result, ensure_ascii=False) + "\n")
        return
    lines = text if text is not None else [f"{k}: {v}" for k, v in result.items()]
    sys.stdout.write("\n".join(lines) + "\n")


def note(*parts) -> None:
    sys.stderr.write(" ".join(str(p) for p in parts) + "\n")


def run_main(main, argv: list[str] | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    as_json = "--json" in argv
    for stream in (sys.stdout, sys.stderr):  # JSON lines are UTF-8 whatever the console code page is
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    try:
        return int(main(argv) or 0)
    except UsageError as e:
        code, msg, fields = EXIT_USAGE, str(e), {}
    except ToolError as e:
        code, msg, fields = e.exit_code, str(e), e.fields
    except SystemExit as e:
        if e.code in (0, None):
            return 0
        code, msg, fields = EXIT_USAGE, "bad arguments (see --help)", {}
    except KeyboardInterrupt:
        code, msg, fields = EXIT_FAIL, "interrupted", {}
    except Exception as e:  # noqa: BLE001 - still one --json line and a kit exit code
        code, msg, fields = EXIT_FAIL, f"{type(e).__name__}: {e}", {}
    if as_json:
        sys.stdout.write(json.dumps({"ok": False, "exit": code, "error": msg, **fields}) + "\n")
    sys.stderr.write(f"{'usage' if code == EXIT_USAGE else 'error'}: {msg}\n")
    return code


def load_font(size: int):
    from PIL import ImageFont  # noqa: PLC0415

    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # Pillow < 10.1
        return ImageFont.load_default()


def dhash(img, size: int = 8) -> int:
    """64-bit difference hash (grayscale, (size+1)×size): near-identical frames differ by a few bits."""
    from PIL import Image  # noqa: PLC0415

    g = img.convert("L").resize((size + 1, size), Image.Resampling.LANCZOS)
    px = list(g.getdata())
    bits = 0
    for row in range(size):
        for col in range(size):
            left, right = px[row * (size + 1) + col], px[row * (size + 1) + col + 1]
            bits = (bits << 1) | (1 if left > right else 0)
    return bits


def hamming(a: int, b: int) -> int:
    return bin(a ^ b).count("1")
