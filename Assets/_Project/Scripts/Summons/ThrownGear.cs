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
        bool done, ignoringThrower;
        readonly HashSet<Rigidbody> hitTargets = new();
        LineRenderer ring;
        AudioSource beep;
        static AudioClip beepClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAudio()
        {
            if (beepClip) Destroy(beepClip);
            beepClip = null;
        }

        public static void Attach(HeldWeapon gear, PlayerController owner)
        {
            if (gear.TryGetComponent(out ThrownGear old)) old.StopTracking();
            var tracker = gear.gameObject.AddComponent<ThrownGear>();
            tracker.gear = gear;
            tracker.owner = owner;
            tracker.stats = gear.Definition.Thrown;
            tracker.thrownAt = Time.time;
            tracker.IgnoreThrower(true);
            if (!tracker.stats.Recoverable) gear.MarkSpent();
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
            if (GetComponent<ReleasedBodyCollision>()) return;
            if (!owner || ignoringThrower == ignored) return;
            ignoringThrower = ignored;
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
                if (Time.time - thrownAt >= 2f)
                {
                    if (stats.Recoverable) StopTracking();
                    else FinishImpact(false);
                }
                return;
            }
            float progress = Mathf.Clamp01((Time.time - thrownAt) / stats.FuseSeconds);
            float radius = stats.Radius;
            ring.startWidth = ring.endWidth = .035f + .025f * (.5f + .5f * Mathf.Sin(progress * Mathf.PI * 12f));
            Vector3 center = transform.position;
            if (Physics.Raycast(center, Vector3.down, out var ground, 30f, World.GroundMask, QueryTriggerInteraction.Ignore)) center.y = ground.point.y;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
            }
            if (Time.time >= nextBeep)
            {
                nextBeep = Time.time + Mathf.Lerp(0.6f, 0.12f, progress);
                if (!GameFeedback.Muted) beep.Play();
                GameFeedback.Burst("Bomb_Warning", transform.position + Vector3.up * .4f, .4f, GameFeedback.SkillColor("BOMB"), .18f);
            }
            if (Time.time < explodeAt) return;
            done = true;
            IgnoreThrower(false);
            Projectile.ExplodeAt(transform.position, owner, gear.word, stats.Damage, stats.EdgeDamage, stats.Radius, stats.Knockback, stats.BreakPower, true);
            gear.Break();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (done || stats.FuseSeconds > 0f || (stats.Recoverable && collision.relativeVelocity.magnitude < 1f)) return;
            var body = collision.rigidbody;
            if (body && body == (owner ? owner.Body : null)) return;
            if (body && hitTargets.Add(body) && body.TryGetComponent(out IDamageable target))
                target.ApplyDamage(Hits.Of(owner, body.position - transform.position, HitSource.Thrown, stats.Damage, stats.Knockback, 0.2f, stats.BreakPower, gear.word));
            if (!stats.Recoverable) FinishImpact(true);
        }

        void FinishImpact(bool impact)
        {
            if (done) return;
            done = true;
            IgnoreThrower(false);
            if (impact)
            {
                GameFeedback.Play(GameCue.Hit);
                GameFeedback.Burst("Foam_Cloud", transform.position, .85f, GameFeedback.SkillColor(gear.word), .45f);
            }
            gear.Break();
        }

        void OnDestroy() => IgnoreThrower(false);
    }
}
