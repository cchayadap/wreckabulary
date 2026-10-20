# Offline composition contract

STATUS: SOURCE VERIFIED — 15 TESTS PASS, SDK REFERENCE PASS; NATIVE EXECUTION NOT RUN

Root composes the selected workshop draft before entering play. Read the current immutable `WorkshopSnapshot`, the user's explicit mode selection and the draft seed; resolve that exact document with `MatchStateValidation.Requirements(mode)`. On any validation error, keep the editor and show its errors. Do not substitute a proof template, change the seed, add a player or relabel a resolved competitive manifest.

Supported initial modes are `ModeDefinition.For("TimeRun")` and `ModeDefinition.For("Freestyle")`. A TeamRace/BrawlRace document can use TimeRun practice only after that profile is explicitly selected and the unchanged draft validates. An Arena document that lacks Freestyle/Explore support remains unsupported locally. Any addition of that capability/profile must be a visible, undoable authoring change before resolution; the runtime does not perform it. This is one-human practice, with no match against simulated human opponents.

Frozen composition API:

```csharp
MatchBinding binding = WordboundLocalSession.CreateBinding(
    mode, exactResolvedCourse, exactContentCatalogueHash, localActorId);

controller.InitializeLocal(
    binding, mode, exactResolvedCourse, geometry, items, rules,
    localActorId, nativeBodyPrefabs, gameplayOptions);

var session = new WordboundLocalSession(
    createFreshRunner, multiplePeerConfiguration, controller.Authority, localActorId);

controller.AttachSession(session);
await session.StartLocal(cancellationToken);
```

`CreateBinding` creates one seat at team 0/slot 0 and a fresh local match ID. Its protocol is `wordbound-local-v2`, distinct from an online match. `catalogueHash` must represent the exact geometry, items, effective rules and authored physical-site profile used by the composition. Root owns selecting/constructing that content bundle. `InitializeLocal` takes the exact same parameters as above; the final `WordboundGameplayOptions` argument is optional. `GameplayScene` must be a registered scene, and AttachSession configures it before startup. The runner factory adds the real `NetworkRunner` and `NetworkSceneManagerDefault`. Copy registered body prefab sources into the isolated configuration and select the actual 60 Hz simulation.

The authority factory is:

```csharp
WordboundFusionAuthority.CreateLocal(
    AuthoritativeMatch match, ResolvedCourse resolvedCourse,
    ItemCatalogue itemCatalogue, GameRules gameRules, string localPlayerId)
```

It accepts only the local protocol and one real seat in the supported mode. It validates the initial complete canonical snapshot against the supplied content. The normal online constructor still requires a verified signed match.

`IWordboundGameplaySession` exposes Runner, RunnerStarted, GameplayReady, Failure and Committed; GameplayStarted/ReplicaReceived/Failed events; ConfigureScene, MarkLocalReady, Poll, SubmitCommand, TryGetAdmittedActor, PublishCheckpoint and Stop. `WordboundFusionSession` implements this interface without changing its existing online behavior. A local session has `CommittedJson` in addition, for its complete memory checkpoint. Startup is complete only after `StartGame(GameMode.Single)` succeeds and the SDK reports exactly one local player. Geometry readiness still comes from the controller after scene isolation and 60 Hz validation.

Use the existing player view, presentation, command HUD and trusted physical-site hooks with the local controller. Return must call `await controller.Stop()` before destroying the play hierarchy and revealing the retained editor. It must not invoke coordinator Return. Stop is idempotent; late startup completion cannot restore readiness.

Local publication exports the whole canonical state, imports it through the strict codec and verifies the exact stamp/hash before acknowledging the simulation's checkpoint window. This is validated memory state, not cloud persistence. Closing the session discards it. Host migration, reconnect, public matchmaking and online certificates have no role in local play.

The new gated native fixture uses `WORDBOUND_LOCAL_GAMEPLAY=1` and category `LocalWordboundGameplay`. It tests TimeRun/Freestyle from a retained workshop draft and seed, real isolated native bodies, movement, jump/landing, a command receipt, complete checkpoint import, no Photon cloud readiness and cleanup. It does not assert craft/socket completion or network fault injection. Those require the authored-site interaction proof after that lane lands.
