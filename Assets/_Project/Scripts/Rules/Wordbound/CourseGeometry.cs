using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static partial class CourseValidator
    {
        static void ValidateGeometry(CourseDocument d, CourseCatalogue catalogue, CoursePlayRequirements play, CourseValidation v)
        {
            var layers = d.Layers.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var modules = d.Modules.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var puzzles = d.Puzzles.ToDictionary(x => x.Id, StringComparer.Ordinal);
            if (layers.Count == 0 || modules.Count == 0) v.Add("geometry.empty", "$.modules", d.Id, "Add a layer and at least one module.");
            foreach (var layer in d.Layers)
                if (Math.Abs((long)layer.HeightMm) > CourseLimits.VerticalMm ||
                    d.Layers.Any(other => other != layer && other.HeightMm == layer.HeightMm))
                    v.Add("layer.height", "$.layers", layer.Id, "Layer heights must be bounded and distinct.");
            foreach (var m in d.Modules)
            {
                if (!layers.ContainsKey(m.LayerId) || !catalogue.Modules.ContainsKey(m.DefinitionId))
                { v.Add("module.reference", "$.modules", m.Id, "Choose an existing layer and approved module definition."); continue; }
                if (!ValidPoint(m.Position) || !Yaw(m.Yaw) || m.Position.Y != layers[m.LayerId].HeightMm ||
                    !ValidBox(catalogue.Bounds(m))) v.Add("module.transform", "$.modules", m.Id, "Invalid bounded module transform or layer height.");
            }
            if (!v.Ok) return;
            for (int i = 0; i < d.Modules.Count; i++)
                for (int j = i + 1; j < d.Modules.Count; j++)
                    if (catalogue.Bounds(d.Modules[i]).Intersects(catalogue.Bounds(d.Modules[j])))
                        v.Add("module.overlap", "$.modules", d.Modules[i].Id, "Module volume overlaps " + d.Modules[j].Id + ".");
            foreach (var c in d.Connections)
            {
                if (!modules.ContainsKey(c.FromModuleId) || !modules.ContainsKey(c.ToModuleId) || c.FromModuleId == c.ToModuleId)
                { v.Add("connection.reference", "$.connections", c.Id, "Connect two distinct existing modules."); continue; }
                if (!string.IsNullOrEmpty(c.GatePuzzleId) && !puzzles.ContainsKey(c.GatePuzzleId))
                    v.Add("gate.unknown", "$.connections", c.Id, "Gate puzzle does not exist.");
                var from = modules[c.FromModuleId]; var to = modules[c.ToModuleId];
                var a = catalogue.Bounds(from); var b = catalogue.Bounds(to);
                if (!new[] { "Walk", "Door", "Stairs", "Ramp", "Bridge", "Traversal" }.Contains(c.Kind) ||
                    c.WidthMm < 900 || c.WidthMm > 10000 || !ValidPoint(c.From) || !ValidPoint(c.To) ||
                    !a.Contains(c.From) || !b.Contains(c.To) || c.From.Y != from.Position.Y || c.To.Y != to.Position.Y)
                { v.Add("connection.geometry", "$.connections", c.Id, "Invalid connection kind, width or floor endpoints."); continue; }
                long dx = (long)c.To.X - c.From.X, dz = (long)c.To.Z - c.From.Z, rise = (long)c.To.Y - c.From.Y;
                long run = Math.Abs(dx) + Math.Abs(dz);
                if (c.Kind == "Walk" || c.Kind == "Door")
                {
                    bool boundaryA = c.From.X == a.Min.X || c.From.X == a.Max.X || c.From.Z == a.Min.Z || c.From.Z == a.Max.Z;
                    bool boundaryB = c.To.X == b.Min.X || c.To.X == b.Max.X || c.To.Z == b.Min.Z || c.To.Z == b.Max.Z;
                    if (!c.From.Equals(c.To) || !boundaryA || !boundaryB)
                        v.Add("door.wall", "$.connections", c.Id, "Walking connections join the same point on two shared floor-level walls.");
                    int half = (c.WidthMm + 1) / 2;
                    bool onXWall = (a.Max.X == b.Min.X && c.From.X == a.Max.X || a.Min.X == b.Max.X && c.From.X == a.Min.X) &&
                        c.From.Z >= Math.Max(a.Min.Z, b.Min.Z) + half && c.From.Z <= Math.Min(a.Max.Z, b.Max.Z) - half;
                    bool onZWall = (a.Max.Z == b.Min.Z && c.From.Z == a.Max.Z || a.Min.Z == b.Max.Z && c.From.Z == a.Min.Z) &&
                        c.From.X >= Math.Max(a.Min.X, b.Min.X) + half && c.From.X <= Math.Min(a.Max.X, b.Max.X) - half;
                    if (!onXWall && !onZWall)
                        v.Add("door.width", "$.connections", c.Id, "Keep the full doorway width inside the shared wall.");
                }
                if (c.Kind == "Stairs" || c.Kind == "Ramp")
                {
                    if (rise <= 0 || run == 0 || (dx != 0 && dz != 0) || rise * 1000000L > run * 700207L)
                        v.Add("stairs.slope", "$.connections", c.Id, "Stairs and ramps ascend on one axis at no more than 35 degrees.");
                    int sx = Math.Sign(dx), sz = Math.Sign(dz);
                    var approach = new CoursePoint(c.From.X - sx * 700, c.From.Y, c.From.Z - sz * 700);
                    var landing = new CoursePoint(c.To.X + sx * 700, c.To.Y, c.To.Z + sz * 700);
                    if (!a.Contains(approach, c.WidthMm / 2) || !b.Contains(landing, c.WidthMm / 2))
                        v.Add("stairs.landing", "$.connections", c.Id, "Keep a clear 0.7 m step-off and full stair width at both ends.");
                    foreach (var m in d.Modules.Where(m => m.Position.Y > c.From.Y && m.Position.Y < c.To.Y))
                    {
                        var other = catalogue.Bounds(m);
                        if (Math.Min(c.From.X, c.To.X) - c.WidthMm / 2 < other.Max.X && Math.Max(c.From.X, c.To.X) + c.WidthMm / 2 > other.Min.X &&
                            Math.Min(c.From.Z, c.To.Z) - c.WidthMm / 2 < other.Max.Z && Math.Max(c.From.Z, c.To.Z) + c.WidthMm / 2 > other.Min.Z)
                            v.Add("stairs.floor", "$.connections", c.Id, "Vertical connection intersects an intermediate floor.");
                    }
                }
                if (c.Kind == "Bridge" && (rise != 0 || run == 0 || run > 100000 || (dx != 0 && dz != 0)))
                    v.Add("bridge.geometry", "$.connections", c.Id, "Bridges are level, axis-aligned spans up to 100 m.");
                if (c.Kind == "Traversal" && (string.IsNullOrEmpty(c.GatePuzzleId) || run > 30000 || Math.Abs(rise) > 15000))
                    v.Add("traversal.preset", "$.connections", c.Id, "Traversal needs a puzzle gate and bounded span; physics acceptance is separate.");
            }
            foreach (var puzzle in d.Puzzles)
            {
                if (!modules.ContainsKey(puzzle.ModuleId) || !catalogue.PuzzlePresets.Contains(puzzle.PresetId))
                    v.Add("puzzle.reference", "$.puzzles", puzzle.Id, "Choose an existing module and approved puzzle preset.");
                if (!string.IsNullOrEmpty(puzzle.RecipeId) &&
                    (!catalogue.Assets.TryGetValue(puzzle.RecipeId, out var recipe) || !recipe.CanCraft ||
                     !LetterBag.FromWord(recipe.Letters).Equals(LetterBag.FromWord(puzzle.RequiredLetters))))
                    v.Add("recipe.invalid", "$.puzzles", puzzle.Id, "Recipe requirements must match an approved craftable object's letters.");
                foreach (string id in puzzle.PrerequisiteIds)
                    if (!puzzles.ContainsKey(id) || id == puzzle.Id) v.Add("puzzle.prerequisite", "$.puzzles", puzzle.Id, "Unknown or self prerequisite.");
                foreach (string id in puzzle.RecoverySupplyIds)
                    if (!d.Supplies.Any(s => s.Id == id)) v.Add("recovery.supply", "$.puzzles", puzzle.Id, "Recovery supply does not exist.");
            }
            foreach (var supply in d.Supplies)
            {
                ValidateLocation(supply.Id, supply.ModuleId, supply.Position, 250, "supplies", modules, catalogue, v);
                foreach (string id in supply.AfterPuzzleIds)
                    if (!puzzles.ContainsKey(id)) v.Add("supply.prerequisite", "$.supplies", supply.Id, "Unknown puzzle prerequisite.");
            }
            foreach (var spawn in d.Spawns)
            {
                ValidateLocation(spawn.Id, spawn.ModuleId, spawn.Position, 500, "spawns", modules, catalogue, v);
                if (!new[] { "Start", "Respawn" }.Contains(spawn.Kind) || !Yaw(spawn.Yaw))
                    v.Add("spawn.invalid", "$.spawns", spawn.Id, "Choose Start or Respawn and a quarter-turn rotation.");
            }
            for (int i = 0; i < d.Spawns.Count; i++)
                for (int j = i + 1; j < d.Spawns.Count; j++)
                    if (d.Spawns[i].Kind == d.Spawns[j].Kind && Math.Abs(d.Spawns[i].Position.Y - d.Spawns[j].Position.Y) < 2000 &&
                        DistanceSquared(d.Spawns[i].Position, d.Spawns[j].Position) < 1000000L)
                        v.Add("spawn.overlap", "$.spawns", d.Spawns[i].Id, "Provide distinct spawn space for every occupant.");
            if (d.Spawns.Count(s => s.Kind == "Start") < play.MaxOccupants ||
                (play.Capability == CourseCapability.Arena && d.Spawns.Count(s => s.Kind == "Respawn") < play.MaxOccupants))
                v.Add("spawn.capacity", "$.spawns", d.Id, "Provide enough starts and arena respawns for the selected capacity.");
            foreach (var r in d.RecoveryAnchors)
            {
                if (r.RadiusMm < 500 || r.RadiusMm > 5000) v.Add("recovery.radius", "$.recoveryAnchors", r.Id, "Recovery radius must be 0.5–5 m.");
                ValidateLocation(r.Id, r.ModuleId, r.Position, Math.Max(0, r.RadiusMm), "recoveryAnchors", modules, catalogue, v);
                foreach (string id in r.AfterPuzzleIds)
                    if (!puzzles.ContainsKey(id)) v.Add("recovery.prerequisite", "$.recoveryAnchors", r.Id, "Unknown puzzle prerequisite.");
            }
            foreach (var c in d.Checkpoints)
            {
                if (!modules.ContainsKey(c.ModuleId) || !d.RecoveryAnchors.Any(r => r.Id == c.RecoveryAnchorId) ||
                    (!string.IsNullOrEmpty(c.EntryConnectionId) && !d.Connections.Any(x => x.Id == c.EntryConnectionId)))
                    v.Add("checkpoint.reference", "$.checkpoints", c.Id, "Unknown module, recovery anchor or entry connection.");
                foreach (string id in c.PuzzleIds)
                    if (!puzzles.ContainsKey(id)) v.Add("checkpoint.puzzle", "$.checkpoints", c.Id, "Unknown puzzle ID.");
            }
            foreach (var z in d.Zones)
            {
                if (!modules.TryGetValue(z.ModuleId, out var m)) { v.Add("zone.module", "$.zones", z.Id, "Unknown module."); continue; }
                var bounds = catalogue.Bounds(m);
                if (!ValidBox(z.Bounds) || !bounds.Contains(z.Bounds.Min) || !bounds.Contains(z.Bounds.Max) ||
                    !z.LayerIds.Contains(m.LayerId) || z.LayerIds.Any(id => !layers.ContainsKey(id)))
                    v.Add("zone.volume", "$.zones", z.Id, "Keep the zone inside its module and permitted layers.");
                if (z.Pool.Any(p => !catalogue.Assets.ContainsKey(p.AssetId))) v.Add("asset.unknown", "$.zones", z.Id, "Pool contains an unapproved asset ID.");
            }
            if (d.Zones.Sum(z => (long)z.MaxCount) * Math.Max(1, play.TeamCount) + d.Supplies.Count * Math.Max(1, play.TeamCount) > CourseLimits.Entities)
                v.Add("entities.limit", "$.zones", d.Id, "Maximum resolved entity count exceeds the bound.");
            foreach (var reserved in d.ReservedVolumes)
                if (!ValidBox(reserved.Bounds) || reserved.Bounds.Min.Y == reserved.Bounds.Max.Y ||
                    !new[] { "Path", "Spawn", "Checkpoint", "Landing", "Recovery" }.Contains(reserved.Reason))
                    v.Add("reserved.invalid", "$.reservedVolumes", reserved.Id, "Use a bounded volume and approved reserve reason.");
            if (!v.Ok) return;
            var allOpen = new HashSet<string>(puzzles.Keys, StringComparer.Ordinal);
            string start = d.Spawns.FirstOrDefault(s => s.Kind == "Start")?.ModuleId;
            if (start == null || Reachable(d, start, allOpen).Count != modules.Count)
                v.Add("geometry.disconnected", "$.connections", d.Id, "All modules must be reachable from a start when gates are open.");
            if (play.Capability != CourseCapability.Circuit && d.Spawns.Select(s => s.ModuleId).Distinct().Any(
                id => Reachable(d, id, new HashSet<string>(StringComparer.Ordinal)).Count != modules.Count))
                v.Add("geometry.spawnTrap", "$.connections", d.Id, "Arena and exploration spawns need access to all modules without required race gates.");
        }

        static void ValidateLocation(string id, string moduleId, CoursePoint position, int margin, string path,
            Dictionary<string, CourseModule> modules, CourseCatalogue catalogue, CourseValidation v)
        {
            if (!modules.TryGetValue(moduleId, out var m) || !ValidPoint(position) ||
                !catalogue.Bounds(m).Contains(position, margin) || position.Y != m.Position.Y)
                v.Add("location.invalid", "$." + path, id, "Place this on its module's floor with safe clearance.");
        }
        internal static long DistanceSquared(CoursePoint a, CoursePoint b)
        { long dx = (long)a.X - b.X, dz = (long)a.Z - b.Z; return dx * dx + dz * dz; }
        internal static HashSet<string> Reachable(CourseDocument d, string start, HashSet<string> completed)
        {
            var adjacency = d.Modules.ToDictionary(m => m.Id, m => new List<string>(), StringComparer.Ordinal);
            foreach (var edge in d.Connections)
            {
                if (!string.IsNullOrEmpty(edge.GatePuzzleId) && !completed.Contains(edge.GatePuzzleId)) continue;
                adjacency[edge.FromModuleId].Add(edge.ToModuleId);
                if (edge.Bidirectional) adjacency[edge.ToModuleId].Add(edge.FromModuleId);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            var queue = new Queue<string>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in adjacency[current])
                    if (seen.Add(next)) queue.Enqueue(next);
            }
            return seen;
        }
    }
}
