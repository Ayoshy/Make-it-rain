# Diorama Ocean - bed asset generator.
#
# One deterministic script builds everything the widget uses:
#   - assets/Ocean/ocean-bed.blend  the editable scene (bed mesh, material)
#   - assets/Ocean/ocean-bed.glb    the same geometry for any other tool
#   - assets/Ocean/ocean-bed.png    the runtime asset: RGB = albedo, A = height
#
# The water shader traces an implicit height field, so the bed is *described* by a
# height function. The mesh and the texture are sampled from that single function,
# which is what keeps the Blender scene and the real-time relief identical: there
# is no bake step that could drift from the geometry.
#
# Height convention shared with Shaders/Ocean.fx:
#   world x,z in [-1, 1]      the tray footprint
#   height y in [-0.70, -0.10]  baked as A = (y + 0.70) / 0.60
#   water line at y = 0, the shader maps A back with stoneTop=-0.40, amplitude .30
#
# Run:  "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --python assets/Ocean/build_bed.py

import math
import os
import sys

import bpy
from mathutils import Vector, noise

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "Ocean")
HEIGHT_MIN, HEIGHT_MAX = -0.70, -0.10
TEXTURE = 512
GRID = 192


def smoothstep(edge0, edge1, value):
    if edge1 == edge0:
        return 0.0
    t = min(max((value - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def dunes(x, z):
    """Slow sand relief: two crossed swells and one domain warp."""
    warp = math.sin(x * 1.7 + z * 1.1)
    return (0.020 * math.sin(x * 2.3 + warp)
            + 0.016 * math.sin(z * 3.1 - warp * 1.4)
            + 0.010 * math.sin(x * 5.7 + z * 4.3))


def ripples(x, z):
    """Wind ripples on the sand: a ridge pattern, not a regular grid."""
    phase = x * 34.0 + math.sin(z * 7.0) * 2.4 + z * 5.0
    return 0.0055 * abs(math.sin(phase)) ** 0.7


ROCKS = (
    # x, z, amplitude, spread, lobes, phase  (a rock is a rounded cone: no overhang)
    (-0.60, -0.30, 0.150, 9.0, 3.0, 0.4),
    (0.52, 0.38, 0.190, 7.0, 4.0, 1.9),
    (-0.24, 0.62, 0.105, 15.0, 3.0, 3.3),
    (0.14, -0.66, 0.070, 26.0, 5.0, 0.9),
    (0.74, -0.16, 0.062, 30.0, 4.0, 2.6),
    (-0.78, 0.52, 0.048, 34.0, 5.0, 4.4),
    (0.34, 0.72, 0.040, 40.0, 4.0, 1.2),
)


def rocks(x, z):
    total = 0.0
    for cx, cz, amplitude, spread, lobes, phase in ROCKS:
        dx, dz = x - cx, z - cz
        # Irregular footprint: the outline breaks into faces instead of a clean
        # dome, without falling into a star or a flower shape.
        angle = math.atan2(dz, dx)
        organ = noise.noise(Vector((x * 2.2 + phase, z * 2.2 - phase, 0.3)))
        lumpy = 1.0 + 0.09 * math.sin(angle * lobes + phase) + 0.16 * organ
        distance = math.sqrt(dx * dx + dz * dz) * lumpy
        total += amplitude * math.exp(-distance * distance * spread)
    return total


def rock_mask(x, z):
    """How much exposed rock shows at this point, from the local height."""
    height = dunes(x, z) + rocks(x, z)
    return smoothstep(0.045, 0.110, height)


def height(x, z):
    base = -0.470 + dunes(x, z) + rocks(x, z) + ripples(x, z)
    grain = noise.noise(Vector((x * 9.0, z * 9.0, 0.5))) * 0.0035
    return min(max(base + grain, HEIGHT_MIN), HEIGHT_MAX)


def albedo(x, z):
    """Sand, wet gravel and bare rock, in the tray's own colours."""
    height_value = height(x, z)
    rock = smoothstep(0.055, 0.130, height_value + 0.470)
    gravel = smoothstep(0.25, 0.75, noise.noise(Vector((x * 3.4, z * 3.4, 1.7))) * 0.5 + 0.5)
    sand = (0.74, 0.63, 0.47)
    dark_sand = (0.55, 0.50, 0.42)
    stone = (0.32, 0.31, 0.33)
    light_stone = (0.52, 0.51, 0.52)
    ridge = abs(math.sin(x * 34.0 + math.sin(z * 7.0) * 2.4 + z * 5.0))
    shade = 1.0 - 0.14 * ridge
    base = [sand[i] * (1.0 - gravel * 0.55) + dark_sand[i] * (gravel * 0.55) for i in range(3)]
    base = [base[i] * shade for i in range(3)]
    mix = smoothstep(0.35, 0.95, rock)
    tone = [light_stone[i] * (1.0 - 0.55 * rock) + stone[i] * (0.55 * rock) for i in range(3)]
    return [base[i] * (1.0 - mix) + tone[i] * mix for i in range(3)]


def encode(height_value):
    return (height_value - HEIGHT_MIN) / (HEIGHT_MAX - HEIGHT_MIN)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.engine = "BLENDER_EEVEE"
    return scene


def build_mesh():
    """The bed as geometry: one height field, so the mesh and the texture agree."""
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=GRID, y_subdivisions=GRID, size=2.0, location=(0, 0, 0))
    bed = bpy.context.active_object
    bed.name = "OceanBed"
    mesh = bed.data
    colors = mesh.color_attributes.new(name="Bed", type="FLOAT_COLOR", domain="POINT")
    for index, vertex in enumerate(mesh.vertices):
        x, y = vertex.co.x, vertex.co.y
        # Blender's grid lies in the XY plane: X is the shader's x, Y the shader's z.
        vertex.co.z = height(x, y)
        rgb = albedo(x, y)
        colors.data[index].color = (rgb[0], rgb[1], rgb[2], 1.0)
    material = bpy.data.materials.new("BedRock")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    attribute = nodes.new("ShaderNodeVertexColor")
    attribute.layer_name = "Bed"
    principled = nodes["Principled BSDF"]
    principled.inputs["Roughness"].default_value = 0.85
    material.node_tree.links.new(attribute.outputs["Color"], principled.inputs["Base Color"])
    mesh.materials.append(material)
    bpy.ops.object.shade_smooth()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Vertex normals come from the displaced grid: nothing else has to be baked.
    mesh.calc_normals_split() if hasattr(mesh, "calc_normals_split") else None
    return bed


def write_texture(path):
    """The asset the widget reads: RGB albedo, A height, top-down, footprint 2x2."""
    image = bpy.data.images.new("ocean-bed", width=TEXTURE, height=TEXTURE, alpha=True, float_buffer=False)
    pixels = [0.0] * (TEXTURE * TEXTURE * 4)
    step = 2.0 / TEXTURE
    for row in range(TEXTURE):
        # Image rows run bottom-up; the world z axis runs from -1 to 1 the same way.
        z = -1.0 + step * (row + 0.5)
        for column in range(TEXTURE):
            x = -1.0 + step * (column + 0.5)
            rgb = albedo(x, z)
            alpha = encode(height(x, z))
            offset = (row * TEXTURE + column) * 4
            pixels[offset] = rgb[0]
            pixels[offset + 1] = rgb[1]
            pixels[offset + 2] = rgb[2]
            pixels[offset + 3] = alpha
    image.pixels[:] = pixels
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def main():
    os.makedirs(OUT, exist_ok=True)
    clear_scene()
    bed = build_mesh()
    blend = os.path.join(OUT, "ocean-bed.blend")
    glb = os.path.join(OUT, "ocean-bed.glb")
    png = os.path.join(OUT, "ocean-bed.png")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=False)
    write_texture(png)
    print("bed: vertices=%d height=[%.3f, %.3f]" % (len(bed.data.vertices), min(v.co.z for v in bed.data.vertices), max(v.co.z for v in bed.data.vertices)))
    for path in (blend, glb, png):
        print("wrote %s (%d bytes)" % (path, os.path.getsize(path)))


main()
