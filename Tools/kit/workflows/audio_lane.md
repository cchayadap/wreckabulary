# Audio lane brief (game-audio)

Paste this under the unit protocol when an orchestrator launches an audio lane in a build wave. The protocol itself (the lane marker, progress file, done marker, resume and fix-pass rules) belongs to `game-studio-run` and `agent-run-recovery`; follow it as written there. This brief only says what the audio lane must deliver.

WoO's audio lane ended with only a plan [C, WoO report]. This lane is not done until the files and checks below exist and pass.

## Inputs

- `Docs/audio/AUDIO_SHEET.md`, or the template from the `game-audio` skill if the sheet is missing.
- The design doc and the milestone's gate card.
- Owned paths as the wave manifest assigns them. Typical ones are `Docs/audio/**`, `ArtSource/audio/**` and `<U>/Assets/Audio/**`, but never the mixer asset while another lane has the editor.

## Deliverables (all required)

1. The sheet has a row for every event the milestone needs, and no row is left `TBD` for this milestone.
2. The files come from synthesis, CC0 fetches or approved libraries, in that order (`game-audio` skill, procedure step 2). Any generated file goes through `assetgen`.
3. The checks' JSON is saved in `Docs/audio/checks/`: `audio_manifest.json`, `audio_check.json`, `audio_check_loops.json` (when loops exist), and `routing_unassigned.txt`. When the editor is available to this lane, `import_audit.json` too.
4. `Docs/qa/runs/<stamp>-audio_pass/audio_pass.json` comes from the summary command in `audio_gate_rows.md`, recorded with `probe_report.mjs --row` when the card has the row.
5. Every audio file has a licence row, and `Docs/audio/licence_pending/` is empty.
6. The game code calls `AudioEventProbe.Fire` for each sheet event that exists in code, and the event-id list is written for the QA scenario's `Expect` call.

## Done marker fields (in addition to the protocol's)

- `files`: every audio file, sheet and check output written.
- `checks`: the `ok` value of each check file, and the `audio_pass` verdict.
- `requests`: anything the lane could not do, such as a mixer group that needs the editor, a human download, or an owner decision (Sonniss H11, a paid plan). Never report such an item as done.

## Not this lane's job

- The owner's listening sign-off (D7): list what changed and stop.
- Mixer edits while another lane holds the Unity editor (one writer per shared resource).
- Changing probe thresholds (owned by `game-qa-probes`), or editing `Docs/licenses.csv` by hand (owned by `game-ship`).
