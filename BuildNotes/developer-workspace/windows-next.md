# Next Windows build and smoke

STATUS: COMPLETE — runbook only. Bounded read-only preparation at HEAD8304fa9375688952faffa64ee0ecb1f5327e8961; this report is the only write. Root owns every build/native/player/service job. No Assets, gate settings, original art or other lane files changed.

Scope: inspect the current gate and prior Windows evidence, then give the smallest current-integration build/smoke sequence covering original equipment, workshop and public-mode entry without duplicating the separate capacity runbook or claiming full-match acceptance.

## Starting evidence and build boundary

Preserve the existing `Builds/qa-20261024T034544Z` review build. Its build.json says source9e013dc9, Unity6000.6.4f1, nondevelopment release-qa, successful build236.3s; the outer gate recorded237.3s. Official workshop run035028 passed5/5 in60.7s. Equipment run035159 passed5/5 with13 commands, but its receipt explicitly calls it a visible diagnostic, not an official-gate replacement. Both belong to the old executable, not current source. References: `BuildNotes/client-view/official-short-editor-pass-20261024.json`, `windows-original-equipment-pass-20261024.json`, `TRY-WORDBOUND.md`.

Current source has **six** equipment QA steps: the original five plus `equipment_bat_motion`. It submits an actual BAT Attack, validates its exact canonical receipt/weapon/cooldown, captures baseline/windup/active/recovery/complete with at most8 GPU buffers, and requires measurable hand/held-weapon motion and at least2 distinct attack images. The old5/5 result does not cover it. Workshop remains5 steps. No current Windows build/run was performed during this review.

At review time LobbyPages/LobbyStage, portrait diagnostics and new Loadout/camera source/tests were still dirty/untracked; several new source files had no Unity metadata yet. Root must finish its already-owned native setup/validation and freeze the intended build inputs first. Preserve all lane work. Checkpoint the exact source/diff and saved profile/layout/font/portrait asset hashes; do not equate HEAD alone with uncommitted build inputs. The gate records both HEAD and a freshness-tree hash in build.json. Stop source/assets changing during build and subsequent comparisons.

Use existing profiles, not profile regeneration: `Assets/Settings/Build Profiles/Win-QA.asset` is Windows64, nondevelopment, GSK_QA; `Win.asset` is nondevelopment without QA. Both use global scene order. Current enabled scenes are Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby and WordboundGameplay. Authoring studios are not player startup scenes. Root should retain the profile/launch-asset checks already established; any unexpected profile/scene change needs source correction rather than an unchecked build bypass.

## Minimal controlled Windows sequence

Serially: **one release-qa build → frontend smoke → workshop smoke → original-equipment smoke**. Use exact returned build ID for all three; do not use `latest`. Build1800s and smoke90s plus the existing30s grace retain the earlier limits. Smoke launches a normal visible1280×720 player, with neither batchmode nor headless rendering; leave its window available for presented-frame captures. Editor/project locks must be free through root's existing gate ownership. No other task's editor/process is to be closed or killed.

Preparation in root's controlled PowerShell process:

```powershell
Set-Location -LiteralPath 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
$env:GSK_UNITY = 'C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe'
$windowsStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$windowsBuildReceipt = '.harness/delivery/windows-build-' + $windowsStamp + '.json'
if (Test-Path -LiteralPath $windowsBuildReceipt) { throw 'Build receipt already exists.' }
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs build --profile release-qa --timeout 1800 --json 1> $windowsBuildReceipt
if ($LASTEXITCODE -ne 0) { throw 'Build failed; retain its receipt and log before further work.' }
$windowsBuildGate = Get-Content -Raw -LiteralPath $windowsBuildReceipt | ConvertFrom-Json
if ($windowsBuildGate.verdict -ne 'PASS' -or !$windowsBuildGate.build.ok -or $windowsBuildGate.build.kind -ne 'release-qa') { throw 'Build receipt did not confirm the intended QA player.' }
$windowsBuildId = $windowsBuildGate.build.id
```

