"""Read-only FBX metadata for the animation intake (stdlib only).

probe_fbx(path) returns the facts mixamo_intake.py checks: binary or ASCII, FBX version, frame rate, clip length,
takes (AnimationStack), curve count, skeleton bone names and whether a mesh is present. It never executes or rewrites
anything. Binary layout: 27-byte header (magic, 0x1A 0x00, uint32 version), then node records (end offset, property
count, property bytes, name length, name, properties, nested records); versions >= 7500 use 64-bit record fields.
"""
from __future__ import annotations

import re
import struct
import zlib

BINARY_MAGIC = b"Kaydara FBX Binary  \x00"
KTIME_PER_SECOND = 46186158000
# FbxTime::EMode -> frames per second (0 = default, 14 = custom: use CustomFrameRate)
TIME_MODES = {
    1: 120.0, 2: 100.0, 3: 60.0, 4: 50.0, 5: 48.0, 6: 30.0, 7: 30.0, 8: 29.97, 9: 29.97, 10: 25.0, 11: 24.0,
    12: 1000.0, 13: 23.976, 15: 96.0, 16: 72.0, 17: 59.94, 18: 119.88,
}
MIXAMO_PREFIX_RE = re.compile(r"^(mixamorig\d*):(.+)$")
MIXAMO_CORE = (
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
)
MAX_BYTES = 512 * 1024 * 1024


class FbxError(ValueError):
    """The file is not a readable FBX."""


class _Node:
    __slots__ = ("name", "props", "children")

    def __init__(self, name, props, children):
        self.name = name
        self.props = props
        self.children = children

    def find(self, name):
        return [c for c in self.children if c.name == name]


def _read_props(buf, pos, count, end):
    props = []
    for _ in range(count):
        if pos >= end:
            raise FbxError("property list overruns its record")
        code = chr(buf[pos])
        pos += 1
        if code == "Y":
            props.append(struct.unpack_from("<h", buf, pos)[0]); pos += 2
        elif code == "C":
            props.append(bool(buf[pos])); pos += 1
        elif code == "I":
            props.append(struct.unpack_from("<i", buf, pos)[0]); pos += 4
        elif code == "F":
            props.append(struct.unpack_from("<f", buf, pos)[0]); pos += 4
        elif code == "D":
            props.append(struct.unpack_from("<d", buf, pos)[0]); pos += 8
        elif code == "L":
            props.append(struct.unpack_from("<q", buf, pos)[0]); pos += 8
        elif code in "fdlib":
            length, encoding, clen = struct.unpack_from("<III", buf, pos)
            pos += 12
            if encoding not in (0, 1):
                raise FbxError(f"unknown array encoding {encoding}")
            props.append(("array", code, length))  # values are never needed here: skip without inflating
            pos += clen
        elif code in "SR":
            (n,) = struct.unpack_from("<I", buf, pos)
            pos += 4
            raw = bytes(buf[pos:pos + n])
            pos += n
            props.append(raw.decode("utf-8", "replace") if code == "S" else raw)
        else:
            raise FbxError(f"unknown property type {code!r}")
    return props


def _read_nodes(buf, pos, end, wide, depth):
    nodes = []
    head = 25 if wide else 13
    fmt = "<QQQB" if wide else "<IIIB"
    while pos + head <= end:
        end_off, nprops, plen, nlen = struct.unpack_from(fmt, buf, pos)
        if end_off == 0:  # null record: end of this list
            return nodes, pos + head
        if end_off > len(buf) or end_off <= pos:
            raise FbxError("corrupt record offset")
        name = bytes(buf[pos + head:pos + head + nlen]).decode("ascii", "replace")
        ppos = pos + head + nlen
        props = _read_props(buf, ppos, nprops, ppos + plen) if nprops else []
        cpos = ppos + plen
        children = []
        if cpos < end_off and depth < 64:
            children, _ = _read_nodes(buf, cpos, end_off, wide, depth + 1)
        nodes.append(_Node(name, props, children))
        pos = end_off
    return nodes, pos


def _p70(node):
    """Properties70 of a node as {name: value}."""
    out = {}
    for p70 in node.find("Properties70"):
        for p in p70.find("P"):
            if len(p.props) >= 5 and isinstance(p.props[0], str):
                out[p.props[0]] = p.props[4]
    return out


def _object_name(raw):
    """Binary FBX object names are 'Name\\x00\\x01Class'."""
    return raw.split("\x00\x01")[0] if isinstance(raw, str) else ""


def _fps(time_mode, custom):
    if time_mode == 14 and isinstance(custom, (int, float)) and custom > 0:
        return float(custom)
    if time_mode in TIME_MODES:
        return TIME_MODES[time_mode]
    if time_mode == 0:
        return 30.0  # eDefaultMode is 30 fps
    return None


