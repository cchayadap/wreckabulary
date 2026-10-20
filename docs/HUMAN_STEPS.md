# Wreckabulary: human steps

Steps only the owner can take. The agent never does them; it may run the ones marked **(approval)** only after the owner says yes in chat to that exact action. Tick each box yourself when done. `kit.mjs doctor` reports the machine checks behind many of them. Below, `kit.mjs` means `node <your skills folder>/game-studio-run/scripts/kit.mjs`.

## Before any wave or autonomous run

- [ ] H1 Update Claude Code to 2.1.277 or later (`claude update`, then `claude --version`). Workflow agents pause at a usage limit only from 2.1.271; older versions lose every agent in flight.
- [ ] H2 Keep auto-continue on (`/config`, `autoContinueAtUsageLimit`) and, if you want, add `"askUserQuestionTimeout": "5m"` to your own `~/.claude/settings.json`. The kit never writes that file, and a project settings file that sets `autoContinueAtUsageLimit` turns the feature off.
- [ ] H3 Keep the machine awake on AC power: `powercfg /change standby-timeout-ac 0` and `powercfg /change hibernate-timeout-ac 0`; set Windows Update active hours. Auto-continue waits for Enter after a long sleep.
- [ ] H4 Approve the project MCP servers on the first interactive launch; sign in to OAuth servers (`claude mcp login <name>`).
- [ ] H5 Keep the Unity licence current (sign in to Unity Hub) and accept UAC prompts for Hub installs.
- [ ] H6 **(approval)** Install the experimental Unity Pipeline package: `unity pipeline install --project-path "<Unity project>"`.
- [ ] H7 Optional: install and start the Blender MCP add-on in the Blender GUI.
- [ ] H8 Create provider accounts, buy credits, and store keys yourself: in Windows Credential Manager for kit tools, or `setx` for MCP servers that read the environment. Never paste a key into chat.
- [ ] H9 Optional: decide on ffmpeg (GPL-3.0; dev machine only).
- [ ] H10 Mixamo only through your own Adobe account, downloaded into `_Inbox/Mixamo/`.

## Decisions only you can make

- [ ] H11 Licence decisions the agent cannot make (third-party packs, AI tool terms, unattended batchmode terms, your `ai_outputs` policy). The `game-ship` skill lists the open ones.
- [ ] H12 Git: whether and when to `git init` (only after the `.gitattributes` block is in place), the remote, the LFS quota, and the end-of-run history. The kit never commits.
- [ ] H13 Company and product names. Tell the agent; it applies them through an editor command.
- [ ] H19 Taste gates: pillars, style lock, golden images, feel clips, cut lines, playtests.
- [ ] H23 Trademark search before the name is public.
- [ ] H24 Privacy and telemetry: opt-in or not, and the privacy note.

## Autonomous runs

- [ ] H14 **(approval)** Register the watchdog and supervisor tasks: `kit.mjs host-tasks install --project <workspace>`.
- [ ] H15 **(approval)** Opt in for one bounded window: `kit.mjs autonomous on --project <workspace>` and `kit.mjs run start --hours N --project <workspace>`, then launch with `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\kit\host\run.ps1 -Workspace . -Name <run name>`. Stop any time by typing `OWNER: stop`.
- [ ] H16 Optional: a Dev Drive for recordings and captures (admin).
- [ ] H17 Optional: install the official Unity agent plugin as a companion (one Unity skill source only).
- [ ] H18 **(approval)** Extend the `agent-run-recovery` skill: `kit.mjs install-user --extend-recovery`.
- [ ] Optional: a status-line script that writes `.harness/quota.json` (usage-limit reset times for the supervisor). Only you edit user-level settings.

## Release (see the `game-ship` skill)

- [ ] H20 Store account and agreements, tax and bank details, and the app fee.
- [ ] H21 Builder login, store-page review, setting the default branch live, and pressing Release.
- [ ] H22 Festival or demo registration deadlines.
