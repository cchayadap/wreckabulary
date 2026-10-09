using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>Settled nearby letters float upright visually; the pooled rigidbody and collection point never move.</summary>
    public sealed class LetterPickupView : MonoBehaviour
    {
        const float ViewDistanceSquared = 100f;
        static readonly Material[] glowMaterials = new Material[3];
        static readonly Quaternion GlyphFacesCamera = Quaternion.Euler(0f, 180f, 0f);

        Rigidbody body;
        Transform visual, cameraTransform;
        Vector3 restPosition, modelCenter;
        Quaternion restRotation;
        LineRenderer halo;
        TextMeshPro undersideGlyph;
        bool undersideWasEnabled;
        float lift, settleAt, blend, phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMaterials()
        {
            for (int i = 0; i < glowMaterials.Length; i++)
            {
                if (glowMaterials[i]) Destroy(glowMaterials[i]);
                glowMaterials[i] = null;
            }
        }

        public void Configure(LetterTile tile, Transform imported, TextMeshPro underside = null)
        {
            Restore();
            body = tile.Body;
            visual = imported;
            undersideGlyph = underside;
            undersideWasEnabled = underside && underside.enabled;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            var bounds = ModelVisual.BoundsIn(visual, visual.gameObject);
            modelCenter = bounds.center;
            lift = bounds.extents.y * visual.localScale.y + .1f;
            phase = tile.Letter * .37f;
            var camera = Camera.main;
            cameraTransform = camera ? camera.transform : null;
            if (!halo)
            {
                halo = SummonEffects.Ring(transform, "Letter rarity halo", Color.white, .24f, Quaternion.Euler(90f, 0f, 0f));
                halo.startWidth = halo.endWidth = .016f;
                halo.numCornerVertices = 0;
                halo.numCapVertices = 0;
                halo.receiveShadows = false;
                halo.shadowCastingMode = ShadowCastingMode.Off;
            }
            halo.sharedMaterial = GlowMaterial(tile.Rarity);
            ResetFlight();
        }

        static Material GlowMaterial(LetterRarity rarity)
        {
            int index = rarity == LetterRarity.Legendary ? 2 : rarity == LetterRarity.Rare ? 1 : 0;
            if (glowMaterials[index]) return glowMaterials[index];
            Color color = index == 2 ? new Color(1f, .68f, .18f) : index == 1 ? new Color(.37f, .88f, .92f) : new Color(1f, .86f, .5f);
            var material = new Material(GameAssets.I.tintBase) { name = "Letter halo " + rarity, color = color };
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.25f);
            glowMaterials[index] = material;
            return material;
        }

        public void ResetFlight()
        {
            settleAt = Time.time + .4f;
            blend = 0f;
            Restore();
            if (halo) halo.enabled = false;
        }

        void OnEnable() => ResetFlight();

        void OnDisable()
        {
            Restore();
            if (halo) halo.enabled = false;
        }

        void Restore()
        {
            if (!visual) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
            if (undersideGlyph) undersideGlyph.enabled = undersideWasEnabled;
        }

        void LateUpdate()
        {
            if (!visual || !body || !cameraTransform) return;
            bool moving = body.linearVelocity.sqrMagnitude > .1f || body.angularVelocity.sqrMagnitude > .5f;
            if (moving) settleAt = Time.time + .25f;
            bool nearby = (cameraTransform.position - transform.position).sqrMagnitude <= ViewDistanceSquared;
            bool show = nearby && !moving && Time.time >= settleAt;
            blend = Mathf.MoveTowards(blend, show ? 1f : 0f, Time.deltaTime * 6f);
            if (blend <= 0f)
            {
                Restore();
                halo.enabled = false;
                return;
            }
            float bob = Mathf.Sin(Time.time * 2.7f + phase) * .025f;
            // Imported mesh glyphs are on +Z; a camera-aligned transform would expose the blank wooden back.
            visual.rotation = Quaternion.Slerp(transform.rotation * restRotation, cameraTransform.rotation * GlyphFacesCamera, blend);
            visual.position = transform.position + Vector3.up * ((lift + bob) * blend) - visual.TransformVector(modelCenter);
            if (undersideGlyph) undersideGlyph.enabled = false;
            halo.enabled = true;
            halo.transform.SetPositionAndRotation(transform.position + Vector3.up * .035f, Quaternion.Euler(90f, 0f, 0f));
            halo.transform.localScale = Vector3.one * (blend * (1f + bob));
        }
    }
}
