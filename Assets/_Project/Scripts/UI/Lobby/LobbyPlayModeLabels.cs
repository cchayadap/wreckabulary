using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public static class LobbyPlayModeLabels
    {
        public static string Name(string id) => id switch
        {
            "TimeRun" or "TeamRace" or "BrawlRace" or "FreeForAll" or "Freestyle" => WorkshopModes.Name(id),
            LobbyMenu.WorkshopMode => "Home builder",
            _ when HouseModes.Has(id) => HouseModes.Name(id),
            _ => id
        };

        public static string Emblem(string id) => id switch
        {
            "TeamRace" => "BALL",
            "BrawlRace" => "BAT",
            "FreeForAll" => "SPEAR",
            "TimeRun" => "CLOCK",
            "Freestyle" => "BENCH", // a town bench: wander the course; the sofa is Furnish the House
            _ => LobbyKit.ModeArt(id)
        };

        public static string Goal(string id) => id switch
        {
            "TimeRun" => "Solve the delivery circuit",
            "Freestyle" => "Explore, spell items and try the puzzles",
            LobbyMenu.WorkshopMode => "Build your home, then play it in " + HouseModes.FamilyName(HouseModeFamily.Classic) +
                " or " + HouseModes.FamilyName(HouseModeFamily.Arcade),
            _ => HouseModes.Goal(id)
        };

        public static string Line(string id)
        {
            string goal = Goal(id);
            return string.IsNullOrEmpty(goal) ? "" : Name(id) + MapCardText.Separator + goal;
        }
    }
}
