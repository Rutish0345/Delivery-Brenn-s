"""Pueblo pixel-art en capas para Unity (resolucion nativa, tiles 32x32).

Escala pensada para personajes de 56x80 px: puerta ~30x58, casa 160x150,
arbol grande 64x84. Luz desde arriba-izquierda, sin contornos negros.

Salidas (carpeta unity/):
  tileset_suelo.png  + tileset_suelo.json   tiles 32x32 sin separacion
  props_atlas.png    + props_atlas.json     sprites RGBA, 4 px de separacion, pivote abajo-centro
  fondo_cielo.png, fondo_arboles.png        fondos tileables en horizontal
  mapa_referencia.png                       escena armada (solo preview)
  preview_x4.png                            recorte del cruce escalado x4 (solo preview)
"""
import json
import os
import random
import numpy as np
from PIL import Image

from generate_town import P, SHADOW, Canvas, disc, ellipse, shade, grass_texture, road_texture

T = 32
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'residencial')
# Ya cortados en Unity: si existen, nunca se sobrescriben (solo se verifica que coincidan).
PROTEGIDOS = {'tileset_suelo.png', 'tileset_suelo.json', 'props_atlas.png', 'props_atlas.json',
              'fondo_cielo.png', 'fondo_arboles.png', 'preview_x4.png'}
SHADOW_RGBA = (*P['gnd_d'], 110)   # sombra plana de un solo tono (alfa uniforme, sin degradado)


class Sprite:
    """Lienzo RGBA con fondo transparente."""

    def __init__(self, w, h, wrapx=False):
        self.w, self.h, self.wrapx = w, h, wrapx
        self.a = np.zeros((h, w, 4), np.uint8)

    def _ok(self, x, y):
        if self.wrapx:
            x %= self.w
        return (x, y) if 0 <= x < self.w and 0 <= y < self.h else None

    def px(self, x, y, c):
        p = self._ok(x, y)
        if p:
            self.a[p[1], p[0], :3] = P[c] if isinstance(c, str) else c
            self.a[p[1], p[0], 3] = 255

    def get(self, x, y):
        p = self._ok(x, y)
        if p and self.a[p[1], p[0], 3] == 255:
            return tuple(int(v) for v in self.a[p[1], p[0], :3])
        return None

    def px_on(self, x, y, c, allowed):
        if self.get(x, y) in allowed:
            self.px(x, y, c)

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px(xx, yy, c)

    def shadow(self, x, y):
        p = self._ok(x, y)
        if not p:
            return
        if self.a[p[1], p[0], 3] == 0:
            self.a[p[1], p[0]] = SHADOW_RGBA
        else:
            c = self.get(x, y)
            if c in SHADOW:
                self.px(x, y, SHADOW[c])


def trim(sp):
    ys, xs = np.nonzero(sp.a[:, :, 3])
    return sp.a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def shadow_ellipse(sp, cx, base, a, b):
    for (x, y) in ellipse(cx, base - b, a, b):
        sp.shadow(x, y)


# ================================================================ tiles 32x32
def tcanvas():
    return Canvas(T, T, wrapy=True)   # envuelve en x e y -> tiles sin costuras


def t_grass(rng, flowers=0):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'grass')
    grass_texture(cv, 0, 0, T, T, rng, density=14, flowers=False)
    for _ in range(flowers):
        x, y = rng.randrange(T), rng.randrange(T)
        c = rng.choice(['cream_l', 'olive_l', 'cream_l', 'pink'])
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            cv.px(x + dx, y + dy, c)
        cv.px(x, y, 'olive' if c != 'olive_l' else 'cream_l')
        cv.px(x + 1, y + 2, 'leaf_d')
        cv.px(x + 1, y + 3, 'leaf_d')
    return cv.a


def t_soil(rng, rows=False):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'gnd')
    for _ in range(70):
        x, y = rng.randrange(T), rng.randrange(T)
        c = rng.choice(['gnd_d', 'gnd_d', 'gnd_l'])
        cv.px(x, y, c)
        if rng.random() < .4:
            cv.px(x + 1, y, c)
    for _ in range(4):   # piedritas
        x, y = rng.randrange(T), rng.randrange(T)
        cv.px(x, y, 'stone_d')
        cv.px(x + 1, y, 'stone_d')
        cv.px(x, y - 1, 'stone')
    if rows:   # surcos
        for y in (6, 14, 22, 30):
            for x in range(T):
                cv.px(x, y, 'gnd_d')
                cv.px(x, y - 1, 'gnd_l' if x % 3 else 'gnd')
    return cv.a


def t_road(rng, extra=None):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'road')
    road_texture(cv, 0, 0, T, T, rng)
    if extra:
        extra(cv, rng)
    return cv.a


def add_shadow_top(cv, rng):
    for x in range(T):
        cv.px(x, 0, 'hole_d')
        cv.px(x, 1, 'road_d')
        cv.px(x, 2, 'road_d')


def add_dash_h(cv, rng):
    for x in range(8, 24):
        cv.px(x, 15, 'dash')
        cv.px(x, 16, 'dash')
        cv.px(x, 17, 'dash_d')


def add_dash_v(cv, rng):
    for y in range(8, 24):
        cv.px(15, y, 'dash')
        cv.px(16, y, 'dash')
        cv.px(17, y, 'dash_d')


def add_cracks(cv, rng, n=3):
    for _ in range(n):
        x, y = rng.randrange(T), rng.randrange(T)
        for _ in range(rng.randint(6, 12)):
            cv.px(x, y, 'hole')
            if rng.random() < .3:
                cv.px(x, y - 1, 'road_l')
            x += rng.choice([1, 1, 0])
            y += rng.choice([-1, 0, 1])


def add_pothole(cv, rng, big=True):
    cx, cy = 15, 16
    pts = ellipse(cx, cy, 8 if big else 5, 4 if big else 3) | ellipse(cx + 5, cy + 2, 4, 2)
    for (x, y) in pts:
        c = 'hole'
        if (x, y - 1) not in pts or (x, y - 2) not in pts:
            c = 'hole_d'
        elif (x, y + 1) not in pts:
            c = 'road_l'
        cv.px(x, y, c)
    for (x, y) in pts:
        if (x, y - 1) not in pts:
            cv.px(x, y - 1, 'road_l')
    if big:   # charco con reflejo
        for (x, y) in ellipse(cx + 1, cy + 1, 4, 1):
            cv.px(x, y, 'glass_d')
        cv.px(cx - 1, cy + 1, 'glass')
        cv.px(cx, cy + 1, 'glass_l')
    add_cracks(cv, rng, 2)


def add_zebra(horizontal):
    def f(cv, rng):
        for k in range(2):
            for i in range(T):
                for j in range(8):
                    x, y = (i, k * 16 + 4 + j) if horizontal else (k * 16 + 4 + j, i)
                    cv.px(x, y, 'stripe_d' if j == 7 else 'stripe')
        for _ in range(30):   # pintura gastada
            cv.px_on(rng.randrange(T), rng.randrange(T), 'road_l', {P['stripe'], P['stripe_d']})
    return f


