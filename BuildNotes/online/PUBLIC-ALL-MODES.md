# Public matchmaking for every multiplayer mode

STATUS: SOURCE VERIFIED; FINAL NATIVE PUBLIC ACCEPTANCE NOT RUN

The approved public catalogue now has nine mode/format entries per seed, including Freestyle/Solo (shown as Co-op). Freestyle uses one shared group of at most eight humans. Fill Off starts only an existing party of at least two; Fill On packs whole parties, starts at eight immediately, or at two or more after the existing30s coalescing deadline. A lone human continues waiting. Public TimeRun Fill Off also requires at least two humans; Fill On retains the existing four-human packing rule. Private solo play remains available.

Quick Play can only join public BuiltIn matches. It cannot consume a private BuiltIn room with the same binding; private Workshop Fill remains its separate explicit consent channel. No bots, invented roster members, or single-player public fallback are added.

The public patch is verified against an isolated source overlay: committed service baseline plus only the public model/party/pool changes and affected service tests. A separate C# Rules assembly combines the current integration domain (including practice-policy hashing) with the public eligibility change; actual runtime/menu/catalogue source and tests reference that assembly. The one-line eligibility change is staged independently of upstream practice hashing, so integration preserves that existing hash contribution.

Public source is committed in `a960651`. Recovered C# evidence: 28/28 PASS (`Logs/online-public/public-csharp.trx`). On 2026-10-23, the isolated public Worker typecheck passed after restoring its omitted generated Cloudflare type declarations. The full Worker run passed 83/84; the remaining legacy model assertion incorrectly required Freestyle Fill rejection. That assertion now checks FFA rejection and Freestyle acceptance; the complete model-file rerun passed 8/8. The actual schema3 exporter was replayed and reproduced the saved fixture byte-for-byte (SHA-256 `5afe3d6a52a11538bb96b44669017816adf2ce5f37bef2790ddb72d0af768c33`).

Pending gates: root's final native nine-entry export and all-five public entry cases. The activation tooling consumes the native export; it never invents production hashes. Native public tests require exact parity with the catalogue assigned to the player launch assets. Root alone deploys the reviewed allowlist and runs Unity.
