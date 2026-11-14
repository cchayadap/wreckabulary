"""providers.json, budget.json, Docs/style_lock.json and ArtSource/_gen/accounts.json (CONTRACTS.md §7.5).

accounts.json is an assetgen addition: {schema:1, providers:{<p>:{plan, recorded_utc, by}}}. The human records the plan
bought for each provider (a human step), so `doctor` and `reserve` can warn on or refuse free tiers.
"""

import datetime as _dt

from . import schemas
from .errors import InfraError, Refused, UsageError
from .util import glob_match, now, read_json

STALE_PRICE_DAYS = 60
BUILTIN_PRESETS = {
    # Per-purpose presets from extract_generation.md §5.2 (the contradictions resolved). A style lock preset of the same
    # name overrides these texts; these exist so a project without a lock still gets sane framing.
    "concept": {"preamble": "Game concept art, clear readable silhouette, plain solid medium-grey background.", "negatives": "text, lettering, logo, watermark, brand names"},
    "concept_character": {"preamble": "Game character concept, hero three-quarter view, readable silhouette, plain solid medium-grey background.", "negatives": "text, logo, watermark"},
    "sheet_ortho": {"preamble": "Orthographic reference sheet, strict front, side and back views, no perspective, pure white #FFFFFF background, even flat lighting.", "negatives": "three-quarter view, perspective, shadows on the background, text, logo"},
    "mesh_input_prop": {"preamble": "Single isolated game prop, three-quarter view from slightly above, centred, whole object in frame, plain dark background, even lighting.", "negatives": "cropped, multiple objects, text, logo, busy background"},
    "ui_cutout": {"preamble": "Flat game UI icon, centred with even padding, bold outline, on a flat solid chroma-green background for matting.", "negatives": "text, lettering, drop shadow baked in, gradient background"},
    "tile": {"preamble": "Seamless tiling texture, top-down, flat even lighting, no perspective, no vignette.", "negatives": "seams, borders, lighting gradient, objects, text"},
    "panorama": {"preamble": "Seamless 360 degree equirectangular panorama, 2:1, horizon level, continuous left and right edges.", "negatives": "seam, borders, text, watermark"},
}
ENDPOINT_KIND_CLASS = {
    "image": "concept", "image_edit": "concept", "sheet": "sheet", "mesh": "prop_mesh", "texture": "texture",
    "panorama": "panorama", "hdri": "hdri", "icon": "icon", "vfx": "vfx", "audio": "audio", "music": "music",
    "voice": "voice", "video": "other", "rig": "other", "motion": "motion", "upscale": "other", "other": "other",
}
ENDPOINT_KIND_CHECK = {
    "image": "image", "image_edit": "image", "sheet": "image", "texture": "tile", "panorama": "pano", "hdri": "pano",
    "icon": "icon", "vfx": "image", "mesh": "mesh", "rig": "mesh", "audio": "audio", "music": "audio", "voice": "audio",
}


def _validate(name, doc, defname, what, path):
    errs = schemas.errors(name, doc, defname)
    if errs:
        raise InfraError(f"{what} is invalid ({path}): " + "; ".join(errs[:5]), reason="config_invalid")


def load_providers(layout):
    path = layout.providers_json
    if not path.is_file():
        raise InfraError(f"providers.json not found at {path}", reason="config_missing")
    doc = read_json(path)
    _validate("providers.schema.json", doc, None, "providers.json", path)
    return doc


def load_budget(layout, required=True):
    if not layout.budget.is_file():
        if not required:
            return None
        raise InfraError(
            "ArtSource/_gen/budget.json is missing. Human step: copy Tools/kit/assetgen/budget.json.tmpl there and set the soft and hard caps.",
            reason="budget_missing",
        )
    doc = read_json(layout.budget)
    _validate("budget.schema.json", doc, None, "budget.json", layout.budget)
    for p, cap in doc.get("providers", {}).items():
        if cap["soft"] > cap["hard"]:
            raise InfraError(f"budget.json: providers.{p}.soft ({cap['soft']}) is above hard ({cap['hard']})", reason="config_invalid")
    return doc


def load_style_lock(layout):
    if not layout.style_lock.is_file():
        return None
    doc = read_json(layout.style_lock)
    _validate("providers.schema.json", doc, "styleLock", "Docs/style_lock.json", layout.style_lock)
    return doc


