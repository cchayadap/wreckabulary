"""Higgsfield CLI driver (research_generation_apis §2.1; extract_generation §8.2).

The CLI (npm @higgsfield/cli 1.1.26, bins `higgsfield` and `higgs`; never the alias `hf`, which can be the Hugging Face
CLI) is started as `node <pkg>/bin/higgsfield.js <argv>` so a multi-line prompt reaches it intact: the npm .cmd shim
goes through cmd.exe, which cuts arguments at the first newline (WoO REPORT). Commands used:
`generate create <job_type> --prompt … [--image p]… [--flag v]… --json` (no --wait, so the job id is the receipt before
any poll), `generate get <id> --json`, `generate cost <job_type> … --json`, `generate list --json`, `account --json`.
The JSON field names are [U] (not in the README), so parsing is tolerant. Higgsfield has no provider-side credit cap
[V help center]: the local hard cap is the only one. image_to_3d defaults to --should_texture false [V MODELS.md].
"""

import json
import os
import shutil
from pathlib import Path

from ..errors import InfraError
from ..net import NoResponse
from ..procutil import is_batch, run_cmd
from ..util import in_throwaway
from .base import Driver, ProviderFailed, SubmitRejected, as_number, first_key, parse_created

CREATED_KEYS = ["created_at", "createdAt", "created", "created_utc", "submitted_at", "submittedAt", "timestamp", "start_time"]  # [U]

HUMAN_STEP = "Human step: npm install -g @higgsfield/cli, then `higgsfield auth login` (browser OAuth), then set GSK_HIGGSFIELD_CLI to <npm root -g>/@higgsfield/cli/bin/higgsfield.js if it is not found."
DONE = {"completed", "complete", "succeeded", "success", "done", "finished"}
FAILED = {"failed", "failure", "error", "canceled", "cancelled", "nsfw", "rejected", "expired"}


def launcher():
    """argv prefix that starts the CLI without cmd.exe when possible."""
    explicit = os.environ.get("GSK_HIGGSFIELD_CLI")
    node = shutil.which("node") or "node"
    if os.environ.get("GSK_ASSETGEN_URL_MAP"):
        # Test mode can relabel licences and prices (providers override), and the real CLI needs no key, so only a
        # fixture CLI in a throwaway folder may run: never a real paid Higgsfield job.
        if not explicit or not in_throwaway(explicit, fixtures=True):
            raise InfraError("test mode (GSK_ASSETGEN_URL_MAP) runs only a fixture Higgsfield CLI: set GSK_HIGGSFIELD_CLI to a fake under the "
                             "system temp folder or the kit's tests/ or _scratch/", reason="test_mode_cli")
    if explicit:
        p = Path(explicit)
        if not p.is_file():
            raise InfraError(f"GSK_HIGGSFIELD_CLI points to a missing file. {HUMAN_STEP}", reason="cli_missing")
        if p.suffix.lower() in (".js", ".mjs", ".cjs"):
            return [node, str(p)]
        return [str(p)]
    found = shutil.which("higgsfield") or shutil.which("higgs")
    if not found:
        raise InfraError(f"Higgsfield CLI not found. {HUMAN_STEP}", reason="cli_missing")
    fp = Path(found)
    if is_batch(fp) or fp.suffix.lower() == ".ps1" or fp.suffix == "":
        js = fp.parent / "node_modules" / "@higgsfield" / "cli" / "bin" / "higgsfield.js"
        if js.is_file():
            return [node, str(js)]
    return [str(fp)]


def parse_json_out(text):
    text = (text or "").strip()
    if not text:
        return None
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        pass
    for line in reversed(text.splitlines()):
        line = line.strip()
        if line.startswith(("{", "[")):
            try:
                return json.loads(line)
            except json.JSONDecodeError:
                continue
    return None


