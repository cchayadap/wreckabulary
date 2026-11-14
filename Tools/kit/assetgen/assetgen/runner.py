"""run, resume, poll, MCP receipt/settle, release, accept and reject.

Attempt folder ArtSource/_gen/<asset>/<stage>/<aNN>/ (CONTRACTS.md §7.5): request.json, prompt.txt, refs.json,
receipt.json, result.json, outputs/, asset_checks.json, review.json. The folder name is the idempotency key, and
request.json's input_sha (endpoint, params, refs, prompt, n, add-ons) says which input the attempt belongs to.

- `run` without --attempt goes back to the newest attempt with the SAME input: an unfinished one is resumed by its
  request id, a settled one is returned as already_settled (pay for another sample with --new-attempt). A different
  prompt, params or refs always gets a new attempt. A resume always uses the spec stored in request.json.
- receipt.json `state` only moves forward: submission_pending → submitted → completed → downloaded → settled, with the
  side exits submission_unknown, submit_rejected, provider_error and released. It is created with O_EXCL before the
  submit, so a crash never leaves a paid call without a receipt. Every save re-reads the file, so a state another
  process wrote (released) is never overwritten by a stale copy. A pending or unknown submission is never resubmitted;
  `assetgen receipt` adopts it when reconcile or the human finds its request id.
- A pending submit whose .attempt.lock a live process holds is in flight, not unknown: other runs wait (exit 75).
- The provider's own status is kept apart in `queue_status` (fal_job.py defect 3).
"""

import json
import os
import time

from . import asset as assetmod
from . import budget, checks, config, locks, net, outputs, rules
from .errors import AssetgenError, Blocked, InfraError, Refused, UsageError
from .layout import check_id, parse_attempt_id
from .ledger import open_reservations
from .licence import generated_licence, generated_row, write_row
from .providers import driver_for, has_driver
from .providers.base import ProviderFailed, SubmitRejected
from .sniff import sniff_file
from .util import create_json_exclusive, read_json, read_json_or, read_text, sha256_file, sha256_text, utc_iso, write_json_atomic, write_text_atomic

ORDER = {"submission_pending": 0, "submitted": 1, "completed": 2, "downloaded": 3, "settled": 4}
SIDE = {"submission_unknown", "submit_rejected", "provider_error", "released"}
UNRESOLVED = {"submission_pending", "submission_unknown"}
RESUMABLE = {"submitted", "completed", "downloaded"}
NO_CHARGE = {"submit_rejected", "provider_error", "released"}
IMAGE_KINDS = {"image", "image_edit", "sheet", "texture", "icon", "vfx", "panorama"}
MODEL_KINDS = {"mesh", "rig", "motion"}
IMAGE_REF_KINDS = ("png", "jpeg", "webp")
PROMPT_MAX = 64 * 1024
PARAMS_MAX = 256 * 1024
RESULT_MAX = 16 * 1024 * 1024
REF_MAX = 50 * 1024 * 1024


# --- small helpers ----------------------------------------------------------------------------------------------------
def parse_refs(spec, layout, allowed_kinds=IMAGE_REF_KINDS):
    """References are files inside W (never kit or harness files) whose content is an image, or GLB for model endpoints.
    Their bytes go to a provider, so nothing else is ever accepted (a key file must not leave as a 'reference')."""
    refs = []
    if not spec:
        return refs
    for item in [s.strip() for s in str(spec).split(",") if s.strip()]:
        path, sep, role = item.rpartition(":")
        if not sep or not role or "/" in role or "\\" in role or not role.replace("_", "").isalnum():
            path, role = item, "ref"
        p = layout.input_file(path, f"reference {path}", REF_MAX)
        kind, _ = sniff_file(p)
        if kind not in allowed_kinds:
            raise Refused(f"reference {path} is {kind}; this endpoint takes {', '.join(allowed_kinds)} only", reason="unsafe_ref")
        refs.append({"abs": str(p), "path": layout.rel(p), "sha256": sha256_file(p), "role": role, "kind": kind})
    return refs


def read_input_text(layout, path, what, max_bytes):
    return read_text(layout.input_file(path, what, max_bytes))


def read_input_json(layout, path, what, max_bytes):
    try:
        return json.loads(read_input_text(layout, path, what, max_bytes))
    except json.JSONDecodeError as e:
        raise UsageError(f"{what} is not JSON: {e}")


def compose_prompt(preset_cfg, text, params):
    parts = [preset_cfg.get("preamble", "").strip(), text.strip(), preset_cfg.get("suffix", "").strip()]
    prompt = "\n\n".join(p for p in parts if p)
    neg = (preset_cfg.get("negatives") or "").strip()
    if neg:
        if "negative_prompt" in params or preset_cfg.get("negative_param"):
            params.setdefault(preset_cfg.get("negative_param", "negative_prompt"), neg)
        else:
            prompt += f"\n\nAvoid: {neg}."
    return prompt


class Attempt:
    def __init__(self, layout, attempt_id):
        self.id = attempt_id
        self.asset, self.stage, self.a = parse_attempt_id(attempt_id)
        self.dir = layout.attempt_dir(attempt_id)
        self.receipt_path = self.dir / "receipt.json"
        self.result_path = self.dir / "result.json"
        self.request_path = self.dir / "request.json"
        self.checks_path = self.dir / "asset_checks.json"
        self.review_path = self.dir / "review.json"
        self.out_dir = self.dir / "outputs"
        self.lock_path = self.dir / ".attempt.lock"

    def receipt(self):
        return read_json_or(self.receipt_path, None)

    def request(self):
        return read_json_or(self.request_path, None)

    def lock_live(self):
        return locks.is_held(self.lock_path)

    def save(self, rec, state=None, *, adopt=False, force=False, **fields):
        """Write receipt.json. The file on disk wins over a stale in-memory copy: a released or other side state is
        never moved back to a forward state (except adopt: a found request id; force: the job was just submitted)."""
        disk = self.receipt()
        if disk and not force:
            dstate = disk.get("state")
            target = state or rec.get("state")
            if dstate in SIDE and dstate != target and target in ORDER and not (adopt and dstate in UNRESOLVED):
                raise AssetgenError(f"{self.id}: receipt.json is {dstate} (written by another process); not moving it to {target}", reason="receipt_conflict")
            if dstate in ORDER and target in ORDER and ORDER[dstate] > ORDER[target]:
                rec = dict(rec, state=dstate)  # another process got further: never regress
        if state:
            cur = rec.get("state")
            if state in ORDER and cur in ORDER and ORDER[state] < ORDER[cur]:
                state = cur
            if state != cur:
                rec.setdefault("history", []).append({"utc": utc_iso(), "state": state})
            rec["state"] = state
        rec.update(fields)
        write_json_atomic(self.receipt_path, rec)
        return rec


