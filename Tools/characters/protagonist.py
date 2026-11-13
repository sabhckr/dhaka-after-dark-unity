# The two player protagonists, built from free CC0 parts (2026-11-10). Runs inside Blender 4.5 with the MPFB 2.0.18 extension and these CC0 asset packs installed:
# makehuman_system_assets, skins01, skins02, shirts01, pants01, shoes01, hair01, eyebrows01, eyelashes01, visemes01, visemes02, faceunits01
# (files.makehumancommunity.org; every asset's own licence field was read: all CC0, bar one CC-BY boot that is not used here). Docs/Characters/LICENSES.md lists each part used.
#
# Each person is made the same way: MakeHuman's body from the phenotype below, the "default" skeleton (163 bones: every finger joint, every toe, jaw, eyes, tongue and lips), their
# skin, eyes, brows, lashes, teeth, tongue, hair and clothes, then the face's shapes: Microsoft's 22 visemes, Meta's 15 and Apple's 52 face units, carried on to the brows, lashes,
# teeth and tongue; and the ten mouth shapes speech is cut to here (AA EE IH OH OU FV MBP L WQ TH), each made from one of those. The body's shape is baked in, the parts the clothes
# cover are cut away, the body is smoothed once (subdivision, shapes kept), and the whole is written as one FBX, Characters/<Male|Female>/<Name>.fbx, its maps copied as they are
# into Textures/ beside it. Unity makes the materials and the prefab from these (Assets/_Project/Editor/Protagonists.cs). Docs/Characters/<Name>-build.json records what was made.
#   /Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/characters/protagonist.py -- male|female [--probe] [--height h] [--render picture-stem]
import bpy, importlib, json, os, re, shutil, sys

HOME = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(HOME, 'Assets/_Project/Characters')


def rel(path):
    # where a map came from, from MPFB's data folder on (the reports name no one's home folder)
    i = path.replace('\\', '/').find('mpfb/data/')
    return path[i:] if i >= 0 else os.path.basename(path)
SVC = 'bl_ext.user_default.mpfb.services'
def service(name): return getattr(importlib.import_module(SVC), name)
HumanService, TargetService, FaceService, ExportService = (service(n) for n in ('HumanService', 'TargetService', 'FaceService', 'ExportService'))

EYES_UUID = '2c12f43b-1303-432c-b7ce-d78346baf2e6'  # high-poly eyes
PEOPLE = {
    'male': dict(name='MalePlayer', folder='Male',
                 phenotype=dict(gender=1.0, age=0.5, muscle=0.62, weight=0.52, proportions=0.62, height=0.5, cupsize=0.5, firmness=0.5,
                                race=dict(asian=0.35, caucasian=0.45, african=0.2)),
                 skin='toigo_light_skin_male_bronze', eyes='brown', eyebrows='eyebrow001', eyelashes='eyelashes01', hair='short04',
                 clothes=['male_casualsuit01', 'shoes04']),
    'female': dict(name='FemalePlayer', folder='Female',
                   phenotype=dict(gender=0.0, age=0.45, muscle=0.55, weight=0.45, proportions=0.62, height=0.57, cupsize=0.5, firmness=0.6,
                                  race=dict(asian=0.35, caucasian=0.45, african=0.2)),
                   skin='cutoff3d_indian_female_enhanced', eyes='brown', eyebrows='eyebrow010', eyelashes='eyelashes01', hair='braid01',
                   clothes=['toigo_basic_tucked_t-shirt', 'toigo_wool_pants', 'shoes06']),
}
# The ten mouth shapes: (name, source shape). Meta's set where it has the sound, Microsoft's where only it does.
MOUTH = [('AA', 'viseme_aa'), ('EE', 'y_iy_ih_ix_06'), ('IH', 'viseme_I'), ('OH', 'viseme_O'), ('OU', 'viseme_U'), ('FV', 'viseme_FF'),
         ('MBP', 'viseme_PP'), ('L', 'l_14'), ('WQ', 'w_uw_07'), ('TH', 'viseme_TH')]


