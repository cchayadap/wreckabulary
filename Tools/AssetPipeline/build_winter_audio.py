"""Author and validate the original Winter House Party sound bank, outside Assets."""
from __future__ import annotations

import argparse
from array import array
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import sys
import wave

RATE = 44_100
TARGET_PEAK_DB = -4.5
REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = REPO / "ArtSource/Collections/Winter/Audio"
SPECS = (
    ("bell-01", "Pickup", .34, 4101, "Rounded brass A5 bell with a lighter E6 answer", (.25, .45)),
    ("bell-02", "Pickup", .39, 4102, "Warm B5 bell with a delayed F-sharp6 shimmer", (.25, .45)),
    ("paper-01", "Craft", .19, 4201, "Three soft folds with a short ribbon-like brush", (.15, .25)),
    ("paper-02", "Craft", .22, 4202, "Two wider paper crinkles and a delicate settling rustle", (.15, .25)),
    ("snow-01", "Dodge", .24, 4301, "Cushioned snow compression with a light trailing swish", (.18, .30)),
    ("snow-02", "Dodge", .28, 4302, "A deeper snow brush with a second soft crunch", (.18, .30)),
)


def lowpass(values, hz, passes=1):
    alpha = 1 - math.exp(-2 * math.pi * hz / RATE)
    for _ in range(passes):
        previous = 0.0
        result = []
        for sample in values:
            previous += alpha * (sample - previous)
            result.append(previous)
        values = result
    return values


def band_noise(rng, count, low, high):
    noise = [rng.uniform(-1, 1) for _ in range(count)]
    upper = lowpass(noise, high, 2)
    lower = lowpass(upper, low)
    return [a - b for a, b in zip(upper, lower)]


def bump(t, centre, width):
    return math.exp(-.5 * ((t - centre) / width) ** 2)


def fade_window(count, attack, release):
    values = []
    for index in range(count):
        start = min(1, index / (attack * RATE))
        end = min(1, (count - 1 - index) / (release * RATE))
        values.append((.5 - .5 * math.cos(math.pi * start)) * (.5 - .5 * math.cos(math.pi * end)))
    return values


def bell(count, variant, rng):
    answer = .087 if variant == 1 else .104
    fundamental = 880 if variant == 1 else 987.7666
    modes = ((1, 1, .105), (2.01, .22, .055), (2.756, .14, .043), (4.07, .048, .03), (5.43, .019, .022))
    noise = band_noise(rng, count, 850, 3400)
    signal = []
    for index in range(count):
        t = index / RATE
        value = noise[index] * .065 * math.exp(-t / .009)
        for offset, pitch, gain in ((0, fundamental, 1), (answer, fundamental * 1.4983, .38)):
            age = t - offset
            if age < 0:
                continue
            attack = 1 - math.exp(-age / .0018)
            for ratio, level, decay in modes:
                value += gain * level * attack * math.exp(-age / decay) * math.sin(math.tau * pitch * ratio * age)
        signal.append(value)
    return signal


def paper(count, variant, rng):
    body = band_noise(rng, count, 420, 3200)
    detail = band_noise(rng, count, 1600, 4700)
    folds = ((.029, .014, 1), (.078, .023, .76), (.137, .019, .49)) if variant == 1 else (
        (.038, .025, .82), (.103, .017, 1), (.169, .027, .36))
    grains = [(rng.uniform(.02, count / RATE - .04), rng.uniform(.0028, .006), rng.uniform(.1, .3)) for _ in range(14)]
    signal = []
    for index in range(count):
        t = index / RATE
        folds_envelope = sum(gain * bump(t, centre, width) for centre, width, gain in folds)
        grain_envelope = sum(gain * bump(t, centre, width) for centre, width, gain in grains)
        flutter = .84 + .1 * math.sin(math.tau * 37 * t) + .06 * math.sin(math.tau * 59 * t + .8)
        signal.append(body[index] * folds_envelope * flutter + detail[index] * grain_envelope * .24)
    return signal


def snow(count, variant, rng):
    cushion = band_noise(rng, count, 110, 1650)
    crystals = band_noise(rng, count, 1250, 3600)
    grains = [(rng.uniform(.02, .14 if variant == 1 else .18), rng.uniform(.004, .009), rng.uniform(.08, .18)) for _ in range(16)]
    signal = []
    for index in range(count):
        t = index / RATE
        sweep = bump(t, .060 if variant == 1 else .078, .037 if variant == 1 else .042)
        sweep += .44 * bump(t, .153 if variant == 1 else .184, .035)
        sparkle = sum(gain * bump(t, centre, width) for centre, width, gain in grains)
        thump = .014 * math.sin(math.tau * (130 if variant == 1 else 113) * t) * bump(t, .035, .018)
        signal.append(cushion[index] * sweep + crystals[index] * sparkle * .21 + thump)
    return signal


