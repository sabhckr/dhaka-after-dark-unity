#!/usr/bin/env python3
# The owner's rigged people and clips, for Unity to import (2026-11-06). Copies each rigged FBX into Assets/_Project/Resources/People/, writes each of its materials' pictures
# beside it (no bigger than SIZE, named by material, as Mixamo's files do not always carry them), and copies the clips into People/Clips/. Unity's importer does the rest: see
# Assets/_Project/Editor/PeopleImport.cs. Vehicles' seated figures do not come through here: bake.py poses those. Run with the CLT python:
#   python3 -I Tools/models/people.py [who...]
import os, re, shutil, subprocess, sys, tempfile, zipfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake, fbxread

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'Assets/_Project/Resources/People')
CLIPS = os.path.join(bake.SRC, 'mixamo/out')
# Pictures go in at the size the owner's files have them: nothing is shrunk here (owner, 2026-11-09).
SEE_THROUGH = {'shalwar': {'Std_Eyelash': 'textures/Std_Eyelash_Opacity.jpeg'}}  # drawn through this picture (NpcCrowd): the model's own is a flat colour

def safe(name): return re.sub(r'[^A-Za-z0-9_-]', '_', name)

def person(who):
    recipe = bake.PEOPLE[who]
    data = open(os.path.join(bake.SRC, recipe['rigged']), 'rb').read() if 'rigged' in recipe else bake.archive(recipe['zip'], recipe['file'], recipe.get('inner'))[0]
    home = os.path.join(OUT, who)
    os.makedirs(home, exist_ok=True)
    with open(os.path.join(OUT, who + '.fbx'), 'wb') as f: f.write(data)
    parts, _ = bake.parts_of(recipe)
    seen = {}
    for part in parts:
        for m in part.mats:
            if m.picture: seen[safe(m.name)] = m.picture
    for name, file in SEE_THROUGH.get(who, {}).items(): seen[name] = bake.archive(recipe['zip'], file, None)[0]
    with tempfile.TemporaryDirectory() as t:
        for name, picture in seen.items():
            raw = os.path.join(t, 'in')
            with open(raw, 'wb') as f: f.write(picture)
            done = os.path.join(home, name + '.png')
            subprocess.run(['/usr/bin/sips', '-s', 'format', 'png', raw, '--out', done], check=True, capture_output=True)
    print('%-10s %6.1f MB fbx, %d pictures %s' % (who, len(data) / 1e6, len(seen), sorted(seen)[:6]))

AS_MADE = {'leather': 'Female_Leather_Suit.Fbx', 'contractor': 'privage+military+contractor.fbx', 'survival': 'Survival_Character.fbx', 'michael': 'Michael.fbx'}  # the owner's own models: the file goes in byte for byte, and every map stays the very file it was made as

