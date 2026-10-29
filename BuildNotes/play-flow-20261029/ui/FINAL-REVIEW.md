# Final Play flow review

STATUS: COMPLETE — READ-ONLY SOURCE/EVIDENCE REVIEW; WINDOWS VISUAL ACCEPTANCE PENDING

Read-only review of current live source and latest native test evidence. No Assets or native runs in this lane. Root owns label truncation; build_acceptance owns map focus. Review will distinguish source guarantees from Windows acceptance still required.

Native evidence confirmed: `playmode_20261029T032644Z.xml` has 75 tests, 73 passes and **two failures** (not 75 failures): focused House map visibility and Home builder label truncation. `editmode_20261029T032349Z.xml` passes all 34 input/layout tests, including the measured compact online form and actual rendered pad focus guards. The two Play failures remain release blockers until their fixes pass the real tests; those paths have assigned owners.

## Findings and preserved behavior

1. **Fix the Create selection ambiguity before final capture.** `PlayPage.cs:209–216` treats Tutorial as selected whenever `Menu.Mode` is Tutorial. Clicking Map editor sets `WordboundMaps=true` while retaining that existing House-mode store, so Create → Tutorial → Map editor can mark both choices selected. Minimal display-only correction: the tutorial selected condition must also require `!WordboundMaps`. No canonical ID, save or launch-path change is needed. Root was notified; this lane did not edit production.
2. **Verify the arena asset binding before packaging.** `WordboundPartyMenuMapPresentation.cs:28` uses `CombatArenaPreview`, but the inspected `Resources/UI/LobbyPlayLayout.asset` has no serialized binding for it yet. FFA then truthfully displays Toybox Arena with no image. Root has the capture/import work; confirm the actual Sprite is assigned and survives the Windows build. Lost Address, Parcel Hall and Parcel Service already have saved bindings.
3. **Fill-off co-op still reaches a predictable error after Find match.** The default human Fill fix is present, FFA clears Fill, and all 34 input tests pass. If a lone user deliberately turns Fill off, `WordboundPartyMenu.cs` enables Find match from `PublicAvailable` alone, while `WordboundPartyCoordinator.cs:273–275` rejects that request. This is a remaining preflight usability refinement: show the concise existing condition before launch, or disable the action with its reason. Preserve the actual coordinator guard and joined-party settings. This does not invalidate the tested new-party default.

The centered category strip is defined in `PlayPage.cs:29–34` and positioned centrally by `LobbyPlayPageView.FitOptions`. Practice has seven direct modes immediately below it; the mode dock stays outside the map scroll. House, solo and creator map cards use the same grid/footprint helper and selected treatment. Escape retains Random only; the home builder retains its two supported houses; Time Trial and Freeplay share the same three installed local courses with the one-seat launch guard. Online course/format/seed resolution and private-content admission remain intact. Parcel/Kitchen have not been silently admitted to public matchmaking.

Matchmaking still opens the existing Wordbound scene, where five direct online modes precede the actual course image. The category strip itself is not persistent in that scene. Back returns through the existing transport flow; the guest header also exposes Workshop where wired. This preserves the current scene/network boundary, so Windows review must cover the transition and return as well as each static screen.

Minimal text is materially improved: the duplicate Play heading/subtitle are hidden; the offline summary keeps one selected objective and one Play action; online seed/source/shuffle/region details are folded under Match settings, with the primary course and Find players choice visible. The shared theme binds Roboto Regular/Medium and a dark neutral palette. `LobbyMenu.cs:619` hides characters on Play. The inspected Play/party presentation source adds no stripe decoration. These are source facts; this review did not inspect a new rendered Windows screenshot and cannot certify the final visual result.

## Windows acceptance order

- First rerun the 75 Play tests after the two owned fixes; preserve the 34/34 Edit receipt and rerun the affected input checks if online source changes again.
- At 1280×720 and a short window, capture Practice, Time Trial, Freeplay, Escape, Home builder, Map editor and Tutorial. Each mode has exactly one selected choice; all direct modes remain readable; focus reveals every real card; header and Play remain fixed. Check no ellipsis, character overlap or stripe background.
- Capture online Time Trial and FFA using their actual installed images. Check guest Connect, collapsed/expanded settings, Ready, Find match, cancellation, retry and Back with mouse and pad. Real focus and controls must stay visible after scene, party, error and window-size changes.
- Verify mode/format changes clear stale map eligibility, human Fill stays explicit, FFA never sends Fill, and a private seed does not enable public Find match. Test actual public/private room connectivity separately; the 34 native tests use a fake HTTP transport and are not evidence of live Photon or all-mode networking.
- Keep visual fidelity claims bounded: House cards show real vector floor plans; Wordbound cards show actual course captures; the Workshop placeholder is still item artwork. Consistent card sizing does not mean every preview has the same art medium or that all maps support every mode.

`final-review-receipt.json` saves exact inspected source/asset/test hashes. No production Assets, metadata, harness, networking service or native process was changed by this review.