def synthesize(spec):
    name, _, seconds, seed, _, _ = spec
    rng = random.Random(seed)
    count = round(seconds * RATE)
    kind, number = name.split("-")
    signal = {"bell": bell, "paper": paper, "snow": snow}[kind](count, int(number), rng)
    signal = lowpass(signal, 6200, 2)
    dc = lowpass(signal, 42)
    signal = [math.tanh((sample - bias) * 1.15) for sample, bias in zip(signal, dc)]
    window = fade_window(count, .006 if kind == "bell" else .008, .035 if kind == "bell" else .026)
    signal = [sample * gain for sample, gain in zip(signal, window)]
    bias = sum(signal) / sum(window)
    signal = [sample - bias * gain for sample, gain in zip(signal, window)]
    gain = 10 ** (TARGET_PEAK_DB / 20) / max(abs(value) for value in signal)
    pcm = [round(max(-1, min(1, value * gain)) * 32767) for value in signal]
    pcm[0] = pcm[-1] = 0
    return pcm


def pcm_bytes(samples):
    return struct.pack("<" + "h" * len(samples), *samples)


def write_wave(path, samples):
    with wave.open(str(path), "wb") as stream:
        stream.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        stream.writeframes(pcm_bytes(samples))


def read_wave(path):
    with wave.open(str(path), "rb") as stream:
        header = {"sample_rate": stream.getframerate(), "channels": stream.getnchannels(), "bits_per_sample": stream.getsampwidth() * 8,
                  "compression": stream.getcomptype(), "frames": stream.getnframes()}
        data = stream.readframes(header["frames"])
    if header["bits_per_sample"] != 16:
        raise ValueError(f"{path.name}: expected 16-bit PCM")
    samples = array("h")
    samples.frombytes(data)
    if sys.byteorder != "little":
        samples.byteswap()
    return header, list(samples), data


def db(value):
    return 20 * math.log10(max(value, 1e-12))


def rms(values):
    return math.sqrt(sum(value * value for value in values) / len(values))


