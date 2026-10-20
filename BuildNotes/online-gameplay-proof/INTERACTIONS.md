# Native interaction proof

STATUS: IN PROGRESS

Baseline preserved at93b28e4. Add one separate two-peer Freestyle case using a distinct authored and signed course. Both authenticated participants use their actual Fusion movement inputs and command path. The host's real isolated physics world supplies positions, collision checks, line of sight and melee geometry.

Per participant: jump and land; walk to a fixed map stool; pick up and verify carried state on both peers; walk while carrying and drop through the ordinary validated release path; collect physical BAT supply tiles; begin crafting and wait for the real simulation timer; equip the crafted BAT; walk into a fixed prop's melee range, attack and verify host-derived durability loss plus replica/commit agreement. No test mutates canonical bodies, inventory, damage or physics transforms after start.

Freestyle disables player damage. This case proves allowed prop melee and equipment, not PvP damage, knockouts, combat fairness or cross-device latency. Those require a separate damage-enabled mode fixture. Navigation uses bounded feedback from the authoritative body observation to generate client movement input; this is automation of the same input path, not a movement implementation substitute.

Source implementation is complete. `WordboundNativeInteractionTests.cs` adds `AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps` as an independent gated case. Existing baseline methods retain their original default course and movement path. `WordboundInteractionCourse.cs` supplies the distinct signed content; ordinary pickup revisions, command payload hashes and receipts are asserted, and the result must appear in complete coordinator-committed replicas acknowledged by the client. A newly crafted body must reconcile exactly on every peer.

Verification: actual installed SDK/Unity/NUnit reference compile PASS, zero warnings/errors (`interaction-compile.log`). Invoking the compiled course preset and actual resolver PASS: four fixed props, two nonrenewing BAT supplies (`interaction-course-check.json`). This did not launch Unity or contact external services.

Native execution: **NOT RUN**. Next action: root first completes the original two-peer baseline, then runs `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps` with `WORDBOUND_EXTERNAL_GAMEPLAY=1`. The source case is ready for that verification; no gameplay success is claimed.
