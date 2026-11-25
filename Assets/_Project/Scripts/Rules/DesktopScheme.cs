namespace Wreckabulary.Rules
{
    public enum SchemeScope { Both, House, Course }

    public enum AttackKind { Swing, Throw, Draw, Shield }

    public enum PressStep { None, Down, Holding, AimStart, Aiming, Tap, Release }

    public static class DesktopScheme
    {
        public const float HoldSeconds = .2f;
        public const string SpellBox = "Spell box", CourseSpell = "Spell", Aim = "Aim", Dodge = "Dodge";

        public static SchemeScope Scope(string action) => action switch
        {
            SpellBox or Dodge => SchemeScope.House,
            CourseSpell or Aim => SchemeScope.Course,
            _ => SchemeScope.Both,
        };

        public static bool ShowsIn(string action, bool course) => Scope(action) != (course ? SchemeScope.House : SchemeScope.Course);

        public static bool Shares(string first, string second)
        {
            var a = Scope(first);
            var b = Scope(second);
            return a == SchemeScope.Both || b == SchemeScope.Both || a == b;
        }

        public static bool FiresOnRelease(AttackKind kind) => kind == AttackKind.Throw;

        public static bool AimsWhileHeld(AttackKind kind) => kind is AttackKind.Throw or AttackKind.Draw;

        public static bool Fires(AttackKind kind, PressStep step) => FiresOnRelease(kind)
            ? step is PressStep.Tap or PressStep.Release
            : step == PressStep.Down;

        public static bool HudOwnsPress(bool pressedNow, bool held, bool overHudAtPress, bool owned) =>
            pressedNow ? overHudAtPress : held && owned;

        public static string ClosesLine(string key) => $"{key} closes.";

        public static string ReadyLine(string word, string key) => $"{word} is ready. Press {key} to spell it.";

        public static string EmptyLine(string key) => "Type a word you can make from your letters. " + ClosesLine(key);

        public static string UnknownLine(string bag) => $"That isn't a recipe. Close, then check the book on {bag}.";
    }

    public struct ClickHold
    {
        bool down, aiming, waitRelease;
        float since;

        public bool Aiming => aiming;
        public bool Down => down;

        public PressStep Update(bool pressed, float now, float holdSeconds = DesktopScheme.HoldSeconds)
        {
            if (waitRelease)
            {
                if (!pressed) waitRelease = false;
                return PressStep.None;
            }
            if (!down)
            {
                if (!pressed) return PressStep.None;
                down = true;
                aiming = false;
                since = now;
                return PressStep.Down;
            }
            if (!pressed)
            {
                bool wasAiming = aiming;
                down = aiming = false;
                return wasAiming ? PressStep.Release : PressStep.Tap;
            }
            if (aiming) return PressStep.Aiming;
            if (now - since < holdSeconds) return PressStep.Holding;
            aiming = true;
            return PressStep.AimStart;
        }

        public void Cancel(bool pressed)
        {
            down = aiming = false;
            waitRelease = pressed;
        }
    }
}
