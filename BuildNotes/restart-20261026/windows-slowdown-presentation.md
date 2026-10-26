# Windows gameplay slowdown — presentation audit

STATUS: SOURCE REVIEW COMPLETE — no concrete presentation regression identified. Read-only source audit; no native job, source edit or timeout change.

Scope: compare known tested source `9e013dc9e766a616ae1d45ed037cbbd863dbf89e` with candidate `3cec54070e619bc0dced035a16b1b06fc281f818`, focusing on recent avatar, animation, story, HUD and lobby presentation work. Root owns profiling and runtime acceptance.

## Confirmed observation

`Logs/player/20261026T123303Z-wordbound_workshop_flow.log` records a failed exact-playtest deadline. Between frame 14/tick 59 and frame 70/tick 3419, the frame counter advances 56 while the simulation advances 3,360 ticks: an aggregate ratio of 60 ticks per frame in each sampled interval. This does not prove that every individual update ran exactly 60 ticks. The 15-frame intervals take approximately 22.9, 14.8 and 29.6 seconds. No recurring exception in this log explains those pauses. This establishes the stall and tick catch-up pattern, not its cause.

Root subsequently reports the **same build**, without source changes, passed workshop run `20261026T123805Z` (5/5, 27.8 seconds, exit 0, console 0) during profiling. Focus was true throughout observed successful frames and false during the earlier failure. This is a useful comparison, not a proven focus diagnosis. A deterministic recent-source regression is not established by the failed run alone.

## Source findings

- Avatar correction assigns a prebuilt saved mesh at spawn/initialization. Runtime `AvatarSurfaceProfile` checks references, counts and blendshape names/frame counts; it does not hash mesh buffers or recalculate normals/tangents. The expensive source fingerprint and surface rebuild helpers are under `Assets/_Project/Editor/Art`. `ModelLibrary.Spawn` and `PlayerAppearance.Initialize` are assignment boundaries, not per-frame callbacks.
- `WordboundGameplayPresentation.Refresh` returns early for an unchanged world/parent/model key/size. Resolver creation and collider/rigidbody scans occur when a view changes. `ApplyAppearance` skips unchanged appearance strings. Held models are created only when missing or their skin changes. New melee state uses reused collections, with the inventory lookup filled only if an actor is attacking or a view still has an active melee animation.
- `WordboundAvatarMotion` evaluates its manual PlayableGraph once per player in presentation `LateUpdate`. The new melee layer sets existing clip times and weights; its masks and hierarchy-path strings are constructed once. There is no per-frame mesh clone, normal rebuild or material-generation loop in this path. Native animation/skinning cost still requires profiling.
- Story notices are built during binding. `WordboundStoryView.Apply` builds a small state-signature string per published snapshot and skips journal/cue reconstruction when that state is unchanged. A TextMesh is created only when the contextual clue changes; its per-frame work is positioning/orientation/scale. The old OnGUI clue panel was removed. Freestyle rejects epilogue evaluation before result-building work.
- Typography clones are cached. Editor reload discovery is excluded from a Windows player. Full TMP object discovery used for cache disposal is tied to session/editor lifecycle, not an Update or canvas-render callback.

## Snapshot and HUD cadence

`WordboundGameplayController.AdvanceHost` captures/reconciles after each admitted host physics step and synchronously invokes `FrameChanged` once the reconciliation publishes (`Assets/_Project/Scripts/Online/Gameplay/WordboundGameplayController.cs:257`, `:281`, `:401`). Catch-up batches therefore run the presentation, story and HUD subscribers multiple times before a rendered update. This publication behavior already exists in `9e013dc9`; the only controller change in the comparison is the cheap `IsPresentationReady` Boolean property.