def _attempt_dirs(layout, asset, stage):
    sd = layout.stage_dir(asset, stage)
    if not sd.is_dir():
        return []
    return sorted((p for p in sd.iterdir() if p.is_dir() and p.name[:1] == "a" and p.name[1:].isdigit()), key=lambda p: -int(p.name[1:]))


def allocate(layout, asset, stage):
    sd = layout.stage_dir(asset, stage)
    sd.mkdir(parents=True, exist_ok=True)
    for _ in range(200):
        nums = [int(p.name[1:]) for p in sd.iterdir() if p.is_dir() and p.name[:1] == "a" and p.name[1:].isdigit()]
        nxt = (max(nums) + 1) if nums else 1
        if nxt > 999:
            raise Refused(f"{asset}/{stage} has no attempt numbers left", reason="policy")
        name = f"a{nxt:02d}"
        try:
            (sd / name).mkdir()
            return f"{asset}/{stage}/{name}"
        except FileExistsError:
            continue
    raise Blocked("could not allocate an attempt folder", reason="attempt_alloc")


def peek_next(layout, asset, stage):
    dirs = _attempt_dirs(layout, asset, stage)
    return f"{asset}/{stage}/a{((int(dirs[0].name[1:]) + 1) if dirs else 1):02d}"


def unresolved_submissions(layout, asset, stage):
    """(unknown, in_flight). A pending submit whose attempt lock a live process holds is in flight right now; a pending
    submit without a live holder (the process died mid-submit) or a submission_unknown is unknown."""
    unknown, in_flight = [], []
    for p in _attempt_dirs(layout, asset, stage):
        rec = read_json_or(p / "receipt.json", None)
        if not rec or rec.get("state") not in UNRESOLVED:
            continue
        if rec["state"] == "submission_pending" and locks.is_held(p / ".attempt.lock"):
            in_flight.append(rec["attempt_id"])
        else:
            unknown.append(rec["attempt_id"])
    return sorted(unknown), sorted(in_flight)


def refuse_unresolved(layout, asset, stage, allow=None):
    unknown, in_flight = unresolved_submissions(layout, asset, stage)
    unknown = [x for x in unknown if x != allow]
    if in_flight:
        raise Blocked(f"{', '.join(in_flight)}: another process is submitting on {asset}/{stage} right now; run again when it finishes "
                      f"(this is not an unknown submission: do not release it)", reason="attempt_in_flight", result={"attempts": in_flight})
    if unknown:
        raise InfraError(
            f"{', '.join(unknown)}: a submit may have reached the provider without a request id. Nothing new is submitted for "
            f"{asset}/{stage} until it is resolved: `assetgen reconcile`, then `assetgen receipt <reservation_id> --request-id <id>` if the "
            f"provider shows the job, or `assetgen release <reservation_id> --provider-confirmed-no-charge --reason \"…\"` if it shows none.",
            reason="submission_unknown", result={"attempts": unknown})


def find_same_input(layout, asset, stage, input_sha):
    """(attempt_id, receipt state or None) of the newest attempt holding this exact input, or (None, None)."""
    for p in _attempt_dirs(layout, asset, stage):
        req = read_json_or(p / "request.json", None)
        if not req or req.get("input_sha") != input_sha:
            continue
        rec = read_json_or(p / "receipt.json", None)
        return f"{asset}/{stage}/{p.name}", (rec or {}).get("state")
    return None, None


# --- release (CLI) ----------------------------------------------------------------------------------------------------
def _release_risk(doc, rec, spec):
    """Why this reservation may cover a call that was billed, or None when nothing can have gone out."""
    if doc["status"] in ("open", "expired"):
        return None
    if rec is not None:
        return None if rec.get("state") in NO_CHARGE else f"attempt {doc['attempt_id']} is {rec.get('state')}: a job may have run"
    sentinel = any(t.startswith("mcp__assetgen__") for t in doc.get("tools", []))
    if sentinel and not (spec or {}).get("manual"):
        return None  # assetgen reserved for its own submit and stopped before creating the receipt: nothing was sent
    return "guard G8 let an MCP call through on it" if not sentinel else "it covers a job run by hand"


def release_reservation(ctx, rid, reason, provider_confirmed=False):
    """`assetgen release`: close the money hold. A reservation whose call may have been billed is released only with
    --provider-confirmed-no-charge (the provider shows no job and no charge); the flag is logged in the release line."""
    doc = budget.read_reservation(ctx.layout, rid)
    att = Attempt(ctx.layout, doc["attempt_id"])
    att.dir.mkdir(parents=True, exist_ok=True)
    lk = locks.try_lock(att.lock_path, f"assetgen release {rid}")
    if lk is None:
        raise Blocked(f"{att.id} is in use by another assetgen process (a submit or poll is in flight); nothing was released", reason="attempt_in_flight")
    try:
        rec = att.receipt()
        risk = _release_risk(doc, rec, (att.request() or {}).get("spec"))
        if risk and not provider_confirmed:
            raise Refused(f"reservation {rid}: {risk}. Release only after the provider shows no job and no charge, with "
                          f"--provider-confirmed-no-charge; if it shows the job, record it with `assetgen receipt {rid} --request-id <id>`.",
                          reason="may_be_billed", result={"attempt_id": att.id})
        note = f"provider-confirmed-no-charge: {reason}" if risk else str(reason)
        doc, warn = budget.release(ctx, rid, note)
        if rec and rec.get("state") not in ("settled", "released"):
            att.save(rec, "released", force=True, released_reason=str(note)[:300], released_utc=utc_iso())
    finally:
        lk.release()
    return {"ok": True, "reservation_id": doc["id"], "attempt_id": doc["attempt_id"], "status": doc["status"], "warning": warn,
            "provider_confirmed_no_charge": bool(risk)}


# --- the REST / CLI path ----------------------------------------------------------------------------------------------
def _driver(ctx, pid, endpoint):
    prov = config.provider(ctx.providers, pid)
    ep = config.find_endpoint(ctx.providers, pid, endpoint)
    return prov, ep, driver_for(ctx.layout, pid, prov, ep, endpoint)


def _access_for(pid, prov):
    access = "cli" if "cli" in prov.get("access", []) and pid == "higgsfield" else "rest"
    if access not in prov.get("access", []):
        raise Refused(f"{pid} has no {access} access in providers.json; use the MCP path", reason="policy")
    return access


