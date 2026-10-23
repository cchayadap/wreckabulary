# Native authored workshop transfer acceptance

STATUS: SOURCE PREPARED; NATIVE EXECUTION NOT RUN

`WordboundNativeGameplayTests.TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap` requires both `WORDBOUND_EXTERNAL_GAMEPLAY=1` and `WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY=1`, deployed bundle support and the real Photon custom provider. It is categorized `ExternalPhotonWorkshopGameplay` with a 600-second NUnit timeout; the existing runner uses an eight-minute lifetime and bounded operation waits. Root alone executes Unity.

The fixture edits the flagship document identity/title and uses seed `fedcba9876543210`, validates the exact Freestyle preview, then creates two independent authenticated guests and real `WordboundPartyCoordinator` instances. The host uploads through `CreateAuthoredParty`; the second joins the party invite and downloads its own bundle before Ready. Both become ready and the host creates/starts a private room. Each coordinator independently verifies its signed launch. The fixture never substitutes this private flow for a public test.

Each native peer composes from its own launch content, not the host's in-memory content. Checks compare independent source/course instances, complete canonical source, source hash, resolved manifest hash, bundle identity, catalogue and effective rules; gameplay frames and native world manifests must match. Existing native identity/body/movement/command/checkpoint assertions run afterward. Cleanup returns reservations, cancels queues, leaves parties, clears credentials and disposes transports/coordinators; teardown failure remains a test failure.

The fixture's preparation transport delegates every request to the real HTTP transport and links the fixture cancellation token only during preparation. It stops linking that token when preparation finishes, so teardown's cancellation cannot erase credentials before authenticated return/leave requests. Coordinators are disposed only after service cleanup (`849978c`).

This verifies authored transfer and actual composition when it runs. It does not claim a completed physical puzzle or manual user interface interaction. Source and native outcomes must be recorded separately.
