using UnityEngine;

namespace Wreckabulary
{
    public static class CombatFeedbackOptions
    {
        public const string DamageKey = "wv.feedback.damage";
        public const string PointsKey = "wv.feedback.points";
        public static bool ShowDamage { get => PlayerPrefs.GetInt(DamageKey, 1) != 0; set => Save(DamageKey, value); }
        public static bool ShowPoints { get => PlayerPrefs.GetInt(PointsKey, 1) != 0; set => Save(PointsKey, value); }

        static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(DamageKey);
            PlayerPrefs.DeleteKey(PointsKey);
            PlayerPrefs.Save();
        }
    }
}
