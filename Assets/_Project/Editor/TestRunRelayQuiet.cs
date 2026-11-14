using System;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Wreckabulary.EditorTools
{
    static class TestRunRelayQuiet
    {
        [InitializeOnLoadMethod]
        static void Quiet()
        {
            if (!Environment.GetCommandLineArgs().Any(a => string.Equals(a, "-runTests", StringComparison.OrdinalIgnoreCase))) return;
            var relay = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Relay.Editor.RelayService", false))
                .FirstOrDefault(t => t != null);
            relay?.GetMethod("SuppressAutoStart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
        }
    }
}
