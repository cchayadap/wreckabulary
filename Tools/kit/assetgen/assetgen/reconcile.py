"""reconcile: find paid work the ledger cannot tie to an attempt (WoO: 131 credits without a job id).

Local findings (no network):
- an MCP reservation the guard marked `used` that never got a receipt (orphan; its estimate is still held as reserved);
- a receipt stuck in submission_pending (with no live process holding the attempt) or submission_unknown (orphan);
- a receipted reservation with no settle after 24 h (stuck);
- a REST, CLI or manual reservation (status `used` from the start) with no receipt line after it expired (stuck). It is
  never released automatically: a job run by hand may still be running.
Provider findings (zero-cost list calls): every Meshy task family in providers.json, page by page; Higgsfield
`generate list`; fal billing events only with --usage, which needs the admin key FAL_ADMIN_KEY and is run by the human.
Jobs older than --since are skipped; a job with no readable creation time is skipped with a warning, never reported.
A provider job the ledger does not know becomes an `orphan` line carrying `actual` and `unit`: the provider's reported
cost, or a provisional list price (then the endpoint ceiling, then the provider's highest list price) until a human
settles it. Orphan actuals count as spent, so they hold the hard cap. Findings already in the ledger are not repeated.
"""

import datetime as _dt

from . import budget as budgetmod
from . import config, locks
from .ledger import open_reservations
from .providers import driver_for, has_driver
from .util import now, read_json_or, utc_iso

STUCK_HOURS = 24


def _key(provider, request_id=None, reservation_id=None, attempt_id=None):
    return (provider, request_id or reservation_id or attempt_id)


def _unit(ctx, pid, ep):
    caps = ((ctx.budget if ctx.layout.budget.is_file() else {}) or {}).get("providers", {})
    return (caps.get(pid) or {}).get("unit") or ((ep or {}).get("price") or {}).get("unit")


def provisional_cost(ctx, pid, endpoint):
    """(amount, unit, basis) for a provider job whose cost the listing does not give, or None."""
    eps = [e for e in ctx.providers.get("endpoints", []) if e.get("provider") == pid]
    ep = config.find_endpoint(ctx.providers, pid, endpoint) if endpoint else None
    unit = _unit(ctx, pid, ep or (eps[0] if eps else None))
    if ep and ep["price"]["per"] != "token_estimate":
        lp = budgetmod.list_price(ep, 1)
        return lp["est"], lp["unit"], f"list price of {ep['id']}"
    bdoc = ctx.budget if ctx.layout.budget.is_file() else None
    ceiling = config.ceiling_for(bdoc, pid, endpoint or "*")
    if ep and ep["price"].get("ceiling") is not None:
        ceiling = max(ceiling or 0, ep["price"]["ceiling"])
    if ceiling:
        return float(ceiling), unit, "ceiling"
    prices = [budgetmod.list_price(e, 1)["est"] for e in eps if e["price"]["per"] != "token_estimate"]
    if prices:
        return max(prices), unit, f"highest {pid} list price"
    return None


def _listing(ctx, pid, prov, since, usage, warnings):
    """Jobs from one provider, merged across its task families; None when it cannot be listed."""
    eps = [e for e in ctx.providers.get("endpoints", []) if e.get("provider") == pid]
    first = eps[0] if eps else None
    drv = driver_for(ctx.layout, pid, prov, first, (first or {}).get("endpoint", ""))
    targets = [(e, e["endpoint"]) for e in eps] if drv.list_per_endpoint else [(first, (first or {}).get("endpoint", ""))]
    merged, seen, listed_any = [], set(), False
    for ep, endpoint in targets:
        d = driver_for(ctx.layout, pid, prov, ep, endpoint) if drv.list_per_endpoint else drv
        try:
            got = d.list_jobs(since, admin=usage)
        except Exception as e:
            if not drv.list_per_endpoint:
                raise
            warnings.append(f"{pid} {endpoint}: listing failed ({type(e).__name__}); its tasks were not checked")
            continue
        if got is None:
            continue
        listed_any = True
        warnings.extend(got.get("warnings") or [])
        for j in got.get("jobs") or []:
            if j["request_id"] not in seen:
                seen.add(j["request_id"])
                merged.append(j)
    return merged if listed_any else None


