# Mode capability audit — 26 October 2026

STATUS: SOURCE AUDIT COMPLETE; bounded Play-page draft in progress. No native tests or service calls performed in this lane.

Baseline: `sethypagna/wordbound-integration`, `fd5bb14777def6df40fdbf8d3b2e88d04755b480`. Root subsequently extracted `PlayPage.cs` unchanged from `LobbyPages.cs`; implementation uses that extracted file. This report describes actual routes, not whole-project acceptance.

## Local play on this PC

| Mode | Human seats | Bots | Teams / objective | Actual route |
| --- | --- | --- | --- | --- |
| Dibs! | 1–4 | Empty seats fill to 4 total | Every player for themselves; first to 3 rounds | `Session.LoadMode("Dibs")` → LivingRoom → RoundManager |
| Double trouble | 1–4 | Empty seats fill to 4 total | Two teams of two, down/revive, first to 3 rounds | `Duos` → LivingRoom → RoundManager |
| The great escape | Runtime accepts 1–4; config note says 2–4 | None | One cooperative crew, recover keepsakes and reach van | `MovingOut` → LivingRoom → MovingOutDirector |
| Moving day | 1–4 | None | One cooperative crew, furnish marked rooms before timeout | `MovingDay` → MovingDay scene/director |
| The Escape! | 1–4 | Explicitly no AI crew | One cooperative crew, word doors / keys / exit | `Escape` → LivingRoom → EscapeDirector; random house |
| Play & learn | Scene admits up to 4 local inputs | No crew fill; training dummy is separate | Shared tutorial steps watch every joined player | Tutorial scene / TutorialDirector |
| Creative workshop | One author / tour player | None | Build and tour a home | Existing CreativeWorkshop route; keep current supported-house checks |

Source: `Scripts/Core/Session.cs:55`, `Scripts/Game/RoundManager.cs:40` and `:129`, `Scripts/Game/PlayerJoinManager.cs:61`, `Scripts/Game/MovingOutDirector.cs:31`, `Scripts/Game/MovingDayDirector.cs:84`, `Scripts/Game/TutorialDirector.cs:101`, `Scripts/Rules/Teams.cs:16`, `Data/Config/rules.json:76`. All paths under `Assets/_Project/`. Tutorial/LivingRoom/MovingDay scene `maxPlayers` values are 4 (`Scenes/Tutorial.unity:1049`, `LivingRoom.unity:2405`, `MovingDay.unity:822`).

Important: the first two humans in Double trouble are assigned opposing teams (`Teams.Assign` produces 0,1,0,1). Two local humans do not currently form a human duo against two bots. The existing `Seats` copy correctly says “2 players, each with a bot”. Do not introduce a “play together vs bots” toggle without implementing team assignment.

The local party counts devices on this PC, not online friends. `LobbyMenu.cs:439` admits gamepads and the right keyboard half to the additional party; the lead player is assigned from the starting controller or desktop/touch in `Seat`. Bots are not persisted in `Session.Bindings`.

## Wordbound online modes

| Mode | Match roster | Maximum invited party | Bots | Public / private contract |
| --- | --- | --- | --- | --- |
| Time Run | 1–4, one cooperative team | 4 | None | Public co-op requires ≥2 humans, or Fill on to find humans. Private permits one human when Fill off. |
| Team Race | Two teams; 1, 2 or 4 seats each | 1 / 2 / 4 by format | None | Both teams require a human. Fill off permits smaller human teams; Fill adds humans. |
| Brawl Race | Same team sizes as Team Race | 1 / 2 / 4 | None | Same admission, with enemy damage enabled. Distinct from original Double trouble. |
| Free-for-All | 2–8, individual players | 1 | None | No Fill switch. Friends can join the same private room with separate one-person parties. |
| Freestyle | 1–8, one cooperative group | 4 | None | Public requires ≥2 humans or Fill on. Private permits one human. No timer/winner. |

Source: `Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs:74` (roster/team rules), `OnlineContracts.cs:16` (network settings allowlist/capacity); `Assets/_Project/Scripts/Online/Client/WordboundPartyCoordinator.cs:82` and `:263`; `WordboundPartyMenu.cs:284`, `:365`, `:413`; `OnlineServices/src/model.ts:118`, `:130`, `:150`; `OnlineServices/src/party.ts:163`; `OnlineServices/src/pool.ts:257`.

