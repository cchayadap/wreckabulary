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
            "TimeRun" => "Time Trial",
            "TeamRace" => "Team Relay",
            "BrawlRace" => "Brawl Relay",
            "FreeForAll" => "Free-for-All",
            "Freestyle" => "Freeplay",
            _ => id
        };
    }
}
