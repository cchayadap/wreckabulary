"""Pure-Python rig logic shared by the Blender scripts and mia_autorig.py. Standard library only (no bpy, no numpy),
so it is unit-tested with plain Python."""
import json
import math
import os
import re
from collections import deque
from fnmatch import fnmatchcase

SCHEMA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "rig_schemas")

# Rest-pose bands for the upper-arm elevation (degrees above horizontal, from shoulder joint to elbow joint).
T_POSE_TOL_DEG = 10.0
A_POSE_RANGE_DEG = (-55.0, -25.0)

LEAF_SUFFIXES = ("_end", ".end", "end", "_tip", ".tip", "tip", "_leaf", ".leaf", "leaf")
LEAF_TOKENS = frozenset({"end", "tip", "leaf"})
DEFAULT_MAX_CELLS = 400


def strip_ns(name):
    return name.split(":")[-1]


def is_leaf_name(name):
    """A tip/end marker joint, which may carry no weight: 'HeadTop_End', 'Head.tip', 'Ear_Tip_L', 'ear_leaf_r'."""
    n = strip_ns(name).lower()
    return any(n.endswith(s) for s in LEAF_SUFFIXES) or bool(LEAF_TOKENS & set(re.split(r"[_.\-\s]+", n)))


def bone_matches(name, patterns):
    """True when a bone name (or the name without its namespace) matches one of the glob patterns, ignoring case."""
    full, short = name.lower(), strip_ns(name).lower()
    return any(fnmatchcase(full, p.lower()) or fnmatchcase(short, p.lower()) for p in patterns)


def unweighted_schema_bones(schema, mapping, per_bone_verts):
    """The schema's `weighted` bones (as the rig names them) that no vertex uses: the rig gate's bone_weighted check."""
    must = [mapping[b] for b in schema.get("weighted", []) if b in mapping]
    return [b for b in must if per_bone_verts.get(b, 0) == 0]


def resolve_schema_path(spec, cwd=None):
    """A schema argument: a path (absolute or relative to cwd), a path relative to this folder, or a bare name
    such as 'mixamo_22' that means rig_schemas/mixamo_22.json. Returns an absolute path or None."""
    cands = []
    base = cwd or os.getcwd()
    cands.append(spec if os.path.isabs(spec) else os.path.join(base, spec))
    here = os.path.dirname(os.path.abspath(__file__))
    cands.append(os.path.join(here, spec))
    name = spec if spec.endswith(".json") else spec + ".json"
    cands.append(os.path.join(SCHEMA_DIR, os.path.basename(name)))
    for c in cands:
        if os.path.isfile(c):
            return os.path.abspath(c)
    return None


def load_schema(spec, cwd=None):
    """Load and normalise a rig schema: {schema:1, name, bones[], required[], weighted[], rest_pose, ...}."""
    if spec in ("any", "none"):
        return {"schema": 1, "name": "any", "bones": [], "required": [], "weighted": [], "rest_pose": "any",
                "max_influences": 4, "path": None}
    path = resolve_schema_path(spec, cwd)
    if path is None:
        raise FileNotFoundError("rig schema not found: %s" % spec)
    with open(path, "r", encoding="utf-8-sig") as fh:
        s = json.load(fh)
    if s.get("schema") != 1 or not isinstance(s.get("bones"), list) or not isinstance(s.get("required"), list):
        raise ValueError("bad rig schema %s: needs schema:1, bones[], required[]" % path)
    missing = [b for b in s["required"] if b not in s["bones"]]
    if missing:
        raise ValueError("bad rig schema %s: required bones not in bones[]: %s" % (path, missing[:5]))
    s.setdefault("name", os.path.splitext(os.path.basename(path))[0])
    s.setdefault("weighted", [])
    s.setdefault("rest_pose", "any")
    s.setdefault("max_influences", 4)
    s.setdefault("match", "ignore_namespace")
    s["path"] = path
    return s


def match_bones(schema, bone_names):
    """Map schema bone names to the rig's bone names. match 'exact' compares full names; 'ignore_namespace'
    (default) also accepts 'mixamorig:Hips' for 'Hips' and the reverse. Returns missing, extra and the mapping."""
    exact = schema.get("match") == "exact"
    by_full = {n: n for n in bone_names}
    by_short = {}
    for n in bone_names:
        by_short.setdefault(strip_ns(n), n)
    mapping = {}
    for want in schema["bones"]:
        got = by_full.get(want)
        if got is None and not exact:
            got = by_short.get(strip_ns(want))
        if got is not None:
            mapping[want] = got
    used = set(mapping.values())
    missing = [b for b in schema["required"] if b not in mapping]
    extra = [n for n in bone_names if n not in used]
    namespaces = sorted({n.split(":")[0] for n in bone_names if ":" in n})
    return {"mapping": mapping, "missing": missing, "extra": extra, "namespaces": namespaces}


