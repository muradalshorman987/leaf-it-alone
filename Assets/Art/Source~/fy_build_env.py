"""Fallen Yard art batch 2 (WP9): environment kits, vegetation, props, ground and skies.

Run inside Blender (Scripting tab), or headless with the bpy module:
    python fy_build_env.py
It clears the scene only when the open file is untitled or is fy_env.blend itself.

Axes: Blender -Y is Unity +Z (forward), Blender +Z is Unity +Y (up).
Units: 1 Blender unit = 1 metre = 1 Unity unit.

Environment art uses its own 8x8 palette, FY_EnvPalette.png, so batch 1's
FY_Palette.png and its UVs stay untouched. Row 7 of the env palette holds the
foliage colours, laid out so a U offset on the FY_Foliage material changes the
season: 0 gold, 0.25 orange, 0.5 red, 0.75 green.
"""
import math
import os
import random

import bpy  # noqa: E402  (bpy must load before bmesh when run as a module)
import bmesh
from mathutils import Matrix, Vector


def script_path():
    try:
        text = bpy.data.texts.get(os.path.basename(__file__))
    except NameError:
        return os.path.abspath("fy_build_env.py")
    return bpy.path.abspath(text.filepath) if text and text.filepath else __file__


HERE = os.path.dirname(os.path.abspath(script_path()))
ART = os.path.dirname(HERE)
ENV = os.path.join(ART, "Environment")
BLEND_PATH = os.path.join(HERE, "fy_env.blend")
ENV_PALETTE_PATH = os.path.join(ART, "Textures", "FY_EnvPalette.png")
TOOL_PALETTE_PATH = os.path.join(ART, "Textures", "FY_Palette.png")
SKY_DIR = os.path.join(ART, "Textures", "Sky")

# ------------------------------------------------------------------ palettes

ENV_PALETTE = [
    # row 0: siding, trim, doors
    ("siding_cream", "E9DDBF"), ("siding_blue", "8FB3C4"), ("siding_blue_dark", "6F94A6"), ("trim", "F4F1E8"),
    ("trim_shadow", "D3CDBE"), ("shutter", "3F6B4E"), ("door_red", "A3412F"), ("door_blue", "2F5673"),
    # row 1: brick and roofs
    ("brick", "A6553A"), ("brick_dark", "86412D"), ("mortar", "CBBBA4"), ("roof_red", "8E3B2E"),
    ("roof_red_dark", "6E2C23"), ("roof_char", "4A4C52"), ("roof_char_dark", "36383D"), ("plaster", "D8CDB6"),
    # row 2: stone, slate, glass
    ("stone_light", "B9B2A4"), ("stone_mid", "9C9586"), ("stone_dark", "7A7468"), ("slate", "54606B"),
    ("slate_dark", "3F4851"), ("glass", "5E7F91"), ("glass_light", "A9C4CF"), ("glass_dark", "3B5361"),
    # row 3: wood and bark
    ("wood_light", "C79A63"), ("wood_mid", "9A6B3F"), ("wood_dark", "6B4528"), ("bark", "5E4532"),
    ("bark_dark", "45321F"), ("birch", "E6E1D3"), ("birch_mark", "3A3530"), ("concrete", "B7B3AA"),
    # row 4: ground
    ("grass_light", "8DAA4E"), ("grass_mid", "6F8F3C"), ("grass_dark", "55732E"), ("soil", "5A4030"),
    ("soil_dark", "43301F"), ("path", "C2B59B"), ("path_dark", "A39578"), ("gravel", "9E978A"),
    # row 5: flowers and harvest
    ("flower_white", "F2EFE6"), ("flower_yellow", "E8C140"), ("flower_purple", "8B6BB3"), ("flower_pink", "D9849A"),
    ("pumpkin", "D9762B"), ("pumpkin_dark", "B35A1C"), ("stem", "4E6B2F"), ("hay", "D8B868"),
    # row 6: metals and evergreens
    ("metal_light", "B8BEC2"), ("metal_dark", "5C6469"), ("iron", "2E3134"), ("mail_red", "B23A2E"),
    ("pine", "2F5A3E"), ("pine_dark", "234631"), ("lamp_glow", "F6E3A1"), ("berry", "B5262E"),
    # row 7: foliage, season columns (gold, orange, red, green), light then dark
    ("fol_light", "E2B34A"), ("fol_dark", "C08E2C"), ("orange_l", "DD8133"), ("orange_d", "B65F22"),
    ("red_l", "C2452F"), ("red_d", "962F22"), ("green_l", "7FA34A"), ("green_d", "5B7E33"),
]
ENV_GRID = 8

TOOL_PALETTE = [
    ("wood_light", "C79A63"), ("wood_mid", "9A6B3F"), ("wood_dark", "6B4528"), ("soil", "4A3424"),
    ("metal_light", "B8BEC2"), ("metal_dark", "5C6469"), ("rubber", "2B2B2E"), ("cream", "EFE3C8"),
    ("orange", "E08A2E"), ("teal", "3E8E8A"), ("teal_dark", "2C6A67"), ("leaf_gold", "D9A441"),
    ("kraft", "C9A36A"), ("kraft_dark", "A8814C"), ("green", "4F7A3F"), ("green_dark", "365A2E"),
]
TOOL_GRID = 4
SWATCH_PX = 16

FOLIAGE = {"fol_light", "fol_dark"}

SKIES = {
    # name: [(v, hex)] from nadir (0) to zenith (1); 0.5 is the horizon.
    "EarlyAutumn": [(0, "6E7A55"), (.47, "A3A57A"), (.5, "F2E3BE"), (.56, "CFE0E6"), (.75, "8FBFDA"), (1, "5E9CCB")],
    "PeakAutumn": [(0, "6B5E45"), (.47, "A8916A"), (.5, "F5CE96"), (.56, "E9D6B8"), (.75, "9DB9CF"), (1, "5F86B5")],
    "LateAutumn": [(0, "5A5A55"), (.47, "8E8C84"), (.5, "D9D4C6"), (.58, "BDC2C2"), (.8, "9AA3A9"), (1, "7D8890")],
    "FirstFrost": [(0, "8A929A"), (.47, "C4CBD0"), (.5, "F4F2EC"), (.56, "DCE6EC"), (.75, "B4CBDB"), (1, "8DAACB")],
}


def hex_rgb(value):
    return [int(value[c:c + 2], 16) / 255.0 for c in (0, 2, 4)]


