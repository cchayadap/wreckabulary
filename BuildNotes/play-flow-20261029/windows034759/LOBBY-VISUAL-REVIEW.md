# Windows lobby visual review

STATUS: COMPLETE — AGENT VISUAL REVIEW; OWNER APPROVAL PENDING

Independent read-only review of every original PNG in `Docs/qa/runs/20261029T035021Z-lobby_experience`, built Windows player `20261029T034759Z`, source `07d814ec`. No Assets, native processes or Git state changes in this lane. Each original is opened directly with view_image; no composites or browser substitutes.

All **24/24 originals** were opened independently at their captured 1280×720 resolution. `checks.json` identifies the Windows build and records PASS; its own scope correctly excludes physical-input, live matchmaking and full-match acceptance. The source revision above was supplied by the integrator. This report independently verifies the image bytes and observations, not release approval or the broader game plan.

## Observed result

The Play organization is clear in the actual player: Practice (Offline), Matchmaking and Create form a centered top strip; the direct mode choices sit immediately below; map cards share dimensions and selection treatment; the footer keeps one prominent Play/Open action. Dibs, Duos, Escape, Time Trial, Freeplay and all three creator tools have readable labels. Home builder no longer truncates. Map editor and Tutorial show one selection each. The extra-local-seat capture shows a clear disabled Play action and a short red reason.

No Play avatar, decorative stripe background, overlapping controls, ellipsized label or cut-off primary action was observed in these captures. The second House map row is intentionally partly below a scroll viewport; this is not reported as clipped artwork. Selected mode underlines, cyan map checks/borders, tab selection, disabled home-builder choices and the blocked Solo action are visually distinct. These stills cannot prove every physical pad movement or that every scrolled card becomes visible; those need the separate native/runtime checks.

The online guest capture is substantially more compact: five modes, the real Lost Address preview, Find players, Match settings, Name and Connect all fit together. Workshop and Back remain readable. This image stops before authentication and does not show a formed party, live queue, FFA preview, retry or match. Those remain separate acceptance evidence.

Settings use clean rows with consistent left labels and right controls. Audio, Profile, Gameplay, Graphics and both control families are readable. Graphics and Controls show proper scroll tracks; bottom controls and reset actions fit. Help uses three illustrated steps rather than large text boxes. Some help/controller notes are small at 720p; larger text/UI scaling remains an accessibility refinement. Customize and Shop preserve the vivid room backdrop and original character/item style, using icons and thumbnails with restrained labels. An avatar is expected on Home, Customize and Shop; it is absent from Play and Settings.

## Visual follow-up and resolved challenge

- **Escape footer wording:** `lobby_local_escape.png` shows “Escape · random”, while its card correctly says “Random house”. Prefer the readable card label in the footer when the next wording pass is made. This is cosmetic and does not alter random-house behavior.

**Resolved false positive — Settings Close.** The first report incorrectly flagged the close X as not discernible in Audio, Gameplay, Graphics and Help. Root challenged that observation after independently opening the originals. I reopened all four original PNGs with `view_image` at original detail: the white top-right X is present at approximately (1233, 87), clear of the content. The close-affordance concern is withdrawn; no production change is warranted. Original image hashes remain unchanged. The first report hash was `df57775115df725eb0f0cbb5cdaa0ea5ca9ab9ad90fc4f92135df8dfad772acf`; the receipt records this correction and the revised report hash.

## Remaining art scope visible in this build

House thumbnails are accurate schematic floor plans; the Workshop card still uses table artwork; the tutorial uses book artwork. Lost Address/Parcel Hall/Parcel Service are actual room captures, with simple walls, floors and fairly sparse furniture. They are clearer and truthful but do not yet demonstrate the owner's requested dense, meticulously detailed, varied map art. The bright, detailed lobby illustration exceeds these map previews in visual richness. Preserve this distinction in progress.md rather than calling the full map/art objective finished.

The displayed skins are coherent color/accessory variations of the original character. The equipment picker uses original item thumbnails. These stills do not establish new mesh detail, BELT completion, impact sound/material pairings, held-item grips, facial action emotions, rigs, animation transitions, shop transactions or gameplay balance. Those remain in their assigned plan/evidence scope.

