using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Which mode is being played, and so which block of rules.json applies. Each scene plays
    /// one mode; a test or a director can override it (for example to try Duos in an empty scene).
    /// </summary>
    public static class Match
    {
        /// <summary>A mode name from rules.json, or null to use the active scene's mode.</summary>
        public static string ModeOverride { get; set; }

        public static string Mode => ModeOverride ?? ModeForScene(SceneManager.GetActiveScene().name);

        /// <summary>The rules for the current mode. Unknown modes (test scenes) play by the defaults.</summary>
        public static GameRules Rules => GameConfig.Current.RulesFor(Mode);

        public static string ModeForScene(string scene) => scene switch
        {
            Session.HubScene => "Hub",
            Session.DibsScene => "Dibs",
            Session.TutorialScene => "Tutorial",
            Session.MovingDayScene => "MovingDay",
            _ => "Default",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => ModeOverride = null;
    }
}
