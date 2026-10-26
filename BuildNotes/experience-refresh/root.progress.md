# Experience refresh — integration

STATUS: IN PROGRESS. Owner continuation on 2026-10-26: minimal, immersive player UI; richer original-style designs, skins and animation; clearer shops/loadouts; stronger gameplay/story rubric and introduction through play.

## Preserved baseline

Integration starts at `9fce9f25`. Windows review build `20261024T034544Z` remains untouched. Workshop 5/5 and original-equipment 5/5 pass; all five modes have bounded two-client public entry receipts. Maximum-roster run `041032` failed all three ID assertions, with clean service teardown. The actual receipt was recovered before changes. These results do not establish production acceptance.

All seven worktrees were inspected. Recovery snapshot `.harness/recovery/2026-10-26T01-32-06-562Z-a6c221ec-9808-4369-927c-7451a245c2b8` verifies 632 dirty/untracked/authored files. Original font/Lunar work and unrelated online files remain unchanged. No heavy job active at recovery.

## First implementation slices

- [~] Root: shop browsing by visual category, concise selected-item detail and preview, then loadout consistency. Preserve existing coins, ownership, recipe-specific finishes, keyboard/controller focus and return behavior.
- [~] Art lane: inspect actual original assets and captures; propose a coherent first collection and motion/skin implementation contract before changing assets.
- [~] Story lane: evidence-based multidimensional gameplay rubric, then validated Unity narrative data and concise contextual introduction. Preserve clue identities, team gating and existing outcomes.
- [~] Online lane: diagnose maximum-roster identity assertion against installed SDK and production binding; preserve required identity and roster checks.
- [ ] Root: integrate reviewed slices, run appropriate gates serially, capture actual UI and motion, inspect combined behavior, update Windows review build.

The shop currently puts themes, collections, accessories, recipe finishes and colours in one long scroll. Existing live previews, earned-coin checkout, ownership and per-recipe equip APIs will be reused. Initial scope changes presentation and navigation; new real-money commerce is outside this slice. Screens should reveal one choice at a time while retaining accessible labels and optional explanation.

Agent reviews are analysis, not human-playtest scores. Visual approval, sustained performance and full online matches remain separate acceptance requirements. Root alone runs Unity/build/player/core/Blender jobs; no kit changes or new paid generation are part of this initial slice.

## 2026-10-26 native integration checkpoint

Source through `229d48be`: shop categories/portrait importer, editable story profile/cues and all three launch paths, confirmed upper-body melee sampling, rubric and bounded Packing Club source generator integrated. Native-generated story asset and metadata are present. The original item catalogue/public approval and earlier Windows review build remain unchanged.

R0 `021502` PASS 33.6 s; selected core `021558` PASS 58/58, 37.3 s. Story setup `022152` PASS 41.7 s. Focused EditMode `022355` PASS 92/92, 75.8 s: 61 presentation, 10 story plan, 13 profile, 7 readability and 1 native persistence. The requested MenuGui fixture did not run because root supplied an incorrect Input assembly name; a correctly scoped invocation remains required. PlayMode `022713` PASS 58/58, 192.5 s. Receipt: `native-slice-20261026.json`, hashes of actual logs/results. No skipped cases.

Portrait import `021707` and `022533` ran successfully; direct image review rejected the first face lighting and found the second still too dark. Root is correcting preview lighting only, without changing original avatar materials. Motion still needs a built-player frame sequence, story actual cue lifecycle still needs composition/capture, and shop needs current UI screenshots.

Capacity rerun is complete, not active: FFA8 passed; Freestyle8 gameplay reached but cleanup failed; TimeRun failed first guest POST. Read-only operator receipt confirms the failed match is CLOSED and leases expired, but eight guest parties/memberships remain. No service mutation was performed. Fresh isolated tests remain queued after local presentation verification.

Collection source is not generated art. Independent review requests a complete eight-export matrix and external palette/material checks before root runs Blender. Separate skin contract review identifies immutable public catalogue hashes, missing authoritative online outfit initialization and thrown appearance. Do not claim current shop choices synchronize online until that full seam is implemented/tested.

Next recoverable action: finish portrait review, rerun idempotent story setup and correctly scoped MenuGui cases, capture actual shop/motion; build/verify/render new geometry after generator repair; preserve exact source receipts and update the Windows review build after green checks.

## 2026-10-26 asset generation and preview diagnosis

Packing Club build-a is now generated, not merely source: editable Blender file, preserved original references, palette, four item/LOD meshes and eight FBX/GLB exports are in `ArtSource/Collections/PackingClub/V1/build-20261026-a`. The exact eight-export verification passes. BAT LOD0/LOD1 have 1,628/924 triangles; SHIELD has 1,044/548. Both have two materials, preserved canonical envelopes/grips, and verified palette bindings. All sixteen matched original/candidate view renders completed. No Unity import, per-player skin selection or final visual acceptance is implied.

Root directly inspected BAT and SHIELD three-quarter comparisons. Build-a is rejected for current visual quality: BAT's broad flat crown and plain finish lose the original's polish; SHIELD's oval silhouette loses the original's angular identity. Preserve build-a and its evidence. A separate build-b will soften BAT construction and retain the original angular shield with layered trim. Original runtime assets remain the default.

Shop portrait renders ran but remain visually rejected. Increasing lights brightened clothes without correcting the flat grey face. The next diagnostic compares sampled animation, a manually evaluated playable, and a static baked pose under identical lights/framing. It does not alter original avatar materials, the palette or the other character-colour lane.