def swatch_uv(palette, grid, name):
    index = next(i for i, (n, _) in enumerate(palette) if n == name)
    return ((index % grid + 0.5) / grid, 1.0 - (index // grid + 0.5) / grid)


def make_palette_image(name, palette, grid, path):
    size = grid * SWATCH_PX
    image = bpy.data.images.get(name) or bpy.data.images.new(name, size, size, alpha=False)
    pixels = [0.0] * (size * size * 4)
    for i, (_, hex_value) in enumerate(palette):
        rgb = hex_rgb(hex_value)
        col, row = i % grid, i // grid
        for y in range(size - (row + 1) * SWATCH_PX, size - row * SWATCH_PX):
            for x in range(col * SWATCH_PX, (col + 1) * SWATCH_PX):
                p = (y * size + x) * 4
                pixels[p:p + 4] = [*rgb, 1.0]
    image.pixels = pixels
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def lerp_stops(stops, v):
    for (v0, c0), (v1, c1) in zip(stops, stops[1:]):
        if v0 <= v <= v1:
            t = (v - v0) / (v1 - v0) if v1 > v0 else 0
            t = t * t * (3 - 2 * t)
            a, b = hex_rgb(c0), hex_rgb(c1)
            return [a[i] + (b[i] - a[i]) * t for i in range(3)]
    return hex_rgb(stops[-1][1])


def make_sky_images():
    os.makedirs(SKY_DIR, exist_ok=True)
    w, h = 8, 256
    for name, stops in SKIES.items():
        image = bpy.data.images.new("FY_Sky_" + name, w, h, alpha=False)
        pixels = []
        for y in range(h):  # Blender rows start at the bottom, which is the nadir.
            rgb = lerp_stops(stops, (y + 0.5) / h)
            pixels += [*rgb, 1.0] * w
        image.pixels = pixels
        image.filepath_raw = os.path.join(SKY_DIR, "FY_Sky_" + name + ".png")
        image.file_format = "PNG"
        image.save()


def make_material(name, image):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


# ------------------------------------------------------------------ builder

class Builder:
    """Collects shells in one bmesh. Each face gets a palette swatch and a material slot.

    `xf` is the current placement matrix, so piece builders can be reused to
    assemble whole houses.
    """

    def __init__(self, palette="env"):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.palette = palette
        self.xf = Matrix.Identity(4)
        self.used_foliage = False

    def _paint(self, face, color):
        if self.palette == "env":
            uv = swatch_uv(ENV_PALETTE, ENV_GRID, color)
            face.material_index = 1 if color in FOLIAGE else 0
            self.used_foliage |= color in FOLIAGE
        else:
            uv = swatch_uv(TOOL_PALETTE, TOOL_GRID, color)
        for loop in face.loops:
            loop[self.uv].uv = uv

    def vert(self, p):
        return self.bm.verts.new(self.xf @ Vector(p))

    def face(self, verts, color):
        face = self.bm.faces.new(verts)
        self._paint(face, color)
        return face

    def ring(self, points):
        return [self.vert(p) for p in points]

    def bridge(self, a, b, color):
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            self.face((a[i], a[j], b[j], b[i]), color)

    def loft(self, rings, colors, cap_start=True, cap_end=True, cap_colors=None):
        """Join rings of equal size. colors has one entry per band, or one per band edge (list of lists)."""
        made = [self.ring(r) for r in rings]
        for i in range(len(made) - 1):
            band = colors[i]
            if isinstance(band, (list, tuple)):
                n = len(made[i])
                for k in range(n):
                    j = (k + 1) % n
                    self.face((made[i][k], made[i][j], made[i + 1][j], made[i + 1][k]), band[k])
            else:
                self.bridge(made[i], made[i + 1], band)
        first = cap_colors[0] if cap_colors else (colors[0] if isinstance(colors[0], str) else colors[0][0])
        last = cap_colors[1] if cap_colors else (colors[-1] if isinstance(colors[-1], str) else colors[-1][0])
        if cap_start:
            self.face(list(reversed(made[0])), first)
        if cap_end:
            self.face(made[-1], last)
        return made

    def tube(self, path, radii, sides, color, cap_start=True, cap_end=True, twist=0.0):
        path = [Vector(p) for p in path]
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(path)
        colors = color if isinstance(color, (list, tuple)) else [color] * (len(path) - 1)
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
                for a in (2 * math.pi * s / sides + twist for s in range(sides))
            ])
        return self.loft(rings, colors, cap_start, cap_end)

    def box(self, center, size, color, top=None):
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        low = [(cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz),
               (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz)]
        high = [(x, y, cz + hz) for x, y, _ in low]
        return self.loft([low, high], [color], cap_colors=(color, top or color))

    def boxr(self, x0, x1, y0, y1, z0, z1, color, top=None):
        return self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), color, top)

    def prism_y(self, profile, y0, y1, color, cap=None):
        """Extrude a closed XZ profile along Y. profile is [(x, z)]."""
        return self.loft([[(x, y0, z) for x, z in profile], [(x, y1, z) for x, z in profile]],
                         [color], cap_colors=(cap or color, cap or color))

    def prism_x(self, profile, x0, x1, colors, cap=None):
        """Extrude a closed YZ profile along X. colors: one per profile edge, or one colour."""
        band = colors if isinstance(colors, (list, tuple)) else [colors] * len(profile)
        c = cap or band[0]
        return self.loft([[(x0, y, z) for y, z in profile], [(x1, y, z) for y, z in profile]],
                         [band], cap_colors=(c, c))

    def cone(self, base, radius, height, sides, colors, rng=None, jitter=0.0, base_cap=True):
        bx, by, bz = base
        rim = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            j = rng.uniform(-jitter, jitter) if rng else 0.0
            rim.append(self.vert((bx + math.cos(a) * radius * (1 + j), by + math.sin(a) * radius * (1 + j),
                                  bz + (rng.uniform(-jitter, jitter) * radius * .5 if rng else 0))))
        apex = self.vert((bx, by, bz + height))
        for s in range(sides):
            c = colors[s % len(colors)] if isinstance(colors, (list, tuple)) else colors
            self.face((rim[s], rim[(s + 1) % sides], apex), c)
        if base_cap:
            c = colors[-1] if isinstance(colors, (list, tuple)) else colors
            self.face(list(reversed(rim)), c)

    def blob(self, center, scale, rng, light, dark, jitter=0.12, subdiv=2, floor=None):
        """A faceted ico blob. Faces pointing up get the light colour."""
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=1.0)
        lookup = {}
        cx, cy, cz = center
        for v in tmp.verts:
            d = v.co * (1 + rng.uniform(-jitter, jitter))
            p = Vector((cx + d.x * scale[0], cy + d.y * scale[1], cz + d.z * scale[2]))
            if floor is not None:
                p.z = max(p.z, floor)
            lookup[v] = self.vert(p)
        for f in tmp.faces:
            n = f.normal
            color = light if n.z > 0.15 + rng.uniform(-.2, .2) else dark
            self.face([lookup[v] for v in f.verts], color)
        tmp.free()

    def double_sided(self, faces):
        for face in list(faces):
            back = self.bm.faces.new([self.bm.verts.new(v.co) for v in reversed(face.verts)])
            back.material_index = face.material_index
            for loop, src in zip(back.loops, reversed(list(face.loops))):
                loop[self.uv].uv = src[self.uv].uv.copy()

    def to_object(self, name, materials, origin=(0, 0, 0), recalc=True):
        if recalc:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces, quad_method="BEAUTY", ngon_method="EAR_CLIP")
        offset = Vector(origin)
        for vert in self.bm.verts:
            vert.co -= offset
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        if self.palette == "env":
            mesh.materials.append(materials["env"])
            if self.used_foliage:
                mesh.materials.append(materials["foliage"])
        else:
            mesh.materials.append(materials["tool"])
        obj = bpy.data.objects.new(name, mesh)
        obj.location = offset
        bpy.context.scene.collection.objects.link(obj)
        return obj


def place(b, x=0.0, y=0.0, z=0.0, rot=0.0):
    b.xf = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rot), 4, "Z")


# ------------------------------------------------------------------ house kits
# Grid: walls are 3 m modules, 3 m per storey, 0.2 m thick. Every wall piece is
# centred on x = 0 with its outer face towards Blender -Y (Unity +Z). Houses are
# 2 modules (6 m) deep so gables and roofs fit every kit.

M, H, T = 3.0, 3.0, 0.2
DEPTH = 6.0
PLINTH = 0.35
CLAD = 0.035

KITS = {
    "Cottage": dict(courses=("siding_blue", "siding_blue_dark"), course_h=0.22, step=0.012, blocks=1,
                    plinth="stone_mid", trim="trim", trim_dark="trim_shadow", door="door_red", shutters="shutter",
                    roof=("roof_red", "roof_red_dark"), pitch=2.4, corner="trim", chimney=("brick", "brick_dark"),
                    win=(1.1, 1.3, 1.0), door_w=1.0, keystone=False),
    "Brick": dict(courses=("brick", "brick_dark"), course_h=0.16, step=0.008, blocks=1,
                  plinth="brick_dark", trim="trim", trim_dark="trim_shadow", door="door_blue", shutters=None,
                  roof=("roof_char", "roof_char_dark"), pitch=1.7, corner="brick_dark", chimney=("brick", "brick_dark"),
                  win=(1.2, 1.4, 0.95), door_w=1.05, keystone=False),
    "Manor": dict(courses=("stone_light", "stone_mid"), course_h=0.3, step=0.014, blocks=3,
                  plinth="stone_dark", trim="stone_light", trim_dark="stone_mid", door="wood_dark", shutters=None,
                  roof=("slate", "slate_dark"), pitch=2.9, corner="stone_light", chimney=("stone_mid", "stone_dark"),
                  win=(1.0, 1.7, 0.9), door_w=1.2, keystone=True),
}


