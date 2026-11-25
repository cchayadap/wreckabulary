using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public static class LobbyPlayModeLabels
    {
        public const string MapsField = "Maps";
        static readonly MemberInfo mapsMember = (MemberInfo)typeof(HouseMode).GetField(MapsField, BindingFlags.Public | BindingFlags.Instance) ??
            typeof(HouseMode).GetProperty(MapsField, BindingFlags.Public | BindingFlags.Instance);

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
            "Freestyle" => "SOFA",
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

        public static int Seats(string id) => HouseModes.Has(id) ? Math.Max(1, ModeAccess.For(id).MaxSeats) : 1;

        public static string SeatTag(string id) => Seats(id) == 1 ? ModeAccess.OnePlayer : ModeAccess.UpTo + " " + Seats(id);

        public static IReadOnlyList<string> Maps(string id)
        {
            var mode = HouseModes.Find(id);
            if (mode == null || mapsMember == null) return Array.Empty<string>();
            object value = mapsMember is FieldInfo field ? field.GetValue(mode) : ((PropertyInfo)mapsMember).GetValue(mode);
            return value is IEnumerable<string> maps ? maps.Where(map => !string.IsNullOrEmpty(map)).ToArray() : Array.Empty<string>();
        }

        public static string MapTag(string id)
        {
            var maps = Maps(id);
            if (maps.Count == 0) return "";
            if (maps.Count > 1) return maps.Count + " maps";
            string name = GameConfig.Current != null && GameConfig.Current.Houses.TryGetValue(maps[0], out var house) ? house.Name : maps[0];
            return name.Split(' ')[0];
        }
    }
}
