using System;

namespace Wreckabulary
{
    public static class WordboundSoloEntry
    {
        static string pending;
        static bool returnToSelection;
        public static string SelectedMode { get; private set; } = "TimeRun";
        public static bool Supports(string mode) => mode == "TimeRun" || mode == "Freestyle";
        public static void Select(string mode)
        {
            if (!Supports(mode)) throw new ArgumentException("Choose Time Run or Freestyle for solo play.", nameof(mode));
            SelectedMode = mode;
        }
        public static void Request(string mode)
        {
            Select(mode);
            pending = mode;
            returnToSelection = false;
        }
        public static string TakeRequest()
        {
            string mode = pending;
            pending = null;
            return mode;
        }
        public static void CancelRequest() { pending = null; returnToSelection = false; }
        public static void MarkReturned() => returnToSelection = true;
        public static bool TakeReturn()
        {
            bool result = returnToSelection;
            returnToSelection = false;
            return result;
        }
        public static void Clear() { CancelRequest(); SelectedMode = "TimeRun"; }
    }
}