def elevation_deg(vec):
    """Angle of a 3D vector above the horizontal plane (Z up), in degrees."""
    x, y, z = vec
    n = math.sqrt(x * x + y * y + z * z)
    if n < 1e-12:
        return None
    return math.degrees(math.asin(max(-1.0, min(1.0, z / n))))


def classify_rest(elev, tol=T_POSE_TOL_DEG):
    if elev is None:
        return "unknown"
    if abs(elev) <= tol:
        return "T"
    if A_POSE_RANGE_DEG[0] <= elev <= A_POSE_RANGE_DEG[1]:
        return "A"
    return "other"


def hierarchy_distances(parents):
    """parents: {bone: parent or None}. Returns dist(a, b), the number of edges between two bones in the tree."""
    adj = {}
    for b, p in parents.items():
        adj.setdefault(b, set())
        if p is not None:
            adj.setdefault(p, set())
            adj[b].add(p)
            adj[p].add(b)
    cache = {}

    def from_(a):
        if a not in cache:
            seen = {a: 0}
            q = deque([a])
            while q:
                x = q.popleft()
                for y in adj.get(x, ()):
                    if y not in seen:
                        seen[y] = seen[x] + 1
                        q.append(y)
            cache[a] = seen
        return cache[a]

    def dist(a, b):
        return from_(a).get(b, 10 ** 6)

    return dist


def parse_range(text):
    """'1.6,1.9' -> (1.6, 1.9); raises ValueError."""
    parts = [p.strip() for p in str(text).split(",")]
    if len(parts) != 2:
        raise ValueError("expected 'min,max', got %r" % text)
    a, b = float(parts[0]), float(parts[1])
    if not (math.isfinite(a) and math.isfinite(b)) or a <= 0 or b < a:
        raise ValueError("bad range %r" % text)
    return a, b


def voxel_floor(max_dim_m, max_cells=DEFAULT_MAX_CELLS):
    """The smallest voxel allowed: max dimension / max_cells (finer proxies cost too much time and memory)."""
    return max_dim_m / float(max_cells) if max_dim_m else 0.0


def voxel_decision(voxel_m, min_gap_m, max_dim_m, max_cells=DEFAULT_MAX_CELLS, gap_pair=None):
    """Decide whether a proxy voxel size is safe. Returns (ok, code, message). A voxel must be at least the floor
    (max_dim / max_cells) and below the narrowest limb gap; when the gap is at or below the floor no voxel works."""
    if not (voxel_m > 0 and math.isfinite(voxel_m)):
        return False, "bad_voxel", "voxel size must be a positive number"
    floor = voxel_floor(max_dim_m, max_cells)
    if min_gap_m is not None and floor and min_gap_m <= floor * (1 + 1e-9):
        pair = " (between %s and %s)" % tuple(gap_pair) if gap_pair else ""
        return False, "gap_below_voxel_floor", (
            "the narrowest limb gap %.4g m%s is at or below the smallest voxel %.4g m (%.3g m / %d cells), so no voxel "
            "keeps those parts apart: leave thin parts such as fingers out of the gap search with --exclude-bones "
            "(they are weighted by nearest bone), give separate parts one bone with --rigid NAME=BONE, pose the limbs "
            "further apart, or raise --max-cells" % (min_gap_m, pair, floor, max_dim_m, max_cells))
    if floor and voxel_m < floor * (1 - 1e-9):
        span = ("between %.4g and %.4g m" % (floor, min_gap_m)) if min_gap_m is not None else "at least %.4g m" % floor
        return False, "voxel_too_fine", "voxel %.4g m gives more than %d cells across %.3g m; use a voxel %s" % (
            voxel_m, max_cells, max_dim_m, span)
    if min_gap_m is not None and voxel_m >= min_gap_m:
        return False, "voxel_too_coarse", (
            "voxel %.4g m >= narrowest limb gap %.4g m: the proxy would fuse limbs; use at most %.4g m"
            % (voxel_m, min_gap_m, max(min_gap_m * 0.5, floor)))
    return True, None, None


def auto_voxel(max_dim_m, min_gap_m, max_cells=DEFAULT_MAX_CELLS):
    """Default voxel: max dimension / 120 (the verified probe value), at most half the narrowest gap, and never
    below the floor (voxel_decision then judges it)."""
    v = max_dim_m / 120.0
    if min_gap_m is not None:
        v = min(v, min_gap_m * 0.5)
    return max(v, voxel_floor(max_dim_m, max_cells))


def limit_and_normalise(weights, limit=4, floor=1e-3):
    """{bone: w} -> the `limit` largest weights above `floor`, normalised to sum 1 (empty stays empty)."""
    items = sorted(((b, w) for b, w in weights.items() if w > floor), key=lambda t: (-t[1], t[0]))[:limit]
    s = sum(w for _, w in items)
    if s <= 0:
        return {}
    return {b: w / s for b, w in items}


