"""frames_to_gif.py: PNG/JPG frames → a looping GIF under a size budget (Pillow, no ffmpeg or gifski).

    uv run --project Tools/kit/capture Tools/kit/capture/frames_to_gif.py --in <folder|glob> --out <gif>
        [--fps 15] [--max-mb 5] [--width 616] [--max-frames 600] [--json]

Result (--json): {ok, out, bytes, fps, frames, width, height, colors, attempts}. Exit 1 when no setting fits the
budget (nothing is written then).
- Steam: every description image must be under 5 MB and the whole About section under 15 MB (VERIFIED,
  research_ship.md §3.2); the budget counts decimal megabytes, the stricter reading. 616 px is the usual description
  column width (CLAIMED), so it is the default and the tool never upscales.
- The encoder shares one palette across all frames (no flicker) and lets Pillow store only the changed rectangle of
  each frame. Over budget, it retries in this order: no dithering, fewer colours (128, 64), narrower frames, then
  every 2nd/4th frame at the same speed, and reports the setting that fit.
- GIF delays are whole centiseconds and browsers clamp very short delays, so fps is limited to 1–50 and the reported
  fps is the one actually stored.
"""
from __future__ import annotations

import argparse
import io
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import capture_common as cc  # noqa: E402

DEFAULT_WIDTH = 616
MAX_ATTEMPTS = 14
FRAME_EXTS = {".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tga", ".tif", ".tiff"}


def load_frames(files: list[Path], width: int, step: int):
    """Every step-th frame at `width`; the first frame sets the height and any frame of another shape is letterboxed
    to it, so a GIF never mixes frame sizes."""
    from PIL import Image, ImageOps  # noqa: PLC0415

    out, size = [], None
    for f in files[::step]:
        with Image.open(f) as im:
            rgb = im.convert("RGB")
        if size is None:
            size = (width, max(1, round(rgb.height * width / rgb.width)))
        if rgb.size != size:
            rgb = ImageOps.pad(rgb, size, method=Image.Resampling.LANCZOS, color=(0, 0, 0))
        out.append(rgb)
    return out


