# Wreckabulary: Wordbound build specification

The owner approved [the implementation plan](wordbound/PLAN.md) and requested implementation on 2026-10-20. That document is the specification, including exact mode rules, scope, ownership, interfaces, milestones and acceptance. Do not substitute the kit's generic milestone examples for those requirements.

James and SethyPagna are the same owner. Preserve ICY's Escape and wardrobe direction, the existing lobby/UI ownership, and all original checkout changes. New work uses isolated `sethypagna/*` branches.

Track observed progress in [STATUS](wordbound/STATUS.md). A passing source test does not establish online support, working assets, a player build, performance, or human usability acceptance.

## Delivery

- Five modes: Time Run, Team Race, Brawl Race, Free-for-All, Freestyle.
- A four-destination, four-layer course with three changing laps and controlled deterministic randomness.
- Offline workshop authoring, three templates, safe JSON sharing and unchanged old home saves.
- Private/public online parties, Solo/Duo/Squad, human Fill, reconnect and automatic host recovery.
- Twelve generated item concepts, editable Blender models and validated Unity integration.
- Verified Windows build and documented limitations, with no premature release claim.

## Verification

Use the approved plan's complete acceptance list. Unity checks go through `Tools/kit/gate/game_gate.mjs`. Existing lightweight RulesHarness and Web tests remain useful independent regression checks. Only the integrator runs Unity. No milestone is accepted from code existence alone.

## Authorization and outstanding human input

Implementation, isolated worktrees and commits after green steps are already authorized. Built-in image generation followed by local Blender work is authorized; external paid generation is not required. Photon account access remains unavailable. Do not invent account credentials, test results, owner acceptance, or external deployment evidence.