def barycentric(p, a, b, c):
    """Barycentric (u, v, w) of point p projected on triangle abc, clamped to the triangle and renormalised."""
    v0 = [b[i] - a[i] for i in range(3)]
    v1 = [c[i] - a[i] for i in range(3)]
    v2 = [p[i] - a[i] for i in range(3)]
    d00 = sum(v0[i] * v0[i] for i in range(3))
    d01 = sum(v0[i] * v1[i] for i in range(3))
    d11 = sum(v1[i] * v1[i] for i in range(3))
    d20 = sum(v2[i] * v0[i] for i in range(3))
    d21 = sum(v2[i] * v1[i] for i in range(3))
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-20:
        return (1.0, 0.0, 0.0)
    v = (d11 * d20 - d01 * d21) / den
    w = (d00 * d21 - d01 * d20) / den
    u = 1.0 - v - w
    u, v, w = max(u, 0.0), max(v, 0.0), max(w, 0.0)
    s = u + v + w
    return (u / s, v / s, w / s) if s > 0 else (1.0, 0.0, 0.0)


def split_clusters(values, min_gap):
    """Sort 1D values and split where consecutive values differ by more than min_gap. Returns [(lo, hi, count)]."""
    xs = sorted(values)
    if not xs:
        return []
    out = []
    start = xs[0]
    prev = xs[0]
    count = 1
    for x in xs[1:]:
        if x - prev > min_gap:
            out.append((start, prev, count))
            start = x
            count = 0
        prev = x
        count += 1
    out.append((start, prev, count))
    return out


FPS_CANDIDATES = (24, 25, 30, 48, 50, 60, 12, 15, 20)


def detect_fps(frames, scene_fps, tol=0.02):
    """The frame rate a clip was sampled at, from its key frames as imported at `scene_fps`: the first candidate
    (scene fps first) on which every key time lands on a whole frame. None when there are too few keys."""
    frames = sorted(set(frames))
    if len(frames) < 3:
        return None
    f0 = frames[0]
    times = [(f - f0) / float(scene_fps) for f in frames]
    order = [scene_fps] + [c for c in FPS_CANDIDATES if c != scene_fps]
    for cand in order:
        if all(abs(t * cand - round(t * cand)) < tol for t in times):
            return cand
    return None


def gate_verdict(failures):
    return "PASS" if not failures else "FAIL"


PATH_KEYS = frozenset({"input", "in", "out", "donor", "path", "metrics_file", "schema_path", "emitted_schema", "copy_dir",
                       "outputs_dir", "provenance_file", "sheet", "frames", "failed", "result_file", "log"})


def find_workspace(start=None):
    """The folder holding Tools/kit/kit.json at or above `start` (default: the working directory), or None."""
    d = os.path.abspath(start or os.getcwd())
    while True:
        if os.path.isfile(os.path.join(d, "Tools", "kit", "kit.json")):
            return d
        up = os.path.dirname(d)
        if up == d:
            return None
        d = up


def show_path(p, ws):
    """CONTRACTS 1.1: POSIX, relative to the workspace when inside it, else absolute POSIX."""
    ap = os.path.abspath(p)
    if ws:
        w = os.path.abspath(ws)
        if os.path.normcase(ap) == os.path.normcase(w) or os.path.normcase(ap).startswith(os.path.normcase(w) + os.sep):
            return os.path.relpath(ap, w).replace("\\", "/")
    return ap.replace("\\", "/")


def present_paths(data, ws=None, key=None):
    """Rewrite path-valued fields (by key name) of a result for output; everything else is left alone."""
    if isinstance(data, dict):
        return {k: present_paths(v, ws, k) for k, v in data.items()}
    if isinstance(data, list):
        return [present_paths(v, ws, key) for v in data]
    if key in PATH_KEYS and isinstance(data, str) and data and os.path.isabs(data):
        return show_path(data, ws)
    return data


def pick_output(paths, want_ext, input_stem=None, debug_tokens=("joints", "bw", "rest", "lbs", "debug")):
    """Choose the main model among a service's output files: right extension, not a debug file; prefer the input's
    stem, then the largest file. Returns a path or None."""
    want_ext = want_ext.lower()
    cands = []
    for p in paths:
        stem, ext = os.path.splitext(os.path.basename(p))
        if ext.lower() != want_ext:
            continue
        tokens = set(stem.lower().replace("-", "_").split("_"))
        if tokens & set(debug_tokens):
            continue
        cands.append(p)
    if not cands:
        return None
    if input_stem:
        same = [p for p in cands if os.path.splitext(os.path.basename(p))[0].lower() == input_stem.lower()]
        if same:
            return same[0]
    return max(cands, key=lambda p: (os.path.getsize(p) if os.path.exists(p) else 0, p))
