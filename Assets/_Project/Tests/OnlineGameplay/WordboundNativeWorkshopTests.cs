using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        [UnityTest, Category("ExternalPhotonWorkshopGameplay"), Timeout(600000)]
        public IEnumerator TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY=1 after deploying verified workshop bundle support.");
            return Run(2, false, authoredWorkshop: true);
        }

        static WordboundClientContent CreateAuthoredWorkshop(string projectRoot)
        {
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            string items = File.ReadAllText(Path.Combine(folder, "items.json"));
            string rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
            var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items));
            var document = flagship.CopyDocument();
            document.Id = "native-authored-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            document.Title = "Native authored transfer proof";
            var mode = ModeDefinition.For("Freestyle");
            var preview = CourseResolver.Resolve(document, flagship.Geometry,
                new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(preview.Success, "The edited draft must validate before creating any guest or party.");
            return WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(preview.Manifest),
                flagship.Geometry, items, rules, mode.Id, mode.Format.ToString(), preview.Manifest.Seed);
        }

        void AssertDownloadedWorkshop()
        {
            Assert.IsFalse(live.PublicQueue);
            Assert.AreEqual(2, peers.Count);
            Assert.AreNotSame(live.ContentFor(0), live.ContentFor(1));
            Assert.AreNotSame(live.ContentFor(0).Course, live.ContentFor(1).Course);
            foreach (var peer in peers)
            {
                int index = live.Clients.FindIndex(client => client.Credentials.PlayerId == peer.Actor);
                var content = live.ContentFor(index);
                Assert.NotNull(content.WorkshopBundle);
                Assert.AreEqual(live.Course.Hash, content.Course.Hash);
                Assert.AreEqual(live.Course.SourceHash, content.Course.SourceHash);
                Assert.AreEqual(CourseJson.ExportDocument(live.Course.CopyDocument()), CourseJson.ExportDocument(content.Course.CopyDocument()));
                Assert.AreEqual(live.Course.Hash, peer.Controller.CourseWorld.ManifestHash);
                Assert.AreEqual(live.Course.Hash, peer.Controller.Frame.Binding.ManifestHash);
                Assert.AreEqual(live.Course.SourceHash, peer.Controller.Frame.Binding.CourseHash);
                Assert.AreEqual(live.Content.CatalogHash, peer.Controller.Frame.Binding.CatalogueHash);
                Assert.AreEqual(WordboundRulesFingerprint.Compute(live.Rules), WordboundRulesFingerprint.Compute(content.Rules));
            }
            Debug.Log("WORDBOUND_AUTHORED_TRANSFER_OBSERVED: two authenticated peers downloaded and composed source=" +
                live.Course.SourceHash + " manifest=" + live.Course.Hash + "; real bodies/input/checkpoint verified; cleanup pending.");
        }
    }
}
