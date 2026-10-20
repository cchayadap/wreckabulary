"""Meshy REST driver (research_generation_apis §2.4, [V] docs).

POST https://api.meshy.ai/<endpoint> (e.g. openapi/v1/image-to-3d) with `Authorization: Bearer <MESHY_API_KEY>` →
{"result": "<task id>"}; GET <endpoint>/<id> → {status: PENDING|IN_PROGRESS|SUCCEEDED|FAILED|CANCELED|EXPIRED,
model_urls, task_error, consumed_credits?, expires_at}. The API needs a Pro plan; free-plan output is CC BY 4.0, and
Meshy may train on uploads, so refs need style_lock uploads_ok.meshy (checked in budget.reserve).
"""

import urllib.parse

from .. import net
from ..errors import UsageError
from ..rules import MESHY_TEXT_LIMIT
from .base import Driver, ProviderFailed, SubmitRejected, as_number, first_key, parse_created

DEFAULT_BASE = "https://api.meshy.ai"
LIST_PAGE_SIZE = 50
LIST_MAX_PAGES = 20


def text_field(endpoint):
    """The request field for text on a Meshy task family, or None when the task takes no text by default."""
    e = endpoint.lower()
    if "text-to" in e:
        return "prompt"
    if "retexture" in e:
        return "text_style_prompt"
    if "image-to-3d" in e:  # image-to-3d and multi-image-to-3d: optional texturing guidance only
        return "texture_prompt"
    return None  # remesh, rigging: no text


class MeshyDriver(Driver):
    access = "rest"
    list_per_endpoint = True

    @property
    def base(self):
        return (self.prov.get("base_url") or DEFAULT_BASE).rstrip("/")

    def _auth(self, key=None):
        return ("meshy", f"Bearer {key or self.key()}")

    def build_input(self, prompt, params, refs, n, ref_param=None, prompt_param=None):
        body = dict(params or {})
        pp = prompt_param or text_field(self.endpoint)
        text = (prompt or "").strip()
        if text and pp and pp != "none":
            if pp == "prompt" and len(text) > MESHY_TEXT_LIMIT:
                raise UsageError(f"Meshy prompts are limited to {MESHY_TEXT_LIMIT} characters (this one has {len(text)})")
            body.setdefault(pp, text[:MESHY_TEXT_LIMIT])
        if refs:
            name = ref_param or ("image_urls" if "multi-image" in self.endpoint else "image_url")
            uris = [net.data_uri(r["abs"]) for r in refs]
            body[name] = uris if name.endswith("s") else uris[0]
        return body

    def submit(self, body, prompt=None, refs=None):
        try:
            j = net.post_json(f"{self.base}/{self.endpoint.strip('/')}", body, auth=self._auth(), timeout=60)
        except net.HttpError as e:
            if 400 <= e.status < 500:
                raise SubmitRejected(e.status, e.message)
            raise net.NoResponse(f"HTTP {e.status} on submit")
        rid = first_key(j, ["result", "id", "task_id"])
        if not rid or not isinstance(rid, (str, int)):
            raise net.NoResponse("submit response carried no task id")
        return {"request_id": str(rid)}

    def _task(self, rid):
        return net.get_json(f"{self.base}/{self.endpoint.strip('/')}/{urllib.parse.quote(str(rid), safe='')}", auth=self._auth(), timeout=30)

    def poll(self, receipt):
        j = self._task(receipt["request_id"]) or {}
        st = str(j.get("status") or "").upper()
        if st == "SUCCEEDED":
            return {"state": "completed", "status": st, "error": None}
        if st in ("FAILED", "CANCELED", "CANCELLED", "EXPIRED"):
            err = (j.get("task_error") or {}).get("message") if isinstance(j.get("task_error"), dict) else j.get("task_error")
            return {"state": "failed", "status": st, "error": str(err or st)[:300]}
        return {"state": "queued" if st == "PENDING" else "running", "status": st or "UNKNOWN", "error": None}

    def fetch_result(self, receipt):
        j = self._task(receipt["request_id"])
        if str((j or {}).get("status", "")).upper() != "SUCCEEDED":
            raise ProviderFailed(f"task is {j.get('status') if isinstance(j, dict) else 'unknown'}")
        return j

    def actual(self, result, receipt):
        return as_number(first_key(result, ["consumed_credits", "credits_consumed", "credits"]))

    def probe(self):
        key = self.key(required=False)
        if not key:
            return {"reachable": None, "detail": "key missing"}
        try:
            j = net.get_json(f"{self.base}/openapi/v1/balance", auth=self._auth(key), timeout=20, retries=1)
            return {"reachable": True, "detail": "balance read (zero cost)", "balance": as_number(first_key(j, ["balance", "credits"]))}
        except net.HttpError as e:
            return {"reachable": False, "detail": f"balance HTTP {e.status}"}
        except Exception as e:
            return {"reachable": False, "detail": type(e).__name__}

    def list_jobs(self, since, admin=False):
        """Newest first, page by page, until a short page or a page that reaches back past `since` (capped)."""
        key = self.key(required=False)
        if not key:
            return None
        jobs, warnings = [], []
        for page in range(1, LIST_MAX_PAGES + 1):
            url = f"{self.base}/{self.endpoint.strip('/')}?page_num={page}&page_size={LIST_PAGE_SIZE}&sort_by=-created_at"
            j = net.get_json(url, auth=self._auth(key), timeout=30)
            items = j if isinstance(j, list) else first_key(j, ["result", "data", "items"]) or []
            items = items if isinstance(items, list) else []
            oldest = None
            for it in items:
                rid = first_key(it, ["id", "task_id"])
                created = parse_created(it.get("created_at"))
                if created is not None and (oldest is None or created < oldest):
                    oldest = created
                if rid:
                    jobs.append({"request_id": str(rid), "endpoint": self.endpoint, "cost": as_number(first_key(it, ["consumed_credits"])), "created": created})
            if len(items) < LIST_PAGE_SIZE or (oldest is not None and oldest < since):
                break
        else:
            warnings.append(f"meshy {self.endpoint}: listing truncated after {LIST_MAX_PAGES} pages of {LIST_PAGE_SIZE}; older tasks in the window were not checked")
        return {"jobs": jobs, "warnings": warnings}
