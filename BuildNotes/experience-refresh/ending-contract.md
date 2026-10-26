# Result-aware epilogue contract

STATUS: COMPLETE — read-only source/design audit, 2026-10-26 UTC, inspected integration `1a74d885`. No runtime/profile edits, Unity jobs, metadata, tests or commits.

**Smallest slice:** a pure presentation resolver plus optional copy in the existing story profile, shown inside the existing menu/journal after confirmation. Keep the canonical result visible first. No automatic modal, cutscene, reward, new objectives or required reading. Root approval and implementation are still pending; this report does not unfreeze the shared profile contract.

## What existing state proves

- `Assets/_Project/Scripts/Rules/Wordbound/MatchProgress.cs:103,228,405,444,475`: wire outcomes are `Win`, `Draw`, `TimeExpired`. There is **no canonical `TimeRunSuccess` outcome**; use that name only as a presentation ID. A final delivery requires every ordered checkpoint, the correct finish and the keepsake carrier, then sets own-team `Finished`. Same-tick valid finishes are collected before timeout. Race timeout also produces Win/Draw by ordered progress then earliest progress tick. Thus winning does not by itself prove delivery.
- `AuthoritativeMatch.cs:790` adds `story:<team>:<zero-based-lap>` on reaching a lap; `MatchStateValidation.cs:133` bounds those flags to reached laps. `story:team:2` means chapter three was reached, not that repair or restoration was completed. `Online/Presentation/WordboundStoryPlan.cs:68-105` already requires the exact bound course, own roster/team, reached checkpoints and that team's flag to reveal journal entries.
- `Rules/Wordbound/WordboundFlagshipLayout.cs:188-198` omits the main Packing alternative on lap three, making the Repair branch mandatory. Valid flagship final delivery proves the required route was traversed. The proposed four **optional Restoration** tasks have no authoritative completion record. Neither 72 authored sites, all journal notices, completed mandatory puzzles nor the RestoreHome chapter flag can unlock “A place for everything.” That branch remains unavailable.
- `Online/Client/WordboundMatchHud.cs:444` currently shows “Confirming result…”, Draw, “Time's up”, then own winner/“Match complete”. It has no epilogue. `WordboundMatchHudMenu.cs:146-164,199` already has an optional journal and independent Return action. `WordboundStoryView.cs:46` caches only result presence; that signature would miss an unconfirmed → confirmed change at the same tick if reused unchanged for endings.

Paths abbreviated above are under `Assets/_Project/Scripts/`.

## Resolution table

Inputs are the already validated current `MatchSnapshot`, its exact bound course/mode, the local roster actor and own-team progress. Use snapshot elapsed ticks only to distinguish race timeout; never a client wall clock. Earned recap is always `plan.Journal(frame, actor)`, never the winner's journal.

| Confirmed condition | Stable presentation ID / concise fallback |
| --- | --- |
| No result, unconfirmed result, migration/recovery pending, missing actor/team or unsupported course | None; retain normal confirmation/recovery UI. Technical interruption/Return is not a story failure. |
| TimeRun + Win + own team in winners + own team Finished | `TimeRunSuccess`: **Home, at last** / “The keepsake is home.” Require the own-team final chapter flag and earned final rooftop notice before this story claim; missing flags give plain confirmed result. |
| TimeRun + TimeExpired, no winners, own team unfinished | `TimeRunExpired`: **Delivery postponed** / “There is more of the way to find.” Optional recap contains only earned clues. No promise of a saved campaign checkpoint. |
| TeamRace/BrawlRace + own Finished + own team in confirmed winners, whether Win or Draw | `RaceDelivery`: **Home, at last** / “Your keepsake is home.” Apply the same earned-story guard. Show own Win/Draw separately and first. Same-tick finishers each receive their own completion. |
| Team/Brawl, no team finished, canonical elapsed limit reached, own team wins or shares draw | `RaceProgressWin`: **Furthest along** / “Furthest along when time ran out.” Or `RaceProgressDraw`: **Level progress** / “Level progress when time ran out.” Neither claims repaired home or final delivery. |
| Team/Brawl, confirmed result excludes own team, own team unfinished | `RaceDefeat`: **Delivery unfinished** / “Your route is still ahead.” Show own defeat/confirmed match result first; optional detail is only own earned journal/chapter. No rival clue or completed-house artwork. |
| FreeForAll | No Lost Address epilogue; retain score/winner/tie result. |
| Freestyle, including Return or completed practice station | No ending and no epilogue ID. Retain neutral session/practice UI and Return; no timeout/loss/completion invention. |

