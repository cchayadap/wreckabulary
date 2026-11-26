namespace Wreckabulary.Rules.Tests
{
    static class HelperTestSpecs
    {
        public static HelperSpec Robot() => new HelperSpec
        {
            Id = "ROBOT", Body = HelperBody.Walks, Pick = HelperPick.Aimed, Bites = HelperBite.Enemies, Attack = HelperAttack.Swing,
            Damage = 8f, Range = 1.1f, Windup = .45f, Cooldown = .75f, Knockback = 3f,
            Speed = 3f, Sight = 8f, Leash = 10f, Seconds = 10f, Strikes = 3, Health = 22f,
        };

        public static HelperSpec Rabbit() => new HelperSpec
        {
            Id = "RABBIT", Body = HelperBody.Hops, Pick = HelperPick.Nearest, Bites = HelperBite.Enemies, Attack = HelperAttack.Touch,
            Damage = 7f, Range = .9f, Windup = .2f, Cooldown = .8f, Knockback = 4f,
            Speed = 3.8f, Sight = 9f, Leash = 11f, Seconds = 12f, Strikes = 4, Health = 24f,
        };

        public static HelperSpec Terrier() => new HelperSpec
        {
            Id = "TERRIER", Body = HelperBody.Walks, Pick = HelperPick.Provoker, Bites = HelperBite.Enemies, Attack = HelperAttack.Swing,
            Damage = 7f, Range = 1f, Windup = .4f, Cooldown = .7f, Knockback = 3f,
            Speed = 3.4f, Sight = 8f, Leash = 10f, Seconds = 10f, Strikes = 3, Health = 20f,
        };

        public static HelperSpec Blaster() => new HelperSpec
        {
            Id = "BLASTER", Body = HelperBody.Stays, Pick = HelperPick.Nearest, Bites = HelperBite.Enemies, Attack = HelperAttack.Shot,
            Damage = 6f, Range = 3.5f, Windup = .5f, Cooldown = .9f, Knockback = 2f, ShotSpeed = 11f,
            Radius = 3.5f, Seconds = 18f, Strikes = 5, Health = 30f,
        };

        public static HelperSpec Chomper() => new HelperSpec
        {
            Id = "CHOMPER", Body = HelperBody.Hops, Pick = HelperPick.Nearest, Bites = HelperBite.Anyone, Attack = HelperAttack.Swing,
            Damage = 10f, Range = 1f, Windup = .45f, Cooldown = .7f, Knockback = 4f,
            Speed = 3.8f, Sight = 8f, Leash = 10f, Seconds = 12f, Strikes = 5, Health = 30f,
        };

        public static HelperSpec Patrol() => new HelperSpec
        {
            Id = "PATROL", Body = HelperBody.Walks, Pick = HelperPick.Nearest, Bites = HelperBite.Enemies, Attack = HelperAttack.Touch,
            Damage = 6f, Range = .8f, Windup = .2f, Cooldown = .9f, Knockback = 4f,
            Speed = 3f, Sight = 5f, Leash = 3f, Seconds = 10f, Strikes = 4, Health = 24f,
        };

        public static HelperSpec Spikes() => new HelperSpec
        {
            Id = "SPIKES", Body = HelperBody.Stays, Pick = HelperPick.Stepper, Bites = HelperBite.Enemies, Attack = HelperAttack.Touch,
            Damage = 7f, Range = 1.2f, Windup = .15f, Cooldown = .8f, Knockback = 3f,
            Radius = 1.2f, Seconds = 20f, Strikes = 6, Health = 40f,
        };

        public static HelperSpec Trap() => new HelperSpec
        {
            Id = "TRAP", Body = HelperBody.Stays, Pick = HelperPick.Stepper, Bites = HelperBite.Enemies, Attack = HelperAttack.Touch,
            Damage = 8f, Range = .6f, Windup = .15f, Cooldown = 1.2f, Knockback = 2f, Slow = .6f, SlowSeconds = 1f,
            Radius = .6f, Seconds = 12f, Strikes = 1, Health = 12f,
        };

        public static HelperSpec Cracker() => new HelperSpec
        {
            Id = "CRACKER", Body = HelperBody.Hops, Pick = HelperPick.Nearest, Bites = HelperBite.Anyone, Attack = HelperAttack.Burst,
            Damage = 12f, Range = 2.5f, Windup = .5f, Cooldown = .9f, Knockback = 5f,
            Speed = 3.5f, Sight = 8f, Leash = 10f, Seconds = 8f, Strikes = 1, Health = 24f,
        };

        public static HelperSpec[] BatchOne() => new[] { Robot(), Rabbit(), Terrier(), Blaster(), Chomper(), Patrol(), Spikes(), Trap() };

        public static HelperCandidate Player(int id, int team, float x, float z, bool hittable = true, bool inSight = true, float y = 0f) =>
            new HelperCandidate(id, team, x, y, z, hittable, inSight);
    }
}
