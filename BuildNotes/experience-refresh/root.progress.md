# Experience refresh — integration

STATUS: IN PROGRESS. Owner continuation on 2026-10-26: minimal, immersive player UI; richer original-style designs, skins and animation; clearer shops/loadouts; stronger gameplay/story rubric and introduction through play.

## Preserved baseline

Integration starts at `9fce9f25`. Windows review build `20261024T034544Z` remains untouched. Workshop 5/5 and original-equipment 5/5 pass; all five modes have bounded two-client public entry receipts. Maximum-roster run `041032` failed all three ID assertions, with clean service teardown. The actual receipt was recovered before changes. These results do not establish production acceptance.

All seven worktrees were inspected. Recovery snapshot `.harness/recovery/2026-10-26T01-32-06-562Z-a6c221ec-9808-4369-927c-7451a245c2b8` verifies 632 dirty/untracked/authored files. Original font/Lunar work and unrelated online files remain unchanged. No heavy job active at recovery.

## First implementation slices

- [~] Root: shop browsing by visual category, concise selected-item detail and preview, then loadout consistency. Preserve existing coins, ownership, recipe-specific finishes, keyboard/controller focus and return behavior.
- [~] Art lane: inspect actual original assets and captures; propose a coherent first collection and motion/skin implementation contract before changing assets.
- [~] Story lane: evidence-based multidimensional gameplay rubric, then validated Unity narrative data and concise contextual introduction. Preserve clue identities, team gating and existing outcomes.
- [~] Online lane: diagnose maximum-roster identity assertion against installed SDK and production binding; preserve required identity and roster checks.
- [ ] Root: integrate reviewed slices, run appropriate gates serially, capture actual UI and motion, inspect combined behavior, update Windows review build.

The shop currently puts themes, collections, accessories, recipe finishes and colours in one long scroll. Existing live previews, earned-coin checkout, ownership and per-recipe equip APIs will be reused. Initial scope changes presentation and navigation; new real-money commerce is outside this slice. Screens should reveal one choice at a time while retaining accessible labels and optional explanation.

Agent reviews are analysis, not human-playtest scores. Visual approval, sustained performance and full online matches remain separate acceptance requirements. Root alone runs Unity/build/player/core/Blender jobs; no kit changes or new paid generation are part of this initial slice.
