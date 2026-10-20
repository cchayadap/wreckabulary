# Main loop: what the orchestrating session does

Installed at `Tools/kit/workflows/main_loop.md`. Owner skill: `game-studio-run` (ARCHITECTURE §7.4). The orchestrating session is the only one that launches waves, harvests them and closes gates. Builders, reviewers and the integrator run inside `build_wave.workflow.js`; they never do any of this.

**Contents**
1. The loop
2. Before a wave (once per wave)
3. Launch
4. After every return
5. What each result means
6. Usage limits
7. Unity recovery after any kill
8. Always playable (revert without git)
9. Freeze sequence
10. Rules that keep the loop honest

All commands run from the workspace root W. Paths are W-relative.

## 1. The loop

1. Run `node Tools/kit/harness/next_action.mjs --json`. It prints one `{kind, detail, refs, milestone, wave}` object.
2. Act on `kind`, in the priority order the script already applied:

| kind | Do this |
|---|---|
| `alert` | A stop or emergency health alert. Stop heavy lanes and follow `detail`; see the runbook in the skill's `references/autonomous-runbook.md`. |
| `paused` | The owner paused the run. Read-only work only, until the owner types `OWNER: resume`. |
| `owner` | Read `Docs/OWNER_INBOX.md`. Act, or log a DECISION or a CHANGE_REQUEST, then append `- #<n> <UTC> <ack>` to `Docs/owner_acks.md`. |
| `freeze` | Run the freeze sequence (§9). |
| `fix` | A gate row is FAIL or BLOCKED, or a TODO is open. Turn it into a fix unit or a direct fix, then re-run the row's tool with `--row`. |
| `wave` | A wave has todo or failed units: go to §3. |
| `gate` | Rows lack a PASS: run them. When every machine row passes: `node Tools/kit/harness/gate_close.mjs <M>`. |
| `idle` | Plan the next milestone (§2), or ask the owner for a human-only step. |

3. Append a `## <UTC> <title>` entry to `Docs/DEVLOG.md` after anything that changed state. `gate_close` refuses a milestone whose DEVLOG is older than its evidence.

## 2. Before a wave (once per wave)

1. **Units file.** Write `BuildNotes/<wave>/units.json`:
   `{"units":[{"id","owned":[globs],"context":"BuildNotes/<wave>/<id>.context.md","brief","verify":["check all","core"]}], "contracts":[…same shape…], "gate_rows":["M1-01",…], "skills":["unity-verify-ladder",…]}`.
   - Owned globs are W-relative and disjoint between units. Put the most valuable units first, so a limit kills the cheap tail.
   - `skills` names at most 4 skills besides `game-studio-run`; the SessionStart hook tells a compacted session to re-invoke them.
2. **Manifest.** `node Tools/kit/harness/wave_manifest.mjs init <wave> --milestone <M> --from BuildNotes/<wave>/units.json [--budget 14] [--max-blocked 0]`. Use `--force` only to replace a manifest on purpose.
3. **Excerpt packs.** Write `BuildNotes/<wave>/<unit>.context.md` for every unit: the exact design sections the unit cites, copied out, so no builder pages through huge design files.
4. **Contracts file.** Write or update `BuildNotes/<wave>/CONTRACTS.md`: shared types, the file-ownership map, and the additive-only rule.
5. **Freeze the design.** `node Tools/kit/harness/wave_manifest.mjs freeze <wave> --docs Docs/TASK.md <design docs…>`. Builders on an unfrozen design are how KNOTWELL's M0 started on files that were still being revised.
6. **Snapshot once.** Only when `.harness/snapshots/<wave>/manifest.json` does not exist yet: `node Tools/kit/harness/wave_manifest.mjs snapshot <wave>`. Never re-snapshot a wave that has started: `snapshot` replaces the previous copy, and the revert target must stay the pre-wave state.
7. **Check capacity.**
   - Run one heavy workflow at a time. Two at once died together in KNOTWELL.
   - Usage: in the desktop app, use its usage tool if one is available; otherwise read `.harness/quota.json` if the optional status-line script writes it. When the 5-hour window is above about 70% used, start a small wave or none.
   - Disk and memory: `.harness/health.json` must hold no stop alert.

## 3. Launch

1. `node Tools/kit/harness/wave_manifest.mjs args <wave>` prints one JSON line with only the `todo` and `failed` units.
2. Call the Workflow tool with `scriptPath: "Tools/kit/workflows/build_wave.workflow.js"` and `args` set to that JSON **object** (not a JSON-encoded string).
   - If the tool refuses a script path outside the session folder (U-05), read the file and pass its text as `script` instead, with the same args.
   - Within the same session, `resumeFromRunId` replays cached agents only while the script and args are unchanged. Fresh args from `args <wave>` are the normal relaunch; finished units return at their Step 0.
3. While it runs, do nothing that touches Unity: the integrator owns every Unity launch of the wave.

## 4. After every return (complete, incomplete or red)

