# Workshop action row correction

STATUS: IMPLEMENTED AND BOUNDED WINDOWS VERIFICATION PASSED; physical-input/owner acceptance open.

The actual 231729 Windows build passes workshop flow 232018, but its original and returned workshop images show Group, Ungroup and To this floor wrapping and clipping inside one short row. The first range-control image shows all added controls correctly. Preserve that run as passing behavior evidence with a failed visual detail; do not promote it as the final corrected delivery.

The correction keeps Group and Ungroup together, then places Move to floor on its own full-width row with a tooltip explaining the current floor. The existing ChangeLayer callback is unchanged. The two QA scroll samples advance by one theme row to account for the added action row. No deadline, gameplay, history, autosave or verification condition changes.

Build 232801, source ec4b127b, and workshop run 233042 now pass all five steps. All eight actual images were opened: Group/Ungroup fit, Move to floor is readable, and height, rotations, floors and Apply/Cancel remain visible. Exact save/playtest/return/Undo and all range-view preservation checks pass. The original and returned editor images are identical. [Visual review](windows-workshop-232801-visual-review.md), [independent flow audit](windows-workshop-232801-verification.md). No additional mirrored copy test was needed for this small layout correction.
