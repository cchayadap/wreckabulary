# Play categories, modes and map eligibility

STATUS: COMPLETE for read-only source and installed-catalogue inspection. No gameplay, public service or native test result is claimed.

Baseline: `016949682e7b031da9e72d06c089b222bbaa9cfb`. This lane wrote only this report, progress and freeze evidence. The owner's desired direction is a centered category → mode → consistent map gallery → clear action, using a modern neutral interface and vivid original artwork.

## Safe interpretation of uniform maps

Use the same map-card design, dimensions, selection treatment and readable names across categories/modes. Reuse the actual same map where current rules and content admission support it. Filter or disable incompatible content with a short reason. The current content does **not** support one identical map list across every mode: circuits, arenas, original house modes and the home builder have different schemas/capabilities. Changing a label cannot make an Arena into a Circuit or grant public admission to a local course.

In particular, local map IDs `LostAddress`, `ParcelHall` and `ParcelService` are not interchangeable with the online public identity `wordbound-lost-address` or with an online seed. The same Lost Address title/image can be presented consistently while its exact online content binding stays unchanged.

## Current source-supported matrix

| Category / flow | Canonical mode(s) | Eligible map/content now | Players and meaning |
| --- | --- | --- | --- |
| Practice, House | `Dibs`, `Duos`, `MovingOut`, `MovingDay` | All installed `GameConfig.Houses`: `pinwheel`, `flat`, `terrace`, `courtyard`, `walkup` | Up to4 local seats. Dibs/Duos UI explicitly reports human/bot composition; other modes retain their existing rules. These are local house modes. |
| Practice, House Escape | `Escape` | A single `random` choice resolving one installed house at run time | Preserve ICY's randomized escape path. `LobbyMenu.Choose` forces random and returns to a real house when leaving Escape. |
| Practice, Wordbound | `TimeRun`, `Freestyle` | `LostAddress`, `ParcelHall`, `ParcelService` | Exactly1 local seat is admitted by `OpenWordboundSolo`; no local couch team or bots are added by this path. Both modes use the same three-map list. |
| Create / tutorial | `Tutorial` | Dedicated training room, not a normal house gallery selection | Existing tutorial scene/path. |
| Create / home builder | `Workshop` | Only `pinwheel` and `courtyard` | `HomeDesigner.Supports` restricts the builder; other house cards are currently disabled. |
| Create / Wordbound | Authored draft's selected mode | Assigned course, flagship, Parcel Service and proof templates via existing Workshop sources | Local exact playtest; a draft must validate for its mode/capability/seed. It is not automatically an admitted public map. |
| Online public, Time Run | `TimeRun` / canonical format `Solo` | `wordbound-lost-address`, one of4 approved exact seeds |1–4 human co-op players. The `Solo` format value here does **not** mean maximum1 player. |
| Online public, Team Race | `TeamRace` / `Solo`, `Duo`, `Squad` | `wordbound-lost-address`, same4 approved seeds |2 teams with1/2/4 humans per team; at least1 human on each team. Enemy damage off. |
| Online public, Brawl Race | `BrawlRace` / `Solo`, `Duo`, `Squad` | `wordbound-lost-address`, same4 approved seeds |2 teams with1/2/4 humans per team; enemy damage on, progression protected. |
| Online public, Free-for-All | `FreeForAll` / `Solo` | `arena-proof` / title `Toybox Arena`, same4 approved seeds |2–8 humans, individual teams, knockout objective, no Fill. |
| Online public, Freestyle | `Freestyle` / `Solo` | `wordbound-lost-address`, same4 approved seeds |1–8 human co-op players, no timer/hazard damage. |
| Online private built-in seed | Same five Wordbound modes/formats | Lost Address for race/explore; Toybox Arena for FFA | A private seed controls layout, not an offline map ID. Private co-op can start solo. |
| Online private authored map | Exact validated Workshop mode/format/seed | Only this release's installed proof or flagship geometry and matching installed items/rules | Content bundle/descriptor binds the selection. Custom assets and altered item/rule files are not transferred. |
| Kitchen pilot | `wordbound-lost-address-kitchen-v1` saved local full-course variant | Separate local authoring scene/course with augmented furniture catalogue | Local only in current admission. Public catalogue and online installed-geometry resolver do not admit it. |

