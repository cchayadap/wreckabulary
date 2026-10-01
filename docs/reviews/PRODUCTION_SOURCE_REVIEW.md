# Integrated production source review

Reviewed the live `feature/wreckabulary-production` working tree on top of `6030d6b`. This is a source review, not a Unity gameplay pass. Files were changing during the review; each finding below identifies the observed behavior and subsequent source corrections separately.

I reviewed the new mode directors, round outcomes, room geometry, AI, inventory/crafting, held/deployed/thrown gear, protection integration, touch bindings/HUD, avatar appearance, front-door menu, data loading and affected integration tests. I authored the narrow `Health.cs` / `PlayerHealth.cs` lifecycle changes earlier, so review of that portion is not independent. I subsequently reviewed the browser engine and UI independently after that implementation became available.

Unity runtime validation remains blocked by the existing Personal-license rejection (exit 198). The root agent separately compiled runtime source against official Unity references with netstandard2.1 / C# 9; compilation does not establish gameplay or UI behavior. I did not change repository source during this review.

Final recheck: no unresolved confirmed P1/P2 remains among the scoped findings below after the latest corrections. Browser mechanics have independent executable evidence; native corrections have source/test review and still require real Unity execution. I did not run a shared C# compile during the final recheck.

## Prioritized findings

### P1 — The touch typewriter disables its own confirm/cancel controls

`Assets/_Project/Scripts/Game/Typewriter.cs`, `Open` and `Update`, freezes its user while awaiting up/down, grab/attack confirmation or spell cancellation. `Assets/_Project/Scripts/Game/GameHud.cs`, `RefreshSkills` (observed line 354), enables skill buttons only when `LocalPlayer.CanAct`, which is false while Frozen. The touch HUD also has no typewriter up/down controls outside the crafting drawer.

Reproduction: join with touch, explore the Hub, approach the typewriter and tap GRAB. The character is frozen; GRAB, SMASH and CRAFT become disabled, so the player cannot confirm or cancel using those controls. The front-door menu originally had no direct Tutorial action. That direct action has since been added, but it alone does not repair the typewriter interaction.

Impact: an advertised Hub interaction traps the touch player and makes the physical mode menu unusable. Status: root added a separate visible typewriter panel with previous/next/select/back actions; I observed its button callbacks and `RefreshNavigation` selection display. The direct Tutorial front-door action is present too. Unity execution of this interaction remains pending.

### P1 — AI partners hold revive without ever starting it

`Assets/_Project/Scripts/Game/BotController.cs`, observed lines 64–71, set `c.grabHeld = true` near a downed teammate but never set the `c.grab` edge. `PlayerCombat.UpdateRevive` returns immediately when no revive is active; starting one requires `grab` through `GrabOrThrow` / `TryRevive`.

Reproduction: solo Duos, down the human near their AI partner. The bot approaches and holds its position, but the human bleeds out because no revive starts.

Status: the mode owner corrected source to pulse grab while not already reviving and added an AI revive integration test. I observed the new `c.grab = !player.Combat.IsReviving` line. Unity execution of that regression remains pending.

### P2 — Moving Day resupply duplicates letters already reserved by a craft

`Assets/_Project/Scripts/Game/MovingDayDirector.cs`, observed lines 243–258, gathers loose tiles and available inventories, then counts boxes and constructed furniture. It does not count `Summoner.IsCrafting` / `CraftWord` or reserved letters. `Summoner.BeginCraft` removes recipe letters immediately and holds their reservation during the channel.

Reproduction: begin crafting BED just before the director's two-second resupply check, with exactly the original BED letters and no other BED box/item. The director sees neither an available recipe nor a constructed BED and delivers another BED box while the original craft is still in progress.

Impact: recovery logic creates extra recipe letters during ordinary crafting. Status: I observed the mode owner's correction adding matching active `CraftWord` allocations to the constructed-item count and iterating each remaining duplicate objective. The owner added a resupply-during-BED-channel regression, unrun in Unity.

### P2 — The front-door postcard overflows portrait windows

`Assets/_Project/Scripts/UI/FrontDoorMenu.cs`, observed lines 58–85, uses a 1920×1080 CanvasScaler with match 0.5 and a fixed 1700×920 sheet. Changing safe-area anchors does not fit/reflow the sheet.

