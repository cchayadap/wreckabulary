# Wreckabulary: known issues

One table row per issue. `gate_close` refuses milestone M while any row is `open` with `recheck_at_gate` equal to M, so an issue deferred "to be rechecked later" really is rechecked.

Columns: id `KI-<n>`; severity `blocker`, `major` or `minor`; the gate where it was found; the gate where it must be rechecked; status `open`, `fixed`, `rechecked` or `wontfix`; and the evidence path.

| id | severity | found_at_gate | recheck_at_gate | status | evidence |
|---|---|---|---|---|---|