“Solo / Duo / Squad” in the race format means players per team, not offline / online. The party cap is min(4, team capacity), even though Freestyle matches can contain 8. Quick Play also requires an approved map/seed; custom workshop maps use private hosting and explicit Custom Fill consent. Local couch inputs are not turned into additional online identities by the current launcher.

Offline Wordbound: Workshop local playtest supports one human in Time Run or Freestyle only (`Scripts/Rules/Wordbound/WorkshopModes.cs:13`, `:46`; `Scripts/Online/Client/WordboundLauncher.cs:202`). It is not an offline bot match or a direct general solo campaign launcher.

The five original house mode IDs are absent from both online settings allowlists. A new UI label cannot network them. They remain local until distinct authority/state/admission adapters are built and tested. Prior detailed audit: `BuildNotes/restart-20261026/all-modes-online-scope.md`.

## Approved minimal flow

1. Home → Play → **Local / Online / Create**.
2. Local: choose one of the original five modes, choose house, see one concise objective + actual participant summary, GO. No unsupported bot toggle. Dibs/Duos alone mention automatic bots.
3. Online immediately calls `Menu.OpenWordbound()`. It does not select the legacy blocked `matchmaking` queue or display local mode cards under an online heading.
4. Create: **Play & learn / Creative workshop**. Keep tutorial room and workshop supported-house routing. Wordbound map authoring remains reachable from the Wordbound launcher; do not relabel home decoration as full Wordbound map authoring.
5. The legacy `Matchmaking` internal ID / blocked guard stays for saved-state compatibility. A stale blocked state offers the real Online entry and a valid Local choice; it must not silently run the house mode online.

Mode cards: original icon + existing title + short category tag. Selected objective and participants appear once in the footer, rather than a paragraph on every card. Map cards retain floor-plan, name and factual floor/room count; remove decorative prose. Keep visible GO, keyboard/gamepad selection and focus restoration.

## Concrete source risks / boundaries

- `PlayPage.QueueLine` currently promises bots in every mode; remove that claim.
- Its Matchmaking tab currently repeats house cards and a nonfunctional private-room card. Replace the tab action with the real online front door, while root preserves legacy safety in LobbyMenu.
- Tutorial is not actually restricted to solo: the scene admits 4 and shared progress watches all players. Label “Learn”, not “Solo only”.
- MovingOut config documentation says 2–4 but runtime accepts 1. Preserve runtime and describe actual seats; separately reconcile design/balance before claiming one-player balance acceptance.
- `LobbyPages` Career empty-state promises bots in every practice match; root owns shared-copy correction.
- No networking protocol, bot policy, team assignment, map generation, saved scene/profile, shop, camera, font or asset changes belong in this slice.

## Verification required after integration

- Focused Play tests: Local/Online/Create labels; correct mode sets; Online loads WordboundLobby directly without starting/changing local mode/map/party; stale Matchmaking remains blocked; selected summary updates on mode/map/party changes; bots only Dibs/Duos; keyboard/controller focus persists; no explanatory paragraph per card.
- Preserve/adapt existing `Assets/_Project/Tests/LobbyTests.cs`: `PlayIsTheWebsTwoPickersOnOnePage`, `TheWorkshopOnlyOffersHousesItCanBuildIn`, `PlayPicksAModeAndMapThenGoStartsTheMatch`, `MatchmakingSaysWhyItCannotStartYet`. Keep ray/floor-plan/no-ellipsis/route assertions; move tutorial selection to Create and exercise stale queue programmatically.
- Retain `Assets/_Project/Tests/ModeRuntimeTests.cs` Dibs/Duos bot/team tests and original house objectives unchanged.
- Run focused PlayMode UI tests after source/core gates; capture native Home/Local/Create/Online at the current window plus minimum supported view, check card/footer clipping and controller focus. This audit has not run or visually accepted those future screens.
- Broader online acceptance remains separate: public/private full matches, capacity, recovery and original house networking are not proven by this menu change.
