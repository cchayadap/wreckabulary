# Parcel Service Batch A handoff

STATUS: FROZEN — SOURCE CHECK PASS; ENGINE-FREE17/17; NATIVE/ART ACCEPTANCE PENDING.

Manifest SHA256: `281fa54af73b14c103780f93c8895b7810de02d5edd46f4f0c80bbf1be5bb83b`.
Patch SHA256: `29fef0cd9210871718c225b1f0c22a175d987d77b9fe9df41afe5e2acce1846e`.

Exactly five new production C# paths are listed in manifest.json. They were absent at freeze. Parent must verify that condition, then apply batch-a.patch or copy the exact candidate bytes. `git apply --check` passed. Unity creates their metadata during parent-controlled import; no .meta/YAML was authored here. Existing public/current sources, item data and assets were not edited.

## Contract

`Wreckabulary.Rules.Wordbound.WordboundParcelServiceContent.Create(ItemCatalogue)` exposes Geometry, CopyDocument(), Resolve(seed,requirements), CourseId=`wordbound-parcel-service-v1`, FurnitureCount=21, FurnitureLetters=90. It returns fresh independent content, not a registration in the existing public pool.

Editor menu: Tools → Wordbound → Courses → Create or select Parcel Service.
Native method: `Wreckabulary.Online.Editor.WordboundParcelServiceSetup.CreateOrSelect()`.
Saved asset: `Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset`.
Resource key: `Wordbound/LocalCourses/ParcelServiceV1`.

The helper creates only absent content, keeps existing edited course bytes/GUIDs, validates both modes and the selected saved mode, refuses dirty existing courses and wrong-type/partial targets, and refuses unimported occupied folder paths. Existing authored items/rules/camera/original visual references are borrowed. It checks original model registration. It never assigns launcher fields; root/Batch B owns explicit selection and Workshop source integration.

## Actual composition and two justified plan corrections

Three real connected rooms: Service8×8m centred(0,0,0), Sorting10×8m centred(9,0,0), Dispatch6×8m centred(17,0,0), ceilings3.6m. Two real2.5m Door connections; clear reserved route and sockets; eight start positions.18 fixed props plus3 seeded BOX parcels in a safe south storage bay give21 total. Original DESK/SHELF counters and storage, BOX/CRATE parcels, CHEST deliveries and one TABLE bench are canonical box-collider map furniture. Normal carrying and exact break-to-word behavior remain intact.

- Omitted both LAMP placements after directly viewing the original ceramic table-lamp silhouette in `Assets/_Project/Resources/UI/Items/LAMP.png`. It is not a floor lamp, and this batch does not introduce stacked/support-mounted prop physics. LAMP is not added to the local catalogue. Counts changed from23/98 to21/90.
- Replaced inbound PLATE with existing CLOCK. Actual PlaceObject application requires a crafted **Deployed** item; PLATE cannot deploy, despite passing the generic authored constructor. CLOCK is already allowed/enabled and deploys for10s, fitting the counter gap. The new factory explicitly requires deployability for all three puzzle recipes, and its authored fixture checks that contract. No shared gameplay rule/validator was changed.

TimeRun traverses outbound, inbound, outbound over three laps using TABLE, STOOL and CLOCK stations. Freestyle retains six authored per-lap practice sites through existing policy. Required letters use protected renewable supplies; optional furniture is not needed for feasibility. No new narrative ending, chest storage interaction, automatic door lock or multiplayer approval is claimed.

## Evidence and parent next steps

1. Read source-check.json: six assemblies,0 errors. This is Roslyn semantic checking, no Unity execution.
2. Read engine-free-result.json:17 passing cases,0 failures,4.80s using the official RulesHarness Runner/NUnitShim against candidate Rules/authored fixtures plus real production pure Rules/Puzzles sources. Tests include128 seeds per mode, actual variation of the box bay, source immutability, carry/drop/break90-letter conservation, blocked-route rejection, authored initial snapshots/budgets, disabled recipe rejection and optional-prop-independent recovery.
3. Parent runs native fixtures `Wreckabulary.Rules.Tests.WordboundParcelServiceContentTests` (7 cases), `Wreckabulary.Tests.WordboundParcelServiceAuthoredTests` (10), and `Wreckabulary.Tests.WordboundParcelServicePersistenceTests` (2). Persistence checks have not run yet. Source compilation passed for those exact files.
4. Parent runs CreateOrSelect, reload/repeat verifies the saved asset and retains exact asset/meta evidence. The existing saved-source profile must be clean and use OriginalWreckabulary. Repeat setup must not reset manual edits. Existing launch/default assigned-course references must remain unchanged until root explicitly binds the new nullable Batch B reference.
5. Run genuine local TimeRun completion and Freestyle practice, including optional furniture destruction/recovery and physical passage through both doors. Pure checks cannot establish actual collider/animation/socket usability.
6. Capture player-height views of all rooms, doorway approaches and the original furniture fronts. Starting pose in Service: position(-2.2,1.7,-2.6), target(1,1,2.3), FOV50. Alternate Sorting pose(-2.3,1.7,-0.4)→(2.2,0.8,2.3), FOV55. These poses are suggestions, not rendered approvals. Root owns preview-source extension and fresh receipts. Do not reuse earlier rejected images.

Independent review: root read early setup and requested occupied-target/folder guards, now applied. The other reviewer was occupied with root's preview and Batch B reviews, so no independent final five-file review is claimed yet. Root should independently skim before integration or delegate when available.

Known visual limits: existing shared wall palette remains; no rendering proof exists for this new spatial composition, object fronts, support readability or player scale. The Windows map selector and isolated Workshop source slot are Batch B, not delivered by creating this asset alone.
