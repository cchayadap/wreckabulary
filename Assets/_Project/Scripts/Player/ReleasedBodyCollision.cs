using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class ReleasedBodyCollision : MonoBehaviour
    {
        readonly List<(Collider item, Collider carrier)> pairs = new();
        float earliest;

        public static void Attach(Rigidbody item, PlayerController carrier)
        {
            if (item.TryGetComponent(out ReleasedBodyCollision old)) old.Restore();
            var grace = item.gameObject.AddComponent<ReleasedBodyCollision>();
            grace.earliest = Time.time + .15f;
            foreach (var mine in item.GetComponentsInChildren<Collider>())
                foreach (var other in carrier.GetComponentsInChildren<Collider>())
                    if (mine.enabled && other.enabled && !Physics.GetIgnoreCollision(mine, other))
                    {
                        Physics.IgnoreCollision(mine, other, true);
                        grace.pairs.Add((mine, other));
                    }
        }

        void FixedUpdate()
        {
            if (Time.time < earliest) return;
            foreach (var pair in pairs)
                if (pair.item && pair.carrier && pair.item.enabled && pair.carrier.enabled &&
                    pair.item.bounds.Intersects(pair.carrier.bounds)) return;
            Restore();
        }

        public void Restore()
        {
            foreach (var pair in pairs)
                if (pair.item && pair.carrier) Physics.IgnoreCollision(pair.item, pair.carrier, false);
            pairs.Clear(); enabled = false;
            Destroy(this);
        }

        void OnDestroy()
        {
            foreach (var pair in pairs)
                if (pair.item && pair.carrier) Physics.IgnoreCollision(pair.item, pair.carrier, false);
            pairs.Clear();
        }
    }
}
