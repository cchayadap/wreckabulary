# Third Party Notices

This package contains no third-party source code. It implements these published, freely usable algorithms from
their descriptions:

- **PCG32** (`GameStudioKit.Determinism.Pcg32`): the permuted congruential generator (XSH RR 64/32) by
  Melissa E. O'Neill, "PCG: A Family of Simple Fast Space-Efficient Statistically Good Algorithms for Random Number
  Generation" (2014), https://www.pcg-random.org/. The seeding and stream selection follow the paper, so the kit's
  tests can check the output against the published pcg32 demo values.
- **FNV-1a 64** (`GameStudioKit.Determinism.Hash64`): the Fowler/Noll/Vo hash, placed in the public domain by its
  authors, http://www.isthe.com/chongo/tech/comp/fnv/.
- **SplitMix64 finaliser** (`Hash64.Mix`): Sebastiano Vigna's public-domain SplitMix64 mixing constants.

The Unity Recorder (`com.unity.recorder`) and Unity Pipeline (`com.unity.pipeline`) packages are optional and are not
bundled. When present, they are covered by their own licences.
