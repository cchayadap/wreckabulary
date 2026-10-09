import math

import bpy
from mathutils import Matrix, Quaternion, Vector

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
SIDES = ("L", "R")
REPLACED = ("Idle", "Walk_InPlace", "Run_InPlace", "Jump_Preview", "Swing_OneHand",
            "Thrust_OneHand", "Throw_OneHand", "Hit_Reaction", "Celebrate")


def rot(axis, degrees):
    return Quaternion(axis, math.radians(degrees))


def body(pitch=0.0, turn=0.0, tilt=0.0):
    return rot(Y, turn) @ rot(X, pitch) @ rot(Z, tilt)


def arm(side, forward=0.0, out=0.0, twist=0.0):
    s = 1 if side == "L" else -1
    return rot(Z, s * out) @ rot(X, -forward) @ rot(Y, s * twist)


def elbow(bend):
    return rot(X, -bend)


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


EASE = {
    "lin": lambda t: t,
    "io": lambda t: t * t * (3 - 2 * t),
    "out": lambda t: 1 - (1 - t) ** 2,
    "in": lambda t: t * t,
    "snap": lambda t: t ** 3,
    "whip": lambda t: 1 - (1 - t) ** 3,
}


def _frame(direction, reference):
    e1 = direction.normalized()
    e2 = (reference - e1 * reference.dot(e1)).normalized()
    return Matrix((e1, e2, e1.cross(e2))).transposed()


def _align(rest_dir, rest_ref, new_dir, new_ref):
    return (_frame(new_dir, new_ref) @ _frame(rest_dir, rest_ref).transposed()).to_quaternion()


class Rig:
    def __init__(self, obj):
        bones = obj.data.bones
        basis = bones[0].matrix_local.to_quaternion()
        for b in bones:
            if basis.rotation_difference(b.matrix_local.to_quaternion()).angle > 1e-4:
                raise RuntimeError(f"author_clips: bone {b.name} has its own rest orientation")
        self.obj = obj
        self.to_body = basis.inverted()
        self.rest = {b.name: self.to_body @ b.head_local for b in bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}

        def depth(name):
            d = 0
            while self.parent[name]:
                name, d = self.parent[name], d + 1
            return d
        self.names = sorted(self.parent, key=depth)
        self.legs = {}
        for s in SIDES:
            hip, knee, ankle = (self.rest[f"{b}_{s}"] for b in ("thigh", "shin", "foot"))
            u0 = (ankle - hip).normalized()
            pole0 = (Z - u0 * Z.dot(u0)).normalized()
            self.legs[s] = {"l1": (knee - hip).length, "l2": (ankle - knee).length,
                            "hip": hip, "knee": knee, "ankle": ankle, "normal": u0.cross(pole0)}
        self.sole = self._sole_points()

    def _sole_points(self):
        boots = bpy.data.objects.get("SK_Boots")
        points = {s: [] for s in SIDES}
        if boots and boots.type == "MESH":
            groups = {g.index: g.name for g in boots.vertex_groups}
            for v in boots.data.vertices:
                for g in v.groups:
                    name = groups.get(g.group, "")
                    if g.weight > 0.5 and name in ("foot_L", "foot_R"):
                        world = boots.matrix_world @ v.co
                        points[name[-1]].append(self.to_body @ world - self.rest[name])
        for s in SIDES:
            if len(points[s]) < 8:
                raise RuntimeError("author_clips: SK_Boots has no vertices weighted to foot_" + s)
            lowest = min(p.y for p in points[s])
            points[s] = [p for p in points[s] if p.y < lowest + 0.03]
        self.ground = min(self.rest[f"foot_{s}"].y + min(p.y for p in points[s]) for s in SIDES)
        return points

    def ankle_height(self, side, pitch):
        r = rot(X, pitch)
        return self.ground - min((r @ p).y for p in self.sole[side])

    def hips(self, local, shift):
        pelvis_w = local.get("root", Quaternion()) @ local.get("pelvis", Quaternion())
        pelvis_p = self.rest["pelvis"] + local.get("root", Quaternion()) @ shift
        return pelvis_w, {s: pelvis_p + pelvis_w @ (self.rest[f"thigh_{s}"] - self.rest["pelvis"]) for s in SIDES}

    def solve_leg(self, local, shift, side, ankle, pitch, splay=0.15):
        leg = self.legs[side]
        pelvis_w, hips = self.hips(local, shift)
        hip = hips[side]
        l1, l2 = leg["l1"], leg["l2"]
        offset = ankle - hip
        d = offset.length
        reach = (l1 + l2) * 0.9995
        miss = max(0.0, d - reach)
        d = min(max(d, abs(l1 - l2) + 1e-4), reach)
        u = offset.normalized()
        out = 1 if side == "L" else -1
        pole = (Z + X * (out * splay)).normalized()
        v = (pole - u * pole.dot(u)).normalized()
        a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
        h = math.sqrt(max(0.0, l1 * l1 - a * a))
        knee = hip + u * a + v * h
        foot_at = hip + u * d
        normal = u.cross(v)
        thigh_w = _align(leg["knee"] - leg["hip"], leg["normal"], knee - hip, normal)
        shin_w = _align(leg["ankle"] - leg["knee"], leg["normal"], foot_at - knee, normal)
        foot_w = rot(X, pitch)
        local[f"thigh_{side}"] = pelvis_w.inverted() @ thigh_w
        local[f"shin_{side}"] = thigh_w.inverted() @ shin_w
        local[f"foot_{side}"] = shin_w.inverted() @ foot_w
        return miss

    def world(self, local, name):
        q = Quaternion()
        chain = []
        while name:
            chain.append(name)
            name = self.parent[name]
        for n in reversed(chain):
            q = q @ local.get(n, Quaternion())
        return q


