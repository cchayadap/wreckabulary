using UnityEngine;

namespace Wreckabulary
{
    public static class ControlTips
    {
        public const string EnabledKey = "wv.tips";
        public const string LearnedKey = "wv.tips.learned";

        public enum Tip { Move, Look, Smash, Aim, Spell, Interact, Bag, Menu }

        static int? learned;
        static int Mask => learned ??= PlayerPrefs.GetInt(LearnedKey, 0);

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                if (value) { PlayerPrefs.DeleteKey(LearnedKey); learned = 0; }
                PlayerPrefs.Save();
            }
        }

        public static bool Learned(Tip tip) => (Mask & (1 << (int)tip)) != 0;

        public static void Learn(Tip tip)
        {
            if (Learned(tip)) return;
            learned = Mask | (1 << (int)tip);
            PlayerPrefs.SetInt(LearnedKey, learned.Value);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(LearnedKey);
            PlayerPrefs.Save();
            learned = 0;
        }

        public static Tip? Next(bool thirdPerson)
        {
            if (!Enabled) return null;
            for (var tip = Tip.Move; tip <= Tip.Menu; tip++)
                if (!Learned(tip) && (thirdPerson || tip is not (Tip.Look or Tip.Aim))) return tip;
            return null;
        }

        public static string Text(Tip tip)
        {
            var keys = DesktopBinding.Shared;
            return tip switch
            {
                Tip.Move => Line(ControlHints.MoveKeys, "move"),
                Tip.Look => Line("Mouse", "look around"),
                Tip.Smash => Line(ControlHints.KeyOf(keys.Attack), "smash, throw, place, block"),
                Tip.Aim => Line(ControlHints.KeyOf(keys.Aim), "aim"),
                Tip.Spell => Line(ControlHints.KeyOf(keys.Spell), "spell a word"),
                Tip.Interact => Line(ControlHints.KeyOf(keys.Interact), "grab & use"),
                Tip.Bag => Line(ControlHints.KeyOf(keys.Bag), "bag & map"),
                _ => Line(ControlHints.KeyOf(keys.Pause), "menu · every control is in Settings › Controls"),
            };
        }

        static string Line(string key, string action) => $"<b><color=#FFE7B8>{key}</color></b>   {action}";
    }
}
