using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary
{
    /// <summary>
    /// Who is playing, carried between scenes: roommates who walked into the house
    /// come along to whichever mode is picked at the typewriter.
    /// </summary>
    public static class Session
    {
        public const string HubScene = "Hub";
        public const string DibsScene = "LivingRoom";
        public const string TutorialScene = "Tutorial";
        public const string MovingDayScene = "MovingDay";

        public static readonly List<InputBinding> Bindings = new();
        public static string MapId { get; private set; } = "pinwheel";
        public static string LobbyQueue, LobbyMode;

        /// <summary>Best Moving Day stars per level, for this play session.</summary>
        public static readonly Dictionary<int, int> MovingDayStars = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Bindings.Clear();
            MovingDayStars.Clear();
            MapId = "pinwheel";
            LobbyQueue = LobbyMode = null;
            Match.Reset();
        }

        public static void RecordStars(int level, int stars)
        {
            if (!MovingDayStars.TryGetValue(level, out int best) || stars > best) MovingDayStars[level] = stars;
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public static string SceneForMode(string mode) => mode switch
        {
            "Dibs" or "Duos" or "MovingOut" => DibsScene,
            "MovingDay" => MovingDayScene,
            "Tutorial" => TutorialScene,
            "Hub" => HubScene,
            _ => throw new System.ArgumentException($"Unknown mode '{mode}'.", nameof(mode)),
        };

        public static void SelectMap(string mapId)
        {
            GameConfig.Current.HouseFor(mapId);
            MapId = string.IsNullOrEmpty(mapId) ? "pinwheel" : mapId;
        }

        public static void LoadMode(string mode, string mapId = null)
        {
            var scene = SceneForMode(mode);
            if (mapId != null) SelectMap(mapId);
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

        /// <summary>Back to the house, if the house is in the build.</summary>
        public static bool GoHome()
        {
            if (!CanLoad(HubScene) || SceneManager.GetActiveScene().name == HubScene) return false;
            Load(HubScene);
            return true;
        }
    }
}