The installed public catalogue has **36 exact bindings**, not36 distinct maps:9 mode/format variants ×4 seeds. The seeds are `0000000000000001`, `0000000000000002`, `0000000000000003`, `0123456789abcdef`.32 bindings use Lost Address and4 use the arena. This is installed metadata inspection, not proof that every full match has passed gameplay/recovery acceptance.

## Important selection and launch findings

1. **Default public co-op can fail after Find match.** PartyMenu starts at mode index0 (`TimeRun`) with `fill=false`. `WordboundPartyCoordinator.Queue` rejects QuickPlay for TimeRun/Freestyle when the party has fewer than2 humans and Fill is off. Fill is currently inside collapsed match settings. Make the human-Fill choice visible for new public co-op selection, or disable the primary action with a short reason until its condition is valid. Preserve existing-party settings; do not silently rewrite a joined party or add bots. Private rooms remain the solo option.
2. **Canonical format semantics differ.** Only TeamRace/BrawlRace accept Duo/Squad. In those modes, display “1 /2 /4 per team” or an equally clear Solo/Duo/Squad row under a per-team label. TimeRun/Freestyle canonical `Solo` does not prohibit co-op. Hide/reset irrelevant format selection and force FFA Fill off as existing code does.
3. **Keep exact map identity checks.** Before public Create/Find, retain `Choices(mode,format)`, `EnsurePublicMapSelection`, `Find` and `ResolveSelection`/registry checks. The cache is keyed by mode+format and resets an unavailable seed to a current eligible choice. A card selection must carry its own mode/format/seed tuple; never launch the stale selection from another mode.
4. **Use correct names for online layouts.** Existing UI labels all approved seed choices “Layout1…4”. These are variants of Lost Address or Toybox Arena, not the three local Solo maps. A shared gallery should show the real course name plus a compact variation/seed indication or shuffle control. Do not show Parcel Service imagery as if it were the FFA arena.
5. **Private selection is a distinct action.** Current “Public maps / Private seed” controls public eligibility, while “Create / Join” describes party setup. A top-level Matchmaking/Private Room category can reorganize these existing operations; it must keep private seed/custom content out of QuickPlay and require explicit custom-map acceptance for Custom Fill.
6. **House online support is still absent.** `LobbyMenu.Blocked` stops house-mode matchmaking; `PlayPage.PickQueue` immediately transitions to Wordbound instead of setting house queue matchmaking. A unified screen must not list House modes as launchable online without implementing the missing network path. Label category controls Practice (Offline), Matchmaking, Create (or Workshop), and retain the available capabilities.
7. **Mode state has two stores.** House selection lives in `LobbyMenu.Mode/Map`; offline Wordbound lives in `WordboundSoloEntry.SelectedMode/SelectedMap` plus PlayPage's family flag. Passing `TimeRun` to `LobbyMenu.Choose` would be reset to Dibs. If the extra family row is visually removed, new mode buttons must still route to the correct existing state/request store rather than treating all IDs as house modes.
8. **Escape and builder map restrictions must survive a common gallery.** Escape forces Random. Home builder admits two houses. A consistent visual gallery may retain disabled choices/reasons, but it must not let a stale map become active after mode/category switching.
9. **Offline Wordbound extra-seat guard must remain.** The Solo primary action is disabled for more than1 local seat, and the launcher independently checks it. Keep that guard; online party size is a different system. Tutorial/house Practice labels should explain local humans/bots when relevant instead of implying network teams.
10. **Creation/import is not public admission.** `WordboundClientContent.ResolveInstalledGeometry` only accepts proof/flagship catalogues. `CreateAuthoredParty` also binds exact installed item/rule JSON. Parcel Service's separate geometry and Kitchen's augmented catalogue cannot be made online-ready by adding a card. Keep those local until an explicit compatibility/admission implementation and verification.

## Display-name recommendation

Do not change canonical IDs, protocol fields, hashes, saves or known QA object IDs for a wording pass. A presentation-only label helper is appropriate.

| Canonical ID | Suggested concise label | Why |
| --- | --- | --- |
| `Dibs` | Dibs! | Existing recognizable title. |
| `Duos` | Team Battle | Makes the2v2 intent clearer than “Double trouble”. |
| `MovingOut` | Moving Out | Avoids confusion with the separate Escape mode. |
| `MovingDay` | Moving Day | Existing meaning. |
| `Escape` | Escape | Distinct word/key escape direction. |
| `TimeRun` | Time Trial | Clear timer objective; use Co-op qualifier online where needed. |
| `TeamRace` | Team Race | Already describes the objective. |
| `BrawlRace` | Brawl Race | Keeps race/combat distinction. |
| `FreeForAll` | Free-for-All | Established readable mode name. |
| `Freestyle` | Freeplay | Plain-language explore/craft mode. |
| `Tutorial` | Tutorial | Clear introduction. |
| `Workshop` | Home Builder | Distinguishes the original furnishing builder from Wordbound map authoring. |

