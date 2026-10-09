using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The mode select in the house. Walk up and press grab to sit at it, step through the modes
    /// with up/down, press grab or attack to pick one, or spell to get up again.
    /// </summary>
    [DefaultExecutionOrder(-40)] // after input is read, before combat sees the grab press
    public class Typewriter : MonoBehaviour
    {
        [Serializable]
        public class Mode
        {
            public string label;
            public string blurb;
            public string scene;
            public string id;
            public int minPlayers = 1;
            public bool comingSoon;
        }

        [SerializeField] Mode[] modes =
        {
            new() { label = "TUTORIAL", blurb = "Learn to smash, spell and summon", scene = Session.TutorialScene },
            new() { label = "DIBS!", id = "Dibs", blurb = "Last roommate standing • solo AI", scene = Session.DibsScene },
            new() { label = "DUOS", id = "Duos", blurb = "Two teams • revive your teammate • solo AI", scene = Session.DibsScene },
            new() { label = "MOVING DAY", blurb = "Co-op: furnish the house together", scene = Session.MovingDayScene },
            new() { label = "MOVING OUT", id = "MovingOut", blurb = "Rescue keepsakes and reach the van", scene = Session.DibsScene },
        };
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] TextMeshPro menuText;
        [SerializeField] TextMeshPro paperText;
        [SerializeField] float useRange = 2.2f;

        readonly StringBuilder sb = new();
        string message;
        float messageUntil;

        public IReadOnlyList<Mode> Modes => modes;
        public PlayerController User { get; private set; }
        public int Selected { get; private set; } = 1;

        void Awake()
        {
            modes = new[]
            {
                new Mode { label = "TUTORIAL", id = "Tutorial", blurb = "Learn to smash, spell and summon", scene = Session.TutorialScene },
                new Mode { label = "DIBS!", id = "Dibs", blurb = "Last roommate standing • solo AI", scene = Session.DibsScene },
                new Mode { label = "DUOS", id = "Duos", blurb = "Two teams • hold grab to revive • solo AI", scene = Session.DibsScene },
                new Mode { label = "MOVING DAY", id = "MovingDay", blurb = "Furnish the house before time runs out", scene = Session.MovingDayScene },
                new Mode { label = "MOVING OUT", id = "MovingOut", blurb = "Rescue keepsakes and get everyone to the van", scene = Session.DibsScene },
            };
        }

        void Update()
        {
            if (User)
            {
                if (User.IsKnockedOut || User.IsHeld) { Close(); return; }
                var c = User.Commands;
                if (c.up) Step(-1);
                if (c.down) Step(1);
                if (c.grab || c.attack) Confirm();
                else if (c.spellDown) Close();
            }
            else
            {
                foreach (var p in World.Players)
                {
                    if (!p.CanAct || p.Combat.IsHolding || !InRange(p) || !p.Commands.grab) continue;
                    Open(p);
                    break;
                }
            }
        }

        void LateUpdate()
        {
            if (menuText)
            {
                Popup.Billboard(menuText.transform);
                menuText.text = MenuLines();
            }
            if (paperText) paperText.text = modes[Selected].label;
        }

        public bool InRange(PlayerController p) =>
            World.Flat(p.transform.position - transform.position).magnitude <= useRange;

        public void Open(PlayerController p)
        {
            Close();
            User = p;
            p.Frozen = true;
            p.FaceTowards(transform.position - p.transform.position);
        }

        public void Close()
        {
            if (User) User.Frozen = false;
            User = null;
        }

        void Step(int delta) => Selected = (Selected + delta + modes.Length) % modes.Length;

        /// <summary>Picks a mode by index. Returns true if its scene is loading.</summary>
        public bool Choose(int index)
        {
            Selected = Mathf.Clamp(index, 0, modes.Length - 1);
            return Confirm();
        }

        bool Confirm()
        {
            var m = modes[Selected];
            int players = joins ? joins.Players.Count : World.Players.Count;
            if (m.comingSoon || !Session.CanLoad(m.scene)) return Say("COMING SOON");
            if (players < m.minPlayers) return Say($"NEEDS {m.minPlayers} ROOMMATES");

            Close();
            Session.LoadMode(m.id);
            return true;
        }

        bool Say(string text)
        {
            message = text;
            messageUntil = Time.time + 1.5f;
            return false;
        }

        static string StarsFor(Mode m)
        {
            if (m.scene != Session.MovingDayScene || Session.MovingDayStars.Count == 0) return "";
            int total = 0;
            foreach (var s in Session.MovingDayStars.Values) total += s;
            return $"  ({total} stars)";
        }

        string MenuLines()
        {
            sb.Clear();
            bool someoneNear = false;
            foreach (var p in World.Players) someoneNear |= InRange(p);

            if (!User)
            {
                sb.Append(someoneNear ? "<color=#FFD24A>Press grab to type</color>" : "<color=#FFF4E0AA>TYPEWRITER</color>");
                return sb.ToString();
            }

            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                string colour = m.comingSoon ? "#FFFFFF66" : "#FFF4E0";
                if (i == Selected)
                    sb.Append("<size=125%><color=#FFD24A>> ").Append(m.label).Append(" <</color></size>\n")
                      .Append("<size=70%><color=#FFF4E0CC>").Append(m.comingSoon ? "coming soon" : m.blurb).Append(StarsFor(m)).Append("</color></size>\n");
                else
                    sb.Append("<color=").Append(colour).Append('>').Append(m.label).Append("</color>\n");
            }
            if (Time.time < messageUntil) sb.Append("<color=#FF8A6A>").Append(message).Append("</color>");
            return sb.ToString();
        }
    }
}
