"""Separate FAN/HAMMER construction pilots from original game geometry."""
import argparse
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys
import time

import bpy
import bmesh
from mathutils import Matrix, Vector

spec = importlib.util.spec_from_file_location("original_pilot_core", Path(__file__).with_name("build_wordbound_original_pilot.py"))
core = importlib.util.module_from_spec(spec)
spec.loader.exec_module(core)

SOURCE = {
    "FAN": "67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7",
    "HAMMER": "b397ce961e7d7369118ca573e4bd5853391a3a1ea4c59076c686f13f4312b67f",
}
VIEWS = ("three-quarter", "front", "side", "rear", "detail", "clay", "scale", "held")


def finish(obj, name, material, target, bevel=0):
    obj.name = name
    core.move_to(obj, target)
    if material:
        obj.data.materials.append(material)
    if obj.type == "MESH":
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) == 4
        if bevel:
            mod = obj.modifiers.new("SmallConstructionEdge", "BEVEL")
            mod.width, mod.segments = bevel, 3
            mod.affect = "EDGES"
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
    obj["pilot"] = "Original-based construction detail; visual review pending"
    return obj


def cylinder(name, location, radius, depth, material, target, axis=(0,0,1), bevel=0, sides=48, oval=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    if oval:
        obj.scale.x, obj.scale.y = oval
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler = Vector(axis).to_track_quat("Z", "Y").to_euler()
    return finish(obj, name, material, target, bevel)


def beam(name, start, end, radius, material, target):
    start, end = Vector(start), Vector(end)
    return cylinder(name, (start+end)/2, radius, (end-start).length, material, target, end-start, sides=24)


def ring(name, center, radius, wire, material, target):
    bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=8, major_radius=radius, minor_radius=wire,
                                    location=center, rotation=(math.pi/2,0,0))
    return finish(bpy.context.object, name, material, target)


def mesh(name, vertices, faces, material, target, smooth=False):
    data = bpy.data.meshes.new(name + "_Geometry")
    data.from_pydata(vertices, [], faces)
    data.materials.append(material)
    data.update()
    for face in data.polygons:
        face.use_smooth = smooth
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    return obj


def original_parts(item, original, target):
    parts, materials = [], {}
    for source in original:
        if source.type != "MESH":
            continue
        for faces in core.component_faces(source.data):
            source_material = source.data.materials[faces[0].material_index]
            key = source_material.name.split("_Classic")[0]
            if key not in materials:
                materials[key] = source_material.copy()
                materials[key].name = item + "_OriginalBased_" + key
            part = core.make_part(source, faces, target, materials[key])
            lo, hi = core.bounds([part]); center = (lo+hi)/2
            if item == "HAMMER":
                role = {"wood_light":"PaleWoodHandle", "wood_dark":"OriginalGripBand", "metal":"SteelHead", "gold":"OriginalEndCap"}[key]
            elif key == "blue":
                role = "BlueGuardRim" if hi.z-lo.z > .5 else "BlueBase"
            elif key == "metal":
                role = "OriginalStand" if hi.x-lo.x < .08 else "OriginalFrontGrille"
            else:
                role = "OriginalBlade" if key == "ivory" else "OriginalRotorCap"
            part.name = item + "_" + role + "_" + str(len(parts)+1).zfill(2)
            part.data.name = part.name + "_EditableGeometry"
            part["source_material"] = key
            part["source_bounds"] = json.dumps([list(lo), list(hi)])
            parts.append(part)
    expected = 11
    if len(parts) != expected:
        raise RuntimeError("Original component structure changed: " + item + " " + str(len(parts)))
    return parts, materials