def analyse(spec, path):
    name, _, _, _, _, bounds = spec
    header, pcm, raw = read_wave(path)
    values = [sample / 32768 for sample in pcm]
    peak, mean = max(abs(value) for value in values), sum(values) / len(values)
    edge = round(.001 * RATE)
    lower = lowpass(values, 8000, 2)
    residual = rms([a - b for a, b in zip(values, lower)])
    metrics = {**header, "seconds": len(pcm) / RATE, "peak_dbfs": db(peak), "rms_dbfs": db(rms(values)), "dc_mean": mean,
               "first_sample": pcm[0], "last_sample": pcm[-1], "first_1ms_rms_dbfs": db(rms(values[:edge])),
               "last_1ms_rms_dbfs": db(rms(values[-edge:])), "max_sample_step": max(abs(a - b) for a, b in zip(values, values[1:])),
               "clipped_samples": sum(abs(value) >= 32767 for value in pcm), "highpass_residual_rms_dbfs": db(residual),
               "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "pcm_sha256": hashlib.sha256(raw).hexdigest()}
    checks = {
        "format": header["sample_rate"] == RATE and header["channels"] == 1 and header["bits_per_sample"] == 16 and header["compression"] == "NONE",
        "length": bounds[0] <= metrics["seconds"] <= bounds[1],
        "headroom": metrics["peak_dbfs"] <= -3,
        "finite_nonzero_energy": math.isfinite(metrics["rms_dbfs"]) and -40 < metrics["rms_dbfs"] < -7,
        "dc_bias": abs(mean) <= .0001,
        "silent_endpoints": pcm[0] == pcm[-1] == 0,
        "faded_endpoints": metrics["first_1ms_rms_dbfs"] < -38 and metrics["last_1ms_rms_dbfs"] < -55,
        "no_clipping": metrics["clipped_samples"] == 0,
        "bounded_sample_steps": metrics["max_sample_step"] < .3,
        "reproducible_pcm": raw == pcm_bytes(synthesize(spec)),
    }
    return {"id": name, "metrics": metrics, "checks": checks, "passed": all(checks.values())}


def pair_correlations(output):
    pairs = []
    for first, second in (("bell-01", "bell-02"), ("paper-01", "paper-02"), ("snow-01", "snow-02")):
        _, a, _ = read_wave(output / (first + ".wav"))
        _, b, _ = read_wave(output / (second + ".wav"))
        count = min(len(a), len(b))
        correlation = sum(x * y for x, y in zip(a, b)) / math.sqrt(sum(x * x for x in a[:count]) * sum(y * y for y in b[:count]))
        pairs.append({"ids": [first, second], "zero_lag_correlation": correlation, "passed": abs(correlation) < .85})
    return pairs


def waveform_svg(output):
    colours = {"bell": "#B48022", "paper": "#A7484D", "snow": "#29756A"}
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="850" viewBox="0 0 1200 850">',
             '<rect width="1200" height="850" fill="#FFF8EA"/>',
             '<g font-family="Segoe UI, sans-serif" fill="#243F3B">',
             '<text x="40" y="44" font-size="26" font-weight="700">Winter House Party · original sound bank</text>',
             '<text x="40" y="72" font-size="16">44.1 kHz mono PCM · −4.5 dBFS peak target · raw waveform, 0–450 ms per row</text>']
    for row, spec in enumerate(SPECS):
        name, _, _, _, description, _ = spec
        _, samples, _ = read_wave(output / (name + ".wav"))
        y = 138 + row * 116
        parts.append(f'<text x="40" y="{y - 18}" font-size="20" font-weight="700">{name}</text>')
        parts.append(f'<text x="40" y="{y + 9}" font-size="12">{spec[2] * 1000:.0f} ms</text>')
        parts.append(f'<line x1="205" x2="1160" y1="{y}" y2="{y}" stroke="#D8D2C3"/>')
        for index in range(0, len(samples), 44):
            group = samples[index:index + 44]
            x = 205 + index / RATE / .45 * 955
            low, high = min(group) / 32768, max(group) / 32768
            parts.append(f'<path d="M{x:.2f},{y - high * 53:.2f}V{y - low * 53:.2f}" stroke="{colours[name.split("-")[0]]}"/>')
        parts.append(f'<text x="205" y="{y + 51}" font-size="14">{description}</text>')
    parts.append('<text x="40" y="835" font-size="14">Static validation is not a substitute for listening in the game mix.</text></g></svg>')
    (output / "waveforms.svg").write_text("\n".join(parts), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check", action="store_true", help="Read and validate existing files without writing anything")
    args = parser.parse_args()
    output = args.output.resolve()
    if output == REPO / "Assets" or REPO / "Assets" in output.parents:
        parser.error("Source synthesis must stay outside Assets; Unity import is a separate stage.")
    if not args.check:
        output.mkdir(parents=True, exist_ok=True)
        for spec in SPECS:
            write_wave(output / (spec[0] + ".wav"), synthesize(spec))
    results = [analyse(spec, output / (spec[0] + ".wav")) for spec in SPECS]
    pairs = pair_correlations(output)
    passed = all(result["passed"] for result in results) and all(pair["passed"] for pair in pairs)
    validation = {"schema": 1, "passed": passed, "sample_policy": "44.1 kHz mono signed 16-bit PCM; peak <= -3 dBFS; bounded durations; fades and DC checked",
                  "clips": results, "variant_pairs": pairs, "listening_review": "Pending native audio capture and listening; no audible-quality claim from numeric checks"}
    if not args.check:
        generator_hash = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
        manifest = {"schema": 1, "collection": "Winter House Party", "status": "source-generated-and-statically-validated" if passed else "validation-failed",
                    "created_utc": datetime.now(timezone.utc).isoformat(), "generator": "Tools/AssetPipeline/build_winter_audio.py",
                    "generator_sha256": generator_hash, "python": sys.version.split()[0],
                    "provenance": {"method": "Original local deterministic additive and filtered-noise synthesis", "third_party_audio": [],
                                   "provider_jobs": [], "source_recordings": [], "license": "No third-party samples; original project-authored output. Project owner determines distribution terms.",
                                   "processing": ["Deterministic seeded oscillators/noise", "Bounded low-pass filters", "42 Hz DC control", "Soft transient saturation",
                                                  "Cosine attack/release", "Weighted DC correction", "Peak normalization -4.5 dBFS", "Signed 16-bit PCM quantization"]},
                    "clips": [{"id": spec[0], "file": spec[0] + ".wav", "cue": spec[1], "seed": spec[3], "description": spec[4],
                               "sha256": result["metrics"]["sha256"], "seconds": result["metrics"]["seconds"]} for spec, result in zip(SPECS, results)],
                    "validation": "validation.json", "waveform_preview": "waveforms.svg", "runtime_integration": "INTEGRATION.md",
                    "stages": {"generated": True, "static_validated": passed, "unity_imported": False, "runtime_integrated": False, "native_listening_reviewed": False}}
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        (output / "validation.json").write_text(json.dumps(validation, indent=2) + "\n", encoding="utf-8")
        waveform_svg(output)
    else:
        manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
        passed &= manifest["generator_sha256"] == hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
        passed &= [entry["id"] for entry in manifest["clips"]] == [spec[0] for spec in SPECS]
        passed &= all(entry["sha256"] == result["metrics"]["sha256"] for entry, result in zip(manifest["clips"], results))
    print(json.dumps({"passed": passed, "clips": [{"id": result["id"], "seconds": result["metrics"]["seconds"],
                      "peak_dbfs": round(result["metrics"]["peak_dbfs"], 2), "rms_dbfs": round(result["metrics"]["rms_dbfs"], 2),
                      "failed_checks": [name for name, ok in result["checks"].items() if not ok]} for result in results]}, indent=2))
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
