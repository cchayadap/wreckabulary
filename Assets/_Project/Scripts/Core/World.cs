using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Scene-wide lookups: live players, physics layers and a parent for temporary objects.</summary>
    public static class World
    {
        static Transform transient;

        // Static state survives between Play sessions when domain reload is off, so reset it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            transient = null;
            Players.Clear();
        }

        /// <summary>Parent for summons, delivery boxes and effects. Cleared between rounds.</summary>
        public static Transform Transient
        {
            get
            {
                if (!transient) transient = new GameObject("Transient").transform;
                return transient;
            }
        }

        public static void ClearTransient()
        {
            if (transient)
            {
                if (Application.isPlaying) Object.Destroy(transient.gameObject);
                else Object.DestroyImmediate(transient.gameObject);
            }
            transient = null;
        }

        public static void FreezeTransient()
        {
            SummonedThing.ClearAll();
            if (!transient) return;
            foreach (var shot in transient.GetComponentsInChildren<Projectile>(true)) Object.Destroy(shot.gameObject);
            foreach (var thrown in transient.GetComponentsInChildren<ThrownGear>(true)) thrown.StopTracking();
            foreach (var tracker in transient.GetComponentsInChildren<ThrowTracker>(true)) Object.Destroy(tracker);
            foreach (var body in transient.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
        }

        public static readonly List<PlayerController> Players = new();

        public static int PlayerLayer => LayerMask.NameToLayer("Player");
        public static int TileLayer => LayerMask.NameToLayer("Tile");

        public static int TileMask => TileLayer >= 0 ? 1 << TileLayer : ~0;

        /// <summary>Everything a player can stand on: not players and not loose tiles.</summary>
        public static int GroundMask
        {
            get
            {
                int mask = ~0;
                if (PlayerLayer >= 0) mask &= ~(1 << PlayerLayer);
                if (TileLayer >= 0) mask &= ~(1 << TileLayer);
                return mask;
            }
        }

        public static PlayerController NearestOpponent(PlayerController me, Vector3 from, float maxDistance = float.MaxValue)
        {
            PlayerController best = null;
            float bestSq = maxDistance * maxDistance;
            foreach (var p in Players)
            {
                if (!p || p == me || p.IsKnockedOut ||
                    (me && !Rules.Teams.AreHostile(me.Team, p.Team))) continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        public static PlayerController PlayerById(int index)
        {
            if (index < 0) return null;
            foreach (var p in Players)
                if (p && p.Index == index) return p;
            return null;
        }

        public static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
