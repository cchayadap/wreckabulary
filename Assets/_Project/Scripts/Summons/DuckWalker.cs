using UnityEngine;

namespace Wreckabulary
{
    /// <summary>DUCK: a waddling decoy that bumps into opponents until someone smashes it.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DuckWalker : MonoBehaviour
    {
        PlayerController owner;
        Rigidbody body;
        float nextBump;

        public static void Spawn(PlayerController owner)
        {
            var built = LetterBuilt.Spawn("DUCK", Vector3.one * 0.32f, 2, new Color(0.98f, 0.82f, 0.25f), World.Transient);
            built.transform.SetPositionAndRotation(owner.transform.position + owner.Facing * 1.2f + Vector3.up * 0.2f,
                                                   Quaternion.LookRotation(owner.Facing));
            var rb = built.gameObject.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            built.gameObject.AddComponent<Smashable>().Init("DUCK", 15f);
            var duck = built.gameObject.AddComponent<DuckWalker>();
            duck.owner = owner;
            SummonedThing.Attach(built.gameObject, "DUCK", null, 12f);
        }

        void Awake() => body = GetComponent<Rigidbody>();

        void FixedUpdate()
        {
            var target = World.NearestOpponent(owner, transform.position);
            if (!target) return;
            var dir = World.Flat(target.transform.position - transform.position);
            if (dir.sqrMagnitude < 0.001f) return;

            var v = body.linearVelocity;
            var h = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), dir.normalized * 3f, 20f * Time.fixedDeltaTime);
            body.linearVelocity = new Vector3(h.x, v.y, h.z);
            body.MoveRotation(Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * 8f));

            if (dir.magnitude < 0.9f && Time.time > nextBump)
            {
                nextBump = Time.time + 1f;
                target.Knock(dir.normalized * 6f + Vector3.up * 2f, 0.5f);
                Popup.Show("QUACK", transform.position + Vector3.up, Color.white, 2.5f);
            }
        }
    }
}
