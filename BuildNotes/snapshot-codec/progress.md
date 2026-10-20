# Match snapshot codec lane

STATUS: IN PROGRESS

Working checkout: `wordbound-puzzle-race`, branch `sethypagna/puzzle-race`. Root owns commits. This lane leaves edits unstaged.

Owned scope: new `MatchSnapshotCodec.cs`, `MatchStateValidation.cs`, matching rules tests, this directory, and domain checks inside `MatchProgress.Restore`. No Unity launches or shared course-helper edits.

Binding contract: CourseHash is source-document hash; ManifestHash is exact resolved-course hash. External catalogue bundle and coordinator roster hashes are pinned by the expected binding. Seats are sorted by player ID; actor/economy index is that position. Body IDs use player:index, item:economyID, tile:tileID, keepsake:team, platform:connectionID.

Wire contract: schema 1, 8 MiB/depth 24 bounds, SHA-256 canonical envelope; signed/unsigned 64-bit values are canonical decimal strings; float/double values are lowercase fixed-width IEEE bit strings. Parse and validate every section plus cross-section invariants before returning a usable snapshot.

Implemented explicit canonical wire projections and transactional validation using temporary progress/economy/journal instances. The journal supports validated receipt decoding. Progress restoration now derives objective counts, reachable checkpoints, complete puzzle prefixes, deadlines and result winners before mutation.

Focused verification: 45 snapshot cases and 13 existing progression cases passed the lightweight rules harness. Added a final clean-race loose-resource ownership regression; real NUnit gate is next.

Next recoverable action: run the real NUnit Wordbound gate, hand the snapshot slice to root, then implement AuthoritativeMatch with the frozen binding/snapshot/command contracts. No Unity launched.
