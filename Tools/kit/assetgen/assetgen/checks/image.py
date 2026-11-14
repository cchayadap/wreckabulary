"""2D checks: image, tiling texture, equirect panorama, icon (Pillow only, no network).

- image: opens and verifies; the real format (magic bytes) matches the extension; optional size, aspect and minimum
  size; optional background uniformity for reference sheets (edge-strip std-dev); palette ΔE (CIEDE2000) and the share of
  pixels in a style-lock reserved hue, as metrics that fail only when a limit is given.
- tile: power-of-two sides; wrap seams: the mean absolute difference between the first and last column (and row) must
  not exceed seam_ratio × the median difference between adjacent interior columns (rows); optional 2×2 proof sheet.
- pano: 2:1 aspect, minimum width (WoO shipped a 256×128 sky), the horizontal wrap seam; pole rows reported.
- icon: a real alpha channel with transparency; halo = semi-transparent pixels outside a 2 px dilation of the opaque
  mask; the object must not touch the canvas border; set consistency of the fill box across a folder.
"""

import math
import statistics
from pathlib import Path

from ..sniff import ext_matches, sniff_file
from . import fail, new_result

try:
    from PIL import Image, ImageChops, ImageFilter, ImageStat
except ImportError:  # pragma: no cover
    Image = None

SEAM_RATIO = 1.5
BG_STD_MAX = 12.0
HALO_MAX = 0.02
PANO_MIN_WIDTH = 2048


# --- colour ---------------------------------------------------------------------------------------------------------
def _srgb_to_lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb_to_lab(rgb):
    r, g, b = (_srgb_to_lin(v) for v in rgb)
    x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047
    y = (0.2126729 * r + 0.7151522 * g + 0.0721750 * b) / 1.0
    z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116

    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def ciede2000(lab1, lab2):
    L1, a1, b1 = lab1
    L2, a2, b2 = lab2
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    Cm = (C1 + C2) / 2
    G = 0.5 * (1 - math.sqrt(Cm ** 7 / (Cm ** 7 + 25 ** 7)))
    a1p, a2p = (1 + G) * a1, (1 + G) * a2
    C1p, C2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360
    h2p = math.degrees(math.atan2(b2, a2p)) % 360
    dLp = L2 - L1
    dCp = C2p - C1p
    if C1p * C2p == 0:
        dhp = 0
    else:
        dhp = h2p - h1p
        if dhp > 180:
            dhp -= 360
        elif dhp < -180:
            dhp += 360
    dHp = 2 * math.sqrt(C1p * C2p) * math.sin(math.radians(dhp) / 2)
    Lpm = (L1 + L2) / 2
    Cpm = (C1p + C2p) / 2
    if C1p * C2p == 0:
        hpm = h1p + h2p
    elif abs(h1p - h2p) <= 180:
        hpm = (h1p + h2p) / 2
    else:
        hpm = (h1p + h2p + 360) / 2 if h1p + h2p < 360 else (h1p + h2p - 360) / 2
    T = (1 - 0.17 * math.cos(math.radians(hpm - 30)) + 0.24 * math.cos(math.radians(2 * hpm))
         + 0.32 * math.cos(math.radians(3 * hpm + 6)) - 0.20 * math.cos(math.radians(4 * hpm - 63)))
    dtheta = 30 * math.exp(-(((hpm - 275) / 25) ** 2))
    Rc = 2 * math.sqrt(Cpm ** 7 / (Cpm ** 7 + 25 ** 7))
    Sl = 1 + (0.015 * (Lpm - 50) ** 2) / math.sqrt(20 + (Lpm - 50) ** 2)
    Sc = 1 + 0.045 * Cpm
    Sh = 1 + 0.015 * Cpm * T
    Rt = -math.sin(math.radians(2 * dtheta)) * Rc
    return math.sqrt((dLp / Sl) ** 2 + (dCp / Sc) ** 2 + (dHp / Sh) ** 2 + Rt * (dCp / Sc) * (dHp / Sh))


def hex_to_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def dominant_colours(img, k=6):
    small = img.convert("RGB")
    small.thumbnail((128, 128))
    q = small.quantize(colors=k, method=Image.Quantize.MEDIANCUT)
    pal = q.getpalette()[: k * 3]
    counts = sorted(q.getcolors(), reverse=True)
    total = sum(c for c, _ in counts) or 1
    return [((pal[i * 3], pal[i * 3 + 1], pal[i * 3 + 2]), c / total) for c, i in counts]


