# Reachable scroll capture review

STATUS: COMPLETE for the bounded source review. Windows execution pending; no production/native writes by this reviewer.

Reviewed root's current three-file diff after the original Workshop000027 audit rejected floor scroll requested639/actual604.8801. Old checks and verifiers remain unchanged.

| Source | SHA-256 reviewed |
| --- | --- |
| Assets/_Project/Scripts/WordboundQa/WordboundWorkshopQa.cs | bf089447c5caafb7a43e48bacea2ef128048fd5e828517300d4ea587ca65ff83 |
| Assets/_Project/Scripts/Workshop/WordboundWorkshop.cs | f3baf83633f421147a5bb774e98cbddd0861a54649deba8ff44d5659c966f5cc |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopInspector.cs | 8c3fa0f445a090278759dfaf7711ea4de662811880d78ce39ca88fc55a883475 |

No blocking source finding in this bounded change. QA first attempts the actual rendered target anchor, waits three frames, and records both the original anchor request and the measured reachable offset. It then deliberately issues that measured offset as a second request, waits three fresh frames, and requires exact stability plus `anchorOffset >= 0` and `anchorOffset + RowHeight <= measured scroll rectangle height`. Thus requested/actual fields describe a real second request instead of relabeling the first mismatch. Target visibility is independent of equality.

The scroll rectangle is read after EndScrollView on Repaint. It is transient derived UI geometry and is not serialized or part of draft/history. Existing field restoration, iterator disposal, deadline, history/revision/seed preservation and capture checks remain intact. No harness or verifier tolerance was relaxed.

Limits: this source review does not prove the measured GUILayout rectangle or final image at runtime. A fresh normal-window capture and unchanged strict audit must confirm it; the failed235645/000027 receipt is retained. The bounded capture is view-only and still does not establish physical scrolling input.
