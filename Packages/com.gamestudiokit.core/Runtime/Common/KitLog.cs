// Game Studio Kit (MIT). One log prefix for every kit message, so gate log scans and the rules linter can tell kit
// output from game output.
using UnityEngine;

namespace GameStudioKit.Common
{
    public static class KitLog
    {
        public const string Prefix = "[GSK] ";

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);

        /// <summary>Logs an error. QaRunner counts console errors as failures, so use this only for real faults.</summary>
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
