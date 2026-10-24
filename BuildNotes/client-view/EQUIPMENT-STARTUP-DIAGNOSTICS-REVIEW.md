# Equipment startup diagnostics independent review

STATUS: IN PROGRESS; awaiting Assets' stable QA-only checkpoint. No source edits or jobs.

Root approved same-sample first-ready/capture canonical, Rigidbody, visual and owned-collider observations, bounded early samples/max rise, and a strict settling criterion within unchanged deadlines. Required accounting: 30 distinct fresh observed frames spanning at least 30 advancing canonical ticks, one epoch and monotonic ticks; unchanged ticks do not count; missing/regressing evidence resets; larger positive gaps are recorded. Required physical bounds include speed at most 0.05 m/s, native collider bottoms within 0.025 m of the exact room floor, canonical/native separation within 0.025 m, and no actual penetration.

Review will check these observations are genuine read-only samples, preserve raw failure evidence, and do not introduce movement/physics writes, course/letter edits, command retries or deadline changes. Settling diagnostics cannot prove a production spawn fix. Root owns compilation, Windows/native execution and visual acceptance.
