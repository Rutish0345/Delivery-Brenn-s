"""Procedural 16-bit pixel-art residential town (top-down 3/4, Stardew-like).

Draws at 1 logical pixel = 4 output pixels (nearest neighbour, no AA).
Outputs:
  town_map.png      3072x960  full map, tiles seamlessly left-right
  town_screen.png   1536x960  one screen centred on the crossroad
  ground_tiles.png  tileable 16x16 ground tiles (x4)
"""
import os
import random
import numpy as np
from PIL import Image

S = 4                      # output pixels per logical pixel
MAP_W, MAP_H = 768, 240    # logical map size  -> 3072x960
SCR_W = 384                # logical screen width -> 1536


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


P = {k: hx(v) for k, v in dict(
    sky='#a8bccd', sky_l='#b9cad7',
    cloud='#e3e8e4', cloud_s='#c9d4db', cloud_ss='#b3c3d1',
    far1='#8aa3bd', far2='#7189a8', far3='#647d9b',
    grass='#7d9a6d', grass_d='#6b875f', grass_l='#93ac7e', grass_sh='#5d6a62',
    bush='#5f7d5c', bush_d='#4a654c', bush_dd='#3d5443', bush_l='#769468',
    mid='#6a8a7a', mid_d='#577563', mid_l='#7f9f88',
    leaf='#6b8c5d', leaf_d='#56764f', leaf_dd='#435d46', leaf_l='#86a571', leaf_h='#a7bb80',
    olive='#b2a35b', olive_l='#cabc79', olive_d='#8b7f47',
    wood='#a65a42', wood_d='#854539', wood_dd='#633233', wood_l='#bf7558',
    roof='#4f7c7e', roof_d='#3f6668', roof_dd='#314f56', roof_l='#679698',
    gnd='#4b3c5b', gnd_d='#3b2f4a', gnd_l='#5c4b6d',
    road='#544a66', road_d='#483f59', road_l='#615778', hole='#3b3249', hole_d='#30283d',
    side='#aaa3b5', side_d='#8f889d', side_l='#bfb9c8', curb='#766e87', curb_l='#c9c3d1',
    stripe='#dcd5c3', stripe_d='#bfb7a6', dash='#c8b46c', dash_d='#a8964f',
    fence='#ded6bb', fence_l='#eee8d3', fence_d='#b3a98d',
    stone='#9b91a7', stone_d='#7a7088', stone_l='#b2a9bc',
    glass='#8eafc4', glass_l='#c4d7df', glass_d='#6d8ba6',
    cream='#d8cdb0', cream_d='#b9ad90', cream_l='#e7decb',
    blue='#7f98b5', blue_d='#68809f', blue_l='#97aec6',
    lamp='#56627a', lamp_d='#414b60', lamp_l='#6d7a92',
    pink='#FF6FA5', pink_d='#cc4f86', purple='#8C52FF', purple_d='#6a3ccc',
    gold='#FFD166', gold_d='#d9a24c', gold_l='#ffe9ad',
).items()}

# palette-preserving shadow: each colour maps to a darker palette colour
SHADOW = {P[a]: P[b] for a, b in [
    ('grass', 'grass_sh'), ('grass_l', 'grass_d'), ('grass_d', 'grass_sh'),
    ('side', 'side_d'), ('side_l', 'side'), ('side_d', 'curb'),
    ('road', 'road_d'), ('road_l', 'road'), ('stripe', 'stripe_d'),
    ('gnd', 'gnd_d'), ('gnd_l', 'gnd'), ('stone', 'stone_d'), ('stone_l', 'stone'),
    ('dash', 'dash_d'), ('curb_l', 'side_d')]}
GRASSES = {P['grass'], P['grass_l'], P['grass_d']}


class Canvas:
    def __init__(self, w, h, wrapy=False):
        self.w, self.h, self.wrapy = w, h, wrapy
        self.a = np.zeros((h, w, 3), np.uint8)

    def _xy(self, x, y):
        if self.wrapy:
            y %= self.h
        elif not 0 <= y < self.h:
            return None
        return x % self.w, y

    def px(self, x, y, c):
        p = self._xy(x, y)
        if p:
            self.a[p[1], p[0]] = P[c] if isinstance(c, str) else c

    def get(self, x, y):
        p = self._xy(x, y)
        return tuple(int(v) for v in self.a[p[1], p[0]]) if p else None

    def px_on(self, x, y, c, allowed):
        if self.get(x, y) in allowed:
            self.px(x, y, c)

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px(xx, yy, c)

    def shadow(self, x, y):
        c = self.get(x, y)
        if c in SHADOW:
            self.px(x, y, SHADOW[c])


def disc(cx, cy, r):
    return {(cx + dx, cy + dy) for dx in range(-r, r + 1) for dy in range(-r, r + 1)
            if dx * dx + dy * dy <= r * r + r}


def ellipse(cx, cy, a, b):
    return {(cx + dx, cy + dy) for dx in range(-a, a + 1) for dy in range(-b, b + 1)
            if (dx / (a + .5)) ** 2 + (dy / (b + .5)) ** 2 <= 1}


