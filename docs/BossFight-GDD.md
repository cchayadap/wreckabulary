# Boss Fight — Phase 1 Design Document (CONFIRMED & FROZEN)

> Project: Wreckabulary · Unity `6000.6.3f1` · URP `17.6.0` · new Input System `1.20.0`
> Authoring date: 2026-11-22 · Frozen: 2026-11-22 · Status: **frozen, playtest amendment v2** (see `docs/design-freeze.json`, version 2)
> Scope: solo player, **one map (`LivingRoom`)**, **one boss (`Calculator`)**. Co-op and extra bosses are deferred batches.
> Requirements in this doc are tagged with IDs (`MECH-…`, `NUM-…`, etc.); the confirmed ones are registered in `docs/design-freeze.json`.

**Confirmed scope decisions (2026-11-22):** map = `LivingRoom` (the existing Dibs/Escape scene, no new arena); boss = `Calculator`; three shield words = `CALC` / `ULA` / `TOR` (sequential split of CALCULATOR — the first draft written as "CACL" was corrected to "CALC" by the user). All other items in this document remain as drafted; the assumptions list at the end records what was accepted vs deferred.

## Playtest amendment v2 — supersedes the original numbers below

After the first playable slice the user reported three problems: no damage reached the boss, the red circles were hard to read and kept interrupting spelling, and a single full-word box per layer was not fun. The following rules are now authoritative; older rows elsewhere in this document are historical draft text.

| ID | Supersedes | New rule |
|---|---|---|
| `AMD-001` | `MECH-009` | The boss has a kinematic `Rigidbody` + box hitbox. `PlayerCombat.Strike()` can now reach `IDamageable`. A hit while shielded shows **SHIELDED!** instead of silently doing nothing. |
| `AMD-002` | `MECH-006`, `NUM-003`, `ART-004` | Telegraphs are a **filled translucent disc + thick pulsing ring**. Circles land **outside** the player's feet (`radius + 0.55 m`), warning is **1.6 s**, at most **2** circles, cooldowns **5.5 / 5.0 / 4.4 / 4.0 s**. A hit costs **1** letter, and **no letters are lost while the player is actively spelling or crafting**. |
| `AMD-003` | `MECH-004`, `LEV-001` | No full-word crates. Each shield word is split into **one smashable per letter**, spread across the clued room with **two decoy caches**. Starter props are spread out too. |

Numbers still awaiting measurement: 240 HP / 7-hit TTK, the new 1.6 s warning, and the letter economy. `@trace AMD-001..003`.

---

## Chapter 0 — Project review: what exists, what is new