def t_sidewalk(rng, edges=''):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'side')
    for i in range(T):   # junta de losa + brillo
        cv.px(i, 0, 'side_d')
        cv.px(0, i, 'side_d')
        cv.px(i, 1, 'side_l')
        cv.px(1, i, 'side_l')
    for _ in range(14):
        cv.px(rng.randrange(2, T), rng.randrange(2, T), rng.choice(['side_d', 'side_l']))
    x, y = rng.randrange(6, 26), rng.randrange(6, 26)   # grieta fina
    for _ in range(5):
        cv.px(x, y, 'side_d')
        x += 1
        y += rng.choice([0, 1])
    for _ in range(3):   # musgo en la junta
        p = rng.randrange(T)
        cv.px(p, 0, rng.choice(['grass_d', 'leaf']))
        cv.px(0, p, rng.choice(['grass_d', 'leaf']))
    # bordillos: (sur) cara visible de 5 px; (norte/este/oeste) solo el canto
    if 'e' in edges:
        for y in range(T):
            cv.px(28, y, 'curb_l')
            cv.px(29, y, 'curb_l')
            cv.px(30, y, 'curb')
            cv.px(31, y, 'curb')
    if 'w' in edges:
        for y in range(T):
            cv.px(0, y, 'curb')
            cv.px(1, y, 'curb')
            cv.px(2, y, 'curb_l')
            cv.px(3, y, 'curb_l')
    if 's' in edges:
        for x in range(T):
            cv.px(x, 26, 'curb_l')
            for y in range(27, 32):
                cv.px(x, y, 'curb')
            cv.px(x, 31, 'stone_d')
            if x % 16 == 0:
                for y in range(27, 31):
                    cv.px(x, y, 'stone_d')
    if 'n' in edges:
        for x in range(T):
            cv.px(x, 0, 'curb')
            cv.px(x, 1, 'curb_l')
            cv.px(x, 2, 'curb_l')
            cv.px(x, 3, 'side_d')
    return cv.a