At a 1080×1920 portrait window with full-screen safe area, scale is 1: the Dibs card at x=-560, width 350 projects to x=-195…155; wardrobe buttons at x=445, width 520 project to x=725…1245. These controls are clipped beyond the viewport.

Impact: Unity Web/desktop portrait windows lose readable/accessibly placed controls. The Android build intentionally locks landscape, so this is not a claim that Android rotates into portrait. Status: I observed the added fit calculation scaling the sheet to both safe-area axes. Real display inspection and minimum touch hit-area validation remain pending; fitting content does not itself prove adequate physical button size.

### P2 — Touch has no in-play route home

`GameHud` has no HOME/back/pause action. `ModeActions` adds HOME, but its entire panel is hidden during normal gameplay. `TutorialDirector` never creates that panel. `BackToHub` only handles keyboard Escape or gamepad Select.

Reproduction: start a mode or Tutorial with touch only, then attempt to return home before a result. No touch action performs this operation. Tutorial has no result menu and waits for all steps before automatic return.

Impact: touch players cannot leave an active experience through the advertised interface. Status: I observed a persistent GameHud HOME button outside the Hub, releasing touch state before calling `Session.GoHome`. Unity execution remains pending.

### P2 — Tutorial BAT recovery retained the old step indices

Adding Jump/Dodge at index 1 moves Spell BAT to index 4. `TutorialDirector.Resupply`, observed line 127, still stopped above index 3. If BAT letters were lost or spent after reaching the spell stage, the recovery box could never arrive.

Status: the mode owner changed recovery to indices 2…4 and suppresses resupply during an active BAT craft. I observed both changes. Unity execution of the recovery regression remains pending.

### P2 — Evacuation integration test contradicted default map data

`Assets/_Project/Tests/ModeRuntimeTests.cs`, `MovingOutRequiresPackedKeepsakesAndSurvivorsAtTheVanThenRestarts`, asserts three keepsakes and three packed items. At initial review, default `house_pinwheel.json` supplied four objectives (Bedroom, Kitchen, Study and Playroom). The test would necessarily fail once Unity ran.

Status: root restored the canonical pinwheel data to three keepsakes. I independently parsed the current JSON and confirmed its count is three, matching the added integration test. Unity execution remains pending.

## Remaining targeted verification

### P2 — Unity melee and grab do not inspect intervening walls

`Assets/_Project/Scripts/Player/PlayerCombat.cs`, `Strike` (observed lines 235–264), gathers an overlap sphere, then filters body identity and facing arc. It never checks whether solid room geometry blocks the target. A static wall has no Rigidbody and is skipped; it does not stop subsequent target processing. `TryGrab` (observed lines 287–309) similarly chooses the nearest eligible overlap body without occlusion.

Concrete geometry: on pinwheel, `(2, 3.5)` and `(2, 4.5)` lie on opposite sides of the `z=4` wall, one metre apart and within unarmed reach 1.1. An attacker facing forward met every original Strike filter for a victim across that wall. Status: the mode owner added a shared chest-origin visibility ray for Strike, TryGrab, revive selection and ongoing revive. I independently reviewed the source: own/target descendants are ignored; player/tile layers and triggers are excluded; query saturation falls back to RaycastAll; placed kinematic cover blocks while loose bodies do not. Five new `WallInteractionTests` cover wall/door player damage, furniture damage, grab, own visual/tile exclusion and revive cancellation. No new confirmed defect was found in that correction. Unity physics execution remains unavailable, so this is a source correction with unrun regressions.

`RoundManager.FinishRound` originally stopped deliveries/clear-out and froze players while active BOMB fuses/projectiles continued. A late fuse could wreck the awarded survivor during RoundOver. Status: I observed `World.FreezeTransient` stopping thrown-gear trackers, destroying projectile/throw-tracker sources, ending timed summons and freezing remaining transient rigidbodies, called from battle and co-op result states. The owner added a late-fuse regression, unrun in Unity.

The currently integrated two gear slots preserve object identity through swapping, consumption marks recipes spent before destroying them, reusable gear bursts its recipe once through its guarded break method, and crafting reservation counts prevent bag-capacity pickup during the channel. I found no additional confirmed gear-refund duplication in the inspected snapshot. This does not substitute for the pending Unity tests of swap/drop/throw/deploy, interrupted channels, durability break and reset cleanup. The gear owner separately repaired released-slot reparenting and thrown skin/miniature scale during the review; those corrections were observed in source.

