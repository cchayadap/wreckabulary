using Fusion;

namespace Wreckabulary.Online
{
    public static class WordboundFusionConfiguration
    {
        public static NetworkProjectConfig Create()
        {
            var config = CreateLocal();
            config.Heap.PageShift = PageSizes._32Kb;
            config.Heap.PageCount = 2048;
            return config;
        }

        public static NetworkProjectConfig CreateLocal()
        {
            var config = new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple };
            foreach (var source in NetworkProjectConfig.Global.PrefabTable.Prefabs)
                config.PrefabTable.AddSource(source);
            config.HostMigration.EnableAutoUpdate = false;
            config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            config.Simulation.TickRateSelection = new TickRate.Selection {
                Client = 60, ClientSendInterval = 1, ServerTickInterval = 1, ServerSendInterval = 1
            };
            return config;
        }
    }
}
