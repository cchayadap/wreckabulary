"""Deterministic impact sounds for Wreckabulary: swings, confirmed contacts, damage and breaks.

    uv run --offline --frozen --project Tools/kit/audio python ArtSource/Audio/ImpactV1/Source/synth_impacts.py \
        --project-root <checkout> --run-name synth-01

Each contact is a striker excitation (force pulse width, own ring, leather slap, bristles) driving a surface's modal
response, so a soft leather strap barely excites glass while a hard buckle or bat rings it. Damage and break layers
are separate files and are only played for a confirmed crack or break. Output: 48 kHz 16-bit mono WAV, normalised by
the kit's BS.1770 helpers to -17 LUFS (a look-ahead limiter tames each transient first) with a -1.5 dBTP ceiling, plus manifest.json (seeds, parameters, sha256).
Own work: no recordings or third-party samples are used.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import math
import os
import re
import sys
from pathlib import Path

import numpy as np
from scipy.ndimage import minimum_filter1d, uniform_filter1d
from scipy.signal import butter, fftconvolve, sosfilt

SR = 48000
WEAPON_PARTS = [("BELT", "Strap"), ("BELT", "Buckle"), ("BAT", "Body"), ("HAMMER", "Body"), ("BROOM", "Body"),
                ("BLADE", "Body"), ("Default", "Any")]
SURFACES = ["Wood", "Glass", "Metal", "Ceramic", "Stone", "Soft", "Body"]
BREAKABLE = ["Wood", "Glass", "Metal", "Ceramic", "Stone", "Soft"]
SWINGS = ["BELT", "BAT", "HAMMER", "BROOM", "BLADE", "Default"]
VARIANTS = {"contact": 3, "swing": 2, "damage": 2, "break": 3}

STRIKERS = {
    "BELT/Strap": dict(pulse_ms=2.4, slap=0.9, weight=0.32, ring=None),
    "BELT/Buckle": dict(pulse_ms=0.22, slap=0.3, weight=0.42,
                        ring=dict(f=[3150, 5070, 7400], tau=[0.04, 0.025, 0.016], a=[1.0, 0.6, 0.35], gain=0.4)),
    "BAT/Body": dict(pulse_ms=0.7, slap=0.08, weight=0.85,
                     ring=dict(f=[420, 1130, 2240], tau=[0.035, 0.02, 0.012], a=[1.0, 0.5, 0.25], gain=0.5)),
    "HAMMER/Body": dict(pulse_ms=0.15, slap=0.04, weight=1.0,
                        ring=dict(f=[1850, 3420, 5600], tau=[0.07, 0.045, 0.02], a=[1.0, 0.6, 0.3], gain=0.4)),
    "BROOM/Body": dict(pulse_ms=1.5, slap=0.15, weight=0.45, bristle=0.9,
                       ring=dict(f=[380, 990], tau=[0.02, 0.012], a=[1.0, 0.4], gain=0.22)),
    "BLADE/Body": dict(pulse_ms=0.2, slap=0.05, weight=0.6,
                       ring=dict(f=[2900, 4800, 6900, 9100], tau=[0.09, 0.06, 0.04, 0.02], a=[1.0, 0.7, 0.4, 0.2], gain=0.45)),
    "Default/Any": dict(pulse_ms=3.2, slap=0.35, weight=0.4, ring=None),
}

SURFACE_MODEL = {
    "Wood": dict(modes=(180, 900, 7), tau=(0.012, 0.05), noise=((150, 2500), 0.008, 0.5), dur=0.55),
    "Glass": dict(modes=(1400, 6800, 9), tau=(0.025, 0.12), noise=((3000, 12000), 0.004, 0.15), dur=0.6),
    "Metal": dict(metal=(230, 420), tau=(0.25, 0.7), noise=((2000, 9000), 0.003, 0.2), dur=1.2),
    "Ceramic": dict(modes=(900, 4300, 7), tau=(0.04, 0.15), noise=((2000, 8000), 0.004, 0.2), dur=0.6),
    "Stone": dict(modes=(240, 1500, 6), tau=(0.006, 0.02), noise=((800, 5000), 0.015, 0.7), dur=0.45),
    "Soft": dict(modes=(70, 240, 3), tau=(0.02, 0.05), noise=((60, 600), 0.03, 1.0), dur=0.45),
    "Body": dict(modes=(140, 520, 4), tau=(0.015, 0.04), noise=((100, 1500), 0.02, 0.8), dur=0.45),
}


def t_axis(n):
    return np.arange(n) / SR


def band(x, lo, hi, order=4):
    hi = min(hi, SR / 2 * 0.95)
    sos = butter(order, [max(20.0, lo), hi], btype="band", fs=SR, output="sos")
    return sosfilt(sos, x)


def lowpass(x, f, order=4):
    return sosfilt(butter(order, min(f, SR / 2 * 0.95), btype="low", fs=SR, output="sos"), x)


def modal(freqs, taus, amps, n, rng, jitter=0.03, phase=True):
    t = t_axis(n)
    out = np.zeros(n)
    for f, tau, a in zip(freqs, taus, amps):
        f *= 1 + rng.uniform(-jitter, jitter)
        ph = rng.uniform(0, 2 * math.pi) if phase else 0.0
        out += a * np.sin(2 * math.pi * f * t + ph) * np.exp(-t / tau)
    return out


def noise_burst(n, rng, lo, hi, tau, delay=0):
    x = band(rng.standard_normal(n), lo, hi)
    t = t_axis(n) - delay / SR
    env = np.where(t >= 0, np.exp(-np.clip(t, 0, None) / tau), 0.0)
    return x * env


def surface_response(surface, n, rng, weight):
    m = SURFACE_MODEL[surface]
    if "metal" in m:
        f0 = rng.uniform(*m["metal"])
        ratios = [1.0, 2.76, 5.40, 8.93, 13.34, 17.2]
        freqs = [f0 * r for r in ratios]
        taus = [rng.uniform(*m["tau"]) / (1 + 0.35 * i) for i in range(len(ratios))]
        amps = [1.0, 0.7, 0.5, 0.35, 0.22, 0.12]
    else:
        lo, hi, count = m["modes"]
        freqs = sorted(rng.uniform(lo, hi, count))
        taus = [rng.uniform(*m["tau"]) * (1.0 - 0.4 * i / count) for i in range(count)]
        amps = [rng.uniform(0.5, 1.0) * (1.0 - 0.5 * i / count) for i in range(count)]
    amps = [a * (0.5 + 0.5 * weight) if f < 800 else a for a, f in zip(amps, freqs)]
    ir = modal(freqs, taus, amps, n, rng)
    (nlo, nhi), ntau, ngain = m["noise"]
    ir += ngain * noise_burst(n, rng, nlo, nhi, ntau) * 3.0
    return ir


def force_pulse(ms):
    w = max(2, int(ms * SR / 1000))
    return np.sin(np.linspace(0, math.pi, w)) / w * 40


def contact(weapon, part, surface, rng):
    s = STRIKERS[f"{weapon}/{part}"]
    n = int(SURFACE_MODEL[surface]["dur"] * SR)
    ir = surface_response(surface, n, rng, s["weight"])
    y = fftconvolve(force_pulse(s["pulse_ms"] * rng.uniform(0.9, 1.1)), ir)[:n]
    if s.get("slap"):
        y += s["slap"] * noise_burst(n, rng, 500, 6500, 0.004) * 2.2
    damped = surface in ("Soft", "Body")
    if s.get("ring"):
        r = s["ring"]
        y += r["gain"] * (0.35 if damped else 1.0) * modal(r["f"], r["tau"], r["a"], n, rng)
    if s.get("bristle"):
        for _ in range(40):
            d = int(rng.uniform(0, 0.045) * SR)
            y += s["bristle"] * 0.25 * noise_burst(n, rng, 2500, 9000, 0.002, delay=d)
    return fade(y)


def damage(surface, rng):
    n = int(0.45 * SR)
    y = np.zeros(n)
    if surface == "Glass":
        for _ in range(int(rng.integers(7, 12))):
            d = int(rng.uniform(0, 0.09) * SR)
            y += noise_burst(n, rng, 3500, 14000, 0.0012, delay=d) * rng.uniform(0.5, 1.0)
        y += 0.25 * modal(sorted(rng.uniform(2500, 7000, 5)), [0.03] * 5, [1] * 5, n, rng)
    elif surface == "Wood":
        for d in (0, int(rng.uniform(0.02, 0.05) * SR)):
            y += noise_burst(n, rng, 300, 3000, 0.006, delay=d) * 1.2
        for _ in range(30):
            y += noise_burst(n, rng, 1500, 6000, 0.0015, delay=int(rng.uniform(0, 0.22) * SR)) * 0.3
        t = t_axis(n)
        creak = np.sin(2 * math.pi * (95 * t + 6 * np.sin(2 * math.pi * 7 * t))) * np.exp(-t / 0.12) * (t < 0.25)
        y += 0.25 * band(np.sign(creak) * np.abs(creak) ** 0.5, 80, 1200, 2)
    elif surface == "Metal":
        f0 = rng.uniform(120, 180)
        y += modal([f0 * r for r in (1, 2.76, 5.4)], [0.15, 0.1, 0.06], [1, 0.6, 0.3], n, rng)
        y += 0.4 * noise_burst(n, rng, 1500, 6000, 0.04)
    elif surface == "Ceramic":
        y += noise_burst(n, rng, 2500, 12000, 0.002) * 1.5
        for _ in range(8):
            d = int(rng.uniform(0.01, 0.13) * SR)
            g = np.zeros(n); g[d:] = modal([rng.uniform(2500, 6500)], [0.015], [1], n - d, rng)
            y += 0.4 * g
    elif surface == "Stone":
        for _ in range(55):
            y += noise_burst(n, rng, 400, 4000, rng.uniform(0.003, 0.01), delay=int(rng.uniform(0, 0.22) * SR)) * rng.uniform(0.2, 0.6)
    else:
        x = band(rng.standard_normal(n), 800, 5000)
        t = t_axis(n)
        grains = (np.sin(2 * math.pi * 90 * t) > 0.2).astype(float)
        y += x * grains * np.exp(-t / 0.12) * (t < 0.3)
    return fade(y)


def grains(n, rng, count, span, make, density_tau):
    y = np.zeros(n)
    for _ in range(count):
        at = int(min(span, rng.exponential(density_tau)) * SR)
        if at >= n - 10:
            continue
        g = make(n - at)
        y[at:] += g * rng.uniform(0.3, 1.0)
    return y


def broken(surface, rng):
    n = int((1.5 if surface in ("Glass", "Metal") else 1.2) * SR)
    y = np.zeros(n)
    if surface == "Glass":
        y += noise_burst(n, rng, 1000, 15000, 0.03) * 2.0
        y += grains(n, rng, 180, 1.3, lambda m: modal([rng.uniform(2000, 9000)], [rng.uniform(0.008, 0.05)], [1], m, rng), 0.35) * 0.6
    elif surface == "Wood":
        y += noise_burst(n, rng, 300, 4000, 0.012) * 2.0
        y += grains(n, rng, 80, 0.4, lambda m: noise_burst(m, rng, 1500, 7000, 0.0015), 0.12) * 0.5
        y += grains(n, rng, 18, 1.0, lambda m: modal(sorted(rng.uniform(200, 900, 3)), [0.02, 0.015, 0.01], [1, .6, .3], m, rng), 0.35) * 0.9
    elif surface == "Metal":
        f0 = rng.uniform(200, 320)
        y += modal([f0 * r for r in (1, 2.76, 5.4, 8.93)], [0.5, 0.35, 0.2, 0.1], [1, .7, .5, .3], n, rng) * 1.2
        y += noise_burst(n, rng, 1500, 9000, 0.05)
        y += grains(n, rng, 14, 1.2, lambda m: modal([rng.uniform(600, 1500) * r for r in (1, 2.76)], [0.15, 0.08], [1, .5], m, rng), 0.4) * 0.6
    elif surface == "Ceramic":
        y += noise_burst(n, rng, 1500, 12000, 0.02) * 1.8
        y += grains(n, rng, 90, 1.0, lambda m: modal([rng.uniform(1200, 6000)], [rng.uniform(0.01, 0.04)], [1], m, rng), 0.3) * 0.7
    elif surface == "Stone":
        y += modal([rng.uniform(60, 150)], [0.08], [1], n, rng) * 1.5 + noise_burst(n, rng, 200, 2000, 0.03)
        y += grains(n, rng, 140, 1.0, lambda m: noise_burst(m, rng, 300, 3000, rng.uniform(0.005, 0.02)), 0.3) * 0.5
    else:
        t = t_axis(n)
        y += modal([rng.uniform(60, 90)], [0.06], [1], n, rng) * 1.2
        y += lowpass(rng.standard_normal(n), 900) * np.exp(-t / 0.18) * 0.8
        y += band(rng.standard_normal(n), 1000, 6000) * ((np.sin(2 * math.pi * 70 * t) > 0) * np.exp(-((t - 0.12) / 0.08) ** 2)) * 0.5
    return fade(y)


SWING_SPEC = {
    "BELT": dict(f=(600, 1800, 900), bw=500, dur=0.42, flutter=(32, 0.35), snap=0.62),
    "BAT": dict(f=(250, 700, 300), bw=320, dur=0.38),
    "HAMMER": dict(f=(180, 480, 220), bw=230, dur=0.46),
    "BROOM": dict(f=(2500, 5000, 3000), bw=2000, dur=0.36, rustle=True),
    "BLADE": dict(f=(1500, 3800, 1800), bw=700, dur=0.3, shimmer=6200),
    "Default": dict(f=(300, 900, 400), bw=350, dur=0.26),
}


def swing(weapon, rng):
    s = SWING_SPEC[weapon]
    n = int(s["dur"] * SR * rng.uniform(0.94, 1.06))
    t = t_axis(n)
    u = t / t[-1]
    f0, f1, f2 = (f * rng.uniform(0.95, 1.05) for f in s["f"])
    fc = np.where(u < 0.55, f0 + (f1 - f0) * (u / 0.55) ** 1.5, f1 + (f2 - f1) * ((u - 0.55) / 0.45))
    base = lowpass(rng.standard_normal(n), s["bw"])
    y = base * np.sin(2 * math.pi * np.cumsum(fc) / SR)
    env = np.sin(math.pi * np.clip(u, 0, 1)) ** 2 * np.exp(-1.5 * np.clip(u - 0.55, 0, None))
    y *= env
    if "flutter" in s:
        hz, depth = s["flutter"]
        y *= 1 - depth + depth * np.sin(2 * math.pi * hz * t) ** 2
    if "snap" in s:
        y += 0.35 * noise_burst(n, rng, 1000, 9000, 0.0025, delay=int(s["snap"] * n))
    if s.get("rustle"):
        y += grains(n, rng, 50, s["dur"], lambda m: noise_burst(m, rng, 3000, 10000, 0.002), s["dur"] * 0.5) * 0.15 * env.max()
    if "shimmer" in s:
        y += 0.05 * np.sin(2 * math.pi * s["shimmer"] * t) * env
    return fade(y, 0.03)


def fade(y, tail=0.02):
    n = len(y)
    k = min(n // 4, int(tail * SR))
    if k > 0:
        y[-k:] *= np.linspace(1, 0, k) ** 2
    y[: int(0.0005 * SR)] *= np.linspace(0, 1, int(0.0005 * SR))
    return y - np.mean(y) * 0


def limit(y, ceiling_db=-3.0, hold_ms=2.0):
    """Look-ahead peak limiter: the gain at every sample is at most what that sample's peak needs, so it never clips."""
    need = np.minimum(1.0, 10 ** (ceiling_db / 20) / np.maximum(np.abs(y), 1e-9))
    w = 2 * int(hold_ms * SR / 1000) + 1
    return y * uniform_filter1d(minimum_filter1d(need, size=w, mode="nearest"), size=w, mode="nearest")


