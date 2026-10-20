using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseResolver
    {
        public static CourseResolution Resolve(CourseDocument document, CourseCatalogue catalogue, CourseResolveRequest request)
        {
            var result = new CourseResolution();
            if (request == null || !CourseRandom.ValidSeed(request.Seed) || request.MaxAttempts < 1 || request.MaxAttempts > CourseLimits.Attempts)
            { result.Validation.Add("request.invalid", "$", "", "Provide a lowercase 64-bit hexadecimal seed and 1–16 attempts."); return result; }
            result.Validation.AddRange(CourseValidator.Validate(document, catalogue, request.Play).Issues);
            if (!result.Validation.Ok) return result;
            CourseDocument frozen;
            try { frozen = document.Clone(); }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            { result.Validation.Add("document.bounds", "$", document.Id, e.Message); return result; }
            var data = new CourseManifestData
            {
                Schema = frozen.Schema, CanonicalVersion = frozen.Schema,
                Seed = request.Seed, Document = frozen, SourceHash = CourseJson.Sha256(CourseJson.ExportDocument(frozen)),
                CatalogueHash = catalogue.Hash, Play = CoursePlayData.From(request.Play)
            };
            data.Supplies = ResolveSupplies(frozen, request.Play);
            data.Placements = Place(frozen, catalogue, request, result.Validation);
            if (!result.Validation.Ok) return result;
            CourseValidation last = null;
            for (int attempt = 0; attempt < request.MaxAttempts; attempt++)
            {
                result.AttemptsUsed = attempt + 1; data.Attempt = attempt;
                data.Laps = new List<CourseLapSelection>();
                var previous = new Dictionary<string, string>(StringComparer.Ordinal);
                if (request.Play.Capability == CourseCapability.Circuit)
                    foreach (var lap in frozen.Laps.OrderBy(x => x.Number))
                    {
                        var alternatives = new List<CourseAlternative>();
                        foreach (string id in lap.SectionIds)
                        {
                            var section = frozen.RouteSections.Single(s => s.Id == id);
                            var pool = section.Alternatives.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
                            if (pool.Count > 1 && previous.TryGetValue(id, out string prior)) pool.RemoveAll(a => a.Id == prior);
                            var random = CourseRandom.Stream(request.Seed, "Routes", id, lap.Number, attempt);
                            var chosen = Weighted(pool, a => a.Weight, ref random);
                            alternatives.Add(chosen); previous[id] = chosen.Id;
                        }
                        data.Laps.Add(CourseValidator.SelectLap(frozen, lap, alternatives));
                    }
                last = new CourseValidation();
                CourseValidator.ValidateSelectedProgression(data, last);
                if (!last.Ok) continue;
                data.Hash = ManifestHash(data);
                string encoded = CourseJson.Encode(CourseJson.Value(data));
                if (System.Text.Encoding.UTF8.GetByteCount(encoded) > CourseLimits.ManifestBytes)
                { result.Validation.Add("manifest.size", "$", "", "Resolved manifest exceeds its byte bound."); return result; }
                result.Manifest = new ResolvedCourse(data, catalogue);
                return result;
            }
            result.Validation.AddRange(last.Issues);
            result.Validation.Add("generation.exhausted", "$", document.Id, "No valid route combination within the attempt bound; constraints were not relaxed.");
            return result;
        }

        static T Weighted<T>(IList<T> values, Func<T, int> weight, ref Pcg32 random)
        {
            int roll = random.Range(0, values.Sum(weight));
            foreach (var value in values) { roll -= weight(value); if (roll < 0) return value; }
            throw new InvalidOperationException("Invalid weighted pool.");
        }
        internal static bool TeamScoped(CoursePlayRequirements play, string purpose) =>
            play.TeamCount > 0 && (play.Ownership == CourseOwnershipPolicy.TeamIsolated && purpose != "Decoration" ||
                                  play.Ownership == CourseOwnershipPolicy.TeamProtected && purpose == "Resources");
        internal static string EntityId(string prefix, string sourceId, int slot, int team) =>
            prefix + "_" + sourceId + "_" + slot.ToString("D4", CultureInfo.InvariantCulture) + (team < 0 ? "_shared" : "_t" + team.ToString(CultureInfo.InvariantCulture));
        internal static List<CourseSupplyData> ResolveSupplies(CourseDocument d, CoursePlayRequirements play)
        {
            var supplies = new List<CourseSupplyData>();
            foreach (var source in d.Supplies.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                bool scoped = play.TeamCount > 0;
                for (int team = 0; team < (scoped ? play.TeamCount : 1); team++)
                    supplies.Add(new CourseSupplyData
                    {
                        Id = EntityId("s", source.Id, 0, scoped ? team : -1), SourceId = source.Id,
                        ModuleId = source.ModuleId, Letters = source.Letters, Position = source.Position,
                        TeamIndex = scoped ? team : -1, Renewable = source.Renewable, Protected = source.Protected,
                        AfterPuzzleIds = source.AfterPuzzleIds.ToList()
                    });
            }
            return supplies;
        }
        static List<CoursePlacementData> Place(CourseDocument d, CourseCatalogue catalogue, CourseResolveRequest request, CourseValidation v)
        {
            var canonical = new List<CoursePlacementData>();
            foreach (var zone in d.Zones.OrderBy(z => z.Purpose == "Resources" ? 0 : z.Purpose == "Props" ? 1 : 2).ThenBy(z => z.Id, StringComparer.Ordinal))
            {
                var random = CourseRandom.Stream(request.Seed, zone.Purpose, zone.Id);
                int count = random.Range(0, 1000) < zone.ChancePermille ? random.Range(zone.MinCount, zone.MaxCount + 1) : 0;
                var pool = zone.Pool.OrderBy(p => p.AssetId, StringComparer.Ordinal).ToList();
                var yaws = zone.Yaws.OrderBy(x => x).ToList();
                for (int slot = 0; slot < count; slot++)
                {
                    string assetId = Weighted(pool, p => p.Weight, ref random).AssetId;
                    var asset = catalogue.Assets[assetId];
                    CoursePlacementData candidate = null;
                    for (int sample = 0; sample < CourseLimits.PlacementSamples; sample++)
                    {
                        var position = new CoursePoint(
                            Sample(zone.Bounds.Min.X, zone.Bounds.Max.X, zone.GridMm, ref random),
                            Sample(zone.Bounds.Min.Y, zone.Bounds.Max.Y, zone.GridMm, ref random),
                            Sample(zone.Bounds.Min.Z, zone.Bounds.Max.Z, zone.GridMm, ref random));
                        if (zone.Placement == "Fixed") position = zone.FixedPosition;
                        candidate = new CoursePlacementData
                        {
                            Id = EntityId("p", zone.Id, slot, -1), ZoneId = zone.Id, AssetId = assetId, ModuleId = zone.ModuleId,
                            Purpose = zone.Purpose, Letters = zone.Purpose == "Resources" ? asset.Letters : "",
                            Position = position, Yaw = zone.Placement == "Fixed" ? zone.FixedYaw : yaws[random.Range(0, yaws.Count)]
                        };
                        if (Fits(candidate, zone, d, catalogue, canonical)) break;
                        candidate = null;
                    }
                    if (candidate == null)
                    {
                        v.Add("placement.exhausted", "$.zones", zone.Id, "No valid placement in 64 samples; reduce density or enlarge the zone.");
                        return new List<CoursePlacementData>();
                    }
                    canonical.Add(candidate);
                }
            }
            var result = new List<CoursePlacementData>();
            foreach (var p in canonical)
            {
                bool scoped = TeamScoped(request.Play, p.Purpose);
                for (int team = 0; team < (scoped ? request.Play.TeamCount : 1); team++)
                    result.Add(new CoursePlacementData
                    {
                        Id = p.Id.Substring(0, p.Id.Length - 7) + (scoped ? "_t" + team.ToString(CultureInfo.InvariantCulture) : "_shared"),
                        ZoneId = p.ZoneId, AssetId = p.AssetId, ModuleId = p.ModuleId, Purpose = p.Purpose,
                        Letters = p.Letters, Position = p.Position, Yaw = p.Yaw, TeamIndex = scoped ? team : -1
                    });
            }
            return result;
        }
        static int Sample(int min, int max, int grid, ref Pcg32 random) => min + random.Range(0, (max - min) / grid + 1) * grid;
        internal static CourseBox Shape(CoursePlacementData p, CourseAssetDefinition a)
        {
            int x = (p.Yaw % 180 == 0 ? a.WidthMm : a.DepthMm), z = (p.Yaw % 180 == 0 ? a.DepthMm : a.WidthMm);
            return new CourseBox(new CoursePoint(p.Position.X - x / 2, p.Position.Y, p.Position.Z - z / 2),
                new CoursePoint(p.Position.X + (x + 1) / 2, p.Position.Y + a.HeightMm, p.Position.Z + (z + 1) / 2));
        }
        internal static bool Fits(CoursePlacementData p, CourseZone zone, CourseDocument d, CourseCatalogue catalogue, IEnumerable<CoursePlacementData> occupied)
        {
            var shape = Shape(p, catalogue.Assets[p.AssetId]);
            var module = catalogue.Bounds(d.Modules.Single(m => m.Id == p.ModuleId));
            if (!zone.Bounds.Contains(p.Position) || !module.Contains(shape.Min) || !module.Contains(shape.Max) ||
                shape.Min.X < zone.Bounds.Min.X || shape.Max.X > zone.Bounds.Max.X ||
                shape.Min.Z < zone.Bounds.Min.Z || shape.Max.Z > zone.Bounds.Max.Z) return false;
            if (d.ReservedVolumes.Any(r => shape.Intersects(r.Bounds))) return false;
            foreach (var spawn in d.Spawns)
                if (shape.Intersects(Reserved(spawn.Position, 1000))) return false;
            foreach (var anchor in d.RecoveryAnchors)
                if (shape.Intersects(Reserved(anchor.Position, anchor.RadiusMm))) return false;
            foreach (var supply in d.Supplies)
                if (shape.Intersects(Reserved(supply.Position, 500))) return false;
            foreach (var connection in d.Connections)
            {
                int radius = connection.WidthMm / 2 + 700;
                var corridor = new CourseBox(
                    new CoursePoint(Math.Min(connection.From.X, connection.To.X) - radius, Math.Min(connection.From.Y, connection.To.Y), Math.Min(connection.From.Z, connection.To.Z) - radius),
                    new CoursePoint(Math.Max(connection.From.X, connection.To.X) + radius, Math.Max(connection.From.Y, connection.To.Y) + 2000, Math.Max(connection.From.Z, connection.To.Z) + radius));
                if (shape.Intersects(corridor)) return false;
            }
            foreach (var other in occupied)
            {
                var box = Shape(other, catalogue.Assets[other.AssetId]);
                int gap = Math.Max(zone.SpacingMm, d.Zones.Single(z => z.Id == other.ZoneId).SpacingMm);
                var padded = new CourseBox(new CoursePoint(shape.Min.X - gap, shape.Min.Y, shape.Min.Z - gap),
                    new CoursePoint(shape.Max.X + gap, shape.Max.Y, shape.Max.Z + gap));
                if (padded.Intersects(box)) return false;
            }
            return true;
        }
        static CourseBox Reserved(CoursePoint p, int radius) => new CourseBox(
            new CoursePoint(p.X - radius, p.Y, p.Z - radius), new CoursePoint(p.X + radius, p.Y + 2000, p.Z + radius));
        internal static string ManifestHash(CourseManifestData data)
        {
            var value = (Dictionary<string, object>)CourseJson.Value(data);
            value.Remove("hash");
            return CourseJson.Sha256(CourseJson.Encode(value));
        }
    }
}
