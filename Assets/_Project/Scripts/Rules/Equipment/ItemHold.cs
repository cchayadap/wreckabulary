using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct HoldTurn
    {
        public readonly float X, Y, Z, W;

        public HoldTurn(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static HoldTurn Identity => new HoldTurn(0f, 0f, 0f, 1f);

        public static HoldTurn Euler(float x, float y, float z) => Axis(0f, 1f, 0f, y) * Axis(1f, 0f, 0f, x) * Axis(0f, 0f, 1f, z);

        public static HoldTurn Euler(float[] degrees) => degrees == null ? Identity : Euler(degrees[0], degrees[1], degrees[2]);

        static HoldTurn Axis(float x, float y, float z, float degrees)
        {
            double half = degrees * Math.PI / 360.0;
            float s = (float)Math.Sin(half);
            return new HoldTurn(x * s, y * s, z * s, (float)Math.Cos(half));
        }

        public HoldTurn Inverse => new HoldTurn(-X, -Y, -Z, W);

        public HoldTurn Normalized
        {
            get
            {
                float length = (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
                return new HoldTurn(X / length, Y / length, Z / length, W / length);
            }
        }

        public static HoldTurn operator *(HoldTurn a, HoldTurn b) => new HoldTurn(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public (float x, float y, float z) Rotate(float x, float y, float z)
        {
            float tx = 2f * (Y * z - Z * y), ty = 2f * (Z * x - X * z), tz = 2f * (X * y - Y * x);
            return (x + W * tx + Y * tz - Z * ty, y + W * ty + Z * tx - X * tz, z + W * tz + X * ty - Y * tx);
        }
    }

    public sealed class ItemHold
    {
        public string Id;
        public float[] Turn = new float[3];
        public float[] Grip;
        public float ModelYaw;
        public float[] ModelShift;
    }

    public static class ItemHolds
    {
        public static readonly HoldTurn OneHandSocket = new HoldTurn(-.4782f, -.1462f, .0755f, .8627f).Normalized;
        public static readonly HoldTurn TwoHandSocket = new HoldTurn(-.655f, -.166f, .0623f, .7345f).Normalized;

        public const string GuardPosedItem = "SHIELD";
        const float SwingLean = 25f, ThrustLean = 45f, GliderLift = 15f, TrayTilt = 30f, HoopStandsUp = 90f;

        static readonly ItemHold[] holds =
        {
            new ItemHold { Id = "ANCHOR", Turn = new[] { SwingLean - 180f, 0f, 0f } },
            new ItemHold { Id = "ARROW", Turn = new[] { 45f, 15f, 0f } },
            new ItemHold { Id = "BARREL", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "BEAR", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "BED", Turn = new[] { 0f, 90f, 0f } },
            new ItemHold { Id = "BENCH", Turn = new[] { -15f, 0f, 0f }, Grip = new[] { -.25f, .3f, -.1f } },
            new ItemHold { Id = "BLASTER", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "BLOCKS", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "BOOT", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "BRICK", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "CABINET", Grip = new[] { 0f, .5675f, 0f }, ModelYaw = 180f, ModelShift = new[] { 0f, 0f, .043f } },
            new ItemHold { Id = "CAMERA", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "CARPET", Turn = new[] { 0f, 90f, 0f } },
            new ItemHold { Id = "CASTLE", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "CHAIR", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "CHILI", Turn = new[] { -SwingLean, 180f, 90f } },
            new ItemHold { Id = "CHOMPER", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "COMPASS", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "CONE", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "CRAB", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "CRATE", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "DESK", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "DISC", Turn = new[] { -30f, -45f, 0f } },
            new ItemHold { Id = "DOOR", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "FENCE", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "FLIPPER", Turn = new[] { -SwingLean, 180f, 0f } },
            new ItemHold { Id = "HELMET", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "HOOP", Turn = new[] { HoopStandsUp, 0f, 0f } },
            new ItemHold { Id = "HOSE", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "LADDER", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "LATTE", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "LEMON", Turn = new[] { 0f, 180f, 90f }, Grip = new[] { -.0675f, .0743f, 0f } },
            new ItemHold { Id = "LID", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "MALLET", Turn = new[] { -SwingLean, 180f, 0f } },
            new ItemHold { Id = "MARBLE", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "MELON", Turn = new[] { -60f, 180f, 0f } },
            new ItemHold { Id = "OIL", Turn = new[] { 0f, 90f, 0f } },
            new ItemHold { Id = "PADDLE", Turn = new[] { -SwingLean, 180f, 0f } },
            new ItemHold { Id = "PAINT", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "PAN", Turn = new[] { 0f, -90f, 105f } },
            new ItemHold { Id = "PEAR", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "PENCIL", Turn = new[] { ThrustLean, 0f, 0f } },
            new ItemHold { Id = "PIANO", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "PILLOW", Turn = new[] { 35f, 0f, 45f } },
            new ItemHold { Id = "PLANE", Turn = new[] { -GliderLift, 0f, 0f } },
            new ItemHold { Id = "PATROL", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "POTION", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "RABBIT", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "RACKET", Turn = new[] { -SwingLean, 180f, 0f } },
            new ItemHold { Id = "RAKE", Turn = new[] { SwingLean, 0f, 0f } },
            new ItemHold { Id = "RAMP", Turn = new[] { 0f, 90f, 0f } },
            new ItemHold { Id = "ROBOT", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "ROCKET", Turn = new[] { -ThrustLean, 180f, 0f } },
            new ItemHold { Id = "SATCHEL", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SHELF", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SLIME", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SOCK", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SOFA", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SPEAKER", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SPIKES", Turn = new[] { TrayTilt, 180f, 0f } },
            new ItemHold { Id = "SPINNER", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "SPOON", Turn = new[] { SwingLean, 0f, 0f } },
            new ItemHold { Id = "STOOL", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "TABLE", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "TEAPOT", Turn = new[] { 0f, 0f, 0f } },
            new ItemHold { Id = "TENT", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "TERRIER", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "TOMATO", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "TONIC", Turn = new[] { 0f, 180f, 0f } },
            new ItemHold { Id = "TORCH", Turn = new[] { -ThrustLean, 180f, 0f } },
            new ItemHold { Id = "TRAP", Turn = new[] { TrayTilt, 180f, 0f } },
        };

        public static IReadOnlyList<ItemHold> All => holds;

        public static ItemHold For(string id)
        {
            foreach (var hold in holds)
                if (hold.Id == id) return hold;
            return null;
        }

        public static HoldTurn Socket(ItemDefinition item) => item != null && item.IsTwoHanded ? TwoHandSocket : OneHandSocket;

        public static HoldTurn InHand(ItemDefinition item)
        {
            var hold = For(item?.Id);
            return hold == null ? HoldTurn.Identity : (Socket(item).Inverse * HoldTurn.Euler(hold.Turn)).Normalized;
        }

        public static float[] Grip(ItemDefinition item) => For(item?.Id)?.Grip ?? item?.Grip;

        public static List<string> Validate(ItemCatalogue items)
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hold in holds)
            {
                void Require(bool ok, string message) { if (!ok) problems.Add(hold.Id + ": " + message); }
                Require(seen.Add(hold.Id), "is listed twice");
                items.TryGet(hold.Id, out var item);
                Require(item != null && item.Enabled, "is not an enabled item");
                if (item == null) continue;
                Require(hold.Id != GuardPosedItem, "is posed by ItemContactPose while raised, which ignores the hold turn");
                Require(item.Use == null || hold.Grip == null, "keeps its catalogue grip, which the drinking pose lifts to the mouth");
                Require(Within(hold.Turn, 180f), "turn needs three angles within 180 degrees");
                Require(Math.Abs(hold.ModelYaw) <= 180f, "model yaw must stay within 180 degrees");
                Require(hold.Grip == null || Inside(hold.Grip, item.Size), "grip must lie inside the item");
                Require(hold.ModelShift == null || Within(hold.ModelShift, Math.Max(item.Size[0], item.Size[2]) * .5f),
                    "model shift must stay inside the item's footprint");
            }
            return problems;
        }

        static bool Within(float[] values, float limit)
        {
            if (values == null || values.Length != 3) return false;
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > limit) return false;
            return true;
        }

        static bool Inside(float[] grip, float[] size) => grip.Length == 3 && size != null && size.Length == 3 &&
            Math.Abs(grip[0]) <= size[0] * .5f && grip[1] >= 0f && grip[1] <= size[1] && Math.Abs(grip[2]) <= size[2] * .5f;
    }
}