Apply actual finish before elapsed-limit branches so an exact-limit finish remains success. Reject inconsistent combinations and fall back to the plain result. Limit this first resolver to the existing flagship story with its selected notices and exact `CourseHash`/`ManifestHash` binding; custom/unknown courses must not inherit Lost Address copy. Use existing earned final notice identity `3:L3_Rooftop_GardenGate`, not a count of clues. Ordinary success is complete and positive regardless of falls, deaths, hints, accessibility, disconnects or optional reading.

## Proposed additive API and profile fields

Preserve all existing constructors and Bind signatures. Proposed pure API: `WordboundStoryPlan.Epilogue(MatchSnapshot frame, string actor, ModeDefinition mode)` returns null or a read-only `{ Id, Title, Cue, ResultKey }`; the already existing `Journal` supplies optional detail. Validate mode hash and the same course binding before resolution. No mutation of result, snapshot, progression or rewards.

Add only `WordboundStoryProfile.Epilogues: List<WordboundStoryEpilogueCopy>`. Each entry has `Id` from the six closed IDs above, `Title: WordboundStoryText` (48-character fallback maximum), and `Cue: WordboundStoryText` (96 maximum). Keys use `wordbound.story.epilogue.<Id>.title` and `.cue`, reusing the current key/fallback seam without claiming a localization service. Reject duplicate/unknown IDs, invalid text and more than six entries. Omitted entries use conservative built-in copy; null/empty legacy lists behave as omitted. No editable predicates, winner selectors, restoration IDs or arbitrary script references in this slice.

Existing authored profiles must keep their chapters/notices/copy: `PopulateDefaults` currently refuses nonempty profiles. Do not rerun it on the installed asset to add epilogues. New profiles may populate these defaults; root can separately expose an explicit add-missing-copy operation that never overwrites authored text. The text-only additions do not alter canonical item/rule/course bytes or public binding hashes.

Expose an optional epilogue/journal section in the existing menu when a resolver value exists, even if the journal is empty. Do not auto-open it. Keep Return available with the existing focus and input behavior; one title and one short cue are enough. No Retry/Practice actions until those transitions are genuinely wired.

## Recovery, idempotence and required tests

Resolve from the current confirmed snapshot on bind and `FrameChanged`, not from a locally remembered finish animation. `AuthoritativeMatch.MatchesTickPresenceAndResult` already detects confirmation changes; include Confirmed/Outcome/WinningTeams/Result.Tick plus own progress/flags in any new UI cache. A stable `ResultKey` can use `Binding.Hash + ownTeam + Result.Tick + Outcome + sorted WinningTeams`; **exclude epoch and wall-clock time**, so host migration does not invent another ending. No auto-open/one-shot reward means rebuilding the view is harmless. Any local dismissed/open state is scoped to that key, never authoritative and never blocks another player.

Before delivery, root should request:

1. Truth-table tests from real progress snapshots: TimeRun success/timeout, exact-limit finish priority, race same-tick draw with both own deliveries, race progress win/draw with no delivery, losing team with only early clues, FFA and Freestyle no ending.
2. Guard tests: mismatched binding/mode, unknown actor, missing own final flag, rival flags only, future flags/unreached checkpoint, and unsupported custom course cannot reveal a homecoming. Reaching chapter three, completing the mandatory Repair branch or all 72 sites never selects Restoration.
3. Confirmation/recovery tests: null → provisional → confirmed at the **same tick**, duplicate delivery, snapshot codec round-trip, reconnect and epoch change reproduce the same ID/key/text; unconfirmed/recovery views show no ending. Resolver leaves snapshot bytes unchanged.
4. Profile tests: old/empty profile fallback, edited text/key resolution, duplicate/unknown IDs and bounds rejected; native save/reload and explicit add-missing defaults preserve existing authored copy.
5. Native UI check: Return and controller focus remain available, optional detail stays closed until selected, long/localized text fits, early defeat leaks no rival story, and a rebind does not force the panel open.

Existing starting points: `Online/Presentation/Tests/WordboundStoryPlanTests.cs`, `WordboundStoryProfileTests.cs`, `Online/Authoring/Tests/WordboundStoryProfilePersistenceTests.cs`, plus the canonical result/snapshot fixtures. All proposed cases above remain NOT-RUN. The broader design's optional-restoration authority/versioning work is a later contract, not a prerequisite for this honest ordinary epilogue.
