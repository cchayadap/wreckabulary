# Parcel Service Windows QA draft

STATUS: FROZEN. Three-assembly semantic compile passes; no Unity/native/player run was performed by this lane.

Manifest SHA256: `1c3c31006970f226dc87ce70559c6e204289a0d591ef963a6104dd2c4c778094`.

Exactly two files change: `Assets/_Project/Scripts/WordboundQa/WordboundSoloQa.cs` and `Wreckabulary.Wordbound.Qa.asmdef`. The assembly adds a direct reference to game-owned `Wreckabulary.Online.Authoring` for the explicit saved course. No Tools/kit or metadata changes.

## Separate scenario

`wordbound_parcel_service` runs three steps: `parcel_service_selection`, `parcel_service_time_run`, and `parcel_service_freestyle`. It launches each mode through the actual Play/Local/Wordbound/map/GO buttons with UI Submit events. No gameplay state or completion is fabricated.

It reuses the existing guarded work/stop budgets, exact invoking input binding and borrowed-wrapper checks, actual Single runner check, assigned-camera render events, canonical tick progress, guide plan agreement, real return handler, destroyed world/client/camera checks and selection restoration. Both modes additionally require six physical sites, exact saved course ID/source/geometry/item/rule binding, matching actual gameplay rules and the assigned camera/visual profiles with the production fallback. Each mode must use one fresh client and camera. Setup snapshots the installed course and its profiles plus exact item/rule text; the existing preservation check verifies them after each mode.

The original `wordbound_solo` registration remains. Its four-step block, all 28 existing Require expressions, and the Guard, RestoreSelection and capture helpers are preserved. Its 72-site check still runs only for the original maps. `freeze-check.json` records these source checks and baseline parity. This is a source comparison, not proof that an old scenario has been rerun.

## Required capture roster

The new run is separate from the original 45-image delivery audit. Expect exactly these nine full-window PNGs:

1. `parcel_service_initial_lobby.png`
2. `solo_parcelservice_timerun_selected.png`
3. `solo_parcelservice_timerun_world.png`
4. `solo_parcelservice_timerun_menu.png`
5. `solo_parcelservice_timerun_returned.png`
6. `solo_parcelservice_freestyle_selected.png`
7. `solo_parcelservice_freestyle_world.png`
8. `solo_parcelservice_freestyle_menu.png`
9. `solo_parcelservice_freestyle_returned.png`

Root must check exact build/scenario identity, three passing steps, all nine referenced PNGs, zero faults and preservation notes before treating it as verified. Open the actual images at full size; nonuniform screenshot pixels alone do not establish aesthetics or correct framing.

## Explicit limits

This tests real runtime composition and UI event dispatch; it does not inject physical keyboard/gamepad events, solve the complete route, accept artwork quality, prove network/team play or test Workshop source switching. Map-lane native physical-play tests are separate evidence.

The current `wordbound_workshop_flow` hardcodes flagship course identity, template autosave paths, random-zone inspector state and edit/playtest expectations. Reusing it safely for Parcel Service would require a broader change. It remains untouched. Actual Windows Parcel Service Workshop source selection, original-source history preservation, edit/save/playtest and return are a coverage gap to address in a dedicated scenario; Batch B's focused native/source-binding tests do not replace that Windows run.
