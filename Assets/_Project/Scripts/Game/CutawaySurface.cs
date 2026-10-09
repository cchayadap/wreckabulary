using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public enum CutawayKind { Ceiling, Roof, UpperWall }

    /// <summary>Authored presentation sections. OwnerStorey identifies the room beneath a ceiling.</summary>
    [DisallowMultipleComponent]
    public sealed class CutawaySurface : MonoBehaviour
    {
        static readonly List<CutawaySurface> active = new();
        internal static IReadOnlyList<CutawaySurface> Active => active;
        [SerializeField] CutawayKind kind;
        [SerializeField] int ownerStorey;
        [SerializeField] string roomId;
        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();

        public CutawayKind Kind => kind;
        public int OwnerStorey => ownerStorey;
        public string RoomId => roomId;
        public IReadOnlyList<Renderer> Renderers => renderers;

        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() => active.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        public void Configure(CutawayKind kind, int ownerStorey, string roomId, Renderer[] renderers)
        {
            this.kind = kind;
            this.ownerStorey = Mathf.Max(0, ownerStorey);
            this.roomId = roomId ?? string.Empty;
            this.renderers = renderers ?? Array.Empty<Renderer>();
        }
    }
}
