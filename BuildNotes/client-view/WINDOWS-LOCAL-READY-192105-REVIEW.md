# Windows local-ready capture — 192105Z

Status: COMPLETE — bounded visual/evidence review. Independent read-only inspection of the actual integration PNGs and `checks.json`; no rendering, player execution, generation or source changes in this lane. The scenario's recorded overall result is FAIL, so this visual observation cannot close Windows acceptance. It is not owner approval or production-quality acceptance.

## What is actually visible

`wordbound_local_ready.png` is a genuine, nonblank 1280×720 gameplay view. It shows the original rounded avatar from behind: red coat/hood, black satchel with a small contrasting clasp, dark boots/trousers and pale cuffs. The silhouette, separated clothing and contact shadow read clearly; this is consistent with the source's retained `Avatar/Avatar` mapping. Face, glasses, front clothing, animation, held equipment and alternate colour choices are not shown.

The room has a brown floor/ceiling, pale walls, a doorway, overhead green structure and a yellow checkpoint ring/post. The HUD visibly reports Time Run, Lap 1/3, Checkpoint 1/32, the keepsake objective, 24:59 remaining and health/stamina100. The lower panel shows the empty letters line and interaction/crafting/throwing guidance. There is no black frame, obvious magenta missing material or detached avatar geometry in this view. The screenshot alone does not establish model identity for every offscreen item or verify lighting/geometry in other rooms.

## Visible issues still worth reviewing

- The avatar occupies approximately half the image height and covers much of the forward centre. This is a substantial camera composition choice; traversal/obstacle visibility needs moving footage before deciding whether to change it.
- The room is sparse, with broad plain surfaces and simple structural forms. This frame does not demonstrate the requested detailed furniture/equipment work or a finished environment.
- The top-left and bottom HUD panels reserve considerable empty space. Their text is readable in the actual capture, but the action guidance gives verbs rather than explicit keyboard/controller bindings, leaving discoverability unproven.
- The yellow checkpoint text crosses its vertical yellow post. The smaller angled room narrative is harder to read from this camera. Those are actual composition/readability observations, not evidence that gameplay sensors or labels are broken.

The workshop and returned-workshop PNGs were also opened. The Attic fills the viewport, eight room labels are separated, mint outlines stay inside the map in this state, and the footer says “Preview ready · exact draft and seed.” Both images are byte-identical to each other and the reviewed `190422Z` workshop capture. This corroborates the earlier static improvement, but adds no hover/drag/zoom/other-floor or human navigation evidence.

## Recorded checks and limits

The actual `wordbound_workshop_flow` result is FAIL. Open, edit/autosave, return and undo/redo pass; `exact_playtest` times out after its unchanged90-second budget. It reached readiness at6.610723 seconds, Unity frame14 and canonical tick60, then entered the required90-update-frame wait. At return entry it had waited only53 frames, elapsed91.085537 seconds, frame68 and tick3300. It still reported a client, active controller/world/camera, readiness, no pause, no gameplay/guide/presentation fault, one marker and93 visuals. These are recorded observations, not proof of responsive gameplay or a sustained frame rate.

The single immediate-ready PNG passed the minimum nonblack/alpha/varied-colour checks. The later world/equipment captures were not reached. The return step confirms the same history and unchanged draft with client/camera destruction; undo/redo exactness passes. Autosave records183314 bytes, primary exact, no backup used and zero temporary files. Console counts are errors0/exceptions0/warnings0. The timeout remains a real delivery blocker despite successful readiness and this visible image.

Evidence is under integration `docs/qa/runs/20261023T192105Z-visible-wordbound_workshop_flow/`; player observations are in `Logs/player/20261023T192105Z-visible-wordbound_workshop_flow.log`. Build receipt `Builds/qa-20261023T191831Z/build.json` identifies release-qa, Unity6000.6.3f1, build HEAD`62450eb060e293d20a1ff1dc57e3263338295777` and successful build, while checks themselves have null commit/tree. This was the separately recorded visible launch, not proof of the still-unapplied gate smoke proposal.

SHA-256: local-ready PNG `6BF415AD5D9E71977718D611F0B05C6F8CA25D1F21AA4A40EC6908F14A386778`; checks `E12FD34D3D7AA1C7379BB75BE95269B9569A47D5CD36201EA9BF6B296D1FD696`; both workshop PNGs `AD72EBA2592686855CB8E79D0156CA3C76E17E10B78BE01D0CAEF85D3D7BE8D0`.
