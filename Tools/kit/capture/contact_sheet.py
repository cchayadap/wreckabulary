"""contact_sheet.py: labelled review grids of screenshots or frames (Pillow, no ffmpeg).

    uv run --project Tools/kit/capture Tools/kit/capture/contact_sheet.py --in <file|folder|glob>... --out <png|jpg>
        [--cols N] [--labels] [--anchor <png>] [--cell-width 480] [--select all|first-last|every:N|last:N]
        [--dedupe BITS] [--max-cells 200] [--title TEXT] [--json]

Result (--json): {ok, out, cells, index, cols, rows, size:[w,h], skipped_duplicates, unreadable[]}.
- Inputs sort by file name (progress captures are named by UTC, so this is time order); globs are expanded here, so
  they work from PowerShell too.
- --labels writes each file's caption under its cell (`<UTC>__<caption>.png` → "caption · UTC").
- --anchor puts an approved reference in a highlighted panel on the left of the grid, for side-by-side review.
- --select first-last makes a before/after pair; every:N keeps every Nth frame (one frame per 2 s of a 30 fps
  sequence is every:60); last:N keeps the newest N.
- --dedupe BITS drops a frame whose 64-bit difference hash is within BITS of the previous kept frame (unchanged views).
- Full-resolution links: <out stem>.index.json maps every cell (row, col, pixel box) to its source file.
"""
from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import capture_common as cc  # noqa: E402

BG = (24, 24, 28)
CELL_BG = (40, 40, 46)
FG = (230, 230, 230)
ANCHOR = (245, 176, 65)
PAD = 8
LABEL_H = 26
TITLE_H = 40
OUT_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
STAMP_RE = re.compile(r"^(\d{8}T\d{6}Z|\d{8}-\d{6}|\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}Z?)__(.+)$")


def label_for(path: Path) -> str:
    m = STAMP_RE.match(path.stem)
    if m:
        return f"{m.group(2).replace('_', ' ')} · {m.group(1)}"
    return path.stem


def select(files: list[Path], how: str) -> list[Path]:
    if how in ("all", ""):
        return files
    if how == "first-last":
        return files[:1] + files[-1:] if len(files) > 1 else files
    m = re.fullmatch(r"(every|last):(\d+)", how)
    if not m or int(m.group(2)) < 1:
        raise cc.UsageError("--select is all, first-last, every:N or last:N")
    n = int(m.group(2))
    if m.group(1) == "every":
        keep = files[::n]
        if files and files[-1] not in keep:
            keep.append(files[-1])
        return keep
    return files[-n:]


def fit(img, w: int, h: int):
    from PIL import Image  # noqa: PLC0415

    im = img.convert("RGB")
    scale = min(w / im.width, h / im.height)
    nw, nh = max(1, round(im.width * scale)), max(1, round(im.height * scale))
    return im.resize((nw, nh), Image.Resampling.LANCZOS)


def truncate(draw, text: str, font, width: int) -> str:
    if draw.textlength(text, font=font) <= width:
        return text
    while text and draw.textlength(text + "…", font=font) > width:
        text = text[:-1]
    return text + "…"


