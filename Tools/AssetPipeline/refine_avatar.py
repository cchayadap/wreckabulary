import math
from collections import defaultdict

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HAIR = "hair"
WHITE = "ivory_badge"
BRASS = "accessory_hardware"
HAIR_RGB = (0.03, 0.017, 0.011)


def _mesh(name):
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"refine: {name} is missing")
    return obj


def _points(obj):
    mw = obj.matrix_world
    return [mw @ v.co for v in obj.data.vertices]


def _bvh(obj, keep=None):
    polys = [list(p.vertices) for p in obj.data.polygons if keep is None or keep(p)]
    return BVHTree.FromPolygons(_points(obj), polys)


def _material_index(obj, name):
    for i, mat in enumerate(obj.data.materials):
        if mat and mat.name == name:
            return i
    return None


def _islands(obj):
    me = obj.data
    key = [tuple(round(c, 5) for c in v.co) for v in me.vertices]
    parent = {}

    def find(a):
        parent.setdefault(a, a)
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for poly in me.polygons:
        first = find(key[poly.vertices[0]])
        for i in poly.vertices[1:]:
            other = find(key[i])
            if other != first:
                parent[other] = first
    groups = defaultdict(list)
    for poly in me.polygons:
        groups[find(key[poly.vertices[0]])].append(poly.index)
    return list(groups.values())


def _bounds(points):
    lo = Vector([min(p[k] for p in points) for k in range(3)])
    hi = Vector([max(p[k] for p in points) for k in range(3)])
    return lo, hi


def _bone_head(rig, name):
    return rig.matrix_world @ rig.data.bones[name].head_local


def _perpendicular(t):
    helper = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    return t.cross(helper).normalized()


def _smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


