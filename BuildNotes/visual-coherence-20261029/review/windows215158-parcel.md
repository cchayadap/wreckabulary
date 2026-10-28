# Windows 215158 Parcel visual review

STATUS: COMPLETE — all eighteen actual final Windows images opened untouched. Source `4ee1d37a4e64751e093d136dd52a6d99be7aa53f`; build `20261028T215158Z`. No source/assets/native changes by this review lane. Bounded correction verified visually; broader art/first-time-user polish remains open.

- [x] Open all nine images from `docs/qa/runs/20261028T215349Z-wordbound_parcel_workshop` and record exact roster/observations.
- [x] Parent confirmed final Parcel Solo `20261028T215618Z-wordbound_parcel_service` PASS; all nine images opened and roster recorded.
- [x] Complete camera/world fixture, cutaway material depth, selection and remaining usability assessment.

Only opened original images count as reviewed. Passing smoke checks alone do not establish visual quality or owner acceptance.

## Workshop: nine original PNGs opened

| Exact filename | Observed result |
| --- | --- |
| parcel_workshop_assigned.png | Lost Address Attic floor loaded; adjoining layers remain translucent. Original larger map is still extremely sparse at overview distance. |
| parcel_workshop_saved.png | Parcel Service retains three named rooms and original props. Selected Sorting floor keeps brown wood; mint outline identifies selection without replacing its material. No floating wall dressing. |
| parcel_workshop_sorting_close.png | Drawer/shelf/crate silhouettes and planks are readable; brown floor and thin mint boundary retained. Sconces/signs/shelves intended for walls are absent from cutaway. Original interactive shelving remains. |
| parcel_workshop_playtest_world.png | Full avatar visible from head to feet, with clear floor shadow; no prior close head camera. Wall sign and sconces remain attached to the full blue wall. Red desk bodies, wood grain and gold handles visible. |
| parcel_workshop_playtest_menu.png | Equipment overlay readable, large original BAT image, recipe list and missing letters state; Return to workshop visible. |
| parcel_workshop_returned.png | Returns to the close Sorting view with selection and floor material preserved. No floating wall dressing. |
| parcel_workshop_assigned_returned.png | Separate Lost Address name/floor restored and retained-history feedback visible. |
| parcel_workshop_parcel_returned.png | Parcel draft name/selection restored, brown floors and mint border preserved. |
| parcel_workshop_lobby.png | Lobby/character returns with complete header and local-play panel. |

`checks.json` reports PASS for four scenario steps, nine 1280×720 focused captures, zero errors/exceptions/warnings. These are supporting run facts, separate from the visual review.

The specific Workshop regression is resolved in these images: wall fixtures no longer float over missing walls; full playtest still has them. Selection is clearer because it preserves the brown wood floor rather than recolouring the whole room cyan. Source lifecycle/native testing remains parent-owned evidence.

Remaining visual/UX limits: the cutaway's light props are still bright and relatively pale; it does not show the strong prop-to-floor contact shadows of gameplay, so no claim of completed lighting parity. Empty rectangular room centres and perimeter rows remain visually sparse. Lost Address at its default overview is particularly hard to read. The initial playtest view faces a wall/props rather than showing the route doorway; the checkpoint ring/text helps, but route discoverability is not proved. Equipment initially offers BAT with no letters and a disabled Craft button, without demonstrating contextual guidance to the current station. The lush lobby illustration remains substantially richer than the actual map. These are open polish/first-time-user concerns, not new smoke failures.

No obvious intentional striped background, fake window repetition, floating wall dressing or renewed giant-head camera is visible in the nine reviewed frames. This is a bounded improvement, not owner acceptance of the map's art.

## Parcel Solo: nine original PNGs opened

Directory: `docs/qa/runs/20261028T215618Z-wordbound_parcel_service`.

| Exact filename | Observed result |
| --- | --- |
| parcel_service_initial_lobby.png | Full character and vivid furnished lobby visible; main navigation/local-play panel readable. |
| solo_parcelservice_timerun_selected.png | Time Run and Parcel Service have distinct selected borders/checks. Three map cards contain actual world views; Parcel Service shows blue office walls, original fixtures and open doorway. |
| solo_parcelservice_timerun_world.png | Head-to-feet avatar, clean floor contact shadow, attached T sign and sconces. Checkpoint/Bring keepsake ring/text and Take keepsake prompt visible. Route doorway is outside this initial frame. |
| solo_parcelservice_timerun_menu.png | Original BAT model/recipe list readable, Missing ABT and disabled Craft visible, Return to lobby shown. |
| solo_parcelservice_timerun_returned.png | Returns to chooser retaining Time Run and Parcel Service selection, with correctly fitted map images and visible Play action. |
| solo_parcelservice_freestyle_selected.png | Freestyle selection changes while Parcel Service selection remains. Subtitle describes exploration/practice and bottom summary identifies one player. |
| solo_parcelservice_freestyle_world.png | Same safe camera and attached fixtures. Cyan Practice this station marker identifies the local activity. Keepsake prompt remains, so a first-time player still encounters two different cues. |
| solo_parcelservice_freestyle_menu.png | Equipment remains legible and can return to lobby; default BAT/no letters does not demonstrate a guided path to this practice station. |
| solo_parcelservice_freestyle_returned.png | Freestyle and Parcel Service choices persist on return; no missing thumbnail or overlapping selection/title observed. |

Solo `checks.json` reports PASS for all three scenario steps, nine captures and zero console errors/exceptions/warnings; root additionally confirmed normal exit and the strict eighteen-capture audit PASS. This review opened the images itself; it does not infer artwork quality from those technical passes.

The new Parcel card depicts the real office and doorway, replacing the earlier identifier-only representation. The three world views are visually distinguishable and the local/mode/map hierarchy is clear in these 1280×720 frames. However, the two older maps still look sparse/plain, and even Parcel's more legible doorway view does not itself teach the word-break/craft/delivery loop. Gameplay contact shadows and original model details are stronger than the Workshop cutaway. The initial avatar is fully visible but still occupies roughly half the image height; these spawn captures do not prove camera quality during movement, carry, wall approach or route traversal.

No new release-blocking visual regression was confirmed in this eighteen-frame scope. The fixed floating fixtures and giant-head spawn issues are absent. Further work should address purposeful room composition, the map/lobby detail gap, context-sensitive first actions and Workshop lighting parity; repeated fake windows, decorative stripes or noninteractive usable-item disguises are not justified by these images. This is not production-wide acceptance, multiplayer proof, full physical gameplay completion or owner approval of the overall design.
