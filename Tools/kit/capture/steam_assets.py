"""steam_assets.py: render and validate the Steam store and library image set (Pillow).

    uv run --project Tools/kit/capture Tools/kit/capture/steam_assets.py validate <dir> [--no-screenshots] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/steam_assets.py render --key-art <png> --logo <png> --out <dir>
        [--icon <png>] [--focus X,Y] [--json]

Result (--json): {ok, assets:[{name, file, size:[w,h], ok, failures[]}], …}; exit 1 on any failure.

Sizes (VERIFIED at partner.steamgames.com/doc/store/assets, research_ship.md §3.2, verified_on 2026-09-29):
header capsule 920x430, small 462x174, main 1232x706, vertical 748x896, page background 1438x810 (optional),
library capsule 600x900, library hero 3840x1240 PNG with no logo, library logo PNG 1280 wide and/or 720 tall,
library header 920x430, shortcut icon 256x256 .ico/.png, app icon 184x184 .jpg, event cover 800x450 and event
header 1920x622 (both only for events). Screenshots: at least 5, 1920x1080 minimum, 16:9, gameplay only.
Description images: each under 5 MB, the section under 15 MB (decimal MB, the strict reading).
Capsule rule (since 1 Sep 2022): artwork, the game name and an official subtitle only. `render` therefore takes no
text at all: every capsule is key art plus the logo, the hero is art only, and steam_assets.json records the layers
so `validate` can refuse anything else. A human still approves every crop and checks the small capsule at 50% zoom
(_preview/small_capsule_50pct.png), picks gameplay-only screenshots, and marks at least 4 as suitable for all ages
in Steamworks.

Folder layout read by `validate`: <name>.png|.jpg (names below), screenshots/*.png|jpg, description/*.gif|png|jpg,
optional steam_assets.json (written by `render`).
"""
from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import capture_common as cc  # noqa: E402

VERIFIED_ON = "2026-09-29"
# name, size ([w,h] or "logo"), formats, logo policy, required, logo box (max w frac, max h frac, centre x, centre y)
SPEC = [
    ("header_capsule", [920, 430], ("png", "jpg"), "required", True, (0.60, 0.55, 0.5, 0.5)),
    ("small_capsule", [462, 174], ("png", "jpg"), "required", True, (0.82, 0.72, 0.5, 0.5)),
    ("main_capsule", [1232, 706], ("png", "jpg"), "required", True, (0.55, 0.45, 0.5, 0.5)),
    ("vertical_capsule", [748, 896], ("png", "jpg"), "required", True, (0.80, 0.28, 0.5, 0.80)),
    ("page_background", [1438, 810], ("png", "jpg"), "none", False, None),
    ("library_capsule", [600, 900], ("png", "jpg"), "required", True, (0.80, 0.28, 0.5, 0.80)),
    ("library_hero", [3840, 1240], ("png",), "forbidden", True, None),
    ("library_logo", "logo", ("png",), "only", True, None),
    ("library_header", [920, 430], ("png", "jpg"), "required", True, (0.60, 0.55, 0.5, 0.5)),
    ("shortcut_icon", [256, 256], ("ico", "png"), "icon", True, (0.80, 0.80, 0.5, 0.5)),
    ("app_icon", [184, 184], ("jpg",), "icon", True, (0.80, 0.80, 0.5, 0.5)),
    ("event_cover", [800, 450], ("png", "jpg"), "required", False, (0.60, 0.55, 0.5, 0.5)),
    ("event_header", [1920, 622], ("png", "jpg"), "required", False, (0.40, 0.60, 0.5, 0.5)),
]
SCREENSHOT_MIN = (1920, 1080)
SCREENSHOT_COUNT = 5
DESC_IMAGE_MAX = 5_000_000
DESC_TOTAL_MAX = 15_000_000
ALLOWED_LAYERS = {"art", "logo"}
FORMAT_OF = {"PNG": "png", "JPEG": "jpg", "ICO": "ico", "GIF": "gif", "WEBP": "webp", "BMP": "bmp"}


def spec_by_name() -> dict:
    return {s[0]: s for s in SPEC}


def image_facts(path: Path) -> dict:
    from PIL import Image  # noqa: PLC0415

    with Image.open(path) as im:
        alpha = "transparency" in im.info
        if im.mode in ("RGBA", "LA", "PA"):
            alpha = im.getchannel("A").getextrema()[0] < 255  # an alpha channel that is fully opaque does not count
        facts = {"format": FORMAT_OF.get(im.format or "", (im.format or "?").lower()), "size": [im.width, im.height],
                 "mode": im.mode, "alpha": alpha}
        if im.format == "ICO":
            facts["ico_sizes"] = sorted({tuple(s) for s in im.info.get("sizes", set())})
    return facts


