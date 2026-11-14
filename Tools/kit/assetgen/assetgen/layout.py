"""Where every generation file lives (CONTRACTS.md §3.3, §6, §7.5).

W is found from --workspace, then CLAUDE_PROJECT_DIR, then the nearest folder above cwd holding Tools/kit/kit.json,
then cwd. The code folder is this package's parent: `Tools/kit/assetgen/` once installed, or
`skills/game-asset-generation/scripts/project/assetgen/` in the kit repo.
"""

import os
import re
from pathlib import Path

from .errors import Refused, UsageError
from .util import in_throwaway, is_inside

CODE_DIR = Path(__file__).resolve().parent.parent
ID_RE = re.compile(r"^[a-z0-9][a-z0-9_-]*$")
ATTEMPT_RE = re.compile(r"^([a-z0-9][a-z0-9_-]*)/([a-z0-9][a-z0-9_-]*)/(a[0-9]{2,3})$")
PROTECTED_DIRS = (".harness", ".claude", ".git", "Tools/kit")
PROTECTED_FILES = (".mcp.json", "CLAUDE.md", "ArtSource/_gen/budget.json", "ArtSource/_gen/accounts.json", "ArtSource/_gen/ledger.jsonl")


def find_workspace(explicit=None, cwd=None):
    if explicit:
        return Path(explicit).resolve()
    env = os.environ.get("CLAUDE_PROJECT_DIR")
    if env and Path(env).is_dir():
        return Path(env).resolve()
    start = Path(cwd or os.getcwd()).resolve()
    for d in [start, *start.parents]:
        if (d / "Tools" / "kit" / "kit.json").is_file():
            return d
    return start


def check_id(value, what):
    if not isinstance(value, str) or not ID_RE.match(value):
        raise UsageError(f"{what} must match [a-z0-9][a-z0-9_-]* (got {value!r})")
    return value


def parse_attempt_id(attempt_id):
    m = ATTEMPT_RE.match(str(attempt_id or ""))
    if not m:
        raise UsageError(f"attempt id must be <asset_id>/<stage>/aNN (got {attempt_id!r})")
    return m.group(1), m.group(2), m.group(3)


def _first_existing(candidates):
    for c in candidates:
        if c.is_file():
            return c
    return None


