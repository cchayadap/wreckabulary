# Match snapshot codec lane

STATUS: VERIFIED

Working checkout: `wordbound-puzzle-race`, branch `sethypagna/puzzle-race`. Root owns commits. This lane leaves edits unstaged.

Owned scope: new `MatchSnapshotCodec.cs`, `MatchStateValidation.cs`, matching rules tests, this directory, and domain checks inside `MatchProgress.Restore`. No Unity launches or shared course-helper edits.

Binding contract: CourseHash is source-document hash; ManifestHash is exact resolved-course hash. External catalogue bundle and coordinator roster hashes are pinned by the expected binding. Seats are sorted by player ID; actor/economy index is that position. Body IDs use player:index, item:economyID, tile:tileID, keepsake:team, platform:connectionID.

Wire contract: schema 1, 8 MiB/depth 24 bounds, SHA-256 canonical envelope; signed/unsigned 64-bit values are canonical decimal strings; float/double values are lowercase fixed-width IEEE bit strings. Parse and validate every section plus cross-section invariants before returning a usable snapshot.

Implemented explicit canonical wire projections and transactional validation using temporary progress/economy/journal instances. The journal supports validated receipt decoding. Progress restoration now derives objective counts, reachable checkpoints, complete puzzle prefixes, deadlines and result winners before mutation.

Verification: 46 focused snapshot cases and 13 existing progression cases are green. Real NUnit R1 core Wordbound passed **87/87**, 16.6 s, evidence `TestResults/core_20261020T111958Z.trx` and `Logs/gate_core_20261020T111958Z.log`. Root committed this snapshot slice. The subsequent full core gate including the first authoritative controller slice passed **412/412**, 222.3 s, evidence `TestResults/core_20261020T113146Z.trx`.

Next recoverable action: continue the controller's focused final checks in `authority-progress.md`; root owns integration and Unity verification. No Unity launched.