def triple(stem, base, normal, rough): return {'': stem + base, '_normal': stem + normal, '_roughness': stem + rough}
def orm(folder, stem, ext='.jpg'): return {'': '%s/%s_BaseColor%s' % (folder, stem, ext), '_normal': '%s/%s_Normal%s' % (folder, stem, ext), '_orm': '%s/%s_OcclusionRoughnessMetallic%s' % (folder, stem, ext)}
def tile(n): return {'': 'Body/Body_BaseColor.%d.png' % n, '_normal': 'Body/Body_Normal.%d.png' % n, '_orm': 'Body/Body_OcclusionRoughnessMetallic.%d.png' % n}
_MC, _GL = 'privage_military_contractor', 'private_contractor1_gloves'
_RN = ('_BaseColor.jpg', '_Normal.jpg', '_Roughness.jpg')
# Models whose maps are loose files in an archive: (archive, folder in it, material -> {ending: file}). The owner's files name them by material slot; each is copied as it is.
LOOSE = {
    'contractor': ('private-military-contractor/textures.zip', 'textures/', {
        'mag pouch': triple(_MC + '_mag_pouch_', 'Base.jpg', 'Norm.jpg', 'Roug.jpg'),
        'vest': triple(_MC + '2_vest_', 'BaseColo.jpg', 'Normal.jpg', 'Roughnes.jpg'),
        'facemask': triple('scp_operator_mask', *_RN),
        'googles': dict(triple('googles_googles', '_BaseColor.png', '_Normal.jpg', '_Roughness.jpg'), _metallic='googles_googles_Metallic.jpg'),
        'l glove': triple(_GL, *_RN),
        'r glove': triple(_GL, *_RN),
        'boots': {'': 'boot_color.jpg', '_normal': 'army_boot_boot_lp_Normal.jpg', '_roughness': 'army_boot_boot_lp_Roughness.jpg'},
        'kneepad': triple('kneepad_kneepad', *_RN),
        'helmet': triple('scp_operator_helmet', *_RN),
        'face': {'': 'swat_face_BaseColor.jpg', '_normal': 'swat_face_Normal.jpg'},
        'scar': {'': 'DefaultMaterial_Base_Color.jpeg', '_normaldx': 'DefaultMaterial_Normal_DirectX.jpeg', '_roughness': 'DefaultMaterial_Roughness.jpeg', '_metallic': 'DefaultMaterial_Metallic.jpeg'},
        'body': triple(_MC + '_body', *_RN),
    }),
    'michael': ('makehuman-system-assets/makehuman_system_assets_cc0.zip', '', {  # its own file names MakeHuman's stock pictures; the skin is one the file left blank
        'Michael_model.body': {'': ('makehuman-skins-vol1/SkinsVol1.zip', 'SkinsVol1/MiddleAge/PNG/caucasian_male_middleage_skinTest_A_dave_middleage_BaseColor_Utility - sRGB - Texture.png'), '_normal': ('makehuman-skins-vol1/SkinsVol1.zip', 'SkinsVol1/MiddleAge/PNG/caucasian_male_middleage_skinTest_A_dave_middleage_Normal_Utility - Raw.png'), '_orm': ('makehuman-skins-vol1/SkinsVol1.zip', 'SkinsVol1/MiddleAge/PNG/caucasian_male_middleage_skinTest_A_dave_middleage_OcclusionRoughnessMetallic_Utility - Raw.png')},  # 4K; the file leaves its skin blank, so this is Skins Vol.1 (SMOKEWORKS ENT, CC BY 4.0)
        'Michael_model.short02': {'': 'hair/short02/short02_diffuse.png', '_cuta': 'hair/short02/short02_diffuse.png'},
        'Michael_model.eyebrow008': {'': 'eyebrows/eyebrow008/eyebrow008.png', '_cuta': 'eyebrows/eyebrow008/eyebrow008.png'},
        'Michael_model.HighPolyEyes': {'': 'eyes/materials/deepblue_eye.png', '_cuta': 'eyes/materials/deepblue_eye.png'},  # its clear front layer is the picture's transparent patch
        'Michael_model.Teeth_Base': {'': 'teeth/teeth_base/teeth.png'},
        'Michael_model.tongue01': {'': 'tongue/tongue01/tongue01_diffuse.png'},
        'Michael_model.shoes04': {'': 'clothes/shoes04/shoes04_diffuse.png', '_normal': 'clothes/shoes04/shoes04_normal.png'},
        'Michael_model.shoes01': {'': 'clothes/shoes01/shoes01_diffuse.png', '_normal': 'clothes/shoes01/shoes01_normal.png'},
        'Michael_model.male_casualsuit03': {'': 'clothes/male_casualsuit03/male_casualsuit03_diffuse.png', '_normal': 'clothes/male_casualsuit03/male_casualsuit03_normal.png'},
    }),
    'survival': ('survival-character/Textures.zip', 'Textures/Textures/', {
        'Jacket1': orm('Jacket', 'Jacket'), 'Backpack2': orm('Backpack', 'Backpack'), 'Gloves1': orm('Gloves', 'Gloves'), 'Jeans1': orm('Jeans', 'Jeans'), 'Shoes1': orm('Shoes', 'Shoes'),
        'Head': tile(1001), 'Body2': tile(1002), 'Arms': tile(1003),  # the file's one body mesh lays each material on its own UDIM tile; wrapping reads the tile as 0 to 1
        'Mouth': {'': 'Mouth/TeethBaseColor.png', '_normal': 'Mouth/Teet_Normal.png', '_orm': 'Mouth/Teeth_ORM.png'},
        'Brows_Leashes': {'': 'Eyebrow_EyeLeashes/Eyebrows_BaseColor.jpg', '_normal': 'Eyebrow_EyeLeashes/Eyebrows_Normal.jpg', '_cut': 'Eyebrow_EyeLeashes/Eyebrows_Opacity.jpg'},
        'Hair3': {'': 'HairStyle/HairBrown_Diffuse_2.jpg', '_normal': 'HairStyle/HairBrown_Normal.jpg', '_cut': 'HairStyle/HairBrown_Alpha.jpg'},
        'Body_Arkit:Eye': {'': 'Eye/Eye_BaseColor.png', '_normal': 'Eye/Eye_Normal.jpg'},
    }),
}
ROLES = {'DiffuseColor': '', 'NormalMap': '_normal', 'TransparentColor': '_opacity'}

