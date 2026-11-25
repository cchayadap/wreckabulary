import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))

from house_item_shapes import (banded, box, box_of, box_uv, cylinder, densify, ellipse, ellipsoid, fit, gathered, interpolate, join, lathe,
                               lobed, mesh_bounds, move, preview, prism, pumpkin, reset, rounded_rect, slab, spline, star, stats, taper, torus, tube)

REPO = Path(__file__).resolve().parents[2]
MODELS = REPO / "Assets/_Project/Art/Collections/HouseItemsV2/Models"
MANIFEST = REPO / "ArtSource/Collections/HouseItemsV2/manifest.json"
SCRIPT = "Tools/AssetPipeline/build_house_items_v2.py"
WORDS = ("RAKE", "TEAPOT", "LID", "TOMATO", "ROCKET", "MARBLE", "LADDER", "PADDLE", "CHILI", "DISC",
         "HELMET", "LEMON", "TORCH", "CARPET", "SLIME", "SPOON", "TENT", "PEAR", "SOCK", "HOSE",
         "RACKET", "MELON", "PIANO", "TONIC", "BRICK", "PENCIL", "RAMP", "OIL", "DOOR", "BOOT")
FIT_WARNING = 0.05
FIT_LIMIT = 0.12
BOUNDS_TOLERANCE = 2e-4
TURN = Matrix.Identity(4)
GROW = {"LEMON": 1.35, "PEAR": 1.3, "TONIC": 1.3, "TOMATO": 1.3}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out")
    p.add_argument("--in-place", action="store_true")
    p.add_argument("--report", required=True)
    p.add_argument("--preview")
    p.add_argument("--only", default=",".join(WORDS))
    p.add_argument("--samples", type=int, default=24)
    args = p.parse_args(argv)
    if bool(args.out) == args.in_place:
        p.error("pass exactly one of --out DIR or --in-place")
    args.words = [w.strip().upper() for w in args.only.split(",") if w.strip()]
    unknown = [w for w in args.words if w not in WORDS]
    if unknown:
        p.error("unknown words: " + ", ".join(unknown))
    return args


def build_rake(size):
    body, accent = "orange_Classic", "teal_Classic"
    ellipsoid("cap", accent, (0, 0, 0.02), (0.026, 0.026, 0.02), 18, 9)
    cylinder("sleeve", accent, 0.024, 0.2, (0, 0, 0.13), "Z", 24, bevel=0.004)
    for z in (0.07, 0.11, 0.15, 0.19):
        torus("ridge", accent, 0.024, 0.006, (0, 0, z), "Z", 20, 5)
    cylinder("handle", body, 0.019, 0.92, (0, 0, 0.69), "Z", 20, bevel=0)
    cylinder("collar", body, 0.026, 0.05, (0, 0, 1.13), "Z", 24, bevel=0.005)
    slab("gusset", body, [(-0.16, 1.16), (0.16, 1.16), (0.03, 1.115), (-0.03, 1.115)], -0.012, 0.012, bevel=(0.004, 2))
    box("crossbar", body, (0.42, 0.05, 0.045), (0, 0, 1.18), 0.012, 3)
    for k in range(9):
        x = -0.18 + 0.045 * k
        path = spline([(x, 0.0, 1.18), (x, 0.05, 1.18), (x, 0.085, 1.15), (x, 0.095, 1.12)], 3)
        tube("tine", body, path, 0.008, 6, reference=Vector((1, 0, 0)))
        ellipsoid("tip", body, (x, 0.095, 1.12), (0.009, 0.009, 0.009), 8, 4)


def build_teapot(size):
    body, accent, dots = "teal_Classic", "yellow_Classic", "white_Classic"
    lathe("pot", body, [(0, 0.0), (0.06, 0.0), (0.07, 0.006), (0.074, 0.016), (0.095, 0.04), (0.108, 0.07), (0.11, 0.095), (0.105, 0.125),
                        (0.09, 0.155), (0.072, 0.18), (0.064, 0.192), (0.064, 0.2), (0, 0.2)], segments=40, sharp=50)
    torus("foot", accent, 0.07, 0.007, (0, 0, 0.007), "Z", 32, 6)
    torus("band", accent, 0.104, 0.008, (0, 0, 0.095), "Z", 40, 6)
    lathe("lid", body, [(0, 0.2), (0.068, 0.2), (0.07, 0.204), (0.066, 0.212), (0.05, 0.225), (0.025, 0.232), (0, 0.234)], segments=36, sharp=50)
    cylinder("stem", accent, 0.01, 0.012, (0, 0, 0.238), "Z", 16, bevel=0.002)
    ellipsoid("knob", accent, (0, 0, 0.255), (0.02, 0.02, 0.017), 16, 8)
    tube("spout", body, spline([(0.09, 0, 0.07), (0.13, 0, 0.09), (0.155, 0, 0.13), (0.168, 0, 0.17)], 4), 0.017, 14, reference=Vector((0, 1, 0)))
    torus("spout_tip", accent, 0.018, 0.005, (0.168, 0, 0.172), "Z", 20, 6)
    tube("handle", body, spline([(-0.1, 0, 0.165), (-0.15, 0, 0.175), (-0.18, 0, 0.13), (-0.16, 0, 0.075), (-0.105, 0, 0.06)], 5), 0.014, 12, reference=Vector((0, 1, 0)))
    for degrees in (45, 90, 135, 225, 270, 315):
        a = math.radians(degrees)
        ellipsoid("dot", dots, (0.106 * math.cos(a), 0.106 * math.sin(a), 0.115), (0.014, 0.005, 0.014), 10, 5, (0, 0, a - math.pi / 2))


def build_lid(size):
    body, lip, metal, accent, hole = "purple_Classic", "gold_Classic", "metal_Classic", "lime_Classic", "dark_Classic"
    centre = 0.21
    dome = [(0, 0.11), (0.05, 0.105), (0.1, 0.088), (0.14, 0.064), (0.17, 0.038), (0.188, 0.018), (0.197, 0.008), (0.2, 0.004)]
    lathe("dome", body, densify(dome, 2) + [(0.2, -0.004), (0, -0.004)], (0, 0, centre), "Y", 48, sharp=45)
    torus("lip", lip, 0.2, 0.011, (0, 0.0, centre), "Y", 48, 6)
    torus("ridge", body, 0.12, 0.005, (0, 0.076, centre), "Y", 40, 5)
    vent = (0.085 * math.cos(math.radians(50)), 0.085 * math.sin(math.radians(50)))
    torus("vent_ring", metal, 0.011, 0.0035, (vent[0], 0.093, centre + vent[1]), "Y", 18, 6)
    ellipsoid("vent", hole, (vent[0], 0.0935, centre + vent[1]), (0.008, 0.004, 0.008), 12, 6)
    cylinder("collar", metal, 0.022, 0.02, (0, 0.118, centre), "Y", 24, bevel=0.003)
    ellipsoid("knob", accent, (0, 0.15, centre), (0.042, 0.03, 0.042), 20, 10)
    ellipsoid("knob_top", accent, (0, 0.178, centre), (0.018, 0.008, 0.018), 16, 6)
    tube("handle", metal, spline([(-0.08, -0.004, centre), (-0.065, -0.04, centre), (0, -0.055, centre), (0.065, -0.04, centre), (0.08, -0.004, centre)], 5),
         0.011, 10, reference=Vector((0, 0, 1)))
    for side in (-1, 1):
        box("mount", metal, (0.034, 0.008, 0.03), (side * 0.08, -0.008, centre), 0.003, 2)


