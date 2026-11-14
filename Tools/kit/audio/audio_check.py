"""audio_check.py: loudness, true-peak, clipping, sample-rate and loop-seam checks for game audio, without ffmpeg.

    uv run --project Tools/kit/audio Tools/kit/audio/audio_check.py <file|dir>... [--class music|sfx|ui|vo|ambience]
        [--loop] [--targets targets.json] [--fix <dir> [--trim] [--xfade-ms 50] [--overwrite]] [--json]

Result (--json): {ok, files:[{path, class, lufs_i, true_peak_dbtp, sample_rate, channels, duration_s, clipping,
loop_seam_click, verdict, failures[], warnings[], ...}], fixed?:[...]}; exit 1 on any FAIL.

Targets [P, ARCHITECTURE §3.10 loudness.md]: music -16 ±1 LUFS, SFX and UI -20..-14, true peak <= -1 dBTP, 48 kHz.
VO -20..-16 and ambience -26..-18 are kit proposals; override any of them with --targets.
Integrated loudness is ITU-R BS.1770-4 via pyloudnorm; files under 400 ms use one block the length of the file.
--fix writes corrected copies (48 kHz, gain to the class centre, true-peak safe, optional loop crossfade and leading
silence trim) into another folder and re-checks them; it cannot repair clipping. It never overwrites an input, and
refuses (exit 2, `existing` lists them) to replace a file in that folder that an earlier --fix run did not write
(recorded in <dir>/.audio_check_fix.json) unless --overwrite is given. Re-running the same --fix is safe: the fix folder
is skipped when walking input folders. The class comes from the folders under the workspace (or the input folder),
nearest first; see infer_class.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audio_common as ac  # noqa: E402

AUDIO_EXTS = {".wav", ".flac", ".ogg", ".oga", ".aif", ".aiff", ".mp3"}
SKIP_DIRS = {".git", ".venv", "node_modules", "__pycache__", "Library", "Temp"}
CLASSES = ("music", "sfx", "ui", "vo", "ambience")
DEFAULT_TARGETS = {
    "music": {"lufs": [-17.0, -15.0], "tp_max": -1.0, "centre": -16.0},
    "sfx": {"lufs": [-20.0, -14.0], "tp_max": -1.0, "centre": -17.0},
    "ui": {"lufs": [-20.0, -14.0], "tp_max": -1.0, "centre": -17.0},
    "vo": {"lufs": [-20.0, -16.0], "tp_max": -1.0, "centre": -18.0},
    "ambience": {"lufs": [-26.0, -18.0], "tp_max": -1.0, "centre": -22.0},
}
PATH_CLASS_HINTS = [  # folder names that name a class outright
    ("music", {"music", "bgm", "mus", "soundtrack"}),
    ("ui", {"ui", "gui", "interface"}),
    ("vo", {"vo", "voice", "voices", "dialogue", "dialog", "barks"}),
    ("ambience", {"amb", "ambience", "ambiences", "ambient", "ambiance"}),
    ("sfx", {"sfx"}),
]
WEAK_CLASS_HINTS = [  # generic folder names, used only when no folder names a class outright (Music/Menu is music)
    ("ui", {"menu", "menus"}),
    ("sfx", {"fx", "sounds", "sound"}),
]
FIX_MANIFEST = ".audio_check_fix.json"  # in the --fix folder: the outputs this tool wrote, with their sha256
SILENCE_DB = -60.0
CLIP_LEVEL = 0.9999
CLIP_MIN_RUN = 3
SEAM_JUMP_FACTOR = 4.0
SEAM_KINK_FACTOR = 8.0
SEAM_ABS_FLOOR = 0.01
SEAM_RMS_WINDOW_S = 0.05
SEAM_RMS_WARN_DB = 6.0


def class_anchor(f: Path, root: Path | None, ws: Path | None) -> Path:
    """The folder whose sub-path names the class: the workspace when the file is inside it, else the parent of the
    input folder (so the folder passed on the command line counts), else the file's grandparent (its own folder
    counts). Folders above that, such as C:/Users/me/Music, never pick the class."""
    if ws is not None and ac.is_inside(f, ws):
        return ws.resolve()
    if root is not None:
        return root.resolve().parent
    return f.resolve().parent.parent


def infer_class(path: Path, anchor: Path | None = None) -> tuple[str, str]:
    """Class from the folders between `anchor` and the file, nearest folder first: a folder that names a class
    (Music, UI, VO, Ambience, SFX, ...) wins over a generic one (Menu, Sounds, FX), then a file-name prefix
    (music_, amb_, ...), else sfx."""
    p = path.resolve()
    try:
        rel = p.relative_to(anchor.resolve() if anchor is not None else p.parent.parent)
        parts = [x.lower() for x in rel.parts[:-1]]
    except ValueError:
        parts = [p.parent.name.lower()]
    for table in (PATH_CLASS_HINTS, WEAK_CLASS_HINTS):
        for folder in reversed(parts):
            for cls, names in table:
                if folder in names:
                    return cls, "path"
    stem = path.stem.lower()
    for cls, names in PATH_CLASS_HINTS + WEAK_CLASS_HINTS:
        if any(stem.startswith(n + "_") for n in names):
            return cls, "path"
    return "sfx", "default"


def collect(paths: list[str], exclude: Path | None = None) -> list[tuple[Path, Path | None]]:
    """[(file, root dir it came from or None)], sorted, unique. Folder walks skip SKIP_DIRS and `exclude` (the --fix
    folder, so a second --fix run never takes its own earlier outputs as inputs)."""
    out, seen = [], set()
    for raw in paths:
        p = Path(raw)
        if not p.exists():
            raise ac.UsageError(f"no such file or folder: {raw}")
        if p.is_file():
            items = [(p, None)]
        else:
            items = []
            # a folder that is itself inside the --fix folder was asked for explicitly: keep it (fix_outputs refuses
            # the in-place overwrite)
            skip = exclude if exclude is not None and not ac.is_inside(p, exclude) else None
            for f in sorted(p.rglob("*")):
                if f.is_file() and f.suffix.lower() in AUDIO_EXTS and not (set(f.relative_to(p).parts[:-1]) & SKIP_DIRS) \
                        and not (skip is not None and ac.is_inside(f, skip)):
                    items.append((f, p))
        for f, root in items:
            key = str(f.resolve()).lower()
            if key not in seen:
                seen.add(key)
                out.append((f, root))
    return out


def clip_stats(x2: np.ndarray) -> tuple[bool, int, int]:
    """(clipping, clipped_samples, longest_run): runs of >= 3 samples at full scale, or any sample above 1.0."""
    a = np.abs(x2)
    over = int(np.count_nonzero(a > 1.0 + 1e-6))
    total, longest = over, 0
    for ch in range(x2.shape[1]):
        m = np.concatenate([[0], (a[:, ch] >= CLIP_LEVEL).astype(np.int8), [0]])
        d = np.diff(m)
        lens = np.flatnonzero(d == -1) - np.flatnonzero(d == 1)
        runs = lens[lens >= CLIP_MIN_RUN]
        total += int(runs.sum())
        if lens.size:
            longest = max(longest, int(lens.max()))
    return (over > 0 or total > over), total, longest


def silence_edges(x2: np.ndarray, sr: int) -> tuple[float, float]:
    thr = 10.0 ** (SILENCE_DB / 20.0)
    loud = np.flatnonzero(np.max(np.abs(x2), axis=1) > thr)
    if loud.size == 0:
        dur = x2.shape[0] / sr * 1000.0
        return dur, dur
    return loud[0] / sr * 1000.0, (x2.shape[0] - 1 - loud[-1]) / sr * 1000.0


def loop_seam(x2: np.ndarray, sr: int) -> dict:
    """Discontinuity where the loop wraps (last sample → first sample), judged against the file's own sample-to-sample
    steps: a value jump above 4x the 99.9th-percentile step (and above 0.01 FS) or a slope kink above 8x the
    99.9th-percentile second difference is a click. Also reports the RMS step across the seam (warning only)."""
    n = x2.shape[0]
    if n < 8:
        return {"click": False, "reason": "too_short"}
    d1 = np.abs(np.diff(x2, axis=0))
    d2 = np.abs(np.diff(x2, n=2, axis=0))
    ref1 = np.maximum(np.percentile(d1, 99.9, axis=0), 1e-6)
    ref2 = np.maximum(np.percentile(d2, 99.9, axis=0), 1e-7)
    jump = np.abs(x2[0] - x2[-1])
    # slope continuity: predict each of the two samples after the wrap from the two before it
    kink = np.maximum(np.abs(x2[0] - (2.0 * x2[-1] - x2[-2])), np.abs(x2[1] - (2.0 * x2[0] - x2[-1])))
    jump_click = (jump > SEAM_JUMP_FACTOR * ref1) & (jump > SEAM_ABS_FLOOR)
    kink_click = (kink > SEAM_KINK_FACTOR * ref2) & (kink > SEAM_ABS_FLOOR)
    w = max(1, min(n // 4, int(SEAM_RMS_WINDOW_S * sr)))
    head = float(np.sqrt(np.mean(x2[:w] ** 2)))
    tail = float(np.sqrt(np.mean(x2[-w:] ** 2)))
    rms_delta = None
    if max(head, tail) > 10 ** (-50 / 20):
        rms_delta = abs(20 * np.log10(max(head, 1e-9) / max(tail, 1e-9)))
    return {
        "click": bool(np.any(jump_click | kink_click)),
        "jump": round(float(np.max(jump)), 6),
        "jump_ratio": round(float(np.max(jump / ref1)), 3),
        "kink": round(float(np.max(kink)), 6),
        "kink_ratio": round(float(np.max(kink / ref2)), 3),
        "rms_delta_db": None if rms_delta is None else round(float(rms_delta), 2),
    }


def analyse(x2: np.ndarray, sr: int, cls: str, targets: dict, loop: bool) -> dict:
    """Metrics, failures and warnings for one decoded signal (shape (n, ch), float)."""
    t = targets[cls]
    failures, warnings = [], []
    n, ch = x2.shape
    duration = n / sr if sr else 0.0
    rec = {"sample_rate": int(sr), "channels": int(ch), "duration_s": round(duration, 4)}
    if sr != ac.TARGET_SR:
        failures.append(f"sample_rate: {sr} Hz, expected {ac.TARGET_SR}")
    if ch > 2:
        failures.append(f"channels: {ch}, game assets are mono or stereo")
    elif ch == 2 and cls in ("sfx", "ui", "vo"):
        warnings.append("channels: stereo " + cls + " (3D-positioned sounds need mono)")
    try:
        lufs, method = ac.integrated_lufs(x2, sr)
    except ac.ToolError as e:
        lufs, method = None, "error"
        failures.append(f"lufs: {e}")
    rec["lufs_i"] = None if lufs is None else round(lufs, 2)
    rec["lufs_method"] = method
    tp = ac.true_peak(x2, sr)
    rec["true_peak_dbtp"] = None if tp <= 0 else round(20 * np.log10(tp), 2)
    sp = float(np.max(np.abs(x2))) if n else 0.0
    rec["sample_peak_dbfs"] = None if sp <= 0 else round(20 * np.log10(sp), 2)
    lo, hi = t["lufs"]
    if lufs is None and method != "error":
        failures.append("silent: nothing above the -70 LUFS gate")
    elif lufs is not None and not (lo <= lufs <= hi):
        failures.append(f"lufs_i: {lufs:.1f} LUFS outside [{lo:g}, {hi:g}] for {cls}")
    if rec["true_peak_dbtp"] is not None and rec["true_peak_dbtp"] > t["tp_max"]:
        failures.append(f"true_peak: {rec['true_peak_dbtp']:.2f} dBTP above {t['tp_max']:g}")
    clipping, clipped, longest = clip_stats(x2)
    rec.update({"clipping": clipping, "clipped_samples": clipped, "longest_full_scale_run": longest})
    if clipping:
        failures.append(f"clipping: {clipped} samples in full-scale runs (longest {longest})")
    lead, tail = silence_edges(x2, sr)
    rec["lead_silence_ms"], rec["tail_silence_ms"] = round(lead, 1), round(tail, 1)
    if cls in ("sfx", "ui") and lead > 20 and lufs is not None:
        warnings.append(f"lead_silence: {lead:.0f} ms before the sound starts (feels laggy on a trigger)")
    if tail > 500 and lufs is not None and not loop:
        warnings.append(f"tail_silence: {tail:.0f} ms of trailing silence")
    if loop:
        seam = loop_seam(x2, sr)
        rec["loop_seam"] = seam
        rec["loop_seam_click"] = seam["click"]
        if seam["click"]:
            failures.append(f"loop_seam_click: jump {seam['jump']} ({seam['jump_ratio']}x typical step), "
                            f"kink ratio {seam['kink_ratio']}")
        if seam.get("rms_delta_db") is not None and seam["rms_delta_db"] > SEAM_RMS_WARN_DB:
            warnings.append(f"loop_seam_level: {seam['rms_delta_db']} dB level step across the loop point")
    else:
        rec["loop_seam"] = None
        rec["loop_seam_click"] = None
    rec["verdict"] = "FAIL" if failures else "PASS"
    rec["failures"], rec["warnings"] = failures, warnings
    return rec


def read_audio(path: Path) -> tuple[np.ndarray, int, str]:
    import soundfile as sf  # noqa: PLC0415

    info = sf.info(str(path))
    x, sr = sf.read(str(path), dtype="float64", always_2d=True)
    return x, int(sr), str(info.subtype)


def check_file(path: Path, cls: str, cls_src: str, targets: dict, loop: bool, ws: Path | None) -> tuple[dict, tuple | None]:
    base = {"path": ac.rel_path(path, ws), "class": cls, "class_source": cls_src}
    try:
        x2, sr, subtype = read_audio(path)
    except Exception as e:  # noqa: BLE001 - any decode error is a FAIL for this file
        return {**base, "lufs_i": None, "true_peak_dbtp": None, "sample_rate": None, "channels": None,
                "duration_s": None, "clipping": None, "loop_seam_click": None, "verdict": "FAIL",
                "failures": [f"unreadable: {type(e).__name__}: {e}"], "warnings": []}, None
    rec = {**base, "subtype": subtype, **analyse(x2, sr, cls, targets, loop)}
    return rec, (x2, sr, subtype)


def crossfade_loop(y: np.ndarray, sr: int, xfade_ms: float) -> np.ndarray:
    """Equal-power crossfade of the head into the tail: output length n - k, seamless at the wrap."""
    y2 = ac.as_2d(y)
    n = y2.shape[0]
    k = int(round(xfade_ms / 1000.0 * sr))
    k = max(0, min(k, n // 3))
    if k < 2:
        return y2
    t = (np.arange(k) + 0.5) / k
    fin, fout = np.sin(t * np.pi / 2)[:, None], np.cos(t * np.pi / 2)[:, None]
    out = y2[k:].copy()
    out[-k:] = y2[n - k:] * fout + y2[:k] * fin
    return out


def trim_lead(y2: np.ndarray, sr: int) -> np.ndarray:
    thr = 10.0 ** (SILENCE_DB / 20.0)
    loud = np.flatnonzero(np.max(np.abs(y2), axis=1) > thr)
    if loud.size == 0:
        return y2
    start = max(0, int(loud[0]) - int(0.001 * sr))
    return y2[start:]


def fix_one(src: Path, decoded: tuple, cls: str, targets: dict, loop: bool, out_path: Path, xfade_ms: float,
            trim: bool, source_rec: dict, ws: Path | None) -> dict:
    import soundfile as sf  # noqa: PLC0415

    x2, sr, subtype = decoded
    y = ac.as_2d(ac.resample_to(x2, sr, ac.TARGET_SR))
    if trim:
        y = trim_lead(y, ac.TARGET_SR)
    if loop:
        y = crossfade_loop(y, ac.TARGET_SR, xfade_ms)
    t = targets[cls]
    y, info = ac.normalise_gain(y, ac.TARGET_SR, t.get("centre", sum(t["lufs"]) / 2), t["tp_max"] - 0.2)
    out_sub = "PCM_24" if subtype in ("PCM_24", "PCM_32", "FLOAT", "DOUBLE") else "PCM_16"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    y = np.clip(y, -1.0, 1.0)
    tmp = out_path.with_name(f".{out_path.stem}.{os.getpid()}.tmp.wav")
    try:
        sf.write(str(tmp), y if y.shape[1] > 1 else y[:, 0], ac.TARGET_SR, subtype=out_sub, format="WAV")
        os.replace(tmp, out_path)
    finally:
        tmp.unlink(missing_ok=True)
    rec, _ = check_file(out_path, cls, "flag", targets, loop, ws)
    rec.update({"source": source_rec["path"], "gain_db": info["gain_db"], "limited": info["limited"],
                "resampled_from": sr if sr != ac.TARGET_SR else None, "trimmed": trim, "crossfaded": loop})
    if source_rec.get("clipping"):
        rec["failures"].append("clipping_source: the source clips; gain cannot repair it (re-export or re-source)")
        rec["verdict"] = "FAIL"
    if info["limited"]:
        rec["warnings"].append("limited: gain lowered to keep the true peak under the ceiling; loudness may be low")
    return rec


def fix_key(out: Path, fix_dir: Path) -> str:
    return out.resolve().relative_to(fix_dir.resolve()).as_posix().lower()


def load_fix_manifest(fix_dir: Path) -> dict[str, str]:
    """{output path relative to fix_dir (lower case): sha256} of the files earlier --fix runs wrote there."""
    try:
        m = json.loads((fix_dir / FIX_MANIFEST).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    files = m.get("files") if isinstance(m, dict) else None
    return {str(k).lower(): str(v) for k, v in files.items()} if isinstance(files, dict) else {}


def save_fix_manifest(fix_dir: Path, owned: dict[str, str]) -> None:
    ac.write_json_atomic(fix_dir / FIX_MANIFEST, {"schema": 1, "tool": "audio_check.py --fix", "updated_utc": ac.utc_iso(),
                                                  "files": dict(sorted(owned.items()))})


def fix_outputs(files: list[tuple[Path, Path | None]], fix_dir: Path, ws: Path | None, overwrite: bool = False) -> list[Path]:
    """One .wav per input under fix_dir (folder inputs keep their sub-paths). Refuses before writing anything when an
    output would land on an input, or on an existing file that an earlier --fix run did not write (same sha256 as
    recorded in FIX_MANIFEST) unless `overwrite`: a Unity asset replaced in place keeps its .meta GUID, so every
    reference would silently play different audio."""
    in_set = {str(f.resolve()).lower() for f, _ in files}
    owned = load_fix_manifest(fix_dir)
    used, outs, clobber = set(), [], []
    for f, root in files:
        rel = f.relative_to(root) if root is not None else Path(f.name)
        out = (fix_dir / rel).with_suffix(".wav")
        i = 2
        while str(out).lower() in used:
            out = out.with_name(f"{(fix_dir / rel).stem}_{i}.wav")
            i += 1
        used.add(str(out).lower())
        if str(out.resolve()).lower() in in_set:
            raise ac.UsageError(f"--fix would overwrite an input ({ac.rel_path(out, ws)}); pick another folder")
        if out.exists() and not overwrite:
            mine = out.is_file() and owned.get(fix_key(out, fix_dir)) == ac.sha256_file(out)
            if not mine:
                clobber.append(ac.rel_path(out, ws))
        outs.append(out)
    if clobber:
        shown = ", ".join(clobber[:5]) + (f" and {len(clobber) - 5} more" if len(clobber) > 5 else "")
        raise ac.ToolError(f"--fix would replace {len(clobber)} existing file(s) it did not write ({shown}); pick an "
                           "empty folder or pass --overwrite", ac.EXIT_USAGE, existing=clobber)
    return outs


def load_targets(path: str | None) -> dict:
    targets = json.loads(json.dumps(DEFAULT_TARGETS))
    if not path:
        return targets
    try:
        extra = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as e:
        raise ac.UsageError(f"--targets: {e}") from e
    for cls, t in extra.items():
        if cls not in CLASSES or not isinstance(t, dict):
            raise ac.UsageError(f"--targets: unknown class {cls!r}")
        merged = {**targets[cls], **t}
        lo_hi = merged.get("lufs")
        if not (isinstance(lo_hi, list) and len(lo_hi) == 2 and lo_hi[0] <= lo_hi[1]):
            raise ac.UsageError(f"--targets: {cls}.lufs must be [low, high]")
        if "lufs" in t and "centre" not in t:  # --fix aims at the middle of the overridden window
            merged["centre"] = (float(lo_hi[0]) + float(lo_hi[1])) / 2.0
        targets[cls] = merged
    return targets


def parse_args(argv: list[str]) -> argparse.Namespace:
    ap = argparse.ArgumentParser(prog="audio_check.py", description=__doc__.split("\n\n")[0])
    ap.add_argument("paths", nargs="+", help="audio files or folders (searched recursively)")
    ap.add_argument("--class", dest="cls", choices=CLASSES, help="asset class; default: inferred from the path, else sfx")
    ap.add_argument("--loop", action="store_true", help="also check the loop seam (the file is played looping)")
    ap.add_argument("--targets", help="JSON overriding the per-class targets, e.g. {\"vo\": {\"lufs\": [-24, -20]}}")
    ap.add_argument("--fix", metavar="DIR", help="write corrected copies into DIR and check them")
    ap.add_argument("--trim", action="store_true", help="with --fix: trim leading silence")
    ap.add_argument("--xfade-ms", type=float, default=50.0, help="with --fix --loop: crossfade length (default 50)")
    ap.add_argument("--overwrite", action="store_true",
                    help="with --fix: replace existing files in DIR that an earlier --fix run did not write")
    ap.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    targets = load_targets(args.targets)
    ws = ac.find_workspace()
    fix_dir = Path(args.fix).resolve() if args.fix else None
    files = collect(args.paths, exclude=fix_dir)
    if not files:
        raise ac.ToolError("no audio files found (" + ", ".join(args.paths) + ")", ac.EXIT_FAIL, files=[])
    outs = fix_outputs(files, fix_dir, ws, args.overwrite) if fix_dir is not None else [None] * len(files)
    owned = load_fix_manifest(fix_dir) if fix_dir is not None else {}
    results, fixed = [], []
    for (f, root), out in zip(files, outs):
        cls, src = (args.cls, "flag") if args.cls else infer_class(f, class_anchor(f, root, ws))
        rec, decoded = check_file(f, cls, src, targets, args.loop, ws)
        results.append(rec)
        ac.note(f"{rec['verdict']} {rec['path']} " + "; ".join(rec["failures"]))
        if out is not None and decoded is not None:
            fixed.append(fix_one(f, decoded, cls, targets, args.loop, out, args.xfade_ms, args.trim, rec, ws))
            owned[fix_key(out, fix_dir)] = ac.sha256_file(out)
            save_fix_manifest(fix_dir, owned)
    judged = fixed if fix_dir is not None else results
    ok = all(r["verdict"] == "PASS" for r in judged) and (fix_dir is None or len(fixed) == len(results))
    result = {"ok": ok, "files": results}
    if fix_dir is not None:
        result["fixed"] = fixed
        result["fix_dir"] = ac.rel_path(fix_dir, ws)
    text = [f"{r['verdict']}  {r['path']}  lufs={r.get('lufs_i')} tp={r.get('true_peak_dbtp')} sr={r.get('sample_rate')}"
            + ("" if not r["failures"] else "  <- " + "; ".join(r["failures"])) for r in results]
    if fix_dir is not None:
        text += [f"fixed {r['verdict']}  {r['path']}  gain={r['gain_db']} dB lufs={r.get('lufs_i')}" for r in fixed]
    text.append("ok" if ok else "FAIL")
    ac.emit(result, args.json, text)
    return ac.EXIT_OK if ok else ac.EXIT_FAIL


if __name__ == "__main__":
    sys.exit(ac.run_main(main))
