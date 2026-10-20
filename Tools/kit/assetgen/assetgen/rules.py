"""Per-endpoint rules that providers.schema.json cannot hold yet (its endpoint object is additionalProperties:false).

A providers.json entry wins once the schema gains the optional fields `ref_param`, `addon_params`, `cost_defaults`
and provider `output_hosts` (requested from the contracts unit); until then these tables are the source.

- REF_PARAMS: the request field that takes reference images. An endpoint without one (a text-to-image model) refuses
  --refs unless --ref-param is given, because the refs would be ignored or rejected (WoO generate.py defect 2).
- ADDON_PARAMS: request params that change the price, mapped to providers.json price.addons. A value missing from the
  map is refused on a priced endpoint, so a params file can never buy 4K or web search at the 1K price.
- COUNT_PARAMS: params that multiply the number of outputs (priced per output).
- OUTPUT_HOSTS: hosts a result file may name for an output on the MCP settle path (agent-supplied result JSON).
Names and hosts marked [U] come from WoO receipts or the review, not from provider docs; confirm with one paid run.
"""

import urllib.parse

from .errors import Refused, UsageError

COUNT_PARAMS = ("num_images", "num_outputs", "num_samples", "number_of_images")

# Param names that change what a call costs on some endpoint. On a priced endpoint, one of these that the endpoint's
# ADDON_PARAMS entry does not map is refused (reason cost_param).
COST_PARAM_NAMES = {
    "resolution", "image_resolution", "enable_web_search", "web_search", "texture", "texture_quality", "geometry_quality",
    "quad", "pbr", "enable_pbr", "addons", "upscale", "upscale_factor", "multi_view", "face_count", "smart_low_poly",
    "smart_lowpoly", "generate_parts", "hd_texture", "should_texture", "tier", "quality", "mode",
}

# endpoint id → {param: {normalised value: add-on name or None (no extra cost)}}
ADDON_PARAMS = {
    "fal:fal-ai/nano-banana-pro": {"resolution": {"1k": None, "2k": None, "4k": "4k"}, "enable_web_search": {"false": None, "true": "web_search"}},
    "fal:fal-ai/nano-banana-pro/edit": {"resolution": {"1k": None, "2k": None, "4k": "4k"}, "enable_web_search": {"false": None, "true": "web_search"}},
    # [U] field names: tripo3d/h3.1 texture no|standard|HD, geometry_quality standard|detailed, quad true|false.
    "fal:tripo3d/h3.1/image-to-3d": {"texture": {"no": None, "none": None, "false": None, "standard": "texture_standard", "true": "texture_standard", "hd": "texture_hd"},
                                     "texture_quality": {"standard": "texture_standard", "detailed": "texture_hd", "hd": "texture_hd"},
                                     "geometry_quality": {"standard": None, "detailed": "detailed_geometry"}, "quad": {"false": None, "true": "quad"}},
    "fal:tripo3d/h3.1/multiview-to-3d": {"texture": {"no": None, "none": None, "false": None, "standard": "texture_standard", "true": "texture_standard", "hd": "texture_hd"},
                                         "texture_quality": {"standard": "texture_standard", "detailed": "texture_hd", "hd": "texture_hd"},
                                         "geometry_quality": {"standard": None, "detailed": "detailed_geometry"}, "quad": {"false": None, "true": "quad"}},
    "fal:tripo3d/p1/image-to-3d": {"texture": {"false": None, "true": "textured"}},
    "fal:fal-ai/hyper3d/rodin/v2.5": {"addons": {"highpack": "highpack"}, "tier": {"regular": None, "sketch": None}},
    "fal:fal-ai/hunyuan-3d/v3.1/pro/image-to-3d": {"enable_pbr": {"false": None, "true": "pbr"}},
    "fal:fal-ai/trellis-2": {"resolution": {"512": None, "1024": "1024p", "1536": "1536p"}},
    "fal:openai/gpt-image-2.5/flare/text-to-image": {"quality": {"low": None, "medium": None, "high": None, "auto": None}},
    "fal:openai/gpt-image-2.5/sunburst/edit": {"quality": {"low": None, "medium": None, "high": None, "auto": None}},
    "meshy:openapi/v1/image-to-3d": {"should_texture": {"true": None, "false": None}, "enable_pbr": {"false": None, "true": "textured_hi"}},
    "meshy:openapi/v1/multi-image-to-3d": {"should_texture": {"true": None, "false": None}, "enable_pbr": {"false": None, "true": "textured_hi"}},
    "meshy:openapi/v2/text-to-3d": {"mode": {"preview": None}},
    "meshy:openapi/v1/retexture": {"enable_pbr": {"false": None, "true": None}},
    # Higgsfield list prices are WoO's per-job averages for the textured mesh [C]; `generate cost` prices the flags live.
    "higgsfield:image_to_3d": {"should_texture": {"true": None, "false": None}},
    "higgsfield:multi_image_to_3d": {"should_texture": {"true": None, "false": None}},
    "tripo:v3/generation/image-to-model": {"texture": {"false": None, "true": None}, "texture_quality": {"standard": None, "detailed": None},
                                           "geometry_quality": {"standard": None, "detailed": "detailed_geometry"}, "quad": {"false": None, "true": "quad"},
                                           "smart_low_poly": {"false": None, "true": "smart_lowpoly"}},
    "tripo:v3/generation/multiview-to-model": {"texture": {"false": None, "true": None}, "texture_quality": {"standard": None, "detailed": None},
                                               "geometry_quality": {"standard": None, "detailed": "detailed_geometry"}, "quad": {"false": None, "true": "quad"},
                                               "smart_low_poly": {"false": None, "true": "smart_lowpoly"}},
}

