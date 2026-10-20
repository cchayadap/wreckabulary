using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundPhysicalSite
    {
        public int Lap { get; }
        public string PuzzleId => Socket.PuzzleId;
        public string CheckpointId { get; }
        public string RecoverySupplyId { get; }
        public WordboundPlacementSocket Socket { get; }
        public CourseBox ArrivalBounds { get; }
        public CourseBox ReceiverStartBounds { get; }
        public IReadOnlyList<CourseBox> FloorPadBounds { get; }
        public string PropSourceZoneId { get; }
        public string WindReceiverZoneId { get; }
        public string WindReceiverRecipeId { get; }
        public string ReceiverRecoverySupplyId { get; }
        public int ActiveWindowTicks { get; }
        public string Hash { get; }
        public ResolvedCoursePhysicalSite Authored { get; }

        public WordboundPhysicalSite(ResolvedCoursePhysicalSite site) : this(
            (site ?? throw new ArgumentNullException(nameof(site))).Lap - 1, site.CheckpointId, site.RecoverySupplyId,
            new WordboundPlacementSocket(site.PuzzleId, WordboundPlatformPath.Metres(site.Position), site.Yaw,
                site.SocketRadiusMm, site.YawTolerance, site.HoldTicks),
            site.ArrivalBounds, site.ReceiverStartBounds, site.FloorPadBounds, site.PropSourceZoneId,
            site.WindReceiverZoneId, site.WindReceiverRecipeId, site.ReceiverRecoverySupplyId, site.ActiveWindowTicks)
        { Authored = site; Hash = site.Hash; }

        public WordboundPhysicalSite(int lap, string checkpointId, string recoverySupplyId, WordboundPlacementSocket socket,
            CourseBox arrivalBounds = default, CourseBox receiverStartBounds = default, IEnumerable<CourseBox> floorPadBounds = null,
            string propSourceZoneId = "", string windReceiverZoneId = "", string windReceiverRecipeId = "",
            string receiverRecoverySupplyId = "", int activeWindowTicks = 0)
        {
            var pads = (floorPadBounds ?? Array.Empty<CourseBox>()).Take(3).ToArray();
            if (lap < 0 || lap > 2 || !WordboundIdentity.IsId(checkpointId) || !WordboundIdentity.IsId(recoverySupplyId) ||
                socket == null || pads.Length > 2 || activeWindowTicks < 0 || activeWindowTicks > 600 ||
                new[] { propSourceZoneId, windReceiverZoneId, windReceiverRecipeId, receiverRecoverySupplyId }
                    .Any(id => id == null || id != "" && !WordboundIdentity.IsId(id)))
                throw new ArgumentException("Physical site identity, timing or sensor count is invalid.");
            Lap = lap; CheckpointId = checkpointId; RecoverySupplyId = recoverySupplyId; Socket = socket;
            ArrivalBounds = arrivalBounds; ReceiverStartBounds = receiverStartBounds; FloorPadBounds = Array.AsReadOnly(pads);
            PropSourceZoneId = propSourceZoneId; WindReceiverZoneId = windReceiverZoneId;
            WindReceiverRecipeId = windReceiverRecipeId; ReceiverRecoverySupplyId = receiverRecoverySupplyId;
            ActiveWindowTicks = activeWindowTicks;
            var fields = new List<string>();
            void Add(object value) => fields.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
            void Point(CoursePoint p) { Add(p.X); Add(p.Y); Add(p.Z); }
            void Box(CourseBox b) { Point(b.Min); Point(b.Max); }
            Add(Lap); Add(PuzzleId); Add(CheckpointId); Add(RecoverySupplyId);
            Add(BitConverter.SingleToInt32Bits(socket.Position.X)); Add(BitConverter.SingleToInt32Bits(socket.Position.Y));
            Add(BitConverter.SingleToInt32Bits(socket.Position.Z)); Add(socket.Yaw); Add(socket.RadiusMm);
            Add(socket.YawToleranceDegrees); Add(socket.HoldTicks);
            Box(arrivalBounds); Box(receiverStartBounds); Add(pads.Length); foreach (var pad in pads) Box(pad);
            Add(propSourceZoneId); Add(windReceiverZoneId); Add(windReceiverRecipeId); Add(receiverRecoverySupplyId); Add(activeWindowTicks);
            Hash = WordboundIdentity.Sha256("wordbound.physical-site|1|" +
                string.Join("", fields.Select(value => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value)));
        }

        internal void Validate(ResolvedCourse course)
        {
            var puzzle = course.Puzzles.SingleOrDefault(p => p.Id == PuzzleId);
            if (puzzle == null || Lap >= course.Laps.Count ||
                !course.Laps[Lap].Checkpoints.Any(c => c.Id == CheckpointId && c.PuzzleIds.Contains(PuzzleId)) ||
                !puzzle.RecoverySupplyIds.Contains(RecoverySupplyId) ||
                !course.Supplies.Any(s => s.SourceId == RecoverySupplyId && s.ModuleId == puzzle.ModuleId && s.Renewable && s.Protected))
                throw new ArgumentException("Physical site is not bound to its selected lap, checkpoint and recovery supply.");
            var module = course.Geometry.Single(m => m.Id == puzzle.ModuleId);
            var at = new CoursePoint(Mm(Socket.Position.X), Mm(Socket.Position.Y), Mm(Socket.Position.Z));
            if (!module.Bounds.Contains(at, Socket.RadiusMm) || at.Y != module.Position.Y)
                throw new ArgumentException("A physical socket must lie on its authored module floor.");
            void Sensor(CourseBox box)
            {
                if (box.Min.X >= box.Max.X || box.Min.Y >= box.Max.Y || box.Min.Z >= box.Max.Z ||
                    !module.Bounds.Contains(box.Min) || !module.Bounds.Contains(box.Max))
                    throw new ArgumentException("Physical sensor volume must have positive size inside its authored module.");
            }
            bool Props(string zone, string recipe = "") => zone != "" && course.Placements.Any(p =>
                p.ZoneId == zone && p.ModuleId == puzzle.ModuleId && p.Purpose == "Props");
            switch (puzzle.PresetId)
            {
                case "CollectLetters":
                    if (!Props(PropSourceZoneId)) throw new ArgumentException("Smash puzzles require an actual authored breakable prop.");
                    break;
                case "PlaceObject": break;
                case "BuildRoute": Sensor(ArrivalBounds); break;
                case "RedirectWind":
                    Sensor(ArrivalBounds); Sensor(ReceiverStartBounds);
                    if (!Props(WindReceiverZoneId, WindReceiverRecipeId) || WindReceiverRecipeId == "" ||
                        !puzzle.RecoverySupplyIds.Contains(ReceiverRecoverySupplyId) ||
                        !course.Supplies.Any(s => s.SourceId == ReceiverRecoverySupplyId && s.ModuleId == puzzle.ModuleId &&
                            s.Letters == WindReceiverRecipeId && s.Renewable && s.Protected))
                        throw new ArgumentException("Wind puzzles require a physical receiver and protected replacement letters.");
                    break;
                case "BalanceWeight":
                    if (FloorPadBounds.Count != 2) throw new ArgumentException("Object switches require two explicit floor pads.");
                    foreach (var pad in FloorPadBounds) Sensor(pad);
                    break;
                case "TimedMechanism":
                    Sensor(ArrivalBounds);
                    if (ActiveWindowTicks < 1) throw new ArgumentException("Timed transport requires a bounded crossing window.");
                    break;
                default: throw new NotSupportedException("Physical puzzle family is unsupported: " + puzzle.PresetId);
            }
            if (puzzle.PresetId != "CollectLetters" && PropSourceZoneId != "" ||
                puzzle.PresetId != "RedirectWind" && (WindReceiverZoneId != "" || WindReceiverRecipeId != "" || ReceiverRecoverySupplyId != "") ||
                puzzle.PresetId != "BalanceWeight" && FloorPadBounds.Count != 0 ||
                puzzle.PresetId != "TimedMechanism" && ActiveWindowTicks != 0)
                throw new ArgumentException("Physical site carries unrelated family data.");
        }

        static int Mm(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000) throw new ArgumentException("Socket position exceeds world bounds.");
            return checked((int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero));
        }
    }
}