def steady_head(rig, local, target):
    chest_w = rig.world(local, "chest")
    neck_w = chest_w.slerp(target, 0.5)
    local["neck"] = chest_w.inverted() @ neck_w
    local["head"] = neck_w.inverted() @ target


WALK = dict(duty=0.58, reach=0.072, lift=0.036, kick=0.0, heel=14, toe=26, width=0.088,
            drop=0.016, bob=0.009, sway=0.008, yaw=9, roll=4, lean=4, run=False,
            swing=32, out=-8, elbow=14, elbow_swing=20, chest_counter=1.6, head_pitch=1)
RUN = dict(duty=0.34, reach=0.088, lift=0.062, kick=0.035, heel=6, toe=38, width=0.084,
           drop=0.026, bob=0.014, sway=0.004, yaw=11, roll=2.5, lean=9, run=True,
           swing=44, out=-4, elbow=62, elbow_swing=26, chest_counter=1.5, head_pitch=-3)


def gait(rig, g, phase):
    local, misses = {}, []
    two = 2 * math.pi
    mid = g["duty"] / 2
    if g["run"]:
        lift = -g["bob"] * math.cos(2 * two * (phase - mid))
    else:
        lift = -g["bob"] * math.cos(2 * two * phase)
    sway = g["sway"] * math.cos(two * (phase - mid))
    shift = Vector((sway, -g["drop"] + lift, 0.0))
    yaw = -g["yaw"] * math.cos(two * phase)
    roll = g["roll"] * math.cos(two * (phase - mid))
    local["pelvis"] = body(pitch=g["lean"] * 0.5, turn=yaw, tilt=roll)
    local["spine"] = body(pitch=g["lean"] * 0.3, turn=-yaw * g["chest_counter"] * 0.5, tilt=-roll * 0.6)
    bounce = (0.5 - 0.5 * math.cos(2 * two * (phase - mid))) if g["run"] else 0.0
    local["chest"] = body(pitch=g["lean"] * 0.2 + 2.5 * bounce, turn=-yaw * g["chest_counter"] * 0.5,
                          tilt=-roll * 0.5)
    steady_head(rig, local, body(pitch=g["head_pitch"] - 1.5 * bounce, turn=-yaw * 0.15))

    for side, offset in (("L", 0.0), ("R", 0.5)):
        p = (phase + offset) % 1.0
        x = g["width"] * (1 if side == "L" else -1)
        rest = rig.legs[side]["ankle"]
        if p < g["duty"]:
            s = p / g["duty"]
            z = g["reach"] - 2 * g["reach"] * s
            pitch = -g["heel"] * (1 - smoothstep(0.0, 0.22, s)) + g["toe"] * smoothstep(0.55, 1.0, s) ** 2
            y = rig.ankle_height(side, pitch)
        else:
            u = (p - g["duty"]) / (1 - g["duty"])
            ease = u - math.sin(two * u) / two
            z = -g["reach"] + 2 * g["reach"] * ease - g["kick"] * math.sin(math.pi * u) * (1 - u) ** 2
            pitch = g["toe"] + (-g["heel"] - g["toe"]) * smoothstep(0.05, 0.85, u)
            y = max(rig.ankle_height(side, pitch), rig.ankle_height(side, 0)
                    + g["lift"] * math.sin(math.pi * u ** 0.85) + g["kick"] * 0.8 * math.sin(math.pi * u) * (1 - u))
        misses.append(rig.solve_leg(local, shift, side, Vector((x, y, rest.z + z)), pitch))

        back = g["swing"] * math.cos(two * (phase + offset - 0.04))
        forward = (1 - back / g["swing"]) / 2
        local[f"upper_arm_{side}"] = arm(side, forward=-back, out=g["out"])
        local[f"forearm_{side}"] = elbow(g["elbow"] + g["elbow_swing"] * forward)
    return local, shift, max(misses)