`WordboundMatchHud.ApplyFrame` still rebuilds a reused body dictionary and searches player/actor/receipt collections on each publication (`Assets/_Project/Scripts/Online/Client/WordboundMatchHud.cs:70`). Those loops and LINQ receipt enumeration are unchanged. `RefreshRecipes` exits when the letter bag is unchanged (`WordboundMatchHudMenu.cs:50`); recipe-card creation is not unconditional per tick. HUD panels rebuild during IMGUI Layout as before. The new journal/epilogue menu cache is populated only while the menu is drawn at Layout, and Freestyle epilogues return immediately. This work is not an identified seconds-per-frame regression.

The story signature did allocate a string before this change; its new fields make the signature longer. It still performs LINQ player/team lookup and joins narrative flags per published snapshot (`WordboundStoryView.cs:42`). This is a bounded recurring allocation candidate for later profiling, not evidence of the observed stall. `MatchBinding.Hash` is a constructor-computed get-only value (`Rules/Wordbound/MatchState.cs:17`); `ResolvedCourse.Hash` accesses stored manifest data (`Rules/Wordbound/CourseManifest.cs:201`). These comparisons do not recompute a SHA hash on every frame.

The new BAT capture buffers, GPU readback and PNG SHA calculations belong to the explicitly invoked equipment QA scenario (`WordboundOriginalEquipmentQa.cs:165`, `WordboundOriginalEquipmentView.cs:52`, `WordboundMeleeCapture.cs:131`). They are not installed as a workshop per-frame capture. The source mesh identity check in equipment QA is also distinct from regular presentation.

## Actual profiled evidence and limits

Independently read `Logs/player/20261026T123805Z-wordbound_workshop_flow.log:69`, `:101` and `:108`:

| Measurement | Successful run result | Meaning |
| --- | --- | --- |
| Observation updates | 90 updates / 9.538 seconds | Functional deadline passed; approximately 106 ms mean update gap is not smooth 60 FPS. |
| Synchronous FrameChanged subscribers | 632 calls / 80.576 ms total | About 0.127 ms per publication in this run, including presentation/story/HUD subscribers. |
| Inclusive HostStep | 632 calls / 6,247.953 ms total | About 9.89 ms per host step; catch-up work is material even in the passing run. |
| Assigned camera callback wall time | 89 matched pairs / 275.291 ms | A bounded CPU-side callback interval, explicitly not GPU or presentation time. |

The phase scopes overlap and must not be summed. FrameChanged timing does not measure separate LateUpdate animation evaluation, rendering, GPU skinning, driver scheduling or total GC cost. The failing run lacks the same diagnostic detail, so the successful run's timing cannot be transplanted into its pauses. A saved corrected head with unchanged vertex/rig/shape counts can still have native skinning cost; source inspection alone does not measure that cost. The menu's fast startup rate is a different workload.

The independent [simulation audit](windows-slowdown-simulation.md) records identical workshop manifest/seed/autosave payload across historical, failed and passing runs. Its source comparison agrees that canonical scheduling and reconciliation did not change in this revision range.

## Conclusion and next action

No confirmed repeated mesh clone/recalculation, SHA fingerprint, new unconditional visual respawn, or material/font regeneration loop explains the stall. The new presentation subscribers are a small measured part of the successful rerun. Do not revert corrected art, remove validation, lower the canonical tick rate or extend the smoke timeout on this evidence.

Preserve both the original failure and the same-build pass. Root's unchanged-build plain workshop confirmation should establish whether the functional result repeats without diagnostic flags. If the long frames recur, capture the existing phase/frame diagnostics in that failing condition and separate host catch-up, render-time animation, GPU/present and focus state before selecting a fix. This report does not certify whole-game performance or final Windows acceptance.

Audit method: bounded `git diff 9e013dc9..3cec5407`, source/call-site reads, direct log inspection; no native process or live source mutation. Checkout observed at `sethypagna/wordbound-integration`, HEAD `3cec54070e619bc0dced035a16b1b06fc281f818`. This lane wrote only this report for the slowdown audit.
