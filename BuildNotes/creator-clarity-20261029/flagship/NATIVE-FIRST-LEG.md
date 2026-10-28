# Full flagship first-leg native proof

STATUS: PASS — BOUNDED FIRST LEG ONLY. Independent read-only review of the actual XML, log and matching test source. No native processes or production changes were made by this reviewer.

## Exact run and evidence

Run `231532` executed `Wreckabulary.OfflineGameplayTests.WordboundNativeFlagshipRouteTests.OriginalStartFirstThreeRoomsAndTwoPuzzlesUseMotorCraftsAndNativeSensors` with the existing explicit LocalFlagshipRoute gate. XML reports1passed/0failed, test duration167.362726seconds, start2026-10-28T23:16:02Z/end23:18:49Z. Parent reported whole gate202.1seconds; that includes work outside the XML test duration and is not substituted for it. Native log line5038 records normal exitcode0. Cleanup reports0failures.

| Evidence | SHA256 |
|---|---|
| TestResults/editmode_20261028T231532Z.xml | `420b664cbe5ba857774a7fb089f8dcafe681b67e2cff87c4dd7f7dc561ac7858` |
| Logs/gate_test_editmode_20261028T231532Z.log | `a7171cf860e98b62fa72e210d23420e5b751659265e6d0d0d95a1d1e3f43d148` |
| Assets/_Project/Tests/OfflineGameplay/Authoring/WordboundNativeFlagshipRouteTests.cs | `d050053947c6fa0f7b766dfa99875e30ce0dd05bccae09e4bba259c8e22c6b4d` |
| map-toughness/manifest.json | `8e828dab9360a0472788b71397ae524be2a6ddd57325d872570512d7eeb4cb34` |

The reviewed live test matches the exact final frozen candidate. Checkout HEAD at review was `c6b86a49ba5c7744ae5017eb33265d4fdb96ca59`; integrated work is separately identified by source hash, so this is not a claim that HEAD alone contains every tested change.

Actual runtime content bindings:

- Source `83868321d648ba9726c064a532845c93adb9a1e5aded936b2ef3559667ccbfb9`
- Course manifest `6e86080bfbe61509ad1278de82c76cf8f0232782f8619db03820f0113212609b`
- Geometry `3a5550b32385ddfd77ae0625081f173c062910e67efff6b842b80210d00206cd`
- Catalogue `275d4e59b35f928ae2c5c8dbfaf7df27915edaae5abd8fd835b68189610b9533`

## What the source and actual run prove

The production Lost Address factory resolves the full34room/72physical-site/3lap course at seed0123456789abcdef. Source asserts four layer heights0/8/16/24m and exact full-world manifest/geometry bindings. The log records all three complete ordered paths and route measures751.2666/799.2666/799.2666m. Those whole-route measurements remain geometry evidence; only the following actual first leg was traversed:

1. Ordinary Start0(-42,0,-18)m. The admitted unchanged motor walks outside the initial trigger, takes the actual keepsake via the public command and carries it through Parcel Gate.
2. Actual Packing_Walk0 door traversal to Label Bench. Six real HAMMER letters are collected; HAMMER is crafted/equipped. The actor approaches the actual north-side TABLE collider without penetration, faces the item, passes real reach/arc/support and submits Attack. The logged actor(-7.0003,0,-20.2158), item(-7,0,-21), colliderbounds(-7.6,0,-21.3455)..(-6.4,.72,-20.6545), distance.78416m versus reach1.25m confirm a plausible supported stance.
3. Bound economy rules produce TABLE map toughness1+.6×5=4. The actual attack observes4→0 with HAMMERbreakPower4.5; one real damage observation is required, then original TABLE fragments are collected. Crafted TABLE item59 physically deploys to CollectLetters socket, completes native support/hold and is consumed through UsePuzzle.
4. Actual Packing_Walk1 door traversal to Crate Press. Recovery letters are collected, crafted TABLE item60 deploys and completes the PlaceObject socket under the same real sensor/command path.

Final assertions require16collected letters,3actual crafts(HAMMER+2TABLE),2solved mechanisms, the exact ordered3room/3checkpoint rosters, ownership of the keepsake, no finished team/result and nonzero admitted movement reads.

| Checkpoint | Tick | Canonical = native actor position(m) | Anchor(m) | Radius/support/carrier |
|---|---:|---|---|---|
| L1_Packing_ParcelGate |776|(-37.1909,0,-11.9996)|(-36,0,-12)|1.2m / true / local-player|
| L1_Packing_LabelBench |6069|(-10.8082,0,-12.0368)|(-12,0,-12)|1.2m / true / local-player|
| L1_Packing_CratePress |9524|(13.1907,0,-12.0467)|(12,0,-12)|1.2m / true / local-player|

The observer verifies strict ordered increments, carrier, actual authored radius, native/canonical positions, ground support and input admission. All four player/team/body/keepsake recovery generations remain at initial0; no knockout/recovery substitutes for travel. The fixture writes admitted movement/facing input only; it does not set player/item position, velocity, solved flags or authority snapshots. Source document JSON and remembered input assets/meta bytes/serialized memory are checked before success and again in teardown. Input callback ownership is restored, observer removed, local client stopped, owned root/world released and PlayMode exited. Actual cleanup0 supports those bounded checks, not preservation of every possible untracked Unity internal state.

## Superseded failures and remaining scope

Historical224431 remains a genuine failed test: the first approach entered the checkpoint before pickup. The corrected fixture intentionally proves carrier-first traversal; production checkpoint rules permit any admitted supported teammate, while finish requires carrier. Historical225343 failed before any Attack because the far-side approach crossed/chased movable furniture. Historical230725 failed the erroneous27-strike assertion. Map furniture uses4toughness here, not crafted definition120durability. The27-strike/6.7-minute-smash/fourteen-minute pacing claims were incorrect and are explicitly superseded in AUDIT/HANDOFF; all old XML/logs/frozen candidates remain unchanged.

This PASS supersedes only those fixture blockers. It does not prove remaining31rooms/70physical sites, FAN/CLOCK/jump/balance behavior on the traversed route, any8m rise, reverse/repair laps, natural full-run finish, pacing/fun, multiplayer/team competition, Windows delivery, recovery under failure or art quality. Do not close the full flagship goal. Extend this same authority/session through the next authored Ribbon Switch/FAN station and later checkpoints without replacing course geometry or injecting poses/results.
