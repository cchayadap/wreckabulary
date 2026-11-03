using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class SmashableLabel : MonoBehaviour
    {
        public const float NearDistance = 4f, FadeBand = 1f, UpperStorey = 1.8f;
        const float ScreenEm = .028f, MinEm = .12f, MaxEm = 1f, Gap = .25f, FadeSpeed = 4f, Opacity = .9f;
        static readonly Color Ink = new(1f, .97f, .91f);
        static Transform root;
        static Material material;
        static Material materialSource;

        Smashable smashable;
        Rigidbody body;
        TextMeshPro text;
        float rise = -1f, alpha;

        public TextMeshPro Text => text;
        public float Alpha => alpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            root = null;
            if (material) Destroy(material);
            material = null; materialSource = null;
        }

        public static float Visibility(bool overhead, float nearestPlayer, float heightAbovePlayers)
        {
            if (overhead) return heightAbovePlayers > UpperStorey ? 0f : 1f;
            return Mathf.Clamp01((NearDistance + FadeBand * .5f - nearestPlayer) / FadeBand);
        }

        void Awake()
        {
            smashable = GetComponent<Smashable>();
            body = GetComponent<Rigidbody>();
        }

        void OnDisable()
        {
            alpha = 0f;
            if (text) text.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (text) Destroy(text.gameObject);
        }

        void LateUpdate()
        {
            float target = Target(out var camera);
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * FadeSpeed);
            if (alpha <= 0f || !camera)
            {
                if (text && text.gameObject.activeSelf) text.gameObject.SetActive(false);
                return;
            }
            if (!Ensure()) return;
            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            if (text.text != smashable.Word) text.text = smashable.Word;
            var view = camera.transform;
            var at = body.worldCenterOfMass + Vector3.up * rise;
            float span = camera.orthographic ? camera.orthographicSize
                : Mathf.Max(.1f, Vector3.Dot(at - view.position, view.forward)) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            text.transform.SetPositionAndRotation(at, view.rotation);
            text.transform.localScale = Vector3.one * Mathf.Clamp(ScreenEm * 2f * span, MinEm, MaxEm);
            text.alpha = alpha * Opacity;
        }

        float Target(out Camera camera)
        {
            camera = null;
            if (!smashable || smashable.IsBroken || !body) return 0f;
            var rig = CameraRig.Instance;
            bool rigged = rig && rig.isActiveAndEnabled && rig.ViewCamera;
            camera = rigged ? rig.ViewCamera : Camera.main;
            if (!camera) return 0f;
            var at = body.worldCenterOfMass;
            float nearest = float.PositiveInfinity, highest = float.NegativeInfinity;
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
            return Visibility(!rigged || !rig.IsThirdPerson, nearest, at.y - highest);
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
            rise = Rise();
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
            materialSource = source;
            material = new Material(source) { name = "Furniture label" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", new Color(.04f, .12f, .12f, .9f));
            material.SetFloat("_OutlineWidth", .22f);
            material.SetFloat("_FaceDilate", .1f);
            ShaderUtilities.UpdateShaderRatios(material);
            return material;
        }
    }
}
