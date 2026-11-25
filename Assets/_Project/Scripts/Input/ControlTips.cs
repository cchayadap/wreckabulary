using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class ControlTips
    {
        public const string EnabledKey = GameplayPrefs.TipsKey;
        public const string LearnedKey = "wv.tips.learned";
        public const string SeenKey = "wv.tips.seen";

        public enum Tip { Move, Look, Smash, Aim, Spell, Interact, Bag, Menu }

        const int TipCount = (int)Tip.Menu + 1;
        static readonly float MouseLookLearned = Mathf.Pow(5f * ShoulderView.MouseSensitivity, 2f);

        static int? learned;
        static TipRetirement clock;
        static int Mask => learned ??= PlayerPrefs.GetInt(LearnedKey, 0);
        static TipRetirement Clock => clock ??= new TipRetirement(TipCount, PlayerPrefs.GetInt(SeenKey, 0));

        static ControlTips() => GameplayPrefs.Changed += Replay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            learned = null;
            clock = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) BeginMatch();
        }

        public static bool Enabled { get => GameplayPrefs.ControlTips; set => GameplayPrefs.ControlTips = value; }

        static void Replay(string key)
        {
            if (key != GameplayPrefs.TipsKey || !GameplayPrefs.ControlTips) return;
            Forget();
            PlayerPrefs.Save();
        }

        public static bool Learned(Tip tip) => (Mask & (1 << (int)tip)) != 0;

        public static void Learn(Tip tip)
        {
            if (Learned(tip)) return;
            learned = Mask | (1 << (int)tip);
            PlayerPrefs.SetInt(LearnedKey, learned.Value);
            PlayerPrefs.Save();
        }

        public static void Reset() => GameplayPrefs.ResetToDefaults(GameplayPrefs.TipsKey);

        static void Forget()
        {
            PlayerPrefs.DeleteKey(LearnedKey);
            PlayerPrefs.DeleteKey(SeenKey);
            learned = 0;
            Clock.Clear();
        }

        public static void BeginMatch() => Clock.BeginMatch();

        public static void LearnFrom(in PlayerCommands commands, bool thirdPerson, bool bagOpen)
        {
            if (commands.move.sqrMagnitude > .25f) Learn(Tip.Move);
            if (thirdPerson && (commands.look.sqrMagnitude > .25f || commands.lookDelta.sqrMagnitude > MouseLookLearned)) Learn(Tip.Look);
            if (commands.attack) Learn(Tip.Smash);
            if (commands.aimHeld) Learn(Tip.Aim);
            if (commands.spellDown) Learn(Tip.Spell);
            if (commands.grab) Learn(Tip.Interact);
            if (bagOpen) Learn(Tip.Bag);
        }

        public static void Showing(Tip tip, float seconds)
        {
            if (Learned(tip)) return;
            int seen = Clock.Seen;
            bool spent = Clock.Show((int)tip, seconds);
            if (Clock.Seen != seen)
            {
                PlayerPrefs.SetInt(SeenKey, Clock.Seen);
                PlayerPrefs.Save();
            }
            if (spent) Learn(tip);
        }

        public static Tip? Next(bool thirdPerson) => Next(thirdPerson, DesktopBinding.Shared);

        public static Tip? Next(bool thirdPerson, InputBinding binding) => Next(thirdPerson, ControlHints.Device(binding));

        public static Tip? Next(bool thirdPerson, ControlDevice device)
        {
            if (!Enabled) return null;
            for (var tip = Tip.Move; tip <= Tip.Menu; tip++)
            {
                if (Learned(tip) || !Teaches(tip, thirdPerson, device)) continue;
                if (Clock.Spent((int)tip)) { Learn(tip); continue; }
                return tip;
            }
            return null;
        }

        static bool Teaches(Tip tip, bool thirdPerson, ControlDevice device) =>
            (thirdPerson || tip is not (Tip.Look or Tip.Aim)) && (device == ControlDevice.MouseKeyboard || KeyFor(tip, device).Length > 0);

        static string KeyFor(Tip tip, ControlDevice device) => ControlHints.For(device, tip switch
        {
            Tip.Move => PlayerControl.Move,
            Tip.Look => PlayerControl.Look,
            Tip.Smash => PlayerControl.Attack,
            Tip.Aim => PlayerControl.Aim,
            Tip.Spell => PlayerControl.Spell,
            Tip.Interact => PlayerControl.Grab,
            Tip.Bag => PlayerControl.Book,
            _ => PlayerControl.Pause,
        });

        public static string Text(Tip tip) => Text(tip, DesktopBinding.Shared);

        public static string Text(Tip tip, InputBinding binding) => Text(tip, ControlHints.Device(binding));

        public static string Text(Tip tip, ControlDevice device)
        {
            bool mouse = device == ControlDevice.MouseKeyboard;
            string key = KeyFor(tip, device);
            return tip switch
            {
                Tip.Move => Line(key, "move"),
                Tip.Look => Line(key, "look around"),
                Tip.Smash => Line(key, mouse ? "smash, throw, place, block" : "smash, throw, place"),
                Tip.Aim => Line(key, mouse ? "hold to aim, let go to throw" : "aim"),
                Tip.Spell => Line(key, ControlWords.HoldsToSpell(device) ? "hold to spell" : "spell a word"),
                Tip.Interact => Line(key, "grab & use"),
                Tip.Bag => Line(key, device == ControlDevice.Touch ? "bag & recipe book" : "hold for bag & recipe book"),
                _ => Line(key, "menu · every control is in Settings › Controls"),
            };
        }

        static string Line(string key, string action) => $"<b><color=#FFE7B8>{key}</color></b>   {action}";
    }
}
