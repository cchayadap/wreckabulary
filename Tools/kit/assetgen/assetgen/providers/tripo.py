"""Tripo direct REST driver, v3 only (ARCHITECTURE §3.7; research_blender_rigging §4, research_generation_apis §2.3).

Base https://openapi.tripo3d.ai/v3 with `Authorization: Bearer <TRIPO_API_KEY>`; v2 is being shut down, so any other
base or a route outside v3/ is refused. Task endpoints: POST v3/generation/image-to-model and multiview-to-model,
POST v3/animations/rig. Before every paid rig assetgen runs the free Rig Check, POST /v3/animations/rig-check (GLB
input only, credits_consumed 0), and refuses the rig unless it says riggable. The actual is the task's
credits_consumed, never the list price (the docs' own example shows 30 for a 25-credit rig).
[U] not in the researched docs: the task poll route (GET /v3/tasks/<task_id> here) and the envelope
{code, data:{task_id, status, output, credits_consumed}}, so parsing is tolerant. Failed or cancelled tasks are not
charged [V docs].
"""

import urllib.parse

from .. import net
from ..errors import InfraError, Refused, UsageError
from .base import Driver, ProviderFailed, SubmitRejected, as_number, first_key

DEFAULT_BASE = "https://openapi.tripo3d.ai/v3"
TASK_ROUTE = "tasks"  # [U]
RIG_CHECK_ROUTE = "animations/rig-check"
DONE = {"success", "succeeded", "completed"}
FAILED = {"failed", "cancelled", "canceled", "banned", "expired", "unknown"}
RIG_INPUT_KEYS = ("input", "original_model_task_id", "model_task_id", "file_token", "model_url", "url")


class TripoDriver(Driver):
    access = "rest"

    @property
    def base(self):
        b = (self.prov.get("base_url") or DEFAULT_BASE).rstrip("/")
        if not b.endswith("/v3"):
            raise InfraError(f"Tripo base_url must be the v3 API (…/v3); got {b!r}. v2 is being shut down.", reason="tripo_v3_only")
        return b

    def _route(self, endpoint=None):
        e = (endpoint or self.endpoint).strip("/")
        if not e.startswith("v3/"):
            raise UsageError(f"Tripo endpoints are v3 routes such as v3/generation/image-to-model (got {e!r}); v2 is being shut down")
        return e[3:]

    def _auth(self, key=None):
        return ("tripo", f"Bearer {key or self.key()}")

    @property
    def is_rig(self):
        return self.ep.get("kind") == "rig" or self._route().startswith("animations/rig")

    def build_input(self, prompt, params, refs, n, ref_param=None, prompt_param=None):
        body = dict(params or {})
        if prompt and prompt_param and prompt_param != "none":
            body.setdefault(prompt_param, prompt)
        if refs:
            if not ref_param:
                raise UsageError("Tripo v3 has no researched upload route: pass the image URL, file token or task id in --params-file")
            uris = [net.data_uri(r["abs"]) for r in refs]
            body[ref_param] = uris if ref_param.endswith("s") else uris[0]
        return body

    def _envelope(self, j, what):
        if isinstance(j, dict) and j.get("code") not in (None, 0, "0"):
            raise SubmitRejected(400, f"{what}: Tripo code {j.get('code')}: {str(j.get('message') or '')[:200]}")
        return j

    def precheck(self, body):
        """The free Rig Check before any paid rig. Returns facts for request.json; raises Refused when not riggable."""
        if not self.is_rig:
            return None
        rig_input = {k: body[k] for k in RIG_INPUT_KEYS if k in body}
        if not rig_input:
            raise UsageError("a Tripo rig needs its input model in --params-file (input: a task id, file token or GLB URL)")
        for v in rig_input.values():
            s = str(v.get("url") if isinstance(v, dict) else v)
            looks_like_file = "/" in s or "." in s.rsplit("/", 1)[-1]
            if looks_like_file and not s.split("?", 1)[0].lower().endswith(".glb"):
                raise Refused("Tripo's Rig Check takes GLB input only; convert the model to GLB first (blender unit)", reason="rig_check_failed")
        try:
            j = net.post_json(f"{self.base}/{RIG_CHECK_ROUTE}", rig_input, auth=self._auth(), timeout=60)
        except net.HttpError as e:
            raise InfraError(f"Tripo Rig Check answered HTTP {e.status}; no paid rig without a passed check", reason="provider_down")
        except net.NoResponse as e:
            raise InfraError(f"Tripo Rig Check gave no answer ({e}); no paid rig without a passed check", reason="provider_down")
        try:
            self._envelope(j, "rig-check")
        except SubmitRejected as e:
            raise Refused(f"Tripo Rig Check refused the input: {e.message}", reason="rig_check_failed")
        riggable = first_key(j, ["riggable", "is_riggable"])
        facts = {"riggable": riggable is True, "rig_type": first_key(j, ["rig_type", "recommended_rig_type"]),
                 "credits_consumed": as_number(first_key(j, ["credits_consumed"]))}
        if riggable is not True:
            raise Refused(f"Tripo Rig Check says the model is not riggable (rig_type {facts['rig_type']!r}); nothing was reserved", reason="rig_check_failed",
                          result={"rig_check": facts})
        wanted = body.get("rig_type")
        if wanted and facts["rig_type"] and str(wanted) != str(facts["rig_type"]):
            facts["warning"] = f"rig_type {wanted} differs from the Rig Check's recommendation {facts['rig_type']}"
        return facts

    def submit(self, body, prompt=None, refs=None):
        try:
            j = net.post_json(f"{self.base}/{self._route()}", body, auth=self._auth(), timeout=60)
        except net.HttpError as e:
            if 400 <= e.status < 500:
                raise SubmitRejected(e.status, e.message)
            raise net.NoResponse(f"HTTP {e.status} on submit")
        self._envelope(j, "submit")
        rid = first_key(j, ["task_id", "id"])
        if not rid or not isinstance(rid, (str, int)):
            raise net.NoResponse("submit response carried no task_id")
        return {"request_id": str(rid)}

    def _task(self, rid):
        j = net.get_json(f"{self.base}/{TASK_ROUTE}/{urllib.parse.quote(str(rid), safe='')}", auth=self._auth(), timeout=30) or {}
        data = j.get("data") if isinstance(j, dict) and isinstance(j.get("data"), dict) else j
        return data if isinstance(data, dict) else {}

    def poll(self, receipt):
        t = self._task(receipt["request_id"])
        st = str(t.get("status") or "").lower()
        if st in DONE:
            return {"state": "completed", "status": st, "error": None}
        if st in FAILED:
            return {"state": "failed", "status": st, "error": str(first_key(t, ["error", "message", "error_msg"]) or st)[:300]}
        return {"state": "queued" if st == "queued" else "running", "status": st or "unknown", "error": None}

    def fetch_result(self, receipt):
        t = self._task(receipt["request_id"])
        if str(t.get("status") or "").lower() not in DONE:
            raise ProviderFailed(f"task is {t.get('status')}")
        return t

    def actual(self, result, receipt):
        return as_number(first_key(result, ["credits_consumed", "consumed_credits"]))
