using System;
using UnityEngine;

namespace Wreckabulary.Online
{
    public static class WordboundClockLineTarget
    {
        public static Vector3 FromCollider(Rigidbody fixture)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            var box = fixture.GetComponent<BoxCollider>();
            if (box == null || box.attachedRigidbody != fixture || !box.enabled || box.isTrigger ||
                !box.gameObject.activeInHierarchy)
                throw new InvalidOperationException("CLOCK machinery requires its active solid root box collider.");
            var bounds = box.bounds;
            if (!Finite(bounds.center) || !Finite(bounds.extents) ||
                bounds.extents.x <= 0 || bounds.extents.y <= 0 || bounds.extents.z <= 0)
                throw new InvalidOperationException("CLOCK machinery requires finite nonempty collider bounds.");
            return bounds.center;
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