The gate creates a unique `Builds/qa-<id>/` and build.json; it does not overwrite034544. Keep its build log, build.json, packed_assets.json and executable hash. Do not add `--allow-dirty` automatically: freeze/checkpoint the intended build source using root's established workflow, and retain any gate refusal. The existing gate may retry an unexplained CLI exit6 once internally; retain the complete gate/build log if that occurs rather than reporting an uninterrupted first-attempt success.

Run each smoke only after inspecting the preceding receipt. Use root's existing unique receipt wrapper or redirect each JSON result to a new `.harness/delivery/` filename; the gate itself creates unique QA/log folders.

```powershell
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs smoke --build $windowsBuildId --scenario smoke_frontend --seconds 90 --seed 1 --json
```

```powershell
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs smoke --build $windowsBuildId --scenario wordbound_workshop_flow --seconds 90 --seed 1 --json
```

```powershell
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs smoke --build $windowsBuildId --scenario wordbound_original_equipment --seconds 90 --seed 1 --json
```

Do not lengthen the120s effective smoke watchdog after a failure or rerun into old evidence. Step annotations are individual limits, not a promise that their sum fits the outer watchdog; inspect actual timing if the expanded motion run times out. Any failed/partial output stays preserved. These three scenarios need no live service connection; they create local workshop proof storage under unique Temp/wbqa folders where relevant. No auth tokens/environment opt-ins from the public-capacity fixture are needed.

## Exact evidence to accept

| Scenario | Current expected steps | What to inspect beyond the gate label |
| --- | --- | --- |
| smoke_frontend | boot; front_end_idle; front_end_screenshot | First scene loaded, camera/frame progress, actual current Home screenshot and no startup exception. This is the kit fallback, not an interactive Shop/Loadout navigation test. Its fps_avg threshold is1, not a60FPS acceptance test. |
| wordbound_workshop_flow | open_wordbound; edit_workshop; exact_playtest; return_to_workshop; undo_redo_preserved | All5 PASS; exact saved/recovered draft and manifest; actual local playtest/body state; same history/draft on return; client/camera teardown; Undo/Redo. Inspect six screenshots: party, workshop, local ready/world, equipment menu, returned workshop. It verifies an offline local flow, not a network match. |
| wordbound_original_equipment | equipment_open; equipment_world; equipment_bat; equipment_bat_motion; equipment_hammer; equipment_return | All6 PASS; original BAT/HAMMER/FAN/SOFA bindings and ground/world/held evidence; real collection/craft/equip receipts; natural BAT phase images and motion JSON; exact draft preserved on return. Look for hand/item occlusion, missing/intersecting geometry and invisible attack motion. Scripted movement is not human animation/art approval. |

For each require gate PASS and checks.json PASS with **all expected steps**, `smoke.exit=0`, `timed_out=false`, normal exit and actual Player.log. The current nonstripped gate's final expression does not independently require exit0; inspect that field explicitly. Also inspect checks.json console errors/exceptions/warnings and fail/assertions, not just absence of exceptionLines. Require zero unexpected console errors/exceptions/warnings, current1280×720 varied/nonblank screenshots and preserved original assets/choice data. Do not suppress a warning/failure to match the old receipt.

Gate outputs: `Docs/qa/runs/<new-run-id>/checks.json` and captures, `Logs/player/<new-run-id>.log`, plus JSON result naming the exact build/run. Preserve the QA run's local storage path/evidence instead of deleting it as generic Temp cleanup. Hash the exact executable and relevant evidence, link the build source/tree, and visually inspect new captures before describing the new build as verified. These observations do not establish presentation FPS, a controlled performance improvement or the user's art approval.

## Public modes and new UI: honest player smoke scope

There is **no registered Windows public-match QA scenario** in the current project. The three project scenarios are workshop_flow, original_equipment and synthetic8_low; the latter is a local synthetic performance workload, not8 authenticated peers. Do not invent a `wordbound_public` gate invocation. The workshop's party screenshot proves the menu renders; it does not connect, queue or play public modes.

