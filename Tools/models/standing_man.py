#!/usr/bin/env python3
# The owner's standing_man, for Unity to import as the player (2026-11-06). Unity has no glTF importer and none may be added, so this reads scene.gltf straight out of the archive, which stays as
# it is, and writes the same mesh, skin and skeleton as an ASCII FBX with its pictures beside it. Nothing about the figure is changed: every triangle, weight and picture is the file's own, and the
# rest pose is the one the skin was bound in. Bones lose Sketchfab's number suffix (mixamorig:Hips_01 to mixamorig:Hips). Assets/_Project/Editor/PeopleImport.cs makes it Humanoid as it imports.
#   python3 -I Tools/models/standing_man.py [out dir]
import json, math, os, re, shutil, struct, subprocess, sys, tempfile, zipfile

HOME = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = os.path.expanduser('~/Downloads/3D assets/standing_man.zip')
WHO = 'standing_man'
PICTURE = {'Body_map': 1024, 'outfit_map': 1024, 'Head_map': 1024}  # the rest are small parts of the face
SMALL = 256
SIZES = {5120: 'f', 5121: 'B', 5122: 'h', 5123: 'H', 5125: 'I', 5126: 'f'}
WIDTH = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
FORMAT = {5120: 'b', 5121: 'B', 5122: 'h', 5123: 'H', 5125: 'I', 5126: 'f'}

class Gltf:
    def __init__(s, zip_path):
        s.zip = zipfile.ZipFile(zip_path)
        name = next(n for n in s.zip.namelist() if n.endswith('.gltf'))
        s.root = os.path.dirname(name)
        s.g = json.loads(s.zip.read(name))
        s.bin = s.zip.read(os.path.join(s.root, s.g['buffers'][0]['uri']))
    def read(s, i):
        a = s.g['accessors'][i]
        v = s.g['bufferViews'][a['bufferView']]
        n, w = a['count'], WIDTH[a['type']]
        f = FORMAT[a['componentType']]
        size = struct.calcsize('<' + f)
        stride = v.get('byteStride', size * w)
        o = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from('<%d%s' % (w, f), s.bin, o + k * stride) for k in range(n)]
    def picture(s, texture):
        return os.path.join(s.root, s.g['images'][s.g['textures'][texture]['source']]['uri'])

def inverse(m):
    """Of a rigid 4x4 in glTF's order: the rotation is transposed, the translation turned back. Checked against the real thing in main."""
    r = [[m[0], m[1], m[2]], [m[4], m[5], m[6]], [m[8], m[9], m[10]]]
    t = m[12:15]
    sq = [sum(c * c for c in row) for row in r]
    out = [0.0] * 16
    for i in range(3):
        for j in range(3): out[i * 4 + j] = r[j][i] / sq[i]
    for j in range(3): out[12 + j] = -sum(t[i] * out[i * 4 + j] for i in range(3))
    out[15] = 1.0
    return out

def multiply(a, b):
    """The matrix that does a, then b, both as row vectors, which is glTF's column order read flat."""
    return [sum(a[i * 4 + k] * b[k * 4 + j] for k in range(4)) for i in range(4) for j in range(4)]

def euler(m):
    """FBX's XYZ order in degrees, from a row-vector matrix."""
    sy = -m[2]
    y = math.asin(max(-1, min(1, sy)))
    if abs(sy) < 0.99999:
        x = math.atan2(m[6], m[10])
        z = math.atan2(m[1], m[0])
    else:
        x = math.atan2(-m[9], m[5])
        z = 0.0
    return [math.degrees(c) for c in (x, y, z)]

def text(values, kind='%.7g'):
    return ','.join(kind % v for v in values)