def run(ctx, a):
    L = ctx.layout
    asset, stage = check_id(a.asset, "--asset"), check_id(a.stage, "--stage")
    preset_cfg, preset_src = config.preset(ctx.style_lock, a.preset)
    for flag, key in (("provider", "provider"), ("endpoint", "endpoint")):
        given, pinned = getattr(a, flag), preset_cfg.get(key)
        if given and pinned and given != pinned and preset_src == "style_lock":
            raise Refused(f"the style lock pins {key} '{pinned}' for preset '{a.preset}' (one model per stage); got '{given}'", reason="policy")
    pid = a.provider or preset_cfg.get("provider")
    endpoint = a.endpoint or preset_cfg.get("endpoint")
    if not pid or not endpoint:
        raise UsageError("--provider and --endpoint are required unless the style-lock preset pins them (there is no default model)")
    prov, ep, drv = _driver(ctx, pid, endpoint)
    access = _access_for(pid, prov)
    kind = (ep or {}).get("kind")
    params = dict(preset_cfg.get("params") or {})
    if a.params_file:
        extra = read_input_json(L, a.params_file, "--params-file", PARAMS_MAX)
        if not isinstance(extra, dict):
            raise UsageError("--params-file must hold a JSON object")
        params.update(extra)
    n = rules.quantity(params, a.n)
    if n > 1 and kind not in IMAGE_KINDS:
        raise UsageError("--n > 1 is only for image endpoints; run one attempt per mesh or audio job")
    if not a.prompt_file:
        raise UsageError("--prompt-file is required (prompts never travel in argv)")
    text = read_input_text(L, a.prompt_file, "--prompt-file", PROMPT_MAX)
    if kind is not None and kind not in IMAGE_KINDS and not (preset_src == "style_lock" and preset_cfg.get("endpoint") == endpoint):
        # Presets are image framing ("plain dark background, even lighting. Avoid: cropped…"): they never steer a mesh,
        # rig or audio job unless the owner wrote the style-lock preset for exactly this endpoint.
        prompt = text.strip()
    else:
        prompt = compose_prompt(preset_cfg, text, params)
    config.require_policy_ok(ctx.style_lock, prompt + "\n" + json.dumps(params))
    refs = parse_refs(a.refs, L, IMAGE_REF_KINDS + (("glb",) if kind in MODEL_KINDS else ()))
    rules.check_refs_allowed(pid, endpoint, ep, refs, a.ref_param)
    ref_param = a.ref_param or rules.ref_param(pid, endpoint, ep) or None
    addons = [s for s in (a.addons or "").split(",") if s]
    spec = {"provider": pid, "endpoint": endpoint, "access": access, "preset": a.preset, "preset_source": preset_src, "params": params,
            "n": n, "ref_param": ref_param, "prompt_param": a.prompt_param, "addons": addons, "lane": a.lane,
            "check_class": a.check_class, "mesh_format": a.mesh_format, "asset_class": a.asset_class}
    prompt_sha = sha256_text(prompt)
    input_sha = sha256_text(json.dumps({"provider": pid, "endpoint": endpoint, "params": params, "refs": [r["sha256"] for r in refs],
                                        "prompt_sha": prompt_sha, "n": n, "addons": sorted(addons), "ref_param": ref_param,
                                        "prompt_param": a.prompt_param}, sort_keys=True))
    if a.dry_run:
        q = budget.quote(ctx, pid, endpoint, n, params, addons, offline=True)
        planned = f"{asset}/{stage}/{a.attempt}" if a.attempt else peek_next(L, asset, stage)
        chk = budget.reserve(ctx, pid=pid, endpoint=endpoint, attempt_id=planned, est=q["est"], unit=q["unit"], access=access, refs=refs, dry_run=True)
        body_keys = sorted(drv.build_input(prompt, params, [], n, ref_param, a.prompt_param).keys()) if access == "rest" else ["job_type", "prompt", "params", "images"]
        return {"ok": True, "dry_run": True, "attempt_id": planned, "provider": pid, "endpoint": endpoint, "access": access, "tier": ctx.tier,
                "est": q["est"], "unit": q["unit"], "quote_source": q["source"], "addons": q.get("addons", []), "spent": chk["spent"], "reserved": chk["reserved"],
                "prompt_sha": prompt_sha, "prompt_lines": prompt.count("\n") + 1, "input_keys": body_keys,
                "refs": [{"path": r["path"], "role": r["role"], "sha256": r["sha256"]} for r in refs], "warnings": chk["warnings"]}
    budget.quote(ctx, pid, endpoint, n, params, addons, offline=True)  # cost params are refused before any folder or money
    body = drv.build_input(prompt, params, refs, n, ref_param, a.prompt_param)
    drv.preflight(body)  # key, launcher and argument safety fail here: before an attempt folder or any money
    explicit = f"{asset}/{stage}/{a.attempt}" if a.attempt else None
    refuse_unresolved(L, asset, stage, allow=explicit)
    if explicit:
        parse_attempt_id(explicit)
        att = Attempt(L, explicit)
        stored = att.request()
        if att.receipt() is not None and stored and stored.get("input_sha") and stored["input_sha"] != input_sha:
            raise Refused(f"{explicit} was submitted with a different prompt, params or refs; omit --attempt to start a new attempt "
                          f"(`assetgen resume {explicit}` finishes the old one)", reason="input_mismatch")
        att.dir.mkdir(parents=True, exist_ok=True)
    else:
        same, state = find_same_input(L, asset, stage, input_sha)
        if same and state == "settled" and not a.new_attempt:
            att = Attempt(L, same)
            return summary(att, att.receipt() or {}, already=True)
        if same and (state is None or state in RESUMABLE):
            att = Attempt(L, same)
        else:
            att = Attempt(L, allocate(L, asset, stage))
    if att.lock_live():
        raise Blocked(f"{att.id} is being run by another assetgen process right now; run again when it finishes", reason="attempt_in_flight",
                      result={"attempt_id": att.id})
    with locks.locked(att.lock_path, f"assetgen run {att.id}", wait_s=float(a.lock_wait_s)):
        return drive(ctx, att, a, spec=spec, prompt=prompt, refs=refs, prompt_sha=prompt_sha, input_sha=input_sha, body=body)


