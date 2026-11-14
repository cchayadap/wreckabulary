"""fal.ai REST queue driver (replaces WoO fal_job.py; ARCHITECTURE §4.3).

Auth is `Authorization: Key <FAL_KEY>` on REST (the MCP uses Bearer) [V research_generation_apis §2.2]. Flow:
POST https://queue.fal.run/<endpoint> → {request_id, status_url, response_url} → GET status → GET response.
A COMPLETED status that carries error/error_type is a failure (fal_job.py defect 2). Prices come from
GET https://api.fal.ai/v1/models/pricing?endpoint_id=<id> (zero cost). Usage and billing-events need an admin key and
are only used by `reconcile --usage`, which the human runs.
"""

import datetime as _dt
import urllib.parse

from .. import net
from ..errors import UsageError
from ..util import utc_iso
from .base import PER_OUTPUT_UNITS, Driver, ProviderFailed, SubmitRejected, as_number, first_key, parse_created

API_BASE = "https://api.fal.ai"
DEFAULT_QUEUE = "https://queue.fal.run"
IMAGE_KINDS = {"image", "image_edit", "sheet", "texture", "icon", "vfx", "panorama"}


class FalDriver(Driver):
    access = "rest"

    @property
    def queue(self):
        return (self.prov.get("base_url") or DEFAULT_QUEUE).rstrip("/")

    def _auth(self, key=None):
        return ("fal", f"Key {key or self.key()}")

    def _app(self):
        parts = self.endpoint.strip("/").split("/")
        return "/".join(parts[:2]) if len(parts) >= 2 else self.endpoint

    def _urls(self, receipt):
        rid = receipt["request_id"]
        base = f"{self.queue}/{self._app()}/requests/{urllib.parse.quote(rid, safe='')}"
        return receipt.get("status_url") or f"{base}/status", receipt.get("response_url") or base

    # money --------------------------------------------------------------------------------------------------------
    def estimate(self, n, params):
        key = self.key(required=False)
        if not key:
            return None
        url = f"{API_BASE}/v1/models/pricing?endpoint_id={urllib.parse.quote(self.endpoint, safe='/')}"
        try:
            j = net.get_json(url, auth=self._auth(key), timeout=30)
        except Exception:
            return None
        items = j if isinstance(j, list) else (j or {}).get("prices") or (j or {}).get("data") or []
        for it in items:
            if not isinstance(it, dict):
                continue
            if it.get("endpoint_id") not in (None, self.endpoint):
                continue
            price = as_number(it.get("unit_price"))
            unit = str(it.get("unit") or "").lower()
            if price is None:
                continue
            if unit in PER_OUTPUT_UNITS:
                return {"est": round(price * n, 6), "unit": "USD", "source": "pricing_api", "unit_price": price, "price_unit": unit}
            return None
        return None

    # submit / poll / result ---------------------------------------------------------------------------------------
    def build_input(self, prompt, params, refs, n, ref_param=None, prompt_param=None):
        body = dict(params or {})
        pp = prompt_param or "prompt"
        if prompt and pp != "none":
            body.setdefault(pp, prompt)
        if n > 1 and (self.ep.get("kind") in IMAGE_KINDS) and "num_images" not in body:
            body["num_images"] = n
        if refs:
            if not ref_param:
                raise UsageError(f"{self.endpoint} has no reference field; pass --ref-param after checking the model schema")
            uris = [net.data_uri(r["abs"]) for r in refs]
            body[ref_param] = uris if ref_param.endswith("s") else uris[0]
        return body

    def submit(self, body, prompt=None, refs=None):
        url = f"{self.queue}/{self.endpoint.strip('/')}"
        try:
            j = net.post_json(url, body, auth=self._auth(), timeout=60)
        except net.HttpError as e:
            if 400 <= e.status < 500:
                raise SubmitRejected(e.status, e.message)
            raise net.NoResponse(f"HTTP {e.status} on submit")
        rid = (j or {}).get("request_id")
        if not rid:
            raise net.NoResponse("submit response carried no request_id")
        out = {"request_id": str(rid)}
        for k in ("status_url", "response_url", "cancel_url"):
            if isinstance(j.get(k), str):
                out[k] = j[k]
        return out

    def poll(self, receipt):
        status_url, _ = self._urls(receipt)
        j = net.get_json(status_url, auth=self._auth(), timeout=30)
        st = str((j or {}).get("status") or "").upper()
        err = (j or {}).get("error") or (j or {}).get("error_type")
        if st == "COMPLETED":
            if err:
                return {"state": "failed", "status": st, "error": str(err)[:300]}
            return {"state": "completed", "status": st, "error": None}
        if st in ("FAILED", "ERROR", "CANCELLED", "CANCELED"):
            return {"state": "failed", "status": st, "error": str(err or st)[:300]}
        return {"state": "queued" if st == "IN_QUEUE" else "running", "status": st or "UNKNOWN", "error": None}

    def fetch_result(self, receipt):
        _, response_url = self._urls(receipt)
        try:
            return net.get_json(response_url, auth=self._auth(), timeout=60)
        except net.HttpError as e:
            if 400 <= e.status < 500 and e.status not in (401, 403, 429):
                raise ProviderFailed(f"result HTTP {e.status}: {e.message}")
            raise

    def probe(self):
        key = self.key(required=False)
        if not key:
            return {"reachable": None, "detail": "key missing"}
        try:
            net.get_json(f"{API_BASE}/v1/models/pricing?endpoint_id={urllib.parse.quote(self.endpoint or 'fal-ai/nano-banana-pro', safe='/')}", auth=self._auth(key), timeout=20, retries=1)
            return {"reachable": True, "detail": "pricing API answered (zero cost)"}
        except net.HttpError as e:
            return {"reachable": False, "detail": f"pricing API HTTP {e.status}"}
        except Exception as e:
            return {"reachable": False, "detail": type(e).__name__}

    def list_jobs(self, since, admin=False):
        """Per-request billing events (admin key FAL_ADMIN_KEY; human-run `reconcile --usage` only)."""
        if not admin:
            return None
        from .. import keys

        admin_key, _ = keys.lookup("FAL_ADMIN_KEY", {"service": keys.SERVICE, "username": "FAL_ADMIN_KEY"})
        if not admin_key:
            return None
        end = utc_iso()
        url = f"{API_BASE}/v1/models/billing-events?start={urllib.parse.quote(utc_iso(since))}&end={urllib.parse.quote(end)}&limit=500"
        j = net.get_json(url, auth=("fal", f"Key {admin_key}"), timeout=60)
        items = j if isinstance(j, list) else first_key(j, ["billing_events", "items", "data", "events"]) or []
        out = []
        for it in items if isinstance(items, list) else []:
            rid = first_key(it, ["request_id", "requestId", "id"])
            if rid:
                # The query itself is bounded by start/end, so an event without a readable time is still in the window.
                out.append({"request_id": str(rid), "endpoint": it.get("endpoint_id"), "cost": as_number(first_key(it, ["cost_total", "cost", "amount"])),
                            "created": parse_created(first_key(it, ["timestamp", "created_at", "time"])), "in_window": True})
        return {"jobs": out, "warnings": []}
