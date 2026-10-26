# Independent Editor evidence review — 214606

STATUS: COMPLETE. The exact native Editor run passes all 27 selected cases, with zero failed, skipped or inconclusive cases. This review ran no Unity/native job and changed no source, generated assets or root receipts.

- XML: `TestResults/editmode_20261026T214606Z.xml`, SHA-256 `275ea622a63f6cae014281280718f119f4474ac0087f4b4ea03755e04fb3229f`.
- Log: `Logs/gate_test_editmode_20261026T214606Z.log`, SHA-256 `3dd94ef2d7786311fa68237f6fd0ca6bbe088f634d23ff61a6208b725bd1f8d1`.
- Exact filter: `PlaySettingsFrameAuthoringTests|WorkspaceEditorTests`. Log ends with test-run exit code 0.
- XML execution time: **10.3748 seconds**, 21:46:37–21:46:47 UTC. Root's reported **45.5 seconds** is the larger gate wall time; these are different measurements.

## What this verifies

The three saved-frame authoring cases all passed. They create real owned Unity assets; edit/save/reopen the frames and profile; retain font/material and transform values; replace only their own Close callback; and repeat missing-only setup with exact asset/metadata byte, file-set and GUID checks. Malformed aliases, nested dynamic regions, hidden Close ownership, unsafe sizes and occupied/traversing destinations are rejected. The corrected malformed-clone case unpacks only owned preview instances, checks the actual new parent and rechecks saved prefab/metadata bytes after cleanup.

The 24 Workspace Editor cases pass, including all seven one-shot preview state cases. Play and Settings each cover a null or saved previous Play entry. These exercise the production entry/restore handlers under explicit synthetic state transitions and preserve authoring scene/settings state; they do **not** open a physical interactive preview and inspect its rendered controls.

All 15 live frame implementation/test files match their reviewed candidates after newline normalization, with the reviewed test-only `6b8e1182` correction substituted for the original fixture. The complete raw hashes are in `independent-editor-214606.json`. No runtime validator, production asset path or assertion was removed to obtain this pass.

## Preservation and limits

The root preservation receipt's baseline hash and scope were checked, and every one of its **4,190 original entries** was independently rehashed at review time: no changed or missing files. This is the recorded authored-asset/metadata/project-settings set, not a claim that all source files are unchanged or that new setup outputs have been validated. New files are outside that original-file comparison.

The earlier 213620 XML/log hashes still match the preserved failure evidence. That run remains 25/27; it is not relabelled as passed. Its linked-prefab reparent no-op is explicit in the old log. The revised shorter-path fixture now passes the unchanged byte-read checks, supporting the temporary path diagnosis without claiming a separate OS-level investigation.

Permanent setup, repeat preservation of newly created frames, actual Play/Settings layout/focus/navigation, real editor interaction and a rebuilt Windows walkthrough remain separate gates. The developer guide draft will be updated after root supplies those final outcomes.
