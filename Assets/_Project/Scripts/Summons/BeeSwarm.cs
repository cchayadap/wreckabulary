using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>BEES: a buzzing cloud of B, E, E, S that chases the nearest other player and stings once for 6 damage.</summary>
    public class BeeSwarm : MonoBehaviour
    {
        PlayerController owner;
        readonly List<Transform> bees = new();
        readonly List<Vector3> offsets = new();
        float speed = 4.5f;

        public static void Spawn(PlayerController owner)
        {
            var go = new GameObject("BEES");
            go.transform.SetParent(World.Transient, false);
            go.transform.position = owner.OverheadPosition;
            var swarm = go.AddComponent<BeeSwarm>();
            swarm.owner = owner;

            var mat = GameAssets.I.Tinted(new Color(0.98f, 0.8f, 0.2f));
            foreach (char c in "BEES")
            {
                var b = LetterBlocks.Create(c.ToString(), Vector3.one * 0.22f, mat, go.transform, false);
                swarm.bees.Add(b.transform);
                swarm.offsets.Add(Random.insideUnitSphere * 0.6f);
            }
            Sfx.Play(Sound.Bees, owner.transform.position);
            var thing = SummonedThing.Attach(go, "BEES", owner, 8f);
            thing.Tick = swarm.Chase;
        }

        void Chase()
        {
            var target = World.NearestOpponent(owner, transform.position);
            var goal = target ? target.transform.position + Vector3.up * 1f : owner.OverheadPosition + Vector3.up;
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
            speed += Time.deltaTime * 0.5f;

            for (int i = 0; i < bees.Count; i++)
            {
                float t = Time.time * 7f + i * 1.7f;
                bees[i].localPosition = offsets[i] + new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f) * 0.5f, Mathf.Cos(t)) * 0.25f;
                bees[i].rotation = Quaternion.LookRotation(World.Flat(goal - transform.position) + Vector3.forward * 0.001f);
            }

            if (target && Vector3.Distance(transform.position, goal) < 0.7f)
            {
                target.Health.ApplyDamage(Hits.Of(owner, target.transform.position - transform.position, HitSource.Thrown, 6f, 2.5f, 0.2f));
                GetComponent<SummonedThing>().FallApart();
            }
        }
    }
}