def palette_metrics(img, palette_hex):
    if not palette_hex:
        return None
    pal_lab = [rgb_to_lab(hex_to_rgb(h)) for h in palette_hex]
    doms = dominant_colours(img)
    rows, weighted, worst = [], 0.0, 0.0
    for rgb, share in doms:
        lab = rgb_to_lab(rgb)
        de = min(ciede2000(lab, p) for p in pal_lab)
        rows.append({"rgb": "#%02X%02X%02X" % rgb, "share": round(share, 4), "delta_e": round(de, 2)})
        weighted += de * share
        worst = max(worst, de)
    return {"dominant": rows, "mean_delta_e": round(weighted, 2), "max_delta_e": round(worst, 2)}


def reserved_hue_share(img, reserved):
    if not reserved:
        return None
    hsv = img.convert("RGB")
    hsv.thumbnail((256, 256))
    raw = hsv.convert("HSV").tobytes()
    data = [(raw[i], raw[i + 1], raw[i + 2]) for i in range(0, len(raw) - 2, 3)]
    n = len(data) or 1
    out = []
    for r in reserved:
        hue, tol = float(r["hue_deg"]), float(r["tolerance_deg"])
        min_s, min_v = float(r.get("min_s", 0)), float(r.get("min_v", 0))
        hits = 0
        for h, s, v in data:
            hd = h * 360.0 / 255.0
            d = abs((hd - hue + 180) % 360 - 180)
            if d <= tol and s / 255.0 >= min_s and v / 255.0 >= min_v:
                hits += 1
        out.append({"hue_deg": hue, "share": round(hits / n, 5)})
    return out


# --- shared ---------------------------------------------------------------------------------------------------------
def _open(path, res):
    kind, ext = sniff_file(path)
    res["metrics"]["format"] = kind
    if kind not in ("png", "jpeg", "webp"):
        fail(res, "format", f"not a PNG, JPEG or WebP image (magic bytes say {kind})", value=kind)
        return None
    if not ext_matches(path, ext):
        fail(res, "format_ext", f"file extension does not match its real format ({kind})", value=Path(path).suffix)
    try:
        with Image.open(path) as im:
            im.verify()
        img = Image.open(path)
        img.load()
    except Exception as e:
        fail(res, "opens", f"image does not open: {type(e).__name__}")
        return None
    res["metrics"].update({"width": img.width, "height": img.height, "mode": img.mode, "bytes": Path(path).stat().st_size,
                           "has_alpha": img.mode in ("RGBA", "LA", "PA") or "transparency" in img.info})
    return img


def _col_diff(img, x1, x2):
    a = img.crop((x1, 0, x1 + 1, img.height))
    b = img.crop((x2, 0, x2 + 1, img.height))
    return sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / len(img.getbands())


def _row_diff(img, y1, y2):
    a = img.crop((0, y1, img.width, y1 + 1))
    b = img.crop((0, y2, img.width, y2 + 1))
    return sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / len(img.getbands())


