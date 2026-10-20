# Wordbound party launcher

STATUS: SOURCE VERIFIED — NATIVE MENU NOT RUN

Owner: online lane, branch sethypagna/online. Owned new source paths: Client/WordboundPartyMenu.cs, Client/WordboundClientContent.cs and new party helpers/tests. Root owns the existing MatchClient, HUD, PlayerView, native integration and scenes. No metadata generation, external deployment or shared lobby edits in this lane.

Scope: isolated usable party interface for five modes, region, Solo/Duo/Squad where applicable, human Fill, guest name, create/join party invite, readiness, private create/join code, launch, cancel, leave and terminal return. Commands use the real authenticated coordinator client. Polling must coalesce and lifecycle cancellation must fence stale completion. Repeated actions cannot produce overlapping mutations. Public Quick Play remains unavailable while the approved built-in registry is empty.

Handoff contract to settle before implementation: the menu emits a verified immutable launch context containing exact content, resolved course, mode, signed match/roster, coordinator credentials and a fresh per-Runner join callback. Root's MatchClient owns actual Unity gameplay creation and stop. Race content uses WordboundPuzzleProof.Create with the exact item and rule-book bytes; non-race content uses real templates. The selected deterministic seed and complete content binding must survive every party/launch check.

Checkpoint: safely merged integration890ea23 into this lane as57deba4. New frontend source and focused tests are drafted. Existing root Client files/assembly configuration remain untouched. The exact composition contract is in CONTRACT.md.

Tests prepared: real content/seed/rule binding across five modes, closed public registry, party/room invite fidelity, lost create response exact retry and replacement blocking, duplicate clicks and coalesced polls, disposal fencing, content mismatch refusal, separate Custom Fill consent, returned-match suppression, proactive refresh, signed launch once and fresh per-Runner credentials, and pending mutation versus automatic admission.

Verification: all 18 focused NUnit tests passed. The menu and helpers compiled against the actual Unity 6000.6.3f1 reference assemblies with zero warnings and zero errors. VERIFICATION.json pins source and assembly hashes. Native menu rendering and complete menu-to-game launch remain NOT RUN. Root independently reported the first authenticated two-peer canonical gameplay baseline green. Compiler window released to the flagship lane; root owns the next native window.

Next recoverable action: commit this verified frontend and merge integration9d1e459 before the separately authorized offline-session interface work. Preserve online RunnerStarted, scene, admission and checkpoint semantics. Root owns launcher wiring and native acceptance.

Independent startup review: read both native logs144759/150614, current Session/Controller/fixture, and installed Fusion.Runtime getters through Unity.Cecil. `NetworkRunner.IsRunning` delegates to `Simulation.IsRunning`, which tests only whether the simulation is not shut down. It does not establish completed client StartGame/runtime-config negotiation. `DeltaTime` reads runtime TickRate. The controller's previous immediate exact60Hz check therefore had a startup-readiness contract mismatch. Root accepted this evidence and owns a per-Runner completed-start barrier plus native verification. No Session, controller, vendor SDK or native fixture was changed in this lane.

Root integration: party source `d4c04b5` landed as `975537a`. Additive lobby entry, launch assets, party/client composition and native scene installer are implemented. R0 and native installer pass after repairing the test assembly reference and clean batch scene setup. Evidence: `docs/wordbound/evidence/launcher-2026-10-20.md`. Offline session is under separate source verification; the root local client is only an ignored scratch draft pending those contracts.

