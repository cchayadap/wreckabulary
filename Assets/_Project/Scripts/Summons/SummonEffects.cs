using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class SummonEffects
    {
        public static bool CanApply(PlayerController player, WordEntry recipe)
        {
            if (!player || recipe == null || !player.CanAct || player.IsDodging || player.Combat.IsChanneling) return false;
            recipe = player.Summoner.ResolveRecipe(recipe);
            if (recipe == null) return false;
            if (!GameConfig.Current.Items.TryGet(recipe.word, out var item) || !item.Enabled) return false;
            if (IsChecklistRecipe(player, recipe)) return true;
            if (player.Combat.IsHolding && !player.Combat.Weapon) return false;
            return player.Combat.HasFreeGearSlot;
        }

        public static bool Apply(PlayerController player, WordEntry recipe)
        {
            if (!CanApply(player, recipe)) return false;
            recipe = player.Summoner.ResolveRecipe(recipe);
            if (IsChecklistRecipe(player, recipe))
            {
                FurnitureCatalog.Summon(player, recipe.word);
            }
            else
            {
                var gear = CatalogGear.Create(GameConfig.Current.Items.Get(recipe.word));
                if (!player.Combat.TryEquip(gear))
                {
                    Object.Destroy(gear.gameObject);
                    return false;
                }
            }
            Popup.Show(recipe.word + "!", player.OverheadPosition + Vector3.up * 0.35f, GameFeedback.SkillColor(recipe.word), 3.5f);
            CameraRig.Shake(0.06f);
            return true;
        }

        /// <summary>Lightweight, collider-free line art that shares the authoritative effect's lifetime.</summary>
        public static LineRenderer Ring(Transform parent, string name, Color color, float radius, Quaternion rotation)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localRotation = rotation;
            var points = new Vector3[40];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            return Line(root.transform, color, points, true);
        }

        public static LineRenderer Line(Transform parent, Color color, Vector3[] points, bool loop = false)
        {
            var line = parent.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = line.endWidth = .035f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = GameAssets.I.Tinted(color);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        static bool IsChecklistRecipe(PlayerController player, WordEntry recipe)
        {
            if (recipe.category != WordCategory.Furniture) return false;
            var explicitWords = player.Summoner.ChecklistPlacementWords;
            if (explicitWords == null) return player.Summoner.WordsOverride != null;
            foreach (string word in explicitWords) if (word == recipe.word) return true;
            return false;
        }
    }
}
