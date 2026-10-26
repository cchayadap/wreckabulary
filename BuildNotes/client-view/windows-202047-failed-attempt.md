# Windows build 202047: failed delivery attempt

STATUS: PRESERVED FAILURE — no delivery promotion.

Build `20261026T202047Z`, source `c44a39a0e1b81b524c691f2a1f5dda5de31e3d6a`, completed its release-QA build gate. Lobby run `20261026T202433Z-lobby_experience` passed 5/5 with 20 captures. Solo run `20261026T202643Z-wordbound_solo` passed setup and TimeRun, then failed Freestyle's 12-second combined camera/simulation/guide readiness wait. It exited 1 normally; the external harness did not time out. Both runs recorded zero console errors/exceptions/warnings.

TimeRun produced four stage captures, 7 observed camera callbacks and 134 canonical ticks with one human/Single runner on the installed 72-site flagship. Its journal count was 0, an observation rather than story-introduction acceptance. Both gameplay steps reported the owned client released, original input bindings restored, and source/preferences preserved. Freestyle retained `QaFailException` in its cleanup record; cleanup does not turn the failed step into a pass.

The built QA waited for `cameraFrames >= 3 && tick > startTick + 5 && guide.MarkerCount > 0`. The failed run did not record terminal values for each operand, so this evidence cannot isolate the cause. Later mode-aware QA changes require a fresh build/run and do not retroactively validate this attempt.

The original authored-file verifier passed 4,190 file/metadata/settings comparisons with no changed paths. Its exact baseline hash/count/source and result are preserved in [the durable receipt](windows-202047-failed-attempt.json), alongside build/gates/logs/checks and 26 actual capture hashes. This receipt does not rerun native tests, replace failed images or promote the build. Current binary hashes do not establish historic per-run byte identity absent from the smoke format.

No full completion, physical-device, multiplayer-match or production-release acceptance is claimed. Creator coverage here is menu selection only; online coverage is its actual initialized frontend.
