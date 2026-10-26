# Delivery verifier revision 4: control legends

STATUS: REVISION 4 / HEADER FOLLOW-UP FROZEN; no actual build, player or delivery verification has been executed by this draft.

This is a separate version of revision 3. Its parent verifier, tests, prior receipts and verified Windows images are unchanged. Use this file directly for a fresh candidate; do not replace the parent script while reproducing an older delivery.

The exact expected evidence is **22 lobby screenshots across the same five steps, plus nine solo screenshots across the same three steps**. The initial revision-4 draft is preserved in `revision1/`. The original 20 lobby names are preserved in order; the new Wordbound mid/bottom controls views follow `lobby_settings_controls_bottom` in Settings. All source/build/run/log/hash, preservation, PNG, mandatory mode-readiness and cleanup checks from revision 3 remain.

New mandatory Settings probes match the reviewed QA candidate:

- `controls_legend_Wordbound_mid`: Wordbound identity and visible `WORDBOUND CONTROLS` header, exact capture and selected Spell-row IDs, 14 active bindings, no rebind, active lease, and fully visible Aim/Jump/Interact/Spell rows with exact labels and RB/A/RT/Y mappings.
- `controls_legend_Wordbound_bottom`: the equivalent exact bottom capture/selected Pause-row proof, Equipment 1/2 with controller dashes, R3 gear cycling and Start/Options.
- `controls_legend_bindings_preserved`: shared source/map identity, exact override preservation, unchanged equal finite integer binding versions, unchanged mouse settings/map enablement, no listener, restored House legend.

The verifier rejects absent probes, earlier 20-capture lobby runs, wrong mode labels/mappings, clipped required rows or changed binding state. It retains the raw probes in the final receipt. A matching family probe is source-produced evidence, not image recognition; an independent reviewer must inspect the new images themselves.

Run syntax and synthetic validation without Unity:

```powershell
node --check BuildNotes/completion-20261027/windows-delivery-draft/revision4/verify-windows-delivery.mjs
node --test BuildNotes/completion-20261027/windows-delivery-draft/revision4/verify-windows-delivery.test.mjs
```

All 28 earlier cases are retained; eleven focused controls/capture/header rejection cases are added. Producer result: **39/39 PASS** (fresh timing recorded in the manifest). Synthetic images and fake binaries are temporary fixtures and are never game acceptance evidence.

CLI arguments remain explicit pairs: `--root`, `--build`, `--source-commit`, `--build-gate`, `--lobby-run`, `--lobby-gate`, `--solo-run`, `--solo-gate`, `--preservation`, `--out`. A fresh output path is required; overwrite is refused. Input evidence is read-only. No service or native process is launched.

Provenance limits are unchanged: `build.tree` is the harness path/size/mtime metadata freshness fingerprint, not a content hash; Git comparison excludes ignored production inputs. The smoke format has no historical per-run executable hash. Current artifact hashes and exact run/build identifiers are recorded, but no stronger historic binary identity is inferred. PNG shape/focus probes require visual review. No physical controller interaction, story introduction, completed match, new network acceptance or full production release acceptance is inferred.
