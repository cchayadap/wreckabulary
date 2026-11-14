using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public interface IWordboundGameplaySession
    {
        NetworkRunner Runner { get; }
        bool RunnerStarted { get; }
        bool GameplayReady { get; }
        string Failure { get; }
        OnlineSnapshotMark Committed { get; }
        event Action GameplayStarted;
        event Action<string, OnlineSnapshotMark> ReplicaReceived;
        event Action<string> Failed;
        void ConfigureScene(SceneRef scene);
        void MarkLocalReady();
        void Poll();
        void SubmitCommand(Dictionary<string, object> command);
        bool TryGetAdmittedActor(PlayerRef peer, out string actorId);
        Task<bool> PublishCheckpoint(CancellationToken cancellationToken = default);
        Task Stop();
    }
}
