# Final native evidence review

STATUS: COMPLETE — no stale current-file proof or unexplained hash mismatch found. Focused native evidence passes; Windows build/smoke and visual acceptance remain separate.

Scope: `final-native-20261026.json`, `tuned-expressions-20261026.json`, their pinned receipts and current files at checkout `304108f5dc8fa3b7c59d26a44e99af6614aa633e`. No source/native actions or evidence rewrites. This document is the only permitted write.

Next: recompute every referenced proof hash/size, validate actual NUnit totals and tuning JSON/meta receipts, distinguish historical before-state from current-state comparisons and inspect any newline-only drift.

## Confirmed first pass

- All 47 direct path/SHA-256/size proofs in final-native-20261026.json match current disk bytes; no missing files or mismatches.
- All 35 direct proofs in tuned-expressions-20261026.json likewise match exactly.
- Actual NUnit XML totals and test-case counts match the summaries: Edit142701 80/80, Play143003 19/19, Edit143633 52/52, earlier tuned-profile Play140057 17/17; zero failed/skipped in each.
- The saved tuning receipt reports an applied/reloaded first save; the repeat reports verified without save with identical before/after profile hashes. Their referenced receipt files match the aggregate proofs.

Nested capture inputs, exact JSON delta/current profile verification and working-tree versus committed line endings remain in progress below.

## Final checks

| Evidence | Independent result |
| --- | --- |
| Final aggregate | SHA-256 `366a19f2e9a772245f021cb839f32ecdae90cc4d7987ba5d35d243bea42365d7`, 15,875 bytes; all 47 direct proofs exact. |
| Tuned-profile aggregate | SHA-256 `aba80bb2269ec8c724c1fdce0f428455a6093a5d864a50b367428c8d1e8671a0`, 13,153 bytes; all 35 direct proofs exact. |
| Tuned capture03 | All ten image hashes exact. All 322 before/after input pairs agree and resolve to matching current content, including four package paths described below. |
| Final holding capture | All 18 image hashes exact. All 322 input pairs agree/current content matches. All 220 native memory-proof before/after fingerprints and dirty flags agree. Preservation/owned cleanup flags are true. |
| Holding measurement checks | All frames satisfy unchanged framing and requested skin-refresh settings. Maximum socket distance `1.4901161193847656e-08` m; maximum recorded grip angle 0; maximum paired-bake world-point mismatch `6.224691446732322e-07` m. These are diagnostic measurements, not triangle clearance or visible motion acceptance. |
| Shared tactile resources | Native receipt starts/ends at zero scopes, zero materials and zero textures; temporary peaks are two materials/six textures. Cleanup verified true, foreign-scope retention false. |

The four apparent filesystem misses were Unity virtual package paths, not absent asset contents: AssetVersion.cs, Lit.shader and their metadata under `Packages/com.unity.render-pipelines.universal`. The active packages-lock entry is builtin URP 17.6.0, and the sole matching local cache is `Library/PackageCache/com.unity.render-pipelines.universal@b0678dfc9e21`; its package.json also says 17.6.0. All four exact cached-file hashes match both capture receipts. A verifier that joins the virtual paths directly to the project root would wrongly report these four as missing; use the resolved package cache without rewriting the historical receipts.

Both tuning receipts' embedded old/new JSON hashes were recomputed. The first JSON delta consists of exactly three values: Idle BrowRelax 55→75, Hurt BrowRelax 4→18, Hurt Blink 38→60. No other serialized pose, natural-blink flag or timing differs. Its after-file hash and unchanged metadata hash match the current saved profile/meta. The second receipt has no JSON delta, identical before/after bytes, saveAttempted=false, reloadedVerified=true and idempotent=true. The first beforeHash is intentionally the historical untuned profile, not a current-file mismatch.

## Normalization and limits

The 11 final source proofs all match working-tree bytes exactly. The saved expression asset also matches commit304108f5 byte-for-byte. Ten C# files use CRLF in the working tree and LF in the committed blob; every one is identical after newline normalization. No source-content difference, stale hash, or proof that accidentally hashed normalized bytes while describing current raw bytes was found. The inspected source paths are Git-clean.

The aggregate's held capture execution started at 14:38:36 UTC; `143859915` is the unique capture-folder timestamp, not a contradictory test-start time. Final status correctly says focused native PASS with Windows pending. The 80/19/52 suites and earlier 17-case suite are distinct runs; their totals must not be summed into a count of independent gameplay behaviors.

This review checked disk proofs, actual XML and receipt consistency. The native memory/scenes/global flags are recorded results from root's runs, not a fresh Editor memory inspection. It did not view the final held images or rerun Unity, Windows, networking or builds. Existing action/multiplayer/visual limitations in the aggregate remain valid. No receipt, asset, source or test was changed; only this review document was written.