NEUTRAL_ARMS = dict(out=-8)


def pose(pelvis=(0, 0, 0), spine=(0, 0, 0), chest=(0, 0, 0), neck=(0, 0, 0), head=(0, 0, 0),
         arm_l=(0, -8), arm_r=(0, -8), elbow_l=10, elbow_r=10, hand_l=(0, 0, 0), hand_r=(0, 0, 0),
         shift=(0, 0, 0), foot_l=(0, 0, 0, 0), foot_r=(0, 0, 0, 0)):
    return {
        "pelvis": body(*pelvis), "spine": body(*spine), "chest": body(*chest),
        "neck": body(*neck), "head": body(*head),
        "upper_arm_L": arm("L", *arm_l), "upper_arm_R": arm("R", *arm_r),
        "forearm_L": elbow(elbow_l), "forearm_R": elbow(elbow_r),
        "hand_L": body(*hand_l), "hand_R": body(*hand_r),
        "_shift": Vector(shift), "_feet": {"L": foot_l, "R": foot_r},
    }


def keyed(rig, keys, frame):
    for (f0, a, _), (f1, b, ease) in zip(keys, keys[1:]):
        if f0 <= frame <= f1:
            t = EASE[ease]((frame - f0) / (f1 - f0)) if f1 > f0 else 1.0
            break
    else:
        f0, a, _ = keys[-1]
        b, t = a, 0.0
    local = {}
    for name, qa in a.items():
        if not name.startswith("_"):
            qb = b[name]
            local[name] = qa.slerp(qb if qa.dot(qb) >= 0 else -qb, t)
    shift = a["_shift"].lerp(b["_shift"], t)
    misses = []
    for s in SIDES:
        fa, fb = a["_feet"][s], b["_feet"][s]
        dx, dy, dz, pitch = (fa[i] + (fb[i] - fa[i]) * t for i in range(4))
        rest = rig.legs[s]["ankle"]
        y = rig.ankle_height(s, pitch) + dy
        misses.append(rig.solve_leg(local, shift, s, Vector((rest.x + dx, y, rest.z + dz)), pitch))
    return local, shift, max(misses)


def idle_pose(rig, u):
    two = 2 * math.pi
    breathe = math.sin(two * 2 * u)
    shift = Vector((0.006 * math.sin(two * u), -0.004 + 0.0025 * breathe, 0.0))
    local = {"pelvis": body(tilt=-1.5 * math.sin(two * u)),
             "spine": body(pitch=0.8 * breathe, tilt=1.0 * math.sin(two * u)),
             "chest": body(pitch=-1.6 * breathe)}
    steady_head(rig, local, body(pitch=1.5 * math.sin(two * u + 1.0), turn=5 * math.sin(two * u + 0.4),
                                 tilt=2 * math.sin(two * u * 2 + 0.7)))
    for side in SIDES:
        sway = 2.5 * math.sin(two * 2 * u - 0.6)
        local[f"upper_arm_{side}"] = arm(side, forward=sway, out=-8 + 1.5 * breathe)
        local[f"forearm_{side}"] = elbow(10 + 2 * breathe)
    misses = [rig.solve_leg(local, shift, s, Vector((rig.legs[s]["ankle"].x, rig.ankle_height(s, 0),
                                                    rig.legs[s]["ankle"].z)), 0) for s in SIDES]
    return local, shift, max(misses)