def master(y, ac, target_lufs=-17.0, tp_ceiling=-1.5):
    """Bring a transient to the SFX loudness window: alternate loudness gain and peak limiting, then cap true peak."""
    for _ in range(10):
        lufs, _ = ac.integrated_lufs(y, SR)
        y = limit(y * 10 ** ((target_lufs + 0.4 - lufs) / 20))
        if abs(ac.integrated_lufs(y, SR)[0] - target_lufs) < 0.6:
            break
    return ac.normalise_gain(y, SR, target_lufs, tp_ceiling)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--project-root", required=True)
    p.add_argument("--run-name", required=True)
    a = p.parse_args()
    root = Path(a.project_root).resolve()
    if not (root / "ProjectSettings" / "ProjectVersion.txt").is_file():
        raise SystemExit("project root must contain ProjectSettings/ProjectVersion.txt")
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,39}", a.run_name):
        raise SystemExit("run name must match ^[a-z0-9][a-z0-9-]{0,39}$")
    sys.path.insert(0, str(root / "Tools" / "kit" / "audio"))
    import audio_common as ac  # noqa: E402  (read-only use of the kit's BS.1770 helpers)

    out = root / "ArtSource" / "Audio" / "ImpactV1" / a.run_name
    if out.exists():
        raise SystemExit(f"{out} exists; choose a new run name")
    script = Path(__file__).resolve()
    jobs = []
    for weapon, part in WEAPON_PARTS:
        for surface in SURFACES:
            for v in range(VARIANTS["contact"]):
                jobs.append(("contact", f"{weapon}_{part}_{surface}_{v + 1}", (weapon, part, surface), dict(weapon=weapon, part=part, surface=surface)))
    for surface in BREAKABLE:
        for v in range(VARIANTS["damage"]):
            jobs.append(("damage", f"{surface}_damaged_{v + 1}", (surface,), dict(surface=surface)))
        for v in range(VARIANTS["break"]):
            jobs.append(("break", f"{surface}_broken_{v + 1}", (surface,), dict(surface=surface)))
    for weapon in SWINGS:
        for v in range(VARIANTS["swing"]):
            jobs.append(("swing", f"{weapon}_swing_{v + 1}", (weapon,), dict(weapon=weapon)))
    makers = {"contact": lambda args, rng: contact(*args, rng), "damage": lambda args, rng: damage(*args, rng),
              "break": lambda args, rng: broken(*args, rng), "swing": lambda args, rng: swing(*args, rng)}
    files = []
    for kind, name, args, params in jobs:
        seed = int(hashlib.sha256(f"{kind}/{name}".encode()).hexdigest()[:8], 16)
        rng = np.random.default_rng(seed)
        y = makers[kind](args, rng)
        y, norm = master(y, ac)
        folder = out / kind
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / f"{name}.wav"
        data = ac.pcm16_wav_bytes(y, SR)
        with open(path, "xb") as f:
            f.write(data)
        files.append({"path": f"{kind}/{name}.wav", "kind": kind, **params, "seed": seed, "seconds": round(len(y) / SR, 4),
                      "sha256": hashlib.sha256(data).hexdigest(), "gain_db": norm["gain_db"], "tp_limited": norm["limited"]})
    manifest = {"schema": 1, "run": a.run_name, "utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                "generator": str(script.relative_to(root)).replace(os.sep, "/"), "generator_sha256": hashlib.sha256(script.read_bytes()).hexdigest(),
                "numpy": np.__version__, "sample_rate": SR, "format": "PCM 16-bit mono WAV", "target_lufs": -17.0, "tp_ceiling_dbtp": -1.5, "limiter": {"ceiling_dbfs": -3.0, "hold_ms": 2.0},
                "licence": "Own work: procedurally synthesised by this script; no recordings or third-party samples.",
                "ships": True, "files": files}
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"run": a.run_name, "files": len(files), "tp_limited": sum(f["tp_limited"] for f in files)}))


if __name__ == "__main__":
    main()
