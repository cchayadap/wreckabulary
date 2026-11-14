"""Shared helpers for the kit's audio tools (installed to Tools/kit/audio/, a uv project).

Conventions (CONTRACTS.md §1): exit codes 0 OK, 1 FAIL, 2 USAGE, 3 INFRA, 75 BLOCKED; with --json stdout holds exactly
one JSON line; paths printed are POSIX and workspace-relative; timestamps are UTC `YYYY-MM-DDTHH:MM:SSZ` and honour
GSK_NOW; keys come from Windows Credential Manager (Python keyring, service `game-studio-kit`) first, then the
environment, and are never printed.
"""
from __future__ import annotations

import datetime as dt
import functools
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np

EXIT_OK, EXIT_FAIL, EXIT_USAGE, EXIT_INFRA, EXIT_BLOCKED = 0, 1, 2, 3, 75
TARGET_SR = 48000
KEYRING_SERVICE = "game-studio-kit"
KIT_JSON_REL = Path("Tools") / "kit" / "kit.json"
LICENCE_COLUMNS = [
    "path", "sha256", "source_kind", "source_url", "licence_id", "licence_url", "author", "attribution_text",
    "retrieved_at_utc", "account_or_plan", "provider", "generator_model", "job_id", "ai_generated", "commercial_ok",
    "training_ban", "redistribution", "ships", "notes",
]


class UsageError(Exception):
    """Bad arguments (exit 2)."""


class ToolError(Exception):
    """A failure with an exit code and extra fields for the --json line."""

    def __init__(self, message: str, exit_code: int = EXIT_FAIL, **fields):
        super().__init__(message)
        self.exit_code = exit_code
        self.fields = fields


# ---------------------------------------------------------------- time

def now_utc() -> dt.datetime:
    """Current UTC time, or GSK_NOW (ISO) when set (tests pin it)."""
    pinned = os.environ.get("GSK_NOW")
    if pinned:
        return dt.datetime.fromisoformat(pinned.replace("Z", "+00:00")).astimezone(dt.timezone.utc)
    return dt.datetime.now(dt.timezone.utc)


