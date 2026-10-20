using System;
using UnityEngine;

namespace Wreckabulary.Online
{
    public static class WordboundBodyMotion
    {
        public static void Apply(Rigidbody body, bool kinematic, bool thrown)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (kinematic && body.collisionDetectionMode != CollisionDetectionMode.Discrete)
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (body.isKinematic != kinematic) body.isKinematic = kinematic;
            var collisionMode = !kinematic && thrown ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            if (body.collisionDetectionMode != collisionMode) body.collisionDetectionMode = collisionMode;
        }
    }
}
