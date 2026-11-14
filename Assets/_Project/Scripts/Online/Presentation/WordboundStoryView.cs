using System;
using System.Collections.Generic;
using System.Globalization;
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
        TextMesh sign;
        WordboundStoryNotice cue;
        public IReadOnlyList<WordboundStoryNotice> Journal { get; private set; } = Array.Empty<WordboundStoryNotice>();

        public WordboundStoryEpilogue Epilogue(ModeDefinition mode) => plan != null && controller && controller.IsPresentationReady
            ? plan.Epilogue(controller.Frame, actor, mode) : null;

        public void Bind(WordboundGameplayController gameplay, ResolvedCourse course, ItemCatalogue items,
            string localActor, Camera camera)
            => Bind(gameplay, course, items, localActor, camera, null);

        public void Bind(WordboundGameplayController gameplay, ResolvedCourse course, ItemCatalogue items,
            string localActor, Camera camera, WordboundStoryProfile profile, Func<string, string> localize = null)
        {
            if (controller) throw new InvalidOperationException("Story view is already bound.");
            controller = gameplay ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            viewCamera = camera; actor = localActor; plan = new WordboundStoryPlan(course, items, profile, localize);
            controller.FrameChanged += Apply;
            Apply(controller.Frame);
        }

        void Apply(MatchSnapshot frame)
        {
            if (frame == null) return;
            var player = frame.Progress.Players.Single(p => p.Id == actor);
            var progress = frame.Progress.Teams.Single(t => t.Team == player.Team);
            string next = progress.Lap + "/" + progress.NextCheckpoint + "/" + progress.Finished + "/" +
                player.Returned + "/" + player.Connected + "/" + player.Eliminated + "/" +
                frame.Progress.Migrating + "/" + (frame.Progress.Result != null) + "/" + string.Join(",", frame.NarrativeFlags);
            if (next == signature) return;
            signature = next;
            Journal = plan.Journal(frame, actor);
            var nextCue = plan.ContextualCue(frame, actor);
            if (nextCue?.Id == cue?.Id) return;
            cue = nextCue;
            ClearSigns();
        }

        void LateUpdate()
        {
            var world = controller ? controller.CourseWorld : null;
            if (!controller || cue == null || controller.Fault != "" || controller.IsSimulationPaused || !world ||
                !world.gameObject.scene.isLoaded || !viewCamera)
            { if (root) root.SetActive(false); return; }
            if (!root)
            {
                root = new GameObject("Wordbound parcel notices") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(root, world.gameObject.scene);
                root.transform.SetParent(world.transform, false);
                var noticeObject = new GameObject("Parcel notice " + cue.Id) { hideFlags = HideFlags.DontSave };
                noticeObject.transform.SetParent(root.transform, false);
                noticeObject.transform.localPosition = new Vector3(cue.Position.X / 1000f,
                    cue.Position.Y / 1000f + 2.6f, cue.Position.Z / 1000f);
                var label = noticeObject.AddComponent<TextMesh>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var renderer = label.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = label.font.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                label.text = Wrap(cue.Cue, 36);
                label.fontSize = 48; label.characterSize = .032f;
                label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.richText = false;
                label.color = new Color(1f, .9f, .63f);
                sign = label;
            }
            root.SetActive(true);
            FaceNotice(sign, viewCamera);
        }

        static void FaceNotice(TextMesh sign, Camera camera)
        {
            var delta = sign.transform.position - camera.transform.position;
            bool nearby = delta.sqrMagnitude < 400f;
            sign.gameObject.SetActive(nearby);
            if (!nearby) return;
            sign.transform.rotation = camera.transform.rotation;
            sign.transform.localScale = Vector3.one * Mathf.Clamp(delta.magnitude / 8f, .8f, 2f);
        }

        static string Wrap(string text, int width)
        {
            var lines = new List<string>(); string line = "";
            foreach (string word in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + word.Length + 1 > width)
                { lines.Add(line); line = ""; }
                string remainder = word;
                int[] elements = StringInfo.ParseCombiningCharacters(remainder);
                while (elements.Length > width)
                {
                    lines.Add(remainder.Substring(0, elements[width]));
                    remainder = remainder.Substring(elements[width]);
                    elements = StringInfo.ParseCombiningCharacters(remainder);
                }
                line += (line.Length == 0 ? "" : " ") + remainder;
            }
            if (line.Length > 0) lines.Add(line);
            return string.Join("\n", lines);
        }

        void ClearSigns()
        {
            sign = null;
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
