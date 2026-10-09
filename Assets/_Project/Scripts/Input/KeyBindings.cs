using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary
{
    public static class KeyBindings
    {
        public const string OverridesKey = "wv.keys", SensitivityKey = "wv.look.sensitivity", InvertKey = "wv.look.invert";
        public const string LegacyBackupKey = "wv.keys.legacy";
        public const float MinSensitivity = .25f, MaxSensitivity = 3f, SensitivityStep = .25f;

        public static float Sensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static event Action Changed;
        public static int Version { get; private set; }

        static InputActionRebindingExtensions.RebindingOperation pending;
        static int quietUntil = -1;
        static int inputLeases;
        static ButtonControl releaseControl;
        public static string LoadWarning { get; private set; }

        public static bool Listening => pending != null;
        public static bool Busy
        {
            get
            {
                if (pending != null || Time.frameCount <= quietUntil) return true;
                if (releaseControl != null && (!releaseControl.device.added || !releaseControl.isPressed)) releaseControl = null;
                return releaseControl != null;
            }
        }
        public static bool GameplayBlocked => inputLeases > 0 || Busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending?.Dispose();
            pending = null;
            quietUntil = -1;
            inputLeases = 0;
            releaseControl = null;
            LoadWarning = null;
            Version = 0;
            Changed = null;
            Sensitivity = 1f;
            InvertY = false;
        }

        public static IEnumerable<(string label, InputAction action, int binding)> Rows(DesktopBinding keys) => Rows(keys.Map);

        static IEnumerable<(string label, InputAction action, int binding)> Rows(InputActionMap map)
        {
            var move = map.FindAction("Move");
            if (move != null)
                foreach (var row in new[] { ("Move forward", "Up"), ("Move back", "Down"), ("Move left", "Left"), ("Move right", "Right") })
                    yield return (row.Item1, move, Part(move, row.Item2));
            foreach (var row in new[] {
                ("Smash, throw, place, block", "Attack"), ("Aim (hold)", "Aim"), ("Jump", "Jump"), ("Dodge", "Dodge"),
                ("Pick up, hold to revive", "Interact"), ("Spell a word", "Spell"), ("Drop gear (hold)", "Drop"),
                ("Hand 1", "Hand 1"), ("Hand 2", "Hand 2"), ("Bag and map (hold)", "Bag"), ("Pause", "Pause") })
            {
                var action = map.FindAction(row.Item2);
                if (action != null && action.bindings.Count > 0) yield return (row.Item1, action, 0);
            }
        }

        /// <summary>Programmatic bindings need repeatable IDs for Unity's override JSON to survive a fresh map.</summary>
        public static void IdentifyBindings(InputActionMap map)
        {
            using var hash = MD5.Create();
            foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    string identity = "Wreckabulary/" + map.name + "/" + action.name + "/" +
                        (binding.isPartOfComposite ? binding.name : binding.path);
                    binding.id = new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(identity)));
                    action.ChangeBinding(i).To(binding);
                }
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
            float savedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            Sensitivity = float.IsNaN(savedSensitivity) || float.IsInfinity(savedSensitivity)
                ? 1f : Mathf.Clamp(savedSensitivity, MinSensitivity, MaxSensitivity);
            InvertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            map.RemoveAllBindingOverrides();
            LoadWarning = null;
            string json = PlayerPrefs.GetString(OverridesKey, "");
            if (json.Length == 0) return;
            try
            {
                var saved = JsonUtility.FromJson<SavedOverrides>(json);
                if (saved?.bindings == null) throw new FormatException("Missing bindings.");
                bool migrated = false, unresolved = false;
                foreach (var entry in saved.bindings)
                {
                    if (entry == null || !Bindable(entry.path)) { unresolved = true; continue; }
                    InputAction found = null;
                    int index = -1;
                    foreach (var (_, action, binding) in Rows(map))
                        if (string.Equals(action.bindings[binding].id.ToString(), entry.id, StringComparison.OrdinalIgnoreCase))
                        { found = action; index = binding; break; }
                    if (found == null)
                    {
                        string actionName = entry.action?.Substring(entry.action.LastIndexOf('/') + 1);
                        var legacy = string.IsNullOrEmpty(actionName) ? null : map.FindAction(actionName);
                        // Old composite IDs cannot identify which movement direction was edited; retain a backup instead of guessing.
                        if (legacy != null && legacy.bindings.Count == 1) { found = legacy; index = 0; migrated = true; }
                    }
                    if (found != null) found.ApplyBindingOverride(index, entry.path);
                    else unresolved = true;
                }
                if (unresolved)
                {
                    PlayerPrefs.SetString(LegacyBackupKey, json);
                    LoadWarning = "Some older keys could not be restored. Please reassign them here.";
                }
                if (migrated || unresolved) Save(map, false);
            }
            catch (Exception)
            {
                map.RemoveAllBindingOverrides();
                PlayerPrefs.SetString(LegacyBackupKey, json);
                PlayerPrefs.DeleteKey(OverridesKey);
                LoadWarning = "Saved controls were unreadable. Defaults are ready to edit.";
                PlayerPrefs.Save();
            }
        }

        [Serializable] sealed class SavedOverrides { public SavedBinding[] bindings; }
        [Serializable] sealed class SavedBinding { public string action, id, path; }

        static bool Bindable(string path) => !string.IsNullOrEmpty(path) &&
            ((path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("/anyKey", StringComparison.OrdinalIgnoreCase)) ||
             path is "<Mouse>/leftButton" or "<Mouse>/rightButton" or "<Mouse>/middleButton" or "<Mouse>/backButton" or "<Mouse>/forwardButton");

        public static Vector2 Look(Vector2 delta) =>
            new Vector2(delta.x, InvertY ? delta.y : -delta.y) * (ShoulderView.MouseSensitivity * Sensitivity);

        public static void SetSensitivity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 1f;
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
            if (action == null || action.actionMap == null) throw new ArgumentException("A mapped action is required.", nameof(action));
            if (binding < 0 || binding >= action.bindings.Count || action.bindings[binding].isComposite) throw new ArgumentOutOfRangeException(nameof(binding));
            if (!Bindable(path)) throw new ArgumentException("Choose a keyboard key or mouse button.", nameof(path));
            var map = action.actionMap;
            string before = action.bindings[binding].effectivePath;
            if (string.Equals(before, path, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var (_, other, index) in Rows(map))
                if ((other != action || index != binding) && string.Equals(other.bindings[index].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    other.ApplyBindingOverride(index, before);
            action.ApplyBindingOverride(binding, path);
            Save(map);
        }

        public static void Listen(InputAction action, int binding, Action done, Func<InputControl, bool> accept = null)
        {
            Stop();
            var map = action.actionMap;
            bool wasOn = map.enabled;
            map.Disable();
            pending = action.PerformInteractiveRebinding(binding)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>/*")
                .WithControlsHavingToMatchPath("<Mouse>/*")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Touchscreen>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed()
                .OnPotentialMatch(op =>
                {
                    if (accept != null)
                        for (int i = op.candidates.Count - 1; i >= 0; i--)
                            if (!accept(op.candidates[i])) op.RemoveCandidate(op.candidates[i]);
                    if (op.candidates.Count > 0) op.Complete();
                })
                .OnApplyBinding((op, path) => Assign(action, binding, path))
                .OnComplete(op => Finish(op, map, wasOn, done))
                .OnCancel(op => Finish(op, map, wasOn, done));
            pending.Start();
        }

        static void Finish(InputActionRebindingExtensions.RebindingOperation operation, InputActionMap map, bool wasOn, Action done)
        {
            if (pending != operation) return;
            releaseControl = operation.selectedControl as ButtonControl;
            if (Keyboard.current?.escapeKey.isPressed == true) releaseControl = Keyboard.current.escapeKey;
            pending = null;
            operation.Dispose();
            quietUntil = Time.frameCount + 1;
            if (wasOn) map.Enable();
            done?.Invoke();
        }

        public static void Stop() => pending?.Cancel();

        public static void CancelFor(InputActionMap map)
        { if (pending != null && pending.action.actionMap == map) Stop(); }

        public static void QuietInput(ButtonControl untilReleased = null)
        {
            quietUntil = Time.frameCount + 1;
            if (untilReleased != null) releaseControl = untilReleased;
        }

        public static IDisposable BlockGameplay()
        { inputLeases++; return new InputLease(); }

        sealed class InputLease : IDisposable
        {
            bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                inputLeases = Mathf.Max(0, inputLeases - 1);
                QuietInput();
            }
        }

        public static void ResetToDefaults()
        {
            Stop();
            DesktopBinding.Shared.Map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            PlayerPrefs.DeleteKey(SensitivityKey);
            PlayerPrefs.DeleteKey(InvertKey);
            PlayerPrefs.DeleteKey(LegacyBackupKey);
            PlayerPrefs.Save();
            Sensitivity = 1f;
            InvertY = false;
            LoadWarning = null;
            Notify();
        }

        static void Notify()
        {
            Version++;
            Changed?.Invoke();
        }

        static void Save(InputActionMap map, bool notify = true)
        {
            PlayerPrefs.SetString(OverridesKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            if (notify) Notify();
        }
    }
}
