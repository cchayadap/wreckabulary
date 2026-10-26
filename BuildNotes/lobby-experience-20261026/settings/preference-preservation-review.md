# Settings test and capture restoration review

STATUS: SOURCE REVIEW COMPLETE after revision 2 correction. No native execution or preference reads/writes by this reviewer; only source and file-hash inspection.

Root identified the first-draft TurnHintKey type error; production LobbyMenu.Turning writes `SetString(TurnHintKey, "seen")`. Revision 2 moves it into StringKeys. The incorrect draft is retained under revision1 and is not the approved native candidate.

The four-case test fixture now matches production storage types:

- Strings: four GraphicsOptions keys (Save writes invariant string scale/cap and string AA/shadows), career, outfit, turn hint, sound pack, keyboard override JSON and legacy backup JSON.
- Ints: muted, V-sync, damage/points visibility, theme effects and mouse inversion.
- Floats: global volume and mouse sensitivity.

Each array snapshots key existence independently of value. Teardown restores values using the same PlayerPrefs type or deletes only keys absent before the fixture. Restoring live mouse sensitivity/invert uses existing setters before restoring their original stored values and existence. Original input-map override JSON is reloaded after clearing test bindings. Live AudioListener.volume and V-sync are restored; GraphicsOptions reloads the restored preferences. The fixture does not claim to preserve arbitrary temporary graphics globals that differ from preferences, and it does not touch real resolution/fullscreen mode. Scene/test-state reset follows the existing project test convention.

The audio test checks a real mute/pack/volume mutation and Reset callback, then verifies feedback, theme motion, career/outfit, binding JSON and all graphics key existence/values remain unchanged. The display test only injects pending state and verifies rectangle separation plus unchanged callback/deadline across tabs and Keep cleanup. The scroll test changes real V-sync and checks both saved value and selected control/scroll retention after rebuild and tab return. Existing ControlsSettingsTests remain necessary for actual key capture/cancel/swap and lease lifecycle.

The focused capture reuses the existing LobbyPreferenceScope (string career/outfit/turn-hint/theme restoration and owned LobbyMenu disposal), adds typed keyboard/mouse preference and map restoration plus live volume/V-sync, and never invokes a settings mutation except tab selection. It clears the injected pending state before teardown so no real Screen.SetResolution revert runs. The countdown reset is solely presentation for each simulated image, not production timer behavior. A fresh child folder preserves earlier images; capture receipts explicitly mark simulatedDisplayPreview. No native render, timeout or physical-controller acceptance is inferred from these source checks.

Current exact candidate and live-byte comparisons are recorded in integration-source-proof.json. Working-tree CRLF and candidate LF may have different SHA values while normalized source remains identical. Root reported integrated R0 153706 PASS; native tests and visual outcomes were still pending at this review.
