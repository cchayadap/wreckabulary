# FFA equipment sources

STATUS: SOURCE CHECKPOINT READY — COMPILER AND UNITY NOT RUN

Root authorized a narrow repair to CourseTemplates.CombatArena and new focused engine-free tests after independent acceptance finding A2. This branch did not yet contain CourseTemplates.cs, so it receives the exact integration file (baseline SHA-256 E5381E247A9921D696945EF4FE58FA24FD829BED192D23894AF2EFAD6E633DD0) and changes only CombatArena. Integration may require add/add resolution; preserve all other template methods unchanged.

Design: eight mirrored shared renewable caches centred at x=-6,-2,2,6m and z=-6,+6m. Each cache has a 7×3 grid of single-letter sources at 0.5m spacing. Every start and respawn is 1.5m from its nearest letter. The 21-letter multiset ABCCDEFHIKLLMMNOOPRST covers every current enabled FFA recipe, including required duplicate C/L/M/O. Existing props/resource zones remain. Source refill uses existing ten-second deterministic missing-letter recovery and the unchanged optional body reservation. No mode/stat/physics rule changes.

The individual positions matter: current WordboundMatchHud.Target selects a single nearest visible body, while AuthoritativeMatch.AddTiles places an entire source at one position. A 21-letter pile could fill the ten-letter inventory before exposing the wanted letter. The grid avoids this authored overlap without changing interaction or physics. It increases bounded source records to168, while retaining the same168 initial supply bodies; no extra body allowance is introduced.

Expected worst-case initial authored count with eight actors is 8 actors +3 props +15 random resource letters +168 supply letters =194 bodies. The existing maximum-eight-player actor reservation is288; at three props the optional pool is213, exceeding the183 authored optional letters. Verify these values against exact source before acceptance. No native selection/physics/performance claim follows from this estimate.

Nineteen engine-free cases are authored in WordboundArenaResourceTests: real-catalogue collection/crafting for all13 enabled recipes; exact resource counts at2/8players and three seeds; symmetric access and distinct letter positions; ordinary unarmed furniture breaking followed by collection/crafting; deterministic missing-letter refill without duplication or crafted-equipment loss. Snapshot publication, durable ACK and strict round-trip are exercised throughout the fixture. Host poses and line-of-sight are controlled by this pure test; it does not claim native movement, target selection or performance acceptance.

Source review checked the template diff is confined to CombatArena, all new tests use public snapshot views, and item/rules config bytes match integration (SHA-256 items79363C8F1F14E7ACA553718B552468D34B3140FE4BA88688FD4DED8330CF2416; rulesF183D9B79D1B438A78FB682455C6CE5E6B15CBD09D47B0831B3AF47B2F697D4C). `git diff --check` passed before checkpoint. No NUnit or compiler result is claimed yet.

Prepared overlay recipe: ArenaResourceTests.csproj links integration Rules and substitutes only this lane's CourseTemplates, with real NUnit3.14 and the existing TestData/catalogue. Run from this worktree root after root grants the compiler window: `dotnet test BuildNotes/ffa-content/ArenaResourceTests.csproj --logger "trx;LogFileName=arena-resources.trx" --results-directory BuildNotes/ffa-content/results -p:UseSharedCompilation=false`. Follow with the integration project's normal Rules reference gate after cherry-pick. Do not run Unity from this lane.

Next: commit source-only checkpoint and await root's explicit compiler window. Public export must wait for the changed FFA binding to pass its source gates. Native targeted selection/collection in the grid remains unverified.
