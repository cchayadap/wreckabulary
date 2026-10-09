using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Creates smashable letter-built furniture, at edit time (scene builder) or at runtime (tutorial).</summary>
    public static class Furniture
    {
        public static Smashable Create(Transform parent, string word, Vector3 position, float yaw, Vector3 blockSize,
                                       int perRow, Color color, float mass, float health)
        {
            var built = LetterBuilt.Spawn(word, blockSize, perRow, color, parent);
            built.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var rb = built.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var smash = built.gameObject.AddComponent<Smashable>();
            smash.Init(word, health);
            return smash;
        }

        /// <summary>A static letter-built prop (desk, sign) that can't be moved or smashed.</summary>
        public static LetterBuilt Prop(Transform parent, string word, Vector3 position, float yaw, Vector3 blockSize,
                                       int perRow, Color color)
        {
            var built = LetterBuilt.Spawn(word, blockSize, perRow, color, parent);
            built.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return built;
        }
    }
}
