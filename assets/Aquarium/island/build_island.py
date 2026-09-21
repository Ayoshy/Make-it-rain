import bpy
import bmesh
import math
import os
import random
from mathutils import Matrix, Vector
from mathutils import noise as mnoise

SEED = 20260917
SAMPLES = int(os.environ.get('ISLAND_SAMPLES', '512'))
RES_W = int(os.environ.get('ISLAND_W', '1280'))
RES_H = int(os.environ.get('ISLAND_H', '720'))
ORTHO_WIDTH = 4.4
CAMERA_Z = 0.42

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RENDER_DIR = os.path.join(REPO, 'artifacts', 'validation', 'aquarium-island')
BLEND_PATH = os.path.join(HERE, 'island.blend')

WATERLINE = 0.0
WATER_BOTTOM = -0.44
COPPER_BOTTOM = -0.70
HALF = 1.995
GLASS_HALF = 2.055
GLASS_TOP = 0.06
HILL_C = Vector((-0.15, 0.30, 0.0))
HILL_R = 1.62
HILL_TOP = 0.48
MOUNT_C = Vector((-0.17, 0.33, 0.0))
MOUNT_FOOT = 1.05
MOUNT_HEIGHT = 1.42


def rnd01(index):
    return random.Random(SEED ^ (index * 2654435761)).random()


def hex_lin(value):
    value = value.lstrip('#')
    out = []
    for i in (0, 2, 4):
        c = int(value[i:i + 2], 16) / 255.0
        c = c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
        out.append(c)
    return (out[0], out[1], out[2], 1.0)


def new_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    return mat, bsdf, nt


def put(bsdf, names, value):
    for name in names:
        if name in bsdf.inputs:
            bsdf.inputs[name].default_value = value
            return True
    return False


def material(name, color, metallic=0.0, roughness=0.5, ior=1.45, alpha=1.0,
             transmission=0.0, specular=0.5, coat=0.0, coat_roughness=0.03):
    mat, bsdf, _ = new_material(name)
    put(bsdf, ['Base Color'], hex_lin(color))
    put(bsdf, ['Metallic'], metallic)
    put(bsdf, ['Roughness'], roughness)
    put(bsdf, ['IOR'], ior)
    put(bsdf, ['Alpha'], alpha)
    put(bsdf, ['Transmission Weight', 'Transmission'], transmission)
    put(bsdf, ['Specular IOR Level', 'Specular'], specular)
    put(bsdf, ['Coat Weight'], coat)
    put(bsdf, ['Coat Roughness'], coat_roughness)
    return mat


def water_material():
    mat, bsdf, nt = new_material('Water')
    put(bsdf, ['Base Color'], hex_lin('#C2ECEF'))
    put(bsdf, ['Metallic'], 0.2)
    put(bsdf, ['Roughness'], 0.0)
    put(bsdf, ['IOR'], 1.33)
    put(bsdf, ['Alpha'], 1.0)
    put(bsdf, ['Transmission Weight', 'Transmission'], 1.0)
    put(bsdf, ['Specular IOR Level', 'Specular'], 0.5)
    put(bsdf, ['Subsurface Weight'], 0.0)
    put(bsdf, ['Subsurface Radius'], (1.0, 0.2, 0.1))
    put(bsdf, ['Subsurface Scale'], 0.05)
    put(bsdf, ['Coat Weight'], 0.0)
    put(bsdf, ['Coat Roughness'], 0.03)
    put(bsdf, ['Coat IOR'], 1.5)
    geom = nt.nodes.new('ShaderNodeNewGeometry')
    dot = nt.nodes.new('ShaderNodeVectorMath')
    dot.operation = 'DOT_PRODUCT'
    nt.links.new(geom.outputs['Normal'], dot.inputs[0])
    nt.links.new(geom.outputs['Incoming'], dot.inputs[1])
    noise = nt.nodes.new('ShaderNodeTexNoise')
    noise.noise_dimensions = '3D'
    for name, value in (('Scale', 6.0), ('Detail', 2.0), ('Roughness', 0.5),
                        ('Lacunarity', 2.0), ('Distortion', 0.0)):
        if name in noise.inputs:
            noise.inputs[name].default_value = value
    wave = nt.nodes.new('ShaderNodeTexWave')
    wave.wave_type = 'BANDS'
    for name, value in (('Scale', 3.0), ('Distortion', 1.0), ('Detail', 2.0),
                        ('Detail Scale', 0.5), ('Phase', 0.0)):
        if name in wave.inputs:
            wave.inputs[name].default_value = value
    nt.links.new(noise.outputs['Color'], wave.inputs['Vector'])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    mul.use_clamp = True
    dot_value = dot.outputs.get('Value') or dot.outputs[1]
    nt.links.new(dot_value, mul.inputs[0])
    nt.links.new(wave.outputs['Color'], mul.inputs[1])
    bump = nt.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.2
    bump.inputs['Distance'].default_value = 0.012
    nt.links.new(mul.outputs[0], bump.inputs['Height'])
    nt.links.new(geom.outputs['Normal'], bump.inputs['Normal'])
    nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    absorption = nt.nodes.new('ShaderNodeVolumeAbsorption')
    absorption.inputs['Color'].default_value = hex_lin('#4AA894')
    absorption.inputs['Density'].default_value = 0.6
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    nt.links.new(absorption.outputs['Volume'], out.inputs['Volume'])
    return mat


