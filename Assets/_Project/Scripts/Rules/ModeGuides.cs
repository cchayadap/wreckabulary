using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules
{
    public sealed class ModeGuideStep
    {
        public string Title = "", Line = "", Image = "", Art = "";
    }

    public sealed class ModeGuide
    {
        public string Mode = "";
        public IReadOnlyList<ModeGuideStep> Steps = Array.Empty<ModeGuideStep>();
    }

    public static class ModeGuideBook
    {
        public const string TimeTrial = "TimeRun", Freeplay = "Freestyle";
        public const string TeamRace = "TeamRace", BrawlRace = "BrawlRace", ArenaFreeForAll = "FreeForAll";
        public const string Press = "Press";
        public const string HowToPlay = "How to play", House = "House", Arena = "Arena";

        static readonly Regex Token = new Regex(@"\{([A-Za-z]+)\}", RegexOptions.CultureInvariant);

        static readonly ModeGuide[] Guides =
        {
            Guide(HouseModes.Tutorial,
                ("Smash the TABLE", "{Press} {Attack} to smash furniture into its letters.", "TABLE"),
                ("Grab the letters", "Walk over letters to drop them in your bag.", "LAMP"),
                ("Spell a BAT", "Use {Spell} to turn B, A and T into a BAT.", "BAT"),
                ("Whack the dummy", "{Press} {Attack} to swing your BAT at the dummy.", "BAT")),
            Guide(HouseModes.Dibs,
                ("Smash for letters", "{Press} {Attack} to break furniture into letters.", "TABLE"),
                ("Spell an item", "Use {Spell} to turn your letters into an item.", "BAT"),
                ("Swing it", "{Press} {Attack} to hit with it, and harder words carry effects.", "HAMMER"),
                ("Last one standing", "Knock everyone else out to win the round.", "BLADE")),
            Guide(HouseModes.Duos,
                ("Team up", "You and your partner share a colour, so stick together.", "SHIELD"),
                ("Revive your partner", "Hold {Grab} next to a downed partner to bring them back.", "WATER"),
                ("Last team standing", "Knock out both rivals to win the round.", "BALL")),
            Guide(HouseModes.FreeForAll,
                ("Everyone for themselves", "Smash, spell and hit anyone you can reach.", "BOMB"),
                ("Score knockouts", "Every knockout you land scores one point.", "BAT"),
                ("Back in a flash", "After a knockout you come back a few seconds later.", "CLOCK"),
                ("Most knockouts wins", "First to the target wins, or the most when time runs out.", "HAMMER")),
            Guide(HouseModes.War,
                ("Two teams", "Your team shares one pile of lives.", "SHIELD"),
                ("Knockouts cost lives", "Each knockout on your team costs your team a life.", "BOMB"),
                ("Hold the middle", "Keep the middle room to your team to earn extra lives.", "CLOCK"),
                ("Run them out", "A team with no lives and nobody standing loses.", "SPEAR")),
            Guide(HouseModes.MovingOut,
                ("Find the keepsakes", "Use {Grab} to pick up a keepsake with a gold name.", "MIRROR"),
                ("Load the van", "Carry it to the VAN circle and drop it inside.", "CRATE"),
                ("Everyone aboard", "Pack every keepsake, then get the whole crew to the van.", "CLOCK")),
            Guide(HouseModes.MovingDay,
                ("Read the checklist", "The checklist shows each piece and the room it goes in.", "TABLE"),
                ("Open the boxes", "{Press} {Attack} on delivery boxes to get their letters.", "CRATE"),
                ("Spell the furniture", "Use {Spell} to spell a piece from the checklist.", "SOFA"),
                ("Set it in place", "Put it down in its room before the truck leaves.", "LAMP")),
            Guide(HouseModes.Escape,
                ("Spell doors open", "Spell the item on a barred door and carry it there.", "LAMP"),
                ("Find the keys", "Walk over a gold key to pick it up.", "CABINET"),
                ("Everybody out", "The last key opens the exit, so get the whole crew through.", "CLOCK")),
            Guide(HouseModes.ShoppingList,
                ("Read your list", "Everyone gets the same list and works from the top down.", "APPLE"),
                ("Spell the top item", "Use {Spell} to spell the next item on your list.", "PIE"),
                ("Use it up", "Hit someone, set it down or use it to tick it off.", "SODA"),
                ("Finish first", "First to tick the whole list wins the round.", "CAKE")),
            Guide(HouseModes.RiddleRush,
                ("Read the riddle", "A riddle shows at the top, and its answer is an item.", "LAMP"),
                ("Find the letters", "Smash furniture for letters, and look for pieces that glow.", "TABLE"),
                ("Spell the answer", "Use {Spell} to spell it first and win a point.", "PLANT"),
                ("First to the target", "The first player to reach the target score wins.", "CLOCK")),
            Guide(TimeTrial,
                ("Follow the markers", "Markers point to the next stop on the delivery circuit.", "TABLE"),
                ("Spell what you need", "Use {Spell} to spell the item a stop asks for.", "HAMMER"),
                ("Beat the clock", "Finish the whole circuit as fast as you can.", "CLOCK")),
            Guide(Freeplay,
                ("Explore the course", "There is no clock, so wander wherever you like.", "SOFA"),
                ("Try the puzzles", "Follow the arrow to a practice station and try its puzzle.", "FAN"),
                ("Spell the course items", "Use {Spell} to see and spell the items this course allows.", "BAT")),
            Guide(TeamRace,
                ("Race the other team", "Two teams run the same course, and the first through every lap wins.", "BALL"),
                ("Collect letters by hand", "{Press} {Grab} on each loose letter to put it in your bag.", "TABLE"),
                ("Open each checkpoint", "Follow the arrow, then spell the item its puzzle asks for.", "FAN"),
                ("Carry the keepsake", "A lap counts only when your keepsake carrier reaches the finish.", "CLOCK")),
            Guide(BrawlRace,
                ("Race and brawl", "Two teams race the same course, and you can hit your rivals.", "BAT"),
                ("Open each checkpoint", "Follow the arrow, then spell the item its puzzle asks for.", "FAN"),
                ("Stop the carrier", "Knock out the rival carrier and their keepsake drops.", "HAMMER"),
                ("Carry the keepsake", "A lap counts only when your keepsake carrier reaches the finish.", "SHIELD")),
            Guide(ArenaFreeForAll,
                ("One shared arena", "Up to 8 players share one arena, and anyone can hit anyone.", "SPEAR"),
                ("Collect letters by hand", "{Press} {Grab} on each loose letter to put it in your bag.", "TABLE"),
                ("Spell and strike", "Use {Spell} to spell an item, then knock out your rivals.", "BAT"),
                ("Most knockouts wins", "First to 10 knockouts wins, or the most when time runs out.", "HAMMER")),
            Guide(HouseModes.BossFight,
                ("Get ready", "Before the boss wakes, {Press} {Attack} to smash furniture.", "TABLE"),
                ("Crack the shields", "Use {Spell} to spell each shield word and crack a layer.", "CLOCK"),
                ("Dodge the red circles", "Step out of a red circle before it lands.", "SHIELD"),
                ("Hit the Calculator", "With its shield down, {Press} {Attack} to knock it out.", "HAMMER")),
        };

        static readonly Dictionary<string, ModeGuide> ByMode = Guides.ToDictionary(g => g.Mode, StringComparer.Ordinal);

        public static IReadOnlyList<ModeGuide> All => Guides;

        public static IReadOnlyList<string> Images { get; } = Guides.SelectMany(g => g.Steps).Select(s => s.Image).ToArray();

        public static bool Has(string modeId) => modeId != null && ByMode.ContainsKey(modeId);

        public static ModeGuide For(string modeId) => modeId != null && ByMode.TryGetValue(modeId, out var guide) ? guide : null;

        public static string ImageId(string modeId, int step) => modeId + "-" + step;

        public static string Name(string modeId) => HouseModes.Has(modeId) ? HouseModes.Name(modeId) : Wordbound.WorkshopModes.Name(modeId);

        public static string Place(string modeId) => modeId switch
        {
            HouseModes.FreeForAll => House,
            ArenaFreeForAll => Arena,
            _ => "",
        };

        public static string Eyebrow(string modeId) => Eyebrow(modeId, Name(modeId));

        public static string Eyebrow(string modeId, string name)
        {
            string place = Place(modeId);
            string title = place.Length == 0 ? name ?? "" : name + MapCardText.Separator + place;
            return title + MapCardText.Separator + HowToPlay;
        }

        public static IEnumerable<string> Tokens(string line)
        {
            foreach (System.Text.RegularExpressions.Match token in Token.Matches(line ?? "")) yield return token.Groups[1].Value;
        }

        public static bool IsToken(string name) =>
            name == Press || Enum.TryParse(name, false, out PlayerControl _);

        public static string Fill(string line, ControlDevice device, Func<PlayerControl, string> key) =>
            Token.Replace(line ?? "", m =>
            {
                string name = m.Groups[1].Value;
                if (name == Press) return ControlWords.Verb(device);
                return Enum.TryParse(name, false, out PlayerControl control) ? key?.Invoke(control) ?? "" : m.Value;
            });

        static ModeGuide Guide(string mode, params (string title, string line, string art)[] steps) => new ModeGuide
        {
            Mode = mode,
            Steps = steps.Select((s, i) => new ModeGuideStep { Title = s.title, Line = s.line, Art = s.art, Image = ImageId(mode, i + 1) }).ToArray(),
        };
    }
}
