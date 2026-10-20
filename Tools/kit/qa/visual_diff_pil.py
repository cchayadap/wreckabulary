"""Pillow fallback for visual_diff.mjs when an image is not a PNG (for example a JPEG capture).

Run by visual_diff.mjs as:
    uv run --no-config --no-project --with pillow==<pin> --with numpy==<pin> python visual_diff_pil.py <baseline> <current> <diff.png> <threshold> <count_aa 0|1>

Uses the same YIQ colour distance and threshold meaning as the kit's built-in PNG diff (Kotsarenko and Ramos 2010, as
popularised by pixelmatch, ISC). Anti-aliased pixels are not detected here, so every pixel over the threshold counts.
Prints one JSON line: {"ok": true, "changed", "total", "layout": false} or {"ok": true, "layout": true, "sizes": [...]}
or {"ok": false, "error"}. Exit 0 on ok, 2 on bad arguments, 3 on a read error.
"""
import json
import sys


def main(argv):
    if len(argv) != 5:
        print(json.dumps({"ok": False, "error": "usage: visual_diff_pil.py <baseline> <current> <diff.png> <threshold> <count_aa>"}))
        return 2
    base_path, cur_path, diff_path, threshold_s, _count_aa = argv
    try:
        threshold = float(threshold_s)
    except ValueError:
        print(json.dumps({"ok": False, "error": "threshold must be a number"}))
        return 2
    try:
        import numpy as np
        from PIL import Image
    except ImportError as e:  # pragma: no cover - environment problem
        print(json.dumps({"ok": False, "error": f"missing dependency: {e}"}))
        return 3
    try:
        a_img = Image.open(base_path).convert("RGBA")
        b_img = Image.open(cur_path).convert("RGBA")
    except Exception as e:  # noqa: BLE001 - report any decode failure as a tool error
        print(json.dumps({"ok": False, "error": f"cannot read image: {e}"}))
        return 3
    if a_img.size != b_img.size:
        sizes = [{"width": a_img.size[0], "height": a_img.size[1]}, {"width": b_img.size[0], "height": b_img.size[1]}]
        print(json.dumps({"ok": True, "layout": True, "sizes": sizes}))
        return 0

    def yiq(img):
        arr = np.asarray(img, dtype=np.float64)
        rgb, alpha = arr[..., :3], arr[..., 3:4] / 255.0
        rgb = 255.0 + (rgb - 255.0) * alpha
        r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
        y = r * 0.29889531 + g * 0.58662247 + b * 0.11448223
        i = r * 0.59597799 - g * 0.2741761 - b * 0.32180189
        q = r * 0.21147017 - g * 0.52261711 + b * 0.31114694
        return y, i, q

    y1, i1, q1 = yiq(a_img)
    y2, i2, q2 = yiq(b_img)
    delta = 0.5053 * (y1 - y2) ** 2 + 0.299 * (i1 - i2) ** 2 + 0.1957 * (q1 - q2) ** 2
    changed_mask = delta > 35215.0 * threshold * threshold
    changed = int(changed_mask.sum())
    total = int(changed_mask.size)

    faded = (255.0 + (y1 - 255.0) * 0.1).clip(0, 255).astype(np.uint8)
    out = np.stack([faded, faded, faded, np.full_like(faded, 255)], axis=-1)
    out[changed_mask] = [255, 0, 0, 255]
    Image.fromarray(out, "RGBA").save(diff_path)
    print(json.dumps({"ok": True, "layout": False, "changed": changed, "total": total}))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
