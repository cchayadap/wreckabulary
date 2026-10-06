using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    public static class KeyBindings
    {
        public const string OverridesKey = "wv.keys", SensitivityKey = "wv.look.sensitivity", InvertKey = "wv.look.invert";
        public const float MinSensitivity = .25f, MaxSensitivity = 3f, SensitivityStep = .25f;

        public static float Sensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static event Action Changed;
        public static int Version { get; private set; }

        static InputActionRebindingExtensions.RebindingOperation pending;
        static int quietUntil = -1;

        public static bool Listening => pending != null;
        public static bool Busy => pending != null || Time.frameCount <= quietUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending?.Dispose();
            pending = null;
            quietUntil = -1;
            Changed = null;
            Sensitivity = 1f;
            InvertY = false;
        }

        public static IEnumerable<(string label, InputAction action, int binding)> Rows(DesktopBinding keys)
        {
            yield return ("Move forward", keys.Move, Part(keys.Move, "Up"));
            yield return ("Move back", keys.Move, Part(keys.Move, "Down"));
            yield return ("Move left", keys.Move, Part(keys.Move, "Left"));
            yield return ("Move right", keys.Move, Part(keys.Move, "Right"));
            yield return ("Smash, throw, place, block", keys.Attack, 0);
            yield return ("Aim (hold)", keys.Aim, 0);
            yield return ("Jump", keys.Jump, 0);
            yield return ("Dodge", keys.Dodge, 0);
            yield return ("Pick up, hold to revive", keys.Interact, 0);
            yield return ("Spell a word", keys.Spell, 0);
            yield return ("Drop gear (hold)", keys.Drop, 0);
            yield return ("Hand 1", keys.Hand1, 0);
            yield return ("Hand 2", keys.Hand2, 0);
            yield return ("Bag and map (hold)", keys.Bag, 0);
            yield return ("Pause", keys.Pause, 0);
        }

        static int Part(InputAction action, string part)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].isPartOfComposite && action.bindings[i].name == part) return i;
            return 0;
        }

        public static string KeyName(InputAction action, int binding) =>
            action.GetBindingDisplayString(binding).ToUpperInvariant();

        public static void Load(InputActionMap map)
        {
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
            InvertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            map.RemoveAllBindingOverrides();
            string json = PlayerPrefs.GetString(OverridesKey, "");
            if (json.Length == 0) return;
            try { map.LoadBindingOverridesFromJson(json); }
            catch (Exception)
            {
                map.RemoveAllBindingOverrides();
                PlayerPrefs.DeleteKey(OverridesKey);
            }
        }

        public static Vector2 Look(Vector2 delta) =>
            new Vector2(delta.x, InvertY ? delta.y : -delta.y) * (ShoulderView.MouseSensitivity * Sensitivity);

        public static void SetSensitivity(float value)
        {
            Sensitivity = Mathf.Clamp(Mathf.Round(value / SensitivityStep) * SensitivityStep, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.Save();
            Notify();
        }

        public static void SetInvertY(bool on)
        {
            InvertY = on;
            PlayerPrefs.SetInt(InvertKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Notify();
        }

        public static void Assign(InputAction action, int binding, string path)
        {
            var map = action.actionMap;
            string before = action.bindings[binding].effectivePath;
            if (string.Equals(before, path, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var (_, other, index) in Rows(DesktopBinding.Shared))
                if ((other != action || index != binding) && string.Equals(other.bindings[index].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    other.ApplyBindingOverride(index, before);
            action.ApplyBindingOverride(binding, path);
            Save(map);
        }

        public static void Listen(InputAction action, int binding, Action done)
        {
            Stop();
            var map = action.actionMap;
            bool wasOn = map.enabled;
            map.Disable();
            pending = action.PerformInteractiveRebinding(binding)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/press")
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Touchscreen>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed()
                .OnApplyBinding((op, path) => Assign(action, binding, path))
                .OnComplete(op => Finish(map, wasOn, done))
                .OnCancel(op => Finish(map, wasOn, done));
            pending.Start();
        }

        static void Finish(InputActionMap map, bool wasOn, Action done)
        {
            pending?.Dispose();
            pending = null;
            quietUntil = Time.frameCount + 1;
            if (wasOn) map.Enable();
            done?.Invoke();
        }

        public static void Stop() => pending?.Cancel();

        public static void ResetToDefaults()
        {
            Stop();
            DesktopBinding.Shared.Map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            PlayerPrefs.DeleteKey(SensitivityKey);
            PlayerPrefs.DeleteKey(InvertKey);
            PlayerPrefs.Save();
            Sensitivity = 1f;
            InvertY = false;
            Notify();
        }

        static void Notify()
        {
            Version++;
            Changed?.Invoke();
        }

        static void Save(InputActionMap map)
        {
            PlayerPrefs.SetString(OverridesKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            Notify();
        }
    }
}
