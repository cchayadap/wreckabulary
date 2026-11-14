using System.Collections.Generic;

namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseTemplates
    {
        public static CourseCatalogue ProofCatalogue() => new CourseCatalogue(
            new[] { new CourseModuleDefinition("Room", 20000, 20000, 4000) },
            new[] {
                new CourseAssetDefinition("BED", "BED", 2000, 1000, 1000),
                new CourseAssetDefinition("FAN", "FAN", 1000, 1000, 1500),
                new CourseAssetDefinition("STOOL", "STOOL", 600, 600, 800),
                new CourseAssetDefinition("PLATE", "PLATE", 400, 400, 100)
            },
            new[] { "CollectLetters", "PlaceObject", "BuildRoute", "RedirectWind", "BalanceWeight", "TimedMechanism" },
            new[] { "Cozy" }, new[] { "FindAddress", "WrongDelivery", "RestoreHome" });

        public static CourseDocument RaceCircuit()
        {
            var d = Base("race-proof", "Lost Parcel Circuit", "TimeRun", "TeamRace", "BrawlRace");
            d.Layers.Add(new CourseLayer { Id = "Loft", HeightMm = 6000 });
            d.Modules.Add(new CourseModule { Id = "Upper", DefinitionId = "Room", LayerId = "Loft", Position = P(0, 6000, 0) });
            d.Modules.Add(new CourseModule { Id = "Side", DefinitionId = "Room", LayerId = "Loft", Position = P(20000, 6000, 0) });
            d.Connections.Add(Stairs("UpA", "Upper", -5000, 5000, -3000, "BedBridge"));
            d.Connections.Add(Stairs("UpB", "Upper", -5000, 5000, 3000, "BedBridge"));
            d.Connections.Add(Stairs("Return", "Side", 5000, 15000, 0, "FanDoor"));
            d.Connections.Add(new CourseConnection {
                Id = "Door", FromModuleId = "Upper", ToModuleId = "Side", Kind = "Door",
                From = P(10000, 6000, -6000), To = P(10000, 6000, -6000), GatePuzzleId = "FanDoor"
            });
            d.Supplies.Add(new CourseSupply { Id = "BedSupply", ModuleId = "Start", Position = P(0, 0, -5000), Letters = "BED" });
            d.Supplies.Add(new CourseSupply { Id = "FanSupply", ModuleId = "Upper", Position = P(0, 6000, -6000), Letters = "FAN" });
            d.Puzzles.Add(new CoursePuzzle {
                Id = "BedBridge", ModuleId = "Start", PresetId = "BuildRoute", RecipeId = "BED",
                RequiredLetters = "BED", RecoverySupplyIds = S("BedSupply")
            });
            d.Puzzles.Add(new CoursePuzzle {
                Id = "FanDoor", ModuleId = "Upper", PresetId = "RedirectWind", RecipeId = "FAN",
                RequiredLetters = "FAN", PrerequisiteIds = S("BedBridge"), RecoverySupplyIds = S("FanSupply")
            });
            d.RecoveryAnchors.Add(new CourseRecovery { Id = "UpperSafe", ModuleId = "Upper", Position = P(0, 6000, -8000) });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Begin", ModuleId = "Start", RecoveryAnchorId = "StartSafe", PuzzleIds = S("BedBridge") });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "UpperA", ModuleId = "Upper", EntryConnectionId = "UpA", RecoveryAnchorId = "UpperSafe", PuzzleIds = S("FanDoor") });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "UpperB", ModuleId = "Upper", EntryConnectionId = "UpB", RecoveryAnchorId = "UpperSafe", PuzzleIds = S("FanDoor") });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Finish", ModuleId = "Start", EntryConnectionId = "Return", RecoveryAnchorId = "StartSafe" });
            d.RouteSections.Add(new CourseRouteSection {
                Id = "Circuit", EntryModuleId = "Start", ExitModuleId = "Start",
                Alternatives = new List<CourseAlternative> {
                    new CourseAlternative { Id = "North", ConnectionIds = S("UpA", "Door", "Return"), CheckpointIds = S("Begin", "UpperA", "Finish") },
                    new CourseAlternative { Id = "South", ConnectionIds = S("UpB", "Door", "Return"), CheckpointIds = S("Begin", "UpperB", "Finish") }
                }
            });
            string[] stories = { "FindAddress", "WrongDelivery", "RestoreHome" };
            for (int lap = 1; lap <= 3; lap++)
                d.Laps.Add(new CourseLap { Number = lap, StartModuleId = "Start", FinishModuleId = "Start", NarrativeId = stories[lap - 1], SectionIds = S("Circuit") });
            d.Zones.Add(Zone("SideResources", "Side", "Loft", "Resources", 21000, 6000, 4000, 29000, 9000));
            d.Zones.Add(Zone("SideProps", "Side", "Loft", "Props", 21000, 6000, -9000, 29000, -4000));
            return d;
        }

        public static CourseDocument CombatArena()
        {
            var d = Base("arena-proof", "Toybox Arena", "FreeForAll");
            const string kit = "ABCCDEFHIKLLMMNOOPRST";
            for (int i = 0; i < 8; i++)
            {
                d.Spawns.Add(new CourseSpawn { Id = "Respawn" + i, ModuleId = "Start", Kind = "Respawn", Position = P(-7000 + i * 2000, 0, 8000) });
                for (int letter = 0; letter < kit.Length; letter++)
                    d.Supplies.Add(new CourseSupply {
                        Id = "ArenaKit" + i + "Tile" + letter, ModuleId = "Start", Letters = kit[letter].ToString(),
                        Position = P(-6000 + i % 4 * 4000 + (letter % 7 - 3) * 500, 0,
                            (i < 4 ? -6000 : 6000) + (letter / 7 - 1) * 500),
                        Renewable = true, Protected = false
                    });
            }
            d.Zones.Add(Zone("ArenaProps", "Start", "Ground", "Props", -8000, 0, -3000, -2000, 4000));
            d.Zones.Add(Zone("ArenaResources", "Start", "Ground", "Resources", 2000, 0, -3000, 8000, 4000));
            return d;
        }

        public static CourseDocument FreestyleWorld()
        {
            var d = Base("freestyle-proof", "Parcel Playroom", "Freestyle");
            d.Modules.Add(new CourseModule { Id = "Garden", DefinitionId = "Room", LayerId = "Ground", Position = P(20000, 0, 0) });
            d.Connections.Add(new CourseConnection {
                Id = "GardenDoor", FromModuleId = "Start", ToModuleId = "Garden", Kind = "Walk",
                From = P(10000, 0, 0), To = P(10000, 0, 0)
            });
            d.Zones.Add(Zone("GardenProps", "Garden", "Ground", "Props", 14000, 0, -7000, 27000, 7000));
            d.Zones.Add(new CourseZone {
                Id = "FixedStool", ModuleId = "Start", Purpose = "Props", Placement = "Fixed",
                FixedPosition = P(-5000, 0, 3000), FixedYaw = 90, Yaws = new List<int> { 90 },
                Bounds = new CourseBox(P(-6000, 0, 2000), P(-4000, 0, 4000)),
                MinCount = 1, MaxCount = 1, LayerIds = S("Ground"),
                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "STOOL" } }
            });
            return d;
        }

        static CourseDocument Base(string id, string title, params string[] modes)
        {
            var d = new CourseDocument { Id = id, Title = title, SupportedModes = S(modes) };
            d.Layers.Add(new CourseLayer { Id = "Ground" });
            d.Modules.Add(new CourseModule { Id = "Start", DefinitionId = "Room", LayerId = "Ground" });
            for (int i = 0; i < 8; i++)
                d.Spawns.Add(new CourseSpawn { Id = "Start" + i, ModuleId = "Start", Position = P(-7000 + i * 2000, 0, -8000) });
            d.RecoveryAnchors.Add(new CourseRecovery { Id = "StartSafe", ModuleId = "Start", Position = P(0, 0, -6500), RadiusMm = 500 });
            return d;
        }
        static CourseZone Zone(string id, string module, string layer, string purpose, int x, int y, int z, int maxX, int maxZ) =>
            new CourseZone {
                Id = id, ModuleId = module, Purpose = purpose, Bounds = new CourseBox(P(x, y, z), P(maxX, y, maxZ)),
                MinCount = 1, MaxCount = 3, LayerIds = S(layer),
                Pool = new List<CourseWeightedAsset> {
                    new CourseWeightedAsset { AssetId = "STOOL", Weight = 2 },
                    new CourseWeightedAsset { AssetId = "PLATE", Weight = 1 }
                }
            };
        static CourseConnection Stairs(string id, string upper, int fromX, int toX, int z, string gate) =>
            new CourseConnection {
                Id = id, FromModuleId = "Start", ToModuleId = upper, Kind = "Stairs", GatePuzzleId = gate,
                From = P(fromX, 0, z), To = P(toX, 6000, z)
            };
        static CoursePoint P(int x, int y, int z) => new CoursePoint(x, y, z);
        static List<string> S(params string[] values) => new List<string>(values);
    }
}
