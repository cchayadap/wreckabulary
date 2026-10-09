using UnityEngine;

namespace Wreckabulary
{
    public static class GameSoundPacks
    {
        public const string PreferenceKey = "wv.sound.pack";
        public static readonly string[] Ids = { "default", "winter" };
        static GameSoundBank winter;
        static readonly int[] played = { -1, -1, -1 };
        static readonly int[] previewed = { -1, -1, -1 };

        public static string Selected => PlayerPrefs.GetString(PreferenceKey, "default") == "winter" ? "winter" : "default";
        public static string Label(string id) => id == "winter" ? "Winter" : "Classic";

        public static bool Select(string id)
        {
            if (id != "default" && id != "winter") return false;
            if (id == "default") PlayerPrefs.DeleteKey(PreferenceKey);
            else PlayerPrefs.SetString(PreferenceKey, id);
            PlayerPrefs.Save();
            return true;
        }

        public static bool Preview(GameCue cue = GameCue.Pickup) => GameFeedback.Preview(cue, Selected);
        public static bool Preview(string pack, GameCue cue = GameCue.Pickup) => GameFeedback.Preview(cue, pack);

        static int Slot(GameCue cue) => cue switch { GameCue.Pickup => 0, GameCue.Craft => 1, GameCue.Dodge => 2, _ => -1 };

        internal static bool TryPick(GameCue cue, string pack, bool preview, out AudioClip clip, out int index)
        {
            clip = null; index = -1;
            int slot = Slot(cue);
            if (pack != "winter" || slot < 0) return false;
            if (!winter) winter = Resources.Load<GameSoundBank>("AudioPacks/Winter");
            return winter && winter.TryNext(cue, (preview ? previewed : played)[slot], out clip, out index);
        }

        internal static void Played(GameCue cue, bool preview, int index)
        {
            int slot = Slot(cue);
            if (slot >= 0 && index >= 0) (preview ? previewed : played)[slot] = index;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            winter = null;
            for (int i = 0; i < played.Length; i++) played[i] = previewed[i] = -1;
        }
    }
}