class Geometry:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.uvs = []

    def vert(self, co):
        self.verts.append(Vector(co))
        return len(self.verts) - 1

    def face(self, indices, uvs):
        self.faces.append(tuple(indices))
        self.uvs.append(list(uvs))

    def tube(self, centres, radii, segments=10, closed=False, normal=None, caps=True):
        n = len(centres)
        tangents = []
        for i in range(n):
            if closed:
                a, b = centres[i - 1], centres[(i + 1) % n]
            else:
                a, b = centres[max(0, i - 1)], centres[min(n - 1, i + 1)]
            tangents.append((b - a).normalized())
        frame = (normal.normalized() if normal is not None else _perpendicular(tangents[0]))
        rings = []
        for i, (c, t, r) in enumerate(zip(centres, tangents, radii)):
            frame = (frame - t * frame.dot(t))
            frame = frame.normalized() if frame.length > 1e-9 else _perpendicular(t)
            side = t.cross(frame)
            ring = []
            for j in range(segments):
                ang = 2 * math.pi * j / segments
                ring.append(self.vert(c + r * (math.cos(ang) * frame + math.sin(ang) * side)))
            rings.append(ring)
        spans = n if closed else n - 1
        for i in range(spans):
            a, b = rings[i], rings[(i + 1) % n]
            v0, v1 = i / max(1, spans), (i + 1) / max(1, spans)
            for j in range(segments):
                k = (j + 1) % segments
                u0, u1 = j / segments, (j + 1) / segments
                self.face((a[j], a[k], b[k], b[j]), [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
        if caps and not closed:
            for ring, centre, sign in ((rings[0], centres[0], -1), (rings[-1], centres[-1], 1)):
                hub = self.vert(centre)
                for j in range(segments):
                    k = (j + 1) % segments
                    tri = (ring[j], ring[k], hub) if sign > 0 else (ring[k], ring[j], hub)
                    self.face(tri, [(0.5, 0.5)] * 3)

    def capsule(self, start, end, radius, segments=12, rings=4):
        axis = end - start
        length = axis.length
        t = axis.normalized()
        centres, radii = [], []
        for i in range(rings, 0, -1):
            phi = (math.pi / 2) * i / rings
            centres.append(start - t * radius * math.sin(phi) * 0.999)
            radii.append(max(radius * math.cos(phi), radius * 0.04))
        for i in range(3):
            centres.append(start + t * length * i / 2)
            radii.append(radius)
        for i in range(1, rings + 1):
            phi = (math.pi / 2) * i / rings
            centres.append(end + t * radius * math.sin(phi) * 0.999)
            radii.append(max(radius * math.cos(phi), radius * 0.04))
        self.tube(centres, radii, segments=segments)

    def torus(self, centre, axis, major, minor, major_segments=32, minor_segments=10,
              phase=0.0, stretch=(1.0, 1.0)):
        axis = axis.normalized()
        e1 = _perpendicular(axis)
        e2 = axis.cross(e1)
        pts = []
        for i in range(major_segments):
            a = phase + 2 * math.pi * i / major_segments
            pts.append(centre + major * (stretch[0] * math.cos(a) * e1 + stretch[1] * math.sin(a) * e2))
        self.tube(pts, [minor] * major_segments, segments=minor_segments, closed=True, normal=axis)

    def join_into(self, target, material, group, label):
        me = bpy.data.meshes.new(label)
        me.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        uv_name = target.data.uv_layers.active.name if target.data.uv_layers.active else "UVMap"
        uv = me.uv_layers.new(name=uv_name)
        for poly, coords in zip(me.polygons, self.uvs):
            for loop, co in zip(poly.loop_indices, coords):
                uv.data[loop].uv = co
        for poly in me.polygons:
            poly.use_smooth = True
        me.materials.append(bpy.data.materials[material])
        me.normals_split_custom_set_from_vertices([v.normal.copy() for v in me.vertices])
        obj = bpy.data.objects.new(label, me)
        bpy.context.scene.collection.objects.link(obj)
        vg = obj.vertex_groups.new(name=group)
        vg.add(list(range(len(me.vertices))), 1.0, "REPLACE")
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        obj.select_set(True)
        target.select_set(True)
        bpy.context.view_layer.objects.active = target
        with bpy.context.temp_override(active_object=target, object=target,
                                       selected_objects=[target, obj],
                                       selected_editable_objects=[target, obj]):
            bpy.ops.object.join()
        target.select_set(False)
        return len(self.faces)


def _delete_polys(obj, poly_indices):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    doomed = [bm.faces[i] for i in sorted(set(poly_indices))]
    loose = {v for f in doomed for v in f.verts}
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in loose if v.is_valid and not v.link_faces], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def remove_hood_crown(hood):
    points = _points(hood)
    doomed = []
    for polys in _islands(hood):
        pts = [points[i] for p in polys for i in hood.data.polygons[p].vertices]
        lo, hi = _bounds(pts)
        if lo.z > 0.93 and hi.z > 1.01:
            doomed.extend(polys)
    if not doomed:
        raise RuntimeError("refine: the Hood crown wasn't found; the source pack changed")
    _delete_polys(hood, doomed)
    return {"hood_crown_polys_removed": len(doomed)}


def add_hair(head, fitted, rig):
    skin = _material_index(head, "ivory_vinyl")
    head_bvh = _bvh(head, keep=lambda p: p.material_index == skin)
    skull = [_points(head)[i] for p in head.data.polygons if p.material_index == skin for i in p.vertices]
    lo, hi = _bounds(skull)
    centre = (lo + hi) / 2
    wear = [_bvh(o) for o in fitted]

    def front_weight(p):
        return _smoothstep(-0.06, -0.15, p.y)

    def dip(x):
        if -0.2 <= x <= 0.1:
            return 0.068 * ((x + 0.2) / 0.3) ** 1.5
        if 0.1 < x <= 0.24:
            return 0.068 - 0.03 * (x - 0.1) / 0.14
        return 0.038 if x > 0.24 else 0.0

    def hairline(p):
        back = 0.80 - 0.45 * (p.y + 0.04)
        f = front_weight(p)
        return (1 - f) * back + f * (0.905 - dip(p.x))

    def surface(d):
        hit = head_bvh.ray_cast(centre + d * 0.6, -d, 0.7)
        return hit[0]

    whorl = Vector((0.03, 0.30, 1.0)).normalized()
    e1 = Vector((1, 0, 0))
    e1 = (e1 - whorl * e1.dot(whorl)).normalized()
    e2 = whorl.cross(e1)

    def direction(theta, phi):
        return (math.cos(theta) * whorl + math.sin(theta) * (math.cos(phi) * e1 + math.sin(phi) * e2)).normalized()

    def inside(theta, phi):
        p = surface(direction(theta, phi))
        return p is not None and p.z > hairline(p)

    nu = 128
    edges = []
    for j in range(nu):
        phi = 2 * math.pi * j / nu
        a, b = 0.0, math.radians(165)
        if not inside(a, phi):
            raise RuntimeError("refine: the hair whorl isn't inside the hairline")
        for _ in range(28):
            mid = (a + b) / 2
            a, b = (mid, b) if inside(mid, phi) else (a, mid)
        edges.append(a)

    geo = Geometry()
    squeezed = [0]
    thick = []
    lip = 0.016
    rows = [0.0, .06, .14, .24, .34, .44, .54, .63, .71, .78, .84, .89, .93, .96, .975, .985, .995]
    sink = [(0.006, -0.0025), (0.018, -0.0045)]

    def vertex(theta, phi, u, t_override=None):
        d = direction(theta, phi)
        p = surface(d)
        if p is None:
            raise RuntimeError("refine: the hair ray missed the head")
        r_head = (p - centre).length
        if t_override is not None:
            return geo.vert(centre + d * (r_head + t_override)), (phi / (2 * math.pi), min(u, 1.0))
        f = front_weight(p)
        top = 0.016 + 0.009 * f
        top *= 1 + 0.14 * math.cos(11 * phi) * _smoothstep(0.08, 0.35, u)
        arc_left = (1 - u) * edges_at[0] * r_head
        roll = math.sqrt(max(0.0, 1 - (1 - min(1.0, arc_left / lip)) ** 2))
        r = r_head + top * roll
        for bvh in wear:
            h = bvh.ray_cast(centre, d, 1.0)
            if h[0] is not None and (h[0] - centre).length - 0.003 < r:
                r = max((h[0] - centre).length - 0.003, r_head + 0.003 * roll)
                squeezed[0] += 1
        thick.append(r - r_head)
        return geo.vert(centre + d * r), (phi / (2 * math.pi), u)

    edges_at = [max(edges)]
    pole, pole_uv = vertex(0.0, 0.0, 0.0)
    grid = []
    for j in range(nu):
        phi = 2 * math.pi * j / nu
        edges_at[0] = edges[j]
        column = [vertex(edges[j] * u, phi, u) for u in rows[1:]]
        column.append(vertex(edges[j], phi, 1.0, t_override=0.0))
        for metres, depth in sink:
            column.append(vertex(edges[j] + metres / 0.2, phi, 1.0, t_override=depth))
        grid.append(column)
    for j in range(nu):
        k = (j + 1) % nu
        a, b = grid[j], grid[k]
        geo.face((pole, b[0][0], a[0][0]), [pole_uv, b[0][1], a[0][1]])
        for i in range(len(a) - 1):
            geo.face((a[i][0], b[i][0], b[i + 1][0], a[i + 1][0]),
                     [a[i][1], b[i][1], b[i + 1][1], a[i + 1][1]])
    _orient_outward(geo, centre)
    added = geo.join_into(head, HAIR, "head", "SK_Head_Hair")
    thick.sort()
    return {"hair_faces": added, "hair_headwear_fits": squeezed[0],
            "hair_thickness_m": {"min": round(thick[0], 4), "median": round(thick[len(thick) // 2], 4),
                                 "max": round(thick[-1], 4)}}


def _orient_outward(geo, centre):
    for n, face in enumerate(geo.faces):
        pts = [geo.verts[i] for i in face]
        normal = Vector()
        for a, b in zip(pts, pts[1:] + pts[:1]):
            normal.x += (a.y - b.y) * (a.z + b.z)
            normal.y += (a.z - b.z) * (a.x + b.x)
            normal.z += (a.x - b.x) * (a.y + b.y)
        mid = sum(pts, Vector()) / len(pts)
        if normal.dot(mid - centre) < 0:
            geo.faces[n] = tuple(reversed(face))
            geo.uvs[n] = list(reversed(geo.uvs[n]))


def add_drawstrings(hoodie):
    bvh = _bvh(hoodie)
    geo = Geometry()
    cord, tip = 0.0042, 0.0075
    for x in (-0.047, 0.047):
        centres = []
        for k in range(7):
            z = 0.600 - 0.010 * k
            hit = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
            if hit[0] is None:
                raise RuntimeError("refine: the Hoodie front wasn't found for the drawstrings")
            centres.append(Vector((x * (1 + 0.04 * k), hit[0].y - cord - 0.0012, z)))
        geo.tube(centres, [cord] * len(centres), segments=10)
        end = centres[-1]
        hit = bvh.ray_cast(Vector((end.x, -1.0, end.z - 0.012)), Vector((0, 1, 0)), 2.0)
        y = (hit[0].y if hit[0] is not None else end.y + cord) - tip - 0.001
        geo.capsule(Vector((end.x, y, end.z + 0.002)), Vector((end.x, y, end.z - 0.016)), tip)
    return {"drawstring_faces": geo.join_into(hoodie, WHITE, "chest", "SK_Hoodie_Drawstrings")}


def add_cuffs(mittens, sleeves, rig):
    mitt_points = _points(mittens)
    notes = {}
    for side, sign in (("L", 1), ("R", -1)):
        hand = _bone_head(rig, f"hand_{side}")
        axis = (hand - _bone_head(rig, f"forearm_{side}")).normalized()
        sleeve_end = 0.0
        for sleeve in sleeves:
            rib = _material_index(sleeve, "fabric_rib")
            pts = _points(sleeve)
            ends = [(pts[i] - hand).dot(axis) for p in sleeve.data.polygons if p.material_index == rib
                    for i in p.vertices if pts[i].x * sign > 0.22]
            if ends:
                sleeve_end = max(sleeve_end, max(ends))
        at = hand + axis * (sleeve_end + 0.002)
        radial = [((p - at) - axis * (p - at).dot(axis)).length for p in mitt_points
                  if p.x * sign > 0 and abs((p - at).dot(axis)) < 0.006]
        if len(radial) < 8:
            raise RuntimeError(f"refine: no mitten surface at the {side} wrist")
        radial.sort()
        r = radial[int(len(radial) * 0.9)]
        geo = Geometry()
        geo.torus(at, axis, r + 0.003, 0.0105, major_segments=36, minor_segments=12)
        notes[f"cuff_{side}"] = {"faces": geo.join_into(mittens, WHITE, f"hand_{side}", f"SK_Mittens_Cuff_{side}"),
                                 "radius_m": round(r, 4), "past_sleeve_m": round(sleeve_end, 4)}
    return notes


def widen_straps(satchel, factor=1.8):
    straps = _material_index(satchel, "satchel_straps")
    pts = _points(satchel)
    to_local = satchel.matrix_world.inverted()
    chest = Vector((0, 0.02, 0.45))
    moved = 0
    for polys in _islands(satchel):
        if satchel.data.polygons[polys[0]].material_index != straps:
            continue
        verts = sorted({i for p in polys for i in satchel.data.polygons[p].vertices})
        if min(pts[i].y for i in verts) > -0.1:
            continue
        bins = defaultdict(list)
        for i in verts:
            bins[round(pts[i].z / 0.006)].append(pts[i].x)
        for i in verts:
            p = pts[i]
            w = _smoothstep(0.12, 0.0, p.y)
            near = [x for k in (-1, 0, 1) for x in bins.get(round(p.z / 0.006) + k, [])]
            xc = sum(near) / len(near)
            q = Vector((xc + (p.x - xc) * (1 + (factor - 1) * w), p.y, p.z))
            q += (q - chest).normalized() * 0.002 * w
            satchel.data.vertices[i].co = to_local @ q
            moved += 1
    satchel.data.update()
    return {"strap_vertices_widened": moved}


def add_buckle(satchel):
    straps = _material_index(satchel, "satchel_straps")
    bvh = _bvh(satchel, keep=lambda p: p.material_index == straps)
    z = 0.465
    hits = []
    for n in range(121):
        x = -0.17 + 0.0011 * n
        hit = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
        if hit[0] is not None and hit[0].y < 0:
            hits.append(hit)
    if not hits:
        raise RuntimeError("refine: the right shoulder strap wasn't found")
    x = (hits[0][0].x + hits[-1][0].x) / 2
    hit = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
    facing = -hit[1].normalized() if hit[1].y > 0 else hit[1].normalized()
    half = (hits[-1][0].x - hits[0][0].x) / 2
    geo = Geometry()
    geo.torus(hit[0] + facing * 0.004, facing, half * 1.25, 0.0045, major_segments=4, minor_segments=8,
              phase=math.pi / 4, stretch=(1.0, 1.0))
    geo.capsule(hit[0] + facing * 0.0055 + Vector((0, 0, half * 0.75)),
                hit[0] + facing * 0.0055 - Vector((0, 0, half * 0.75)), 0.0028, segments=8, rings=2)
    return {"buckle_faces": geo.join_into(satchel, BRASS, "chest", "SK_Satchel_Buckle"),
            "buckle_at": [round(c, 4) for c in hit[0]], "strap_width_m": round(2 * half, 4)}


def baggy_trousers(joggers, rig):
    me = joggers.data
    to_local = joggers.matrix_world.inverted()
    moved = 0
    for side, sign in (("L", 1), ("R", -1)):
        line = sorted((_bone_head(rig, f"{b}_{side}") for b in ("thigh", "shin", "foot")), key=lambda p: -p.z)

        def axis_at(z):
            for a, b in zip(line, line[1:]):
                if a.z >= z >= b.z:
                    t = (a.z - z) / (a.z - b.z)
                    return a.lerp(b, t)
            return line[0] if z > line[0].z else line[-1]

        for v in me.vertices:
            p = joggers.matrix_world @ v.co
            if p.x * sign <= 0:
                continue
            a = axis_at(p.z)
            k = 1.3 + 0.4 * _smoothstep(0.29, 0.17, p.z)
            out = Vector((p.x - a.x, p.y - a.y, 0)) * k
            nx = a.x + out.x
            if nx * sign < 0.012:
                nx = 0.012 * sign
            q = Vector((nx, a.y + out.y, p.z - 0.008 * _smoothstep(0.19, 0.14, p.z)))
            v.co = to_local @ q
            moved += 1
    me.update()
    return {"trouser_vertices_widened": moved}


def ensure_hair_material(library):
    mat = bpy.data.materials.get(HAIR) or bpy.data.materials.new(HAIR)
    mat.diffuse_color = (*HAIR_RGB, 1.0)
    fabric = library.materials.get("fabric_main")
    if fabric is None:
        raise RuntimeError("refine: fabric_main isn't in the material library yet")
    entry = {k: fabric[k] for k in ("alphaMode", "alphaCutoff", "baseMap", "normalMap", "emissionMap")}
    entry.update(name=HAIR, baseColor=[*HAIR_RGB, 1.0], metallic=0.0, roughness=0.62, emissive=[0, 0, 0],
                 doubleSided=False, family=HAIR, skin=None, sources=["avatar/refine"])
    library.materials[HAIR] = entry
    return mat


def _skull_centre(head):
    skin = _material_index(head, "ivory_vinyl")
    pts = _points(head)
    lo, hi = _bounds([pts[i] for p in head.data.polygons if p.material_index == skin for i in p.vertices])
    return (lo + hi) / 2


def make_room(obj, centre, scale, z_from=None, z_to=None):
    to_local = obj.matrix_world.inverted()
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        f = 1.0 if z_from is None else _smoothstep(z_from, z_to, p.z)
        v.co = to_local @ (centre + (p - centre) * (1 + (scale - 1) * f))
    obj.data.update()
    return scale


def refine(rig, library):
    ensure_hair_material(library)
    report = {}
    report.update(remove_hood_crown(_mesh("SK_Hood")))
    centre = _skull_centre(_mesh("SK_Head"))
    report["headwear_room"] = {"SK_Hood": make_room(_mesh("SK_Hood"), centre, 1.04, 0.60, 0.74),
                               "SK_Cap": make_room(_mesh("SK_Cap"), centre, 1.03)}
    report.update(add_hair(_mesh("SK_Head"), [_mesh("SK_Hood"), _mesh("SK_Cap")], rig))
    report.update(add_drawstrings(_mesh("SK_Hoodie")))
    report.update(add_cuffs(_mesh("SK_Mittens"), [_mesh("SK_Hoodie"), _mesh("SK_Crewneck")], rig))
    report.update(widen_straps(_mesh("SK_Satchel")))
    report.update(add_buckle(_mesh("SK_Satchel")))
    report.update(baggy_trousers(_mesh("SK_Joggers"), rig))
    stray = [o.name for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("SK_")]
    if stray:
        raise RuntimeError(f"refine: additions left behind: {stray}")
    return report