Reviewed: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/_Project/Scripts/**`, `Assets/_Project/Data/Config/{rules,items}.json`, `Assets/_Project/Data/word_list.csv`, scenes, tests, docs.

| Capability you need | Already in the project? | Concrete reuse point |
|---|---|---|
| Modes / session routing | **Yes** | `Core/Match.cs:11-22`, `Core/Session.cs:55-90` (scene→mode + mode→scene switches) |
| A self-contained mode state machine | **Yes, ideal template** | `Game/EscapeDirector.cs` — `State` enum `:17`, `Configure()` `:96`, `HookSummoner()` `:207`, `OnSummoned()` `:213`, `Restart()` `:250`, runtime HUD canvas `:1378` |
| Letters from smashing props | **Yes** | `Letters/Smashable.cs:41-103` — `Init(word, health)`, `Break()` bursts tiles via `TilePool.Burst/BurstFrom`; `AnyBroken` event `:30` |
| Word solving / crafting | **Yes** | `Words/WordSolver.cs`, `Rules/Letters.cs` (`LetterBag`), `Player/LetterInventory.cs`, `Player/Summoner.cs:26-30` (`Summoned` event + `WordsOverride`) |
| Rooms + wayfinding | **Yes** | `Rules/Rooms.cs` — `RoomGraph`, `HouseLayout.RoomAt/LettersIn`, 5 authored `house_*.json` |
| Melee / thrown damage on any target | **Yes** | `Player/Damage.cs` `IDamageable`, `Player/PlayerCombat.Strike():217-247` already calls `ApplyDamage` on hit colliders |
| Weapon damage table | **Yes** | `items.json`: BAT 14, BROOM 10, LAMP 16, SPEAR 18, TABLE 22, BLADE 24, HAMMER 28; thrown BALL 12 |
| Hurt / stun / respawn | **Yes** | `Player/PlayerController.cs:281-427`, `Rules/Health.cs`, `HitInfo.Hazard()` in `Player/Damage.cs:41-45` |
| Telegraphed red-circle AoE | **No** | Closest: `Summons/ThrownGear.cs:90-109` (growing ring + beeps) and `Game/ClearOutController.cs:65-123` (hazard damage). New focused component needed. |
| Boss entity / shield / phase HP | **No** | `HealthModel` is player-only (downed/revive/team). New `IDamageable` boss component needed. |
| Boss HUD (name slots, shield layers, boss HP) | **No** | `GameHud.cs:1168-1196` only has generic instruction/checklist/timer. Use `EscapeDirector.BuildNotice()` canvas pattern. |
| Boss-name data | **No** | Add a small CSV/ScriptableObject; keep `word_list.csv` + `items.json` as the shared spell pool. |

**Registration points a new mode must touch** (scattered hard-coded switches — easy to miss one): `Rules/GameRules.cs:154-163` key whitelist + `Apply():167-232`, `Data/Config/rules.json:63-138`, `Core/Session.cs:55-62`, `Core/Match.cs:15-22`, `Game/RoundManager.cs:45-56`, `UI/Lobby/LobbyMenu.cs:25` + `:742-761`, `UI/Lobby/LobbyPages.cs:335-339`, `UI/Lobby/LobbyKit.cs:315-333`, `Game/Typewriter.cs:27-34` + `:50-57`, `Game/GameHud.cs:807-811`, `Game/GameHudCards.cs:492-494`, `Game/PlayerJoinManager.cs:64` + `:83`.

`git status` at review time: clean except untracked `ProjectSettings/Packages/` — no work in progress was disturbed.

---

## The 5 changes that make this "a lot more fun"

These are the deliberate upgrades on top of your rundown. Each is a concrete rule, not a vibe.

1. **Getting hit makes you DROP LETTERS, not just HP.** A red-circle hit scatters 1 random letter from your bag back into the room — but **never while you are actively spelling**. Circles land outside your feet, so standing still and spelling is safe; walking into a circle is the risk. This is the single biggest tension upgrade. (`MECH-006`, amended by `AMD-002`)
2. **The boss name is 3 short words = 3 shield layers, not one long word.** Each cracked word triggers a 0.8 s slow-motion shatter and a staged escalation. You get three satisfying "I did it!" beats instead of one. (`MECH-003`)
3. **A choice every few seconds: save letters or spend them.** Any spare letters can spell existing chaos words — `ZAP` stuns the boss 2 s, `BEES` intercepts one red circle, `MAGNET` vacuums nearby tiles. Those same letters are also the boss name. This is the decision-density engine. (`MECH-005`)
4. **The boss reacts to you.** A grumpy appliance with text bubbles: laughs at a wrong word, complains when you smash its furniture, whiffs when you dodge late. Nothing changes mechanically, but the mode gains a personality. (`MECH-008`)
5. **"Slick Dodge": dodge in the last 0.3 s of a telegraph and you auto-collect the 2 nearest tiles** plus a boss "whiff" reaction. Rewards skill instead of just avoidance. (`MECH-007`)

---

## 1. One-line positioning

**Solo, 4–5 minute silly boss fight: smash furniture into letters, spell a grumpy appliance's three-word name to crack its shield, dodge red stomp circles, then hit it with a weapon you built yourself.**

## 2. Genre & reference points

| Item | Content |
|---|---|
| Genre labels | Local arcade brawler · word-collection action · single-boss arena |
| Reference points (mechanism layer only) | *Escape* (this project's spell-to-unlock loop), classic arena-boss telegraph/dodge fights, party-game "name that object" comedy |
| Differentiation | The word is not a weapon recipe — it is the **boss's own name**, and spelling it is the shield-break key. Letters are simultaneously your ammunition and your health of progress. |
| IP rule | Original appliance characters only. No copied characters, no recoloring of protected IP, no living-artist style imitation. |

## 3. Target player & scenario

- **Age/skill:** 10+ family/couch players; reads short words, comfortable with WASD + one action key. No reading-speed requirement — the spell UI stays open while moving slowly.
- **Scenario:** 4–5 minute pick-up-and-play session on one PC, replayable for best time.
- **Not for:** players wanting deep progression or online competition (out of scope).

## 4. Platform & form factor

| Field | Value |
|---|---|
| Main platform | **PC / Windows only** (solo first) |
| Design resolution | 1920×1080, 16:9, fullscreen/windowed; UI scales with `CanvasScaler` reference 1920×1080 |
| Safe area | ≥ 64 px top / 48 px bottom free of critical HUD (matches existing `GameHud`) |
| Input | Keyboard (WASD move, E grab/attack, Q spell, Space dodge) + gamepad (left stick, A, X, B); reuses `InputBinding.PlayerCommands` — **no new input system** |
| Target framerate | 60 fps target / 30 fps floor, measured in `LivingRoom` scene with 3 active AoE circles + up to 40 loose tiles |
| Memory budget | ≤ 1.5 GB peak for this mode (existing URP house + boss + VFX), to be verified by Profiler |
| Package budget | Reuses current build; boss/UI assets target ≤ 15 MB additional compressed |
| Save location | `PlayerPrefs` for best time / boss wins (no new file path) |
| Network | None. Offline solo only. |
| Store materials | Not applicable for this mode (ships inside existing game). |
| Porting batch (not now) | Couch co-op 2–4: needs team-size rules, bot exclusion like Escape, split HUD. Mobile: separate touch layout + larger telegraph circles. |
| Source note | Platform limits here are project targets, not store policy; store requirements, if ever published, must be checked against official docs at that time. |

## 5. Core loop — three layers

### Micro loop (seconds, ~0.4–1.2 s per action)
`Choose target prop → approach → attack (E) → prop shatters into tiles → walk over tiles to pick up → HUD letter slot lights up → (optional) spend on chaos word`
- Feedback budget: hit flash + splinter burst + sound ≤ 0.2 s after input (`Smashable.cs:94-101` already does this).
- **Churn point:** walking to pick up scattered tiles feels slow. **Countermeasure:** tiles have a 1.6 m auto-magnet pickup radius (`LetterInventory.FixedUpdate` overlap sphere already exists at `:30-43`); `MAGNET` chaos word extends it to 5 m for 6 s.

### Single-run loop (~4 min 20 s target)
| Phase | Duration | Player decision | Churn point → countermeasure |
|---|---|---|---|
| P0 Preparing | 45 s | Which rooms to scout, which weapon recipe to craft | Standing still waiting → timer runs down early when player has crafted 1 weapon *and* visited ≥ 3 rooms (min 25 s) |
| P1 Boss Appear | 12 s | Read the 3 name words + their room clues | Info overload → reveal one word at a time, 4 s apart |
| P2 Item Drop & Preventing | 3 × 35 s = 105 s | Go to clue room vs dodge vs spend letters | Getting lost → minimap ping + room name in HUD; getting hit loses letters → visible "2 letters scattered!" toast |
| P3 Hit the Boss | 75 s | Attack window vs dodge vs last-second chaos word | Boss HP sponge → 240 HP with 1.5× vulnerability multiplier and 24-dmg BLADE = 7 hits |
| P4 Result | 5 s | Retry / next boss | — |

### Long-term loop (hours / across runs)
- Each run grants: best-time record, fewest-knockout record, boss-win count, and one unlocked silly crown/boss variant every 3 wins (stored in `PlayerPrefs`).
- Unlocked bosses change name words and attack patterns, not just colors — replay has a reason.

**Decision density check:** P2 averages one meaningful choice every 3–5 s (dodge / pickup / spell / spend), no 30 s blank window. P0 is intentionally low-density (scouting) but capped at 45 s.

## 6. Core mechanics list

| ID | Mechanic | Effect | Tier | Reuse |
|---|---|---|---|---|
| `MECH-001` | 4-phase run state machine | Preparing → Appear → Drop&Prevent → Hit → Result | P0 | `EscapeDirector` pattern |
| `MECH-002` | Boss name reveal | 3 words shown as empty slots + room clues | P0 | `GameHud` checklist lines |
| `MECH-003` | 3 shield layers | Each spelled word cracks one layer; slow-mo shatter | P0 | New + `Summoner.Summoned` |
| `MECH-004` | Room clue caches | One `Smashable` per **letter** spread across the named room + 2 decoys; boss announces room | P0 | `Smashable.Init`, `HouseLayout` (`AMD-003`) |
| `MECH-005` | Chaos word spending | Spare letters craft existing items; boss-mode effects override some | P0/P1 | `Summoner` + `SummonEffects` |
| `MECH-006` | Red-circle AoE + letter scatter | Filled disc + ring, Warning → Active → Fade; hit = layer damage, 1 letter dropped, **no drop while spelling** | P0 | `ThrownGear` ring + `HitInfo.Hazard` (`AMD-002`) |
| `MECH-007` | Slick Dodge | Last-0.3 s dodge picks up 2 nearest tiles | P1 | `PlayerController.TryDodge` |
| `MECH-008` | Boss taunts | Text bubbles on smash / wrong spell / dodge / phase change | P1 | `WorldSpaceBillboard` |
| `MECH-009` | Vulnerable hit phase | Shield gate opens; melee/thrown damage applies; enraged AoE | P0 | `IDamageable` |
| `MECH-010` | Prep weapon crafting | 45 s to craft one weapon from provided props | P0 | existing recipes |
| `MECH-011` | Records + boss unlocks | PlayerPrefs best time/wins/crowns | P1 | new, small |

### State machine — run phases (exhaustive)

| Current | Event | Condition | Next | Side effect |
|---|---|---|---|---|
| Preparing | timer ≥ 45 s, OR (crafted ≥ 1 weapon AND rooms visited ≥ 3 AND elapsed ≥ 25 s) | — | Boss Appear | freeze player input 1 s, camera pan to boss |
| Boss Appear | 12 s elapsed | — | Drop&Prevent (layer 1) | reveal word 1 + room clue |
| Drop&Prevent | word i spelled correctly | `LetterBag.Contains` on target word; layer < 3 | short Stagger (4 s, no AoE) → next layer or Hit if i=3 | shield layer −1, slow-mo 0.8 s |
| Drop&Prevent | round timer ≥ 165 s | shield still up | Result (lose) | boss "reboots", scoreboard |
| Stagger | 4 s elapsed | — | Drop&Prevent (next layer) or Hit | resume AoE schedule |
| Hit | boss HP ≤ 0 | — | Result (win) | boss deflate, confetti |
| Hit | player HP ≤ 0 | — | Respawn (3 s) | drop 3 tiles, boss taunt, HP reset 100 |
| Hit | round timer ≥ 300 s | boss HP > 0 | Result (lose) | — |
| Result | confirm | — | Lobby or retry | write PlayerPrefs |

### State machine — telegraphed AoE (exhaustive)

| Current | Event | Condition | Next | Side effect |
|---|---|---|---|---|
| Idle | boss attack tick | cooldown elapsed, layer ≥ 1 | Warning | pick centre = player pos + `radius + 0.55 m` in a random direction; radius from phase table; `warningLeft = 1.6` |
| Warning | `warningLeft ≤ 0.3` and player dodges inside circle | dodge active | Slick Dodge | collect 2 nearest tiles, boss "whiff" bubble, circle → Fade |
| Warning | `warningLeft ≤ 0` | — | Active (0.45 s) | red fill solid, `Physics.OverlapSphere` once at start + once at end |
| Active | hit player | within radius, not invulnerable | Fade for that player | `HitInfo.Hazard(layer)` + scatter 1 letter **only when not spelling/crafting** |
| Active | 0.45 s elapsed | — | Fade | — |
| Fade | 0.25 s elapsed | — | Idle | ring released to pool |
| Any | phase changes to Stagger | — | Idle | cancel all Warning/Active circles |

**Judgement rules:** circle = `Physics.OverlapSphere(centre, radius, playerLayer)`; table damage per overlap, once per circle per player (dedupe by circle ID); ties (two circles same frame) take the closest circle only. **Max 2 simultaneous circles**; overflow skips the spawn. `AMD-002`.

### Input map (logic actions already exist in `InputBinding.PlayerCommands`)

| Logic action | Keyboard | Gamepad | Note |
|---|---|---|---|
| move | WASD | left stick | simultaneous with spell |
| grab/attack | E | A | breaks prop / hits boss |
| spell | Q (hold) | X (hold) | opens `Summoner` UI, move scale 0.35 |
| dodge | Space | B | 0.35 s, 0.15 s i-frames, 4 m |
| spell up/down | W/S | D-pad | when spell UI open |
| target confirm | E | A | when spell UI open |
| pause | Esc | Start | — |

Same-frame duplicate inputs: first event wins, rest ignored until next frame. Touch input is **not** implemented in this batch; listed as porting batch.

### Feedback list

| Event | Visual | Audio | Haptic | Latency |
|---|---|---|---|---|
| prop hit | white flash 1 frame + splinter | `GameCue.Break` | light | immediate |
| tile pickup | slot lights up, +1 pop | soft chime | none | ≤ 0.2 s |
| AoE warning | red circle grows + beeps faster | beep loop | light pulse | immediate |
| AoE hit | screen edge red, 2 letters fly out | low thud | medium | immediate |
| shield crack | layer shatters, 0.8 s slow-mo | glass crack + boss yelp | long | ≤ 0.1 s |
| boss hit (vulnerable) | white flash + damage number | meaty clunk | medium | immediate |
| wrong word | boss laugh bubble, red X on word | comedic "wah" | none | ≤ 0.2 s |

### Edge cases

| Case | Expected behaviour | Verification |
|---|---|---|
| `maxLetters` full (16) | reject pickup with "Bag full!" toast; tile stays on floor | test: spawn 17 tiles |
| boss word longer than bag | validated offline: longest name word ≤ 6, bag 16 | data validation script |
| two circles hit same frame | closest one only; other ignored | unit test with 2 overlaps |
| room has no valid spawn | fall back to any connected room via `RoomGraph.Connected` | test on `LivingRoom` layout |
| player disconnects/quit mid-spell | `Summoner.OnDisable` cancels craft and refunds letters | existing behaviour, verify |
| boss destroyed while staggered | cancel timers, unsubscribe events, go to Result | test with forced kill |
| zero/negative config values | clamp to lower bound and log warning | rules validator |

## 7. Level / flow structure

| Run segment | Set-up | Unlock / progression | Difficulty coefficient (see §8) |
|---|---|---|---|
| P0 Preparing (0–45 s) | Player at house entrance; 3 "starter" props + 1 weapon-crate prop per room; boss asleep outside living room with snore bubbles | Craft ≥ 1 weapon → early Appear | 0.0 |
| P1 Boss Appear (45–57 s) | Boss stomps in, camera shake 0.4 s, word 1 + room clue revealed | — | 0.0 |
| P2 Layer 1 (57–92 s) | CALC's 4 letter caches + 2 decoys spread in clue room; ≤2 circles / 5.5 s, r 1.6 m, 10 dmg | Spell word 1 → layer crack + 4 s stagger | 0.18 |
| P2 Layer 2 (92–127 s) | ULA caches + decoys; ≤2 circles / 5.0 s, r 1.8 m, 12 dmg | Spell word 2 → layer crack | 0.31 |
| P2 Layer 3 (127–162 s) | TOR caches + decoys; ≤2 circles / 4.4 s, r 2.0 m, 14 dmg | Spell word 3 → shield break | 0.55 |
| P3 Hit (162–237 s) | Shield off; enraged 3 circles / 2.8 s, r 2.2 m, 14 dmg | Boss HP 240 → 0 | 0.77 |
| P4 Result (237–242 s) | Scorecard: time, knockouts, words spelled | Retry / next boss | — |

Breather rule: the 4 s Stagger after every shield crack keeps the curve from rising monotonically for 3+ consecutive segments. World layout: reuse `LivingRoom` scene and any of the 5 `house_*.json` maps (same as Escape); no new scene required for MVP.

Boss vocabulary (**one boss at launch**, original; stored in `rules.json` under `BossFight.bossWords`):

| Boss | Name words (3 shield layers) | Room clue style |
|---|---|---|
| Calculator | `CALC`, `ULA`, `TOR` | exact room names (all in `LivingRoom` layout) |

Constraint: each name word is 4/3/3 letters and is **always** obtainable from its scattered caches: every letter of the word gets its own smashable in the clued room, so obtainability does not depend on random map drops. Validation runs against `LivingRoom`'s `HouseLayout` before spawn. Longest word 4 ≤ bag 16. (`AMD-003`) Deferred bosses (Wobbly Broom King / Grumpy Duck Boss) are recorded in the pending list, not in this build.

## 8. Numbers & formulas

All values are starting points, marked **待实测 (to be playtested)** until verified in Editor.

### 8.1 Damage to boss

```
bossDamage = meleeDamage × vulnerability × combo − armor
```
| Variable | Meaning | Range | Unit |
|---|---|---|---|
| `meleeDamage` | weapon from `items.json` | 10 (BROOM) – 28 (HAMMER) | HP |
| `vulnerability` | shield state | 1.0 shielded (damage refused) / 1.5 Vulnerable | multiplier |
| `combo` | 3-hit chain bonus | 1.0 – 1.3 | multiplier |
| `armor` | fixed reduction | 0 (MVP) | HP |

Extreme checks:
- Min effective (BROOM, Vulnerable, no combo): `10 × 1.5 × 1.0 − 0 = 15` HP
- Max effective (HAMMER, Vulnerable, full combo): `28 × 1.5 × 1.3 − 0 = 54.6` HP
- Shielded: `ApplyDamage()` returns `false` before formula; **0 damage** regardless of weapon
- Negative guard: `Mathf.Max(1f, result)` → never below 1 HP when Vulnerable

### 8.2 Boss HP and time-to-kill

- `bossHP = 240` HP. Weapon damage from BLADE 24 → `240 / (24 × 1.5) = 6.67 → 7` hits. At ~0.45 s per swing + 0.3 s movement = ~5.3 s of net attacking; 75 s phase is generous (move/dodge consumes the rest). **待实测** for real DTK.
- Extreme: BROOM 10 → `240 / 15 = 16` hits ≈ 12 s net attacking; still inside 75 s.

### 8.3 AoE difficulty coefficient (reproducible, not a vibe)

```
D = (N × A × d) / (H × T)     where A = π r², H = 100 HP, T = segment seconds
```
| Segment | N attacks | r (m) | A (m²) | d (dmg) | T (s) | D |
|---|---|---|---|---|---|---|
| Layer 1 | 8 (35/4.5) | 1.6 | 8.04 | 10 | 35 | 0.18 |
| Layer 2 | 9 (35/4.0) | 1.8 | 10.18 | 12 | 35 | 0.31 |
| Layer 3 | 11 (35/3.2) | 2.0 | 12.57 | 14 | 35 | 0.55 |
| Hit | 27 (75/2.8) | 2.2 | 15.21 | 14 | 75 | 0.77 |

Extreme: max radius 2.2 m circle diameter 4.4 m vs dodge distance 4 m — dodge alone cannot clear the far edge, so the pattern must always leave a gap ≥ 0.6 m on one side. **Rule:** centre offset is clamped so that at least one 1.2 m safe wedge remains inside the room. Verified by `HouseLayout.RoomAt` bounds check.

### 8.4 Letter economy (production / consumption)

| Source | Letters produced (expected) |
|---|---|
| 3 word crates (ANGRY 5 + TOAST 5 + MAN 3) | 13 |
| 4 prep props (avg 4 letters) | 16 |
| **Total** | **29** |
| Name words (must spend) | 13 |
| One chaos word per run (planned, e.g. ZAP 3) | 3 |
| Waste from hits (2 × ~2 hits) | 4 |
| **Total consumption** | **20** |

Production/consumption = 29 / 20 = **1.45** (target band 1.1–1.4; slightly generous for a first solo build, intentional so the first run is winnable — tune down prep props to 3 in balance pass). All values **待实测**.

### 8.5 Drop rates

No random drops in MVP: crates and props are deterministic. Every word is guaranteed obtainable. Random boss selection is uniform 1/3 per run, expected 3 runs to see all bosses.

### 8.6 Configuration keys to add (strict whitelist)

`GameRules.cs:154-163` and `rules.json` mode `"BossFight"` must gain these keys (each must be added to `Known` or config load throws):
`prepSeconds`, `appearSeconds`, `layerSeconds`, `hitSeconds`, `bossMaxHealth`, `bossVulnerability`, `aoeWarnSeconds`, `aoeActiveSeconds`, `aoeRadius`, `aoeDamage`, `aoeCooldown`, `maxAoe`, `lettersLostOnHit`, `slickDodgeWindow`, `maxLetters` (existing key, override to 16), `roundTimeLimit` (existing, 300).
`GameRules.Validate()` additions: `aoeActiveSeconds > 0`, `aoeWarnSeconds > slickDodgeWindow`, `bossMaxHealth > 0`, `lettersLostOnHit <= maxLetters`, `maxAoe >= 1`.

## 9. Art style & asset list

**Style anchors** (silly party chaos; original, no IP mimicry)

| Field | Spec |
|---|---|
| View | 3D perspective, 45° top-down-ish, same camera as existing house scenes |
| Proportion | 3-head-tall chunky boss, ~1.8 m tall vs 1.7 m player; oversized appliance body, tiny arms |
| Lighting | upper-left key light, 3 brightness steps, soft shadows |
| Brush/edge | toon-shaded, no black outlines, soft rounded silhouettes |
| Saturation | background low (20–35%), props mid (45–60%), VFX/AoE high (75–90%) |
| Taboo | no gore, no jump scares, no black outlines, no realistic materials, no copyrighted character likeness, no living-artist style imitation |

**Palette** (hex, fixed)

| Use | Hex | Relative lightness |
|---|---|---|
| Primary (boss body) | `#F5A623` | L* ≈ 68 |
| Secondary (friendly UI / letters) | `#4ECDC4` | L* ≈ 72 |
| Danger / AoE | `#FF4757` | L* ≈ 62 |
| Background (walls) | `#FFF3E0` | L* ≈ 95 |
| UI base | `#2C3E50` | L* ≈ 24 |
| Vulnerable-window gold | `#FFD166` | L* ≈ 82 |

Brightness ladder: background 95 → environment 72 → character 55 → VFX 82; all cross-layer gaps ≥ 15 L* points. 25% thumbnail silhouette check + greyscale separation check required on boss and AoE ring.

**Asset list** (P0 = cannot run a round without it)

| ID | Asset | Spec | Qty | Priority | Source |
|---|---|---|---|---|---|
| ART-001 | Boss body (Calculator) | procedural primitive composite, ~1.8 m, 1 variant | 1 | P0 | procedural placeholder → **needs artist refinement** |
| ART-002 | Boss face/eyes | 2D sprite sheet 128×128, 4 expressions | 1 | P0 | AI-generated + **manual refinement** |
| ART-003 | Shield layers | 3 concentric translucent rings, 2.4 m radius | 1 | P0 | procedural |
| ART-004 | Red AoE ring/fill | `LineRenderer` ring + disc, radius 1.2–2.2 m | 1 | P0 | procedural (from `ThrownGear`) |
| ART-005 | Word crate prop | reskin of existing box, 0.8 m | 1 | P0 | existing + reskin |
| ART-006 | Boss HP bar + 3 word slots + timer HUD | 1920×1080 canvas, 9-slice panels | 1 | P0 | uGUI/TMP + **manual polish** |
| ART-007 | Shield shatter VFX | 12 shards + gold flash, 0.8 s | 1 | P1 | procedural |
| ART-008 | Taunt bubbles | 9-slice speech bubble + TMP | 1 | P1 | generated + **manual refinement** |
| ART-009 | Victory confetti | particle prefab, 3 s | 1 | P2 | procedural |

Naming: `chr_boss_<variant>_<state>`, `fx_aoe_warning_01`, `ui_bossword_<slot>_<state>`, `sfx_boss_<event>`; directory under `Assets/_Project/Art/Generated/BossFight/` and `Resources/UI/BossFight/`, matching existing import pipeline.

## 10. Audio & presentation

| Cue | Trigger | Asset | Note |
|---|---|---|---|
| `sfx_boss_snore` | Preparing loop | looping, comedic | fades out at Appear |
| `sfx_boss_stomp` | Appear / phase change | low thud + shake | 0.4 s camera shake |
| `sfx_aoe_beep` | Warning, accelerating | existing `Bomb_Warning` pattern (`GameFeedback.Burst`) | beep interval 0.4 → 0.15 s |
| `sfx_shield_crack` | each layer break | glass crack + yelp | pitch rises per layer |
| `sfx_boss_hurt` | Vulnerable damage | meaty clunk | reuses existing hit cue with lower pitch |
| `sfx_word_wrong` | wrong spell | "wah" trombone | comedic, not punishing |
| `sfx_victory` | boss HP 0 | rising fanfare | 2 s |

Music: reuse current house track; at P3 switch to a 30 s looped "boss" variant (P1, needs new asset). No audio in this batch is "guaranteed" to feel right — all cues marked **待实测** for mix and timing.

## 11. UI / UX & information architecture

| Screen/element | Content | Transition |
|---|---|---|
| Lobby mode card | "Boss Fight — solo, 4 min" | select → `Session.LoadMode("BossFight")` |
| Prep HUD | timer, "Smash props · craft 1 weapon · scout 3 rooms" | → Appear at timer/condition |
| Boss bar (top centre) | boss name as 3 word slots: `_ _ _ _ _ / _ _ _ _ _ / _ _ _`; 3 shield pips; HP bar only after shield break | persists through P2/P3 |
| Room clue toast (right) | "TOAST is in the KITCHEN!" + minimap ping | replaced each layer |
| Spell panel (bottom centre) | opens on Q; shows letter bag, ready words, hints | closes on craft/cancel |
| AoE warning | in-world red circle only, no full-screen prompt | — |
| Result card | time, knockouts, words spelled, best-time delta | Retry / Lobby |

First-time flow: a 3-line tutorial appears during Preparing (move, smash E, spell Q). No separate tutorial scene needed.

## 12. Technical plan / engineering structure

Unity `6000.6.3f1`, URP `17.6.0`, new Input System `1.20.0`, existing `Wreckabulary.asmdef` (runtime) + `Wreckabulary.Rules.asmdef` (engine-free).

Recommended new code (all under `Assets/_Project/Scripts/Game/BossFight/`):
- `BossFightDirector.cs` — phase state machine, config, event wiring; follows `EscapeDirector` shape; sets `RoundManager.enabled = false`.
- `BossController.cs` — `IDamageable` shield gate, HP, phase, taunt events.
- `TelegraphedAoE.cs` + `AoEPool.cs` — warning/active/fade, object pool.
- `BossNameCatalogue.cs` — reads `bossName` / `bossWords` from `Match.Rules`, validates word lengths/letter availability.
- `BossFightHud.cs` — runtime canvas via `EscapeDirector.BuildNotice()` pattern.
- `BossRules.cs` — new config fields + validation, added to whitelist.

Data: `Data/Config/rules.json` add `"BossFight"` block (including `bossName` and `bossWords`); no new CSV or ScriptableObject — those need a data-setup re-run; `Data/word_list.csv` and `items.json` unchanged (shared pool).

Dependencies / ordering (no cycles): `BossRules` → `BossNameCatalogue` → `BossController` + `TelegraphedAoE` → `BossFightDirector` → `BossFightHud`. MVP end condition: one full solo run from Preparing to Result, reloadable, with no console errors and self-check script passing.

## 13. Content & workload estimate (rough ranges, not a schedule)

| Item | Count | Rough human-day range |
|---|---|---|
| New C# classes | 6 | 4–8 |
| New config rows / CSV | 1 boss + ~16 rule keys | 0.5–1 |
| P0 art placeholders | 6 | 1–3 (plus refinement) |
| P0 audio cues | 7 | 1–2 |
| HUD layout | 1 canvas | 1–2 |
| Tests (rules + playmode) | ~20 cases | 2–4 |
| **Total MVP** | — | **9.5–20 person-days (indicative only; not a commitment)** |

## 14. Risks & cut order

| Risk | Category | Probability | Impact | Trigger signal | Response |
|---|---|---|---|---|---|
| Boss name letters unobtainable on `LivingRoom` | Content | low | high | validation script reports missing letter in `LivingRoom` layout | fall back to a shorter name word; crates always carry full word |
| AoE + 40 tiles frame drop | Technical | medium | medium | < 30 fps in profiler scenario | pool AoE, cap tiles to 24, disable culling-heavy VFX |
| Spelling UX too slow while dodging | Design | medium | high | playtest: > 2 consecutive hits during spell | freeze AoE while spell panel open in P2 (P1 rule) |
| Scope creep from co-op/mobile | Scope | high | high | any request to add second player before MVP | refuse — solo only, documented porting batch |
| Art placeholders feel unfinished | Content | high | low | review screenshots | procedural first, artist refinement flagged per asset |
| Store/platform policy drift | External | low | low | only if published | check official docs at publish time; not in this batch |

Cut order: **P2** (confetti, music variant, extra bosses) → **P1** (taunts, Slick Dodge, chaos-word overrides, minimap ping) → never cut P0 (phases, shield, crates, AoE, hit phase, HUD). If P0 is still too large, replace 3 name words with 2 — do not drop the shield-break mechanic.

Feasibility verdict: **需裁剪可做 (doable with trimming)** — the only genuinely new system is a focused `TelegraphedAoE`; everything else reuses existing code. Scope ledger: P0 systems 7 (< 8), P0 assets 6 (< 40), segments 7 in one reused scene (< 15), code modules 6 (< 12), prose < 1000 words. Verified by completing the MVP end condition above.

## 15. Milestones & external acceptance criteria

| Milestone | Deliverable | Externally verifiable acceptance |
|---|---|---|
| M1 Design frozen | `docs/design-freeze.json` status `frozen` | check script reports frozen + item count |
| M2 Rules & data | rules.json + whitelist + boss CSV | `dotnet` rules tests pass; validator reports 0 missing letters for CALC/ULA/TOR in `LivingRoom` |
| M3 Core loop | Director + crates + shield + AoE | PlayMode test: spell 3 words → shield breaks → boss damage applies; log shows all 4 phases in order |
| M4 HUD & feedback | boss bar, word slots, toasts, VFX | screenshot of each phase + recorded video of one full run under 5 min |
| M5 Verification | self-check + profiler | `check_unity_project.py` JSON report: 0 errors; profiler shows ≥ 30 fps in defined scenario |
| M6 Delivery | this GDD updated + README entry | README mode table includes BossFight; no doc drift for this mode |

---

## Confirmation ledger / deferred items (待确认清单)

**Resolved on 2026-11-22 (待拍板 → decided)**
| Question | Decision |
|---|---|
| 4-phase rundown, ~4–5 min solo | ✅ Confirmed as drafted |
| Map | ✅ `LivingRoom` only (no new arena) |
| Boss | ✅ `Calculator` only (extra bosses deferred) |
| Shield words | ✅ `CALC` / `ULA` / `TOR` (user corrected "CACL" → "CALC") |
| Chaos-word effects (ZAP/BEES/MAGNET) | ✅ Confirmed in MVP via blanket GDD confirmation; implemented as P0 |

**Deferred to later batches (not in this build)**
| Item | Batch |
|---|---|
| 2nd/3rd bosses (Wobbly Broom King / Grumpy Duck Boss) and unlock crowns | P2 content |
| Couch co-op (2–4 players), bot handling, split HUD | porting batch |
| Mobile/touch layout and larger telegraph circles | porting batch |
| Boss music variant, victory confetti, boss voice acting | P1/P2 audio-art |
| Slick Dodge (`MECH-007`) and boss taunts (`MECH-008`) | P1 — designed, not implemented in this slice |

**To verify externally (待查证)**
| Item | Why | Who | Blocked step |
|---|---|---|---|
| Any store/platform requirements if this ships standalone | affects UI/save compliance | publisher docs, check at publish time | not this batch |
| Exact Input System key binding conflicts with existing rebindable keys | avoid overriding player bindings | Editor check on `KeyBindings.cs` | M3 |

**To measure (待实测)**
| Item | Why | Who | Blocked step |
|---|---|---|---|
| 240 boss HP / 7-hit TTK actually feels right | balance | playtest | M3 sign-off |
| AoE radius 2.2 m vs 4 m dodge leaves a fair gap | fairness | playtest + profiler | M3 sign-off |
| 29:20 letter economy ratio | pacing | playtest | M3 sign-off |
| 60/30 fps with 3 circles + tiles | performance | Profiler | M5 |

**Assumptions accepted as part of the GDD confirmation:** 3 shield layers instead of one long name; hit-scatters-letters rule; chaos-word spending; solo-first with co-op deferred; `LivingRoom` reused instead of a new arena; 240 boss HP; all numbers in §8. Both taunts and Slick Dodge remain designed-but-deferred (P1), as recorded above.

---

### Design-phase recap
- Reviewed the project (Chapter 0) and wrote a 15-chapter GDD with 11 numbered mechanics, three-layer loop, state machines, reproducible formulas with extreme checks, art/audio/UI specs, feasibility verdict, and a verification plan.
- User confirmed the GDD and set scope to one map (`LivingRoom`) and one boss (`Calculator`, CALC/ULA/TOR). Design is now frozen in `docs/design-freeze.json` version 1.

### Next (Phase 2, in progress)
- Implement the MVP slice: rules/config → boss name data → boss/telegraph systems → director/HUD → mode registration → verification.
