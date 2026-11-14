using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    public sealed class WordboundGameplayPrefabs
    {
        public NetworkObject Player { get; }
        public NetworkObject Item { get; }
        public NetworkObject Platform { get; }
        public WordboundGameplayPrefabs(NetworkObject player, NetworkObject item, NetworkObject platform)
        {
            Player = Check(player, true); Item = Check(item, false); Platform = Check(platform, false);
        }
        static NetworkObject Check(NetworkObject prefab, bool player)
        {
            if (prefab == null || prefab.GetComponent<WordboundNetworkBody>() == null ||
                prefab.GetComponent<WordboundGameplayBody>() == null ||
                player && prefab.GetComponent<CapsuleCollider>() == null ||
                !player && prefab.GetComponent<BoxCollider>() == null ||
                (prefab.transform.localScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("Gameplay prefabs require canonical body, presentation, root collider and metre scale.");
            return prefab;
        }
        public NetworkObject For(WorldBodyKind kind) => kind == WorldBodyKind.Player ? Player :
            kind == WorldBodyKind.Platform ? Platform : Item;
    }

    public sealed class WordboundHostObservations
    {
        public IEnumerable<ProgressEvent> Progress { get; set; } = Array.Empty<ProgressEvent>();
        public IEnumerable<HostDamage> Damage { get; set; } = Array.Empty<HostDamage>();
        public IEnumerable<HostMechanismObservation> Mechanisms { get; set; } = Array.Empty<HostMechanismObservation>();
        public IEnumerable<HostThrownContact> ThrownContacts { get; set; } = Array.Empty<HostThrownContact>();
    }

    public sealed class WordboundGameplayOptions
    {
        public SceneRef? GameplayScene { get; set; }
        public WordboundCourseWorldOptions World { get; set; }
        public Func<WordboundMovementInput> ReadMovement { get; set; }
        public Func<string, WordboundMovementEnvironment> MovementEnvironment { get; set; }
        public Func<string, ResolvedCoursePuzzle, WorldBodyState, bool> PuzzleEvaluator { get; set; }
        public Func<MatchSnapshot, IReadOnlyList<WorldBodyState>, WordboundHostObservations> ObserveHost { get; set; }
        public Action<MatchSnapshot, IReadOnlyDictionary<string, WordboundNetworkBody>> BeforeHostPhysics { get; set; }
        public Action<WordboundGameplayBody> ConfigureVisual { get; set; }
        public Action<WordboundNetworkBody> LocalPlayerChanged { get; set; }
        public Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public int CheckpointMilliseconds { get; set; } = 1000;
    }
}
