using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class WordboundFlagshipContent
    {
        static readonly string[] DestinationIds = { "Packing", "Kitchen", "Attic", "Rooftop" };
        static readonly string[] DestinationNames = { "Packing Yard", "Kitchen Works", "Attic Stacks", "Rooftop Garden" };
        static readonly string[] Landmarks = { "Giant brass parcel bell", "Turning teapot chimney", "Lit toy-house archive", "Broken address windmill" };
        static readonly string[][] RoomNames = {
            new[] { "Parcel Gate", "Label Bench", "Crate Press", "Ribbon Switch", "Conveyor Turn", "Spring Dock", "Address Desk", "Lift Landing" },
            new[] { "Service Landing", "Cutlery Sort", "Pantry Latch", "Mixer Gears", "Steam Corner", "Counter Step", "Recipe Balance", "Dumbwaiter" },
            new[] { "Trunk Landing", "Lost Labels", "Toy Socket", "Fan Archive", "Clock Corner", "Book Spring", "Scale Shelf", "Chimney Landing" },
            new[] { "Garden Gate", "Potting Sort", "Trellis Latch", "Windmill", "Rain Clock", "Planter Step", "Address Balance", "Rooftop Sorter" }
        };
        static readonly string[] Objectives = {
            "Spell HAMMER, smash the marked furniture, recover its letters, and spell TABLE.",
            "Spell the labelled object and place it into the matching address socket.",
            "Aim a crafted FAN and push the marked STOOL into the sorting receiver.",
            "Use CLOCK to slow the sorting arms and carry the keepsake through the active window.",
            "Deploy the spring furniture and reach the raised parcel marker.",
            "Place the crafted cover furniture across the linked floor pads; no second player is required."
        };
        static readonly string[][] Clues = {
            new[] { "The bell rings for an address that nobody wrote.", "The kitchen labels describe rooms upstairs.", "Old labels show this house used to fit together.", "The windmill repeats one missing address." },
            new[] { "The Movers follow the labels exactly, even when they are wrong.", "A sorter stamped the kitchen's parcels with attic labels.", "The archive shows a toy-house sorting diagram with a broken pointer.", "The windmill is sending the broken pointer's instructions back downstairs." },
            new[] { "Repair the labels in the workshop and send them home.", "Restore the recipe labels to their proper drawers.", "Return the old house diagram to its correct order.", "Set the final address and deliver the keepsake to the rooftop sorter." }
        };
        static readonly int[] RoomX = { -36000, -12000, 12000, 36000, 36000, 12000, -12000, -36000 };
        static readonly int[] RoomZ = { -12000, -12000, -12000, -12000, 12000, 12000, 12000, 12000 };
        static string RoomId(int destination, int room) => DestinationIds[destination] + "_" + RoomNames[destination][room].Replace(" ", "");
        static string CheckpointId(int lap, string module) => "L" + lap + "_" + module;

        CourseDocument Build(List<WordboundFlagshipDestination> destinations, List<WordboundFlagshipRoom> rooms,
            List<WordboundFlagshipPuzzleSite> sites, List<WordboundFlagshipStoryBeat> story)
        {
            var d = new CourseDocument { Id = CourseId, Title = "The Lost Address",
                SupportedModes = new List<string> { "TimeRun", "TeamRace", "BrawlRace", "Freestyle" } };
            for (int floor = 0; floor < 4; floor++)
            {
                d.Layers.Add(new CourseLayer { Id = DestinationIds[floor], HeightMm = floor * 8000 });
                var ids = new List<string>();
                for (int room = 0; room < 8; room++)
                {
                    string id = RoomId(floor, room); ids.Add(id);
                    AddRoom(d, rooms, id, floor, RoomNames[floor][room],
                        room == 0 ? "Arrival, orientation and safe return" : room == 7 ? "Destination handoff and delivery" : Objectives[room - 1],
                        new CoursePoint(RoomX[room], floor * 8000, RoomZ[room]));
                }
                if (floor == 0)
                {
                    AddRoom(d, rooms, "Packing_MislabelRoom", 0, "Mislabel Room", "Trace the misplaced address labels; gather repair materials.",
                        new CoursePoint(-12000, 0, -36000));
                    AddRoom(d, rooms, "Packing_RepairBay", 0, "Repair Bay", "Repair the address assembly before its final delivery.",
                        new CoursePoint(12000, 0, -36000));
                    ids.Add("Packing_MislabelRoom"); ids.Add("Packing_RepairBay");
                }
                destinations.Add(new WordboundFlagshipDestination(DestinationIds[floor], DestinationNames[floor], Landmarks[floor], floor * 8000, ids));
                for (int edge = 0; edge < 7; edge++)
                    Walk(d, DestinationIds[floor] + "_Walk" + edge, RoomId(floor, edge), RoomId(floor, edge + 1));
                Walk(d, DestinationIds[floor] + "_Return", RoomId(floor, 7), RoomId(floor, 0));
            }
            Walk(d, "Packing_BranchIn", RoomId(0, 1), "Packing_MislabelRoom");
            Walk(d, "Packing_BranchAcross", "Packing_MislabelRoom", "Packing_RepairBay");
            Walk(d, "Packing_BranchOut", "Packing_RepairBay", RoomId(0, 2));
            for (int floor = 0; floor < 3; floor++)
                d.Connections.Add(new CourseConnection {
                    Id = "Rise" + floor, FromModuleId = RoomId(floor, 7), ToModuleId = RoomId(floor + 1, 0),
                    Kind = "Stairs", WidthMm = 2500,
                    From = new CoursePoint(-36000, floor * 8000, 6000),
                    To = new CoursePoint(-36000, (floor + 1) * 8000, -6000)
                });
            for (int i = 0; i < 8; i++)
                d.Spawns.Add(new CourseSpawn {
                    Id = "Start" + i, ModuleId = RoomId(0, 0),
                    Position = new CoursePoint(-36000 - 6000 + (i % 4) * 4000, 0, -12000 + (i / 4 == 0 ? -6000 : 6000))
                });
            for (int lap = 1; lap <= 3; lap++) AddLap(d, lap, sites, story);
            return d;
        }

        void AddRoom(CourseDocument d, List<WordboundFlagshipRoom> rooms, string id, int floor,
            string name, string purpose, CoursePoint position)
        {
            var module = new CourseModule { Id = id, DefinitionId = "FlagshipRoom", LayerId = DestinationIds[floor], Position = position };
            d.Modules.Add(module); rooms.Add(new WordboundFlagshipRoom(id, DestinationIds[floor], name, purpose, position));
            d.RecoveryAnchors.Add(new CourseRecovery { Id = "Safe_" + id, ModuleId = id,
                Position = position, RadiusMm = 1200 });
            d.ReservedVolumes.Add(new CourseReservedVolume { Id = "PathX_" + id, Reason = "Path",
                Bounds = new CourseBox(Offset(position, -12000, 0, -1800), Offset(position, 12000, 2500, 1800)) });
            d.ReservedVolumes.Add(new CourseReservedVolume { Id = "PathZ_" + id, Reason = "Path",
                Bounds = new CourseBox(Offset(position, -1800, 0, -12000), Offset(position, 1800, 2500, 12000)) });
            if (id == RoomId(0, 0)) return;
            d.Zones.Add(new CourseZone {
                Id = "Props_" + id, ModuleId = id, Purpose = "Props", MinCount = 1, MaxCount = 1,
                Bounds = new CourseBox(Offset(position, 5000, 0, 5000), Offset(position, 10000, 0, 10000)),
                GridMm = 250, SpacingMm = 500, LayerIds = new List<string> { module.LayerId },
                Pool = new List<CourseWeightedAsset> {
                    new CourseWeightedAsset { AssetId = "TABLE", Weight = 3 },
                    new CourseWeightedAsset { AssetId = "SOFA", Weight = 2 },
                    new CourseWeightedAsset { AssetId = "STOOL", Weight = 3 },
                    new CourseWeightedAsset { AssetId = "PLATE", Weight = 1 }
                }
            });
            if (id.EndsWith("MislabelRoom", StringComparison.Ordinal) || id.EndsWith("RepairBay", StringComparison.Ordinal))
                d.Supplies.Add(new CourseSupply { Id = "Practice_" + id, ModuleId = id,
                    Position = Offset(position, -5000, 0, -5000), Letters = id.EndsWith("RepairBay", StringComparison.Ordinal) ? "HAMMER" : "BROOM" });
        }

        void AddLap(CourseDocument d, int lap, List<WordboundFlagshipPuzzleSite> sites, List<WordboundFlagshipStoryBeat> story)
        {
            bool reverse = lap == 2;
            var floors = reverse ? new[] { 3, 2, 1, 0 } : new[] { 0, 1, 2, 3 };
            string previousPuzzle = "";
            var courseLap = new CourseLap { Number = lap, NarrativeId = Narratives[lap - 1],
                StartModuleId = RoomId(floors[0], reverse ? 7 : 0), FinishModuleId = RoomId(floors[3], reverse ? 0 : 7) };
            for (int fi = 0; fi < floors.Length; fi++)
            {
                int floor = floors[fi];
                var order = reverse ? Enumerable.Range(0, 8).Reverse().ToArray() : Enumerable.Range(0, 8).ToArray();
                foreach (int room in order)
                {
                    string moduleId = RoomId(floor, room);
                    var checkpoint = new CourseCheckpoint { Id = CheckpointId(lap, moduleId), ModuleId = moduleId, RecoveryAnchorId = "Safe_" + moduleId };
                    d.Checkpoints.Add(checkpoint);
                    if (room >= 1 && room <= 6)
                    {
                        int family = room - 1;
                        string recipe = Recipe(lap, family), puzzleId = "Puzzle_" + checkpoint.Id, supplyId = "Letters_" + checkpoint.Id;
                        var module = d.Modules.Single(m => m.Id == moduleId);
                        var puzzle = new CoursePuzzle {
                            Id = puzzleId, ModuleId = moduleId, PresetId = Families[family], RecipeId = recipe,
                            RequiredLetters = recipe, MinimumPlayers = 1, RecoverySupplyIds = new List<string> { supplyId }
                        };
                        if (previousPuzzle.Length != 0) puzzle.PrerequisiteIds.Add(previousPuzzle);
                        d.Puzzles.Add(puzzle); checkpoint.PuzzleIds.Add(puzzleId);
                        d.Supplies.Add(new CourseSupply { Id = supplyId, ModuleId = moduleId, Letters = recipe,
                            Position = Offset(module.Position, -5000, 0, -2000 - lap * 2000),
                            AfterPuzzleIds = puzzle.PrerequisiteIds.ToList() });
                        string resourceZone = "", toolSupply = "";
                        var sitePosition = Offset(module.Position, lap == 1 ? 3000 : 8000, 0, lap == 3 ? -4000 : -9000);
                        if (family == 0)
                        {
                            resourceZone = "Marked_" + checkpoint.Id;
                            var at = Offset(module.Position, lap == 1 ? 5000 : 9000, 0, lap == 3 ? -5000 : -9000);
                            d.Zones.Add(new CourseZone {
                                Id = resourceZone, ModuleId = moduleId, Purpose = "Props", Placement = "Fixed",
                                FixedPosition = at, Bounds = new CourseBox(Offset(at, -1000, 0, -1000), Offset(at, 1000, 0, 1000)),
                                MinCount = 1, MaxCount = 1, Yaws = new List<int> { 0 }, LayerIds = new List<string> { module.LayerId },
                                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "TABLE" } }
                            });
                            toolSupply = "Tools_" + checkpoint.Id;
                            d.Supplies.Add(new CourseSupply { Id = toolSupply, ModuleId = moduleId, Letters = "HAMMER",
                                Position = Offset(module.Position, -8000, 0, -2000 - lap * 2000),
                                AfterPuzzleIds = puzzle.PrerequisiteIds.ToList() });
                            puzzle.RecoverySupplyIds.Add(toolSupply);
                        }
                        string receiverZone = "", receiverSupply = "";
                        if (family == 2)
                        {
                            receiverZone = "Receiver_" + checkpoint.Id;
                            receiverSupply = "ReceiverLetters_" + checkpoint.Id;
                            var at = Offset(sitePosition, 0, 0,
                                Math.Min(900, WordboundFlagshipEquipment.Mm(Equipment.Single(item => item.Id == recipe).Radius * .35f)));
                            d.Zones.Add(new CourseZone {
                                Id = receiverZone, ModuleId = moduleId, Purpose = "Props", Placement = "Fixed",
                                FixedPosition = at, Bounds = new CourseBox(Offset(at, -500, 0, -500), Offset(at, 500, 0, 500)),
                                MinCount = 1, MaxCount = 1, Yaws = new List<int> { 0 }, LayerIds = new List<string> { module.LayerId },
                                Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "STOOL" } }
                            });
                            d.Supplies.Add(new CourseSupply { Id = receiverSupply, ModuleId = moduleId, Letters = "STOOL",
                                Position = Offset(module.Position, -8000, 0, -2000 - lap * 2000),
                                AfterPuzzleIds = puzzle.PrerequisiteIds.ToList() });
                            puzzle.RecoverySupplyIds.Add(receiverSupply);
                        }
                        sites.Add(new WordboundFlagshipPuzzleSite(lap, puzzle, sitePosition,
                            Equipment.Single(item => item.Id == recipe), Objectives[family], checkpoint.Id, supplyId,
                            resourceZone, receiverZone, receiverSupply, toolSupply));
                        previousPuzzle = puzzleId;
                    }
                }
                string entry = RoomId(floor, order[0]), exit = RoomId(floor, order[7]);
                var section = new CourseRouteSection { Id = "L" + lap + "_" + DestinationIds[floor], EntryModuleId = entry, ExitModuleId = exit };
                var mainEdges = Enumerable.Range(0, 7).Select(i => DestinationIds[floor] + "_Walk" + i).ToList();
                var mainRooms = Enumerable.Range(0, 8).Select(i => RoomId(floor, i)).ToList();
                if (floor != 0 || lap != 3) section.Alternatives.Add(Alternative(section.Id + "_Main", lap, mainEdges, mainRooms, reverse));
                if (floor == 0 && lap != 1)
                {
                    var branchEdges = mainEdges.ToList(); branchEdges.RemoveAt(1);
                    branchEdges.InsertRange(1, new[] { "Packing_BranchIn", "Packing_BranchAcross", "Packing_BranchOut" });
                    var branchRooms = mainRooms.ToList(); branchRooms.InsertRange(2, new[] { "Packing_MislabelRoom", "Packing_RepairBay" });
                    foreach (string id in new[] { "Packing_MislabelRoom", "Packing_RepairBay" })
                        d.Checkpoints.Add(new CourseCheckpoint { Id = CheckpointId(lap, id), ModuleId = id, RecoveryAnchorId = "Safe_" + id });
                    section.Alternatives.Add(Alternative(section.Id + "_Repair", lap, branchEdges, branchRooms, reverse));
                    story.Add(new WordboundFlagshipStoryBeat(lap, CheckpointId(lap, "Packing_RepairBay"), Narratives[lap - 1],
                        lap == 2 ? "The misprinted labels came from the rooftop sorter." : "The repaired pointer can now carry the true address upstairs."));
                }
                d.RouteSections.Add(section); courseLap.SectionIds.Add(section.Id);
                story.Add(new WordboundFlagshipStoryBeat(lap, CheckpointId(lap, entry), Narratives[lap - 1], Clues[lap - 1][floor]));
                if (fi < 3)
                {
                    int low = Math.Min(floor, floors[fi + 1]);
                    string next = RoomId(floors[fi + 1], reverse ? 7 : 0);
                    var rise = new CourseRouteSection { Id = "L" + lap + "_Rise" + low, EntryModuleId = exit, ExitModuleId = next };
                    rise.Alternatives.Add(new CourseAlternative { Id = rise.Id + "_Stairs", ConnectionIds = new List<string> { "Rise" + low } });
                    d.RouteSections.Add(rise); courseLap.SectionIds.Add(rise.Id);
                }
            }
            d.Laps.Add(courseLap);
        }

        static CourseAlternative Alternative(string id, int lap, List<string> edges, List<string> rooms, bool reverse)
        {
            if (reverse) { edges = edges.AsEnumerable().Reverse().ToList(); rooms = rooms.AsEnumerable().Reverse().ToList(); }
            return new CourseAlternative { Id = id, ConnectionIds = edges, CheckpointIds = rooms.Select(room => CheckpointId(lap, room)).ToList() };
        }
        static string Recipe(int lap, int family) => family switch
        {
            0 => "TABLE", 1 => lap == 1 ? "TABLE" : "SOFA", 2 => "FAN", 3 => "CLOCK",
            4 => lap == 2 ? "STOOL" : "BED", 5 => lap == 2 ? "SOFA" : "TABLE",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        static CoursePoint Offset(CoursePoint p, int x, int y, int z) => new CoursePoint(p.X + x, p.Y + y, p.Z + z);
        static void Walk(CourseDocument d, string id, string fromId, string toId)
        {
            var a = d.Modules.Single(m => m.Id == fromId).Position;
            var b = d.Modules.Single(m => m.Id == toId).Position;
            var point = new CoursePoint((a.X + b.X) / 2, a.Y, (a.Z + b.Z) / 2);
            d.Connections.Add(new CourseConnection { Id = id, FromModuleId = fromId, ToModuleId = toId,
                Kind = "Door", From = point, To = point, WidthMm = 2500 });
        }
    }
}
