"""Quote, reserve, receipt, release: money is reserved under ledger.lock before any submit (ARCHITECTURE P10).

reserve refuses when spent + reserved + estimate > hard (reason hard_cap), a research_only endpoint meets an
ai_outputs tier of ship or marketing_only (research_only), the recorded plan never ships (blocked_plan), refs go to a
provider that may train on uploads without style_lock uploads_ok (uploads_not_ok), or any other policy rule fails
(policy). It writes one `reserve` ledger line and .harness/reservations/<id>.json (harness.schema.json#/$defs/
reservation). MCP reservations start `open`, and guard G8 lets exactly one call through and marks them `used`. REST and
CLI reservations are consumed by assetgen itself, so they start `used` with a sentinel tool name that G8 never matches.
"""

import datetime as _dt

from . import config, rules, schemas
from .errors import AssetgenError, Refused, UsageError
from .layout import parse_attempt_id
from .ledger import Ledger, open_reservations, totals
from .util import file_stamp, now, rand_suffix, read_json, utc_iso, write_json_atomic

EPS = 1e-9


class Ctx:
    """Per-command context: layout plus lazily loaded configs."""

    def __init__(self, layout):
        self.layout = layout
        self.ledger = Ledger(layout)
        self._providers = None
        self._budget = None
        self._style = None
        self._style_loaded = False
        self._accounts = None

    @property
    def providers(self):
        if self._providers is None:
            self._providers = config.load_providers(self.layout)
        return self._providers

    @property
    def budget(self):
        if self._budget is None:
            self._budget = config.load_budget(self.layout)
        return self._budget

    @property
    def style_lock(self):
        if not self._style_loaded:
            self._style = config.load_style_lock(self.layout)
            self._style_loaded = True
        return self._style

    @property
    def accounts(self):
        if self._accounts is None:
            self._accounts = config.load_accounts(self.layout)
        return self._accounts

    @property
    def tier(self):
        return config.ai_outputs(self.style_lock)


# --- quote ----------------------------------------------------------------------------------------------------------
DEFAULT_SECONDS = 30.0


