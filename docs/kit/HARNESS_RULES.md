# Harness rules (game-studio-kit)

This file is installed as `Docs/kit/HARNESS_RULES.md` and imported by the project's `CLAUDE.md`. The kit owns it: `kit.mjs update` replaces it, so put project-specific rules in `CLAUDE.md` instead. Project: Wreckabulary. Log prefix: `[Wreckabulary]`.

These rules are the HOW of the build. The WHAT is `Docs/TASK.md`. Where a rule names a mechanism, the mechanism enforces it; the rule says why it exists.

## 1. Start of every session (after startup, resume, clear or compaction)

1. Read `.harness/brief.md`. The SessionStart hook writes it from the run window, health alerts, the owner inbox, the current milestone, TODO, DEVLOG and the newest wave. If it is missing or stale, run `node Tools/kit/harness/next_action.mjs`.
2. Invoke the `game-studio-run` skill, plus every skill the brief lists under "Re-invoke skills". Compaction keeps only the first part of each skill, so re-invoking restores the rules you are about to rely on.
3. Do the one thing `next_action` names. Anything that is not in a file is forgotten at the next compaction: write it down.

## 2. State files

- **Read** before planning: `Docs/TASK.md` (the spec), `Docs/PLAN.md`, `Docs/TODO.md`, `Docs/KNOWN_ISSUES.md`, `Docs/MAP.md`, the current card in `Docs/qa/gates/`.
- **Append**, never rewrite: `Docs/DEVLOG.md` (`## <UTC> <title>` entries), `Docs/DECISIONS.md`, `Docs/owner_acks.md`, `Docs/CHANGE_REQUESTS.md`, `Docs/qa/baselines.md`.
- **Generated or tool-written; never hand-edit:** `Docs/qa/gates.json`, `Docs/qa/gates.jsonl`, `Docs/REPORT.md`, `Docs/STATS.json`, `Docs/OWNER_INBOX.md`, `Docs/waves/*.json`, `Docs/licenses.csv`. Deny rules and the guard hook refuse hand edits, because a ledger an agent can edit proves nothing.
- Every machine-written time is UTC (`YYYY-MM-DDTHH:MM:SSZ`).

## 3. Verify through the gate only

- Unity work is verified with `node Tools/kit/gate/game_gate.mjs <check|core|compile|test|exec|build|smoke> --json`, never by starting `Unity.exe` yourself. The gate holds the one Unity lock, picks the warm or cold route, and records history.
- Cheapest rung first: `check all` and `core` must be green before any Unity rung.
- Exit 75 (BLOCKED) means an editor or a lock holds the project: stop and report, do not retry in a loop. Exit 3 (INFRA) is infrastructure (licence, missing tool), not a code failure.
- Rows of a milestone card are recorded by their tool with `--row <id>` (`game_gate.mjs … --row M1-03`, `Tools/kit/qa/probe_report.mjs <run dir> --row M1-04`).

## 4. Milestones close only through `gate_close`

- `node Tools/kit/harness/gate_close.mjs <M>` is the only way a milestone closes. It needs a fresh PASS for every row, intact evidence hashes, a current DEVLOG, no due KNOWN_ISSUES recheck, no open guard hit, and a verified owner confirmation for every human row.
- Verdict words are exactly PASS, FAIL, BLOCKED and NOT-RUN. There is no "pass with fixes": fix, re-run, then report.
- A gate needs screenshots **and** numbers. Motion needs clips or frame sheets, because motion defects are invisible in stills.
- Never say a milestone is done because it looks done. Run `gate_close <M> --dry-run --json` and report its blocking rows.

## 5. Lanes and waves

- Contracts first, then parallel builders on disjoint owned paths, then one integrator. Only the integrator launches Unity; builders verify with R0 and R1.
- Every lane prompt starts with `GSK-LANE:<wave>/<unit>`, writes `BuildNotes/<wave>/<unit>.progress.md` first and `<unit>.done.json` (or `.blocked.json`) last.
- Changes to shared contracts are additive only and listed in `contract_changes`; other cross-unit needs go in `requests`.
- Launch waves only from `Tools/kit/workflows/build_wave.workflow.js` with the args from `node Tools/kit/harness/wave_manifest.mjs args <wave>`; the full loop is `Tools/kit/workflows/main_loop.md`. Run one heavy workflow at a time.
- A dead agent means "retry later", never "no issues found".

## 6. The owner

- The owner steers by typing in the session. The UserPromptSubmit hook records every prompt in `Docs/OWNER_INBOX.md` and applies `OWNER:` commands: `pause`, `resume`, `stop`, `confirm <row|CR-n|GH-n>`, `unlock harness`, `lock harness`, and `OWNER-OVERRIDE: <exact command>`.
- Acknowledge each inbox item by appending `- #<n> <UTC> <what you did>` to `Docs/owner_acks.md`. A change request becomes `CR-<n>` in `Docs/CHANGE_REQUESTS.md` with its spec delta, affected gates and cost; it takes effect after `OWNER: confirm CR-<n>`.
- Never mark anything owner-accepted yourself. Status words: implemented, agent-verified, pending owner review (the default for anything visual), owner-confirmed (only after the owner's own confirmation).
- Decide and log in `Docs/DECISIONS.md` instead of asking. Ask only for the human-only steps in `Docs/HUMAN_STEPS.md`.

## 7. Money

- Paid generation runs only through `assetgen` (`uv run --project Tools/kit/assetgen assetgen …`) or an MCP call covered by an open `assetgen reserve` reservation. The guard denies anything else. Hard caps live in `ArtSource/_gen/budget.json`, and only the owner changes them.
- A poll timeout is not a failure: resume by job id. Never resubmit a paid job until the provider confirms it failed.

## 8. Never

- Kill a process by name. Kill by PID only.
- Delete recursively, or move, anything under `Assets/`, `Packages/` or `ProjectSettings/` from a shell. Use the editor through the gate's `exec`.
- Edit `Tools/kit/**`, `.claude/**`, `.mcp.json` or the embedded kit package mid-run (the owner can type `OWNER: unlock harness` for a bounded window).
- Write any owner-control or ledger file from a shell, even with the harness unlocked.
- Run destructive git (reset --hard, clean -f, force push, history rewrite) without a fresh `git bundle` backup and an `OWNER-OVERRIDE:` for that exact command.
- `git commit`, `git init` or `git push` unless `.harness/policy.json` allows it.
- Print, echo or send an API key or token, or enter a password. Keys stay in Windows Credential Manager or the user environment.
- Start `claude` from a shell, or write under `~/.claude/`.

## 9. Evidence

- Every gate: screenshots and numbers; clips or frame sheets for motion; the built player for anything that ships.
- Every claim in DEVLOG, REPORT or a review cites a file you opened. Never describe an image you did not look at.
- Unrecorded numbers are "not recorded", never zero.

## 10. Stopping

- In an autonomous window the keep-working Stop hook may send you back to work. It lets you stop when the owner typed `OWNER: stop`, while the run is paused, while `.harness/WAIT_UNTIL` holds a future reset time, or after three stops without file progress (it then writes `.harness/STALLED`).
- You may create `.harness/ALLOW_STOP` only after `gate_close.mjs wrapup` has passed.