def body(g):
    """One mesh of all ten, with a material for each triangle, and the joints that move each corner."""
    mats, faces = [], []
    pts, normals, uvs, joints, weights = [], [], [], [], []
    for mesh in g.g['meshes']:
        for p in mesh['primitives']:
            at = p['attributes']
            base = len(pts)
            pts += g.read(at['POSITION'])
            normals += g.read(at['NORMAL'])
            uvs += [(u, 1 - v) for u, v in g.read(at['TEXCOORD_0'])]
            joints += g.read(at['JOINTS_0'])
            weights += g.read(at['WEIGHTS_0'])
            name = g.g['materials'][p['material']]['name']
            if name not in mats: mats.append(name)
            k = mats.index(name)
            idx = [i[0] for i in g.read(p['indices'])]
            for i in range(0, len(idx), 3): faces.append((base + idx[i], base + idx[i + 1], base + idx[i + 2], k))
    return mats, faces, pts, normals, uvs, joints, weights

def skeleton(g):
    skin = g.g['skins'][0]
    nodes = g.g['nodes']
    order = skin['joints']
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []): parent[c] = i
    binds = [inverse(list(m)) for m in g.read(skin['inverseBindMatrices'])]
    bones = []
    for k, node in enumerate(order):
        name = re.sub(r'_\d+$', '', nodes[node]['name'])
        up = parent.get(node)
        p = order.index(up) if up in order else -1
        local = binds[k] if p < 0 else multiply(binds[k], inverse(binds[p]))
        bones.append({'name': name, 'parent': p, 'bind': binds[k], 'local': local})
    return bones

