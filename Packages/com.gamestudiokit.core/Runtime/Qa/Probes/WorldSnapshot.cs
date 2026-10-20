// Game Studio Kit (MIT). Before/after snapshot of a scene's top-level groups for bulk tools (placement, dressing,
// terrain fitting). Row 17 of the WoO problem table: a bulk tool sank 64 groups by 31.57 m and nobody noticed.
// Diff() reports the largest move the tool did not intend (bulk_tool_unintended_move_m), groups outside the intended
// prefixes that disappeared (bulk_tool_missing_groups: deleted, renamed or reparented), and anything that ended below the
// terrain (below_terrain_objects).
using System;
using System.Collections.Generic;
using GameStudioKit.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameStudioKit.Qa.Probes
{
    public sealed class WorldSnapshot
    {
        public sealed class Entry
        {
            public string Path;
            public Vector3 Position;
            public bool Active;
        }

        public string Scene;
        public string Utc;
        public readonly List<Entry> Entries = new List<Entry>();

        /// <summary>Records every root object and its direct children (the "groups" a bulk tool moves).</summary>
        public static WorldSnapshot Capture(Scene scene)
        {
            var snap = new WorldSnapshot { Scene = scene.path, Utc = KitIo.UtcNowIso() };
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var root in scene.GetRootGameObjects())
            {
                Add(snap, root.transform, names);
                foreach (Transform child in root.transform) Add(snap, child, names);
            }
            return snap;
        }

        static void Add(WorldSnapshot snap, Transform t, Dictionary<string, int> names)
        {
            string path = WorldAudit.PathOf(t);
            names.TryGetValue(path, out int n);
            names[path] = n + 1;
            if (n > 0) path += "#" + n; // disambiguate siblings with the same name
            snap.Entries.Add(new Entry { Path = path, Position = t.position, Active = t.gameObject.activeInHierarchy });
        }

        public sealed class DiffResult
        {
            public float MaxUnintendedMoveM;
            public int UnintendedMoves;
            /// <summary>Groups gone from the after snapshot (all of them).</summary>
            public int Missing;

            /// <summary>Groups gone that were outside the intended prefixes: a failure (bulk_tool_missing_groups).</summary>
            public int UnintendedMissing;
            public int BelowTerrain;
            public readonly List<string> Offenders = new List<string>();
            public readonly List<AuditExemption> Allowed = new List<AuditExemption>();
        }

        /// <summary>
        /// Compares two snapshots. Paths under one of <paramref name="intendedPrefixes"/> (the prefix itself, "prefix/…" or a
        /// same-named sibling "prefix#n"; never "prefix2") may move or disappear. Elsewhere, moves above
        /// <paramref name="thresholdM"/> and missing groups are offenders. <paramref name="after"/>'s scene is also checked for
        /// groups that ended below a terrain (when <paramref name="afterScene"/> is valid).
        /// </summary>
        public static DiffResult Diff(WorldSnapshot before, WorldSnapshot after, IList<string> intendedPrefixes, float thresholdM = 0.5f, Scene afterScene = default)
        {
            var result = new DiffResult();
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var e in after.Entries) map[e.Path] = e;
            foreach (var b in before.Entries)
            {
                bool intended = ProbeMath.PathUnderAny(b.Path, intendedPrefixes);
                if (!map.TryGetValue(b.Path, out var a))
                {
                    result.Missing++;
                    if (intended) continue;
                    result.UnintendedMissing++;
                    if (result.Offenders.Count < 50) result.Offenders.Add(b.Path + " is gone (deleted, renamed or reparented)");
                    continue;
                }
                if (intended) continue;
                float move = Vector3.Distance(a.Position, b.Position);
                if (move > result.MaxUnintendedMoveM) result.MaxUnintendedMoveM = move;
                if (move > thresholdM)
                {
                    result.UnintendedMoves++;
                    if (result.Offenders.Count < 50) result.Offenders.Add(b.Path + " moved " + KitJsonWriter.FormatNumber(move) + " m");
                }
            }
            if (afterScene.IsValid() && afterScene.isLoaded)
            {
                var terrains = WorldAudit.Collect<Terrain>(afterScene, false);
                foreach (var root in afterScene.GetRootGameObjects())
                {
                    foreach (Transform t in root.transform)
                    {
                        foreach (var terrain in terrains)
                        {
                            if (terrain.terrainData == null || t.GetComponentInParent<Terrain>() == terrain) continue;
                            var tp = terrain.transform.position;
                            var size = terrain.terrainData.size;
                            var p = t.position;
                            if (p.x < tp.x || p.x > tp.x + size.x || p.z < tp.z || p.z > tp.z + size.z) continue;
                            float h = terrain.SampleHeight(p) + tp.y;
                            if (p.y < h - 0.5f)
                            {
                                if (AuditAllow.Allows(t.gameObject, AuditException.BelowTerrain, out string why))
                                {
                                    result.Allowed.Add(new AuditExemption { Path = WorldAudit.PathOf(t), Flag = AuditException.BelowTerrain.ToString(), Why = why });
                                    break;
                                }
                                result.BelowTerrain++;
                                if (result.Offenders.Count < 50) result.Offenders.Add(WorldAudit.PathOf(t) + " is " + KitJsonWriter.FormatNumber(h - p.y) + " m below the terrain");
                            }
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public static List<ProbeResult> Evaluate(DiffResult diff, ProbeConfig config)
        {
            config = config ?? ProbeConfig.Defaults();
            return new List<ProbeResult>
            {
                config.Evaluate("WorldSnapshot", "bulk_tool_unintended_move_m", diff.MaxUnintendedMoveM),
                config.Evaluate("WorldSnapshot", "bulk_tool_missing_groups", diff.UnintendedMissing),
                config.Evaluate("WorldSnapshot", "below_terrain_objects", diff.BelowTerrain),
            };
        }

        public Dictionary<string, object> ToJsonObject()
        {
            var entries = new List<object>();
            foreach (var e in Entries)
            {
                entries.Add(new Dictionary<string, object>
                {
                    { "path", e.Path },
                    { "position", new List<object> { (double)e.Position.x, (double)e.Position.y, (double)e.Position.z } },
                    { "active", e.Active },
                });
            }
            return new Dictionary<string, object> { { "schema", 1 }, { "scene", Scene }, { "utc", Utc }, { "entries", entries } };
        }

        public static WorldSnapshot FromJson(string json)
        {
            var root = KitJson.AsObject(KitJson.Parse(json)) ?? throw new FormatException("snapshot: the root is not an object");
            var snap = new WorldSnapshot { Scene = KitJson.GetString(root, "scene"), Utc = KitJson.GetString(root, "utc") };
            var list = KitJson.GetList(root, "entries");
            if (list == null) return snap;
            foreach (var item in list)
            {
                var o = KitJson.AsObject(item);
                if (o == null) continue;
                var pos = KitJson.GetList(o, "position");
                var v = Vector3.zero;
                if (pos != null && pos.Count == 3) v = new Vector3((float)KitJson.ToDouble(pos[0]), (float)KitJson.ToDouble(pos[1]), (float)KitJson.ToDouble(pos[2]));
                snap.Entries.Add(new Entry { Path = KitJson.GetString(o, "path"), Position = v, Active = KitJson.GetBool(o, "active", true) });
            }
            return snap;
        }
    }
}
