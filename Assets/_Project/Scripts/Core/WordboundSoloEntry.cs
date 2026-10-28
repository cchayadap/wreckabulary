using System;

namespace Wreckabulary
{
    public static class WordboundSoloEntry
    {
        public const string LostAddressMap = "LostAddress", ParcelHallMap = "ParcelHall", ParcelServiceMap = "ParcelService";
        public const string ParcelServiceCourseId = "wordbound-parcel-service-v1";
        static string pending;
        static bool returnToSelection;
        public static string SelectedMode { get; private set; } = "TimeRun";
        public static string SelectedMap { get; private set; } = LostAddressMap;
        public static bool Supports(string mode) => mode == "TimeRun" || mode == "Freestyle";
        public static bool SupportsMap(string map) => map == LostAddressMap || map == ParcelHallMap || map == ParcelServiceMap;
        public static void SelectMap(string map)
        {
            if (!SupportsMap(map)) throw new ArgumentException("Choose an installed solo map.", nameof(map));
            SelectedMap = map;
        }
        public static void Select(string mode)
        {
            if (!Supports(mode)) throw new ArgumentException("Choose Time Run or Freestyle for solo play.", nameof(mode));
            SelectedMode = mode;
        }
        public static void Request(string mode)
        {
            Select(mode);
            WordboundWorkshopEntry.CancelRequest();
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
        public static void Clear() { CancelRequest(); SelectedMode = "TimeRun"; SelectedMap = LostAddressMap; }
    }
}