def build_tileset(seed=3):
    rng = random.Random(seed)
    tiles = [
        ('pasto_a', t_grass(rng)), ('pasto_b', t_grass(rng)), ('pasto_c', t_grass(rng)),
        ('flores_a', t_grass(rng, 3)), ('flores_b', t_grass(rng, 5)),
        ('tierra_morada', t_soil(rng)), ('tierra_surcos', t_soil(rng, rows=True)),
        ('asfalto_a', t_road(rng)), ('asfalto_b', t_road(rng)),
        ('asfalto_sombra_bordillo', t_road(rng, add_shadow_top)),
        ('asfalto_grieta', t_road(rng, add_cracks)),
        ('linea_h', t_road(rng, add_dash_h)), ('linea_v', t_road(rng, add_dash_v)),
        ('bache_grande', t_road(rng, add_pothole)),
        ('bache_chico', t_road(rng, lambda cv, r: add_pothole(cv, r, big=False))),
        ('cebra_h', t_road(rng, add_zebra(True))), ('cebra_v', t_road(rng, add_zebra(False))),
        ('banqueta', t_sidewalk(rng)),
        ('banqueta_bordillo_s', t_sidewalk(rng, 's')), ('banqueta_bordillo_n', t_sidewalk(rng, 'n')),
        ('banqueta_bordillo_e', t_sidewalk(rng, 'e')), ('banqueta_bordillo_w', t_sidewalk(rng, 'w')),
        ('banqueta_esquina_se', t_sidewalk(rng, 'es')), ('banqueta_esquina_sw', t_sidewalk(rng, 'ws')),
        ('banqueta_esquina_ne', t_sidewalk(rng, 'en')), ('banqueta_esquina_nw', t_sidewalk(rng, 'wn')),
    ]
    cols = 8
    rows = (len(tiles) + cols - 1) // cols
    sheet = np.zeros((rows * T, cols * T, 4), np.uint8)
    meta = []
    for i, (name, a) in enumerate(tiles):
        x, y = (i % cols) * T, (i // cols) * T
        sheet[y:y + T, x:x + T, :3] = a
        sheet[y:y + T, x:x + T, 3] = 255
        meta.append(dict(name=name, index=i, x=x, y=y, w=T, h=T))
    return sheet, meta, {n: a for n, a in tiles}


# ================================================================ props
WALLS = {'madera': ('wood', 'wood_d', 'wood_l'), 'oliva': ('olive', 'olive_d', 'olive_l'),
         'azul': ('blue', 'blue_d', 'blue_l'), 'crema': ('cream', 'cream_d', 'cream_l')}


def canopy2(sp, cx, cy, r, rng):
    shade(sp, disc(cx, cy, r), 'leaf_dd', 'leaf_d', 'leaf_d')
    blobs = []
    for _ in range(int(r * 1.6)):
        a = rng.uniform(0, 6.283)
        d = rng.uniform(0, r - 6)
        blobs.append((int(cx + np.cos(a) * d), int(cy + np.sin(a) * d * .9), rng.randint(5, 9)))
    blobs.sort(key=lambda t: (t[1], t[0]))
    for (bx, by, br) in blobs:
        shade(sp, disc(bx, by, br), 'leaf_d', 'leaf', 'leaf_l', rim='leaf_dd')
    leafy = {P['leaf'], P['leaf_l']}
    for _ in range(r * 5):   # marcas de hojas
        x, y = cx + rng.randint(-r, r), cy + rng.randint(-r, r)
        if sp.get(x, y) in leafy:
            sp.px(x, y, 'leaf_d')
            sp.px_on(x + 1, y - 1, 'leaf_d', leafy)
    for (bx, by, br) in [b for b in blobs if b[0] < cx + 2 and b[1] < cy + 2][:6]:   # racimos al sol
        x, y = bx - br // 2, by - br // 2
        for dx, dy in ((0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (0, 2)):
            sp.px_on(x + dx, y + dy, 'leaf_h', leafy | {P['leaf_d']})


def make_tree(w, h, r, rng):
    sp = Sprite(w, h)
    base = h - 1
    cx = w // 2
    shadow_ellipse(sp, cx + 3, base, r - 4, max(3, r // 7))
    for y in range(base - h // 3 - 4, base + 1):
        for i, c in enumerate(('wood_l', 'wood_l', 'wood', 'wood', 'wood', 'wood_d', 'wood_d', 'wood_dd')):
            sp.px(cx - 4 + i, y, c)
        if y % 7 == 0:
            sp.px(cx - 1, y, 'wood_d')
            sp.px(cx, y + 1, 'wood_d')
    for dx, c in ((-6, 'wood'), (-5, 'wood_l'), (4, 'wood_d'), (5, 'wood_dd')):   # raices
        sp.px(cx + dx, base, c)
        sp.px(cx + dx, base - 1, c) if abs(dx) < 6 else None
    canopy2(sp, cx, r + 1, r, rng)
    for _ in range(5):   # musgo en el tronco
        sp.px(cx - 4 + rng.randint(0, 2), base - rng.randint(1, 10), rng.choice(['leaf', 'leaf_d']))
    return sp


def make_pine(rng):
    w, h = 44, 84
    sp = Sprite(w, h)
    base, cx = h - 1, w // 2
    shadow_ellipse(sp, cx + 3, base, 14, 3)
    for y in range(base - 14, base + 1):
        for i, c in enumerate(('wood_l', 'wood', 'wood', 'wood_d', 'wood_dd')):
            sp.px(cx - 2 + i, y, c)
    for tier, (top, bot) in enumerate(((2, 30), (16, 50), (32, 70))):
        pts = set()
        for y in range(top, bot):
            half = 3 + (y - top) * (9 + tier * 4) // (bot - top)
            for x in range(cx - half, cx + half + 1):
                pts.add((x, y))
        shade(sp, pts, 'bush_d', 'bush', 'bush_l', rim='bush_dd')
        for x in range(cx - 20, cx + 21, 3):   # puntas aserradas
            if (x, bot - 1) in pts:
                sp.px(x, bot, 'bush_dd')
    return sp


def make_bush(w, h, rng, pal=('bush_dd', 'bush_d', 'bush', 'bush_l'), flowers=None):
    sp = Sprite(w, h + 8)
    base = h + 7
    dd, d, b, l = pal
    shadow_ellipse(sp, w // 2 + 2, base, w // 2 - 1, 2)
    n = max(2, w // 9)
    blobs = []
    for i in range(n):
        bx = int((i + .5) * w / n) + rng.randint(-2, 2)
        rr = rng.randint(h // 2 - 1, h // 2 + 2)
        blobs.append((bx, base - rr, rr))
    for _ in range(n // 2):
        bx = rng.randint(w // 4, 3 * w // 4)
        blobs.append((bx, base - h + 6, rng.randint(5, 7)))
    for (bx, by, rr) in sorted(blobs, key=lambda t: t[1]):
        shade(sp, {p for p in disc(bx, by, rr) if p[1] <= base - 1 and 0 <= p[0] < w}, d, b, l, rim=dd)
    for _ in range(w):
        x, y = rng.randrange(w), rng.randrange(base)
        sp.px_on(x, y, d, {P[b]})
    if flowers:
        for _ in range(w // 5):
            x, y = rng.randrange(2, w - 2), rng.randrange(4, base - 3)
            if sp.get(x, y) in (P[b], P[l]):
                sp.px(x, y, flowers)
                sp.px(x + 1, y + 1, 'cream_l' if flowers != 'cream_l' else 'olive_l')
    return sp


def window2(sp, x, y, w, h, trim, trim_d, rng, box=None, shutters=None):
    if shutters:
        for sx in (x - 6, x + w):
            sp.rect(sx, y, 6, h, shutters[0])
            for yy in range(y + 2, y + h - 1, 3):
                sp.rect(sx + 1, yy, 4, 1, shutters[1])
            for yy in range(y, y + h):
                sp.px(sx + 5, yy, shutters[1])
    sp.rect(x, y, w, h, trim)
    sp.rect(x + w - 2, y, 2, h, trim_d)
    sp.rect(x, y + h - 2, w, 2, trim_d)
    gx0, gy0, gw, gh = x + 2, y + 2, w - 4, h - 4
    mx, my = gx0 + gw // 2, gy0 + gh // 2
    for yy in range(gy0, gy0 + gh):
        for xx in range(gx0, gx0 + gw):
            px0 = gx0 if xx < mx else mx + 2
            py0 = gy0 if yy < my else my + 2
            lx, ly = xx - px0, yy - py0
            c = 'glass'
            if lx + ly in (2, 3) or lx + ly == 8:
                c = 'glass_l'
            elif ly == 0 or lx == 0:
                c = 'glass_d'
            sp.px(xx, yy, c)
    sp.rect(mx, gy0, 2, gh, trim)
    sp.rect(gx0, my, gw, 2, trim)
    sp.px(mx + 1, gy0, trim_d)
    sp.rect(x - 2, y + h, w + 4, 2, 'cream_l')
    sp.rect(x - 2, y + h + 2, w + 4, 1, 'cream_d')
    if box:
        sp.rect(x, y + h + 3, w, 5, 'wood')
        sp.rect(x, y + h + 3, w, 1, 'wood_l')
        sp.rect(x, y + h + 7, w, 1, 'wood_dd')
        for i in range(0, w, 6):
            sp.px(x + i, y + h + 5, 'wood_d')
        for i in range(w):   # plantas que desbordan
            top = rng.randint(1, 4)
            for k in range(top):
                sp.px(x + i, y + h + 2 - k, rng.choice(['leaf', 'leaf_d', 'leaf_l']))
            if rng.random() < .15:
                sp.px(x + i, y + h + 8, 'leaf_d')
        for i in range(2, w - 2, 5):
            fy = y + h - rng.randint(1, 3)
            sp.px(x + i, fy, box)
            sp.px(x + i + 1, fy, box)
            sp.px(x + i, fy + 1, box)


def make_house(kind, door, gable, shutters, seed):
    rng = random.Random(seed)
    W, H = 160, 150
    sp = Sprite(W, H)
    c, cd, cl = WALLS[kind]
    base, wt, wx0, wx1 = H - 1, 70, 6, 154
    trims = ('wood', 'wood_d') if kind == 'crema' else ('cream_l', 'cream_d')
    # pared
    for y in range(wt, base + 1):
        k = (y - wt) % 7
        for x in range(wx0, wx1):
            col = c
            if kind != 'crema':
                if k == 6:
                    col = cd
                elif k == 0 and (x // 23 + (y - wt) // 7) % 3 == 0:
                    col = cl
            else:
                rv = rng.random()
                col = cd if rv < .03 else cl if rv < .05 else c
            sp.px(x, y, col)
    if kind == 'madera':
        for y in range(wt, base, 7):
            for _ in range(3):
                sx = rng.randrange(wx0 + 6, wx1 - 6)
                for k in range(6):
                    sp.px(sx, y + k, cd)
                sp.px(sx + 1, y + 2, cl)
    for y in range(wt, base + 1):
        for i in range(4):
            sp.px(wx0 + i, y, trims[0] if i < 3 else trims[1])
            sp.px(wx1 - 1 - i, y, trims[1])
    for y in range(76, 80):   # sombra del alero
        for x in range(wx0, wx1):
            sp.px(x, y, cd if y < 79 or x % 2 else c)
    # cimiento de piedra
    for y in range(base - 7, base + 1):
        r = y - (base - 7)
        for x in range(wx0, wx1):
            col = 'stone'
            if r == 0:
                col = 'stone_l'
            elif r == 4 or (x + (r // 4) * 6) % 12 == 0:
                col = 'stone_d'
            sp.px(x, y, col)
    # ventanas
    box = rng.choice(['pink', 'cream_l', 'olive_l', None])
    for wxp in (18, 112):
        window2(sp, wxp, 90, 30, 28, *trims, rng, box=box, shutters=shutters)
    # puerta 30x58
    dc, dcd = door
    dx, dy, dw, dh = 65, base - 8 - 57, 30, 58
    sp.rect(dx - 3, dy - 3, dw + 6, dh + 3, trims[0])
    sp.rect(dx + dw, dy - 3, 3, dh + 3, trims[1])
    sp.rect(dx - 5, dy - 5, dw + 10, 2, trims[1])   # dintel
    sp.rect(dx - 5, dy - 6, dw + 10, 1, trims[0])
    sp.rect(dx, dy, dw, dh, dc)
    sp.rect(dx + dw - 2, dy, 2, dh, dcd)
    for (px0, py0, pw, ph) in ((dx + 4, dy + 16, 9, 16), (dx + 17, dy + 16, 9, 16),
                               (dx + 4, dy + 36, 9, 17), (dx + 17, dy + 36, 9, 17)):
        sp.rect(px0, py0, pw, 1, dcd)
        sp.rect(px0, py0, 1, ph, dcd)
    sp.rect(dx + 6, dy + 4, 18, 8, 'glass')   # ventanita
    sp.rect(dx + 6, dy + 4, 18, 1, 'glass_d')
    sp.px(dx + 8, dy + 6, 'glass_l')
    sp.px(dx + 9, dy + 7, 'glass_l')
    sp.px(dx + 10, dy + 8, 'glass_l')
    sp.rect(dx + 14, dy + 4, 2, 8, dcd)
    sp.rect(dx + 23, dy + 32, 2, 3, 'gold')    # perilla
    sp.px(dx + 23, dy + 32, 'gold_l')
    # farol de porche
    lx, ly = dx + dw + 6, dy + 8
    sp.rect(lx + 2, ly - 2, 2, 2, 'lamp_d')
    sp.rect(lx, ly, 6, 1, 'lamp_d')
    sp.rect(lx, ly + 1, 6, 6, 'gold')
    sp.rect(lx + 1, ly + 2, 2, 3, 'gold_l')
    sp.rect(lx + 5, ly + 1, 1, 6, 'gold_d')
    sp.rect(lx, ly + 7, 6, 1, 'lamp_d')
    # escalones
    sp.rect(dx - 4, base - 7, dw + 8, 4, 'stone')
    sp.rect(dx - 4, base - 7, dw + 8, 1, 'stone_l')
    sp.rect(dx - 8, base - 3, dw + 16, 4, 'stone')
    sp.rect(dx - 8, base - 3, dw + 16, 1, 'stone_l')
    sp.rect(dx - 8, base, dw + 16, 1, 'stone_d')
    sp.rect(dx + dw + 4, base - 7, 2, 4, 'stone_d')
    sp.rect(dx + dw + 6, base - 3, 2, 4, 'stone_d')
    # hiedra
    leafy_ok = lambda x: wx0 - 1 <= x <= wx1
    for _ in range(2 if rng.random() < .5 else 1):
        x = rng.choice([wx0 + 2, wx0 + 5, wx1 - 4, wx1 - 7])
        y = base - 6
        for _ in range(rng.randint(40, 75)):
            sp.px(x, y, 'leaf_dd')
            if rng.random() < .7:
                for ddx, ddy, col in ((-1, 0, 'leaf'), (-2, 0, 'leaf_l'), (1, 0, 'leaf_d'),
                                      (0, -1, 'leaf_l'), (1, -1, 'leaf'), (2, 0, 'leaf_d')):
                    if rng.random() < .6 and leafy_ok(x + ddx):
                        sp.px(x + ddx, y + ddy, col)
            y -= 1
            if rng.random() < .3:
                x = max(wx0, min(wx1 - 1, x + rng.choice([-1, 1])))
    # techo a cuatro aguas, tejas pizarra-teal
    top, roof_h, inset_top = 12, 64, 30
    for r in range(roof_h):
        y = top + r
        ins = max(0, inset_top - r)
        x0, x1 = ins, W - ins
        rb = roof_h - 1 - r
        course, k = rb // 6, rb % 6
        for x in range(x0, x1):
            m = (x + course * 7) % 14
            if r == 0:
                col = 'roof_l'
            elif rb == 0:
                col = 'roof_dd'
            elif rb in (1, 2):
                col = 'roof_d'
            elif k == 0:
                col = 'roof_d'
            elif m == 0 and k > 1:
                col = 'roof_d'
            elif k == 5 and 1 <= m <= 6:
                col = 'roof_l'
            else:
                col = 'roof'
            if x - x0 < 2 and 0 < r and rb > 2:
                col = 'roof_l'
            if x1 - x <= 2 and r > 0:
                col = 'roof_dd'
            elif x1 - x <= 10 and col == 'roof' and r > 0:
                col = 'roof_d'   # cara lateral en sombra
            sp.px(x, y, col)
    for _ in range(40):   # desgaste
        x, y = rng.randrange(8, W - 8), rng.randrange(top + 4, top + roof_h - 4)
        sp.px_on(x, y, rng.choice(['roof_d', 'roof_l']), {P['roof']})
    for _ in range(rng.randint(6, 11)):   # musgo
        mx, my = rng.randrange(6, W - 10), top + roof_h - 4 - rng.randint(0, 22)
        for (x, y) in disc(mx, my, rng.randint(1, 3)):
            if sp.get(x, y) in (P['roof'], P['roof_d'], P['roof_l']):
                sp.px(x, y, 'bush' if (x + y) % 3 else 'bush_l' if y < my else 'bush_d')
    # frontón sobre la puerta
    if gable:
        gx, gh, half_b = 80, 34, 34
        ya = 79 - gh
        for j in range(gh + 1):
            y = ya + j
            half = 2 + j * half_b // gh
            for x in range(gx - half, gx + half):
                kk = (y - wt) % 7
                col = c if kind == 'crema' or kk != 6 else cd
                sp.px(x, y, col)
            for i, col in enumerate(('roof_l', 'roof_l', 'roof', 'roof_d')):
                sp.px(gx - half - 4 + i, y, col)
            for i, col in enumerate(('roof_d', 'roof_dd', 'roof_dd', 'roof_d')):
                sp.px(gx + half + i, y, col)
        sp.rect(gx - 2, ya - 2, 4, 2, 'roof_l')
        ay = ya + 18
        for (x, y) in disc(gx, ay, 6):
            sp.px(x, y, trims[0])
        for (x, y) in disc(gx, ay, 4):
            sp.px(x, y, 'glass')
        sp.rect(gx - 4, ay, 9, 1, trims[0])
        sp.rect(gx, ay - 4, 1, 9, trims[0])
        sp.px(gx - 2, ay - 2, 'glass_l')
        sp.px(gx - 3, ay - 1, 'glass_l')
        sp.px(gx + 2, ay + 3, 'glass_d')
        for y in range(76, 80):
            for x in range(gx - half_b, gx + half_b):
                if sp.get(x, y) == P[c]:
                    sp.px(x, y, cd)
    # chimenea de ladrillo
    cx = rng.choice([40, 108])
    for y in range(4, 40):
        ry = y - 4
        for x in range(cx, cx + 14):
            col = 'wood'
            if ry % 4 == 3:
                col = 'wood_d'
            elif (x - cx + (ry // 4) % 2 * 4) % 8 == 7:
                col = 'wood_d'
            if x - cx < 2 and ry % 4 != 3:
                col = 'wood_l'
            if x - cx >= 12:
                col = 'wood_dd'
            sp.px(x, y, col)
    sp.rect(cx - 2, 0, 18, 4, 'stone')
    sp.rect(cx - 2, 0, 17, 1, 'stone_l')
    sp.rect(cx - 2, 3, 18, 1, 'stone_d')
    sp.rect(cx + 14, 40, 6, 2, 'roof_dd')   # sombra de la chimenea en el techo
    # arbustos al pie de la casa
    for bx in (wx0 - 2, wx1 - 34):
        bsh = make_bush(36, 14, rng, flowers=rng.choice(['pink', 'cream_l', 'olive_l']))
        a = bsh.a
        hh, ww = a.shape[:2]
        for yy in range(hh - 3):   # sin su sombra: la casa ya apoya en el suelo
            for xx in range(ww):
                if a[yy, xx, 3] == 255:
                    sp.px(bx + xx, base - (hh - 3) + 1 + yy, tuple(int(v) for v in a[yy, xx, :3]))
    return sp


def make_fence(ivy_seed=None):
    sp = Sprite(32, 40)
    base = 39
    for x in range(32):
        for y in (base - 28, base - 10):
            sp.px(x, y, 'fence')
            sp.px(x, y + 1, 'fence_d')
            sp.px(x, y + 2, 'fence_d')
        sp.shadow(x, base)
        sp.shadow(x, base - 1)
    cols = ('fence_l', 'fence_l', 'fence', 'fence', 'fence', 'fence_d')
    for i in range(4):
        x0 = 1 + 8 * i
        for y in range(base - 33, base + 1):
            for k in range(6):
                sp.px(x0 + k, y, cols[k])
        for k in (1, 2, 3, 4):
            sp.px(x0 + k, base - 34, cols[min(k, 4)])
        sp.px(x0 + 2, base - 35, 'fence_l')
        sp.px(x0 + 3, base - 35, 'fence')
        sp.px(x0 + 5, base - 34, 'fence_d')
        sp.rect(x0, base, 6, 1, 'fence_d')
    rng = random.Random(ivy_seed or 1)
    for _ in range(18):   # vetas
        sp.px_on(rng.randrange(32), rng.randrange(6, 38), 'fence_d', {P['fence']})
    for x in range(32):   # pasto en la base
        for k in range(rng.randint(0, 3)):
            sp.px(x, base - k, rng.choice(['grass_d', 'grass', 'leaf_d']))
    if ivy_seed:
        x, y = rng.randrange(4, 12), base - 2
        for _ in range(42):
            sp.px(x, y, 'leaf_dd')
            for ddx, ddy, c in ((-1, 0, 'leaf'), (1, 0, 'leaf_d'), (0, -1, 'leaf_l'), (1, -1, 'leaf')):
                if rng.random() < .6:
                    sp.px(x + ddx, y + ddy, c)
            x += rng.choice([1, 1, 0])
            y += rng.choice([-1, -1, 0, 1]) if y > base - 30 else 1
            if x > 30:
                break
    return sp


def make_post(sp, x, base, h):
    for y in range(base - h + 4, base + 1):
        for i, c in enumerate(('wood_l', 'wood_l', 'wood', 'wood', 'wood', 'wood', 'wood_d', 'wood_dd')):
            sp.px(x + i, y, c)
    sp.rect(x - 1, base - h, 10, 4, 'stone')
    sp.rect(x - 1, base - h, 9, 1, 'stone_l')
    sp.rect(x - 1, base - h + 3, 10, 1, 'stone_d')


def make_gate():
    sp = Sprite(64, 44)
    for x in range(64):
        sp.shadow(x, 43)
    make_post(sp, 1, 43, 44)
    make_post(sp, 55, 43, 44)
    return sp


def make_fence_end():
    sp = Sprite(10, 44)
    make_post(sp, 1, 43, 44)
    return sp


def make_mailbox(col, cold, coll):
    sp = Sprite(20, 48)
    base = 47
    shadow_ellipse(sp, 11, base, 6, 1)
    for y in range(18, base + 1):
        for i, c in enumerate(('wood_l', 'wood', 'wood_d', 'wood_dd')):
            sp.px(8 + i, y, c)
    for y in range(6, 19):
        x0 = 3 if y == 6 else 2 if y == 7 else 1
        for x in range(x0, 19 - (x0 - 1)):
            c = col
            if y <= 8 or x == x0:
                c = coll
            if y >= 17 or x >= 17:
                c = cold
            sp.px(x, y, c)
    sp.rect(1, 9, 3, 8, cold)          # tapa frontal
    sp.px(2, 12, 'stone_l')
    for y in range(1, 13):            # banderita
        sp.px(18, y, 'wood_d')
    sp.rect(15, 1, 3, 4, 'pink')
    sp.rect(15, 4, 3, 1, 'pink_d')
    for k in range(4):
        sp.px(7 + k, base - k, rng_leaf(k))
    return sp


def rng_leaf(k):
    return ('grass_d', 'leaf', 'grass_d', 'leaf_d')[k]


def make_lamp():
    sp = Sprite(24, 112)
    base = 111
    shadow_ellipse(sp, 14, base, 9, 2)
    sp.rect(6, base - 7, 12, 8, 'lamp')
    sp.rect(6, base - 7, 12, 1, 'lamp_l')
    sp.rect(6, base - 7, 1, 8, 'lamp_l')
    sp.rect(16, base - 6, 2, 7, 'lamp_d')
    for y in range(30, base - 7):
        for i, c in enumerate(('lamp_l', 'lamp_l', 'lamp', 'lamp', 'lamp_d', 'lamp_d')):
            sp.px(9 + i, y, c)
    for y, c in ((60, 'lamp_l'), (61, 'lamp_d'), (88, 'lamp_l'), (89, 'lamp_d')):
        sp.rect(8, y, 8, 1, c)
    sp.rect(11, 1, 2, 2, 'lamp_l')
    sp.rect(10, 3, 4, 2, 'lamp')
    sp.rect(6, 5, 12, 2, 'lamp')
    sp.rect(6, 5, 12, 1, 'lamp_l')
    sp.rect(4, 7, 16, 2, 'lamp_d')
    for y in range(9, 25):
        sp.px(5, y, 'lamp')
        sp.px(18, y, 'lamp_d')
        for x in range(6, 18):
            sp.px(x, y, 'gold')
        sp.px(17, y, 'gold_d')
        sp.px(11, y, 'gold_d')
        sp.px(12, y, 'gold_d')
    sp.rect(7, 11, 3, 8, 'gold_l')
    sp.rect(13, 11, 2, 5, 'gold_l')
    sp.rect(6, 22, 12, 3, 'gold_d')
    sp.rect(5, 25, 14, 2, 'lamp_d')
    sp.rect(9, 27, 6, 3, 'lamp_d')
    return sp


def make_sign():
    sp = Sprite(44, 96)
    base = 95
    shadow_ellipse(sp, 24, base, 6, 1)
    for y in range(6, base + 1):
        for i, c in enumerate(('lamp_l', 'lamp', 'lamp', 'lamp_d')):
            sp.px(20 + i, y, c)
    sp.rect(18, base - 2, 8, 3, 'lamp_d')
    sp.rect(20, 3, 4, 3, 'lamp_l')
    # placa con nombre (morada, letras doradas)
    sp.rect(0, 6, 44, 10, 'purple')
    sp.rect(0, 6, 44, 1, 'cream_l')
    sp.rect(0, 14, 44, 2, 'purple_d')
    sp.rect(42, 6, 2, 10, 'purple_d')
    glyphs = ['111', '101', '111', '000', '110', '101', '110', '000', '111', '100', '111', '000',
              '101', '111', '101', '000', '111', '010', '010', '000', '011', '100', '110']
    for gi, g in enumerate(glyphs):
        for k, on in enumerate(g):
            if on == '1':
                sp.px(4 + gi + gi // 4 * 0, 8 + k, 'gold')
    # placa cruzada (rosa, vista en escorzo)
    sp.rect(16, 20, 12, 7, 'pink')
    sp.rect(16, 20, 12, 1, 'cream_l')
    sp.rect(16, 25, 12, 2, 'pink_d')
    for x in (18, 20, 23, 25):
        sp.px(x, 22, 'gold_l')
        sp.px(x, 23, 'gold_l')
    return sp


def make_car_side(col, cold, coll, seed):
    rng = random.Random(seed)
    sp = Sprite(96, 52)
    base = 51
    shadow_ellipse(sp, 50, base, 44, 3)
    # carroceria
    for y in range(22, 44):
        inset = 3 if y == 22 else 1 if y == 23 else 0
        for x in range(2 + inset, 94 - inset):
            c = col
            if y <= 24:
                c = coll
            elif y >= 40 or x >= 91:
                c = cold
            elif x <= 3:
                c = coll
            sp.px(x, y, c)
    sp.rect(4, 31, 88, 1, cold)            # moldura
    sp.rect(48, 25, 1, 15, cold)           # linea de puerta
    sp.rect(30, 27, 4, 1, coll)
    sp.rect(54, 27, 4, 1, coll)
    sp.rect(68, 18, 24, 5, coll)           # cofre visto desde arriba
    sp.rect(68, 18, 24, 1, 'cream_l')
    sp.rect(4, 19, 18, 4, coll)            # cajuela
    # cabina: techo + ventanas
    for y in range(4, 22):
        x0 = 26 - min(6, (y - 4) // 2) if y > 10 else 26
        x1 = 68 + min(6, (y - 4) // 2) if y > 10 else 68
        for x in range(x0, x1):
            if y < 11:
                c = coll if y < 6 or x < 30 else col
                if x >= x1 - 2:
                    c = cold
            else:
                c = 'glass'
                if x in (x0, x0 + 1, x1 - 2, x1 - 1, 46, 47) or y == 21:
                    c = col if x not in (x1 - 2, x1 - 1) else cold
                elif (x - y) % 13 in (0, 1):
                    c = 'glass_l'
                elif y == 11:
                    c = 'glass_d'
            sp.px(x, y, c)
    sp.rect(26, 4, 42, 1, 'cream_l')
    sp.rect(70, 14, 3, 3, cold)            # espejo
    # luces
    sp.rect(90, 26, 4, 4, 'gold')
    sp.px(90, 26, 'gold_l')
    sp.rect(2, 26, 2, 4, 'pink')
    sp.rect(0, 38, 6, 3, 'stone')
    sp.rect(90, 38, 6, 3, 'stone')
    sp.rect(0, 40, 6, 1, 'stone_d')
    sp.rect(90, 40, 6, 1, 'stone_d')
    # ruedas
    for wx in (22, 74):
        for (x, y) in disc(wx, 42, 10):
            if y <= 42 and sp.get(x, y):
                sp.px(x, y, 'hole')
        for (x, y) in disc(wx, 43, 8):
            sp.px(x, y, 'lamp_d' if (x - wx) + (y - 43) < 6 else 'hole_d')
        for (x, y) in disc(wx, 43, 4):
            sp.px(x, y, 'stone' if x <= wx else 'stone_d')
        sp.px(wx - 1, 42, 'stone_l')
        sp.px(wx, 43, 'lamp')
    for _ in range(6):   # polvo / lodo en la parte baja
        sp.px_on(rng.randrange(6, 90), rng.randrange(36, 40), cold, {P[col]})
    return sp


def make_car_front(col, cold, coll):
    sp = Sprite(52, 84)
    base = 83
    shadow_ellipse(sp, 28, base, 24, 3)
    for y in range(4, 80):   # silueta de la carroceria
        x0 = 4 if 8 < y < 76 else 6
        for x in range(x0, 52 - x0):
            sp.px(x, y, col)
        sp.px(x0, y, coll)
        sp.px(x0 + 1, y, coll)
        sp.px(51 - x0, y, cold)
        sp.px(50 - x0, y, cold)
    sp.rect(10, 6, 32, 6, 'glass_d')      # medallon trasero
    sp.rect(10, 12, 32, 18, coll)         # techo
    sp.rect(10, 12, 32, 1, 'cream_l')
    sp.rect(38, 12, 4, 18, col)
    for y in range(30, 46):               # parabrisas
        ins = max(0, 6 - (y - 30) // 2)
        for x in range(8 + ins, 44 - ins):
            c = 'glass'
            if (x + y) % 15 in (0, 1, 2):
                c = 'glass_l'
            elif y == 30:
                c = 'glass_d'
            sp.px(x, y, c)
    for y in range(46, 66):               # cofre
        for x in range(6, 46):
            sp.px(x, y, coll if x < 14 or y < 48 else col)
    sp.rect(25, 48, 2, 18, cold)
    sp.rect(6, 66, 40, 9, cold)           # parrilla
    for x in range(14, 38, 3):
        sp.rect(x, 68, 1, 5, 'hole')
    sp.rect(7, 67, 6, 5, 'gold')          # faros
    sp.rect(39, 67, 6, 5, 'gold')
    sp.px(8, 68, 'gold_l')
    sp.px(40, 68, 'gold_l')
    sp.rect(4, 75, 44, 4, 'stone')        # defensa
    sp.rect(4, 75, 44, 1, 'stone_l')
    sp.rect(4, 78, 44, 1, 'stone_d')
    for wx in (2, 46):                     # llantas
        sp.rect(wx, 64, 4, 18, 'hole_d')
        sp.rect(wx, 64, 4, 2, 'lamp_d')
    sp.rect(0, 34, 4, 4, cold)            # espejos
    sp.rect(48, 34, 4, 4, cold)
    return sp


def make_stone():
    sp = Sprite(16, 9)
    pts = ellipse(7, 4, 7, 4)
    shade(sp, pts, 'stone_d', 'stone', 'stone_l')
    for x in range(16):
        sp.px_on(x, 8, 'grass_d', set()) if False else None
    return sp


def make_veg_patch(rng):
    sp = Sprite(32, 22)
    for row, y in enumerate((5, 12, 19)):
        for x in range(2, 30, 6):
            ox = x + rng.randint(-1, 1)
            for (px_, py, c) in ((0, 0, 'leaf_d'), (1, 0, 'leaf'), (-1, -1, 'leaf'), (2, -1, 'leaf_l'),
                                 (0, -2, 'leaf_l'), (1, -2, 'leaf'), (0, 1, 'leaf_dd'), (1, 1, 'leaf_dd')):
                sp.px(ox + px_, y + py + (1 if row == 2 else 0) + 1, c)
            if rng.random() < .3:
                sp.px(ox + 1, y - 3, rng.choice(['olive_l', 'cream_l', 'pink']))
    return sp


def build_props(seed=21):
    rng = random.Random(seed)
    door_pink, door_purple = ('pink', 'pink_d'), ('purple', 'purple_d')
    door_teal, door_wood = ('roof_d', 'roof_dd'), ('wood_d', 'wood_dd')
    props = {
        'casa_madera': make_house('madera', door_pink, True, None, 1),
        'casa_crema': make_house('crema', door_teal, False, ('roof', 'roof_d'), 2),
        'casa_azul': make_house('azul', door_wood, True, None, 3),
        'casa_oliva': make_house('oliva', door_purple, False, ('roof', 'roof_d'), 4),
        'arbol_grande': make_tree(64, 84, 28, random.Random(5)),
        'arbol_grande_b': make_tree(64, 84, 28, random.Random(6)),
        'arbol_mediano': make_tree(52, 70, 22, random.Random(7)),
        'pino': make_pine(rng),
        'arbusto_grande': make_bush(48, 24, rng),
        'arbusto_flores': make_bush(48, 24, rng, flowers='pink'),
        'arbusto_flores_crema': make_bush(40, 20, rng, flowers='cream_l'),
        'arbusto_chico': make_bush(30, 16, rng),
        'seto': make_bush(64, 22, rng),
        'arbusto_lejano': make_bush(40, 18, rng, pal=('mid_d', 'mid_d', 'mid', 'mid_l')),
        'barda': make_fence(),
        'barda_hiedra': make_fence(ivy_seed=9),
        'barda_porton': make_gate(),
        'barda_poste_final': make_fence_end(),
        'buzon_teal': make_mailbox('roof', 'roof_dd', 'roof_l'),
        'buzon_terracota': make_mailbox('wood', 'wood_dd', 'wood_l'),
        'farol': make_lamp(),
        'senal_calle': make_sign(),
        'auto_lateral_teal': make_car_side('roof', 'roof_dd', 'roof_l', 1),
        'auto_lateral_terracota': make_car_side('wood', 'wood_dd', 'wood_l', 2),
        'auto_lateral_oliva': make_car_side('olive', 'olive_d', 'olive_l', 3),
        'auto_frontal_azul': make_car_front('blue', 'blue_d', 'blue_l'),
        'piedra_camino': make_stone(),
        'huerto_plantas': make_veg_patch(rng),
    }
    return {k: trim(v) for k, v in props.items()}


def pack_atlas(props, max_w=1024, pad=4):
    items = sorted(props.items(), key=lambda kv: (-kv[1].shape[0], kv[0]))
    x, y, row_h, placed = pad, pad, 0, []
    for name, a in items:
        h, w = a.shape[:2]
        if x + w + pad > max_w:
            x, y, row_h = pad, y + row_h + pad, 0
        placed.append((name, x, y, w, h))
        x += w + pad
        row_h = max(row_h, h)
    H = y + row_h + pad
    atlas = np.zeros((H, max_w, 4), np.uint8)
    meta = []
    for name, x, y, w, h in placed:
        atlas[y:y + h, x:x + w] = props[name]
        meta.append(dict(name=name, x=x, y=y, w=w, h=h, y_unity=H - y - h))
    meta.sort(key=lambda m: m['name'])
    return atlas, meta


# ================================================================ fondos
def build_sky(rng, w=1024, h=160):
    cv = Canvas(w, h)
    cv.rect(0, 0, w, h, 'sky')
    for y in range(12):
        for x in range(w):
            if y < 4 or (y < 8 and (x + y) % 2 == 0) or (y < 12 and (x + y) % 4 == 0):
                cv.px(x, y, 'sky_l')
    x = 0
    while x < w:
        cy, cw = rng.randint(24, 70), rng.randint(50, 110)
        pts, bx = set(), x
        while bx < x + cw:
            r = rng.randint(6, 12)
            pts |= disc(bx, cy - r // 2, r)
            bx += rng.randint(7, 12)
        pts = {(px_, py) for (px_, py) in pts if py <= cy + 4}
        blocks = {(px_ // 3, py // 3) for (px_, py) in pts}
        pts = {(bx_ * 3 + dx, by * 3 + dy) for (bx_, by) in blocks for dx in range(3) for dy in range(3)}
        shade(cv, pts, 'cloud_s', 'cloud', 'cloud', rim='cloud_s')
        bottom = max(py for _, py in pts)
        for (px_, py) in pts:
            if py >= bottom - 2:
                cv.px(px_, py, 'cloud_ss')
        x += cw + rng.randint(60, 160)
    return cv.a


def build_far_trees(rng, w=1024, h=112):
    sp = Sprite(w, h, wrapx=True)
    for color, ybase, rmin, rmax in (('far1', 40, 9, 16), ('far2', 64, 8, 14)):
        x = 0
        while x < w:
            cy = ybase - rng.randint(0, 10)
            if rng.random() < .3:   # conifera escalonada
                hgt = rng.randint(28, 40)
                for k in range(hgt):
                    half = (k // 2) if k % 6 < 4 else (k // 2) - 2
                    for xx in range(x - max(1, half // 2), x + max(1, half // 2) + 1):
                        sp.px(xx, cy - hgt // 2 + k, color)
            else:
                for (px_, py) in disc(x, cy, rng.randint(rmin, rmax)):
                    sp.px(px_, py, color)
            sp.rect(x - 14, cy, 30, h - cy, color)
            x += rng.randint(12, 22)
    return sp.a


# ================================================================ mapa de referencia
def build_reference(tiles, props, sky, far, beds=True, ground_hook=None, registro=None):
    """beds=False quita los canteros de tierra morada; ground_hook(m, grid) dibuja sobre el suelo antes de los props.
    registro: dict opcional donde se guarda el trazado (grid, objs y el suelo bajo cada bache) para exportarlo."""
    COLS, ROWS = 64, 30
    Wm, Hm = COLS * T, ROWS * T
    rng = random.Random(8)
    m = np.zeros((Hm, Wm, 3), np.uint8)
    for x in range(0, Wm, sky.shape[1]):
        m[0:160, x:x + sky.shape[1]] = sky[:, :min(sky.shape[1], Wm - x)]

    def paste(a, x0, y0):
        h, w = a.shape[:2]
        for yy, xx in zip(*np.nonzero(a[:, :, 3])):
            X, Y = x0 + xx, y0 + yy
            if not (0 <= X < Wm and 0 <= Y < Hm):
                continue
            if a[yy, xx, 3] == 255:
                m[Y, X] = a[yy, xx, :3]
            else:
                c = tuple(int(v) for v in m[Y, X])
                m[Y, X] = SHADOW.get(c, tuple(int(v * .78) for v in c))

    for x in range(0, Wm, far.shape[1]):
        paste(far, x, 160 - far.shape[0])

    grid = {}
    for r in range(5, ROWS):
        for c in range(COLS):
            v = rng.random()
            grid[r, c] = 'flores_a' if v < .04 else 'flores_b' if v < .06 else \
                'pasto_a' if v < .55 else 'pasto_b' if v < .82 else 'pasto_c'
    for c in range(COLS):
        grid[15, c], grid[16, c] = 'banqueta', 'banqueta_bordillo_s'
        grid[17, c] = 'asfalto_sombra_bordillo'
        for r in (18, 19, 20, 21):
            grid[r, c] = rng.choice(['asfalto_a', 'asfalto_a', 'asfalto_b'])
        if not 26 <= c <= 38:
            grid[19, c] = 'linea_h'
        grid[22, c], grid[23, c] = 'banqueta_bordillo_n', 'banqueta'
    for r in range(5, ROWS):
        grid[r, 28], grid[r, 29] = 'banqueta', 'banqueta_bordillo_e'
        grid[r, 35], grid[r, 36] = 'banqueta_bordillo_w', 'banqueta'
        for c in range(30, 35):
            grid[r, c] = rng.choice(['asfalto_a', 'asfalto_b'])
        if not 13 <= r <= 25:
            grid[r, 32] = 'linea_v'
    grid[16, 28], grid[16, 29] = 'banqueta_bordillo_s', 'banqueta_esquina_se'
    grid[16, 35], grid[16, 36] = 'banqueta_esquina_sw', 'banqueta_bordillo_s'
    grid[22, 28], grid[22, 29] = 'banqueta_bordillo_n', 'banqueta_esquina_ne'
    grid[22, 35], grid[22, 36] = 'banqueta_esquina_nw', 'banqueta_bordillo_n'
    for c in range(30, 35):
        grid[15, c] = grid[16, c] = grid[22, c] = grid[23, c] = 'cebra_v'
        grid[17, c] = 'asfalto_a'
    for r in range(17, 22):
        for c in (28, 29, 35, 36):
            grid[r, c] = 'cebra_h'
    for (r, c, n) in ((18, 10, 'bache_grande'), (21, 47, 'bache_chico'), (9, 33, 'bache_grande'),
                      (20, 22, 'asfalto_grieta'), (18, 55, 'asfalto_grieta'), (27, 31, 'bache_chico')):
        if registro is not None and n.startswith('bache'):
            registro.setdefault('bajo_bache', {})[r, c] = grid[r, c]   # el asfalto que queda si el bache no aparece
        grid[r, c] = n

    doors_up = [128, 352, 576, 800, 1280, 1504, 1728, 1952]
    gates_low = [224, 448, 672, 1376, 1600, 1824]
    for d in (doors_up if beds else []):   # canteros de tierra morada bajo las ventanas
        hx_ = d - 80
        for wxp in (hx_ + 33, hx_ + 127):
            grid[12, wxp // 32] = 'tierra_morada'
    beds = [(26, 2), (26, 16), (27, 40), (26, 58)] if beds else []
    for (r, c) in beds:
        for dr in (0, 1):
            for dc in (0, 1):
                grid[r + dr, c + dc] = 'tierra_surcos'

    for (r, c), n in grid.items():
        m[r * T:(r + 1) * T, c * T:(c + 1) * T] = tiles[n]
    if ground_hook:
        ground_hook(m, grid)

    objs = []   # (base_y, nombre, centro_x, voltear)

    def put(name, cx, base, flip=False):
        objs.append((base, name, cx, flip))

    x = 10
    while x < Wm:
        if not 860 <= x <= 1210:
            put('arbusto_lejano', x, rng.randint(178, 196))
        x += rng.randint(24, 40)
    houses = ['casa_madera', 'casa_crema', 'casa_azul', 'casa_oliva']
    for i, d in enumerate(doors_up):
        put(houses[i % 4], d, 383)
        y = 386
        while y < 470:
            put('piedra_camino', d + rng.randint(-2, 2), y + 8)
            y += 12
        for wxp in ((d - 47, d + 47) if beds else ()):
            put('huerto_plantas', wxp, 412)
        put('buzon_teal' if i % 2 else 'buzon_terracota', d + 46, 478)
    for gx in (240, 464, 688, 1392, 1616, 1840, 20, 2040):
        put(rng.choice(['arbol_grande', 'arbol_grande_b', 'pino']), gx + rng.randint(-6, 6), rng.randint(300, 330))
    for gx in (240, 688, 1616):
        put('arbol_mediano', gx + rng.randint(-8, 8), 446)
    for gx in (464, 1392, 1840):
        put(rng.choice(['arbusto_flores', 'arbusto_grande']), gx, 450)

    def fence_row(base, gates, hedge_cols=()):
        for c in list(range(0, 28)) + list(range(37, 64)):
            x0 = c * 32
            if any(g - 32 <= x0 < g + 32 for g in gates):
                continue
            if c in hedge_cols:
                if c % 2 == 0:
                    put('seto', x0 + 32, base + 2)
                continue
            put('barda_hiedra' if rng.random() < .25 else 'barda', x0 + 16, base)
        for g in gates:
            put('barda_porton', g, base + 2)
        put('barda_poste_final', 27 * 32 + 27, base + 2)
        put('barda_poste_final', 37 * 32 + 4, base + 2)

    fence_row(476, doors_up)
    fence_row(812, gates_low, hedge_cols=range(40, 49))
    for g in gates_low:
        y = 820
        while y < Hm:
            put('piedra_camino', g + rng.randint(-2, 2), y + 8)
            y += 12
    for tx in (90, 340, 560, 800, 1240, 1480, 1700, 1960):
        put(rng.choice(['arbol_grande', 'arbol_grande_b', 'arbol_mediano']), tx + rng.randint(-10, 10), rng.randint(905, 955))
    for bx in (150, 610, 1290, 1880, 40):
        put(rng.choice(['arbusto_flores', 'arbusto_flores_crema', 'arbusto_chico']), bx, rng.randint(870, 890))
    for (r, c) in beds:
        for dr in (0, 1):
            put('huerto_plantas', c * 32 + 32, (r + dr) * 32 + 28)
    for (bx, b) in ((870, 240), (868, 330), (1206, 260), (1204, 350), (868, 900), (1206, 880)):
        put(rng.choice(['arbusto_grande', 'arbusto_chico']), bx, b)

    for x in (240, 464, 688, 912, 1168, 1392, 1616, 1840):
        put('farol', x, 504)
    for x in (128, 352, 576, 800, 1168, 1280, 1504, 1728, 1952):
        put('farol', x, 764)
    put('senal_calle', 1136, 510)
    put('senal_calle', 940, 760)
    put('auto_lateral_teal', 420, 694)
    put('auto_lateral_oliva', 1660, 692)
    put('auto_lateral_terracota', 1520, 604, flip=True)
    put('auto_frontal_azul', 992, 420)

    objs.sort(key=lambda o: o[0])
    for base, name, cx, flip in objs:
        a = props[name][:, ::-1] if flip else props[name]
        h, w = a.shape[:2]
        paste(a, cx - w // 2, base - h + 1)
    if registro is not None:
        registro.update(grid=grid, objs=objs, cols=COLS, rows=ROWS)
    return m


# ================================================================ mapa para Unity
DINAMICOS = ('auto_', 'bache_')   # se mueven o aparecen por codigo: no van pintados en el mapa


def mapa_unity(registro, props):
    """Convierte el trazado de build_reference al formato que lee ConstruirMapa.cs.

    Coordenadas en tiles con origen abajo-izquierda y la y hacia arriba (como Unity).
    Props y autos: (x, y) es el pivote abajo-centro del sprite, igual que en props_atlas.json.
    Baches: (x, y) es la celda del Tilemap.
    """
    cols, rows, grid = registro['cols'], registro['rows'], registro['grid']
    bajo_bache = registro.get('bajo_bache', {})
    suelo, dinamicos = [], []
    for r in range(rows):   # fila 0 = la de arriba, como se ve la imagen
        fila = []
        for c in range(cols):
            n = grid.get((r, c))   # las filas del cielo no tienen tile
            if n and n.startswith(DINAMICOS):
                dinamicos.append(dict(nombre=n, tipo='tile', x=c, y=rows - 1 - r))
                n = bajo_bache[r, c]
            fila.append(n)
        suelo.append(fila)

    alto_px = rows * T
    lista = []
    for base, name, cx, flip in registro['objs']:
        w = props[name].shape[1]
        # mismo encuadre que paste(): el sprite empieza en cx - w//2 y su ultima fila es base
        x = (cx - w // 2 + w / 2) / T
        y = (alto_px - (base + 1)) / T
        if name.startswith(DINAMICOS):
            dinamicos.append(dict(nombre=name, tipo='sprite', x=x, y=y, voltear=flip))
        else:
            lista.append(dict(nombre=name, x=x, y=y))
    return dict(columnas=cols, filas=rows, tile=T,
                coordenadas='tiles; origen abajo-izquierda, y hacia arriba; '
                            'props/autos = pivote abajo-centro, baches = celda',
                suelo_orden='suelo[0] es la fila de arriba; null = cielo (sin tile)',
                suelo=suelo, props=lista, dinamicos=dinamicos)


def guardar_mapa(data, name):
    """JSON legible: una fila del suelo o un objeto por linea."""
    partes = []
    for k, v in data.items():
        if isinstance(v, list):
            cuerpo = '[\n' + ',\n'.join('    ' + json.dumps(o, ensure_ascii=False) for o in v) + '\n  ]'
        else:
            cuerpo = json.dumps(v, ensure_ascii=False)
        partes.append(f'  {json.dumps(k)}: {cuerpo}')
    with open(os.path.join(OUT, name), 'w', encoding='utf-8') as f:
        f.write('{\n' + ',\n'.join(partes) + '\n}\n')


def save(a, name):
    Image.fromarray(a).save(os.path.join(OUT, name))


def save_protegido(a, name):
    """Guarda solo si no existe; si existe, avisa si el contenido generado ya no coincide."""
    path = os.path.join(OUT, name)
    if name in PROTEGIDOS and os.path.exists(path):
        if not np.array_equal(np.asarray(Image.open(path)), a):
            print(f'AVISO: {name} protegido; lo generado ya no coincide y NO se sobrescribio')
        return
    Image.fromarray(a).save(path)


def json_protegido(data, name):
    path = os.path.join(OUT, name)
    if name in PROTEGIDOS and os.path.exists(path):
        return
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    sheet, tmeta, tiles = build_tileset()
    save_protegido(sheet, 'tileset_suelo.png')
    json_protegido(dict(imagen='tileset_suelo.png', tile=T, columnas=8, origen='arriba-izquierda',
                        tiles=tmeta), 'tileset_suelo.json')

    props = build_props()
    atlas, meta = pack_atlas(props)
    save_protegido(atlas, 'props_atlas.png')
    json_protegido(dict(imagen='props_atlas.png', ancho=atlas.shape[1], alto=atlas.shape[0],
                        origen='arriba-izquierda (y_unity = desde abajo)', pivote=[0.5, 0.0],
                        separacion=4, sprites=meta), 'props_atlas.json')

    rng = random.Random(4)
    sky, far = build_sky(rng), build_far_trees(rng)
    save_protegido(sky, 'fondo_cielo.png')
    save_protegido(far, 'fondo_arboles.png')

    registro = {}
    ref = build_reference(tiles, props, sky, far, beds=False, registro=registro)   # sin canteros de tierra morada
    save(ref, 'mapa_referencia.png')
    mapa = mapa_unity(registro, props)
    guardar_mapa(mapa, 'mapa_residencial.json')
    crop = ref[250:490, 640:1024]
    save_protegido(np.asarray(Image.fromarray(crop).resize((crop.shape[1] * 4, crop.shape[0] * 4), Image.NEAREST)),
                   'preview_x4.png')
    print('tiles:', len(tmeta), '| props:', len(meta), '| mapa', ref.shape[1], 'x', ref.shape[0],
          '| colocados:', len(mapa['props']), '| dinamicos:', len(mapa['dinamicos']))
