# Workshop viewport render check

STATUS: PASS for native viewport capture. Full editor interaction and usability acceptance are pending.

Command: `node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundWorkshopCapture.Run --json`.

The native R3 gate passed in 171.9 seconds at revision `b68c3e6`. Log: `Logs/gate_exec_Run_20261020T135327Z.log`. Receipt: `Logs/exec/20261020T135327Z.json`.

All three 1280×720 images were opened and inspected. Race Circuit shows its selected wooden ground floor, ghosted upper platforms and vertical links. Combat Arena and Freestyle World show their wooden ground surfaces and sparse starter props. They are small starter layouts, not the planned large flagship world.

These are viewport-only URP captures, with the Ground layer selected. Palette, properties, drag controls, menus and controller focus are absent from these images and are not verified by this run. No visual or usability owner acceptance is claimed.

| Template | Image | SHA-256 |
| --- | --- | --- |
| Race Circuit | [race-circuit.png](race-circuit.png) | `47377a3b82965b7c48e05e7469a3b94e82122c5324209c33c6737ef12f719da5` |
| Combat Arena | [combat-arena.png](combat-arena.png) | `0d3aaf4b470ef1d92c4f79b0260ce1cd626e73b145ebfb7dd7e6bc0208844663` |
| Freestyle World | [freestyle-world.png](freestyle-world.png) | `b4d6b20898110972333531117c4315a120db0e36e98938bc98874c465003e7d4` |

The adjacent JSON files preserve original capture metadata, including manifest/source hashes, seed, camera and original `Logs/wordbound/workshop-*.png` filenames. The images here are byte-for-byte copies.