def check_asset(spec, path: Path) -> list[str]:
    name, size, formats, logo, _req, _box = spec
    failures = []
    try:
        f = image_facts(path)
    except Exception as e:  # noqa: BLE001
        return [f"unreadable: {type(e).__name__}: {e}"]
    ext = path.suffix.lower().lstrip(".").replace("jpeg", "jpg")
    if f["format"] not in formats or ext not in formats:
        failures.append(f"format: {f['format']} (.{ext}); Steam wants {' or '.join(formats)}")
    if f["format"] != ext and f["format"] in FORMAT_OF.values():
        failures.append(f"extension .{ext} does not match the {f['format']} content")
    w, h = f["size"]
    if size == "logo":
        if not ((w == 1280 and h <= 720) or (h == 720 and w <= 1280)):
            failures.append(f"size: {w}x{h}; the library logo is 1280 px wide and/or 720 px tall (fits 1280x720)")
        if not f["alpha"]:
            failures.append("alpha: the library logo must be a transparent PNG (it is drawn over the hero)")
    elif name == "shortcut_icon" and f["format"] == "ico":
        if (256, 256) not in [tuple(s) for s in f.get("ico_sizes", [])]:
            failures.append(f"size: the .ico holds {f.get('ico_sizes')}, needs 256x256")
    elif [w, h] != size:
        failures.append(f"size: {w}x{h}, expected {size[0]}x{size[1]}")
    return failures


def validate_dir(d: Path, screenshots: bool = True, only: set[str] | None = None, ws: Path | None = None) -> dict:
    import json  # noqa: PLC0415

    assets, notes = [], []
    manifest = None
    mpath = d / "steam_assets.json"
    if mpath.is_file():
        try:
            manifest = json.loads(mpath.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError) as e:
            assets.append({"name": "steam_assets.json", "file": cc.rel_path(mpath, ws), "size": None, "ok": False,
                           "failures": [f"unreadable manifest: {e}"]})
    layers_of = {a.get("name"): a.get("layers", []) for a in (manifest or {}).get("assets", [])} if manifest else {}
    known = set()
    for spec in SPEC:
        name, size, formats, logo, required, _ = spec
        if only is not None and name not in only:
            continue
        cands = [d / f"{name}.{ext}" for ext in formats + ("png", "jpg", "jpeg", "ico", "gif", "webp")]
        found = next((c for c in cands if c.is_file()), None)
        for c in cands:
            known.add(c.name.lower())
        if found is None:
            if required:
                assets.append({"name": name, "file": None, "size": None if size == "logo" else size, "ok": False,
                               "failures": [f"missing: {name}.{formats[0]}"]})
            continue
        fails = check_asset(spec, found)
        layers = layers_of.get(name)
        if layers is not None:
            extra = sorted(set(layers) - ALLOWED_LAYERS)
            if extra:
                fails.append(f"layers: {extra} not allowed; capsules carry artwork and the logo only")
            if logo == "forbidden" and "logo" in layers:
                fails.append("layers: the library hero must not carry the logo")
            if logo == "none" and "logo" in layers:
                fails.append("layers: the page background is artwork only")
        try:
            got = image_facts(found)["size"]
        except Exception:  # noqa: BLE001
            got = None
        assets.append({"name": name, "file": cc.rel_path(found, ws), "size": got, "ok": not fails, "failures": fails})
    shots = 0
    if screenshots and only is None:
        sd = d / "screenshots"
        files = sorted([p for p in sd.iterdir() if p.is_file() and p.suffix.lower() in (".png", ".jpg", ".jpeg")]) if sd.is_dir() else []
        shots = len(files)
        for p in files:
            fails = []
            try:
                w, h = image_facts(p)["size"]
                if w < SCREENSHOT_MIN[0] or h < SCREENSHOT_MIN[1]:
                    fails.append(f"size: {w}x{h} below the 1920x1080 minimum")
                if w * 9 != h * 16:
                    fails.append(f"aspect: {w}x{h} is not 16:9")
            except Exception as e:  # noqa: BLE001
                w = h = None
                fails.append(f"unreadable: {e}")
            assets.append({"name": "screenshot", "file": cc.rel_path(p, ws), "size": [w, h] if w else None,
                           "ok": not fails, "failures": fails})
        count_fail = [] if shots >= SCREENSHOT_COUNT else [f"count: {shots} screenshots, Steam needs at least {SCREENSHOT_COUNT}"]
        assets.append({"name": "screenshots", "file": cc.rel_path(sd, ws), "size": None, "ok": not count_fail,
                       "failures": count_fail})
        notes.append("screenshots must show gameplay only (no concept art, pre-rendered cinematic stills or awards); "
                      "mark at least 4 as suitable for all ages in Steamworks (human)")
    desc_total = 0
    dd = d / "description"
    if dd.is_dir() and only is None:
        for p in sorted(dd.iterdir()):
            if not p.is_file():
                continue
            b = p.stat().st_size
            desc_total += b
            fails = [f"bytes: {b} over the 5 MB per-image limit"] if b >= DESC_IMAGE_MAX else []
            assets.append({"name": "description_image", "file": cc.rel_path(p, ws), "size": None, "ok": not fails,
                           "failures": fails})
        if desc_total >= DESC_TOTAL_MAX:
            assets.append({"name": "description", "file": cc.rel_path(dd, ws), "size": None, "ok": False,
                           "failures": [f"bytes: {desc_total} total over the 15 MB section limit"]})
    unknown = sorted(p.name for p in d.iterdir() if p.is_file() and p.name.lower() not in known
                     and p.name != "steam_assets.json") if d.is_dir() else []
    return {"ok": all(a["ok"] for a in assets), "dir": cc.rel_path(d, ws), "assets": assets, "screenshots": shots,
            "description_bytes": desc_total, "unknown_files": unknown, "notes": notes, "verified_on": VERIFIED_ON}


