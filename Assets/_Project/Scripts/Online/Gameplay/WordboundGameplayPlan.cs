using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundBodyPresentation
    {
        public WorldVector Size { get; }
        public string VisualKey { get; }
        public int CarrierSeat { get; }
        public WordboundBodyPresentation(WorldVector size, string visualKey, int carrierSeat = -1)
        {
            if (size.X <= 0 || size.Y <= 0 || size.Z <= 0 || size.X > 100 || size.Y > 100 || size.Z > 100 ||
                !WordboundIdentity.IsId(visualKey) || carrierSeat < -1 || carrierSeat > 7)
                throw new ArgumentException("Invalid bounded body presentation.");
            Size = size; VisualKey = visualKey; CarrierSeat = carrierSeat;
        }
    }

    public sealed class WordboundReconciliationPlan
    {
        public IReadOnlyList<string> Remove { get; }
        public IReadOnlyList<WorldBodyState> Add { get; }
        public IReadOnlyList<WorldBodyState> Keep { get; }
        public WordboundReconciliationPlan(IEnumerable<string> existing, MatchSnapshot next)
        {
            var ids = existing?.Take(513).ToArray() ?? throw new ArgumentNullException(nameof(existing));
            if (next == null || ids.Length > 512 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                next.Bodies.Count > 512) throw new ArgumentException("Canonical reconciliation exceeds the proof body budget or repeats IDs.");
            var current = new HashSet<string>(ids, StringComparer.Ordinal);
            var wanted = new HashSet<string>(next.Bodies.Select(b => b.Id), StringComparer.Ordinal);
            if (wanted.Count != next.Bodies.Count) throw new ArgumentException("Canonical body IDs are duplicated.");
            Remove = Array.AsReadOnly(ids.Where(id => !wanted.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            Add = Array.AsReadOnly(next.Bodies.Where(b => !current.Contains(b.Id)).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray());
            Keep = Array.AsReadOnly(next.Bodies.Where(b => current.Contains(b.Id)).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray());
        }
    }

    public static class WordboundGameplayPlan
    {
        public static bool IsPaused(bool localReady, bool transportReady, bool isHost, bool hostReady,
            bool hostPaused, MatchSnapshot frame) => !localReady || !transportReady || frame == null ||
            frame.Progress.Migrating || frame.Progress.Result != null || isHost && (!hostReady || hostPaused);

        public static WordboundBodyPresentation Presentation(WorldBodyState body, MatchSnapshot snapshot,
            ResolvedCourse course, CourseCatalogue geometry, ItemCatalogue items)
        {
            if (body == null || snapshot == null || !snapshot.Bodies.Any(b => ReferenceEquals(b, body)))
                throw new ArgumentException("Presentation requires a body from the exact canonical frame.");
            if (body.Kind == WorldBodyKind.Player)
                return new WordboundBodyPresentation(new WorldVector(.6f, 1.8f, .6f), "player");
            if (body.Kind == WorldBodyKind.Platform)
            {
                var connection = course.Connections.Single(c => c.Id == body.SourceId);
                return new WordboundBodyPresentation(new WorldVector(connection.WidthMm / 1000f, .24f, connection.WidthMm / 1000f), "platform");
            }
            if (body.Kind == WorldBodyKind.Keepsake)
            {
                var team = snapshot.Progress.Teams.Single(t => "keepsake:" + t.Team.ToString(CultureInfo.InvariantCulture) == body.Id);
                int carrier = string.IsNullOrEmpty(team.KeepsakeCarrier) ? -1 : Seat(snapshot.Binding, team.KeepsakeCarrier);
                return new WordboundBodyPresentation(new WorldVector(.45f, .45f, .45f), "keepsake", carrier);
            }
            long id = Number(body.Id);
            if (body.Kind == WorldBodyKind.Tile)
                return new WordboundBodyPresentation(new WorldVector(.3f, .12f, .3f),
                    "letter:" + snapshot.Economy.Tiles.Single(t => t.Key == id).Value);
            var item = snapshot.Economy.Items.Single(i => i.Id == id);
            var placement = course.Placements.FirstOrDefault(p => p.Id == body.SourceId);
            WorldVector size;
            string key;
            if (placement != null)
            {
                var asset = geometry.Assets[placement.AssetId];
                size = new WorldVector(asset.WidthMm / 1000f, asset.HeightMm / 1000f, asset.DepthMm / 1000f);
                key = asset.Id;
            }
            else
            {
                if (!items.TryGet(item.Word, out var definition) || definition.Size == null || definition.Size.Length != 3)
                    throw new ArgumentException("Canonical crafted item has no approved dimensions.");
                size = new WorldVector(definition.Size[0], definition.Size[1], definition.Size[2]);
                key = definition.Id;
            }
            int owner = -1;
            if (item.State == ItemState.Held || item.State == ItemState.Carried)
            {
                owner = item.HolderId;
            }
            return new WordboundBodyPresentation(size, key, owner);
        }

        public static Dictionary<string, object> Command(PlayerCommand command) => new Dictionary<string, object>
        {
            ["sequence"] = command.Sequence.ToString(CultureInfo.InvariantCulture),
            ["epoch"] = command.Epoch.ToString(CultureInfo.InvariantCulture),
            ["action"] = command.Action.ToString(), ["target"] = command.Target, ["revision"] = command.Revision,
            ["slot"] = command.Slot, ["value"] = command.Value
        };

        public static long NextSequence(MatchSnapshot frame, string actor, long lastSent, long sentEpoch)
        {
            if (frame == null || !frame.Commands.Frontiers.TryGetValue(actor, out long frontier))
                throw new ArgumentException("Command sequence requires a canonical signed seat.");
            long previous = sentEpoch == frame.Epoch ? Math.Max(frontier, lastSent) : frontier;
            if (previous >= long.MaxValue - 1) throw new InvalidOperationException("Command sequence space is exhausted.");
            return previous + 1;
        }

        public static int Seat(MatchBinding binding, string actor)
        {
            for (int i = 0; i < binding.Seats.Count; i++) if (binding.Seats[i].PlayerId == actor) return i;
            throw new ArgumentException("Actor does not belong to the signed roster.");
        }
        static long Number(string id) => long.Parse(id.Substring(id.IndexOf(':') + 1), CultureInfo.InvariantCulture);
    }
}
