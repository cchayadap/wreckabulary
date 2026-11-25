import argparse
import json
import os
import sys

import numpy
from PIL import Image, ImageDraw

ALPHA_FLOOR = 128
NEUTRAL_SATURATION = 0.20
CELL = 96
LABEL = 14
WHITE_BY_ROLE = {"PLATE", "BOMB", "MIRROR"}


def stats(path):
    rgba = numpy.asarray(Image.open(path).convert("RGBA"), dtype=numpy.float64) / 255.0
    pixels = rgba[rgba[..., 3] >= ALPHA_FLOOR / 255.0][:, :3]
    if len(pixels) == 0:
        return {"pixels": 0, "meanS": 0.0, "medianS": 0.0, "meanV": 0.0, "neutralShare": 1.0, "meanRGB": [0, 0, 0]}
    high, low = pixels.max(axis=1), pixels.min(axis=1)
    sats = numpy.where(high > 0, (high - low) / numpy.where(high > 0, high, 1), 0.0)
    return {
        "pixels": int(len(pixels)),
        "meanS": round(float(sats.mean()), 3),
        "medianS": round(float(numpy.median(sats)), 3),
        "meanV": round(float(high.mean()), 3),
        "clippedShare": round(float((high >= 0.995).mean()), 3),
        "neutralShare": round(float((sats < NEUTRAL_SATURATION).mean()), 3),
        "meanRGB": [round(float(c), 3) for c in pixels.mean(axis=0)],
    }


def folder_stats(folder):
    names = sorted(name[:-4] for name in os.listdir(folder) if name.endswith(".png"))
    return {name: stats(os.path.join(folder, name + ".png")) for name in names}


def contact_sheet(columns, out):
    words = sorted(set().union(*[set(c[1]) for c in columns]))
    sheet = Image.new("RGBA", (CELL * len(columns), (CELL + LABEL) * len(words)), (244, 238, 226, 255))
    draw = ImageDraw.Draw(sheet)
    for row, word in enumerate(words):
        for col, (folder, data) in enumerate(columns):
            x, y = col * CELL, row * (CELL + LABEL)
            path = os.path.join(folder, word + ".png")
            if os.path.exists(path):
                icon = Image.open(path).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
                sheet.alpha_composite(icon, (x, y))
            s = data.get(word, {}).get("meanS")
            draw.text((x + 4, y + CELL), f"{word} S {s:.2f}" if s is not None else word, fill=(40, 34, 30, 255))
    sheet.convert("RGB").save(out)


def main():
    parser = argparse.ArgumentParser(description="Mean saturation per item icon, with an optional before/after contact sheet.")
    parser.add_argument("folders", nargs="+", help="icon folders; the first is 'before' when two are given")
    parser.add_argument("--sheet", help="write a side-by-side contact sheet PNG here")
    parser.add_argument("--json", help="write the numbers as JSON here")
    parser.add_argument("--min-mean", type=float, default=None, help="exit 1 if an icon outside the white-by-role list has a mean S below this in the last folder")
    args = parser.parse_args()
    columns = [(folder, folder_stats(folder)) for folder in args.folders]
    last = columns[-1][1]
    words = sorted(set().union(*[set(c[1]) for c in columns]))
    header = "WORD".ljust(10) + "".join(f"{'meanS':>8}{'medS':>7}{'neut':>7}{'clip':>7}" for _ in columns) + ("   delta" if len(columns) > 1 else "")
    print(header)
    for word in words:
        row = word.ljust(10)
        for _, data in columns:
            d = data.get(word)
            row += f"{d['meanS']:>8.3f}{d['medianS']:>7.3f}{d['neutralShare']:>7.2f}{d['clippedShare']:>7.2f}" if d else " " * 29
        if len(columns) > 1 and word in columns[0][1] and word in last:
            row += f"{last[word]['meanS'] - columns[0][1][word]['meanS']:>+8.3f}"
        print(row)
    for folder, data in columns:
        values = sorted(d["meanS"] for d in data.values())
        print(f"SUMMARY {folder}: icons {len(values)}, median of mean S {values[len(values) // 2]:.3f}, "
              f"below 0.35: {sum(1 for v in values if v < 0.35)}")
    if args.sheet:
        contact_sheet(columns, args.sheet)
        print("SHEET " + args.sheet)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as handle:
            json.dump({folder: data for folder, data in columns}, handle, indent=1)
    if args.min_mean is not None:
        low = [w for w, d in last.items() if w not in WHITE_BY_ROLE and d["meanS"] < args.min_mean]
        print("ICON_SATURATION " + json.dumps({"minMean": args.min_mean, "below": low}))
        if low:
            sys.exit(1)


if __name__ == "__main__":
    main()