def build(recipe):
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    info = HumanService._create_default_human_info_dict()
    info.update(name=recipe['name'], phenotype=recipe['phenotype'], rig='default',
                eyes='high-poly.mhclo', eyebrows=recipe['eyebrows'] + '.mhclo', eyelashes=recipe['eyelashes'] + '.mhclo',
                teeth='teeth_base.mhclo', tongue='tongue01.mhclo', hair=recipe['hair'] + '.mhclo',
                clothes=[c + '.mhclo' for c in recipe['clothes']],
                skin_mhmat=recipe['skin'] + '.mhmat', skin_material_type='MAKESKIN', eyes_material_type='MAKESKIN',
                alternative_materials={EYES_UUID: recipe['eyes'] + '.mhmat'})
    settings = HumanService.get_default_deserialization_settings()
    settings['subdiv_levels'] = 0  # the body alone is smoothed, below; the clothes come as their makers drew them
    body = HumanService.deserialize_from_dict(info, settings)
    TargetService.bake_targets(body)  # the phenotype becomes the body's own shape
    FaceService.load_targets(body, load_microsoft_visemes=True, load_meta_visemes=True, load_arkit_faceunits=True)
    FaceService.interpolate_targets(body)
    smooth = body.modifiers.new('Subdivision', 'SUBSURF')
    smooth.levels, smooth.render_levels = 0, 1
    return body


def report(rig):
    meshes = [o for o in rig.children if o.type == 'MESH']
    out = dict(bones=len(rig.data.bones), height=None, meshes={})
    lo, hi = 1e9, -1e9
    for m in meshes:
        dg = m.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
        tris = sum(len(p.vertices) - 2 for p in dg.polygons)
        zs = [(m.matrix_world @ v.co).z for v in dg.vertices]
        lo, hi = min(lo, min(zs)), max(hi, max(zs))
        if m.name.endswith('body') or m.name == 'Body': out['body_height'] = round(max(zs), 4)  # the crown above the ground the bare feet stand on (MPFB puts them at 0)
        keys = [k.name for k in m.data.shape_keys.key_blocks] if m.data.shape_keys else []
        mats = {}
        for s in m.material_slots:
            if not s.material or not s.material.node_tree: continue
            mats[s.material.name] = sorted({(n.image.name, n.image.size[0], n.image.size[1], rel(n.image.filepath)) for n in s.material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image})
        out['meshes'][m.name] = dict(verts=len(dg.vertices), tris=tris, keys=len(keys), first_keys=keys[:4], materials=mats)
        m.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    out['height'] = round(hi - lo, 4)
    return out


def mouth(meshes):
    """The ten mouth shapes, each the whole of one source shape, on every mesh that has that source."""
    made = {}
    for m in meshes:
        keys = m.data.shape_keys.key_blocks if m.data.shape_keys else None
        if keys is None: continue
        for k in keys: k.value = 0
        for name, source in MOUTH:
            if source not in keys: continue
            keys[source].value = 1
            m.shape_key_add(name=name, from_mix=True)
            keys[source].value = 0
            made.setdefault(m.name, []).append(name)
    return made


def part(obj, recipe):
    """Body, Eyes, Eyebrows, Eyelashes, Hair, Teeth, Tongue; clothes keep their asset's name."""
    stem = obj.name.replace('_export_copy', '').split('.', 1)[-1]
    named = {'body': 'Body', 'high-poly': 'Eyes', 'teeth_base': 'Teeth', 'tongue01': 'Tongue',
             recipe['eyebrows']: 'Eyebrows', recipe['eyelashes']: 'Eyelashes', recipe['hair']: 'Hair'}
    return named.get(stem, stem)


def role(path):
    stem = os.path.splitext(os.path.basename(path))[0].lower()
    for kind in ('_normal', '_ao', '_bump', '_specular', '_roughness', '_opacity'):
        if stem.endswith(kind) or stem.endswith(kind.replace('_', '')): return kind
    return ''


