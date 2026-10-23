using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTeamRacePreservesWholeDuoParties() => PublicEntry("TeamRace", RaceFormat.Duo, 4);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicBrawlRacePreservesWholeDuoParties() => PublicEntry("BrawlRace", RaceFormat.Duo, 4);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTimeRunRequiresTwoRealHumans() => PublicEntry("TimeRun", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicFreeForAllAllocatesTwoIndividuals() => PublicEntry("FreeForAll", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicFreestyleFillsTwoRealHumansWithCoopRules() => PublicEntry("Freestyle", RaceFormat.Solo, 2);
        IEnumerator PublicEntry(string mode, RaceFormat format, int count)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1 only after the exact exported catalogue is approved on the Worker.");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_CATALOGUE") ??
                Path.Combine(projectRoot, "Assets/_Project/Resources/Wordbound/public-catalogue.json");
            Assert.IsTrue(File.Exists(path), "Root must generate the native public catalogue first.");
            var catalogue = WordboundPublicCourseCatalog.Parse(File.ReadAllText(path));
            var launchAssets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(launchAssets, "The public entry test requires the actual player launch assets.");
            catalogue.RequireRelease(launchAssets.MapPoolVersion);
            Assert.AreEqual(catalogue.Json, launchAssets.PublicCoursesJson,
                "The tested catalogue must be the exact catalogue assigned to the shipped player.");
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            var selected = catalogue.ResolveSelection(File.ReadAllText(Path.Combine(folder, "items.json")),
                File.ReadAllText(Path.Combine(folder, "rules.json")), mode, format.ToString(), "0123456789abcdef");
            Assert.IsTrue(new WordboundPublicCourseRegistry(catalogue.Json).Allows(selected));
            yield return Run(count, false, productionMode: selected.Mode.Id,
                productionFormat: format, publicCatalogue: catalogue);
        }
    }
}