After automated local smoke, use the exact same QA executable normally for a bounded human-visible UI inspection: Home → Shop → Loadout; hover/selection preview restore; ownership/equip state and per-recipe finish controls; keyboard/controller focus; current portraits/fonts/framing; then Wordbound's five mode/public-map selections. Confirm TimeRun, TeamRace, BrawlRace, FreeForAll and Freestyle labels/options, approved public courses and Solo/Duo/Squad availability only on the two race modes. Record observed behavior/screenshots and any saved-choice edit; do not clear PlayerPrefs or call this a full no-code/Play preservation test. Existing smoke scripts do not perform these interactions.

A live Windows public-entry smoke is a separate coordinated step with distinct authenticated players, matching approved catalogue/region/mode/map settings, sufficient compatible parties and normal UI lifecycle. Use the existing Connect/Create party/Ready/Quick Play flow and record each actual Windows client/build identity. No automatic public-player orchestration or complete five-mode match runner exists here. Test an entry/movement/command/return observation only when participants and the existing service prerequisites are ready, then confirm return/cancel/leave on each owned party; a queue with no compatible humans is not a passed entry. Preserve sanitized observations without credentials. Root's exact capacity retries, their cleanup acceptance and historical residual-state limits remain in [online-next.md](online-next.md); this runbook does not repeat them or substitute Editor clients for Windows evidence.

## Later stripped release

After the review/acceptance scope is actually met, build the separate stripped profile and smoke **its own returned ID**:

```powershell
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs build --profile release --timeout 1800 --json
```

```powershell
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs smoke --build $strippedBuildId --stripped --seconds 90 --json
```

Set `$strippedBuildId` from that release build's verified result, never the QA ID. Stripped smoke expects perf.json/Player.log and normal startup; its bounded alive-until-timeout result can be accepted by the existing stripped gate and is different from QA's clean scenario exit. It does not run the workshop/equipment assertions or establish multiplayer/full-match behavior. Keep this later packaging step distinct from the minimal new review-build sequence; a release-qa pass is not a stripped-release pass.

Source anchors: `Tools/kit/gate/lib/player.mjs:75–170,220–312`; `gate.config.json`; `lib/config.mjs:16–43`; `Assets/_Project/Scripts/WordboundQa/{WordboundWorkshopQa,WordboundOriginalEquipmentQa,WordboundMeleeCapture}.cs`; `Packages/com.gamestudiokit.core/Runtime/Qa/SmokeFrontendScenario.cs`; `Assets/_Project/Editor/Online/WordboundBuildProfiles.cs`; `ProjectSettings/EditorBuildSettings.asset`.

Inspected SHA256: OriginalEquipmentQa `93d46f851f9da26b79487ed78a8897921eb3b9f577f178bd630ee9f2161bf61d`; WorkshopQa `86ba4ec86f5fed025d99b1a6759809733e6d515199df68f3ced06006672ea643`; Win-QA profile `aa009173edcf4565321577a940599e1fa9e459655a5d4fc5852efb362d5b72b4`. Recheck after root's pending source/native work. No build, native test, player, network request, source edit, commit or settings mutation ran for this report.

Path correction: root's only approved editor launch path is **C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe**. I previously copied a stale long-path candidate from earlier preparation and checked that a file existed, without reconciling it against the confirmed short-path recovery/launch contract. That was insufficient validation; no job was launched from this report. Fresh read-only verification confirms ProductVersion6000.6.4f1_12bfff696524 matches ProjectVersion6000.6.4f1/revision12bfff696524, the executable has a valid Unity Technologies signature, and its Windows standalone support directory exists. The exact Node executable, profile mapping, gate option parser/build/smoke dispatch and90s+30s smoke limit were rechecked; all six PowerShell blocks parse without execution. Use only this short editor path, not a long-path substitute or fallback. The same stale assignment in online-next.md was corrected.
