using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public static class LobbyMapSnapshots
    {
        public const string Folder = "MapSnapshots", ToyboxArena = "ToyboxArena";
        static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();

        static readonly Dictionary<string, string> traits = new Dictionary<string, string>
        {
            ["pinwheel"] = "Rooms round a playroom",
            ["courtyard"] = "Open-air fountain square",
            ["flat"] = "Tiled kitchen and bathroom",
            ["terrace"] = "Checkered kitchen floor",
            ["walkup"] = "Four flats stacked high",
            [WordboundSoloEntry.ParcelServiceMap] = "Two floors",
            [WordboundSoloEntry.LostAddressMap] = "Four floors",
            [WordboundSoloEntry.ParcelHallMap] = "Four floors, furnished start",
            [ToyboxArena] = "Walled arena under the skyline",
        };

        public static Sprite For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (loaded.TryGetValue(id, out var sprite) && sprite) return sprite;
            sprite = Resources.Load<Sprite>(Folder + "/" + id);
            if (sprite) loaded[id] = sprite; else loaded.Remove(id);
            return sprite;
        }

        public static string Trait(string id) => id != null && traits.TryGetValue(id, out string trait) ? trait : "";

        public static string HouseCaption(string id, HouseLayout layout)
        {
            string trait = Trait(id);
            if (layout == null) return trait;
            if (trait.Length == 0) return MapCardText.HouseLine(layout);
            int floors = layout.StoreyFloors().Count;
            return trait + MapCardText.Separator + floors + (floors == 1 ? " floor" : " floors");
        }

        public static string CourseKey(string mode) =>
            OnlineMaps.CourseFor(mode) == WordboundFlagshipContent.CourseId ? WordboundSoloEntry.LostAddressMap : ToyboxArena;

        public static Sprite ForMode(string mode) => string.IsNullOrEmpty(mode) ? null : For(CourseKey(mode));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget() => loaded.Clear();
    }
}
