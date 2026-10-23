using System.Collections;
using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.PhotonTests
{
    public sealed class WordboundFusionWeavingTests
    {
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RecompileConfiguredNetworkAssemblies()
        {
            Assert.Contains("Wreckabulary.Online.Fusion", NetworkProjectConfig.Global.AssembliesToWeave);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            yield return new RecompileScripts(false);
            AssertCarrierIsWeaved();
        }

        [Test]
        public void ConfiguredCarrierHasNativeGeneratedNetworkState()
        {
            Assert.Contains("Wreckabulary.Online.Fusion", NetworkProjectConfig.Global.AssembliesToWeave);
            AssertCarrierIsWeaved();
        }

        [Test]
        public void OnlineHeapPolicyOwnsItsConfigurationAndPreservesRegistrations()
        {
            var shared = NetworkProjectConfig.Global;
            var sharedShift = shared.Heap.PageShift;
            int sharedPages = shared.Heap.PageCount;
            var registrations = shared.PrefabTable.Prefabs.ToArray();
            var local = WordboundFusionConfiguration.CreateLocal();
            var first = WordboundFusionConfiguration.Create();
            var second = WordboundFusionConfiguration.Create();
            Assert.AreNotSame(shared.Heap, first.Heap);
            Assert.AreNotSame(first.Heap, second.Heap);
            Assert.AreNotSame(local.Heap, first.Heap);
            Assert.AreEqual(PageSizes._32Kb, first.Heap.PageShift);
            Assert.AreEqual(2048, first.Heap.PageCount);
            Assert.AreEqual(64L * 1024 * 1024, (1L << (int)first.Heap.PageShift) * first.Heap.PageCount);
            Assert.AreEqual(0, first.Heap.GlobalsSize);
            Assert.AreEqual(NetworkProjectConfig.PeerModes.Multiple, first.PeerMode);
            Assert.IsFalse(first.HostMigration.EnableAutoUpdate);
            Assert.AreEqual(NetworkConfiguration.ReliableDataTransfers.ClientToServer, first.Network.ReliableDataTransferModes);
            Assert.AreEqual(NetworkProjectConfig.InvokeRenderModes.Always, first.InvokeRenderMode);
            Assert.AreEqual(60, first.Simulation.TickRateSelection.Client);
            Assert.AreEqual(1, first.Simulation.TickRateSelection.ClientSendInterval);
            Assert.AreEqual(1, first.Simulation.TickRateSelection.ServerTickInterval);
            Assert.AreEqual(1, first.Simulation.TickRateSelection.ServerSendInterval);
            Assert.AreEqual(TickRate.ValidateResult.Ok, first.Simulation.TickRateSelection.Validate());
            CollectionAssert.AreEqual(registrations, first.PrefabTable.Prefabs.ToArray());
            CollectionAssert.AreEqual(registrations, local.PrefabTable.Prefabs.ToArray());
            Assert.AreEqual(first.PeerMode, local.PeerMode);
            Assert.AreEqual(first.HostMigration.EnableAutoUpdate, local.HostMigration.EnableAutoUpdate);
            Assert.AreEqual(first.Network.ReliableDataTransferModes, local.Network.ReliableDataTransferModes);
            Assert.AreEqual(first.Simulation.TickRateSelection.Client, local.Simulation.TickRateSelection.Client);
            Assert.AreEqual(first.Simulation.TickRateSelection.ClientSendInterval, local.Simulation.TickRateSelection.ClientSendInterval);
            Assert.AreEqual(first.Simulation.TickRateSelection.ServerTickInterval, local.Simulation.TickRateSelection.ServerTickInterval);
            Assert.AreEqual(first.Simulation.TickRateSelection.ServerSendInterval, local.Simulation.TickRateSelection.ServerSendInterval);
            Assert.IsNotEmpty(registrations, "Prepared online prefabs must be registered before this native check.");
            first.Heap.PageCount = 1;
            Assert.AreEqual(2048, second.Heap.PageCount);
            Assert.AreEqual(sharedShift, shared.Heap.PageShift);
            Assert.AreEqual(sharedPages, shared.Heap.PageCount);
            CollectionAssert.AreEqual(registrations, shared.PrefabTable.Prefabs.ToArray());
            Assert.AreEqual(PageSizes._32Kb, local.Heap.PageShift);
            Assert.AreEqual(256, local.Heap.PageCount);
        }

        [Test]
        public void BakedBodyAndElectionStateFitTheAdmittedHistoryEnvelope()
        {
            var config = WordboundFusionConfiguration.Create();
            int pageBytes = 1 << (int)config.Heap.PageShift;
            const int bodySegmentBytes = 1280, carrierSegmentBytes = 6144, auxiliarySegments = 6;
            foreach (string name in new[] { "WordboundPlayerBody", "WordboundItemBody", "WordboundPlatformBody" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Wordbound/Network/" + name + ".prefab");
                Assert.NotNull(prefab, name);
                var network = prefab.GetComponent<NetworkObject>();
                Assert.NotNull(network, name);
                Assert.IsTrue(network.EnableInterpolation, name);
                Assert.AreEqual(3, network.NetworkedBehaviours.Length, name);
                Assert.IsTrue(network.NetworkedBehaviours.All(b => b != null && b.GetType().GetCustomAttributes(false)
                    .Any(a => a.GetType().Name == "NetworkBehaviourWeavedAttribute")), name);
                int words = NetworkObject.GetWordCount(network);
                Assert.Greater(words, 0, name);
                Assert.LessOrEqual(words, 315, "The body layout grew beyond the measured 1,260-byte capacity envelope: " + name);
            }
            var carrier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Wordbound/Network/WordboundSnapshotChunk.prefab");
            Assert.NotNull(carrier);
            var carrierNetwork = carrier.GetComponent<NetworkObject>();
            Assert.NotNull(carrierNetwork);
            AssertCarrierIsWeaved();
            int carrierWords = NetworkObject.GetWordCount(carrierNetwork);
            Assert.Greater(carrierWords, 0);
            Assert.LessOrEqual(carrierWords * 4, carrierSegmentBytes);
            int segmentsPerBody = config.Simulation.TickRateSelection.Client + auxiliarySegments;
            int bodySegmentsPerPage = pageBytes / bodySegmentBytes;
            int carrierSegmentsPerPage = pageBytes / carrierSegmentBytes;
            long bodySegments = (long)WordboundEntityBudget.MaximumBodies * segmentsPerBody;
            long bodyPages = (bodySegments + bodySegmentsPerPage - 1) / bodySegmentsPerPage;
            long carrierPages = (segmentsPerBody + carrierSegmentsPerPage - 1) / carrierSegmentsPerPage;
            Assert.LessOrEqual(bodyPages + carrierPages, config.Heap.PageCount * 3L / 4,
                "The admitted body history and election carrier must leave at least one quarter of the object heap for SDK state and fragmentation.");
        }

        static void AssertCarrierIsWeaved()
        {
            Assert.IsTrue(typeof(WordboundFusionSnapshotChunk).GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "NetworkBehaviourWeavedAttribute"),
                "The native Fusion weaver must compile the carrier before creating a live runner.");
        }
    }
}
