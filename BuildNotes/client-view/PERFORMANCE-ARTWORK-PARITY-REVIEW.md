# Performance workload artwork parity review

STATUS: PASS — bounded source review of recover_online checkpoint `ba86a4d`. Compile/native/performance execution remains pending. No heavy job or production edit by this reviewer.

Scope: `WordboundGameplayPresentation.cs`, `WordboundPerformanceWorkload.cs`, and `WordboundPerformanceQa.cs`. Exclude baseline-only `14ab16d` from integration.

The workload now obtains the exact initialized resolver from the running local world's presentation component. It no longer creates a separate resolver with legacy collection defaults. The presentation's accessor returns the existing instance, and the per-frame guard requires the same instance and a fault-free presentation. Root's local-client composition supplies its actual assigned visual profile to that resolver.

Source observation follows the resolver's existing order: per-item override, selected collection for the standard skin, otherwise the original ModelLibrary model. It compares actual MeshFilter/SkinnedMeshRenderer shared-mesh identities, counts topology/index/vertex data, observes every material slot/shader/map, and verifies override material-slot precedence. Original skin/tactile material changes are recorded rather than incorrectly required to equal untouched imported materials. Profile validation prevents null override material arrays. No concrete source-selection or Unity API defect found; root's QA assembly already directly references Presentation and the types used here.

All traversal, lists and material observations occur during setup, before warmup/measurement. The recurring resolver identity/fault check adds no collection allocation. The recorded source/model/skin/material evidence replaces the earlier unconditional DetailV2 assertion, so an original-artwork measurement cannot be mislabeled as detailed collection acceptance.

The existing 1080p Low setup, preference restoration, minimum 3-second/120-frame warmup, 60-second measurement, camera path and sampling bounds are unchanged. This remains **eight synthetic actors plus the separately counted local world actor, zero remote network peers**. It is not eight-player network, active-combat or visual-approval evidence. Root must execute the presented release-QA Windows workload and inspect its actual metadata/screenshots before treating the result as measured.
