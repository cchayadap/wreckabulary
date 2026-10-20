"""Local, free, numeric asset checks (extract_generation §6.3; research_generation_apis §4.1–§4.3).

Every check returns {ok, class, path, metrics:{}, failures:[{check, message, value?, limit?}], warnings:[]}.
Classes: image, tile, pano, icon, mesh, audio. Thresholds are defaults the caller can override; they are PROPOSED
numbers, documented in references/checks.md.
"""

from pathlib import Path

from ..errors import UsageError

CLASSES = ("image", "tile", "pano", "icon", "mesh", "audio")


def new_result(cls, path):
    return {"ok": True, "class": cls, "path": str(path), "metrics": {}, "failures": [], "warnings": []}


def fail(res, check, message, value=None, limit=None):
    item = {"check": check, "message": message}
    if value is not None:
        item["value"] = value
    if limit is not None:
        item["limit"] = limit
    res["failures"].append(item)
    res["ok"] = False
    return res


def run_check(cls, path, opts=None, style_lock=None):
    opts = dict(opts or {})
    path = Path(path)
    if cls not in CLASSES:
        raise UsageError(f"--class must be one of {', '.join(CLASSES)}")
    if not path.is_file():
        raise UsageError(f"no such file: {path}")
    if cls == "mesh":
        from .glb import check_mesh

        return check_mesh(path, opts, style_lock)
    if cls == "audio":
        from .audio import check_audio

        return check_audio(path, opts)
    from . import image

    return {"image": image.check_image, "tile": image.check_tile, "pano": image.check_pano, "icon": image.check_icon}[cls](path, opts, style_lock)


def run_checks(cls, path, opts=None, style_lock=None):
    """A file, or every file of a folder (icons also get a set-consistency pass)."""
    path = Path(path)
    if path.is_dir():
        files = sorted(p for p in path.iterdir() if p.is_file() and not p.name.startswith("."))
        results = [run_check(cls, f, opts, style_lock) for f in files]
        out = {"ok": all(r["ok"] for r in results), "class": cls, "path": str(path), "metrics": {"files": len(results)}, "failures": [], "warnings": [], "files": results}
        if cls == "icon" and len(results) >= 3:
            from .image import icon_set_consistency

            icon_set_consistency(out, results, opts)
        for r in results:
            for f in r["failures"]:
                out["failures"].append(dict(f, path=r["path"]))
        out["ok"] = not out["failures"]
        return out
    return run_check(cls, path, opts, style_lock)