Story setup repeat gate `023950` was cancelled after 434.8 seconds without progressing beyond IL postprocessor connectivity retries. The first setup passed and both story asset hashes remain unchanged. Repeat-idempotence is NOT-RUN; the cancellation is an infrastructure observation, not a failed idempotence assertion. The exact owned Unity process was stopped; unrelated processes and caches were preserved. Private evidence: `.harness/delivery/story-setup-cancel-20261026.json` and its hashed copied log.

The additive six-outcome epilogue contract is approved for implementation in the isolated story lane. It will remain optional, use confirmed own-team state, distinguish actual delivery from progress-at-timeout, and never infer the unimplemented optional Restoration ending. Online cosmetics admission is being reviewed separately from geometry imports. No updated Windows build has been produced yet; the previous tested review build is intact.

## 2026-10-26 integrated experience and build-b

Source `a1000f5c` integrates the six epilogue resolver/profile/Inspector, the optional journal wiring, a separate transport-readiness predicate, concise loadout navigation and the accepted-attack capture scenario. The journal resolves current confirmed state at Layout and stays optional; result pause does not disable its read-only scrolling. The loadout now routes a selected recipe directly to its shop finish and restores saved appearance after colour previews. Independent source reviews found no remaining blocker in these slices. Native verification remains pending.

The new Windows QA motion step retains the previous five checks and adds a real BAT attack, exact command receipt, natural windup/active/recovery/complete frames, grip matrices and bounded GPU copies. PNG encoding occurs after the swing. Baseline/completion may legitimately match; the check requires observed motion plus distinct moving images rather than universal hash uniqueness. This source has not yet run in a player.

Combined R0 `034211` PASS in 209.5 seconds. Portrait diagnostic retry `034659` is running with a 600-second bound. Earlier `031649` FAIL remains recorded (300-second compile timeout; no probe images). Host observation at `03:51:44Z` reports 100% CPU and 4,337 MiB free of 32,123 MiB; this is a resource snapshot, not proof of the timeout's cause. App panel opening did not return, and browser automation timed out. No successful browser review is claimed.

Build-b is generated and verified: BAT 2,308/1,172 triangles, SHIELD 968/472; all eight exports and sixteen matched render receipts pass. Root inspected BAT original/refined three-quarter and refined front, plus SHIELD refined front/three-quarter/side/rear. The revision is suitable for an optional Unity comparison: rounded BAT crown, readable wrapped grip/laminate details, restored angular shield and layered border. Subtle panel shading and tiny details still need in-game inspection; original defaults remain unchanged. See `packing-club-build-b-20261026.json` and the local `PACKING-CLUB-REVIEW.html`. Native import, per-player online appearance, performance and owner acceptance remain open.

## 2026-10-26T04:41:55Z Cleanup integration and validation queue

Integrated only cleanup commit 96a334792759bb82869bc25cca23f94e0f36b7e3 as 0c083004. The production return contract is confirmed at OnlineServices/src/pool.ts:401. A second independent source review found no concrete blocker in phase ordering, failure retention, credential snapshots or abort disposal. The native diff contains only the service-cleanup hunk; isolated full-duration changes and unrelated worktree dirt remain untouched.

R0 042624 PASS in 196 seconds, including Runtime and Editor. The generated Editor project includes test source with UNITY_INCLUDE_TESTS; this is compilation, not execution of the 15 new cleanup cases. Exact hashes and limits are in continuation-source-20261026.json. PackingClubSetup now selects verified build-b by default for the optional Unity comparison importer; actual runtime art remains original.

Validation queue, sole Unity owner root: first execute WordboundLiveCleanupTests (15 fake cases); then the expanded story/presentation/profile/persistence/MenuGui scope (129 source-derived cases) in Wreckabulary.Online.Presentation.Tests, Wreckabulary.Online.Authoring.Tests and Wreckabulary.Online.Client.Input.Tests. Repeat ShopCollectionsTests|WinterCollectionTests|LobbyTests (59 expected). Counts must be confirmed from resulting XML. No direct native test yet covers IsPresentationReady or result-time epilogue recovery/scrolling. The existing UIRefinementCaptureTests screenshot case does not cover the new loadout footer or Wordbound journal; use a fresh WRECKABULARY_UI_CAPTURE_DIRECTORY and inspect actual new flows separately. Public capacity and authored-workshop cleanup cases remain individual fresh runs after the fake fixture passes.

## 2026-10-26T04:45:38Z Current recovery checkpoint

Portrait isolation gate 043145 FAIL, 605.7 seconds: unchanged 600-second deadline. Burst was disabled only through this process's supported arguments, with timestamps enabled. The same ILPP connectivity wait occurred. Three exact owned lower-priority helpers were restored to Normal at 04:38:18Z and the verified ILPP runner at 04:39:47Z; script compilation began at 04:39:52Z, but did not finish before timeout. This coincidence is not causal proof, and disabling Burst does not establish normal build correctness. No diagnostic images were created.

All owned Unity/compiler processes are verified gone; a reused PID now belonged to an unrelated node process and was explicitly excluded, never modified. No installed settings, caches or original game assets were changed. Receipt portrait-compiler-isolation-20261026.json includes 13 verified artifact hashes and both diagnostic phases. No heavy job remains active. Pending owner machine-availability reply is still unanswered; avoid another identical native retry under unchanged contention.

Next recoverable action: after reduced contention or a concrete compiler correction, restore the normal Burst-enabled launch and execute the 15-case cleanup fixture, then the focused experience checks above. Diagnose portraits from actual A/B/C images before choosing a rendering fix; import/validate optional Packing Club build-b; capture actual loadout/journal and attack motion; produce a new Windows review build only after those pass. The earlier tested 034544 executable remains intact. Generated portrait PNGs are preserved unaccepted drafts; their 15-file private snapshot is verified. Final production acceptance remains open.
