using System.Collections;
using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using Wreckabulary.Online;
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

        static void AssertCarrierIsWeaved()
        {
            Assert.IsTrue(typeof(WordboundFusionSnapshotChunk).GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "NetworkBehaviourWeavedAttribute"),
                "The native Fusion weaver must compile the carrier before creating a live runner.");
        }
    }
}
