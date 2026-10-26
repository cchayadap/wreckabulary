# Workshop range Windows capture — independent review

STATUS: SOURCE REVIEW PASS — native visibility and physical-input acceptance pending.

Reviewer: story_status. Source-only scope; no live edits, compilation/native/player/service execution or producer-file edits.

## Pinned baseline

`Assets/_Project/Scripts/WordboundQa/WordboundWorkshopQa.cs`, SHA256 `86ba4ec86f5fed025d99b1a6759809733e6d515199df68f3ced06006672ea643`.

The current scenario has five steps: opening Wordbound (60 s), editing Workshop (60 s), exact playtest (90 s), returning to Workshop (45 s), and Undo/Redo (15 s). Its one QA edit changes the title and seed/lock, then records the exact edited recovery JSON and validated manifest. It verifies primary autosave recovery, no backup fallback or temporary files, real PNG pixels/dimensions, exact playtest identity, return to the same History, and exact original/edited snapshots through Undo/Redo.

The real inspector derives its room selection from `History.Current.Selection`. Selecting an existing random-zone owner inside the existing isolated QA edit can expose the actual UI without adding a second editing transaction. Capture-only zone/foldout/scroll changes must restore on iterator failure as well as success; they must not rewrite the course, mutate the authored placement fields, alter the preview manifest, or rewrite the verified autosave.

## Frozen candidate and verdict

No blocking source finding in patch `246c7913f7597c35b0df980208a8fd19481c3917ffa4a7f479be1a4420ef99db`, candidate `6ccc7b25ebd31b5967c107fc6c3c4db90f9ffaf3dd9f3e8ef7b4467a0c646768`. Final producer manifest: `ff784f1051d00f91f0d407b7fb0cfbb712804eec0c475454fae4fd63fe23f643`.

- Exact baseline, candidate, integrated live source and patch hashes match. Strict reverse application passed after root integration. The companion JSON independently checks unchanged five-step deadlines, exactly one History.Execute, unchanged pixel validator, and byte-equivalent normalized Playtest-through-end source.
- The owner selection is deliberately added to the existing isolated QA edit before `edited` and primary autosave are recorded. It adds no extra transaction and changes no zone, module or placement data. The original initial/edited Undo/Redo checks therefore remain meaningful.
- Selection uses a real existing random zone on the active visible, unlocked floor. The actual private `SelectZone` handler populates the staged fields; idle-gesture preflight prevents it from canceling active editing. Its list copies isolate the saved original list references. The original workshop screenshot remains, followed by two real presented-screen captures for height and floors (8 total).
- Actual screenshot encoding, registration, screen dimensions and nonblank pixel checks remain the existing CaptureVerified/StoryCamera path. No synthetic rendering or image replacement is introduced. Requested and actual scroll offsets are recorded; the source does not falsely assert that all controls are visible.
- Temporary selection-view fields, search, expansion and scroll restore independently in finally. Separate checks require the original History, snapshot/JSON, revision, Undo/Redo availability, primary bytes, deterministic resolved manifest and LastPreview object. `history.Preview` resolves without assigning LastPreview or editing history. No Apply, Undo, Redo, map save or settings mutation is introduced by the capture helper.
- Nested capture iterators are driven locally and disposed through guarded cleanup, so ordinary nested capture exceptions reach restoration. The producer correctly documents that process termination, a stalled player loop or an outer harness timeout can bypass ordinary iterator cleanup; the 12-second inner bound does not certify those external cases. Existing outer deadlines remain unchanged.
- Producer semantic receipt reports QA assembly 11 sources / 304 references, zero errors. The reviewer read and hashed that receipt but did not rerun compilation or native tests.

Root may proceed to the bounded Windows smoke. Actual images must show Height, four rotations, room-only explanation/mandatory owner, and Apply/Cancel at the captured scroll endpoints. The two offsets are initial samples, not guaranteed visibility or physical mouse/controller proof. Root's reported R0 success and prior 35/35 workshop tests are separate evidence, not results of this review. No production-completion claim follows.
