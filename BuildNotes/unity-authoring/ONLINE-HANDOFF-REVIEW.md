# Workshop online handoff source review

STATUS: SOURCE REVIEW COMPLETE. Compilation, HTTP/service execution and native UI acceptance were not run by this reviewer.

Scope: integration `.harness/staged-online/` plus the matching current Online client content/coordinator implementation. Root owns integration and lifecycle; no reviewed source was edited here.

## Findings and disposition

1. **CourseAsset profile overrides initially disappeared online.** Local play selects CourseAsset camera/visual overrides, while the original `BeginMatch` used only LaunchAssets profiles. Root's staged revision now captures the local overrides keyed to the exact uploaded BundleId and exposes `OnlineCameraFor` / `OnlineVisualFor`. The integration instructions require the matching BeginMatch hook. This resolves the source contract once that hook is applied. Remote peers retain their local visual settings; profiles are not added to signed gameplay identity. Capture before upload also preserves the association when a partial upload is retried from the party screen.

2. **Installed-content compatibility initially failed too late.** The original FromAuthored accepted the supplied catalogue and item/rule bytes even when remote clients could not load them. Online's current revision now resolves only installed proof/flagship catalogue hashes in FromAuthored and rejects descriptor item/rule hashes differing from coordinator installation before CreateParty. Source inspection confirms the checks precede HTTP. Online reports rejection/round-trip tests authored, not run. No executable asset transfer is implied.

No further concrete blocker was found in this bounded source review. Exact immutable draft JSON, seed, selected mode/team capacity and resolved manifest are checked before content creation/upload; the called bundle path revalidates imported data, canonical identity, capacity and physical-plan content. The snapshot is passed into the event rather than fetched after an await. A preview cancels the current editor gesture. The prescribed UI guard prevents editing/second start while upload is pending; autosave remains available without mutating History. Success hides the workshop; failure clears busy in finally and leaves History intact with directions to return to party for retry/leave. Coordinator retry retains the same content/key, and its lifetime cancellation prevents post-destruction state application.

## Native checks still required

- Apply the documented event subscriptions/unsubscriptions, busy GUI/controller guard and BeginMatch profile hooks, then compile the assembled tree.
- Edit a saved CourseAsset, choose a mode/format, start online, and assert uploaded descriptor/document/manifest match the captured draft exactly. Check the undo stack survives return.
- Hold upload pending and verify controls do not alter the draft or issue a second start. Fail after party creation, return to party, retry the same bundle, and leave cleanly.
- Verify the local host's CourseAsset profile is used only for the matching bundle and unrelated/remote content falls back to the local launcher settings.
- Reject unknown geometry and changed item/rule bytes before any CreateParty request.

## Reviewed source pins (SHA-256)

| File | SHA-256 |
| --- | --- |
| staged `WordboundLauncherWorkshopOnline.cs` | `6C906180D7526E5D62541EAB238BF68A66887A7200A9F337E48DE7051079FECA` |
| staged `WordboundWorkshopOnline.cs` | `1C8753037F9BB574B16AF092EA29FEFD3500E2DC3B7DB3A2E257D3648395C63E` |
| staged `WordboundWorkshopOnlineContent.cs` | `DF5BC4D1D0B4C64AE8637C9CFD13AABF2ECBE99767A5FE9692862125DA6BF7E0` |
| staged `WordboundWorkshopOnlineLaunchTests.cs` | `8247974636BF1298D27F29AE4B0DB47550A14692FCD001766FF5EEFD3D99E776` |
| staged `INTEGRATION.md` | `F49CBDC0889C3BB5F150FA7ABCD255BC2B23C3063164044A97F3887C295EC69A` |
| Online `WordboundClientContent.cs` | `A149DC0B7F3DA6D75C09D162CF010266D52B7FE695F15DB88DA31234CFC3CCCC` |
| Online `WordboundPartyCoordinator.cs` | `BBA23CC1C9EE0768B6C6FD62666D574AC2878202F2A54DE01D6AC489ED9FFB69` |
