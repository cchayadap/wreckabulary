# Gameplay artwork integration

The source requires Gameplay composition `9d69fa1`, the existing `WordboundNetworkBody`, and integration's four-argument opt-in `ModelVisual.Spawn`. Import the existing ModelLibrary, wardrobe data and Wordbound equipment collection first. No shared asset, wardrobe, catalogue or player controller is modified.

Attach one `WordboundGameplayPresentation` to each persistent gameplay bootstrap. Do not share a presenter between controllers. Set up before starting the session:

```csharp
var presentation = bootstrap.AddComponent<WordboundGameplayPresentation>();
presentation.Initialize(new WordboundVisualResolver(itemCatalogue, useWordboundCollection: true));
options.ConfigureVisual = presentation.ConfigureVisual;
controller.Initialize(online, mode, course, geometry, itemCatalogue, rules, playerId, prefabs, options);
presentation.Bind(controller);
// Then construct/attach the session with controller.Authority and start it.
```

The bootstrap must surface `presentation.Failed` / `presentation.Fault` and stop the session on that terminal presentation error. The component hides its owned models on error; it never advances or mutates the match to recover missing artwork. `Clear()` releases artwork/animation graphs and resets the match binding; normally destroy the persistent presenter with its controller after Stop. `ConfigureVisual`, `ApplyFrame` and `Present(deltaTime)` are public for integration probes. Normal runtime presentation uses LateUpdate.

Body prefab `NetworkRigidbody.InterpolationTarget` must be an initially empty, unit-scale `VisualRoot` child under the body, with no colliders or rigidbodies. The resolver never moves/scales the authoritative root, its colliders or the interpolation target. If that target is not assigned yet, the component waits and retries; it does not substitute a primitive.

## Artwork and size

Exact item IDs resolve through the real ItemCatalogue.Model key. Course AssetIds which differ from item IDs require explicit `courseModels` mappings passed into `WordboundVisualResolver`; it never guesses a model from letters. Default non-item mappings are authored `Avatar/Avatar`, `Letters/Tile_A`–`Tile_Z`, `Environment/Platform_2m`, and `Items/CHEST` for a keepsake. The keepsake and platform mappings can be explicitly overridden without editing catalogues.

Classic skin opts into the existing Wordbound collection through ModelVisual; Candy/Arcade and uncovered models keep the existing fallback behavior. Wordbound fitting uses the importer-verified true size/ground/grip metadata, avoiding rotated child mesh AABB overestimation. Other models fit their transformed mesh bounds. Non-player artwork fills ShapeSize with its floor at Y=0 and centered X/Z. Avatar height fits 1.8 m uniformly, preserving authored body proportions; the existing capsule remains .6 × 1.8 × .6 m. Extended arms and garments are cosmetic and can extend beyond that capsule.

The source model's imported child transforms remain intact beneath orientation and fit wrappers. A letter receives the existing -90° X presentation rotation before fitting. Artwork containing a Collider, Rigidbody, Collider2D or Rigidbody2D is rejected before activation; the resolver does not add physics, primitive substitutes, lights or cameras.

## Appearance, animation and carried objects

Player outfits come from the verified frame's MatchActorState.Appearance, deserialized and sanitized with the existing wardrobe API. An empty appearance uses the existing unsaved default presentation outfit. This never reads or writes a remote player's local preference slot. Parent bootstrap remains responsible for submitting the user's chosen outfit through the authoritative appearance contract; this component does not add an unvalidated network appearance input.

`PlayerAppearance.Dress` keeps its material-slot tint behavior, including boots/glasses/satchel. Unselected clothing additionally uses renderer.forceRenderingOff so GameplayBody's active-state renderer refresh cannot reveal it. The imported Generic clips provide idle/walk/run and a masked one-hand/two-hand hold layer. Their manually evaluated graph follows the interpolated root and disables root motion; it does not drive movement, jump decisions, attack timing or physics.

Inactive inventory entities retain their hidden canonical world model. A second artwork-only instance follows the same runner's registered carrier avatar. Held items use authored HeldScale; Carried furniture stays full size. The authored Grip_R aligns to grip_R, and a keepsake uses grip_L to avoid occupying the same hand as an equipped item. Outfit item skin is resolved for this held copy. No body collider is reparented. Missing/inactive carriers hide held copies; dropped bodies show the world model; despawn or runner replacement releases the old graphs and copies. Root input/physics continues to own gameplay.

There is no limb IK or swing/attack/jump reaction layer in this slice. Existing hold clips and grip placement need native visual acceptance, especially full-size furniture and simultaneous keepsake/weapon carrying. Thrown/dropped artwork follows canonical full body ShapeSize and Classic world skin; this component does not invent a distinct thrown shape or persist a thrower's skin absent a replicated presentation contract.

## Verification

Run the local source check with:

```text
dotnet build BuildNotes/gameplay-presentation/ReferenceCheck.csproj --no-restore --nologo -v minimal
```

This references the installed Unity 6000.6.3f1 and integration's real assembled Gameplay/Physics/Fusion/core/art APIs. It compiles the runtime and native tests; it does not execute engine calls.

Native test class: `Wreckabulary.Tests.WordboundPresentationTests`, new Editor-only modern TestRunner assembly. Twenty cases cover all 12 models using actual imported mesh vertices, material remaps, no physics children, feet origin, nonuniform course dimensions, hand alignment and held/carried scale, A/Z letter orientation, platform/keepsake mapping, wardrobe hiding despite a renderer refresh, authored clips/sockets, and unknown-key refusal.

Native execution and screenshots are NOT RUN in this lane. Root's remaining gate: run those tests after import; inspect actual host and client views while carrying/dropping/throwing and changing active state; verify both signed avatars retain their outfits; verify recovery recreates fresh held copies without old-runner leakage; exercise teardown and session fault handling. Compare gameplay colliders before/after artwork to confirm the renderer layer cannot change authoritative physics.