Imported mesh appearance, animated socket positions, colliders, doorway traversal, mobile layout/hit areas, real scene startup order and completed match/tutorial flows still require an actual Unity run. No runtime success or complete production readiness is claimed here.

## Independent browser mechanics review

I read `Web/src/engine.js` and its mechanics tests against canonical Rules JSON and the current Unity implementations. The browser author's reported 27 passing tests cover selected engine behavior; they did not exercise the failures below. I executed isolated Node reproductions against the browser source while the author was preparing additional fixes. I disabled AI only to isolate the human mechanic in these reproductions. These checks ran real engine methods rather than Unity stubs. The browser files were not edited by me.

### P1 — Destroyed reusable tools retain infinite active zones

At review time, `Game.breakItem` removed the backing item and returned its recipe letters but did not remove the item's zone. `tick` applied every zone without validating its backing item. BED and MAT have no expiry, so their zones persisted forever.

Executed reproduction: craft/deploy MAT, break it, place the player inside its prior zone and tick. Result: item state `gone`, deployment count 0, zone count 1, speed multiplier still 1.6, returned MAT letters and `audit().balanced === true`.

Impact: repeated break/recraft loops can accumulate ghost jump pads/speed strips beyond the two-deployment limit. This is an ownership failure even though the letter ledger balances. Status: the author added cleanup on break/pickup plus backing-item validation during zone ticks. I repeated the MAT reproduction against the new source: zone count 0, no newly applied speed buff, item `gone`, audit balanced. This case is independently resolved in the engine.

### P1 — Moving Day can spend the only remaining checklist letter

The initial browser mode started with a finite furnished map, allowed every enabled recipe and had no delivery/resupply path. At that first review, Unity started without furniture, restricted crafting to its checklist and supplied labelled boxes. The later user-directed all-recipes change is described below.

Executed reproduction on both maps: break original furniture, then craft/deploy SOAP six times, allowing each zone to expire. Both maps then had zero P, LAMP remained unplaced, the game stayed `playing`, and the audit remained balanced. Pinwheel had 127 remaining tiles and 24 spent letters; courtyard had 97 remaining tiles and 24 spent letters. There was no legal action that could regenerate P.

Impact: ordinary allowed recipe use could permanently prevent co-op completion. Status: the author added an unfurnished initial house, labelled recipe parcels and recovery resupply. Root subsequently directed that all 12 recipes remain available for creative play, so SOAP acceptance is intentional; the native policy has now been reconciled too. I executed a legal spent-P reproduction on both maps against the new source: break initial parcels, collect/craft/place SOAP to spend the sole P, then run the recovery check. Both maps deliver a new LAMP parcel while preserving the ledger (`spent=4`, audit balanced). The permanent-P-depletion case is independently resolved by recovery. The updated co-op helper-AI completion regression also passes both modes/maps without teleporting.

### P1 — Weapon windup resolves after its owner drops it

At review time, `attack` stored weapon stats/item id in `pendingAttack`; the tick resolved that attack if its player was merely alive. It did not require the original weapon to remain equipped or the player to remain unstunned and out of a dodge. Unity's melee coroutine checks those conditions before and during the active window.

Executed reproduction: equip BLADE, begin its attack, immediately drop BLADE, advance past windup and tick. The nearby victim ended at 76 HP while the attacker held no item. The letter audit still balanced. Swapping/throwing or hit-stun can likewise leave an obsolete attack pending.

Impact: action interruption and equipment ownership did not control the scheduled hit. Status: the author bound the active window to the equipped item/slot and CanAct, with per-target deduplication. I repeated the BLADE/drop reproduction against the new source: victim 100 HP, pending attack cleared, no equipped item, audit balanced. This case is independently resolved in the engine. Other interruption combinations require their new regression cases.

### P2 — Crafted durability uses different hit units

Browser `resolveAttack` and blast code originally subtracted player Damage from crafted gear durability, but BreakPower from original furniture. Unity `HeldWeapon.ApplyDamage` subtracts BreakPower. For example, BAT wore a crafted target by 14 in the browser and by 2 in Unity; BOMB wore it by 45 versus 4. This changes reuse/cover lifetime substantially. Status: I observed both browser attack and blast code now applying BreakPower uniformly. A deployed-cover execution regression is still appropriate.

### P2 — Dodge eligibility and direction are not captured consistently