1. **Harvest the journal.** `node "<home>/.claude/skills/agent-run-recovery/scripts/harvest_journal.mjs" "<transcriptDir>" --save Docs/runs/<wave> --json`, where `<transcriptDir>` is the run folder the Workflow result names. It writes `<label>.result.json`, `<label>.interrupted.md` and `harvest.json`, and classifies each failure as LIMIT, AUTH_BLIP or OTHER. The script comes with the recovery extension (`kit.mjs install-user --extend-recovery`, owner-approved). Without it, read `<transcriptDir>/journal.jsonl` as the `agent-run-recovery` skill describes.
2. **Update the manifest.** `node Tools/kit/harness/wave_manifest.mjs update <wave> --harvest Docs/runs/<wave>`. Done and blocked markers win; a unit with neither becomes `failed`.
3. **Audit the transcripts.** `node Tools/kit/harness/wave_manifest.mjs audit <wave> --transcripts "<transcriptDir>"`. The guard may not run inside workflow agents (U-29), so this replays every tool call through it. Hits go to `Docs/qa/guard_hits.jsonl`, and `gate_close` refuses the milestone until the owner confirms each one (`OWNER: confirm GH-n`) and you run `wave_manifest.mjs resolve <wave> GH-n --reason "<why>"`.
4. **Log it.** A DEVLOG entry with the result status, the unit counts from `wave_manifest.mjs status <wave>`, and the next step.

## 5. What each result means

| `status` | Meaning | Next |
|---|---|---|
| `invalid-args` | The args failed the script's own check; nothing was launched | Fix `units.json`, re-run `init --force`, relaunch |
| `incomplete`, `stage: canary` | The canary died: a limit or auth problem already stands | §6 |
| `incomplete`, `breaker.tripped` | Several agents died in a row or two retry chains ran out | §6 |
| `incomplete` with `blocked` | A unit (or contract unit) wrote `.blocked.json` | Read `reason` and `needs`; resolve them; `wave_manifest.mjs mark <wave> <unit> --status todo`; relaunch |
| `incomplete`, other stage | A stage got fewer inputs than it needs | Relaunch after step 4; units already done return at once |
| `integrated-red` | Built, but a rung or a gate row failed | Each failing row becomes a TODO or a fix unit in the next wave; a failing test is never weakened |
| `integrated-blocked` | The integrator hit BLOCKED (75): an editor or a lock holds the project | §7, then relaunch (integrate-only when every unit is done) |
| `unaudited` | Green, but an audit agent died | Run the checks yourself: `node Tools/kit/dev/unity_rules_lint.mjs --json`, `node Tools/kit/gate/game_gate.mjs check asmdef --json`, and the done-marker ownership check |
| `complete` | Green and audited | Run the `gate_review` workflow (owner skill `game-qa-probes`), then `node Tools/kit/harness/gate_close.mjs <M>`. On FAIL each blocking row becomes a TODO; on PASS move to the next milestone. |

## 6. Usage limits

1. Read the reset time from the harvest (`resets_text`, for example `resets 4:10am (Asia/Hong_Kong)`) or from `.harness/stopfailure.jsonl` `resets_utc`. Convert it to UTC.
2. Write that UTC time as one line to `.harness/WAIT_UNTIL`. The keep-working Stop hook then lets the session stop, and the supervisor sleeps until the reset plus 2 minutes.
3. Append a DEVLOG entry and end the turn. Do not relaunch into a live limit: every launch dies in about a second.
4. After the reset (interactive auto-continue re-arms at most twice; otherwise the supervisor relaunches the session), start again at §1. The wave relaunch uses fresh args.

## 7. Unity recovery after any kill

1. Look for orphaned `Unity.exe`, `bee_backend.exe` and `UnityShaderCompiler.exe`. Stop them **by PID only**: the guard denies kill-by-name, which once took down an unrelated editor.
2. `node Tools/kit/gate/game_gate.mjs status --json` shows locks and the resident editor. The gate breaks locks whose holder is dead; never delete a lock file by hand.
3. Run `check all` and `core` on the partial tree before relaunching builders.

## 8. Always playable (revert without git)

From M1 on, the gold path must pass after every integration.
1. After an integration, run the gold-path rows of the milestone card.
2. If the gold path broke and there is no obvious fix within 20 minutes: `node Tools/kit/harness/wave_manifest.mjs restore <wave> --dry-run`, read the list, then run it without `--dry-run`. It restores the pre-wave files, moves the wave's new files aside to `.harness/snapshots/_removed/<wave>-<stamp>/`, and marks the wave's done units `failed` with `REVERTED`.
3. Log the revert in DEVLOG and DECISIONS, then relaunch with the lesson in the units' briefs.
4. A polish lane whose probe pass count drops is reverted the same way.

## 9. Freeze sequence (the last 25% of the run window, or the owner says freeze)

1. No new features.
2. In order: build, smoke, perf, captures, trailer cut, `node Tools/kit/harness/report_gen.mjs`, then `node Tools/kit/harness/gate_close.mjs wrapup`.
3. Only after the `wrapup` milestone is closed may you create `.harness/ALLOW_STOP` and write the final DEVLOG entry. The Stop hook ignores `ALLOW_STOP` before that.

## 10. Rules that keep the loop honest

- A dead agent is "retry later", never "no issues found".
- Never hand-edit `Docs/qa/gates.json`, `Docs/qa/gates.jsonl`, `Docs/REPORT.md` or `Docs/STATS.json`. Tools write them.
- Never mark owner acceptance. Human rows close only after the owner types `OWNER: confirm <row>`.
- Never start a second heavy workflow while one runs, and never launch `claude` from a shell (guard G15).
