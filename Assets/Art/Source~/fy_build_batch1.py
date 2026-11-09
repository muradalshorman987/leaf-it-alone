"""Fallen Yard art batch 1: builds every asset in Blender and exports FBX for Unity.

Run inside Blender (Scripting tab, or exec with __file__ set). It clears the
scene only when the open file is untitled or is fy_batch1.blend itself.

Axes: Blender -Y is Unity +Z (forward), Blender +Z is Unity +Y (up).
Units: 1 Blender unit = 1 metre = 1 Unity unit.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector


def script_path():
    # Blender's text editor reports __file__ as a path inside the .blend, so use the text's real path.
    text = bpy.data.texts.get(os.path.basename(__file__))
    return bpy.path.abspath(text.filepath) if text and text.filepath else __file__


HERE = os.path.dirname(os.path.abspath(script_path()))
ART = os.path.dirname(HERE)
BLEND_PATH = os.path.join(HERE, "fy_batch1.blend")
PALETTE_PATH = os.path.join(ART, "Textures", "FY_Palette.png")

PALETTE = [
    ("wood_light", "C79A63"), ("wood_mid", "9A6B3F"), ("wood_dark", "6B4528"), ("soil", "4A3424"),
    ("metal_light", "B8BEC2"), ("metal_dark", "5C6469"), ("rubber", "2B2B2E"), ("cream", "EFE3C8"),
    ("orange", "E08A2E"), ("teal", "3E8E8A"), ("teal_dark", "2C6A67"), ("leaf_gold", "D9A441"),
    ("kraft", "C9A36A"), ("kraft_dark", "A8814C"), ("green", "4F7A3F"), ("green_dark", "365A2E"),
]
GRID = 4
SWATCH_PX = 16


def swatch_uv(name):
    index = next(i for i, (n, _) in enumerate(PALETTE) if n == name)
    return ((index % GRID + 0.5) / GRID, 1.0 - (index // GRID + 0.5) / GRID)


def make_palette_image():
    size = GRID * SWATCH_PX
    image = bpy.data.images.get("FY_Palette") or bpy.data.images.new("FY_Palette", size, size, alpha=False)
    pixels = [0.0] * (size * size * 4)
    for i, (_, hex_value) in enumerate(PALETTE):
        rgb = [int(hex_value[c:c + 2], 16) / 255.0 for c in (0, 2, 4)]
        col, row = i % GRID, i // GRID
        for y in range(size - (row + 1) * SWATCH_PX, size - row * SWATCH_PX):
            for x in range(col * SWATCH_PX, (col + 1) * SWATCH_PX):
                p = (y * size + x) * 4
                pixels[p:p + 4] = [*rgb, 1.0]
    image.pixels = pixels
    image.filepath_raw = PALETTE_PATH
    image.file_format = "PNG"
    image.save()
    return image


def make_palette_material(image):
    mat = bpy.data.materials.get("FY_Palette") or bpy.data.materials.new("FY_Palette")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def make_leaf_material():
    mat = bpy.data.materials.get("FY_Leaf") or bpy.data.materials.new("FY_Leaf")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.70, 0.37, 0.05, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


class Builder:
    """Collects closed shells in one bmesh, colouring each face from the palette."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _paint(self, face, color):
        uv = swatch_uv(color)
        for loop in face.loops:
            loop[self.uv].uv = uv

    def face(self, verts, color):
        face = self.bm.faces.new(verts)
        self._paint(face, color)
        return face

    def ring(self, points):
        return [self.bm.verts.new(p) for p in points]

    def bridge(self, a, b, color):
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            self.face((a[i], a[j], b[j], b[i]), color)

    def loft(self, rings, colors, cap_start=True, cap_end=True):
        """Join rings of equal size. colors has one entry per band."""
        made = [self.ring(r) for r in rings]
        for i in range(len(made) - 1):
            self.bridge(made[i], made[i + 1], colors[i])
        if cap_start:
            self.face(list(reversed(made[0])), colors[0])
        if cap_end:
            self.face(made[-1], colors[-1])
        return made

    def tube(self, path, radii, sides, color):
        """Sweep a regular polygon along a path, with no twist between rings."""
        path = [Vector(p) for p in path]
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(path)
        rings = []
        u = None
        for i, p in enumerate(path):
            t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
            if u is None:
                ref = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 0, 1))
                u = ref.cross(t).normalized()
            else:
                u = (u - t * u.dot(t)).normalized()
            v = t.cross(u)
            rings.append([
                p + (u * math.cos(a) + v * math.sin(a)) * radii[i]
                for a in (2 * math.pi * s / sides for s in range(sides))
            ])
        return self.loft(rings, [color] * (len(path) - 1))

    def box(self, center, size, color):
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        low = [(cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz),
               (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz)]
        high = [(x, y, cz + hz) for x, y, _ in low]
        return self.loft([low, high], [color])

    def to_object(self, name, material, origin=(0, 0, 0), recalc=True):
        if recalc:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        offset = Vector(origin)
        for vert in self.bm.verts:
            vert.co -= offset
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = offset
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ------------------------------------------------------------------ leaves
# Outlines are (x, v) with v = +0.5 at the tip and -0.5 at the stem, matching
# LeafMeshFactory: 1 unit long, pivot at the centre, Unity +Z towards the tip.

