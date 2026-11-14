using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Camera framing", fileName = "LobbyCamera")]
    public sealed class LobbyCameraProfile : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyCamera";
        public const float DefaultDistance = 4.2f, DefaultEyeHeight = .95f, DefaultLookHeight = .5f, DefaultFieldOfView = 30f;

        [Range(1.5f, 8f), Tooltip("Camera distance in metres. Small rooms retain their existing depth limit.")]
        public float Distance = DefaultDistance;
        [Range(.25f, 3f), Tooltip("Camera height above the avatar's standing floor, in metres.")]
        public float EyeHeight = DefaultEyeHeight;
        [Range(0f, 2f), Tooltip("Aim height above the avatar's standing floor, in metres.")]
        public float LookHeight = DefaultLookHeight;
        [Range(15f, 60f), Tooltip("Vertical field of view in degrees.")]
        public float FieldOfView = DefaultFieldOfView;

        public bool TryValidate(out string reason)
        {
            if (!Between(Distance, 1.5f, 8f)) { reason = "Distance must be between 1.5 and 8 metres."; return false; }
            if (!Between(EyeHeight, .25f, 3f)) { reason = "Eye height must be between 0.25 and 3 metres."; return false; }
            if (!Between(LookHeight, 0f, 2f)) { reason = "Look height must be between 0 and 2 metres."; return false; }
            if (!Between(FieldOfView, 15f, 60f)) { reason = "Field of view must be between 15 and 60 degrees."; return false; }
            reason = null;
            return true;
        }

        static bool Between(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
