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

## First frozen implementation review

Reviewed the three-file working delta above Assets' excluded alignment commit `7b01f5fa`, with Online independently reviewing the Store. No source or test execution in this lane.

The new normal discard path addresses the earlier feedback hazard: Store commits the discard first, removes the memory entry, and converts only subsequent cleanup failure into a notice; Inspector `DiscardPending` then reloads, clearing dirty/conflict state. The added real read-share-lock regression closes and recreates the Inspector while the old primary remains, verifies the committed discard is unchanged, and verifies the saved form is loaded without resaving discarded edits. Existing Apply still requires a successful pending backup before changing the saved asset.

One concrete blocker was sent to Assets and independently confirmed by Online: `RecoveryNotice` reparses the primary even when `ReadState` intentionally ignored those exact bytes via the latest record's `PriorPrimaryHash`. After explicitly discarding a corrupt locked primary, then committing a new pending edit while primary publication remains locked, losing static memory makes `Load` return the new valid draft but Inspector `Reload` subsequently sets `recoveryFailed` when the notice reparses the acknowledged corrupt primary. The notice must use the same recognized-primary rule, with a reopen/Inspector regression. This is a recovery usability failure, not evidence that the pending bytes were lost.

The new tests otherwise exercise meaningful boundaries: actual changed-publication locks, removal of process-static recovery before loading, incomplete staging ignored, complete ordering independent of timestamps, deliberate reversion to older content as a new revision, same-revision conflict, strict malformed UTF-8/oversize/GUID/source/checksum failures, explicit legacy choice, and unfinished v1 form migration. Final source verdict awaits the corrective checkpoint, bounded storage/cap closure reviewed by Online, and final test inventory. Execution remains NOT RUN.