def drive(ctx, att, a, spec=None, prompt=None, refs=None, prompt_sha=None, input_sha=None, body=None):
    L = ctx.layout
    rec = att.receipt()
    if rec is not None:
        state = rec.get("state")
        if state == "settled":
            return summary(att, rec, already=True)
        if state in UNRESOLVED:
            raise InfraError(
                f"{att.id}: the submit may or may not have reached {rec['provider']} (state {state}). It is never resubmitted. "
                f"Run `assetgen reconcile` and check the provider: `assetgen receipt {rec.get('reservation_id')} --request-id <id>` adopts a job it "
                f"shows; `assetgen release {rec.get('reservation_id')} --provider-confirmed-no-charge --reason \"…\"` if it shows none.",
                reason="submission_unknown", result={"attempt_id": att.id, "state": state, "reservation_id": rec.get("reservation_id")})
        if state in SIDE:
            raise Refused(f"{att.id} is closed ({state}); run again for a new attempt", reason=state, result={"attempt_id": att.id, "state": state})
        if rec.get("access") in ("mcp", "manual"):
            raise UsageError(f"{att.id} is an {rec.get('access')} attempt: finish it with `assetgen settle {rec.get('reservation_id')} --result <file>`")
        prov, ep, drv = _driver(ctx, rec["provider"], rec["endpoint"])
        spec = (att.request() or {}).get("spec") or spec or {}  # a resume finishes the job that was submitted, as it was specified
        _ensure_receipt(ctx, rec)
        pool, slots = config.pool_size(ctx.providers, ctx.budget, rec["provider"])
        slot = locks.acquire_slot(L.pools, pool, slots, f"assetgen resume {att.id}", wait_s=float(a.pool_wait_min) * 60)
        try:
            return finish(ctx, att, rec, drv, ep, spec, a)
        finally:
            slot.release()
    if spec is None:
        raise Refused(f"{att.id} has no receipt: nothing was submitted, so there is nothing to resume", reason="no_receipt")
    pid, endpoint, access = spec["provider"], spec["endpoint"], spec["access"]
    prov, ep, drv = _driver(ctx, pid, endpoint)
    write_json_atomic(att.request_path, {"schema": 1, "attempt_id": att.id, "utc": utc_iso(), "spec": spec, "prompt_sha": prompt_sha, "input_sha": input_sha,
                                         "refs": [{"path": r["path"], "sha256": r["sha256"], "role": r["role"]} for r in refs]})
    write_text_atomic(att.dir / "prompt.txt", prompt)
    write_json_atomic(att.dir / "refs.json", [{"path": r["path"], "sha256": r["sha256"], "role": r["role"], "kind": r["kind"]} for r in refs])
    if body is None:
        body = drv.build_input(prompt, spec["params"], refs, spec["n"], spec.get("ref_param"), spec.get("prompt_param"))
    drv.preflight(body)
    facts = drv.precheck(body)  # zero-cost provider check (Tripo Rig Check): refuses before any money
    if facts is not None:
        spec = dict(spec, precheck=facts)
        write_json_atomic(att.request_path, dict(att.request() or {}, spec=spec))
    q = budget.quote(ctx, pid, endpoint, spec["n"], spec["params"], spec["addons"], offline=bool(a.offline_quote), driver=drv, prompt_len=len(prompt))
    pool, slots = config.pool_size(ctx.providers, ctx.budget, pid)
    slot = locks.acquire_slot(L.pools, pool, slots, f"assetgen run {att.id}", wait_s=float(a.pool_wait_min) * 60)
    try:
        lines = ctx.ledger.lines()
        held = [r for r in open_reservations(lines).values() if r.get("attempt_id") == att.id]
        warnings = []
        if held:
            rid = held[0]["reservation_id"]
            doc = budget.read_reservation(L, rid)
            if doc["status"] == "open":
                raise Refused(f"{att.id} holds an MCP reservation ({rid}); finish it through the MCP path", reason="policy")
            est, unit = held[0]["est"], held[0]["unit"]
        else:
            r = budget.reserve(ctx, pid=pid, endpoint=endpoint, attempt_id=att.id, est=q["est"], unit=q["unit"], access=access,
                               lane=spec.get("lane"), refs=refs, prompt_sha=prompt_sha, input_sha=input_sha, lock_wait_s=float(a.lock_wait_s))
            rid, est, unit, warnings = r["reservation_id"], r["est"], r["unit"], r["warnings"]
        if facts and facts.get("warning"):
            warnings = list(warnings) + [facts["warning"]]
        lic = generated_licence(pid, prov, ep, config.plan_of(ctx.accounts, pid), ctx.tier)
        rec = {"schema": 1, "attempt_id": att.id, "provider": pid, "endpoint": endpoint, "access": access, "reservation_id": rid,
               "state": "submission_pending", "queue_status": None, "request_id": None, "created_utc": utc_iso(), "est": est, "unit": unit,
               "unit_est": q.get("unit_est"), "quote_source": q["source"], "licence": lic, "tier": ctx.tier, "outputs": [], "error": None,
               "history": [{"utc": utc_iso(), "state": "submission_pending"}], "warnings": warnings}
        if L.test_mode:
            rec["test_mode"] = True
        if not create_json_exclusive(att.receipt_path, rec):
            raise Blocked(f"{att.id}: another process created the receipt first", reason="race")
        try:
            sub = drv.submit(body, prompt=prompt, refs=refs)
        except InfraError as e:
            # Raised before anything was sent (missing key, refused host, program did not start): not submitted.
            att.save(rec, "submit_rejected", error=e.message[:300])
            budget.release(ctx, rid, f"not submitted: {e.reason or 'infra'}")
            raise
        except SubmitRejected as e:
            att.save(rec, "submit_rejected", error=f"HTTP {e.status}: {e.message}"[:300])
            budget.release(ctx, rid, f"submit_rejected: {e.status}")
            cls = InfraError if e.status in (401, 403) else Refused
            raise cls(f"{pid} refused the submit (HTTP {e.status}): {e.message[:200]}. Nothing was queued; the reservation is released.",
                      reason="key_rejected" if e.status in (401, 403) else "submit_rejected", result={"attempt_id": att.id})
        except net.NoResponse as e:
            att.save(rec, "submission_unknown", error=str(e)[:300])
            ctx.ledger.append({"event": "error", "attempt_id": att.id, "provider": pid, "endpoint": endpoint, "reservation_id": rid,
                               "reason": f"submission_unknown: {str(e)[:200]}"})
            raise InfraError(f"{att.id}: no answer to the submit ({e}). It is NOT resubmitted. Run `assetgen reconcile`; `assetgen receipt {rid} --request-id <id>` "
                             f"adopts the job if the provider shows it, and release needs --provider-confirmed-no-charge.",
                             reason="submission_unknown", result={"attempt_id": att.id, "reservation_id": rid})
        # The job exists now: its request id is written whatever another process did to the files meanwhile.
        rec = att.save(rec, "submitted", force=True, submitted_utc=utc_iso(), **sub)
        try:
            budget.record_receipt(ctx, rid, rec["request_id"])
        except Refused as e:
            # The reservation was closed under us. Keep the request id, say so in the ledger, and still finish, so the
            # settle line records what the job really cost.
            ctx.ledger.append({"event": "error", "attempt_id": att.id, "provider": pid, "endpoint": endpoint, "reservation_id": rid,
                               "request_id": rec["request_id"], "reason": f"receipt refused after a successful submit: {e.message[:200]}"})
            rec = att.save(rec, None, force=True, warnings=list(rec.get("warnings") or []) + ["receipt refused after submit; the settle line records the cost"])
        return finish(ctx, att, rec, drv, ep, spec, a)
    finally:
        slot.release()


def _ensure_receipt(ctx, rec):
    """A crash between writing receipt.json and the ledger receipt line is repaired on resume (idempotent)."""
    if rec.get("request_id") and rec.get("reservation_id"):
        doc = budget.read_reservation(ctx.layout, rec["reservation_id"])
        if doc["status"] in ("open", "used"):
            budget.record_receipt(ctx, rec["reservation_id"], rec["request_id"])


