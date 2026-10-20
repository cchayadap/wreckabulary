# Wreckabulary: decisions

Append only. A logged decision replaces a question to the owner: decide, write it here, continue. The owner can reverse any entry through a change request.

Entry format: a level-2 heading `D-<n> <UTC> <title>`, then four lines:

- `decision:` what was chosen.
- `options:` the alternatives considered.
- `why:` the reason, with the evidence file or measurement.
- `reversible: yes` or `reversible: no` (and the cost to reverse).

Number decisions in order (D-1, D-2, …) and never renumber.

## D-1 2026-10-20T09:32:00Z Preserve the approved scope and owner authorization

- decision: The approved `docs/wordbound/PLAN.md` remains the product specification. Use the kit's verification tools, but its newly installed example spec and default owner prompts do not supersede the user's implementation request or approved commit workflow.
- options: Reinterpret generic bootstrap templates as a new approval process, or retain the user's concrete approved plan.
- why: The user attached the plan and explicitly requested implementation and a goal. The integration worktree and existing dirty checkout are recorded in `docs/wordbound/STATUS.md`.
- reversible: yes.

## D-2 2026-10-20T09:32:00Z Use native collaboration with exclusive path ownership

- decision: Use this session's available collaboration agents and separate Git worktrees. Root is the only Unity integrator. Keep durable lane reports and verification evidence.
- options: Native collaboration, or the kit's unavailable Claude-specific Workflow runtime.
- why: The available tool surface supports collaboration agents; importing toolkit files does not make another agent runtime callable.
- reversible: yes.

## D-3 2026-10-20T09:43:22Z Adopt existing project requirements instead of template extras

- decision: Keep the approved plan's acceptance list. Generic kit checklist items without owner-defined targets remain recorded limitations: no fixed autonomous time window or token budget, no provider spend bands (built-in image tool only), no interruption drill, no preapproved camera manifest or trailer, and no invented owner visual acceptance. Add gameplay/probe specifics as their implementation lands.
- options: Invent extra delivery requirements, or retain the concrete approved specification with explicit evidence limits.
- why: docs/wordbound/PLAN.md defines this expansion; generic bootstrap templates are not a replacement brief. Runtime, network and usability verification remain mandatory.
- reversible: yes.
