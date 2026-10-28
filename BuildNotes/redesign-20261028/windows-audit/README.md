# Windows delivery evidence audit

Read-only audit of an explicit release-QA Windows build and three explicit smoke runs. The only output is a new JSON receipt; an existing output is refused. No Unity/player launches, source edits, purchases, network operations or preserved-asset rehash sweep occur.

Expected current contract: lobby 24 PNGs / 5 steps, solo 13 PNGs / 4 steps, workshop 8 PNGs / 5 steps. These names come from the current `LobbyExperienceQa.cs`, `WordboundSoloQa.cs` and `WordboundWorkshopQa.cs`, not from whatever files happen to exist. Old runs missing Finishes/Equipment or Parcel Hall are rejected.

Run with Node 24 (or another compatible Node runtime):

```powershell
node BuildNotes/redesign-20261028/windows-audit/verify-windows-delivery.mjs `
  --root C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration `
  --build EXACT_YYYYMMDDTHHMMSSZ `
  --source-commit FULL_BUILT_COMMIT_SHA `
  --build-gate EXACT_BUILD_GATE_STDOUT_PATH `
  --lobby-run Docs/qa/runs/EXACT_TIMESTAMP-lobby_experience `
  --lobby-gate EXACT_LOBBY_GATE_STDOUT_PATH `
  --solo-run Docs/qa/runs/EXACT_TIMESTAMP-wordbound_solo `
  --solo-gate EXACT_SOLO_GATE_STDOUT_PATH `
  --workshop-run Docs/qa/runs/EXACT_TIMESTAMP-wordbound_workshop_flow `
  --workshop-gate EXACT_WORKSHOP_GATE_STDOUT_PATH `
  --preservation EXACT_ROOT_FINAL_PRESERVATION_JSON `
  --out BuildNotes/redesign-20261028/windows-audit/NEW_AUDIT_NAME.json
```

Every option is mandatory, explicit and unique. Paths may be absolute or relative to root. Run paths must be canonical `Docs/qa/runs/<id>` folders. Gate files must each contain one terminal JSON result; concatenated retries are rejected. Do not substitute a previous build's run or copy images between runs.

## Checks

- Exact successful build ID, full source commit, release-QA kind, binary pack identity and executable/managed artifact presence. Every packed file is size-checked and SHA256-hashed; recorded packed hashes are checked when present.
- Current tracked/nonignored production inputs in Assets/Packages/ProjectSettings must match the built commit; untracked nonignored production inputs reject the audit.
- Smoke command/run/build identity, player exit 0, no timeout/stripping, exact checks receipt, source commit/tree when embedded, terminal PASS and console 0 errors/exceptions/warnings.
- Exact step/capture names/order/count; no extra PNGs; PNG signature/IHDR/IEND, dimensions and current SHA256; available runtime frame/focus/pixel observations.
- All original control-legend labels, row visibility, retained bindings, cleanup budgets, preference preservation and return checks from the previous verifier remain.
- Lobby human counts and three Solo one-human Single-runner observations; borrowed invoking input and wrapper disposal. Workshop has no independent roster probe, which is stated rather than inferred.
- Workshop exact copied autosave, current isolated `Temp/wbqa/<guid>` primary hash, no temporary write leftovers, saved seed/range fields, view-only restoration, live advancing playtest, destroyed client/camera on return and Undo/Redo.
- Root final preservation receipt must be PASS, unchanged and later than all three runs. Its baseline hash, scope, file count, unique bounded paths and baseline-commit ancestry are verified. Authored asset/font contents are not rehashed again. The receipt records this delegated preservation boundary.

A failed audit exits nonzero and does not publish a pass receipt. A pass is deliberately named `BOUNDED_LOBBY_SOLO_WORKSHOP_WINDOWS_PASS`; it does not claim pixel approval, physical input, full story completion, public/private matchmaking, multiplayer/bots, performance or production readiness. Three Workshop PNGs lack per-image frame/focus/pixel probes; their same-window dimensions and bytes are checked, and that limitation is recorded. Source `build.tree` is the harness metadata freshness fingerprint, not a content hash. Current binary hashes do not establish historic per-run byte identity when the smoke lacks it.

## Validator verification

`node --test BuildNotes/redesign-20261028/windows-audit/verify-windows-delivery.test.mjs`

62 synthetic tests passed on 2026-10-28, including the positive 45-capture fixture, output overwrite rejection, stale/missing views, roster/source changes, reused/failed gates, bad PNG metadata, workshop autosave/return errors, stale preservation receipts and the serializer's omitted captures field on a step with no captures. Missing captures remain rejected on a step that requires images. Synthetic fixtures are explicitly labelled non-game data, use isolated temporary folders and are removed after each test.

The actual build `20261028T155755Z` and lobby `155953`, solo `160051`, workshop `160232` runs passed the bounded audit in [windows155755-root.json](windows155755-root.json). This receipt records the exact then-current production sources; later work must use a fresh build and receipt. The owner subsequently rejected the menu styling. This behavior/provenance pass does not imply visual acceptance.
