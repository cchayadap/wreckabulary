# Story presentation

STATUS: IN PROGRESS

The flagship stores optional clues for each lap and destination, but no runtime component displays them. Root owns new story presentation files and the narrow MatchHud hookup. The Freestyle lane owns puzzle practice; Online owns party/content transport; the arena lane owns CombatArena resources.

Presentation will read the exact resolved course and the local team's authoritative progress and narrative flags. It must not mutate progression, pause competitive play, reveal another team's progress, or show a branch absent from the selected route. Nearby destination notices, a brief discovery banner and an optional menu journal will expose the existing authored text. Restored progress must reconstruct the same journal without a second discovery event.

Native verification is currently running against the preceding source. Stage source outside Assets until it exits, then run source and focused presentation checks before the next player build. No player visual acceptance is claimed.

Source moved into Presentation after the preceding native run exited. MatchHud now binds the story view for both local and online clients and offers the optional collected-clue journal. Notices use the selected checkpoint's actual recovery position; journal visibility requires the local team's reached checkpoint and matching narrative flag. Recreating the view reconstructs the journal, while the live instance remembers already announced clues across rollback.

Five focused presentation checks are authored for selected routes/positions, own-team visibility, future-flag leakage, reconstruction without mutation and unknown readers. Compilation, execution and actual player captures remain NOT-RUN for this source.

Root R0 source compilation PASS in109 seconds (`Logs/gate_check_all_20261020T185125Z.log`). The five tests and native/player behavior remain NOT-RUN. No authoritative state, course bytes, snapshot schema or mode statistics changed for story presentation.