class HiggsfieldDriver(Driver):
    access = "cli"

    def _run(self, args, timeout=180):
        code, out, err, timed_out = run_cmd(launcher() + list(args), timeout=timeout)
        return code, parse_json_out(out), out, err, timed_out

    @staticmethod
    def _flags(params):
        argv = []
        for k, v in (params or {}).items():
            if k.startswith("_"):
                continue
            vals = v if isinstance(v, list) else [v]
            for item in vals:
                argv += [f"--{k}", str(item).lower() if isinstance(item, bool) else str(item)]
        return argv

    def build_input(self, prompt, params, refs, n, ref_param=None, prompt_param=None):
        return {"job_type": self.endpoint, "prompt": prompt, "params": dict(params or {}), "images": [r["abs"] for r in (refs or [])]}

    def key(self, required=True):
        return None  # OAuth login held by the CLI itself

    def _create_argv(self, body):
        text = body.get("prompt") or ""
        argv = ["generate", "create", body["job_type"]]
        if text:
            argv += ["--prompt", (" " + text) if text.startswith("-") else text]
        for img in body.get("images", []):
            argv += ["--image", str(img)]
        return argv + self._flags(body.get("params")) + ["--json"]

    def preflight(self, body):
        from ..procutil import check_args

        check_args(launcher() + self._create_argv(body))

    def estimate(self, n, params):
        try:
            code, j, _, _, _ = self._run(["generate", "cost", self.endpoint, *self._flags(params), "--json"], timeout=60)
        except InfraError:
            return None
        if code != 0 or j is None:
            return None
        v = as_number(first_key(j, ["credits", "cost", "total_credits", "estimate", "price"])) if isinstance(j, dict) else as_number(j)
        if v is None:
            return None
        return {"est": round(v * n, 6), "unit": "credits", "source": "pricing_api"}

    def submit(self, body, prompt=None, refs=None):
        code, j, out, err, timed_out = self._run(self._create_argv(body), timeout=180)
        if code is None:
            raise InfraError(f"Higgsfield CLI could not start. {HUMAN_STEP}", reason="cli_missing")
        rid = first_key(j, ["id", "job_id", "jobId", "request_id", "uuid"]) if isinstance(j, dict) else None
        if rid and code == 0 and not timed_out:
            return {"request_id": str(rid)}
        if code == 2 and not timed_out and j is None:
            # CLI usage errors exit before any network call.
            raise SubmitRejected(2, "CLI usage error")
        raise NoResponse("Higgsfield CLI returned no job id" + (" (timed out)" if timed_out else f" (exit {code})"))

    def _get(self, rid):
        code, j, _, _, timed_out = self._run(["generate", "get", str(rid), "--json"], timeout=60)
        if code != 0 or not isinstance(j, dict):
            raise InfraError(f"higgsfield generate get failed (exit {code}{', timed out' if timed_out else ''})", reason="provider_down")
        return j

    def poll(self, receipt):
        j = self._get(receipt["request_id"])
        st = str(first_key(j, ["status", "state"]) or "").lower()
        if st in DONE:
            return {"state": "completed", "status": st, "error": None}
        if st in FAILED:
            return {"state": "failed", "status": st, "error": str(first_key(j, ["error", "message", "reason"]) or st)[:300]}
        return {"state": "queued" if st in ("queued", "pending", "waiting") else "running", "status": st or "unknown", "error": None}

    def fetch_result(self, receipt):
        j = self._get(receipt["request_id"])
        st = str(first_key(j, ["status", "state"]) or "").lower()
        if st and st not in DONE:
            raise ProviderFailed(f"job is {st}")
        return j

    def actual(self, result, receipt):
        return as_number(first_key(result, ["credits_used", "credits", "cost"]))

    def probe(self):
        try:
            code, j, _, _, _ = self._run(["account", "--json"], timeout=60)
        except InfraError as e:
            return {"reachable": None, "detail": e.message}
        if code != 0:
            return {"reachable": False, "detail": f"account exit {code} (not logged in?)"}
        return {"reachable": True, "detail": "account read (zero cost)", "balance": as_number(first_key(j, ["credits", "balance"])) if isinstance(j, dict) else None}

    def list_jobs(self, since, admin=False):
        try:
            code, j, _, _, _ = self._run(["generate", "list", "--json"], timeout=90)
        except InfraError:
            return None
        if code != 0:
            return None
        items = j if isinstance(j, list) else (first_key(j, ["jobs", "items", "data", "results"]) or [])
        out = []
        for it in items if isinstance(items, list) else []:
            rid = first_key(it, ["id", "job_id", "jobId"])
            if rid:
                out.append({"request_id": str(rid), "endpoint": first_key(it, ["job_type", "model", "type"]), "cost": as_number(first_key(it, ["credits", "cost"])),
                            "created": parse_created(first_key(it, CREATED_KEYS))})
        return {"jobs": out, "warnings": []}
