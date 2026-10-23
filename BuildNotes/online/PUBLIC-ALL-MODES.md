# Public matchmaking for every multiplayer mode

STATUS: IN PROGRESS — SOURCE GATES RUNNING; NATIVE PUBLIC ACCEPTANCE NOT RUN

The approved public catalogue now has nine mode/format entries per seed, including Freestyle/Solo (shown as Co-op). Freestyle uses one shared group of at most eight humans. Fill Off starts only an existing party of at least two; Fill On packs whole parties, starts at eight immediately, or at two or more after the existing30s coalescing deadline. A lone human continues waiting. Public TimeRun Fill Off also requires at least two humans; Fill On retains the existing four-human packing rule. Private solo play remains available.

Quick Play can only join public BuiltIn matches. It cannot consume a private BuiltIn room with the same binding; private Workshop Fill remains its separate explicit consent channel. No bots, invented roster members, or single-player public fallback are added.

The public patch is verified against an isolated source overlay: committed service baseline plus only the public model/party/pool changes and affected service tests. A separate C# Rules assembly combines the current integration domain (including practice-policy hashing) with the public eligibility change; actual runtime/menu/catalogue source and tests reference that assembly. The one-line eligibility change is staged independently of upstream practice hashing, so integration preserves that existing hash contribution.

Pending gates: focused C#/Worker tests, regenerated actual schema3 interoperability bytes, source commit, root's final native nine-entry export and all-five public entry cases. The activation tooling consumes the native export; it never invents production hashes. Root alone deploys the reviewed allowlist and runs Unity.
