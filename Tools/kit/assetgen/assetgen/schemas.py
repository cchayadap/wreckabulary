"""JSON Schema validation against the kit's shared schema files (CONTRACTS.md §8).

Sub-schemas are validated as {"$ref": "#/$defs/<def>", "$defs": schema["$defs"]}. When a schema file cannot be found
the validators return None and callers fall back to their own minimal checks; `assetgen doctor` reports it.
"""

import json
from functools import lru_cache

from .util import read_text

_LAYOUT = None


def bind(layout):
    global _LAYOUT
    _LAYOUT = layout
    _load.cache_clear()
    _validator.cache_clear()


@lru_cache(maxsize=None)
def _load(name):
    if _LAYOUT is None:
        return None
    path = _LAYOUT.schema_file(name)
    if path is None:
        return None
    return json.loads(read_text(path))


@lru_cache(maxsize=None)
def _validator(name, defname):
    schema = _load(name)
    if schema is None:
        return None
    from jsonschema import Draft202012Validator

    if defname:
        sub = {"$ref": f"#/$defs/{defname}", "$defs": schema.get("$defs", {})}
        return Draft202012Validator(sub)
    return Draft202012Validator(schema)


def available(name):
    return _load(name) is not None


def errors(name, doc, defname=None):
    """List of 'path: message' strings, [] when valid, or None when the schema is not available."""
    v = _validator(name, defname)
    if v is None:
        return None
    out = []
    for e in sorted(v.iter_errors(doc), key=lambda e: list(e.path)):
        loc = "/".join(str(p) for p in e.path) or "(root)"
        out.append(f"{loc}: {e.message}")
    return out