def load_accounts(layout):
    if not layout.accounts.is_file():
        return {"schema": 1, "providers": {}}
    doc = read_json(layout.accounts)
    if not isinstance(doc, dict) or not isinstance(doc.get("providers", {}), dict):
        raise InfraError("ArtSource/_gen/accounts.json must be {schema:1, providers:{<p>:{plan}}}", reason="config_invalid")
    doc.setdefault("providers", {})
    return doc


def ai_outputs(style_lock):
    """The effective policy tier. No style lock, or a lock the owner has not approved, means the safest tier:
    reference_only (nothing generated enters Assets/). An agent-drafted lock cannot grant ship or marketing_only."""
    lock = style_lock or {}
    tier = lock.get("ai_outputs") or "reference_only"
    if tier != "reference_only" and lock.get("approved_by_owner") is not True:
        return "reference_only"
    return tier


def provider(providers, pid):
    p = providers.get("providers", {}).get(pid)
    if p is None:
        raise UsageError(f"unknown provider {pid!r}; providers.json lists: {', '.join(sorted(providers.get('providers', {})))}")
    return p


def find_endpoint(providers, pid, endpoint):
    for ep in providers.get("endpoints", []):
        if ep.get("provider") == pid and ep.get("endpoint") == endpoint:
            return ep
        if ep.get("id") == f"{pid}:{endpoint}":
            return ep
    return None


def plan_of(accounts, pid):
    return ((accounts or {}).get("providers", {}).get(pid) or {}).get("plan")


def plan_warning(prov_cfg, plan):
    if plan is None:
        return "plan not recorded in ArtSource/_gen/accounts.json (human step)" if prov_cfg.get("blocked_plans") or prov_cfg.get("attribution_plans") or prov_cfg.get("watermark_plans") else None
    p = str(plan).lower()
    if p in [x.lower() for x in prov_cfg.get("blocked_plans", [])]:
        return f"plan '{plan}' never ships (free tier or ownership clause)"
    if p in [x.lower() for x in prov_cfg.get("attribution_plans", [])]:
        return f"plan '{plan}' ships only with attribution (CC BY)"
    if p in [x.lower() for x in prov_cfg.get("watermark_plans", [])]:
        return f"plan '{plan}' embeds a watermark at generation"
    return None


def days_since(date_str):
    try:
        d = _dt.date.fromisoformat(str(date_str)[:10])
    except ValueError:
        return None
    return (now().date() - d).days


def price_sheet_age_days(providers):
    return days_since(providers.get("verified_on"))


def stale_entries(providers, max_days=STALE_PRICE_DAYS):
    out = []
    for pid, p in providers.get("providers", {}).items():
        age = days_since(p.get("verified_on"))
        if age is not None and age > max_days:
            out.append({"id": pid, "age_days": age})
    for ep in providers.get("endpoints", []):
        age = days_since(ep.get("verified_on"))
        if age is not None and age > max_days:
            out.append({"id": ep["id"], "age_days": age})
    return out


def pool_size(providers, budget, pid):
    prov = provider(providers, pid)
    pool = prov.get("pool", pid)
    n = providers.get("pools", {}).get(pool, {}).get("max", 1)
    cap = (budget or {}).get("max_parallel", {}).get(pid)
    if isinstance(cap, int) and cap >= 1:
        n = min(n, cap)
    return pool, max(1, n)


def ceiling_for(budget, pid, endpoint):
    ceilings = ((budget or {}).get("providers", {}).get(pid) or {}).get("ceilings") or {}
    best = None
    for pattern, value in ceilings.items():
        if pattern == "*":
            continue
        if glob_match(pattern, endpoint):
            best = value if best is None else max(best, value)
    if best is None and "*" in ceilings:
        best = ceilings["*"]
    return best


def preset(style_lock, name):
    lock_presets = (style_lock or {}).get("presets") or {}
    if name in lock_presets:
        return dict(lock_presets[name]), "style_lock"
    if name in BUILTIN_PRESETS:
        return dict(BUILTIN_PRESETS[name]), "builtin"
    known = sorted(set(lock_presets) | set(BUILTIN_PRESETS))
    raise UsageError(f"unknown preset {name!r}; known: {', '.join(known)}")


def banned_hits(style_lock, text):
    import re

    hits = []
    for w in (style_lock or {}).get("banned_words", []) or []:
        if w and re.search(r"(?<![A-Za-z0-9])" + re.escape(w) + r"(?![A-Za-z0-9])", text, re.I):
            hits.append(w)
    return hits


def require_policy_ok(style_lock, text):
    hits = banned_hits(style_lock, text)
    if hits:
        raise Refused(f"prompt contains banned words from the style lock: {', '.join(hits)} (brand and IP words get 422s and cannot ship)", reason="policy")