def fbx(path, mats, faces, pts, normals, uvs, joints, weights, bones, pictures):
    ids = iter(range(1000000, 9000000))
    w = open(path, 'w')
    p = lambda s='': w.write(s + '\n')
    mesh_model, geometry, skin_id, pose = next(ids), next(ids), next(ids), next(ids)
    for b in bones: b['id'], b['attr'], b['cluster'] = next(ids), next(ids), next(ids)
    mat_ids = [next(ids) for _ in mats]
    tex = [(next(ids), mats.index(m), slot, f) for m, slot, f in pictures]
    links = {}
    for i, (js, ws) in enumerate(zip(joints, weights)):
        total = sum(ws) or 1
        for j, x in zip(js, ws):
            if x > 0: links.setdefault(j, []).append((i, x / total))
    used = [b for k, b in enumerate(bones) if k in links]
    p('; FBX 7.4.0 project file')
    p('FBXHeaderExtension:  {\n\tFBXHeaderVersion: 1003\n\tFBXVersion: 7400\n\tCreator: "Tools/models/standing_man.py"\n}')
    p('GlobalSettings:  {\n\tVersion: 1000\n\tProperties70:  {')
    for k, v in (('UpAxis', 1), ('UpAxisSign', 1), ('FrontAxis', 2), ('FrontAxisSign', 1), ('CoordAxis', 0), ('CoordAxisSign', 1), ('OriginalUpAxis', 1), ('OriginalUpAxisSign', 1)):
        p('\t\tP: "%s", "int", "Integer", "",%d' % (k, v))
    p('\t\tP: "UnitScaleFactor", "double", "Number", "",1\n\t\tP: "OriginalUnitScaleFactor", "double", "Number", "",1\n\t}\n}')
    p('Definitions:  {\n\tVersion: 100\n\tCount: %d' % (1 + 2 + 2 * len(bones) + 1 + len(used) + 1 + len(mats) + len(tex)))
    for kind, n in (('GlobalSettings', 1), ('Geometry', 1), ('Model', 1 + len(bones)), ('NodeAttribute', len(bones)), ('Deformer', 1 + len(used)), ('Pose', 1), ('Material', len(mats)), ('Texture', len(tex))):
        p('\tObjectType: "%s" {\n\t\tCount: %d\n\t}' % (kind, n))
    p('}\nObjects:  {')
    p('\tGeometry: %d, "Geometry::%s", "Mesh" {' % (geometry, WHO))
    flat = [c for q in pts for c in q]
    p('\t\tVertices: *%d {\n\t\t\ta: %s\n\t\t}' % (len(flat), text(flat)))
    idx = []
    for a, b, c, _ in faces: idx += [a, b, -c - 1]
    p('\t\tPolygonVertexIndex: *%d {\n\t\t\ta: %s\n\t\t}' % (len(idx), text(idx, '%d')))
    p('\t\tGeometryVersion: 124')
    flat = [c for q in normals for c in q]
    p('\t\tLayerElementNormal: 0 {\n\t\t\tVersion: 102\n\t\t\tName: ""\n\t\t\tMappingInformationType: "ByVertice"\n\t\t\tReferenceInformationType: "Direct"\n\t\t\tNormals: *%d {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t}' % (len(flat), text(flat)))
    flat = [c for q in uvs for c in q]
    n = len(uvs)
    p('\t\tLayerElementUV: 0 {\n\t\t\tVersion: 101\n\t\t\tName: "UVMap"\n\t\t\tMappingInformationType: "ByVertice"\n\t\t\tReferenceInformationType: "IndexToDirect"\n\t\t\tUV: *%d {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t\tUVIndex: *%d {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t}' % (len(flat), text(flat), n, text(range(n), '%d')))
    m = [f[3] for f in faces]
    p('\t\tLayerElementMaterial: 0 {\n\t\t\tVersion: 101\n\t\t\tName: ""\n\t\t\tMappingInformationType: "ByPolygon"\n\t\t\tReferenceInformationType: "IndexToDirect"\n\t\t\tMaterials: *%d {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t}' % (len(m), text(m, '%d')))
    p('\t\tLayer: 0 {\n\t\t\tVersion: 100')
    for kind in ('Normal', 'UV', 'Material'):
        p('\t\t\tLayerElement:  {\n\t\t\t\tType: "LayerElement%s"\n\t\t\t\tTypedIndex: 0\n\t\t\t}' % kind)
    p('\t\t}\n\t}')
    p('\tModel: %d, "Model::%s", "Mesh" {\n\t\tVersion: 232\n\t\tShading: T\n\t\tCulling: "CullingOff"\n\t}' % (mesh_model, WHO))
    for b in bones:
        l = b['local']
        t, r = l[12:15], euler(l)
        p('\tNodeAttribute: %d, "NodeAttribute::%s", "LimbNode" {\n\t\tTypeFlags: "Skeleton"\n\t}' % (b['attr'], b['name']))
        p('\tModel: %d, "Model::%s", "LimbNode" {\n\t\tVersion: 232\n\t\tProperties70:  {' % (b['id'], b['name']))
        p('\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",%.7g,%.7g,%.7g' % tuple(t))
        p('\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",%.7g,%.7g,%.7g' % tuple(r))
        p('\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1\n\t\t}\n\t\tShading: T\n\t\tCulling: "CullingOff"\n\t}')
    p('\tDeformer: %d, "Deformer::Skin", "Skin" {\n\t\tVersion: 101\n\t\tLink_DeformAcuracy: 50\n\t}' % skin_id)
    eye = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    for k, b in enumerate(bones):
        if k not in links: continue
        vs = links[k]
        p('\tDeformer: %d, "SubDeformer::Cluster %s", "Cluster" {\n\t\tVersion: 100\n\t\tUserData: "", ""' % (b['cluster'], b['name']))
        p('\t\tIndexes: *%d {\n\t\t\ta: %s\n\t\t}' % (len(vs), text([v[0] for v in vs], '%d')))
        p('\t\tWeights: *%d {\n\t\t\ta: %s\n\t\t}' % (len(vs), text([v[1] for v in vs])))
        p('\t\tTransform: *16 {\n\t\t\ta: %s\n\t\t}' % text(inverse(b['bind']), '%.9g'))
        p('\t\tTransformLink: *16 {\n\t\t\ta: %s\n\t\t}\n\t}' % text(b['bind'], '%.9g'))
    p('\tPose: %d, "Pose::BIND_POSES", "BindPose" {\n\t\tType: "BindPose"\n\t\tVersion: 100\n\t\tNbPoseNodes: %d' % (pose, len(bones) + 1))
    p('\t\tPoseNode:  {\n\t\t\tNode: %d\n\t\t\tMatrix: *16 {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t}' % (mesh_model, text(eye, '%d')))
    for b in bones:
        p('\t\tPoseNode:  {\n\t\t\tNode: %d\n\t\t\tMatrix: *16 {\n\t\t\t\ta: %s\n\t\t\t}\n\t\t}' % (b['id'], text(b['bind'], '%.9g')))
    p('\t}')
    for i, name in zip(mat_ids, mats):
        p('\tMaterial: %d, "Material::%s", "" {\n\t\tVersion: 102\n\t\tShadingModel: "phong"\n\t\tMultiLayer: 0\n\t\tProperties70:  {' % (i, name))
        p('\t\t\tP: "DiffuseColor", "Color", "", "A",1,1,1\n\t\t\tP: "SpecularFactor", "Number", "", "A",0.2\n\t\t}\n\t}')
    for i, _, _, f in tex:
        rel = WHO + '/' + f
        p('\tTexture: %d, "Texture::%s", "" {\n\t\tType: "TextureVideoClip"\n\t\tVersion: 202\n\t\tTextureName: "Texture::%s"\n\t\tMedia: "Video::%s"\n\t\tFileName: "%s"\n\t\tRelativeFilename: "%s"\n\t\tModelUVTranslation: 0,0\n\t\tModelUVScaling: 1,1\n\t\tTexture_Alpha_Source: "None"\n\t\tCropping: 0,0,0,0\n\t}' % (i, f, f, f, rel, rel))
    p('}\nConnections:  {')
    c = lambda a, b: p('\tC: "OO",%d,%d' % (a, b))
    c(mesh_model, 0)
    c(geometry, mesh_model)
    c(skin_id, geometry)
    for i in mat_ids: c(i, mesh_model)
    for t, k, slot, _ in tex: p('\tC: "OP",%d,%d, "%s"' % (t, mat_ids[k], slot))
    for k, b in enumerate(bones):
        c(b['id'], 0 if b['parent'] < 0 else bones[b['parent']]['id'])
        c(b['attr'], b['id'])
        if k in links:
            c(b['cluster'], skin_id)
            c(b['id'], b['cluster'])
    p('}')
    w.close()
    return len(used)

