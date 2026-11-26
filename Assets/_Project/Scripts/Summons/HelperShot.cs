using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HelperShot : MonoBehaviour
    {
        public const float LifeSeconds = 2.5f;
        public const float Size = .14f;
        public const float Mass = .2f;

        HelperAgent source;
        HelperSpec spec;
        PlayerController maker;
        int makerId, makerTeam;
        bool spent;

        public HelperAgent Source => source;
        public HelperSpec Spec => spec;

        public static HelperShot Fire(HelperAgent agent, Vector3 from, Vector3 aimAt)
        {
            var direction = aimAt - from;
            if (direction.sqrMagnitude < .0001f) direction = agent.transform.forward;
            direction.Normalize();
            var go = new GameObject($"{agent.Word} shot");
            go.transform.SetParent(World.Transient, false);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            LetterBlocks.Create("", Vector3.one * Size, GameAssets.I.Tinted(GameFeedback.SkillColor(agent.Word)), go.transform, true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mass;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction * agent.Spec.ShotSpeed;
            var shot = go.AddComponent<HelperShot>();
            shot.source = agent;
            shot.spec = agent.Spec;
            shot.maker = agent.Maker;
            shot.makerId = agent.MakerId;
            shot.makerTeam = agent.MakerTeam;
            var mine = go.GetComponentInChildren<Collider>();
            if (mine)
                foreach (var own in agent.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, own);
            Destroy(go, LifeSeconds);
            return shot;
        }

        void OnCollisionEnter(Collision c)
        {
            if (spent) return;
            var rb = c.rigidbody;
            if (rb && rb.GetComponent<LetterTile>()) return;
            spent = true;
            if (rb && rb.TryGetComponent(out PlayerController player) && HelperAgent.Legal(spec, makerId, makerTeam, player))
                HelperAgent.Hit(source, spec, maker, player, transform.position, HitSource.Thrown);
            Destroy(gameObject);
        }
    }
}
