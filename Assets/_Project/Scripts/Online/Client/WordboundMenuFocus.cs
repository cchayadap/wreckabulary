using System.Collections.Generic;

namespace Wreckabulary.Online
{
    public sealed class WordboundMenuFocus
    {
        readonly List<string> controls = new List<string>();
        string pending;
        public string Selected { get; private set; }

        public void Reset(string preferred = null)
        { controls.Clear(); Selected = preferred; pending = null; }

        public void BeginLayout() => controls.Clear();
        public void Register(string id) => controls.Add(id);

        public void EndLayout()
        {
            if (!controls.Contains(Selected)) Selected = controls.Count == 0 ? null : controls[0];
            if (!controls.Contains(pending)) pending = null;
        }

        public void Move(int direction)
        {
            if (direction == 0 || controls.Count == 0) return;
            int index = controls.IndexOf(Selected);
            Selected = controls[(index + (direction < 0 ? -1 : 1) + controls.Count) % controls.Count];
            pending = null;
        }

        public void Confirm()
        { if (controls.Contains(Selected)) pending = Selected; }

        public void CancelConfirmation() => pending = null;

        public bool Consume(string id)
        {
            if (pending != id) return false;
            pending = null;
            return true;
        }
    }
}
