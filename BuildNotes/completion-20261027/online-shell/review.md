# Online shell independent source review

STATUS: COMPLETE — no remaining blocking source finding for bounded native testing.

Reviewed final revision3 patch `2b5d7fa7c5a231e8dc4b49d7092d27ca5443bac3a6dcb67d7642e50c6b2fb657`. Independently verified five candidate hashes, four baseline hashes, strict forward `git apply --check --whitespace=error-all` and C#9 Editor/test/QA syntax (five files, zero errors). This review ran no Unity, service, physical input or screenshot job and changed no live source.

## Finding resolved before integration

Revision1 reset controller focus on Back from Join or an optional party section without requesting scroll reveal. A previously scrolled Join view could select the Create tab outside the viewport. Revision2 sets reveal on each new Back focus reset, resets Join→Create scroll, and adds nonzero-scroll/reveal/selected-control assertions to the actual handlers. The earlier retry and active-match priorities remain intact.

Producer self-review also identified that two new public-queue tests used one human in Freestyle with Fill off, which the existing coordinator rejects before HTTP. Revision3 adds the real Fill action before creating those two parties. I independently checked both setups against Queue preflight; runtime candidates remain unchanged from revision2. Earlier source clearance did not prove those tests could reach their requests.

## Source assessment

- Create/Join and both optional sections enter PartyContext. Queued actions retain the existing Update-only dispatch and are discarded when the visible context changes. Hidden controls do not register for focus; Layout/Repaint still cannot directly submit a coordinator request.
- Ready, public eligibility, explicit custom-room consent, accept/start, cancellation, leaving and uncertain-response Retry use the original coordinator methods and action IDs. The new queue-without-assignment status is display-only. Tests preserve request key/body identity on Retry and distinguish Cancel from Leave.
- Back closes an optional section before leaving an idle party; retry and active-match return retain priority. Active-attempt Back retains the prior Leave behavior, so native review should label that behavior accurately rather than call it Cancel.
- The backdrop uses the original selected LobbyThemes resource or explicit theme override. It borrows the texture and restores GUI.color; the error path restores GUI.contentColor. There is no new texture/material/camera allocation or destruction of borrowed art. Existing theme validation/fingerprint covers the added finite shade and override controls. Disposed UI resources and coordinator/input objects have existing idempotent cleanup paths used by the new borrowed-resource test.
- Composition text matches the current ModeDefinition capacities/formats, and RegionLabels indexing is bounded by OnlineSettings' exact asia/eu/us validation. Nothing adds online support to the original five local house modes or invents bot support. This retains the Home image context, not the Home 3D scene or avatar.
- Ten added cases meaningfully cover visible-control registration, stale hidden action rejection, section Back behavior, cancel/leave, retry identity, GUI/borrowed art preservation, mode copy and invalid shade. Direct handler calls and fake HTTP prove source flow; they do not prove a physical controller or live service.

## Native gates still open

Compile and run the focused party-input/theme fixtures; inspect guest/Create/Join/party/settings/private/search/assignment/retry views at small height and maximum supported font. Confirm focus reveal after Back and that Ready/Find match/Cancel remain reachable. Real screenshots must decide text load, panel/background contrast and original-art coherence. The new fields also need actual saved Interface.asset default/Inspector edit/reload verification; this patch does not serialize a new scene or overwrite an artist's theme. Re-run the Windows lobby route and existing real online gates separately. Source clearance is not visual, network or owner acceptance.