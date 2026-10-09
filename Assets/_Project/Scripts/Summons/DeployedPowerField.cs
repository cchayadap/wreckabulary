using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Neutral, wall-blocked fields use scaled physics time and never own recipe letters.</summary>
    [DisallowMultipleComponent]
    public sealed class DeployedPowerField : MonoBehaviour
    {
        const float HeightReach = 1.25f, ContactSeconds = .18f;
        DeployStats stats;
        Rigidbody source;
        Collider[] overlaps = new Collider[32];
        RaycastHit[] walls = new RaycastHit[16];
        readonly HashSet<Rigidbody> touched = new();
        Transform visual;
        readonly LineRenderer[] ribbons = new LineRenderer[3];
        Transform clockHand;
        public DeployEffect Effect => stats.Effect;
        public float Radius => stats.Radius;
        public Transform VisualRoot => visual;

        public void Configure(ItemDefinition definition)
        {
            stats = definition.Deploy;
            source = GetComponent<Rigidbody>();
            visual = new GameObject(stats.Effect == DeployEffect.WindField ? "Fan wind field" : "Clock slow field").transform;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.up * .06f;
            var color = GameFeedback.SkillColor(definition.Id);
            var points = new Vector3[41];
            float arc = Mathf.Clamp(stats.ArcDegrees, 1f, 360f);
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.Lerp(-arc * .5f, arc * .5f, (float)i / (points.Length - 1)) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * stats.Radius;
            }
            SummonEffects.Line(visual, color, points, arc >= 359f);
            if (stats.Effect == DeployEffect.WindField)
            {
                for (int i = 0; i < ribbons.Length; i++)
                {
                    var strand = new GameObject("Moving wind ribbon").transform;
                    strand.SetParent(visual, false);
                    ribbons[i] = SummonEffects.Line(strand, color, new Vector3[12]);
                    ribbons[i].startWidth = .055f;
                    ribbons[i].endWidth = .012f;
                }
                GameFeedback.Play(GameCue.Boost);
            }
            else
            {
                for (int i = 0; i < 12; i++)
                {
                    var tick = new GameObject("Clock field tick").transform;
                    tick.SetParent(visual, false);
                    float angle = i * Mathf.PI / 6f;
                    var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    SummonEffects.Line(tick, color, new[] { radial * (stats.Radius - .18f), radial * stats.Radius });
                }
                clockHand = new GameObject("Moving clock hand").transform;
                clockHand.SetParent(visual, false);
                SummonEffects.Line(clockHand, color, new[] { Vector3.up * .01f, new Vector3(0f, .01f, stats.Radius * .8f) });
                GameFeedback.Play(GameCue.Protect);
            }
        }

        void FixedUpdate()
        {
            if (stats == null || !source) return;
            Vector3 origin = transform.position + Vector3.up * .5f;
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(origin, stats.Radius + .75f, overlaps, ~0, QueryTriggerInteraction.Ignore)) == overlaps.Length && overlaps.Length < 256)
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            touched.Clear();
            for (int i = 0; i < count; i++)
            {
                var body = overlaps[i].attachedRigidbody;
                if (!body || body == source || body.isKinematic || !touched.Add(body)) continue;
                if (Mathf.Abs(body.position.y - transform.position.y) > HeightReach) continue;
                var delta = World.Flat(body.worldCenterOfMass - transform.position);
                if (delta.sqrMagnitude > stats.Radius * stats.Radius) continue;
                if (stats.ArcDegrees < 359f && delta.sqrMagnitude > .001f &&
                    Vector3.Dot(delta.normalized, transform.forward) < Mathf.Cos(stats.ArcDegrees * .5f * Mathf.Deg2Rad)) continue;
                if (!ClearPath(origin, body)) continue;
                body.TryGetComponent<PlayerController>(out var player);
                if (player && !player.CanAct) continue;
                if (stats.Effect == DeployEffect.SlowField)
                {
                    if (player) player.Slow(stats.Strength, ContactSeconds);
                }
                else
                {
                    if (player) player.MakeSlippery(ContactSeconds);
                    if (Vector3.Dot(body.linearVelocity, transform.forward) < stats.Strength)
                        body.AddForce(transform.forward * stats.Strength, ForceMode.Acceleration);
                }
            }
        }

        bool ClearPath(Vector3 origin, Rigidbody target)
        {
            var direction = target.worldCenterOfMass - origin;
            if (direction.sqrMagnitude < .01f) return true;
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction.normalized, walls, direction.magnitude, World.GroundMask,
                QueryTriggerInteraction.Ignore)) == walls.Length && walls.Length < 128)
                System.Array.Resize(ref walls, walls.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var body = walls[i].rigidbody;
                if (body == source || body == target) continue;
                if (!body || body.isKinematic) return false;
            }
            return true;
        }

        void Update()
        {
            if (!visual || stats == null) return;
            if (clockHand) clockHand.localRotation = Quaternion.Euler(0f, -Time.time * 28f, 0f);
            if (stats.Effect != DeployEffect.WindField) return;
            for (int i = 0; i < ribbons.Length; i++)
            {
                var line = ribbons[i];
                if (!line) continue;
                float phase = Mathf.Repeat(Time.time * .9f + i / 3f, 1f);
                for (int p = 0; p < line.positionCount; p++)
                {
                    float t = p / (float)(line.positionCount - 1);
                    float z = Mathf.Repeat(phase + t * .33f, 1f) * stats.Radius;
                    float x = (i - 1) * z * .22f + Mathf.Sin(t * Mathf.PI * 2f + Time.time * 5f) * .07f;
                    line.SetPosition(p, new Vector3(x, .45f + i * .16f, z));
                }
            }
        }

        void OnEnable()
        {
            if (visual) visual.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (visual) visual.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (visual) Destroy(visual.gameObject);
            visual = null;
        }
    }
}