def fold(x, v):
    return 0.035 * (1.0 - min(abs(x) / 0.5, 1.0)) - 0.03 * max(v, 0.0)


LEAF_OUTLINES = {
    "Maple": [(0, .5), (.12, .22), (.46, .30), (.26, 0), (.36, -.32), (0, -.5),
              (-.36, -.32), (-.26, 0), (-.46, .30), (-.12, .22)],
    "Oak": [(.06, .5), (.12, .30), (.31, .24), (.12, .03), (.20, -.20), (0, -.5),
            (-.20, -.20), (-.12, .03), (-.31, .24), (-.12, .30), (-.06, .5)],
    "Birch": [(0, .5), (.17, .27), (.30, .02), (.20, -.29), (0, -.5),
              (-.20, -.29), (-.30, .02), (-.17, .27)],
    "Ginkgo": [(0, -.5), (.13, -.08), (.47, .22), (.33, .44), (.10, .46), (0, .21),
               (-.10, .46), (-.33, .44), (-.47, .22), (-.13, -.08)],
}


def add_double_sided(builder, top_faces, uv_of):
    """Duplicate faces with reversed winding so single-sided URP materials show both sides."""
    for face in list(top_faces):
        verts = [builder.bm.verts.new(v.co) for v in reversed(face.verts)]
        back = builder.bm.faces.new(verts)
        for loop in back.loops:
            loop[builder.uv].uv = uv_of(loop.vert.co)


def planar_uv(co):
    return (co.x + 0.5, -co.y + 0.5)


def build_flat_leaf(name, outline, material):
    b = Builder()
    verts = [b.bm.verts.new((x, -v, fold(x, v))) for x, v in outline]
    # Winding is clockwise seen from above in this list, so reverse it for +Z.
    face = b.bm.faces.new(list(reversed(verts)))
    tris = bmesh.ops.triangulate(b.bm, faces=[face], quad_method="BEAUTY", ngon_method="BEAUTY")["faces"]
    for tri in tris:
        for loop in tri.loops:
            loop[b.uv].uv = planar_uv(loop.vert.co)
    add_double_sided(b, tris, planar_uv)
    return b.to_object(name, material, recalc=False)


def build_pine(name, material):
    b = Builder()
    needles = [(-.17, .46), (-.05, .50), (.08, .49), (.19, .44)]
    tris = []
    for tip_x, tip_v in needles:
        side = Vector((tip_v + .5, -tip_x, 0)).normalized() * .035
        mid = Vector(((tip_x) * .45, -(-.5 + (tip_v + .5) * .45), .01))
        quad = [b.bm.verts.new(p) for p in (
            (0, .5, 0), mid + side, (tip_x, -tip_v, 0), mid - side)]
        # Ensure the diamond faces +Z whichever way the needle leans.
        area = (quad[1].co - quad[0].co).cross(quad[2].co - quad[0].co).z
        face = b.bm.faces.new(quad if area > 0 else list(reversed(quad)))
        tris += bmesh.ops.triangulate(b.bm, faces=[face])["faces"]
    for tri in tris:
        for loop in tri.loops:
            loop[b.uv].uv = planar_uv(loop.vert.co)
    add_double_sided(b, tris, planar_uv)
    return b.to_object(name, material, recalc=False)


def build_wet_clump(name, material):
    """A soggy mound: solid, so it needs no back faces. 14 triangles."""
    b = Builder()
    rim = [(.44, .05, .02), (.30, .38, -.03), (-.02, .50, .03), (-.33, .30, -.02),
           (-.46, -.08, .03), (-.22, -.42, -.03), (.10, -.50, .02), (.38, -.30, -.02)]
    verts = [b.bm.verts.new(p) for p in rim]
    hub = b.bm.verts.new((-.04, -.03, .17))
    tris = []
    for i in range(len(verts)):
        tris.append(b.bm.faces.new((verts[i], verts[(i + 1) % len(verts)], hub)))
    bottom = b.bm.faces.new(list(reversed(verts)))
    tris += bmesh.ops.triangulate(b.bm, faces=[bottom], ngon_method="BEAUTY")["faces"]
    for tri in tris:
        for loop in tri.loops:
            loop[b.uv].uv = planar_uv(loop.vert.co)
    return b.to_object(name, material)