def build_sheet(items: list[tuple[Path, str]], cols: int, cell_w: int, labels: bool, anchor: Path | None = None,
                title: str | None = None):
    """Render the grid. items: (path, label). Returns (image, cells, unreadable, geometry)."""
    from PIL import Image, ImageDraw  # noqa: PLC0415

    opened, unreadable = [], []
    for p, lab in items:
        try:
            with Image.open(p) as im:
                im.load()
                opened.append((p, lab, im.copy()))
        except Exception as e:  # noqa: BLE001 - a broken file becomes a placeholder cell, not a crash
            unreadable.append({"path": str(p), "error": f"{type(e).__name__}: {e}"})
            opened.append((p, lab, None))
    first = next((im for _, _, im in opened if im is not None), None)
    aspect = (first.height / first.width) if first is not None else 9 / 16
    aspect = min(max(aspect, 0.25), 4.0)
    cell_h = max(16, round(cell_w * aspect))
    n = len(opened)
    cols = max(1, min(cols, n)) if n else 1
    rows = max(1, math.ceil(n / cols))
    lab_h = LABEL_H if labels else 0
    title_h = TITLE_H if title else 0
    anchor_img = None
    if anchor is not None:
        with Image.open(anchor) as im:
            im.load()
            anchor_img = im.copy()
    left = (cell_w + PAD * 3) if anchor_img is not None else 0
    width = left + PAD + cols * (cell_w + PAD)
    height = title_h + PAD + rows * (cell_h + lab_h + PAD)
    if anchor_img is not None:
        height = max(height, title_h + PAD + cell_h + LABEL_H + PAD * 2)
    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)
    font = cc.load_font(15)
    if title:
        draw.text((PAD, 10), truncate(draw, title, cc.load_font(20), width - 2 * PAD), fill=FG, font=cc.load_font(20))
    if anchor_img is not None:
        ax, ay = PAD, title_h + PAD
        draw.rectangle([ax - 3, ay - 3, ax + cell_w + 2, ay + cell_h + 2], outline=ANCHOR, width=3)
        draw.rectangle([ax, ay, ax + cell_w - 1, ay + cell_h - 1], fill=CELL_BG)
        th = fit(anchor_img, cell_w, cell_h)
        sheet.paste(th, (ax + (cell_w - th.width) // 2, ay + (cell_h - th.height) // 2))
        draw.text((ax, ay + cell_h + 6), truncate(draw, f"ANCHOR · {anchor.name}", font, cell_w), fill=ANCHOR, font=font)
    cells = []
    for i, (p, lab, im) in enumerate(opened):
        r, c = divmod(i, cols)
        x = left + PAD + c * (cell_w + PAD)
        y = title_h + PAD + r * (cell_h + lab_h + PAD)
        draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], fill=CELL_BG)
        size = None
        if im is not None:
            th = fit(im, cell_w, cell_h)
            sheet.paste(th, (x + (cell_w - th.width) // 2, y + (cell_h - th.height) // 2))
            size = [im.width, im.height]
        else:
            draw.line([x, y, x + cell_w - 1, y + cell_h - 1], fill=(200, 60, 60), width=3)
            draw.line([x, y + cell_h - 1, x + cell_w - 1, y], fill=(200, 60, 60), width=3)
        if labels:
            draw.text((x + 2, y + cell_h + 5), truncate(draw, f"{i + 1}. {lab}", font, cell_w - 4), fill=FG, font=font)
        cells.append({"i": i, "path": str(p), "label": lab, "row": r, "col": c, "box": [x, y, cell_w, cell_h], "size": size})
    return sheet, cells, unreadable, {"cols": cols, "rows": rows, "cell": [cell_w, cell_h]}


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="contact_sheet.py", description="labelled image grid for reviews")
    ap.add_argument("--in", dest="inputs", nargs="+", action="extend", required=True, help="files, folders or globs")
    ap.add_argument("--out", required=True, help="output .png or .jpg")
    ap.add_argument("--cols", type=int, help="columns (default: about square)")
    ap.add_argument("--labels", action="store_true", help="caption under every cell")
    ap.add_argument("--anchor", help="reference image shown beside the grid")
    ap.add_argument("--cell-width", type=int, default=480)
    ap.add_argument("--select", default="all", help="all | first-last | every:N | last:N")
    ap.add_argument("--dedupe", type=int, default=-1, metavar="BITS", help="skip frames within BITS of the previous one")
    ap.add_argument("--max-cells", type=int, default=200)
    ap.add_argument("--title")
    ap.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def dedupe(files: list[Path], bits: int) -> tuple[list[Path], list[str]]:
    from PIL import Image  # noqa: PLC0415

    kept, skipped, last = [], [], None
    for f in files:
        try:
            with Image.open(f) as im:
                h = cc.dhash(im)
        except Exception:  # noqa: BLE001 - unreadable files are kept and reported by build_sheet
            kept.append(f)
            last = None
            continue
        if last is not None and cc.hamming(h, last) <= bits:
            skipped.append(str(f))
            continue
        kept.append(f)
        last = h
    return kept, skipped


def main(argv):
    args = parse_args(argv)
    out = Path(args.out)
    if out.suffix.lower() not in OUT_EXTS:
        raise cc.UsageError("--out must end in .png, .jpg, .jpeg or .webp")
    if not (64 <= args.cell_width <= 4096):
        raise cc.UsageError("--cell-width must be 64–4096")
    if args.cols is not None and args.cols < 1:
        raise cc.UsageError("--cols must be >= 1")
    if args.anchor and not Path(args.anchor).is_file():
        raise cc.UsageError(f"--anchor not found: {args.anchor}")
    ws = cc.find_workspace()
    files, missing = cc.expand_inputs(args.inputs)
    out_key = str(out.resolve()).lower()
    files = [f for f in files if str(f.resolve()).lower() != out_key]
    if not files:
        raise cc.ToolError("no input images matched: " + ", ".join(args.inputs), cc.EXIT_FAIL, unmatched=missing)
    skipped = []
    if args.dedupe >= 0:
        files, skipped = dedupe(files, args.dedupe)
    files = select(files, args.select)
    subsampled = False
    if len(files) > args.max_cells:
        step = len(files) / args.max_cells
        files = [files[min(len(files) - 1, int(i * step))] for i in range(args.max_cells - 1)] + [files[-1]]
        subsampled = True
    cols = args.cols or max(1, math.ceil(math.sqrt(len(files))))
    items = [(f, label_for(f)) for f in files]
    sheet, cells, unreadable, geo = build_sheet(items, cols, args.cell_width, args.labels,
                                                Path(args.anchor) if args.anchor else None, args.title)
    kw = {"quality": 88} if out.suffix.lower() in (".jpg", ".jpeg", ".webp") else {"optimize": True}
    cc.save_image_atomic(sheet, out, **kw)
    index_path = out.with_name(out.stem + ".index.json")
    for c in cells:
        c["path"] = cc.rel_path(c["path"], ws)
    for u in unreadable:
        u["path"] = cc.rel_path(u["path"], ws)
    cc.write_json_atomic(index_path, {
        "schema": 1, "utc": cc.utc_iso(), "out": cc.rel_path(out, ws), "anchor": cc.rel_path(args.anchor, ws) if args.anchor else None,
        "cols": geo["cols"], "rows": geo["rows"], "cell": geo["cell"], "cells": cells,
    })
    result = {
        "ok": not unreadable, "out": cc.rel_path(out, ws), "cells": len(cells), "index": cc.rel_path(index_path, ws),
        "cols": geo["cols"], "rows": geo["rows"], "size": [sheet.width, sheet.height],
        "skipped_duplicates": len(skipped), "subsampled": subsampled, "unmatched": missing, "unreadable": unreadable,
    }
    cc.emit(result, args.json, [f"wrote {result['out']} ({len(cells)} cells, {geo['cols']}x{geo['rows']}, "
                                f"{sheet.width}x{sheet.height})", f"index {result['index']}"]
            + [f"unreadable: {u['path']}" for u in unreadable] + [f"no match: {m}" for m in missing])
    return cc.EXIT_OK if result["ok"] else cc.EXIT_FAIL


if __name__ == "__main__":
    sys.exit(cc.run_main(main))