def export(body, recipe):
    """An export copy, baked, renamed and written with its maps. The built person is left as it was."""
    who = recipe['name']
    rig = ExportService.create_character_copy(body)
    copy = next(o for o in rig.children if o.type == 'MESH' and o.name.split('.', 1)[-1].startswith('body'))
    ExportService.bake_modifiers_remove_helpers(copy, bake_masks=True, bake_subdiv=True, remove_helpers=True)
    for o in list(bpy.data.objects):
        if o != rig and o.parent != rig: bpy.data.objects.remove(o, do_unlink=True)
    rig.name = rig.data.name = who
    meshes = [o for o in rig.children if o.type == 'MESH']
    made = mouth(meshes)
    home = os.path.join(OUT, recipe['folder'], 'Textures')
    os.makedirs(home, exist_ok=True)
    maps = {}
    for m in meshes:
        name = part(m, recipe)
        m.name = m.data.name = name
        for slot in m.material_slots:
            mat = slot.material
            if mat is None: continue
            mat.name = name
            for n in mat.node_tree.nodes:
                if n.type != 'TEX_IMAGE' or not n.image: continue
                src = bpy.path.abspath(n.image.filepath)
                kind = role(src)
                dest = os.path.join(home, name + kind + os.path.splitext(src)[1].lower())
                shutil.copyfile(src, dest)  # byte for byte: no picture is resized or re-encoded
                maps[os.path.basename(dest)] = dict(source=rel(src), size=list(n.image.size))
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx = os.path.join(OUT, recipe['folder'], who + '.fbx')
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', armature_nodetype='NULL', use_armature_deform_only=False, path_mode='STRIP', embed_textures=False)
    return rig, made, maps, fbx


def render(rig, path):
    """Front and three-quarter pictures of the export, lit plainly, to look at."""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1600
    world = bpy.data.worlds.new('w'); world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.37, 1); world.node_tree.nodes['Background'].inputs[1].default_value = 0.8
    scene.world = world
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 3.5
    sun.rotation_euler = (0.9, 0.2, 0.6); scene.collection.objects.link(sun)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 85
    import mathutils
    shots = []
    for label, angle, height, dist, aim in (('full', 0, 1.0, 6.2, 0.95), ('three-quarter', 35, 1.0, 6.2, 0.95), ('face', 0, 1.62, 1.05, 1.62), ('side', 90, 1.0, 6.2, 0.95)):
        a = mathutils.Matrix.Rotation(__import__('math').radians(angle), 4, 'Z')
        cam.location = a @ mathutils.Vector((0, -dist, height))
        cam.rotation_euler = (mathutils.Vector((0, 0, aim)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = path + '_' + label + '.png'
        bpy.ops.render.render(write_still=True)
        shots.append(scene.render.filepath)
    return shots


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    recipe = dict(PEOPLE[args[0]])
    if '--height' in args: recipe['phenotype'] = dict(recipe['phenotype'], height=float(args[args.index('--height') + 1]))
    body = build(recipe)
    if '--probe' in args:
        print('BUILT', json.dumps(report(body.parent), default=str))
        sys.exit(0)
    rig, made, maps, fbx = export(body, recipe)
    result = report(rig)
    result.update(mouth=made, maps=maps, fbx=os.path.relpath(fbx, HOME), fbx_mb=round(os.path.getsize(fbx) / 1e6, 1), recipe=recipe)
    os.makedirs(os.path.join(HOME, 'Docs/Characters'), exist_ok=True)
    with open(os.path.join(HOME, 'Docs/Characters', recipe['name'] + '-build.json'), 'w') as f: json.dump(result, f, indent=1, default=str)
    print('BUILT', json.dumps(result, indent=1, default=str))
    if '--render' in args: print('RENDERED', render(rig, args[args.index('--render') + 1]))
