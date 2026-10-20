# Canonical deployment lifecycle

STATUS: SOURCE VERIFIED; NATIVE SCHEMA-2 VERIFICATION PENDING

Root authorized Items.cs, Wordbound/EconomySnapshot.cs, AuthoritativeMatch.cs, MatchSnapshotCodec.cs, MatchStateValidation.cs, the MatchState.Schema constant, and focused tests. Earlier schema-1 native evidence remains historical. Home and Course formats are unchanged.

Items default to `DeploymentTick = -1`, preserving existing local-game behavior. Wordbound sets the field to the authoritative tick of a successful Deploy. Pickup clears it immediately; all non-deployed states are cleared during synchronization. Expiry uses the pinned catalogue lifetime, rounded upward to simulation ticks; zero means permanent. Finite lifetimes must be non-negative and no greater than one day.

At a deadline, the host breaks the deployed item once, returns its locked letters through Economy.Break, disables its canonical body, and schedules source recovery where applicable. This happens inside the tick transaction before command execution. Invalid later observations roll back activation/expiry, body changes, allocation and returned letters together. A replayed Deploy receipt does not restart the timer.

The codec writes the activation tick as a required decimal string. `MatchSnapshot.Schema` and `MatchSnapshotCodec.CanonicalVersion` are both 2; schema/canonical version 1 is rejected, with no inferred deployment time or implicit checkpoint upgrade. `MatchSnapshotCodec.ProtocolVersion` recommends `wordbound-v2` for new admission/bootstrap clients. Root and the online lane own the corresponding production and native fixture pins.

Validation requires a causal activation tick for every live deployed crafted item and rejects a deadline at or before the captured tick. Other item states must retain -1. Pure legacy Economy capture/restore allows unset deployment timing.

Verification: 128/128 real NUnit cases passed against the new Rules source (11 seconds), including authority, snapshot, mechanism, return, economy, physical proof-policy and 13 new adversarial deployment cases. The Rules netstandard2.1 build passed with zero warnings/errors (7.26 seconds).

New coverage checks expiry at the exact tick, replayed Deploy receipts, a failed transaction after expiration already mutated state, migration immediately before and at expiration, new deadlines on redeploy, permanent pads, legacy unset timing, rehashed invalid/missing/non-string activation counters, and hard rejection of old schema/canonical versions.

Next action: root integrates these changes, pins wordbound-v2 for current clients and runs native schema-2 checkpoints. The physical adapter can now add finite FAN/CLOCK forces using DeploymentExpiryTick rather than local timers; that force/target extension is not implemented by this canonical lifecycle change. Native execution is root-owned and NOT RUN in this lane.