# Add-ons an endpoint charges when the param is absent (its default is the paid option).
COST_DEFAULTS = {
    "fal:tripo3d/p1/image-to-3d": {"texture": "true"},  # P1 texture defaults to true: $0.50, not $0.40 [V fal page]
}

# endpoint id → request field for references ("" = the endpoint takes none). Higgsfield passes refs as --image flags.
REF_PARAMS = {
    "fal:fal-ai/nano-banana-pro": "",
    "fal:fal-ai/nano-banana-pro/edit": "image_urls",
    "fal:openai/gpt-image-2.5/flare/text-to-image": "",
    "fal:openai/gpt-image-2.5/sunburst/edit": "image_urls",
    "fal:fal-ai/imageutils/rembg": "image_url",
    "fal:tripo3d/h3.1/image-to-3d": "image_url",
    "fal:tripo3d/h3.1/multiview-to-3d": "image_urls",  # [U]
    "fal:tripo3d/p1/image-to-3d": "image_url",
    "fal:fal-ai/hyper3d/rodin/v2.5": "input_image_urls",  # [U]
    "fal:fal-ai/hunyuan-3d/v3.1/pro/image-to-3d": "input_image_url",  # [U]
    "fal:fal-ai/trellis-2": "image_url",  # [U]
    "fal:fal-ai/patina/material": "image_url",  # [U]
    "fal:fal-ai/hunyuan_world": "image_url",
    "fal:fal-ai/elevenlabs/sound-effects/v2": "",
    "fal:google/lyria-3.5": "",
    "fal:fal-ai/stable-audio-25/text-to-audio": "",
    "fal:sonilo/v1.1/text-to-music": "",
    "fal:fal-ai/gemini-3.1-flash-tts": "",
    "meshy:openapi/v1/image-to-3d": "image_url",
    "meshy:openapi/v1/multi-image-to-3d": "image_urls",
    "meshy:openapi/v2/text-to-3d": "",
    "meshy:openapi/v1/retexture": "image_style_url",  # [U]
    "meshy:openapi/v1/rigging": "",
    "meshy:openapi/v1/remesh": "",
    "tripo:v3/generation/image-to-model": "",  # v3 upload route not researched: pass an image URL in --params-file
    "tripo:v3/generation/multiview-to-model": "",
    "tripo:v3/animations/rig": "",
}

# Suggested edit variants for text-to-image endpoints that take no references.
EDIT_VARIANT = {
    "fal:fal-ai/nano-banana-pro": "fal-ai/nano-banana-pro/edit",
    "fal:openai/gpt-image-2.5/flare/text-to-image": "openai/gpt-image-2.5/sunburst/edit",
}

# provider → allowed output hosts: an exact host, or ".suffix" for any subdomain. [U] from WoO receipts and review.
OUTPUT_HOSTS = {
    "fal": ["fal.media", ".fal.media", ".fal.run", ".fal.ai"],
    "meshy": [".meshy.ai"],
    "higgsfield": [".higgsfield.ai"],
    "tripo": [".tripo3d.ai", ".tripo3d.com"],
    "scenario": [".scenario.com", ".scenario.gg"],
}

