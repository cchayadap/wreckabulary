using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        static readonly string[] EntryLabels = { "Create", "Join" };
        int entryPage;
        bool showPartySettings, showPrivateActions;

        void DrawOnlineBackground(Vector2 viewport)
        {
            if (Event.current.type != EventType.Repaint) return;
            var settings = ui.ConfigurationError.Length == 0 ? Theme : null;
            Texture2D background = settings && settings.OnlineBackground ? settings.OnlineBackground :
                !settings || settings.OnlineUseLobbyBackground ? LobbyThemes.Current.Background : null;
            if (!background) return;
            var previous = GUI.color;
            try
            {
                GUI.color = Color.white;
                var bounds = new Rect(Vector2.zero, viewport);
                GUI.DrawTexture(bounds, background, ScaleMode.ScaleAndCrop, false);
                Color shade = ui.Ink;
                float opacity = settings ? settings.OnlineBackgroundShade : .18f;
                shade.a = float.IsNaN(opacity) || float.IsInfinity(opacity) ? .18f : Mathf.Clamp01(opacity);
                GUI.color = shade;
                GUI.DrawTexture(bounds, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            }
            finally { GUI.color = previous; }
        }

        void DrawJoinForms()
        {
            GUILayout.Space(ui.Gap); GUILayout.Label("Party invite");
            partyInvite = PartyText("party-invite", partyInvite, 160);
            GUILayout.BeginHorizontal();
            PartyControl("paste-party", "Paste", () => partyInvite = GUIUtility.systemCopyBuffer);
            PartyControl("join-party", "Join party", () => Invoke(() => coordinator.JoinParty(partyInvite)));
            GUILayout.EndHorizontal();
            GUILayout.Space(ui.Gap); GUILayout.Label("Private room invite");
            privateCode = PartyText("room-invite", privateCode, 220);
            GUILayout.BeginHorizontal();
            PartyControl("paste-room", "Paste", () => privateCode = GUIUtility.systemCopyBuffer);
            PartyControl("use-room-settings", "Use room settings", () => Invoke(ApplyRoomSettings));
            GUILayout.EndHorizontal();
            GUILayout.Label("Use the room settings, then create your party.", hint);
        }

        void DrawPrivateActions(bool canAct)
        {
            PartyControl("create-private", "Create room", () => Invoke(() => coordinator.Queue("PrivateCreate")));
            GUILayout.Label("Room invite or code");
            privateCode = PartyText("room-code", privateCode, 220);
            GUILayout.BeginHorizontal();
            PartyControl("paste-room", "Paste", () => privateCode = GUIUtility.systemCopyBuffer);
            PartyControl("join-private", "Join room", () => Invoke(JoinRoom));
            GUILayout.EndHorizontal();
            if (coordinator.Party.Settings.Fill)
            {
                PartyControl("custom-consent", "Include custom rooms",
                    () => customFillConsent = !customFillConsent, true, customFillConsent);
                GUI.enabled = canAct && customFillConsent;
                PartyControl("custom-fill", "Find custom room", () => Invoke(() => coordinator.Queue("CustomFill")));
                GUI.enabled = canAct;
            }
        }

        static string Composition(string selectedMode, string selectedFormat) => selectedMode switch
        {
            "TimeRun" => "Time Run · Co-op · up to 4 humans",
            "TeamRace" => "Team Race · 2 teams · " + TeamSize(selectedFormat) + " per team · no damage",
            "BrawlRace" => "Brawl Race · 2 teams · " + TeamSize(selectedFormat) + " per team · combat",
            "FreeForAll" => "Free-for-All · 2–8 humans",
            "Freestyle" => "Freestyle · Co-op · up to 8 humans · no timer",
            _ => selectedMode
        };
        static string TeamSize(string selectedFormat) => selectedFormat == "Squad" ? "4" : selectedFormat == "Duo" ? "2" : "1";
        static string MatchStatus(string state, string channel) => state switch
        {
            "FORMING" => channel == "PrivateCreate" ? "Room open" : "Finding players…",
            "STARTED" => "Joining match…",
            _ => state.Replace('_', ' ')
        };
    }
}