def cladding(b, kit, x0, x1, z0, z1, seed, width_at=None):
    """Courses on the outer face (y < -T/2) of a wall rectangle. width_at(z) clips to a gable triangle."""
    rng = random.Random(seed)
    ch = kit["course_h"]
    colors = kit["courses"]
    k = int(math.floor(z0 / ch + 1e-6))
    while k * ch < z1 - 1e-6:
        a, c = max(z0, k * ch), min(z1, (k + 1) * ch)
        if c - a > 1e-3:
            lo, hi = x0, x1
            if width_at:
                half = width_at(c)
                lo, hi = max(lo, -half), min(hi, half)
            if hi - lo > 0.02:
                y_out = -T / 2 - CLAD - (kit["step"] if k % 2 else 0)
                if kit["blocks"] > 1:
                    # Stagger block joints from course to course.
                    span = M / kit["blocks"]
                    off = (k % 2) * span / 2
                    cuts = sorted({lo, hi, *[x for x in (span * i - M / 2 + off for i in range(-2, kit["blocks"] + 3)) if lo < x < hi]})
                    for i, (p, q) in enumerate(zip(cuts, cuts[1:])):
                        if q - p < 0.02:
                            continue
                        col = colors[(k + i + rng.randint(0, 1)) % 2]
                        g = 0.006
                        top = c if not width_at else c
                        if width_at:
                            # Trapezoid course for gables.
                            hb, ht = width_at(a), width_at(c)
                            pts = [(max(p, -hb), a), (min(q, hb), a), (min(q, ht), top), (max(p, -ht), top)]
                            if pts[1][0] - pts[0][0] < .02 or pts[2][0] - pts[3][0] < .0:
                                continue
                            b.prism_y([(pts[0][0] + g, a), (pts[1][0] - g, a), (max(pts[2][0] - g, pts[3][0] + g), c), (pts[3][0] + g, c)],
                                      y_out, -T / 2, col)
                        else:
                            b.boxr(p + g, q - g, y_out, -T / 2, a + g, c - g, col)
                else:
                    col = colors[k % 2]
                    if width_at:
                        hb, ht = width_at(a), width_at(c)
                        b.prism_y([(max(lo, -hb), a), (min(hi, hb), a), (min(hi, ht), c), (max(lo, -ht), c)], y_out, -T / 2, col)
                    else:
                        b.boxr(lo, hi, y_out, -T / 2, a, c, col)
        k += 1


def wall_core(b, kit, holes, plinth=True):
    """Structural wall with rectangular holes [(x0, x1, z0, z1)], plus cladding around them."""
    xs = sorted({-M / 2, M / 2, *[h[0] for h in holes], *[h[1] for h in holes]})
    seed = 1
    for x0, x1 in zip(xs, xs[1:]):
        cut = [h for h in holes if h[0] <= x0 + 1e-6 and h[1] >= x1 - 1e-6]
        zs = [0.0]
        for h in cut:
            zs += [h[2], h[3]]
        zs.append(H)
        for z0, z1 in zip(zs[0::2], zs[1::2]):
            if z1 - z0 < 1e-3:
                continue
            b.boxr(x0, x1, -T / 2, T / 2, z0, z1, "plaster")
            cz0 = PLINTH if plinth and z0 < PLINTH else z0
            cz1 = H - 0.18 if z1 > H - 0.18 else z1
            if cz1 > cz0:
                cladding(b, kit, x0, x1, cz0, cz1, seed)
            seed += 1
    if plinth:
        segments = [(-M / 2, M / 2)]
        for h in holes:
            if h[2] < PLINTH:
                segments = [s for seg in segments for s in ((seg[0], min(seg[1], h[0])), (max(seg[0], h[1]), seg[1])) if s[1] - s[0] > 1e-3]
        for x0, x1 in segments:
            b.boxr(x0, x1, -T / 2 - .07, -T / 2, 0, PLINTH, kit["plinth"], top=kit["plinth"])
    # Trim band along the top of the storey.
    b.boxr(-M / 2, M / 2, -T / 2 - .06, -T / 2, H - .18, H, kit["trim"], top=kit["trim_dark"])


def window_dressing(b, kit, x0, x1, z0, z1):
    trim, dark = kit["trim"], kit["trim_dark"]
    fy = -T / 2
    b.boxr(x0 - .02, x1 + .02, -.02, .02, z0, z1, "glass")
    # Muntins
    mx = (x0 + x1) / 2
    b.boxr(mx - .025, mx + .025, -.05, -.02, z0, z1, trim)
    mz = z0 + (z1 - z0) * .55
    b.boxr(x0, x1, -.05, -.02, mz - .025, mz + .025, trim)
    # Highlight panes
    b.boxr(x0 + .06, x0 + .2, -.03, -.02, mz + .1, z1 - .08, "glass_light")
    # Frame
    w = .08
    b.boxr(x0 - w, x0, fy - .07, fy + .03, z0 - w, z1 + w, trim)
    b.boxr(x1, x1 + w, fy - .07, fy + .03, z0 - w, z1 + w, trim)
    b.boxr(x0, x1, fy - .07, fy + .03, z1, z1 + w, trim)
    b.boxr(x0 - .14, x1 + .14, fy - .13, fy + .03, z0 - .1, z0, trim, top=dark)  # sill
    if kit["keystone"]:
        b.boxr(x0 - .12, x1 + .12, fy - .1, fy, z1 + w, z1 + w + .14, dark)
        b.prism_y([(mx - .12, z1 + w), (mx + .12, z1 + w), (mx + .17, z1 + w + .3), (mx - .17, z1 + w + .3)], fy - .12, fy, trim)
    if kit["shutters"]:
        sw = (x1 - x0) * .42
        for sx0, sx1 in ((x0 - w - .03 - sw, x0 - w - .03), (x1 + w + .03, x1 + w + .03 + sw)):
            b.boxr(sx0, sx1, fy - .07, fy - .02, z0 - .02, z1 + .02, kit["shutters"])
            b.boxr(sx0 + .04, sx1 - .04, fy - .09, fy - .07, z0 + (z1 - z0) * .5 - .03, z0 + (z1 - z0) * .5 + .03, "pine_dark")


def build_wall(b, kit, kind, plinth=True):
    ww, wh, wz = kit["win"]
    if kind == "Wall":
        wall_core(b, kit, [], plinth)
    elif kind == "WallWindow":
        x0, x1 = -ww / 2, ww / 2
        wall_core(b, kit, [(x0, x1, wz, wz + wh)], plinth)
        window_dressing(b, kit, x0, x1, wz, wz + wh)
    elif kind == "WallDoor":
        dw, dh = kit["door_w"], 2.25
        x0, x1 = -dw / 2, dw / 2
        z0 = PLINTH
        wall_core(b, kit, [(x0, x1, 0, z0 + dh)], plinth)
        trim, dark = kit["trim"], kit["trim_dark"]
        fy = -T / 2
        b.boxr(x0, x1, -.04, .04, 0, z0, kit["plinth"])  # threshold block
        b.boxr(x0, x1, -.02, .04, z0, z0 + dh, kit["door"])
        for pz0, pz1 in ((z0 + .2, z0 + .95), (z0 + 1.15, z0 + dh - .45)):
            b.boxr(x0 + .14, x1 - .14, -.04, -.02, pz0, pz1, kit["door"] if kit["door"] != "wood_dark" else "wood_mid")
        b.boxr(x0 + .14, x1 - .14, -.03, -.02, z0 + dh - .35, z0 + dh - .1, "glass")
        b.boxr(x1 - .2, x1 - .12, -.08, -.02, z0 + 1.0, z0 + 1.08, "metal_light")
        w = .1
        b.boxr(x0 - w, x0, fy - .08, fy + .03, z0, z0 + dh + w, trim)
        b.boxr(x1, x1 + w, fy - .08, fy + .03, z0, z0 + dh + w, trim)
        b.boxr(x0 - w, x1 + w, fy - .08, fy + .03, z0 + dh, z0 + dh + w, trim)
        if kit["keystone"]:
            b.boxr(x0 - .25, x1 + .25, fy - .14, fy, z0 + dh + w, z0 + dh + w + .16, dark)
            b.boxr(x0 - .32, x0 - .12, fy - .16, fy, 0, z0 + dh + w, trim, top=dark)
            b.boxr(x1 + .12, x1 + .32, fy - .16, fy, 0, z0 + dh + w, trim, top=dark)
        else:
            # Small canopy over the door.
            cz = z0 + dh + .25
            b.prism_x([(fy, cz + .3), (fy - .7, cz), (fy - .7, cz - .06), (fy, cz + .24)], x0 - .3, x1 + .3,
                      [kit["roof"][0], kit["trim"], kit["roof"][1], kit["trim"]])
            for sx in (x0 - .2, x1 + .2):
                b.prism_y([(sx - .04, cz + .2), (sx + .04, cz + .2), (sx + .04, cz - .02), (sx - .04, cz - .3)],
                          fy - .6, fy, trim)


def build_corner(b, kit, storeys=1):
    s = .32
    top = H * storeys
    if kit["keystone"]:  # Manor quoins
        b.boxr(-s / 2, s / 2, -s / 2, s / 2, 0, top, kit["courses"][1])
        z, i = 0.0, 0
        while z < top - 1e-6:
            hgt = min(.3, top - z)
            big = .42 if i % 2 == 0 else .34
            b.boxr(-big / 2, big / 2, -big / 2, big / 2, z + .01, z + hgt - .01, kit["corner"])
            z += hgt
            i += 1
    else:
        b.boxr(-s / 2, s / 2, -s / 2, s / 2, 0, top, kit["corner"], top=kit["trim_dark"])
        b.boxr(-s / 2 - .04, s / 2 + .04, -s / 2 - .04, s / 2 + .04, 0, PLINTH, kit["plinth"])