# ---------------------------------------------------------------- render

def cover(img, w: int, h: int, focus=(0.5, 0.5)):
    from PIL import Image  # noqa: PLC0415

    scale = max(w / img.width, h / img.height)
    nw, nh = max(w, round(img.width * scale)), max(h, round(img.height * scale))
    big = img.resize((nw, nh), Image.Resampling.LANCZOS)
    cx, cy = focus[0] * nw, focus[1] * nh
    x0 = int(min(max(cx - w / 2, 0), nw - w))
    y0 = int(min(max(cy - h / 2, 0), nh - h))
    return big.crop((x0, y0, x0 + w, y0 + h)), scale > 1.0


def place_logo(canvas, logo, box):
    from PIL import Image  # noqa: PLC0415

    mw, mh, cx, cy = box
    W, H = canvas.size
    s = min(mw * W / logo.width, mh * H / logo.height)
    lw, lh = max(1, round(logo.width * s)), max(1, round(logo.height * s))
    lg = logo.resize((lw, lh), Image.Resampling.LANCZOS)
    x = int(round(cx * W - lw / 2))
    y = int(round(min(max(cy * H - lh / 2, 0), H - lh)))
    canvas.alpha_composite(lg, (max(0, x), y))
    return canvas


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def render(key_art: Path, logo_path: Path, out: Path, icon: Path | None, focus, ws: Path | None) -> dict:
    from PIL import Image, ImageEnhance, ImageFilter  # noqa: PLC0415

    import contact_sheet  # noqa: PLC0415

    with Image.open(key_art) as im:
        art = im.convert("RGBA")
    with Image.open(logo_path) as im:
        logo_has_alpha = im.mode in ("RGBA", "LA", "PA") or "transparency" in im.info
        logo = im.convert("RGBA")
    icon_art = None
    if icon is not None:
        with Image.open(icon) as im:
            icon_art = im.convert("RGBA")
    bbox = logo.getbbox()
    if bbox:
        logo = logo.crop(bbox)
    warnings = []
    if art.width < 3840 or art.height < 2160:
        warnings.append(f"key art is {art.width}x{art.height}; 3840x2160 or larger avoids upscaling the hero")
    if not logo_has_alpha:
        warnings.append("logo has no transparency; it will cover the artwork as a solid block")
    out.mkdir(parents=True, exist_ok=True)
    manifest = {"schema": 1, "utc": cc.utc_iso(), "verified_on": VERIFIED_ON,
                "key_art": {"path": cc.rel_path(key_art, ws), "sha256": sha(key_art), "size": [art.width, art.height]},
                "logo": {"path": cc.rel_path(logo_path, ws), "sha256": sha(logo_path), "size": [logo.width, logo.height]},
                "assets": []}
    rendered = []
    for name, size, formats, policy, _req, box in SPEC:
        if size == "logo":
            if logo.width * 720 >= logo.height * 1280:  # wide logo: exactly 1280 wide
                s = 1280 / logo.width
                lw, lh = 1280, min(720, max(1, round(logo.height * s)))
            else:  # tall logo: exactly 720 tall
                s = 720 / logo.height
                lw, lh = min(1280, max(1, round(logo.width * s))), 720
            img = logo.resize((lw, lh), Image.Resampling.LANCZOS)
            layers, upscaled = ["logo"], s > 1.0
        else:
            w, h = size
            src = icon_art if (policy == "icon" and icon_art is not None) else art
            img, upscaled = cover(src, w, h, focus if src is art else (0.5, 0.5))
            layers = ["art"]
            if policy == "none":
                img = ImageEnhance.Brightness(img.convert("RGB")).enhance(0.45).filter(ImageFilter.GaussianBlur(2)).convert("RGBA")
            if policy in ("required",) or (policy == "icon" and icon_art is None):
                img = place_logo(img, logo, box)
                layers.append("logo")
        fmt = formats[0]
        path = out / f"{name}.{fmt}"
        if fmt == "jpg":
            cc.save_image_atomic(img.convert("RGB"), path, format="JPEG", quality=92)
        elif fmt == "ico":
            cc.save_image_atomic(img, path, format="ICO", sizes=[(256, 256)])
        elif name == "library_logo":
            cc.save_image_atomic(img, path, format="PNG", optimize=True)
        else:
            cc.save_image_atomic(img.convert("RGB"), path, format="PNG", optimize=True)
        manifest["assets"].append({"name": name, "file": path.name, "size": [img.width, img.height], "layers": layers,
                                   "upscaled": bool(upscaled)})
        rendered.append(path)
        if upscaled and name not in ("library_logo",):
            warnings.append(f"{name}: key art was upscaled")
    cc.write_json_atomic(out / "steam_assets.json", manifest)
    prev_dir = out / "_preview"
    prev_dir.mkdir(exist_ok=True)
    with Image.open(out / "small_capsule.png") as sc_img:
        half = sc_img.resize((sc_img.width // 2, sc_img.height // 2), Image.Resampling.LANCZOS)
    small50 = prev_dir / "small_capsule_50pct.png"
    cc.save_image_atomic(half, small50, format="PNG")
    items = [(p, p.stem.replace("_", " ")) for p in rendered] + [(small50, "small capsule at 50% (must still read)")]
    sheet, _cells, _unr, _geo = contact_sheet.build_sheet(items, 4, 420, True, None, "Steam image set: human approves every crop")
    preview = prev_dir / "preview.png"
    cc.save_image_atomic(sheet, preview, format="PNG", optimize=True)
    rendered_names = {s[0] for s in SPEC}
    res = validate_dir(out, screenshots=False, only=rendered_names, ws=ws)
    res.update({"preview": cc.rel_path(preview, ws), "small_capsule_50pct": cc.rel_path(small50, ws),
                "manifest": cc.rel_path(out / "steam_assets.json", ws), "warnings": warnings})
    return res


def parse_focus(s: str | None):
    if not s:
        return (0.5, 0.5)
    try:
        x, y = (float(v) for v in s.split(","))
    except ValueError as e:
        raise cc.UsageError("--focus is X,Y in 0..1, e.g. 0.5,0.4") from e
    if not (0 <= x <= 1 and 0 <= y <= 1):
        raise cc.UsageError("--focus is X,Y in 0..1")
    return (x, y)


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="steam_assets.py", description="render/validate Steam store images")
    sub = ap.add_subparsers(dest="cmd", required=True)
    v = sub.add_parser("validate")
    v.add_argument("dir")
    v.add_argument("--no-screenshots", action="store_true", help="skip the screenshots/ checks")
    v.add_argument("--json", action="store_true")
    r = sub.add_parser("render")
    r.add_argument("--key-art", required=True)
    r.add_argument("--logo", required=True)
    r.add_argument("--out", required=True)
    r.add_argument("--icon", help="square art for the two icons (default: key art plus logo)")
    r.add_argument("--focus", help="X,Y focus point for crops in 0..1 (default 0.5,0.5)")
    r.add_argument("--json", action="store_true")
    return ap.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    ws = cc.find_workspace()
    if args.cmd == "validate":
        d = Path(args.dir)
        if not d.is_dir():
            raise cc.UsageError(f"not a folder: {args.dir}")
        res = validate_dir(d, screenshots=not args.no_screenshots, ws=ws)
    else:
        for label, p in (("--key-art", args.key_art), ("--logo", args.logo), ("--icon", args.icon)):
            if p and not Path(p).is_file():
                raise cc.UsageError(f"{label} not found: {p}")
        res = render(Path(args.key_art), Path(args.logo), Path(args.out), Path(args.icon) if args.icon else None,
                     parse_focus(args.focus), ws)
    lines = [f"{'ok  ' if a['ok'] else 'FAIL'} {a['name']:18s} {a['file'] or '-'} "
             + ("" if a["ok"] else "; ".join(a["failures"])) for a in res["assets"]]
    lines += [f"note: {n}" for n in res.get("notes", [])] + [f"warn: {w}" for w in res.get("warnings", [])]
    lines.append("ok" if res["ok"] else "FAIL")
    cc.emit(res, args.json, lines)
    return cc.EXIT_OK if res["ok"] else cc.EXIT_FAIL


if __name__ == "__main__":
    sys.exit(cc.run_main(main))
