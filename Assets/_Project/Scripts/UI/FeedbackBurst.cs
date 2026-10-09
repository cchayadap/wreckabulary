using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>Bounded, reusable imported mesh cues. Round cleanup owns the pooled scene objects.</summary>
    public sealed class FeedbackBurst : MonoBehaviour
    {
        const int PerModelBudget = 6;
        static readonly Dictionary<string, List<FeedbackBurst>> pools = new();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static Sprite generatedSpark;
        static bool loadedSpark;

        Renderer[] renderers;
        MaterialPropertyBlock properties;
        Transform view;
        Transform sparkle;
        bool faceCamera;
        Vector3 origin;
        float began, duration, size, normalization;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPools()
        {
            pools.Clear();
            if (generatedSpark) Destroy(generatedSpark);
            generatedSpark = null;
            loadedSpark = false;
        }

        public static void Play(string model, Vector3 position, float size, Color? color = null, float seconds = .45f)
        {
            var transient = World.Transient;
            if (!pools.TryGetValue(model, out var pool)) pools[model] = pool = new List<FeedbackBurst>(PerModelBudget);
            FeedbackBurst cue = null;
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (!pool[i] || pool[i].transform.parent != transient) { pool.RemoveAt(i); continue; }
                if (!pool[i].gameObject.activeSelf) cue = pool[i];
            }
            if (!cue)
            {
                if (pool.Count >= PerModelBudget) return;
                var library = ModelLibrary.Load();
                string key = "VFX/" + model;
                if (!library || !library.Find(key)) return;
                var root = new GameObject(model + " cue");
                root.transform.SetParent(transient, false);
                var copy = ModelVisual.Spawn(key, root.transform);
                if (!copy) { Destroy(root); return; }
                var bounds = ModelVisual.BoundsIn(root.transform, copy);
                copy.transform.localPosition -= bounds.center;
                cue = root.AddComponent<FeedbackBurst>();
                cue.faceCamera = model is not ("Craft_Ring" or "Pickup_Ring" or "Soap_Puddle" or "Bomb_Warning");
                cue.normalization = 1f / Mathf.Max(.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                cue.renderers = root.GetComponentsInChildren<Renderer>();
                cue.properties = new MaterialPropertyBlock();
                foreach (var renderer in cue.renderers)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                if (model is "Impact_Star" or "Craft_Ring") cue.AddGeneratedSpark();
                pool.Add(cue);
            }
            cue.origin = position;
            cue.size = Mathf.Max(.01f, size) * cue.normalization;
            cue.duration = Mathf.Clamp(seconds, .12f, 1.2f);
            cue.began = Time.time;
            var camera = Camera.main;
            cue.view = camera ? camera.transform : null;
            if (color.HasValue)
            {
                cue.properties.SetColor(BaseColor, color.Value);
                cue.properties.SetColor(LegacyColor, color.Value);
            }
            foreach (var renderer in cue.renderers) renderer.SetPropertyBlock(color.HasValue ? cue.properties : null);
            cue.transform.SetPositionAndRotation(position, cue.faceCamera && cue.view ? cue.view.rotation : Quaternion.identity);
            cue.transform.localScale = Vector3.one * cue.size * .45f;
            cue.gameObject.SetActive(true);
        }

        void AddGeneratedSpark()
        {
            if (!loadedSpark)
            {
                loadedSpark = true;
                var texture = Resources.Load<Texture2D>("UI/Generated/impact-spark");
                if (texture) generatedSpark = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width);
            }
            if (!generatedSpark) return;
            sparkle = new GameObject("Generated impact sparkle").transform;
            sparkle.SetParent(transform, false);
            sparkle.localScale = Vector3.one / normalization;
            sparkle.localPosition = (faceCamera ? -Vector3.forward * .035f : Vector3.up * .3f) / normalization;
            var sprite = sparkle.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = generatedSpark;
            sprite.sortingOrder = 2;
        }

        void LateUpdate()
        {
            float progress = (Time.time - began) / duration;
            if (progress >= 1f) { gameObject.SetActive(false); return; }
            float pop = progress < .18f ? Mathf.Lerp(.45f, 1.12f, progress / .18f) : Mathf.Lerp(1.12f, 0f, (progress - .18f) / .82f);
            transform.position = origin + Vector3.up * (progress * .32f);
            transform.localScale = Vector3.one * (size * pop);
            if (view && faceCamera) transform.rotation = view.rotation;
            if (view && sparkle) sparkle.rotation = view.rotation;
        }
    }

}