def build_tomato(size):
    body, accent, shine = "scarlet_Classic", "lime_Classic", "white_Classic"
    rx, rz, cz, dimple = 0.106, 0.08, 0.08, 0.014
    pumpkin("fruit", body, (0, 0, cz), (rx, rx, rz), lobes=5, groove=0.14, dimple=dimple, segments=48, rings=24)

    def surface(r):
        v = math.asin(min(1.0, r / rx))
        return cz + rz * math.cos(v) - dimple * math.exp(-(v / 0.4) ** 2)

    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 5
        c, s = math.cos(a), math.sin(a)
        path = spline([(r * c, r * s, surface(r) + lift) for r, lift in ((0.0, 0.006), (0.02, 0.006), (0.04, 0.005), (0.058, 0.004), (0.07, 0.013))], 4)
        taper("sepal", accent, path, interpolate([0.011, 0.012, 0.009, 0.006, 0.0025], len(path)), 8, Vector((0, 0, 1)))
    top = surface(0)
    ellipsoid("calyx", accent, (0, 0, top + 0.006), (0.021, 0.021, 0.009), 16, 8)
    tube("stem", accent, spline([(0, 0, top + 0.008), (0.002, 0, top + 0.026), (0.011, 0, top + 0.037)], 4), 0.0065, 10, reference=Vector((0, 1, 0)))
    ellipsoid("stem_tip", accent, (0.012, 0, top + 0.038), (0.008, 0.007, 0.006), 10, 5)
    for u, v, long, tall in ((math.radians(144), 0.72, 0.024, 0.008), (math.radians(150), 1.02, 0.007, 0.006)):
        at = (rx * 0.985 * math.sin(v) * math.cos(u), rx * 0.985 * math.sin(v) * math.sin(u), cz + rz * 0.985 * math.cos(v))
        ellipsoid("shine", shine, at, (long, 0.003, tall), 14, 6, (v + math.pi / 2, 0, u + math.pi / 2))


def build_rocket(size):
    body, accent, nose, metal, glass = "yellow_Classic", "navy_Classic", "scarlet_Classic", "metal_Classic", "glass_Classic"
    lathe("nozzle", metal, [(0, 0.0), (0.042, 0.0), (0.044, 0.004), (0.034, 0.03), (0.028, 0.05), (0.03, 0.06), (0, 0.06)], segments=32, sharp=40)
    lathe("hull", body, [(0, 0.055), (0.046, 0.055), (0.05, 0.065), (0.056, 0.1), (0.058, 0.2), (0.058, 0.36), (0.056, 0.4), (0, 0.4)], segments=36, sharp=40)
    lathe("nose", nose, [(0, 0.4), (0.058, 0.4), (0.057, 0.43), (0.052, 0.47), (0.043, 0.51), (0.03, 0.55), (0.016, 0.58), (0, 0.6)], segments=36, sharp=60)
    for z in (0.12, 0.385):
        torus("band", accent, 0.058, 0.008, (0, 0, z), "Z", 36, 6)
    fin = [(0.05, 0.04), (0.1, 0.0), (0.1, 0.03), (0.058, 0.17)]
    for k in range(4):
        part = slab("fin", accent, fin, -0.006, 0.006, bevel=(0.002, 2))
        move(part, (0, 0, math.pi / 2 * k))
    torus("port_rim", metal, 0.022, 0.006, (0, 0.058, 0.29), "Y", 24, 6)
    ellipsoid("port", glass, (0, 0.056, 0.29), (0.019, 0.008, 0.019), 16, 8)


def build_marble(size):
    pouch, cord = "purple_Classic", "yellow_Classic"
    gathered("pouch", pouch, densify([(0, 0.0), (0.06, 0.0), (0.095, 0.015), (0.118, 0.045), (0.125, 0.085), (0.118, 0.125), (0.095, 0.16), (0.065, 0.19),
                                      (0.05, 0.205), (0.052, 0.215), (0.07, 0.235), (0.082, 0.245), (0.078, 0.252), (0.06, 0.245), (0.045, 0.225), (0, 0.215)], 2),
             folds=12, amount=0.04, segments=48)
    torus("drawstring", cord, 0.053, 0.009, (0, 0, 0.205), "Z", 32, 8)
    for side in (-1, 1):
        tube("cord", cord, spline([(side * 0.05, 0.02, 0.205), (side * 0.075, 0.045, 0.18), (side * 0.09, 0.06, 0.13)], 4), 0.006, 8, reference=Vector((1, 0, 0)))
        ellipsoid("bead", cord, (side * 0.09, 0.06, 0.125), (0.014, 0.014, 0.014), 12, 6)
    for at, glass, swirl, tilt in (((-0.025, 0.01, 0.258), "teal_Classic", cord, 0.4),
                                   ((0.028, -0.012, 0.25), "scarlet_Classic", cord, -0.6),
                                   ((0.0, 0.03, 0.236), cord, pouch, 1.1),
                                   ((0.005, -0.03, 0.24), "lime_Classic", cord, -1.3)):
        ellipsoid("marble", glass, at, (0.033, 0.033, 0.033), 16, 8)
        part = torus("swirl", swirl, 0.031, 0.004, (0, 0, 0), "Z", 20, 5)
        move(part, (tilt, 0.5, tilt * 2), at)


def build_ladder(size):
    rail, accent, metal = "blue_Classic", "yellow_Classic", "metal_Classic"
    top, foot, height = 0.04, 0.28, 1.04
    length = math.hypot(foot - top, height)
    lean = math.atan2(foot - top, height)
    for side in (-1, 1):
        x = side * 0.25
        box("front_rail", rail, (0.045, 0.05, length), (x, (top + foot) / 2, height / 2), 0.01, 3, (lean, 0, 0))
        box("back_rail", rail, (0.045, 0.05, length), (x, -(top + foot) / 2, height / 2), 0.01, 3, (-lean, 0, 0))
        box("foot", accent, (0.06, 0.07, 0.03), (x, 0.285, 0.015), 0.008, 2)
        box("foot", accent, (0.06, 0.07, 0.03), (x, -0.285, 0.015), 0.008, 2)
        cylinder("hinge", metal, 0.015, 0.04, (x, 0.03, 1.035), "X", 16, bevel=0.003)
        cylinder("hinge", metal, 0.015, 0.04, (x, -0.03, 1.035), "X", 16, bevel=0.003)
        y = foot - (foot - top) * 0.55 / height
        tube("spreader", metal, spline([(x, y, 0.55), (x, 0.0, 0.52), (x, -y, 0.55)], 4), 0.006, 8)
    for z in (0.27, 0.53, 0.79):
        y = foot - (foot - top) * z / height
        box("tread", accent, (0.45, 0.1, 0.03), (0, y - 0.01, z), 0.008, 2)
    for z in (0.3, 0.7):
        y = foot - (foot - top) * z / height
        box("brace", rail, (0.45, 0.03, 0.03), (0, -y, z), 0.006, 2)
    box("platform", accent, (0.5, 0.16, 0.04), (0, 0, 1.06), 0.012, 3)


