using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary
{
    public static class Session
    {
        public const string HubScene = "Hub";
        public const string DibsScene = "LivingRoom";
        public const string TutorialScene = "Tutorial";
        public const string MovingDayScene = "MovingDay";
        public const string WordboundScene = "WordboundLobby";

        public static readonly List<InputBinding> Bindings = new();
        public static string MapId { get; private set; } = "pinwheel";
        public static string LobbyQueue, LobbyMode, LobbyHome, LobbyMap, LobbyMaps;
        public static Rules.HomeLayout Home { get; private set; }

        public const string StarsKey = "wv.movingDay.stars";
        static Dictionary<string, int> movingDayStars;
        public static IReadOnlyDictionary<string, int> MovingDayStars => Stars();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Bindings.Clear();
            WordboundSoloEntry.Clear();
            WordboundWorkshopEntry.Clear();
            movingDayStars = null;
            MapId = "pinwheel";
            LobbyQueue = LobbyMode = LobbyHome = LobbyMap = LobbyMaps = null;
            Home = null;
            Match.Reset();
        }

        public static void RecordStars(string map, int stars)
        {
            var book = Stars();
            if (string.IsNullOrEmpty(map) || stars < 1 || (book.TryGetValue(map, out int best) && stars <= best)) return;
            book[map] = Math.Min(3, stars);
            PlayerPrefs.SetString(StarsKey, string.Join(";", book.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture))));
            PlayerPrefs.Save();
        }

        static Dictionary<string, int> Stars()
        {
            if (movingDayStars != null) return movingDayStars;
            movingDayStars = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string entry in PlayerPrefs.GetString(StarsKey, "").Split(';'))
            {
                int at = entry.IndexOf('=');
                if (at > 0 && int.TryParse(entry.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stars) && stars is >= 1 and <= 3)
                    movingDayStars[entry.Substring(0, at)] = stars;
            }
            return movingDayStars;
        }

        public static void Remember(InputBinding binding)
        {
            if (binding is BotBinding) return;
            if (!Bindings.Exists(b => b.Id == binding.Id)) Bindings.Add(binding);
        }

        public static bool CanLoad(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        public static void Load(string scene)
        {
            Match.ModeOverride = null;
            Home = null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public static string SceneForMode(string mode) => mode switch
        {
            Rules.HouseModes.MovingDay => MovingDayScene,
            Rules.HouseModes.Tutorial => TutorialScene,
            "Hub" => HubScene,
            _ when Rules.HouseModes.Has(mode) => DibsScene,
            _ => throw new System.ArgumentException($"Unknown mode '{mode}'.", nameof(mode)),
        };


        public const string LobbyRandomMap = "random";

        public static string RandomHouseId()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var key in GameConfig.Current.Houses.Keys) ids.Add(key);
            return ids.Count == 0 ? "pinwheel" : ids[UnityEngine.Random.Range(0, ids.Count)];
        }

        public static string ResolveMap(string mode, string mapId) =>
            Rules.HouseModes.FixedHouse(mode) is string fixedHouse ? fixedHouse
            : mapId == LobbyRandomMap || (mode == Rules.HouseModes.Escape && string.IsNullOrEmpty(mapId)) ? RandomHouseId() : mapId;

        public static void SelectMap(string mapId)
        {
            GameConfig.Current.HouseFor(mapId);
            MapId = string.IsNullOrEmpty(mapId) ? "pinwheel" : mapId;
        }

        public static void LoadMode(string mode, string mapId = null, Rules.HomeLayout home = null)
        {
            var scene = SceneForMode(mode);
            mapId = ResolveMap(mode, mapId);
            if (mapId != null) SelectMap(mapId);
            Home = home != null && Rules.HomeDesigner.Playable(mode) && home.Map == MapId ? home.Clone() : null;
            Match.ModeOverride = mode;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public static bool OpenWorkshop(string mapId, out string error)
        {
            if (CreativeWorkshop.Instance) { error = "A workshop is already open."; return false; }
            var workshop = new GameObject("Creative Workshop").AddComponent<CreativeWorkshop>();
            mapId ??= MapId;
            return workshop.Open(Rules.HomeDesigner.Supports(mapId) ? mapId : "pinwheel", out error);
        }

        public static bool GoHome()
        {
            if (!CanLoad(HubScene) || SceneManager.GetActiveScene().name == HubScene) return false;
            Load(HubScene);
            return true;
        }
    }
}
