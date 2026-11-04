using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundParcelServiceContent
    {
        public const string CourseId = "wordbound-parcel-service-v2";
        public const string LegacyCourseId = "wordbound-parcel-service-v1";
        public const string CourseTitle = "Parcel Depot";
        public const int FurnitureCount = 138;
        public const int FurnitureLetters = 671;
        public const int PhysicalSiteCount = 11;
        public const int MezzanineHeightMm = 4000;
        public static readonly CoursePoint StartSeat = new CoursePoint(-28000, 0, 0);
        public static readonly CoursePoint RearStartSeat = new CoursePoint(-28000, 0, -2600);
        readonly string documentJson;
        public CourseCatalogue Geometry { get; }
        public CourseDocument CopyDocument() => CourseJson.ImportDocument(documentJson).Document;
        public static WordboundParcelServiceContent Create(ItemCatalogue items) => new WordboundParcelServiceContent(items);
        public CourseResolution Resolve(string seed, CoursePlayRequirements play) =>
            CourseResolver.Resolve(CopyDocument(), Geometry, new CourseResolveRequest(seed, play));

        WordboundParcelServiceContent(ItemCatalogue items)
        {
            if (items == null || items.Validate(64).Count != 0) throw new ArgumentException("Use the valid original item catalogue.");
            var craftable = new HashSet<string>(new[] { "TABLE", "STOOL", "CLOCK", "SOFA", "BED", "HAMMER" }, StringComparer.Ordinal);
            var assets = new[] { "BOX", "CRATE", "CHEST", "DESK", "SHELF", "BARREL", "CABINET", "CHAIR", "PLANT",
                    "TABLE", "STOOL", "CLOCK", "SOFA", "BED", "HAMMER" }
                .Select(id => {
                    if (!items.TryGet(id, out var item) || item.Size == null || item.Size.Length != 3 ||
                        item.Model != "Items/" + id || craftable.Contains(id) && !item.Enabled || id != "HAMMER" && craftable.Contains(id) && !item.CanDeploy)
                        throw new ArgumentException("Parcel Depot needs original model and dimensions for " + id);
                    return new CourseAssetDefinition(id, id, WordboundFlagshipEquipment.Mm(item.Size[0]),
                        WordboundFlagshipEquipment.Mm(item.Size[2]), WordboundFlagshipEquipment.Mm(item.Size[1]), craftable.Contains(id));
                }).ToArray();
            Geometry = new CourseCatalogue(new[] {
                new CourseModuleDefinition("YardLot", 16000, 40000, 2600),
                new CourseModuleDefinition("VanBay", 5000, 2400, 2600),
                new CourseModuleDefinition("CounterRoom", 10000, 8000, 3600),
                new CourseModuleDefinition("DockRoom", 10000, 24000, 4800),
                new CourseModuleDefinition("SortingHall", 26000, 24000, 6000),
                new CourseModuleDefinition("LockerAisle", 26000, 8000, 3600),
                new CourseModuleDefinition("DispatchGarage", 15000, 40000, 5200),
                new CourseModuleDefinition("MezzanineOffice", 26000, 8000, 3200)
            }, assets, new[] { "PlaceObject" }, new[] { "Cozy" }, new[] { "FindAddress", "WrongDelivery", "RestoreHome" });
            documentJson = CourseJson.ExportDocument(Build());
        }

        CourseDocument Build()
        {
            var d = new CourseDocument { Schema = 2, Id = CourseId, Title = CourseTitle,
                SupportedModes = new List<string> { "TimeRun", "Freestyle" } };
            d.Layers.Add(new CourseLayer { Id = "Packing" });
            d.Layers.Add(new CourseLayer { Id = "Mezzanine", HeightMm = MezzanineHeightMm });
            Room("Yard", "YardLot", -23000, 0);
            Van("VanNorth", 12000); Van("VanMiddle", 0); Van("VanSouth", -12000);
            Room("Counter", "CounterRoom", -10000, 16000);
            Room("Dock", "DockRoom", -10000, 0);
            Room("Hall", "SortingHall", 8000, 0);
            Room("Lockers", "LockerAisle", 8000, 16000);
            Room("Garage", "DispatchGarage", 28500, 0);
            Room("Mezzanine", "MezzanineOffice", 8000, 16000, "Mezzanine");

            Door("VanNorthDoor", "Yard", "VanNorth", -31000, 12000, 1800);
            Door("VanMiddleDoor", "Yard", "VanMiddle", -31000, 0, 1800);
            Door("VanSouthDoor", "Yard", "VanSouth", -31000, -12000, 1800);
            Door("YardCounter", "Yard", "Counter", -15000, 16000, 2500);
            Door("YardDockNorth", "Yard", "Dock", -15000, 6000, 4000);
            Door("YardDockSouth", "Yard", "Dock", -15000, -6000, 4000);
            Door("CounterDock", "Counter", "Dock", -10000, 12000, 2500);
            Door("CounterLockers", "Counter", "Lockers", -5000, 16000, 2500);
            Door("ConveyorNorth", "Dock", "Hall", -5000, 6000, 3500);
            Door("ConveyorSouth", "Dock", "Hall", -5000, -6000, 3500);
            Door("HallLockers", "Hall", "Lockers", 8000, 12000, 3000);
            Door("GarageNorth", "Hall", "Garage", 21000, 6000, 4000);
            Door("GarageSouth", "Hall", "Garage", 21000, -6000, 4000);
            Door("LockersGarage", "Lockers", "Garage", 21000, 17500, 3000);
            Stairs("HallStairs", "Hall", 1000, 8000, 1000, 15000);
            Stairs("GarageStairs", "Garage", 27000, 13500, 20000, 13500);

            Lane("Lane_VanNorth", "Yard", -31000, 11100, -23000, 12900);
            Lane("Lane_VanSouth", "Yard", -31000, -12900, -23000, -11100);
            Lane("Lane_YardCounter", "Yard", -23000, 14750, -15000, 17250);
            Lane("Lane_YardDockNorth", "Yard", -23000, 4000, -15000, 8000);
            Lane("Lane_YardDockSouth", "Yard", -23000, -8000, -15000, -4000);
            Lane("Lane_DockNorth", "Dock", -15000, 4000, -5000, 8000);
            Lane("Lane_DockSouth", "Dock", -15000, -8000, -5000, -4000);
            Lane("Lane_HallNorth", "Hall", -5000, 4000, 21000, 8000);
            Lane("Lane_HallSouth", "Hall", -5000, -8000, 21000, -4000);
            Lane("Lane_LockersGarage", "Lockers", 8000, 16000, 21000, 19000);
            Lane("Lane_GarageNorth", "Garage", 21000, 4000, 28500, 8000);
            Lane("Lane_GarageSouth", "Garage", 21000, -8000, 28500, -4000);
            Lane("Lane_GarageLockers", "Garage", 21000, 16000, 28500, 19000);
            Lane("Lane_GarageStairs", "Mezzanine", 17500, 12000, 19500, 16000);
            d.ReservedVolumes.Add(new CourseReservedVolume { Id = "Spawn_Yard", Reason = "Spawn",
                Bounds = new CourseBox(P(-29000, -4000), new CoursePoint(-22200, 2500, 1250)) });

            for (int slot = 0; slot < 8; slot++)
                d.Spawns.Add(new CourseSpawn { Id = "Start" + slot, ModuleId = "Yard",
                    Position = P(StartSeat.X + slot % 4 * 1600, slot == 0 ? StartSeat.Z : slot < 4 ? RearStartSeat.Z : -1200) });

            Recovery("YardSafe", "Yard", -20500, 0);
            Recovery("CounterSafe", "Counter", -12500, 16000);
            Recovery("DockSafe", "Dock", -10000, 0);
            Recovery("HallSafe", "Hall", 17000, 0);
            Recovery("LockersSafe", "Lockers", -1000, 16000);
            Recovery("GarageSafe", "Garage", 24000, 0);
            Recovery("OfficeSafe", "Mezzanine", 15500, 16000);

            Supply("PracticeHammer", "VanMiddle", -35200, 0, "HAMMER");
            Supply("BedLetters", "Dock", -7000, 1000, "BED");
            Supply("TableLetters", "Hall", 4000, -1000, "TABLE");
            Supply("StepLetters", "Lockers", 4000, 15000, "STOOL");
            Supply("ClockLetters", "Counter", -6500, 15200, "CLOCK");
            Supply("SofaLetters", "Mezzanine", 4000, 15000, "SOFA");
            Supply("StoolLetters", "Garage", 34000, -1000, "STOOL");

            Puzzle("DockBed", "Dock", "BED", "BedLetters");
            Puzzle("SortingBench", "Hall", "TABLE", "TableLetters");
            Puzzle("OfficeSofa", "Mezzanine", "SOFA", "SofaLetters", "SortingBench");
            Puzzle("DispatchStand", "Garage", "STOOL", "StoolLetters", "SortingBench");
            Puzzle("LockerStep", "Lockers", "STOOL", "StepLetters");
            Puzzle("ReturnClock", "Counter", "CLOCK", "ClockLetters");

            Checkpoint("OutStart", "Yard", "YardSafe", "", "");
            Checkpoint("OutDock", "Dock", "DockSafe", "", "DockBed");
            Checkpoint("OutHall", "Hall", "HallSafe", "", "SortingBench");
            Checkpoint("OutOffice", "Mezzanine", "OfficeSafe", "HallStairs", "OfficeSofa");
            Checkpoint("OutGarage", "Garage", "GarageSafe", "", "DispatchStand");
            Checkpoint("InStart", "Garage", "GarageSafe", "", "");
            Checkpoint("InHall", "Hall", "HallSafe", "GarageSouth", "SortingBench");
            Checkpoint("InLockers", "Lockers", "LockersSafe", "LockersGarage", "LockerStep");
            Checkpoint("InCounter", "Counter", "CounterSafe", "", "ReturnClock");
            Checkpoint("InFinish", "Yard", "YardSafe", "YardCounter", "");

            Section("Intake", "Yard", "Dock",
                ("IntakeNorth", new[] { "YardDockNorth" }, new[] { "OutStart", "OutDock" }),
                ("IntakeSouth", new[] { "YardDockSouth" }, new[] { "OutStart", "OutDock" }));
            Section("Sort", "Dock", "Hall",
                ("SortNorth", new[] { "ConveyorNorth" }, new[] { "OutHall" }),
                ("SortSouth", new[] { "ConveyorSouth" }, new[] { "OutHall" }));
            Section("Dispatch", "Hall", "Garage",
                ("DispatchFloor", new[] { "GarageNorth" }, new[] { "OutGarage" }),
                ("DispatchMezzanine", new[] { "HallStairs", "GarageStairs" }, new[] { "OutOffice", "OutGarage" }));
            Section("Return", "Garage", "Counter",
                ("ReturnHall", new[] { "GarageSouth", "ConveyorSouth", "CounterDock" }, new[] { "InStart", "InHall", "InCounter" }),
                ("ReturnLockers", new[] { "LockersGarage", "CounterLockers" }, new[] { "InStart", "InLockers", "InCounter" }));
            Section("Home", "Counter", "Yard",
                ("HomeGate", new[] { "YardCounter" }, new[] { "InFinish" }));
            for (int lap = 1; lap <= 3; lap++)
            {
                bool inbound = lap == 2;
                d.Laps.Add(new CourseLap { Number = lap, StartModuleId = inbound ? "Garage" : "Yard",
                    FinishModuleId = inbound ? "Yard" : "Garage", NarrativeId = new[] { "FindAddress", "WrongDelivery", "RestoreHome" }[lap - 1],
                    SectionIds = inbound ? new List<string> { "Return", "Home" } : new List<string> { "Intake", "Sort", "Dispatch" } });
                if (inbound)
                {
                    Site(lap, "SortingBench", "InHall", "TableLetters");
                    Site(lap, "LockerStep", "InLockers", "StepLetters");
                    Site(lap, "ReturnClock", "InCounter", "ClockLetters");
                }
                else
                {
                    Site(lap, "DockBed", "OutDock", "BedLetters");
                    Site(lap, "SortingBench", "OutHall", "TableLetters");
                    Site(lap, "OfficeSofa", "OutOffice", "SofaLetters");
                    Site(lap, "DispatchStand", "OutGarage", "StoolLetters");
                }
            }
            Socket("DockBed", -14000, 1250, -11600, 3800);
            Socket("SortingBench", 11300, 1250, 13700, 3200);
            Socket("OfficeSofa", 10800, 17250, 13200, 19600);
            Socket("DispatchStand", 31200, 1250, 32800, 3000);
            Socket("LockerStep", -3300, 17250, -1700, 19200);
            Socket("ReturnClock", -8500, 17250, -6700, 19600);

            Grid("YardPallet", "Yard", "CRATE", -28500, 17500, 1000, 1000, 2, 2, 0);
            Grid("YardVanNorthParcels", "Yard", "BOX", -27500, 13800, 900, 900, 2, 1, 0);
            Prop("YardBarrelWest", "Yard", "BARREL", -29500, 7000, 0);
            Prop("YardBarrelEast", "Yard", "BARREL", -28500, 7000, 0);
            Grid("YardBarrelStack", "Yard", "BARREL", -29500, 7850, 1000, 850, 2, 1, 0);
            Grid("YardCrateSouth", "Yard", "CRATE", -28500, -17500, 1000, 1000, 2, 1, 0);
            Grid("YardVanSouthParcels", "Yard", "BOX", -27500, -13800, 900, 900, 2, 1, 0);
            Grid("YardChest", "Yard", "CHEST", -20000, 11000, 1200, 1000, 2, 1, 0);
            Grid("YardPlanterNorth", "Yard", "PLANT", -21000, 19500, 1200, 1000, 3, 1, 0);
            Grid("YardPlanterSouth", "Yard", "PLANT", -21000, -19500, 1200, 1000, 3, 1, 0);
            Prop("VanNorthChest", "VanNorth", "CHEST", -35000, 12000, 0);
            Prop("VanNorthBox", "VanNorth", "BOX", -33700, 12000, 0);
            Prop("VanMiddleBox", "VanMiddle", "BOX", -33300, 700, 0);
            Prop("VanSouthChest", "VanSouth", "CHEST", -35000, -12000, 0);
            Prop("VanSouthBox", "VanSouth", "BOX", -33700, -12000, 0);

            Prop("CounterDeskWest", "Counter", "DESK", -13600, 13350, 0);
            Prop("CounterDeskEast", "Counter", "DESK", -6400, 13350, 0);
            Prop("CounterChairWest", "Counter", "CHAIR", -13600, 12500, 180);
            Prop("CounterChairEast", "Counter", "CHAIR", -6400, 12500, 180);
            Prop("CounterShelfWest", "Counter", "SHELF", -12800, 19500, 180);
            Prop("CounterShelfEast", "Counter", "SHELF", -5800, 19500, 180);
            Prop("CounterClock", "Counter", "CLOCK", -11700, 19600, 180);
            Prop("CounterPlant", "Counter", "PLANT", -14300, 19300, 0);
            Prop("CounterPlantEast", "Counter", "PLANT", -5450, 18300, 0);

            Grid("DockCrateNorthWest", "Dock", "CRATE", -14000, 10000, 1000, 1000, 2, 2, 0);
            Grid("DockCrateNorthEast", "Dock", "CRATE", -7000, 10000, 1000, 1000, 2, 2, 0);
            Grid("DockParcelsEast", "Dock", "BOX", -6500, 3000, 900, 900, 2, 1, 0);
            Grid("DockParcelsWest", "Dock", "BOX", -14300, -2300, 900, 900, 2, 1, 0);
            Grid("DockChestSouthWest", "Dock", "CHEST", -14000, -10500, 1200, 1000, 2, 1, 0);
            Grid("DockCrateSouthEast", "Dock", "CRATE", -7000, -10000, 1000, -1000, 2, 2, 0);

            Grid("ConveyorNorthTable", "Hall", "TABLE", -3500, 2700, 1450, 1000, 5, 1, 0);
            Grid("ConveyorNorthParcels", "Hall", "BOX", 3800, 2700, 900, 900, 2, 1, 0);
            Grid("ConveyorSouthTable", "Hall", "TABLE", 10000, -2700, 1450, 1000, 5, 1, 0);
            Grid("SortingLineWest", "Hall", "TABLE", -1800, -2700, 1450, 1000, 3, 1, 0);
            Prop("SortingBenchProp", "Hall", "TABLE", 15500, 2100, 0);
            Prop("SortingShelfWest", "Hall", "SHELF", -3500, 11500, 180);
            Prop("SortingShelfNorth", "Hall", "SHELF", 4000, 11500, 180);
            Prop("SortingShelfEast", "Hall", "SHELF", 14000, 11500, 180);
            Grid("SortingRackNorth", "Hall", "SHELF", 11600, 9300, 1250, 1000, 3, 1, 0);
            Prop("SortingCrateNorth", "Hall", "CRATE", -2000, 10000, 0);
            Grid("SortingParcelsNorth", "Hall", "BOX", 3600, 9300, 900, 900, 2, 1, 0);
            Prop("SortingShelfSouth", "Hall", "SHELF", 13000, -11500, 0);
            Grid("SortingParcelsSouth", "Hall", "BOX", 16700, -10500, 900, 900, 2, 1, 0);
            d.Zones.Add(new CourseZone { Id = "SortingParcelBay", ModuleId = "Hall", Purpose = "Props",
                Bounds = new CourseBox(P(-2000, -11500), P(5000, -8500)), MinCount = 3, MaxCount = 3,
                GridMm = 250, SpacingMm = 250, Yaws = new List<int> { 0, 180 }, LayerIds = new List<string> { "Packing" },
                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "BOX" } } });

            foreach (int x in new[] { -4200, -800, 2400, 11000 })
                Prop("LockerShelfNorth" + Name(x), "Lockers", "SHELF", x, 19500, 180);
            foreach (int x in new[] { -4200, 3600, 11500, 17000 })
                Prop("LockerShelfSouth" + Name(x), "Lockers", "SHELF", x, 12500, 0);
            Grid("LockerCabinetNorth", "Lockers", "CABINET", 3700, 19450, 1250, 1000, 3, 1, 180);
            Grid("LockerBenchNorth", "Lockers", "STOOL", 3700, 18300, 1250, 1000, 2, 1, 0);
            Grid("LockerCabinetSouth", "Lockers", "CABINET", 12800, 12550, 1250, 1000, 3, 1, 0);
            Grid("LockerBenchSouth", "Lockers", "STOOL", 13425, 13800, 1250, 1000, 2, 1, 0);

            foreach (int x in new[] { -3500, 2400, 15000, 17500 })
                Prop("OfficeDesk" + Name(x), "Mezzanine", "DESK", x, 19000, 180);
            Prop("OfficeChairWest", "Mezzanine", "CHAIR", -3500, 18000, 180);
            Prop("OfficeChairEast", "Mezzanine", "CHAIR", 15000, 18000, 180);
            foreach (int x in new[] { 2400, 17500 })
                Prop("OfficeChair" + Name(x), "Mezzanine", "CHAIR", x, 18000, 180);
            foreach (int x in new[] { -3500, 4500, 12500 })
                Prop("OfficeCabinet" + Name(x), "Mezzanine", "CABINET", x, 12700, 0);
            Prop("OfficeLoungeSofa", "Mezzanine", "SOFA", -600, 18900, 0);
            foreach (int x in new[] { -1950, 750 })
                Prop("OfficeLoungePlant" + Name(x), "Mezzanine", "PLANT", x, 19550, 0);
            Prop("OfficePlantEast", "Mezzanine", "PLANT", 20500, 19500, 0);

            Grid("GarageCrateNorth", "Garage", "CRATE", 32000, 9000, 1000, 1000, 2, 2, 0);
            Grid("GarageCrateWest", "Garage", "CRATE", 24500, 10000, 1000, 1000, 2, 1, 0);
            Grid("GarageCrateSouth", "Garage", "CRATE", 32000, -10000, 1000, -1000, 2, 2, 0);
            Grid("GarageChest", "Garage", "CHEST", 32000, 15500, 1200, 1000, 2, 1, 0);
            Prop("GarageWorkbench", "Garage", "TABLE", 35300, 12500, 270);
            Prop("GarageWorkStool", "Garage", "STOOL", 34400, 12500, 0);
            Prop("GarageBarrelWest", "Garage", "BARREL", 23500, -14500, 0);
            Prop("GarageBarrelEast", "Garage", "BARREL", 24500, -14500, 0);
            Prop("GarageBarrelEnd", "Garage", "BARREL", 25500, -14500, 0);
            Grid("GarageBarrelDepot", "Garage", "BARREL", 23500, -13650, 1000, 850, 3, 1, 0);
            return d;

            int Floor(string module) => d.Layers.Single(layer => layer.Id == d.Modules.Single(m => m.Id == module).LayerId).HeightMm;
            CoursePoint At(string module, int x, int z) => new CoursePoint(x, Floor(module), z);
            void Room(string id, string definition, int x, int z, string layer = "Packing")
            {
                int y = d.Layers.Single(value => value.Id == layer).HeightMm;
                d.Modules.Add(new CourseModule { Id = id, DefinitionId = definition, LayerId = layer, Position = new CoursePoint(x, y, z) });
                var size = Geometry.Modules[definition];
                Lane("Route_" + id, id, x - size.WidthMm / 2, z - 1250, x + size.WidthMm / 2, z + 1250);
                Lane("Cross_" + id, id, x - 1250, z - size.DepthMm / 2, x + 1250, z + size.DepthMm / 2);
            }
            void Van(string id, int z) => d.Modules.Add(new CourseModule { Id = id, DefinitionId = "VanBay", LayerId = "Packing", Position = P(-33500, z) });
            void Lane(string id, string module, int x1, int z1, int x2, int z2)
            {
                int y = Floor(module);
                d.ReservedVolumes.Add(new CourseReservedVolume { Id = id, Reason = "Path",
                    Bounds = new CourseBox(new CoursePoint(x1, y, z1), new CoursePoint(x2, y + 2500, z2)) });
            }
            void Socket(string puzzle, int x1, int z1, int x2, int z2) =>
                Lane("Socket_" + puzzle, d.Puzzles.Single(p => p.Id == puzzle).ModuleId, x1, z1, x2, z2);
            void Door(string id, string from, string to, int x, int z, int width) => d.Connections.Add(new CourseConnection {
                Id = id, FromModuleId = from, ToModuleId = to, Kind = "Door", WidthMm = width, From = P(x, z), To = P(x, z) });
            void Stairs(string id, string from, int x1, int z1, int x2, int z2) => d.Connections.Add(new CourseConnection {
                Id = id, FromModuleId = from, ToModuleId = "Mezzanine", Kind = "Stairs", WidthMm = 2000,
                From = P(x1, z1), To = new CoursePoint(x2, MezzanineHeightMm, z2) });
            void Recovery(string id, string module, int x, int z) => d.RecoveryAnchors.Add(new CourseRecovery {
                Id = id, ModuleId = module, Position = At(module, x, z), RadiusMm = 800 });
            void Supply(string id, string module, int x, int z, string word) => d.Supplies.Add(new CourseSupply {
                Id = id, ModuleId = module, Position = At(module, x, z), Letters = word, Renewable = true, Protected = true });
            void Puzzle(string id, string module, string recipe, string supply, string prerequisite = "") => d.Puzzles.Add(new CoursePuzzle {
                Id = id, ModuleId = module, PresetId = "PlaceObject", RecipeId = recipe, RequiredLetters = recipe,
                ConsumeLetters = true, RecoverySupplyIds = new List<string> { supply },
                PrerequisiteIds = prerequisite == "" ? new List<string>() : new List<string> { prerequisite } });
            void Checkpoint(string id, string module, string safe, string entry, string puzzle) => d.Checkpoints.Add(new CourseCheckpoint {
                Id = id, ModuleId = module, RecoveryAnchorId = safe, EntryConnectionId = entry,
                PuzzleIds = puzzle == "" ? new List<string>() : new List<string> { puzzle } });
            void Section(string id, string from, string to, params (string Id, string[] Connections, string[] Checkpoints)[] routes) =>
                d.RouteSections.Add(new CourseRouteSection { Id = id, EntryModuleId = from, ExitModuleId = to,
                    Alternatives = routes.Select(route => new CourseAlternative { Id = route.Id,
                        ConnectionIds = route.Connections.ToList(), CheckpointIds = route.Checkpoints.ToList() }).ToList() });
            void Site(int lap, string puzzle, string checkpoint, string supply)
            {
                var at = SitePosition(puzzle);
                d.PhysicalSites.Add(new CoursePhysicalSite { Lap = lap, PuzzleId = puzzle, CheckpointId = checkpoint,
                    RecoverySupplyId = supply, Position = at, Target = at, SocketRadiusMm = 500, YawTolerance = 15, HoldTicks = 30 });
            }
            CoursePoint SitePosition(string puzzle) => puzzle switch
            {
                "DockBed" => At("Dock", -12800, 2600),
                "SortingBench" => At("Hall", 12500, 2000),
                "OfficeSofa" => At("Mezzanine", 12000, 18600),
                "DispatchStand" => At("Garage", 32000, 2000),
                "LockerStep" => At("Lockers", -2500, 18500),
                "ReturnClock" => At("Counter", -7600, 18500),
                _ => throw new ArgumentOutOfRangeException(nameof(puzzle))
            };
            void Grid(string prefix, string module, string assetId, int x, int z, int stepX, int stepZ, int columns, int rows, int yaw)
            {
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                        Prop(prefix + (char)('A' + row * columns + column), module, assetId, x + column * stepX, z + row * stepZ, yaw);
            }
            void Prop(string id, string module, string assetId, int x, int z, int yaw)
            {
                var asset = Geometry.Assets[assetId]; bool sideways = yaw % 180 != 0;
                int hx = ((sideways ? asset.DepthMm : asset.WidthMm) + 1) / 2 + 250;
                int hz = ((sideways ? asset.WidthMm : asset.DepthMm) + 1) / 2 + 250;
                string layer = d.Modules.Single(m => m.Id == module).LayerId;
                d.Zones.Add(new CourseZone { Id = id, ModuleId = module, Purpose = "Props", Placement = "Fixed",
                    FixedPosition = At(module, x, z), FixedYaw = yaw, Bounds = new CourseBox(At(module, x - hx, z - hz), At(module, x + hx, z + hz)),
                    MinCount = 1, MaxCount = 1, SpacingMm = 250, Yaws = new List<int> { yaw }, LayerIds = new List<string> { layer },
                    Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = assetId } } });
            }
        }
        static CoursePoint P(int x, int z) => new CoursePoint(x, 0, z);
        static string Name(int x) => x < 0 ? "W" + -x : "E" + x;
    }
}
