# Native Photon authentication

STATUS: PASS for the three executed authentication scenarios.

The owner saved the Custom Server URL, static `appId` and replacement `providerKey`, disabled anonymous access, and selected rejection when the provider is unavailable. The provider key stays outside the client and tracked source.

- Source revision: `64d2481`.
- Unity 6000.6.3f1; Fusion 2.1.3.
- Command: `WORDBOUND_EXTERNAL_PHOTON_AUTH=1`, then `node Tools/kit/gate/game_gate.mjs test editmode --filter WordboundPhotonAuthenticationTests --json`.
- Gate: R3, cold, PASS, exit 0; 3/3 passed, none skipped; 128.6 seconds.
- XML: `TestResults/editmode_20261020T135810Z.xml`.
- XML SHA-256: `712643d68d33560fbdd547156808f4fcfe8321abccd37837ca60dac19ec7b2c7`.
- Log: `Logs/gate_test_editmode_20261020T135810Z.log`.

## Observed behavior

1. An authenticated host and client connected through the actual deployed coordinator and Photon app. Photon used the two distinct coordinator-issued player IDs. A caller-supplied client identity did not replace the provider's identity.
2. An anonymous connection was rejected after a successfully authenticated positive-control host started.
3. An invalid custom ticket was rejected after a successfully authenticated positive-control host started.

Each test includes bounded runner cleanup and restores the previous scene registration. The expected authentication exceptions belong to the two rejected cases.

The key rotation was separately verified against the live HTTP provider: the old key is rejected and its replacement accepts a valid one-use ticket. See `BuildNotes/online/PROVIDER-ROTATION.json`.

These checks do not test an actual provider outage, gameplay admission certificates, physical gameplay, host recovery, cross-device behavior or latency/loss tolerance. The provider-outage rejection setting is owner-confirmed configuration, not fault-injection evidence. Earlier anonymous transport probes are historical and should not be rerun against this app without adapting them to authentication.