MATS = {}


def build_materials():
    MATS['grass_a'] = material('GrassA', '#74A050', roughness=0.85)
    MATS['grass_b'] = material('GrassB', '#639045', roughness=0.85)
    MATS['sand'] = material('Sand', '#EFB98F', roughness=0.72)
    MATS['rock'] = material('Rock', '#C6CBD0', roughness=0.55)
    MATS['rock_dark'] = material('RockDark', '#AEB5BB', roughness=0.6)
    MATS['mount_a'] = material('MountainA', '#7C9E5C', roughness=0.8)
    MATS['mount_b'] = material('MountainB', '#6A8A4E', roughness=0.8)
    MATS['mount_grey'] = material('MountainGrey', '#969FA4', roughness=0.6)
    MATS['snow'] = material('Snow', '#F2F5F7', roughness=0.55)
    MATS['tree_dark'] = material('TreeDark', '#5E9C4E', roughness=0.8)
    MATS['tree_light'] = material('TreeLight', '#9CC47A', roughness=0.8)
    MATS['trunk'] = material('Trunk', '#8A6A4B', roughness=0.85)
    MATS['copper'] = material('Copper', '#B96A3F', roughness=0.5)
    MATS['glass'] = material('Glass', '#E4F2F9', roughness=0.01, ior=1.45, transmission=1.0)
    MATS['backdrop'] = material('Backdrop', '#DCEBF4', roughness=1.0)
    MATS['water'] = water_material()


def xform(loc=(0, 0, 0), scale=(1, 1, 1), rot_z=0.0):
    m = Matrix.Translation(Vector(loc))
    if rot_z:
        m = m @ Matrix.Rotation(rot_z, 4, 'Z')
    m = m @ Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
    return m


def make_object(name, bm, mats):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    for mat in mats:
        me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def add_cone(bm, r1, r2, depth, loc, mat_index, segments=6, rot_z=0.0):
    existing = set(bm.faces)
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=r1, radius2=r2, depth=depth,
                          matrix=xform(loc, rot_z=rot_z))
    for face in bm.faces:
        if face not in existing:
            face.material_index = mat_index


def vy_island_radius(t):
    u = (t - 0.5) / 0.5
    return math.sqrt(max(0.0, 1.0 - u * u))


