using System;
using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        readonly WordboundMenuFocus partyFocus = new WordboundMenuFocus();
        readonly WordboundPadFocusGate partyPadGate = new WordboundPadFocusGate();
        WordboundInputBinding partyInput;
        Action pendingPartyAction;
        string pendingPartyContext;
        bool partyUsesPad, partyTyping, revealPartySelection, partyGuiFocused, pendingPartyPad;
        int partyInputRevision;
        float partyViewportHeight = 600;

        string PartyContext => coordinator == null ? "" : string.Join("|", Visible, launching, returning,
            coordinator.Authenticated, coordinator.Busy, coordinator.RetryRequired, coordinator.MatchActive,
            coordinator.Party?.Id, coordinator.Party?.Revision, mode, region, format, customSeedMode, seed,
            entryPage, showCreateSettings, showPartySettings, showPrivateActions);

        void SuspendPartyInput()
        {
            pendingPartyAction = null;
            pendingPartyContext = null;
            partyFocus.CancelConfirmation();
            partyInput?.SuspendGameplay();
            partyPadGate.Suspend();
        }

        void ProcessPartyInput(bool focused)
        {
            if (coordinator == null) return;
            bool active = Visible && !launching && !returning &&
                (!coordinator.MatchActive || !string.IsNullOrEmpty(localError));
            if (!active || !focused) { SuspendPartyInput(); return; }
            partyInput ??= new WordboundInputBinding();
            bool lost = partyInput.RefreshDevice(!partyTyping);
            if (lost || partyInputRevision != partyInput.Revision)
            {
                partyInputRevision = partyInput.Revision;
                if (lost || pendingPartyPad) SuspendPartyInput();
                else
                {
                    partyFocus.CancelConfirmation();
                    partyInput.SuspendGameplay(); partyPadGate.Suspend();
                }
            }
            partyUsesPad = partyInput.ActiveGamepad != null;
            bool blocked = partyTyping || DesktopBinding.Typing || KeyBindings.GameplayBlocked;
            if (pendingPartyPad && blocked) SuspendPartyInput();
            var action = pendingPartyAction;
            string context = pendingPartyContext;
            pendingPartyAction = null; pendingPartyContext = null;
            if (action != null && context == PartyContext)
            {
                partyFocus.CancelConfirmation();
                action();
                partyInput.SuspendGameplay(); partyPadGate.Suspend();
                return;
            }
            if (!partyPadGate.CanRead(partyInput.ActiveGamepad, focused, blocked))
            { partyFocus.CancelConfirmation(); return; }
            var commands = partyInput.ReadMenu(Time.unscaledTime);
            if (commands.Back) { PartyBack(); return; }
            if (commands.Step != 0) { partyFocus.Move(commands.Step); revealPartySelection = true; }
            if (commands.Confirm) partyFocus.Confirm();
            if (Mathf.Abs(commands.Scroll) > .25f)
                scroll.y = Mathf.Max(0, scroll.y - commands.Scroll * 420 * Time.unscaledDeltaTime);
        }

        void PartyBack()
        {
            partyFocus.CancelConfirmation();
            if (coordinator.Busy || launching || returning) return;
            if (coordinator.RetryRequired) { partyFocus.Reset("retry"); revealPartySelection = true; return; }
            if (coordinator.MatchActive) { _ = Return(); return; }
            if (coordinator.Party != null && !coordinator.Party.HasAttempt)
            {
                if (showPrivateActions) { showPrivateActions = false; partyFocus.Reset("private-actions"); revealPartySelection = true; return; }
                if (showPartySettings) { showPartySettings = false; partyFocus.Reset("party-settings"); revealPartySelection = true; return; }
            }
            if (coordinator.Party == null && entryPage == 0 && showCreateSettings)
            { CloseCreateSettings(); return; }
            if (coordinator.Party == null && entryPage != 0)
            { entryPage = 0; scroll = Vector2.zero; partyFocus.Reset("entry:0"); revealPartySelection = true; return; }
            if (coordinator.Party != null) { Invoke(() => coordinator.LeaveParty()); return; }
            CloseRequested?.Invoke();
        }

        void QueuePartyAction(Action action, bool fromPad)
        {
            if (pendingPartyAction != null || !partyGuiFocused) return;
            pendingPartyAction = action; pendingPartyContext = PartyContext;
            pendingPartyPad = fromPad;
            GUI.FocusControl(null); partyTyping = false;
        }

        void PartyControl(string id, string label, Action action, bool toggle = false, bool value = false)
        {
            bool enabled = GUI.enabled;
            if (Event.current.type == EventType.Layout && enabled) partyFocus.Register(id);
            bool focused = partyUsesPad && partyFocus.Selected == id;
            GUI.SetNextControlName("party:" + id);
            string text = (focused ? "› " : "") + (toggle ? value ? "● " : "○ " : "") + label;
            GUIStyle style = focused || toggle && value ? ui.Selected :
                id == "connect" || id == "create-party" || id == "quick-play" ? ui.Primary : GUI.skin.button;
            var content = new GUIContent(text);
            bool clicked = toggle ? ui.Toggle(value, content, style, GUILayout.MinHeight(ui.RowHeight)) != value :
                ui.Button(content, style, GUILayout.MinHeight(ui.RowHeight));
            if (focused && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("party:" + id);
                if (revealPartySelection)
                {
                    var rect = GUILayoutUtility.GetLastRect();
                    if (rect.yMin < scroll.y) scroll.y = rect.yMin;
                    else if (rect.yMax > scroll.y + partyViewportHeight)
                        scroll.y = Mathf.Max(0, rect.yMax - partyViewportHeight);
                    revealPartySelection = false;
                }
            }
            bool confirmed = partyUsesPad && enabled && partyGuiFocused &&
                Event.current.type == EventType.Repaint && partyFocus.Consume(id);
            if (clicked || confirmed) { partyFocus.CancelConfirmation(); QueuePartyAction(action, confirmed && !clicked); }
        }

        void PartyChoices(string id, int selected, string[] labels, int columns, Action<int> choose)
        {
            for (int row = 0; row < labels.Length; row += columns)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < Math.Min(row + columns, labels.Length); i++)
                {
                    int index = i;
                    PartyControl(id + ":" + i, (selected == i ? "• " : "") + labels[i], () => choose(index));
                }
                GUILayout.EndHorizontal();
            }
        }

        string PartyText(string id, string value, int maximum)
        {
            GUI.SetNextControlName("party-text:" + id);
            return GUILayout.TextField(value, maximum, GUILayout.MinHeight(ui.RowHeight));
        }

        void BeginPartyGui(bool focused)
        {
            partyGuiFocused = focused;
            if (Event.current.type == EventType.Layout) partyFocus.BeginLayout();
        }

        void EndPartyGui()
        {
            if (Event.current.type == EventType.Layout) partyFocus.EndLayout();
            partyTyping = (GUI.GetNameOfFocusedControl() ?? "").StartsWith("party-text:", StringComparison.Ordinal);
        }

        void OnApplicationFocus(bool focused) { if (!focused) SuspendPartyInput(); }
        void OnDisable() => SuspendPartyInput();
    }
}
