using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class Match
    {
        public static string ModeOverride { get; set; }

        public static string Mode => ModeOverride ?? ModeForScene(SceneManager.GetActiveScene().name);

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
