using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-40)]
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

        static readonly string[] MenuModes =
        {
<<<<<<< HEAD
            Rules.HouseModes.Tutorial, Rules.HouseModes.Dibs, Rules.HouseModes.Duos, Rules.HouseModes.FreeForAll, Rules.HouseModes.War,
            Rules.HouseModes.MovingDay, Rules.HouseModes.MovingOut, Rules.HouseModes.ShoppingList, Rules.HouseModes.RiddleRush,
=======
            new() { label = "TUTORIAL", blurb = "Learn to smash, spell and summon", scene = Session.TutorialScene },
            new() { label = "DIBS!", id = "Dibs", blurb = "Last roommate standing • solo AI", scene = Session.DibsScene },
            new() { label = "DUOS", id = "Duos", blurb = "Two teams • revive your teammate • solo AI", scene = Session.DibsScene },
            new() { label = "MOVING DAY", blurb = "Co-op: furnish the house together", scene = Session.MovingDayScene },
            new() { label = "MOVING OUT", id = "MovingOut", blurb = "Rescue keepsakes and reach the van", scene = Session.DibsScene },
            new() { label = "BOSS FIGHT", id = "BossFight", blurb = "Solo: spell CALC / ULA / TOR, then hit the Calculator", scene = Session.DibsScene },
>>>>>>> fd3f0126 (first commit)
        };

        [SerializeField] Mode[] modes = Array.Empty<Mode>();
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] TextMeshPro menuText;
        [SerializeField] TextMeshPro paperText;
        [SerializeField] float useRange = 2.2f;

        const int MenuRows = 5;

        readonly StringBuilder sb = new();
        string message;
        float messageUntil;

        public IReadOnlyList<Mode> Modes => modes;
        public PlayerController User { get; private set; }
        public int Selected { get; private set; } = 1;

        void Awake()
        {
<<<<<<< HEAD
            modes = new Mode[MenuModes.Length];
            for (int i = 0; i < modes.Length; i++) modes[i] = FromRegistry(MenuModes[i]);
=======
            modes = new[]
            {
                new Mode { label = "TUTORIAL", id = "Tutorial", blurb = "Learn to smash, spell and summon", scene = Session.TutorialScene },
                new Mode { label = "DIBS!", id = "Dibs", blurb = "Last roommate standing • solo AI", scene = Session.DibsScene },
                new Mode { label = "DUOS", id = "Duos", blurb = "Two teams • hold grab to revive • solo AI", scene = Session.DibsScene },
                new Mode { label = "MOVING DAY", id = "MovingDay", blurb = "Furnish the house before time runs out", scene = Session.MovingDayScene },
                new Mode { label = "MOVING OUT", id = "MovingOut", blurb = "Rescue keepsakes and get everyone to the van", scene = Session.DibsScene },
                new Mode { label = "BOSS FIGHT", id = "BossFight", blurb = "Solo: spell CALC / ULA / TOR, then hit the Calculator", scene = Session.DibsScene },
            };
>>>>>>> fd3f0126 (first commit)
        }

        static Mode FromRegistry(string id) => new Mode
        {
            label = Rules.HouseModes.Name(id).ToUpperInvariant(), id = id, blurb = Rules.HouseModes.Goal(id), scene = Session.SceneForMode(id),
        };

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

            int first = Mathf.Clamp(Selected - MenuRows / 2, 0, Mathf.Max(0, modes.Length - MenuRows));
            int last = Mathf.Min(modes.Length, first + MenuRows);
            for (int i = first; i < last; i++)
            {
                var m = modes[i];
                bool more = (i == first && first > 0) || (i == last - 1 && last < modes.Length);
                string colour = m.comingSoon || more ? "#FFFFFF66" : "#FFF4E0";
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