class Layout:
    def __init__(self, ws, providers_override=None):
        self.ws = Path(ws).resolve()
        self.code_dir = CODE_DIR
        self.gen = self.ws / "ArtSource" / "_gen"
        self.budget = self.gen / "budget.json"
        self.ledger = self.gen / "ledger.jsonl"
        self.ledger_lock = self.gen / "ledger.lock"
        self.accounts = self.gen / "accounts.json"
        self.cc0 = self.ws / "ArtSource" / "_cc0"
        self.refs = self.ws / "ArtSource" / "_refs"
        self.harness = self.ws / ".harness"
        self.backups = self.harness / "backups"
        self.reservations = self.harness / "reservations"
        self.pools = self.harness / "pools"
        self.docs = self.ws / "Docs"
        self.style_lock = self.docs / "style_lock.json"
        self.generation_stats = self.docs / "STATS" / "generation.json"
        self.costs = self.docs / "COSTS.md"
        self.license_row = self.ws / "Tools" / "kit" / "ship" / "license_row.mjs"
        self.providers_override = providers_override or os.environ.get("GSK_ASSETGEN_PROVIDERS") or None

    # --- config files -------------------------------------------------------------------------------------------
    @property
    def test_mode(self):
        return bool(os.environ.get("GSK_ASSETGEN_URL_MAP"))

    @property
    def providers_json(self):
        if self.providers_override:
            # An edited copy could relabel a research_only endpoint as commercial or price it at 0. Any shell can set
            # the test-mode variable, so the override also needs a throwaway workspace (system temp or the kit's
            # _scratch/): reservations written there are never seen by guard G8 in a real project.
            if not self.test_mode:
                raise UsageError("--providers / GSK_ASSETGEN_PROVIDERS are honoured only in test mode (GSK_ASSETGEN_URL_MAP); edit Tools/kit/assetgen/providers.json instead")
            if not in_throwaway(self.ws):
                raise UsageError("--providers / GSK_ASSETGEN_PROVIDERS are honoured only in a throwaway test workspace (under the system temp folder or the kit's _scratch/)")
            return Path(self.providers_override).resolve()
        found = _first_existing([self.ws / "Tools" / "kit" / "assetgen" / "providers.json", self.code_dir / "providers.json"])
        return found or (self.code_dir / "providers.json")

    # --- what assetgen may read and write -----------------------------------------------------------------------
    def protected(self, path):
        """Kit and harness control files: never an input that is sent to a provider, never a download target."""
        p = Path(path).resolve()
        for rel in PROTECTED_DIRS:
            if is_inside(p, (self.ws / rel).resolve()):
                return rel
        for rel in PROTECTED_FILES:
            if is_inside(p, (self.ws / rel).resolve()):
                return rel
        return None

    def resolve_in_ws(self, path, what):
        """Resolve a user path against W (relative paths are W-relative) and refuse anything outside W."""
        raw = Path(path)
        p = (raw if raw.is_absolute() else (self.ws / raw)).resolve()
        if not is_inside(p, self.ws):
            raise Refused(f"{what} must be inside the workspace ({str(path)!r} is not)", reason="unsafe_path")
        return p

    def input_file(self, path, what, max_bytes=None):
        """A file whose content may be sent to a provider: inside W, not a kit or harness control file, capped."""
        p = self.resolve_in_ws(path, what)
        hit = self.protected(p)
        if hit:
            raise Refused(f"{what} may not come from {hit} (kit and harness files are never sent to a provider)", reason="unsafe_path")
        if not p.is_file():
            raise UsageError(f"{what} not found: {path}")
        if max_bytes is not None and p.stat().st_size > max_bytes:
            raise UsageError(f"{what} is {p.stat().st_size} bytes; the limit is {max_bytes}")
        return p

    def output_target(self, path, what):
        """A file or folder assetgen writes on request: inside W and outside kit and harness control files."""
        p = self.resolve_in_ws(path, what)
        hit = self.protected(p)
        if hit:
            raise Refused(f"{what} may not be written under {hit}", reason="unsafe_path")
        return p

    @property
    def budget_template(self):
        return _first_existing([self.ws / "Tools" / "kit" / "assetgen" / "budget.json.tmpl", self.code_dir / "budget.json.tmpl"])

    def schema_file(self, name):
        """Installed: Tools/kit/schemas/<name> (and Tools/kit/lib/schemas/ for the harness schemas). Repo: the owning skill."""
        skills = self.code_dir.parents[2].parent if len(self.code_dir.parents) > 3 else None
        cands = [
            self.code_dir.parent / "schemas" / name,
            self.code_dir.parent / "lib" / "schemas" / name,
            self.ws / "Tools" / "kit" / "schemas" / name,
            self.ws / "Tools" / "kit" / "lib" / "schemas" / name,
        ]
        if skills is not None:
            cands += [
                skills / "game-asset-generation" / "assets" / "schemas" / name,
                skills / "game-studio-run" / "scripts" / "lib" / "schemas" / name,
                skills / "game-ship" / "assets" / "schemas" / name,
            ]
        return _first_existing(cands)

    # --- per-attempt and per-asset paths ------------------------------------------------------------------------
    def asset_dir(self, asset_id):
        return self.gen / check_id(asset_id, "asset id")

    def asset_json(self, asset_id):
        return self.asset_dir(asset_id) / "asset.json"

    def stage_dir(self, asset_id, stage):
        return self.asset_dir(asset_id) / check_id(stage, "stage")

    def attempt_dir(self, attempt_id):
        asset, stage, a = parse_attempt_id(attempt_id)
        return self.gen / asset / stage / a

    def reservation_file(self, rid):
        if not re.match(r"^r-[0-9]{8}T[0-9]{6}Z-[a-z0-9]{4,12}$", str(rid or "")):
            raise UsageError(f"not a reservation id: {rid!r}")
        return self.reservations / f"{rid}.json"

    def rel(self, path):
        from .util import rel_posix

        return rel_posix(path, self.ws)