def build_paddle(size):
    wood, front, back, edge, logo = "wood_light_Classic", "scarlet_Classic", "navy_Classic", "white_Classic", "yellow_Classic"
    slab("face_front", [wood, front], ellipse(0.15, 0.15, 0, 0.37, 48), 0.0, 0.012, bevel=(0.002, 1))
    slab("face_back", [wood, back], ellipse(0.15, 0.15, 0, 0.37, 48), -0.012, 0.0, bevel=(0.002, 1))
    for y in (0.012, -0.012):
        torus("edge_line", edge, 0.141, 0.0022, (0, y, 0.37), "Y", 48, 4)
    move(prism("logo", logo, star(5, 0.022, 0.009), 0.011, 0.015, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (0, 0, 0.27))
    slab("throat", wood, [(-0.022, 0.17), (0.022, 0.17), (0.03, 0.19), (0.05, 0.228), (-0.05, 0.228), (-0.03, 0.19)], -0.012, 0.012, bevel=(0.004, 3))
    path = spline([(0, 0, 0.012), (0, 0, 0.1), (0, 0, 0.17), (0, 0, 0.2)], 4)
    taper("handle", wood, path, interpolate([0.021, 0.0225, 0.0225, 0.019], len(path)), 16, Vector((0, 1, 0)))
    turns, count = 5, 60
    wrap = [(0.0245 * math.cos(2 * math.pi * turns * i / count), 0.0245 * math.sin(2 * math.pi * turns * i / count), 0.03 + 0.12 * i / count) for i in range(count + 1)]
    tube("wrap", front, wrap, 0.0045, 6)
    cylinder("end_cap", front, 0.027, 0.022, (0, 0, 0.011), "Z", 24, bevel=0.005)


def build_chili(size):
    body, accent = "scarlet_Classic", "lime_Classic"
    path = spline([(-0.12, 0, 0.075), (-0.05, 0, 0.07), (0.03, 0, 0.05), (0.1, 0, 0.028), (0.145, 0, 0.025), (0.16, 0, 0.035)], 5)
    taper("pepper", body, path, interpolate([0.045, 0.044, 0.036, 0.024, 0.01, 0.002], len(path)), 16, Vector((0, 1, 0)))
    ellipsoid("calyx", accent, (-0.125, 0, 0.075), (0.02, 0.05, 0.05), 16, 8)
    for k in range(5):
        a = 2 * math.pi * k / 5
        ellipsoid("sepal", accent, (-0.11, 0.035 * math.cos(a), 0.075 + 0.035 * math.sin(a)), (0.018, 0.012, 0.012), 10, 5)
    tube("stem", accent, spline([(-0.135, 0, 0.08), (-0.155, 0, 0.1), (-0.16, 0, 0.125)], 4), 0.008, 10, reference=Vector((0, 1, 0)))


def build_disc(size):
    body, accent, mark = "orange_Classic", "navy_Classic", "white_Classic"
    lathe("disc", body, [(0, 0.044), (0.06, 0.045), (0.11, 0.046), (0.135, 0.047), (0.146, 0.044), (0.15, 0.036), (0.149, 0.02), (0.144, 0.006),
                         (0.138, 0.0), (0.132, 0.003), (0.13, 0.02), (0.124, 0.031), (0.1, 0.034), (0.05, 0.035), (0, 0.035)], segments=56, sharp=55)
    torus("ring", accent, 0.07, 0.003, (0, 0, 0.0455), "Z", 40, 6)
    torus("ring", accent, 0.115, 0.0035, (0, 0, 0.0465), "Z", 48, 6)
    prism("star", mark, star(5, 0.04, 0.017), 0.043, 0.0475, bevel=(0.001, 1))


def build_helmet(size):
    shell, crest, trim, visor, slit = "scarlet_Classic", "teal_Classic", "yellow_Classic", "metal_Classic", "dark_Classic"
    lathe("shell", shell, [(0, 0.0), (0.128, 0.0), (0.142, 0.008), (0.15, 0.04), (0.153, 0.11), (0.15, 0.17), (0.139, 0.225), (0.118, 0.268),
                           (0.088, 0.3), (0.052, 0.32), (0, 0.328)], segments=40, sharp=50)
    torus("brim", trim, 0.146, 0.011, (0, 0, 0.014), "Z", 40, 8)
    for k in range(10):
        a = 2 * math.pi * k / 10 + math.pi / 10
        ellipsoid("rivet", trim, (0.157 * math.cos(a), 0.157 * math.sin(a), 0.014), (0.008, 0.008, 0.008), 10, 5)
    slab("visor", visor, rounded_rect(0.2, 0.13, 0.02, 0.04, 0.1, 6), 0.095, 0.162, bevel=(0.004, 2))
    box("slit", slit, (0.15, 0.012, 0.02), (0, 0.164, 0.19), 0.004, 2)
    for x in (-0.05, -0.025, 0.0, 0.025, 0.05):
        ellipsoid("vent", slit, (x, 0.162, 0.13), (0.006, 0.004, 0.012), 8, 4)
    outline = [(-0.15, 0.27), (-0.11, 0.36), (-0.03, 0.41), (0.05, 0.405), (0.11, 0.37), (0.14, 0.31),
               (0.12, 0.26), (0.08, 0.3), (0.0, 0.322), (-0.08, 0.3), (-0.13, 0.25)]
    move(slab("crest", crest, outline, -0.016, 0.016, bevel=(0.005, 2)), (0, 0, math.pi / 2))
    for side in (-1, 1):
        move(prism("badge", trim, star(5, 0.032, 0.014), 0.14, 0.158, bevel=(0.002, 1)), (0, side * math.pi / 2, 0), (0, 0, 0.17))
    tube("strap", slit, spline([(-0.07, -0.13, 0.05), (-0.04, -0.185, 0.075), (0.04, -0.185, 0.075), (0.07, -0.13, 0.05)], 4), 0.009, 8)


def build_lemon(size):
    peel, leaf, label, ink = "yellow_Classic", "emerald_Classic", "white_Classic", "navy_Classic"
    lemon = [(0, -0.088), (0.006, -0.085), (0.011, -0.079), (0.022, -0.07), (0.036, -0.056), (0.047, -0.036), (0.053, -0.014),
             (0.055, 0.006), (0.053, 0.026), (0.046, 0.046), (0.034, 0.061), (0.02, 0.072), (0.01, 0.08), (0.005, 0.085), (0, 0.088)]
    lathe("lemon", peel, densify(lemon, 2), (0, 0, 0.055), "X", 36)
    cylinder("stem", leaf, 0.006, 0.014, (0.093, 0, 0.055), "X", 10, bevel=0)
    ellipsoid("leaf", leaf, (0.09, 0.014, 0.075), (0.028, 0.012, 0.0045), 14, 6, (0.3, 0.7, 0.4))
    tilt = math.radians(30)
    for mat, radial, radii in ((ink, 0.051, (0.021, 0.0045, 0.015)), (label, 0.0515, (0.017, 0.005, 0.012))):
        ellipsoid("sticker", mat, (-0.012, radial * math.cos(tilt), 0.055 + radial * math.sin(tilt)), radii, 16, 6, (tilt, 0, 0))


def build_torch(size):
    body, accent, rubber, lens = "cobalt_Classic", "yellow_Classic", "dark_Classic", "glow_Classic"
    cylinder("tail", rubber, 0.036, 0.024, (0, 0, 0.012), "Z", 24, bevel=0.005)
    cylinder("body", body, 0.033, 0.3, (0, 0, 0.17), "Z", 28, bevel=0.004)
    for z in (0.06, 0.09, 0.12, 0.15):
        torus("grip_ring", rubber, 0.033, 0.006, (0, 0, z), "Z", 28, 6)
    box("switch_plate", accent, (0.03, 0.016, 0.05), (0, 0.033, 0.245), 0.006, 2)
    ellipsoid("switch", rubber, (0, 0.043, 0.245), (0.009, 0.006, 0.014), 12, 6)
    lathe("neck", body, [(0, 0.318), (0.033, 0.318), (0.036, 0.33), (0.05, 0.37), (0.062, 0.4), (0, 0.4)], segments=36, sharp=40)
    cylinder("bezel", accent, 0.066, 0.036, (0, 0, 0.415), "Z", 36, bevel=0.005)
    cylinder("lens", lens, 0.053, 0.008, (0, 0, 0.434), "Z", 32, bevel=0)
    torus("lens_ring", accent, 0.056, 0.005, (0, 0, 0.434), "Z", 36, 6)
    torus("strap", rubber, 0.022, 0.005, (0, 0, -0.016), "X", 20, 6)


def build_carpet(size):
    rug, border, motif, fringe = "scarlet_Classic", "yellow_Classic", "teal_Classic", "white_Classic"
    width, thick, roll = 0.66, 0.02, 0.06
    box("rug", rug, (width, 1.42, thick), (0, 0.03, thick / 2), 0.004, 2)
    for side in (-1, 1):
        box("border", border, (0.05, 1.36, 0.006), (side * (width / 2 - 0.045), 0.04, thick + 0.002), 0.002, 1)
    for y in (-0.62, 0.7):
        box("border", border, (width - 0.04, 0.05, 0.006), (0, y, thick + 0.002), 0.002, 1)
    for y in (-0.33, 0.03, 0.39):
        move(prism("diamond", motif, [(0, -0.11), (0.11, 0), (0, 0.11), (-0.11, 0)], thick, thick + 0.006, bevel=(0.002, 1)), at=(0, y, 0))
        move(prism("diamond_dot", border, [(0, -0.035), (0.035, 0), (0, 0.035), (-0.035, 0)], thick + 0.006, thick + 0.01, bevel=(0.001, 1)), at=(0, y, 0))
    cylinder("roll", rug, roll, width, (0, -0.72, roll), "X", 28, bevel=0.004)
    for side in (-1, 1):
        torus("roll_edge", border, 0.04, 0.006, (side * width / 2, -0.72, roll), "X", 24, 6)
        torus("roll_core", border, 0.018, 0.005, (side * width / 2, -0.72, roll), "X", 16, 6)
    for k in range(11):
        cylinder("tassel", fringe, 0.008, 0.06, (-0.3 + 0.06 * k, 0.77, 0.008), "Y", 8, bevel=0)


def build_slime(size):
    goo, tub, label = "lime_Classic", "magenta_Classic", "white_Classic"
    lathe("tub", tub, [(0, 0.0), (0.082, 0.0), (0.088, 0.006), (0.094, 0.1), (0.098, 0.112)], segments=40, sharp=40)
    torus("rim", tub, 0.097, 0.007, (0, 0, 0.112), "Z", 40, 6)
    lathe("label", label, [(0.0905, 0.033), (0.0935, 0.035), (0.0965, 0.08), (0.0935, 0.082), (0.0905, 0.082)], segments=40)
    move(prism("logo", goo, star(8, 0.022, 0.012), 0.093, 0.1, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (0, 0, 0.0575))
    lobed("goo", goo, (0, 0, 0.122), (0.1, 0.1, 0.045), lobes=7, amount=0.12, segments=36, rings=14)
    for degrees, length in ((15, 0.06), (135, 0.045), (200, 0.075), (255, 0.05), (320, 0.035)):
        a = math.radians(degrees)
        c, s = math.cos(a), math.sin(a)
        path = spline([(0.104 * c, 0.104 * s, 0.118), (0.101 * c, 0.101 * s, 0.1), (0.098 * c, 0.098 * s, 0.11 - length)], 4)
        taper("drip", goo, path, interpolate([0.016, 0.012, 0.01], len(path)), 12, Vector((c, s, 0)))
        ellipsoid("drop", goo, (0.098 * c, 0.098 * s, 0.11 - length), (0.013, 0.013, 0.015), 12, 6)
    lobed("puddle", goo, (0, 0, 0.006), (0.15, 0.15, 0.008), lobes=9, amount=0.1, segments=36, rings=6)
    for at, r in (((0.03, -0.02, 0.165), 0.014), ((-0.04, 0.03, 0.158), 0.01), ((0.05, 0.045, 0.15), 0.008)):
        ellipsoid("bubble", label, at, (r, r, r), 12, 6)


def build_spoon(size):
    body, hollow, accent, loop = "teal_Classic", "soap_Classic", "yellow_Classic", "navy_Classic"
    path = spline([(0, 0, 0.03), (0, 0, 0.2), (0, 0, 0.36), (0, 0.004, 0.42)], 4)
    taper("handle", body, path, interpolate([0.019, 0.018, 0.014, 0.012, 0.016], len(path)), 16, Vector((0, 1, 0)))
    ellipsoid("bowl", body, (0, 0, 0.5), (0.068, 0.028, 0.095), 28, 14)
    ellipsoid("hollow", hollow, (0, 0.012, 0.505), (0.055, 0.02, 0.078), 24, 12)
    move(prism("star", accent, star(5, 0.03, 0.013), 0.024, 0.031, bevel=(0.001, 1)), (math.pi / 2, 0, 0), (0, 0, 0.5))
    for z in (0.07, 0.11, 0.15):
        torus("band", accent, 0.019, 0.005, (0, 0, z), "Z", 20, 6)
    ellipsoid("knob", accent, (0, 0, 0.03), (0.024, 0.024, 0.024), 16, 8)
    torus("loop", loop, 0.012, 0.005, (0, 0, -0.006), "Y", 16, 6)


def build_tent(size):
    canvas, trim, door, flag = "orange_Classic", "yellow_Classic", "dark_Classic", "teal_Classic"
    half, height, depth = 0.3, 0.42, 0.28
    slab("canvas", canvas, [(-half, 0), (half, 0), (0, height)], -depth, depth, bevel=(0.01, 2))
    slab("door", door, [(-0.12, 0.0), (0.12, 0.0), (0, 0.27)], depth - 0.002, depth + 0.006)
    for side in (-1, 1):
        tube("piping", trim, [(side * 0.12, depth + 0.008, 0.004), (0, depth + 0.008, 0.27)], 0.009, 8)
        ellipsoid("tie", trim, (side * 0.062, depth + 0.012, 0.135), (0.014, 0.01, 0.014), 10, 5)
    lean = math.atan2(height, half)
    normal = (math.sin(lean), math.cos(lean))
    for t in (0.12, 0.6):
        x, z = half * (1 - t) + normal[0] * 0.004, height * t + normal[1] * 0.004
        for side in (-1, 1):
            box("stripe", trim, (0.05, 2 * depth - 0.02, 0.008), (side * x, 0, z), 0.002, 1, (0, side * lean, 0))
    cylinder("ridge", trim, 0.013, 2 * depth + 0.06, (0, 0, height), "Y", 14, bevel=0)
    cylinder("flagpole", trim, 0.007, 0.13, (0, depth + 0.02, height + 0.06), "Z", 8, bevel=0)
    move(slab("flag", flag, [(0.0, 0.0), (0.09, 0.025), (0.0, 0.05)], -0.004, 0.004), at=(0.007, depth + 0.02, height + 0.07))
    slab("window", flag, ellipse(0.05, 0.05, 0, 0.2, 20), -depth - 0.006, -depth + 0.002)
    for x in (-1, 1):
        for y in (-1, 1):
            box("peg", trim, (0.025, 0.025, 0.03), (x * 0.33, y * 0.25, 0.015), 0.006, 2)


def build_pear(size):
    skin, leaf, stem, border, label = "lime_Classic", "emerald_Classic", "wood_dark_Classic", "scarlet_Classic", "ivory_Classic"
    pear = [(0, 0.0), (0.03, 0.002), (0.05, 0.012), (0.062, 0.03), (0.066, 0.05), (0.062, 0.072), (0.05, 0.092), (0.037, 0.11),
            (0.031, 0.128), (0.028, 0.146), (0.022, 0.162), (0.012, 0.172), (0, 0.175)]
    lathe("pear", skin, densify(pear, 2), segments=40)
    ellipsoid("stem_cap", stem, (0, 0, 0.172), (0.01, 0.01, 0.006), 12, 6)
    tube("stem", stem, spline([(0, 0, 0.168), (0.004, 0, 0.19), (0.014, 0, 0.206)], 4), 0.006, 10, reference=Vector((0, 1, 0)))
    ellipsoid("leaf", leaf, (0.03, 0.006, 0.196), (0.026, 0.011, 0.004), 14, 6, (0.2, -0.45, 0.25))
    ellipsoid("sticker_rim", border, (0, 0.061, 0.05), (0.021, 0.0055, 0.017), 16, 6)
    ellipsoid("sticker", label, (0, 0.0615, 0.05), (0.017, 0.006, 0.013), 16, 6)
    ellipsoid("sticker_dot", border, (0, 0.0672, 0.05), (0.005, 0.002, 0.005), 10, 5)


def build_sock(size):
    knit, contrast, cuff, patch = "magenta_Classic", "yellow_Classic", "white_Classic", "teal_Classic"
    path = spline([(-0.055, 0, 0.3), (-0.045, 0, 0.22), (-0.05, 0, 0.14), (-0.045, 0, 0.075), (-0.02, 0, 0.042), (0.04, 0, 0.036), (0.1, 0, 0.034),
                   (0.14, 0, 0.036)], 5)
    taper("sock", knit, path, interpolate([0.042, 0.04, 0.039, 0.037, 0.04, 0.038, 0.035, 0.03], len(path)), 16, Vector((0, 1, 0)))
    ellipsoid("heel", contrast, (-0.058, 0, 0.047), (0.03, 0.036, 0.034), 16, 8)
    ellipsoid("toe", contrast, (0.142, 0, 0.036), (0.036, 0.032, 0.034), 16, 8)
    cylinder("cuff", cuff, 0.047, 0.045, (-0.055, 0, 0.3025), "Z", 20, bevel=0)
    torus("fold", cuff, 0.047, 0.008, (-0.055, 0, 0.278), "Z", 20, 5)
    for k in range(12):
        a = 2 * math.pi * k / 12
        cylinder("rib", knit, 0.0035, 0.04, (-0.055 + 0.047 * math.cos(a), 0.047 * math.sin(a), 0.303), "Z", 6, bevel=0)
    for z in (0.24, 0.21, 0.12):
        x = -0.047 if z > 0.15 else -0.049
        torus("stripe", contrast, 0.0395, 0.006, (x, 0, z), "Z", 20, 4)
    move(prism("patch", patch, star(5, 0.02, 0.009), 0.035, 0.043, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (-0.048, 0, 0.165))


def build_hose(size):
    hose, nozzle, metal, water = "emerald_Classic", "yellow_Classic", "metal_Classic", "water_Classic"
    radius, thick, turns, pitch = 0.115, 0.021, 2.6, 0.044
    end = math.pi / 2
    start = end - 2 * math.pi * turns
    steps = int(turns * 24)
    points = []
    for i in range(steps + 1):
        a = start + (end - start) * i / steps
        points.append((radius * math.cos(a), radius * math.sin(a), thick + pitch * (a - start) / (2 * math.pi)))
    top = points[-1][2]
    points += [(0, 0.14, top), (0, 0.165, top)]
    tube("hose", hose, points, thick, 10)
    ellipsoid("inlet", metal, points[0], (0.026, 0.026, 0.026), 14, 7)
    torus("coupling", metal, thick + 0.002, 0.007, (0, 0.175, top), "Y", 20, 6)
    cylinder("nozzle", nozzle, 0.024, 0.08, (0, 0.215, top), "Y", 20, bevel=0.004)
    cylinder("tip", nozzle, 0.018, 0.03, (0, 0.27, top), "Y", 18, bevel=0.003, top=0.012)
    torus("tip_ring", metal, 0.016, 0.004, (0, 0.285, top), "Y", 16, 5)
    box("trigger", nozzle, (0.02, 0.05, 0.06), (0, 0.2, top - 0.05), 0.006, 2)
    for at, r in (((0, 0.31, top + 0.005), (0.014, 0.02, 0.014)), ((0.012, 0.345, top + 0.012), (0.011, 0.015, 0.011)),
                  ((-0.01, 0.37, top), (0.009, 0.012, 0.009))):
        ellipsoid("spray", water, at, r, 12, 6)


def build_racket(size):
    frame, strings, wrap, trim = "magenta_Classic", "lime_Classic", "yellow_Classic", "navy_Classic"
    centre, rx, rz = 0.5, 0.125, 0.16
    torus("frame", frame, (rx, rz), 0.014, (0, 0, centre), "Y", 64, 10)
    for k in range(7):
        x = -0.09 + 0.03 * k
        box("main", strings, (0.005, 0.004, 2 * rz * math.sqrt(1 - (x / rx) ** 2)), (x, 0.0015, centre), 0)
    for k in range(9):
        dz = -0.128 + 0.032 * k
        box("cross", strings, (2 * rx * math.sqrt(1 - (dz / rz) ** 2), 0.004, 0.005), (0, -0.0015, centre + dz), 0)
    for side in (-1, 1):
        joint = centre - rz * math.sqrt(1 - (0.07 / rx) ** 2)
        tube("arm", frame, spline([(side * 0.07, 0, joint + 0.004), (side * 0.046, 0, 0.322), (side * 0.014, 0, 0.276)], 4), 0.012, 10,
             reference=Vector((0, 1, 0)))
    slab("throat", trim, [(-0.012, 0.284), (0.012, 0.284), (0.047, 0.341), (0.0, 0.329), (-0.047, 0.341)], -0.006, 0.006, bevel=(0.002, 1))
    move(prism("badge", wrap, star(5, 0.016, 0.007), 0.006, 0.009, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (0, 0, 0.307))
    path = spline([(0, 0, 0.012), (0, 0, 0.1), (0, 0, 0.2), (0, 0, 0.282)], 4)
    taper("handle", frame, path, interpolate([0.0175, 0.019, 0.0185, 0.0155], len(path)), 16, Vector((0, 1, 0)))
    turns, count = 7, 84
    tube("wrap", wrap, [(0.0198 * math.cos(2 * math.pi * turns * i / count), 0.0198 * math.sin(2 * math.pi * turns * i / count), 0.03 + 0.2 * i / count)
                        for i in range(count + 1)], 0.0042, 6)
    torus("collar", trim, 0.0175, 0.005, (0, 0, 0.268), "Z", 24, 6)
    cylinder("butt", trim, 0.023, 0.024, (0, 0, 0.012), "Z", 24, bevel=0.005)


def build_melon(size):
    skin, stripe, rind, flesh, seed = "lime_Classic", "emerald_Classic", "white_Classic", "scarlet_Classic", "dark_Classic"
    a, b, cut = 0.15, 0.105, 0.06
    body = [(b * math.sqrt(max(0.0, 1 - (t / a) ** 2)), t) for t in (-0.15, -0.146, -0.136, -0.118, -0.09, -0.055, -0.02, 0.02, 0.045, cut)]
    banded("melon", [skin, stripe], densify(body, 2) + [(0, cut)], (0, 0, b), "Y", 42, period=6, stripe=2, wave=0.1, waves=1.5)
    lathe("rind", rind, [(0.0915, cut - 0.002), (0.0915, cut + 0.005), (0.089, cut + 0.0062), (0.074, cut + 0.0062), (0.074, cut + 0.003)], (0, 0, b), "Y", 42)
    lathe("flesh", flesh, [(0.075, cut + 0.004), (0.075, cut + 0.008), (0.06, cut + 0.012), (0.035, cut + 0.0145), (0, cut + 0.0155)], (0, 0, b), "Y", 36)
    for count, r, h in ((7, 0.05, 0.0118), (4, 0.025, 0.0148)):
        for k in range(count):
            phi = 2 * math.pi * k / count + (0.3 if count == 4 else 0.0)
            ellipsoid("seed", seed, (r * math.cos(phi), cut + h + 0.0012, b + r * math.sin(phi)), (0.0045, 0.0024, 0.0085), 6, 4, (0, math.pi / 2 - phi, 0))
    tube("stem", stripe, spline([(0, -a + 0.004, b), (0.003, -a - 0.012, b + 0.004), (0.012, -a - 0.02, b + 0.014), (0.02, -a - 0.016, b + 0.022)], 3), 0.006, 8,
         reference=Vector((0, 0, 1)))


def build_piano(size):
    case, keys, black, sheet, trim = "scarlet_Classic", "white_Classic", "dark_Classic", "ivory_Classic", "teal_Classic"
    box("case", case, (0.5, 0.16, 0.44), (0, -0.07, 0.27), 0.01, 3)
    box("lid", case, (0.56, 0.2, 0.03), (0, -0.065, 0.505), 0.008, 2)
    box("lid_trim", trim, (0.56, 0.008, 0.012), (0, 0.036, 0.505), 0.002, 1)
    for side in (-1, 1):
        box("cheek", case, (0.03, 0.3, 0.29), (side * 0.265, 0, 0.195), 0.008, 2)
        for y in (-0.12, 0.12):
            ellipsoid("foot", trim, (side * 0.265, y, 0.025), (0.026, 0.026, 0.025), 12, 6)
        turn = math.pi if side > 0 else 0.0
        move(prism("star", trim, star(5, 0.034, 0.014, turn), 0.25, 0.254, bevel=(0.001, 1)), (0, side * math.pi / 2, 0), (0, -0.07, 0.41))
    box("keybed", case, (0.5, 0.15, 0.05), (0, 0.075, 0.205), 0.006, 2)
    box("key_rail", case, (0.5, 0.02, 0.035), (0, 0.02, 0.255), 0.004, 2)
    for k in range(12):
        box("key", keys, (0.036, 0.12, 0.022), (-0.22 + 0.04 * k, 0.088, 0.241), 0.003, 1)
    for k in (0, 1, 3, 4, 5, 7, 8, 10):
        box("black_key", black, (0.022, 0.07, 0.018), (-0.2 + 0.04 * k, 0.065, 0.26), 0.003, 1)
    box("ledge", trim, (0.3, 0.035, 0.012), (0, 0.027, 0.31), 0.003, 1)
    slab("sheet", sheet, rounded_rect(0.2, 0.13, 0.008, 0.008, 0.316, 4), 0.012, 0.018)
    for x, z in ((-0.035, 0.352), (0.035, 0.37)):
        ellipsoid("note", black, (x, 0.0195, z), (0.016, 0.0025, 0.011), 14, 6, (0, 0.4, 0))
        box("stem", black, (0.005, 0.004, 0.07), (x + 0.0135, 0.0195, z + 0.035), 0)
    box("beam", black, (0.082, 0.004, 0.013), (0.0135, 0.0195, 0.4285), 0, rotation=(0, -math.atan2(0.018, 0.07), 0))


def build_tonic(size):
    glass, label, bolt, cork, gloss = "purple_Classic", "yellow_Classic", "scarlet_Classic", "wood_light_Classic", "white_Classic"
    lathe("bottle", glass, [(0, 0.0), (0.04, 0.0), (0.046, 0.003), (0.0495, 0.009), (0.051, 0.02), (0.051, 0.088), (0.0502, 0.096), (0.048, 0.104),
                            (0.0445, 0.111), (0.0395, 0.118), (0.0335, 0.125), (0.027, 0.131), (0.0215, 0.137), (0.018, 0.143), (0.017, 0.15),
                            (0.017, 0.168), (0.0205, 0.17), (0.021, 0.175), (0.0195, 0.179), (0.016, 0.18), (0, 0.18)], segments=32)
    lathe("label", label, [(0.0505, 0.032), (0.0528, 0.034), (0.0528, 0.083), (0.0505, 0.085)], segments=32)
    shape = [(-0.004, 0.023), (0.014, 0.023), (0.003, 0.003), (0.012, 0.003), (-0.011, -0.024), (-0.003, -0.005), (-0.013, -0.005)]
    move(prism("bolt", bolt, [(x, -z) for x, z in shape], 0.05, 0.056, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (0, 0, 0.0585))
    cylinder("cork", cork, 0.0165, 0.032, (0, 0, 0.19), "Z", 20, bevel=0.003, top=0.0185)
    torus("twine", label, 0.0182, 0.0028, (0, 0, 0.158), "Z", 20, 5)
    tube("tag_string", label, spline([(0.013, 0.012, 0.157), (0.024, 0.024, 0.146), (0.038, 0.038, 0.13)], 3), 0.0016, 5)
    move(slab("tag", gloss, rounded_rect(0.03, 0.022, 0.004, 0.004, -0.022, 3), -0.0015, 0.0015), (0, 0, -math.pi / 4), (0.04, 0.04, 0.131))
    u, s, r = math.radians(120), math.radians(42), 0.0468
    ellipsoid("gloss", gloss, (r * math.cos(u), r * math.sin(u), 0.106), (0.0032, 0.0013, 0.011), 10, 5, (s, 0, u - math.pi / 2))
    ellipsoid("gloss_dot", gloss, (0.0512 * math.cos(u), 0.0512 * math.sin(u), 0.0925), (0.0025, 0.0012, 0.0025), 8, 4, (0, 0, u - math.pi / 2))


def build_brick(size):
    plastic, face, ink = "cobalt_Classic", "yellow_Classic", "dark_Classic"
    box("brick", plastic, (0.32, 0.16, 0.115), (0, 0, 0.0575), 0.008, 3)
    for x in (-0.12, -0.04, 0.04, 0.12):
        for y in (-0.04, 0.04):
            cylinder("stud", plastic, 0.024, 0.032, (x, y, 0.129), "Z", 16, bevel=0.003)
    slab("smiley", face, ellipse(0.036, 0.036, 0, 0.058, 32), 0.079, 0.083, bevel=(0.0015, 1))
    for side in (-1, 1):
        ellipsoid("eye", ink, (side * 0.012, 0.0832, 0.068), (0.0055, 0.002, 0.0085), 10, 5)
    tube("smile", ink, [(0.021 * math.cos(math.radians(a)), 0.0836, 0.055 + 0.021 * math.sin(math.radians(a))) for a in range(205, 340, 15)], 0.0028, 6,
         reference=Vector((0, 1, 0)))


def build_pencil(size):
    body, wood, lead, metal, rubber = "yellow_Classic", "wood_light_Classic", "dark_Classic", "metal_Classic", "magenta_Classic"
    eraser = grip = band = rubber
    cylinder("eraser", eraser, 0.026, 0.05, (0, 0, 0.025), "Z", 24, bevel=0.008)
    cylinder("ferrule", metal, 0.029, 0.045, (0, 0, 0.07), "Z", 24, bevel=0.003)
    for z in (0.057, 0.07, 0.083):
        torus("crimp", metal, 0.029, 0.003, (0, 0, z), "Z", 24, 5)
    cylinder("body", body, 0.03, 0.52, (0, 0, 0.35), "Z", 6, bevel=0.004)
    for z, depth in ((0.548, 0.014), (0.568, 0.005)):
        cylinder("band", band, 0.0306, depth, (0, 0, z), "Z", 6, bevel=0)
    cylinder("wood", wood, 0.0262, 0.12, (0, 0, 0.67), "Z", 24, bevel=0, top=0.0082)
    cylinder("lead", lead, 0.0083, 0.03, (0, 0, 0.745), "Z", 16, bevel=0, top=0.0015)
    lathe("grip", grip, densify([(0.026, 0.12), (0.031, 0.122), (0.036, 0.135), (0.0375, 0.16), (0.035, 0.175), (0.0375, 0.19), (0.035, 0.205),
                                 (0.0375, 0.22), (0.036, 0.245), (0.031, 0.258), (0.026, 0.26)], 2), segments=24)


def build_ramp(size):
    deck, arrows, rail, sticker = "teal_Classic", "yellow_Classic", "metal_Classic", "scarlet_Classic"
    width, height, front, back, top = 0.4, 0.2, 0.3, -0.3, -0.27

    def h(u):
        return 0.012 + (height - 0.012) * ((front - u) / (front - top)) ** 1.7

    def slope(u):
        return -1.7 * (height - 0.012) * ((front - u) / (front - top)) ** 0.7 / (front - top)

    curve = [(front - (front - top) * k / 14, h(front - (front - top) * k / 14)) for k in range(15)]
    move(slab("ramp", deck, [(front, 0.0)] + curve + [(back, height), (back, 0.0)], -width / 2, width / 2, bevel=(0.006, 2)), (0, 0, math.pi / 2))
    cylinder("coping", rail, 0.013, width + 0.02, (0, -0.282, height), "X", 20, bevel=0.002)
    chevron = [(0, -0.055), (0.1, 0.02), (0.1, 0.05), (0, -0.02), (-0.1, 0.05), (-0.1, 0.02)]
    for c in (0.17, 0.03, -0.11):
        move(prism("arrow", arrows, chevron, -0.001, 0.004, bevel=(0.001, 1)), (math.atan(slope(c)), 0, 0), (0, c, h(c)))
    for side in (-1, 1):
        turn = math.pi if side > 0 else 0.0
        move(prism("sticker", sticker, star(5, 0.045, 0.019, turn), width / 2, width / 2 + 0.004, bevel=(0.001, 1)), (0, side * math.pi / 2, 0), (0, -0.165, 0.075))
        tube("edge", arrows, [(side * (width / 2 - 0.004), u, z + 0.004) for u, z in curve], 0.009, 8, reference=Vector((1, 0, 0)))


def build_oil(size):
    can, metal, band, logo, oil = "orange_Classic", "metal_Classic", "cobalt_Classic", "white_Classic", "yellow_Classic"
    lathe("can", can, [(0, 0.0), (0.068, 0.0), (0.075, 0.004), (0.078, 0.012), (0.078, 0.058), (0.076, 0.066), (0.07, 0.07), (0.06, 0.08), (0.045, 0.095),
                       (0.03, 0.11), (0.022, 0.12), (0.02, 0.125), (0, 0.125)], segments=40, sharp=50)
    torus("seam", metal, 0.071, 0.0045, (0, 0, 0.069), "Z", 40, 6)
    lathe("band", band, [(0.0778, 0.016), (0.0795, 0.018), (0.0795, 0.054), (0.0778, 0.056)], segments=40)
    drop = [(0.012 * math.sin(t) * math.sin(t / 2), 0.015 * math.cos(t)) for t in (2 * math.pi * k / 20 for k in range(20))]
    move(prism("logo", logo, [(x, -z) for x, z in drop], 0.077, 0.0815, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (0, 0, 0.036))
    cylinder("collar", metal, 0.021, 0.016, (0, 0, 0.128), "Z", 24, bevel=0.002)
    path = spline([(0, 0, 0.13), (0, 0.02, 0.165), (0, 0.07, 0.215), (0, 0.14, 0.252)], 4)
    taper("spout", metal, path, interpolate([0.012, 0.0095, 0.007, 0.0055], len(path)), 12, Vector((1, 0, 0)))
    taper("drip", oil, spline([(0, 0.142, 0.249), (0, 0.144, 0.238), (0, 0.145, 0.228)], 3), interpolate([0.0035, 0.004, 0.006], 7), 10, Vector((1, 0, 0)))
    ellipsoid("drop", oil, (0, 0.145, 0.222), (0.009, 0.009, 0.011), 12, 6)
    lobed("puddle", oil, (0, 0.13, 0.004), (0.045, 0.04, 0.004), lobes=6, amount=0.15, segments=28, rings=5)
    tube("handle", band, spline([(0, -0.075, 0.03), (0, -0.104, 0.05), (0, -0.108, 0.085), (0, -0.085, 0.108), (0, -0.03, 0.114)], 4), 0.0075, 10,
         reference=Vector((1, 0, 0)))


def build_door(size):
    slab_colour, panel, pane, brass, metal = "lime_Classic", "emerald_Classic", "cobalt_Classic", "yellow_Classic", "metal_Classic"
    width, height, thick = 0.5, 0.9, 0.06
    face = thick / 2
    box("door", slab_colour, (width, thick, height), (0, 0, height / 2), 0.012, 3)
    for x in (-0.115, 0.115):
        for y in (face, -face):
            box("panel", panel, (0.17, 0.012, 0.28), (x, y, 0.24), 0.005, 2)
    box("letterbox", metal, (0.2, 0.012, 0.045), (0, face + 0.002, 0.47), 0.004, 2)
    box("flap", metal, (0.17, 0.006, 0.02), (0, face + 0.009, 0.476), 0.002, 1)
    slab("pane", pane, ellipse(0.088, 0.088, 0, 0.7, 40), -face - 0.004, face + 0.004)
    for side in (1, -1):
        torus("window_ring", brass, 0.09, 0.012, (0, side * (face + 0.002), 0.7), "Y", 40, 6)
        box("bar", brass, (0.17, 0.008, 0.012), (0, side * (face + 0.006), 0.7), 0)
        box("bar", brass, (0.012, 0.008, 0.17), (0, side * (face + 0.006), 0.7), 0)
    cylinder("rose", brass, 0.022, 0.008, (0.18, face + 0.004, 0.44), "Y", 20, bevel=0.002)
    cylinder("knob_neck", brass, 0.008, 0.014, (0.18, face + 0.014, 0.44), "Y", 12, bevel=0)
    ellipsoid("knob", brass, (0.18, face + 0.036, 0.44), (0.022, 0.016, 0.022), 16, 8)
    box("kick_plate", metal, (0.44, 0.006, 0.07), (0, face + 0.001, 0.05), 0.002, 1)
    for z in (0.18, 0.72):
        box("hinge", metal, (0.02, 0.03, 0.08), (-width / 2 - 0.004, 0, z), 0.003, 2)
    for x in (-0.13, 0.13):
        tube("handle", brass, spline([(x, -face, 0.4), (x, -face - 0.04, 0.42), (x, -face - 0.045, 0.48), (x, -face - 0.04, 0.54), (x, -face, 0.56)], 4), 0.012, 10,
             reference=Vector((1, 0, 0)))


def build_boot(size):
    rubber, trim, tab, inside = "yellow_Classic", "navy_Classic", "scarlet_Classic", "dark_Classic"
    x0 = -0.05
    path = spline([(x0, 0, 0.25), (x0, 0, 0.19), (x0, 0, 0.14), (x0 + 0.012, 0, 0.092), (x0 + 0.045, 0, 0.062), (0.03, 0, 0.05), (0.092, 0, 0.047)], 4)
    taper("boot", rubber, path, interpolate([0.0495, 0.048, 0.047, 0.049, 0.05, 0.047, 0.044], len(path)), 20, Vector((0, 1, 0)))
    lathe("collar", rubber, [(0.045, 0.245), (0.0498, 0.25), (0.0508, 0.268), (0.0528, 0.288), (0.0545, 0.298), (0.05, 0.302), (0.0465, 0.299)], (x0, 0, 0),
          segments=28)
    lathe("inside", inside, [(0.0466, 0.2995), (0.043, 0.275), (0, 0.268)], (x0, 0, 0), segments=28)
    torus("rim", trim, 0.0515, 0.006, (x0, 0, 0.299), "Z", 28, 5)
    ellipsoid("heel", trim, (x0 - 0.001, 0, 0.044), (0.0505, 0.0485, 0.034), 16, 8)
    ellipsoid("toe", rubber, (0.092, 0, 0.047), (0.043, 0.044, 0.044), 16, 8)
    outline = [(x0 + 0.049 * math.cos(math.radians(a)), 0.049 * math.sin(math.radians(a))) for a in range(90, 271, 15)]
    outline += [(0.065 + 0.07 * math.cos(math.radians(a)), 0.051 * math.sin(math.radians(a))) for a in range(-90, 91, 15)]
    prism("sole", trim, outline, 0.0, 0.022, bevel=(0.004, 2))
    torus("pull_tab", tab, 0.016, 0.0045, (x0 - 0.056, 0, 0.29), "Y", 16, 4)
    move(prism("star", tab, star(5, 0.022, 0.0095), 0.042, 0.0495, bevel=(0.001, 1)), (-math.pi / 2, 0, 0), (x0, 0, 0.2))


BUILDERS = {"RAKE": build_rake, "TEAPOT": build_teapot, "LID": build_lid, "TOMATO": build_tomato, "ROCKET": build_rocket,
            "MARBLE": build_marble, "LADDER": build_ladder, "PADDLE": build_paddle, "CHILI": build_chili, "DISC": build_disc,
            "HELMET": build_helmet, "LEMON": build_lemon, "TORCH": build_torch, "CARPET": build_carpet, "SLIME": build_slime,
            "SPOON": build_spoon, "TENT": build_tent, "PEAR": build_pear, "SOCK": build_sock, "HOSE": build_hose,
            "RACKET": build_racket, "MELON": build_melon, "PIANO": build_piano, "TONIC": build_tonic, "BRICK": build_brick,
            "PENCIL": build_pencil, "RAMP": build_ramp, "OIL": build_oil, "DOOR": build_door, "BOOT": build_boot}


def manifest():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def design_size(entry):
    size = entry["size"]
    return Vector((size[0], size[2], size[1]))


def export(obj, word, path):
    root = bpy.data.objects.new(word, None)
    bpy.context.scene.collection.objects.link(root)
    data = obj.data.copy()
    data.name = word + "_Mesh"
    data.transform(TURN)
    data.update()
    copy = bpy.data.objects.new(word + "_Mesh", data)
    bpy.context.scene.collection.objects.link(copy)
    copy.parent = root
    for o in bpy.context.scene.objects:
        o.select_set(o in (root, copy))
    bpy.context.view_layer.objects.active = root
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    bpy.data.objects.remove(copy)
    bpy.data.objects.remove(root)
    bpy.data.meshes.remove(data)
    blob = path.read_bytes()
    shown = path.relative_to(REPO) if path.is_relative_to(REPO) else path
    return {"path": str(shown).replace("\\", "/"), "bytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()}


def reimport(path, word, size):
    reset()
    bpy.ops.import_scene.fbx(filepath=str(path))
    points = [TURN @ (o.matrix_world @ v.co) for o in bpy.context.scene.objects if o.type == "MESH" for v in o.data.vertices]
    lo, hi = box_of(points)
    lo, hi = Vector(lo), Vector(hi)
    extent = hi - lo
    scale = max(size) / max(extent)
    errors = []
    for i, axis in enumerate("xyz"):
        if abs(extent[i] * scale - size[i]) > BOUNDS_TOLERANCE * 10:
            errors.append(f"{word}: reimported {axis} {extent[i] * scale:.4f} vs {size[i]:.4f}")
    if abs((lo.x + hi.x) / 2) * scale > BOUNDS_TOLERANCE * 10 or abs((lo.y + hi.y) / 2) * scale > BOUNDS_TOLERANCE * 10 or abs(lo.z) * scale > BOUNDS_TOLERANCE * 10:
        errors.append(f"{word}: reimport is not bottom-centred")
    return {"unitScale": round(1.0 / scale, 6), "min": [round(v, 5) for v in lo], "max": [round(v, 5) for v in hi]}, errors


def record_manifest(results):
    raw = MANIFEST.read_bytes().decode("utf-8")
    newline = "\r\n" if "\r\n" in raw else "\n"
    data = json.loads(raw)
    for entry in data["entries"]:
        result = results.get(entry["word"])
        if result:
            entry["rebuilt"] = {"script": SCRIPT, "bytes": result["fbx"]["bytes"], "sha256": result["fbx"]["sha256"], "triangles": result["triangles"]}
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    MANIFEST.write_bytes(text.replace("\n", newline).encode("utf-8"))


def main():
    args = parse_args()
    entries = {e["word"]: e for e in manifest()["entries"]}
    report = {"script": SCRIPT, "blender": bpy.app.version_string, "mode": "in-place" if args.in_place else "staged", "models": {}, "errors": [], "warnings": []}
    out_dir = MODELS if args.in_place else Path(args.out)
    written = {}
    for word in args.words:
        reset()
        entry = entries[word]
        size = design_size(entry)
        BUILDERS[word](size)
        obj = join(word)
        obj.data.transform(Matrix.Scale(GROW.get(word, 1.0), 4))
        obj.data.update()
        box_uv(obj)
        lo, hi = mesh_bounds(obj)
        natural = [round(v, 4) for v in (hi - lo)]
        factors = fit(obj, size)
        item = {"natural": natural, "fitFactors": [round(f, 4) for f in factors], **stats(obj)}
        if any(abs(f - 1) > FIT_LIMIT for f in factors):
            report["errors"].append(f"{word}: fit factors {item['fitFactors']} exceed {FIT_LIMIT:.0%}; natural size {natural} differs from the manifest")
        elif any(abs(f - 1) > FIT_WARNING for f in factors):
            report["warnings"].append(f"{word}: fit factors {item['fitFactors']} exceed {FIT_WARNING:.0%}")
        if item["triangles"] > entry["maxTriangles"]:
            report["errors"].append(f"{word}: {item['triangles']} triangles is over the {entry['maxTriangles']} budget")
        if sorted(set(item["materials"])) != sorted(entry["materials"]):
            report["errors"].append(f"{word}: material slots {sorted(set(item['materials']))} differ from the manifest {sorted(entry['materials'])}")
        if args.preview:
            item["preview"] = preview(obj, word, args.preview, args.samples)
        target = out_dir / f"{word}.fbx"
        item["fbx"] = export(obj, word, target)
        again, errors = reimport(target, word, size)
        item["reimport"] = again
        report["errors"] += errors
        report["models"][word] = item
        written[word] = item
        print(f"HOUSE_ITEM_V2 {word} tris={item['triangles']} fit={item['fitFactors']} materials={item['materials']}", flush=True)
    if args.in_place and not report["errors"]:
        record_manifest(written)
        report["manifest"] = str(MANIFEST.relative_to(REPO)).replace("\\", "/")
    report["status"] = "OK" if not report["errors"] else "FAIL"
    Path(args.report).parent.mkdir(parents=True, exist_ok=True)
    Path(args.report).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("HOUSE_ITEMS_V2_BUILD " + json.dumps({"status": report["status"], "models": list(report["models"]), "errors": report["errors"][:20],
                                                "warnings": report["warnings"], "report": args.report}), flush=True)
    if report["errors"]:
        raise RuntimeError(f"{len(report['errors'])} check(s) failed; see {args.report}")


main()
