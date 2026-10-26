# Independent starter physical fixture review

STATUS: SOURCE REVIEW COMPLETE — revision 2 has no remaining source blocker. No live source edits, native jobs or service calls by this reviewer.

Revision1 patch88d77831 / candidate291967b4 examined. Two bounded corrections sent to producer: split input restoration and observer detachment into independent cleanup attempts; explicitly require the deployed native BoxCollider enabled and active to support the stated physical contract. Neither changes gameplay authority or acceptance thresholds. Integrity and remaining progression checks are being completed.

## Final revision 2 findings

The two findings above are resolved in the separate `cleanup-collider.patch` (`7881534c510a2a6d2b6b3042be71b96116181ccaba77896234b56eaad707de4e`). Source candidate `a0863aab7f6b53fb8d6bd3f10001f02f5bf8550b5dc441efecb5af9b66dfff6b` now attempts input restoration and observer removal independently, records each failure, and still performs stop/root/Play Mode cleanup. It checks the real deployed BoxCollider is enabled and active. No movement, puzzle, progress, durability or timeout threshold is weakened. Revision 1 source remains preserved with original hash `291967b4bf445cabe9027ff4a3d8ae91f8e7f19dd11eed52b7002105a5b89dd8`.

I independently verified the candidate, full revision 2 patch and six reference-input hashes against the manifest. Root integrated revision 2 while review was finishing: current live source equals the candidate after newline normalization, and strict reverse-apply check passes. The first forward-apply check then correctly reported the already-applied path. Independent C#9 parse with Editor/test/QA symbols has zero syntax errors. Producer's separate in-memory semantic check is recorded in source-check.json; I did not rerun semantic compilation or native execution.

Source review confirms the launcher uses a unique owned Workshop storage folder, the exact RaceCircuit template and real autosave/history/RequestPlaytest/ReturnToWorkshop paths. The only gameplay-driving substitution is this owned controller's ReadMovement delegate; actions use public Submit and check authority receipts. There are no direct body-position, progression, solved-state, simulation-clock or collision-rule writes. The actual production local return stops gameplay before removing the owned client and restoring the Workshop/lobby camera. The fixture restores its movement delegate before normal return and during failure cleanup, and refuses to overwrite a delegate it no longer owns.

The progression checks agree with MatchProgress: two ordered checkpoints plus one finish increment per lap; the final checkpoint and carrier finish can legitimately publish together. The exception permits that exact transition, still requires observed lap starts/first checkpoints/deliveries and terminal 3 laps/9 objectives, and checks carrier ownership/position at each lap transition. It requires fresh generated letter IDs, three fresh crafts, installed durability, actual socket hold/site identity and consumption/body cleanup. Original STOOL mesh identity and active renderer are source/presentation checks, not visual-quality acceptance.

## Native evidence and limits

I independently read the revision 1 result `TestResults/editmode_20261026T180952Z.xml`: exact starter case PASS, 1/1, zero failed/skipped, case duration 114.761832 seconds (root reported whole gate 146.2 seconds). The associated log records laps at ticks 2578/4392/6390, fresh renewable generations 0/1/2, actual ReturnToWorkshop with preserved save/history and cleanup failures=0. This belongs to revision 1. Revision 2 adds stronger assertions/cleanup and needs its own fresh execution; it cannot inherit a native PASS by source review.

This fixture is scripted admitted motor input in local native gameplay. It does not establish physical keyboard/controller selection, camera/visual polish, Windows delivery, public matchmaking, multi-client race ownership or automatic host recovery. Those remain separate root-owned acceptance work.

Evidence SHA256 `TestResults/editmode_20261026T180952Z.xml`: `73604f45d8f703ad8addc0e805113e9d6e5b7b768854af36ea7a83a89de2af21`.

Evidence SHA256 `Logs/gate_test_editmode_20261026T180952Z.log`: `f5c7c538a290fe1ecd1c64734bc3115ec151980987f16fd1a15f366f94abf64b`.