def gable_half(kit):
    pitch = kit["pitch"]
    return lambda z: max(0.0, DEPTH / 2 * (1 - z / pitch))


def build_gable(b, kit):
    pitch = kit["pitch"]
    half = DEPTH / 2
    b.prism_y([(-half, 0), (half, 0), (0, pitch)], -T / 2, T / 2, "plaster")
    cladding(b, kit, -half, half, 0, pitch - .05, 7, width_at=gable_half(kit))
    # Attic vent
    vz = pitch * .45
    r = .28
    octagon = [(math.cos(a) * r, vz + math.sin(a) * r) for a in (2 * math.pi * i / 8 + math.pi / 8 for i in range(8))]
    b.prism_y(octagon, -T / 2 - .09, -T / 2 + .02, kit["trim"])
    inner = [(x * .7, vz + (z - vz) * .7) for x, z in octagon]
    b.prism_y(inner, -T / 2 - .11, -T / 2 - .09, "glass_dark")


def roof_profile(kit, overhang=.45, courses=7, base=.14, butt=.035):
    """Closed YZ profile of a gable roof, with stepped shingle courses on top.

    Returns (points, colours), one colour per edge from point i to i+1.
    """
    pitch = kit["pitch"]
    half = DEPTH / 2
    slope = pitch / half
    e = half + overhang
    ze = -overhang * slope
    light, dark = kit["roof"]
    pts, cols = [], []

    def onslope(t, side):
        y = side * e * (1 - t)
        z = ze + (pitch - ze) * t
        n = Vector((0, side * slope, 1)).normalized() if side else Vector((0, 0, 1))
        return Vector((0, y, z)), n

    # Left slope (y < 0) from eave up to the ridge, then the right slope down.
    for side, rng_ in ((-1, range(courses)), (1, reversed(range(courses)))):
        for k in rng_:
            if side == -1:
                p0, n = onslope(k / courses, -1)
                p1, _ = onslope((k + 1) / courses, -1)
                n = Vector((0, -slope, 1)).normalized()
                pts += [p0 + n * base, p0 + n * (base + butt)]
                cols += [dark, light if k % 2 == 0 else dark]
                if k == courses - 1:
                    pts.append(p1 + n * base)
                    cols.append(dark)
                    pts.append(Vector((0, 0, pitch + base * 1.25 + .05)))
                    cols.append(dark)
            else:
                p0, _ = onslope((k + 1) / courses, 1)
                p1, _ = onslope(k / courses, 1)
                n = Vector((0, slope, 1)).normalized()
                if k == courses - 1:
                    pts.append(p0 + n * base)
                    cols.append(light if k % 2 == 0 else dark)
                else:
                    pts.append(p0 + n * (base))
                    cols.append(light if k % 2 == 0 else dark)
                pts.append(p1 + n * (base + butt))
                cols.append(dark)
    # The right slope above ends on a butt; close down to the eave underside and back under the ridge.
    ny = Vector((0, slope, 1)).normalized()
    pts.append(Vector((0, e, ze)) + ny * 0)
    cols.append(kit["trim_dark"] if kit["trim"] != "stone_light" else "wood_dark")
    pts.append(Vector((0, 0, pitch)))
    cols.append(kit["trim_dark"] if kit["trim"] != "stone_light" else "wood_dark")
    pts.append(Vector((0, -e, ze)))
    cols.append(dark)
    return [(p.y, p.z) for p in pts], cols


def build_roof(b, kit, length=M):
    profile, cols = roof_profile(kit)
    b.prism_x(profile, -length / 2, length / 2, cols, cap=kit["roof"][1])


def build_chimney(b, kit):
    a, d = kit["chimney"]
    b.boxr(-.35, .35, -.35, .35, 0, 2.6, a)
    for z in (.6, 1.2, 1.8):
        b.boxr(-.37, .37, -.37, .37, z, z + .08, d)
    b.boxr(-.42, .42, -.42, .42, 2.6, 2.75, d, top=d)
    b.boxr(-.12, .12, -.12, .12, 2.75, 2.95, "iron")


def build_steps(b, kit):
    color = kit["plinth"]
    for i in range(3):
        depth = .9 - i * .3
        z1 = PLINTH * (i + 1) / 3
        b.boxr(-.85, .85, -depth, 0, 0, z1, color, top="stone_light" if kit["keystone"] else "concrete")


KIT_PIECES = ["Wall", "WallWindow", "WallDoor", "Corner", "Gable", "Roof", "Chimney", "Steps"]


def build_kit_piece(b, kit, piece):
    if piece.startswith("Wall"):
        build_wall(b, kit, piece)
    elif piece == "Corner":
        build_corner(b, kit)
    elif piece == "Gable":
        build_gable(b, kit)
    elif piece == "Roof":
        build_roof(b, kit)
    elif piece == "Chimney":
        build_chimney(b, kit)
    elif piece == "Steps":
        build_steps(b, kit)


