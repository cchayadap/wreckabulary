# Clean production importer

STATUS: FROZEN / SEMANTIC PASS. Integrator owns native verification and replacement.

`candidate/WordboundArenaPreviewImport.cs` is the exact original pre-recovery importer with only `OtherFields` corrected to remove `CombatArenaPreview` inside the validated `MonoBehaviour` wrapper. The one-attempt finalizer is absent. No imports, fields, source/content guards, normal immutable-image/repeat behavior or asset settings otherwise change.

Candidate SHA256: `ed504d7ec5c7417bb6b3a69507da863b067cbc4b811c701c0cea46c5bb960209`.

Required live baseline is the reviewed recovery candidate SHA256 `180106c8b7dc3286ee2c4b9c31aae41de748dd57ddacd6cf658eebf1fb1ae7ea`, pinned in `manifest.json`. After root confirms `FinalizeRetainedImport` passed and its completion receipt is retained, copy only this candidate to its existing importer Assets path, preserving its metadata. Keep the full bounded recovery candidate, original baseline, manifests, native receipts and exact before-import backup as historical evidence.

Semantic compilation of the clean candidate passes29 sources/314 references with zero errors (`source-check.json`). This child did not edit Assets or run Unity. Later helper-source changes do not invalidate an already-completed historical import receipt; a new first import still requires a fresh matching capture, while the existing completed-import branch verifies immutable image/receipt/GUID and preserves current owner layout edits.
