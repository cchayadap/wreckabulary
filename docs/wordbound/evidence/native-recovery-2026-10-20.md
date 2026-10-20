# Native recovery investigation

STATUS: IN PROGRESS

Integration: `7323c16` plus the per-yield carrier reader and probe changes. No SDK source or binaries were modified.

## Observed results

- `TestResults/editmode_20261020T132615Z.xml`: **FAIL**, 17 passed / 18 total, 124.5 seconds. The 16 gameplay policy cases and complete canonical single-carrier recovery passed. The larger recovery failed completeness validation.
- XML SHA-256: `e6a52abadb6949c4797cf454c66b9beccf8c21ead805315453ff89bfd023b225`.
- Canonical proof: 12,019 raw bytes, 1,870 compressed bytes, one carrier, 9.81 seconds. Recovered bytes, mark, rebuilt carriers and the full application-state import matched. This covers two peers on one PC, not authenticated gameplay.
- `TestResults/editmode_20261020T133054Z.xml`: **FAIL**, 0 passed / 1 total, 119.2 seconds. The client received all 33 carriers before upload. After host replacement, the resume iterator returned only carrier **0 of 33**, with matching mark and wire hash, 4,096 payload bytes and a declared 132,470-byte packet.
- XML SHA-256: `ab69778675b41a541ad0b66bcab5db35d5d5d5b8928596404d84e0128001cb95`.
- No temporary object reference reuse was observed. Per-yield capture remains appropriate because the SDK destroys temporary objects after each yield; it did not fix the missing cloud carriers.

## Installed SDK contract

Read-only inspection used Unity's installed `Unity.Cecil.dll` against the approved Fusion 2.1.3 `Fusion.Runtime.dll`, SHA-256 `d213b95e4694c167f5f6faffcbd75ca1e47f19a9249c91a4964f8342fac9b560`.

`NetworkRunner.PushHostMigrationSnapshot` awaits a send operation. That operation queues data through `CloudServices.SendStateSnapshot` and returns success without waiting for the cloud acknowledgement. The runner's internal confirmed-tick guard applies to a later send.

`Simulation.Server.WriteHostMigrationData` writes a bounded incremental transfer from a rotating object queue. Its initial transfer budget is 4,096 bytes; one call does not promise to transfer every object. This explains the observed single returned carrier. A fixed double-send is also insufficient to establish complete application-state durability.

The application must not acknowledge a complete checkpoint or final result from that first Boolean success. Runtime code will not access private SDK fields or treat a delay as proof of persistence.

## Required correction

Use the coordinator to acknowledge complete immutable canonical checkpoints, with an atomic committed head, writer fencing, exact prior-head checks, verified bytes/hash, bounded uploads and retention of the previous committed checkpoint while an upload is incomplete. Photon continues to provide transport and host election. The replacement host must restore only the coordinator's committed checkpoint.

Protocol implementation, independent review and native authenticated recovery are pending. The previous single-push session path is not accepted for gameplay or release.