def fan_details(parts, materials, target):
    blue, silver, ivory = materials["blue"], materials["metal"], materials["ivory"]
    recess = blue.copy(); recess.name = "FAN_MotorVentInterior"
    shader = recess.node_tree.nodes.get("Principled BSDF")
    for link in tuple(shader.inputs["Base Color"].links):
        recess.node_tree.links.remove(link)
    shader.inputs["Base Color"].default_value = (.025,.045,.065,1)
    shader.inputs["Roughness"].default_value = .8
    profile = ((.035,.047),(.042,.063),(.101,.063),(.113,.057))
    vertices = [(r*math.cos(math.tau*j/64), y, .56+r*math.sin(math.tau*j/64)) for y,r in profile for j in range(64)]
    faces = []
    for i in range(len(profile)-1):
        for j in range(64):
            k=(j+1)%64
            faces.append((i*64+j,(i+1)*64+j,(i+1)*64+k,i*64+k))
    faces.append(tuple(range(64)))
    shell=mesh("FAN_RearMotorShell",vertices,faces,blue,target,True)
    shell.data.polygons[-1].use_smooth=False
    cylinder("FAN_MotorInteriorBehindVents",(0,.103,.56),.056,.004,recess,target,(0,1,0))
    ring("FAN_RearVentFrame",(0,.112,.56),.051,.006,blue,target)
    cylinder("FAN_RearBearingCover",(0,.112,.56),.014,.006,blue,target,(0,1,0),.001)
    for i in range(12):
        angle=math.tau*i/12
        direction=Vector((math.cos(angle),0,math.sin(angle)))
        origin=Vector((0,.112,.56))
        beam("FAN_RearVentRib_"+str(i+1),origin+direction*.014,origin+direction*.051,.0018,blue,target)
    beam("FAN_ConnectedMotorShaft",(0,.012,.56),(0,.043,.56),.010,silver,target)
    beam("FAN_StandToTiltNeck",(0,0,.38),(0,.072,.46),.018,silver,target)
    beam("FAN_TiltYokeCrossbar",(-.075,.072,.463),(.075,.072,.463),.009,silver,target)
    for sign, side in ((-1,"Left"),(1,"Right")):
        beam("FAN_"+side+"TiltSupport",(sign*.075,.072,.463),(sign*.075,.072,.56),.009,silver,target)
        beam("FAN_"+side+"TiltAxle",(sign*.057,.072,.56),(sign*.085,.072,.56),.008,silver,target)
        cylinder("FAN_"+side+"TiltFastener",(sign*.086,.072,.56),.011,.005,blue,target,(1,0,0),.001)
    ring("FAN_RearWireGuard",(0,.052,.56),.215,.0025,silver,target)
    ring("FAN_RearGuardInnerSupport",(0,.052,.56),.074,.0025,silver,target)
    for i in range(12):
        angle=math.tau*i/12
        direction=Vector((math.cos(angle),0,math.sin(angle)))
        start=Vector((0,.052,.56))+direction*.074
        middle=Vector((0,.052,.56))+direction*.215
        end=Vector((0,.012,.56))+direction*.246
        beam("FAN_RearGuardSpoke_"+str(i+1),start,middle,.0018,silver,target)
        beam("FAN_RearGuardMount_"+str(i+1),middle,end,.0018,silver,target)
    for i in range(4):
        angle=math.pi/4+math.pi*i/2
        direction=Vector((math.cos(angle),0,math.sin(angle)))
        beam("FAN_FrontGuardClip_"+str(i+1),Vector((0,-.061,.56))+direction*.216,
             Vector((0,-.010,.56))+direction*.247,.0025,silver,target)
    cylinder("FAN_BaseSpeedDial",(0,-.052,.054),.015,.011,ivory,target,bevel=.0015)
    beam("FAN_DialIndicator",(0,-.060,.060),(0,-.049,.060),.0012,silver,target)
    return ["Original blue rim/base, silver front grille/stand and pale blades retained",
            "Open rear ventilation ribs sit over recessed motor interior; motor shaft overlaps original rotor cap",
            "Tilt supports/axles join motor shell to existing stand; rear guard and four front clips join original rim",
            "Small base speed dial uses original ivory/silver material language"]


