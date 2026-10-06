using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ThrownGear : MonoBehaviour
    {
        HeldWeapon gear;
        PlayerController owner;
        ThrownStats stats;
        float thrownAt, explodeAt, nextBeep;
        bool done;
        readonly HashSet<Rigidbody> hitTargets = new();
        LineRenderer ring;
        AudioSource beep;
        static AudioClip beepClip;

        public static void Attach(HeldWeapon gear, PlayerController owner)
        {
            if (gear.TryGetComponent(out ThrownGear old)) old.StopTracking();
            var tracker = gear.gameObject.AddComponent<ThrownGear>();
            tracker.gear = gear;
            tracker.owner = owner;
            tracker.stats = gear.Definition.Thrown;
            tracker.thrownAt = Time.time;
            tracker.IgnoreThrower(true);
            if (tracker.stats.FuseSeconds <= 0f) return;
            gear.MarkSpent();
            tracker.explodeAt = Time.time + tracker.stats.FuseSeconds;
            tracker.ring = gear.gameObject.AddComponent<LineRenderer>();
            tracker.ring.useWorldSpace = true;
            tracker.ring.loop = true;
            tracker.ring.positionCount = 48;
            tracker.ring.startWidth = tracker.ring.endWidth = 0.05f;
            tracker.ring.sharedMaterial = GameAssets.I.Tinted(new Color(1f, 0.35f, 0.15f));
            tracker.beep = gear.gameObject.AddComponent<AudioSource>();
            tracker.beep.volume = 0.2f;
            tracker.beep.spatialBlend = 0.7f;
            if (!beepClip)
            {
                beepClip = AudioClip.Create("Bomb fuse tick", 800, 1, 16000, false);
                var samples = new float[800];
                for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 2f * Mathf.PI * 880f / 16000f) * (1f - i / 800f);
                beepClip.SetData(samples, 0);
            }
            tracker.beep.clip = beepClip;
        }

        void IgnoreThrower(bool ignored)
        {
            if (!owner) return;
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var other in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(mine, other, ignored);
        }

        public void StopTracking()
        {
            if (done) return;
            done = true;
            IgnoreThrower(false);
            owner = null;
            if (ring) Destroy(ring);
            if (beep) Destroy(beep);
            Destroy(this);
        }

        void Update()
        {
            if (done) return;
            if (Time.time - thrownAt > 0.2f) IgnoreThrower(false);
            if (stats.FuseSeconds <= 0f)
            {
                if (Time.time - thrownAt >= 2f) StopTracking();
                return;
            }
            float progress = Mathf.Clamp01((Time.time - thrownAt) / stats.FuseSeconds);
            float radius = Mathf.Lerp(0.3f, stats.Radius, progress);
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
            }
            if (Time.time >= nextBeep)
            {
                nextBeep = Time.time + Mathf.Lerp(0.6f, 0.12f, progress);
                beep.Play();
            }
            if (Time.time < explodeAt) return;
            done = true;
            IgnoreThrower(false);
            Projectile.ExplodeAt(transform.position, owner, gear.word, stats.Damage, stats.EdgeDamage, stats.Radius, stats.Knockback, stats.BreakPower, true);
            gear.Break();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (done || stats.FuseSeconds > 0f || collision.relativeVelocity.magnitude < 1f) return;
            var body = collision.rigidbody;
            if (!body || body == (owner ? owner.Body : null) || !hitTargets.Add(body)) return;
            if (body.TryGetComponent(out IDamageable target))
                target.ApplyDamage(Hits.Of(owner, body.position - transform.position, HitSource.Thrown, stats.Damage, stats.Knockback, 0.2f, stats.BreakPower, gear.word));
        }

        void OnDestroy() => IgnoreThrower(false);
    }
}
