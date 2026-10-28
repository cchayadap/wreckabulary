# Solo map integration review

STATUS: SOURCE REVIEW COMPLETE — NATIVE/PLAYER VALIDATION PENDING

Read-only review of root's launcher, Windows solo QA and native solo-entry test changes. Scope: explicit map dispatch, original default/coverage, one-shot intent, bindings, scoped restoration and unique capture keys. No live source or native process changes by this reviewer.

One validation gap: `Assets/_Project/Tests/OfflineGameplay/WordboundNativeSoloEntryTests.cs:192` checks the returned SelectedMode but does not check SelectedMap. Add `Assert.AreEqual(soloMap, WordboundSoloEntry.SelectedMap)` beside that assertion. The newly named ParcelHallStartsItsOwnFurnishedCourseAndReturnsToItsSelection test otherwise could pass if return silently reverted the map. Windows QA already checks both fields, so this is missing native coverage rather than a confirmed runtime defect.

No functional source defect found in the reviewed integration. The optional map defaults to LostAddress and takes exactly the previous WordboundClientContent path; ParcelHall uses the separate copied document, resolves with the selected Solo mode, and constructs validated authored content. Unsupported maps/modes reject. StartSolo reads SelectedMap before its first await; dispatch/return ownership and the exact borrowed input binding are unchanged.

The original TimeRun and Freestyle native tests and Windows steps remain. The new Windows Parcel Hall step is additional Freestyle coverage. Its seed, manifest, source, catalogue and mode binding comparisons use the explicit chosen course; it also checks all six Arrival furniture zones and the72 physical sites. This is launch/render/return coverage, not a full puzzle finish or multiplayer claim.

Capture names remain unique: one initial lobby image and four images for each of timerun, freestyle and parcelhall_freestyle, for13 distinct paths. Original timerun/freestyle screenshot keys are unchanged. Generic cleanup/preservation note names are safe across runs because QaContext.Note records into CurrentStep.Probes; each run has a distinct step. Mode-tagged note keys now use the lowercase map-aware tag consistently.

Windows Setup captures the original solo map, mode, family, local house/map, page and exact binding objects. RestoreSelection restores SelectedMap alongside those fields and runs on both normal guarded cleanup and abandoned-step cleanup. Asset-memory and item/rule-byte preservation checks remain. Native teardown still clears its test session, restores preference values, releases its owned launch configuration and checks source hashes.

Recommended next check: add the returned-map assertion, then run the original two solo routes plus Parcel Hall launch/return and inspect the three actual world captures. This review ran no Unity/player processes and makes no visual acceptance claim.
