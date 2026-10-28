using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundParcelServiceContent
    {
        public const string CourseId = "wordbound-parcel-service-v1";
        public const int FurnitureCount = 21;
        public const int FurnitureLetters = 90;
        readonly string documentJson;
        public CourseCatalogue Geometry { get; }
        public CourseDocument CopyDocument() => CourseJson.ImportDocument(documentJson).Document;
        public static WordboundParcelServiceContent Create(ItemCatalogue items) => new WordboundParcelServiceContent(items);
        public CourseResolution Resolve(string seed, CoursePlayRequirements play) =>
            CourseResolver.Resolve(CopyDocument(), Geometry, new CourseResolveRequest(seed, play));

        WordboundParcelServiceContent(ItemCatalogue items)
        {
            if (items == null || items.Validate(64).Count != 0) throw new ArgumentException("Use the valid original item catalogue.");
            var craftable = new HashSet<string>(new[] { "TABLE", "STOOL", "CLOCK", "HAMMER" }, StringComparer.Ordinal);
            var assets = new[] { "BOX", "CRATE", "CHEST", "DESK", "SHELF", "TABLE", "STOOL", "CLOCK", "HAMMER" }
                .Select(id => {
                    if (!items.TryGet(id, out var item) || item.Size == null || item.Size.Length != 3 ||
                        item.Model != "Items/" + id || craftable.Contains(id) && !item.Enabled || id != "HAMMER" && craftable.Contains(id) && !item.CanDeploy)
                        throw new ArgumentException("Parcel Service needs original model and dimensions for " + id);
                    return new CourseAssetDefinition(id, id, WordboundFlagshipEquipment.Mm(item.Size[0]),
                        WordboundFlagshipEquipment.Mm(item.Size[2]), WordboundFlagshipEquipment.Mm(item.Size[1]), craftable.Contains(id));
                }).ToArray();
            Geometry = new CourseCatalogue(new[] {
                new CourseModuleDefinition("ServiceRoom", 8000, 8000, 3600),
                new CourseModuleDefinition("SortingRoom", 10000, 8000, 3600),
                new CourseModuleDefinition("DispatchRoom", 6000, 8000, 3600)
            }, assets, new[] { "PlaceObject" }, new[] { "Cozy" }, new[] { "FindAddress", "WrongDelivery", "RestoreHome" });
            documentJson = CourseJson.ExportDocument(Build());
        }

        CourseDocument Build()
        {
            var d = new CourseDocument { Schema = 2, Id = CourseId, Title = "Parcel Service",
                SupportedModes = new List<string> { "TimeRun", "Freestyle" } };
            d.Layers.Add(new CourseLayer { Id = "Packing" });
            Room("Service", "ServiceRoom", 0, 4000);
            Room("Sorting", "SortingRoom", 9000, 5000);
            Room("Dispatch", "DispatchRoom", 17000, 3000);
            Door("ServiceSorting", "Service", "Sorting", 4000);
            Door("SortingDispatch", "Sorting", "Dispatch", 14000);
            for (int slot = 0; slot < 8; slot++)
                d.Spawns.Add(new CourseSpawn { Id = "Start" + slot, ModuleId = "Service",
                    Position = P(-2400 + slot % 4 * 1600, slot < 4 ? -2600 : -1200) });
            Recovery("ServiceSafe", "Service", P(0, 500));
            Recovery("SortingSafe", "Sorting", P(7300, -400));
            Recovery("DispatchSafe", "Dispatch", P(17000, -1300));
            Supply("ClockLetters", "Service", P(-1700, 1300), "CLOCK");
            Supply("PracticeHammer", "Service", P(1700, 1300), "HAMMER");
            Supply("TableLetters", "Sorting", P(9000, -1500), "TABLE");
            Supply("StoolLetters", "Dispatch", P(18400, -1600), "STOOL");
            Puzzle("SortingBench", "Sorting", "TABLE", "TableLetters");
            Puzzle("DispatchStand", "Dispatch", "STOOL", "StoolLetters", "SortingBench");
            Puzzle("ReturnClock", "Service", "CLOCK", "ClockLetters", "SortingBench");
            Checkpoint("OutboundStart", "Service", "ServiceSafe", "", "");
            Checkpoint("OutboundSort", "Sorting", "SortingSafe", "ServiceSorting", "SortingBench");
            Checkpoint("OutboundDelivery", "Dispatch", "DispatchSafe", "SortingDispatch", "DispatchStand");
            Checkpoint("InboundStart", "Dispatch", "DispatchSafe", "", "");
            Checkpoint("InboundSort", "Sorting", "SortingSafe", "SortingDispatch", "SortingBench");
            Checkpoint("InboundReturn", "Service", "ServiceSafe", "ServiceSorting", "ReturnClock");
            Route("Outbound", "Service", "Dispatch", new[] { "ServiceSorting", "SortingDispatch" },
                new[] { "OutboundStart", "OutboundSort", "OutboundDelivery" });
            Route("Inbound", "Dispatch", "Service", new[] { "SortingDispatch", "ServiceSorting" },
                new[] { "InboundStart", "InboundSort", "InboundReturn" });
            for (int lap = 1; lap <= 3; lap++)
            {
                bool inbound = lap == 2;
                d.Laps.Add(new CourseLap { Number = lap, StartModuleId = inbound ? "Dispatch" : "Service",
                    FinishModuleId = inbound ? "Service" : "Dispatch", NarrativeId = new[] { "FindAddress", "WrongDelivery", "RestoreHome" }[lap - 1],
                    SectionIds = new List<string> { inbound ? "Inbound" : "Outbound" } });
                Site(lap, "SortingBench", inbound ? "InboundSort" : "OutboundSort", "TableLetters", P(10900, 1900));
                Site(lap, inbound ? "ReturnClock" : "DispatchStand", inbound ? "InboundReturn" : "OutboundDelivery",
                    inbound ? "ClockLetters" : "StoolLetters", inbound ? P(0, 2300) : P(17000, 1200));
            }
            Reserve("SortingSocket", P(10000, 850), P(11800, 2650));
            Reserve("DispatchSocket", P(16400, 650), P(17600, 1750));
            Reserve("ReturnSocket", P(-350, 1850), P(350, 2750));
            Prop("ServiceCounterLeft", "Service", "DESK", -1200, 2400, 180);
            Prop("ServiceCounterRight", "Service", "DESK", 1200, 2400, 180);
            Prop("ServiceShelfLeft", "Service", "SHELF", -2900, 3200, 180);
            Prop("ServiceShelfRight", "Service", "SHELF", 2900, 3200, 180);
            Prop("ServiceParcel", "Service", "CHEST", 3000, 2500, 180);
            Prop("SortingBenchProp", "Sorting", "TABLE", 9000, 2100, 0);
            Prop("SortingShelfWest", "Sorting", "SHELF", 5500, 3300, 180);
            Prop("SortingShelfEast", "Sorting", "SHELF", 12500, 3300, 180);
            Prop("SortingCrateNorthWest", "Sorting", "CRATE", 5500, 2400, 0);
            Prop("SortingCrateNorth", "Sorting", "CRATE", 6700, 2400, 0);
            Prop("SortingCrateSouthWest", "Sorting", "CRATE", 5500, -2800, 0);
            Prop("SortingCrateSouthEast", "Sorting", "CRATE", 12500, -2800, 0);
            Prop("SortingBoxNorthEast", "Sorting", "BOX", 12500, 2400, 0);
            d.Zones.Add(new CourseZone { Id = "SortingParcelBay", ModuleId = "Sorting", Purpose = "Props",
                Bounds = new CourseBox(P(6100, -3500), P(11900, -2100)), MinCount = 3, MaxCount = 3,
                GridMm = 250, SpacingMm = 250, Yaws = new List<int> { 0, 180 }, LayerIds = new List<string> { "Packing" },
                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "BOX" } } });
            Prop("DispatchCounter", "Dispatch", "DESK", 17000, 2400, 180);
            Prop("DispatchParcelWest", "Dispatch", "CHEST", 15400, 2400, 180);
            Prop("DispatchParcelEast", "Dispatch", "CHEST", 18600, 2400, 180);
            Prop("DispatchBoxWest", "Dispatch", "BOX", 15200, -2600, 0);
            Prop("DispatchBoxEast", "Dispatch", "BOX", 18800, -2600, 0);
            return d;

            void Room(string id, string definition, int x, int halfWidth)
            {
                d.Modules.Add(new CourseModule { Id = id, DefinitionId = definition, LayerId = "Packing", Position = P(x, 0) });
                Reserve("Route_" + id, P(x - halfWidth, -1250), P(x + halfWidth, 1250));
            }
            void Reserve(string id, CoursePoint min, CoursePoint max) => d.ReservedVolumes.Add(new CourseReservedVolume {
                Id = id, Reason = "Path", Bounds = new CourseBox(min, new CoursePoint(max.X, 2500, max.Z)) });
            void Door(string id, string from, string to, int x) => d.Connections.Add(new CourseConnection {
                Id = id, FromModuleId = from, ToModuleId = to, Kind = "Door", WidthMm = 2500, From = P(x, 0), To = P(x, 0) });
            void Recovery(string id, string module, CoursePoint position) => d.RecoveryAnchors.Add(new CourseRecovery {
                Id = id, ModuleId = module, Position = position, RadiusMm = 600 });
            void Supply(string id, string module, CoursePoint position, string word) => d.Supplies.Add(new CourseSupply {
                Id = id, ModuleId = module, Position = position, Letters = word, Renewable = true, Protected = true });
            void Puzzle(string id, string module, string recipe, string supply, string prerequisite = "") => d.Puzzles.Add(new CoursePuzzle {
                Id = id, ModuleId = module, PresetId = "PlaceObject", RecipeId = recipe, RequiredLetters = recipe,
                ConsumeLetters = true, RecoverySupplyIds = new List<string> { supply },
                PrerequisiteIds = prerequisite == "" ? new List<string>() : new List<string> { prerequisite } });
            void Checkpoint(string id, string module, string safe, string entry, string puzzle) => d.Checkpoints.Add(new CourseCheckpoint {
                Id = id, ModuleId = module, RecoveryAnchorId = safe, EntryConnectionId = entry,
                PuzzleIds = puzzle == "" ? new List<string>() : new List<string> { puzzle } });
            void Route(string id, string from, string to, string[] connections, string[] checkpoints) => d.RouteSections.Add(new CourseRouteSection {
                Id = id, EntryModuleId = from, ExitModuleId = to, Alternatives = new List<CourseAlternative> {
                    new CourseAlternative { Id = id + "Route", ConnectionIds = connections.ToList(), CheckpointIds = checkpoints.ToList() } } });
            void Site(int lap, string puzzle, string checkpoint, string supply, CoursePoint at) => d.PhysicalSites.Add(new CoursePhysicalSite {
                Lap = lap, PuzzleId = puzzle, CheckpointId = checkpoint, RecoverySupplyId = supply, Position = at, Target = at,
                SocketRadiusMm = 500, YawTolerance = 15, HoldTicks = 30 });
            void Prop(string id, string module, string assetId, int x, int z, int yaw)
            {
                var asset = Geometry.Assets[assetId]; bool sideways = yaw % 180 != 0;
                int hx = ((sideways ? asset.DepthMm : asset.WidthMm) + 1) / 2 + 250;
                int hz = ((sideways ? asset.WidthMm : asset.DepthMm) + 1) / 2 + 250;
                d.Zones.Add(new CourseZone { Id = id, ModuleId = module, Purpose = "Props", Placement = "Fixed",
                    FixedPosition = P(x, z), FixedYaw = yaw, Bounds = new CourseBox(P(x - hx, z - hz), P(x + hx, z + hz)),
                    MinCount = 1, MaxCount = 1, SpacingMm = 250, Yaws = new List<int> { yaw }, LayerIds = new List<string> { "Packing" },
                    Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = assetId } } });
            }
        }
        static CoursePoint P(int x, int z) => new CoursePoint(x, 0, z);
    }
}

