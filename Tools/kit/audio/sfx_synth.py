"""sfx_synth.py: seeded procedural sound effects → deterministic 48 kHz / 16-bit mono WAV plus an Own-Work licence row.

    uv run --project Tools/kit/audio Tools/kit/audio/sfx_synth.py --recipe <name|file.json> --seed N --out <wav>
        [--set key=value]... [--class ui|sfx|ambience|music|vo] [--target-lufs L] [--author A] [--ships]
        [--no-licence] [--json]
    uv run --project Tools/kit/audio Tools/kit/audio/sfx_synth.py --list [--json]

The same recipe, parameters and seed give byte-identical output on the same numpy version (the RNG is numpy PCG64
seeded from [seed, layer]). Built-in recipes cover UI blips, confirm/error/pickup jingles, jumps, whooshes, impacts,
footsteps, frog croaks (formant-filtered pulse trains), Karplus-Strong plucks, FM bells and a seamless noise bed.
A recipe file layers built-ins:
    {"schema": 1, "name": "door_slam", "class": "sfx", "target_lufs": -16, "loop": false,
     "layers": [{"recipe": "impact", "params": {"f_start": 90}, "gain_db": 0, "offset_s": 0},
                {"recipe": "whoosh", "params": {"dur": 0.3}, "gain_db": -9, "offset_s": 0}]}
Output is normalised to the class centre (ui/sfx -17 LUFS, ambience -22) with the true peak at or below -1.5 dBTP, so
it passes audio_check.py. The licence row (source_kind procedural, licence Own-Work) goes through
Tools/kit/ship/license_row.mjs; without it the row is saved as a pending JSON file.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audio_common as ac  # noqa: E402

SR = ac.TARGET_SR
CLASS_CENTRE = {"ui": -17.0, "sfx": -17.0, "ambience": -22.0, "music": -16.0, "vo": -18.0}
TP_CEILING_DB = -1.5
MAX_DURATION_S = 30.0


# ---------------------------------------------------------------- building blocks

def _n(dur: float) -> int:
    return max(8, int(round(float(dur) * SR)))


def _t(n: int) -> np.ndarray:
    return np.arange(n) / SR


def env_ad(n: int, attack_s: float, decay_s: float) -> np.ndarray:
    """Linear attack, then exponential decay with time constant decay_s."""
    t = _t(n)
    a = np.clip(t / max(attack_s, 1e-4), 0.0, 1.0)
    return a * np.exp(-np.maximum(t - attack_s, 0.0) / max(decay_s, 1e-4))


def glide(f0: float, f1: float, n: int) -> np.ndarray:
    """Exponential pitch glide from f0 to f1 over n samples."""
    return f0 * (f1 / f0) ** (np.arange(n) / max(n - 1, 1))


def osc(freq: np.ndarray, shape: str = "sine", phase0: float = 0.0) -> np.ndarray:
    ph = phase0 + 2.0 * np.pi * np.cumsum(freq) / SR
    if shape == "sine":
        return np.sin(ph)
    if shape == "triangle":
        return (2.0 / np.pi) * np.arcsin(np.sin(ph))
    if shape == "square":  # soft square: fewer aliasing artefacts than a hard one
        return np.tanh(4.0 * np.sin(ph)) / np.tanh(4.0)
    if shape == "saw":  # band-limited-ish saw from 12 harmonics, rolled off above Nyquist
        out = np.zeros_like(ph)
        for k in range(1, 13):
            mask = (k * freq) < (SR / 2 - 500)
            out += mask * np.sin(k * ph) / k
        return out * (2.0 / np.pi)
    raise ac.UsageError(f"unknown oscillator shape {shape!r} (sine, triangle, square, saw)")


def _sos(kind: str, f, order: int = 2):
    from scipy.signal import butter  # noqa: PLC0415

    nyq = SR / 2.0
    if kind == "band":
        lo, hi = max(20.0, f[0]), min(nyq * 0.95, f[1])
        return butter(order, [lo / nyq, hi / nyq], btype="bandpass", output="sos")
    return butter(order, min(max(float(f), 20.0), nyq * 0.95) / nyq, btype=kind, output="sos")


def filt(x: np.ndarray, kind: str, f, order: int = 2) -> np.ndarray:
    from scipy.signal import sosfilt  # noqa: PLC0415

    return sosfilt(_sos(kind, f, order), x)


def swept_bandpass(x: np.ndarray, centres: np.ndarray, q: float, block: int = 256) -> np.ndarray:
    """Band-pass whose centre follows `centres` (one value per sample), updated per block with the filter state
    carried over (RBJ biquad)."""
    from scipy.signal import lfilter  # noqa: PLC0415

    y = np.zeros_like(x)
    zi = np.zeros(2)
    for s in range(0, len(x), block):
        fc = float(np.clip(centres[min(s + block // 2, len(x) - 1)], 30.0, SR * 0.45))
        w0 = 2 * np.pi * fc / SR
        alpha = np.sin(w0) / (2 * q)
        b = np.array([alpha, 0.0, -alpha])
        a = np.array([1 + alpha, -2 * np.cos(w0), 1 - alpha])
        y[s:s + block], zi = lfilter(b / a[0], a / a[0], x[s:s + block], zi=zi)
    return y


def fade_tail(y: np.ndarray, ms: float = 5.0) -> np.ndarray:
    k = min(len(y), max(2, int(ms / 1000 * SR)))
    y = y.copy()
    y[-k:] *= np.linspace(1.0, 0.0, k)
    return y


def jitter(rng: np.random.Generator, value: float, amount: float) -> float:
    return float(value) * (1.0 + amount * (2.0 * rng.random() - 1.0))


def notes_seq(rng, freqs, note_s, gap_s, shape, vary, decay_s) -> np.ndarray:
    parts = []
    for f in freqs:
        n = _n(note_s)
        f0 = jitter(rng, f, vary)
        parts.append(osc(np.full(n, f0), shape) * env_ad(n, 0.002, decay_s))
        parts.append(np.zeros(_n(gap_s)))
    return np.concatenate(parts)


# ---------------------------------------------------------------- recipes

def r_ui_blip(rng, p):
    n = _n(p["dur"])
    f0 = jitter(rng, p["freq"], p["vary"])
    return osc(glide(f0, f0 * p["glide"], n), p["shape"]) * env_ad(n, p["attack_ms"] / 1000, p["decay_ms"] / 1000)


def r_ui_confirm(rng, p):
    return notes_seq(rng, p["notes"], p["note_ms"] / 1000, p["gap_ms"] / 1000, p["shape"], p["vary"], p["decay_ms"] / 1000)


def r_ui_error(rng, p):
    return notes_seq(rng, p["notes"], p["note_ms"] / 1000, p["gap_ms"] / 1000, p["shape"], p["vary"], p["decay_ms"] / 1000)


def r_pickup(rng, p):
    return notes_seq(rng, p["notes"], p["note_ms"] / 1000, 0.0, p["shape"], p["vary"], p["decay_ms"] / 1000)


def r_jump(rng, p):
    n = _n(p["dur"])
    f0, f1 = jitter(rng, p["f_start"], p["vary"]), jitter(rng, p["f_end"], p["vary"])
    y = 0.7 * osc(glide(f0, f1, n), "sine") + 0.3 * osc(glide(f0, f1, n), "triangle")
    return y * env_ad(n, 0.004, p["dur"] / 2.5)


def r_whoosh(rng, p):
    n = _n(jitter(rng, p["dur"], p["vary"]))
    t = np.arange(n) / max(n - 1, 1)
    peak = min(max(p["peak_at"], 0.05), 0.95)
    rise = np.clip(t / peak, 0, 1)
    fall = np.clip((1 - t) / (1 - peak), 0, 1)
    shape = np.where(t < peak, rise, fall)
    centres = p["f_lo"] * (p["f_hi"] / p["f_lo"]) ** shape
    noise = rng.standard_normal(n)
    y = swept_bandpass(noise, centres, p["q"])
    return y * np.sin(np.pi * t) ** p["env_power"]


def r_impact(rng, p):
    n = _n(p["dur"])
    t = _t(n)
    f0 = jitter(rng, p["f_start"], p["vary"])
    thump_n = _n(p["drop_s"])
    fcurve = np.concatenate([glide(f0, p["f_end"], thump_n), np.full(max(0, n - thump_n), p["f_end"])])[:n]
    body = osc(fcurve, "sine") * env_ad(n, 0.001, p["body_decay_s"])
    noise = filt(rng.standard_normal(n), "lowpass", p["noise_lp"], 2) * np.exp(-t / p["noise_decay_s"])
    click = np.zeros(n)
    k = min(n, int(0.0015 * SR))
    click[:k] = rng.standard_normal(k) * np.linspace(1, 0, k)
    y = body + p["noise_gain"] * noise + p["click_gain"] * click
    return np.tanh(p["drive"] * y) / np.tanh(p["drive"])


def r_footstep(rng, p):
    n = _n(p["dur"])
    t = _t(n)
    y = np.zeros(n)
    for i, (delay, gain) in enumerate([(0.0, 1.0), (p["toe_delay_ms"] / 1000, 0.55)]):
        s = int(jitter(rng, delay, p["vary"]) * SR) if delay else 0
        m = n - s
        if m <= 8:
            continue
        tt = t[:m]
        burst = filt(rng.standard_normal(m), "lowpass", jitter(rng, p["cutoff"], p["vary"]), 2) * np.exp(-tt / p["decay_s"])
        thump = osc(np.full(m, jitter(rng, p["thump_hz"], p["vary"])), "sine") * np.exp(-tt / 0.03) * p["thump_gain"]
        y[s:] += gain * (burst + thump) * (1.0 if i == 0 else 0.9)
    return y


def r_croak(rng, p):
    """A frog croak: a buzzy glottal pulse train with a rise-fall pitch contour and a 20–40 Hz rattle, shaped by three
    parallel formant band-passes, repeated per syllable."""
    parts = []
    for _ in range(int(p["syllables"])):
        n = _n(jitter(rng, p["syllable_ms"] / 1000, p["vary"]))
        t = np.arange(n) / max(n - 1, 1)
        f0 = jitter(rng, p["f0"], p["vary"])
        contour = f0 * (1.0 + p["bend"] * np.sin(np.pi * t))
        src = osc(contour, "saw")
        rattle = 0.5 + 0.5 * np.tanh(3 * np.sin(2 * np.pi * np.cumsum(np.full(n, jitter(rng, p["rattle_hz"], p["vary"]))) / SR))
        src = src * (1.0 - p["rattle_depth"] + p["rattle_depth"] * rattle)
        y = np.zeros(n)
        for fc, bw, g in zip(p["formants"], p["bandwidths"], p["formant_gains"]):
            fcj = jitter(rng, fc, p["vary"] / 2)
            y += g * filt(src, "band", (fcj - bw / 2, fcj + bw / 2), 2)
        env = np.clip(t * n / max(1, int(0.008 * SR)), 0, 1) * (1.0 - t) ** 0.6
        parts.append(y * env)
        parts.append(np.zeros(_n(jitter(rng, p["gap_ms"] / 1000, p["vary"]))))
    return np.concatenate(parts[:-1]) if len(parts) > 1 else parts[0]


def r_pluck(rng, p):
    """Karplus-Strong: a noise burst circulating in a delay line with an averaging low-pass (vectorised per period)."""
    n = _n(p["dur"])
    f = jitter(rng, p["freq"], p["vary"] / 3)
    period = max(2, int(round(SR / f)))
    burst = rng.uniform(-1, 1, period)
    burst = p["brightness"] * burst + (1 - p["brightness"]) * np.convolve(burst, np.ones(4) / 4, mode="same")
    y = np.zeros(n + period + 1)
    y[:period] = burst
    pos = period
    while pos < n:
        end = min(pos + period, n)
        prev = y[pos - period:end - period]
        prev1 = y[pos - period + 1:end - period + 1]
        y[pos:end] = p["damping"] * 0.5 * (prev + prev1)
        pos = end
    return y[:n]


def r_bell(rng, p):
    n = _n(p["dur"])
    t = _t(n)
    fc = jitter(rng, p["freq"], p["vary"] / 3)
    idx = p["index"] * np.exp(-t / (p["decay_s"] * 0.6))
    mod = idx * np.sin(2 * np.pi * fc * p["ratio"] * t)
    return np.sin(2 * np.pi * fc * t + mod) * env_ad(n, 0.002, p["decay_s"])


def r_noise_bed(rng, p):
    """Seamless ambience loop: coloured noise built in the frequency domain (inherently periodic) with an LFO whose
    cycle count is an integer, so the file loops without a seam."""
    n = _n(p["dur"])
    spec = rng.standard_normal(n // 2 + 1) + 1j * rng.standard_normal(n // 2 + 1)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    shape = f ** (-p["tilt"] / 2.0)
    shape *= 1.0 / (1.0 + (f / p["lowpass"]) ** 4)
    shape *= (f / (f + p["highpass"])) ** 2
    y = np.fft.irfft(spec * shape, n)
    cycles = max(1, int(round(p["lfo_hz"] * p["dur"])))
    lfo = 1.0 - p["lfo_depth"] * (0.5 + 0.5 * np.cos(2 * np.pi * cycles * np.arange(n) / n + rng.uniform(0, 2 * np.pi)))
    return y * lfo


RECIPES = {
    "ui_blip": (r_ui_blip, "ui", "short tonal UI tick", {
        "freq": 880.0, "dur": 0.08, "glide": 1.06, "shape": "sine", "attack_ms": 2.0, "decay_ms": 25.0, "vary": 0.03}),
    "ui_confirm": (r_ui_confirm, "ui", "two rising notes (accept / confirm)", {
        "notes": [660.0, 990.0], "note_ms": 70.0, "gap_ms": 10.0, "shape": "triangle", "decay_ms": 45.0, "vary": 0.01}),
    "ui_error": (r_ui_error, "ui", "two falling buzzy notes (error / cancel)", {
        "notes": [440.0, 311.0], "note_ms": 90.0, "gap_ms": 15.0, "shape": "square", "decay_ms": 60.0, "vary": 0.01}),
    "pickup": (r_pickup, "sfx", "three-note rising arpeggio (coin / collect)", {
        "notes": [988.0, 1319.0, 1976.0], "note_ms": 55.0, "shape": "square", "decay_ms": 40.0, "vary": 0.01}),
    "jump": (r_jump, "sfx", "upward pitch sweep", {"f_start": 300.0, "f_end": 720.0, "dur": 0.16, "vary": 0.04}),
    "whoosh": (r_whoosh, "sfx", "band-passed noise sweep (swing, dash, pass-by)", {
        "dur": 0.6, "f_lo": 350.0, "f_hi": 2600.0, "peak_at": 0.55, "q": 1.4, "env_power": 1.5, "vary": 0.05}),
    "impact": (r_impact, "sfx", "thump with a pitch drop, noise crack and click (hit, land, slam)", {
        "dur": 0.5, "f_start": 120.0, "f_end": 42.0, "drop_s": 0.12, "body_decay_s": 0.11, "noise_lp": 2500.0,
        "noise_decay_s": 0.02, "noise_gain": 0.6, "click_gain": 0.3, "drive": 1.5, "vary": 0.05}),
    "footstep": (r_footstep, "sfx", "heel and toe noise transients; vary the seed for variants", {
        "dur": 0.16, "cutoff": 1400.0, "decay_s": 0.018, "thump_hz": 90.0, "thump_gain": 0.5, "toe_delay_ms": 28.0,
        "vary": 0.12}),
    "croak": (r_croak, "sfx", "frog croak / ribbit (formant-filtered pulse train)", {
        "syllables": 2, "syllable_ms": 120.0, "gap_ms": 55.0, "f0": 105.0, "bend": 0.25, "rattle_hz": 32.0,
        "rattle_depth": 0.6, "formants": [450.0, 1150.0, 2400.0], "bandwidths": [140.0, 180.0, 260.0],
        "formant_gains": [1.0, 0.6, 0.25], "vary": 0.06}),
    "pluck": (r_pluck, "sfx", "Karplus-Strong plucked string", {
        "freq": 220.0, "dur": 0.9, "damping": 0.996, "brightness": 0.6, "vary": 0.03}),
    "bell": (r_bell, "sfx", "FM bell / chime", {"freq": 660.0, "ratio": 1.4, "index": 4.0, "dur": 1.4, "decay_s": 0.45,
                                            "vary": 0.02}),
    "noise_bed": (r_noise_bed, "ambience", "seamless coloured-noise ambience loop (wind, room tone)", {
        "dur": 4.0, "tilt": 1.0, "lowpass": 3500.0, "highpass": 60.0, "lfo_hz": 0.25, "lfo_depth": 0.35, "vary": 0.0}),
}


LOOP_RECIPES = {"noise_bed"}  # built-ins that are seamless by construction


def coerce(key: str, raw: str, default):
    try:
        if isinstance(default, list):
            val = json.loads(raw)
            if not isinstance(val, list):
                raise ValueError
            return [float(v) for v in val]
        if isinstance(default, bool):
            return raw.lower() in ("1", "true", "yes")
        if isinstance(default, int):
            return int(raw)
        if isinstance(default, float):
            return float(raw)
        return raw
    except ValueError as e:
        raise ac.UsageError(f"--set {key}={raw}: expected {type(default).__name__}") from e


def merged_params(name: str, overrides: dict) -> dict:
    if name not in RECIPES:
        raise ac.UsageError(f"unknown recipe {name!r}; run --list")
    defaults = RECIPES[name][3]
    unknown = sorted(set(overrides) - set(defaults))
    if unknown:
        raise ac.UsageError(f"recipe {name}: unknown parameter(s) {', '.join(unknown)}; known: {', '.join(defaults)}")
    p = dict(defaults)
    for k, v in overrides.items():
        p[k] = coerce(k, v, defaults[k]) if isinstance(v, str) and not isinstance(defaults[k], str) else v
    for k in ("dur",):
        if k in p and not (0.005 <= float(p[k]) <= MAX_DURATION_S):
            raise ac.UsageError(f"recipe {name}: dur must be 0.005–{MAX_DURATION_S} s")
    return p


def load_spec(recipe: str, sets: dict) -> tuple[dict, str]:
    """(spec, provenance) where spec = {name, class, target_lufs?, layers:[{recipe, params, gain_db, offset_s}]}."""
    path = Path(recipe)
    if recipe.lower().endswith(".json"):
        if not path.is_file():
            raise ac.UsageError(f"recipe file not found: {recipe}")
        try:
            raw = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as e:
            raise ac.UsageError(f"recipe file {recipe}: {e}") from e
        if raw.get("schema") != 1:
            raise ac.UsageError(f"recipe file {recipe}: schema must be 1")
        layers = raw.get("layers") or ([{"recipe": raw["recipe"], "params": raw.get("params", {})}] if raw.get("recipe") else [])
        if not layers:
            raise ac.UsageError(f"recipe file {recipe}: needs 'recipe' or 'layers'")
        if sets:
            raise ac.UsageError("--set applies to built-in recipes only; edit the recipe file instead")
        spec = {"name": str(raw.get("name") or path.stem), "class": raw.get("class"), "target_lufs": raw.get("target_lufs"),
                "duration_s": raw.get("duration_s"), "loop": bool(raw.get("loop", False)), "layers": []}
        for i, lay in enumerate(layers):
            if not isinstance(lay, dict) or "recipe" not in lay:
                raise ac.UsageError(f"recipe file {recipe}: layer {i} needs a 'recipe'")
            spec["layers"].append({"recipe": lay["recipe"], "params": merged_params(lay["recipe"], lay.get("params") or {}),
                                   "gain_db": float(lay.get("gain_db", 0.0)), "offset_s": float(lay.get("offset_s", 0.0))})
        digest = hashlib.sha256(path.read_bytes()).hexdigest()[:12]
        return spec, f"recipe file {path.name} sha256:{digest}"
    spec = {"name": recipe, "class": None, "target_lufs": None, "duration_s": None, "loop": recipe in LOOP_RECIPES,
            "layers": [{"recipe": recipe, "params": merged_params(recipe, sets), "gain_db": 0.0, "offset_s": 0.0}]}
    prov = f"recipe {recipe}" + (" " + " ".join(f"{k}={v}" for k, v in sorted(sets.items())) if sets else "")
    return spec, prov


def render(spec: dict, seed: int, cls: str, target_lufs: float) -> tuple[np.ndarray, dict]:
    rendered = []
    for i, lay in enumerate(spec["layers"]):
        rng = np.random.default_rng([int(seed), i])
        fn = RECIPES[lay["recipe"]][0]
        y = np.asarray(fn(rng, lay["params"]), dtype=np.float64)
        y = y * 10 ** (lay["gain_db"] / 20.0)
        rendered.append((int(round(max(0.0, lay["offset_s"]) * SR)), y))
    total = max(off + len(y) for off, y in rendered)
    if spec.get("duration_s"):
        total = _n(min(float(spec["duration_s"]), MAX_DURATION_S))
    out = np.zeros(total)
    for off, y in rendered:
        m = max(0, min(len(y), total - off))
        out[off:off + m] += y[:m]
    if not np.any(np.abs(out) > 1e-9):
        raise ac.ToolError("the recipe rendered silence", ac.EXIT_FAIL)
    if not spec.get("loop"):  # a loop must not fade to silence at its end (that is a click at the wrap)
        out = fade_tail(out, 5.0)
    out, info = ac.normalise_gain(out, SR, target_lufs, TP_CEILING_DB)
    info["soft_limit_db"] = 0
    if info["limited"]:
        # transient-heavy sounds (plucks, long-tailed hits) hit the peak ceiling first: push them through a soft knee
        base = out
        for drive_db in (3, 6, 9, 12):
            cand, cinfo = ac.normalise_gain(soft_knee(base * 10 ** (drive_db / 20.0)), SR, target_lufs, TP_CEILING_DB)
            out, info = cand, {**cinfo, "soft_limit_db": drive_db}
            lufs, _ = ac.integrated_lufs(out, SR)
            if lufs is not None and lufs >= target_lufs - 1.0:
                break
    return np.clip(out, -1.0, 1.0), info


def soft_knee(y: np.ndarray, thr: float = 0.5) -> np.ndarray:
    """Soft-knee saturation above `thr` (full scale 1.0): lowers the crest factor so peak-limited sounds reach their
    loudness target."""
    a = np.abs(y)
    over = a > thr
    out = y.copy()
    out[over] = np.sign(y[over]) * (thr + (1 - thr) * np.tanh((a[over] - thr) / (1 - thr)))
    return out


def list_recipes() -> dict:
    return {name: {"class": cls, "about": about, "params": params} for name, (_, cls, about, params) in RECIPES.items()}


def parse_sets(items: list[str]) -> dict:
    out = {}
    for it in items or []:
        if "=" not in it:
            raise ac.UsageError(f"--set expects key=value, got {it!r}")
        k, v = it.split("=", 1)
        out[k.strip()] = v.strip()
    return out


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="sfx_synth.py", description="seeded procedural SFX → 48 kHz 16-bit WAV")
    ap.add_argument("--recipe", help="built-in recipe name or a recipe .json file")
    ap.add_argument("--seed", type=int)
    ap.add_argument("--out", help="output .wav path")
    ap.add_argument("--set", action="append", default=[], metavar="KEY=VALUE", help="override a built-in parameter")
    ap.add_argument("--class", dest="cls", choices=sorted(CLASS_CENTRE), help="loudness class (default from the recipe)")
    ap.add_argument("--target-lufs", type=float)
    ap.add_argument("--author", default="project team", help="licence row author (default: project team)")
    ap.add_argument("--ships", action="store_true", help="mark the licence row as shipping")
    ap.add_argument("--no-licence", action="store_true", help="do not write a licence row")
    ap.add_argument("--list", action="store_true", help="list the built-in recipes and their parameters")
    ap.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    if args.list:
        recipes = list_recipes()
        ac.emit({"ok": True, "recipes": recipes}, args.json,
                [f"{k:11s} {v['class']:9s} {v['about']}" for k, v in recipes.items()])
        return ac.EXIT_OK
    if not args.recipe or args.seed is None or not args.out:
        raise ac.UsageError("--recipe, --seed and --out are required (or --list)")
    if args.seed < 0:
        raise ac.UsageError("--seed must be >= 0")
    out = Path(args.out)
    if out.suffix.lower() != ".wav":
        raise ac.UsageError("--out must be a .wav file")
    spec, prov = load_spec(args.recipe, parse_sets(args.set))
    first = spec["layers"][0]["recipe"]
    cls = args.cls or spec.get("class") or RECIPES[first][1]
    if cls not in CLASS_CENTRE:
        raise ac.UsageError(f"class {cls!r} is not one of {', '.join(sorted(CLASS_CENTRE))}")
    target = args.target_lufs if args.target_lufs is not None else (spec.get("target_lufs") or CLASS_CENTRE[cls])
    y, info = render(spec, args.seed, cls, float(target))
    data = ac.pcm16_wav_bytes(y, SR)
    ac.write_bytes_atomic(out, data)
    sha = hashlib.sha256(data).hexdigest()
    lufs, method = ac.integrated_lufs(y, SR)
    tp = ac.true_peak(y, SR)
    ws = ac.find_workspace()
    row = {
        "source_kind": "procedural", "licence_id": "Own-Work", "author": args.author, "retrieved_at_utc": ac.utc_iso(),
        "ai_generated": "false", "commercial_ok": "true", "training_ban": "false", "redistribution": "allowed",
        "ships": "true" if args.ships else "false",
        "notes": f"sfx_synth.py {prov} seed {args.seed} class {cls}",
    }
    lic = ac.write_licence_row(out, row, ws, enabled=not args.no_licence)
    result = {
        "ok": True, "out": ac.rel_path(out, ws), "recipe": spec["name"], "seed": args.seed, "class": cls,
        "sha256": sha, "sample_rate": SR, "channels": 1, "bits": 16, "duration_s": round(len(y) / SR, 4),
        "lufs_i": None if lufs is None else round(lufs, 2), "lufs_method": method,
        "true_peak_dbtp": None if tp <= 0 else round(20 * np.log10(tp), 2), "gain_db": info["gain_db"],
        "limited": info["limited"], "soft_limit_db": info.get("soft_limit_db", 0), "licence_row": lic,
    }
    ac.emit(result, args.json, [f"wrote {result['out']} ({result['duration_s']} s, {result['lufs_i']} LUFS, "
                                f"{result['true_peak_dbtp']} dBTP) sha256 {sha[:12]}", f"licence row: {lic['status']}"])
    return ac.EXIT_OK


if __name__ == "__main__":
    sys.exit(ac.run_main(main))
