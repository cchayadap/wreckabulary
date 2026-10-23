using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class WordboundPuzzleGuide : MonoBehaviour
    {
        WordboundGameplayController controller;
        WordboundPuzzleGuideView view;
        ResolvedCourse course;
        Camera viewCamera;
        string actor, matchHash;
        IReadOnlyList<WordboundGuideMarker> markers = Array.Empty<WordboundGuideMarker>();
        public string Fault { get; private set; } = "";
        public int MarkerCount => view?.MarkerCount ?? 0;

        public void Bind(WordboundGameplayController gameplay, ResolvedCourse resolved, string localPlayerId,
            Camera camera = null)
        {
            if (!gameplay || resolved == null || string.IsNullOrEmpty(localPlayerId))
                throw new ArgumentException("Guide needs the gameplay controller, exact course and local player.");
            if (controller) throw new InvalidOperationException("Clear the previous guide binding first.");
            controller = gameplay; course = resolved; actor = localPlayerId; viewCamera = camera;
            controller.FrameChanged += ApplyFrame;
            controller.Faulted += GameplayFailed;
            ApplyFrame(controller.Frame);
        }

        void ApplyFrame(MatchSnapshot frame)
        {
            if (Fault != "") return;
            try
            {
                if (frame != null)
                {
                    if (matchHash != null && frame.Binding.Hash != matchHash)
                        throw new ArgumentException("Guide cannot mix different match identities.");
                    matchHash = frame.Binding.Hash;
                }
                markers = WordboundPuzzleGuidePlan.Build(course, frame, actor);
                Refresh();
            }
            catch (Exception error) { GameplayFailed(error.GetType().Name + ": " + error.Message); }
        }

        void LateUpdate()
        {
            if (controller && controller.Frame == null) markers = Array.Empty<WordboundGuideMarker>();
            Refresh();
        }

        void Refresh()
        {
            var world = controller ? controller.CourseWorld : null;
            if (!isActiveAndEnabled || Fault != "" || !controller || controller.Fault != "" ||
                controller.IsSimulationPaused || !world || !world.isActiveAndEnabled || !world.gameObject.scene.isLoaded)
            { DisposeView(); return; }
            if (world.ManifestHash != course.Hash)
            { GameplayFailed("Guide world differs from the exact course."); return; }
            try
            {
                if (view == null || view.Parent != world.transform)
                {
                    DisposeView();
                    view = new WordboundPuzzleGuideView(world.transform);
                }
                view.Show(markers);
                view.FaceLabels(viewCamera);
            }
            catch (Exception error) { GameplayFailed(error.GetType().Name + ": " + error.Message); }
        }

        void GameplayFailed(string reason)
        {
            Fault = reason;
            markers = Array.Empty<WordboundGuideMarker>();
            DisposeView();
        }

        void DisposeView() { view?.Dispose(); view = null; }
        void OnDisable() => DisposeView();
        void OnDestroy() => Clear();
        public void Clear()
        {
            if (controller)
            {
                controller.FrameChanged -= ApplyFrame;
                controller.Faulted -= GameplayFailed;
            }
            DisposeView();
            markers = Array.Empty<WordboundGuideMarker>();
            controller = null; course = null; actor = null; matchHash = null; viewCamera = null; Fault = "";
        }
    }

    public sealed class WordboundPuzzleGuideView : IDisposable
    {
        readonly List<TextMesh> labels = new List<TextMesh>();
        Material lines;
        GameObject root;
        string signature;
        bool disposed;
        public Transform Parent { get; }
        public GameObject Root => root;
        public int MarkerCount { get; private set; }

        public WordboundPuzzleGuideView(Transform courseRoot)
        {
            if (!courseRoot || !courseRoot.gameObject.scene.IsValid() || !courseRoot.gameObject.scene.isLoaded ||
                (courseRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("Guide visuals need a loaded, unit-scale course scene.");
            Parent = courseRoot;
            lines = WordboundRenderMaterials.Create(false, "Wordbound guide lines");
            if (lines.HasProperty("_BaseColor")) lines.SetColor("_BaseColor", Color.white);
            lines.color = Color.white;
            root = NewObject("Wordbound puzzle guide", null);
            SceneManager.MoveGameObjectToScene(root, courseRoot.gameObject.scene);
            root.transform.SetParent(courseRoot, false);
        }

        public void Show(IReadOnlyList<WordboundGuideMarker> markers)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WordboundPuzzleGuideView));
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            string next = string.Join("|", markers.Select(m => m.Id + "/" + m.Ready));
            if (next == signature) return;
            ClearMarkers();
            signature = next;
            foreach (var marker in markers) Draw(marker);
            MarkerCount = markers.Count;
            root.SetActive(MarkerCount != 0);
        }

        void Draw(WordboundGuideMarker marker)
        {
            var node = NewObject(marker.Id, root.transform).transform;
            Color tint = Tint(marker);
            var at = Metres(marker.Position);
            float top = at.y + 1f;
            if (marker.RadiusMm > 0)
            {
                int segments = 48;
                var ring = new Vector3[segments];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    ring[i] = at + new Vector3(Mathf.Sin(angle) * marker.RadiusMm / 1000f, .035f,
                        Mathf.Cos(angle) * marker.RadiusMm / 1000f);
                }
                Line(node, ring, tint, true);
                if (marker.Kind == WordboundGuideKind.Socket)
                {
                    var rotation = Quaternion.Euler(0, marker.Yaw, 0);
                    var start = at + Vector3.up * .055f;
                    var tip = start + rotation * Vector3.forward * Mathf.Max(.45f, marker.RadiusMm / 1000f);
                    Line(node, new[] { start, tip, tip + rotation * new Vector3(-.16f, 0, -.18f),
                        tip, tip + rotation * new Vector3(.16f, 0, -.18f) }, tint);
                }
                if (marker.Kind == WordboundGuideKind.Checkpoint || marker.Kind == WordboundGuideKind.Finish)
                    Line(node, new[] { at + Vector3.up * .04f, at + Vector3.up * 2f }, tint, width: .08f);
            }
            else
            {
                DrawBox(node, marker.Bounds, tint);
                top = Mathf.Max(top, marker.Bounds.Max.Y / 1000f + .35f);
                if (marker.Kind == WordboundGuideKind.ArmPath)
                    Line(node, new[] { Metres(marker.Position) + Vector3.up * .05f,
                        Metres(marker.End) + Vector3.up * .05f }, tint, width: .07f);
            }
            var label = NewObject(marker.Label, node).AddComponent<TextMesh>();
            label.transform.localPosition = new Vector3(at.x, top, at.z);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = marker.Label; label.fontSize = 64; label.characterSize = .025f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = tint; label.richText = false;
            label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            labels.Add(label);
        }

        void DrawBox(Transform parent, CourseBox box, Color tint)
        {
            var min = Metres(box.Min); var max = Metres(box.Max);
            float bottom = min.y + .035f, top = Mathf.Max(bottom + .035f, max.y);
            var floor = new[] { new Vector3(min.x, bottom, min.z), new Vector3(max.x, bottom, min.z),
                new Vector3(max.x, bottom, max.z), new Vector3(min.x, bottom, max.z) };
            Line(parent, floor, tint, true);
            if (max.y - min.y <= .12f) return;
            var roof = floor.Select(p => new Vector3(p.x, top, p.z)).ToArray();
            Line(parent, roof, tint, true, .025f);
            for (int i = 0; i < 4; i++) Line(parent, new[] { floor[i], roof[i] }, tint, width: .025f);
        }

        void Line(Transform parent, Vector3[] points, Color tint, bool loop = false, float width = .045f)
        {
            var line = NewObject("Guide line", parent).AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = loop; line.positionCount = points.Length;
            line.SetPositions(points); line.widthMultiplier = width; line.numCornerVertices = 2;
            line.sharedMaterial = lines; line.startColor = tint; line.endColor = tint;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", tint); properties.SetColor("_Color", tint);
            line.SetPropertyBlock(properties);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off; line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void FaceLabels(Camera camera)
        {
            if (!camera || disposed || !root || !root.activeSelf) return;
            foreach (var label in labels)
            {
                if (!label) continue;
                var delta = label.transform.position - camera.transform.position;
                if (delta.sqrMagnitude > .001f)
                    label.transform.rotation = Quaternion.LookRotation(delta.normalized, camera.transform.up);
                float distance = delta.magnitude;
                label.transform.localScale = Vector3.one * Mathf.Clamp(distance / 8f, .8f, 2f);
                label.gameObject.SetActive(distance < 55f);
            }
        }

        GameObject NewObject(string name, Transform parent)
        {
            var value = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = Parent.gameObject.layer };
            if (parent) value.transform.SetParent(parent, false);
            return value;
        }
        static Vector3 Metres(CoursePoint p) => new Vector3(p.X / 1000f, p.Y / 1000f, p.Z / 1000f);
        static Color Tint(WordboundGuideMarker marker) => marker.Kind switch
        {
            WordboundGuideKind.ArmPath => new Color(1f, .36f, .2f),
            WordboundGuideKind.Checkpoint => marker.Ready ? new Color(1f, .85f, .22f) : new Color(.75f, .73f, .65f),
            WordboundGuideKind.Finish => new Color(1f, .85f, .22f),
            WordboundGuideKind.Arrival => new Color(.35f, 1f, .55f),
            WordboundGuideKind.Pad => new Color(.9f, .55f, 1f),
            WordboundGuideKind.Source => new Color(1f, .9f, .6f),
            _ => new Color(.25f, .9f, 1f)
        };
        void ClearMarkers()
        {
            labels.Clear(); MarkerCount = 0;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                child.SetActive(false); Release(child);
            }
        }
        static void Release(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; labels.Clear(); MarkerCount = 0;
            if (root) root.SetActive(false);
            Release(root); Release(lines); root = null; lines = null;
        }
    }
}
