import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "ArtSource/Collections/WordboundPvP"
PALETTE = ["C99250", "8A5E37", "F3E7CE", "839773", "B76B50", "334D42", "DBBE80", "CBB897"]
OAK, WALNUT, CREAM, SAGE, TERRA, DARK, GOLD, LINEN = range(8)
PAINT = None
BRASS = None
ACTIVE = None


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def linear(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4


def colour(value):
    return tuple(linear(int(value[i:i + 2], 16) / 255) for i in (0, 2, 4))


def material(name, value, roughness, metallic=0):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    shader = result.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*colour(value), 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    result.diffuse_color = (*colour(value), 1)
    return result


def materials(output):
    global PAINT, BRASS
    texture = bpy.data.images.new("WordboundPvPPalette", width=512, height=64, alpha=False)
    pixels = []
    for y in range(64):
        for x in range(512):
            cell = x // 64
            base = colour(PALETTE[cell])
            grain = 1 + .018 * math.sin(x * .7 + math.sin(y * .19)) * math.sin(y * 1.2)
            pixels.extend([min(1, channel * grain) for channel in base] + [1])
    texture.pixels = pixels
    texture.filepath_raw = str(output / "Exports/WordboundPvPPalette.png")
    texture.file_format = "PNG"
    texture.save()
    texture.pack()
    PAINT = material("WordboundPvPPaint", "FFFFFF", .62)
    node = PAINT.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = texture
    PAINT.node_tree.links.new(node.outputs["Color"], PAINT.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    BRASS = material("WordboundPvPBrass", "D6AE62", .30, .72)


def empty(name, parent=None, at=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = at
    obj.empty_display_size = .035
    return obj


def finish(obj, name, paint=OAK, metal=False, smooth=False, parent=None):
    obj.name = ACTIVE.name + "_" + name
    obj.parent = parent or ACTIVE
    obj.data.materials.clear()
    obj.data.materials.append(BRASS if metal else PAINT)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
    if not metal:
        for loop in uv.data:
            u, v = loop.uv
            loop.uv = ((paint + .12 + .76 * (u % 1)) / 8, .12 + .76 * (v % 1))
    return obj


def box(name, at, size, paint=OAK, bevel=.014, segments=2, metal=False, rotation=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Rounded editable edges", "BEVEL")
        modifier.width = min(bevel, min(size) * .40)
        modifier.segments = segments
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        modifier = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        modifier.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.rotation_euler = rotation
    return finish(obj, name, paint, metal, parent=parent)


def sphere(name, at, size, paint=CREAM, metal=False, segments=16, rings=8, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=at)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, paint, metal, True, parent)


def lathe(name, profile, paint=OAK, sectors=24, at=(0, 0, 0), metal=False, rotation=(0, 0, 0), parent=None):
    verts = [(radius * math.cos(j * math.tau / sectors), radius * math.sin(j * math.tau / sectors), z)
             for z, radius in profile for j in range(sectors)]
    faces = []
    for i in range(len(profile) - 1):
        for j in range(sectors):
            k = (j + 1) % sectors
            faces.append((i * sectors + j, i * sectors + k, (i + 1) * sectors + k, (i + 1) * sectors + j))
    faces += [tuple(reversed(range(sectors))), tuple((len(profile) - 1) * sectors + j for j in range(sectors))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = ((math.atan2(co.y, co.x) / math.tau) % 1,
                                      (co.z - profile[0][0]) / max(.0001, profile[-1][0] - profile[0][0]))
    obj.location = at
    obj.rotation_euler = rotation
    return finish(obj, name, paint, metal, True, parent)


def rod(name, start, end, radius, paint=OAK, metal=False, sectors=12, parent=None):
    start, end = Vector(start), Vector(end)
    delta = end - start
    obj = lathe(name, [(0, radius * .83), (radius * .2, radius),
                       (delta.length - radius * .2, radius), (delta.length, radius * .83)],
                paint, sectors, start, metal, parent=parent)
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return obj


def torus(name, at, radius, tube, paint=CREAM, metal=False, rotation=(0, 0, 0), segments=32, sides=6, parent=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=sides,
                                    major_radius=radius, minor_radius=tube, location=at, rotation=rotation)
    return finish(bpy.context.object, name, paint, metal, True, parent)


def wrap(name, z0, z1, radius, paint=CREAM, turns=5):
    points, faces = [], []
    count, sides = turns * 20, 5
    for i in range(count + 1):
        angle = math.tau * turns * i / count
        center = Vector((radius * math.cos(angle), radius * math.sin(angle), z0 + (z1 - z0) * i / count))
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        for j in range(sides):
            cross = j * math.tau / sides
            points.append(center + radial * (.003 * math.cos(cross)) + Vector((0, 0, .007 * math.sin(cross))))
    for i in range(count):
        for j in range(sides):
            faces.append((i * sides + j, i * sides + (j + 1) % sides,
                          (i + 1) * sides + (j + 1) % sides, (i + 1) * sides + j))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, name, paint, smooth=True)


def panel(name, outline, thickness, paint, y=0, metal=False, bevel=.008):
    count = len(outline)
    verts = [(x, y + side * thickness / 2, z) for side in (-1, 1) for x, z in outline]
    faces = [tuple(reversed(range(count))), tuple(count + i for i in range(count))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if bevel:
        modifier = obj.modifiers.new("Soft panel edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    finish(obj, name, paint, metal)
    return obj


def bat():
    lathe("Oak_barrel", [(0,.028),(.02,.043),(.04,.043),(.055,.027),(.27,.025),(.32,.033),
                         (.43,.045),(.58,.062),(.78,.081),(.94,.086),(.985,.078),(1.018,.062)], sectors=32)
    lathe("Cream_grip", [(.055,.029),(.065,.032),(.264,.032),(.275,.029)], CREAM)
    wrap("Grip_binding", .07, .26, .032, turns=6)
    lathe("Sage_collar", [(.276,.032),(.286,.039),(.318,.039),(.326,.033)], SAGE)
    torus("Brass_collar", (0,0,.28), .032, .004, metal=True)


def broom():
    box("Brush_back", (0,0,.14), (.30,.095,.065), TERRA, .018)
    for i in range(10):
        box("Bristle_bundle_%02d" % i, ((i-4.5)*.028,0,.064), (.027,.082,.127), CREAM, .008,
            rotation=(0,(i-4.5)*-.008,0))
    lathe("Sage_socket", [(0,.030),(.04,.027),(.073,.021)], SAGE, at=(0,0,.17))
    lathe("Oak_handle", [(0,.018),(.015,.021),(.782,.021),(.80,.016)], at=(0,0,.2212))
    for side in (-1,1):
        sphere("Socket_pin", (side*.025,0,.198), (.006,.009,.009), metal=True, segments=10,rings=5)


def hammer():
    lathe("Oak_handle", [(0,.030),(.02,.040),(.05,.032),(.40,.026),(.46,.040)], sectors=24)
    lathe("Sage_grip", [(.05,.031),(.065,.035),(.22,.035),(.23,.030)], SAGE)
    wrap("Sage_wrap",.065,.215,.035,SAGE,5)
    lathe("Padded_head", [(0,.062),(.02,.0875),(.245,.0875),(.265,.062)], CREAM, sectors=24,
          at=(-.1325,0,.5475),rotation=(0,math.pi/2,0))
    for side in (-1,1):
        lathe("Brass_cap", [(0,.075),(.008,.087),(.023,.087),(.028,.070)], metal=True,
              at=(side*.1325,0,.5475),rotation=(0,side*math.pi/2,0))
    torus("Handle_collar",(0,0,.405),.030,.008,TERRA)


def spear():
    lathe("Oak_shaft",[(0,.021),(.02,.025),(.96,.019),(.99,.025)],sectors=20)
    lathe("Cream_grip",[(.35,.024),(.36,.028),(.57,.028),(.58,.024)],CREAM,sectors=20)
    wrap("Grip_wrap",.365,.565,.028,CREAM,6)
    for z in (.34,.59,.98):
        lathe("Sage_binding",[(0,.026),(.008,.032),(.03,.032),(.038,.026)],SAGE,sectors=20,at=(0,0,z))
    panel("Cream_leaf",[(0,1.235),(.014,1.22),(.085,1.073),(.06,1.02),(0,.993),(-.06,1.02),(-.085,1.073),(-.014,1.22)],
          .047,CREAM,bevel=.007)
    panel("Brass_leaf_socket",[(0,.995),(.055,1.024),(.071,1.082),(.035,1.058),(0,1.02),(-.035,1.058),(-.071,1.082),(-.055,1.024)],
          .068,GOLD,metal=True,bevel=.004)
    lathe("Brass_foot",[(0,.019),(.008,.028),(.039,.028),(.045,.025)],metal=True,sectors=20)


def shield():
    outline=[(0,.57),(.16,.50),(.23,.31),(.17,.11),(0,0),(-.17,.11),(-.23,.31),(-.16,.50)]
    panel("Cream_rim",outline,.061,CREAM,bevel=.016)
    inner=[(x*.84,.285+(z-.285)*.84) for x,z in outline]
    panel("Sage_face",inner,.025,SAGE,y=.045,bevel=.01)
    diamond=[(0,.40),(.085,.285),(0,.17),(-.085,.285)]
    panel("Cream_diamond",diamond,.025,CREAM,y=.066,bevel=.007)
    panel("Terracotta_inset",[(x*.65,.285+(z-.285)*.65) for x,z in diamond],.018,TERRA,y=.084,bevel=.004)
    for x,z in [(0,.53),(0,.04),(-.20,.295),(.20,.295)]:
        box("Brass_tab",(x,.048,z),(.052,.027,.067),metal=True,bevel=.008)
        sphere("Brass_rivet",(x,.066,z),(.009,.007,.009),metal=True,segments=10,rings=5)
    rod("Rear_handle",(0,-.11,.20),(0,-.11,.38),.021,OAK,sectors=16)
    for z in (.20,.38):
        box("Handle_bracket",(0,-.076,z),(.048,.098,.033),metal=True,bevel=.006)


def plate():
    lathe("Ceramic_dish",[(0,.085),(.009,.15),(.025,.21),(.052,.255),(.066,.27),
                          (.073,.265),(.066,.248),(.039,.20),(.025,.12),(.025,.006)],CREAM,sectors=48)
    torus("Sage_inner_ring",(0,0,.055),.236,.007,SAGE,segments=48)
    torus("Brass_outer_rim",(0,0,.072),.269,.004,metal=True,segments=48)


def table():
    box("Cream_top_edge",(0,0,.665),(1.2,.691,.11),CREAM,.035)
    box("Oak_tabletop",(0,0,.724),(1.10,.59,.018),OAK,.007)
    box("Lower_shelf",(0,0,.22),(1.04,.55,.065),OAK,.018)
    for x in (-.49,.49):
        for y in (-.235,.235):
            lathe("Oak_leg",[(0,.038),(.04,.055),(.57,.055),(.61,.045)],OAK,20,at=(x,y,0))
            lathe("Sage_leg_collar",[(0,.056),(.009,.061),(.039,.061),(.045,.056)],SAGE,20,at=(x,y,.49))
            sphere("Brass_pin",(x,y+(.05 if y>0 else -.05),.57),(.009,.006,.009),metal=True,segments=10,rings=5)
    for x in (-.47,.47):
        for y in (-.22,.22):
            box("Sage_corner_inlay",(x,y,.735),(.12,.09,.005),SAGE,.015)


def sofa():
    box("Oak_base",(0,0,.16),(1.43,.72,.12),OAK,.025)
    for x in (-.60,.60):
        for y in (-.27,.27):
            rod("Oak_foot",(x*1.05,y*1.06,0),(x,y,.18),.045,OAK,sectors=16)
    box("Sage_back",(0,-.30,.59),(1.37,.17,.64),SAGE,.06,3)
    for x in (-.657,.657):
        box("Cream_arm_piping",(x,0,.48),(.206,.80,.51),CREAM,.075,3)
        box("Sage_arm_panel",(x,0,.48),(.212,.775,.475),SAGE,.072,3)
        box("Terracotta_arm_pad",(x,0,.70),(.216,.78,.15),TERRA,.06,3)
    for i,x in enumerate((-.43,0,.43)):
        box("Cream_seat_piping_%d" % i,(x,.04,.36),(.435,.63,.15),CREAM,.045,3)
        box("Terracotta_seat_%d" % i,(x,.04,.388),(.416,.615,.16),TERRA,.046,3)
        box("Cream_back_piping_%d" % i,(x,-.235,.68),(.435,.16,.50),CREAM,.05,3)
        box("Terracotta_back_%d" % i,(x,-.213,.68),(.413,.16,.48),TERRA,.05,3)
    sphere("Base_brass_mark",(0,.368,.17),(.018,.006,.008),metal=True,segments=12,rings=6)


def bed():
    box("Oak_frame",(0,0,.245),(.96,1.49,.15),OAK,.025)
    box("Cream_mattress",(0,0,.41),(.94,1.42,.20),CREAM,.075,3)
    box("Sage_quilt",(0,.10,.515),(.92,1.05,.11),SAGE,.045,3)
    box("Terracotta_band",(0,.51,.56),(.95,.23,.04),TERRA,.016)
    box("Cream_pillow",(0,-.48,.55),(.76,.30,.13),CREAM,.06,3)
    for y,z in [(-.72,.66),(.72,.45)]:
        box("Oak_end_panel",(0,y,z),(.91,.075,.30),OAK,.06,3)
        box("Sage_end_inset",(0,y+.043,z),(.76,.025,.20),SAGE,.05,3)
        for x in (-.477,.477):
            lathe("Oak_bedpost",[(0,.035),(.025,.047),(.06,.037),(z+.08,.037),(z+.10,.032)],OAK,16,at=(x,y,0))
            sphere("Rounded_finial",(x,y,z+.12),(.045,.045,.045),OAK,segments=12,rings=6)
            sphere("Brass_end_pin",(x,y+.036,.29),(.008,.005,.008),metal=True,segments=8,rings=4)
    for x in (-.23,0,.23):
        box("Quilt_seam",(x,.08,.571),(.008,.95,.004),LINEN,.001)


def stool():
    lathe("Sage_seat_base",[(.342,.21),(.352,.25),(.392,.26),(.405,.25)],SAGE,sectors=32)
    lathe("Cream_padded_seat",[(.399,.243),(.417,.26),(.445,.249),(.461,.213),(.466,.12)],CREAM,sectors=32)
    for x in (-.145,.145):
        for y in (-.145,.145):
            rod("Oak_leg",(x*1.20,y*1.20,0),(x,y,.35),.035,OAK,sectors=16)
    for x in (-.16,.16):
        rod("Oak_brace",(x,-.16,.15),(x,.16,.15),.022,OAK,sectors=12)
    for y in (-.16,.16):
        rod("Oak_brace",(-.16,y,.15),(.16,y,.15),.022,OAK,sectors=12)
    for x in (-.16,.16):
        for y in (-.16,.16):
            sphere("Brass_joint",(x,y,.15),(.028,.028,.028),metal=True,segments=10,rings=5)


def fan():
    box("Cream_base",(0,0,.06),(.32,.25,.12),CREAM,.05,3)
    box("Sage_base_trim",(0,0,.019),(.32,.25,.038),SAGE,.015)
    lathe("Sage_pedestal",[(.105,.066),(.14,.045),(.34,.028),(.36,.038)],SAGE,sectors=24)
    lathe("Sage_motor",[(0,.11),(.018,.14),(.105,.14),(.13,.10)],SAGE,sectors=32,
          at=(0,-.075,.56),rotation=(math.pi/2,0,0))
    rotor=empty("FAN_Rotor",ACTIVE,(0,.034,.56))
    for i in range(3):
        angle=i*math.tau/3
        blade=sphere("Sage_blade_%d" % i,(math.sin(angle)*.119,0,math.cos(angle)*.119),
                     (.085,.014,.145),SAGE,segments=12,rings=6,parent=rotor)
        blade.rotation_euler.y=angle
    torus("Cream_head_edge",(0,.005,.56),.245,.016,CREAM,rotation=(math.pi/2,0,0),segments=40)
    torus("Brass_grille",(0,.070,.56),.255,.006,metal=True,rotation=(math.pi/2,0,0),segments=40)
    for i in range(8):
        angle=i*math.tau/8
        rod("Brass_grille_spoke",(0,.084,.56),(.254*math.sin(angle),.07,.56+.254*math.cos(angle)),
            .0045,metal=True,sectors=8)
    sphere("Cream_hub",(0,.087,.56),(.073,.022,.073),CREAM,segments=24,rings=10)
    lathe("Sage_knob",[(0,.023),(.016,.026),(.033,.021)],SAGE,sectors=16,at=(0,.061,.105))


def clock():
    lathe("Oak_case",[(0,.215),(.018,.24),(.085,.24),(.10,.219)],OAK,sectors=48,
          at=(0,-.05,.249),rotation=(-math.pi/2,0,0))
    lathe("Cream_dial",[(0,.207),(.011,.214),(.018,.21)],CREAM,sectors=48,
          at=(0,.048,.249),rotation=(-math.pi/2,0,0))
    for i in range(12):
        angle=i*math.tau/12
        box("Brass_tick_%02d" % i,(math.sin(angle)*.177,.071,.249+math.cos(angle)*.177),
            (.011,.008,.037),metal=True,bevel=.004,rotation=(0,angle,0))
    for name,angle,length in [("Hour",-.9,.125),("Minute",.8,.165)]:
        parent=empty("CLOCK_"+name+"Hand",ACTIVE,(0,.081,.249))
        parent.rotation_euler.y=angle
        box(name+"_sage_hand",(0,0,length*.42),(.020,.013,length),DARK,.008,parent=parent)
    sphere("Brass_center_pin",(0,.097,.249),(.025,.014,.025),metal=True,segments=16,rings=8)
    for x in (-.16,.16):
        sphere("Oak_foot",(x,0,.035),(.045,.052,.035),OAK,segments=16,rings=8)


BUILDERS={name.upper():function for name,function in list(globals().items())
          if name in ("bat","broom","hammer","spear","shield","plate","table","sofa","bed","stool","fan","clock")}


def descendants(root):
    return [root]+list(root.children_recursive)


def bounds(root):
    bpy.context.view_layer.update()
    points=[root.matrix_world.inverted() @ obj.matrix_world @ vertex.co
            for obj in descendants(root) if obj.type=="MESH" for vertex in obj.data.vertices]
    return Vector([min(p[i] for p in points) for i in range(3)]),Vector([max(p[i] for p in points) for i in range(3)])


def normalize(root,spec):
    low,high=bounds(root)
    target=Vector((spec["sizeUnityMetres"][0],spec["sizeUnityMetres"][2],spec["sizeUnityMetres"][1]))
    scale=Vector([target[i]/(high[i]-low[i]) for i in range(3)])
    offset=Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))
    original_children=list(root.children)
    anchor=empty(root.name+"_NormalizedGeometry",root)
    for obj in original_children:
        obj.parent=anchor
    anchor.location=Vector([offset[i]*scale[i] for i in range(3)])
    anchor.scale=scale
    center_depth={"SOFA":.0165,"SHIELD":-.030825,"CLOCK":.031}.get(spec["id"],0)
    anchor.location.y+=center_depth
    bpy.context.view_layer.update()
    grip=spec["gripUnityMetres"]
    empty(root.name+"_Grip_R",root,(grip[0],grip[2],grip[1]))
    empty(root.name+"_Front",root,(0,target.y*.6,target.z*.5))
    root["item_id"]=spec["id"]
    root["held_scale"]=spec["heldScale"]
    root["collision_owner"]="gameplay; visual model contains no colliders"


def inspect_model(root,spec):
    meshes=[obj for obj in descendants(root) if obj.type=="MESH"]
    total=0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        total+=len(obj.data.loop_triangles)
    low,high=bounds(root)
    mats=sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    if total>6000 or len(mats)>2:
        raise RuntimeError(f"{root.name}: over budget: {total} triangles, {len(mats)} materials")
    if abs(low.z)>.0001:
        raise RuntimeError(f"{root.name}: not grounded")
    return {"id":spec["id"],"triangles":total,"triangle_budget":6000,"materials":mats,
            "mesh_count":len(meshes),"parts":[obj.name for obj in descendants(root)],
            "bounds_blender_min":list(low),"bounds_blender_max":list(high),
            "size_unity_metres":spec["sizeUnityMetres"],"grip_unity_metres":spec["gripUnityMetres"],
            "held_scale":spec["heldScale"],"concept":spec["destination"],
            "has_uv":all(bool(obj.data.uv_layers) for obj in meshes)}


def export(root,output,spec):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    marker=bpy.data.objects[root.name+"_Grip_R"]
    original_name=marker.name
    marker.name="Grip_R"
    fbx=output/"Exports"/(spec["id"]+".fbx")
    glb=output/"Exports"/(spec["id"]+".glb")
    try:
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={"EMPTY","MESH"},
            apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",axis_forward="-Z",axis_up="Y",
            bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type="FACE",use_tspace=False,
            use_custom_props=True,add_leaf_bones=False,bake_anim=False,path_mode="RELATIVE",embed_textures=False)
        bpy.ops.export_scene.gltf(filepath=str(glb),export_format="GLB",use_selection=True,
            export_yup=True,export_apply=True,export_animations=False,export_extras=True,export_materials="EXPORT")
    finally:
        marker.name=original_name
    return {key:{"path":str(file.relative_to(output)).replace("\\","/"),"sha256":sha256(file),"bytes":file.stat().st_size}
            for key,file in (("fbx",fbx),("glb",glb))}


def studio():
    world=bpy.data.worlds.new("Wordbound studio")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs[0].default_value=(.78,.83,.9,1)
    world.node_tree.nodes["Background"].inputs[1].default_value=.45
    scene=bpy.context.scene
    scene.world=world
    for name,at,power,size,tint in [("Key",(-3,4,5),420,4,(1,.88,.73)),
                                   ("Fill",(3,1,3),220,3,(.78,.88,1)),
                                   ("Rim",(1,-3,4),300,3,(1,.94,.82))]:
        data=bpy.data.lights.new(name,"AREA")
        data.energy,data.size,data.color=power,size,tint
        obj=bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj)
        obj.location=at
        obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat("-Z","Y").to_euler()
    data=bpy.data.cameras.new("Wordbound preview camera")
    camera=bpy.data.objects.new("Wordbound preview camera",data)
    scene.collection.objects.link(camera)
    data.type="ORTHO"
    scene.camera=camera
    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=4
    scene.render.threads_mode="FIXED"
    scene.render.threads=6
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.film_transparent=True
    scene.view_settings.view_transform="AgX"
    return camera


def render(root,camera,output,item):
    low,high=bounds(root)
    center=(low+high)/2
    scene=bpy.context.scene
    for view,offset in [("front",(0,4,.3)),("side",(4,0,.3)),("three-quarter",(2.5,4,2.4)),("icon",(2.5,4,2.4))]:
        camera.location=center+Vector(offset)
        camera.rotation_euler=(center-camera.location).to_track_quat("-Z","Y").to_euler()
        inverse=camera.rotation_euler.to_quaternion().inverted()
        points=[inverse@(obj.matrix_world@v.co-center) for obj in descendants(root) if obj.type=="MESH" for v in obj.data.vertices]
        span=max(max(p[i] for p in points)-min(p[i] for p in points) for i in (0,1))
        camera.data.ortho_scale=span*1.22
        scene.render.resolution_x=scene.render.resolution_y=512 if view=="icon" else 640
        dest=output/("Icons" if view=="icon" else "Previews")/(item+("" if view=="icon" else "-"+view)+".png")
        scene.render.filepath=str(dest)
        bpy.ops.render.render(write_still=True)
        print("WORDBOUND_RENDER "+str(dest),flush=True)


def preview_records(output,item):
    records=[]
    for view in ("front","side","three-quarter","icon"):
        file=output/("Icons" if view=="icon" else "Previews")/(item+("" if view=="icon" else "-"+view)+".png")
        if file.exists():
            records.append({"view":view,"path":str(file.relative_to(output)).replace("\\","/"),
                            "sha256":sha256(file),"bytes":file.stat().st_size})
    return records


def render_saved(output,selected=None):
    manifest=json.loads((output/"manifest.json").read_text(encoding="utf-8"))
    source=output/manifest["source"]["path"]
    if sha256(source)!=manifest["source"]["sha256"]:
        raise RuntimeError("Editable source differs from the production manifest")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    roots=[bpy.data.objects[entry["id"]] for entry in manifest["assets"]]
    for root in roots:
        root.location=(0,0,0)
    bpy.context.view_layer.update()
    for root in roots:
        if selected and root.name not in selected:
            continue
        for other in roots:
            for obj in descendants(other):
                obj.hide_render=other!=root
        render(root,bpy.context.scene.camera,output,root.name)
    for entry in manifest["assets"]:
        entry["previews"]=preview_records(output,entry["id"])
    manifest["status"]="source-exports-and-previews-produced"
    manifest["render_generator_sha256"]=sha256(__file__)
    (output/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")


def verify(output):
    manifest=json.loads((output/"manifest.json").read_text(encoding="utf-8"))
    checks=[]
    for entry in manifest["assets"]:
        for format_name in ("fbx","glb"):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            file=output/entry["exports"][format_name]["path"]
            errors=[]
            if sha256(file)!=entry["exports"][format_name]["sha256"]:
                errors.append("file hash differs")
            if format_name=="fbx":
                bpy.ops.import_scene.fbx(filepath=str(file))
            else:
                bpy.ops.import_scene.gltf(filepath=str(file))
            objects=list(bpy.context.scene.objects)
            meshes=[obj for obj in objects if obj.type=="MESH"]
            triangles=0
            degenerate=0
            for obj in meshes:
                obj.data.calc_loop_triangles()
                triangles+=len(obj.data.loop_triangles)
                degenerate+=sum(triangle.area<1e-12 for triangle in obj.data.loop_triangles)
            points=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
            low=[min(v[i] for v in points) for i in range(3)]
            high=[max(v[i] for v in points) for i in range(3)]
            error=max(abs(got-want) for actual,expected in ((low,entry["bounds_blender_min"]),(high,entry["bounds_blender_max"]))
                      for got,want in zip(actual,expected))
            if triangles!=entry["triangles"]:
                errors.append(f"triangle mismatch {triangles}")
            if degenerate:
                errors.append(f"degenerate triangles {degenerate}")
            if error>.001:
                errors.append(f"bounds drift {error}")
            if any(obj.type in {"CAMERA","LIGHT"} for obj in objects):
                errors.append("embedded camera or light")
            marker=next((obj for obj in objects if obj.name=="Grip_R"),None)
            expected=entry["grip_unity_metres"]
            grip=Vector((expected[0],expected[2],expected[1]))
            if not marker or (marker.matrix_world.translation-grip).length>.001:
                errors.append("Grip_R missing or moved")
            mats={slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}
            if len(mats)>2 or not mats:
                errors.append("material count invalid")
            if any(not obj.data.uv_layers for obj in meshes):
                errors.append("missing UVs")
            images=[node.image for mat in bpy.data.materials if mat.use_nodes
                    for node in mat.node_tree.nodes if node.type=="TEX_IMAGE" and node.image]
            if not images or any(max(image.size)>512 or min(image.size)<1 for image in images):
                errors.append("palette texture missing or oversized")
            checks.append({"id":entry["id"],"format":format_name,"passed":not errors,"triangles":triangles,
                           "max_bounds_error_metres":error,"materials":sorted(mats),"errors":errors})
            print("WORDBOUND_CHECK "+json.dumps(checks[-1]),flush=True)
    report={"blender":bpy.app.version_string,"passed":all(check["passed"] for check in checks),
            "formats":["fbx","glb"],"native_unity_validation":"NOT RUN","checks":checks}
    (output/"export-verification.json").write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    if not report["passed"]:
        raise RuntimeError("Wordbound export validation failed")


def main():
    global ACTIVE
    parser=argparse.ArgumentParser()
    parser.add_argument("--only",default="")
    parser.add_argument("--no-render",action="store_true")
    parser.add_argument("--verify-only",action="store_true")
    parser.add_argument("--render-only",action="store_true")
    argv=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    args=parser.parse_args(argv)
    if args.verify_only:
        verify(OUTPUT)
        return
    if args.render_only:
        selected={item.strip().upper() for item in args.only.split(",") if item.strip()}
        render_saved(OUTPUT,selected)
        return
    specifications=json.loads((OUTPUT/"prompts.json").read_text(encoding="utf-8"))["entries"]
    selected={item.strip().upper() for item in args.only.split(",") if item.strip()}
    if selected:
        specifications=[item for item in specifications if item["id"] in selected]
    if not specifications:
        raise RuntimeError("No selected assets")
    for directory in ("Source","Exports","Icons","Previews"):
        (OUTPUT/directory).mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    materials(OUTPUT)
    entries=[]
    roots=[]
    for spec in specifications:
        if not (OUTPUT/spec["destination"]).exists():
            raise RuntimeError("Missing concept "+spec["id"])
        ACTIVE=empty(spec["id"])
        BUILDERS[spec["id"]]()
        normalize(ACTIVE,spec)
        entry=inspect_model(ACTIVE,spec)
        entry["concept_sha256"]=sha256(OUTPUT/spec["destination"])
        entry["exports"]=export(ACTIVE,OUTPUT,spec)
        collection=bpy.data.collections.new(spec["id"]+" editable parts")
        scene.collection.children.link(collection)
        for obj in descendants(ACTIVE):
            for old in list(obj.users_collection):
                old.objects.unlink(obj)
            collection.objects.link(obj)
        entries.append(entry)
        roots.append(ACTIVE)
        print("WORDBOUND_ASSET "+json.dumps({"id":entry["id"],"triangles":entry["triangles"],
              "materials":entry["materials"],"size":entry["size_unity_metres"]}),flush=True)
    camera=studio()
    if not args.no_render:
        for root in roots:
            for other in roots:
                for obj in descendants(other):
                    obj.hide_render=other!=root
            render(root,camera,OUTPUT,root.name)
        for entry in entries:
            entry["previews"]=preview_records(OUTPUT,entry["id"])
    for root in roots:
        for obj in descendants(root):
            obj.hide_render=False
    for i,root in enumerate(roots):
        root.location=((i%4)*2.1,(i//4)*2.2,0)
    source=OUTPUT/"Source/WordboundPvP.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    manifest={"collection":"WordboundPvP","status":"source-and-exports-produced",
        "blender":bpy.app.version_string,"generator":"Tools/AssetPipeline/build_wordbound_pvp.py",
        "generator_sha256":sha256(__file__),
        "provenance":"Original deterministic Blender geometry authored from the separately recorded OpenAI concept sheets. No external paid provider or image-to-3D conversion.",
        "source":{"path":"Source/WordboundPvP.blend","sha256":sha256(source)},
        "palette":{"path":"Exports/WordboundPvPPalette.png","size":[512,64],"srgb_hex":PALETTE,
                   "sha256":sha256(OUTPUT/"Exports/WordboundPvPPalette.png")},
        "export":{"metres":True,"axis_forward":"-Z","axis_up":"Y","cameras_lights":False,"colliders":False},
        "material_specs":[{"name":"WordboundPvPPaint","base_map":"WordboundPvPPalette.png","roughness":.62,"metallic":0},
                          {"name":"WordboundPvPBrass","srgb_hex":"D6AE62","roughness":.30,"metallic":.72}],
        "placement":"FBX roots grounded at origin. Preserve imported axis transforms under placement anchor. Source Blender +Y faces Unity +Z. Export Grip_R is exact catalogue grip; source markers have item prefix.",
        "native_unity_validation":"NOT RUN; staged outside game Assets","assets":entries}
    (OUTPUT/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    print("WORDBOUND_SOURCE_READY "+str(OUTPUT),flush=True)


if __name__=="__main__":
    main()