def utc_iso(d: dt.datetime | None = None) -> str:
    return (d or now_utc()).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def utc_file_stamp(d: dt.datetime | None = None) -> str:
    return (d or now_utc()).astimezone(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")


# ---------------------------------------------------------------- paths

def find_workspace(start: str | os.PathLike | None = None) -> Path | None:
    """First ancestor (inclusive) holding Tools/kit/kit.json: from `start`, else CLAUDE_PROJECT_DIR, else cwd."""
    base = start or os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    d = Path(base).resolve()
    for cand in [d, *d.parents]:
        if (cand / KIT_JSON_REL).is_file():
            return cand
    return None


def is_inside(child: Path, parent: Path) -> bool:
    try:
        child.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def rel_path(p: str | os.PathLike, ws: Path | None = None) -> str:
    """POSIX path relative to the workspace (or the cwd when outside it); absolute POSIX as a last resort."""
    ap = Path(p).resolve()
    for base in [b for b in (ws, Path.cwd()) if b is not None]:
        if is_inside(ap, base):
            return ap.relative_to(Path(base).resolve()).as_posix() or "."
    return ap.as_posix()


def sha256_file(p: str | os.PathLike) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def write_bytes_atomic(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    tmp.write_bytes(data)
    os.replace(tmp, path)


def write_json_atomic(path: Path, obj) -> None:
    write_bytes_atomic(path, (json.dumps(obj, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))


# ---------------------------------------------------------------- output

def emit(result: dict, as_json: bool, text: list[str] | str | None = None) -> None:
    if as_json:
        sys.stdout.write(json.dumps(result, ensure_ascii=False, allow_nan=False, default=_json_default) + "\n")
        return
    lines = text if text is not None else [f"{k}: {v}" for k, v in result.items()]
    sys.stdout.write(("\n".join(lines) if isinstance(lines, list) else lines) + "\n")


def _json_default(o):
    if isinstance(o, (np.floating,)):
        return float(o)
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, (np.bool_,)):
        return bool(o)
    if isinstance(o, Path):
        return o.as_posix()
    raise TypeError(f"not JSON serialisable: {type(o).__name__}")


def note(*parts) -> None:
    sys.stderr.write(" ".join(str(p) for p in parts) + "\n")


def run_main(main, argv: list[str] | None = None) -> int:
    """Run main(argv) with the kit's error mapping; prints {ok:false, exit, error} on --json failures."""
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
    except SystemExit as e:  # argparse
        if e.code in (0, None):
            return 0
        code, msg, fields = EXIT_USAGE, "bad arguments (see --help)", {}
    except KeyboardInterrupt:
        code, msg, fields = EXIT_FAIL, "interrupted", {}
    except Exception as e:  # noqa: BLE001 - an unexpected error is still a FAIL with a --json line, never a bare traceback
        code, msg, fields = EXIT_FAIL, f"{type(e).__name__}: {e}", {}
    if as_json:
        sys.stdout.write(json.dumps({"ok": False, "exit": code, "error": msg, **fields}, default=_json_default) + "\n")
    sys.stderr.write(f"{'usage' if code == EXIT_USAGE else 'error'}: {msg}\n")
    return code


# ---------------------------------------------------------------- secrets

def get_secret(name: str) -> tuple[str | None, str]:
    """(value, storage) with storage keyring|env|missing. The value is never printed or logged by kit tools.
    GSK_KEYRING=off skips Credential Manager (tests)."""
    if os.environ.get("GSK_KEYRING", "").lower() not in ("off", "0", "false"):
        try:
            import keyring  # noqa: PLC0415

            v = keyring.get_password(KEYRING_SERVICE, name)
            if v:
                return v, "keyring"
        except Exception:  # noqa: BLE001 - no backend, locked store, …: fall through to env
            pass
    v = os.environ.get(name)
    if v:
        return v, "env"
    return None, "missing"


# ---------------------------------------------------------------- licence rows

def blank_licence_row() -> dict:
    return {c: "" for c in LICENCE_COLUMNS}


_FLAG_MAP = [
    ("path", "--path"), ("source_kind", "--source-kind"), ("licence_id", "--licence-id"),
    ("source_url", "--source-url"), ("licence_url", "--licence-url"), ("author", "--author"),
    ("attribution_text", "--attribution"), ("retrieved_at_utc", "--retrieved-at"),
    ("account_or_plan", "--account-or-plan"), ("provider", "--provider"),
    ("generator_model", "--generator-model"), ("job_id", "--job-id"), ("commercial_ok", "--commercial-ok"),
    ("training_ban", "--training-ban"), ("redistribution", "--redistribution"), ("notes", "--notes"),
]


def licence_row_argv(row: dict, replace: bool = True) -> list[str]:
    """`license_row.mjs add` arguments for a row (CONTRACTS §4.9). Booleans map to bare flags."""
    argv = ["add"]
    for key, flag in _FLAG_MAP:
        v = row.get(key, "")
        if v not in ("", None):
            argv += [flag, str(v)]
    if str(row.get("ai_generated", "")).lower() == "true":
        argv.append("--ai-generated")
    if str(row.get("ships", "")).lower() == "true":
        argv.append("--ships")
    if replace:
        argv.append("--replace")
    argv.append("--json")
    return argv


def write_licence_row(file_path: Path, row: dict, ws: Path | None, enabled: bool = True) -> dict:
    """Append/replace the licence row for `file_path` through Tools/kit/ship/license_row.mjs (the only writer of
    Docs/licenses.csv). When the writer or node is missing, save the full row as a pending JSON file instead, so
    `node Tools/kit/ship/license_row.mjs add --from-json <file>` can apply it later. A failing writer is reported in
    the returned status, never raised."""
    full = blank_licence_row()
    full.update({k: ("" if v is None else str(v).lower() if isinstance(v, bool) else str(v)) for k, v in row.items()})
    if not enabled:
        return {"status": "skipped", "reason": "--no-licence"}
    if ws is None or not is_inside(file_path, ws):
        pending = file_path.with_name(file_path.name + ".licence.json")
        full["path"] = rel_path(file_path, ws)
        write_json_atomic(pending, full)
        return {"status": "pending", "reason": "no kit workspace around the file", "pending": rel_path(pending, ws)}
    full["path"] = file_path.resolve().relative_to(ws.resolve()).as_posix() + ("/" if file_path.is_dir() else "")
    writer = ws / "Tools" / "kit" / "ship" / "license_row.mjs"
    node = shutil.which("node")
    if writer.is_file() and node:
        try:
            p = subprocess.run([node, str(writer), *licence_row_argv(full)], cwd=str(ws), capture_output=True,
                               text=True, encoding="utf-8", errors="replace", timeout=120, stdin=subprocess.DEVNULL)
            last = next((ln for ln in reversed(p.stdout.splitlines()) if ln.strip()), "")
            try:
                parsed = json.loads(last) if last else {}
            except json.JSONDecodeError:
                parsed = {}
            if p.returncode == 0:
                return {"status": "written", "path": full["path"]}
            reason = parsed.get("error") or p.stderr.strip()[-300:] or f"exit {p.returncode}"
            status_fail = {"status": "failed", "exit": p.returncode, "reason": reason, "path": full["path"]}
        except (OSError, subprocess.SubprocessError) as e:
            status_fail = {"status": "failed", "reason": str(e), "path": full["path"]}
    else:
        status_fail = None
    pending_dir = ws / "Docs" / "audio" / "licence_pending"
    safe = full["path"].rstrip("/").replace("/", "__")
    pending = pending_dir / f"{safe}.licence.json"
    write_json_atomic(pending, full)
    out = {"status": "pending", "pending": rel_path(pending, ws), "path": full["path"],
           "reason": "Tools/kit/ship/license_row.mjs or node not found" if status_fail is None else status_fail["reason"]}
    return out


# ---------------------------------------------------------------- signal maths

def as_2d(x: np.ndarray) -> np.ndarray:
    x = np.asarray(x, dtype=np.float64)
    return x.reshape(-1, 1) if x.ndim == 1 else x


def integrated_lufs(x: np.ndarray, sr: int) -> tuple[float | None, str]:
    """ITU-R BS.1770-4 integrated loudness (pyloudnorm). Files shorter than one 400 ms gating block are measured with
    a single block the length of the file (method "short_block"), so short SFX get a comparable number.
    Returns (lufs or None when silent / gated out, method)."""
    import pyloudnorm as pyln  # noqa: PLC0415

    x2 = as_2d(x)
    n = x2.shape[0]
    if n < 16:
        return None, "too_short"
    if x2.shape[1] > 5:
        raise ToolError(f"{x2.shape[1]} channels: BS.1770 metering supports at most 5", EXIT_FAIL)
    if n / sr >= 0.4:
        meter, method = pyln.Meter(sr), "bs1770"
    else:
        meter, method = pyln.Meter(sr, block_size=(n - 0.5) / sr), "short_block"
    data = x2[:, 0] if x2.shape[1] == 1 else x2
    val = float(meter.integrated_loudness(data))
    if not np.isfinite(val) or val < -70.0:
        return None, method
    return val, method


@functools.lru_cache(maxsize=8)
def _tp_filter(sr: int, factor: int) -> np.ndarray:
    """Interpolation low-pass for true-peak metering: flat (±0.01 dB) to min(20 kHz, 0.45·sr), 60 dB down where the
    first image starts, unity DC gain (resample_poly multiplies by `factor`). The default resample_poly filter
    ripples by about +0.4 dB at fs/4, which would over-read true peaks."""
    from scipy.signal import firwin, kaiserord  # noqa: PLC0415

    nyq = factor * sr / 2.0
    pass_edge = min(20000.0, 0.45 * sr)
    stop_edge = sr - pass_edge
    numtaps, beta = kaiserord(60.0, (stop_edge - pass_edge) / nyq)
    numtaps |= 1
    return firwin(numtaps, (sr / 2.0) / nyq, window=("kaiser", beta))


def true_peak(x: np.ndarray, sr: int) -> float:
    """Linear true peak per BS.1770-4 Annex 2: 4x oversampling below 96 kHz, 2x below 192 kHz (chunked, so long
    music files do not need gigabytes). Never below the sample peak."""
    from scipy.signal import resample_poly  # noqa: PLC0415

    x2 = as_2d(x)
    n = x2.shape[0]
    peak = float(np.max(np.abs(x2))) if n else 0.0
    factor = 4 if sr < 96000 else 2 if sr < 192000 else 1
    if factor == 1 or n == 0:
        return peak
    h = _tp_filter(int(sr), factor)
    chunk, pad = 1 << 18, len(h) // factor + 8
    # The filter rings where the file starts and ends (a step to silence or a held edge value); those few samples
    # are covered by the sample peak instead, so a loop or an abrupt end does not read as an intersample over.
    edge = len(h) // 2 + factor
    total = n * factor
    for s in range(0, n, chunk):
        a, e = max(0, s - pad), min(n, s + chunk + pad)
        y = resample_poly(x2[a:e], factor, 1, axis=0, window=h, padtype="edge")
        lo = (s - a) * factor
        hi = lo + (min(n, s + chunk) - s) * factor
        glo, ghi = s * factor, s * factor + (hi - lo)  # global upsampled indices of y[lo:hi]
        lo += max(0, edge - glo)
        hi -= max(0, ghi - (total - edge))
        if hi > lo:
            peak = max(peak, float(np.max(np.abs(y[lo:hi]))))
    return peak


def normalise_gain(x: np.ndarray, sr: int, target_lufs: float, tp_ceiling_db: float) -> tuple[np.ndarray, dict]:
    """Scale x to target_lufs, then lower the gain if the true peak would exceed tp_ceiling_db (no limiter, so the
    result can land below the target; `limited` says so)."""
    peak = float(np.max(np.abs(x))) if np.size(x) else 0.0
    if peak <= 0:
        return x, {"gain_db": 0.0, "limited": False, "lufs_before": None, "method": "silent"}
    pre_db = 20.0 * np.log10(0.5 / peak)  # bring very quiet renders above the -70 LUFS gate before measuring
    x = x * (0.5 / peak)
    lufs, method = integrated_lufs(x, sr)
    if lufs is None:
        return x, {"gain_db": round(float(pre_db), 3), "limited": False, "lufs_before": None, "method": method}
    gain_db = target_lufs - lufs
    y = x * (10.0 ** (gain_db / 20.0))
    lufs_before = lufs - pre_db
    gain_db += pre_db
    tp = true_peak(y, sr)
    limited = False
    ceiling = 10.0 ** (tp_ceiling_db / 20.0)
    if tp > ceiling:
        extra = 20.0 * np.log10(ceiling / tp)
        y = y * (10.0 ** (extra / 20.0))
        gain_db += extra
        limited = True
    return y, {"gain_db": round(float(gain_db), 3), "limited": limited, "lufs_before": round(float(lufs_before), 3),
               "method": method}


def resample_to(x: np.ndarray, sr: int, target: int = TARGET_SR) -> np.ndarray:
    if sr == target:
        return x
    from math import gcd  # noqa: PLC0415

    from scipy.signal import resample_poly  # noqa: PLC0415

    g = gcd(int(sr), int(target))
    return resample_poly(np.asarray(x, dtype=np.float64), target // g, int(sr) // g, axis=0)


def pcm16_wav_bytes(y: np.ndarray, sr: int) -> bytes:
    """Deterministic 16-bit PCM WAV bytes (stdlib wave: no timestamps or metadata chunks)."""
    import io  # noqa: PLC0415
    import wave  # noqa: PLC0415

    y2 = as_2d(y)
    q = np.clip(np.round(y2 * 32767.0), -32768, 32767).astype("<i2")
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(q.shape[1])
        w.setsampwidth(2)
        w.setframerate(int(sr))
        w.writeframes(q.tobytes())
    return buf.getvalue()