def swing_keys():
    n = pose()
    wind = pose(pelvis=(0, -16, 0), spine=(-3, -14, 0), chest=(-5, -20, -4), head=(4, 30, 0),
                arm_r=(-38, 58, 0), elbow_r=64, arm_l=(40, -6), elbow_l=34, hand_r=(-25, 0, 0),
                shift=(-0.008, -0.014, -0.006))
    hit = pose(pelvis=(4, 12, 0), spine=(5, 12, 0), chest=(7, 18, 4), head=(-4, -24, 0),
               arm_r=(70, 30, 0), elbow_r=8, arm_l=(-28, -6), elbow_l=24, hand_r=(8, 0, 0),
               shift=(0.004, -0.02, 0.012))
    follow = pose(pelvis=(6, 22, 0), spine=(7, 16, 0), chest=(9, 22, 6), head=(-6, -32, 0),
                  arm_r=(76, -34, 0), elbow_r=28, arm_l=(-36, -6), elbow_l=30, hand_r=(16, 0, 0),
                  shift=(0.008, -0.022, 0.014))
    settle = pose(pelvis=(2, 6, 0), spine=(2, 4, 0), chest=(2, 6, 0), arm_r=(24, -10), elbow_r=24,
                  shift=(0.002, -0.008, 0.004))
    return [(0, n, "lin"), (4, wind, "out"), (7, hit, "snap"), (12, follow, "whip"),
            (22, settle, "io"), (33, n, "io")]


def thrust_keys():
    n = pose()
    cock = pose(pelvis=(-2, -12, 0), spine=(-2, -10, 0), chest=(-4, -14, 0), head=(2, 20, 0),
                arm_r=(-22, -10, 10), elbow_r=96, arm_l=(24, -6), elbow_l=30,
                shift=(0, -0.012, -0.014))
    lunge = pose(pelvis=(8, 12, 0), spine=(6, 10, 0), chest=(8, 14, 0), head=(-8, -18, 0),
                 arm_r=(84, -24), elbow_r=4, arm_l=(-30, -6), elbow_l=20,
                 shift=(0, -0.024, 0.026), foot_r=(0, 0, 0.02, 0))
    hold = pose(pelvis=(7, 10, 0), spine=(5, 8, 0), chest=(7, 12, 0), head=(-6, -14, 0),
                arm_r=(80, -22), elbow_r=10, arm_l=(-24, -6), elbow_l=20,
                shift=(0, -0.022, 0.022), foot_r=(0, 0, 0.02, 0))
    return [(0, n, "lin"), (4, cock, "out"), (7, lunge, "snap"), (12, hold, "out"),
            (20, pose(shift=(0, -0.006, 0.006), arm_r=(20, -10), elbow_r=20), "io"), (27, n, "io")]


def throw_keys():
    n = pose()
    cock = pose(pelvis=(-4, -16, 0), spine=(-6, -12, 0), chest=(-10, -20, -6), head=(6, 24, 0),
                arm_r=(176, 12, 10), elbow_r=92, arm_l=(52, 6), elbow_l=24,
                shift=(-0.004, -0.008, -0.014), foot_l=(0, 0, 0.012, 0))
    release = pose(pelvis=(6, 12, 0), spine=(8, 10, 0), chest=(12, 18, 4), head=(-6, -20, 0),
                   arm_r=(118, -8), elbow_r=14, arm_l=(-10, -4), elbow_l=30,
                   shift=(0.004, -0.016, 0.016), foot_l=(0, 0, 0.012, 0))
    follow = pose(pelvis=(10, 20, 0), spine=(10, 14, 0), chest=(16, 22, 6), head=(-10, -26, 0),
                  arm_r=(52, -40, -10), elbow_r=26, arm_l=(-30, -4), elbow_l=30,
                  shift=(0.006, -0.022, 0.02), foot_l=(0, 0, 0.012, 0))
    return [(0, n, "lin"), (3, cock, "out"), (6, release, "snap"), (11, follow, "whip"),
            (22, pose(pelvis=(3, 6, 0), chest=(3, 6, 0), arm_r=(18, -14), elbow_r=20,
                      shift=(0.002, -0.008, 0.006)), "io"), (33, n, "io")]


