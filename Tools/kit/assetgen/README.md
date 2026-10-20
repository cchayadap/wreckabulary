# assetgen

The only path to paid generation in a Game Studio Kit project (CONTRACTS.md §4.6, ARCHITECTURE §3.7). Installed to
`Tools/kit/assetgen/` as a uv project. Run from the workspace root:

```
uv run --project Tools/kit/assetgen assetgen <command> … [--json]
```

It replaces WoO's `fal_job.py`, `generate.py` and the unshipped `gen.py` (ARCHITECTURE §4.3), rewritten from their
defect lists in `_research/extract_generation.md` §8; no WoO code is reused.

## Commands

| Command | What it does |
|---|---|
| `doctor [--offline]` | Key storage per name (`keyring`, `env` or `missing`, never a value), configs, schemas, plans, zero-cost provider probes, price-sheet age, the licence writer, and expanded keys in `.mcp.json`. |
| `quote --provider P --endpoint E [--n N] [--addons a,b] [--params-file f] [--offline]` | `{est, unit, source, addons}`, where source is `pricing_api`, `providers.json` or `ceiling`. A live price never goes below the list price plus add-ons. Params that change the price (`num_images`, `resolution: 4K`, `enable_web_search`, Tripo/Rodin/Trellis quality options) are priced as add-ons; one assetgen cannot price is refused (`cost_param`). |
| `reserve --provider P --endpoint E --attempt <asset>/<stage>/aNN [--tools mcp__…] [--params-file f] [--refs p:role,…] [--lane L] [--dry-run]` | MCP path step 1: reserve money under `ArtSource/_gen/ledger.lock` and write `.harness/reservations/<id>.json` (`open`). Refused while a submit on that asset/stage is unknown or in flight. A Tripo rig is never reserved by hand (`run` does the free Rig Check). |
| `receipt <reservation_id> --request-id <id>` | MCP path step 3: record the `submit_job` request id before the first `check_job`. Refused while the reservation is still `open` (guard G8 has not let a call through). On an attempt whose submit was unknown it **adopts** the job the provider shows; `resume` or `settle` then records its real cost. |
| `settle <reservation_id> --result <file> [--request <file>] [--actual X --actual-source "…"] [--output-keys k]` | MCP path step 5. assetgen first asks the provider for the job by request id (zero cost) and uses that result; without a driver or key it uses the agent's file, whose outputs must be https URLs on the provider's CDN (`result_source: agent_file`; such outputs never ship). `--actual` below the estimate needs `--actual-source`. |
| `release <reservation_id> --reason "…" [--provider-confirmed-no-charge]` | Free a hold. A reservation whose call may have been billed (used by an MCP call, or an attempt that may have submitted) needs the flag, which is logged in the release line. Refused (75) while another process holds the attempt. |
| `run --asset A --stage S --preset P [--provider P --endpoint E] --prompt-file f [--refs p:role,…] [--n N] [--params-file f] [--new-attempt]` | REST and CLI path: preflight → reserve → receipt → submit → poll → download → checks → settle. Running the same input again resumes its attempt, or returns it `already_settled` (`--new-attempt` pays for another sample); a different prompt, params or refs gets a new attempt. Exit 75 `attempt_in_flight` while another process submits on that asset/stage. |
| `resume <attempt_id>` / `poll --all` | Resume by request id with the spec stored in `request.json`. Nothing is ever resubmitted. |
| `reconcile [--since 2d] [--usage] [--provider p]` | Append `orphan` lines for paid work with no attempt, with the provider's cost (or a provisional list price) as `actual`, which counts as spent. Lists every Meshy task family page by page; skips jobs older than `--since` and undated ones (warning). Reports used reservations that expired without a receipt as `stuck`. `--usage` reads fal billing events with `FAL_ADMIN_KEY`, and only the human runs it. |
| `checks <path> --class image\|tile\|pano\|icon\|mesh\|audio [--out f] [thresholds]` | `{ok, class, metrics, failures}`. `--out` stays inside W and only replaces an earlier checks result. |
| `accept <attempt_id> --reason "…" [--output N\|all] [--waive-checks]` / `reject …` | Write `asset.json` and a licence row through `Tools/kit/ship/license_row.mjs add --from-json -`. Unverified MCP results and test-mode receipts are written with `ships=false`. |
| `asset-set <asset_id> key=value` | The only way other tools edit `asset.json`. |
| `report` / `budget` | Write `Docs/STATS/generation.json` and `Docs/COSTS.md` from the ledger, or print the caps. |
| `fetch-cc0 --source polyhaven\|ambientcg\|kenney --id <id> [--res 4k] [--format hdr] [--out <W-relative dir>] [--replace]` | CC0 download with md5, size or sha256 verification and a licence row. `--out` stays inside W. An existing file with other content is never overwritten (`exists_differs`) unless `--replace`, which first moves it to `.harness/backups/cc0/<UTC>/`. |
| `fetch-refs --manifest <json>` | sha256-pinned private style references, written under `ArtSource/_refs/` only (no hidden path segments) and only when the content is png, jpeg or webp. Never overwrites a changed file. |

