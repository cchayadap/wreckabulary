"""Licence facts for every receipt and every accepted or downloaded file (ARCHITECTURE §3.7, §3.13; research §5).

Every receipt carries `licence` and `tier` (the style-lock ai_outputs value: ship, reference_only or marketing_only).
Rows go to Docs/licenses.csv only through game-ship's writer: `node Tools/kit/ship/license_row.mjs add --from-json -`,
with the 19 licenses.schema.json columns as keys and string values.
"""

import json
import shutil

from .errors import InfraError, Refused
from .procutil import run_cmd

COLUMNS = ["path", "sha256", "source_kind", "source_url", "licence_id", "licence_url", "author", "attribution_text",
           "retrieved_at_utc", "account_or_plan", "provider", "generator_model", "job_id", "ai_generated", "commercial_ok",
           "training_ban", "redistribution", "ships", "notes"]

# Terms pages opened by the research lane (research_generation_apis.md §2, §5 and the fact-check), verified 2026-09-29.
TERMS_URL = {
    "fal": "https://fal.ai/terms",
    "tripo": "https://www.tripo3d.ai/terms",
    "scenario": "https://www.scenario.com/terms-and-conditions",
    "higgsfield": "https://higgsfield.ai/creator-hub/help-center/account/who-owns-my-generations-and-can-i-use-them-commercially",
    "meshy": "https://www.meshy.ai/pricing",
    "elevenlabs": "https://elevenlabs.io/eleven-music-model-specific-terms",
}
# Bans on training with the outputs (research §5 licence gate): Higgsfield ToS 5.1(iii)/5.2(iv); Recraft §7.2; Tripo
# forbids building competing models. Others are not established.
OUTPUT_TRAINING_BAN = {"higgsfield": "true", "recraft": "true", "tripo": "true"}
PUBLIC_SOURCE = {"fal": "https://fal.ai/models/{endpoint}", "meshy": "https://www.meshy.ai", "higgsfield": "https://higgsfield.ai",
                 "tripo": "https://www.tripo3d.ai", "scenario": "https://www.scenario.com"}


def generated_licence(pid, prov_cfg, ep_cfg, plan, tier):
    """The licence block stored in receipt.json and used for the licence row."""
    badge = (ep_cfg or {}).get("badge", "unknown")
    notes = [f"tier={tier}", f"badge={badge}"]
    plan_l = (plan or "").lower()
    blocked = [p.lower() for p in (prov_cfg or {}).get("blocked_plans", [])]
    attribution_plans = [p.lower() for p in (prov_cfg or {}).get("attribution_plans", [])]
    watermark_plans = [p.lower() for p in (prov_cfg or {}).get("watermark_plans", [])]
    if badge == "research_only" or (plan_l and plan_l in blocked):
        commercial = "false"
    elif ep_cfg is None or (ep_cfg.get("ship") or {}).get("blocked"):
        commercial = "unknown"
        if ep_cfg is not None:
            notes.append("ship blocked: " + (ep_cfg.get("ship") or {}).get("reason", ""))
    elif not plan and (blocked or attribution_plans):
        commercial = "unknown"
        notes.append("plan not recorded")
    else:
        commercial = "true"
    attribution = ""
    if plan_l and plan_l in attribution_plans:
        attribution = f"Created with {prov_cfg.get('name', pid)} ({plan} plan, CC BY 4.0)"
        notes.append("attribution required")
    if plan_l and plan_l in watermark_plans:
        notes.append("watermark embedded at generation")
    if tier == "reference_only":
        notes.append("reference_only: never placed in Assets/")
    endpoint = (ep_cfg or {}).get("endpoint", "")
    return {
        "licence_id": f"AI-generated:{pid}:{endpoint}" if endpoint else f"AI-generated:{pid}",
        "licence_url": TERMS_URL.get(pid, ""),
        "badge": badge,
        "commercial_ok": commercial,
        "training_ban": OUTPUT_TRAINING_BAN.get(pid, "unknown"),
        "attribution_text": attribution,
        "account_or_plan": plan or "",
        "tier": tier,
        "notes": "; ".join(notes),
    }


def generated_row(path_rel, sha256, pid, endpoint, request_id, retrieved_utc, lic, ships):
    src = PUBLIC_SOURCE.get(pid, "").format(endpoint=endpoint)
    return {
        "path": path_rel, "sha256": sha256, "source_kind": "generated", "source_url": src, "licence_id": lic["licence_id"],
        "licence_url": lic["licence_url"], "author": "", "attribution_text": lic["attribution_text"], "retrieved_at_utc": retrieved_utc,
        "account_or_plan": lic["account_or_plan"], "provider": pid, "generator_model": endpoint, "job_id": request_id or "",
        "ai_generated": "true", "commercial_ok": lic["commercial_ok"], "training_ban": lic["training_ban"], "redistribution": "allowed",
        "ships": "true" if ships else "false", "notes": lic["notes"],
    }


def cc0_row(path_rel, sha256, source, source_url, author, retrieved_utc, ships, notes=""):
    return {
        "path": path_rel, "sha256": sha256, "source_kind": "cc0_download", "source_url": source_url, "licence_id": "CC0-1.0",
        "licence_url": "https://creativecommons.org/publicdomain/zero/1.0/", "author": author, "attribution_text": "",
        "retrieved_at_utc": retrieved_utc, "account_or_plan": "", "provider": source, "generator_model": "", "job_id": "",
        "ai_generated": "false", "commercial_ok": "true", "training_ban": "false", "redistribution": "allowed",
        "ships": "true" if ships else "false", "notes": notes,
    }


def writer_available(layout):
    return layout.license_row.is_file() and shutil.which("node") is not None


def write_row(layout, row, replace=False):
    """Add one row through license_row.mjs. Raises InfraError (writer missing) or Refused (row refused)."""
    missing = [c for c in COLUMNS if c not in row]
    if missing:
        raise Refused(f"internal: licence row lacks {missing}", reason="licence")
    if not layout.license_row.is_file():
        raise InfraError("Tools/kit/ship/license_row.mjs is missing: install the kit (game-ship) before accepting or fetching assets", reason="licence_writer_missing")
    node = shutil.which("node")
    if not node:
        raise InfraError("node is not on PATH (needed for Tools/kit/ship/license_row.mjs)", reason="node_missing")
    argv = [node, str(layout.license_row), "add", "--from-json", "-"] + (["--replace"] if replace else []) + ["--json"]
    code, out, err, timed_out = run_cmd(argv, timeout=60, cwd=str(layout.ws), input_text=json.dumps({c: str(row[c]) for c in COLUMNS}))
    if code == 0:
        return True
    msg = (err or out).strip().splitlines()[-1:] or ["no output"]
    if code == 1:
        raise Refused(f"license_row.mjs refused the row for {row['path']}: {msg[0][:300]}", reason="licence")
    raise InfraError(f"license_row.mjs failed (exit {code}{', timed out' if timed_out else ''}): {msg[0][:300]}", reason="licence_writer")
