using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class SmashableLabel : MonoBehaviour
    {
        public const float NearDistance = 4f, OverheadDistance = 6f, FadeBand = 1f, UpperStorey = 1.8f;
        public const float ShowingHeadStart = 1f, LetterWidth = .78f, Padding = .2f;
        const float ScreenEm = .028f, MinEm = .12f, MaxEm = 1f, Gap = .25f, FadeSpeed = 4f, Opacity = .9f;
        static readonly Color Ink = new(1f, .97f, .91f);
        static readonly List<SmashableLabel> live = new();
        static readonly List<SmashableLabel> wanted = new();
        static readonly List<Rect> kept = new();
        static readonly System.Comparison<SmashableLabel> NearestFirst =
            (a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.order.CompareTo(b.order);
        static int resolvedFrame = -1, nextOrder;
        static Transform root;
        static Material material, onTop;
        static Material materialSource;

        Smashable smashable;
        Rigidbody body;
        TextMeshPro text;
        Camera view;
        float rise = -1f, alpha, visibility, rank;
        bool overhead, crowded;
        int order;

        public TextMeshPro Text => text;
        public float Alpha => alpha;
        public bool Crowded => crowded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            root = null;
            if (material) Destroy(material);
            if (onTop) Destroy(onTop);
            material = onTop = null; materialSource = null;
            live.Clear(); wanted.Clear(); kept.Clear();
            resolvedFrame = -1;
            nextOrder = 0;
        }

        public static float Visibility(bool overhead, float nearestPlayer, float heightAbovePlayers)
        {
            if (overhead && heightAbovePlayers > UpperStorey) return 0f;
            float reach = overhead ? OverheadDistance : NearDistance;
            return Mathf.Clamp01((reach + FadeBand * .5f - nearestPlayer) / FadeBand);
        }

        public static Rect ScreenBox(Vector2 centre, float lineHeight, int letters)
        {
            float width = lineHeight * (LetterWidth * Mathf.Max(1, letters) + Padding * 2f), height = lineHeight * (1f + Padding * 2f);
            return new Rect(centre.x - width * .5f, centre.y - height * .5f, width, height);
        }

        public static bool Crowds(Rect box, IReadOnlyList<Rect> taken)
        {
            for (int i = 0; i < taken.Count; i++)
                if (box.Overlaps(taken[i])) return true;
            return false;
        }

        void Awake()
        {
            smashable = GetComponent<Smashable>();
            body = GetComponent<Rigidbody>();
            order = nextOrder++;
        }

        void OnEnable() => live.Add(this);

        void OnDisable()
        {
            live.Remove(this);
            alpha = 0f;
            if (text) text.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (text) Destroy(text.gameObject);
        }

        void LateUpdate()
        {
            if (resolvedFrame != Time.frameCount) Resolve();
            var camera = view;
            alpha = Mathf.MoveTowards(alpha, crowded ? 0f : visibility, Time.unscaledDeltaTime * FadeSpeed);
            if (alpha <= 0f || !camera)
            {
                if (text && text.gameObject.activeSelf) text.gameObject.SetActive(false);
                return;
            }
            if (!Ensure()) return;
            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            if (text.text != smashable.Word) text.text = smashable.Word;
            var shared = overhead ? onTop : material;
            if (text.fontSharedMaterial != shared) text.fontSharedMaterial = shared;
            var eye = camera.transform;
            var at = LabelPoint();
            float span = camera.orthographic ? camera.orthographicSize
                : Mathf.Max(.1f, Vector3.Dot(at - eye.position, eye.forward)) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            text.transform.SetPositionAndRotation(at, eye.rotation);
            text.transform.localScale = Vector3.one * Mathf.Clamp(ScreenEm * 2f * span, MinEm, MaxEm);
            text.alpha = alpha * Opacity;
        }

        static void Resolve()
        {
            resolvedFrame = Time.frameCount;
            wanted.Clear();
            kept.Clear();
            foreach (var label in live)
            {
                label.crowded = false;
                label.visibility = label.Target(out label.view, out float nearest);
                label.rank = nearest - (label.alpha > .5f ? ShowingHeadStart : 0f);
                if (label.visibility > 0f) wanted.Add(label);
            }
            wanted.Sort(NearestFirst);
            foreach (var label in wanted)
            {
                var camera = label.view;
                var at = camera.WorldToScreenPoint(label.LabelPoint());
                if (at.z <= 0f) { label.crowded = true; continue; }
                var box = ScreenBox(at, ScreenEm * camera.pixelHeight, label.smashable.Word.Length);
                if (Crowds(box, kept)) label.crowded = true;
                else kept.Add(box);
            }
        }

        Vector3 LabelPoint()
        {
            if (rise < 0f) rise = Rise();
            return body.worldCenterOfMass + Vector3.up * rise;
        }

        float Target(out Camera camera, out float nearest)
        {
            camera = null;
            nearest = float.PositiveInfinity;
            if (!smashable || smashable.IsBroken || !body) return 0f;
            if (GameHud.Active && GameHud.Active.BagOpen) return 0f;
            var rig = CameraRig.Instance;
            bool rigged = rig && rig.isActiveAndEnabled && rig.ViewCamera;
            camera = rigged ? rig.ViewCamera : Camera.main;
            if (!camera) return 0f;
            var at = body.worldCenterOfMass;
            float highest = float.NegativeInfinity;
            foreach (var player in World.Players)
            {
                if (!player || !player.isActiveAndEnabled) continue;
                if (player.Combat && player.Combat.Held == body) return 0f;
                if (!CameraRig.IsHuman(player) || player.IsEliminated) continue;
                var feet = player.transform.position;
                nearest = Mathf.Min(nearest, Vector3.Distance(feet, at));
                highest = Mathf.Max(highest, feet.y);
            }
            if (float.IsNegativeInfinity(highest)) return 0f;
            overhead = !rigged || !rig.IsThirdPerson;
            return Visibility(overhead, nearest, at.y - highest);
        }

        bool Ensure()
        {
            if (text) return true;
            var font = GameAssets.I ? GameAssets.I.font : null;
            if (!font || !font.material) return false;
            if (!root) root = new GameObject("Furniture labels").transform;
            var label = new GameObject("Label " + smashable.Word);
            label.transform.SetParent(root, false);
            text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSharedMaterial = LabelMaterial(font.material);
            text.fontSize = 10f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Ink;
            text.rectTransform.sizeDelta = new Vector2(20f, 2f);
            text.text = smashable.Word;
            var shape = text.GetComponent<MeshRenderer>();
            shape.shadowCastingMode = ShadowCastingMode.Off;
            shape.receiveShadows = false;
            return true;
        }

        float Rise()
        {
            float top = float.NegativeInfinity;
            foreach (var part in GetComponentsInChildren<Renderer>())
                if (part.enabled) top = Mathf.Max(top, part.bounds.max.y);
            float centre = body.worldCenterOfMass.y;
            return (float.IsNegativeInfinity(top) ? .6f : Mathf.Max(.3f, top - centre)) + Gap;
        }

        static Material LabelMaterial(Material source)
        {
            if (material && materialSource == source) return material;
            if (material) Destroy(material);
            if (onTop) Destroy(onTop);
            materialSource = source;
            material = new Material(source) { name = "Furniture label" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", new Color(.04f, .12f, .12f, .9f));
            material.SetFloat("_OutlineWidth", .22f);
            material.SetFloat("_FaceDilate", .1f);
            ShaderUtilities.UpdateShaderRatios(material);
            onTop = new Material(material) { name = "Furniture label overhead" };
            onTop.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            return material;
        }
    }
}