def _quantity(per, params, prompt_len):
    """Units per output for non-per-call prices, read from the request params; conservative defaults otherwise."""
    params = params or {}
    if per == "second":
        for k in ("duration_seconds", "duration", "seconds", "length_seconds"):
            if isinstance(params.get(k), (int, float)):
                return float(params[k])
        return DEFAULT_SECONDS
    if per == "1k_chars":
        chars = len(str(params.get("text") or params.get("prompt") or "")) or prompt_len or 1000
        return float(-(-chars // 1000))
    if per == "megapixel":
        w, h = params.get("width"), params.get("height")
        size = params.get("image_size")
        if isinstance(size, dict):
            w, h = size.get("width"), size.get("height")
        if isinstance(w, (int, float)) and isinstance(h, (int, float)):
            return max(0.01, w * h / 1e6)
        return 1.05
    return 1.0


def list_price(ep_cfg, n, addons=(), params=None, prompt_len=0):
    if not ep_cfg:
        return None
    price = ep_cfg["price"]
    if price["per"] == "token_estimate":
        return None
    base = float(price["base"])
    extra = 0.0
    for a in addons or ():
        if a not in (price.get("addons") or {}):
            raise UsageError(f"unknown add-on {a!r} for {ep_cfg['id']}; known: {', '.join(sorted(price.get('addons') or {})) or 'none'}")
        extra += float(price["addons"][a])
    qty = _quantity(price["per"], params, prompt_len)
    return {"est": round((base * qty + extra) * n, 6), "unit": price["unit"], "source": "providers.json"}


def quote(ctx, pid, endpoint, n=1, params=None, addons=(), offline=False, driver=None, prompt_len=0):
    prov = config.provider(ctx.providers, pid)
    ep = config.find_endpoint(ctx.providers, pid, endpoint)
    budget = ctx.budget if ctx.layout.budget.is_file() else None
    # Params that change the price (resolution 4K, web search, textures…) become add-ons; an unpriceable one is refused.
    from_params = rules.addons_from_params(pid, endpoint, ep, params)
    addons = list(addons or []) + [x for x in from_params if x not in (addons or [])]
    listed = list_price(ep, n, addons, params, prompt_len)
    live = None
    if not offline and driver is not None:
        try:
            live = driver.estimate(n, params or {})
        except AssetgenError:
            live = None
    ceiling = config.ceiling_for(budget, pid, endpoint)
    unit = (ep or {}).get("price", {}).get("unit") or ((budget or {}).get("providers", {}).get(pid) or {}).get("unit")
    if ep and ep["price"].get("ceiling") is not None:
        ceiling = ep["price"]["ceiling"] if ceiling is None else max(ceiling, ep["price"]["ceiling"])
    if live and listed:
        # The live price is authoritative, but never estimate below the list price plus requested add-ons.
        best = dict(live) if live["est"] >= listed["est"] else dict(listed, source="pricing_api", note="live price below list price plus add-ons")
    else:
        best = live or listed
    if best is None and ceiling is not None:
        best = {"est": round(float(ceiling) * n, 6), "unit": unit or "USD", "source": "ceiling"}
    if best is None:
        raise Refused(
            f"no price for {pid}:{endpoint}: add the endpoint to providers.json or a ceiling to budget.json providers.{pid}.ceilings",
            reason="policy",
        )
    best.setdefault("unit", unit or "USD")
    if ep is None:
        best["warning"] = "endpoint not in providers.json: badge and licence unknown"
    best["provider"] = pid
    best["endpoint"] = endpoint
    best["n"] = n
    best["addons"] = addons
    if ep and ep["price"]["per"] in ("image",) and n:
        best["unit_est"] = round(best["est"] / n, 6)  # per output: settle charges max(est, unit_est × outputs)
    return best


# --- reservation files ----------------------------------------------------------------------------------------------
def new_reservation_id():
    return f"r-{file_stamp()}-{rand_suffix(6)}"


def read_reservation(layout, rid):
    path = layout.reservation_file(rid)
    if not path.is_file():
        raise UsageError(f"no reservation {rid}")
    return read_json(path)


def write_reservation(layout, doc):
    errs = schemas.errors("harness.schema.json", doc, "reservation")
    if errs:
        raise AssetgenError("internal: invalid reservation: " + "; ".join(errs[:4]), reason="reservation_invalid")
    write_json_atomic(layout.reservation_file(doc["id"]), doc)


def update_reservation(layout, rid, **fields):
    doc = read_reservation(layout, rid)
    doc.update({k: v for k, v in fields.items() if v is not None})
    write_reservation(layout, doc)
    return doc


def sweep_expired(ctx, lines):
    """Open reservations past expires_utc hold no money: mark them expired and append a release line (under lock)."""
    released = []
    t = now()
    openl = open_reservations(lines)
    if not ctx.layout.reservations.is_dir():
        return released
    for f in sorted(ctx.layout.reservations.glob("r-*.json")):
        try:
            doc = read_json(f)
        except Exception:
            continue
        if doc.get("status") != "open":
            continue
        try:
            expired = _dt.datetime.fromisoformat(doc["expires_utc"].replace("Z", "+00:00")) <= t
        except Exception:
            expired = False
        if not expired:
            continue
        doc["status"] = "expired"
        write_reservation(ctx.layout, doc)
        if doc["id"] in openl:
            line = ctx.ledger.append({"event": "release", "attempt_id": doc["attempt_id"], "provider": doc["provider"], "endpoint": doc["endpoint"],
                                      "reservation_id": doc["id"], "reason": "expired: the reservation was never used"})
            lines.append(line)
        released.append(doc["id"])
    return released


# --- policy -----------------------------------------------------------------------------------------------------------
def check_policy(ctx, pid, endpoint, *, tools=(), has_refs=False, access="rest"):
    """Raise Refused for any policy rule; return a list of warnings."""
    warnings = []
    prov = config.provider(ctx.providers, pid)
    ep = config.find_endpoint(ctx.providers, pid, endpoint)
    tier = ctx.tier
    if access not in prov.get("access", []):
        raise Refused(f"{pid} does not support access '{access}' (providers.json access: {', '.join(prov.get('access', []))})", reason="policy")
    mutating = set(prov.get("mcp_mutating_tools") or [])
    for t in tools:
        if not t.startswith("mcp__"):
            raise UsageError(f"--tools takes MCP tool names (mcp__…); got {t!r}")
        if mutating and t not in mutating:
            raise Refused(f"{t} is not a mutating {pid} tool in providers.json ({', '.join(sorted(mutating))})", reason="policy")
        if t.endswith("__run_model") and not (ep or {}).get("fast"):
            raise Refused(f"run_model is allowed only for endpoints marked fast:true; use submit_job → check_job → get_job_result for {endpoint} (a timeout would lose the request id)", reason="policy")
    if ep is None:
        if tier in ("ship", "marketing_only"):
            raise Refused(f"{pid}:{endpoint} is not in providers.json, so its licence badge is unknown and ai_outputs is {tier}", reason="policy")
        warnings.append("endpoint not in providers.json: estimate from a ceiling, licence unknown")
    else:
        if ep.get("badge") == "research_only" and tier in ("ship", "marketing_only"):
            raise Refused(f"{ep['id']} carries fal's 'Research only' badge and ai_outputs is {tier}", reason="research_only")
        if (ep.get("ship") or {}).get("blocked") and tier == "ship":
            raise Refused(f"{ep['id']} is blocked for shipping: {(ep.get('ship') or {}).get('reason', '')}", reason="policy")
    plan = config.plan_of(ctx.accounts, pid)
    if plan and plan.lower() in [p.lower() for p in prov.get("blocked_plans", [])]:
        if tier != "reference_only":
            raise Refused(f"{pid} plan '{plan}' never ships (free tier or provider-owned output) and ai_outputs is {tier}", reason="blocked_plan")
        warnings.append(f"{pid} plan '{plan}' output is reference-only at best")
    w = config.plan_warning(prov, plan)
    if w and w not in warnings:
        warnings.append(f"{pid}: {w}")
    uploads = has_refs or any(t.endswith("__upload_file") for t in tools)
    if uploads and prov.get("training_on_uploads") in (True, "unknown"):
        ok = ((ctx.style_lock or {}).get("uploads_ok") or {}).get(pid) is True
        if not ok:
            raise Refused(
                f"{pid} may train on uploaded content (training_on_uploads={prov.get('training_on_uploads')}); the owner must say yes once and it is recorded as style_lock uploads_ok.{pid}=true",
                reason="uploads_not_ok",
            )
    return warnings


def attempts_in_stage(lines, asset, stage):
    """Attempts that count against attempts_per_stage: reserved, unless every reservation was released or expired
    before any receipt (nothing was ever submitted)."""
    prefix = f"{asset}/{stage}/"
    reserved = {l["attempt_id"] for l in lines if l.get("event") == "reserve" and str(l.get("attempt_id", "")).startswith(prefix)}
    receipted = {l.get("attempt_id") for l in lines if l.get("event") == "receipt"}
    still_open = {l.get("attempt_id") for l in open_reservations(lines).values()}
    return sorted(a for a in reserved if a in receipted or a in still_open)


def reserve(ctx, *, pid, endpoint, attempt_id, est, unit, tools=(), lane=None, access="mcp", refs=None, prompt_sha=None,
            input_sha=None, dry_run=False, lock_wait_s=30.0, extra_warnings=()):
    asset, stage, _ = parse_attempt_id(attempt_id)
    budget = ctx.budget
    cap = (budget.get("providers") or {}).get(pid)
    if cap is None:
        raise Refused(f"budget.json has no cap for provider '{pid}': add providers.{pid} {{unit, soft, hard}} (human step)", reason="hard_cap")
    if cap["unit"] != unit:
        raise Refused(f"unit mismatch: budget.json caps {pid} in {cap['unit']} but the estimate is in {unit}", reason="policy")
    if access == "mcp" and not tools:
        raise UsageError("an MCP reservation needs --tools (for example mcp__fal-ai__submit_job)")
    warnings = list(extra_warnings) + check_policy(ctx, pid, endpoint, tools=tools, has_refs=bool(refs), access=access)
    ttl = int(budget.get("reservation_ttl_min", 60))

    def _decide(lines):
        active = [r for r in open_reservations(lines).values() if r.get("attempt_id") == attempt_id]
        if active:
            raise Refused(f"attempt {attempt_id} already holds reservation {active[0]['reservation_id']}", reason="policy")
        existing = attempts_in_stage(lines, asset, stage)
        if attempt_id in existing:
            raise Refused(f"attempt {attempt_id} was already submitted once; start a new attempt", reason="policy")
        if len(existing) >= int(budget.get("attempts_per_stage", 3)):
            raise Refused(
                f"{asset}/{stage} already used {len(existing)} of attempts_per_stage={budget.get('attempts_per_stage', 3)}: change route or simplify the asset (ARCHITECTURE §3.7)",
                reason="policy",
            )
        t = totals(lines, budget).get(pid, {"spent": 0.0, "reserved": 0.0})
        after = t["spent"] + t["reserved"] + est
        if after > cap["hard"] + EPS:
            raise Refused(
                f"hard cap: {pid} spent {t['spent']:.4f} + reserved {t['reserved']:.4f} + estimate {est:.4f} = {after:.4f} > hard {cap['hard']} {unit}",
                reason="hard_cap",
                result={"spent": t["spent"], "reserved": t["reserved"], "est": est, "hard": cap["hard"], "unit": unit},
            )
        if after > cap["soft"] + EPS:
            warnings.append(f"soft cap passed: {pid} would be at {after:.4f} of soft {cap['soft']} {unit}")
        return t

    if dry_run:
        t = _decide(ctx.ledger.lines())
        return {"dry_run": True, "attempt_id": attempt_id, "est": est, "unit": unit, "spent": t["spent"], "reserved": t["reserved"], "warnings": warnings}

    with ctx.ledger.lock("assetgen reserve", lock_wait_s):
        lines = ctx.ledger.lines()
        sweep_expired(ctx, lines)
        _decide(lines)
        rid = new_reservation_id()
        created = now()
        expires = created + _dt.timedelta(minutes=ttl)
        doc = {
            "schema": 1, "id": rid, "attempt_id": attempt_id, "provider": pid, "endpoint": endpoint,
            "tools": list(tools) if tools else [f"mcp__assetgen__{access}"], "est": est, "unit": unit,
            "created_utc": utc_iso(created), "expires_utc": utc_iso(expires), "status": "open" if access == "mcp" else "used",
            "request_id": None,
        }
        if access != "mcp":
            doc["used_utc"] = utc_iso(created)
            doc["tool_use_id"] = f"assetgen:{access}"
        if lane:
            doc["lane"] = lane
        if ctx.layout.test_mode:
            doc["test_mode"] = True  # guard G8 and licence rows must never honour a test-mode reservation
        write_reservation(ctx.layout, doc)
        ctx.ledger.append({
            "utc": utc_iso(created), "event": "reserve", "attempt_id": attempt_id, "asset_id": asset, "stage": stage, "lane": lane,
            "provider": pid, "endpoint": endpoint, "access": access, "reservation_id": rid, "est": est, "unit": unit,
            "request_id": None, "prompt_sha": prompt_sha, "input_sha": input_sha,
            "refs": [{"sha256": r["sha256"], "role": r["role"]} for r in (refs or [])] or None,
        })
    return {"reservation_id": rid, "attempt_id": attempt_id, "est": est, "unit": unit, "expires_utc": utc_iso(expires),
            "path": ctx.layout.rel(ctx.layout.reservation_file(rid)), "status": doc["status"], "warnings": warnings}


def record_receipt(ctx, rid, request_id, lock_wait_s=30.0):
    if not request_id or not str(request_id).strip():
        raise UsageError("--request-id is required")
    with ctx.ledger.lock("assetgen receipt", lock_wait_s):
        doc = read_reservation(ctx.layout, rid)
        if doc["status"] in ("settled", "released", "expired"):
            raise Refused(f"reservation {rid} is {doc['status']}", reason="policy")
        if doc["status"] == "open":
            # Only MCP reservations are ever open, and guard G8 marks one `used` when it lets the paid call through.
            # A receipt on an open reservation would record a request id no provider call produced.
            raise Refused(f"reservation {rid} is still open: guard G8 has not let a {', '.join(doc.get('tools', []))} call through, so there is no "
                          f"request id to record. Make the MCP call first (the guard marks the reservation used).", reason="not_used")
        if doc["status"] == "receipted":
            if doc.get("request_id") == request_id:
                return doc, False
            raise Refused(f"reservation {rid} already has request id {doc.get('request_id')}", reason="policy")
        ctx.ledger.append({"event": "receipt", "attempt_id": doc["attempt_id"], "provider": doc["provider"], "endpoint": doc["endpoint"],
                           "reservation_id": rid, "request_id": str(request_id)})
        doc = update_reservation(ctx.layout, rid, status="receipted", request_id=str(request_id))
    return doc, True


def release(ctx, rid, reason, lock_wait_s=30.0):
    if not reason or not str(reason).strip():
        raise UsageError("--reason is required")
    with ctx.ledger.lock("assetgen release", lock_wait_s):
        doc = read_reservation(ctx.layout, rid)
        if doc["status"] in ("settled", "released"):
            raise Refused(f"reservation {rid} is already {doc['status']}", reason="policy")
        was = doc["status"]
        if rid in open_reservations(ctx.ledger.lines()):
            ctx.ledger.append({"event": "release", "attempt_id": doc["attempt_id"], "provider": doc["provider"], "endpoint": doc["endpoint"],
                               "reservation_id": rid, "request_id": doc.get("request_id"), "reason": str(reason)})
        doc = update_reservation(ctx.layout, rid, status="released")
    warn = None
    if was in ("used", "receipted"):
        warn = "the reserved call may have been billed; release only after the provider confirms it was not charged (reconcile)"
    return doc, warn


def settle_line(ctx, doc, actual, reason=None):
    """Append settle and mark the reservation settled (caller holds the ledger lock)."""
    ctx.ledger.append({"event": "settle", "attempt_id": doc["attempt_id"], "provider": doc["provider"], "endpoint": doc["endpoint"],
                       "reservation_id": doc["id"], "request_id": doc.get("request_id"), "actual": float(actual), "unit": doc["unit"], "reason": reason})
    return update_reservation(ctx.layout, doc["id"], status="settled")