def reconcile(ctx, since, usage=False, only=None):
    L = ctx.layout
    lines = ctx.ledger.lines()
    known_ids = {str(l["request_id"]) for l in lines if l.get("request_id")}
    have = {_key(l.get("provider"), l.get("request_id"), l.get("reservation_id"), l.get("attempt_id")) for l in lines if l.get("event") == "orphan"}
    findings, stuck, warnings = [], [], []
    open_by_rid = open_reservations(lines)
    receipted_rids = {l.get("reservation_id") for l in lines if l.get("event") == "receipt"}
    t_now = now()

    def attempt_live(attempt_id):
        try:
            return locks.is_held(L.attempt_dir(attempt_id) / ".attempt.lock")
        except Exception:
            return False

    if L.reservations.is_dir():
        for f in sorted(L.reservations.glob("r-*.json")):
            doc = read_json_or(f, None)
            if not doc or doc.get("status") != "used":
                continue
            sentinel = any(t.startswith("mcp__assetgen__") for t in doc.get("tools", []))
            if not sentinel:
                findings.append({"provider": doc["provider"], "request_id": None, "reservation_id": doc["id"], "attempt_id": doc["attempt_id"],
                                 "reason": f"reservation {doc['id']} was used by an MCP call ({doc.get('used_utc', '?')}) but has no receipt"})
                continue
            # REST, CLI and manual reservations start `used`: report one that expired with no receipt line (AG: money held forever).
            if doc["id"] not in open_by_rid or doc["id"] in receipted_rids or attempt_live(doc["attempt_id"]):
                continue
            try:
                expired = _dt.datetime.fromisoformat(doc["expires_utc"].replace("Z", "+00:00")) <= t_now
            except Exception:
                expired = False
            if expired:
                stuck.append({"reservation_id": doc["id"], "attempt_id": doc["attempt_id"], "provider": doc["provider"], "request_id": None,
                              "since": doc.get("created_utc"), "reason": "used reservation expired with no receipt: it still holds its estimate",
                              "next": f"check the provider; `assetgen receipt {doc['id']} --request-id <id>` if a job ran, otherwise "
                                      f"`assetgen release {doc['id']} --reason \"…\"` (add --provider-confirmed-no-charge for a job run by hand)"})

    if L.gen.is_dir():
        for rp in sorted(L.gen.glob("*/*/a*/receipt.json")):
            rec = read_json_or(rp, None)
            if not rec or rec.get("state") not in ("submission_pending", "submission_unknown"):
                continue
            if rec["state"] == "submission_pending" and locks.is_held(rp.parent / ".attempt.lock"):
                continue  # another process is submitting right now: in flight, not unknown
            findings.append({"provider": rec["provider"], "request_id": None, "reservation_id": rec.get("reservation_id"), "attempt_id": rec["attempt_id"],
                             "reason": f"submit outcome unknown ({rec['state']}): the provider may hold a job with no id in the ledger"})

    receipted = {l["reservation_id"]: l for l in lines if l.get("event") == "receipt" and l.get("reservation_id") in open_by_rid}
    cutoff = t_now - _dt.timedelta(hours=STUCK_HOURS)
    for rid, l in receipted.items():
        try:
            t = _dt.datetime.fromisoformat(l["utc"].replace("Z", "+00:00"))
        except Exception:
            continue
        if t < cutoff:
            stuck.append({"reservation_id": rid, "attempt_id": l.get("attempt_id"), "provider": l["provider"], "request_id": l.get("request_id"), "since": l["utc"]})

    for pid, prov in ctx.providers.get("providers", {}).items():
        if only and pid not in only:
            continue
        if not has_driver(pid) or not ({"rest", "cli"} & set(prov.get("access", []))):
            continue
        if pid == "fal" and not usage:
            continue
        try:
            jobs = _listing(ctx, pid, prov, since, usage, warnings)
        except Exception as e:
            warnings.append(f"{pid}: provider listing failed ({type(e).__name__})")
            continue
        if jobs is None:
            if pid == "fal":
                warnings.append("fal: --usage needs FAL_ADMIN_KEY (human-run only)")
            continue
        undated = 0
        for j in jobs:
            created = j.get("created")
            if created is None and not j.get("in_window"):
                undated += 1
                continue
            if created is not None and created < since:
                continue
            if j["request_id"] in known_ids:
                continue
            f = {"provider": pid, "request_id": j["request_id"], "reservation_id": None, "attempt_id": None, "endpoint": j.get("endpoint")}
            if j.get("cost") is not None:
                f.update(actual=float(j["cost"]), unit=_unit(ctx, pid, config.find_endpoint(ctx.providers, pid, j.get("endpoint") or "")),
                         reason=f"provider job with no matching attempt_id in the ledger (cost {j['cost']} reported by {pid})")
            else:
                p = provisional_cost(ctx, pid, j.get("endpoint"))
                if p:
                    f.update(actual=float(p[0]), unit=p[1], reason=f"provider job with no matching attempt_id in the ledger (cost not listed; provisional {p[0]} {p[1]} = {p[2]} until a human settles it)")
                else:
                    f["reason"] = "provider job with no matching attempt_id in the ledger (cost unknown and no list price)"
                    warnings.append(f"{pid}: orphan {j['request_id']} has no cost and no list price to hold against the cap")
            findings.append(f)
        if undated:
            warnings.append(f"{pid}: {undated} listed job(s) carry no readable creation time, so --since cannot place them; they were not reported")

    new = []
    with ctx.ledger.lock("assetgen reconcile"):
        for f in findings:
            k = _key(f["provider"], f["request_id"], f["reservation_id"], f["attempt_id"])
            if k in have:
                continue
            have.add(k)
            line = {"event": "orphan", "provider": f["provider"], "request_id": f["request_id"], "reason": f["reason"]}
            for field in ("reservation_id", "attempt_id", "endpoint"):
                if f.get(field):
                    line[field] = f[field]
            if f.get("actual") is not None and f.get("unit"):
                line["actual"] = f["actual"]
                line["unit"] = f["unit"]
            ctx.ledger.append(line)
            new.append(f)
    return {"ok": not new, "since": utc_iso(since), "orphans": findings, "new_orphans": len(new), "stuck": stuck, "warnings": warnings}