# ------------------------------------------------------------------ tools
# Held tools have their pivot at the main grip so view-models can parent to a hand.

def build_rake(name, material):
    b = Builder()
    b.tube([(0, 0, .30), (0, 0, -.78)], .016, 6, "wood_light")
    b.tube([(0, 0, .30), (0, 0, .33)], [.018, .012], 6, "wood_dark")
    b.tube([(0, 0, .06), (0, 0, -.14)], .0185, 6, "cream")
    b.tube([(0, 0, -.76), (0, 0, -.87)], [.022, .030], 6, "orange")
    count = 11
    for i in range(count):
        s = (i / (count - 1)) * 2 - 1
        b.tube([(s * .04, 0, -.86), (s * .27, 0, -1.22), (s * .285, .07, -1.30)],
               [.008, .007, .005], 4, "metal_light")
    arc = [(s * .19, -.004 * (1 - s * s), -1.10) for s in (-1, -.5, 0, .5, 1)]
    b.tube(arc, .008, 4, "metal_dark")
    return b.to_object(name, material)


def build_blower(name, material):
    b = Builder()
    z = -.14
    b.tube([(0, .08, 0), (0, -.07, 0)], .019, 6, "rubber")
    b.tube([(0, .08, .004), (0, .08, -.08)], .016, 6, "teal_dark")
    b.tube([(0, -.07, .004), (0, -.07, -.08)], .016, 6, "teal_dark")
    b.tube([(0, -.02, -.016), (0, -.02, -.05)], .010, 4, "orange")
    b.tube([(0, .18, z), (0, .14, z), (0, -.04, z), (0, -.08, z)],
           [.085, .11, .11, .07], 10, "teal")
    b.tube([(0, .105, z), (0, .06, z)], .1135, 10, "orange")
    b.tube([(0, .195, z), (0, .178, z)], [.072, .086], 10, "metal_dark")
    b.tube([(0, -.075, z), (0, -.30, -.16), (0, -.62, -.19)], [.056, .042, .030], 8, "cream")
    b.tube([(0, -.60, -.188), (0, -.66, -.194)], [.033, .035], 8, "rubber")
    return b.to_object(name, material)


# ------------------------------------------------------------------ props
# Props have their pivot at the centre of the base, so they sit on the ground at y = 0.

def rounded_rect(width, depth, z, chamfer, jitter=None):
    w, d, c = width / 2, depth / 2, chamfer
    points = [(w - c, -d), (w, -d + c), (w, d - c), (w - c, d),
              (-w + c, d), (-w, d - c), (-w, -d + c), (-w + c, -d)]
    return [(x, y, z + (jitter() if jitter else 0.0)) for x, y in points]


def build_bag(name, material):
    b = Builder()
    rng = random.Random(7)
    wobble = lambda: rng.uniform(-.012, .012)
    width, depth = .46, .32
    rings = [
        rounded_rect(width * .90, depth * .90, 0, .03),
        rounded_rect(width * .97, depth * .97, .05, .04),
        rounded_rect(width * 1.04, depth * 1.04, .40, .05),
        rounded_rect(width * 1.00, depth * 1.00, .66, .04),
        rounded_rect(width * 1.02, depth * 1.02, .70, .04, wobble),
        rounded_rect(width * 1.02, depth * 1.02, .76, .04, wobble),
    ]
    made = b.loft(rings, ["kraft_dark", "kraft", "kraft", "kraft_dark", "cream"], cap_end=False)
    top = made[-1]
    hub = b.bm.verts.new((.02, -.01, .87))
    for i in range(len(top)):
        b.face((top[i], top[(i + 1) % len(top)], hub), "leaf_gold")
    diamond = [(0, .49), (.07, .40), (0, .29), (-.07, .40)]
    b.loft([[(x, -.150, z) for x, z in diamond], [(x, -.1725, z) for x, z in diamond]], ["green"])
    return b.to_object(name, material)