These are recommendations, not owner-approved new mechanics or a requirement to change every title. For a centered mode row, short labels plus one selected-mode subtitle avoid large poster text blocks.

## Minimum integration checks

- Practice House→Escape→another House preserves Random semantics and returns to an eligible map.
- House→Wordbound Time Trial/Freeplay→House routes the correct state store; preserves map selection where still eligible and prevents extra-seat Solo launch.
- Matchmaking mode/format changes update eligibility and primary-action state; empty public catalogue, mismatched release and stale seed fail safely.
- Solo public co-op with Fill off shows its reason before Queue; enabling human Fill permits the intended request. FFA never sends Fill=true.
- Private seed stays private; guest/party/invite/room flows preserve exact canonical mode+format+seed and region. Changing an authored party's mode still requires leaving/revalidating rather than silently reusing a bound preview.
- Shared map imagery/name/presentation never changes effective content identity. Offline Parcel/Kitchen remain excluded from public choices.
- Keyboard/controller selection remains visible after refresh; any intentional display-label changes update relevant game-owned test/probe assertions while keeping launch/focus/state checks. Current tests explicitly assert old Local/Online/Create text and legacy family button names.

## Source references inspected

- `Assets/_Project/Scripts/UI/Lobby/PlayPage.cs`: queue definitions23–30, immediate Matchmaking transition211–221, local families and Solo modes/maps58–176, Escape-only Random109–119, house builder eligibility371–379.
- `Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs`: queue/mode IDs22–27, online/solo/workshop requests93–125, Choose735–760, local-only Blocked765–767, current display names816–826, local seat/bot wording829–848.
- `Assets/_Project/Scripts/Core/WordboundSoloEntry.cs`: mode/map IDs and validation7–27; `Online/Client/WordboundLauncherSolo.cs`: exact offline map factory15–46 and one-source launch.
- `Assets/_Project/Scripts/Core/GameData.cs`: installed map loading33–42 and Houses default/additional assembly73–80; `Resources/GameData.asset` referenced map GUIDs resolve to pinwheel/flat/terrace/courtyard/walkup JSON metadata.
- `Assets/_Project/Scripts/Rules/HomeDesign.cs:126`: builder supports only pinwheel/courtyard.
- `Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs`: definitions79–98, format validation75–77, roster checks102–122; `MatchStateValidation.cs:191–196`: Circuit/Arena/Explore and ownership requirements.
- `Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipLayout.cs:39–40`: flagship supports TimeRun/TeamRace/BrawlRace/Freestyle; `CourseTemplates.cs`: Toybox Arena/arena-proof and FreeForAll-only definition.
- `Assets/_Project/Scripts/Rules/Wordbound/WordboundParcelServiceContent.cs`: separate three-module geometry and TimeRun/Freestyle support; `WordboundKitchenPilotContent.cs`: separately versioned full-course/local catalogue.
- `Assets/_Project/Scripts/Online/Client/WordboundClientContent.cs:25–59`: online built-in factories;92–99 installed online authored geometry restriction. `WordboundPartyCoordinator.cs:222–228`,250–258,268–280: exact item/rule binding, authored setting restrictions, public admission and human Fill requirement.
- `Assets/_Project/Scripts/Online/Client/WordboundPartyMenu.cs`: modes/formats/default fields13–30; settings284–355; `WordboundPartyMenuPresentation.cs`: mode reset/selection and composition. `WordboundPublicCourseCatalog.cs`: exact selection key/filter/resolve; `Resources/Wordbound/public-catalogue.json`:36 bindings,9 mode/format variants,4 seeds and2 course IDs.
- `Assets/_Project/Tests/LobbyPlayChoiceTests.cs`: existing family object IDs, extra-seat/focus checks and Local/Online/Create text expectations418–420. `Online/Client/Tests/Input/WordboundPartyMenuInputTests.cs` and `Tests/WordboundPublicCourseCatalogTests.cs`: existing selected-settings/public-map test surface.

No source changes or native tests were run. Parent/UI lane received the early matrix and default-Fill hazard while drafting.
