using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// How furniture words look when spelled into existence (Moving Day). Unknown words get a plain
    /// wooden row of blocks, so new checklist items work without code changes.
    /// </summary>
    public static class FurnitureCatalog
    {
        struct Spec
        {
            public Vector3 block;
            public int perRow;
            public Color color;
            public float mass;
        }

        static readonly Dictionary<string, Spec> Specs = new()
        {
            ["BED"] = new Spec { block = new Vector3(0.8f, 0.5f, 1.6f), color = new Color(0.45f, 0.6f, 0.85f), mass = 5f },
            ["SOFA"] = new Spec { block = new Vector3(0.75f, 0.7f, 0.8f), color = new Color(0.78f, 0.4f, 0.29f), mass = 5f },
            ["TABLE"] = new Spec { block = new Vector3(0.5f, 0.45f, 0.8f), color = new Color(0.66f, 0.45f, 0.29f), mass = 4f },
            ["DESK"] = new Spec { block = new Vector3(0.6f, 0.7f, 0.8f), color = new Color(0.54f, 0.35f, 0.23f), mass = 4.5f },
            ["LAMP"] = new Spec { block = Vector3.one * 0.4f, perRow = 1, color = new Color(0.95f, 0.83f, 0.54f), mass = 2f },
            ["CHAIR"] = new Spec { block = Vector3.one * 0.4f, perRow = 3, color = new Color(0.71f, 0.51f, 0.35f), mass = 3f },
            ["RUG"] = new Spec { block = new Vector3(0.9f, 0.08f, 1.4f), color = new Color(0.85f, 0.55f, 0.6f), mass = 2f },
            ["TV"] = new Spec { block = new Vector3(0.6f, 0.5f, 0.25f), color = new Color(0.25f, 0.25f, 0.3f), mass = 2f },
            ["PLANT"] = new Spec { block = Vector3.one * 0.4f, perRow = 1, color = new Color(0.44f, 0.68f, 0.35f), mass = 2.5f },
            ["CLOCK"] = new Spec { block = Vector3.one * 0.35f, perRow = 3, color = new Color(0.85f, 0.76f, 0.6f), mass = 2f },
        };

        static readonly Spec Default = new() { block = Vector3.one * 0.45f, color = new Color(0.8f, 0.6f, 0.42f), mass = 3f };

        public static Smashable Spawn(string word, Vector3 position, float yaw, Transform parent)
        {
            word = word.ToUpperInvariant();
            var spec = Specs.TryGetValue(word, out var s) ? s : Default;
            var rules = Match.Rules;
            float health = (rules.FurnitureToughnessBase + rules.FurnitureToughnessPerLetter * word.Length)
                * Smashable.HealthPerBreakPower;
            return Furniture.Create(parent, word, position, yaw, spec.block, spec.perRow, spec.color, spec.mass, health);
        }

        /// <summary>Spells a piece of furniture into existence just in front of the player.</summary>
        public static Smashable Summon(PlayerController p, string word)
        {
            var at = p.transform.position + p.Facing * 1.5f + Vector3.up * 0.2f;
            float yaw = Quaternion.LookRotation(p.Facing).eulerAngles.y;
            return Spawn(word, at, yaw, World.Transient);
        }
    }
}
