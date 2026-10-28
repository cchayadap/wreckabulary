# Sparse Play grid independent source review

STATUS: COMPLETE — no blocking source defect found in the bounded patch. Read-only review; no native execution or production edits by this reviewer.

## Scope and identity

Reviewed the frozen manifest, baseline/candidate copies, single-card-width.patch, relevant layout helpers and focused/existing tests. All three baseline hashes match the manifest; all three candidate hashes match both the manifest and current live files. Parent reports integration at source commit 74ffae0359766d04d1632728f5da0a7f72457887.

## Confirmed behavior

- The capacity calculation is valid for the installed Unity UI implementation. LobbyPlayCardGrid derives columns from available width and MaximumColumns, then explicitly sets cellSize and FixedColumnCount. The cached com.unity.ugui GridLayoutGroup uses that fixed cellSize even with fewer children than columns. Its actual-cell clamp affects placement extent, not card width. LobbyKit.Grid uses horizontal ordering and default upper-left alignment, so the lone card stays in the first house-card column rather than centering or stretching.
- Escape Random and Wordbound Create share HouseSize and the current house-count cap. One real card occupies one normal house slot; no fake selectable placeholders are introduced. One child still produces one row of height. Narrower widths naturally reduce capacity to one; card width remains positive for the supported positive-size parent.
- Two-mode strips and Solo's two-map layout keep maximumColumns equal to their two actual children. Five-house layouts keep the cap equal to their actual count. Removing the child-count clamp therefore does not change columns or footprint for those full sets. Tutorial guidance is untouched.
- Fit still runs on dimension changes and LateUpdate, changing only grid geometry and preferred height when needed. It does not recreate objects, replace Selectables, reset selection, or change choice/launch callbacks. Existing LobbyScrollFollow responds to viewport/content changes and preserves intentional manual scrolling when selection and sizes are unchanged. Header and launch areas remain outside the scroll viewport.
- Workshop availability, destination, session guards and launch behavior are unchanged. The only copy edit is the concise card caption “Build & playtest”.

## Test assessment

The added test compares actual rendered Random and Create card width, height and left edge against the first normal house card at three page sizes (1868×968, 1388×968 and 900×580). It also checks single-choice counts, selected Random focus, fixed header/action bounds, unchanged launch admission, no accidental scene transition, and Create text overflow. These are meaningful behavioral assertions.

Existing retained tests cover house scrolling/focus, resize back and forth, manual-scroll stability, Solo availability/selection, and all-family frame/focus preservation at 1920×1080, 854×480, 2560×1080 and back. The new test recreates each family at each size; it does not itself resize a focused Escape card in place. The unchanged fit/selection code and retained resize coverage make that a coverage limit, not an identified regression.

Read parent-owned native-verification.json: official core 41/41 and runtime 33/33, zero skipped, with referenced gate/TRX/XML evidence. Those are parent-run results, not an independent run by this reviewer.

## Limits

Source/test review does not establish final visual preference, controller feel, every possible authored house configuration, arbitrary viewport sizes, or gameplay/network correctness. Latest Windows screenshot review is separate. No owner acceptance is inferred.
