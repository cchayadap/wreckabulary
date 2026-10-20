"""Minimal audio facts for generated audio (stdlib wave). Loudness, true peak and loop clicks are game-audio's
audio_check.py (Tools/kit/audio/); this check only proves the file is what it claims and flags obvious problems."""

import array
import wave

from ..sniff import sniff_file
from . import fail, new_result

EXPECTED_RATE = 48000


def check_audio(path, opts):
    res = new_result("audio", path)
    kind, _ = sniff_file(path)
    res["metrics"]["format"] = kind
    res["warnings"].append("loudness (LUFS), true peak and loop clicks: run Tools/kit/audio/audio_check.py")
    if kind != "wav":
        if kind in ("mp3", "ogg", "m4a"):
            res["warnings"].append(f"{kind} is not measured here; convert or run audio_check.py")
            return res
        return fail(res, "format", f"not an audio file (magic bytes say {kind})", value=kind)
    try:
        with wave.open(str(path), "rb") as w:
            rate, ch, width, frames = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
            raw = w.readframes(frames) if width == 2 else b""
    except (wave.Error, EOFError) as e:
        return fail(res, "opens", f"WAV does not open: {e}")
    res["metrics"].update({"sample_rate": rate, "channels": ch, "sample_width": width, "duration_s": round(frames / float(rate or 1), 4)})
    want = int(opts.get("sample_rate", EXPECTED_RATE))
    if rate != want:
        fail(res, "sample_rate", f"sample rate is not {want} Hz", value=rate, limit=want)
    if raw:
        s = array.array("h")
        s.frombytes(raw)
        if s:
            peak = max(abs(min(s)), abs(max(s)))
            clipped = sum(1 for v in s if v >= 32767 or v <= -32768)
            res["metrics"]["peak"] = round(peak / 32768.0, 5)
            res["metrics"]["clipped_samples"] = clipped
            if clipped > max(1, len(s) // 1000):
                fail(res, "clipping", "more than 0.1% of samples are at full scale", value=clipped)
    return res
