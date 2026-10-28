# Parcel Service physical completion tests

STATUS: FROZEN SOURCE CHECKED; NOT NATIVE RUN.

One new test source is ready at `candidate/Assets/_Project/Tests/OfflineGameplay/Authoring/WordboundNativeParcelServiceTests.cs`. Apply `physical-play.patch` or copy this exact file to the same production path. Parent owns Unity import/meta generation and native execution. The installed saved `Resources/Wordbound/LocalCourses/ParcelServiceV1.asset` is required; the fixture refuses dirty course edits and uses a temporary in-memory clone for mode selection.

- Manifest SHA256: `73266d437e28c4231834460bd03089b348b12433418d71cc2de411e8b17e7560`
- Patch SHA256: `45738d76446db76a59ff035582d772af76ce441b4c65d321d6c083ca53007400`
- Exact native filter: `Wreckabulary.OfflineGameplayTests.WordboundNativeParcelServiceTests`
- Environment: `WORDBOUND_LOCAL_PARCEL=1`
- Assembly: `Wreckabulary.Offline.AuthoringGameplay.Tests`
- Two Unity tests; each bounded by 900 seconds, with 780-second gameplay budget and 30-second shutdown limit.
- Prerequisite: integrate the menu lane's `WordboundPracticeGuidePlan.Nearest` unfinished-site correction before running.

The TimeRun case uses the production local client/Fusion world and admitted motor input to collect all 30 real recovery letters, craft and deploy six unmodified recipes (TABLE ×3, STOOL ×2, CLOCK ×1), hold all real physical sockets, consume each crafted object, cross nine ordered checkpoints and deliver the keepsake through all three finishes. It requires a naturally confirmed Win, 12 ordered objectives, continuous non-regressing progression and original model meshes.

The Freestyle case walks to all three locations, repeatedly uses the actual HUD nearest-site selector, selects each of the six unfinished authored sites through the normal authority command and solves it with real recovery/craft/deploy/use operations. No race progress or terminal result may appear. A real map-origin TABLE is picked up, carried and dropped at the SortingBench socket; it must remain physically incomplete through a full hold and return `WrongPuzzleItem`, then be moved back out of the socket before the crafted proof.

The fixture never modifies Rigidbody position/velocity, authority snapshots, solved flags, result flags, recipes, craft durations, public catalogue or course content. It verifies native world floor support at movement destinations, real wall/floor colliders, 21 initial map bodies, real deployed BoxColliders, original meshes, exact saved course+meta bytes and source object preservation. Owned local client shutdown, canonical world removal, input delegate restoration and temporary clone cleanup are checked.

Source evidence: `source-check.json` (0 errors against the current Unity response-file references) and successful `git apply --check`. This is compile evidence only. Root must run Unity, inspect failures and retain official XML/log receipts. Full human controls/menu clicking, Windows delivery, visual acceptance, different seeds, public matchmaking and performance profiling are separate acceptance tasks. No course-layout adjustment should be made merely to accommodate this fixture without checking the actual playable route.