def _provider_call(fn, *args):
    try:
        return fn(*args)
    except net.HttpError as e:
        raise InfraError(f"provider answered HTTP {e.status} ({e.message[:120]}); the job keeps its request id: resume later", reason="provider_down")


def _poll_interval(a):
    ms = os.environ.get("GSK_ASSETGEN_POLL_MS")
    if ms:
        return max(0.0, float(ms) / 1000.0)
    return max(0.5, float(getattr(a, "poll_s", 3.0) or 3.0))


def finish(ctx, att, rec, drv, ep, spec, a):
    """Poll → result → download → checks → settle. Safe to call again at any state."""
    if rec["state"] == "submitted":
        deadline = time.monotonic() + float(a.timeout_min) * 60
        while True:
            st = _provider_call(drv.poll, rec)
            if st["status"] != rec.get("queue_status"):
                rec = att.save(rec, None, queue_status=st["status"])
                ctx.ledger.append({"event": "status", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec["request_id"], "status": str(st["status"])})
            if st["state"] == "completed":
                break
            if st["state"] == "failed":
                return _provider_failed(ctx, att, rec, st.get("error") or st["status"])
            if time.monotonic() > deadline:
                raise InfraError(f"{att.id} is still {st['status']} after {a.timeout_min} min. It keeps request id {rec['request_id']}: "
                                 f"`assetgen resume {att.id}` later (a timeout is not a failure; nothing is resubmitted).",
                                 reason="timeout", result={"attempt_id": att.id, "request_id": rec["request_id"], "state": rec["state"]})
            time.sleep(_poll_interval(a))
        try:
            result = _provider_call(drv.fetch_result, rec)
        except ProviderFailed as e:
            return _provider_failed(ctx, att, rec, str(e))
        write_json_atomic(att.result_path, result)
        rec = att.save(rec, "completed", completed_utc=utc_iso())
        ctx.ledger.append({"event": "complete", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec["request_id"], "status": "completed"})
    if rec["state"] == "completed":
        result = read_json(att.result_path)
        keys = (ep or {}).get("output_keys") or []
        if spec.get("output_keys"):
            keys = spec["output_keys"]
        if not keys:
            raise UsageError(f"{rec['provider']}:{rec['endpoint']} has no output_keys in providers.json; nothing can be allowlisted")
        exclude = outputs.collect_urls(spec.get("params") or {})
        rec = download_outputs(ctx, att, rec, result, keys, exclude, spec.get("mesh_format") or "glb")
    if rec["state"] == "downloaded":
        rec = check_and_settle(ctx, att, rec, drv, ep, spec, a)
    return summary(att, rec)


def _provider_failed(ctx, att, rec, error):
    att.save(rec, "provider_error", error=str(error)[:300])
    ctx.ledger.append({"event": "error", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec.get("request_id"),
                       "reservation_id": rec["reservation_id"], "reason": f"provider_error: {str(error)[:200]}"})
    budget.release(ctx, rec["reservation_id"], f"provider_error: {str(error)[:200]}")
    raise Refused(f"{rec['provider']} reports the job failed: {str(error)[:200]}. The reservation is released (providers do not bill failed jobs [C]; reconcile checks).",
                  reason="provider_error", result={"attempt_id": att.id, "request_id": rec.get("request_id")})


def download_outputs(ctx, att, rec, result, output_keys, exclude, mesh_format="glb", host_ok=None, allow_data=True):
    ref_shas = {r["sha256"] for r in read_json_or(att.dir / "refs.json", [])}
    if att.out_dir.is_dir():
        for stale in att.out_dir.glob("*.part"):  # left by a crash mid-download; the download restarts
            stale.unlink(missing_ok=True)
    items = outputs.extract(result, output_keys, exclude=exclude, keep_mesh=(mesh_format, "glb"), host_ok=host_ok, allow_data=allow_data)
    outs, dropped, seen = [], [], set()
    for i, item in enumerate(items):
        info = net.download(item["url"], att.out_dir, str(i))
        if info["sha256"] in ref_shas or info["sha256"] in seen:
            info["file"].unlink(missing_ok=True)
            dropped.append({"key": item["key"], "why": "input echo" if info["sha256"] in ref_shas else "duplicate"})
            continue
        seen.add(info["sha256"])
        outs.append({"path": ctx.layout.rel(info["file"]), "sha256": info["sha256"], "bytes": info["bytes"], "kind": info["kind"]})
    with ctx.ledger.lock("assetgen download"):
        ctx.ledger.append({"event": "download", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec.get("request_id"), "outputs": outs})
    return att.save(rec, "downloaded", outputs=outs, dropped=dropped, downloaded_utc=utc_iso())


def _check_opts(ctx, spec, cls, asset_class):
    opts = {}
    budgets = (ctx.style_lock or {}).get("class_budgets") or {}
    if cls == "mesh" and asset_class in budgets:
        opts["class_budget"] = asset_class
    if cls == "tile" and "texture" in budgets and budgets["texture"].get("tex_max"):
        opts["tex_max"] = budgets["texture"]["tex_max"]
    return opts


def settle_amount(rec, reported):
    """What the call cost: the provider's reported number when there is one, else the estimate, and never less than
    the per-output estimate times the outputs actually made (a params file can ask for more outputs than --n)."""
    if reported is not None:
        return float(reported), "provider"
    actual = float(rec["est"])
    unit_est = rec.get("unit_est")
    if unit_est:
        made = len(rec.get("outputs") or []) + len([d for d in rec.get("dropped") or [] if d.get("why") == "duplicate"])
        actual = max(actual, round(float(unit_est) * made, 6))
    return actual, "estimate"


