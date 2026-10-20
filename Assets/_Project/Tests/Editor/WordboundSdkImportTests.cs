using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine.TestTools;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundSdkImportTests
    {
        [UnityTest, Category("ExternalSetup")]
        public IEnumerator ImportInspectedPackageToCompletion()
        {
            string package = Environment.GetEnvironmentVariable("WORDBOUND_FUSION_PACKAGE");
            if (string.IsNullOrEmpty(package)) Assert.Ignore("Only runs when an inspected SDK package is explicitly supplied.");
            string relative = Environment.GetEnvironmentVariable("WORDBOUND_FUSION_VERIFY_PATH");
            Assert.IsTrue(relative != null && relative.StartsWith("Assets/Photon/", StringComparison.Ordinal));
            bool done = false;
            string failure = null;
            AssetDatabase.ImportPackageCallback completed = _ => done = true;
            AssetDatabase.ImportPackageFailedCallback failed = (_, reason) => { failure = reason; done = true; };
            AssetDatabase.ImportPackageCallback cancelled = _ => { failure = "Import cancelled."; done = true; };
            AssetDatabase.importPackageCompleted += completed;
            AssetDatabase.importPackageFailed += failed;
            AssetDatabase.importPackageCancelled += cancelled;
            EditorApplication.LockReloadAssemblies();
            try
            {
                WordboundPhotonSetup.ImportFusion();
                double deadline = EditorApplication.timeSinceStartup + 180;
                while (!done && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(done, "Package import did not complete within 180 seconds.");
                Assert.IsNull(failure, failure);
                Assert.IsTrue(File.Exists(relative), "Completion event must also produce the expected SDK file.");
            }
            finally
            {
                AssetDatabase.importPackageCompleted -= completed;
                AssetDatabase.importPackageFailed -= failed;
                AssetDatabase.importPackageCancelled -= cancelled;
                EditorApplication.UnlockReloadAssemblies();
            }
            yield return new RecompileScripts(false);
        }
    }
}
