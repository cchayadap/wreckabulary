# Windows 210349 independent visual review

STATUS: COMPLETE — bounded visual improvement reviewed; remaining polish and controller-help mode context recorded. Owner acceptance pending.

I opened all **29 actual 1280×720 PNGs**: 20 from lobby run `20261026T210547Z-lobby_experience` and 9 from solo run `20261026T210721Z-wordbound_solo`, both for build `20261026T210349Z`, source `0ba2309aaad25c01e2e75da4232c07a25f989567`. The checks record lobby 5/5 and solo 3/3 PASS. Every inspected image has its own observation, dimensions and SHA256 in [the review manifest](windows-210349-visual-review.json). No images were generated/replaced, and no native/player or source changes were performed.

The compact Solo/Maps panels remove the large cream gap visible in the actual prior 202433 Solo and Maps images, which I also opened. Cards, selected states, family/queue tabs and GO/OPEN remain readable. The two-seat restriction is clear and GO is visibly disabled. Original full House/Home & tutorial grids remain intact. **No new primary-control clipping or overlapping text was observed in this set.**

Both solo world images show the original character and a coherent game camera. The HUD now visibly says `[Q] Craft · [ESC] Menu`, with `[LMB] Smash furniture`, matching the reported invoking DesktopBinding. Both menu captures show full-color original recipe thumbnails, selected BAT/NeedABT details and **Return to lobby**. The scroll viewport contains its partly visible bottom row without covering the fixed footer. Both returned captures show the correct solo selection and no remaining game HUD.

Customize and Shop retain the original cream face, vivid clothing and clean portrait cutouts. Settings tabs, graphics choices, visible Controls scrollbar and final bindings are readable. Home's Local/Online actions are clear; the online screenshot shows the real guest Connect form only.

Remaining findings:

- **Avatar composition:** compact Solo/Maps panels still cover the centered avatar's head and torso, leaving its legs visible. Room space is more visible, but this is not full avatar/lobby-art acceptance.
- **Controller-help context:** Settings shows Aim=Right stick and Pause=View/Share. Current Wordbound uses right shoulder/Start; that does not prove the original house legend is wrong. The global screen needs clear mode context or separate Wordbound guidance. Root is auditing the house mapping. This keyboard capture set establishes no physical controller behavior.
- **Environment polish:** the starting room is visually sparse, with mostly plain walls and wood floor. The TimeRun checkpoint label is readable but pale against the room. These stills do not establish final detailed/varied map-art quality.

Receipt context: TimeRun readiness records expected 1/rendered 1 marker, 3 camera frames and 105 ticks; Freestyle records expected 0/rendered 0, 3 frames and 53 ticks. Later observations record 7 camera frames each,128/70 ticks and journal 0 in both modes. Both input wrappers are reported released with source bindings preserved, and both gameplay cleanups report no failure. These are recorded runtime assertions, not actions performed by this reviewer. Zero Freestyle markers matches its plan; journal 0 does not establish story-introduction delivery.

This review accepts only the observed UI/launch-view improvement at this resolution. Full course/endings, creator playtesting, animation feel, physical input, arbitrary small windows, full art approval and public/team online matches remain outside this visual evidence.
