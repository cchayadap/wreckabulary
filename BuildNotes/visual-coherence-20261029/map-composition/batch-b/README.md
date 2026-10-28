# Parcel Service — Batch B handoff

STATUS: FROZEN DRAFT. Source compilation passes; native execution and root review remain pending.

Manifest SHA256: `409c590abe7dec397da3ea6d5d1f450bfc7517ff110ee60e692492ca835fc8e6`.
Base HEAD: `c488ecee309c06d1298f3e312c0a772102fe9230`.

`manifest.json` is the exact integration roster: ten modified source/test files plus one new Editor source. `baseline/` preserves the original bytes; `candidate/` contains replacements; `batch-b.patch` is the review diff. `freeze-check.json` records matching live baselines and candidate hashes for all 11 targets. No production files, Unity assets, metadata or native processes were changed by this lane.

## Implemented in the draft

- Add the explicit local map ID `WordboundSoloEntry.ParcelServiceMap` (`ParcelService`) and course ID `ParcelServiceCourseId` (`wordbound-parcel-service-v1`). Lost Address remains the default; the original two map IDs and five-argument factory API remain compatible.
- Add the third Solo card and optional editable `LobbyPlayLayout.ParcelServicePreview` Sprite. Missing art uses a neutral map/layers symbol. Selection, short-frame scrolling, fixed footer and existing seat restrictions use the current controls.
- Add nullable `WordboundLaunchAssets.ParcelServiceCourse`. The six-argument `CreateSoloContent` overload consumes this saved course's document, geometry, item catalogue and rule book. It resolves a copy for the selected solo mode/seed, preserves source bytes and fails explicitly if the saved assignment is missing or incompatible. No procedural or default-content fallback exists for this map.
- Use the course camera/visual profiles when assigned, otherwise the existing launch profiles. Root's preview extension uses the same contract. Existing story/UI profiles remain unchanged.
- Add an explicit Parcel Service Workshop source with its own remembered history, selection, mode and scoped autosave. Assigned, Flagship and template sources retain their existing identities and histories. Parcel source flags are set before the first visual refresh. Missing/rebound source inputs fail explicitly; invalid unselected Parcel setup does not block the original sources.
- Preserve root's existing Workshop footer buttons: Frame selection and Frame floor.
- Add `WordboundParcelServiceLaunchSetup.BindInstalledCourse()` to make the saved launch binding concrete without rewriting the course. It validates both supported solo modes, preserves an already valid reference, and only fills an empty slot. Before writing, it creates an exact non-overwriting launch asset/meta backup under `BuildNotes/.../map-composition/launch-binding/`; afterwards it checks both GUIDs, source asset/meta bytes and launch metadata.

## Integration and native verification

1. Confirm every live target still matches its baseline hash, then copy the exact manifest candidates. Integrate Batch A and the root-owned preview extension separately. Let Unity create the new Editor script metadata normally.
2. Invoke Batch A's `Wreckabulary.Online.Editor.WordboundParcelServiceSetup.CreateOrSelect()` to create the saved asset only when absent.
3. Invoke `Wreckabulary.Online.Editor.WordboundParcelServiceLaunchSetup.BindInstalledCourse()` (menu: Tools → Wordbound → Parcel Service → Bind saved course to local launch). Inspect its backup/receipt. Repeat once and confirm a valid existing assignment produces no saved-file changes. A different valid saved assignment must be preserved; dirty inputs must reject before writing.
4. Run the focused `LobbyPlayChoiceTests` and `WordboundWorkshopSourceBindingTests`, including short-frame third-card reachability, both solo modes, exact saved edits, missing input failure, source switching/history/redo preservation, autosave isolation and online preparation lock. Run existing original local/native coverage unchanged.
5. Root extends/runs the actual Windows Solo and Workshop scenarios for Parcel Service, captures its selected Play card and actual gameplay, and checks saved edits, return-to-lobby and source switching. This draft does not modify `WordboundSoloQa` or `WordboundNativeSoloEntryTests`; those original tests compile unchanged, but they do not prove the new course's Windows gameplay.

## Evidence and limits

`source-check.json`: seven assemblies, zero compiler errors using candidate substitutions and the current Unity response files. Emission was in memory; this is not a Unity import, test execution, scene reload or player run.

The tests use an in-memory compatible saved course to isolate the binding contract. Batch A's final authored geometry and its CLOCK service puzzle need root's own native/Windows verification. Batch A's final content is 21 props and 90 optional letters; the removed floor lamps and PLATE puzzle are not reintroduced here.

No new public/online catalogue entry is added. No generated preview or new artwork is claimed. The Parcel source is exposed through the existing Workshop source UI; a new dedicated controller quick action is not added. Runtime visual framing, actual preview identity, controller usability and new course completion remain native review items.