def shade(cv, pts, dark, base, light, rim=None):
    """Fill a shape lit from top-left: hue-shifted dark rim instead of outlines."""
    for (x, y) in pts:
        if (x, y + 1) not in pts or (x + 1, y) not in pts:
            c = rim or dark
        elif (x + 1, y + 2) not in pts or (x + 2, y + 1) not in pts:
            c = dark
        elif (x - 1, y - 2) not in pts or (x - 2, y - 1) not in pts:
            c = light
        else:
            c = base
        cv.px(x, y, c)


# ---------------------------------------------------------------- textures
def grass_texture(cv, x0, y0, w, h, rng, density=26, flowers=True):
    for _ in range(w * h // density):
        x, y = x0 + rng.randrange(w), y0 + rng.randrange(h)
        if rng.random() < .55:   # light tuft
            for dx, dy in ((0, 0), (-1, 1), (1, 1)):
                cv.px_on(x + dx, y + dy, 'grass_l', GRASSES)
            cv.px_on(x, y + 1, 'grass_d', GRASSES)
        else:                    # dark blade pair
            cv.px_on(x, y, 'grass_d', GRASSES)
            cv.px_on(x + 1, y - 1, 'grass_d', GRASSES)
    for _ in range(w * h // 900):   # soft lighter patches (stepped 2px blocks)
        cx, cy = x0 + rng.randrange(w), y0 + rng.randrange(h)
        for (x, y) in disc(cx // 2, cy // 2, rng.randint(1, 2)):
            for dx in (0, 1):
                for dy in (0, 1):
                    cv.px_on(x * 2 + dx, y * 2 + dy, 'grass_l', {P['grass']})
    if flowers:
        for _ in range(w * h // 700):
            x, y = x0 + rng.randrange(w), y0 + rng.randrange(h)
            c = rng.choice(['cream_l', 'cream_l', 'olive_l', 'olive_l', 'pink'])
            for dx, dy in ((0, 0), (1, 1), (-1, 1)):
                if rng.random() < .8:
                    cv.px_on(x + dx, y + dy, c, GRASSES)
            cv.px_on(x, y + 2, 'leaf_d', GRASSES)


def road_texture(cv, x0, y0, w, h, rng):
    road = {P['road']}
    for _ in range(w * h // 9):
        x, y = x0 + rng.randrange(w), y0 + rng.randrange(h)
        c = rng.choice(['road_d', 'road_d', 'road_l'])
        cv.px_on(x, y, c, road)
        if rng.random() < .3:
            cv.px_on(x + 1, y, c, road)


def crack(cv, x, y, n, rng):
    for _ in range(n):
        cv.px_on(x, y, 'road_d', {P['road'], P['road_l']})
        x += rng.choice([1, 1, 0])
        y += rng.choice([-1, 0, 1])


def pothole(cv, cx, cy, rng):
    pts = ellipse(cx, cy, rng.randint(2, 4), 2) | ellipse(cx + rng.randint(2, 4), cy + 1, 2, 1)
    for (x, y) in pts:
        c = 'hole'
        if (x, y - 1) not in pts:
            c = 'hole_d'
        elif (x, y + 1) not in pts:
            c = 'road_l'
        cv.px(x, y, c)
    for (x, y) in pts:   # lit rim on the far (upper) lip
        if (x, y - 1) not in pts and rng.random() < .7:
            cv.px(x, y - 1, 'road_l')
    if rng.random() < .6:   # rain puddle
        cv.px(cx, cy, 'glass_d')
        cv.px(cx + 1, cy, 'glass_d')
        cv.px(cx + 1, cy, 'glass')
    crack(cv, cx + 4, cy, rng.randint(3, 6), rng)


# ---------------------------------------------------------------- props
def cloud(cv, x, y, w, rng):
    pts = set()
    bx = x
    while bx < x + w:
        r = rng.randint(3, 6)
        pts |= disc(bx, y - r // 2, r)
        bx += rng.randint(4, 7)
    pts = {(px_, py) for (px_, py) in pts if py <= y + 2}
    # snap to 2px blocks for stepped edges
    blocks = {(px_ // 2, py // 2) for (px_, py) in pts}
    pts = {(bx_ * 2 + dx, by * 2 + dy) for (bx_, by) in blocks for dx in (0, 1) for dy in (0, 1)}
    shade(cv, pts, 'cloud_s', 'cloud', 'cloud', rim='cloud_s')
    bottom = max(py for _, py in pts)
    for (px_, py) in pts:
        if py == bottom:
            cv.px(px_, py, 'cloud_ss')


def canopy(cv, cx, cy, r, rng, pal=('leaf_dd', 'leaf_d', 'leaf', 'leaf_l', 'leaf_h')):
    dd, d, b, l, h = pal
    shade(cv, disc(cx, cy, r), dd, d, d)
    blobs = []
    for _ in range(int(r * 1.6)):
        a = rng.uniform(0, 6.283)
        dist = rng.uniform(0, r - 3)
        blobs.append((int(cx + np.cos(a) * dist), int(cy + np.sin(a) * dist * .85), rng.randint(3, max(3, r // 2))))
    blobs.sort(key=lambda t: t[1])
    for (bx, by, br) in blobs:
        shade(cv, disc(bx, by, br), d, b, l, rim=dd)
    for (bx, by, br) in blobs[:max(2, len(blobs) // 3)]:   # sunlit leaf clusters
        x, y = bx - br // 2, by - br // 2
        for dx, dy in ((0, 0), (1, 0), (0, 1)):
            cv.px(x + dx, y + dy, h)


def tree(cv, cx, base, r, rng):
    for (x, y) in ellipse(cx + 2, base, r, max(2, r // 4)):
        cv.shadow(x, y)
    th = r // 2 + 4
    for y in range(base - th, base + 1):
        cv.px(cx - 1, y, 'wood_l')
        cv.px(cx, y, 'wood')
        cv.px(cx + 1, y, 'wood_d')
        cv.px(cx + 2, y, 'wood_dd')
    cv.px(cx - 2, base, 'wood_d')
    cv.px(cx + 3, base, 'wood_dd')
    canopy(cv, cx, base - th - r + 3, r, rng)


def bush(cv, x, base, w, h, rng, pal=('bush_dd', 'bush_d', 'bush', 'bush_l'), flowers=None):
    dd, d, b, l = pal
    n = max(2, w // 4)
    blobs = []
    for i in range(n):
        bx = x + int((i + .5) * w / n) + rng.randint(-1, 1)
        rr = rng.randint(max(2, h // 2 - 1), max(3, h // 2 + 1))
        blobs.append((bx, base - rr + 1, rr))
    for (x_, y_) in ellipse(x + w // 2 + 1, base, w // 2 + 1, 1):
        cv.shadow(x_, y_)
    for (bx, by, rr) in sorted(blobs, key=lambda t: t[1]):
        pts = {p for p in disc(bx, by, rr) if p[1] <= base}
        shade(cv, pts, d, b, l, rim=dd)
    if flowers:
        for _ in range(w // 3):
            fx, fy = x + rng.randrange(w), base - rng.randint(2, h)
            if cv.get(fx, fy) in (P[b], P[l]):
                cv.px(fx, fy, flowers)


def fence(cv, x0, x1, base, gaps=(), rng=None):
    for x in range(x0, x1):
        if any(g0 <= x < g1 for g0, g1 in gaps):
            continue
        k = (x - x0) % 4
        if k == 3:
            cv.px(x, base - 6, 'fence_d')
            cv.px(x, base - 2, 'fence_d')
            cv.shadow(x, base + 1)
            continue
        for y in range(base - 8, base + 1):
            cv.px(x, y, ('fence_l', 'fence', 'fence_d')[k])
        if k == 1:
            cv.px(x, base - 9, 'fence_l')
        cv.shadow(x + 1, base + 1)
    for g0, g1 in gaps:   # gate posts
        for gx in (g0 - 1, g1):
            for y in range(base - 10, base + 1):
                cv.px(gx, y, 'wood_d' if gx == g1 else 'wood')
            cv.px(gx, base - 11, 'wood_l')
    if rng:   # creeping ivy on the pickets
        for _ in range((x1 - x0) // 22):
            vx = rng.randrange(x0, x1)
            if any(g0 - 2 <= vx < g1 + 2 for g0, g1 in gaps):
                continue
            for i in range(rng.randint(4, 9)):
                y = base - rng.randint(0, 7)
                cv.px(vx + i, y, rng.choice(['leaf', 'leaf_d', 'leaf_l']))
                if rng.random() < .5:
                    cv.px(vx + i, y + 1, 'leaf_dd')


def mailbox(cv, x, base, col, cold):
    for y in range(base - 8, base + 1):
        cv.px(x + 2, y, 'wood')
        cv.px(x + 3, y, 'wood_d')
    cv.rect(x, base - 12, 6, 4, col)
    for i in range(6):
        cv.px(x + i, base - 12, 'cream_l' if i < 5 else col)
        cv.px(x + i, base - 9, cold)
    cv.px(x + 5, base - 11, cold)
    cv.px(x + 5, base - 10, cold)
    cv.px(x - 1, base - 13, 'pink')      # little flag
    cv.px(x - 1, base - 12, 'pink_d')
    cv.px(x - 1, base - 11, 'wood_d')
    cv.shadow(x + 4, base + 1)
    cv.shadow(x + 5, base + 1)


def street_lamp(cv, x, base):
    for i in range(6):
        cv.shadow(x + 1 + i, base + 1)
    cv.rect(x - 1, base - 1, 5, 2, 'lamp_d')
    cv.px(x - 1, base - 1, 'lamp')
    for y in range(base - 26, base - 1):
        cv.px(x, y, 'lamp_l')
        cv.px(x + 1, y, 'lamp')
        cv.px(x + 2, y, 'lamp_d')
    # lantern
    cv.rect(x - 2, base - 33, 7, 1, 'lamp_d')
    cv.rect(x - 1, base - 34, 5, 1, 'lamp')
    cv.px(x + 1, base - 35, 'lamp_l')
    for y in range(base - 32, base - 27):
        cv.px(x - 2, y, 'lamp')
        cv.px(x + 4, y, 'lamp_d')
        for xx in range(x - 1, x + 4):
            cv.px(xx, y, 'gold')
    cv.px(x, base - 31, 'gold_l')
    cv.px(x + 1, base - 31, 'gold_l')
    cv.px(x, base - 30, 'gold_l')
    for xx in range(x - 1, x + 4):
        cv.px(xx, base - 28, 'gold_d')
    cv.rect(x - 2, base - 27, 7, 1, 'lamp_d')


def street_sign(cv, x, base):
    for y in range(base - 24, base + 1):
        cv.px(x, y, 'lamp_l')
        cv.px(x + 1, y, 'lamp_d')
    cv.shadow(x + 2, base + 1)
    cv.shadow(x + 3, base + 1)
    # street-name blade (purple, gold lettering)
    cv.rect(x - 7, base - 26, 16, 4, 'purple')
    cv.rect(x - 7, base - 23, 16, 1, 'purple_d')
    for i, on in enumerate('1101011101101101'):
        if on == '1' and 0 < i < 15:
            cv.px(x - 7 + i, base - 25, 'gold')
    # cross blade (pink)
    cv.rect(x - 2, base - 21, 6, 3, 'pink')
    cv.rect(x - 2, base - 19, 6, 1, 'pink_d')
    cv.px(x, base - 20, 'gold_l')
    cv.px(x + 2, base - 20, 'gold_l')


def ivy(cv, x, y, n, rng, xmin, xmax):
    for _ in range(n):
        cv.px(x, y, 'leaf_dd')
        if rng.random() < .65:
            for dx, dy, c in ((-1, 0, 'leaf'), (1, 0, 'leaf_d'), (0, -1, 'leaf_l'), (1, -1, 'leaf')):
                if rng.random() < .65 and xmin <= x + dx <= xmax:
                    cv.px(x + dx, y + dy, c)
        y -= 1
        if rng.random() < .3:
            x = min(xmax, max(xmin, x + rng.choice([-1, 1])))


def window(cv, x, y, w, h, trim, trim_d, box, rng):
    cv.rect(x, y, w, h, trim)
    for i in range(h):
        cv.px(x + w - 1, y + i, trim_d)
    for i in range(w):
        cv.px(x + i, y + h - 1, trim_d)
    gx0, gy0, gw, gh = x + 1, y + 1, w - 2, h - 2
    mx = gx0 + gw // 2
    for yy in range(gy0, gy0 + gh):
        for xx in range(gx0, gx0 + gw):
            c = 'glass'
            lx = xx - (gx0 if xx < mx else mx + 1)
            if lx + (yy - gy0) in (1, 2):
                c = 'glass_l'
            elif yy == gy0 + gh - 1 or xx in (mx - 1, gx0 + gw - 1):
                c = 'glass_d'
            cv.px(xx, yy, c)
    for yy in range(gy0, gy0 + gh):
        cv.px(mx, yy, trim)
    cv.rect(x - 1, y + h, w + 2, 1, 'cream_l')
    cv.rect(x - 1, y + h + 1, w + 2, 1, 'cream_d')
    if box:   # flower box
        cv.rect(x, y + h + 2, w, 3, 'wood')
        cv.rect(x, y + h + 4, w, 1, 'wood_d')
        for i in range(w):
            cv.px(x + i, y + h + 1, rng.choice(['leaf', 'leaf_d', 'leaf_l']))
            if rng.random() < .5:
                cv.px(x + i, y + h, rng.choice(['leaf', 'leaf_l']))
            if i % 3 == 1:
                cv.px(x + i, y + h - 1 + rng.randint(0, 1), rng.choice([box, 'cream_l', 'olive_l']))


WALLS = {'wood': ('wood', 'wood_d', 'wood_l'), 'olive': ('olive', 'olive_d', 'olive_l'),
         'blue': ('blue', 'blue_d', 'blue_l'), 'cream': ('cream', 'cream_d', 'cream_l')}


def house(cv, hx_, base, w, kind, wall_h, roof_h, door, gable, rng):
    c, cd, cl = WALLS[kind]
    wt = base - wall_h
    # cast shadow down-right
    for yy in range(base, base + 3):
        for xx in range(hx_ + 2 + (yy - base), hx_ + w + 3):
            cv.shadow(xx, yy)
    for yy in range(wt, base - 3):
        cv.shadow(hx_ + w, yy)
        cv.shadow(hx_ + w + 1, yy)
    # wall
    for yy in range(wt, base):
        for xx in range(hx_, hx_ + w):
            col = c
            if kind != 'cream' and (yy - wt) % 4 == 3:
                col = cd
            elif kind == 'cream' and rng.random() < .05:
                col = cd
            cv.px(xx, yy, col)
    if kind == 'wood':
        for yy in range(wt, base, 4):
            for _ in range(2):
                sx = rng.randrange(hx_ + 3, hx_ + w - 3)
                for k in range(3):
                    cv.px(sx, yy + k, cd)
    trims = ('wood', 'wood_d') if kind == 'cream' else ('cream_l', 'cream_d')
    for yy in range(wt, base):
        cv.px(hx_, yy, trims[0])
        cv.px(hx_ + 1, yy, trims[0])
        cv.px(hx_ + w - 2, yy, trims[1])
        cv.px(hx_ + w - 1, yy, trims[1])
    for xx in range(hx_, hx_ + w):   # eave shadow + foundation
        cv.px(xx, wt + 3, cd)
        cv.px(xx, wt + 4, cd)
        for k, yy in enumerate(range(base - 3, base)):
            col = 'stone_l' if k == 0 else 'stone'
            if k > 0 and (xx + (yy % 2) * 3) % 6 == 0:
                col = 'stone_d'
            cv.px(xx, yy, col)
        cv.px(xx, base - 1, 'stone_d') if xx > hx_ + w - 3 else None
    # windows
    box = rng.choice([None, 'pink', 'cream_l', 'olive_l'])
    wy = wt + 8
    window(cv, hx_ + 5, wy, 11, 10, *trims if kind != 'cream' else ('cream_l', 'cream_d'), box=box, rng=rng)
    window(cv, hx_ + w - 16, wy, 11, 10, *trims if kind != 'cream' else ('cream_l', 'cream_d'), box=box, rng=rng)
    # door
    dc, dcd = door
    dw, dh = 8, 15
    dx = hx_ + w // 2 - dw // 2
    dy = base - 3 - dh
    cv.rect(dx - 1, dy - 1, dw + 2, dh + 1, trims[0])
    for yy in range(dy - 1, base - 3):
        cv.px(dx + dw, yy, trims[1])
    cv.rect(dx, dy, dw, dh, dc)
    for yy in range(dy, dy + dh):
        cv.px(dx + dw - 1, yy, dcd)
    cv.rect(dx + 2, dy + 2, 4, 3, 'glass')
    cv.px(dx + 2, dy + 2, 'glass_l')
    cv.px(dx + 5, dy + 4, 'glass_d')
    cv.rect(dx + 1, dy + 7, 5, 1, dcd)
    cv.rect(dx + 1, dy + 11, 5, 1, dcd)
    cv.px(dx + dw - 2, dy + 9, 'gold')
    cv.rect(dx - 2, base - 3, dw + 4, 3, 'stone')
    cv.rect(dx - 2, base - 3, dw + 4, 1, 'stone_l')
    cv.rect(dx - 2, base - 1, dw + 4, 1, 'stone_d')
    # porch lantern
    cv.px(dx + dw + 3, dy + 2, 'lamp_d')
    cv.rect(dx + dw + 2, dy + 3, 3, 3, 'gold')
    cv.px(dx + dw + 2, dy + 3, 'gold_l')
    cv.px(dx + dw + 4, dy + 5, 'gold_d')
    cv.rect(dx + dw + 2, dy + 6, 3, 1, 'lamp_d')
    # ivy climbing a corner
    if rng.random() < .7:
        side = rng.random() < .5
        ix = hx_ + 2 if side else hx_ + w - 3
        ivy(cv, ix, base - 3, rng.randint(14, wall_h + 6), rng, hx_ - 1, hx_ + w)
    # roof (slate-teal, hip shape, shingle courses)
    top = wt + 3 - roof_h
    rx0, rx1 = hx_ - 4, hx_ + w + 4
    inset_top = min(14, w // 3)
    for r in range(roof_h):
        y = top + r
        ins = max(0, inset_top - r)
        x0, x1 = rx0 + ins, rx1 - ins
        rb = roof_h - 1 - r
        course, k = rb // 4, rb % 4
        for xx in range(x0, x1):
            m = (xx + course * 3) % 7
            if r == 0:
                col = 'roof_l'
            elif rb == 0:
                col = 'roof_dd'
            elif rb == 1 or k == 0:
                col = 'roof_d'
            elif m == 0 and k in (1, 2):
                col = 'roof_d'
            elif k == 3 and m in (2, 3):
                col = 'roof_l'
            else:
                col = 'roof'
            if xx == x0 and 0 < r and rb > 1:
                col = 'roof_l'
            if xx == x1 - 1 and r > 0:
                col = 'roof_dd'
            cv.px(xx, y, col)
    # moss on the roof
    for _ in range(rng.randint(3, 7)):
        mx_ = rng.randrange(rx0 + 2, rx1 - 2)
        my = top + roof_h - 3 - rng.randint(0, 6)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (-1, 1)):
            if rng.random() < .7:
                cv.px(mx_ + dx, my + dy, rng.choice(['bush', 'bush_l', 'bush_d']))
    # front gable over the door
    if gable:
        gh, gw = 13, 13
        gx = hx_ + w // 2
        ya = wt + 3 - gh
        for j in range(gh + 1):
            y = ya + j
            half = 1 + j * gw // gh
            for xx in range(gx - half, gx + half):
                col = c if (y - wt) % 4 != 3 or kind == 'cream' else cd
                cv.px(xx, y, col)
            cv.px(gx - half - 1, y, 'roof_l')
            cv.px(gx - half - 2, y, 'roof')
            cv.px(gx + half, y, 'roof_d')
            cv.px(gx + half + 1, y, 'roof_dd')
        cv.px(gx - 1, ya - 1, 'roof_l')
        cv.px(gx, ya - 1, 'roof_d')
        # round attic window
        ay = ya + 6
        for (x, y) in disc(gx, ay, 2):
            cv.px(x - 1 if x < gx else x, y, trims[0])
        cv.rect(gx - 2, ay - 1, 3, 3, 'glass')
        cv.px(gx - 2, ay - 1, 'glass_l')
        cv.px(gx, ay + 1, 'glass_d')
    # chimney
    cx = hx_ + (w * 3 // 4 if rng.random() < .5 else w // 4) - 3
    for yy in range(top - 6, top + 7):
        ry = yy - (top - 6)
        for xx in range(cx, cx + 6):
            col = 'wood'
            if ry % 3 == 2:
                col = 'wood_d'
            elif (xx - cx + (ry // 3) % 2 * 2) % 4 == 3:
                col = 'wood_d'
            if xx == cx and ry % 3 != 2:
                col = 'wood_l'
            if xx == cx + 5:
                col = 'wood_dd'
            cv.px(xx, yy, col)
    cv.rect(cx - 1, top - 8, 8, 2, 'stone')
    cv.rect(cx - 1, top - 8, 7, 1, 'stone_l')
    cv.px(cx + 6, top - 7, 'stone_d')
    return dx + dw // 2   # door centre x


def garden_bed(cv, x, y, w, h, rng, flower):
    cv.rect(x, y, w, h, 'gnd')
    cv.rect(x, y, w, 1, 'gnd_d')
    for i in range(w):
        if rng.random() < .25:
            cv.px(x + i, y + h - 1, 'gnd_l')
    for i in range(1, w - 1, 3):   # sprouts / little plants
        py = y + rng.randint(0, max(0, h - 2))
        cv.px(x + i, py, 'leaf')
        cv.px(x + i + 1, py, 'leaf_d')
        cv.px(x + i, py - 1, 'leaf_l')
        if rng.random() < .45:
            cv.px(x + i, py - 2, rng.choice([flower, 'olive_l', 'cream_l']))


# ---------------------------------------------------------------- scene
def build_map(seed=11):
    rng = random.Random(seed)
    W, H = MAP_W, MAP_H
    cv = Canvas(W, H)

    # sky + clouds
    cv.rect(0, 0, W, 48, 'sky')
    for y in range(0, 10):
        for x in range(W):
            if (x + y) % 2 == 0 and y < 4 or y < 2:
                cv.px(x, y, 'sky_l')
    x = 0
    while x < W:
        cloud(cv, x + rng.randint(0, 20), rng.randint(8, 18), rng.randint(20, 46), rng)
        x += rng.randint(70, 120)

    # distant trees: flat blue silhouettes, two layers
    for color, ybase, rmin, rmax, step in (('far1', 26, 5, 9, 8), ('far2', 36, 4, 8, 7)):
        x = 0
        while x < W:
            cy = ybase - rng.randint(0, 6)
            if rng.random() < .25:   # conifer
                hgt = rng.randint(14, 20)
                for k in range(hgt):
                    half = k // 2 // 1 if k % 4 else k // 2 - 1
                    for xx in range(x - max(0, half // 1), x + max(1, half) + 1):
                        cv.px(xx, cy - hgt // 2 + k, color)
            else:
                for (px_, py) in disc(x, cy, rng.randint(rmin, rmax)):
                    cv.px(px_, py, color)
            cv.rect(x - 8, cy, 18, 48 - cy, color)
            x += step + rng.randint(0, 5)

    # ground
    cv.rect(0, 44, W, H - 44, 'grass')
    grass_texture(cv, 0, 44, W, H - 44, rng)

    # --- street network
    VR0, VR1 = 346, 378        # vertical road x range
    VS_L, VS_R = 332, 378      # vertical sidewalks: [332,346) and [378,392)
    for y in range(44, H):
        for x in list(range(VS_L, VR0)) + list(range(VR1, VR1 + 14)):
            cv.px(x, y, 'side')
    for y0 in (140, 189):
        cv.rect(0, y0, W, 13, 'side')
        cv.rect(0, y0, W, 1, 'side_l')
    for x in range(W):   # slab joints
        if x % 16 == 0:
            for y in list(range(141, 153)) + list(range(190, 202)):
                cv.px(x, y, 'side_d')
    for y in range(44, H):
        if y % 16 == 0 and not (140 <= y < 202):
            for x in list(range(VS_L, VR0)) + list(range(VR1, VR1 + 14)):
                cv.px(x, y, 'side_d')
        for x, c in ((VR0 - 2, 'curb_l'), (VR0 - 1, 'curb'), (VR1, 'curb'), (VR1 + 1, 'curb_l')):
            cv.px(x, y, c)
    cv.rect(0, 153, W, 1, 'curb_l')
    cv.rect(0, 154, W, 2, 'curb')
    cv.rect(0, 188, W, 1, 'curb_l')
    cv.rect(0, 156, W, 32, 'road')
    cv.rect(VR0, 44, VR1 - VR0, H - 44, 'road')
    cv.rect(0, 156, W, 1, 'road_d')
    for y in range(44, H):
        cv.px(VR0, y, 'road_d')
    road_texture(cv, 0, 156, W, 32, rng)
    road_texture(cv, VR0, 44, VR1 - VR0, H - 44, rng)
    # weeds/moss in sidewalk joints and edges
    sides = {P['side'], P['side_d'], P['side_l']}
    for _ in range(260):
        x, y = rng.randrange(W), rng.choice([140, 141, 152, 189, 190, 201, rng.randrange(141, 152)])
        cv.px_on(x, y, rng.choice(['grass_d', 'leaf', 'grass_l']), sides)
    # dashed centre lines
    for x in range(W):
        if (x % 16) < 8 and not (VS_L - 4 <= x < VR1 + 18):
            cv.px(x, 171, 'dash')
            cv.px(x, 172, 'dash_d')
    for y in range(44, H):
        if (y % 16) < 8 and not (134 <= y < 208):
            cv.px(361, y, 'dash')
            cv.px(362, y, 'dash_d')
    # crosswalks
    for xs in ((VS_L + 1, VR0 - 2), (VR1 + 2, VR1 + 13)):
        for y in range(158, 187, 4):
            for x in range(*xs):
                cv.px(x, y, 'stripe')
                cv.px(x, y + 1, 'stripe_d')
    for ys in ((140, 153), (189, 202)):
        for x in range(VR0 + 2, VR1 - 1, 4):
            for y in range(*ys):
                cv.px(x, y, 'stripe')
                cv.px(x + 1, y, 'stripe_d')
    for _ in range(90):   # worn paint
        x, y = rng.randrange(VS_L, VR1 + 14), rng.randrange(140, 202)
        cv.px_on(x, y, 'road', {P['stripe'], P['stripe_d']})
    for (x, y) in ((250, 165), (470, 179), (606, 162), (90, 181), (366, 108), (720, 176), (355, 226)):
        pothole(cv, x, y, rng)
    for _ in range(14):
        crack(cv, rng.randrange(W), rng.randrange(158, 186), rng.randint(4, 9), rng)

    objs = []   # (base_y, draw)

    # midground hedge band
    x = 0
    while x < W:
        if not (VS_L - 14 <= x < VR1 + 14):
            w = rng.randint(10, 18)
            b = rng.randint(54, 60)
            objs.append((b, lambda x=x, w=w, b=b: bush(cv, x, b, w, rng.randint(7, 10), rng,
                                                          pal=('mid_d', 'mid_d', 'mid', 'mid_l'))))
        x += rng.randint(6, 10)

    # upper row of houses
    kinds = ['wood', 'cream', 'blue', 'olive']
    doors = [('pink', 'pink_d'), ('purple', 'purple_d'), ('wood_d', 'wood_dd'), ('roof_d', 'roof_dd')]
    LOT = 88
    lots = [VR1 + 14 + 2 + i * LOT for i in range(8)]
    for i, lx in enumerate(lots):
        w = rng.choice([56, 60, 64, 68])
        hx_ = lx + (LOT - w) // 2 + rng.randint(-4, 4)
        kind = kinds[i % 4]
        door = doors[(i * 3) % 4]
        gable = rng.random() < .5
        wall_h, roof_h = rng.randint(34, 38), rng.randint(26, 30)
        base = 122
        dcx = hx_ + w // 2

        def draw_house(hx_=hx_, w=w, kind=kind, door=door, gable=gable, wall_h=wall_h, roof_h=roof_h, base=base):
            house(cv, hx_, base, w, kind, wall_h, roof_h, door, gable, rng)
            fl = rng.choice(['pink', 'olive_l', 'cream_l'])
            garden_bed(cv, hx_ + 2, base + 1, w // 2 - 9, 4, rng, fl)
            garden_bed(cv, hx_ + w // 2 + 7, base + 1, w // 2 - 9, 4, rng, fl)
            y = base + 1
            while y < 139:
                sx = hx_ + w // 2 - 3 + rng.randint(-1, 1)
                cv.rect(sx, y, 6, 3, 'stone')
                cv.rect(sx, y, 5, 1, 'stone_l')
                cv.rect(sx + 1, y + 2, 5, 1, 'stone_d')
                y += 4
        objs.append((base, draw_house))
        # back-yard tree peeking over/around the house
        tx = lx + rng.choice([4, LOT - 4])
        objs.append((rng.randint(96, 110), lambda tx=tx: tree(cv, tx, rng.randint(96, 110), rng.randint(10, 13), rng)))
        # front-yard tree or bush
        if rng.random() < .55:
            fx = lx + rng.choice([8, LOT - 10])
            objs.append((133, lambda fx=fx: tree(cv, fx, 133, rng.randint(7, 9), rng)))
        else:
            fx = lx + rng.choice([3, LOT - 18])
            objs.append((132, lambda fx=fx: bush(cv, fx, 132, 14, 8, rng, flowers=rng.choice(['pink', 'cream_l', 'olive_l']))))
        # fence with gate + mailbox
        objs.append((136, lambda lx=lx, dcx=dcx: fence(cv, lx, lx + LOT, 136, gaps=[(dcx - 4, dcx + 4)], rng=rng)))
        mb = rng.choice([('roof', 'roof_d'), ('wood', 'wood_d'), ('blue', 'blue_d'), ('olive', 'olive_d')])
        objs.append((140, lambda dcx=dcx, mb=mb: mailbox(cv, dcx + 7, 140, *mb)))

    # lower yards
    for i, lx in enumerate(lots):
        pcx = lx + LOT // 2 + rng.randint(-10, 10)

        def draw_path(pcx=pcx):
            y = 203
            while y < H:
                sx = pcx - 3 + rng.randint(-1, 1)
                cv.rect(sx, y, 6, 3, 'stone')
                cv.rect(sx, y, 5, 1, 'stone_l')
                cv.rect(sx + 1, y + 2, 5, 1, 'stone_d')
                y += 4
        objs.append((202, draw_path))
        bx = lx + (6 if pcx > lx + LOT // 2 else LOT - 26)
        fl = rng.choice(['pink', 'olive_l', 'cream_l'])
        objs.append((214, lambda bx=bx, fl=fl: garden_bed(cv, bx, 218, 20, 9, rng, fl)))
        if i % 3 == 1:   # hedge instead of fence
            for hx0 in range(lx, lx + LOT, 12):
                if not (pcx - 10 <= hx0 <= pcx + 4):
                    objs.append((211, lambda hx0=hx0: bush(cv, hx0, 211, 12, 9, rng)))
        else:
            objs.append((211, lambda lx=lx, pcx=pcx: fence(cv, lx, lx + LOT, 211, gaps=[(pcx - 4, pcx + 4)], rng=rng)))
        tx = lx + (LOT - 16 if pcx < lx + LOT // 2 else 16) + rng.randint(-4, 4)
        tb = rng.randint(234, 242)
        objs.append((tb, lambda tx=tx, tb=tb: tree(cv, tx, tb, rng.randint(12, 15), rng)))
        if rng.random() < .6:
            sx = lx + rng.randint(30, 50)
            if abs(sx + 6 - pcx) > 12:
                objs.append((232, lambda sx=sx: bush(cv, sx, 232, 14, 8, rng, flowers=rng.choice(['pink', 'cream_l']))))

    # bushes hugging the vertical road
    for (bx, b) in ((VS_L - 16, 70), (VS_L - 14, 100), (VR1 + 16, 76), (VR1 + 15, 112), (VS_L - 16, 226), (VR1 + 15, 230)):
        objs.append((b, lambda bx=bx, b=b: bush(cv, bx, b, 14, 9, rng)))

    # lamps and signs
    for x in (VS_L + 4, VR1 + 8, 470, 602, 734, 98, 230):
        objs.append((147, lambda x=x: street_lamp(cv, x, 147)))
    for x in (VS_L + 6, 420, 552, 684, 48, 180, VR1 + 9):
        objs.append((199, lambda x=x: street_lamp(cv, x, 199)))
    objs.append((150, lambda: street_sign(cv, VR1 + 3, 150)))
    objs.append((198, lambda: street_sign(cv, VS_L + 11, 198)))

    for _, draw in sorted(objs, key=lambda t: t[0]):
        draw()
    return cv


def build_tiles(seed=5):
    """Seamless 16x16 ground tiles: grass, grass+flowers, soil, road, road dash, sidewalk, crosswalk x2."""
    rng = random.Random(seed)
    T = 16
    names = ['grass', 'grass_flowers', 'soil', 'road', 'road_dash', 'sidewalk', 'crosswalk_h', 'crosswalk_v']
    sheet = np.zeros((T, T * len(names), 3), np.uint8)
    for i, n in enumerate(names):
        cv = Canvas(T, T, wrapy=True)
        if n.startswith('grass'):
            cv.rect(0, 0, T, T, 'grass')
            grass_texture(cv, 0, 0, T, T, rng, density=18, flowers=False)
            if n == 'grass_flowers':
                for (x, y, c) in ((3, 4, 'cream_l'), (11, 9, 'olive_l'), (6, 12, 'pink')):
                    for dx, dy in ((0, 0), (1, 1), (-1, 1)):
                        cv.px(x + dx, y + dy, c)
                    cv.px(x, y + 2, 'leaf_d')
        elif n == 'soil':
            cv.rect(0, 0, T, T, 'gnd')
            for _ in range(40):
                cv.px(rng.randrange(T), rng.randrange(T), rng.choice(['gnd_d', 'gnd_l', 'gnd_d']))
        elif n.startswith('road') or n.startswith('crosswalk'):
            cv.rect(0, 0, T, T, 'road')
            road_texture(cv, 0, 0, T, T, rng)
            if n == 'road_dash':
                cv.rect(0, 7, 8, 1, 'dash')
                cv.rect(0, 8, 8, 1, 'dash_d')
            if n == 'crosswalk_h':
                for y in (2, 6, 10, 14):
                    cv.rect(1, y, 14, 1, 'stripe')
                    cv.rect(1, y + 1, 14, 1, 'stripe_d')
            if n == 'crosswalk_v':
                for x in (2, 6, 10, 14):
                    cv.rect(x, 1, 1, 14, 'stripe')
                    cv.rect(x + 1, 1, 1, 14, 'stripe_d')
        elif n == 'sidewalk':
            cv.rect(0, 0, T, T, 'side')
            cv.rect(0, 0, T, 1, 'side_d')
            cv.rect(0, 0, 1, T, 'side_d')
            cv.rect(1, 1, T - 1, 1, 'side_l')
            cv.px(9, 6, 'side_d')
            cv.px(10, 7, 'side_d')
            cv.px(0, 11, 'grass_d')
        sheet[:, i * T:(i + 1) * T] = cv.a
    return sheet, names


def up(a):
    h, w = a.shape[:2]
    return Image.fromarray(a).resize((w * S, h * S), Image.NEAREST)


if __name__ == '__main__':
    out = os.path.dirname(os.path.abspath(__file__))
    cv = build_map()
    up(cv.a).save(os.path.join(out, 'town_map.png'))
    cx = 362                                   # centre the screen on the crossroad
    cols = [(cx - SCR_W // 2 + i) % MAP_W for i in range(SCR_W)]
    up(cv.a[:, cols]).save(os.path.join(out, 'town_screen.png'))
    sheet, names = build_tiles()
    up(sheet).save(os.path.join(out, 'ground_tiles.png'))
    # tiling check: 2x copies side by side
    up(np.concatenate([cv.a, cv.a], axis=1)).resize((MAP_W * 2, MAP_H), Image.NEAREST) \
        .save(os.path.join(out, '_tiling_preview.png'))
    print('ok', names)
