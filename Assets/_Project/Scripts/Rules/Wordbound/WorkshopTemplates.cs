using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WorkshopTemplate { RaceCircuit, CombatArena, FreestyleWorld }

    public sealed class WorkshopTemplatePreset
    {
        public WorkshopSnapshot Snapshot { get; }
        public CourseCatalogue Catalogue { get; }
        public CoursePlayRequirements Play { get; }
        internal WorkshopTemplatePreset(CourseDocument course, CoursePlayRequirements play)
        {
            Snapshot = new WorkshopSnapshot(course); Catalogue = CourseTemplates.ProofCatalogue(); Play = play;
        }
    }
    public static class WorkshopTemplates
    {
        public static WorkshopTemplatePreset Create(WorkshopTemplate template)
        {
            switch (template)
            {
                case WorkshopTemplate.RaceCircuit:
                    return new WorkshopTemplatePreset(PlayableRaceCircuit(),
                        MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")));
                case WorkshopTemplate.CombatArena:
                    return new WorkshopTemplatePreset(CourseTemplates.CombatArena(),
                        new CoursePlayRequirements("FreeForAll", CourseCapability.Arena, 0, 1, 8, CourseOwnershipPolicy.Individual));
                case WorkshopTemplate.FreestyleWorld:
                    return new WorkshopTemplatePreset(CourseTemplates.FreestyleWorld(),
                        new CoursePlayRequirements("Freestyle", CourseCapability.Explore, 0, 0, 8, CourseOwnershipPolicy.Shared));
                default: throw new ArgumentOutOfRangeException(nameof(template));
            }
        }

        static CourseDocument PlayableRaceCircuit()
        {
            var course = new CourseDocument {
                Schema = 2, Id = "race-proof", Title = "Lost Parcel Circuit",
                SupportedModes = new List<string> { "TimeRun", "TeamRace", "BrawlRace" }
            };
            course.Layers.Add(new CourseLayer { Id = "Ground" });
            course.Modules.Add(new CourseModule { Id = "Start", DefinitionId = "Room", LayerId = "Ground" });
            for (int slot = 0; slot < 8; slot++)
                course.Spawns.Add(new CourseSpawn {
                    Id = "Start" + slot, ModuleId = "Start", Position = new CoursePoint(-7000 + slot * 2000, 0, -8000)
                });
            course.RecoveryAnchors.Add(new CourseRecovery {
                Id = "StartSafe", ModuleId = "Start", Position = new CoursePoint(-2000, 0, -1000), RadiusMm = 800
            });
            course.RecoveryAnchors.Add(new CourseRecovery {
                Id = "DeliverySafe", ModuleId = "Start", Position = new CoursePoint(2000, 0, -2000), RadiusMm = 800
            });
            course.Supplies.Add(new CourseSupply {
                Id = "StoolLetters", ModuleId = "Start", Position = new CoursePoint(-3000, 0, 1000),
                Letters = "STOOL", Protected = true, Renewable = true
            });
            course.Puzzles.Add(new CoursePuzzle {
                Id = "ParcelStand", ModuleId = "Start", PresetId = "PlaceObject", RecipeId = "STOOL",
                RequiredLetters = "STOOL", RecoverySupplyIds = new List<string> { "StoolLetters" }, ConsumeLetters = true
            });
            course.Checkpoints.Add(new CourseCheckpoint {
                Id = "StandReady", ModuleId = "Start", RecoveryAnchorId = "StartSafe",
                PuzzleIds = new List<string> { "ParcelStand" }
            });
            course.Checkpoints.Add(new CourseCheckpoint {
                Id = "Delivery", ModuleId = "Start", RecoveryAnchorId = "DeliverySafe"
            });
            course.RouteSections.Add(new CourseRouteSection {
                Id = "Circuit", EntryModuleId = "Start", ExitModuleId = "Start",
                Alternatives = new List<CourseAlternative> { new CourseAlternative {
                    Id = "GroundRoute", CheckpointIds = new List<string> { "StandReady", "Delivery" }
                } }
            });
            string[] chapters = { "FindAddress", "WrongDelivery", "RestoreHome" };
            for (int lap = 1; lap <= 3; lap++)
            {
                course.Laps.Add(new CourseLap {
                    Number = lap, StartModuleId = "Start", FinishModuleId = "Start", NarrativeId = chapters[lap - 1],
                    SectionIds = new List<string> { "Circuit" }
                });
                course.PhysicalSites.Add(new CoursePhysicalSite {
                    Lap = lap, PuzzleId = "ParcelStand", CheckpointId = "StandReady", RecoverySupplyId = "StoolLetters",
                    Position = new CoursePoint(0, 0, 2000), Target = new CoursePoint(0, 0, 2000),
                    SocketRadiusMm = 500, YawTolerance = 15, HoldTicks = 30
                });
            }
            return course;
        }
    }
}
