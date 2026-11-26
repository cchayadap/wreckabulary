using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class HelperTrap : MonoBehaviour
    {
        public const float ColliderHeight = .06f;
        public const float FloorLift = .01f;

        public float Radius { get; private set; }

        public void Configure(HelperSpec spec)
        {
            Radius = spec.Radius;
            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (TryGetComponent(out BoxCollider box))
            {
                box.size = new Vector3(box.size.x, ColliderHeight, box.size.z);
                box.center = Vector3.up * (ColliderHeight * .5f);
            }
            var rotation = transform.rotation;
            transform.SetPositionAndRotation(transform.position + Vector3.up * FloorLift, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        }

        public bool Covers(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= Radius * Radius;
        }
    }
}
