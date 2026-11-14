"""ArtSource/_gen/ledger.jsonl: append-only, written only by assetgen under ArtSource/_gen/ledger.lock.

Every line is validated against budget.schema.json#/$defs/ledgerLine before it is written. Totals are derived from
the ledger alone: spent = sum of settle.actual plus orphan.actual (costs reconcile found); reserved = the estimates of
reserve lines that no settle or release line has closed yet.
"""

import contextlib

from . import locks, schemas
from .errors import AssetgenError
from .util import append_jsonl, read_jsonl, utc_iso

REQUIRED = {"utc", "event", "provider"}


class Ledger:
    def __init__(self, layout):
        self.layout = layout
        self.path = layout.ledger
        self._depth = 0

    @contextlib.contextmanager
    def lock(self, purpose="assetgen", wait_s=30.0):
        """Re-entrant within one process; cross-process exclusive through ledger.lock."""
        if self._depth:
            self._depth += 1
            try:
                yield
            finally:
                self._depth -= 1
            return
        with locks.locked(self.layout.ledger_lock, purpose, wait_s):
            self._depth = 1
            try:
                yield
            finally:
                self._depth = 0

    def lines(self):
        return read_jsonl(self.path)

    def append(self, line):
        # Windows emulates O_APPEND with seek + write, so every append happens under the ledger lock.
        if not self._depth:
            with self.lock("assetgen append"):
                return self._append(line)
        return self._append(line)

    def _append(self, line):
        line = {k: v for k, v in line.items() if v is not None or k == "request_id"}
        line.setdefault("utc", utc_iso())
        errs = schemas.errors("budget.schema.json", line, "ledgerLine")
        if errs is None:
            missing = REQUIRED - set(line)
            errs = [f"missing {m}" for m in sorted(missing)]
        if errs:
            raise AssetgenError("internal: refusing to write an invalid ledger line: " + "; ".join(errs[:4]), reason="ledger_invalid")
        append_jsonl(self.path, line)
        return line


def open_reservations(lines):
    """reservation_id → reserve line, for reserve lines not closed by settle or release."""
    closed = {l.get("reservation_id") for l in lines if l.get("event") in ("settle", "release") and l.get("reservation_id")}
    out = {}
    for l in lines:
        if l.get("event") == "reserve" and l.get("reservation_id") not in closed:
            out[l["reservation_id"]] = l
    return out


def totals(lines, budget=None):
    prov = {}

    def slot(p):
        return prov.setdefault(p, {"unit": None, "spent": 0.0, "reserved": 0.0, "calls": 0, "orphans": 0})

    for l in lines:
        p = l.get("provider")
        if not p:
            continue
        s = slot(p)
        if l.get("unit") and not s["unit"]:
            s["unit"] = l["unit"]
        ev = l.get("event")
        if ev == "settle":
            s["spent"] += float(l.get("actual") or 0)
        elif ev == "receipt":
            s["calls"] += 1
        elif ev == "orphan":
            s["orphans"] += 1
            # Paid work reconcile found with no attempt (WoO: 131 credits without a job id) counts against the cap:
            # the provider's reported cost, or a provisional list price until a human settles it.
            s["spent"] += float(l.get("actual") or 0)
    for l in open_reservations(lines).values():
        slot(l["provider"])["reserved"] += float(l.get("est") or 0)
    for p, cap in ((budget or {}).get("providers") or {}).items():
        s = slot(p)
        s["unit"] = s["unit"] or cap.get("unit")
        s["soft"] = cap.get("soft")
        s["hard"] = cap.get("hard")
        s["remaining"] = round(cap.get("hard", 0) - s["spent"] - s["reserved"], 6)
    for s in prov.values():
        s["spent"] = round(s["spent"], 6)
        s["reserved"] = round(s["reserved"], 6)
    return prov


def lines_for_attempt(lines, attempt_id):
    return [l for l in lines if l.get("attempt_id") == attempt_id]


def events(lines, attempt_id, event):
    return [l for l in lines if l.get("attempt_id") == attempt_id and l.get("event") == event]