def hit_keys():
    n = pose()
    recoil = pose(pelvis=(-8, 0, 3), spine=(-10, 0, 2), chest=(-16, 4, 4), neck=(-10, 0, 0), head=(-12, 6, 6),
                  arm_l=(48, 34), elbow_l=26, arm_r=(52, 30), elbow_r=30,
                  shift=(0, -0.016, -0.016), foot_l=(0, 0, 0, 0), foot_r=(0, 0, 0, 0))
    rebound = pose(pelvis=(6, 0, -2), spine=(6, 0, -1), chest=(10, -2, -2), head=(8, -4, -3),
                   arm_l=(20, 8), elbow_l=34, arm_r=(24, 6), elbow_r=36, shift=(0, -0.02, 0.006))
    dazed = pose(pelvis=(2, 0, 0), spine=(2, 0, 2), chest=(4, 0, 2), head=(6, 0, 8),
                 arm_l=(8, 6), elbow_l=18, arm_r=(10, 4), elbow_r=18, shift=(0, -0.01, 0))
    return [(0, n, "lin"), (3, recoil, "whip"), (8, rebound, "io"), (19.5, dazed, "io")]


def celebrate_keys():
    n = pose()
    crouch = pose(pelvis=(10, 0, 0), chest=(8, 0, 0), arm_l=(-30, 0), arm_r=(-30, 0), elbow_l=20,
                  elbow_r=20, shift=(0, -0.045, 0))
    up = pose(pelvis=(-4, 0, 0), chest=(-8, 0, 0), head=(-10, 0, 0), arm_l=(165, 28), arm_r=(165, 28),
              elbow_l=10, elbow_r=10, shift=(0, 0.03, 0), foot_l=(0, 0.05, 0, 28), foot_r=(0, 0.05, 0, 28))
    land = pose(pelvis=(8, 0, 0), chest=(6, 0, 0), head=(-4, 0, 0), arm_l=(150, 36), arm_r=(150, 36),
                elbow_l=20, elbow_r=20, shift=(0, -0.035, 0))
    up2 = pose(pelvis=(-4, 6, 0), chest=(-8, 8, 0), head=(-12, 4, 0), arm_l=(170, 20), arm_r=(170, 20),
               elbow_l=6, elbow_r=6, shift=(0, 0.034, 0), foot_l=(0, 0.055, 0, 30), foot_r=(0, 0.055, 0, 30))
    wave_a = pose(chest=(-4, -8, 4), head=(-8, -6, 4), arm_r=(160, 40), elbow_r=40, hand_r=(0, 0, -20),
                  arm_l=(-10, 10), elbow_l=60, shift=(0, -0.008, 0))
    wave_b = pose(chest=(-4, -10, 6), head=(-8, -8, 6), arm_r=(162, 30), elbow_r=10, hand_r=(0, 0, 20),
                  arm_l=(-10, 10), elbow_l=60, shift=(0, -0.006, 0))
    return [(0, n, "lin"), (6, crouch, "io"), (12, up, "whip"), (18, land, "in"), (23, crouch, "out"),
            (29, up2, "whip"), (35, land, "in"), (40, wave_a, "io"), (45, wave_b, "io"), (50, wave_a, "io"),
            (55, wave_b, "io"), (66, n, "io")]


