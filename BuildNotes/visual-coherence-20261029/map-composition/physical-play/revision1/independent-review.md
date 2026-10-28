# Parcel Service native lifecycle repair review

STATUS: COMPLETE — NO BLOCKING SOURCE FINDING; NATIVE RERUN PENDING.

Scope: compare the bounded test lifecycle/cleanup repair against the current live test and first failed native run. Preserve actual movement, crafts, physical sockets, carrier checkpoints, completion and source preservation assertions. No production or native writes by this lane.

Reviewed manifest SHA256 `0f63a04de9ce2dcfb19134afeb63c2d719eb6dad7815d761e50e098adea98e9b`, candidate SHA256 `0505af783ebbeeb16f9a30268b511a138b794000b7a40a487413832661c70024`. Live and saved baseline both match `06d0fb15e613ac3c94b34596ba2055568e1ec9b37a48898939c9b9521cbba033`. Source-check receipt reports one assembly, three sources, zero errors. No compiler or native run was launched by this reviewer.

Read `TestResults/editmode_20261028T194837Z.xml`: both tests failed during real client startup because DontDestroyOnLoad was called outside Play mode; teardown then reported zero movement reads as source preservation failure and retained delayed-destroy objects. This is startup failure evidence, not a gameplay failure/pass.

The revision moves EnterPlayMode into each root UnityTest enumerator, checks Application.isPlaying immediately after it, and repeats the guard in Start before client creation. This matches the existing direct transition structure in WordboundNativeWorkshopEntryTests. The installed test framework's enumerator helper explicitly rejects PC resume while nested, supporting removal of the nested boundary; only the next native run can confirm the full transition on this editor.

All 82 original Assert/CollectionAssert expressions remain present. Solve, RejectMapTable and the full RouteTo/movement/command/health/original-mesh helper section are byte-equivalent as text. No puzzle, craft, authority, letter count, carrier/checkpoint, actual floor support, physical hold or terminal result assertion was weakened.

Source asset/meta/memory checks remain in RequireSourcePreserved and still run during teardown. The movement-read/non-regressed-progress success check moved to RequireGameplayEvidence, called by both completed test bodies, so an early startup failure no longer produces a misleading source-corruption error. Cleanup destroys only fixture-owned root/clone fields, rejects persistent assets, uses delayed destruction in Play mode and immediate destruction in Edit mode, retains shutdown/world-release checks and bounded stop-task fault observation.

No blocking source issue found in the bounded repair. Native rerun remains required for root transition, actual movement/physics assertions and cleanup. The first 0/2 failed run is retained and is not superseded by source compilation.
