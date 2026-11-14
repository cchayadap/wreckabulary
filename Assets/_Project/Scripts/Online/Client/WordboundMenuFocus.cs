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

        public bool MoveGrid(int horizontal, int vertical, int columns)
        {
            if (columns < 1 || horizontal == 0 && vertical == 0 || Selected == null || !Selected.StartsWith("recipe:")) return false;
            int current = controls.IndexOf(Selected);
            if (current < 0) return false;
            int first = current, last = current;
            while (first > 0 && controls[first - 1].StartsWith("recipe:")) first--;
            while (last + 1 < controls.Count && controls[last + 1].StartsWith("recipe:")) last++;
            int next;
            if (vertical != 0)
            {
                int row = (current - first) / columns, column = (current - first) % columns;
                int nextRow = row + (vertical < 0 ? -1 : 1);
                if (nextRow < 0) next = first - 1;
                else if (nextRow > (last - first) / columns) next = last + 1;
                else next = System.Math.Min(last, first + nextRow * columns + column);
            }
            else next = current + (horizontal < 0 ? -1 : 1);
            Selected = controls[(next + controls.Count) % controls.Count];
            pending = null;
            return true;
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