def _interior_median(img, axis, samples=64):
    n = img.width if axis == "x" else img.height
    if n < 4:
        return 0.0
    step = max(1, (n - 2) // samples)
    vals = []
    for i in range(1, n - 2, step):
        vals.append(_col_diff(img, i, i + 1) if axis == "x" else _row_diff(img, i, i + 1))
    return statistics.median(vals) if vals else 0.0


def _rgb(img):
    return img.convert("RGB")


def _apply_common(img, res, opts, style_lock):
    m = res["metrics"]
    size = opts.get("size")
    if size:
        w, h = (int(v) for v in str(size).lower().split("x"))
        if (img.width, img.height) != (w, h):
            fail(res, "size", f"expected {w}x{h}", value=f"{img.width}x{img.height}", limit=f"{w}x{h}")
    min_size = opts.get("min_size")
    if min_size and min(img.width, img.height) < int(min_size):
        fail(res, "min_size", f"shorter side below {min_size} px", value=min(img.width, img.height), limit=int(min_size))
    aspect = opts.get("aspect")
    if aspect:
        aw, ah = (float(v) for v in str(aspect).split(":"))
        want, got = aw / ah, img.width / img.height
        m["aspect"] = round(got, 4)
        if abs(got - want) / want > 0.01:
            fail(res, "aspect", f"aspect {got:.3f} is not {aspect}", value=round(got, 4), limit=round(want, 4))
    palette = (style_lock or {}).get("palette")
    if palette:
        pm = palette_metrics(img, palette)
        m["palette"] = pm
        if opts.get("max_de") is not None and pm["mean_delta_e"] > float(opts["max_de"]):
            fail(res, "palette_delta_e", "dominant colours are too far from the style-lock palette", value=pm["mean_delta_e"], limit=float(opts["max_de"]))
    reserved = (style_lock or {}).get("reserved_hues")
    if reserved:
        rh = reserved_hue_share(img, reserved)
        m["reserved_hues"] = rh
        if opts.get("max_reserved") is not None:
            for r in rh:
                if r["share"] > float(opts["max_reserved"]):
                    fail(res, "reserved_hue", f"{r['share'] * 100:.2f}% of pixels sit in the reserved hue {r['hue_deg']}°", value=r["share"], limit=float(opts["max_reserved"]))


# --- classes --------------------------------------------------------------------------------------------------------
def check_image(path, opts, style_lock=None):
    res = new_result("image", path)
    img = _open(path, res)
    if img is None:
        return res
    _apply_common(img, res, opts, style_lock)
    if opts.get("bg_uniform"):
        rgb = _rgb(img)
        w, h = rgb.size
        bw, bh = max(1, w // 20), max(1, h // 20)
        strips = [rgb.crop((0, 0, w, bh)), rgb.crop((0, h - bh, w, h)), rgb.crop((0, 0, bw, h)), rgb.crop((w - bw, 0, w, h))]
        std = max(max(ImageStat.Stat(s.convert("L")).stddev) for s in strips)
        res["metrics"]["bg_edge_std"] = round(std, 2)
        limit = float(opts.get("bg_std_max", BG_STD_MAX))
        if std > limit:
            fail(res, "bg_uniform", "background edge strips are not uniform", value=round(std, 2), limit=limit)
    return res


def _seams(img, res, both=True):
    rgb = _rgb(img)
    if max(rgb.size) > 2048:
        rgb.thumbnail((2048, 2048))
    m = res["metrics"]
    lr = _col_diff(rgb, 0, rgb.width - 1)
    ix = _interior_median(rgb, "x")
    m["seam_lr"] = round(lr, 3)
    m["interior_x"] = round(ix, 3)
    m["seam_ratio_lr"] = round(lr / max(ix, 0.5), 3)
    if both:
        tb = _row_diff(rgb, 0, rgb.height - 1)
        iy = _interior_median(rgb, "y")
        m["seam_tb"] = round(tb, 3)
        m["interior_y"] = round(iy, 3)
        m["seam_ratio_tb"] = round(tb / max(iy, 0.5), 3)
    return rgb


def check_tile(path, opts, style_lock=None):
    res = new_result("tile", path)
    img = _open(path, res)
    if img is None:
        return res
    _apply_common(img, res, opts, style_lock)
    w, h = img.size
    pot = lambda n: n > 0 and (n & (n - 1)) == 0
    res["metrics"]["power_of_two"] = pot(w) and pot(h)
    if not opts.get("allow_npot") and not (pot(w) and pot(h)):
        fail(res, "power_of_two", "tiling textures need power-of-two sides", value=f"{w}x{h}")
    tex_max = opts.get("tex_max")
    if tex_max and max(w, h) > int(tex_max):
        fail(res, "tex_max", "texture is larger than the class budget", value=max(w, h), limit=int(tex_max))
    rgb = _seams(img, res, both=True)
    limit = float(opts.get("seam_ratio", SEAM_RATIO))
    for axis in ("lr", "tb"):
        r = res["metrics"][f"seam_ratio_{axis}"]
        if r > limit:
            fail(res, f"seam_{axis}", f"visible wrap seam ({'left/right' if axis == 'lr' else 'top/bottom'})", value=r, limit=limit)
    proof = opts.get("proof")
    if proof:
        sheet = Image.new("RGB", (rgb.width * 2, rgb.height * 2))
        for dx in (0, rgb.width):
            for dy in (0, rgb.height):
                sheet.paste(rgb, (dx, dy))
        Path(proof).parent.mkdir(parents=True, exist_ok=True)
        sheet.save(proof, format="PNG")
        res["metrics"]["proof_sheet"] = str(proof)
    return res


def check_pano(path, opts, style_lock=None):
    res = new_result("pano", path)
    img = _open(path, res)
    if img is None:
        return res
    _apply_common(img, res, opts, style_lock)
    w, h = img.size
    ratio = w / h if h else 0
    res["metrics"]["aspect"] = round(ratio, 4)
    if abs(ratio - 2.0) / 2.0 > 0.01:
        fail(res, "aspect", "an equirect panorama must be 2:1", value=round(ratio, 4), limit=2.0)
    min_w = int(opts.get("min_width", PANO_MIN_WIDTH))
    if w < min_w:
        fail(res, "min_width", f"panorama narrower than {min_w} px (WoO shipped a 256x128 sky)", value=w, limit=min_w)
    rgb = _seams(img, res, both=False)
    quarter = _col_diff(rgb, 0, rgb.width // 4)
    res["metrics"]["quarter_turn_diff"] = round(quarter, 3)
    limit = float(opts.get("seam_ratio", SEAM_RATIO))
    if res["metrics"]["seam_ratio_lr"] > limit:
        fail(res, "wrap_seam", "left and right edges do not meet", value=res["metrics"]["seam_ratio_lr"], limit=limit)
    band = max(1, rgb.height // 20)
    top = ImageStat.Stat(rgb.crop((0, 0, rgb.width, 1)).convert("L")).stddev[0]
    bottom = ImageStat.Stat(rgb.crop((0, rgb.height - 1, rgb.width, rgb.height)).convert("L")).stddev[0]
    res["metrics"].update({"pole_band_px": band, "pole_top_row_std": round(top, 2), "pole_bottom_row_std": round(bottom, 2)})
    if max(top, bottom) > float(opts.get("pole_std_warn", 25.0)):
        res["warnings"].append("pole rows vary a lot along the row: inspect the top and bottom 5% for smear or pinching")
    return res


def _alpha_facts(img):
    rgba = img.convert("RGBA")
    a = rgba.getchannel("A")
    lo, hi = a.getextrema()
    visible = a.point(lambda v: 255 if v >= 13 else 0)
    opaque = a.point(lambda v: 255 if v >= 242 else 0)
    semi = a.point(lambda v: 255 if 13 <= v < 242 else 0)
    dil = opaque.filter(ImageFilter.MaxFilter(5))
    halo = ImageChops.multiply(semi, ImageChops.invert(dil))
    return {"alpha_min": lo, "alpha_max": hi, "visible_px": visible.histogram()[255], "halo_px": halo.histogram()[255], "bbox": visible.getbbox()}


def check_icon(path, opts, style_lock=None):
    res = new_result("icon", path)
    img = _open(path, res)
    if img is None:
        return res
    _apply_common(img, res, opts, style_lock)
    m = res["metrics"]
    if not m["has_alpha"]:
        return fail(res, "alpha", "no alpha channel: icons need real transparency, not a painted background")
    f = _alpha_facts(img)
    m.update({"alpha_min": f["alpha_min"], "visible_px": f["visible_px"], "halo_px": f["halo_px"]})
    if f["alpha_min"] >= 250:
        return fail(res, "transparency", "alpha channel is fully opaque")
    if not f["bbox"] or f["visible_px"] == 0:
        return fail(res, "empty", "icon has no visible pixels")
    halo = f["halo_px"] / max(1, f["visible_px"])
    m["halo_ratio"] = round(halo, 5)
    limit = float(opts.get("halo_max", HALO_MAX))
    if halo > limit:
        fail(res, "halo", "semi-transparent fringe outside the opaque shape (halo on coloured UI)", value=round(halo, 5), limit=limit)
    x0, y0, x1, y1 = f["bbox"]
    w, h = img.size
    pad = {"left": x0, "top": y0, "right": w - x1, "bottom": h - y1}
    m["padding_px"] = pad
    m["fill_ratio"] = round(((x1 - x0) * (y1 - y0)) / float(w * h), 4)
    if min(pad.values()) == 0:
        fail(res, "touches_border", "the icon touches the canvas edge: crop to bounds, then re-pad to a fixed canvas")
    return res


def icon_set_consistency(out, results, opts):
    fills = [r["metrics"].get("fill_ratio") for r in results if r["metrics"].get("fill_ratio")]
    if len(fills) < 3:
        return
    med = statistics.median(fills)
    tol = float(opts.get("fill_tolerance", 0.25))
    out["metrics"]["fill_ratio_median"] = round(med, 4)
    for r in results:
        fr = r["metrics"].get("fill_ratio")
        if fr and abs(fr - med) / med > tol:
            fail(r, "set_padding", "fill box differs from the rest of the icon set (scale drift)", value=fr, limit=round(med, 4))