def build_hill():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0, matrix=Matrix.Identity(4))
    for v in bm.verts:
        t = (v.co.z + 1.0) * 0.5
        d = Vector((v.co.x, v.co.y))
        if d.length < 1e-6:
            d = Vector((1e-3, 0.0))
        d.normalize()
        if t <= 0.5:
            z = (t / 0.5 - 1.0) * 0.42
            fac = 1.0 - (0.5 - t) * 0.12
        else:
            z = ((t - 0.5) / 0.5) * HILL_TOP
            fac = vy_island_radius(t)
        r = HILL_R * fac
        r *= 1.0 + 0.055 * mnoise.noise(Vector((d.x * 2.4, d.y * 2.4, z * 2.0)))
        wobble = 0.02 * mnoise.noise(Vector((d.x * 5.0, d.y * 5.0, 1.0)))
        v.co = Vector((HILL_C.x + d.x * r, HILL_C.y + d.y * r, HILL_C.z + z + wobble))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(9.0),
                             verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    obj = make_object('Island', bm, [MATS['grass_a'], MATS['grass_b'], MATS['sand']])
    for p in obj.data.polygons:
        c = p.center
        dx = (c.x - HILL_C.x) / HILL_R
        dy = (c.y - HILL_C.y) / HILL_R
        rn = math.hypot(dx, dy)
        wob = mnoise.noise(Vector((c.x * 2.0, c.y * 2.0, 0.77)))
        apron = Vector((c.x - MOUNT_C.x, c.y - MOUNT_C.y)).length
        if c.z < 0.04 or rn > 0.90 + 0.05 * wob:
            p.material_index = 2
        elif c.z < 0.32 and apron < 1.15 and c.y < MOUNT_C.y + 0.05:
            p.material_index = 2
        else:
            p.material_index = 0 if rnd01(p.index) < 0.62 else 1
    return obj


def build_mountain():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0, matrix=Matrix.Identity(4))
    for v in bm.verts:
        t = (v.co.z + 1.0) * 0.5
        d = Vector((v.co.x, v.co.y))
        if d.length < 1e-6:
            d = Vector((1e-3, 0.0))
        d.normalize()
        prof = (1.0 - t) ** 0.64
        prof += 0.18 * math.exp(-((t - 0.42) / 0.15) ** 2)
        r = MOUNT_FOOT * prof
        r *= 1.0 + 0.16 * mnoise.noise(Vector((d.x * 2.2, d.y * 2.2, t * 2.6)))
        r *= 1.0 + 0.18 * mnoise.noise(Vector((d.x * 3.4, d.y * 3.4, t * 4.2)))
        r *= 1.0 + 0.12 * math.exp(-((t - 0.36) / 0.13) ** 2) * max(0.0, d.x * 0.8 + d.y * 0.2)
        z = 0.02 + t * MOUNT_HEIGHT
        z += 0.04 * mnoise.noise(Vector((d.x * 3.0, d.y * 3.0, t * 4.0)))
        z += 0.07 * t * mnoise.noise(Vector((d.x * 5.0, d.y * 5.0, t * 7.0)))
        v.co = Vector((MOUNT_C.x + d.x * r, MOUNT_C.y + d.y * r, z))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(8.0),
                             verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    obj = make_object('Mountain', bm, [MATS['mount_a'], MATS['mount_b'],
                                       MATS['mount_grey'], MATS['snow']])
    for p in obj.data.polygons:
        c = p.center
        n = p.normal
        if c.z > 1.10 + 0.16 * mnoise.noise(c * 2.8) and n.z > 0.2:
            p.material_index = 3
        elif c.z < 0.50:
            p.material_index = 0 if rnd01(p.index) < 0.6 else 1
        elif n.z < 0.60 and rnd01(p.index * 7 + 3) < 0.35:
            p.material_index = 2
        else:
            p.material_index = 0 if rnd01(p.index) < 0.65 else 1
    return obj


def build_rock(name, loc, size, seed, squash=(1.0, 1.0, 1.0), rot_z=0.0,
               subdiv=2, dissolve=14.0, mats=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0, matrix=Matrix.Identity(4))
    off = Vector((seed * 0.71, seed * 0.13, 0.0))
    for v in bm.verts:
        n = mnoise.noise(v.co * 1.9 + off)
        v.co *= 1.0 + 0.28 * n
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(dissolve),
                             verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    m = xform(loc, (size * squash[0], size * squash[1], size * squash[2]), rot_z)
    bm.transform(m)
    obj = make_object(name, bm, mats or [MATS['rock'], MATS['rock_dark']])
    for p in obj.data.polygons:
        if rnd01(p.index * 3 + seed) < 0.25:
            p.material_index = 1
    return obj


