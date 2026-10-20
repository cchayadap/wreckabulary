# Local launch composition review

STATUS: COMPLETE — READ-ONLY SOURCE REVIEW; NO BUILD OR RUNTIME CLAIM

Root requested a bounded review while the source/native compiler window was occupied. Reviewed new local MatchClient, launcher playtest flow and WordboundWorkshopPlaytest. Source pins below identify the captured draft.

Confirmed teardown edge: `WordboundLocalMatchClient.StartLocal` catches startup failure and awaits `StopGameplay`; if Runner shutdown itself fails, that exception escapes. `WordboundLauncher.PlaytestWorkshop` then unconditionally clears the local client and destroys its hierarchy. Thus a shutdown exception can discard the recovery context before shutdown is confirmed. Reported to root: preserve that context when shutdown is unconfirmed, or expose a confirmed stopped outcome and destroy only after it. The controller's cached Stop fix prevents premature completion from overlapping calls, but does not make a failed shutdown successful.

Final source recheck: root repaired that path. Component setup is within the cleanup try/catch; `ShutdownConfirmed` is true only before any startup or after successful awaited cleanup. The launcher retains the matching client and exposes its failure UI when shutdown is unconfirmed. Confirmed shutdown may destroy it and resume the workshop. The generation fence rejects stale startup completion/catch after Return or replacement. No remaining confirmed premature-destruction defect in the captured repair.

Recovery limit: cached controller Stop faults/cancellation are terminal outcomes for that instance. Repeating Return awaits the same outcome; it does not retry a failed Runner shutdown. The retained client is a safe failure boundary, not proof of automatic recovery. This limitation was sent to root with the final source disposition.

The remaining bounded checks found no additional confirmed issue: the launcher preserves the selected snapshot; creation checks source hash, seed, selected mode requirements and a strict manifest roundtrip; only explicit TimeRun/Freestyle are accepted; authored content binds item/rule sources and physical plan; a fresh one-human local binding is used without synthetic online credentials. Local startup has no HTTP/Photon Cloud authentication call. Generation/identity guards prevent a late completed startup from hiding the workshop after Return. Runtime scene/body readiness remains untested by this source review.

Final repaired source SHA-256:

| Integration file | Hash |
| --- | --- |
| `Assets/_Project/Scripts/Online/Client/WordboundLocalMatchClient.cs` | `2455C0A6A303857B490D9AD39FD6FA6BD88571C41439D9D37C376F138AB5168B` |
| `Assets/_Project/Scripts/Online/Client/WordboundLauncher.cs` | `1731A1384C9FDB19A5F8794CAAB74CA2D8DEA007FB8E15C27FFF96F1E694ACE2` |
| `Assets/_Project/Scripts/Online/Client/WordboundWorkshopPlaytest.cs` | `384EF9A678AC7A7A012CA5F7ABFC02067687AA6E844A025F35755C54AF085D46` |

The initial finding was captured at LocalMatchClient `5E30E6E9B2CE0EA5442AB2533464D326106436AA9DD2E652B9C0F6BC9097508D` and Launcher `726A12856885EED09D59CCA9DB1E49D69622B5831ACAAE5BA4AF3DAB0BDF27AE`. Native local-play and shutdown acceptance remain root's next verification step.
