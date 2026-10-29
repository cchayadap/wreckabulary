# Final task-scope and developer-guide review

STATUS: COMPLETE — read-only documentation/source review, 29 October 2026.

Observed HEAD: `566e270347225fc152bf4259eeab357f55a99d5e`. This review wrote only evidence in this compatibility folder. No Assets, native jobs, Git state, production documents or public catalogue were changed.

## BELT and impact audio

The requested scope is present and correctly unfinished. `progress.md:18–19` and `progress.md:162–164` agree with `docs/wordbound/PLAN.md:294–313`: detailed editable leather/buckle mesh and rig, hand grip, stable visual articulation, readable wind-up/contact/recovery, bounded authoritative reach/damage/cooldown, held/miss/drop/throw states, skin/camera/remote review and unavailable unfinished catalogue entries.

The audio criteria distinguish a swing, confirmed contact and actual damage/breakage; a belt touching glass does not automatically produce shattering. They cover BELT/BAT/tools against glass, wood, metal, ceramic, stone and soft surfaces; intensity and sample variation; once-only prediction/replication/replay feedback; editable Inspector profiles/surface tags and organized provenance; near/far/occluded listening, volume/mute, glancing/repeated/missed/breaking hits, eight-player mixing and performance. The plan explicitly includes reconnect/recovery deduplication and held/placed/thrown targets. No missing required category was found, and no implementation or test completion is claimed for these unchecked tasks.

## Small document corrections for the integrator

1. `docs/wordbound/PLAN.md:296` still says, “Work is paused at his request after recording this scope.” This conflicts with the explicit resumed-work heading and description at `progress.md:8–10`. Replace that sentence with a historical-pause note or the current pending implementation status; preserve the equipment/audio scope.
2. `docs/wordbound/SCENES-AND-TESTING.md:64` still broadly says Kitchen physics are unfinished. `progress.md:15` now records native030350 passing3/3 for the isolated appliance encounter, with full-map/visual/holding-camera approval still open. Qualify the guide similarly. The older organization checkpoint at `progress.md:31` should also distinguish this later bounded physics result from remaining map traversal and acceptance.
3. The guide's build001001 remains an accurate fallback until the replacement Windows build is actually verified. Update that reference and the try instructions only after the new source-matched build passes; do not turn a pending build into a tested claim.

## Saved scene and mode/map clarity

The saved-scene instructions are practical: separate lobby page prefabs, four exact course-entry commands/scene/course paths, automatic Play Opened Scene setup, return to default Hub, distinct Workshop drafts versus Course assets, and repeat/edit-preservation limits are all stated (`docs/wordbound/SCENES-AND-TESTING.md:7–51`). The guide explicitly warns that local authoring does not admit a map to public matchmaking. This supports James's ordinary Unity edit/save/test workflow.

The eligibility matrix in `REVIEW.md` remains a source-capability reference at its explicitly pinned01694968 baseline; its historical UI-default findings must not be presented as current defects after the Play integration. Keep the shared card design and compatible maps, while preserving these boundaries:

- Original House modes use the installed houses; Escape keeps its Random rule. The house builder supports only Pinwheel/Courtyard. Original House online support remains production work, not an effect of relabelling Play.
- Offline Wordbound TimeRun/Freestyle share Lost Address, Parcel Hall and Parcel Service, with the one-local-seat gate.
- Online built-ins use Lost Address for TimeRun/TeamRace/BrawlRace/Freestyle and Toybox Arena for FreeForAll. Only TeamRace/BrawlRace have the Solo/Duo/Squad race-format variants. TimeRun's canonical Solo format still permits1–4 online humans.
- The installed public catalogue has36 exact mode/format/seed bindings over those two online course identities. A local Parcel Service or Kitchen entry is not public admission, nor is an offline map ID an online seed.

Sources for those boundaries are cited in the detailed `REVIEW.md`: `ModeDefinition`, `WordboundSoloEntry`, `WordboundLauncherSolo`, `WordboundClientContent`, `WordboundPublicCourseCatalog`, `WordboundPartyCoordinator`, `LobbyMenu`, `GameData` and `HomeDesign`. Display renames should preserve canonical save/network IDs. Native UI, actual map imagery and the fresh Windows build remain the root lane's verification responsibility.