def _summarise(kind, version, gs, models, stacks, curves, geometries):
    fps = _fps(gs.get("TimeMode"), gs.get("CustomFrameRate"))
    takes = []
    for name, props in stacks:
        start = props.get("LocalStart", props.get("ReferenceStart"))
        stop = props.get("LocalStop", props.get("ReferenceStop"))
        dur = (stop - start) / KTIME_PER_SECOND if isinstance(start, int) and isinstance(stop, int) else None
        takes.append({"name": name, "duration_s": round(dur, 4) if dur is not None else None})
    span = None
    if isinstance(gs.get("TimeSpanStart"), int) and isinstance(gs.get("TimeSpanStop"), int):
        span = (gs["TimeSpanStop"] - gs["TimeSpanStart"]) / KTIME_PER_SECOND
    duration = next((t["duration_s"] for t in takes if t["duration_s"]), None)
    if duration is None and span is not None:
        duration = round(span, 4)
    bones = [n for n, t in models if t in ("LimbNode", "Null", "Root", "Skeleton")]
    prefixes = {}
    for b in bones:
        m = MIXAMO_PREFIX_RE.match(b)
        if m:
            prefixes.setdefault(m.group(1), set()).add(m.group(2))
    prefix = max(prefixes, key=lambda k: len(prefixes[k])) if prefixes else None
    found = prefixes.get(prefix, set())
    return {
        "kind": kind,
        "version": version,
        "fps": fps,
        "time_mode": gs.get("TimeMode"),
        "duration_s": duration,
        "frames": int(round(duration * fps)) if duration is not None and fps else None,
        "takes": takes,
        "curves": curves,
        "has_mesh": geometries > 0,
        "bones": bones,
        "mixamo_prefix": prefix,
        "mixamo_bones": len(found),
        "mixamo_core_missing": [b for b in MIXAMO_CORE if b not in found],
    }


def _probe_binary(buf):
    if len(buf) < 27 or buf[23] is None:
        raise FbxError("truncated header")
    (version,) = struct.unpack_from("<I", buf, 23)
    if version < 6000 or version > 10000:
        raise FbxError(f"implausible FBX version {version}")
    nodes, _ = _read_nodes(buf, 27, len(buf), version >= 7500, 0)
    top = {n.name: n for n in nodes}
    gs = _p70(top["GlobalSettings"]) if "GlobalSettings" in top else {}
    models, stacks, curves, geometries = [], [], 0, 0
    objects = top.get("Objects")
    if objects is not None:
        for o in objects.children:
            if o.name == "Model" and len(o.props) >= 3:
                models.append((_object_name(o.props[1]), o.props[2]))
            elif o.name == "AnimationStack" and len(o.props) >= 2:
                stacks.append((_object_name(o.props[1]), _p70(o)))
            elif o.name == "AnimationCurve":
                curves += 1
            elif o.name == "Geometry":
                geometries += 1
    return _summarise("binary", version, gs, models, stacks, curves, geometries)


_ASCII_P = re.compile(r'P:\s*"([^"]+)"\s*,\s*"[^"]*"\s*,\s*"[^"]*"\s*,\s*"[^"]*"\s*,\s*([-+0-9.eE]+)')


def _probe_ascii(text):
    m = re.search(r"FBX\s+(\d+)\.(\d+)\.(\d+)", text[:200])
    version = int(m.group(1)) * 1000 + int(m.group(2)) * 100 + int(m.group(3)) if m else None
    gs = {}
    gsm = re.search(r"GlobalSettings:\s*\{(.*?)\n\}", text, re.S)
    if gsm:
        for name, val in _ASCII_P.findall(gsm.group(1)):
            gs[name] = float(val) if re.search(r"[.eE]", val) else int(val)
    models = [(name, typ) for name, typ in re.findall(r'Model:\s*-?\d+\s*,\s*"Model::([^"]*)"\s*,\s*"([^"]*)"', text)]
    stacks = []
    for sm in re.finditer(r'AnimationStack:\s*-?\d+\s*,\s*"AnimStack::([^"]*)"\s*,\s*"[^"]*"\s*\{(.*?)\n\t\}', text, re.S):
        props = {}
        for name, val in _ASCII_P.findall(sm.group(2)):
            props[name] = int(val) if re.fullmatch(r"[-+]?\d+", val) else float(val)
        stacks.append((sm.group(1), props))
    curves = len(re.findall(r"\bAnimationCurve:\s*-?\d+", text))
    geometries = len(re.findall(r"\bGeometry:\s*-?\d+", text))
    return _summarise("ascii", version, gs, models, stacks, curves, geometries)


def probe_fbx(path):
    """Metadata of one FBX file. Raises FbxError when it is not a readable FBX."""
    with open(path, "rb") as fh:
        buf = fh.read(MAX_BYTES + 1)
    if len(buf) > MAX_BYTES:
        raise FbxError("file larger than 512 MiB")
    if buf.startswith(BINARY_MAGIC):
        try:
            return _probe_binary(memoryview(buf).tobytes())
        except (struct.error, IndexError, zlib.error) as e:
            raise FbxError(f"corrupt binary FBX: {e}") from e
    head = buf[:64].lstrip(b"\xef\xbb\xbf").lstrip()
    if head.startswith(b"; FBX"):
        return _probe_ascii(buf.decode("utf-8", "replace"))
    raise FbxError("not an FBX file (no binary magic and no '; FBX' ASCII header)")