def shared_palette(frames, colors: int):
    """One palette for the whole clip, from up to 16 evenly spaced frames tiled into one image."""
    from PIL import Image  # noqa: PLC0415

    picks = frames[:: max(1, len(frames) // 16)][:16]
    tw = 160
    tiles = [f.resize((tw, max(1, round(f.height * tw / f.width)))) for f in picks]
    th = max(t.height for t in tiles)
    mont = Image.new("RGB", (tw * len(tiles), th))
    for i, t in enumerate(tiles):
        mont.paste(t, (i * tw, 0))
    return mont.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)


def encode(frames, fps: float, step: int, colors: int, dither: bool) -> tuple[bytes, int]:
    from PIL import Image  # noqa: PLC0415

    pal = shared_palette(frames, colors)
    mode = Image.Dither.FLOYDSTEINBERG if dither else Image.Dither.NONE
    q = [f.quantize(palette=pal, dither=mode) for f in frames]
    delay_cs = max(2, round(100.0 * step / fps))
    buf = io.BytesIO()
    q[0].save(buf, format="GIF", save_all=True, append_images=q[1:], duration=delay_cs * 10, loop=0,
              optimize=False, disposal=1)
    return buf.getvalue(), delay_cs


def plan_next(state: dict, size: int, budget: int) -> bool:
    """Mutate state to the next cheaper setting; False when there is nothing left to try."""
    ratio = size / budget
    if state["dither"]:
        state["dither"] = False
    elif state["colors"] > 64 and ratio < 1.6:
        state["colors"] //= 2
    elif state["width"] > 200:
        state["width"] = max(200, int(state["width"] * max(0.55, min(0.9, (1 / ratio) ** 0.5))))
    elif state["step"] < 4:
        state["step"] *= 2
    elif state["colors"] > 16:
        state["colors"] //= 2
    else:
        return False
    return True


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="frames_to_gif.py", description="frames → looping GIF under a size budget")
    ap.add_argument("--in", dest="inputs", nargs="+", action="extend", required=True, help="folder or glob of frames")
    ap.add_argument("--out", required=True)
    ap.add_argument("--fps", type=float, default=15.0)
    ap.add_argument("--max-mb", type=float, default=5.0, help="size budget in decimal MB (default 5)")
    ap.add_argument("--width", type=int, default=DEFAULT_WIDTH, help="output width in px (default 616; never upscales)")
    ap.add_argument("--max-frames", type=int, default=600)
    ap.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    out = Path(args.out)
    if out.suffix.lower() != ".gif":
        raise cc.UsageError("--out must end in .gif")
    if not (1 <= args.fps <= 50):
        raise cc.UsageError("--fps must be 1–50 (GIF delays are centiseconds; browsers clamp faster frames)")
    if args.max_mb <= 0 or args.width < 16 or args.max_frames < 1:
        raise cc.UsageError("--max-mb, --width and --max-frames must be positive (width >= 16)")
    ws = cc.find_workspace()
    files, missing = cc.expand_inputs(args.inputs, FRAME_EXTS)
    if not files:
        raise cc.ToolError("no frames matched: " + ", ".join(args.inputs), cc.EXIT_FAIL, unmatched=missing)
    if len(files) > args.max_frames:
        stride = len(files) / args.max_frames
        files = [files[int(i * stride)] for i in range(args.max_frames)]
    from PIL import Image  # noqa: PLC0415

    with Image.open(files[0]) as im0:
        src_w = im0.width
    budget = int(args.max_mb * 1_000_000)
    state = {"width": min(args.width, src_w), "colors": 256, "step": 1, "dither": True}
    attempts, data, delay_cs = [], b"", 0
    frames_cache: dict[tuple[int, int], list] = {}
    tried, frames = dict(state), []  # the setting (and frames) that `data` was actually encoded with
    for n in range(1, MAX_ATTEMPTS + 1):
        key = (state["width"], state["step"])
        if key not in frames_cache:
            frames_cache.clear()
            frames_cache[key] = load_frames(files, state["width"], state["step"])
        frames = frames_cache[key]
        data, delay_cs = encode(frames, args.fps, state["step"], state["colors"], state["dither"])
        tried = dict(state)
        attempts.append({**tried, "bytes": len(data)})
        cc.note(f"attempt {n}: width {state['width']} colors {state['colors']} step {state['step']} "
                f"dither {state['dither']} -> {len(data)} bytes")
        if len(data) <= budget or n == MAX_ATTEMPTS:
            break  # plan_next only when another attempt will really run
        if not plan_next(state, len(data), budget):
            break
    fits = len(data) <= budget
    result = {
        "ok": fits, "out": cc.rel_path(out, ws), "bytes": len(data), "budget_bytes": budget,
        "fps": round(100.0 / delay_cs, 3) if delay_cs else None, "frames": len(frames),
        "source_frames": len(files), "width": tried["width"], "height": frames[0].height, "colors": tried["colors"],
        "dither": tried["dither"], "frame_step": tried["step"], "attempts": attempts, "unmatched": missing,
    }
    if fits:
        out.parent.mkdir(parents=True, exist_ok=True)
        tmp = out.with_name(f".{out.stem}.tmp.gif")
        tmp.write_bytes(data)
        tmp.replace(out)
    else:
        result["error"] = f"no setting fits {budget} bytes (best {min(a['bytes'] for a in attempts)}); nothing written"
    cc.emit(result, args.json, [f"{'wrote' if fits else 'FAIL'} {result['out']}: {len(data)} bytes, {result['frames']} "
                                f"frames at {result['fps']} fps, {result['width']}x{result['height']}, {tried['colors']} colours"])
    return cc.EXIT_OK if fits else cc.EXIT_FAIL


if __name__ == "__main__":
    sys.exit(cc.run_main(main))