def jump_pose(rig, u):
    crouch = pose(pelvis=(12, 0, 0), spine=(6, 0, 0), chest=(8, 0, 0), head=(-10, 0, 0),
                  arm_l=(-40, 6), arm_r=(-40, 6), elbow_l=24, elbow_r=24, shift=(0, -0.05, 0.004))
    launch = pose(pelvis=(-4, 0, 0), chest=(-8, 0, 0), head=(-12, 0, 0), arm_l=(150, 52), arm_r=(150, 52),
                  elbow_l=10, elbow_r=10, shift=(0, 0.012, 0), foot_l=(0, 0.01, 0, 34), foot_r=(0, 0.01, 0, 34))
    tuck = pose(pelvis=(-6, 0, 0), chest=(-4, 0, 0), head=(-6, 0, 0), arm_l=(70, 72), arm_r=(70, 72),
                elbow_l=30, elbow_r=30, shift=(0, 0.0, 0), foot_l=(0.004, 0.075, 0.026, 16),
                foot_r=(-0.004, 0.06, 0.008, 22))
    fall = pose(pelvis=(2, 0, 0), chest=(0, 0, 0), head=(-4, 0, 0), arm_l=(30, 76), arm_r=(30, 76),
                elbow_l=22, elbow_r=22, shift=(0, 0.004, 0), foot_l=(0.006, 0.022, 0.012, 10),
                foot_r=(-0.006, 0.03, -0.006, 14))
    keys = [(0.0, pose(), "lin"), (0.15, crouch, "out"), (0.3, launch, "whip"), (0.48, tuck, "out"),
            (0.75, fall, "io"), (1.0, fall, "lin")]
    return keyed(rig, keys, u)


def _curves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    return [c for layer in action.layers for strip in layer.strips for bag in strip.channelbags
            for c in bag.fcurves]


def _frames(end):
    frames = [float(f) for f in range(int(math.floor(end)) + 1)]
    if end - math.floor(end) > 1e-6:
        frames.append(end)
    return frames


def write_action(rig, name, sample):
    old = bpy.data.actions[name]
    start, end = old.frame_range
    if abs(start) > 1e-6:
        raise RuntimeError(f"author_clips: {name} starts at frame {start}, expected 0")
    fake_user = old.use_fake_user
    old.name = name + "__pack"
    action = bpy.data.actions.new(name)
    obj = rig.obj
    data = obj.animation_data or obj.animation_data_create()
    previous = data.action
    data.action = action
    previous_q = {}
    worst = 0.0
    for frame in _frames(end):
        local, shift, miss = sample(frame, end)
        worst = max(worst, miss)
        for bone in rig.names:
            pb = obj.pose.bones[bone]
            pb.rotation_mode = "QUATERNION"
            q = local.get(bone, Quaternion()).normalized()
            if bone in previous_q and previous_q[bone].dot(q) < 0:
                q = -q
            previous_q[bone] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=bone)
            if bone == "pelvis":
                pb.location = shift
                pb.keyframe_insert("location", frame=frame, group=bone)
    for curve in _curves(action):
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"
    for pb in obj.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    data.action = previous if previous and previous != old else None
    action.use_fake_user = fake_user
    bpy.data.actions.remove(old)
    if tuple(round(v, 4) for v in action.frame_range) != (round(start, 4), round(end, 4)):
        raise RuntimeError(f"author_clips: {name} range changed to {tuple(action.frame_range)}")
    return round(worst, 5)


def author(rig_obj):
    rig = Rig(rig_obj)
    report = {"replaced": [], "ik_overreach_m": {}, "locomotion": {}}

    def loop(g):
        return lambda frame, end: gait(rig, g, (frame / end) % 1.0)

    def keys(build):
        k = build()
        return lambda frame, end: keyed(rig, k, frame)

    samplers = {
        "Idle": lambda frame, end: idle_pose(rig, (frame / end) % 1.0),
        "Walk_InPlace": loop(WALK),
        "Run_InPlace": loop(RUN),
        "Jump_Preview": lambda frame, end: jump_pose(rig, frame / end),
        "Swing_OneHand": keys(swing_keys),
        "Thrust_OneHand": keys(thrust_keys),
        "Throw_OneHand": keys(throw_keys),
        "Hit_Reaction": keys(hit_keys),
        "Celebrate": keys(celebrate_keys),
    }
    for name in REPLACED:
        report["ik_overreach_m"][name] = write_action(rig, name, samplers[name])
        report["replaced"].append(name)
    for name, g in (("Walk_InPlace", WALK), ("Run_InPlace", RUN)):
        end = bpy.data.actions[name].frame_range[1]
        report["locomotion"][name] = {"stride": round(2 * g["reach"] / g["duty"], 4),
                                      "cycle_seconds": round(end / bpy.context.scene.render.fps, 4),
                                      "duty": g["duty"]}
    worst = max(report["ik_overreach_m"].values())
    if worst > 0.004:
        raise RuntimeError(f"author_clips: a foot target is {worst:.4f} m out of reach")
    return report
