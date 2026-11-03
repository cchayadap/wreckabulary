"""Pure-Python BELT geometry: sections, sweeps, weights and the analytic bend. No bpy; unit-testable.

Blender frame: +Z is the belt length (Unity +Y), +X the strap width, +Y the strap thickness (front face is -Y).
Metres throughout.
"""
from __future__ import annotations

import math

SIZE = (0.112, 1.200, 0.072)          # catalogue size [x, y, z] in Unity metres
GRIP = (0.0, 0.160, 0.0)              # catalogue grip [x, y, z] in Unity metres
HELD_SCALE = 0.5

STRAP_W, STRAP_T, STRAP_R = 0.075, 0.024, 0.008
STRAP_Z0, STRAP_Z1 = 0.365, 1.000
GRIP_Z0, GRIP_Z1, GRIP_R = 0.035, 0.285, 0.030
HOLES_Z = (0.46, 0.51, 0.56, 0.61, 0.66)
HOLE_D = 0.014
KEEPERS = ((0.900, 0.930), (0.940, 0.970))
BAR_Z = 1.112
BAR_R = 0.008
FOLD_R = BAR_R + 0.001 + STRAP_T / 2
SHIFT_Z0, SHIFT_Z1 = 0.980, 1.060
BACK_Z = 1.020
FRAME_Z0, FRAME_Z1, FRAME_W = 1.104, 1.200, 0.112
FRAME_TUBE = 0.008

BONES = (
    ("Belt_Root", 0.160, 0.365),
    ("Belt_01", 0.365, 0.455),
    ("Belt_02", 0.455, 0.550),
    ("Belt_03", 0.550, 0.650),
    ("Belt_04", 0.650, 0.755),
    ("Belt_05", 0.755, 0.870),
    ("Belt_06", 0.870, 1.000),
    ("Belt_Tip", 1.000, 1.200),
)
JOINT_BLEND = 0.03
JOINT_LIMIT_DEG = 35.0
TOTAL_LIMIT_DEG = 150.0

LEATHER = {
    "Classic": {"base": "#B47B4A", "edge": "#7A4A2B", "stitch": "#ECE2CA", "roughness": 0.58},
    "Candy": {"base": "#52A2A0", "edge": "#2F6E6C", "stitch": "#FDF7E3", "roughness": 0.40},
    "Arcade": {"base": "#6F5795", "edge": "#44335E", "stitch": "#E3D8F7", "roughness": 0.45},
}
GOLD = {"base": "#F5C873", "metallic": 0.68, "roughness": 0.32}


def rounded_rect(w: float, t: float, r: float, per_corner: int = 4):
    """Counter-clockwise rounded rectangle in (x, y); corners start at +x,+y. Returns [(x, y, angle_deg)]."""
    cx, cy = w / 2 - r, t / 2 - r
    corners = ((cx, cy, 0.0), (-cx, cy, 90.0), (-cx, -cy, 180.0), (cx, -cy, 270.0))
    pts = []
    for ox, oy, a0 in corners:
        for k in range(per_corner):
            a = a0 + 90.0 * k / (per_corner - 1)
            pts.append((ox + r * math.cos(math.radians(a)), oy + r * math.sin(math.radians(a)), a))
    return pts


def section_u(x: float, angle: float) -> float:
    """Atlas U in the strap tile (0..0.5), continuous around the section: faces span 0.06..0.44, edges take the paint."""
    if x > 0:
        return 0.5 - 0.06 * angle / 90.0 if angle <= 90.0 else 0.44 + 0.06 * (angle - 270.0) / 90.0
    return 0.06 - 0.06 * (angle - 90.0) / 90.0 if angle <= 180.0 else 0.06 * (angle - 180.0) / 90.0


def smooth(t: float) -> float:
    t = 0.0 if t < 0 else 1.0 if t > 1 else t
    return t * t * (3 - 2 * t)


def strap_path(step: float = 0.0235):
    """Centre-line points (y, z) from the strap start, forward shift, fold around the buckle bar, back layer."""
    pts = []
    z = STRAP_Z0
    while z < SHIFT_Z0 - 1e-9:
        pts.append((0.0, z))
        z += step
    for i in range(6):
        zz = SHIFT_Z0 + (BAR_Z - SHIFT_Z0) * i / 5
        pts.append((-FOLD_R * smooth((zz - SHIFT_Z0) / (SHIFT_Z1 - SHIFT_Z0)), zz))
    for i in range(1, 13):
        phi = math.pi - math.pi * i / 12
        pts.append((FOLD_R * math.cos(phi), BAR_Z + FOLD_R * math.sin(phi)))
    for i in range(1, 5):
        pts.append((FOLD_R, BAR_Z - (BAR_Z - BACK_Z) * i / 4))
    return pts


def path_frames(pts):
    """Tangent and in-plane normal (both in y,z) for each path point."""
    out = []
    for i, (y, z) in enumerate(pts):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        ty, tz = b[0] - a[0], b[1] - a[1]
        n = math.hypot(ty, tz) or 1.0
        ty, tz = ty / n, tz / n
        out.append(((y, z), (ty, tz), (-tz, ty)))
    return out


def arc_lengths(pts):
    s, out = 0.0, [0.0]
    for a, b in zip(pts, pts[1:]):
        s += math.hypot(b[0] - a[0], b[1] - a[1])
        out.append(s)
    return out


def joints():
    return [b[1] for b in BONES[1:]]


def weights_at(z: float, blend: float = JOINT_BLEND):
    """At most two bone influences by rest height, cosine-blended within +-blend of each joint."""
    names = [b[0] for b in BONES]
    js = joints()
    for k, jz in enumerate(js):
        if jz - blend <= z <= jz + blend:
            u = 0.5 - 0.5 * math.cos(math.pi * (z - (jz - blend)) / (2 * blend))
            lower, upper = names[k], names[k + 1]
            return {lower: 1.0 - u, upper: u} if u < 1.0 else {upper: 1.0}
    for k, (name, z0, z1) in enumerate(BONES):
        if k == 0 and z < z1:
            return {name: 1.0}
        if z0 <= z < z1:
            return {name: 1.0}
    return {names[-1]: 1.0}


def bend_angles(tip_deg: float, segments: int = 6):
    """Tip-weighted share of a total bend across Belt_01..Belt_06, as the runtime driver applies it."""
    total = segments * (segments + 1) / 2
    return [tip_deg * (i + 1) / total for i in range(segments)]


def hex_rgb(h: str):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