def check_and_settle(ctx, att, rec, drv, ep, spec, a, settle_reason=None):
    kind = (ep or {}).get("kind", "image")
    cls = spec.get("check_class") or config.ENDPOINT_KIND_CHECK.get(kind, "image")
    asset_class = spec.get("asset_class") or config.ENDPOINT_KIND_CLASS.get(kind, "other")
    opts = _check_opts(ctx, spec, cls, asset_class)
    files = []
    for o in rec.get("outputs", []):
        path = ctx.layout.ws / o["path"]
        compatible = (cls == "mesh" and o["kind"] in ("glb", "fbx")) or (cls in ("image", "tile", "pano", "icon") and o["kind"] in ("png", "jpeg", "webp")) or (cls == "audio" and o["kind"] in ("wav", "mp3", "ogg", "m4a"))
        if not compatible:
            files.append({"ok": True, "class": cls, "path": o["path"], "metrics": {"format": o["kind"]}, "failures": [], "warnings": [f"not checked as {cls} ({o['kind']})"]})
            continue
        try:
            r = checks.run_check(cls, path, opts, ctx.style_lock)
        except Exception as e:  # a check bug must not block settling money that was spent
            r = {"ok": False, "class": cls, "path": o["path"], "metrics": {}, "failures": [{"check": "check_error", "message": f"{type(e).__name__}: {e}"[:200]}], "warnings": []}
        r["path"] = o["path"]
        files.append(r)
    failures = [dict(f, path=r["path"]) for r in files for f in r["failures"]]
    if not rec.get("outputs"):
        failures.append({"check": "no_outputs", "message": "the job completed but no allowlisted output was found"})
    report = {"schema": 1, "attempt_id": att.id, "utc": utc_iso(), "ok": not failures, "class": cls, "files": files, "failures": failures}
    write_json_atomic(att.checks_path, report)
    reported = None
    try:
        reported = drv.actual(read_json_or(att.result_path, {}), rec) if drv else None
    except Exception:
        reported = None
    actual, source = settle_amount(rec, reported)
    with ctx.ledger.lock("assetgen settle"):
        ctx.ledger.append({"event": "check", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec.get("request_id"),
                           "result": {"ok": report["ok"], "failures": [f"{f.get('path', '')}:{f['check']}" for f in failures][:50]}})
        doc = budget.read_reservation(ctx.layout, rec["reservation_id"])
        if doc["status"] != "settled":
            budget.settle_line(ctx, doc, actual, settle_reason)
        asset_cls = spec.get("asset_class") or config.ENDPOINT_KIND_CLASS.get(kind, "other")
        assetmod.record_attempt(ctx.layout, att.asset, att.stage, att.id, asset_cls, (ctx.style_lock or {}).get("version"))
    return att.save(rec, "settled", actual=float(actual), actual_source=settle_reason or source, settled_utc=utc_iso(), checks_ok=report["ok"])


def summary(att, rec, already=False):
    report = read_json_or(att.checks_path, None)
    out = {"ok": bool(report and report.get("ok")), "attempt_id": att.id, "state": rec.get("state"), "reservation_id": rec.get("reservation_id"),
           "request_id": rec.get("request_id"), "provider": rec.get("provider"), "endpoint": rec.get("endpoint"), "est": rec.get("est"),
           "actual": rec.get("actual"), "unit": rec.get("unit"), "tier": rec.get("tier"), "licence_id": (rec.get("licence") or {}).get("licence_id"),
           "outputs": rec.get("outputs", []), "checks": {"ok": bool(report and report.get("ok")), "failures": (report or {}).get("failures", [])},
           "warnings": rec.get("warnings", [])}
    if rec.get("result_source"):
        out["result_source"] = rec["result_source"]
    if already:
        out["already_settled"] = True
        out["note"] = "this exact input was already generated and settled; nothing was paid. Pass --new-attempt to pay for another sample."
    return out


def resume(ctx, a, attempt_id):
    att = Attempt(ctx.layout, attempt_id)
    if not att.receipt_path.is_file():
        raise Refused(f"{attempt_id} has no receipt: nothing was submitted, so there is nothing to resume", reason="no_receipt")
    if att.lock_live():
        raise Blocked(f"{attempt_id} is being run by another assetgen process right now", reason="attempt_in_flight", result={"attempt_id": attempt_id})
    with locks.locked(att.lock_path, f"assetgen resume {attempt_id}", wait_s=float(a.lock_wait_s)):
        return drive(ctx, att, a)


def resumable_attempts(layout):
    out = []
    if not layout.gen.is_dir():
        return out
    for rp in sorted(layout.gen.glob("*/*/a*/receipt.json")):
        rec = read_json_or(rp, None)
        if rec and rec.get("access") in ("rest", "cli") and rec.get("state") in RESUMABLE:
            out.append(rec["attempt_id"])
    return out


# --- MCP (and manual) path --------------------------------------------------------------------------------------------
def mcp_reserve(ctx, a):
    L = ctx.layout
    asset, stage, _ = parse_attempt_id(a.attempt)
    pid, endpoint = a.provider, a.endpoint
    prov = config.provider(ctx.providers, pid)
    ep = config.find_endpoint(ctx.providers, pid, endpoint)
    tools = [t.strip() for t in (a.tools or "").split(",") if t.strip()]
    if not tools and "mcp" in prov.get("access", []):
        tools = [t for t in prov.get("mcp_mutating_tools", []) if t.endswith("__submit_job")][:1]
        if not tools:
            raise UsageError(f"--tools is required for an MCP reservation on {pid} (the exact mutating tool names)")
    # Without MCP access this is a manual reservation for a CLI or REST job run by hand: receipt and settle close it.
    access = "mcp" if tools else _access_for(pid, prov)
    if not tools and pid == "tripo" and (ep or {}).get("kind") == "rig":
        raise Refused("a Tripo rig is never reserved by hand: `assetgen run` runs the free Rig Check first and records credits_consumed", reason="policy")
    att = Attempt(L, a.attempt)
    if att.receipt_path.is_file():
        raise Refused(f"{a.attempt} already has a receipt; start a new attempt", reason="policy")
    params = {}
    if getattr(a, "params_file", None):
        params = read_input_json(L, a.params_file, "--params-file", PARAMS_MAX)
        if not isinstance(params, dict):
            raise UsageError("--params-file must hold a JSON object")
    n = rules.quantity(params, a.n)
    drv = driver_for(L, pid, prov, ep, endpoint) if has_driver(pid) and not a.offline_quote else None
    addons = [s for s in (a.addons or "").split(",") if s]
    q = budget.quote(ctx, pid, endpoint, n, params, addons, offline=bool(a.offline_quote), driver=drv)
    refs = parse_refs(a.refs, L, IMAGE_REF_KINDS + (("glb",) if (ep or {}).get("kind") in MODEL_KINDS else ()))
    if not a.dry_run:
        refuse_unresolved(L, asset, stage)  # nothing new for an asset/stage while a submit there is unknown or in flight
    r = budget.reserve(ctx, pid=pid, endpoint=endpoint, attempt_id=a.attempt, est=q["est"], unit=q["unit"], tools=tools, lane=a.lane,
                       access=access, refs=refs, dry_run=bool(a.dry_run), lock_wait_s=float(a.lock_wait_s))
    if a.dry_run:
        return dict(r, ok=True, quote_source=q["source"])
    att.dir.mkdir(parents=True, exist_ok=True)
    write_json_atomic(att.request_path, {"schema": 1, "attempt_id": a.attempt, "utc": utc_iso(), "spec": {"provider": pid, "endpoint": endpoint, "access": access,
                                          "tools": tools, "n": n, "lane": a.lane, "addons": q.get("addons", addons), "params": params, "manual": not tools,
                                          "unit_est": q.get("unit_est")},
                                          "refs": [{"path": x["path"], "sha256": x["sha256"], "role": x["role"]} for x in refs]})
    if refs:
        write_json_atomic(att.dir / "refs.json", [{"path": x["path"], "sha256": x["sha256"], "role": x["role"], "kind": x["kind"]} for x in refs])
    return dict(r, ok=True, quote_source=q["source"], tier=ctx.tier)