No new production-blocking label clipping or overlap was found in these 24 stills. Minor Escape wording and the remaining art scope are retained; this is agent review, with owner style approval pending.

## Exact original PNG roster

Every file below was opened directly through view_image. SHA-256 hashes were taken from the original PNG files; the same machine-readable roster is saved in `LOBBY-PNG-ROSTER.json`.

| Original PNG | SHA-256 |
| --- | --- |
| lobby_create_build.png | `56daaa00fbbec6539762b6df8a2ff01ff14143ff16cc96a9b1ce01a67c37de0e` |
| lobby_create_learn.png | `ef3ffdf402b9d7e6b03c25a22cbc42786c5a0b997cb8005bebd819ea710fa7e4` |
| lobby_create_wordbound.png | `79d608ac2c1f2d4e65071acfa90815cd332792aeb5ef500f186dd8638218182d` |
| lobby_customize_equipment.png | `d11fe84b38ce8a235a9ab791e5a1d097b0cfba4d2d0c13bcf9f6fa90d3161c46` |
| lobby_customize_finishes.png | `9e15e257d7197a63ce8403fb12605ed294a7c9632bbdad3e65d60db65c0a014a` |
| lobby_customize.png | `a5b1127e74c0bc81cbd943f9486b6490a83306ecdffd0306d131d27af0443872` |
| lobby_home_returned.png | `36154eb2537fecd1a772c7852312690d08ace55f3c581e3b0b83b80c2ab2dc46` |
| lobby_local_dibs.png | `a1eccc21aa1accbf732b43462da9b8cdec7a96297a2e7e1a437eed51ce357fd6` |
| lobby_local_duos.png | `de0454fef6002678c58ae9c4b427be2c6d4053ab4bc94479529924424fc6c22a` |
| lobby_local_escape.png | `bb6b70efc5721907359736a71b35187ccc9bdf23b7a8516ca27e58ea6c25ad7b` |
| lobby_online_ready.png | `0b11cc8e765dc7ff191dcd9cba4525469765bb87864f42b4adc965e30e07c5f0` |
| lobby_settings_audio.png | `604d4eb787f7bc250d2f511a67daf3e26d315cd016992e98e646bc032d450100` |
| lobby_settings_controls_bottom.png | `13196fa7e1c754daa2acd7bdef268d0c6ac8d9f4fddcf151afcde82bdf1da7d4` |
| lobby_settings_controls.png | `3db44aa332837230625fd729b54b850992849d8d0fd3de9c03750774e7b8959f` |
| lobby_settings_gameplay.png | `f12e02c7153295a618ab699010e525ff79ad8851f0ddde7a5c6b217817afd643` |
| lobby_settings_graphics.png | `9b3aa02feee9d3cb5642454e081e2cdcea58928b42a22ad8a63286970b76e0c2` |
| lobby_settings_help.png | `e0b786b6b2f261b78de5377eacf01145ab3028265064bc1dac4c98bf946df0d8` |
| lobby_settings_profile.png | `0533a65dc7aced2755be7978a6623c97c9923355a02f9a82ae3942cd005bd7b6` |
| lobby_settings_wordbound_controls_bottom.png | `4d8b95c1c56a1b305e52ba4f6d659cc951005b3cc9ac4dde868ddd0718079267` |
| lobby_settings_wordbound_controls_mid.png | `417ade106e6f1107b5ac4797abf6b0445926d6cbafdfbe75db60806df6f50f95` |
| lobby_shop.png | `caf1a5f1fb36ed67c1c779a849282af7b8a50066fcfbfcfc0182daa25d292ef9` |
| lobby_solo_freestyle.png | `268f35e9fb3de1b4ebe130726387af061ef0654f5412c319508e25e76eaad5b7` |
| lobby_solo_timerun.png | `31598b18e456617bd75e1748c93223c63fe19b6320adf935a2293e5df248ecef` |
| lobby_solo_two_seats.png | `f31d5c3da61fb8dc172d7fcab1cf796a02bf235c69c7d07a927ed851ae260896` |
