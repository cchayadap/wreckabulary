# Parcel Service material setup independent review

STATUS: COMPLETE — NO BLOCKING SOURCE FINDING; NATIVE/REPEAT/VISUAL CHECKS PENDING.

Reviewed one-file frozen manifest SHA256 `1298495b4997804ea68feddc616a1b8755cc6edc549e7d74dc6e85ab9274ac70` and candidate SHA256 `95d466a3a04665a0bac159c7d96fc11c7704609d37d3d4f09de666c467012323`. Both exact hashes match. Read the combined five-assembly zero-error source-check receipt and its recorded dependency roster. No production asset, metadata, source or native writes by this reviewer.

The first-write path requires the exact saved course identity, expected original visual assignment, clean persistent course/source/model/material dependencies, valid room profile, original loaded wood material references and vacant output/backup paths. Existing custom mappings and explicit Packing material assignments are refused. It clones saved tactile seed materials, preserving their textures/detail/gloss/keywords/UV properties and changing colour on the new materials only. The visual profile and room profile are separate clones. Only the course's VisualProfile assignment changes after shared source hashes and gameplay identity are checked.

Exact course asset/meta bytes are backed up before creating outputs or assigning the new profile. A review request added an explicit live-course metadata comparison against that backup after assignment; the frozen candidate now contains it at lines 131–132. This closes the prior evidence gap between backup integrity and actual unchanged GUID metadata.

Repeat handling is read-only: validates backups, clean saved outputs and existing assignments, then selects the saved profile. It does not reapply colours/remaps or write assets, so valid owner edits remain. Unexpected assignments and partial states are reported while preserving their files; interrupted creation is not silently retried or rolled back. Original material role records and source hashes are retained separately from the new outputs. The runtime material-remap implementation is covered by the separate material-authoring review.

No blocking source issue found. Native first execution, exact unchanged repeat, saved edited-colour repeat, dirty/partial-state rejection and the actual painted result remain unverified. The strict dependency cleanliness loop also checks package shader main assets; if Unity marks one dirty automatically, retain that first failure and bound any exception to the proven package shader case, as with the existing preview guard. This is a possible native prerequisite issue, not permission to skip source material cleanliness checks.

This report confirms only the reviewed source behavior. It does not establish aesthetic acceptance, successful saved asset creation or successful runtime/Workshop rendering.
