# Lobby verification

STATUS: NATIVE SOURCE VERIFIED; WINDOWS PENDING. The last delivered Windows build remains 20261026T145311Z until the Windows walkthrough passes.

The integrated change separates Local / Online / Create, retains the original art, shortens card copy, shows actual participants once, exposes Customize and Shop, labels local devices explicitly, removes the routine welcome toast, and groups settings. Narrow navigation reflows to two rows; large coin balances also participate in the width decision. Home labels are saved through Unity, with exact other-field and dependency preservation checks.

## Corrections found during verification

- Editor 153803: 7/8 pass. The blanket dirty-dependency check refused an imported Nunito source font path. No production migration was run.
- Editor 154725: compilation caught a missing Wreckabulary.Rules import in the new Windows QA scenario. The namespace was added; syntax checks alone had not detected it.
- Editor 154921: 8/10 pass. Allowing the imported main Font alone did not resolve the dirty dependency. Diagnostic 155147 identifies the actual object as UnityEngine.Texture2D, Font Texture, a TrueTypeFontImporter subasset.
- The bounded repair recognizes only a confirmed imported Font and its same-path Material/Texture2D subassets. It preserves serialized-state hashes, dirty flags, clean importer state and source/meta bytes. It never clears, saves or edits them. Authored Unity assets and dirty importers still refuse. Owned-copy positive and negative tests exercise this boundary.
- Play 155358: 19/21 pass. Shop's existing RefreshBar resized the button for the balance alone; its calculation now includes the visible Shop label, and navigation considers that width. The settings test now waits for the existing post-close input guard, rather than incorrectly requiring immediate gameplay input. The production guard remains unchanged.

Current verification is serialized through the official gate, using Unity 6000.6.4f1. Other jobs continue running. All failed runs and source drafts are retained. Baseline screenshots are in developer-workspace/lobby-capture-2026-10-26T15-22-13-199Z; 276 saved authored files were fingerprinted before the real Home migration.

Editor 155841 passes all 12 Home cases. Production migration 160028 applies and verifies exactly three Home text fields; 275 other authored files and the Home metadata remain unchanged. Repeat 160158 reports ALREADY APPLIED; NO SAVE. All 276 authored outputs match the post-migration baseline after the repeat. The original-font checks prove serialized state/dirty flags and disk bytes; they do not compare GPU atlas pixels.

The Windows scenario uses actual UI Submit callbacks and 15 requested screenshots. It checks mode summaries, navigation bounds, six settings categories, controls lease release, Back focus and the real initialized Wordbound frontend. It does not claim physical keyboard/gamepad acceptance, matchmaking or a completed online match. Original house modes remain local.

Full Play run160302 passes90/91. Its only failure is the old330/280 fixed-card-size expectation; those values were intentionally reduced to260/244. Targeted run160635 passes the corrected case and the settings capture,2/2. This completes all91 behavior checks across the full and targeted runs, not one91/91 result. The12 settings images include all six normal tabs and the simulated confirmation footer on all six. Display dimensions/mode remain unchanged. All276 authored outputs remain identical to the post-Home baseline.
