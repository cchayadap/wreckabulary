using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundStoryView : MonoBehaviour
    {
        WordboundGameplayController controller;
        WordboundStoryPlan plan;
        Camera viewCamera;
        string actor, signature;
        GameObject root;
        readonly List<TextMesh> signs = new List<TextMesh>();
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        float bannerUntil;
        WordboundStoryNotice banner;
        int currentLap;
        public IReadOnlyList<WordboundStoryNotice> Journal { get; private set; } = Array.Empty<WordboundStoryNotice>();

        public void Bind(WordboundGameplayController gameplay, ResolvedCourse course, ItemCatalogue items,
            string localActor, Camera camera)
        {
            if (controller) throw new InvalidOperationException("Story view is already bound.");
            controller = gameplay ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            viewCamera = camera; actor = localActor; plan = new WordboundStoryPlan(course, items);
            controller.FrameChanged += Apply;
            Apply(controller.Frame);
        }

        void Apply(MatchSnapshot frame)
        {
            if (frame == null) return;
            var player = frame.Progress.Players.Single(p => p.Id == actor);
            var progress = frame.Progress.Teams.Single(t => t.Team == player.Team);
            string next = progress.Lap + "/" + progress.NextCheckpoint + "/" + string.Join(",", frame.NarrativeFlags);
            if (next == signature) return;
            bool first = signature == null;
            signature = next;
            currentLap = Math.Min(progress.Lap + 1, 3);
            Journal = plan.Journal(frame, actor);
            var newest = Journal.LastOrDefault();
            if (!first && newest != null && !seen.Contains(newest.Id))
            {
                banner = newest;
                bannerUntil = Time.unscaledTime + 8;
            }
            foreach (var notice in Journal) seen.Add(notice.Id);
            ClearSigns();
        }

        void LateUpdate()
        {
            var world = controller ? controller.CourseWorld : null;
            if (!controller || plan.Notices.Count == 0 || controller.Fault != "" || controller.IsSimulationPaused || !world ||
                !world.gameObject.scene.isLoaded || !viewCamera)
            { if (root) root.SetActive(false); return; }
            if (!root)
            {
                root = new GameObject("Wordbound parcel notices") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(root, world.gameObject.scene);
                root.transform.SetParent(world.transform, false);
                foreach (var notice in plan.Notices.Where(n => n.Lap == currentLap))
                {
                    var sign = new GameObject("Parcel notice " + notice.Id) { hideFlags = HideFlags.DontSave };
                    sign.transform.SetParent(root.transform, false);
                    sign.transform.localPosition = new Vector3(notice.Position.X / 1000f,
                        notice.Position.Y / 1000f + 2.6f, notice.Position.Z / 1000f);
                    var label = sign.AddComponent<TextMesh>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    var renderer = label.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = label.font.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    label.text = notice.Place + "\n" + Wrap(notice.Clue, 42);
                    label.fontSize = 48; label.characterSize = .026f;
                    label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                    label.richText = false;
                    label.color = new Color(1f, .9f, .63f);
                    signs.Add(label);
                }
            }
            root.SetActive(true);
            foreach (var sign in signs)
            {
                var delta = sign.transform.position - viewCamera.transform.position;
                bool nearby = delta.sqrMagnitude < 400f;
                sign.gameObject.SetActive(nearby);
                if (nearby && delta.sqrMagnitude > .001f)
                    sign.transform.rotation = Quaternion.LookRotation(delta.normalized, viewCamera.transform.up);
            }
        }

        static string Wrap(string text, int width)
        {
            var lines = new List<string>(); string line = "";
            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + word.Length + 1 > width)
                { lines.Add(line); line = ""; }
                line += (line.Length == 0 ? "" : " ") + word;
            }
            if (line.Length > 0) lines.Add(line);
            return string.Join("\n", lines);
        }

        void OnGUI()
        {
            if (banner == null || Time.unscaledTime >= bannerUntil || !controller ||
                controller.IsSimulationPaused || controller.Fault != "") return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .6f, 1.8f);
            Matrix4x4 saved = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                GUILayout.BeginArea(new Rect(Screen.width / scale - 330, 18, 312, 140), GUI.skin.box);
                GUILayout.Label(banner.Place + " · clue found");
                GUILayout.Label(banner.Clue, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.Label("Read collected clues in the equipment menu.");
                GUILayout.EndArea();
            }
            finally { GUI.matrix = saved; }
        }

        void ClearSigns()
        {
            signs.Clear();
            if (!root) return;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            root = null;
        }
        void OnDisable() => ClearSigns();
        void OnDestroy()
        {
            if (controller) controller.FrameChanged -= Apply;
            ClearSigns();
        }
    }
}
