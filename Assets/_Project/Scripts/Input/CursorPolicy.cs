using UnityEngine;

namespace Wreckabulary
{
    public static class CursorPolicy
    {
        public static bool Locked { get; private set; }

        public static bool WantsLock(bool mouseLook, bool pointerNeeded, bool focused) => mouseLook && !pointerNeeded && focused;

        public static void Apply(bool locked)
        {
            if (locked == Locked && (Cursor.lockState == CursorLockMode.Locked) == locked) return;
            Locked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocked() => Locked = false;
    }
}
