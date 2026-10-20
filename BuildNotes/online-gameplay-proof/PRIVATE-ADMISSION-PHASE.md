# Private fixture phase correction

STATUS: SOURCE CORRECTION — NATIVE EXECUTION PENDING ROOT

`MatchPool.drain` seals every logically full reservation, including private races whose two Fill-Off parties have already joined. The earlier native helper required `FORMING` and always sent `launch`; that is correct for open Freestyle staging but rejects an already sealed race before transport startup.

The helper now accepts only `FORMING` or `ADMITTING` with the exact expected seat count, derives the exact roster binding, and calls `launch` only for `FORMING`. Both branches still require matching `ADMITTING`, generation and roster hash before original per-player readiness, certificate verification and exact whole-party membership checks.

No production allocator, admission, settings or timing behavior changed. Root authorized this narrow fixture correction as a separate source checkpoint while retaining the compiler/native slot. Native validation remains pending.