def mcp_receipt(ctx, rid, request_id, held=False):
    """Record the request id. For an attempt whose submit outcome was unknown (submission_pending/unknown), this adopts
    the job reconcile or the human found: receipt.json moves to submitted, and resume/settle then record its real cost."""
    doc = budget.read_reservation(ctx.layout, rid)
    att = Attempt(ctx.layout, doc["attempt_id"])
    att.dir.mkdir(parents=True, exist_ok=True)
    lk = None
    if not held:
        lk = locks.try_lock(att.lock_path, f"assetgen receipt {rid}")
        if lk is None:
            raise Blocked(f"{att.id} is in use by another assetgen process (a submit or poll is in flight); its own receipt is written by that process",
                          reason="attempt_in_flight")
    try:
        doc, new = budget.record_receipt(ctx, rid, request_id)
        rec = att.receipt()
        adopted = False
        if rec is None:
            prov = config.provider(ctx.providers, doc["provider"])
            ep = config.find_endpoint(ctx.providers, doc["provider"], doc["endpoint"])
            spec = (att.request() or {}).get("spec") or {}
            # A receipt created here is for a job assetgen did not submit: an MCP call, or a job run by hand ("manual").
            access = "mcp" if any(not t.startswith("mcp__assetgen__") for t in doc.get("tools", [])) else "manual"
            rec = {"schema": 1, "attempt_id": doc["attempt_id"], "provider": doc["provider"], "endpoint": doc["endpoint"], "access": access,
                   "reservation_id": rid, "state": "submitted", "queue_status": None, "request_id": str(request_id), "created_utc": utc_iso(),
                   "submitted_utc": utc_iso(), "est": doc["est"], "unit": doc["unit"], "unit_est": spec.get("unit_est"),
                   "licence": generated_licence(doc["provider"], prov, ep, config.plan_of(ctx.accounts, doc["provider"]), ctx.tier),
                   "tier": ctx.tier, "outputs": [], "error": None, "history": [{"utc": utc_iso(), "state": "submitted"}]}
            if doc.get("test_mode") or ctx.layout.test_mode:
                rec["test_mode"] = True
            create_json_exclusive(att.receipt_path, rec) or att.save(rec)
        elif rec.get("state") in UNRESOLVED:
            if rec.get("reservation_id") not in (None, rid):
                raise Refused(f"{att.id} belongs to reservation {rec.get('reservation_id')}, not {rid}", reason="policy")
            rec = att.save(rec, "submitted", adopt=True, request_id=str(request_id), submitted_utc=utc_iso(),
                           adopted={"utc": utc_iso(), "from_state": rec.get("state"), "note": "request id found after an unknown submit (reconcile or provider dashboard)"})
            adopted = True
    finally:
        if lk is not None:
            lk.release()
    out = {"ok": True, "reservation_id": rid, "attempt_id": doc["attempt_id"], "request_id": str(request_id), "status": doc["status"],
           "new": new, "receipt": ctx.layout.rel(att.receipt_path), "tier": rec.get("tier"), "licence_id": (rec.get("licence") or {}).get("licence_id"),
           "state": rec.get("state")}
    if adopted:
        out["adopted"] = True
        out["next"] = f"assetgen resume {att.id}" if rec.get("access") in ("rest", "cli") else f"assetgen settle {rid} --result <file>"
    return out


def _verify_with_provider(ctx, rec):
    """Fetch the job's result from the provider by request id (zero cost) when assetgen has a driver and a key.
    Returns the provider's result, or None when the provider cannot be asked. Refuses when the provider says the job
    is not finished or failed: an agent's result file must not stand in for a job the provider does not show."""
    pid = rec["provider"]
    if not has_driver(pid):
        return None
    try:
        prov, ep, drv = _driver(ctx, pid, rec["endpoint"])
        if drv.access == "rest" and drv.key(required=False) is None:
            return None
        st = drv.poll(rec)
    except (InfraError, net.HttpError, net.NoResponse, UsageError):
        return None
    if st["state"] == "failed":
        raise Refused(f"{pid} reports job {rec['request_id']} failed ({st.get('error') or st['status']}); release the reservation instead of settling",
                      reason="provider_error")
    if st["state"] != "completed":
        raise Refused(f"{pid} reports job {rec['request_id']} is {st['status']}; settle after it completes", reason="not_complete")
    try:
        return drv.fetch_result(rec)
    except (InfraError, net.HttpError, net.NoResponse, ProviderFailed):
        return None


def mcp_settle(ctx, a, rid):
    L = ctx.layout
    doc = budget.read_reservation(L, rid)
    if doc["status"] == "settled":
        att = Attempt(L, doc["attempt_id"])
        return summary(att, att.receipt() or {}, already=True)
    if doc["status"] != "receipted":
        raise Refused(f"reservation {rid} is {doc['status']}: run `assetgen receipt {rid} --request-id <id from submit_job>` first", reason="no_receipt")
    if a.actual is not None:
        if a.actual < 0:
            raise UsageError("--actual cannot be negative")
        if a.actual + 1e-9 < float(doc["est"]) and not (a.actual_source or "").strip():
            raise Refused(f"--actual {a.actual} is below the reserved estimate {doc['est']} {doc['unit']}: name the evidence with --actual-source "
                          f"(for example the provider's billing events or usage page)", reason="policy")
    att = Attempt(L, doc["attempt_id"])
    if att.lock_live():
        raise Blocked(f"{att.id} is in use by another assetgen process", reason="attempt_in_flight")
    with locks.locked(att.lock_path, f"assetgen settle {att.id}", wait_s=float(a.lock_wait_s)):
        rec = att.receipt()
        if rec is None:
            mcp_receipt(ctx, rid, doc["request_id"], held=True)
            rec = att.receipt()
        if rec.get("access") in ("rest", "cli"):
            raise UsageError(f"{att.id} is a {rec.get('access')} attempt that assetgen submitted: finish it with `assetgen resume {att.id}`")
        ep = config.find_endpoint(ctx.providers, doc["provider"], doc["endpoint"])
        keys = [k for k in (a.output_keys or "").split(",") if k] or (ep or {}).get("output_keys")
        if not keys:
            raise UsageError(f"{doc['provider']}:{doc['endpoint']} is not in providers.json: pass --output-keys (the result keys that hold outputs)")
        exclude = outputs.collect_urls(read_input_json(L, a.request, "--request", RESULT_MAX)) if a.request else set()
        prov = config.provider(ctx.providers, rec["provider"])

        def limits(source):
            # An agent-supplied result may only name https outputs on the provider's CDN; never inline data.
            agent_file = source != "provider"
            return ((lambda u: rules.host_allowed(rec["provider"], u, prov)) if agent_file else None), not agent_file

        if rec["state"] == "submitted":
            result = _verify_with_provider(ctx, rec)
            source = "provider"
            if result is None:
                if not a.result:
                    raise UsageError("--result <saved get_job_result JSON> is required")
                result = read_input_json(L, a.result, "--result", RESULT_MAX)
                source = "agent_file"
            host_ok, allow_data = limits(source)
            outputs.extract(result, keys, exclude=exclude, host_ok=host_ok, allow_data=allow_data)  # refuse before the state moves
            write_json_atomic(att.result_path, result)
            rec = att.save(rec, "completed", completed_utc=utc_iso(), result_source=source)
            ctx.ledger.append({"event": "complete", "attempt_id": att.id, "provider": rec["provider"], "request_id": rec["request_id"], "status": "completed"})
        if rec["state"] == "completed":
            host_ok, allow_data = limits(rec.get("result_source"))
            rec = download_outputs(ctx, att, rec, read_json(att.result_path), keys, exclude, a.mesh_format or "glb", host_ok=host_ok, allow_data=allow_data)

        class _Fixed:
            def actual(self, result, receipt):
                return float(a.actual) if a.actual is not None else None

        reason = f"actual source: {a.actual_source.strip()[:200]}" if a.actual is not None and (a.actual_source or "").strip() else None
        spec = {"check_class": a.check_class, "asset_class": a.asset_class}
        if rec["state"] == "downloaded":
            rec = check_and_settle(ctx, att, rec, _Fixed(), ep, spec, a, settle_reason=reason)
        return summary(att, rec)


