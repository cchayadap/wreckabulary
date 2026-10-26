# Independent BAT screenshot orientation review

STATUS: SOURCE REVIEW COMPLETE — no source blocker found for a bounded native test. Native compilation, the five Editor cases and an actual Windows sequence remain pending in this review.

Reviewed patch SHA256: `53f0e612083b7a3be105e83c72a28edb9696aef440514048442c9976dc3328cd`.

- Capture candidate: `f5d3387d93077e91f63b5b5f4515f314f29fd5c1f18c20dead9ac1344c78b9a7`.
- Test candidate: `33932bcfbc71fd385df4730de8475e6736ef97c43ec311defd5dcc8c473f9da2`.
- Independently verified both hashes, strict forward `git apply --check --whitespace=error`, and C# 9 Roslyn syntax with `UNITY_EDITOR`, `GSK_QA`, `UNITY_INCLUDE_TESTS`: zero syntax errors in both files. This is syntax checking, not Unity type checking or test execution.

## Confirmed behavior

1. Calibration runs immediately after the baseline screen copy at the same `WaitForEndOfFrame`, before subscribing/submitting Attack. It checks the recorded frame, sample count/phase, dimensions and absent command. A failed, mismatched or ambiguous calibration sets the existing error and prevents Attack submission.
2. Every RGB channel is compared for both identity and vertically mirrored row indices. The selection requires exactly one orientation with mean delta at most 1 and maximum channel delta at most 2. A symmetric image, a color-transfer mismatch or two failing candidates cannot silently select a flip. Alpha is deliberately not the orientation metric; the original colors and alpha are retained in export.
3. The reference is the existing upright native screenshot path, captured without an intervening yielded frame. Its unmodified PNG, hash, frame, dimensions through native texture validation, errors, chosen mapping and calibration duration are evidence. It uses `FileMode.CreateNew`, so an existing reference cannot be overwritten.
4. The eight existing GPU motion buffers and accepted command/phase/grip/camera/time checks are unchanged. Baseline readback and reference encoding happen before the attack. During the attack, capture still performs bounded screen copies; the measured row swap is applied only while saving buffers after capture stops. No synthetic pose, repeated attack, camera rotation, recoloring or tolerance change is introduced.
5. `RenderTexture.active` is restored in `finally`; both temporary Texture2D instances are destroyed. Persistent motion buffers retain the existing disposal owner. The calibration allocation is bounded by the validated window size; its additional two texture arrays and PNG encoding still have a one-time memory/time cost and are not claimed free.
6. The five tests exercise identity preservation, vertical restoration with an odd middle row, ambiguous symmetry rejection, RGB mismatch rejection, and unverified/incomplete-array rejection. Reflection intentionally reaches the existing Editor-loaded QA assembly without modifying either asmdef. These tests do not prove a platform's screenshot orientation or GPU synchronization behavior.

## Required native acceptance

- Compile and pass the five Editor cases through the official gate.
- In a fresh Windows equipment run, require one unique calibration choice within both thresholds, same frame and before-Attack true; inspect the upright reference against the exported baseline.
- Confirm the original receipt/phase/motion/grip assertions still pass for the same single attack, with all eight phase images upright and the usual expected output dimensions. Confirm baseline/attack images contain the actual game view and HUD.
- Preserve the already captured inverted run as evidence. A fail-closed calibration should be investigated with its recorded RGB errors, not resolved by loosening thresholds or unconditionally forcing a flip.

This reviewer ran no native process, made no live Assets edit and changed only this review file.

## Reviewed follow-up: let calibration finish before the attack frame

Root identified a timing risk after the first source review: synchronous baseline comparison/PNG encoding could enlarge the next frame's delta and hide a short windup. The final patch `4b6fa4024b8b9a6be5dbd5ec6294ba961adb78cff779247464498a33651e7bcf` adds one ordinary `WaitForEndOfFrame` after successful calibration, before the separate subscription/Attack block. It records the later submission frame/time and gap from the preserved baseline, and fails if the frame did not advance. Failed calibration still skips both the wait block and Attack.

Final capture source hash: `fc6aff1d2a2f97ca0633120dea810763a8a8e64f2b378d4cdc1ecc73c2a1e194`; test file is unchanged. Independently rechecked exact hashes, strict forward apply and C# 9 syntax: PASS, zero syntax errors. No time scale, capture delta, tick rate, attack deadline or phase acceptance threshold changed. Source is clear for the bounded native test; actual windup capture must still pass.
