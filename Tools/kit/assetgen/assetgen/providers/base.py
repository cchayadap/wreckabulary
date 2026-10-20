"""Driver interface. A driver never decides money or policy; runner.py and budget.py do."""

from .. import keys
from ..errors import InfraError


class SubmitRejected(Exception):
    """The provider refused the submit outright (4xx): nothing was queued, nothing is billed."""

    def __init__(self, status, message):
        super().__init__(message)
        self.status = status
        self.message = message


class ProviderFailed(Exception):
    """The job exists and the provider reports that it failed."""


PER_OUTPUT_UNITS = {"image", "images", "call", "calls", "request", "requests", "generation", "generations", "unit", "units",
                    "mesh", "meshes", "model", "models", "3d_model", "task", "tasks", "video", "videos"}


class Driver:
    access = "rest"
    list_per_endpoint = False  # reconcile lists every providers.json endpoint of this provider (Meshy task families)

    def __init__(self, layout, pid, prov_cfg, ep_cfg, endpoint):
        self.layout = layout
        self.pid = pid
        self.prov = prov_cfg
        self.ep = ep_cfg or {}
        self.endpoint = endpoint

    # keys ---------------------------------------------------------------------------------------------------------
    def key(self, required=True):
        value, _ = keys.provider_key(self.prov)
        if value is None and required:
            env = (self.prov.get("auth") or {}).get("env", "?")
            raise InfraError(f"{self.pid}: key {env} is missing. " + keys.human_step(env), reason="key_missing")
        return value

    # interface ----------------------------------------------------------------------------------------------------
    def preflight(self, body):
        """Everything that can fail before money moves (key, launcher, argument safety). Raises InfraError."""
        self.key()

    def estimate(self, n, params):
        return None

    def precheck(self, body):
        """A zero-cost provider check that must pass before money moves (Tripo Rig Check). Returns facts or None."""
        return None

    def build_input(self, prompt, params, refs, n, ref_param=None, prompt_param=None):
        raise NotImplementedError

    def submit(self, body, prompt=None, refs=None):
        raise NotImplementedError

    def poll(self, receipt):
        raise NotImplementedError

    def fetch_result(self, receipt):
        raise NotImplementedError

    def actual(self, result, receipt):
        return None

    def probe(self):
        return {"reachable": None, "detail": "no zero-cost probe"}

    def list_jobs(self, since, admin=False):
        """None when the provider cannot be listed, else {"jobs": [{request_id, endpoint, cost, created}], "warnings": []}.
        `created` is an aware datetime or None; reconcile drops undated jobs with a warning."""
        return None


def parse_created(v):
    """A creation time from a provider listing: epoch seconds or milliseconds, or an ISO string. None if unreadable."""
    import datetime as _dt

    from ..util import UTC, parse_utc

    if v is None or isinstance(v, bool):
        return None
    if isinstance(v, (int, float)) or (isinstance(v, str) and v.strip().replace(".", "", 1).isdigit()):
        x = float(v)
        if x <= 0:
            return None
        if x > 1e12:
            x /= 1000.0
        try:
            return _dt.datetime.fromtimestamp(x, UTC)
        except (OverflowError, OSError, ValueError):
            return None
    try:
        return parse_utc(str(v))
    except (ValueError, TypeError):
        return None


def first_key(obj, names):
    if not isinstance(obj, dict):
        return None
    for n in names:
        if n in obj and obj[n] not in (None, ""):
            return obj[n]
    for wrap in ("data", "result", "job", "task"):
        inner = obj.get(wrap)
        if isinstance(inner, dict):
            v = first_key(inner, names)
            if v not in (None, ""):
                return v
    return None


def as_number(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None
