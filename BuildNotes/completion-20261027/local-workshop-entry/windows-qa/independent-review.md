# Create selection QA and supported-layout fixture review

STATUS: COMPLETE — no blocking source finding in Windows QA patch `56a117b1` or the bounded supported-layout fixture correction. No live source, native test, saved preference or scene changes by this reviewer. Native reruns and Windows images remain root-owned gates.

## Windows selection path

Independently checked patch SHA `56a117b1ffa15215c2a86a62ec50de2c8ded5f56cbbfb37714d8bda66c1563a5`, candidate SHA `d7ad88f7bf2c8278e5fc38507fd4cbf3cf730c1b68de3894af2ad04b393cf542`, exact current live equality and strict reverse apply with `--whitespace=error-all` (PASS). The single added screenshot raises the expected walkthrough total from 19 to 20.

`GuardChoices` parameterizes the previously reviewed owned iterator driver without removing assertions or increasing deadlines. Local retains 25 seconds within 30; Create gets a deadline set immediately when its Do callback is entered, 15 seconds within the unchanged 20-second step. Nested enumerators are driven on the owned stack; ordinary completion, nested exception and the owned deadline all reach reverse-order disposal, recording cleanup failures. Create's finally restores its prior WordboundMaps family; preference preservation is in a separate finally so a page-restoration exception cannot skip it. The documented abrupt-termination/long-blocked-frame/shorter-external-budget limitation still applies.

The new navigation invokes real Submit callbacks on **Create Wordbound** and **Create house**. Production `PlayPage` handles these by setting WordboundMaps and rebuilding/reselecting the page. The test checks the card and OPEN button but submits neither; it does not call the launcher or workshop storage. Queue/mode/map and party/input-binding references are compared across the switch, and typed saved preferences are checked/restored on failure. The unchanged active-Hub assertion is consistent with this source path, but is not an independent filesystem audit of every possible autosave writer. This is selection-only QA, not actual creator launch, workshop playtest completion, physical-controller or multiplayer acceptance.

## Supported-layout correction

Opened the actual `TestResults/playmode_20261026T200347Z.xml`: **9 total, 8 passed, 1 failed, 0 skipped**, duration 10.2725364 seconds. `CreateFamilyKeepsHomeModeMapAndEveryLocalSeat` expected `flat` but got `pinwheel`. XML SHA `da152a188b47213da51a714a9835af4007097d239314540569056d490f131d75`. This failed run remains unchanged; the correction is not retroactive evidence of a pass.

The failure is explained by the existing production contract: `HomeDesigner.Supports` admits only `pinwheel` and `courtyard`; `LobbyMenu.Choose` deliberately replaces an unsupported Workshop map with a supported map, defaulting to pinwheel when necessary. Requesting flat before testing retention was therefore invalid setup.

The two new creator cases now request **courtyard**, immediately assert that this non-default supported map was selected, and retain the exact courtyard assertion after switching/returning. This strengthens the setup and still catches unwanted reset to pinwheel. Compared with the frozen revision2 native fixture, its only new delta is the map value, explicit precondition and corresponding return assertion. No runtime support list, launch behavior, timeout, seat/reference assertion, storage path or cleanup rule changes. Existing solo-house flat cases are unchanged and remain appropriate to their different route.

`Session.SelectMap` only validates the house and updates in-memory MapId; it does not persist a preference or autosave. Existing TestScenes/session teardown and native fixture preference/source preservation still apply. Native creator tests retain actual Single playtest/storage/return checks and their separate lifecycle limits; this review does not infer their result from the UI correction.

Reviewed corrected live hashes:

- `LobbyPlayChoiceTests.cs`: `d15025eca05027a357e05710f96e821f3c2a8f88cd6967046c1bf311e62cc341`.
- `WordboundNativeWorkshopEntryTests.cs`: `49fedad0d05ae46a3fb09183bdd91293c813f717c4d2d593cef0c2a281d89cbb`.
- Contract `HomeDesign.cs`: `839f442b84341003a88bd5d7d4175401f47f699d23ec520b6ed4afb2eec2bf40`.

Root must retain the fresh nine-case lobby result, three native creator cases and actual Windows 20-image walkthrough as distinct acceptance evidence. No native execution occurred during this review.
