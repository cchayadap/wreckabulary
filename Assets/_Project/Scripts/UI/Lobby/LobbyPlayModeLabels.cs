using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public static class LobbyPlayModeLabels
    {
        public static string Name(string id) => id switch
        {
            "Dibs" => "Dibs!",
            "Duos" => "Duos",
            "MovingOut" => "Moving Out",
            "MovingDay" => "Moving Day",
            "Escape" => "Escape",
            "TimeRun" or "TeamRace" or "BrawlRace" or "FreeForAll" or "Freestyle" => WorkshopModes.Name(id),
            _ => id
        };
    }
}
