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
            Popup.Show(recipe.word + "!", player.OverheadPosition + Vector3.up * 0.8f, player.Color, 5f);
            CameraRig.Shake(0.06f);
            return true;
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
