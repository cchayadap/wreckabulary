# Workshop mode and physical controls handoff

Root launcher setup:

```csharp
workshop.ConfigureLocalPlaytest(true);
workshop.OpenFlagship(WordboundFlagshipContent.Create(items), saveFolder);
workshop.PlaytestRequested += manifest =>
{
    var draft = workshop.LastPlaytestDraft;
    var requirements = workshop.PlayRequirements;
    // Capture these exact values synchronously for the local launcher.
};
```

`ConfigureFlagship(content)` adds a `Flagship draft` palette choice without opening it. `OpenFlagship` uses that exact factory document/geometry and explicitly selects TimeRun. `SelectMode(modeId, format)` chooses the requirements before Preview; `EnableFreestyleSandbox()` visibly adds Freestyle as an undoable authoring transaction. Local playtests permit TimeRun/Freestyle only. `ShowStatus(message)` provides concise inline launcher errors, and `ResumeEditing()` preserves the current history after returning.

`LastPlaytestDraft` is assigned before the event and remains immutable if the launcher later changes editor state. No mode relabelling, implicit reroll or invented players occur. Geometry-only race templates remain editable; missing or mismatched selected lap/puzzle/checkpoint profiles produce actionable guidance instead of dispatching an incomplete physical course.

Physical controls are stored in the canonical schema-2 document. Choose a puzzle in a selected module, choose the lap and use **Add physical controls**. The module must already have an ordered checkpoint on that lap and protected renewable recipe letters. The action adds the family-specific sensor/source defaults as one undo transaction. Preview remains responsible for full bounds, route and resource validation.

Select an existing physical site to edit checkpoint/source references, socket position/yaw/radius/hold, arrival/receiver volumes, balance switches and moving-arm endpoints/dimensions/timing. Source selection uses an inline searchable list. Drag the labelled viewport controls to move with grid snap; Escape cancels without editing, release commits once. Receiver dragging also moves its fixed source. Module transforms, floor changes, duplication and deletion preserve or remap owned profiles and fixture IDs.

Controller tools offer **Next physical puzzle**, **Next puzzle control** and **Edit modules**. With a site selected, D-pad cycles controls, left stick moves the selected control, X turns the socket, and shoulders undo/redo. Keyboard-focusable numeric fields provide vertical placement and volume sizes.

Verification: pure58/58 and installed-Unity reference compilation passed. Native mouse/controller usability, real viewport captures and complete physical solutions have not run for this follow-up; root owns those gates. The profile UI does not replace a complete route/checkpoint designer, and it does not silently invent checkpoints when a module lacks one.
