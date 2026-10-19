"""Builds every model for Pulse and exports one FBX per model.

Run from the project folder:
    blender --background --factory-startup --python Tools/blender/build_models.py -- Assets/Game/Art/Models ArtSource/pulse_models.blend

Rules every model follows:
- Unity space: x right, y up, z forward, in metres. The origin is where the part is mounted
  (ground for things that stand, the hip for a leg, the centre for wheels and projectiles).
- Four material slots, always in this order: Base, Accent, Dark, Glow. The game assigns the real
  materials at run time, for example the state colour as Accent on a weapon.
- Flat shading and bevelled edges, so even plain boxes catch the light.
"""
import math
import os
import random
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
OUT_DIR = os.path.abspath(args[0])
BLEND_PATH = os.path.abspath(args[1])

BS, AC, DK, GL = 0, 1, 2, 3   # Base, Accent, Dark, Glow
MATS = []
AXIS = {"y": (0.0, 0.0, 0.0), "z": (math.radians(90), 0.0, 0.0), "x": (0.0, math.radians(90), 0.0)}
built = []


def unity(x, y, z):
    """Unity (right, up, forward) to Blender. A front facing -Y in Blender faces +Z in Unity."""
    return (-x, -z, y)


def done(slot):
    obj = bpy.context.active_object
    for mat in MATS:
        obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.material_index = slot
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def box(size, center, slot=BS, bev=0.025, rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=unity(*center), rotation=rot)
    bpy.context.active_object.scale = (size[0], size[2], size[1])
    obj = done(slot)
    width = min(bev, min(size) * 0.3)
    if width > 0.004:
        modifier = obj.modifiers.new("Bevel", "BEVEL")
        modifier.width = width
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def cyl(radius, length, center, axis="y", sides=10, slot=BS, end=None, rot=None):
    """A cylinder or cone along a Unity axis. `end` is the radius at the far (up or forward) end."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=radius if end is None else end, depth=length,
        location=unity(*center), rotation=rot or AXIS[axis])
    return done(slot)


def ball(radius, center, slot=BS, sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=radius, location=unity(*center))
    return done(slot)


def torus(major, minor, center, slot=BS, axis="y"):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major, minor_radius=minor, major_segments=12, minor_segments=5,
        location=unity(*center), rotation=AXIS[axis])
    return done(slot)


def rock(radius, squash, seed, slot=BS, sub=2, at=(0.0, 0.0), rough=0.22):
    """An irregular boulder: an ico sphere with every vertex pushed in or out. The seed fixes its shape."""
    rng = random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=sub, radius=radius, location=unity(at[0], radius * squash * 0.55, at[1]))
    obj = bpy.context.active_object
    for vertex in obj.data.vertices:
        factor = 1.0 + rng.uniform(-rough, rough)
        vertex.co.x *= factor
        vertex.co.y *= factor
        vertex.co.z *= factor * squash
    return done(slot)


def plate(radius, sides, seed, slot=BS, height=0.0, at=(0.0, 0.0), rough=0.25):
    """A flat, irregular polygon lying on the ground."""
    rng = random.Random(seed)
    bpy.ops.mesh.primitive_circle_add(
        vertices=sides, radius=radius, fill_type="NGON", location=unity(at[0], height, at[1]))
    obj = bpy.context.active_object
    for vertex in obj.data.vertices:
        factor = 1.0 + rng.uniform(-rough, rough)
        vertex.co.x *= factor
        vertex.co.y *= factor
    return done(slot)


def around(count, radius, make, start=0.0):
    """Calls make(x, z, angle) for points on a circle and returns the parts."""
    parts = []
    for i in range(count):
        angle = start + i * 2.0 * math.pi / count
        parts.append(make(math.cos(angle) * radius, math.sin(angle) * radius, angle))
    return parts


def anchors():
    """Four hidden 2 mm triangles, one per material, so every model has all four slots in order."""
    mesh = bpy.data.meshes.new("Anchors")
    verts, faces = [], []
    for i in range(4):
        x = i * 0.004
        verts += [(x, 0.0, -0.02), (x + 0.002, 0.0, -0.02), (x, 0.002, -0.02)]
        faces.append((i * 3, i * 3 + 1, i * 3 + 2))
    mesh.from_pydata(verts, [], faces)
    obj = bpy.data.objects.new("Anchors", mesh)
    bpy.context.collection.objects.link(obj)
    for mat in MATS:
        mesh.materials.append(mat)
    for i, polygon in enumerate(mesh.polygons):
        polygon.material_index = i
    return obj


def build(name, parts):
    """Joins the parts into one mesh with its origin at the mount point, and exports it."""
    parts = [anchors()] + [p for p in parts if p is not None]
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = obj.data.name = name
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False)
    built.append(obj)


def mount_ring():
    return [cyl(0.3, 0.08, (0, 0.04, 0), sides=12, slot=DK), cyl(0.24, 0.05, (0, 0.1, 0), sides=12, slot=AC)]


# ---------------------------------------------------------------- the machine

def machine():
    parts = [
        box((1.7, 0.34, 2.8), (0, 0.52, 0), bev=0.06),
        box((1.2, 0.14, 2.4), (0, 0.3, 0), DK),
        box((1.5, 0.12, 2.5), (0, 0.89, 0), bev=0.04),
        box((1.74, 0.07, 2.84), (0, 0.73, 0), AC),
        box((1.4, 0.3, 0.3), (0, 0.5, 1.52), DK, bev=0.05),
        box((0.8, 0.16, 0.05), (0, 0.6, 1.42), DK),
        box((1.5, 0.08, 0.1), (0, 0.38, 1.7), AC), box((0.08, 0.2, 0.1), (0.6, 0.48, 1.68), AC),
        box((0.08, 0.2, 0.1), (-0.6, 0.48, 1.68), AC),
        box((0.24, 0.12, 0.06), (0.55, 0.62, 1.42), GL), box((0.24, 0.12, 0.06), (-0.55, 0.62, 1.42), GL),
        box((0.2, 0.08, 0.05), (0.6, 0.62, -1.41), GL), box((0.2, 0.08, 0.05), (-0.6, 0.62, -1.41), GL),
        box((1.3, 0.22, 0.2), (0, 0.48, -1.48), DK), box((0.2, 0.1, 0.16), (0, 0.36, -1.6), DK),
        box((0.05, 0.05, 2.2), (0.72, 0.98, 0), DK), box((0.05, 0.05, 2.2), (-0.72, 0.98, 0), DK),
        box((0.12, 0.22, 0.9), (0.9, 0.56, 0), AC), box((0.12, 0.22, 0.9), (-0.9, 0.56, 0), AC),
        cyl(0.06, 0.5, (0.66, 1.1, -1.22), sides=8, slot=DK), cyl(0.06, 0.5, (-0.66, 1.1, -1.22), sides=8, slot=DK),
        cyl(0.075, 0.08, (0.66, 1.36, -1.22), sides=8, slot=AC), cyl(0.075, 0.08, (-0.66, 1.36, -1.22), sides=8, slot=AC),
        box((0.5, 0.05, 0.5), (0, 0.955, -0.5), DK, bev=0.0), cyl(0.012, 0.7, (0.6, 1.3, 1.1), sides=5, slot=DK),
        ball(0.03, (0.6, 1.66, 1.1), GL),
    ]
    for x in (-0.97, 0.97):
        for z in (-0.9, 0.9):
            parts += [box((0.5, 0.07, 0.95), (x, 0.8, z), bev=0.03), box((0.06, 0.16, 0.95), (x * 1.2, 0.72, z), AC)]
    for z in (-0.7, -0.35, 0.35, 0.7):
        parts += [cyl(0.03, 0.03, (0.86, 0.73, z), axis="x", sides=6, slot=DK),
                  cyl(0.03, 0.03, (-0.86, 0.73, z), axis="x", sides=6, slot=DK)]
    build("Crawler_Body", parts)

    # The two smaller frames. Same deck height as the Crawler, so every module sits on all three.
    def chassis(name, width, length, rails):
        half, front, back = width / 2, length / 2, -length / 2
        body = [
            box((width, 0.34, length), (0, 0.52, 0), bev=0.06),
            box((width - 0.5, 0.14, length - 0.4), (0, 0.3, 0), DK),
            box((width - 0.2, 0.12, length - 0.3), (0, 0.89, 0), bev=0.04),
            box((width + 0.04, 0.07, length + 0.04), (0, 0.73, 0), AC),
            box((width - 0.3, 0.28, 0.28), (0, 0.5, front + 0.1), DK, bev=0.05),
            box((width - 0.2, 0.08, 0.1), (0, 0.38, front + 0.28), AC),
            box((0.22, 0.12, 0.06), (half - 0.3, 0.62, front + 0.02), GL), box((0.22, 0.12, 0.06), (-half + 0.3, 0.62, front + 0.02), GL),
            box((0.18, 0.08, 0.05), (half - 0.25, 0.62, back - 0.01), GL), box((0.18, 0.08, 0.05), (-half + 0.25, 0.62, back - 0.01), GL),
            box((width - 0.4, 0.22, 0.2), (0, 0.48, back - 0.08), DK),
            box((0.12, 0.22, length * 0.3), (half + 0.05, 0.56, 0), AC), box((0.12, 0.22, length * 0.3), (-half - 0.05, 0.56, 0), AC),
            cyl(0.012, 0.6, (half - 0.25, 1.25, front - 0.3), sides=5, slot=DK), ball(0.03, (half - 0.25, 1.56, front - 0.3), GL),
        ]
        if rails:
            body += [box((0.05, 0.05, length - 0.6), (half - 0.13, 0.98, 0), DK), box((0.05, 0.05, length - 0.6), (-half + 0.13, 0.98, 0), DK)]
        else:
            # A roll bar over the back.
            body += [cyl(0.04, 0.5, (half - 0.14, 1.2, back + 0.35), sides=6, slot=DK), cyl(0.04, 0.5, (-half + 0.14, 1.2, back + 0.35), sides=6, slot=DK),
                     box((width - 0.2, 0.07, 0.07), (0, 1.46, back + 0.35), DK, bev=0.02)]
        for x in (-half - 0.12, half + 0.12):
            for z in (-length * 0.32, length * 0.32):
                body += [box((0.5, 0.07, 0.9), (x, 0.8, z), bev=0.03), box((0.06, 0.16, 0.9), (x * 1.22, 0.72, z), AC)]
        build(name, body)

    chassis("Buggy_Body", 1.4, 2.1, rails=False)
    chassis("Rover_Body", 1.55, 2.45, rails=True)

    wheel = [cyl(0.38, 0.3, (0, 0, 0), axis="x", sides=16, slot=DK), cyl(0.25, 0.34, (0, 0, 0), axis="x", sides=10, slot=AC),
             cyl(0.11, 0.4, (0, 0, 0), axis="x", sides=6), cyl(0.3, 0.02, (0.16, 0, 0), axis="x", sides=10, slot=DK)]
    for k in range(10):
        a = k * math.pi / 5
        wheel.append(box((0.32, 0.07, 0.12), (0, 0.38 * math.cos(a), 0.38 * math.sin(a)), DK, bev=0.015, rot=(a, 0, 0)))
    for k in range(5):
        a = k * 2 * math.pi / 5
        wheel.append(cyl(0.025, 0.36, (0, 0.17 * math.cos(a), 0.17 * math.sin(a)), axis="x", sides=5, slot=DK))
    build("Wheel", wheel)

    build("Autocannon", mount_ring() + [
        box((0.42, 0.26, 0.5), (0, 0.27, 0), bev=0.04), box((0.3, 0.2, 0.12), (0, 0.28, 0.3), DK),
        cyl(0.045, 0.75, (0.09, 0.29, 0.72), axis="z", sides=8, slot=DK), cyl(0.045, 0.75, (-0.09, 0.29, 0.72), axis="z", sides=8, slot=DK),
        cyl(0.066, 0.1, (0.09, 0.29, 1.06), axis="z", sides=8, slot=AC), cyl(0.066, 0.1, (-0.09, 0.29, 1.06), axis="z", sides=8, slot=AC),
        cyl(0.06, 0.06, (0.09, 0.29, 0.5), axis="z", sides=8, slot=AC), cyl(0.06, 0.06, (-0.09, 0.29, 0.5), axis="z", sides=8, slot=AC),
        box((0.16, 0.2, 0.3), (0.3, 0.26, -0.05), AC), box((0.07, 0.06, 0.22), (0.2, 0.39, 0.04), DK),
        box((0.08, 0.08, 0.16), (-0.12, 0.45, 0.05), DK), box((0.05, 0.05, 0.02), (-0.12, 0.45, 0.14), GL, bev=0),
        box((0.3, 0.03, 0.08), (0, 0.41, -0.12), DK, bev=0), box((0.3, 0.03, 0.08), (0, 0.41, -0.22), DK, bev=0),
        box((0.34, 0.14, 0.1), (0, 0.24, -0.28), DK),
    ])
    # Longshot: one long barrel with a brake at the end, a scope and a heavy breech.
    build("Longshot", mount_ring() + [
        box((0.34, 0.26, 0.56), (0, 0.27, -0.06), bev=0.04), box((0.26, 0.2, 0.2), (0, 0.28, 0.3), DK),
        cyl(0.06, 1.25, (0, 0.3, 0.98), axis="z", sides=8, slot=DK), cyl(0.085, 0.3, (0, 0.3, 0.52), axis="z", sides=8, slot=AC),
        box((0.2, 0.12, 0.18), (0, 0.3, 1.62), AC, bev=0.02), box((0.24, 0.03, 0.05), (0, 0.3, 1.58), DK, bev=0),
        box((0.24, 0.03, 0.05), (0, 0.3, 1.66), DK, bev=0),
        cyl(0.04, 0.42, (0, 0.47, 0.02), axis="z", sides=8, slot=DK), cyl(0.05, 0.03, (0, 0.47, 0.24), axis="z", sides=8, slot=GL),
        box((0.06, 0.08, 0.06), (0, 0.42, -0.1), DK), box((0.06, 0.08, 0.06), (0, 0.42, 0.14), DK),
        box((0.14, 0.2, 0.32), (0.25, 0.26, -0.1), AC), box((0.3, 0.16, 0.12), (0, 0.24, -0.38), DK),
    ])
    # Frost Jet: a wide flat nozzle fed by one fat tank with cooling ribs.
    build("FrostJet", mount_ring() + [
        box((0.36, 0.2, 0.42), (0, 0.22, -0.02), bev=0.04),
        cyl(0.17, 0.52, (0, 0.44, -0.14), axis="z", sides=10, slot=AC), ball(0.16, (0, 0.44, -0.4), AC),
        cyl(0.185, 0.04, (0, 0.44, -0.02), axis="z", sides=10, slot=DK), cyl(0.185, 0.04, (0, 0.44, -0.16), axis="z", sides=10, slot=DK),
        cyl(0.185, 0.04, (0, 0.44, -0.3), axis="z", sides=10, slot=DK),
        box((0.14, 0.1, 0.34), (0, 0.28, 0.36), DK, bev=0.02), box((0.4, 0.07, 0.16), (0, 0.28, 0.6), AC, bev=0.02),
        box((0.34, 0.03, 0.03), (0, 0.28, 0.69), GL, bev=0), cyl(0.025, 0.3, (0.2, 0.24, 0.2), axis="z", sides=5, slot=DK),
        box((0.1, 0.1, 0.12), (0, 0.36, 0.14), DK),
    ])
    build("Flamethrower", mount_ring() + [
        box((0.36, 0.2, 0.42), (0, 0.22, -0.02), bev=0.04),
        cyl(0.11, 0.5, (0.14, 0.4, -0.12), axis="z", sides=10, slot=AC), cyl(0.11, 0.5, (-0.14, 0.4, -0.12), axis="z", sides=10, slot=AC),
        cyl(0.122, 0.04, (0.14, 0.4, -0.02), axis="z", sides=10, slot=DK), cyl(0.122, 0.04, (-0.14, 0.4, -0.02), axis="z", sides=10, slot=DK),
        cyl(0.122, 0.04, (0.14, 0.4, -0.26), axis="z", sides=10, slot=DK), cyl(0.122, 0.04, (-0.14, 0.4, -0.26), axis="z", sides=10, slot=DK),
        ball(0.1, (0.14, 0.4, -0.38), AC), ball(0.1, (-0.14, 0.4, -0.38), AC),
        cyl(0.06, 0.36, (0, 0.28, 0.42), axis="z", sides=8, slot=DK, end=0.1), cyl(0.12, 0.05, (0, 0.28, 0.62), axis="z", sides=8, slot=AC),
        ball(0.035, (0, 0.19, 0.6), GL), cyl(0.025, 0.3, (0.2, 0.24, 0.2), axis="z", sides=5, slot=DK),
        box((0.1, 0.1, 0.12), (0, 0.36, 0.14), DK), box((0.24, 0.05, 0.2), (0, 0.33, -0.02), DK),
    ])
    build("OilSprayer", mount_ring() + [
        cyl(0.22, 0.4, (0, 0.34, -0.08), sides=12, slot=AC), cyl(0.235, 0.04, (0, 0.22, -0.08), sides=12, slot=DK),
        cyl(0.235, 0.04, (0, 0.46, -0.08), sides=12, slot=DK), cyl(0.16, 0.04, (0, 0.56, -0.08), sides=12, slot=DK),
        cyl(0.05, 0.05, (0.08, 0.6, -0.08), sides=6), box((0.18, 0.18, 0.18), (0, 0.22, 0.22), bev=0.03),
        box((0.06, 0.22, 0.06), (0, 0.38, 0.22), DK), cyl(0.04, 0.5, (0, 0.47, 0.44), axis="z", sides=8, slot=DK),
        cyl(0.045, 0.12, (0, 0.47, 0.72), axis="z", sides=8, end=0.085), cyl(0.09, 0.03, (0, 0.47, 0.79), axis="z", sides=8, slot=AC),
        cyl(0.025, 0.3, (0.16, 0.2, 0.1), axis="z", sides=5, slot=DK), box((0.08, 0.1, 0.04), (0, 0.34, 0.15), GL, bev=0),
    ])
    coil = mount_ring() + [
        box((0.42, 0.14, 0.42), (0, 0.17, 0), bev=0.04), cyl(0.06, 0.62, (0, 0.55, 0), sides=8, slot=DK),
        ball(0.13, (0, 0.92, 0), AC), cyl(0.09, 0.05, (0, 0.82, 0), sides=8, slot=DK),
        box((0.1, 0.16, 0.1), (0.21, 0.3, 0), bev=0.02), box((0.1, 0.16, 0.1), (-0.21, 0.3, 0), bev=0.02),
        box((0.06, 0.04, 0.02), (0, 0.2, 0.22), GL, bev=0),
    ]
    for y, r in ((0.36, 0.2), (0.48, 0.17), (0.6, 0.14), (0.71, 0.11)):
        coil.append(torus(r, 0.022, (0, y, 0), AC))
    coil += around(4, 0.17, lambda x, z, a: cyl(0.018, 0.5, (x, 0.47, z), sides=5, slot=DK), start=math.pi / 4)
    build("TeslaCoil", coil)
    build("SupportModule", [
        box((0.38, 0.1, 0.38), (0, 0.05, 0), DK), box((0.3, 0.14, 0.3), (0, 0.17, 0), bev=0.03),
        box((0.3, 0.1, 0.03), (0, 0.29, -0.08), AC, bev=0), box((0.3, 0.1, 0.03), (0, 0.29, 0), AC, bev=0),
        box((0.3, 0.1, 0.03), (0, 0.29, 0.08), AC, bev=0), cyl(0.04, 0.04, (0.11, 0.26, 0.13), sides=6, slot=GL),
        cyl(0.02, 0.2, (-0.16, 0.14, 0.16), sides=5, slot=DK),
    ])

    # Projectiles: origin at the centre, nose toward +z.
    build("Bolt", [
        cyl(0.075, 0.42, (0, 0, 0.16), axis="z", sides=6, end=0.0), cyl(0.0, 0.2, (0, 0, -0.15), axis="z", sides=6, end=0.075),
        box((0.2, 0.012, 0.16), (0, 0, -0.12), bev=0), box((0.012, 0.2, 0.16), (0, 0, -0.12), bev=0),
    ])
    build("Glob", [rock(0.28, 1.0, 21, sub=2, rough=0.12)])

    # A Beat, the currency: a splinter of the Pulse. A four-sided shard standing on its point.
    build("Beat", [
        cyl(0.2, 0.42, (0, 0.5, 0), sides=4, slot=GL, end=0.0), cyl(0.0, 0.3, (0, 0.14, 0), sides=4, slot=GL, end=0.2),
        box((0.07, 0.07, 0.07), (0.07, 0.4, 0.07), BS, bev=0),
    ])

    # A heart, dropped by enemies: two round lobes over a square stood on its corner, with a small
    # highlight. Flat, so it can turn on the spot. Its origin is at its lower tip.
    turned = (0.0, math.radians(45), 0.0)
    build("Heart", [
        cyl(0.2, 0.16, (0.16, 0.48, 0), axis="z", sides=12, slot=GL), cyl(0.2, 0.16, (-0.16, 0.48, 0), axis="z", sides=12, slot=GL),
        box((0.42, 0.42, 0.16), (0, 0.32, 0), GL, bev=0, rot=turned),
        box((0.09, 0.09, 0.02), (-0.17, 0.54, 0.085), BS, bev=0), box((0.09, 0.09, 0.02), (-0.17, 0.54, -0.085), BS, bev=0),
    ])


# ---------------------------------------------------------------- enemies and the lobby character

def creatures():
    build("Grunt", [
        box((0.5, 0.18, 0.4), (0, 0.42, 0), DK), cyl(0.34, 0.36, (0, 0.68, 0), sides=6, end=0.26),
        box((0.2, 0.1, 0.32), (0.36, 0.84, 0), AC), box((0.2, 0.1, 0.32), (-0.36, 0.84, 0), AC),
        box((0.3, 0.2, 0.28), (0, 0.96, 0.06), bev=0.04), box((0.26, 0.06, 0.05), (0, 0.97, 0.21), GL, bev=0),
        cyl(0.012, 0.26, (0.1, 1.18, -0.05), sides=5, slot=DK), ball(0.025, (0.1, 1.32, -0.05), GL),
        box((0.1, 0.3, 0.12), (0.4, 0.62, 0.08), DK), box((0.1, 0.3, 0.12), (-0.4, 0.62, 0.08), DK),
        box((0.12, 0.1, 0.22), (0.4, 0.44, 0.18), AC), box((0.12, 0.1, 0.22), (-0.4, 0.44, 0.18), AC),
        box((0.26, 0.24, 0.14), (0, 0.7, -0.26), DK), cyl(0.03, 0.18, (-0.08, 0.9, -0.28), sides=6, slot=DK),
        box((0.2, 0.05, 0.02), (0, 0.72, 0.3), AC, bev=0), box((0.34, 0.06, 0.3), (0, 0.52, 0), AC),
    ])
    build("Grunt_Leg", [
        box((0.12, 0.28, 0.14), (0, -0.14, 0), DK), ball(0.07, (0, -0.02, 0), AC), box((0.1, 0.2, 0.1), (0, -0.29, 0.02)),
        box((0.16, 0.07, 0.26), (0, -0.385, 0.06), AC), box((0.14, 0.05, 0.03), (0, -0.2, 0.08), AC, bev=0),
    ])
    build("Brute", [
        cyl(0.5, 0.26, (0, 0.52, 0), sides=8, slot=DK), cyl(0.8, 0.7, (0, 0.98, 0), sides=6, end=0.62),
        box((1.1, 0.3, 0.5), (0, 1.22, 0.26), AC, bev=0.05), box((0.5, 0.4, 0.62), (0.86, 1.32, 0), bev=0.06),
        box((0.5, 0.4, 0.62), (-0.86, 1.32, 0), bev=0.06), box((0.3, 0.7, 0.3), (0.9, 0.8, 0.1), DK),
        box((0.3, 0.7, 0.3), (-0.9, 0.8, 0.1), DK), box((0.42, 0.36, 0.5), (0.9, 0.42, 0.2), AC, bev=0.06),
        box((0.42, 0.36, 0.5), (-0.9, 0.42, 0.2), AC, bev=0.06), box((0.4, 0.26, 0.36), (0, 1.52, 0.15), DK, bev=0.05),
        box((0.36, 0.08, 0.05), (0, 1.54, 0.34), GL, bev=0), cyl(0.07, 0.5, (0.3, 1.66, -0.3), sides=8, slot=DK),
        cyl(0.07, 0.5, (-0.3, 1.66, -0.3), sides=8, slot=DK), cyl(0.085, 0.07, (0.3, 1.93, -0.3), sides=8, slot=AC),
        cyl(0.085, 0.07, (-0.3, 1.93, -0.3), sides=8, slot=AC), box((0.9, 0.6, 0.14), (0, 1.1, -0.56), DK),
        box((0.22, 0.22, 0.05), (0, 0.92, 0.7), GL, bev=0), box((0.6, 0.08, 0.08), (0.86, 1.55, 0), AC),
        box((0.6, 0.08, 0.08), (-0.86, 1.55, 0), AC), box((0.7, 0.1, 0.06), (0, 0.7, 0.62), AC, bev=0),
    ])
    build("Brute_Leg", [
        box((0.26, 0.3, 0.3), (0, -0.15, 0), DK, bev=0.04), ball(0.14, (0, -0.02, 0), AC),
        box((0.3, 0.2, 0.34), (0, -0.34, 0.03), bev=0.04), box((0.36, 0.1, 0.5), (0, -0.45, 0.08), AC, bev=0.03),
    ])

    species()

    # Kiln, the Warden of the Cinder Flats: a walking furnace five metres tall. Its fire shows through
    # the grate in its chest; its fists are anvils.
    kiln = [
        box((2.7, 0.9, 1.9), (0, 1.75, 0), DK, bev=0.1), cyl(1.9, 2.4, (0, 3.35, 0), sides=8, end=1.5),
        box((1.6, 1.4, 0.2), (0, 3.15, 1.72), DK, bev=0.05), box((1.3, 1.1, 0.1), (0, 3.15, 1.8), GL, bev=0),
        box((1.6, 1.3, 1.7), (2.3, 4.25, 0), AC, bev=0.14), box((1.6, 1.3, 1.7), (-2.3, 4.25, 0), AC, bev=0.14),
        box((0.85, 1.9, 0.95), (2.5, 2.9, 0.15), DK, bev=0.08), box((0.85, 1.9, 0.95), (-2.5, 2.9, 0.15), DK, bev=0.08),
        box((1.35, 1.15, 1.5), (2.5, 1.55, 0.3), AC, bev=0.14), box((1.35, 1.15, 1.5), (-2.5, 1.55, 0.3), AC, bev=0.14),
        box((1.0, 0.12, 0.08), (2.5, 1.75, 1.07), GL, bev=0), box((1.0, 0.12, 0.08), (-2.5, 1.75, 1.07), GL, bev=0),
        box((1.05, 0.75, 0.95), (0, 4.95, 0.3), DK, bev=0.08), box((0.75, 0.15, 0.06), (0, 5.0, 0.79), GL, bev=0),
        cyl(0.3, 1.4, (0.85, 5.3, -0.9), sides=8, slot=DK), cyl(0.3, 1.4, (-0.85, 5.3, -0.9), sides=8, slot=DK),
        cyl(0.32, 0.12, (0.85, 6.06, -0.9), sides=8, slot=GL), cyl(0.32, 0.12, (-0.85, 6.06, -0.9), sides=8, slot=GL),
        cyl(0.42, 1.9, (0, 5.5, -1.15), sides=8, slot=DK), cyl(0.44, 0.14, (0, 6.5, -1.15), sides=8, slot=GL),
        cyl(1.1, 1.7, (0, 3.3, -1.55), axis="z", sides=8, slot=AC),
        box((0.3, 1.7, 1.3), (1.78, 3.25, 0.2), AC, bev=0.05), box((0.3, 1.7, 1.3), (-1.78, 3.25, 0.2), AC, bev=0.05),
        box((2.2, 0.25, 0.2), (0, 2.3, 1.6), AC, bev=0.04), box((2.4, 0.25, 0.2), (0, 4.15, 1.5), AC, bev=0.04),
    ]
    for x in (-0.48, -0.16, 0.16, 0.48):
        kiln.append(box((0.09, 1.3, 0.14), (x, 3.15, 1.9), DK, bev=0))
    build("Kiln", kiln)
    build("Kiln_Leg", [
        box((0.9, 1.1, 1.0), (0, -0.55, 0), DK, bev=0.1), ball(0.42, (0, -0.05, 0), AC),
        box((1.15, 0.4, 1.6), (0, -1.25, 0.2), AC, bev=0.08), box((0.7, 0.1, 0.06), (0, -1.1, 1.0), GL, bev=0),
    ])

    # The small robot the player walks around the lobby with.
    build("Character", [
        box((0.34, 0.3, 0.24), (0, 0.58, 0), bev=0.04), box((0.12, 0.08, 0.03), (0, 0.62, 0.125), GL, bev=0),
        cyl(0.05, 0.06, (0, 0.76, 0), sides=8, slot=DK), box((0.3, 0.22, 0.26), (0, 0.9, 0), bev=0.05),
        box((0.24, 0.08, 0.04), (0, 0.91, 0.13), GL, bev=0), cyl(0.05, 0.04, (0.16, 0.9, 0), axis="x", sides=8, slot=AC),
        cyl(0.05, 0.04, (-0.16, 0.9, 0), axis="x", sides=8, slot=AC), cyl(0.012, 0.2, (0.08, 1.1, -0.04), sides=5, slot=DK),
        ball(0.03, (0.08, 1.21, -0.04), GL), box((0.24, 0.26, 0.12), (0, 0.6, -0.18), AC, bev=0.03),
        ball(0.06, (0.2, 0.7, 0), DK), ball(0.06, (-0.2, 0.7, 0), DK), box((0.28, 0.1, 0.2), (0, 0.4, 0), DK),
        cyl(0.03, 0.14, (0.07, 0.62, -0.26), sides=6, slot=DK), box((0.26, 0.03, 0.02), (0, 0.5, 0.125), AC, bev=0),
    ])
    build("Character_Leg", [
        box((0.09, 0.2, 0.1), (0, -0.1, 0), DK), box((0.1, 0.12, 0.12), (0, -0.24, 0), bev=0.02),
        box((0.12, 0.06, 0.2), (0, -0.33, 0.04), AC, bev=0.02),
    ])
    build("Character_Arm", [
        box((0.07, 0.2, 0.08), (0, -0.1, 0), bev=0.02), box((0.08, 0.14, 0.09), (0, -0.26, 0.02), DK, bev=0.02),
        box((0.09, 0.07, 0.1), (0, -0.36, 0.03), AC, bev=0.02),
    ])


def species():
    """The nine species of the open world, three per biome. Base takes the state colour in the game,
    Accent is the hide of the biome, Glow its state colour. Mini bosses and bosses are the same
    models, drawn larger. Species with legs have a leg model; the others squash and stretch."""
    lean = math.radians(35)

    # ---- Cinder Flats ----
    # Cinder Mite: a low bug with an ember sac on its back. Comes in packs.
    build("Mite", [
        box((0.34, 0.16, 0.44), (0, 0.24, 0), bev=0.05), box((0.28, 0.1, 0.24), (0, 0.33, -0.1), AC, bev=0.03),
        box((0.22, 0.13, 0.16), (0, 0.22, 0.27), DK, bev=0.03), box((0.16, 0.04, 0.03), (0, 0.24, 0.355), GL, bev=0),
        cyl(0.025, 0.18, (0.08, 0.2, 0.42), axis="z", sides=4, slot=AC, end=0.0),
        cyl(0.025, 0.18, (-0.08, 0.2, 0.42), axis="z", sides=4, slot=AC, end=0.0),
        ball(0.09, (0, 0.38, -0.16), GL), box((0.12, 0.05, 0.3), (0.2, 0.26, 0), DK), box((0.12, 0.05, 0.3), (-0.2, 0.26, 0), DK),
        cyl(0.02, 0.12, (0.1, 0.4, 0.1), sides=4, slot=DK), cyl(0.02, 0.12, (-0.1, 0.4, 0.1), sides=4, slot=DK),
    ])
    build("Mite_Leg", [
        box((0.05, 0.16, 0.06), (0, -0.08, 0), DK), box((0.07, 0.05, 0.16), (0, -0.175, 0.04), AC, bev=0.015),
    ])
    # Slag Ram: a wedge of slag behind a ram plate with two horns.
    build("Ram", [
        cyl(0.5, 0.5, (0, 0.66, -0.08), sides=6, end=0.4), box((0.74, 0.2, 0.44), (0, 0.38, 0), DK, bev=0.04),
        box((0.96, 0.52, 0.24), (0, 0.62, 0.52), AC, bev=0.07), box((0.76, 0.16, 0.3), (0, 0.94, 0.46), AC, bev=0.05),
        cyl(0.1, 0.52, (0.36, 0.84, 0.82), axis="z", sides=5, slot=DK, end=0.0),
        cyl(0.1, 0.52, (-0.36, 0.84, 0.82), axis="z", sides=5, slot=DK, end=0.0),
        box((0.56, 0.06, 0.03), (0, 0.6, 0.645), GL, bev=0), box((0.62, 0.34, 0.5), (0, 0.98, -0.22), DK, bev=0.05),
        cyl(0.07, 0.42, (0.2, 1.3, -0.4), sides=6, slot=DK), cyl(0.07, 0.42, (-0.2, 1.3, -0.4), sides=6, slot=DK),
        cyl(0.075, 0.05, (0.2, 1.53, -0.4), sides=6, slot=GL), cyl(0.075, 0.05, (-0.2, 1.53, -0.4), sides=6, slot=GL),
        box((0.22, 0.4, 0.5), (0.52, 0.66, 0.1), AC, bev=0.04), box((0.22, 0.4, 0.5), (-0.52, 0.66, 0.1), AC, bev=0.04),
        box((0.2, 0.12, 0.05), (0, 0.8, 0.645), DK, bev=0),
    ])
    build("Ram_Leg", [
        box((0.2, 0.3, 0.26), (0, -0.15, 0), DK, bev=0.04), ball(0.1, (0, -0.02, 0), AC),
        box((0.24, 0.12, 0.4), (0, -0.34, 0.06), AC, bev=0.03),
    ])
    # Ember Spitter: a pot-bellied walker with a mortar on its back.
    build("Spitter", [
        cyl(0.36, 0.3, (0, 0.52, 0), sides=7, end=0.32), ball(0.3, (0, 0.78, -0.04)),
        cyl(0.15, 0.62, (0, 1.06, 0.2), sides=8, slot=DK, rot=(lean, 0, 0)),
        cyl(0.18, 0.09, (0, 1.3, 0.37), sides=8, slot=AC, rot=(lean, 0, 0)),
        cyl(0.11, 0.05, (0, 1.34, 0.4), sides=8, slot=GL, rot=(lean, 0, 0)),
        ball(0.22, (0, 0.64, -0.34), AC), ball(0.12, (0.2, 0.58, -0.36), GL), ball(0.1, (-0.2, 0.62, -0.34), GL),
        box((0.2, 0.26, 0.32), (0.38, 0.62, 0), AC, bev=0.04), box((0.2, 0.26, 0.32), (-0.38, 0.62, 0), AC, bev=0.04),
        box((0.26, 0.05, 0.03), (0, 0.72, 0.3), GL, bev=0), box((0.5, 0.1, 0.36), (0, 0.4, 0), DK),
    ])
    build("Spitter_Leg", [
        box((0.1, 0.3, 0.12), (0, -0.15, 0), DK), ball(0.07, (0, -0.02, 0), AC), box((0.16, 0.07, 0.24), (0, -0.345, 0.05), AC, bev=0.02),
    ])

    # ---- Frostline ----
    # Rime Stalker: lean and low, two ice blades forward, a fin on its back.
    build("Stalker", [
        box((0.3, 0.26, 0.62), (0, 0.62, 0), bev=0.07), box((0.24, 0.2, 0.28), (0, 0.72, 0.42), DK, bev=0.05),
        box((0.2, 0.04, 0.03), (0, 0.75, 0.565), GL, bev=0),
        cyl(0.06, 0.74, (0.3, 0.6, 0.4), axis="z", sides=4, slot=AC, end=0.0),
        cyl(0.06, 0.74, (-0.3, 0.6, 0.4), axis="z", sides=4, slot=AC, end=0.0),
        box((0.1, 0.12, 0.3), (0.26, 0.6, 0.02), DK), box((0.1, 0.12, 0.3), (-0.26, 0.6, 0.02), DK),
        box((0.04, 0.32, 0.42), (0, 0.9, -0.1), AC, bev=0.015), cyl(0.0, 0.5, (0, 0.62, -0.54), axis="z", sides=5, slot=DK, end=0.08),
        ball(0.07, (0, 0.66, -0.3), GL), box((0.34, 0.08, 0.3), (0, 0.48, -0.04), DK),
    ])
    build("Stalker_Leg", [
        box((0.07, 0.32, 0.09), (0, -0.16, 0), DK), ball(0.05, (0, -0.02, 0), AC), box((0.09, 0.05, 0.22), (0, -0.385, 0.06), AC, bev=0.015),
    ])
    # Frost Hulk: a wall of a thing with crags of ice for shoulders and fists like anvils.
    build("Hulk", [
        cyl(0.85, 0.8, (0, 1.02, 0), sides=6, end=0.7), box((1.34, 0.18, 0.92), (0, 0.6, 0), DK, bev=0.05),
        cyl(0.52, 0.56, (0.98, 1.5, 0), sides=5, slot=AC, end=0.16), cyl(0.52, 0.56, (-0.98, 1.5, 0), sides=5, slot=AC, end=0.16),
        cyl(0.17, 0.74, (0.3, 1.72, -0.32), sides=4, slot=AC, end=0.0), cyl(0.17, 0.74, (-0.3, 1.72, -0.32), sides=4, slot=AC, end=0.0),
        cyl(0.2, 0.9, (0, 1.86, -0.38), sides=4, slot=AC, end=0.0),
        box((0.34, 0.8, 0.38), (1.02, 0.86, 0.1), DK, bev=0.05), box((0.34, 0.8, 0.38), (-1.02, 0.86, 0.1), DK, bev=0.05),
        box((0.52, 0.46, 0.58), (1.02, 0.4, 0.2), bev=0.09), box((0.52, 0.46, 0.58), (-1.02, 0.4, 0.2), bev=0.09),
        box((0.44, 0.32, 0.42), (0, 1.56, 0.2), DK, bev=0.06), box((0.32, 0.07, 0.04), (0, 1.6, 0.415), GL, bev=0),
        box((0.34, 0.34, 0.06), (0, 1.02, 0.74), GL, bev=0), box((0.5, 0.5, 0.08), (0, 1.02, 0.7), DK, bev=0.03),
        box((0.9, 0.1, 0.1), (0, 1.36, 0.6), AC, bev=0.02),
    ])
    build("Hulk_Leg", [
        box((0.32, 0.36, 0.36), (0, -0.18, 0), DK, bev=0.05), ball(0.16, (0, -0.02, 0), AC),
        box((0.4, 0.18, 0.56), (0, -0.44, 0.08), AC, bev=0.04),
    ])
    # Shard Lancer: tall and thin, with a long lance tipped in crystal.
    build("Lancer", [
        cyl(0.22, 0.7, (0, 0.86, 0), sides=6, end=0.16), box((0.38, 0.14, 0.28), (0, 0.5, 0), DK, bev=0.03),
        box((0.52, 0.12, 0.3), (0, 1.22, 0), AC, bev=0.04), ball(0.16, (0, 1.44, 0.02)),
        box((0.22, 0.05, 0.04), (0, 1.45, 0.16), GL, bev=0),
        cyl(0.05, 1.5, (0.3, 1.14, 0.42), axis="z", sides=6, slot=DK), cyl(0.1, 0.42, (0.3, 1.14, 1.36), axis="z", sides=4, slot=GL, end=0.0),
        cyl(0.08, 0.1, (0.3, 1.14, 1.1), axis="z", sides=6, slot=AC), box((0.14, 0.2, 0.3), (0.3, 1.1, -0.1), DK),
        box((0.3, 0.42, 0.2), (0, 0.96, -0.24), DK, bev=0.03),
        cyl(0.07, 0.42, (0.1, 1.34, -0.26), sides=4, slot=AC, end=0.0), cyl(0.07, 0.5, (-0.1, 1.38, -0.26), sides=4, slot=AC, end=0.0),
        box((0.1, 0.36, 0.1), (-0.3, 1.0, 0.04), DK),
    ])
    build("Lancer_Leg", [
        box((0.07, 0.4, 0.08), (0, -0.2, 0), DK), ball(0.05, (0, -0.02, 0), AC), box((0.1, 0.05, 0.2), (0, -0.445, 0.04), AC, bev=0.015),
    ])

    # ---- Tar Marsh ----
    # Tar Leech: a segmented slug with a ring of teeth. No legs.
    build("Leech", [
        ball(0.3, (0, 0.28, 0.24)), cyl(0.27, 0.32, (0, 0.26, -0.04), axis="z", sides=7, slot=DK),
        cyl(0.23, 0.3, (0, 0.24, -0.34), axis="z", sides=7), cyl(0.0, 0.36, (0, 0.2, -0.66), axis="z", sides=6, slot=DK, end=0.18),
        torus(0.17, 0.05, (0, 0.28, 0.5), AC, axis="z"), ball(0.11, (0, 0.28, 0.47), GL),
        cyl(0.045, 0.24, (0, 0.56, 0.1), sides=4, slot=AC, end=0.0), cyl(0.04, 0.2, (0, 0.52, -0.18), sides=4, slot=AC, end=0.0),
        cyl(0.03, 0.16, (0, 0.46, -0.44), sides=4, slot=AC, end=0.0),
        ball(0.05, (0.2, 0.42, 0.34), GL), ball(0.05, (-0.2, 0.42, 0.34), GL),
    ])
    # Bloater: a swollen sac of tar on a small foot, covered in blisters.
    build("Bloater", [
        ball(0.64, (0, 0.78, 0), sub=2), torus(0.56, 0.07, (0, 0.74, 0), DK), cyl(0.42, 0.26, (0, 0.14, 0), sides=8, slot=DK, end=0.52),
        ball(0.2, (0.34, 1.2, 0.2), GL), ball(0.15, (-0.3, 1.26, -0.1), GL), ball(0.13, (0.05, 1.3, -0.34), GL),
        ball(0.16, (-0.46, 0.9, 0.36), GL), ball(0.12, (0.5, 0.82, -0.3), GL),
        box((0.34, 0.3, 0.1), (0, 0.84, 0.62), AC, bev=0.04), box((0.2, 0.05, 0.03), (0, 0.86, 0.68), GL, bev=0),
        cyl(0.07, 0.22, (0.3, 1.36, -0.16), sides=6, slot=DK), cyl(0.07, 0.22, (-0.2, 1.4, 0.2), sides=6, slot=DK),
        box((0.14, 0.3, 0.3), (0.62, 0.6, 0), AC, bev=0.04), box((0.14, 0.3, 0.3), (-0.62, 0.6, 0), AC, bev=0.04),
    ])
    # Ooze: a heap of tar with scrap stuck in it.
    build("Ooze", [
        rock(0.62, 0.75, 121, sub=2, rough=0.14), rock(0.32, 0.8, 122, sub=1, at=(0.46, 0.3), rough=0.16),
        rock(0.26, 0.8, 123, sub=1, at=(-0.42, -0.34), rough=0.16), rock(0.2, 0.9, 124, sub=1, at=(-0.3, 0.44), rough=0.16),
        ball(0.12, (0.15, 0.5, 0.44), GL), ball(0.09, (-0.17, 0.42, 0.48), GL),
        box((0.26, 0.08, 0.32), (-0.1, 0.66, -0.06), AC, bev=0.02, rot=(0.3, 0.2, 0.4)),
        box((0.22, 0.06, 0.2), (0.32, 0.42, -0.3), DK, bev=0.02, rot=(0.5, 0.1, 0.0)),
        cyl(0.03, 0.4, (-0.3, 0.6, 0.1), sides=5, slot=DK, rot=(0.4, 0.3, 0.0)),
    ])


# ---------------------------------------------------------------- scenery

def scenery():
    build("Rock_A", [rock(1.0, 0.7, 1), rock(0.5, 0.8, 5, at=(0.8, 0.4)), rock(0.35, 0.7, 6, at=(-0.7, -0.6))])
    build("Rock_B", [rock(0.6, 0.9, 2, sub=1), rock(0.25, 0.8, 7, sub=1, at=(0.5, -0.2))])
    build("Cliff", [rock(3.0, 1.3, 3), rock(1.6, 1.1, 8, at=(2.4, 1.0)), rock(1.2, 0.9, 9, at=(-2.2, -1.2))])
    build("Pebbles", [rock(0.16, 0.7, 30 + i, sub=1, at=(math.cos(i * 1.9) * 0.5, math.sin(i * 1.9) * 0.5)) for i in range(5)])
    build("Shards", [cyl(0.07, 0.3 + 0.12 * (i % 3), (math.cos(i * 2.2) * 0.25, 0.14, math.sin(i * 2.2) * 0.25), sides=4, slot=AC, end=0.0) for i in range(5)])
    tuft = []
    for i in range(7):
        a = i * 0.9
        tuft.append(box((0.03, 0.3 + 0.06 * (i % 3), 0.008), (math.cos(a) * 0.12, 0.16, math.sin(a) * 0.12), AC, bev=0, rot=(0.25, 0, a)))
    build("Tuft", tuft)

    # A small bush. The machine flattens these when it drives over them.
    build("Bush", [
        rock(0.32, 0.8, 70, slot=AC, sub=1, rough=0.25), rock(0.24, 0.8, 71, slot=AC, sub=1, at=(0.26, 0.1), rough=0.25),
        rock(0.2, 0.8, 72, slot=AC, sub=1, at=(-0.2, -0.18), rough=0.25), cyl(0.04, 0.2, (0, 0.1, 0), sides=5, slot=DK),
    ])

    # A machine that stopped when the Pulse was split.
    build("Husk", [
        box((1.5, 0.45, 2.6), (0, 0.3, 0), bev=0.06), box((1.1, 0.5, 1.0), (0, 0.76, -0.4), bev=0.05),
        box((0.9, 0.25, 0.05), (0, 0.8, 0.11), DK), box((1.3, 0.3, 0.3), (0, 0.3, 1.38), DK),
        cyl(0.4, 0.3, (-0.95, 0.35, -0.9), axis="x", sides=10, slot=DK), cyl(0.4, 0.3, (0.95, 0.35, -0.9), axis="x", sides=10, slot=DK),
        cyl(0.4, 0.3, (-0.95, 0.3, 0.9), axis="x", sides=10, slot=DK), cyl(0.4, 0.3, (1.4, 0.15, 1.0), sides=10, slot=DK),
        cyl(0.22, 0.32, (1.4, 0.15, 1.0), sides=8, slot=AC), box((0.3, 0.9, 0.3), (0.3, 1.2, 0.6), AC, rot=(0.25, 0, 0.1)),
        box((0.7, 0.04, 0.9), (-0.3, 0.56, 0.5), AC, rot=(0.12, 0, 0.3)), cyl(0.05, 0.9, (-0.5, 1.2, -0.7), sides=6, slot=DK),
        box((0.5, 0.3, 0.4), (0.2, 0.65, 0.6), DK), rock(0.4, 0.6, 12, at=(-1.2, 0.6)), rock(0.3, 0.6, 13, at=(1.0, -1.5)),
    ])
    pylon = [cyl(0.5, 0.2, (0, 0.1, 0), sides=6, slot=DK), cyl(0.12, 1.0, (0, 5.2, 0), sides=6), ball(0.16, (0, 5.85, 0), GL),
             box((1.9, 0.1, 0.1), (0, 4.4, 0), DK), box((1.3, 0.1, 0.1), (0, 3.5, 0), DK)]
    for x, z in ((0.35, 0.35), (-0.35, 0.35), (0.35, -0.35), (-0.35, -0.35)):
        pylon.append(cyl(0.05, 4.8, (x * 0.6, 2.5, z * 0.6), sides=5, slot=BS))
    for y in (0.9, 1.9, 2.9, 3.9):
        pylon += [box((0.5, 0.05, 0.05), (0, y, 0.2), DK, bev=0), box((0.5, 0.05, 0.05), (0, y, -0.2), DK, bev=0),
                  box((0.05, 0.05, 0.5), (0.2, y, 0), DK, bev=0), box((0.05, 0.05, 0.5), (-0.2, y, 0), DK, bev=0)]
    for x in (-0.9, 0.9):
        pylon += [cyl(0.06, 0.2, (x, 4.25, 0), sides=6, slot=AC), cyl(0.06, 0.2, (x * 0.65, 3.35, 0), sides=6, slot=AC)]
    build("Pylon", pylon)

    # Cinder Flats
    tree = [cyl(0.24, 2.6, (0, 1.3, 0), sides=6, slot=DK, end=0.1), cyl(0.36, 0.3, (0, 0.15, 0), sides=6, slot=DK, end=0.24)]
    for i, (h, lean, length) in enumerate(((1.4, 0.9, 1.1), (1.9, -0.8, 0.9), (2.3, 0.7, 0.7), (1.1, -1.0, 0.6))):
        a = i * 1.7
        tree.append(cyl(0.06, length, (math.cos(a) * 0.3, h + 0.2, math.sin(a) * 0.3), sides=5, slot=DK, end=0.02, rot=(lean, 0, a)))
    tree += [ball(0.05, (0.2, 2.2, 0.1), GL), ball(0.04, (-0.15, 1.5, -0.2), GL)]
    build("CharredTree", tree)
    vent = [cyl(0.55, 0.12, (0, 0.06, 0), sides=8, slot=GL), cyl(0.3, 0.5, (0, 0.2, 0), sides=6, slot=GL, end=0.0)]
    vent += around(7, 0.75, lambda x, z, a: rock(0.32, 0.8, 40 + int(a * 10), sub=1, at=(x, z)))
    build("EmberVent", vent)
    crack = []
    for i in range(6):
        crack.append(box((0.16, 0.02, 0.9), (math.sin(i * 1.3) * 0.25, 0.012, i * 0.7 - 1.75), GL, bev=0, rot=(0, 0, math.sin(i * 2.1) * 0.5)))
        crack.append(box((0.4, 0.016, 1.0), (math.sin(i * 1.3) * 0.25, 0.006, i * 0.7 - 1.75), DK, bev=0, rot=(0, 0, math.sin(i * 2.1) * 0.5)))
    build("LavaCrack", crack)

    # Frostline
    shards = [rock(0.5, 0.5, 50, sub=1)]
    for i in range(6):
        a = i * 1.1
        shards.append(cyl(0.2 - i * 0.015, 1.5 - i * 0.16, (math.cos(a) * 0.35, 0.55, math.sin(a) * 0.35), sides=5, slot=AC, end=0.0,
                          rot=(math.cos(a) * 0.3, math.sin(a) * 0.3, a)))
    shards.append(cyl(0.12, 1.1, (0, 0.6, 0), sides=5, slot=GL, end=0.0))
    build("IceShards", shards)
    # Packed hard and solid: it blocks the machine, so it is built around its origin.
    build("SnowDrift", [rock(1.4, 0.34, 51, sub=2, rough=0.12, at=(-0.3, -0.12)), rock(0.8, 0.34, 52, sub=2, at=(0.8, 0.38), rough=0.12)])
    pine = [cyl(0.14, 1.0, (0, 0.5, 0), sides=6, slot=DK)]
    for i, (y, r) in enumerate(((0.9, 0.9), (1.5, 0.7), (2.05, 0.5), (2.55, 0.3))):
        pine += [cyl(r, 0.8, (0, y + 0.2, 0), sides=7, slot=AC, end=0.05), cyl(r * 0.8, 0.5, (0, y + 0.42, 0), sides=7, end=0.02)]
    build("FrozenPine", pine)
    build("IceBoulder", [rock(0.9, 0.9, 53, slot=AC, sub=1), rock(0.5, 0.4, 54, at=(0.5, 0.6), rough=0.1),
                         cyl(0.12, 0.7, (-0.5, 0.4, 0.3), sides=4, slot=GL, end=0.0)])

    # Tar Marsh
    build("TarPool", [plate(1.5, 9, 60, DK, height=0.012), plate(1.72, 9, 60, GL, height=0.006),
                      ball(0.14, (0.4, 0.0, 0.2), DK), ball(0.09, (-0.5, 0.0, -0.3), DK), ball(0.06, (0.1, 0.0, -0.6), GL)])
    derrick = [box((1.8, 0.2, 1.8), (0, 0.1, 0), DK, bev=0.04), cyl(0.3, 1.0, (0.5, 0.7, -0.5), sides=10, slot=AC),
               cyl(0.32, 0.05, (0.5, 0.5, -0.5), sides=10, slot=DK), cyl(0.32, 0.05, (0.5, 0.95, -0.5), sides=10, slot=DK),
               box((0.2, 0.2, 2.6), (0, 3.3, 0.2), bev=0.03), box((0.5, 0.6, 0.4), (0, 3.2, 1.5), DK, bev=0.06),
               box((0.3, 0.5, 0.5), (0, 3.2, -1.1), AC), cyl(0.04, 2.6, (0, 1.9, 1.5), sides=5, slot=DK),
               cyl(0.14, 0.3, (0, 0.35, 1.5), sides=8, slot=DK), ball(0.08, (0, 3.7, 0.2), GL)]
    for x, z in ((0.6, 0.6), (-0.6, 0.6), (0.6, -0.6), (-0.6, -0.6)):
        derrick.append(cyl(0.06, 3.3, (x * 0.55, 1.75, z * 0.55 + 0.2), sides=5))
    for y in (1.0, 2.0, 2.9):
        derrick += [box((0.8, 0.05, 0.05), (0, y, 0.55), DK, bev=0), box((0.8, 0.05, 0.05), (0, y, -0.15), DK, bev=0)]
    build("Derrick", derrick)
    pipes = [box((1.2, 0.12, 0.8), (0, 0.06, 0), DK)]
    for i, (x, z, h) in enumerate(((-0.35, 0.1, 1.6), (0.05, -0.15, 2.3), (0.4, 0.15, 1.1))):
        pipes += [cyl(0.14, h, (x, h / 2, z), sides=8, slot=DK), cyl(0.17, 0.08, (x, h, z), sides=8, slot=AC),
                  cyl(0.16, 0.05, (x, h * 0.45, z), sides=8, slot=AC)]
    pipes += [cyl(0.07, 0.8, (0.05, 0.9, 0.3), axis="x", sides=6, slot=DK), ball(0.09, (0.4, 1.12, 0.15), GL)]
    build("PipeCluster", pipes)
    reeds = []
    for i in range(9):
        a = i * 1.4
        reeds.append(cyl(0.03, 0.9 + 0.2 * (i % 4), (math.cos(a) * 0.3, 0.5, math.sin(a) * 0.3), sides=4, slot=AC, end=0.012,
                         rot=(math.cos(a) * 0.12, math.sin(a) * 0.12, a)))
        if i % 3 == 0:
            reeds.append(cyl(0.05, 0.22, (math.cos(a) * 0.3, 1.05, math.sin(a) * 0.3), sides=6, slot=DK))
    build("Reeds", reeds)

    # Dungeon wall pillar and the arch over a dungeon entrance.
    build("Pillar", [
        cyl(0.9, 0.3, (0, 0.15, 0), sides=6, slot=DK), cyl(0.7, 3.0, (0, 1.8, 0), sides=6, end=0.55),
        cyl(0.74, 0.12, (0, 1.0, 0), sides=6, slot=DK), cyl(0.63, 0.12, (0, 2.4, 0), sides=6, slot=DK),
        cyl(0.75, 0.25, (0, 3.42, 0), sides=6, slot=AC), cyl(0.4, 0.25, (0, 3.67, 0), sides=8, slot=DK, end=0.58),
        ball(0.3, (0, 3.86, 0), GL), box((0.2, 0.6, 0.08), (0, 1.7, 0.6), AC), box((0.12, 0.1, 0.04), (0, 1.9, 0.65), GL, bev=0),
    ])
    # One cell of dungeon wall, two metres square. Low enough that the machine is not lost behind it.
    build("DungeonWall", [
        box((2.0, 0.25, 2.0), (0, 0.125, 0), DK, bev=0), box((2.0, 1.55, 2.0), (0, 1.025, 0), bev=0.06),
        box((1.6, 0.1, 1.6), (0, 1.84, 0), AC, bev=0.03), box((0.34, 0.07, 0.34), (0, 1.92, 0), GL, bev=0),
    ])
    # One cell of the stands around an arena: two steps, the lower one toward the arena.
    build("ArenaStand", [
        box((2.0, 1.0, 1.0), (0, 0.5, 0.5), bev=0.04), box((2.0, 1.8, 1.0), (0, 0.9, -0.5), bev=0.04),
        box((2.0, 0.1, 0.08), (0, 1.25, 0.96), DK, bev=0), box((0.08, 0.3, 0.08), (0.9, 1.12, 0.96), DK, bev=0),
        box((0.08, 0.3, 0.08), (-0.9, 1.12, 0.96), DK, bev=0), box((2.0, 0.08, 0.05), (0, 0.72, 1.0), AC, bev=0),
        box((0.16, 0.5, 0.16), (0.9, 2.05, -0.9), DK, bev=0.02), ball(0.14, (0.9, 2.38, -0.9), GL),
    ])

    # Kiln's Keep: what stands behind the entrance to the Warden's arena. A curtain wall with a gate
    # between two towers, and the keep itself behind, with chimneys that glow. Its front faces south.
    keep = [
        box((9.5, 7.0, 3.0), (-7.25, 3.5, 0), bev=0.12), box((9.5, 7.0, 3.0), (7.25, 3.5, 0), bev=0.12),
        box((5.0, 2.4, 3.0), (0, 5.8, 0), bev=0.1), box((5.0, 4.6, 0.4), (0, 2.3, 0.9), DK, bev=0),
        box((5.4, 0.5, 3.3), (0, 4.75, 0), AC, bev=0.08),
        box((12.0, 10.5, 7.0), (0, 5.25, 6.0), bev=0.2), box((12.6, 0.8, 7.6), (0, 10.9, 6.0), AC, bev=0.1),
        cyl(0.9, 4.2, (3.2, 12.9, 6.6), sides=8, slot=DK), cyl(0.9, 4.2, (-3.2, 12.9, 6.6), sides=8, slot=DK),
        cyl(0.95, 0.3, (3.2, 15.1, 6.6), sides=8, slot=GL), cyl(0.95, 0.3, (-3.2, 15.1, 6.6), sides=8, slot=GL),
        cyl(1.2, 5.6, (0, 13.6, 7.4), sides=8, slot=DK), cyl(1.25, 0.34, (0, 16.5, 7.4), sides=8, slot=GL),
        box((1.4, 2.6, 0.2), (0, 7.4, 2.45), GL, bev=0), box((1.0, 1.8, 0.2), (3.6, 7.0, 2.45), GL, bev=0),
        box((1.0, 1.8, 0.2), (-3.6, 7.0, 2.45), GL, bev=0),
    ]
    for x in (-12.5, 12.5):
        keep += [box((5.0, 11.0, 5.0), (x, 5.5, 0), bev=0.15), box((5.7, 0.8, 5.7), (x, 11.3, 0), AC, bev=0.1),
                 box((0.9, 1.9, 0.2), (x, 7.6, -2.55), GL, bev=0), box((5.4, 0.5, 5.4), (x, 0.25, 0), DK, bev=0)]
        for mx, mz in ((-2.2, -2.2), (2.2, -2.2), (-2.2, 2.2), (2.2, 2.2), (0, -2.2), (0, 2.2)):
            keep.append(box((1.0, 1.0, 1.0), (x + mx, 12.2, mz), AC, bev=0.06))
    for x in (-3.6, 3.6):
        keep += [box((2.6, 9.0, 3.6), (x, 4.5, -0.4), bev=0.12), box((3.0, 0.6, 4.0), (x, 9.3, -0.4), AC, bev=0.08),
                 ball(0.42, (x * 0.8, 5.3, -2.3), GL)]
    for i in range(9):
        x = -9.6 + i * 2.4
        if abs(x) > 1.5:
            keep.append(box((1.1, 0.9, 0.7), (x, 7.45, -1.15), AC, bev=0.05))
    # More of it, most of all on top, because the camera looks down on it.
    # The gate: a portcullis, a furnace window over it, fire baskets on the towers beside it, steps.
    for x in (-2.0, -1.2, -0.4, 0.4, 1.2, 2.0):
        keep.append(cyl(0.07, 4.5, (x, 2.25, -0.75), sides=6, slot=DK))
    keep += [box((4.6, 0.14, 0.14), (0, 1.5, -0.75), DK, bev=0), box((4.6, 0.14, 0.14), (0, 3.1, -0.75), DK, bev=0),
             box((2.4, 1.2, 0.3), (0, 6.0, -1.55), DK, bev=0.05), box((2.0, 0.85, 0.12), (0, 6.0, -1.7), GL, bev=0),
             box((5.8, 0.25, 1.3), (0, 0.125, -2.3), DK, bev=0.02), box((6.6, 0.12, 2.0), (0, 0.06, -3.0), bev=0.01)]
    for x in (-0.7, -0.35, 0.0, 0.35, 0.7):
        keep.append(box((0.08, 0.95, 0.1), (x, 6.0, -1.78), DK, bev=0))
    for x in (-3.6, 3.6):
        keep += [box((0.5, 0.12, 0.5), (x, 3.0, -2.5), DK, bev=0.02), cyl(0.2, 0.3, (x, 3.2, -2.5), sides=6, slot=DK, end=0.34),
                 ball(0.24, (x, 3.45, -2.5), GL), box((0.9, 3.2, 0.08), (x, 7.0, -2.26), AC, bev=0),
                 box((0.42, 0.42, 0.07), (x, 7.7, -2.32), GL, bev=0, rot=(0, math.radians(45), 0)),
                 box((1.7, 0.3, 1.7), (x, 9.7, -0.4), DK, bev=0.03), cyl(0.42, 0.16, (x, 9.93, -0.4), sides=6, slot=GL)]
        for mx, mz in ((-1.2, -1.9), (1.2, -1.9), (-1.2, 1.1), (1.2, 1.1)):
            keep.append(box((0.6, 0.7, 0.6), (x + mx, 9.9, mz), AC, bev=0.05))
    # The curtain wall: buttresses and slits in front, a walk and a second row of merlons on top.
    for side in (-1, 1):
        keep += [box((9.5, 0.2, 2.2), (side * 7.25, 7.1, 0.2), DK, bev=0)]
        for x in (5.4, 7.6, 9.6):
            keep.append(cyl(0.8, 5.2, (side * x, 2.6, -1.7), sides=4, end=0.3))
        for x in (6.5, 8.6):
            keep.append(box((0.18, 1.4, 0.1), (side * x, 4.8, -1.53), DK, bev=0))
        for i in range(4):
            keep.append(box((1.1, 0.9, 0.7), (side * (5.0 + i * 1.9), 7.45, 1.15), AC, bev=0.05))
    # The corner towers: a floor, a fire basket in the middle, a spike on every corner.
    for x in (-12.5, 12.5):
        keep += [box((4.3, 0.2, 4.3), (x, 11.78, 0), DK, bev=0), cyl(0.5, 0.5, (x, 12.1, 0), sides=6, slot=DK, end=0.75),
                 cyl(0.62, 0.16, (x, 12.42, 0), sides=6, slot=GL), box((5.3, 0.4, 5.3), (x, 4.0, 0), AC, bev=0.06),
                 box((0.2, 1.5, 0.1), (x - 1.3, 5.6, -2.53), DK, bev=0), box((0.2, 1.5, 0.1), (x + 1.3, 5.6, -2.53), DK, bev=0)]
        for mx, mz in ((-2.2, -2.2), (2.2, -2.2), (-2.2, 2.2), (2.2, 2.2)):
            keep.append(cyl(0.3, 1.3, (x + mx, 13.3, mz), sides=4, slot=DK, end=0.02))
    # The keep itself: turrets on its corners, a parapet, a grate over the fire inside, pipes, bands
    # on the chimneys, slits in its front.
    for x in (-6.0, 6.0):
        for z in (2.5, 9.5):
            keep += [cyl(0.95, 12.6, (x, 6.3, z), sides=6), cyl(1.25, 2.0, (x, 13.6, z), sides=6, slot=AC, end=0.05),
                     cyl(1.05, 0.3, (x, 9.0, z), sides=6, slot=DK)]
        keep.append(box((0.14, 1.1, 0.5), (x * 1.16, 9.6, 2.5), GL, bev=0))
    for i in range(6):
        x = -4.5 + i * 1.8
        keep += [box((0.9, 0.7, 0.5), (x, 11.65, 2.45), AC, bev=0.05), box((0.9, 0.7, 0.5), (x, 11.65, 9.55), AC, bev=0.05)]
    for i in range(3):
        z = 4.2 + i * 1.8
        keep += [box((0.5, 0.7, 0.9), (-6.05, 11.65, z), AC, bev=0.05), box((0.5, 0.7, 0.9), (6.05, 11.65, z), AC, bev=0.05)]
    keep += [box((4.8, 0.5, 2.4), (0, 11.5, 4.2), DK, bev=0.06), box((4.2, 0.2, 1.8), (0, 11.72, 4.2), GL, bev=0),
             cyl(0.26, 4.6, (-4.9, 11.55, 6.2), axis="z", sides=8, slot=DK), cyl(0.26, 4.6, (4.9, 11.55, 6.2), axis="z", sides=8, slot=DK),
             cyl(0.2, 1.7, (-4.05, 11.55, 8.3), axis="x", sides=8, slot=DK), cyl(0.2, 1.7, (4.05, 11.55, 8.3), axis="x", sides=8, slot=DK),
             box((1.1, 0.3, 1.1), (-2.2, 11.4, 8.4), AC, bev=0.04), box((1.1, 0.3, 1.1), (2.2, 11.4, 8.4), AC, bev=0.04),
             cyl(1.02, 0.28, (3.2, 12.0, 6.6), sides=8, slot=AC), cyl(1.02, 0.28, (-3.2, 12.0, 6.6), sides=8, slot=AC),
             cyl(1.02, 0.28, (3.2, 13.8, 6.6), sides=8, slot=AC), cyl(1.02, 0.28, (-3.2, 13.8, 6.6), sides=8, slot=AC),
             cyl(1.34, 0.32, (0, 12.4, 7.4), sides=8, slot=AC), cyl(1.34, 0.32, (0, 14.8, 7.4), sides=8, slot=AC)]
    for x in (-2.0, -1.2, -0.4, 0.4, 1.2, 2.0):
        keep.append(box((0.12, 0.2, 2.0), (x, 11.86, 4.2), DK, bev=0))
    for x in (-4.8, -2.3, 2.3, 4.8):
        keep += [box((0.45, 1.3, 0.12), (x, 9.0, 2.44), GL, bev=0), box((0.5, 10.5, 0.3), (x * 1.0 + (0.9 if x > 0 else -0.9), 5.25, 2.42), AC, bev=0.04)]
    # A furnace house on either side of the keep, with its own stack, piped into it.
    for side in (-1, 1):
        keep += [box((3.4, 5.0, 4.6), (side * 8.3, 2.5, 6.4), bev=0.12), box((3.7, 0.4, 4.9), (side * 8.3, 5.2, 6.4), AC, bev=0.06),
                 box((0.15, 1.3, 2.4), (side * 10.03, 2.3, 6.4), GL, bev=0), cyl(0.5, 3.6, (side * 8.9, 7.2, 7.6), sides=8, slot=DK),
                 cyl(0.56, 0.2, (side * 8.9, 9.1, 7.6), sides=8, slot=GL), cyl(0.3, 1.6, (side * 7.0, 6.2, 5.4), axis="x", sides=8, slot=DK),
                 cyl(0.3, 1.2, (side * 7.7, 5.9, 5.4), sides=8, slot=DK)]
        for z in (5.6, 6.4, 7.2):
            keep.append(box((0.18, 1.4, 0.1), (side * 10.07, 2.3, z), DK, bev=0))
    # Slag heaped against the foot of the wall.
    keep += [rock(0.8, 0.6, 31, DK, at=(-9.2, -2.3)), rock(0.5, 0.7, 32, DK, at=(-8.0, -2.6)), rock(0.9, 0.55, 33, DK, at=(8.6, -2.4)),
             rock(0.45, 0.7, 34, DK, at=(10.2, -3.0)), rock(0.6, 0.6, 35, DK, at=(-11.6, -3.2))]
    build("KilnsKeep", keep)
    # One leaf of the gate the Warden comes through: five metres wide, to cover half the opening.
    build("BossGate", [
        box((5.0, 3.4, 0.6), (0, 1.7, 0), DK, bev=0.08), box((5.0, 0.3, 0.7), (0, 0.6, 0), AC, bev=0.03),
        box((5.0, 0.3, 0.7), (0, 2.8, 0), AC, bev=0.03), box((0.3, 3.4, 0.7), (-2.2, 1.7, 0), AC, bev=0.03),
        box((0.3, 3.4, 0.7), (2.2, 1.7, 0), AC, bev=0.03), box((0.5, 0.5, 0.75), (0, 1.7, 0), GL, bev=0.05),
    ])

    gate = [box((7.2, 0.6, 0.9), (0, 4.1, 0), bev=0.06), box((1.6, 0.9, 1.0), (0, 4.1, 0), DK, bev=0.08),
            ball(0.3, (0, 4.1, 0.5), GL), ball(0.3, (0, 4.1, -0.5), GL), box((7.4, 0.12, 1.0), (0, 4.46, 0), AC)]
    for x in (-3.0, 3.0):
        gate += [box((0.8, 3.6, 0.8), (x, 2.0, 0), bev=0.06), box((1.1, 0.4, 1.1), (x, 0.2, 0), DK, bev=0.06),
                 box((1.0, 0.25, 1.0), (x, 3.7, 0), AC), box((0.5, 1.6, 0.5), (x * 1.24, 0.8, 0), DK, bev=0.05),
                 box((0.2, 0.5, 0.2), (x * 0.55, 3.55, 0), GL), box((0.86, 0.1, 0.86), (x, 1.4, 0), DK),
                 box((0.86, 0.1, 0.86), (x, 2.6, 0), DK), box((0.3, 0.9, 0.06), (x, 2.0, 0.43), AC, bev=0)]
    build("Gate", gate)

    build("PartCrate", [
        box((0.5, 0.34, 0.36), (0, 0, 0), DK, bev=0.04), box((0.52, 0.06, 0.38), (0, 0.1, 0), AC, bev=0.01),
        box((0.52, 0.06, 0.38), (0, -0.1, 0), AC, bev=0.01), box((0.1, 0.36, 0.38), (-0.17, 0, 0), bev=0.01),
        box((0.1, 0.36, 0.38), (0.17, 0, 0), bev=0.01), box((0.14, 0.1, 0.04), (0, 0, 0.19), GL, bev=0),
        box((0.14, 0.1, 0.04), (0, 0, -0.19), GL, bev=0), box((0.22, 0.04, 0.06), (0, 0.2, 0), DK, bev=0.01),
    ])


# ---------------------------------------------------------------- the lobby (the Roundhouse)

def tuning_room():
    # Everything in the room behind the bought door. Each piece faces +z, into the room.
    # The floor: a slab of plates with grates, cable channels and warning marks at the door (-z).
    floor = [box((10.8, 0.1, 9.4), (0, -0.03, 0), DK, bev=0)]
    for ix in range(4):
        for iz in range(3):
            floor.append(box((2.5, 0.04, 2.95), (-3.9 + ix * 2.6, 0.035, -3.05 + iz * 3.05), BS if (ix + iz) % 2 else AC, bev=0.01))
    floor += [box((3.9, 0.05, 2.9), (0, 0.06, 1.7), DK, bev=0.02), box((3.5, 0.03, 2.5), (0, 0.09, 1.7), AC, bev=0.01)]
    for i in range(9):
        floor += [box((0.08, 0.05, 2.2), (-4.4 + i * 0.16, 0.07, -1.6), DK, bev=0), box((0.08, 0.05, 2.2), (3.1 + i * 0.16, 0.07, 3.0), DK, bev=0)]
    floor += [box((0.16, 0.05, 4.2), (-2.6, 0.075, 1.0), DK, bev=0), box((2.4, 0.05, 0.16), (-1.4, 0.075, 3.0), DK, bev=0),
              box((0.12, 0.05, 3.0), (2.4, 0.075, -1.2), DK, bev=0), box((1.2, 0.05, 0.12), (3.0, 0.075, -2.7), DK, bev=0)]
    for i in range(10):
        floor.append(box((0.5, 0.045, 0.3), (-2.25 + i * 0.5, 0.07, -4.3), GL if i % 2 == 0 else DK, bev=0, rot=(0, 0, 0.5)))
    build("TR_Floor", floor)

    wall = [box((5.3, 4.6, 0.4), (0, 2.3, 0), bev=0.04), box((5.4, 0.5, 0.6), (0, 0.25, 0), DK, bev=0.05),
            box((5.4, 0.3, 0.6), (0, 4.6, 0), AC, bev=0.04), cyl(0.1, 5.3, (0, 3.5, 0.3), axis="x", sides=8, slot=DK),
            cyl(0.07, 5.3, (0, 3.78, 0.3), axis="x", sides=8, slot=AC), box((1.7, 0.12, 0.06), (0, 4.1, 0.23), GL, bev=0),
            box((1.5, 1.1, 0.08), (-1.6, 2.2, 0.23), AC, bev=0.02), box((1.5, 1.1, 0.08), (1.6, 2.2, 0.23), AC, bev=0.02),
            cyl(0.14, 0.16, (-0.7, 3.5, 0.34), axis="x", sides=8, slot=AC), cyl(0.14, 0.16, (1.9, 3.78, 0.34), axis="x", sides=8, slot=DK),
            box((0.3, 0.5, 0.16), (2.3, 1.2, 0.27), DK, bev=0.03), box((0.2, 0.08, 0.04), (2.3, 1.3, 0.36), GL, bev=0)]
    for x in (-2.55, -0.85, 0.85, 2.55):
        wall.append(box((0.16, 4.3, 0.5), (x, 2.3, 0), DK, bev=0.02))
    build("TR_Wall", wall)
    build("TR_Corner", [box((0.7, 4.9, 0.7), (0, 2.45, 0), DK, bev=0.05), box((0.86, 0.3, 0.86), (0, 4.9, 0), AC, bev=0.04),
                        box((0.86, 0.4, 0.86), (0, 0.2, 0), AC, bev=0.04), box((0.1, 2.6, 0.1), (0, 2.6, 0.38), GL, bev=0)])
    # The side toward the camera is only a kerb with posts, so the room can be looked into.
    kerb = [box((5.3, 0.5, 0.4), (0, 0.25, 0), DK, bev=0.04), box((5.4, 0.1, 0.5), (0, 0.52, 0), AC, bev=0.02)]
    for x in (-2.4, -0.8, 0.8, 2.4):
        kerb += [box((0.14, 0.7, 0.14), (x, 0.9, 0), DK, bev=0.02), ball(0.07, (x, 1.3, 0), GL)]
    kerb.append(cyl(0.035, 5.3, (0, 1.1, 0), axis="x", sides=6, slot=AC))
    build("TR_Kerb", kerb)

    # The press: where a part is raised to a higher level. Its head, which moves, is a model of its own.
    press = [box((3.2, 0.5, 2.1), (0, 0.25, 0), DK, bev=0.08), box((3.3, 0.12, 2.2), (0, 0.06, 0), AC, bev=0.03),
             box((1.7, 0.12, 1.3), (0, 0.56, 0.1), AC, bev=0.03), box((1.3, 0.05, 0.9), (0, 0.64, 0.1), DK, bev=0),
             cyl(0.17, 3.2, (1.3, 2.1, -0.55), sides=10, slot=DK), cyl(0.17, 3.2, (-1.3, 2.1, -0.55), sides=10, slot=DK),
             cyl(0.24, 0.3, (1.3, 0.65, -0.55), sides=10, slot=AC), cyl(0.24, 0.3, (-1.3, 0.65, -0.55), sides=10, slot=AC),
             box((3.2, 0.45, 0.6), (0, 3.75, -0.55), bev=0.06), box((3.3, 0.1, 0.7), (0, 4.0, -0.55), AC, bev=0.03),
             box((0.5, 0.7, 0.4), (1.45, 0.9, 0.75), bev=0.04), box((0.36, 0.3, 0.05), (1.45, 1.0, 0.96), GL, bev=0),
             box((0.1, 0.1, 0.06), (1.33, 0.7, 0.96), GL, bev=0), box((0.1, 0.1, 0.06), (1.57, 0.7, 0.96), AC, bev=0),
             cyl(0.05, 1.4, (-1.5, 0.75, 0.6), sides=6, slot=DK, rot=(0.5, 0, 0)), cyl(0.05, 2.2, (-0.6, 3.9, -0.9), axis="x", sides=6, slot=DK),
             box((0.5, 0.5, 0.5), (-1.3, 3.75, -0.55), DK, bev=0.05), box((0.5, 0.5, 0.5), (1.3, 3.75, -0.55), DK, bev=0.05),
             cyl(0.3, 0.6, (0, 4.25, -0.55), sides=8, slot=DK), box((0.9, 0.06, 0.5), (0, 0.5, 1.2), AC, bev=0.01)]
    for x, z in ((-0.72, -0.4), (0.72, -0.4), (-0.72, 0.6), (0.72, 0.6)):
        press += [cyl(0.07, 0.5, (x, 0.85, z), sides=6, slot=DK, end=0.03), ball(0.06, (x, 1.14, z), GL)]
    build("TR_Press", press)
    build("TR_PressHead", [
        cyl(0.2, 1.1, (0, -0.55, 0), sides=10, slot=DK), cyl(0.26, 0.14, (0, -0.07, 0), sides=10, slot=AC),
        box((1.1, 0.34, 0.9), (0, -1.25, 0.3), AC, bev=0.05), box((0.8, 0.06, 0.6), (0, -1.45, 0.3), GL, bev=0),
        box((2.95, 0.16, 0.22), (0, -1.2, 0), bev=0.03), box((0.42, 0.34, 0.42), (1.3, -1.2, 0), bev=0.04),
        box((0.42, 0.34, 0.42), (-1.3, -1.2, 0), bev=0.04), cyl(0.06, 0.5, (0.4, -1.0, 0.3), sides=6, slot=DK),
        cyl(0.06, 0.5, (-0.4, -1.0, 0.3), sides=6, slot=DK),
    ])

    # The Tuner: a squat machine with a cluster of lenses for a head. Its two arms are a model of their own.
    build("NPC_Tuner", [
        cyl(0.55, 0.25, (0, 0.125, 0), sides=10, slot=DK), cyl(0.42, 0.2, (0, 0.35, 0), sides=10, slot=AC),
        box((0.95, 0.8, 0.85), (0, 0.85, 0), bev=0.08), box((0.7, 0.3, 0.06), (0, 0.9, 0.44), DK, bev=0.02),
        box((0.14, 0.1, 0.04), (-0.2, 0.92, 0.48), GL, bev=0), box((0.14, 0.1, 0.04), (0.0, 0.92, 0.48), GL, bev=0),
        box((0.14, 0.1, 0.04), (0.2, 0.92, 0.48), AC, bev=0), cyl(0.2, 1.1, (0, 1.05, 0), axis="x", sides=8, slot=DK),
        box((0.62, 0.42, 0.5), (0, 1.5, 0.05), AC, bev=0.06), cyl(0.13, 0.16, (-0.14, 1.56, 0.34), axis="z", sides=10, slot=DK),
        cyl(0.09, 0.05, (-0.14, 1.56, 0.44), axis="z", sides=10, slot=GL), cyl(0.08, 0.14, (0.14, 1.6, 0.34), axis="z", sides=8, slot=DK),
        cyl(0.055, 0.05, (0.14, 1.6, 0.43), axis="z", sides=8, slot=GL), cyl(0.05, 0.12, (0.16, 1.42, 0.33), axis="z", sides=8, slot=DK),
        cyl(0.03, 0.05, (0.16, 1.42, 0.41), axis="z", sides=8, slot=GL), cyl(0.015, 0.5, (0.22, 1.95, -0.1), sides=5, slot=DK),
        ball(0.04, (0.22, 2.2, -0.1), GL), box((0.3, 0.24, 0.3), (0, 0.55, -0.5), DK, bev=0.04),
        box((1.0, 0.12, 0.9), (0, 0.46, 0), DK, bev=0.03),
    ])
    build("NPC_Tuner_Arm", [
        cyl(0.07, 0.5, (0, 0, 0.25), axis="z", sides=6, slot=DK), ball(0.09, (0, 0, 0.5), AC),
        cyl(0.05, 0.45, (0, -0.08, 0.72), axis="z", sides=6, rot=(1.2, 0, 0)), box((0.1, 0.1, 0.16), (0, -0.2, 0.95), DK, bev=0.02),
        ball(0.04, (0, -0.22, 1.05), GL),
    ])

    # A desk with three screens, a sloped panel of keys and a lever.
    console = [box((2.3, 0.9, 0.85), (0, 0.45, 0), DK, bev=0.05), box((2.1, 0.08, 0.6), (0, 0.98, 0.14), AC, bev=0.02, rot=(0.28, 0, 0)),
               box((2.3, 0.5, 0.2), (0, 1.15, -0.34), bev=0.04), cyl(0.04, 0.5, (0.9, 1.1, 0.25), sides=6, slot=DK, rot=(0.3, 0, 0)),
               ball(0.07, (0.9, 1.36, 0.3), GL), cyl(0.06, 0.9, (-1.2, 0.45, -0.2), sides=6, slot=DK), box((0.5, 0.3, 0.06), (0, 0.5, 0.44), AC, bev=0.02),
               box((0.1, 0.06, 0.02), (-0.14, 0.52, 0.48), GL, bev=0), box((0.1, 0.06, 0.02), (0.14, 0.52, 0.48), GL, bev=0)]
    for i, x in enumerate((-0.72, 0.0, 0.72)):
        tall = 0.5 if i == 1 else 0.42
        console += [cyl(0.03, 0.4, (x, 1.5, -0.3), sides=5, slot=DK), box((0.64, tall + 0.06, 0.08), (x, 1.75 + tall / 2, -0.26), DK, bev=0.02),
                    box((0.56, tall, 0.03), (x, 1.75 + tall / 2, -0.21), GL, bev=0)]
    for row in range(3):
        for col in range(9):
            console.append(box((0.1, 0.03, 0.08), (-0.6 + col * 0.15, 1.0 + row * 0.03, 0.02 + row * 0.11), BS if (row + col) % 4 else GL, bev=0, rot=(0.28, 0, 0)))
    build("TR_Console", console)

    # A rack of parts: tubes, plates, boxes and gears on four shelves.
    shelf = [box((2.5, 0.06, 0.7), (0, 0.3 + i * 0.85, 0), bev=0.01) for i in range(4)]
    for x in (-1.2, 1.2):
        for z in (-0.3, 0.3):
            shelf.append(box((0.08, 3.3, 0.08), (x, 1.65, z), DK, bev=0.01))
    shelf += [box((2.5, 3.2, 0.04), (0, 1.65, -0.34), DK, bev=0), box((2.6, 0.1, 0.8), (0, 3.32, 0), AC, bev=0.02)]
    for i in range(4):
        y = 0.33 + i * 0.85
        kind = i % 4
        if kind == 0:
            shelf += [box((0.6, 0.4, 0.5), (-0.8, y + 0.2, 0), AC, bev=0.03), box((0.5, 0.3, 0.45), (-0.1, y + 0.15, 0.02), bev=0.03),
                      box((0.5, 0.3, 0.45), (-0.1, y + 0.46, 0.0), AC, bev=0.03), box((0.7, 0.5, 0.5), (0.75, y + 0.25, 0), DK, bev=0.03),
                      box((0.3, 0.06, 0.02), (0.75, y + 0.3, 0.26), GL, bev=0)]
        elif kind == 1:
            for k in range(5):
                shelf.append(cyl(0.09, 0.6, (-0.95 + k * 0.24, y + 0.09, 0), axis="z", sides=8, slot=DK if k % 2 else AC))
            shelf += [cyl(0.24, 0.1, (0.6, y + 0.05, 0), sides=10, slot=AC), cyl(0.2, 0.1, (0.6, y + 0.15, 0), sides=10),
                      cyl(0.16, 0.1, (0.6, y + 0.25, 0), sides=10, slot=DK), cyl(0.2, 0.08, (1.0, y + 0.2, 0), axis="z", sides=10, slot=AC)]
        elif kind == 2:
            for k in range(6):
                shelf.append(box((0.9, 0.035, 0.5), (-0.55, y + 0.02 + k * 0.045, 0.02 * (k % 2)), AC if k % 2 else BS, bev=0))
            shelf += [box((0.24, 0.5, 0.24), (0.4, y + 0.25, 0), DK, bev=0.03), ball(0.12, (0.4, y + 0.6, 0), GL),
                      box((0.5, 0.36, 0.4), (0.9, y + 0.18, 0), bev=0.03)]
        else:
            shelf += [cyl(0.13, 0.9, (-0.6, y + 0.13, 0), axis="x", sides=8, slot=DK), cyl(0.07, 0.5, (0.1, y + 0.07, 0.1), axis="x", sides=8, slot=AC),
                      box((0.45, 0.3, 0.4), (0.85, y + 0.15, 0), AC, bev=0.03), box((0.2, 0.2, 0.2), (0.3, y + 0.1, -0.1), bev=0.02)]
    build("TR_Shelf", shelf)

    # Tools on a board, and a tray under it.
    tools = [box((3.2, 1.7, 0.08), (0, 2.0, 0), DK, bev=0.02), box((3.3, 0.1, 0.14), (0, 2.88, 0.02), AC, bev=0.02),
             box((3.2, 0.08, 0.34), (0, 1.1, 0.14), AC, bev=0.02), box((3.2, 0.14, 0.05), (0, 1.18, 0.3), DK, bev=0)]
    for i in range(6):
        x = -1.35 + i * 0.3
        long = 0.5 + 0.12 * (i % 3)
        tools += [box((0.07, long, 0.04), (x, 2.5 - long / 2, 0.06), AC, bev=0), box((0.16, 0.12, 0.05), (x, 2.5, 0.06), AC, bev=0.01)]
    tools += [box((0.08, 0.7, 0.05), (0.7, 2.1, 0.06), bev=0), box((0.34, 0.2, 0.16), (0.7, 2.5, 0.08), DK, bev=0.03),
              box((0.7, 0.3, 0.03), (1.25, 2.3, 0.06), AC, bev=0), box((0.14, 0.2, 0.05), (0.92, 2.3, 0.06), DK, bev=0.01),
              cyl(0.2, 0.05, (1.25, 1.75, 0.06), axis="z", sides=12, slot=AC), cyl(0.07, 0.07, (1.25, 1.75, 0.06), axis="z", sides=8, slot=DK),
              box((0.5, 0.06, 0.03), (0.6, 1.6, 0.06), GL, bev=0), cyl(0.06, 0.3, (-1.0, 1.24, 0.14), sides=6, slot=DK),
              cyl(0.06, 0.24, (-0.7, 1.21, 0.14), sides=6), box((0.3, 0.12, 0.2), (0.2, 1.2, 0.14), bev=0.02)]
    build("TR_ToolWall", tools)

    # A heavy bench with a vise, drawers, a lamp on an arm and a half-built part on top.
    bench = [box((2.5, 0.14, 0.95), (0, 0.95, 0), AC, bev=0.03), box((2.3, 0.7, 0.8), (0, 0.5, -0.02), DK, bev=0.04),
             box((0.3, 0.3, 0.4), (-0.9, 1.17, 0.2), DK, bev=0.03), box((0.3, 0.3, 0.12), (-0.9, 1.17, 0.5), DK, bev=0.02),
             cyl(0.035, 0.5, (-0.9, 1.12, 0.6), axis="z", sides=6), cyl(0.02, 0.3, (-0.9, 1.12, 0.84), axis="x", sides=5, slot=AC),
             cyl(0.16, 0.24, (0.2, 1.14, 0.0), sides=8), box((0.3, 0.16, 0.5), (0.2, 1.3, 0.1), bev=0.03),
             cyl(0.04, 0.5, (0.2, 1.3, 0.55), axis="z", sides=6, slot=DK), ball(0.04, (0.2, 1.42, 0.2), GL),
             cyl(0.025, 0.9, (1.05, 1.45, -0.3), sides=5, slot=DK, rot=(0.0, 0.5, 0)), cyl(0.12, 0.14, (0.84, 1.86, -0.3), sides=8, slot=DK, end=0.2),
             ball(0.07, (0.84, 1.78, -0.3), GL), box((0.4, 0.03, 0.26), (0.85, 1.04, 0.2), bev=0)]
    for i in range(3):
        bench += [box((0.66, 0.5, 0.04), (-0.72 + i * 0.72, 0.52, 0.4), bev=0.02), box((0.24, 0.04, 0.04), (-0.72 + i * 0.72, 0.6, 0.44), GL, bev=0)]
    for i in range(7):
        bench.append(cyl(0.03, 0.04, (0.55 + 0.1 * (i % 4), 1.04, -0.25 + 0.09 * (i % 3)), sides=6, slot=DK))
    build("TR_Bench", bench)

    # Three gas bottles in a frame, with valves and a gauge.
    tanks = [box((1.5, 0.08, 0.6), (0, 0.04, 0), DK, bev=0.02), box((1.5, 0.06, 0.06), (0, 0.9, 0.28), DK, bev=0),
             box((1.5, 0.06, 0.06), (0, 0.9, -0.28), DK, bev=0), box((0.06, 1.0, 0.06), (-0.72, 0.5, 0.28), DK, bev=0),
             box((0.06, 1.0, 0.06), (0.72, 0.5, 0.28), DK, bev=0), cyl(0.06, 0.08, (0.5, 1.9, 0.22), axis="z", sides=10, slot=DK),
             cyl(0.045, 0.02, (0.5, 1.9, 0.27), axis="z", sides=10, slot=GL)]
    for i, x in enumerate((-0.46, 0.0, 0.46)):
        tanks += [cyl(0.2, 1.5, (x, 0.83, 0), sides=10, slot=AC if i == 1 else BS), cyl(0.12, 0.16, (x, 1.66, 0), sides=10, slot=DK, end=0.07),
                  cyl(0.05, 0.14, (x, 1.8, 0), sides=6, slot=DK), cyl(0.09, 0.03, (x, 1.86, 0), sides=6, slot=AC),
                  cyl(0.205, 0.05, (x, 1.2, 0), sides=10, slot=DK)]
    tanks.append(cyl(0.02, 1.0, (0.0, 1.9, 0.1), axis="x", sides=5, slot=DK))
    build("TR_Tanks", tanks)

    # Open bins of small parts, one on top of two.
    bins = []
    for x, y, z in ((-0.5, 0.0, 0.0), (0.5, 0.0, 0.05), (0.02, 0.5, 0.0)):
        bins += [box((0.9, 0.5, 0.7), (x, y + 0.25, z), DK, bev=0.03), box((0.8, 0.05, 0.6), (x, y + 0.48, z), BS, bev=0),
                 box((0.4, 0.14, 0.03), (x, y + 0.26, z + 0.36), AC, bev=0)]
        for k in range(5):
            bins.append(box((0.14, 0.12, 0.14), (x - 0.25 + 0.13 * k, y + 0.54, z - 0.15 + 0.08 * (k % 3)), AC if k % 2 else GL, bev=0.02))
    build("TR_Bins", bins)

    # A reel of cable on a stand.
    build("TR_Reel", [
        cyl(0.6, 0.06, (-0.3, 0.75, 0), axis="x", sides=14, slot=DK), cyl(0.6, 0.06, (0.3, 0.75, 0), axis="x", sides=14, slot=DK),
        cyl(0.42, 0.55, (0, 0.75, 0), axis="x", sides=12, slot=AC), cyl(0.12, 0.8, (0, 0.75, 0), axis="x", sides=8),
        box((0.08, 0.9, 0.5), (-0.42, 0.42, 0), DK, bev=0.02), box((0.08, 0.9, 0.5), (0.42, 0.42, 0), DK, bev=0.02),
        box((1.0, 0.06, 0.7), (0, 0.03, 0), DK, bev=0.01), cyl(0.035, 0.9, (0.1, 0.1, 0.5), axis="z", sides=5, slot=AC),
    ])

    # A vent that lets steam out, piped up the wall.
    vent = [box((1.0, 0.6, 0.5), (0, 0.3, 0.05), DK, bev=0.04), cyl(0.16, 3.6, (0.3, 2.4, -0.05), sides=8, slot=AC),
            cyl(0.2, 0.12, (0.3, 1.2, -0.05), sides=8, slot=DK), cyl(0.2, 0.12, (0.3, 3.0, -0.05), sides=8, slot=DK),
            cyl(0.09, 0.1, (-0.2, 0.65, 0.05), sides=8, slot=AC)]
    for i in range(5):
        vent.append(box((0.8, 0.04, 0.1), (0, 0.12 + i * 0.1, 0.3), AC, bev=0, rot=(0.5, 0, 0)))
    build("TR_Vent", vent)


def rooms():
    # The furniture of the rooms behind the other bay doors. Each piece stands on the floor at its
    # origin and faces +z, into the room, unless it says otherwise.

    # A floor for any room: a slab of plates, with marks at the door (-z).
    floor = [box((10.8, 0.1, 9.4), (0, -0.03, 0), DK, bev=0)]
    for ix in range(4):
        for iz in range(3):
            floor.append(box((2.5, 0.04, 2.95), (-3.9 + ix * 2.6, 0.035, -3.05 + iz * 3.05), BS if (ix + iz) % 2 else AC, bev=0.01))
    for i in range(10):
        floor.append(box((0.5, 0.045, 0.3), (-2.25 + i * 0.5, 0.07, -4.3), GL if i % 2 == 0 else DK, bev=0, rot=(0, 0, 0.5)))
    build("RM_Floor", floor)

    # ------------------------------------------------ the Mechanic's shop
    # A four-post lift with two runways, ramps at the door end, a pump with its lamp on one post.
    lift = [box((0.62, 0.12, 4.4), (0.8, 0.96, 0), AC, bev=0.02), box((0.62, 0.12, 4.4), (-0.8, 0.96, 0), AC, bev=0.02),
            box((0.5, 0.03, 4.2), (0.8, 1.03, 0), DK, bev=0), box((0.5, 0.03, 4.2), (-0.8, 1.03, 0), DK, bev=0),
            box((2.9, 0.16, 0.2), (0, 0.82, 1.8), DK, bev=0.02), box((2.9, 0.16, 0.2), (0, 0.82, -1.8), DK, bev=0.02),
            box((0.62, 0.08, 1.2), (0.8, 0.5, -2.7), AC, bev=0.01, rot=(-0.72, 0, 0)), box((0.62, 0.08, 1.2), (-0.8, 0.5, -2.7), AC, bev=0.01, rot=(-0.72, 0, 0)),
            box((0.5, 0.6, 0.4), (1.72, 0.3, 1.8), bev=0.04), box((0.3, 0.2, 0.05), (1.72, 0.42, 2.02), GL, bev=0),
            cyl(0.05, 0.6, (1.72, 0.9, 1.7), sides=6, slot=DK), ball(0.07, (1.72, 1.22, 1.7), GL),
            cyl(0.08, 0.8, (0, 0.42, 0), sides=8, slot=DK), cyl(0.12, 0.3, (0, 0.15, 0), sides=8, slot=AC),
            box((2.2, 0.1, 0.5), (0, 0.05, 0), DK, bev=0.02)]
    for x in (-1.45, 1.45):
        for z in (-1.8, 1.8):
            lift += [box((0.22, 2.3, 0.22), (x, 1.15, z), DK, bev=0.03), box((0.5, 0.08, 0.5), (x, 0.04, z), AC, bev=0.01),
                     box((0.3, 0.12, 0.3), (x, 2.36, z), AC, bev=0.02), box((0.05, 1.5, 0.05), (x * 0.92, 1.3, z), GL, bev=0)]
    build("MR_Lift", lift)

    # A crane on wheels. What hangs from its hook is a model of its own, so it can sway.
    build("MR_Hoist", [
        box((0.14, 0.12, 1.9), (0.5, 0.12, 0.5), DK, bev=0.02), box((0.14, 0.12, 1.9), (-0.5, 0.12, 0.5), DK, bev=0.02),
        box((1.14, 0.14, 0.2), (0, 0.13, -0.4), DK, bev=0.02), box((0.18, 2.2, 0.18), (0, 1.2, -0.4), AC, bev=0.03),
        box((0.14, 0.16, 2.1), (0, 2.5, 0.42), AC, bev=0.03, rot=(-0.42, 0, 0)), cyl(0.07, 1.3, (0, 1.45, -0.05), sides=8, slot=DK, rot=(0.5, 0, 0)),
        cyl(0.04, 0.9, (0, 1.9, 0.2), sides=6, rot=(0.5, 0, 0)), box((0.22, 0.24, 0.2), (0, 0.62, -0.28), DK, bev=0.03),
        cyl(0.02, 0.5, (0.14, 0.8, -0.2), sides=5, slot=GL, rot=(0.9, 0, 0)), cyl(0.09, 0.08, (0.5, 0.06, 1.4), axis="x", sides=8, slot=DK),
        cyl(0.09, 0.08, (-0.5, 0.06, 1.4), axis="x", sides=8, slot=DK), cyl(0.09, 0.08, (0.5, 0.06, -0.4), axis="x", sides=8, slot=DK),
        cyl(0.09, 0.08, (-0.5, 0.06, -0.4), axis="x", sides=8, slot=DK), box((0.2, 0.2, 0.24), (0, 2.9, 1.34), DK, bev=0.03),
    ])
    # A reactor block on a chain. Its origin is the hook it hangs from.
    build("MR_Load", [
        cyl(0.02, 0.9, (0, -0.45, 0), sides=5, slot=DK), cyl(0.06, 0.1, (0, -0.92, 0), sides=6, slot=AC),
        box((0.7, 0.62, 0.9), (0, -1.3, 0), bev=0.06), box((0.76, 0.1, 0.96), (0, -1.02, 0), DK, bev=0.02),
        box((0.76, 0.1, 0.96), (0, -1.58, 0), DK, bev=0.02), box((0.3, 0.3, 0.04), (0, -1.3, 0.47), GL, bev=0),
        box((0.3, 0.3, 0.04), (0, -1.3, -0.47), GL, bev=0), cyl(0.12, 0.2, (0.42, -1.3, 0.2), axis="x", sides=8, slot=AC),
        cyl(0.12, 0.2, (-0.42, -1.3, -0.2), axis="x", sides=8, slot=AC), cyl(0.05, 0.3, (0.2, -0.86, 0.3), sides=6, slot=DK),
        cyl(0.05, 0.3, (-0.2, -0.86, -0.3), sides=6, slot=DK),
    ])
    # A roll cabinet full of drawers, with a tray of tools on top.
    chest = [box((1.2, 1.3, 0.62), (0, 0.8, 0), AC, bev=0.04), box((1.26, 0.08, 0.68), (0, 1.48, 0), DK, bev=0.02),
             box((1.1, 0.16, 0.5), (0, 1.6, 0), bev=0.03), box((0.5, 0.06, 0.08), (-0.2, 1.71, 0.05), DK, bev=0.01),
             box((0.08, 0.06, 0.4), (0.3, 1.71, 0), bev=0.01), cyl(0.05, 0.3, (0.1, 1.72, -0.12), axis="x", sides=6, slot=GL)]
    for i in range(6):
        y = 0.28 + i * 0.2
        chest += [box((1.08, 0.17, 0.04), (0, y, 0.31), bev=0.01), box((0.5, 0.03, 0.04), (0, y + 0.03, 0.34), DK, bev=0)]
    for x in (-0.5, 0.5):
        for z in (-0.24, 0.24):
            chest.append(cyl(0.07, 0.06, (x, 0.07, z), axis="x", sides=8, slot=DK))
    build("MR_ToolChest", chest)

    # A compressor: a tank on feet, a motor and a flywheel on top, a gauge, a coil of hose.
    comp = [cyl(0.36, 1.4, (0, 0.55, 0), axis="x", sides=12, slot=AC), ball(0.36, (0.7, 0.55, 0), AC), ball(0.36, (-0.7, 0.55, 0), AC),
            box((0.12, 0.3, 0.6), (0.5, 0.15, 0), DK, bev=0.02), box((0.12, 0.3, 0.6), (-0.5, 0.15, 0), DK, bev=0.02),
            box((0.6, 0.36, 0.44), (-0.25, 1.08, 0), DK, bev=0.04), cyl(0.26, 0.08, (0.42, 1.1, 0), axis="x", sides=12),
            cyl(0.08, 0.14, (0.42, 1.1, 0), axis="x", sides=8, slot=DK), cyl(0.09, 0.05, (0, 0.98, 0.3), axis="z", sides=10, slot=DK),
            cyl(0.07, 0.02, (0, 0.98, 0.33), axis="z", sides=10, slot=GL), cyl(0.03, 1.6, (-0.62, 1.7, -0.2), sides=5, slot=DK),
            box((0.1, 0.08, 0.02), (-0.25, 1.14, 0.23), GL, bev=0)]
    for i in range(4):
        comp.append(torus(0.22, 0.035, (0.75, 0.16 + i * 0.07, 0.42), DK))
    build("MR_Compressor", comp)

    # A gear for the wall. It lies in the plane of the wall and turns on its middle.
    gear = [cyl(0.9, 0.12, (0, 0, 0), axis="z", sides=20, slot=AC), cyl(0.74, 0.14, (0, 0, 0), axis="z", sides=20),
            cyl(0.24, 0.24, (0, 0, 0), axis="z", sides=10, slot=DK), cyl(0.1, 0.3, (0, 0, 0), axis="z", sides=8, slot=AC)]
    for i in range(14):
        a = i * 2.0 * math.pi / 14
        gear.append(cyl(0.12, 0.12, (math.cos(a) * 0.96, math.sin(a) * 0.96, 0), axis="z", sides=6, slot=AC))
    for i in range(5):
        a = i * 2.0 * math.pi / 5
        gear.append(cyl(0.15, 0.16, (math.cos(a) * 0.5, math.sin(a) * 0.5, 0), axis="z", sides=8, slot=DK))
    build("MR_Gear", gear)

    # Three stands on a rail, for the weapons the player has. The game puts the weapons on them.
    stand = [box((3.0, 0.14, 0.7), (0, 0.07, 0), DK, bev=0.03), box((3.0, 1.5, 0.08), (0, 1.0, -0.36), DK, bev=0.02),
             box((3.1, 0.1, 0.14), (0, 1.78, -0.34), AC, bev=0.02), box((2.6, 0.05, 0.03), (0, 1.55, -0.31), GL, bev=0)]
    for x in (-1.0, 0.0, 1.0):
        stand += [cyl(0.2, 0.8, (x, 0.54, 0), sides=10), cyl(0.3, 0.08, (x, 0.96, 0), sides=10, slot=AC),
                  cyl(0.26, 0.1, (x, 0.19, 0), sides=10, slot=AC), box((0.4, 0.2, 0.03), (x, 1.3, -0.31), bev=0)]
    build("MR_WeaponStand", stand)

    # A welding cart: a set on two wheels with a bottle, dials, a coil of cable and the torch in its holder.
    weld = [box((0.7, 0.7, 0.5), (0, 0.5, 0), AC, bev=0.04), box((0.6, 0.3, 0.03), (0, 0.6, 0.26), DK, bev=0),
            cyl(0.07, 0.02, (-0.15, 0.62, 0.28), axis="z", sides=8, slot=GL), cyl(0.07, 0.02, (0.15, 0.62, 0.28), axis="z", sides=8, slot=GL),
            cyl(0.17, 1.2, (0, 0.75, -0.42), sides=10), cyl(0.1, 0.14, (0, 1.42, -0.42), sides=8, slot=DK, end=0.06),
            cyl(0.2, 0.1, (0.4, 0.2, 0), axis="x", sides=10, slot=DK), cyl(0.2, 0.1, (-0.4, 0.2, 0), axis="x", sides=10, slot=DK),
            box((0.06, 0.9, 0.06), (0.3, 1.0, -0.3), DK, bev=0), box((0.06, 0.9, 0.06), (-0.3, 1.0, -0.3), DK, bev=0),
            box((0.7, 0.06, 0.06), (0, 1.42, -0.3), DK, bev=0), cyl(0.03, 0.4, (0.42, 0.95, 0.1), sides=5, slot=DK, rot=(0.6, 0, 0)),
            ball(0.04, (0.42, 1.12, 0.22), GL), box((0.5, 0.06, 0.4), (0, 0.06, 0.3), DK, bev=0.01)]
    for i in range(3):
        weld.append(torus(0.18, 0.03, (-0.42, 0.75 + i * 0.06, 0.12), DK, axis="x"))
    build("MR_Welder", weld)

    # What lies on the floor of a workshop: a creeper, two stands, a pan of oil, spanners.
    clutter = [box((1.1, 0.06, 0.5), (0, 0.12, 0), AC, bev=0.02), box((0.3, 0.1, 0.4), (0.38, 0.19, 0), DK, bev=0.03),
               cyl(0.28, 0.1, (-1.0, 0.05, 0.5), sides=12, slot=DK), cyl(0.24, 0.02, (-1.0, 0.1, 0.5), sides=12, slot=AC),
               box((0.5, 0.03, 0.07), (0.3, 0.02, 0.7), bev=0, rot=(0, 0, 0.4)), box((0.4, 0.03, 0.06), (0.6, 0.02, 0.62), bev=0, rot=(0, 0, -0.5)),
               box((0.3, 0.2, 0.2), (1.1, 0.1, 0.3), DK, bev=0.03), box((0.2, 0.04, 0.1), (1.1, 0.22, 0.3), GL, bev=0)]
    for x, z in ((-0.42, -0.2), (0.42, -0.2), (-0.42, 0.2), (0.42, 0.2)):
        clutter.append(cyl(0.05, 0.06, (x, 0.05, z), axis="x", sides=6, slot=DK))
    for x in (-1.2, 1.5):
        clutter += [cyl(0.2, 0.06, (x, 0.03, -0.5), sides=6, slot=DK), cyl(0.05, 0.5, (x, 0.3, -0.5), sides=6),
                    box((0.2, 0.06, 0.12), (x, 0.56, -0.5), AC, bev=0.01)]
    build("MR_Clutter", clutter)

    # ------------------------------------------------ the Archivist's archive
    # A wall of small drawers, each with its label, a few with a lamp that says it holds something.
    stacks = [box((2.8, 3.7, 0.7), (0, 1.9, 0), DK, bev=0.04), box((2.9, 0.16, 0.8), (0, 3.8, 0), AC, bev=0.03),
              box((2.9, 0.2, 0.8), (0, 0.1, 0), AC, bev=0.03)]
    for row in range(8):
        for col in range(6):
            x, y = -1.125 + col * 0.45, 0.45 + row * 0.42
            stacks += [box((0.4, 0.36, 0.06), (x, y, 0.36), BS if (row + col) % 3 else AC, bev=0.015),
                       box((0.16, 0.05, 0.03), (x, y + 0.06, 0.4), DK, bev=0)]
            if (row * 7 + col * 3) % 5 == 0:
                stacks.append(box((0.06, 0.06, 0.03), (x + 0.13, y - 0.1, 0.4), GL, bev=0))
    build("AR_Stacks", stacks)

    # A card cabinet on legs, one drawer pulled out with its cards showing.
    cards = [box((1.5, 1.0, 0.62), (0, 1.0, 0), DK, bev=0.04), box((1.56, 0.08, 0.68), (0, 1.54, 0), AC, bev=0.02)]
    for x in (-0.65, 0.65):
        for z in (-0.24, 0.24):
            cards.append(box((0.08, 0.5, 0.08), (x, 0.25, z), DK, bev=0.01))
    for row in range(4):
        for col in range(5):
            x, y = -0.56 + col * 0.28, 0.63 + row * 0.24
            out = 0.3 if (row, col) == (2, 3) else 0.0
            cards += [box((0.25, 0.2, 0.05 + out), (x, y, 0.33 + out / 2), AC if (row + col) % 2 else BS, bev=0.01),
                      box((0.1, 0.05, 0.02), (x, y, 0.37 + out), GL if (row + col) % 4 == 0 else DK, bev=0)]
    for k in range(6):
        cards.append(box((0.2, 0.14, 0.012), (0.28, 1.22, 0.4 + k * 0.035), BS, bev=0))
    cards += [box((0.4, 0.3, 0.3), (-0.4, 1.74, 0), bev=0.03), box((0.36, 0.04, 0.26), (-0.4, 1.9, 0), AC, bev=0)]
    build("AR_CardCabinet", cards)

    # A sloped reading desk with an open ledger, a lamp on an arm, a glass on another, and a stool.
    desk = [box((1.8, 0.08, 1.0), (0, 1.08, 0), AC, bev=0.02, rot=(0.3, 0, 0)), box((1.7, 0.9, 0.7), (0, 0.5, -0.1), DK, bev=0.04),
            box((1.8, 0.06, 0.1), (0, 0.96, 0.5), DK, bev=0.01), box((0.62, 0.03, 0.8), (-0.33, 1.14, 0.04), BS, bev=0, rot=(0.3, 0, 0)),
            box((0.62, 0.03, 0.8), (0.33, 1.14, 0.04), BS, bev=0, rot=(0.3, 0, 0)), box((0.05, 0.04, 0.82), (0, 1.15, 0.04), DK, bev=0, rot=(0.3, 0, 0)),
            cyl(0.025, 0.9, (0.8, 1.6, -0.4), sides=5, slot=DK), cyl(0.025, 0.6, (0.62, 2.0, -0.26), sides=5, slot=DK, rot=(0.8, 0, 0.6)),
            cyl(0.14, 0.16, (0.42, 1.92, -0.1), sides=8, slot=DK, end=0.22), ball(0.08, (0.42, 1.84, -0.1), GL),
            cyl(0.02, 0.7, (-0.8, 1.5, -0.4), sides=5, slot=DK), torus(0.16, 0.025, (-0.62, 1.62, -0.05), AC, axis="z"),
            cyl(0.14, 0.01, (-0.62, 1.62, -0.05), axis="z", sides=12, slot=GL), cyl(0.08, 0.12, (0.72, 1.0, 0.3), sides=8, slot=DK),
            cyl(0.26, 0.08, (0, 0.62, 1.1), sides=10, slot=AC), cyl(0.05, 0.58, (0, 0.3, 1.1), sides=6, slot=DK),
            cyl(0.22, 0.04, (0, 0.02, 1.1), sides=8, slot=DK)]
    for i in range(3):
        desk += [box((0.5, 0.26, 0.04), (-0.56 + i * 0.56, 0.5, 0.27), bev=0.01), box((0.16, 0.03, 0.03), (-0.56 + i * 0.56, 0.56, 0.3), GL, bev=0)]
    build("AR_Desk", desk)

    # A plinth with a glass case on it. The game stands a part in the case and turns it. The top of
    # the case is a frame and not a lid: the camera looks in from above.
    case = [box((0.84, 0.9, 0.84), (0, 0.45, 0), DK, bev=0.05), box((0.94, 0.08, 0.94), (0, 0.94, 0), AC, bev=0.02),
            box((0.94, 0.12, 0.94), (0, 0.06, 0), AC, bev=0.03), torus(0.3, 0.03, (0, 1.0, 0), GL), box((0.5, 0.12, 0.03), (0, 0.6, 0.43), BS, bev=0)]
    for x in (-0.42, 0.42):
        case.append(box((0.05, 0.05, 0.9), (x, 2.08, 0), AC, bev=0))
        for z in (-0.42, 0.42):
            case.append(box((0.04, 1.1, 0.04), (x, 1.53, z), AC, bev=0))
    for z in (-0.42, 0.42):
        case.append(box((0.9, 0.05, 0.05), (0, 2.08, z), AC, bev=0))
    build("AR_Case", case)

    # A round table with a lens in it. The game lets a part hang over it as a picture made of light.
    table = [cyl(1.0, 0.7, (0, 0.35, 0), sides=14, slot=DK), cyl(1.1, 0.1, (0, 0.72, 0), sides=14, slot=AC),
             cyl(0.75, 0.04, (0, 0.78, 0), sides=14, slot=DK), cyl(0.5, 0.03, (0, 0.81, 0), sides=14, slot=GL),
             cyl(1.12, 0.14, (0, 0.07, 0), sides=14, slot=AC), box((0.7, 0.06, 0.36), (0, 0.84, 0.92), bev=0.02, rot=(0.35, 0, 0))]
    table += around(3, 0.86, lambda x, z, a: cyl(0.05, 0.6, (x, 1.06, z), sides=6, slot=AC, end=0.02), start=0.5)
    table += around(3, 0.86, lambda x, z, a: ball(0.06, (x, 1.38, z), GL), start=0.5)
    table += around(10, 1.02, lambda x, z, a: box((0.1, 0.16, 0.1), (x, 0.4, z), GL if int(a * 3) % 2 else BS, bev=0))
    build("AR_Projector", table)

    # A ladder on wheels, leaning back against whatever stands behind it.
    ladder = [box((0.07, 3.7, 0.1), (0.3, 1.8, -0.36), DK, bev=0.01, rot=(-0.2, 0, 0)), box((0.07, 3.7, 0.1), (-0.3, 1.8, -0.36), DK, bev=0.01, rot=(-0.2, 0, 0)),
              cyl(0.1, 0.06, (0.36, 0.1, 0.02), axis="x", sides=8, slot=AC), cyl(0.1, 0.06, (-0.36, 0.1, 0.02), axis="x", sides=8, slot=AC),
              box((0.8, 0.06, 0.12), (0, 3.56, -0.72), AC, bev=0.01)]
    for k in range(9):
        y = 0.4 + k * 0.36
        ladder.append(box((0.6, 0.05, 0.14), (0, y, -0.36 - (y - 1.8) * 0.203), AC, bev=0.01))
    build("AR_Ladder", ladder)

    # A cabinet that remembers: two reels of tape behind glass (the reels are a model of their
    # own, so they can turn), rows of lamps, a row of switches and a bin for what it prints.
    bank = [box((2.0, 2.4, 0.8), (0, 1.2, 0), DK, bev=0.05), box((2.06, 0.1, 0.86), (0, 2.44, 0), AC, bev=0.02),
            box((1.7, 0.9, 0.04), (0, 1.75, 0.4), BS, bev=0.02), box((1.76, 0.06, 0.08), (0, 1.27, 0.42), AC, bev=0),
            box((1.76, 0.06, 0.08), (0, 2.23, 0.42), AC, bev=0), box((1.7, 0.3, 0.06), (0, 0.42, 0.41), AC, bev=0.02),
            box((0.6, 0.4, 0.5), (0.5, 0.2, 0.62), bev=0.03), box((0.5, 0.02, 0.4), (0.5, 0.42, 0.62), BS, bev=0)]
    for row in range(3):
        for col in range(12):
            lit = (row * 5 + col * 7) % 4
            bank.append(box((0.08, 0.08, 0.03), (-0.77 + col * 0.14, 0.78 + row * 0.14, 0.41), GL if lit == 0 else AC if lit == 1 else BS, bev=0))
    for col in range(8):
        bank.append(box((0.05, 0.12, 0.06), (-0.7 + col * 0.2, 0.42, 0.46), DK, bev=0.01, rot=(0.4 if col % 3 else -0.4, 0, 0)))
    for i in range(6):
        bank.append(box((1.5, 0.03, 0.03), (0, 0.1 + i * 0.03, 0.41), DK, bev=0))
    build("AR_TapeBank", bank)
    reel = [cyl(0.32, 0.05, (0, 0, 0), axis="z", sides=16, slot=AC), cyl(0.26, 0.07, (0, 0, 0), axis="z", sides=16, slot=DK),
            cyl(0.09, 0.1, (0, 0, 0), axis="z", sides=8, slot=AC)]
    for i in range(3):
        a = i * 2.0 * math.pi / 3
        reel.append(cyl(0.07, 0.09, (math.cos(a) * 0.17, math.sin(a) * 0.17, 0), axis="z", sides=6, slot=BS))
    build("AR_Reel", reel)

    # A bin of rolled plans, a stack of boxed files, and one plan unrolled on the floor.
    scrolls = [box((0.8, 0.7, 0.8), (0, 0.35, 0), DK, bev=0.04), box((0.86, 0.06, 0.86), (0, 0.7, 0), AC, bev=0.02),
               box((1.3, 0.02, 0.9), (1.3, 0.02, 0.3), BS, bev=0), box((1.1, 0.01, 0.7), (1.3, 0.035, 0.3), AC, bev=0),
               cyl(0.06, 0.9, (1.3, 0.07, -0.2), axis="x", sides=8, slot=BS)]
    for i in range(7):
        a = i * 0.9
        tall = 0.9 + 0.25 * (i % 3)
        scrolls += [cyl(0.07, tall, (math.cos(a) * 0.22, 0.25 + tall / 2, math.sin(a) * 0.22), sides=8, slot=BS if i % 2 else AC),
                    cyl(0.075, 0.04, (math.cos(a) * 0.22, 0.27 + tall, math.sin(a) * 0.22), sides=8, slot=GL if i % 3 == 0 else DK)]
    for i in range(4):
        scrolls.append(box((0.6, 0.2, 0.44), (-1.0 + 0.03 * (i % 2), 0.1 + i * 0.21, 0.1), AC if i % 2 else BS, bev=0.02))
    build("AR_Scrolls", scrolls)

    # ------------------------------------------------ the Smelter's foundry
    # A track on the floor, along x, for the cart that carries the crucible.
    rail = [box((6.4, 0.08, 0.08), (0, 0.1, 0.45), AC, bev=0.01), box((6.4, 0.08, 0.08), (0, 0.1, -0.45), AC, bev=0.01),
            box((0.3, 0.3, 1.2), (3.3, 0.15, 0), DK, bev=0.03), box((0.3, 0.3, 1.2), (-3.3, 0.15, 0), DK, bev=0.03)]
    for i in range(9):
        rail.append(box((0.2, 0.06, 1.3), (-3.0 + i * 0.75, 0.03, 0), DK, bev=0.01))
    build("SR_Rail", rail)
    # The cart: four wheels, a frame, and a crucible full of metal on two trunnions, with its wheel to tip it.
    build("SR_Cart", [
        box((1.3, 0.14, 1.1), (0, 0.4, 0), DK, bev=0.03), cyl(0.2, 0.1, (0.45, 0.24, 0.45), axis="z", sides=10, slot=AC),
        cyl(0.2, 0.1, (-0.45, 0.24, 0.45), axis="z", sides=10, slot=AC), cyl(0.2, 0.1, (0.45, 0.24, -0.45), axis="z", sides=10, slot=AC),
        cyl(0.2, 0.1, (-0.45, 0.24, -0.45), axis="z", sides=10, slot=AC), box((0.1, 0.9, 0.14), (0.62, 0.9, 0), DK, bev=0.02),
        box((0.1, 0.9, 0.14), (-0.62, 0.9, 0), DK, bev=0.02), cyl(0.42, 0.8, (0, 1.05, 0), sides=12, end=0.5),
        cyl(0.44, 0.04, (0, 1.46, 0), sides=12, slot=GL), cyl(0.52, 0.08, (0, 1.42, 0), sides=12, slot=DK),
        cyl(0.07, 1.36, (0, 1.1, 0), axis="x", sides=8, slot=AC), cyl(0.22, 0.05, (0.74, 1.1, 0), axis="x", sides=10, slot=AC),
        cyl(0.03, 0.2, (0.8, 1.1, 0.18), axis="x", sides=5, slot=DK), box((0.5, 0.12, 0.2), (0, 0.62, 0.5), AC, bev=0.02),
    ])
    # A bed of sand with the shape of a frame cut into it, running with metal, and two smaller moulds.
    moulds = [box((3.2, 0.34, 2.3), (0, 0.17, 0), DK, bev=0.04), box((3.0, 0.06, 2.1), (0, 0.33, 0), BS, bev=0),
              box((0.12, 0.05, 1.5), (-0.5, 0.36, 0.1), GL, bev=0), box((0.12, 0.05, 1.5), (0.5, 0.36, 0.1), GL, bev=0),
              box((1.1, 0.05, 0.12), (0, 0.36, 0.8), GL, bev=0), box((1.1, 0.05, 0.12), (0, 0.36, -0.6), GL, bev=0),
              box((1.1, 0.05, 0.12), (0, 0.36, 0.1), GL, bev=0), cyl(0.16, 0.05, (0, 0.36, -0.95), sides=8, slot=GL),
              box((0.5, 0.05, 0.3), (-1.1, 0.36, -0.6), GL, bev=0), box((0.5, 0.05, 0.3), (-1.1, 0.36, 0.5), AC, bev=0),
              box((0.6, 0.1, 0.4), (-1.1, 0.36, -0.6), DK, bev=0.02), box((0.6, 0.1, 0.4), (-1.1, 0.36, 0.5), DK, bev=0.02),
              cyl(0.03, 1.8, (1.2, 0.44, 0.2), axis="z", sides=5, slot=AC), box((0.5, 0.04, 0.1), (1.2, 0.42, -0.72), AC, bev=0)]
    build("SR_Moulds", moulds)
    # A power hammer: a heavy frame over an anvil with a billet on it. Its head is a model of its own.
    hammer = [box((1.5, 0.5, 1.5), (0, 0.25, 0), DK, bev=0.06), box((0.9, 0.5, 0.9), (0, 0.75, 0.15), bev=0.05),
              box((0.7, 0.12, 0.7), (0, 1.06, 0.15), AC, bev=0.02), box((0.5, 0.1, 0.24), (0, 1.17, 0.15), GL, bev=0.02),
              box((0.6, 3.0, 0.5), (0, 2.0, -0.5), DK, bev=0.05), box((0.7, 0.6, 1.1), (0, 3.3, -0.1), DK, bev=0.05),
              box((0.16, 1.3, 0.1), (0.3, 2.5, 0.2), AC, bev=0.01), box((0.16, 1.3, 0.1), (-0.3, 2.5, 0.2), AC, bev=0.01),
              cyl(0.3, 0.3, (0, 3.75, -0.1), sides=10, slot=AC), cyl(0.5, 0.1, (0.4, 2.2, -0.5), axis="x", sides=12, slot=AC),
              cyl(0.03, 1.0, (-0.42, 1.6, -0.3), sides=5, slot=DK, rot=(0.3, 0, 0)), ball(0.06, (-0.42, 2.08, -0.16), GL)]
    build("SR_Hammer", hammer)
    build("SR_HammerHead", [
        box((0.44, 0.9, 0.44), (0, 0.45, 0), bev=0.04), box((0.5, 0.14, 0.5), (0, 0.07, 0), DK, bev=0.02),
        cyl(0.1, 1.0, (0, 1.4, 0), sides=8, slot=AC), box((0.5, 0.1, 0.5), (0, 0.86, 0), AC, bev=0.02),
    ])
    # Bellows: boards with folds of leather between them, a nozzle, and the lever that works them.
    # Its origin is the hinge at the nozzle end, so squeezing it is a matter of scaling its height.
    bellows = [box((1.0, 0.08, 1.6), (0, 0.9, 0.9), AC, bev=0.02), box((1.0, 0.08, 1.6), (0, 0.3, 0.9), AC, bev=0.02),
               cyl(0.1, 0.5, (0, 0.6, -0.1), axis="z", sides=8, slot=DK, end=0.06), box((0.3, 0.5, 0.3), (0, 0.6, 0.12), DK, bev=0.04),
               box((0.08, 0.06, 1.2), (0, 0.98, 1.3), DK, bev=0.01), ball(0.1, (0, 0.98, 1.92), GL)]
    for i in range(4):
        y = 0.4 + i * 0.133
        bellows.append(box((0.92 - 0.06 * (i % 2), 0.1, 1.5), (0, y, 0.92), DK, bev=0.03))
    build("SR_Bellows", bellows)
    # A bin of coal, a shovel in it, lumps on the floor in front.
    coal = [box((1.7, 0.9, 1.0), (0, 0.45, 0), DK, bev=0.04), box((1.76, 0.08, 1.06), (0, 0.9, 0), AC, bev=0.02),
            box((1.5, 0.5, 0.06), (0, 0.4, 0.5), AC, bev=0.02), cyl(0.03, 1.5, (0.5, 1.2, 0.1), sides=5, slot=AC, rot=(0.3, 0.2, 0)),
            box((0.3, 0.04, 0.36), (0.38, 0.62, 0.3), BS, bev=0.01, rot=(0.6, 0, 0))]
    for i in range(12):
        a = i * 1.7
        coal.append(rock(0.2 + 0.05 * (i % 3), 0.8, 70 + i, DK, sub=1, at=(math.cos(a) * 0.5 * (i % 4) / 3.0, math.sin(a) * 0.3)))
    for obj in coal[-12:]:
        obj.location.z += 0.72
    for i in range(4):
        coal.append(rock(0.14, 0.8, 90 + i, DK, sub=1, at=(-0.6 + i * 0.4, 0.75 + 0.1 * (i % 2))))
    build("SR_CoalBin", coal)
    # Ingots on a pallet, the top ones still glowing, a pair of tongs laid across.
    ingots = [box((1.4, 0.12, 1.1), (0, 0.06, 0), DK, bev=0.02)]
    for layer in range(3):
        for i in range(4 - layer):
            hot = layer == 2 or (layer == 1 and i == 1)
            x = -0.5 + i * 0.34 + layer * 0.17
            ingots.append(box((0.28, 0.16, 0.8), (x, 0.2 + layer * 0.17, 0.02 * (layer % 2)), GL if hot else BS, bev=0.03))
    ingots += [box((0.04, 0.03, 1.2), (0.2, 0.74, 0.1), AC, bev=0, rot=(0, 0, 0.5)), box((0.04, 0.03, 1.2), (0.26, 0.76, 0.1), AC, bev=0, rot=(0, 0, 0.62))]
    build("SR_Ingots", ingots)
    # A quench trough, full, with a plate hanging in it and a drain.
    quench = [box((1.8, 0.8, 0.9), (0, 0.4, 0), DK, bev=0.04), box((1.6, 0.04, 0.7), (0, 0.78, 0), AC, bev=0),
              box((1.86, 0.08, 0.96), (0, 0.8, 0), DK, bev=0.02), box((1.56, 0.03, 0.66), (0, 0.8, 0), BS, bev=0),
              box((0.5, 0.7, 0.06), (0.3, 1.0, 0), bev=0.01, rot=(0, 0, 0.2)), cyl(0.02, 0.8, (0.3, 1.6, 0), sides=5, slot=DK),
              box((0.9, 0.06, 0.06), (0.3, 2.0, 0), DK, bev=0), cyl(0.04, 2.0, (-0.6, 1.0, 0), sides=6, slot=DK), cyl(0.04, 2.0, (1.2, 1.0, 0), sides=6, slot=DK),
              box((1.9, 0.06, 0.06), (0.3, 2.0, 0), DK, bev=0), cyl(0.07, 0.4, (-0.98, 0.2, 0.2), axis="x", sides=8, slot=AC),
              cyl(0.1, 0.06, (-1.16, 0.2, 0.2), axis="x", sides=8, slot=AC)]
    build("SR_Quench", quench)


def lobby():
    # Wall pieces are 6 m wide. The inside of the building is toward +z.
    # No two faces of a piece lie in the same plane, or they would flicker: the posts stand higher
    # than the cap, the cap sits between them, and the foot is a little deeper than the posts. Where
    # two pieces meet at an angle, a pillar of its own covers the joint (RH_Pillar).
    build("RH_Wall", [
        box((5.9, 5.0, 0.5), (0, 2.5, 0), bev=0.04), box((6.0, 0.5, 0.86), (0, 0.25, 0), DK, bev=0.05),
        box((0.4, 5.34, 0.8), (2.8, 2.67, 0), DK), box((0.4, 5.34, 0.8), (-2.8, 2.67, 0), DK),
        box((5.2, 0.4, 0.9), (0, 5.0, 0), AC), box((1.6, 0.9, 0.1), (0, 3.4, 0.28), GL, bev=0),
        box((1.8, 1.1, 0.08), (0, 3.4, 0.25), DK), cyl(0.08, 6.0, (0, 1.6, 0.36), axis="x", sides=8, slot=DK),
        cyl(0.06, 6.0, (0, 1.85, 0.36), axis="x", sides=8, slot=AC), box((1.6, 1.2, 0.08), (1.7, 2.5, 0.27), AC),
        box((1.6, 1.2, 0.08), (-1.7, 2.5, 0.27), AC), box((0.5, 0.12, 0.2), (0, 4.4, 0.35), DK),
        box((0.4, 0.05, 0.1), (0, 4.33, 0.38), GL, bev=0), cyl(0.12, 0.12, (-1.2, 1.6, 0.4), axis="x", sides=8, slot=AC),
        cyl(0.12, 0.12, (1.2, 1.85, 0.4), axis="x", sides=8, slot=DK), box((0.3, 0.4, 0.14), (2.2, 1.0, 0.3), DK),
    ])
    # The pillar that stands where two wall pieces meet. It is thicker and higher than both, so
    # the ends of the pieces, which cannot fit each other at an angle, disappear inside it.
    build("RH_Pillar", [
        box((1.05, 5.7, 1.05), (0, 2.85, 0), DK, bev=0.07), box((1.3, 0.62, 1.3), (0, 0.31, 0), AC, bev=0.06),
        box((1.24, 0.3, 1.24), (0, 5.72, 0), AC, bev=0.05), box((0.9, 0.16, 0.9), (0, 5.95, 0), DK, bev=0.04),
        box((1.12, 0.14, 1.12), (0, 1.9, 0), bev=0.02), box((1.12, 0.14, 1.12), (0, 3.9, 0), bev=0.02),
        box((0.16, 1.7, 0.06), (0, 2.9, 0.54), GL, bev=0), box((0.3, 0.3, 0.1), (0, 4.6, 0.54), AC, bev=0.03),
        cyl(0.07, 5.0, (0.36, 2.9, 0.5), sides=6, slot=AC), cyl(0.1, 0.1, (0.36, 1.2, 0.5), sides=6, slot=DK),
        cyl(0.1, 0.1, (0.36, 4.4, 0.5), sides=6, slot=DK),
    ])
    # The same wall and the same pillar cut off at the height of a railing. They stand on the side
    # the camera looks in from, so that the building is closed all the way round and can still be
    # looked into. The dark slab on top is the cut.
    low = [box((5.9, 1.0, 0.5), (0, 0.5, 0), bev=0.03), box((6.0, 0.5, 0.86), (0, 0.25, 0), DK, bev=0.05),
           box((5.86, 0.06, 0.44), (0, 1.03, 0), DK, bev=0), box((0.4, 1.3, 0.8), (2.8, 0.65, 0), DK), box((0.4, 1.3, 0.8), (-2.8, 0.65, 0), DK),
           cyl(0.06, 5.2, (0, 0.76, 0.3), axis="x", sides=8, slot=AC), cyl(0.045, 5.2, (0, 1.42, 0), axis="x", sides=6, slot=AC),
           box((1.5, 0.3, 0.06), (1.4, 0.74, -0.27), AC, bev=0.01), box((1.5, 0.3, 0.06), (-1.4, 0.74, -0.27), AC, bev=0.01)]
    for x in (-1.9, -0.65, 0.65, 1.9):
        low += [box((0.1, 0.42, 0.1), (x, 1.24, 0), DK, bev=0.01), box((0.3, 0.06, 0.52), (x, 1.01, 0), AC, bev=0)]
    build("RH_WallLow", low)
    build("RH_PillarLow", [
        box((1.05, 1.55, 1.05), (0, 0.775, 0), DK, bev=0.07), box((1.3, 0.62, 1.3), (0, 0.31, 0), AC, bev=0.06),
        box((1.2, 0.14, 1.2), (0, 1.6, 0), AC, bev=0.04), box((0.84, 0.05, 0.84), (0, 1.7, 0), DK, bev=0),
        box((0.16, 0.5, 0.06), (0, 1.1, 0.54), GL, bev=0), ball(0.1, (0, 1.82, 0), GL),
    ])

    # The display that stands at the turntable: a screen on a stalk, with a board of keys under it.
    display = [cyl(0.44, 0.14, (0, 0.07, 0), sides=10, slot=DK), cyl(0.3, 0.1, (0, 0.19, 0), sides=10, slot=AC),
               cyl(0.11, 1.0, (0, 0.7, 0), sides=8, slot=DK), box((0.36, 0.3, 0.3), (0, 0.5, 0), AC, bev=0.04),
               box((1.46, 0.96, 0.12), (0, 1.62, 0.02), DK, bev=0.04, rot=(-0.24, 0, 0)),
               box((1.28, 0.78, 0.03), (0, 1.62, 0.09), GL, bev=0, rot=(-0.24, 0, 0)),
               box((1.5, 0.1, 0.18), (0, 2.12, -0.1), AC, bev=0.02), box((0.1, 1.0, 0.16), (0.76, 1.62, 0.0), AC, bev=0.02, rot=(-0.24, 0, 0)),
               box((0.1, 1.0, 0.16), (-0.76, 1.62, 0.0), AC, bev=0.02, rot=(-0.24, 0, 0)),
               box((1.1, 0.07, 0.4), (0, 1.04, 0.3), AC, bev=0.02, rot=(0.5, 0, 0)), box((0.3, 0.2, 0.3), (0, 0.96, 0.08), DK, bev=0.03),
               cyl(0.02, 0.5, (0.6, 2.4, -0.1), sides=5, slot=DK), ball(0.05, (0.6, 2.66, -0.1), GL),
               cyl(0.03, 0.6, (-0.3, 0.6, -0.2), sides=5, slot=AC, rot=(0.4, 0, 0))]
    for col in range(6):
        for row in range(2):
            display.append(box((0.12, 0.04, 0.1), (-0.4 + col * 0.16, 1.08 + row * 0.06 - 0.03, 0.26 + row * 0.11), GL if (col + row) % 3 == 0 else BS, bev=0, rot=(0.5, 0, 0)))
    build("RH_Display", display)

    door = [box((6.0, 0.8, 0.9), (0, 4.6, 0), bev=0.06), box((4.6, 4.2, 0.2), (0, 2.1, -0.1), AC, bev=0.03),
            box((1.6, 0.5, 0.1), (0, 4.6, 0.48), DK), ball(0.14, (2.65, 4.0, 0.5), GL), ball(0.14, (-2.65, 4.0, 0.5), GL),
            box((0.7, 5.2, 0.9), (2.65, 2.6, 0), DK, bev=0.06), box((0.7, 5.2, 0.9), (-2.65, 2.6, 0), DK, bev=0.06),
            box((1.0, 0.3, 1.1), (2.65, 0.15, 0), AC), box((1.0, 0.3, 1.1), (-2.65, 0.15, 0), AC)]
    for i in range(6):
        door.append(box((4.6, 0.08, 0.26), (0, 0.5 + i * 0.65, -0.1), DK, bev=0))
    build("RH_BayDoor", door)

    # A bay door that can be bought open, in pieces: the frame that stays, the shutter that rolls up
    # into the lintel (its origin is its top edge, so the game can shorten it from below), and one
    # arm of the lock, which swings up on a hinge at the post (its origin is that hinge).
    frame = [box((6.0, 0.8, 0.9), (0, 4.6, 0), bev=0.06), box((1.6, 0.5, 0.1), (0, 4.6, 0.48), DK),
             ball(0.14, (2.65, 4.0, 0.5), GL), ball(0.14, (-2.65, 4.0, 0.5), GL),
             box((0.7, 5.2, 0.9), (2.65, 2.6, 0), DK, bev=0.06), box((0.7, 5.2, 0.9), (-2.65, 2.6, 0), DK, bev=0.06),
             box((1.0, 0.3, 1.1), (2.65, 0.15, 0), AC), box((1.0, 0.3, 1.1), (-2.65, 0.15, 0), AC),
             box((4.6, 0.06, 1.2), (0, 0.03, 0), DK, bev=0), box((4.7, 0.3, 0.5), (0, 4.25, -0.05), DK, bev=0.04),
             box((0.3, 0.3, 0.3), (2.3, 2.1, 0.32), AC, bev=0.04), box((0.3, 0.3, 0.3), (-2.3, 2.1, 0.32), AC, bev=0.04),
             box((0.5, 0.12, 0.06), (0, 4.62, 0.54), GL, bev=0)]
    for x in (-1.8, -0.6, 0.6, 1.8):
        frame.append(box((0.5, 0.05, 0.5), (x, 0.07, 0.3), AC, bev=0, rot=(0, 0, 0.785)))
    build("RH_DoorFrame", frame)
    shutter = [box((4.6, 4.2, 0.2), (0, -2.1, -0.1), AC, bev=0.03), box((4.6, 0.3, 0.26), (0, -4.05, -0.1), DK, bev=0.03),
               box((0.7, 0.08, 0.06), (0, -3.3, 0.04), DK, bev=0)]
    for i in range(6):
        shutter.append(box((4.6, 0.08, 0.26), (0, -3.7 + i * 0.65, -0.1), DK, bev=0))
    build("RH_Shutter", shutter)
    build("RH_LockBar", [
        box((2.2, 0.3, 0.24), (1.1, 0, 0.1), DK, bev=0.04), box((0.2, 0.36, 0.3), (0.5, 0, 0.1), AC, bev=0.03),
        box((0.2, 0.36, 0.3), (1.5, 0, 0.1), AC, bev=0.03), box((0.3, 0.2, 0.08), (2.0, 0, 0.24), GL, bev=0),
        cyl(0.2, 0.34, (0, 0, 0.1), axis="z", sides=8, slot=AC),
    ])

    # The turntable is two pieces. The ring stays where it is; the deck in its middle turns, with
    # the machine on it. The rails on the deck line up with the stubs on the ring when it faces the gate.
    table = [cyl(4.0, 0.12, (0, 0.06, 0), sides=28, slot=DK), cyl(3.7, 0.06, (0, 0.15, 0), sides=28),
             cyl(2.85, 0.05, (0, 0.2, 0), sides=28, slot=DK)]
    for z in (-3.25, 3.25):
        table += [box((0.14, 0.1, 0.8), (0.75, 0.22, z), AC), box((0.14, 0.1, 0.8), (-0.75, 0.22, z), AC),
                  box((1.7, 0.05, 0.16), (0, 0.19, z), DK, bev=0)]
    table += around(12, 3.85, lambda x, z, a: box((0.2, 0.06, 0.2), (x, 0.16, z), GL, bev=0))
    table += around(16, 3.2, lambda x, z, a: cyl(0.06, 0.04, (x, 0.19, z), sides=6, slot=DK), start=0.2)
    # A plate under each of the four arms.
    for degrees in (35, 145, 215, 325):
        a = math.radians(degrees)
        table.append(box((1.1, 0.06, 1.1), (3.3 * math.cos(a), 0.2, 3.3 * math.sin(a)), AC, bev=0.02, rot=(0, 0, -a)))
    build("Turntable", table)

    deck = [cyl(2.7, 0.08, (0, 0.04, 0), sides=28), cyl(2.72, 0.03, (0, 0.015, 0), sides=28, slot=DK),
            box((0.14, 0.1, 5.3), (0.75, 0.11, 0), AC), box((0.14, 0.1, 5.3), (-0.75, 0.11, 0), AC),
            cyl(0.6, 0.05, (0, 0.1, 0), sides=12, slot=DK)]
    for z in (-2.2, -1.1, 0.0, 1.1, 2.2):
        deck.append(box((1.7, 0.05, 0.16), (0, 0.09, z), DK, bev=0))
    # Marks along the rim, so that the turning shows.
    deck += around(8, 2.45, lambda x, z, a: box((0.5, 0.03, 0.14), (x, 0.09, z), GL if a < 0.1 else DK, bev=0, rot=(0, 0, -a)))
    deck += around(4, 1.9, lambda x, z, a: box((0.6, 0.03, 0.6), (x, 0.085, z), DK, bev=0.02, rot=(0, 0, -a)), start=math.pi / 4)
    build("TurntableDeck", deck)

    # A robot arm in four pieces, each with its origin at the joint it turns on. The upper arm and
    # the forearm lie along +Z; the game turns them. Shoulder 1.0 above the floor, upper arm 2.0 long,
    # forearm 1.6, tool 0.4.
    build("Arm_Base", [
        cyl(0.46, 0.2, (0, 0.1, 0), sides=10, slot=DK), cyl(0.3, 0.5, (0, 0.45, 0), sides=10),
        cyl(0.36, 0.08, (0, 0.72, 0), sides=10, slot=AC), box((0.2, 0.2, 0.3), (0, 0.4, -0.36), DK, bev=0.03),
        cyl(0.04, 0.5, (0.22, 0.45, -0.3), sides=5, slot=DK), ball(0.05, (0.22, 0.72, -0.3), GL),
    ])
    build("Arm_Turret", [
        cyl(0.32, 0.12, (0, -0.2, 0), sides=10, slot=DK), box((0.5, 0.42, 0.56), (0, 0, 0), bev=0.06),
        cyl(0.22, 0.62, (0, 0, 0), axis="x", sides=10, slot=DK), box((0.3, 0.3, 0.4), (0, 0.02, -0.4), AC, bev=0.05),
        box((0.1, 0.06, 0.04), (0.14, 0.14, 0.29), GL, bev=0),
    ])
    build("Arm_Upper", [
        box((0.2, 0.26, 2.0), (0, 0, 1.0), bev=0.04), box((0.26, 0.1, 1.3), (0, 0.16, 0.95), AC, bev=0.02),
        cyl(0.17, 0.4, (0, 0, 2.0), axis="x", sides=10, slot=DK), cyl(0.045, 1.1, (0.13, -0.14, 0.9), axis="z", sides=6, slot=DK),
        cyl(0.03, 0.5, (0.13, -0.14, 1.6), axis="z", sides=6, slot=AC), box((0.24, 0.3, 0.2), (0, 0, 0.35), DK, bev=0.03),
        box((0.06, 0.04, 0.5), (-0.12, 0.05, 1.3), GL, bev=0),
    ])
    build("Arm_Fore", [
        box((0.15, 0.18, 1.6), (0, 0, 0.8), AC, bev=0.03), box((0.19, 0.22, 0.34), (0, 0, 0.3), bev=0.03),
        cyl(0.11, 0.3, (0, 0, 1.6), axis="x", sides=8, slot=DK), cyl(0.025, 1.2, (-0.1, 0.1, 0.8), axis="z", sides=5, slot=DK),
        box((0.17, 0.05, 0.3), (0, 0.11, 1.15), DK, bev=0),
    ])
    build("Arm_Welder", [
        box((0.14, 0.14, 0.16), (0, 0, 0.08), DK, bev=0.02), cyl(0.07, 0.18, (0, 0, 0.24), axis="z", sides=8),
        cyl(0.05, 0.1, (0, 0, 0.36), axis="z", sides=8, slot=DK, end=0.02), ball(0.035, (0, 0, 0.4), GL),
        cyl(0.02, 0.2, (0.08, 0.05, 0.14), axis="z", sides=5, slot=AC),
    ])
    build("Arm_Claw", [
        box((0.16, 0.14, 0.14), (0, 0, 0.07), DK, bev=0.02), cyl(0.06, 0.1, (0, 0, 0.18), axis="z", sides=8, slot=AC),
        box((0.04, 0.05, 0.24), (0.07, 0, 0.3), bev=0.01, rot=(0, 0, 0.25)), box((0.04, 0.05, 0.24), (-0.07, 0, 0.3), bev=0.01, rot=(0, 0, -0.25)),
        box((0.05, 0.16, 0.05), (0, 0, 0.2), DK, bev=0),
    ])

    # The rack next to the Smelter's furnace: a gantry with a bare frame hanging from it, and plates
    # stacked against its foot. This is where frames are forged.
    rack = [box((0.22, 3.2, 0.22), (-1.4, 1.6, 0), DK, bev=0.03), box((0.22, 3.2, 0.22), (1.4, 1.6, 0), DK, bev=0.03),
            box((3.3, 0.24, 0.3), (0, 3.26, 0), AC, bev=0.04), box((0.6, 0.1, 0.9), (-1.4, 0.05, 0), DK), box((0.6, 0.1, 0.9), (1.4, 0.05, 0), DK),
            box((0.3, 0.2, 0.36), (0, 3.04, 0), DK, bev=0.03), cyl(0.02, 1.0, (-0.55, 2.45, 0), sides=5, slot=DK),
            cyl(0.02, 1.0, (0.55, 2.45, 0), sides=5, slot=DK),
            # the frame that hangs
            box((0.1, 0.12, 1.9), (-0.55, 1.9, 0), bev=0.02), box((0.1, 0.12, 1.9), (0.55, 1.9, 0), bev=0.02),
            box((1.2, 0.1, 0.1), (0, 1.9, -0.8), bev=0.02), box((1.2, 0.1, 0.1), (0, 1.9, 0.8), bev=0.02), box((1.2, 0.1, 0.1), (0, 1.9, 0), AC, bev=0.02),
            cyl(0.1, 0.06, (0, 1.98, 0.4), sides=8, slot=DK), cyl(0.1, 0.06, (0, 1.98, -0.4), sides=8, slot=DK),
            # plates and a second frame on the floor
            box((1.3, 0.06, 0.8), (0.2, 0.14, 0.5), AC, bev=0.01), box((1.2, 0.06, 0.8), (0.25, 0.21, 0.52), bev=0.01),
            box((1.1, 0.06, 0.7), (0.15, 0.28, 0.46), AC, bev=0.01), box((0.08, 1.5, 0.1), (-0.9, 0.75, -0.3), bev=0.02, rot=(0.25, 0, 0)),
            box((0.08, 1.5, 0.1), (-0.5, 0.75, -0.3), bev=0.02, rot=(0.25, 0, 0)),
            ball(0.07, (1.4, 3.3, 0.2), GL), ball(0.07, (-1.4, 3.3, 0.2), GL)]
    build("FrameRack", rack)

    bench = [box((2.2, 0.1, 0.9), (0, 0.9, 0), AC, bev=0.03), box((2.0, 0.06, 0.7), (0, 0.3, 0), DK),
             box((0.2, 0.2, 0.3), (-0.8, 1.05, 0.2), DK), box((0.5, 0.25, 0.3), (0.6, 1.08, 0), bev=0.03),
             box((0.3, 0.04, 0.04), (0.6, 1.24, 0), DK, bev=0), box((2.2, 1.0, 0.06), (0, 1.6, -0.42), DK),
             cyl(0.02, 0.5, (0.95, 1.2, -0.3), sides=5, slot=DK), ball(0.07, (0.95, 1.48, -0.3), GL),
             cyl(0.1, 0.16, (-0.2, 1.03, -0.1), sides=8), box((0.4, 0.03, 0.25), (0.05, 0.97, 0.2), DK, bev=0)]
    for x, z in ((1.0, 0.38), (-1.0, 0.38), (1.0, -0.38), (-1.0, -0.38)):
        bench.append(box((0.1, 0.9, 0.1), (x, 0.45, z), DK))
    for i in range(5):
        bench.append(box((0.06, 0.3 + 0.08 * (i % 3), 0.04), (-0.8 + i * 0.35, 1.6, -0.38), AC, bev=0))
    build("Workbench", bench)

    shelf = [box((2.2, 2.6, 0.5), (0, 1.3, 0), DK, bev=0.04), box((0.9, 0.6, 0.4), (0, 0.9, 0.42), bev=0.04),
             box((0.7, 0.4, 0.03), (0, 0.98, 0.63), GL, bev=0), box((0.7, 0.06, 0.2), (0, 0.66, 0.6), DK)]
    for row in range(3):
        y = 1.55 + row * 0.42
        shelf.append(box((2.0, 0.05, 0.48), (0, y - 0.16, 0.02), AC, bev=0))
        for i in range(9):
            tall = 0.2 + 0.08 * ((i * 3 + row) % 3)
            shelf.append(box((0.14, tall, 0.3), (-0.85 + i * 0.21, y - 0.13 + tall / 2, 0.08), BS if (i + row) % 3 else AC, bev=0.01))
    build("Shelf", shelf)

    build("Furnace", [
        box((2.0, 1.8, 1.6), (0, 0.9, 0), DK, bev=0.08), box((0.9, 0.8, 0.1), (0, 0.8, 0.82), AC),
        box((0.7, 0.6, 0.06), (0, 0.8, 0.86), GL, bev=0), cyl(0.3, 2.4, (0.5, 3.1, -0.3), sides=10, slot=DK),
        cyl(0.7, 0.6, (0.5, 2.1, -0.3), sides=10, slot=DK, end=0.3), cyl(0.34, 0.1, (0.5, 4.3, -0.3), sides=10, slot=AC),
        cyl(0.1, 1.2, (-1.3, 1.2, 0), axis="x", sides=8, slot=AC), cyl(0.1, 1.0, (-1.8, 0.7, 0), sides=8, slot=AC),
        cyl(0.25, 0.3, (-1.4, 0.15, 0.7), sides=10, end=0.3), cyl(0.2, 0.04, (-1.4, 0.3, 0.7), sides=10, slot=GL),
        box((0.3, 0.1, 0.14), (1.3, 0.05, 0.6)), box((0.3, 0.1, 0.14), (1.3, 0.05, 0.8)), box((0.3, 0.1, 0.14), (1.3, 0.15, 0.7)),
        box((2.1, 0.12, 1.7), (0, 1.86, 0), AC), box((0.1, 0.1, 0.02), (0.7, 1.4, 0.81), GL, bev=0),
        box((0.1, 0.1, 0.02), (0.5, 1.4, 0.81), GL, bev=0),
    ])
    build("Crate", [box((0.8, 0.8, 0.8), (0, 0.4, 0), AC, bev=0.04), box((0.84, 0.1, 0.84), (0, 0.2, 0), DK),
                    box((0.84, 0.1, 0.84), (0, 0.6, 0), DK), box((0.1, 0.84, 0.84), (0, 0.4, 0), DK)])
    build("Barrel", [cyl(0.3, 0.8, (0, 0.4, 0), sides=12, slot=AC), cyl(0.315, 0.05, (0, 0.15, 0), sides=12, slot=DK),
                     cyl(0.315, 0.05, (0, 0.65, 0), sides=12, slot=DK), cyl(0.24, 0.03, (0, 0.81, 0), sides=12, slot=DK),
                     cyl(0.04, 0.04, (0.12, 0.84, 0), sides=6)])
    build("Lamp", [cyl(0.22, 0.1, (0, 0.05, 0), sides=10, slot=DK), cyl(0.04, 2.6, (0, 1.4, 0), sides=6, slot=DK),
                   box((0.32, 0.2, 0.32), (0, 2.75, 0), DK, bev=0.04), ball(0.14, (0, 2.6, 0), GL),
                   box((0.4, 0.04, 0.4), (0, 2.87, 0), AC)])

    # The three machines that wake first. Their glow slot is their status lamp.
    build("NPC_Fitter", [
        box((0.9, 0.3, 1.0), (0, 0.15, 0), DK, bev=0.05), box((0.7, 0.7, 0.6), (0, 0.72, 0), bev=0.06),
        box((0.5, 0.3, 0.4), (0, 1.25, 0.05), AC, bev=0.05), box((0.1, 0.08, 0.03), (0.12, 1.27, 0.26), GL, bev=0),
        box((0.1, 0.08, 0.03), (-0.12, 1.27, 0.26), GL, bev=0), ball(0.08, (0, 1.5, 0), GL),
        cyl(0.06, 1.0, (0.5, 1.2, 0.2), sides=6, slot=DK, rot=(0.9, 0, 0.3)), box((0.2, 0.14, 0.24), (0.62, 1.5, 0.62), AC),
        box((0.14, 0.5, 0.16), (-0.46, 0.7, 0.1), DK), box((0.18, 0.16, 0.26), (-0.46, 0.42, 0.18), AC),
        box((0.74, 0.1, 0.64), (0, 0.42, 0), DK), box((0.3, 0.2, 0.06), (0, 0.8, 0.31), DK),
        cyl(0.16, 0.92, (0, 0.16, 0.42), axis="x", sides=8, slot=DK), cyl(0.16, 0.92, (0, 0.16, -0.42), axis="x", sides=8, slot=DK),
    ])
    build("NPC_Archivist", [
        cyl(0.35, 0.2, (0, 0.1, 0), sides=10, slot=DK), cyl(0.16, 1.4, (0, 0.9, 0), sides=8),
        box((0.6, 0.14, 0.3), (0, 1.55, 0), AC, bev=0.04), ball(0.22, (0, 1.85, 0), BS, sub=2),
        cyl(0.1, 0.12, (0, 1.85, 0.22), axis="z", sides=10, slot=DK), cyl(0.07, 0.04, (0, 1.85, 0.29), axis="z", sides=10, slot=GL),
        cyl(0.03, 0.8, (0.3, 1.15, 0.05), sides=5, slot=DK), cyl(0.03, 0.8, (-0.3, 1.15, 0.05), sides=5, slot=DK),
        box((0.3, 0.04, 0.22), (0, 0.78, 0.22), AC, bev=0), box((0.28, 0.04, 0.2), (0, 0.83, 0.22), BS, bev=0),
        box((0.26, 0.04, 0.2), (0, 0.88, 0.22), AC, bev=0), cyl(0.18, 0.05, (0, 0.5, 0), sides=8, slot=AC),
        cyl(0.18, 0.05, (0, 1.2, 0), sides=8, slot=AC), ball(0.05, (0.14, 2.06, -0.05), GL),
    ])
    build("NPC_Smelter", [
        cyl(0.6, 0.9, (0, 0.6, 0), sides=10, slot=DK, end=0.5), box((0.4, 0.3, 0.06), (0, 0.6, 0.55), GL, bev=0),
        box((0.5, 0.4, 0.06), (0, 0.6, 0.52), AC), cyl(0.3, 0.25, (0, 1.2, 0), sides=10),
        box((0.34, 0.07, 0.04), (0, 1.22, 0.29), GL, bev=0), cyl(0.1, 0.5, (0.25, 1.6, -0.1), sides=8, slot=DK),
        cyl(0.12, 0.06, (0.25, 1.86, -0.1), sides=8, slot=AC), box((0.3, 0.5, 0.3), (0.72, 0.7, 0.1), AC, bev=0.05),
        box((0.3, 0.5, 0.3), (-0.72, 0.7, 0.1), AC, bev=0.05), box((0.34, 0.24, 0.4), (0.72, 0.34, 0.2), DK, bev=0.05),
        box((0.34, 0.24, 0.4), (-0.72, 0.34, 0.2), DK, bev=0.05), cyl(0.66, 0.14, (0, 0.14, 0), sides=10, slot=AC),
        cyl(0.52, 0.06, (0, 1.06, 0), sides=10, slot=AC),
    ])


# ---------------------------------------------------------------- round 11: weapons, an Echo, things to find

def finds():
    # Mortar: a short fat tube leaning forward between two cheeks, with shells standing by.
    lean = (math.radians(35), 0.0, 0.0)
    build("Mortar", mount_ring() + [
        box((0.5, 0.16, 0.5), (0, 0.2, 0), bev=0.04),
        box((0.1, 0.3, 0.3), (0.22, 0.38, 0.02), DK), box((0.1, 0.3, 0.3), (-0.22, 0.38, 0.02), DK),
        cyl(0.13, 0.72, (0, 0.56, 0.12), sides=10, slot=DK, rot=lean),
        cyl(0.158, 0.09, (0, 0.8, 0.29), sides=10, slot=AC, rot=lean),
        cyl(0.158, 0.12, (0, 0.33, -0.04), sides=10, slot=AC, rot=lean),
        cyl(0.05, 0.2, (0.3, 0.38, -0.2), sides=6, slot=AC), cyl(0.05, 0.08, (0.3, 0.52, -0.2), sides=6, slot=DK, end=0.0),
        cyl(0.05, 0.2, (-0.3, 0.38, -0.2), sides=6, slot=AC), cyl(0.05, 0.08, (-0.3, 0.52, -0.2), sides=6, slot=DK, end=0.0),
        box((0.1, 0.06, 0.04), (0, 0.3, 0.27), GL, bev=0),
    ])

    # Mine Layer: a hopper with two mines on top and a chute down its back.
    build("MineLayer", mount_ring() + [
        box((0.5, 0.3, 0.46), (0, 0.27, 0.06), bev=0.04),
        box((0.36, 0.05, 0.5), (0, 0.2, -0.4), DK, bev=0.01, rot=(math.radians(-14), 0, 0)),
        box((0.04, 0.12, 0.5), (0.2, 0.24, -0.4), AC, bev=0.01, rot=(math.radians(-14), 0, 0)),
        box((0.04, 0.12, 0.5), (-0.2, 0.24, -0.4), AC, bev=0.01, rot=(math.radians(-14), 0, 0)),
        cyl(0.16, 0.07, (0, 0.46, 0.06), sides=10, slot=DK), cyl(0.16, 0.07, (0, 0.54, 0.06), sides=10, slot=AC),
        cyl(0.05, 0.03, (0, 0.59, 0.06), sides=6, slot=GL),
        cyl(0.13, 0.06, (0, 0.27, -0.4), sides=10, slot=DK),
        box((0.1, 0.08, 0.04), (0.16, 0.3, 0.3), GL, bev=0),
    ])

    # A mine as it lies on the ground: a low drum with a lamp on top and four prongs.
    build("Mine", [
        cyl(0.34, 0.08, (0, 0.04, 0), sides=10, slot=DK), cyl(0.28, 0.12, (0, 0.14, 0), sides=10, end=0.2),
        torus(0.29, 0.03, (0, 0.1, 0), AC), cyl(0.08, 0.07, (0, 0.23, 0), sides=6, slot=GL),
    ] + around(4, 0.22, lambda x, z, a: cyl(0.025, 0.14, (x, 0.2, z), sides=5, slot=DK), start=math.pi / 4))

    # Saw Ram: a toothed blade lying flat on an arm ahead of its motor, so that it reads from above.
    saw = mount_ring() + [
        box((0.4, 0.24, 0.44), (0, 0.26, -0.12), bev=0.04),
        box((0.18, 0.1, 0.66), (0, 0.27, 0.36), DK),
        cyl(0.47, 0.04, (0, 0.36, 0.78), sides=16, slot=AC),
        cyl(0.14, 0.12, (0, 0.38, 0.78), sides=8, slot=DK), cyl(0.06, 0.05, (0, 0.46, 0.78), sides=6, slot=GL),
        box((0.34, 0.12, 0.3), (0, 0.44, -0.12), DK), box((0.08, 0.06, 0.04), (0.12, 0.47, 0.05), GL, bev=0),
    ]
    saw += around(12, 0.5, lambda x, z, a: box((0.13, 0.04, 0.09), (x, 0.36, 0.78 + z), AC, bev=0.01, rot=(0, 0, -a + 0.5)))
    build("SawRam", saw)

    # Railgun: two long rails with a line of light between them, held by four brackets.
    rail = mount_ring() + [
        box((0.36, 0.26, 0.6), (0, 0.27, -0.14), bev=0.04), box((0.3, 0.14, 0.22), (0, 0.46, -0.26), DK),
        box((0.06, 0.1, 1.5), (0.1, 0.3, 0.9), DK, bev=0.015), box((0.06, 0.1, 1.5), (-0.1, 0.3, 0.9), DK, bev=0.015),
        box((0.08, 0.03, 1.42), (0, 0.3, 0.9), GL, bev=0),
        box((0.12, 0.2, 0.3), (0.26, 0.26, -0.12), AC), box((0.12, 0.2, 0.3), (-0.26, 0.26, -0.12), AC),
        box((0.08, 0.06, 0.04), (0, 0.52, -0.14), GL, bev=0),
    ]
    for z in (0.34, 0.74, 1.14, 1.52):
        rail += [box((0.34, 0.05, 0.07), (0, 0.39, z), AC, bev=0.01), box((0.34, 0.05, 0.07), (0, 0.21, z), AC, bev=0.01),
                 box((0.05, 0.2, 0.07), (0.17, 0.3, z), AC, bev=0.01), box((0.05, 0.2, 0.07), (-0.17, 0.3, z), AC, bev=0.01)]
    build("Railgun", rail)

    # An Echo: a shard of the Pulse, and the same shard twice more, each smaller. Origin at its lower tip.
    echo = []
    for x, size in ((-0.2, 1.0), (0.16, 0.6), (0.38, 0.36)):
        echo += [cyl(0.17 * size, 0.52 * size, (x, 0.42 + 0.26 * size, 0), sides=6, slot=GL, end=0.0),
                 cyl(0.0, 0.42 * size, (x, 0.42 - 0.21 * size, 0), sides=6, slot=GL, end=0.17 * size)]
    echo.append(box((0.07, 0.07, 0.07), (-0.3, 0.5, 0.1), BS, bev=0))
    build("Echo", echo)

    # A vein: Beats grown out of a lump of rock, large and small, leaning every way.
    vein = [rock(0.55, 0.45, 77, slot=BS, sub=2, rough=0.2), rock(0.3, 0.5, 78, slot=DK, sub=1, at=(0.42, 0.22), rough=0.2)]
    for x, z, height, radius, tip in ((0, 0, 1.15, 0.2, (0.0, 0.0)), (0.3, 0.12, 0.75, 0.14, (0.3, 0.1)), (-0.3, 0.08, 0.85, 0.15, (-0.28, 0.12)),
                                      (0.04, -0.32, 0.62, 0.12, (0.05, -0.34)), (-0.14, 0.34, 0.5, 0.1, (-0.2, 0.3)), (0.3, -0.24, 0.4, 0.09, (0.3, -0.2))):
        vein.append(cyl(radius, height, (x, 0.18 + height / 2, z), sides=4, slot=GL, end=0.0, rot=(tip[1], tip[0], x * 2.0)))
    build("BeatVein", vein)

    # A cache: a strapped crate on a skid, with a lamp in the lock on each long side.
    def crate():
        return [
            box((1.5, 0.8, 1.1), (0, 0.45, 0), bev=0.06), box((1.6, 0.1, 1.2), (0, 0.05, 0), DK),
            box((0.14, 0.9, 1.16), (0.52, 0.47, 0), DK), box((0.14, 0.9, 1.16), (-0.52, 0.47, 0), DK),
        ]
    build("Cache", crate() + [
        box((1.58, 0.26, 1.18), (0, 0.95, 0), AC, bev=0.06),
        box((0.16, 0.3, 1.22), (0.52, 0.95, 0), DK), box((0.16, 0.3, 1.22), (-0.52, 0.95, 0), DK),
        box((0.3, 0.34, 0.12), (0, 0.72, 0.56), DK), box((0.14, 0.14, 0.06), (0, 0.74, 0.63), GL, bev=0.02),
        box((0.3, 0.34, 0.12), (0, 0.72, -0.56), DK), box((0.14, 0.14, 0.06), (0, 0.74, -0.63), GL, bev=0.02),
    ])
    # The same crate opened: dark inside, its lid leaning against its back.
    build("CacheOpen", crate() + [
        box((1.3, 0.05, 0.9), (0, 0.87, 0), DK, bev=0),
        box((1.58, 0.26, 1.18), (0, 0.6, -0.8), AC, bev=0.06, rot=(math.radians(-70), 0, 0)),
    ])


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for name, rgb in (("Base", (0.91, 0.89, 0.85)), ("Accent", (0.6, 0.64, 0.68)), ("Dark", (0.12, 0.14, 0.17)), ("Glow", (0.36, 0.95, 0.63))):
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*rgb, 1.0)
        MATS.append(mat)
    os.makedirs(OUT_DIR, exist_ok=True)

    machine()
    creatures()
    scenery()
    lobby()
    tuning_room()
    rooms()
    finds()

    # Lay the models out in rows so the .blend file is easy to look through.
    for index, obj in enumerate(built):
        obj.location = ((index % 10) * 8.0, (index // 10) * 10.0, 0.0)
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("PULSE_MODELS_BUILT", len(built))


main()
