# Durable Inspector draft review

STATUS: IN PROGRESS — independent proposal/source review. No Unity, compiler, IO probe, native, player or performance jobs; no edits to Assets' source paths.

Root assigned proposal `789ee963` and the forthcoming implementation in Store, existing CourseAssetEditor and authoring persistence tests. Assets owns those paths; this lane owns only this report. Original historical IO1175/locked/BOM evidence remains in INSPECTOR-DRAFT-IO-REVIEW.md and Assets' AUTHORING-SAVE-FAILURE-REVIEW.md. The new recovery design does not establish the cause of the historical unexpected replacement failure.

## Confirmed existing boundary

Current Save serializes the latest pending form into process-static memory, writes one adjacent .tmp, then uses File.Replace or initial File.Move. A failed changed publication leaves memory recovery and may leave the temporary file; Load never chooses that temporary, and a later write can overwrite it. Thus memory retention is not process/domain-reload recovery. The proposed revisioned immutable committed sidecar closes discoverability/ordering without substituting unsafe primary deletion or claiming every disk write succeeds.

The proposed revision/checksum ordering, fresh staging-to-committed Move, equivalent same-revision checks, legacy explicit choice, and higher-revision discard tombstone are a coherent bounded design. Revision and integrity checks must include the identity/order/type fields, not only the document text. Incomplete staging is not a committed candidate; unsupported/corrupt/conflicting committed evidence must not silently load an older version. Existing invalid authoring form recovery remains distinct from authored-content validation at Apply.

## Early concrete implementation concerns

1. **Committed discard versus failed cleanup:** existing Inspector catch paths retain dirty state. If Clear commits a higher discard then throws only because cleanup failed, OnDisable/TrySavePendingDraft could save the old dirty form as a newer record and undo the user's discard. Apply has the same problem after successful asset persistence followed by Clear cleanup failure. Logical discard completion must reset memory and Inspector draft/dirty state, while cleanup failure remains visibly reported. A failure before the discard commit must retain the pending form. Require an actual controlled cleanup-failure/reopen/Inspector-close regression.
2. **Exact ordering on retries and reverting content:** identical latest payload may reuse its committed revision, but matching an older primary is not enough. Editing back to primary content after a different newer pending record needs a new revision. Equality includes saved-source binding and document, and revision allocation must examine surviving records after reload.
3. **Recovery and cleanup remain separate:** cleanup may remove only recognized owned records whose revision is superseded by a validated committed record. A partial staging write, unreadable record, conflict, directory failure or failure to commit the first pending record cannot be described as durable recovery. Preserve the memory-only warning where appropriate and the original primary publication exception.

These are review requirements for the source checkpoint, not findings that the pending implementation already violates them. Assets has received the committed-discard/Inspector feedback hazard before freezing source. Stable implementation and tests are pending.
