# Wreckabulary production plan

The latest laptop commit is `6030d6b`. Its engine-free rules and authored art are
the foundation. Earlier prototype documentation describing letters as health or
a six-letter limit is superseded: players have 100 HP and an 18-letter loose bag.

## Goal

A warm, tactile house brawler: plush roommates turn realistically shaped household
objects into letter tiles, spell useful gear, and fight or cooperate. Walnut,
ceramic, stitched cloth and rubber should read as different materials. The game
must offer a complete match loop with instructions, feedback, results and retry.

PC keyboard/mouse, controllers and mobile touch controls share the same commands.
Unity is the primary project. A standalone HTML 3D edition uses the same checked-in
catalogue, map and rule JSON; its JavaScript mechanics require separate parity and
browser validation. Raster-generated UI illustrations are described as raster art.
Actual 3D models come from the supplied mesh pipeline.

## Delivery slices

1. Rules and economy: 100 HP, 18 letters, two carried gear slots, two deployed items;
   crafting reserves exact letters and refunds cancellation; consumables spend
   letters; reusable items release their letters once when destroyed.
2. Twelve authored recipes: BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE,
   SOAP, SOFA and TABLE. Their catalogue timings, ranges, durability and effects
   drive both editions. Other modelled items furnish the house.
3. Maps: connected Pinwheel House and an enlarged courtyard house, with readable
   routes, distinct room dressing, clear spawn space and clear-out warnings.
4. Modes: Dibs, Duos, Moving Day and Moving Out. Each has an objective, solo play
   with bots where required, an end state, and replay or return to the house.
5. Presentation: imported avatar and furniture, selectable cosmetic wardrobe and
   item colourways, restrained impact/skill feedback, audible cues, readable HUD
   and touch joystick/action buttons that fit a phone landscape screen.
6. Validation: engine-free rules, regression tests, actual browser interaction,
   Unity compilation/EditMode/PlayMode, representative captures and platform
   builds. Device performance requires real-device testing before a release claim.

## Acceptance and evidence

| Area | Required observable result |
| --- | --- |
| Crafting | Exact repeated letters consumed; cancellation conserves letters; no bag overflow or disabled recipe |
| Combat | One hit per target; allies follow friendly-fire rules; cooldown and revival reset correctly |
| Gear | Switch, use, drop and deploy both slots; correct lifespan and one-time refunds |
| Modes | Solo can start; objective progresses; win/loss/tie terminates; retry resets transient state |
| Maps | Doors traversable; spawns free; shrinking danger telegraphed; camera follows play |
| Art | Supplied meshes appear in play; wardrobe hides unused modules; import rotations retained |
| Controls | Keyboard/mouse, gamepad and simultaneous touch movement/aim/action supported |
| HTML | Local bundled dependencies; same JSON sources; functional browser checks and no console errors |
| Unity | Exact 6000.6.3f1 editor; real tests and builds recorded separately from source inspection |
| Authorship | Production branch authored as SethyPagna with no coauthor trailers |

`docs/progress/WRECKABULARY.md` contains the current evidence and the next action.
Online matchmaking and a commercial release require additional service/device
work; they must not be inferred from local multiplayer or rules-only room codes.