# --- accept / reject --------------------------------------------------------------------------------------------------
def accept(ctx, a, attempt_id):
    L = ctx.layout
    att = Attempt(L, attempt_id)
    if not a.reason or not a.reason.strip():
        raise UsageError("--reason is required")
    rec = att.receipt()
    if not rec or rec.get("state") != "settled":
        raise Refused(f"{attempt_id} is not settled (state {rec.get('state') if rec else 'none'}); only settled attempts can be accepted", reason="policy")
    report = read_json_or(att.checks_path, {"ok": False})
    if not report.get("ok") and not a.waive_checks:
        raise Refused(f"{attempt_id} failed its checks ({len(report.get('failures', []))} failures); fix or pass --waive-checks with the reason", reason="check",
                      result={"failures": report.get("failures", [])[:10]})
    outs = rec.get("outputs") or []
    if not outs:
        raise Refused(f"{attempt_id} has no outputs", reason="check")
    if a.output == "all":
        chosen = outs
    else:
        i = int(a.output or 0)
        if i < 0 or i >= len(outs):
            raise UsageError(f"--output must be 0..{len(outs) - 1} or all")
        chosen = [outs[i]]
    for o in chosen:
        if not (L.ws / o["path"]).is_file() or sha256_file(L.ws / o["path"]) != o["sha256"]:
            raise Refused(f"{o['path']} is missing or changed since download (sha256)", reason="check")
    tier = ctx.tier
    lic = dict(rec.get("licence") or generated_licence(rec["provider"], None, None, None, tier))
    lic["notes"] = (lic.get("notes", "") + f"; accepted at tier={tier}").strip("; ")
    # A result the agent supplied (MCP settle without provider evidence) or anything made in test mode never ships:
    # the licence row must not call a file provider output when no provider call vouches for it.
    unverified = rec.get("result_source") == "agent_file"
    why_not = "unverified agent-supplied MCP result" if unverified else ("made in test mode" if rec.get("test_mode") else None)
    if why_not:
        lic["notes"] += f"; reference_only: {why_not}"
    ships = tier == "ship" and lic.get("commercial_ok") == "true" and not why_not
    doc = assetmod.load(L, att.asset)
    already = {(x["attempt_id"], x["path"]) for x in doc.get("accepted", [])}
    todo = [o for o in chosen if (attempt_id, o["path"]) not in already]
    if not todo:
        return {"ok": True, "attempt_id": attempt_id, "already_accepted": True, "paths": [o["path"] for o in chosen], "tier": tier}
    with ctx.ledger.lock("assetgen accept"):
        rows = []
        for o in todo:
            row = generated_row(o["path"], o["sha256"], rec["provider"], rec["endpoint"], rec.get("request_id"), rec.get("downloaded_utc") or utc_iso(), lic, ships)
            write_row(L, row, replace=True)
            rows.append(o["path"])
        doc = assetmod.load(L, att.asset)
        st = doc["stages"].setdefault(att.stage, {"attempts": [], "accepted": None})
        if attempt_id not in st["attempts"]:
            st["attempts"].append(attempt_id)
        st["accepted"] = attempt_id
        for o in todo:
            doc.setdefault("accepted", []).append({"attempt_id": attempt_id, "path": o["path"], "sha256": o["sha256"], "utc": utc_iso(), "reason": a.reason})
        doc.setdefault("licence_rows", [])
        doc["licence_rows"] += [p for p in rows if p not in doc["licence_rows"]]
        doc["ai_outputs_at_accept"] = tier
        if (ctx.style_lock or {}).get("version"):
            doc["style_lock_version"] = ctx.style_lock["version"]
        if a.waive_checks and not report.get("ok"):
            doc["notes"] = (doc.get("notes", "") + f" | {attempt_id}: checks waived: {a.reason}").strip(" |")
        assetmod.save(L, doc)
        ctx.ledger.append({"event": "accept", "attempt_id": attempt_id, "provider": rec["provider"], "request_id": rec.get("request_id"),
                           "reason": a.reason, "by": a.by or "agent"})
    write_json_atomic(att.review_path, {"schema": 1, "attempt_id": attempt_id, "verdict": "accept", "reason": a.reason, "by": a.by or "agent",
                                        "utc": utc_iso(), "paths": [o["path"] for o in todo], "checks_waived": bool(a.waive_checks and not report.get("ok"))})
    note = None
    if tier == "reference_only":
        note = "reference_only: accepted as reference; nothing generated enters Assets/"
    elif why_not:
        note = f"ships=false: {why_not}; the licence row is reference_only"
    return {"ok": True, "attempt_id": attempt_id, "paths": [o["path"] for o in todo], "licence_rows": rows, "tier": tier, "ships": ships, "note": note}


def reject(ctx, a, attempt_id):
    att = Attempt(ctx.layout, attempt_id)
    if not a.reason or not a.reason.strip():
        raise UsageError("--reason is required")
    rec = att.receipt()
    if not rec:
        raise Refused(f"{attempt_id} has no receipt", reason="policy")
    with ctx.ledger.lock("assetgen reject"):
        ctx.ledger.append({"event": "reject", "attempt_id": attempt_id, "provider": rec["provider"], "request_id": rec.get("request_id"),
                           "reason": a.reason, "by": a.by or "agent"})
    write_json_atomic(att.review_path, {"schema": 1, "attempt_id": attempt_id, "verdict": "reject", "reason": a.reason, "by": a.by or "agent", "utc": utc_iso()})
    return {"ok": True, "attempt_id": attempt_id, "verdict": "reject"}