def as_made(who):
    data = open(os.path.join(bake.SRC, AS_MADE[who]), 'rb').read()
    home = os.path.join(OUT, who)
    os.makedirs(home, exist_ok=True)
    with open(os.path.join(OUT, who + '.fbx'), 'wb') as f: f.write(data)
    if who in LOOSE:
        archive, folder, table = LOOSE[who]
        count = 0
        with zipfile.ZipFile(os.path.join(bake.SRC, archive)) as z:
            for material, maps in table.items():
                for ending, file in maps.items():
                    other = isinstance(file, tuple)  # a map from another archive
                    name = file[1] if other else file
                    with open(os.path.join(home, safe(material) + ending + os.path.splitext(name)[1].lower()), 'wb') as f:
                        f.write(zipfile.ZipFile(os.path.join(bake.SRC, file[0])).read(name) if other else z.read(folder + name))
                    count += 1
        print('%-10s %6.1f MB fbx, %d maps, as made' % (who, len(data) / 1e6, count))
        return
    root = fbxread.parse(data)
    objects = {o.props[0]: o for o in root.get('Objects').kids if o.props}
    label = lambda o: o.props[1].split('\x00')[0].split('::')[-1]
    leaf = lambda v: v.get('RelativeFilename').props[0].replace('\\', '/').rsplit('/', 1)[-1]
    packed = {}  # a picture two materials share is packed once, under one name
    for o in objects.values():
        if o.name == 'Video' and o.get('Content') is not None and o.get('Content').props and o.get('Content').props[0]: packed[leaf(o)] = o.get('Content').props[0]
    maps, video, meshes = {}, {}, {}
    for c in root.get('Connections').all('C'):
        child, parent = objects.get(c.props[1]), objects.get(c.props[2])
        if child is None or parent is None: continue
        if child.name == 'Texture' and parent.name == 'Material': maps.setdefault(c.props[2], []).append((c.props[3], c.props[1]))
        elif child.name == 'Video' and parent.name == 'Texture': video[c.props[2]] = child
        elif child.name == 'Material' and parent.name == 'Model': meshes.setdefault(c.props[1], []).append(label(parent))
    names = [label(objects[m]) for m in maps]
    count = 0
    for m, slots in maps.items():
        name = safe(label(objects[m]))
        if names.count(label(objects[m])) > 1: name = safe(meshes[m][0]) + '_' + name  # several materials of one name: the part of the body tells them apart
        for role, texture in slots:
            if role not in ROLES or leaf(video[texture]) not in packed: continue
            with open(os.path.join(home, name + ROLES[role] + os.path.splitext(leaf(video[texture]))[1].lower()), 'wb') as f: f.write(packed[leaf(video[texture])])
            count += 1
    print('%-10s %6.1f MB fbx, %d maps, as made' % (who, len(data) / 1e6, count))

# Microsoft Rocketbox characters (MIT licence), each with its FBX and its maps in ~/Downloads/3D assets/rocketbox/<Name>/. They already name their maps by material, _color, _normal and _specular: all go in byte for byte.
ROCKETBOX = os.path.join(bake.SRC, 'rocketbox')
def rocketbox_names(): return {d.lower(): d for d in os.listdir(ROCKETBOX) if os.path.isdir(os.path.join(ROCKETBOX, d))} if os.path.isdir(ROCKETBOX) else {}

