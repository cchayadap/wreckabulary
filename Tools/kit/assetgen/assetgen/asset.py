"""ArtSource/_gen/<asset_id>/asset.json (budget.schema.json#/$defs/assetManifest). Only assetgen writes it; other
tools call `assetgen asset-set <asset_id> key=value`. Writes happen under the ledger lock."""

import json

from . import schemas
from .errors import AssetgenError, UsageError
from .util import read_json, write_json_atomic

SETTABLE = {"class", "usage", "rig_route", "notes", "style_lock_version", "ai_outputs_at_accept"}


def load(layout, asset_id, cls="other"):
    path = layout.asset_json(asset_id)
    if path.is_file():
        return read_json(path)
    return {"schema": 1, "asset_id": asset_id, "class": cls, "stages": {}}


def save(layout, doc):
    errs = schemas.errors("budget.schema.json", doc, "assetManifest")
    if errs:
        raise AssetgenError("asset.json would be invalid: " + "; ".join(errs[:4]), reason="asset_invalid")
    write_json_atomic(layout.asset_json(doc["asset_id"]), doc)
    return doc


def record_attempt(layout, asset_id, stage, attempt_id, cls="other", style_lock_version=None):
    doc = load(layout, asset_id, cls)
    st = doc["stages"].setdefault(stage, {"attempts": [], "accepted": None})
    if attempt_id not in st["attempts"]:
        st["attempts"].append(attempt_id)
    if style_lock_version and not doc.get("style_lock_version"):
        doc["style_lock_version"] = style_lock_version
    return save(layout, doc)


def set_key(layout, asset_id, key, raw_value):
    if key not in SETTABLE:
        raise UsageError(f"asset-set can change only: {', '.join(sorted(SETTABLE))}")
    path = layout.asset_json(asset_id)
    if not path.is_file():
        raise UsageError(f"no asset.json for {asset_id}")
    doc = read_json(path)
    value = raw_value
    if key == "style_lock_version":
        try:
            value = int(raw_value)
        except ValueError:
            raise UsageError("style_lock_version must be an integer")
    elif raw_value.startswith(("{", "[")):
        try:
            value = json.loads(raw_value)
        except json.JSONDecodeError:
            pass
    old = doc.get(key)
    doc[key] = value
    save(layout, doc)
    return old, value
