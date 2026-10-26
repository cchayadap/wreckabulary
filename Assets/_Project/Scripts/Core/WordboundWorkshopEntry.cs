namespace Wreckabulary
{
    public static class WordboundWorkshopEntry
    {
        static bool pending, returnToSelection;
        public static void Request()
        {
            WordboundSoloEntry.CancelRequest();
            pending = true;
            returnToSelection = false;
        }
        public static bool TakeRequest()
        {
            bool result = pending;
            pending = false;
            return result;
        }
        public static void CancelRequest() { pending = false; returnToSelection = false; }
        public static void MarkReturned() => returnToSelection = true;
        public static bool TakeReturn()
        {
            bool result = returnToSelection;
            returnToSelection = false;
            return result;
        }
        public static void Clear() => CancelRequest();
    }
}