def rocketbox(who):
    folder = rocketbox_names()[who]
    home = os.path.join(OUT, who)
    os.makedirs(home, exist_ok=True)
    shutil.copyfile(os.path.join(ROCKETBOX, folder, folder + '.fbx'), os.path.join(OUT, who + '.fbx'))
    maps = [f for f in os.listdir(os.path.join(ROCKETBOX, folder)) if re.fullmatch(r'[a-z]\d+_[a-z_]+_(color|normal|specular)\.tga', f)]
    for f in maps: shutil.copyfile(os.path.join(ROCKETBOX, folder, f), os.path.join(home, f))
    print('%-22s %d maps, as made' % (who, len(maps)))

# Character Creator people from Sketchfab (2026-11-10, free, CC BY by Jungle Jim): full CC rig (five toes, three bones a finger, jaw, tongue, eyes) and the face's blend shapes.
# Each folder holds the FBX as made and its maps named <material>_<Role>; they go in byte for byte, renamed to the endings Person.Map reads.
CC4 = {
    'bouncer': ('npc-aaa-the-bouncer-animated/unpacked', 'source/inner/The Bouncer.Fbx'),
    'balthazar': ('npc-aaa-balthazar-rigged-animated/unpacked', 'source/inner/Balthazar walking.Fbx'),
    'walker': ('npc-aaa-realistic-woman-walking-animated/unpacked', 'source/Realistic woman walking.fbx'),
    'alina': ('npc-aaa-alina/unpacked', 'source/Alina Ip.fbx'),
}
CC4_ROLES = {'Diffuse': '', 'Normal': '_normal', 'Opacity': '_opacity', 'Specular': '_specular', 'Metallic': '_metallic', 'Roughness': '_roughness'}

def cc4(who):
    folder, fbx = CC4[who]
    src = os.path.join(bake.SRC, folder)
    home = os.path.join(OUT, who)
    os.makedirs(home, exist_ok=True)
    shutil.copyfile(os.path.join(src, fbx), os.path.join(OUT, who + '.fbx'))
    count = 0
    for f in sorted(os.listdir(os.path.join(src, 'textures'))):
        m = re.fullmatch(r'(.+)_([A-Za-z]+)(\.[A-Za-z]+)', f)
        if not m or m.group(2) not in CC4_ROLES: continue
        shutil.copyfile(os.path.join(src, 'textures', f), os.path.join(home, safe(m.group(1)) + CC4_ROLES[m.group(2)] + m.group(3).lower().replace('.jpeg', '.jpg')))
        count += 1
    print('%-10s %d maps, as made' % (who, count))

def clips():
    home = os.path.join(OUT, 'Clips')
    os.makedirs(home, exist_ok=True)
    names = [f for f in sorted(os.listdir(CLIPS)) if f.endswith('.fbx') and '-rigged' not in f]
    for f in names: shutil.copyfile(os.path.join(CLIPS, f), os.path.join(home, f))
    print('clips', names)
    extra = os.path.join(bake.SRC, 'mixamo/new-clips')  # motion from Mixamo, each as it downloaded; the figure in the file goes unused
    for f in sorted(os.listdir(extra)):
        if f.endswith('.fbx'): shutil.copyfile(os.path.join(extra, f), os.path.join(home, f))
    print('new clips', len(os.listdir(extra)))
    behaviour = os.path.join(bake.SRC, 'mixamo/behaviour-clips')  # 2026-11-10: reactions to danger and street life, from Mixamo, Without Skin, 30 fps, no reduction
    armed = os.path.join(bake.SRC, 'mixamo/trailer-clips')  # the trailer's gun clips, for armed people on the street
    for folder, keep in ((behaviour, lambda f: True), (armed, lambda f: f.startswith(('pistol-', 'rifle-', 'crouch-', 'reload')))):
        got = [f for f in sorted(os.listdir(folder)) if f.endswith('.fbx') and keep(f)]
        for f in got: shutil.copyfile(os.path.join(folder, f), os.path.join(home, f))
        print(os.path.basename(folder), got)

if __name__ == '__main__':
    box = rocketbox_names()
    for who in sys.argv[1:] or bake.PEOPLE: (cc4 if who in CC4 else rocketbox if who in box else as_made if who in AS_MADE else person)(who)
    if not all(who in AS_MADE or who in box or who in CC4 for who in sys.argv[1:] or [None]): clips()