MESHY_TEXT_LIMIT = 600  # Meshy's documented prompt limit (characters)


def ep_key(pid, endpoint):
    return f"{pid}:{endpoint}"


def norm_value(v):
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float) and v.is_integer():
        v = int(v)
    return str(v).strip().lower()


def ref_param(pid, endpoint, ep_cfg):
    """The declared reference field, '' when the endpoint takes none, or None when nothing is known."""
    if ep_cfg and "ref_param" in ep_cfg:
        return ep_cfg.get("ref_param") or ""
    return REF_PARAMS.get(ep_key(pid, endpoint))


def check_refs_allowed(pid, endpoint, ep_cfg, refs, explicit_ref_param):
    if not refs or explicit_ref_param or pid == "higgsfield":
        return
    declared = ref_param(pid, endpoint, ep_cfg)
    if declared:
        return
    hint = EDIT_VARIANT.get(ep_key(pid, endpoint))
    known = "takes no reference images" if declared == "" else "has no known reference field"
    raise UsageError(
        f"{pid}:{endpoint} {known}, so --refs would be ignored or rejected while the run looks fine"
        + (f"; use the edit variant --endpoint {hint}" if hint else "")
        + ", or name the field with --ref-param if you checked the model schema"
    )


def quantity(params, n_flag):
    """Outputs per call: --n, or a count param in the request. Both given and different is a usage error."""
    counts = {k: params[k] for k in COUNT_PARAMS if k in (params or {})}
    for k, v in counts.items():
        if isinstance(v, bool) or not isinstance(v, int) or v < 1:
            raise UsageError(f"params.{k} must be a positive integer (got {v!r})")
    vals = set(counts.values())
    if len(vals) > 1:
        raise UsageError(f"params give different output counts: {counts}")
    from_params = vals.pop() if vals else None
    if n_flag is not None and from_params is not None and int(n_flag) != from_params:
        raise UsageError(f"--n {n_flag} differs from params {counts}: the reservation would price {n_flag} outputs while the provider makes {from_params}")
    return int(n_flag if n_flag is not None else (from_params or 1))


def addons_from_params(pid, endpoint, ep_cfg, params):
    """Add-ons implied by the request params. Raises Refused for a cost param this endpoint does not price."""
    params = params or {}
    if not ep_cfg or (ep_cfg.get("price") or {}).get("per") == "token_estimate":
        return []  # unknown endpoints are priced from a ceiling; token-priced ones from their per-call ceiling
    table = ep_cfg.get("addon_params") or ADDON_PARAMS.get(ep_key(pid, endpoint), {})
    defaults = ep_cfg.get("cost_defaults") or COST_DEFAULTS.get(ep_key(pid, endpoint), {})
    priced = (ep_cfg.get("price") or {}).get("addons") or {}
    out = []
    names = set(params) | set(defaults)
    for name in sorted(names):
        if name in table:
            value = norm_value(params[name]) if name in params else defaults[name]
            values = table[name]
            if value not in values:
                raise Refused(f"{pid}:{endpoint}: {name}={params.get(name, value)!r} changes the price in a way assetgen cannot quote "
                              f"(known values: {', '.join(sorted(values))})", reason="cost_param")
            addon = values[value]
            if addon:
                if addon not in priced:
                    raise Refused(f"{pid}:{endpoint}: {name}={value} maps to add-on {addon!r}, which providers.json does not price", reason="cost_param")
                if addon not in out:
                    out.append(addon)
        elif name in COST_PARAM_NAMES and name in params:
            raise Refused(f"{pid}:{endpoint}: params.{name} may change the price and assetgen has no rule for it on this endpoint; "
                          f"drop it, or quote it through --addons after checking the provider's price page", reason="cost_param")
    return out


def host_allowed(pid, url, prov_cfg=None):
    hosts = (prov_cfg or {}).get("output_hosts") or OUTPUT_HOSTS.get(pid) or []
    parts = urllib.parse.urlsplit(url)
    if parts.scheme != "https":
        return False
    host = (parts.hostname or "").lower()
    for h in hosts:
        if h.startswith(".") and (host.endswith(h) or host == h[1:]):
            return True
        if host == h:
            return True
    return False