def build_house(b, kit, length=3, storeys=1, back_door=False):
    """A whole house from kit pieces: `length` modules wide, 2 deep."""
    half_x = length * M / 2
    half_y = DEPTH / 2
    centres = [-half_x + M / 2 + i * M for i in range(length)]
    for s in range(storeys):
        z = s * H
        plinth = s == 0
        for i, x in enumerate(centres):
            front = "WallDoor" if (s == 0 and i == length // 2) else "WallWindow"
            place(b, x, -half_y, z, 0)
            build_wall(b, kit, front, plinth)
            place(b, x, half_y, z, 180)
            build_wall(b, kit, "WallDoor" if (back_door and s == 0 and i == 0) else ("WallWindow" if i % 2 == 0 else "Wall"), plinth)
        for j, y in enumerate((-half_y + M / 2, half_y - M / 2)):
            place(b, half_x, y, z, 90)
            build_wall(b, kit, "WallWindow" if j == 0 else "Wall", plinth)
            place(b, -half_x, y, z, -90)
            build_wall(b, kit, "WallWindow", plinth)
    for x in (-half_x, half_x):
        for y in (-half_y, half_y):
            place(b, x, y, 0, 0)
            build_corner(b, kit, storeys)
    top = storeys * H
    place(b, half_x, 0, top, 90)
    build_gable(b, kit)
    place(b, -half_x, 0, top, -90)
    build_gable(b, kit)
    place(b, 0, 0, top, 0)
    build_roof(b, kit, length * M + .5)
    place(b, half_x - 1.2, .9, top, 0)
    build_chimney(b, kit)
    place(b, centres[length // 2], -half_y - T / 2 - .07, 0, 0)
    build_steps(b, kit)
    place(b)


# ------------------------------------------------------------------ fences
# Sections are 2 m long, centred on x = 0, with one post at the -X end. Chain them
# every 2 m and finish a run with the matching _Post piece.

def fence_picket(b, with_post=True):
    if with_post:
        picket_post(b, -1.0)
    for z in (.25, .72):
        b.boxr(-1, 1, .0, .04, z, z + .07, "trim_shadow")
    n = 12
    for i in range(n):
        x = -1 + (i + .5) * 2 / n
        w = .075
        b.prism_y([(x - w / 2, .06), (x + w / 2, .06), (x + w / 2, .88), (x, .95), (x - w / 2, .88)], -.02, 0, "trim")


def picket_post(b, x=0.0):
    b.boxr(x - .05, x + .05, -.05, .07, 0, 1.0, "trim", top="trim_shadow")
    b.loft([[(x - .06, -.06, 1.0), (x + .06, -.06, 1.0), (x + .06, .08, 1.0), (x - .06, .08, 1.0)],
            [(x, .01, 1.1)] * 4], ["trim_shadow"], cap_end=False)


def fence_picket_gate(b):
    """Returns the door builder too: the door is a child hinged at x = -0.94."""
    picket_post(b, -1.0)
    door = Builder()
    for z in (.2, .72):
        door.boxr(-.94, .92, .0, .04, z, z + .08, "trim_shadow")
    # Diagonal brace
    door.prism_y([(-.9, .26), (-.82, .2), (.9, .74), (.82, .8)], .04, .07, "trim_shadow")
    n = 11
    for i in range(n):
        x = -.94 + (i + .5) * 1.86 / n
        w = .075
        top = .95 + .12 * math.sin(math.pi * (i + .5) / n)
        door.prism_y([(x - w / 2, .08), (x + w / 2, .08), (x + w / 2, top - .07), (x, top), (x - w / 2, top - .07)],
                     -.02, 0, "trim")
    door.boxr(.8, .86, -.06, -.02, .5, .56, "iron")
    return door


def fence_rail(b, with_post=True, seed=3):
    rng = random.Random(seed)
    if with_post:
        rail_post(b, -1.0, rng)
    for z in (.45, .85):
        sag = rng.uniform(.01, .04)
        b.tube([(-1.02, .06, z), (0, .06, z - sag), (1.02, .06, z + rng.uniform(-.02, .02))], .045, 5,
               ["wood_mid", "wood_light"], twist=rng.uniform(0, 1))


def rail_post(b, x=0.0, rng=None):
    rng = rng or random.Random(5)
    b.tube([(x, 0, 0), (x + rng.uniform(-.02, .02), 0, .6), (x, 0, 1.08)], [.075, .07, .065], 6,
           ["wood_dark", "wood_mid"], twist=rng.uniform(0, 1))


def fence_stone(b, seed=11):
    rng = random.Random(seed)
    courses = [.2, .18, .17]
    z = 0.0
    for c, hgt in enumerate(courses):
        x = -1.0 + (c % 2) * .18
        x = -1.0
        while x < 1.0 - 1e-6:
            w = rng.uniform(.28, .42) if x > -1 else (.2 + (c % 2) * .15)
            w = min(w, 1.0 - x)
            if 1.0 - (x + w) < .12:
                w = 1.0 - x
            d = .46 - c * .04
            col = rng.choice(["stone_light", "stone_mid", "stone_mid", "stone_dark"])
            jy = rng.uniform(-.015, .015)
            b.boxr(x + .01, x + w - .01, -d / 2 + jy, d / 2 + jy, z + .005, z + hgt - .005, col)
            x += w
        z += hgt
    x = -1.0
    while x < 1.0 - 1e-6:
        w = min(rng.uniform(.3, .45), 1.0 - x)
        if 1.0 - (x + w) < .15:
            w = 1.0 - x
        b.boxr(x + .01, x + w - .01, -.2, .2, z, z + .08, "stone_light", top="stone_light")
        x += w


def fence_iron(b, with_post=True):
    if with_post:
        iron_post(b, -1.0)
    for z in (.12, 1.05):
        b.boxr(-1, 1, -.02, .02, z, z + .04, "iron")
    n = 14
    for i in range(n):
        x = -1 + (i + .5) * 2 / n
        b.boxr(x - .012, x + .012, -.012, .012, .02, 1.25, "iron")
        b.loft([[(x - .03, -.03, 1.25), (x + .03, -.03, 1.25), (x + .03, .03, 1.25), (x - .03, .03, 1.25)],
                [(x, 0, 1.35)] * 4], ["iron"], cap_end=False)


def iron_post(b, x=0.0):
    b.boxr(x - .07, x + .07, -.07, .07, 0, 1.4, "iron")
    b.boxr(x - .09, x + .09, -.09, .09, 1.4, 1.46, "iron")
    b.blob((x, 0, 1.53), (.07, .07, .07), random.Random(2), "iron", "iron", jitter=0, subdiv=1)


# ------------------------------------------------------------------ trees
# Pivot at the base of the trunk. Leafy crowns use FY_Foliage so a yard can set
# the season with the material's U offset.

def branch_tree(b, rng, height, trunk_r, n_main, spread, twig_depth, bark=("bark", "bark_dark"), trunk_colors=None,
                lean=.1, crown=None):
    trunk_top = Vector((rng.uniform(-lean, lean), rng.uniform(-lean, lean), height * .62))
    mid = trunk_top * .5 + Vector((rng.uniform(-.08, .08), rng.uniform(-.08, .08), 0))
    path = [Vector((0, 0, -.05)), Vector((0, 0, height * .12)), mid, trunk_top, trunk_top * 1.0 + Vector((0, 0, height * .25))]
    radii = [trunk_r * 1.35, trunk_r, trunk_r * .8, trunk_r * .6, trunk_r * .25]
    colors = trunk_colors or [bark[0], bark[0], bark[1], bark[0]]
    b.tube(path, radii, 7, colors, cap_start=True, cap_end=True, twist=rng.uniform(0, 1))
    tips = []
    for i in range(n_main):
        ang = 2 * math.pi * (i + rng.uniform(-.25, .25)) / n_main
        start_t = rng.uniform(.55, .95)
        start = path[2].lerp(path[4], start_t - .4) if start_t > .7 else path[2].lerp(path[3], start_t)
        out = Vector((math.cos(ang), math.sin(ang), 0))
        length = height * rng.uniform(.3, .42)
        end = start + out * length * spread + Vector((0, 0, length * rng.uniform(.6, .9)))
        bend = start.lerp(end, .5) + Vector((0, 0, -length * .08)) + out * length * .08
        r = trunk_r * rng.uniform(.4, .5)
        b.tube([start, bend, end], [r, r * .65, r * .2], 5, [bark[0], bark[1]], twist=rng.uniform(0, 1))
        tips.append((end, out))
        if twig_depth:
            for k in range(twig_depth):
                p = start.lerp(end, rng.uniform(.5, .85))
                side = Vector((-out.y, out.x, 0)) * (1 if k % 2 else -1)
                tl = length * rng.uniform(.3, .45)
                tend = p + (side * .7 + out * .6) * tl + Vector((0, 0, tl * .6))
                b.tube([p, tend], [r * .45, r * .12], 4, bark[1], twist=rng.uniform(0, 1))
                tips.append((tend, out))
    return tips


def tree_maple(b, leafy, seed=21):
    rng = random.Random(seed)
    tips = branch_tree(b, rng, 5.2, .17, 5, .9, 0 if leafy else 2)
    if leafy:
        b.blob((0, 0, 4.4), (1.9, 1.9, 1.5), rng, "fol_light", "fol_dark", jitter=.14)
        for end, out in tips[:5]:
            c = end + out * .25 + Vector((0, 0, .2))
            b.blob(c, (1.0, 1.0, .85), rng, "fol_light", "fol_dark", jitter=.18)


def tree_oak(b, leafy, seed=34):
    rng = random.Random(seed)
    tips = branch_tree(b, rng, 4.8, .26, 6, 1.4, 0 if leafy else 2, lean=.15)
    if leafy:
        b.blob((0, 0, 4.0), (2.5, 2.5, 1.3), rng, "fol_light", "fol_dark", jitter=.12)
        for end, out in tips[:6]:
            c = end + out * .3
            b.blob(c, (1.25, 1.25, .8), rng, "fol_light", "fol_dark", jitter=.18)


def tree_birch(b, leafy, seed=55):
    rng = random.Random(seed)
    trunk_colors = ["birch", "birch_mark", "birch", "birch"]
    tips = branch_tree(b, rng, 6.0, .11, 4, .5, 0 if leafy else 2, bark=("birch", "birch_mark"),
                       trunk_colors=trunk_colors, lean=.2)
    # Extra dark bands on the trunk for the birch look.
    for z in (.9, 1.6, 2.4, 3.1):
        r = .1 - z * .008
        b.tube([(0, 0, z), (0, 0, z + .06)], r + .006, 7, "birch_mark", twist=rng.uniform(0, 1))
    if leafy:
        b.blob((0, 0, 4.6), (1.2, 1.2, 2.0), rng, "fol_light", "fol_dark", jitter=.15)
        for end, out in tips[:4]:
            b.blob(end + Vector((0, 0, .1)), (.75, .75, 1.0), rng, "fol_light", "fol_dark", jitter=.18)


def tree_pine(b, seed=89):
    rng = random.Random(seed)
    b.tube([(0, 0, -.05), (0, 0, 1.0), (0, 0, 6.0)], [.2, .16, .05], 6, ["bark", "bark_dark"])
    tiers = [(.9, 1.9, 2.1), (2.0, 1.55, 1.9), (3.0, 1.2, 1.7), (3.9, .85, 1.5), (4.7, .5, 1.4)]
    for i, (z, r, h) in enumerate(tiers):
        b.cone((0, 0, z), r, h, 9, ["pine", "pine_dark", "pine", "pine", "pine_dark"], rng=rng, jitter=.08)


def bush_round(b, seed=101, scale=1.0):
    rng = random.Random(seed)
    b.blob((0, 0, .45 * scale), (.75 * scale, .7 * scale, .55 * scale), rng, "fol_light", "fol_dark", floor=0)
    b.blob((.35 * scale, .2 * scale, .35 * scale), (.45 * scale, .45 * scale, .4 * scale), rng, "fol_light", "fol_dark", floor=0)
    b.blob((-.3 * scale, -.25 * scale, .3 * scale), (.4 * scale, .4 * scale, .35 * scale), rng, "fol_light", "fol_dark", floor=0)


def bush_berry(b, seed=131):
    rng = random.Random(seed)
    b.blob((0, 0, .4), (.6, .6, .45), rng, "green_l", "green_d", floor=0)
    b.blob((.25, -.2, .3), (.38, .38, .32), rng, "green_l", "green_d", floor=0)
    for i in range(9):
        a = rng.uniform(0, 2 * math.pi)
        z = rng.uniform(.3, .7)
        r = .55 * math.sqrt(max(0.0, 1 - ((z - .4) / .5) ** 2)) + .04
        b.box((math.cos(a) * r, math.sin(a) * r, z), (.07, .07, .07), "berry")


def hedge(b, seed=151):
    """2 m evergreen hedge section with flat, matching ends so sections tile."""
    rng = random.Random(seed)
    base = [(-.42, 0), (.42, 0), (.48, .6), (.4, 1.05), (.15, 1.15), (-.15, 1.15), (-.4, 1.05), (-.48, .6)]
    rings = []
    xs = [-1.0, -.5, 0.0, .5, 1.0]
    for i, x in enumerate(xs):
        jit = 0 if i in (0, len(xs) - 1) else .06
        rings.append([(x, y * (1 + rng.uniform(-jit, jit)), z * (1 + rng.uniform(0, jit)) if z > 0 else 0) for y, z in base])
    colors = []
    for i in range(len(xs) - 1):
        colors.append(["green_d", "green_d", "green_l", "green_l", "green_l", "green_l", "green_d", "green_d"]
                      if i % 2 == 0 else
                      ["green_d", "green_d", "green_d", "green_l", "green_l", "green_l", "green_l", "green_d"])
    b.loft(rings, colors, cap_colors=("green_d", "green_d"))


# ------------------------------------------------------------------ props
# Pivot at the centre of the base.

def mailbox(b):
    """Returns the flag builder; the flag is a child hinged on the box's +X side."""
    b.boxr(-.05, .05, -.05, .05, 0, 1.0, "wood_mid", top="wood_dark")
    b.boxr(-.07, .07, -.3, .22, 1.0, 1.05, "wood_dark")
    profile = [(.12, 0), *[(math.cos(math.pi * i / 6) * .12, .14 + math.sin(math.pi * i / 6) * .12) for i in range(7)], (-.12, 0)]
    # profile is XZ; mailbox runs along Y with the door at -Y.
    b.prism_y([(x, z + 1.05) for x, z in profile], -.3, .2, "door_blue")
    b.prism_y([(x * 1.05, z * 1.04 + 1.045) for x, z in profile], -.32, -.3, "trim")
    b.boxr(-.03, .03, -.34, -.32, 1.17, 1.2, "metal_light")
    flag = Builder()
    z = 1.18  # hinge height on the +X side; the flag lies along +Y when down
    flag.boxr(.12, .135, -.025, .025, z - .025, z + .025, "metal_dark")
    flag.boxr(.135, .15, -.015, .3, z - .012, z + .012, "mail_red")
    flag.boxr(.135, .15, .2, .3, z - .012, z + .1, "mail_red")
    return flag


def bench(b):
    for x in (-.65, .65):
        b.boxr(x - .04, x + .04, -.2, -.14, 0, .45, "iron")
        b.boxr(x - .04, x + .04, .16, .22, 0, .85, "iron")
        b.boxr(x - .04, x + .04, -.22, .22, .4, .45, "iron")
        b.boxr(x - .04, x + .04, -.24, .2, .62, .66, "iron")
    for i in range(4):
        y = -.2 + i * .1
        b.boxr(-.8, .8, y, y + .085, .45, .48, "wood_light" if i % 2 else "wood_mid")
    for i in range(3):
        z = .55 + i * .1
        b.boxr(-.8, .8, .17, .2, z, z + .08, "wood_light" if i % 2 else "wood_mid")


def wheelbarrow(b):
    tray = [[(-.32, -.5, .35), (.32, -.5, .35), (.32, .28, .35), (-.32, .28, .35)],
            [(-.4, -.6, .62), (.4, -.6, .62), (.4, .42, .62), (-.4, .42, .62)]]
    b.loft(tray, ["door_red"], cap_end=False, cap_colors=("door_red", "door_red"))
    b.boxr(-.38, .38, -.58, .4, .6, .62, "metal_dark")
    b.tube([(-.07, -.82, .22), (.07, -.82, .22)], .22, 10, "iron")
    b.tube([(-.08, -.82, .22), (.08, -.82, .22)], .08, 6, "metal_light")
    for x in (-.25, .25):
        b.tube([(x * .4, -.85, .22), (x, -.2, .38), (x * 1.2, .95, .6)], .025, 5, "wood_mid")
        b.tube([(x * .9, .1, .35), (x * .9, .25, 0)], .022, 4, "iron")
        b.box((x * 1.2, .97, .6), (.06, .14, .06), "iron")
    b.blob((0, -.1, .62), (.3, .42, .08), random.Random(4), "fol_light", "orange_d", jitter=.2)


def birdbath(b):
    b.tube([(0, 0, 0), (0, 0, .08)], [.28, .26], 8, "stone_mid")
    b.tube([(0, 0, .08), (0, 0, .4), (0, 0, .75)], [.1, .07, .11], 8, "stone_light")
    rings = [[(math.cos(a) * r, math.sin(a) * r, z) for a in (2 * math.pi * i / 12 for i in range(12))]
             for r, z in ((.12, .75), (.42, .86), (.45, .92), (.38, .92), (.06, .82))]
    b.loft(rings, ["stone_light", "stone_light", "stone_mid", "stone_light"], cap_start=True, cap_end=True)
    b.tube([(0, 0, .87), (0, 0, .88)], .36, 12, "glass_light")


def pumpkin(b, scale=1.0, seed=8):
    rng = random.Random(seed)
    lobes = 8
    rings = []
    for z, r in ((0, .1), (.05, .26), (.16, .34), (.28, .3), (.36, .14)):
        ring = []
        for i in range(lobes * 2):
            a = math.pi * i / lobes
            rr = r * (1 if i % 2 == 0 else .88)
            ring.append((math.cos(a) * rr * scale, math.sin(a) * rr * scale, z * scale))
        rings.append(ring)
    band = ["pumpkin" if i % 2 == 0 else "pumpkin_dark" for i in range(lobes * 2)]
    b.loft(rings, [band] * 4, cap_colors=("pumpkin_dark", "pumpkin_dark"))
    b.tube([(0, 0, .34 * scale), (.03 * scale, .01, .45 * scale), (.07 * scale, .02, .48 * scale)],
           [.035 * scale, .028 * scale, .02 * scale], 5, "stem")


def hay_bale(b):
    b.boxr(-.5, .5, -.3, .3, 0, .4, "hay", top="hay")
    for x in (-.25, .25):
        b.boxr(x - .015, x + .015, -.31, .31, -.0, .41, "wood_dark")
    b.blob((.1, 0, .41), (.35, .2, .04), random.Random(9), "hay", "pumpkin_dark", jitter=.3, subdiv=1)


def trash_can(b):
    """Returns the lid builder: child pivoted at the rear hinge."""
    b.tube([(0, 0, 0), (0, 0, .95)], [.26, .3], 10, "metal_dark")
    for z in (.2, .55):
        b.tube([(0, 0, z), (0, 0, z + .04)], .29 + z * .04, 10, "metal_light")
    lid = Builder()
    lid.tube([(0, 0, .95), (0, 0, 1.0)], [.33, .3], 10, "metal_light")
    lid.tube([(0, 0, 1.0), (0, 0, 1.04)], [.3, .2], 10, "metal_dark")
    lid.boxr(-.08, .08, -.025, .025, 1.04, 1.08, "iron")
    return lid


def lamp_post(b):
    b.tube([(0, 0, 0), (0, 0, .15)], [.16, .13], 8, "iron")
    b.tube([(0, 0, .15), (0, 0, 2.4)], [.06, .045], 8, "iron")
    b.tube([(0, 0, 2.4), (0, 0, 2.5)], [.12, .1], 8, "iron")
    b.loft([[(math.cos(a) * r, math.sin(a) * r, z) for a in (2 * math.pi * i / 6 for i in range(6))]
            for r, z in ((.1, 2.5), (.17, 2.85))], ["lamp_glow"], cap_start=False, cap_end=False)
    b.loft([[(math.cos(a) * r, math.sin(a) * r, z) for a in (2 * math.pi * i / 6 for i in range(6))]
            for r, z in ((.22, 2.85), (.04, 3.05))], ["iron"], cap_start=True, cap_end=True)
    b.tube([(0, 0, 2.5), (0, 0, 2.85)], .03, 4, "iron")


def rock(b, seed, scale):
    rng = random.Random(seed)
    b.blob((0, 0, scale[2] * .55), scale, rng, "stone_light", "stone_dark", jitter=.2, floor=0, subdiv=1)


def flower_pot(b, seed=12):
    rng = random.Random(seed)
    rings = [[(math.cos(a) * r, math.sin(a) * r, z) for a in (2 * math.pi * i / 8 for i in range(8))]
             for r, z in ((.13, 0), (.19, .3), (.21, .3), (.21, .36))]
    b.loft(rings, ["pumpkin_dark", "brick", "brick"], cap_end=False)
    b.tube([(0, 0, .3), (0, 0, .34)], .19, 8, "soil")
    for i in range(5):
        a = 2 * math.pi * i / 5 + rng.uniform(-.3, .3)
        r = rng.uniform(.03, .1)
        top = Vector((math.cos(a) * r * 1.6, math.sin(a) * r * 1.6, rng.uniform(.5, .62)))
        b.tube([(math.cos(a) * r, math.sin(a) * r, .33), top], .012, 3, "stem")
        b.blob(top, (.06, .06, .045), rng, "flower_purple", "flower_pink", jitter=.1, subdiv=1)


def woodpile(b, seed=13):
    rng = random.Random(seed)
    for row, (n, z) in enumerate(((5, .1), (4, .27), (3, .44), (2, .6))):
        for i in range(n):
            x = (i - (n - 1) / 2) * .19
            b.tube([(x, -.45 + rng.uniform(-.04, .04), z), (x, .45 + rng.uniform(-.04, .04), z)], .09, 6,
                   ["bark"], twist=rng.uniform(0, 1))


def dog_house(b):
    b.boxr(-.45, .45, -.55, .55, 0, .7, "wood_light", top="wood_mid")
    b.prism_y([(-.45, .7), (.45, .7), (0, 1.05)], -.55, .55, "wood_light")
    b.prism_x([(-.65, .62), (0, 1.12), (.65, .62), (.65, .68), (0, 1.2), (-.65, .68)], -.52, .52,
              ["roof_red", "roof_red", "roof_red_dark", "roof_red", "roof_red", "roof_red_dark"])
    arch = [(-.18, .0), (.18, .0), (.18, .35)] + [(math.cos(a) * .18, .35 + math.sin(a) * .18)
                                                 for a in (math.pi * i / 6 for i in range(1, 6))] + [(-.18, .35)]
    b.prism_y([(x, z + .05) for x, z in arch], -.556, -.549, "soil_dark")


# ------------------------------------------------------------------ regrowth
# Small double-sided meshes that pop up as the yard gets clean.

def grass_tuft(b, seed, blades=9, height=.28):
    rng = random.Random(seed)
    faces = []
    for i in range(blades):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0, .07)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        face_dir = rng.uniform(0, 2 * math.pi)
        side = Vector((math.cos(face_dir), math.sin(face_dir), 0)) * rng.uniform(.018, .03)
        lean = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(.03, .12)
        tip = base + lean + Vector((0, 0, height * rng.uniform(.6, 1.1)))
        col = rng.choice(["grass_light", "grass_mid", "grass_mid", "grass_dark"])
        faces.append(b.face([b.vert(base - side), b.vert(base + side), b.vert(tip)], col))
    b.double_sided(faces)


def flower_clump(b, seed, petal, centre):
    rng = random.Random(seed)
    faces = []
    grass_tuft(b, seed + 1, blades=5, height=.18)
    for i in range(3):
        a = 2 * math.pi * i / 3 + rng.uniform(-.4, .4)
        r = rng.uniform(.03, .08)
        head = Vector((math.cos(a) * r * 1.5, math.sin(a) * r * 1.5, rng.uniform(.25, .38)))
        b.tube([(math.cos(a) * r, math.sin(a) * r, 0), head], .008, 3, "stem")
        n = 6
        pr = rng.uniform(.045, .06)
        tilt = Vector((rng.uniform(-.2, .2), rng.uniform(-.2, .2), 1)).normalized()
        u = tilt.cross(Vector((1, 0, 0))).normalized()
        v = tilt.cross(u)
        hub = b.vert(head + tilt * .012)
        rim = [b.vert(head + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * pr) for k in range(n)]
        for k in range(n):
            faces.append(b.face([rim[k], rim[(k + 1) % n], hub], petal))
        b.box(tuple(head + tilt * .015), (.022, .022, .018), centre)
    b.double_sided(faces)


# ------------------------------------------------------------------ ground tiles
# Flat at y = 0 on every edge so tiles butt together. Pivot at the centre.

def ground_grass(b, seed=301, size=4.0, n=8):
    rng = random.Random(seed)
    verts = {}
    for i in range(n + 1):
        for j in range(n + 1):
            edge = i in (0, n) or j in (0, n)
            z = 0 if edge else rng.uniform(-.02, .05)
            verts[i, j] = b.vert((-size / 2 + size * i / n, -size / 2 + size * j / n, z))
    for i in range(n):
        for j in range(n):
            col = rng.choice(["grass_light", "grass_mid", "grass_mid", "grass_dark"])
            b.face([verts[i, j], verts[i + 1, j], verts[i + 1, j + 1], verts[i, j + 1]], col)


def stone_slab(b, center, radius, rng, height=.05, sides=7, col=None):
    cx, cy = center
    pts = []
    for k in range(sides):
        a = 2 * math.pi * k / sides + rng.uniform(-.2, .2)
        r = radius * rng.uniform(.8, 1.05)
        pts.append((cx + math.cos(a) * r * 1.0, cy + math.sin(a) * r * .85))
    top_col = col or rng.choice(["path", "path", "path_dark"])
    b.loft([[(x, y, -.02) for x, y in pts], [(x, y, height) for x, y in pts]], ["path_dark"],
           cap_colors=("path_dark", top_col))


def ground_path(b, seed=311):
    """2 m long, 1 m wide flagstone path along Unity Z."""
    rng = random.Random(seed)
    for row in range(4):
        y = -.75 + row * .5
        if row % 2 == 0:
            stone_slab(b, (-.22, y), .23, rng)
            stone_slab(b, (.25, y + .02), .21, rng)
        else:
            stone_slab(b, (0, y), .3, rng, sides=8)


def ground_stepping(b, seed=321):
    rng = random.Random(seed)
    for k in range(4):
        y = -.75 + k * .5
        stone_slab(b, (rng.uniform(-.12, .12), y), .2, rng, height=.04, sides=7, col="stone_light")


def ground_driveway(b):
    """3 m wide, 6 m long concrete slab with control joints."""
    for k in range(3):
        y0 = -3 + k * 2
        b.boxr(-1.5 + .01, 1.5 - .01, y0 + .01, y0 + 2 - .01, -.02, .03, "concrete", top="concrete" if k % 2 == 0 else "gravel")


def ground_flowerbed(b, seed=331):
    rng = random.Random(seed)
    b.boxr(-1, 1, -.5, .5, -.02, .05, "soil", top="soil")
    b.blob((0, 0, .03), (.9, .4, .05), rng, "soil", "soil_dark", jitter=.2, floor=.02, subdiv=1)
    for x0, x1, y0, y1 in ((-1.05, 1.05, -.55, -.5), (-1.05, 1.05, .5, .55), (-1.05, -1, -.5, .5), (1, 1.05, -.5, .5)):
        b.boxr(x0, x1, y0, y1, -.02, .12, "wood_mid", top="wood_light")


# ------------------------------------------------------------------ tools (batch 1 palette)
# Pivot at the main grip; nozzle or intake towards Blender -Y (Unity +Z).

def leaf_vacuum(b):
    z = -.13
    b.tube([(0, .1, 0), (0, -.08, 0)], .02, 6, "rubber")
    b.tube([(0, .1, .004), (0, .1, -.07)], .017, 6, "green_dark")
    b.tube([(0, -.08, .004), (0, -.08, -.07)], .017, 6, "green_dark")
    b.tube([(0, -.03, -.018), (0, -.03, -.05)], .011, 4, "orange")
    # Motor housing with a fan bulge on the left side.
    b.tube([(0, .2, z), (0, .16, z), (0, -.06, z), (0, -.1, z)], [.08, .105, .105, .075], 10, "green")
    b.tube([(-.07, -.02, z), (-.13, -.02, z)], [.09, .06], 10, "green_dark")
    b.tube([(-.132, -.02, z), (-.14, -.02, z)], [.05, .03], 8, "metal_dark")
    b.tube([(0, .125, z), (0, .08, z)], .109, 10, "orange")
    # Intake tube angled down and forward, with a wide mouth.
    b.tube([(0, -.095, z - .01), (0, -.32, -.24), (0, -.6, -.36)], [.06, .05, .045], 8, "cream")
    b.tube([(0, -.58, -.352), (0, -.66, -.39)], [.05, .065], 8, "rubber")
    b.tube([(0, -.3, -.235), (0, -.34, -.25)], .056, 8, "orange")
    # Collection bag slung under the back.
    b.tube([(0, .2, z), (0, .26, -.2), (0, .3, -.34), (0, .22, -.44), (0, .08, -.42)],
           [.06, .1, .13, .11, .05], 8, ["kraft_dark", "kraft", "kraft", "kraft_dark"])
    b.tube([(0, .19, z), (0, .21, z - .02)], .07, 8, "metal_dark")


def mulcher(b):
    # Handheld shredder: drum below a top grip, chute in front.
    b.tube([(0, .09, 0), (0, -.09, 0)], .02, 6, "rubber")
    b.tube([(0, .09, .004), (0, .09, -.1)], .017, 6, "metal_dark")
    b.tube([(0, -.09, .004), (0, -.09, -.1)], .017, 6, "metal_dark")
    b.tube([(-.13, 0, -.22), (.13, 0, -.22)], [.15, .15], 12, "orange")
    b.tube([(-.14, 0, -.22), (-.15, 0, -.22)], [.12, .08], 12, "metal_dark")
    b.tube([(.14, 0, -.22), (.15, 0, -.22)], [.12, .08], 12, "metal_dark")
    # Front intake chute and rear outlet.
    b.loft([[(-.1, -.1, -.14), (.1, -.1, -.14), (.1, -.1, -.32), (-.1, -.1, -.32)],
            [(-.12, -.3, -.2), (.12, -.3, -.2), (.12, -.3, -.4), (-.12, -.3, -.4)]], ["teal"], cap_end=False,
           cap_colors=("teal", "teal"))
    b.boxr(-.122, .122, -.32, -.3, -.4, -.2, "metal_light")
    b.tube([(0, .12, -.28), (0, .26, -.36)], [.05, .06], 8, "teal_dark")
    b.box((0, -.03, -.06), (.05, .05, .07), "metal_dark")


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
    return [obj] + list(obj.children_recursive)


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for item in hierarchy(obj):
        item.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE",
    )


def tri_count(obj):
    total = 0
    for item in hierarchy(obj):
        item.data.calc_loop_triangles()
        total += len(item.data.loop_triangles)
    return total


ASSETS = []  # (object, folder, budget)


def make(name, folder, budget, fn, *args, palette="env", child=None, child_origin=(0, 0, 0)):
    b = Builder(palette)
    extra = fn(b, *args)
    obj = b.to_object(name, MATS)
    if isinstance(extra, Builder):
        sub = extra.to_object(name + "_" + (child or "Part"), MATS, origin=child_origin)
        sub.parent = obj
    ASSETS.append((obj, folder, budget))
    return obj


def kit_piece(b, kit, piece):
    build_kit_piece(b, kit, piece)


def house(b, kit, length, storeys, back_door):
    build_house(b, kit, length, storeys, back_door)


MATS = {}


def build_all():
    clear_scene()
    env_img = make_palette_image("FY_EnvPalette", ENV_PALETTE, ENV_GRID, ENV_PALETTE_PATH)
    tool_img = bpy.data.images.load(TOOL_PALETTE_PATH, check_existing=True)
    MATS["env"] = make_material("FY_EnvPalette", env_img)
    MATS["foliage"] = make_material("FY_Foliage", env_img)
    MATS["tool"] = make_material("FY_Palette", tool_img)
    make_sky_images()

    for kit_name, kit in KITS.items():
        folder = "Environment/Buildings/" + kit_name
        for piece in KIT_PIECES:
            budget = 1200 if piece in ("Gable",) else 800
            make(f"FY_{kit_name}_{piece}", folder, budget, kit_piece, kit, piece)
    make("FY_House_Cottage", "Environment/Buildings", 8000, house, KITS["Cottage"], 3, 1, False)
    make("FY_House_Brick", "Environment/Buildings", 8000, house, KITS["Brick"], 3, 1, True)
    make("FY_House_Manor", "Environment/Buildings", 16000, house, KITS["Manor"], 4, 2, True)

    fences = "Environment/Fences"
    make("FY_Fence_Picket", fences, 400, fence_picket)
    make("FY_Fence_Picket_Post", fences, 60, picket_post)
    make("FY_Fence_Picket_Gate", fences, 500, fence_picket_gate, child="Door", child_origin=(-.94, 0, 0))
    make("FY_Fence_Rail", fences, 200, fence_rail)
    make("FY_Fence_Rail_Post", fences, 60, rail_post)
    make("FY_Fence_Stone", fences, 400, fence_stone)
    make("FY_Fence_Iron", fences, 500, fence_iron)
    make("FY_Fence_Iron_Post", fences, 150, iron_post)
    make("FY_Hedge", fences, 200, hedge)

    trees = "Environment/Trees"
    make("FY_Tree_Maple", trees, 900, tree_maple, True)
    make("FY_Tree_Maple_Bare", trees, 900, tree_maple, False)
    make("FY_Tree_Oak", trees, 1000, tree_oak, True)
    make("FY_Tree_Oak_Bare", trees, 1000, tree_oak, False)
    make("FY_Tree_Birch", trees, 900, tree_birch, True)
    make("FY_Tree_Birch_Bare", trees, 900, tree_birch, False)
    make("FY_Tree_Pine", trees, 500, tree_pine)
    make("FY_Bush_Round", trees, 300, bush_round)
    make("FY_Bush_Small", trees, 300, bush_round, 107, .6)
    make("FY_Bush_Berry", trees, 400, bush_berry)

    props = "Environment/Props"
    make("FY_Mailbox", props, 300, mailbox, child="Flag", child_origin=(.14, 0, 1.18))
    make("FY_Bench", props, 400, bench)
    make("FY_Wheelbarrow", props, 500, wheelbarrow)
    make("FY_Birdbath", props, 300, birdbath)
    make("FY_Pumpkin", props, 300, pumpkin)
    make("FY_Pumpkin_Small", props, 300, pumpkin, .6, 9)
    make("FY_HayBale", props, 150, hay_bale)
    make("FY_TrashCan", props, 300, trash_can, child="Lid", child_origin=(0, .3, 1.0))
    make("FY_LampPost", props, 300, lamp_post)
    make("FY_Rock_A", props, 100, rock, 401, (.5, .4, .35))
    make("FY_Rock_B", props, 100, rock, 402, (.9, .7, .45))
    make("FY_FlowerPot", props, 400, flower_pot)
    make("FY_Woodpile", props, 500, woodpile)
    make("FY_DogHouse", props, 300, dog_house)

    regrow = "Environment/Regrowth"
    make("FY_Regrow_Grass_A", regrow, 40, grass_tuft, 501)
    make("FY_Regrow_Grass_B", regrow, 40, grass_tuft, 502, 12, .36)
    make("FY_Regrow_Flowers_White", regrow, 200, flower_clump, 511, "flower_white", "flower_yellow")
    make("FY_Regrow_Flowers_Purple", regrow, 200, flower_clump, 512, "flower_purple", "flower_yellow")
    make("FY_Regrow_Flowers_Yellow", regrow, 200, flower_clump, 513, "flower_yellow", "pumpkin_dark")

    ground = "Environment/Ground"
    make("FY_Ground_Grass_4m", ground, 200, ground_grass)
    make("FY_Ground_Path_2m", ground, 300, ground_path)
    make("FY_Ground_SteppingStones_2m", ground, 200, ground_stepping)
    make("FY_Ground_Driveway_3x6", ground, 100, ground_driveway)
    make("FY_Ground_FlowerBed_2x1", ground, 200, ground_flowerbed)

    make("FY_LeafVacuum", "Models/Tools", 700, leaf_vacuum, palette="tool")
    make("FY_Mulcher", "Models/Tools", 700, mulcher, palette="tool")

    bpy.context.view_layer.update()
    report = []
    for obj, folder, budget in ASSETS:
        export_fbx(obj, os.path.join(ART, folder, obj.name + ".fbx"))
        tris = tri_count(obj)
        line = f"{obj.name}: {tris} tris (budget {budget})" + ("  OVER BUDGET" if tris > budget else "")
        report.append((obj.name, folder, tris, budget))
        print(line)

    # Lay everything out in rows for the saved source file.
    rows = {}
    for obj, folder, _ in ASSETS:
        rows.setdefault(folder, []).append(obj)
    y = 0.0
    for folder, objs in rows.items():
        x = 0.0
        depth = 0.0
        for obj in objs:
            dims = obj.dimensions
            obj.location = (x + dims.x / 2, y, 0)
            x += dims.x + 1.0
            depth = max(depth, dims.y)
        y += depth + 2.0
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("saved", BLEND_PATH)
    return report


REPORT = build_all()
