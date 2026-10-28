using System;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.WordboundRuntime
{
    public enum WordboundCourseNodeKind
    {
        Layer, Module, Floor, Wall, Connection, ConnectionFrom, ConnectionTo,
        Puzzle, Checkpoint, Spawn, Recovery, Placement, Supply
    }

    [DisallowMultipleComponent]
    public sealed class WordboundCourseNode : MonoBehaviour
    {
        public WordboundCourseNodeKind Kind { get; private set; }
        public string StableId { get; private set; }
        public string ModuleId { get; private set; }
        public string GatePuzzleId { get; private set; }
        public int OwnerTeam { get; private set; }
        public bool Protected { get; private set; }
        public CourseOwnershipPolicy Ownership { get; private set; }

        internal void Bind(WordboundCourseNodeKind kind, string id, string module, string gate,
            CourseOwnershipPolicy ownership, int team = -1, bool protectedResource = true)
        {
            Kind = kind; StableId = id; ModuleId = module; GatePuzzleId = gate;
            Ownership = ownership; OwnerTeam = team; Protected = protectedResource;
        }
    }

    public sealed class WordboundCourseWorldOptions
    {
        public bool EnableRoomArchitecture { get; set; } = true;
        public WordboundRoomStyleProfile RoomStyle { get; set; }
        public System.Collections.Generic.IReadOnlyList<WordboundModuleDressingEntry> ModuleDressing { get; set; }
        public Material FloorMaterial { get; set; }
        public Material WallMaterial { get; set; }
        public Material ConnectorMaterial { get; set; }
        public Action<Collider, WordboundCourseNode> ConfigureCollision { get; set; }
    }
}
