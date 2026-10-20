# Windows delivery preparation

STATUS: IN PROGRESS — BUILD NOT RUN

The gate configuration names `Assets/Settings/Build Profiles/Win.asset`, `Win-QA.asset` and `Win-Dev.asset`. That directory is absent in this checkout as of `49ca736`. Actual Windows builds must create and validate these project assets through Unity before using the configured gate; no gate configuration or kit source was changed.

Unity's public [BuildProfile API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile.html) exposes scene and scripting-define overrides. Its [scriptingDefines documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile-scriptingDefines.html) states that the profile must be active for its defines to apply. The installed 6000.6.3f1 editor remains authoritative for creation and platform APIs; newer reference source is not proof that an API exists locally.

Next:

1. Inspect the installed editor's profile creation API and create missing Windows profiles through a project editor helper invoked by the gate. Preserve the existing scene order and append-only Wordbound scene registration. Verify the Windows platform and exact scene list on each saved asset.
2. Keep `GSK_QA` in the QA profile only. Add a project-owned QA scenario for opening Wordbound, editing a draft, launching the exact local playtest, returning with history intact, and capturing the visible UI. The kit's generic front-end smoke alone does not establish this behavior.
3. Run the required source/native checks, then build and smoke through the gate. Inspect actual screenshots and measured results before reporting delivery. The stripped player and the QA player require separate evidence.

No build profiles, player binaries, published releases or new human approvals are claimed here.
