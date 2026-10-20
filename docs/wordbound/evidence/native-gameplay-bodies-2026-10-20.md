# Native gameplay body setup

STATUS: PASS for prefab creation and registration; playable physics acceptance is pending.

- Unity 6000.6.3f1, Fusion 2.1.3, Physics Addon 2.1.2.
- Source base: `99bed27`, plus `WordboundGameplaySetup` and its editor assembly references.
- Command: `node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundGameplaySetup.CreateBodyPrefabs --json`.
- Gate: R3, cold, PASS, exit 0, 229.1 seconds.
- Log: `Logs/gate_exec_CreateBodyPrefabs_20261020T133956Z.log`.
- Receipt: `Logs/exec/20261020T133956Z.json`.

Unity created and baked Player, Item and Platform prefabs under `Assets/_Project/Prefabs/Wordbound/Network`. The helper checks native Fusion weaving, a single root rigidbody and collider, unit root scale, a child interpolation target, no competing NetworkTransform, and valid native prefab table IDs.

The first attempt failed because baking requires the asset GUID. The helper now saves the new prefab through Unity, loads its asset contents, then bakes and saves it. No prefab YAML or asset metadata was authored manually.

This result proves native prefab creation and registration. Movement, collision, held items, jumping and migration still require live gameplay checks.

## Presentation and puzzle checks

`TestResults/editmode_20261020T134553Z.xml`: FAIL, 26/29 passed, 110.1 seconds. All nine physical puzzle policy tests passed; 17/20 presentation tests passed.

The SHIELD, SOFA and CLOCK visual floor-alignment checks failed at `WordboundPresentationTests.cs:42`, measuring 0.0277424678, 0.0148500502 and 0.0279000103 metres against a 0.002-metre limit. The presentation owner is investigating the bounds calculation. This failed run is not visual acceptance.