def build_compost_bin(name, material):
    body = Builder()
    size, height = 1.0, .95
    half = size / 2
    post = .08
    inner = half - post / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            body.box((sx * inner, sy * inner, height / 2), (post, post, height), "wood_dark")
    body.box((0, 0, .02), (size - .02, size - .02, .04), "wood_dark")

    slat_h, gap, thick = .13, .035, .025
    span = size - post * 2
    for i in range(5):
        z = .10 + slat_h / 2 + i * (slat_h + gap)
        color = "wood_light" if i % 2 == 0 else "wood_mid"
        body.box((0, inner, z), (span, thick, slat_h), color)
        body.box((inner, 0, z), (thick, span, slat_h), color)
        body.box((-inner, 0, z), (thick, span, slat_h), color)
        if i >= 2:
            body.box((0, -inner, z), (span, thick, slat_h), color)

    hatch_top = .10 + 2 * slat_h + gap
    hatch_z = (.08 + hatch_top) / 2
    body.box((0, -inner - .006, hatch_z), (span, .03, hatch_top - .08), "green")
    body.box((0, -inner - .03, hatch_z), (.18, .025, .03), "metal_dark")

    rail = .05
    body.box((0, inner, height + rail / 2), (size, post, rail), "wood_dark")
    body.box((0, -inner, height + rail / 2), (size, post, rail), "wood_dark")
    body.box((inner, 0, height + rail / 2), (post, span, rail), "wood_dark")
    body.box((-inner, 0, height + rail / 2), (post, span, rail), "wood_dark")

    rim = rounded_rect(span - .02, span - .02, .62, .12)
    made = body.loft([rounded_rect(span - .02, span - .02, .05, .12), rim], ["soil"], cap_end=False)
    hub = body.bm.verts.new((.06, .04, .84))
    colors = ["soil", "leaf_gold", "soil", "soil", "leaf_gold", "soil", "soil", "leaf_gold"]
    for i in range(len(made[-1])):
        body.face((made[-1][i], made[-1][(i + 1) % len(made[-1])], hub), colors[i])

    body_obj = body.to_object(name, material)

    # Lid is a child object so gameplay code can swing it open around its local X axis.
    hinge = (0, half + .01, height + rail + .005)
    lid = Builder()
    plank_w = .165
    lid_z = hinge[2] + .018
    for i in range(6):
        x = -half + .004 + plank_w / 2 + i * (plank_w + .0015)
        color = "green" if i % 2 == 0 else "green_dark"
        lid.box((x, 0, lid_z), (plank_w, size + .02, .03), color)
    for x in (-.30, .30):
        lid.box((x, 0, lid_z + .03), (.06, size - .10, .03), "green_dark")
    lid.box((0, -half - .005, lid_z + .05), (.20, .03, .025), "metal_dark")
    for x in (-.30, .30):
        lid.box((x, half + .01, lid_z - .004), (.08, .012, .04), "metal_light")
    lid_obj = lid.to_object(name + "_Lid", material, origin=hinge)
    lid_obj.parent = body_obj
    return body_obj


# ------------------------------------------------------------------ pipeline

def clear_scene():
    current = bpy.data.filepath
    if current and os.path.normcase(current) != os.path.normcase(BLEND_PATH):
        raise RuntimeError("Refusing to clear another open file: " + current)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def hierarchy(obj):
    return [obj] + [c for c in obj.children_recursive]


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for item in hierarchy(obj):
        item.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
    )


def tri_count(obj):
    total = 0
    for item in hierarchy(obj):
        item.data.calc_loop_triangles()
        total += len(item.data.loop_triangles)
    return total


def build_all():
    clear_scene()
    palette = make_palette_material(make_palette_image())
    leaf_mat = make_leaf_material()
    assets = []
    for key, outline in LEAF_OUTLINES.items():
        assets.append((build_flat_leaf("FY_Leaf_" + key, outline, leaf_mat), "Models/Leaves", 20))
    assets.append((build_pine("FY_Leaf_PineNeedles", leaf_mat), "Models/Leaves", 20))
    assets.append((build_wet_clump("FY_Leaf_WetClump", leaf_mat), "Models/Leaves", 20))
    assets.append((build_rake("FY_Rake", palette), "Models/Tools", 400))
    assets.append((build_blower("FY_LeafBlower", palette), "Models/Tools", 500))
    assets.append((build_bag("FY_LeafBag", palette), "Models/Props", 300))
    assets.append((build_compost_bin("FY_CompostBin", palette), "Models/Props", 800))

    bpy.context.view_layer.update()
    for obj, folder, budget in assets:
        export_fbx(obj, os.path.join(ART, folder, obj.name + ".fbx"))
        tris = tri_count(obj)
        print(f"{obj.name}: {tris} tris (budget {budget})" + ("  OVER BUDGET" if tris >= budget else ""))

    # Spread everything out for the saved source file. Exports above were all at the origin.
    leaf_x = 0.0
    prop_x = 0.0
    for obj, folder, _ in assets:
        if folder.endswith("Leaves"):
            obj.location = (leaf_x, -2.0, 0.2)
            leaf_x += 1.3
        else:
            obj.location.x = prop_x
            prop_x += 1.6
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("saved", BLEND_PATH)


build_all()
