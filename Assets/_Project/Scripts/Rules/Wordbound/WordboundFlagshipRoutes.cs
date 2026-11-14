using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundFlagshipRouteMeasure
    {
        public int Lap { get; }
        public double AuthoredMetres { get; }
        public double ShortestOrderedCheckpointMetres { get; }
        public int RequiredRoomVisits { get; }
        public int VerticalConnections { get; }
        internal WordboundFlagshipRouteMeasure(int lap, double authored, double shortest, int visits, int vertical)
        { Lap = lap; AuthoredMetres = authored; ShortestOrderedCheckpointMetres = shortest; RequiredRoomVisits = visits; VerticalConnections = vertical; }
    }

    public static class WordboundFlagshipRoutes
    {
        public static IReadOnlyList<WordboundFlagshipRouteMeasure> Measure(ResolvedCourse course)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            if (course.CopyDocument().Id != WordboundFlagshipContent.CourseId || course.Laps.Count != 3)
                throw new ArgumentException("Measure a complete resolved flagship circuit.");
            var modules = course.Geometry.ToDictionary(m => m.Id, StringComparer.Ordinal);
            var edges = course.Connections.ToDictionary(e => e.Id, StringComparer.Ordinal);
            var indices = modules.Keys.OrderBy(id => id, StringComparer.Ordinal).Select((id, i) => (id, i))
                .ToDictionary(pair => pair.id, pair => pair.i, StringComparer.Ordinal);
            var distances = new double[indices.Count, indices.Count];
            for (int i = 0; i < indices.Count; i++)
                for (int j = 0; j < indices.Count; j++) distances[i, j] = i == j ? 0 : double.PositiveInfinity;
            foreach (var edge in course.Connections)
            {
                double length = Distance(modules[edge.FromModuleId].Position, edge.From) + Distance(edge.From, edge.To) +
                    Distance(edge.To, modules[edge.ToModuleId].Position);
                int a = indices[edge.FromModuleId], b = indices[edge.ToModuleId];
                distances[a, b] = Math.Min(distances[a, b], length);
                if (edge.Bidirectional) distances[b, a] = Math.Min(distances[b, a], length);
            }
            for (int k = 0; k < indices.Count; k++)
                for (int i = 0; i < indices.Count; i++)
                    for (int j = 0; j < indices.Count; j++)
                        distances[i, j] = Math.Min(distances[i, j], distances[i, k] + distances[k, j]);
            var results = new List<WordboundFlagshipRouteMeasure>();
            foreach (var lap in course.Laps)
            {
                string current = lap.StartModuleId;
                var trail = new List<string> { current };
                double authored = 0; int vertical = 0;
                foreach (string id in lap.ConnectionIds)
                {
                    var edge = edges[id];
                    bool forward = edge.FromModuleId == current;
                    if (!forward && (!edge.Bidirectional || edge.ToModuleId != current))
                        throw new InvalidOperationException("The resolved flagship route is discontinuous.");
                    string next = forward ? edge.ToModuleId : edge.FromModuleId;
                    var from = forward ? edge.From : edge.To; var to = forward ? edge.To : edge.From;
                    authored += Distance(modules[current].Position, from) + Distance(from, to) + Distance(to, modules[next].Position);
                    if (from.Y != to.Y) vertical++;
                    trail.Add(next); current = next;
                }
                if (current != lap.FinishModuleId || lap.Checkpoints.Count != trail.Count ||
                    !lap.Checkpoints.Select(c => c.ModuleId).SequenceEqual(trail) ||
                    !lap.Checkpoints.Select(c => c.RouteStep).SequenceEqual(Enumerable.Range(0, trail.Count)))
                    throw new InvalidOperationException("Every flagship route room needs its own ordered checkpoint at the room centre.");
                foreach (var checkpoint in lap.Checkpoints)
                {
                    var anchor = course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId);
                    if (anchor.ModuleId != checkpoint.ModuleId || !anchor.Position.Equals(modules[checkpoint.ModuleId].Position))
                        throw new InvalidOperationException("Flagship distance measurements require the actual checkpoint anchors at room centres.");
                }
                double shortest = 0;
                for (int i = 1; i < trail.Count; i++) shortest += distances[indices[trail[i - 1]], indices[trail[i]]];
                if (authored < 600 || authored > 900 || shortest < 600 || shortest > 900 ||
                    vertical != 3 || trail.Distinct(StringComparer.Ordinal).Count() < 24)
                    throw new InvalidOperationException("The flagship must retain 600–900 m of required traversal, three height transitions and at least 24 room visits.");
                results.Add(new WordboundFlagshipRouteMeasure(lap.Number, authored, shortest, trail.Count, vertical));
            }
            return results.AsReadOnly();
        }

        static double Distance(CoursePoint a, CoursePoint b)
        {
            double x = (long)a.X - b.X, y = (long)a.Y - b.Y, z = (long)a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z) / 1000d;
        }
    }
}
