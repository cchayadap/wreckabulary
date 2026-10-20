# Public catalogue preparation

STATUS: SOURCE REVIEW AND SOURCE GATES PASS — NATIVE ACTIVATION NOT RUN

Root owns the current compiler/native slot. This lane owns new public catalogue serialization/tests, the narrow public registry consumer, approved seed selection in PartyMenu, and a new Editor exporter/binder. Existing content construction, authoritative gameplay, service/deployment, shared assets/scenes and secrets remain outside this change.

Implemented artifact contract: schema 1, exact protocol and map-pool release version, plus ordered courses containing mode, format, seed and exact BuiltIn content binding. `WordboundPublicCourseCatalog.BuiltinContentJson` produces the flat Worker binding projection from that same artifact. Legacy empty `[]` means a closed public registry; nonempty unversioned arrays remain accepted by the compatibility registry but are rejected as menu catalogues. The catalogue and menu validate the release before creating a network client.

The menu offers approved course/seed selection before party creation, separately from explicit custom private seeds. Selection never changes an existing party or invitation's map. An existing custom party receives the leave/recreate remedy only when approved choices actually exist for that mode/format; an empty catalogue instead directs the player to a private room. The Editor helper resolves every selected public mode/format from the actual item/rule source, writes one deterministic catalogue, imports it and assigns LaunchAssets using Unity APIs. Root executes it only after native approval; no generated shared asset is created in this lane.

Each approved seed resolves eight public variants: TimeRun Solo; TeamRace Solo/Duo/Squad; BrawlRace Solo/Duo/Squad; FreeForAll Solo. Freestyle remains private. The catalogue rejects private bindings, metadata/mode-hash mismatches, duplicate selections or bindings, unknown fields/protocol, and release mismatch. Selection recomputes actual installed content before party creation, rejecting a plausible seed paired with another resolved manifest.

The limit remains at most 128 bindings and the existing `OnlineJson` 32 KiB encoded JSON bound. The latter can reject a seed list before the count limit is reached. The exporter uses the existing content constructor and its current validation; this change does not edit or weaken content construction/preflight.

## Native integration contract

The root-only Editor entry point is `Wreckabulary.EditorTools.WordboundPublicCourseSetup.ExportAndAssignFromEnvironment`. It requires explicit `WORDBOUND_PUBLIC_RELEASE` and `WORDBOUND_PUBLIC_SEEDS` (comma-separated, exact 16-character lowercase hexadecimal seeds). It loads actual items/rules from `Assets/_Project/Data/Config`, resolves all eight variants per seed, writes `Assets/_Project/Resources/Wordbound/public-catalogue.json`, synchronously imports/readbacks the exact bytes, and assigns `LaunchAssets.ApprovedPublicCourses` plus `MapPoolVersion`. Changed content or seeds cannot overwrite the same release version.

The existing launcher already passes the assigned catalogue text and release into the menu, so no additional launcher hook is required. Root must preserve its existing `WordboundEntityBudget` content-preflight line when cherry-picking the narrow registry parser hunk. The lane's older content constructor has not been rewritten.

After native content approval and export, the deployment owner can parse the resulting artifact and obtain `BuiltinContentJson` for `BUILTIN_CONTENT_JSON`. The artifact's release must match client launch settings. No Worker config, secrets, service code, generated approval asset or remote state has been changed by this task.

## Evidence and next action

Sethy completed a bounded independent source review on the working tree at base `00dfeba`: no concrete blocker found in exact bindings/metadata, legacy closure, deterministic projection, invitation/existing-party seed preservation, or native import/assignment. Review did not execute code, GUI, export or deployment.

Focused tests cover real eight-variant resolution, deterministic ordering and roundtrip, flat projection equivalence, exact selection, altered seed/catalogue binding rejection, strict metadata/schema/protocol/release, duplicates, bounds and closed legacy behavior. The separate test assembly passed all 17 cases. The runtime compiled against actual installed Unity/Fusion/integration assemblies, and the separate Editor exporter project passed with zero warnings/errors in 7.26 seconds. Exact source and dependency/output hashes are recorded in `PublicCatalogue/source-review.json` and `PublicCatalogue/verification.json`.

Source-only checks completed: `git diff --check` passed and all three project recipe XML files parsed. These checks do not prove C# compilation or behavior.

Next action: root integrates the verified source, preserving its content preflight, then runs the native menu/editor checks when the final content is approved. Root retains all native/editor execution and activation. Export, asset assignment, GUI interaction, deployment and public queue activation have not been run by this lane.
