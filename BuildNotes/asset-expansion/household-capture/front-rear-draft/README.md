# Household front/rear follow-up

STATUS: DRAFT ONLY — `capture-02` has not run. `capture-01` and the live diagnostic source are preserved.

The completed first capture is useful rear evidence. The inspected kitchen and attic images do not expose the main handles, door faces or drawer fronts, so they cannot establish front-surface quality. This bounded patch adds the opposite camera view without changing prefabs, materials, lights, scales, cleanup or preservation checks.

Each group gets a matched pair: `gallery-front/rear`, `kitchen-front/rear`, `attic-front/rear` and `rooftop-parcel-front/rear`, eight 1,280 × 800 PNGs. The output folder changes to `BuildNotes/asset-expansion/household-capture/capture-02`, with the same refusal to overwrite existing results.

The front candidate uses camera direction **(-1.1, +0.7, +1.7)** from the group center; the rear baseline uses **(+1.1, +0.7, -1.7)**. Both keep positive elevation, the same distance, the same geometry arrangement and the same world-space neutral lights. Each view's framing is the maximum needed for both directions, so the pair shares its orthographic scale. The receipt records pair, orientation, actual camera position/rotation, offset direction, bounds and scale, and rejects a mismatched pair.

“Front” is an explicit camera-side candidate until its rendered image is inspected. Confirm door/drawer faces, handles, glass, normals and the plant/bench/parcel surfaces in the actual output before quality acceptance. The capture remains an editor diagnostic, not gameplay or physics proof.

`baseline/` holds the exact live source used for this draft; `draft/` holds the candidate. `manifest.json` records both hashes and every existing `capture-01` artifact hash. `patch` changes only the Editor diagnostic source. Root owns applying it, required gates and native execution after the current job finishes.
