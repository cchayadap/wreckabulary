# Windows audit and Workshop visual review

STATUS: READY FOR EXACT BUILD/RUN IDS; no fresh Windows result or visual acceptance claimed here.

Read the current three verifier sources and their CLI contracts. Run order is: fresh scoped-source Windows build → all six smoke scenarios → final authored-file preservation → strict base audit → independent Parcel audit and inspector audit. All six runs must use the same exact build/source. The final preservation UTC must follow the latest run. Preserve each failed/retried gate as a separate file; each selected gate input must contain exactly one terminal result. Keep current isolated Temp/wbqa autosaves, original PNGs, checks and logs. Production Assets/Packages/ProjectSettings must match the built commit, including absence of nonignored untracked files.

Set the following values from root's actual receipts (do not infer timestamps or reuse prior delivery evidence). `$wbOut` must be a new existing evidence directory; each output filename below must not exist. These commands verify evidence only and launch no native process.

```powershell
$wbRoot = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
$wbBuild = 'EXACT_YYYYMMDDTHHMMSSZ'
$wbSource = 'EXACT_FULL_BUILT_COMMIT'
$wbOut = 'EXACT_NEW_OUTPUT_DIRECTORY'
Set-Location -LiteralPath $wbRoot

node BuildNotes/redesign-20261028/windows-audit/verify-windows-delivery.mjs `
  --root $wbRoot --build $wbBuild --source-commit $wbSource `
  --build-gate EXACT_BUILD_GATE_STDOUT `
  --lobby-run Docs/qa/runs/EXACT_ID-lobby_experience --lobby-gate EXACT_LOBBY_GATE_STDOUT `
  --solo-run Docs/qa/runs/EXACT_ID-wordbound_solo --solo-gate EXACT_SOLO_GATE_STDOUT `
  --workshop-run Docs/qa/runs/EXACT_ID-wordbound_workshop_flow --workshop-gate EXACT_WORKSHOP_GATE_STDOUT `
  --preservation EXACT_POST_ALL_SIX_RUNS_PRESERVATION_JSON --out "$wbOut/base-45.json"
if ($LASTEXITCODE -ne 0) { throw 'Base audit failed; keep its original evidence.' }

node BuildNotes/visual-coherence-20261029/map-composition/windows-audit/verify-parcel-windows.mjs `
  --root $wbRoot --build $wbBuild --source-commit $wbSource --base-audit "$wbOut/base-45.json" `
  --solo-run Docs/qa/runs/EXACT_ID-wordbound_parcel_service --solo-gate EXACT_PARCEL_SOLO_GATE_STDOUT `
  --workshop-run Docs/qa/runs/EXACT_ID-wordbound_parcel_workshop --workshop-gate EXACT_PARCEL_WORKSHOP_GATE_STDOUT `
  --out "$wbOut/parcel-18.json"
if ($LASTEXITCODE -ne 0) { throw 'Parcel audit failed; keep its original evidence.' }

node BuildNotes/creator-clarity-20261029/workshop/extra-capture/verify-inspector-windows.mjs `
  --root $wbRoot --build $wbBuild --source-commit $wbSource --base-audit "$wbOut/base-45.json" `
  --run Docs/qa/runs/EXACT_ID-wordbound_parcel_workshop_inspector --gate EXACT_INSPECTOR_GATE_STDOUT `
  --out "$wbOut/inspector-11.json"
if ($LASTEXITCODE -ne 0) { throw 'Inspector audit failed; keep its original evidence.' }
```

The original strict contracts remain 45 images (Lobby5steps/24, Solo4/13, Workshop5/8), then Parcel18 (Solo3/9, Workshop4/9). The added flavor is5steps/11, so all three audits bind74 files across six runs. Do not replace old Workshop/Parcel runs with the extended flavor. Base audit performs full packed validation; the two supplemental audits delegate those checks and rebind its recorded evidence. Inspector audit additionally checks PNG CRC/zlib scanlines, exact context/rectangle and restoration notes. Gate PASS, exit0, clean console, source binding and preservation do not constitute visual approval.

## Assigned visual review: 28 actual files

Open each original full PNG at original dimensions, record exact run/build, filename and observed dimensions. No crops, overlays, removal of labels, substituted previews or deduplicating equal-looking images between runs. Compare relevant earlier215158 images only as dated baseline; the actual new images govern findings.

- Original Workshop8: wordbound_party; wordbound_workshop; wordbound_workshop_range_height; wordbound_workshop_range_floors; wordbound_local_ready; wordbound_local_world; wordbound_equipment_menu; wordbound_returned_workshop.
- Parcel Workshop9: parcel_workshop_assigned; parcel_workshop_saved; parcel_workshop_sorting_close; parcel_workshop_playtest_world; parcel_workshop_playtest_menu; parcel_workshop_returned; parcel_workshop_assigned_returned; parcel_workshop_parcel_returned; parcel_workshop_lobby.
- Inspector11: the same nine Parcel names in the separate inspector run, plus parcel_workshop_new_puzzle and parcel_workshop_room_routes between sorting_close and playtest_world.

## Acceptance checklist

- [ ] All28 files opened directly; exact roster/count and dimensions recorded. Unopened images are explicitly listed, not marked reviewed.
- [ ] Objects/Puzzles/Room tabs are fixed, readable and visibly selected. Inspector scroll remains inside its region; no overlap with header, viewport or footer.
- [ ] New puzzle shows recognizable original recipe images and short labels. No noncraftable furniture offered, missing/blank art, tiny thumbnails or clipped choice/creation action. A screenshot alone cannot prove every recipe or Add action.
- [ ] Room/routes shows usable Safe/Stops/Routes/Laps/Spawns navigation and readable room/recovery choices. Advanced IDs are optional; default choices are friendly and unambiguous. Report any crowded coordinate/toolbar row explicitly.
- [ ] Original object selection and both range captures show their intended height/floor controls, selected values and scroll position; no stale offscreen section or lost fields.
- [ ] Footer and primary actions fit. Preview/Playtest/return labels and enabled/disabled cues are legible. Focus/navigation assertions are reported separately from visual reachability.
- [ ] Sorting close view retains real wood/material detail and original props, thin selection border, no floor-wide mint fill, floating wall fixtures or rendering artifacts. Route remains legible through handles/overlays.
- [ ] Gameplay capture leaves a useful route view around the avatar; menus do not obscure critical choices. No generic BOX/TABLE placeholder presented as actual map imagery, decorative stripes or excess prose.
- [ ] Returning restores the right source/draft and inspector context visibly. Exact history/autosave/source/device preservation comes from audit probes, not appearance alone.
- [ ] Report concrete blocking clips/selection defects first, then remaining aesthetic issues and precise limits. No owner acceptance, complete creator, full route, public multiplayer, performance or production-ready claim from these captures.