def build_tree(name, loc, scale, rot_z, seed):
    bm = bmesh.new()
    add_cone(bm, 0.022, 0.022, 0.10, (0.0, 0.0, 0.05), 0, segments=5)
    add_cone(bm, 0.115, 0.0, 0.17, (0.0, 0.0, 0.125), 1, segments=7)
    add_cone(bm, 0.088, 0.0, 0.15, (0.0, 0.0, 0.21), 1, segments=7)
    add_cone(bm, 0.058, 0.0, 0.13, (0.0, 0.0, 0.29), 2, segments=7)
    bm.transform(xform(loc, (scale, scale, scale), rot_z))
    return make_object(name, bm, [MATS['trunk'], MATS['tree_dark'], MATS['tree_light']])


def build_block():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0, matrix=Matrix.Identity(4))
    for v in bm.verts:
        v.co = Vector((v.co.x * 2.062, v.co.y * 2.062,
                       COPPER_BOTTOM + (v.co.z + 1.0) * 0.5 * (WATER_BOTTOM - COPPER_BOTTOM)))
    try:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=0.02,
                        segments=2, profile=0.7, affect='EDGES', clamp_overlap=True)
    except Exception as exc:
        print('copper bevel skipped:', exc)
    make_object('CopperPlate', bm, [MATS['copper']])

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0, matrix=Matrix.Identity(4))
    for v in bm.verts:
        v.co = Vector((v.co.x * HALF, v.co.y * HALF,
                       WATER_BOTTOM + (v.co.z + 1.0) * 0.5 * (WATERLINE - WATER_BOTTOM)))
    make_object('WaterVolume', bm, [MATS['water']])

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0, matrix=Matrix.Identity(4))
    bm.normal_update()
    for v in bm.verts:
        v.co = Vector((v.co.x * GLASS_HALF, v.co.y * GLASS_HALF,
                       WATER_BOTTOM + (v.co.z + 1.0) * 0.5 * (GLASS_TOP - WATER_BOTTOM)))
    kill = [f for f in bm.faces if abs(f.normal.z) > 0.5]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=-0.055)
    try:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=0.018,
                        segments=2, profile=0.7, affect='EDGES', clamp_overlap=True)
    except Exception as exc:
        print('glass bevel skipped:', exc)
    make_object('GlassCase', bm, [MATS['glass']])

    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=8, y_segments=8, size=200.0,
                          matrix=Matrix.Translation((0.0, 0.0, COPPER_BOTTOM - 0.04)))
    floor = make_object('Backdrop', bm, [MATS['backdrop']])
    try:
        floor.is_shadow_catcher = True
    except Exception as exc:
        print('shadow catcher skipped:', exc)


def hill_surface_z(x, y):
    dx = (x - HILL_C.x) / HILL_R
    dy = (y - HILL_C.y) / HILL_R
    rn = min(1.0, math.hypot(dx, dy))
    return HILL_TOP * math.sqrt(max(0.0, 1.0 - rn * rn))


