using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Rules.Wordbound;

namespace UnityEngine
{
    public class GameObject { }
    public class Object { public static void Destroy(object value) { } }
}
namespace Photon.Realtime
{
    public enum CustomAuthenticationType { Custom }
    public class AuthenticationValues
    {
        public CustomAuthenticationType AuthType;
        public void SetAuthPostData(Dictionary<string, object> data) { }
    }
}
namespace Fusion.Photon.Realtime
{
    public class FusionAppSettings
    {
        public string FixedRegion, AppVersion, AppIdFusion;
        public FusionAppSettings GetCopy() => (FusionAppSettings)MemberwiseClone();
    }
}
namespace Fusion.Sockets
{
    public struct NetAddress { }
    public enum NetConnectFailedReason { }
    public enum NetDisconnectReason { }
}
namespace Fusion
{
    public interface INetworkRunnerCallbacks { }
    public struct NetworkPrefabRef { }
    public struct NetworkInput { }
    public struct SimulationMessagePtr { }
    public struct SessionInfo { }
    public struct PlayerRef
    {
        public int RawEncoded;
        public PlayerRef(int value) { RawEncoded = value; }
        public static bool operator ==(PlayerRef a, PlayerRef b) => a.RawEncoded == b.RawEncoded;
        public static bool operator !=(PlayerRef a, PlayerRef b) => !(a == b);
        public override bool Equals(object value) => value is PlayerRef other && this == other;
        public override int GetHashCode() => RawEncoded;
    }
    public struct Tick { public int Raw; }
    public struct ReliableKey { public static ReliableKey FromInts(int a, int b, int c, int d) => default; }
    public enum ShutdownReason { Ok, HostMigration }
    public enum GameMode { Host, Client, AutoHostOrClient }
    public sealed class HostMigrationToken { public GameMode GameMode; }
    public sealed class NetworkConfiguration
    {
        [Flags] public enum ReliableDataTransfers { ClientToServer = 1, ClientToClientWithServerProxy = 2 }
        public ReliableDataTransfers ReliableDataTransferModes = ReliableDataTransfers.ClientToServer;
    }
    public sealed class HostMigrationConfig { public bool EnableAutoUpdate; }
    public sealed class NetworkProjectConfig
    {
        public HostMigrationConfig HostMigration = new HostMigrationConfig();
        public NetworkConfiguration Network = new NetworkConfiguration();
    }
    public sealed class NetworkRunnerCallbackArgs
    {
        public sealed class ConnectRequest { public void Accept() { } public void Refuse() { } }
    }
    public sealed class StartGameResult { public bool Ok = true; public ShutdownReason ShutdownReason; }
    public sealed class StartGameArgs
    {
        public GameMode GameMode;
        public string SessionName;
        public int PlayerCount;
        public bool IsVisible, IsOpen, EnableClientSessionCreation;
        public global::Fusion.Photon.Realtime.FusionAppSettings CustomPhotonAppSettings;
        public global::Photon.Realtime.AuthenticationValues AuthValues;
        public NetworkProjectConfig Config;
        public byte[] ConnectionToken;
        public HostMigrationToken HostMigrationToken;
        public Action<NetworkRunner> HostMigrationResume;
        public CancellationToken StartGameCancellationToken;
    }
    public sealed class NetworkObject
    {
        public T GetComponent<T>() where T : class => new Wreckabulary.Online.WordboundFusionSnapshotChunk { Object = this } as T;
    }
    public sealed class NetworkRunner
    {
        int tick;
        public bool IsRunning = true, IsServer = true;
        public PlayerRef LocalPlayer = new PlayerRef(1);
        public readonly List<PlayerRef> ActivePlayers = new List<PlayerRef> { new PlayerRef(1), new PlayerRef(2) };
        public readonly Dictionary<PlayerRef, string> Users = new Dictionary<PlayerRef, string>();
        public readonly Dictionary<PlayerRef, byte[]> Tokens = new Dictionary<PlayerRef, byte[]>();
        public Func<Task<bool>> Push = () => Task.FromResult(true);
        public Func<Task> ShutdownCompletion = () => Task.CompletedTask;
        public Func<StartGameArgs, Task<StartGameResult>> Startup = _ => Task.FromResult(new StartGameResult());
        public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
        public int Sends, Disconnects;
        public Tick Tick => new Tick { Raw = ++tick };
        public void AddCallbacks(INetworkRunnerCallbacks callbacks) { }
        public Task<StartGameResult> StartGame(StartGameArgs args) => Startup(args);
        public string GetPlayerUserId(PlayerRef player) => Users.TryGetValue(player, out var value) ? value : "";
        public byte[] GetPlayerConnectionToken(PlayerRef player) => Tokens.TryGetValue(player, out var value) ? value : null;
        public void SendReliableDataToPlayer(PlayerRef player, ReliableKey key, byte[] data) { Sends++; }
        public void SendReliableDataToServer(ReliableKey key, byte[] data) { Sends++; }
        public void Disconnect(PlayerRef player) { Disconnects++; }
        public NetworkObject Spawn(NetworkPrefabRef prefab, Action<NetworkRunner, NetworkObject> onBeforeSpawned)
        { var obj = new NetworkObject(); onBeforeSpawned(this, obj); return obj; }
        public void Despawn(NetworkObject obj) { }
        public Task<bool> PushHostMigrationSnapshot() => Push();
        public IEnumerable<NetworkObject> GetResumeSnapshotNetworkObjects() => Array.Empty<NetworkObject>();
        public Task Shutdown(bool destroyGameObject = true, ShutdownReason shutdownReason = ShutdownReason.Ok) => ShutdownCompletion();
    }
}
namespace Wreckabulary.Online
{
    public sealed class WordboundFusionSnapshotChunk
    {
        public Fusion.NetworkObject Object;
        public void Write(OnlineSnapshotMark mark, OnlineSnapshotPacket packet, int index) { }
        public static string Restore(IEnumerable<WordboundFusionSnapshotChunk> source, out OnlineSnapshotMark mark)
        { mark = null; throw new FormatException("No native resume fixture in lifecycle tests."); }
    }
}
