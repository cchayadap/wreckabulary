# Wreckabulary: change requests

Every owner message that asks for a change to scope, look, feel or priorities becomes a change request here, so it survives compaction and has a cost attached.

Entry format: a level-2 heading `CR-<n> from inbox #<m> <UTC>`, then:

- `request:` the owner's words, quoted (short).
- `spec delta:` the exact change to `Docs/TASK.md` (section and new text or row).
- `affected gates:` the rows or milestones it changes.
- `cost:` time against the time box, spend, and what slips.
- `tier:` P0-fix, P0-add, P1 or P2.
- `status:` proposed, confirmed, done or declined.

A request takes effect after the owner types `OWNER: confirm CR-<n>`. It moves ahead of listed P0 work only when the owner says so.