Exit codes: 0 ok; 1 refused or FAIL; 2 usage; 3 INFRA; 75 a lock or pool wait expired, or an attempt is in flight.

## Inputs that are sent to providers

`--prompt-file` (64 KiB), `--params-file`, `--refs`, `--result`, `--request` and `--manifest` must be inside W and never
under `.harness/`, `.claude/`, `.git/` or `Tools/kit/`. References must sniff as png, jpeg or webp (GLB for mesh and rig
endpoints). A text-to-image endpoint takes no references: use its `/edit` variant, or `--ref-param` after checking the
model schema. Built-in presets are image framing and never add text to a mesh, rig or audio job. Prompt files may be
UTF-8 or UTF-16 with a BOM (Windows PowerShell 5.1 `>`); anything else is a usage error that names the file.

## Tripo

The Tripo driver is v3 only (`https://openapi.tripo3d.ai/v3`, Bearer `TRIPO_API_KEY`). Before every paid
`v3/animations/rig` it runs the free Rig Check (`POST /v3/animations/rig-check`, GLB input only) and refuses when it
says not riggable. The actual is the task's `credits_consumed`. The task poll route (`GET /v3/tasks/<id>`) and the
response fields are [U]: confirm them with one paid run.

## Files

| File | Written by |
|---|---|
| `providers.json` (here) | the kit (`providers.schema.json`); prices and licences carry `verified_on` |
| `budget.json.tmpl` → `ArtSource/_gen/budget.json` | the human (caps) |
| `ArtSource/_gen/accounts.json` | the human: `{schema:1, providers:{fal:{plan:"pay-as-you-go"}, meshy:{plan:"pro"}}}` |
| `ArtSource/_gen/ledger.jsonl` | assetgen only (`budget.schema.json#/$defs/ledgerLine`) |
| `ArtSource/_gen/<asset>/<stage>/aNN/` | `request.json`, `prompt.txt`, `refs.json`, `receipt.json` (state, request id, licence, tier), `result.json`, `outputs/`, `asset_checks.json`, `review.json` |
| `.harness/reservations/<id>.json`, `.harness/pools/<pool>-<n>.lock` | assetgen (guard G8 marks MCP reservations `used`) |

## Keys (human step)

Keys are read from Windows Credential Manager through Python `keyring` first (service `game-studio-kit`, user = the
variable name), then from the environment. To store one, type it yourself; never paste it into a chat:

```
uv run --project Tools/kit/assetgen python -c "import keyring,getpass; keyring.set_password('game-studio-kit','FAL_KEY',getpass.getpass())"
```

## Tests

From the kit repository:
- `uv run --project skills/game-asset-generation/scripts/project/assetgen pytest tests/unit/assetgen`
- `node --test tests/unit/assetgen/*.test.mjs`

Every test runs offline, against a local mock server (`GSK_ASSETGEN_URL_MAP`, loopback only, test keys only) and a fake
Higgsfield CLI. Test mode is confined: the `--providers` override works only in a workspace under the system temp folder
or the kit's `_scratch/`, the Higgsfield CLI must be a fixture there or in the kit's `tests/`, and every reservation and
receipt written in test mode carries `test_mode: true` (its outputs never ship).