def build_details():
    trees = [
        (-1.30, 0.09, 1.15), (-1.42, 0.46, 1.00), (-1.11, -0.29, 0.90),
        (-1.23, 0.84, 1.10), (0.64, 0.84, 1.05), (0.88, 0.34, 0.95),
        (0.72, -0.35, 1.05), (-0.11, 1.40, 1.10), (-0.74, 1.21, 0.95),
        (-0.49, -0.66, 0.90), (-1.42, -0.41, 0.75),
    ]
    for i, (x, y, scale) in enumerate(trees):
        z = hill_surface_z(x, y) - 0.03
        build_tree('Tree%02d' % i, (x, y, z), scale, rnd01(i * 11 + 1) * math.tau, 100 + i)

    rocks = [
        (-1.42, -0.91, 0.20, 0.10), (-1.05, -1.16, 0.12, -0.4),
        (0.88, -1.03, 0.16, 0.7), (1.63, -0.04, 0.11, 1.4),
        (0.14, 1.58, 0.13, 2.0), (-1.73, 0.21, 0.14, 2.6),
        (0.64, -1.53, 0.070, 0.2), (1.51, -1.53, 0.075, 1.1),
    ]
    for i, (x, y, size, rot) in enumerate(rocks):
        z = 0.0 if y > -1.0 else -0.05
        build_rock('Rock%02d' % i, (x, y, z), size, 200 + i,
                   squash=(1.0, 0.85, 0.7), rot_z=rot)

    pillars = [
        (1.25, -1.35, 0.100, 0.16), (0.45, -1.65, 0.085, 0.13),
        (-0.20, -1.60, 0.075, 0.12), (-1.35, -0.85, 0.080, 0.12),
    ]
    for i, (x, y, size, height) in enumerate(pillars):
        build_rock('Pillar%02d' % i, (x, y, -0.27), size, 300 + i,
                   squash=(1.0, 0.9, height / size), rot_z=rnd01(i) * math.tau)

    build_rock('Islet', (0.92, -1.45, -0.06), 0.28, 400,
               squash=(1.0, 0.9, 0.5), rot_z=0.5)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0,
                               matrix=xform((0.92, -1.45, 0.085), (0.18, 0.16, 0.045)))
    make_object('IsletGrass', bm, [MATS['grass_a']])
    build_tree('IsletTree0', (0.86, -1.43, 0.10), 0.60, 0.9, 500)
    build_tree('IsletTree1', (1.00, -1.50, 0.10), 0.52, 2.1, 501)


def build_camera():
    cam_data = bpy.data.cameras.new('Camera')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = ORTHO_WIDTH
    cam = bpy.data.objects.new('Camera', cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = Vector((0.0, -9.0, CAMERA_Z))
    cam.rotation_euler = (math.pi / 2.0, 0.0, 0.0)
    bpy.context.scene.camera = cam
    return cam


def add_area_light(name, loc, target, size, energy):
    data = bpy.data.lights.new(name, type='AREA')
    data.size = size
    data.energy = energy
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = Vector(loc)
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


def build_lights():
    key = add_area_light('Key', (0.3, -3.6, 3.8), (0.0, 0.0, 0.3), 7.0, 700.0)
    key.data.color = (1.0, 0.97, 0.92)
    add_area_light('Fill', (-3.2, -2.0, 1.6), (0.0, 0.0, 0.3), 7.0, 320.0)
    add_area_light('Rim', (0.6, 3.6, 3.0), (0.0, 0.0, 0.3), 4.0, 260.0)
    world = bpy.data.worlds.new('World')
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = hex_lin('#8CC3E8')
    bg.inputs['Strength'].default_value = 1.25
    bpy.context.scene.world = world


def configure_render():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'GPU'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        for dev in prefs.devices:
            dev.use = dev.type == 'OPTIX'
    except Exception as exc:
        print('OptiX setup skipped:', exc)
    scene.cycles.samples = SAMPLES
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.use_denoising = True
    try:
        scene.cycles.denoiser = 'OPENIMAGEDENOISE'
        scene.cycles.denoising_use_gpu = True
    except Exception as exc:
        print('denoiser setup skipped:', exc)
    scene.cycles.max_bounces = 12
    scene.cycles.transmission_bounces = 12
    scene.cycles.transparent_max_bounces = 16
    scene.cycles.volume_bounces = 4
    try:
        scene.cycles.caustics_reflective = False
        scene.cycles.caustics_refractive = True
    except Exception:
        pass
    scene.render.resolution_x = RES_W
    scene.render.resolution_y = RES_H
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.image_settings.color_depth = '8'
    try:
        scene.view_settings.view_transform = 'AgX'
        scene.view_settings.look = 'AgX - Punchy'
        scene.view_settings.exposure = 0.4
    except Exception as exc:
        print('view transform setup skipped:', exc)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_materials()
    build_hill()
    build_mountain()
    build_block()
    build_details()
    build_camera()
    build_lights()
    configure_render()
    os.makedirs(RENDER_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.context.scene.render.filepath = os.path.join(RENDER_DIR, 'render-%dx%d.png' % (RES_W, RES_H))
    bpy.ops.render.render(write_still=True)
    print('ISLAND_RENDER_OK', bpy.context.scene.render.filepath)


main()