def hammer_details(parts, materials, target):
    head = next(part for part in parts if part["source_material"] == "metal")
    original_bounds = core.bounds([head])
    bpy.context.view_layer.objects.active = head
    head.select_set(True)
    weld = head.modifiers.new("JoinCoincidentSourceHeadSeams", "WELD")
    weld.merge_threshold = .0000001
    bpy.ops.object.modifier_apply(modifier=weld.name)

    def require_closed():
        topology = bmesh.new()
        try:
            topology.from_mesh(head.data)
            if not topology.faces or any(not edge.is_manifold for edge in topology.edges):
                raise RuntimeError("Hammer steel head must remain a closed manifold")
        finally:
            topology.free()

    require_closed()
    cutter = cylinder("HAMMER_TemporaryEyeTool",(0,0,.54),1,.26,None,target,oval=(.0405,.037))
    modifier=head.modifiers.new("ActualHandleEye", "BOOLEAN")
    modifier.operation,modifier.solver,modifier.object="DIFFERENCE","EXACT",cutter
    bpy.context.view_layer.objects.active=head
    head.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    head.select_set(False)
    bpy.data.objects.remove(cutter,do_unlink=True)
    bpy.context.view_layer.update()
    require_closed()
    if any((a-b).length>.00001 for a,b in zip(original_bounds,core.bounds([head]))):
        raise RuntimeError("Hammer eye changed steel head outer bounds")
    ray_z = original_bounds[1].z + .05
    ray_depth = original_bounds[1].z-original_bounds[0].z+.1
    samples = [(0,0),(.02025,0),(-.02025,0),(0,.0185),(0,-.0185)]
    for x,y in samples:
        if head.ray_cast(Vector((x,y,ray_z)),Vector((0,0,-1)),distance=ray_depth)[0]:
            raise RuntimeError("Hammer eye still contains steel at an interior section probe")
    for x in (-.06075,.06075):
        if not head.ray_cast(Vector((x,0,ray_z)),Vector((0,0,-1)),distance=ray_depth)[0]:
            raise RuntimeError("Hammer eye removed expected surrounding steel")
    head["eye_validation"] = "Closed manifold before/after; five empty eye rays, two retained-steel rays; original head bounds"
    cylinder("HAMMER_WoodTenonThroughEye",(0,0,.56975),1,.1295,materials["wood_light"],target,
             oval=(.0395,.0355),bevel=.0006)
    bottom,top=.612,.6348
    vertices=[(-.0006,-.018,bottom),(.0006,-.018,bottom),(.0006,.018,bottom),(-.0006,.018,bottom),
              (-.003,-.018,top),(.003,-.018,top),(.003,.018,top),(-.003,.018,top)]
    faces=((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
    mesh("HAMMER_FittedSteelWedge",vertices,faces,materials["metal"],target)
    return ["Original rectangular blue-gray steel head, thin warm end caps, pale handle and seven grip bands retained",
            "Candidate head UV seam vertices welded at 0.0000001 m for a closed solid; source model untouched",
            "Actual eye cut through head interior; manifold, five eye/two surrounding section rays and outer bounds guarded",
            "Pale wood tenon overlaps original handle inside head and fills the eye; small inset steel wedge closes the connection",
            "No ivory mallet head, decorative plates or aging/noise added"]


def avatar_context(source_root, original, candidate, grip, scale):
    target=core.collection("OriginalAvatarContext")
    objects=core.import_original(source_root/"Web/public/art/avatar.glb",target)
    wardrobe=json.loads((source_root/"Web/public/art/manifest.json").read_text(encoding="utf-8"))["wardrobe"]
    defaults={"SK_Head"}|{p["mesh"] for p in wardrobe["pieces"] if p["id"] in wardrobe["default"]["pieces"].values()}
    wrapper=bpy.data.objects.new("AvatarContextRoot",None);target.objects.link(wrapper)
    for obj in objects:
        if obj.parent is None:obj.parent=wrapper
        if obj.type=="MESH" and obj.name.split(".")[0] not in defaults:obj.hide_render=True
        if obj.type=="ARMATURE" and obj.animation_data:
            for track in obj.animation_data.nla_tracks:track.mute=True
            action=next((a for a in bpy.data.actions if a.name=="Hold_OneHand"),None)
            if action is None:raise RuntimeError("Original Hold_OneHand action missing")
            strip=next((strip for track in obj.animation_data.nla_tracks for strip in track.strips if strip.action==action),None)
            obj.animation_data.action=action
            if strip and strip.action_slot:obj.animation_data.action_slot=strip.action_slot
            if obj.animation_data.action_slot is None:raise RuntimeError("Original Hold_OneHand action slot missing")
    bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    socket=next((obj.matrix_world.copy() for obj in objects if obj.name.split(".")[0]=="grip_R"),None)
    if socket is None:
        socket=next((obj.matrix_world@obj.pose.bones["grip_R"].matrix for obj in objects if obj.type=="ARMATURE" and "grip_R" in obj.pose.bones),None)
    if socket is None:raise RuntimeError("Original avatar right grip missing")
    for name,source in (("Original",original),("Candidate",candidate)):
        held=core.collection("Held"+name)
        root=bpy.data.objects.new("Held"+name+"Root",None);held.objects.link(root)
        for obj in source.all_objects:
            if obj.type not in ("MESH","CURVE"):continue
            clone=obj.copy();clone.data=obj.data;held.objects.link(clone)
            clone.parent=root;clone.matrix_local=obj.matrix_world.copy()
        root.matrix_world=socket@Matrix.Scale(scale,4)@Matrix.Translation(-grip.translation)
        held.hide_render=held.hide_viewport=True
    target.hide_render=target.hide_viewport=True


def build(args):
    item,out=args.item,args.output_root
    if out.exists():raise RuntimeError("Preserve previous output; choose a new pilot revision")
    source=args.source_root/"Web/public/art/items"/(item+".glb")
    if core.digest(source)!=SOURCE[item]:raise RuntimeError("Original reference bytes changed")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    original=core.collection("OriginalItem");objects=core.import_original(source,original)
    original_bounds=core.bounds(objects)
    grip=next(obj.matrix_world.copy() for obj in objects if obj.name.startswith("Grip_R"))
    candidate=core.collection("CandidateItem")
    parts,materials=original_parts(item,objects,candidate)
    notes=(fan_details if item=="FAN" else hammer_details)(parts,materials,candidate)
    bpy.context.view_layer.update()
    candidate_bounds=core.bounds(candidate.all_objects)
    if any((a-b).length>.00001 for a,b in zip(original_bounds,candidate_bounds)):
        raise RuntimeError("Construction changed original overall bounds: "+str([list(v) for v in candidate_bounds]))
    grip_obj=bpy.data.objects.new(item+"_Grip_R_OriginalTransform",None);candidate.objects.link(grip_obj);grip_obj.matrix_world=grip
    definitions=json.loads((args.source_root/"Assets/_Project/Data/Config/items.json").read_text(encoding="utf-8"))["items"]
    held_scale=next(row["heldScale"] for row in definitions if row["id"]==item)
    core.studio();avatar_context(args.source_root,original,candidate,grip,held_scale)
    original.hide_render=original.hide_viewport=True
    out.mkdir(parents=True);(out/"Source").mkdir();(out/"Previews").mkdir()
    shutil.copy2(source,out/"Source"/(item+".original.glb"))
    shutil.copy2(__file__,out/"Source"/Path(__file__).name)
    shutil.copy2(core.__file__,out/"Source"/Path(core.__file__).name)
    bpy.ops.file.pack_all()
    blend=out/(item+"-original-based-pilot.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    meshes=[obj for obj in candidate.all_objects if obj.type=="MESH"]
    core.write_json(out/"build-receipt.json",{"status":"VISUAL CONSTRUCTION PILOT — NOT APPROVED OR IMPORTED INTO UNITY","item":item,
        "source_sha256":SOURCE[item],"build_script_sha256":core.digest(__file__),"helper_sha256":core.digest(core.__file__),
        "blender":bpy.app.version_string,"blend_sha256":core.digest(blend),"original_bounds_blender_metres":[list(v) for v in original_bounds],
        "candidate_bounds_blender_metres":[list(v) for v in candidate_bounds],"grip_world_matrix_blender":[list(row) for row in grip],
        "held_scale_from_original_config":held_scale,"original_components":len(parts),"candidate_mesh_components":len(meshes),
        "candidate_triangles":sum(sum(len(face.vertices)-2 for face in obj.data.polygons) for obj in meshes),
        "parts":[obj.name for obj in meshes],"construction":notes,
        "limitations":["Construction/contact/normal quality requires actual front, side, rear and detail inspection",
            "Held scene is a static original-avatar grip-aligned source pose, not Unity hand-fit verification",
            "No delivery export, baked map, runtime replacement, performance or owner-appearance acceptance"]})
    print("TOOL_PILOT_BUILT "+str(out),flush=True)


def render(args):
    out,item=args.output_root,args.item
    receipt=json.loads((out/"build-receipt.json").read_text(encoding="utf-8"))
    blend=out/(item+"-original-based-pilot.blend")
    if receipt["item"]!=item or core.digest(blend)!=receipt["blend_sha256"]:raise RuntimeError("Pilot source mismatch")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    scene=bpy.context.scene;scene.cycles.samples=args.samples
    scene.render.threads_mode,scene.render.threads="FIXED",args.threads
    lo,hi=(Vector(v) for v in receipt["original_bounds_blender_metres"])
    for view in args.views.split(","):
        if view not in VIEWS:raise ValueError("Unknown view "+view)
        held=view=="held";context=view in ("held","scale")
        center=(lo+hi)/2
        scale=max(hi-lo)*1.28
        offset={"three-quarter":(2.3,-3.6,2.3),"front":(0,-8,.4),"side":(8,0,.4),"rear":(2.3,3.6,2.3),
                "detail":(2.2,3,1.8) if item=="FAN" else (2.1,-3.3,2.8),"clay":(2.3,-3.6,2.3),"scale":(2.8,-4.5,2),"held":(2.8,-4.5,2)}[view]
        if view=="detail":center=Vector((0,.065,.56)) if item=="FAN" else Vector((0,0,.56));scale=.32 if item=="FAN" else .43
        if context:center=Vector((.32 if view=="scale" else 0,0,.55));scale=1.7
        scene.camera.location=center+Vector(offset)
        scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat("-Z","Y").to_euler()
        scene.camera.data.ortho_scale=scale
        scene.render.resolution_x=scene.render.resolution_y=args.size;scene.render.resolution_percentage=100
        bpy.data.collections["OriginalAvatarContext"].hide_render=not context
        bpy.data.collections["OriginalAvatarContext"].hide_viewport=not context
        bpy.data.objects["AvatarContextRoot"].location=(.75,0,0) if view=="scale" else (0,0,0)
        scene.view_layers[0].material_override=bpy.data.materials["NeutralClay"] if view=="clay" else None
        for variant in ("original","candidate"):
            for name in ("OriginalItem","CandidateItem","HeldOriginal","HeldCandidate"):
                visible=(name==("Held"+variant.title()) if held else name==(variant.title()+"Item"))
                bpy.data.collections[name].hide_render=bpy.data.collections[name].hide_viewport=not visible
            bpy.context.view_layer.update()
            path=out/"Previews"/(item+"-"+view+"-"+variant+".png");meta=path.with_suffix(".json")
            settings={"item":item,"variant":variant,"view":view,"blend_sha256":receipt["blend_sha256"],
                      "render_script_sha256":core.digest(__file__),"helper_sha256":core.digest(core.__file__),"blender":bpy.app.version_string,
                      "camera_matrix":[list(row) for row in scene.camera.matrix_world],"ortho_scale":scene.camera.data.ortho_scale,
                      "resolution":[args.size,args.size],"samples":args.samples,"threads":args.threads,"denoising":True,
                      "label":"Static source grip pose, not Unity verification" if held else "Matched original-game studio comparison"}
            if path.exists() or meta.exists():
                previous=json.loads(meta.read_text(encoding="utf-8")) if meta.exists() else {}
                if not path.exists() or core.digest(path)!=previous.get("sha256") or any(previous.get(k)!=v for k,v in settings.items()):
                    raise RuntimeError("Existing render differs; preserve it and choose a new revision: "+str(path))
                continue
            scene.render.filepath=str(path);started=time.monotonic();bpy.ops.render.render(write_still=True)
            core.write_json(meta,{**settings,"sha256":core.digest(path),"elapsed_seconds":round(time.monotonic()-started,3)})
            print("TOOL_PILOT_RENDERED "+str(path),flush=True)
    gallery(args)


def gallery(args):
    rows=[]
    for view in VIEWS:
        cells=[];settings=[]
        for variant in ("original","candidate"):
            image=args.output_root/"Previews"/(args.item+"-"+view+"-"+variant+".png")
            meta=image.with_suffix(".json")
            if not image.exists() or not meta.exists():
                cells.append("<td>Not rendered</td>");continue
            record=json.loads(meta.read_text(encoding="utf-8"))
            if core.digest(image)!=record["sha256"]:raise RuntimeError("Gallery preview hash mismatch")
            settings.append({k:v for k,v in record.items() if k not in ("variant","sha256","elapsed_seconds")})
            relative="Previews/"+image.name
            cells.append('<td><a href="'+relative+'"><img alt="'+view+' '+variant+'" src="'+relative+'"></a></td>')
        if all("Not rendered" in cell for cell in cells):continue
        if len(settings)==2 and settings[0]!=settings[1]:raise RuntimeError("Gallery pair settings differ: "+view)
        rows.append('<tr><th colspan="2">'+view+'</th></tr><tr>'+''.join(cells)+'</tr>')
    page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Original-based construction pilot</title><style>body{font:16px system-ui;background:#eeece5;color:#26352d;max-width:1250px;margin:auto;padding:24px}table{width:100%}td{width:50%;padding:8px}th{text-align:left;padding-top:20px}img{width:100%}a{color:#26634e}</style>'''
    page+='<h1>Original Wreckabulary · '+args.item+' pilot</h1><p>Visual construction candidate only. Owner appearance acceptance and Unity checks remain pending. Held pose is a static source grip reference.</p>'
    page+='<p><a href="'+args.item+'-original-based-pilot.blend">Editable Blender source</a> · <a href="build-receipt.json">Provenance</a> · <a href="Source/'+args.item+'.original.glb">Unmodified original</a></p>'
    page+='<table><tr><th>Original game model</th><th>Original-based candidate</th></tr>'+''.join(rows)+'</table></html>\n'
    (args.output_root/"PILOT-REVIEW.html").write_text(page,encoding="utf-8")


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--item",choices=tuple(SOURCE),required=True)
    parser.add_argument("--source-root",type=Path,required=True);parser.add_argument("--output-root",type=Path,required=True)
    parser.add_argument("--stage",choices=("build","render"),required=True)
    parser.add_argument("--views",default="three-quarter");parser.add_argument("--size",type=int,default=960)
    parser.add_argument("--samples",type=int,default=64);parser.add_argument("--threads",type=int,default=6)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    args.source_root,args.output_root=args.source_root.resolve(),args.output_root.resolve()
    allowed=(args.source_root/"ArtSource/Collections/WordboundPvP").resolve()
    if args.output_root.parent!=allowed or not args.output_root.name.startswith("OriginalBasedPilot"+args.item):
        raise ValueError("Choose a new OriginalBasedPilot"+args.item+" revision beneath ArtSource/Collections/WordboundPvP")
    if not 1<=args.threads<=6 or not 16<=args.samples<=256 or not 320<=args.size<=1600:raise ValueError("Unbounded preview settings")
    (build if args.stage=="build" else render)(args)


if __name__=="__main__":main()
