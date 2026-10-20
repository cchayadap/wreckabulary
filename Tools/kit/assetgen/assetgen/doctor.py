"""doctor: keys (storage method only, never a value), configs, schemas, plans, zero-cost provider probes, price-sheet
age, the licence writer, and the .mcp.json expanded-key trap (ARCHITECTURE §5.2, critic C-10)."""

import re

from . import config, keys, licence, schemas
from .errors import InfraError
from .providers import driver_for, has_driver
from .util import read_jsonl, read_text

KEY_SHAPES = [
    re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}:[0-9a-f]{16,}", re.I),
    re.compile(r"\bmsy_[A-Za-z0-9]{12,}"),
    re.compile(r"\bsk-[A-Za-z0-9]{16,}"),
    re.compile(r"\bhf_[A-Za-z0-9]{16,}"),
]


def doctor(ctx, offline=False):
    L = ctx.layout
    checks, warnings, human = [], [], []

    def row(name, ok, message, level="FAIL"):
        checks.append({"name": name, "ok": ok, "level": "OK" if ok else level, "message": message})

    for name in ("providers.schema.json", "budget.schema.json", "harness.schema.json"):
        row(f"schema:{name}", schemas.available(name), "found" if schemas.available(name) else "not found: ledger lines are not schema-checked", "WARN")
    try:
        providers = ctx.providers
        row("providers_json", True, L.rel(L.providers_json) if str(L.providers_json).lower().startswith(str(L.ws).lower()) else str(L.providers_json).replace("\\", "/"))
    except InfraError as e:
        row("providers_json", False, e.message)
        return {"ok": False, "keys": [], "providers": [], "price_sheet_age_days": None, "checks": checks, "warnings": warnings, "human_steps": human, "_exit": 3}
    try:
        b = config.load_budget(L, required=False)
        if b is None:
            row("budget_json", False, "ArtSource/_gen/budget.json missing: paid runs are refused until it exists", "WARN")
            human.append("Copy Tools/kit/assetgen/budget.json.tmpl to ArtSource/_gen/budget.json and set soft/hard caps per provider.")
        else:
            row("budget_json", True, "valid")
    except InfraError as e:
        row("budget_json", False, e.message)
    try:
        sl = config.load_style_lock(L)
        tier = config.ai_outputs(sl)
        row("style_lock", True, f"ai_outputs={tier}" if sl else "no Docs/style_lock.json: tier defaults to reference_only", "WARN")
        if sl and not sl.get("approved_by_owner"):
            declared = sl.get("ai_outputs")
            warnings.append("style_lock.json is not approved_by_owner yet" + (f": its ai_outputs={declared} is treated as reference_only until the owner approves" if declared != tier else ""))
    except InfraError as e:
        tier = "reference_only"
        row("style_lock", False, e.message)
    try:
        read_jsonl(L.ledger, strict=True)
        row("ledger", True, "parses" if L.ledger.is_file() else "empty (no ledger yet)")
    except ValueError as e:
        row("ledger", False, f"ledger has a bad line: {e}")
    writer = licence.writer_available(L)
    row("licence_writer", writer, "Tools/kit/ship/license_row.mjs found" if writer else "Tools/kit/ship/license_row.mjs or node missing: accept and fetch-cc0 cannot write licence rows", "WARN")
    mcp = L.ws / ".mcp.json"
    if mcp.is_file():
        text = read_text(mcp)
        leaked = any(p.search(text) for p in KEY_SHAPES)
        row("mcp_json_keys", not leaked, "only ${VAR} references" if not leaked else ".mcp.json holds an expanded key: remove it, rotate the key, use ${FAL_KEY}")
        if '"fal-ai"' in text and "Bearer ${FAL_KEY}" not in text:
            warnings.append(".mcp.json has a fal-ai entry without the literal header 'Bearer ${FAL_KEY}'")

    accounts = ctx.accounts
    key_rows, prov_rows, seen_env = [], [], set()
    for pid, prov in sorted(providers.get("providers", {}).items()):
        env = (prov.get("auth") or {}).get("env")
        if env and env not in seen_env:
            seen_env.add(env)
            storage = keys.storage_of(env, (prov.get("auth") or {}).get("keyring"))
            key_rows.append({"name": env, "storage": storage})
            if storage == "missing":
                human.append(keys.human_step(env))
            elif storage == "env":
                warnings.append(f"{env} is read from the environment; Windows Credential Manager keeps it out of agent shells (OWNER_DEFAULTS C-19)")
        plan = config.plan_of(accounts, pid)
        pw = config.plan_warning(prov, plan)
        reachable, detail = None, "mcp or oauth: check with `claude mcp get`" if "mcp" in prov.get("access", []) else "not probed"
        if not offline and has_driver(pid) and ({"rest", "cli"} & set(prov.get("access", []))):
            ep = next((e for e in providers.get("endpoints", []) if e.get("provider") == pid), None)
            try:
                p = driver_for(L, pid, prov, ep, (ep or {}).get("endpoint", "")).probe()
                reachable, detail = p.get("reachable"), p.get("detail", "")
            except Exception as e:
                reachable, detail = False, type(e).__name__
        prov_rows.append({"id": pid, "access": prov.get("access", []), "reachable": reachable, "detail": detail, "plan": plan, "plan_warning": pw})
        if pw:
            warnings.append(f"{pid}: {pw}")
    age = config.price_sheet_age_days(providers)
    stale = config.stale_entries(providers)
    if age is not None and age > config.STALE_PRICE_DAYS:
        warnings.append(f"providers.json prices are {age} days old (> {config.STALE_PRICE_DAYS}): re-check prices and licences before a batch")
    for s in stale[:10]:
        warnings.append(f"{s['id']} verified {s['age_days']} days ago")
    ok = all(c["ok"] or c["level"] != "FAIL" for c in checks)
    # A FAIL row is a broken environment (invalid config, expanded key in .mcp.json): INFRA, never a code failure.
    return {"_exit": 0 if ok else 3, "ok": ok, "tier": tier, "keys": key_rows, "providers": prov_rows, "price_sheet_age_days": age, "stale_entries": stale,
            "checks": checks, "warnings": warnings, "human_steps": human}