def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HOME, 'Assets/_Project/Resources/People')
    g = Gltf(SOURCE)
    mats, faces, pts, normals, uvs, joints, weights = body(g)
    bones = skeleton(g)
    # The head's normal picture names its second UV set, but the file's two sets are the same, so URP's Lit reading the first finds it.
    pictures = []
    home = os.path.join(out, WHO)
    os.makedirs(home, exist_ok=True)
    with tempfile.TemporaryDirectory() as t:
        for m in g.g['materials']:
            name = m['name']
            slots = [('DiffuseColor', m['pbrMetallicRoughness']['baseColorTexture']['index'], name + '.png')]
            n = m.get('normalTexture')
            if n: slots.append(('NormalMap', n['index'], name + '_normal.png'))
            for slot, texture, f in slots:
                raw = os.path.join(t, 'in' + os.path.splitext(g.picture(texture))[1])
                with open(raw, 'wb') as o: o.write(g.zip.read(g.picture(texture)))
                # sips gives a PNG with no records in it, no bigger than the size asked for
                subprocess.run(['/usr/bin/sips', '-s', 'format', 'png', '-Z', str(PICTURE.get(name, SMALL)), raw, '--out', os.path.join(home, f)], check=True, capture_output=True)
                pictures.append((name, slot, f))
    n = fbx(os.path.join(out, WHO + '.fbx'), mats, faces, pts, normals, uvs, joints, weights, bones, pictures)
    top = max(q[1] for q in pts)
    print('%s: %d corners, %d triangles, %d materials, %d bones (%d move the skin), %.1f cm tall, %d pictures' % (WHO, len(pts), len(faces), len(mats), len(bones), n, top, len(pictures)))
    # The bind pose must rebuild from the bones' local transforms, or the figure would come in bent.
    worst = 0.0
    for b in bones:
        m = b['local']
        q = b
        while q['parent'] >= 0:
            q = bones[q['parent']]
            m = multiply(m, q['local'])
        worst = max(worst, max(abs(x - y) for x, y in zip(m, b['bind'])))
    print('bind pose rebuilt to within %.5f' % worst)
    if worst > 1e-3: sys.exit(1)

if __name__ == '__main__':
    main()
