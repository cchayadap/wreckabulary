# Windows equipment review — 145634

STATUS: PASS for bounded actual-Windows grip/art regression review. Agent-reviewed improvement; owner visual acceptance and broader action quality remain pending.

Build `20261026T145311Z`, run `20261026T145634Z-wordbound_original_equipment`. Independently opened both held-item stills and all eight BAT motion frames. The checks report all six assertions PASS and zero console errors, exceptions or warnings.

## Actual visual findings

- Held BAT remains upright/outward with its handle entering the original mitten. Held HAMMER retains a visible blue/silver/gold head and short shaft above the hand. No obvious floating gap, reversed grip or head/hood clipping regression appears in these stills.
- BAT body and item advance together through the sampled windup, active swing, recovery and return. Active/recovery frames retain a visible connection to the hand and a clear silhouette ahead of the torso. The completed frame returns to the same outward hold. There is no stale-body/independently moving weapon defect like the rejected Editor preview series.
- Windup places the hand and part of the bat near/behind the head in this three-quarter QA camera. More of the bat is visible in the current sampled windup than in run134114, but the canonical/interpolated sample ages differ. This is not evidence of a new pose improvement or removal of all occlusion. The separately corrected 18-view diagnostic found no obvious sampled-pose penetration blocker; it does not replace continuous Windows inspection.
- The original cream face, red hoodie, dark mittens/boots and original weapon materials remain recognizable. The large cropped foreground bat is a separate map prop. The rounded mitten supports a stylized grip; articulated finger wrapping is not shown.

## Receipt verification

All eight motion image hashes independently match `equipment-bat-motion.json`, SHA-256 `334cfbc9eda2cf921828a35b8de8ecfbe7908808ba0119e49ca690e11e93c0e7`. Baseline and same-frame upright reference are byte-identical, SHA-256 `0a5e17649b05bedf9047aa3ca896db6b31d17ff46f0000e796318b003d6cd0ab`. Measured vertical-row calibration has RGB mean/max delta 0; identity orientation fails that comparison. Calibration precedes Attack and one natural frame separates calibration from submission.

All eight grip separations are 0 m. Independent normalized hand/grip rotation-axis comparison gives minimum dot `0.999999999999037`. Camera/FOV, epoch and held entity remain stable across the sequence, and the camera matches134114. All recorded player/weapon bounds are inside frame. Thirty-nine canonical callbacks have zero missing ticks. Windup/active/cycle timings remain 11/7/37 ticks; no timing or authority change is inferred.

The current sampled ages are windup4/6, active11/12, recovery18/19 and complete37, versus134114's 2/5,13/16,19/21,38. Pixel differences must therefore not be treated as a matched-time A/B test.

Held BAT SHA-256: `1332981bfed0fe2630274cc0f58fe50b0dad9b99b24f4137bbb18b8af3991417`.
Held HAMMER SHA-256: `539a3c753534c3afec74d628713710ae3eebed86d0533a55b110b2f59cb34a92`.

## Images reviewed

- `wordbound_original_held_bat.png`
- `wordbound_original_held_hammer.png`
- `wordbound_original_bat_motion_00_baseline.png`
- `wordbound_original_bat_motion_01_windup.png`
- `wordbound_original_bat_motion_02_windup.png`
- `wordbound_original_bat_motion_03_active.png`
- `wordbound_original_bat_motion_04_active.png`
- `wordbound_original_bat_motion_05_recovery.png`
- `wordbound_original_bat_motion_06_recovery.png`
- `wordbound_original_bat_motion_07_complete.png`

Scope: local Windows QA inspection camera, original BAT motion and HAMMER static hold. This does not approve all weapons/actions/skins, remote-player animation, continuous input feel, normal gameplay-camera readability or performance. No new pose correction is indicated by these samples before delivering the candidate for owner playtesting.
