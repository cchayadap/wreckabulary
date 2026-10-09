using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public enum SummonEndReason { Consumed, Expired, OwnerGone, ConditionEnded, Reset, Destroyed }
    /// <summary>
    /// A summon that lasts a while (SHIELD, SKATES, MAGNET…). When it wears out it falls apart
    /// into the letters it was spelled from, so anyone can grab them and spell again.
    /// </summary>
    public class SummonedThing : MonoBehaviour
    {
        static readonly List<SummonedThing> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string Word;
        public PlayerController Owner;
        public float Expires = float.MaxValue;
        /// <summary>Returns false to end early (e.g. armor used up).</summary>
        public Func<bool> KeepAlive;
        public Action Tick;
        public Action Ended;
        public Action<SummonEndReason> EndedWithReason;
        public bool ReturnsLetters = true;
        public SummonEndReason? EndReason { get; private set; }

        bool ended;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static SummonedThing Attach(GameObject go, string word, PlayerController owner, float duration)
        {
            var s = go.AddComponent<SummonedThing>();
            s.Word = word;
            s.Owner = owner;
            s.Expires = Time.time + duration;
            return s;
        }

        void Update()
        {
            if (ended) return;
            if (Owner && Owner.IsKnockedOut) { Finish(SummonEndReason.OwnerGone); return; }
            if (Time.time >= Expires) { Finish(SummonEndReason.Expired); return; }
            if (KeepAlive != null && !KeepAlive()) { Finish(SummonEndReason.ConditionEnded); return; }
            Tick?.Invoke();
        }

        public void FallApart() => Finish(SummonEndReason.Consumed);

        public void Finish(SummonEndReason reason)
        {
            if (ended) return;
            ended = true;
            EndReason = reason;
            EndedWithReason?.Invoke(reason);
            Ended?.Invoke();
            bool returnLetters = ReturnsLetters && reason != SummonEndReason.Reset && reason != SummonEndReason.Destroyed;
            if (returnLetters && TryGetComponent(out Smashable smash)) { smash.Break(); return; }

            var pool = TilePool.Instance;
            if (pool && returnLetters)
            {
                var built = GetComponent<LetterBuilt>();
                if (built && built.Blocks.Count == Word.Length) pool.BurstFrom(built.Blocks, Word, transform.position, 3f);
                else pool.Burst(Word, transform.position + Vector3.up * 0.5f, 3f);
            }
            Destroy(gameObject);
        }

        /// <summary>Removes every summon without dropping letters (round reset).</summary>
        public static void ClearAll()
        {
            foreach (var thing in All.ToArray())
                if (thing) thing.Finish(SummonEndReason.Reset);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (ended) return;
            ended = true;
            EndReason = SummonEndReason.Destroyed;
            EndedWithReason?.Invoke(SummonEndReason.Destroyed);
            Ended?.Invoke();
        }
    }
}
