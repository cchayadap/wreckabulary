# Native online production-content extension

STATUS: SOURCE COMPILED — ZERO WARNINGS / ERRORS; NATIVE NOT RUN

Root requested the smallest extension from the existing authenticated Freestyle proof fixtures to real flagship race composition and recovery. The current two/four/eight-peer proof map is not flagship race or physical-puzzle acceptance. After the initial read-only proposal, root authorized these exact additions:

- `EightAuthenticatedPeersComposeFlagshipTeamRaceSquad`
- `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture`

The existing small proof and interaction cases remain. The implementation extends the existing fixture/helper and its asmdef; no new framework, production source, coordinator endpoint or service change is involved.

## Content and party setup

The fixture builds `WordboundClientContent` from the actual `items.json`, `rules.json`, chosen mode/format and fixed seed `0123456789abcdef`. It passes the production object's exact Mode, Geometry, Course, Items, Rules and PrivateBinding into `WordboundLiveMatch`. It does not reproduce the flagship hash formula, alter the course, or register it for public matchmaking.

Team races form two equal whole parties: four plus four for Team Race Squad, two plus two for Brawl Duo. The prior groups-of-four helper would put a four-peer test in only one team. The signed roster now verifies two exact teams containing one distinct original party each. Settings use the actual mode format and `MatchSnapshotCodec.ProtocolVersion`.

Each peer initializes its real controller and validates the bound `IWordboundPuzzleContent`. It installs all four production `WordboundPuzzleRuntime` callbacks used by MatchClient: BeforeHostPhysics, ObserveHost, Evaluate and EvaluateMovementEnvironment. Counters observe actual callback execution; they do not fabricate physical observations or puzzle completion.

## Assertions

Both additions retain the authenticated identity, isolated scene, exact canonical/native body set, actual client movement, deduplicated client command and exact replica ACK assertions of the existing proof.

Production-specific assertions pin the full course, manifest, mode, catalogue and roster hashes; check the complete team/lap mechanism count; require every authored moving fixture's canonical/native ID; enforce the 512-body bound; and observe nonzero canonical physical motion produced by actual callbacks. This is production-content physics composition, not proof that all puzzle families can be completed.

The four-peer recovery case subscribes to every survivor's FrameChanged before awaiting the original host's Controller.Stop, discovers the actual elected survivor instead of assuming peers[1], and waits for all three survivors to start new Runners at exactly the next domain epoch. It captures the replacement's first reconciled host frame and requires zero authoritative advances at that point. The committed moving-fixture frontier is compared against that captured frame, so later simulation cannot conceal a reset. It separately verifies one host, exactly the surviving authenticated roster, the original host disconnected, the committed command receipt retained once, and complete bodies on every survivor.

After recovery, a surviving client submits another real command. The fixture requires that command in the newly elected host and every surviving replica with an exact newer-epoch ACK. Other surviving replicas must still have zero authoritative advances.

## Execution and limits

- Existing gate: `WORDBOUND_EXTERNAL_GAMEPLAY=1`, category `ExternalPhotonGameplay`.
- Same production custom authentication, coordinator, public trust, native prefabs and proof scene as the existing fixture.
- Direct Client and Puzzles assembly references are added to the native test asmdef.
- The reference recipe now compiles tests against actual separately compiled integration assemblies, rather than flattening gameplay/physics sources into the test assembly.
- Source compilation passed against actual separate integration assemblies. Native execution is NOT RUN here.
- Graceful host departure is tested; abrupt process kill, adverse network conditions, cross-device play, fresh-client discovery and full puzzle completion remain separate evidence.

Next action: root integrates this fixture checkpoint, updates the native assemblies, and runs the exact tests below. Root owns all native execution.

Exact Editor test filters:

```text
Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.EightAuthenticatedPeersComposeFlagshipTeamRaceSquad
Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture
```

Set `WORDBOUND_EXTERNAL_GAMEPLAY=1` for the native runner process. No additional opt-in or provider setting is introduced. Run each case separately initially. The fixture's overall service/gameplay cancellation deadline is eight minutes. Teardown additionally allows 20 seconds per peer, five seconds for pending tasks, and 60 seconds for coordinator cleanup: at most 225 seconds for eight peers or 145 seconds for four, apart from Unity enter/exit/reload. A 900-second outer harness timeout per case accommodates those bounds and editor transitions. Actual flagship runtime is unmeasured; these bounds are not a passing-time claim.

## Independent source review

Sethy's bounded review identified two assertion gaps, both repaired before compilation: delayed phase comparison could conceal a reset, and two equal party labels did not prove the original party membership. The fixture now captures the first replacement host frame before advancement and compares every signed party ID and exact player set to the original authenticated PartyGroup. The review found no additional concrete elected-host, callback-installation or cleanup issue. No native outcome is inferred from this review.

## Reference verification

One reference build passed in 6.35 seconds with zero warnings and zero errors:

```powershell
dotnet build BuildNotes/online-gameplay-proof/ReferenceCheck.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal
```

The test source compiles against separate actual Unity-generated game assemblies, installed Fusion 2.1.3, Unity 6000.6.3f1 and NUnit. This catches direct assembly/API dependencies without compiling production source into the test assembly. Integration HEAD at capture was `48221d93ed504ef48754d2627279519b5bedfbd6`; the referenced native DLL hashes below identify the compiled inputs precisely, independently of newer uncompiled source. No Unity, HTTP, Photon or coordinator operation ran in this verification.

| Artifact | SHA-256 |
| --- | --- |
| WordboundLiveMatch.cs | E3B002346BC1E118AE0AB9CA9C3EFEB67E9EAB8EA4348C24DBBB47FC46D0A595 |
| WordboundNativeGameplayTests.cs | 41EF96544E41E71C95AF37AD109E644F468E82EA4FDE006902CBB9310E76EE5E |
| Native test asmdef | 326597619B4C28D322717F6D551055447158863FB91B0AC8168ED0B7D8331B74 |
| ReferenceCheck.csproj | 9A8746045372D8BAB6134B7A4BFFE548270DBDF52545C7D9357309F4CA68D8A6 |
| ReferenceCheck.dll | B4E54DB40F4B98D8B739360816D1B0E9C8D35F48036654C8EC105091032EC380 |
| production-content-compile.log | 1F310608BCC800A4EE9F9992907BF121054BA1B599522DCA5DF7060DCF690D6E |
| Wreckabulary.Rules.dll | 9C00F77EC3E706BF5C10A5C864E5045C4E0FFC1FA54C45AF1027613CF4C7FCE3 |
| Wreckabulary.Online.Client.dll | 9160961F1133E0331F8B6EA1378B62735E59E053D5A117559942C06890E5BA8B |
| Wreckabulary.Online.Puzzles.dll | EBEF9BD5BFD58BFCBEE192DEF0198EC15B8A1A1934B529891D969F49B64ED688 |
| Wreckabulary.Online.Gameplay.dll | 53B4C430854BCC8EF88D675FBD618E0767EE0EFEA09EC0170006D48A583973D2 |
| Wreckabulary.Online.Fusion.dll | 7C98699A53D2CF75C5B2B464F75D0BAB966DF6C6ECB60908730095D09E561FEB |
