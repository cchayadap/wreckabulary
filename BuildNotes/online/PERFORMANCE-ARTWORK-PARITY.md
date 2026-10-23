# Performance workload artwork parity

STATUS: SOURCE COMPLETE; independent bounded source review PASS by recover_input. Compile, native and presented performance execution NOT RUN in this lane. Source scope is one read-only presentation getter, the existing QA workload/driver, and this report.

Confirmed defect: the workload constructed a null-profile resolver, silently selecting the optional equipment collection despite the original-artwork profile default. It also required DetailV2 textures and reported an unconditional detailed-material success. The correction must preserve the synthetic eight actors plus one local world actor, zero remote peers, 1080p Low settings, camera, sample duration and pass limits.

## Correction

`WordboundGameplayPresentation.ArtworkResolver` exposes its initialized resolver read-only. The workload takes that exact instance from the actual local client and rejects missing/faulted presentation or replacement of the resolver during measurement. It does not reconstruct a profile or select a separate equipment collection. Synthetic held items use the resolver's existing skin selection from the synthetic actor's outfit.

At setup, each of BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, FAN and CLOCK is checked against its selected source: the per-item profile override first, then the optional collection when enabled with standard skin, otherwise the original ModelLibrary entry. Actual shared mesh references must match the source. Both MeshFilter and SkinnedMeshRenderer assets are included; triangle counts include only triangle topology. Override material slots are checked against the override/source slot selection. Originals retain their existing skin and TactileMaterials processing, so their actual resulting materials are observed rather than incorrectly compared with untouched imported materials.

The QA notes now record effective base artwork policy, active profile name, and each item's selected source, model key/name, mesh names/vertices/topology/index counts, renderer/material slots, shader, supported color/metallic/smoothness values, relevant material keywords and actual texture names/dimensions. Missing shader properties are recorded as null. Metadata is collected once before sampling; the sampling loop adds only a constant-time resolver identity/fault check. The former unconditional detailed-material claim and fixed DetailV2 texture requirements are removed.

## Preserved measurement and limits

- Eight synthetic animated actors with eight held items, one actual local world actor, one local session and zero remote peers. This remains a synthetic rendering workload, not eight networked or active-combat players.
- Presented Windows release-QA player, 1920×1080 Low, existing render scale and graphics settings, exact flagship seed `0314159265358979`, camera path/FOV and avatar pose loop.
- Existing warmup (at least 3 seconds and 120 frames), 60-second sample, at least 120 measured frames, 16.6 ms p95 limit, camera visibility checks and settings/scene cleanup.
- Original artwork/profile selection may change the measured cost; no timing, image-quality, public matchmaking or multiplayer acceptance is claimed by this source correction.

## Verification and handoff

`git diff --check` passes. Source tracing confirms the local client initializes this presentation with the launch profile and binds the gameplay world before the workload is prepared. The QA assembly already references Presentation; the existing QA JSON writer supports the dictionary/list/scalar metadata. No property-only test was added.

Root follow-up: compile the assembled source; create a successful release-QA Windows build; run the presented scenario on that exact build:

```powershell
node Tools/kit/gate/game_gate.mjs smoke --build <successful-release-qa-build-id> --scenario wordbound_synthetic8_low --seconds 300 --json
```

Inspect `base_artwork_source`, `visual_profile` and all eight `held_item_artwork` records before interpreting timings. Default originals must report the original source unless a specific item has an explicit override. Optional collection and custom override selection must report their actual sources when those profiles are used. A passing default run does not prove unrun alternate profiles meet the performance target.

The lane's `14ab16d` commit is only a baseline copy of root's existing QA/presentation files and must not be integrated. Deliver only the subsequent parity delta. Existing dirty `ModeDefinition.cs` remains untouched.
