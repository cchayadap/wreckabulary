# Windowed smoke proposal — independent source review

Status: SOURCE PASS for the bounded proposal. Review-only; `BuildNotes/client-view/WINDOWED-SMOKE-PROPOSAL.patch` in integration is unapplied. No kit source edits, smoke execution, Unity or builds in this lane. Actual gate validation is NOT RUN.

Scope: the proposed two-file addition of an optional `run(..., { windowsHide })` value, defaulting to `true`, with only `cmdSmoke` opting out for its actual built player. Check process ownership, timeout and verdict preservation, Windows launch semantics and the bounded harness rule before any application.

## Source findings

The patch changes one JSDoc option, one spawn option and the existing smoke-player call. `opts.windowsHide ?? true` preserves hidden launch when omitted or null and honors explicit false. The sole changed caller is the executable selected and validated by `cmdSmoke`; both QA/dev and stripped-release smoke branches use it. The existing configured 1280×720 windowed arguments remain intact. No shell, command string, executable selection or environment change is introduced.

The same direct child PID reaches `onSpawn` and `liveChildren`, and the same completion path removes it. Standard streams, process-group setting, timeout duration, PID-tree termination, drain grace, exit/error results, fresh evidence directories, checks/perf schema validation and final verdict predicates are unchanged. All other `run` calls still use the hidden default; process-query and tree-kill helpers retain their explicit hidden setting. No concrete structural blocker was found.

## Windows meaning and limits

Local version inspection returned Node `v24.15.0` with libuv `1.51.0`. The matching official [Node process wrapper](https://github.com/nodejs/node/blob/v24.15.0/src/process_wrap.cc#L303-L315) maps a true option to `UV_PROCESS_WINDOWS_HIDE`. The matching [libuv Windows spawn implementation](https://github.com/libuv/libuv/blob/v1.51.0/src/win/process.c#L958-L987) supplies a startup show-window flag and selects `SW_HIDE` for that flag, otherwise `SW_SHOWDEFAULT`. Thus false removes the explicit GUI-window suppression as well as the ordinary console-hide request. It does not force focus, foreground ownership or a valid rendered frame; the player's own behavior and desktop session still matter.

This is a supported launch correction for a smoke path documented as normally windowed. Source inspection cannot establish that it alone resolves the earlier black capture or the later gameplay timeout. Root must apply only within the authorized bounded window, then run the real gate smoke and inspect fresh readiness facts, screenshots, timeout/exit evidence and process cleanup. Keep any failing result intact; no alternate-launch success is substituted for a gate pass.

## Unapplied boundary and evidence

The inspected local `docs/kit/HARNESS_RULES.md` section 8 says: “Edit `Tools/kit/**`, `.claude/**`, `.mcp.json` or the embedded kit package mid-run (the owner can type `OWNER: unlock harness` for a bounded window).” This is the explicit rule requiring the owner's bounded unlock before this patch is applied. This report does not authorize or perform that change, and no owner-control or ledger file was edited.

SHA-256 at review: proposal `2B03EA1F2ED7A838B23EF2AEC064B94CFE1498C01F5BE39E9F4B60B69664CDE0`; original `Tools/kit/lib/proc.mjs` `06EF6F12DB15A724B2A005D3B826599C34789C978F493A1B2457F62117DF2C22`; original `Tools/kit/gate/lib/player.mjs` `ABB72087987706E46C63F88F80B656D49B9E173D296E3E883BC6FD6C9114B1D4`. A focused `git diff --name-only` for those two kit files returned no changes. Review consulted their actual source and `Tools/kit/gate/gate.config.json`; the patch was not applied or executed.

Packaging correction: root later normalized only the proposal's CRLF line endings to LF because its initial `git apply --check` failed against LF kit files. Root reports that the corrected dry check passes. Independently rereading the patch confirms the same textual delta and SHA-256 `10F2CC23D83C13BADF2F81683AEB5C706582E406403F56429DEF9F698D0B803C`; both original kit-file hashes above remain unchanged. This is patch applicability evidence, not actual smoke execution. The proposal remains unapplied and bounded owner authorization remains pending.

## 2026-10-24T01:14:19Z Owner-authorized application

The owner explicitly approved this bounded harness fix in the current conversation. Root verified proposal SHA-256 10F2CC23D83C13BADF2F81683AEB5C706582E406403F56429DEF9F698D0B803C, ran git apply --check successfully, and applied exactly the reviewed two-file delta. Both changed JavaScript files pass node --check. No other kit file, deadline, cleanup or verdict condition changed. Official normal-window smoke is the next verification; no successful runtime result is claimed by application alone.