Browser dodge originally checked only life state and cooldown. It allowed a hit-stunned player to dash and used the live facing direction for displacement every tick, allowing mouse motion to steer an already-started dash. Unity checks CanAct and captures `dodgeDirection` at the start. Status: both corrections are now present. I executed the updated engine: stunned dodge returned false; after a forward dodge and sideways aim, x stayed 0 and z advanced 0.5714. The captured dodge direction remained forward.

### P2 — Dodge does not cancel an active craft reservation

The updated `Game.dodge` clears a timed use/place action and pending melee, but leaves `p.craft` reserved and active. `completeCraft` checks only time. Unity `Summoner.Update` cancels and refunds whenever `controller.IsDodging` or CanAct becomes false.

Executed reproduction: collect BAT, begin crafting, dodge at 0.2 seconds, then advance to craft completion. Initially, dodge was accepted, the bag remained empty and the BAT craft remained active; BAT subsequently appeared equipped. Status: the author added cancellation/refund in dodge, CanAct validation in craft completion and a pending-melee craft guard. I repeated the reproduction: dodge accepted, craft null, bag `BAT`, no equipped BAT after the prior completion deadline, audit balanced. This case is independently resolved in the engine.

### P2 — Pause still advances live simulation

`Web/src/main.js`, `animate` (observed line 40), runs the outer `else` simulation loop when `screen === 'game'` but `paused === true`. That loop still invokes `game.tick(dt,{})` on a playing game. Human input is disabled, while AI, hazards and the match clock continue behind the “The mess can wait” pause card.

Impact: leaving a game paused could cause a timeout or knockout while the player had no input. Status: the outer fallback tick now runs only when `screen !== 'game'`. I independently ran real Chromium against the author's immutable production preview: start Dibs, use a normal pause click, wait 1.2 seconds, compare time and all four player HP/positions, then resume with a normal click. The paused snapshot stayed exactly equal and the clock resumed afterward; no page errors occurred. Evidence: `evidence/independent-browser-pause.json`. This case is independently resolved in a real browser.

### P2 — Recoverable projectiles pass through physical cover

`Game.tickProjectiles` checks wall segments and players but never tests furniture/cover collision or applies BreakPower to those items. Unity `ThrownGear.OnCollisionEnter` instead applies the thrown stats to any collided `IDamageable` body.

Executed reproduction: player at `(0,0)`, crafted TABLE dropped at `(0,1.3)`, victim at `(0,2.5)`, throw BALL forward and advance four 0.05-second projectile steps. Initially: victim 88 HP, TABLE durability 120 unchanged, BALL landed beyond it, ledger balanced. Status: the author added oriented-item segment collision and BreakPower wear. I repeated the same legal craft/drop/throw reproduction: victim 100 HP, TABLE durability 119, BALL returned to world state with no active projectile, ledger balanced. This case is independently resolved in the engine.

The author’s timed use/deployment channels, physical Moving Out carry/drop, warning/filling/closed hazard phases, directional MAT footprint, BED trajectory and respawn cooldown resets are now observed in source. I independently ran the updated mechanics file directly: 40 tests passed, 0 failed, including co-op helper AI completion on both real layouts without teleporting and full melee-recovery craft eligibility. The separate pause reproduction above ran in real Chromium. These checks do not establish native Unity gameplay, exhaustive AI reliability across player choices, real-device touch behavior or complete production readiness.

## Final native Moving Day parity recheck

The gear owner added `Summoner.ChecklistPlacementWords` and a trusted `ResolveRecipe` lookup before either craft or effect application. `MovingDayDirector.ConfigurePlayer` promotes configured objective IDs into Furniture entries and unions the remaining live recipes, so the actual mode offers all 12 enabled IDs. Direct placement is limited to the explicit objective set; non-objective MAT still creates a usable catalogue speed strip. LAMP, whose catalogue category is Utilities, can now build plain checklist furniture through its trusted objective entry.

I independently read the final Summoner, SummonEffects and MovingDayDirector changes and their new tests. The tests exercise actual-mode all-12 availability, objective LAMP placement, non-objective MAT gear, disabled IDs, direct-effect active-list bypass refusal, SOAP cancellation/refund, normal SOAP consumption and a replacement LAMP parcel after spent recipe letters. Recipe resolution prevents caller-supplied categories from bypassing the active recipe policy. No confirmed source defect was found in this final correction. These native tests remain unrun because the Unity license rejection persists.
