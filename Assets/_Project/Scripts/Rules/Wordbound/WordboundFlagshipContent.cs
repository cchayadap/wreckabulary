using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundFlagshipDestination
    {
        public string Id { get; }
        public string Name { get; }
        public string Landmark { get; }
        public int HeightMm { get; }
        public IReadOnlyList<string> ModuleIds { get; }
        internal WordboundFlagshipDestination(string id, string name, string landmark, int height, IEnumerable<string> modules)
        { Id = id; Name = name; Landmark = landmark; HeightMm = height; ModuleIds = Array.AsReadOnly(modules.ToArray()); }
    }

    public sealed class WordboundFlagshipRoom
    {
        public string Id { get; }
        public string DestinationId { get; }
        public string Name { get; }
        public string Purpose { get; }
        public CoursePoint Position { get; }
        internal WordboundFlagshipRoom(string id, string destination, string name, string purpose, CoursePoint position)
        { Id = id; DestinationId = destination; Name = name; Purpose = purpose; Position = position; }
    }

    public sealed class WordboundFlagshipStoryBeat
    {
        public int Lap { get; }
        public string CheckpointId { get; }
        public string EventId { get; }
        public string Clue { get; }
        public bool Optional => true;
        internal WordboundFlagshipStoryBeat(int lap, string checkpoint, string eventId, string clue)
        { Lap = lap; CheckpointId = checkpoint; EventId = eventId; Clue = clue; }
    }

    public sealed class WordboundFlagshipEquipment
    {
        public string Id { get; }
        public CoursePoint SizeMm { get; }
        public int Durability { get; }
        public string DeployEffect { get; }
        public float Strength { get; }
        public float Radius { get; }
        public float LifetimeSeconds { get; }
        public float MeleeReach { get; }
        public float BreakPower { get; }
        internal WordboundFlagshipEquipment(ItemDefinition item)
        {
            Id = item.Id; SizeMm = new CoursePoint(Mm(item.Size[0]), Mm(item.Size[1]), Mm(item.Size[2]));
            Durability = item.Durability; DeployEffect = item.Deploy?.Effect.ToString() ?? "";
            Strength = item.Deploy?.Strength ?? 0; Radius = item.Deploy?.Radius ?? 0;
            LifetimeSeconds = item.Deploy?.LifetimeSeconds ?? 0;
            MeleeReach = item.Melee?.Reach ?? 0; BreakPower = item.Melee?.BreakPower ?? 0;
        }
        internal static int Mm(float metres) => checked((int)Math.Round(metres * 1000d, MidpointRounding.AwayFromZero));
        internal object[] Values() => new object[] { Id, SizeMm.X, SizeMm.Y, SizeMm.Z, Durability, DeployEffect,
            BitConverter.SingleToInt32Bits(Strength), BitConverter.SingleToInt32Bits(Radius),
            BitConverter.SingleToInt32Bits(LifetimeSeconds), BitConverter.SingleToInt32Bits(MeleeReach),
            BitConverter.SingleToInt32Bits(BreakPower) };
    }

    public sealed class WordboundFlagshipPuzzleSite
    {
        public int Lap { get; }
        public string PuzzleId { get; }
        public string ModuleId { get; }
        public string PresetId { get; }
        public string RecipeId { get; }
        public string Objective { get; }
        public string CheckpointId { get; }
        public string RecoverySupplyId { get; }
        public string ToolRecoverySupplyId { get; }
        public string ToolRecipeId => ToolRecoverySupplyId.Length == 0 ? "" : "HAMMER";
        public string PropSourceZoneId { get; }
        public string WindReceiverZoneId { get; }
        public string WindReceiverRecipeId { get; }
        public string ReceiverRecoverySupplyId { get; }
        public CoursePoint Position { get; }
        public CoursePoint Target { get; }
        public CourseBox ArrivalBounds { get; }
        public CourseBox ReceiverStartBounds { get; }
        public IReadOnlyList<CourseBox> FloorPadBounds { get; }
        public int Yaw => 0;
        public int SocketRadiusMm => 250;
        public int YawTolerance => 15;
        public int HoldTicks => PresetId == "BalanceWeight" ? 60 : 30;
        public int MinimumPlayers => 1;
        public float ActiveWindowSeconds { get; }
        internal WordboundFlagshipPuzzleSite(int lap, CoursePuzzle puzzle, CoursePoint at,
            WordboundFlagshipEquipment equipment, string objective, string checkpointId, string recoverySupplyId,
            string propSourceZoneId, string windReceiverZoneId, string receiverRecoverySupplyId, string toolRecoverySupplyId)
        {
            Lap = lap; PuzzleId = puzzle.Id; ModuleId = puzzle.ModuleId; PresetId = puzzle.PresetId;
            RecipeId = puzzle.RecipeId; Position = at; Objective = objective;
            CheckpointId = checkpointId; RecoverySupplyId = recoverySupplyId;
            ToolRecoverySupplyId = toolRecoverySupplyId;
            PropSourceZoneId = propSourceZoneId; WindReceiverZoneId = windReceiverZoneId;
            WindReceiverRecipeId = windReceiverZoneId.Length == 0 ? "" : "STOOL";
            ReceiverRecoverySupplyId = receiverRecoverySupplyId;
            int rise = puzzle.PresetId == "BuildRoute"
                ? Math.Min(1500, (int)(equipment.Strength * equipment.Strength / (2 * 9.81) * 550)) : 0;
            int distance = puzzle.PresetId == "BuildRoute" ? equipment.SizeMm.Z / 2 + 800 :
                puzzle.PresetId == "RedirectWind" ? Math.Min(1800, WordboundFlagshipEquipment.Mm(equipment.Radius * .7f)) :
                puzzle.PresetId == "TimedMechanism" ? Math.Min(1500, WordboundFlagshipEquipment.Mm(equipment.Radius * .5f)) : 700;
            Target = new CoursePoint(at.X, at.Y + rise, at.Z + distance);
            ArrivalBounds = Box(Target, 250, 0, 250, 900);
            var receiver = new CoursePoint(at.X, at.Y, at.Z + Math.Min(900, WordboundFlagshipEquipment.Mm(equipment.Radius * .35f)));
            ReceiverStartBounds = Box(receiver, 350, 0, 350, 700);
            FloorPadBounds = Array.AsReadOnly(puzzle.PresetId == "BalanceWeight" ? new[]
            {
                Box(new CoursePoint(at.X - equipment.SizeMm.X / 4, at.Y, at.Z), 100, 0, 100, 80),
                Box(new CoursePoint(at.X + equipment.SizeMm.X / 4, at.Y, at.Z), 100, 0, 100, 80)
            } : Array.Empty<CourseBox>());
            ActiveWindowSeconds = puzzle.PresetId == "TimedMechanism"
                ? Math.Min(3, equipment.LifetimeSeconds / 2) : 0;
        }
        static CourseBox Box(CoursePoint at, int x, int lowerY, int z, int upperY) => new CourseBox(
            new CoursePoint(at.X - x, at.Y + lowerY, at.Z - z), new CoursePoint(at.X + x, at.Y + upperY, at.Z + z));
        static object[] Bounds(CourseBox box) => new object[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z };
        internal object[] Values() => new object[] { Lap, PuzzleId, ModuleId, PresetId, RecipeId, Objective,
            CheckpointId, RecoverySupplyId, ToolRecoverySupplyId, ToolRecipeId, PropSourceZoneId,
            WindReceiverZoneId, WindReceiverRecipeId, ReceiverRecoverySupplyId,
            Position.X, Position.Y, Position.Z, Target.X, Target.Y, Target.Z, BitConverter.SingleToInt32Bits(ActiveWindowSeconds),
            Yaw, SocketRadiusMm, YawTolerance, HoldTicks, Bounds(ArrivalBounds), Bounds(ReceiverStartBounds),
            FloorPadBounds.Select(box => (object)Bounds(box)).ToArray() };
    }

    public sealed partial class WordboundFlagshipContent
    {
        public const int Version = 1;
        public const string CourseId = "wordbound-lost-address";
        static readonly string[] Recipes = { "BAT", "BROOM", "HAMMER", "SPEAR", "SHIELD", "PLATE",
            "TABLE", "SOFA", "BED", "STOOL", "FAN", "CLOCK" };
        static readonly string[] Families = { "CollectLetters", "PlaceObject", "RedirectWind",
            "TimedMechanism", "BuildRoute", "BalanceWeight" };
        static readonly string[] Narratives = { "FindAddress", "WrongDelivery", "RestoreHome" };
        readonly string documentJson;
        public CourseCatalogue Geometry { get; }
        public string Hash { get; }
        public IReadOnlyList<WordboundFlagshipEquipment> Equipment { get; }
        public IReadOnlyList<WordboundFlagshipDestination> Destinations { get; }
        public IReadOnlyList<WordboundFlagshipRoom> Rooms { get; }
        public IReadOnlyList<WordboundFlagshipPuzzleSite> PuzzleSites { get; }
        public IReadOnlyList<WordboundFlagshipStoryBeat> Story { get; }
        public CourseDocument CopyDocument() => CourseJson.ImportDocument(documentJson).Document;

        WordboundFlagshipContent(ItemCatalogue catalogue)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            if (catalogue.Validate(64).Count != 0) throw new ArgumentException("Use a valid gameplay item catalogue.");
            var equipment = Recipes.Select(id =>
            {
                if (!catalogue.TryGet(id, out var item) || !item.Enabled) throw new ArgumentException("Flagship equipment is unavailable: " + id);
                return new WordboundFlagshipEquipment(item);
            }).ToArray();
            foreach (string id in new[] { "BED", "STOOL", "FAN", "CLOCK" })
            {
                var item = catalogue.Get(id);
                var effect = id == "FAN" ? DeployEffect.WindField : id == "CLOCK" ? DeployEffect.SlowField : DeployEffect.JumpPad;
                if (item.Deploy == null || item.Deploy.Effect != effect || item.Deploy.Strength <= 0 ||
                    ((id == "BED" || id == "STOOL") && item.Deploy.LifetimeSeconds != 0) ||
                    ((id == "FAN" || id == "CLOCK") && (item.Deploy.Radius < 1.5f || item.Deploy.LifetimeSeconds < 6)))
                    throw new NotSupportedException("The flagship requires the approved deployed behavior of " + id + ".");
            }
            foreach (string id in new[] { "TABLE", "SOFA" })
                if (catalogue.Get(id).Deploy?.Effect != DeployEffect.Cover)
                    throw new NotSupportedException("Flagship sockets and switches require deployable cover furniture: " + id);
            Geometry = new CourseCatalogue(new[] { new CourseModuleDefinition("FlagshipRoom", 24000, 24000, 6000) },
                equipment.Select(item => new CourseAssetDefinition(item.Id, item.Id, item.SizeMm.X, item.SizeMm.Z, item.SizeMm.Y)),
                Families, new[] { "Cozy" }, Narratives);
            Equipment = Array.AsReadOnly(equipment);
            var destinations = new List<WordboundFlagshipDestination>();
            var rooms = new List<WordboundFlagshipRoom>();
            var sites = new List<WordboundFlagshipPuzzleSite>();
            var story = new List<WordboundFlagshipStoryBeat>();
            var document = Build(destinations, rooms, sites, story);
            documentJson = CourseJson.ExportDocument(document);
            Destinations = destinations.AsReadOnly(); Rooms = rooms.AsReadOnly();
            PuzzleSites = sites.AsReadOnly(); Story = story.AsReadOnly();
            Hash = CourseJson.Sha256(CourseJson.Encode(new Dictionary<string, object>
            {
                ["version"] = Version, ["course"] = documentJson, ["geometry"] = Geometry.Hash,
                ["equipment"] = equipment.Select(item => (object)item.Values()).ToList(),
                ["sites"] = sites.Select(site => (object)site.Values()).ToList(),
                ["story"] = story.Select(beat => (object)new object[] { beat.Lap, beat.CheckpointId, beat.EventId, beat.Clue }).ToList(),
                ["rooms"] = rooms.Select(room => (object)new object[] { room.Id, room.Name, room.Purpose }).ToList(),
                ["destinations"] = destinations.Select(d => (object)new object[] { d.Id, d.Name, d.Landmark }).ToList()
            }));
        }
        public static WordboundFlagshipContent Create(ItemCatalogue catalogue) => new WordboundFlagshipContent(catalogue);
        public CourseResolution Resolve(string seed, CoursePlayRequirements requirements)
        {
            var result = CourseResolver.Resolve(CopyDocument(), Geometry, new CourseResolveRequest(seed, requirements));
            if (result.Success && requirements.Capability == CourseCapability.Circuit)
                WordboundFlagshipRoutes.Measure(result.Manifest);
            return result;
        }
    }
}
