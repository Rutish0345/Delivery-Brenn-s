"""Zona comercial pixel-art en capas para Unity (resolucion nativa, tiles 32x32).

Mismas reglas que generate_unity.py (personaje 56x80, puerta 30x58, luz arriba-izquierda).
Letreros solo con iconos (sin texto). Salidas en unity/comercial/:
  tileset_suelo.png + .json     tiles de calle/banqueta + cajon de estacionamiento, alcorque, coladera, adoquin
  props_atlas.png + .json       tiendas, bancas, botes, autos, camioneta... (4 px separacion, pivote abajo)
  fondo_cielo.png, fondo_arboles.png
  mapa_referencia.png           3072x960, escena armada (solo preview)
"""
import json
import os
import random
import numpy as np
from PIL import Image

from generate_town import P, SHADOW, disc, ellipse, shade
from generate_unity import (T, Sprite, trim, shadow_ellipse, build_tileset, tcanvas, t_road, t_sidewalk, t_grass,
                            make_tree, make_bush, make_lamp, make_sign, make_car_side, make_car_front,
                            window2, pack_atlas, build_sky, build_far_trees, WALLS)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'comercial')


# ---------------------------------------------------------------- iconos (sin texto)
def draw_icon(sp, rows, cx, cy, legend):
    h, w = len(rows), len(rows[0])
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch in legend:
                sp.px(int(cx) - w // 2 + i, int(cy) - h // 2 + j, legend[ch])


ICONS = {
    'tijeras': ([".PP.....PP.",
                 "P..P...P..P",
                 "P..P...P..P",
                 ".PPs...sPP.",
                 "....s.s....",
                 ".....s.....",
                 "....s.S....",
                 "...s...S...",
                 "..s.....S..",
                 ".s.......S."], {'P': 'pink', 's': 'stone_l', 'S': 'stone'}),
    'peine': (["ccccccccccc",
               "CCCCCCCCCCC",
               "c.c.c.c.c.c",
               "c.c.c.c.c.c",
               "c.c.c.c.c.c",
               "C.C.C.C.C.C"], {'c': 'cream_l', 'C': 'cream_d'}),
    'pan': (["...ooooooo...",
             ".ooOooOooOoo.",
             "oOooOooOooOoo",
             "ooooooooooooo",
             "odddddddddddo",
             ".ddddddddddd."], {'o': 'olive_l', 'O': 'cream_l', 'd': 'olive_d'}),
    'cruz': (["....ggG....",
              "....ggG....",
              "....ggG....",
              "....ggG....",
              "ggggggggggG",
              "ggggggggggG",
              "GGGGggGGGGG",
              "....ggG....",
              "....ggG....",
              "....ggG....",
              "....GGG...."], {'g': 'leaf_l', 'G': 'leaf_d'}),
    'flor': (["...p.p...",
              "..pPpPp..",
              ".ppPgPpp.",
              "..pPpPp..",
              "...p.p...",
              "....l....",
              "..ll.l...",
              "...lll...",
              "....l...."], {'p': 'pink', 'P': 'pink_d', 'g': 'gold', 'l': 'leaf_l'}),
}

# ---------------------------------------------------------------- tiendas
SHOPS = {
    'panaderia': dict(wall='ladrillo', awning=('olive', 'olive_d', 'olive_l'), door=('wood_d', 'wood_dd'),
                      board=('wood_d', 'wood_dd', 'wood'), icons=['pan'], roof_vent=True),
    'floreria': dict(wall='oliva', awning=('leaf', 'leaf_d', 'leaf_l'), door=('pink', 'pink_d'),
                     board=('leaf_d', 'leaf_dd', 'leaf'), icons=['flor'], roof_vent=False),
    'farmacia': dict(wall='azul', awning=('roof', 'roof_d', 'roof_l'), door=('roof_d', 'roof_dd'),
                     board=('cream_l', 'cream_d', 'cream'), icons=['cruz'], roof_vent=True),
    'salon_belleza': dict(wall='crema', awning=('blue', 'blue_d', 'blue_l'), door=('purple', 'purple_d'),
                          board=('blue_d', 'roof_dd', 'blue'), icons=['tijeras', 'peine'], roof_vent=False),
}


def wall_fill(sp, kind, x0, x1, y0, y1, rng):
    if kind == 'ladrillo':
        for y in range(y0, y1):
            for x in range(x0, x1):
                row = (y - y0) // 4
                if (y - y0) % 4 == 3 or (x + row % 2 * 5) % 10 == 9:
                    col = 'wood_dd'
                else:
                    brick = hash(((x + row % 2 * 5) // 10, row)) % 7
                    col = 'wood_l' if brick == 0 else 'wood_d' if brick == 1 else 'wood'
                    if (y - y0) % 4 == 0 and brick > 1 and (x % 10) < 3:
                        col = 'wood_l'
                sp.px(x, y, col)
        return ('cream_l', 'cream_d'), 'wood_d'
    c, cd, cl = WALLS[kind]
    for y in range(y0, y1):
        k = (y - y0) % 7
        for x in range(x0, x1):
            if kind == 'crema':
                rv = rng.random()
                col = cd if rv < .03 else cl if rv < .05 else c
            else:
                col = cd if k == 6 else cl if k == 0 and (x // 23 + (y - y0) // 7) % 3 == 0 else c
            sp.px(x, y, col)
    return (('wood', 'wood_d') if kind == 'crema' else ('cream_l', 'cream_d')), cd


def goods(sp, kind, x0, y0, w, h, rng):
    """Interior del aparador."""
    sp.rect(x0, y0, w, h, 'glass_d')
    sp.rect(x0, y0, w, 3, 'roof_dd')               # sombra del interior bajo el toldo
    shelves = (y0 + 17, y0 + 33)
    if kind != 'salon_belleza':
        for sy in shelves:
            sp.rect(x0, sy, w, 2, 'wood')
            sp.rect(x0, sy, w, 1, 'wood_l')
            sp.rect(x0, sy + 2, w, 1, 'wood_dd')
    if kind == 'panaderia':
        for sy in shelves:
            x = x0 + 3
            while x < x0 + w - 12:
                pts = ellipse(x + 5, sy - 3, 5, 2)
                shade(sp, pts, 'olive_d', 'olive', 'olive_l')
                sp.px(x + 3, sy - 4, 'wood_l')
                sp.px(x + 6, sy - 4, 'wood_l')
                x += 13
        cx = x0 + w - 10                              # pastel
        sp.rect(cx - 6, shelves[1] - 9, 12, 9, 'cream_l')
        sp.rect(cx - 6, shelves[1] - 9, 12, 2, 'pink')
        sp.rect(cx + 4, shelves[1] - 9, 2, 9, 'cream_d')
        sp.px(cx, shelves[1] - 11, 'gold')
    elif kind == 'farmacia':
        cols = ['blue_l', 'cream_l', 'leaf_l', 'roof_l', 'olive_l']
        for sy in shelves:
            for x in range(x0 + 3, x0 + w - 4, 5):
                c = rng.choice(cols)
                hh = rng.randint(5, 8)
                sp.rect(x, sy - hh, 3, hh, c)
                sp.rect(x + 2, sy - hh, 1, hh, 'stone_d')
                sp.rect(x, sy - hh - 1, 3, 1, 'cream_l')
        cx, cy = x0 + w // 2, y0 + 8                  # cruz verde pegada al vidrio
        sp.rect(cx - 2, cy - 5, 5, 11, 'leaf_l')
        sp.rect(cx - 5, cy - 2, 11, 5, 'leaf_l')
        sp.rect(cx + 3, cy - 2, 3, 5, 'leaf')
        sp.rect(cx - 2, cy + 3, 5, 3, 'leaf')
    elif kind == 'floreria':
        for sy in shelves:
            x = x0 + 4
            while x < x0 + w - 10:
                sp.rect(x + 1, sy - 6, 7, 6, rng.choice(['stone', 'roof', 'wood']))
                sp.rect(x + 6, sy - 6, 2, 6, 'stone_d')
                for (px_, py) in disc(x + 4, sy - 10, 4):
                    sp.px(px_, py, 'leaf' if (px_ + py) % 3 else 'leaf_d')
                for _ in range(5):
                    sp.px(x + rng.randint(1, 7), sy - rng.randint(8, 13),
                          rng.choice(['pink', 'cream_l', 'olive_l', 'cream_l', 'purple']))
                x += 12
        for x in range(x0 + 4, x0 + w - 4, 9):       # plantas colgantes
            for k in range(rng.randint(3, 7)):
                sp.px(x + (k % 2), y0 + 3 + k, rng.choice(['leaf', 'leaf_l', 'leaf_d']))
    elif kind == 'salon_belleza':
        for mx in (x0 + 18, x0 + w - 22):
            for (px_, py) in ellipse(mx, y0 + 18, 9, 12):
                sp.px(px_, py, 'wood_l')
            for (px_, py) in ellipse(mx, y0 + 18, 7, 10):
                sp.px(px_, py, 'glass_l' if (px_ - py) % 9 < 2 else 'glass')
            sp.rect(mx - 6, y0 + 30, 12, 3, 'wood_d')            # tocador
            sp.rect(mx - 4, y0 + 34, 9, 7, 'roof_dd')            # silla
            sp.rect(mx - 3, y0 + 34, 7, 2, 'blue_d')
            sp.px(mx + 4, y0 + 29, 'pink')                       # frasquitos
            sp.px(mx - 4, y0 + 29, 'cream_l')
        for k in range(6):                                       # planta colgante
            sp.px(x0 + w // 2 + (k % 2), y0 + 3 + k, rng.choice(['leaf', 'leaf_l']))
    # reflejos diagonales en el vidrio
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            d = (x - x0) - (y - y0) * 0 + (y - y0)
            if d % 46 in (8, 9, 10) or d % 46 == 13:
                if sp.get(x, y) in (P['glass_d'], P['roof_dd']):
                    sp.px(x, y, 'glass')


def make_shop(kind, seed):
    cfg = SHOPS[kind]
    rng = random.Random(seed)
    W, H = 192, 176
    base = H - 1
    sp = Sprite(W, H)
    wx0, wx1, wt = 4, 188, 34
    trims, cd = wall_fill(sp, cfg['wall'], wx0, wx1, wt, base + 1, rng)
    for y in range(wt, base + 1):                                 # esquineros
        for i in range(3):
            sp.px(wx0 + i, y, trims[0])
            sp.px(wx1 - 1 - i, y, trims[1])
    for y in range(40, 43):
        sp.rect(wx0, y, wx1 - wx0, 1, cd)                         # sombra del alero
    # planta alta
    for i, wxp in enumerate((26, 84, 142)):
        window2(sp, wxp, 44, 24, 20, *trims, rng, box=rng.choice([None, 'pink', 'cream_l']) if i != 1 else None)
    # cornisa
    sp.rect(0, 72, W, 4, trims[0])
    sp.rect(0, 75, W, 1, trims[1])
    for x in range(2, W, 6):
        sp.rect(x, 76, 3, 2, trims[1])
    # letrero solo con iconos (sin texto)
    bg, bgd, deco = cfg['board']
    bx0, bx1 = 56, 136
    sp.rect(bx0 - 2, 77, bx1 - bx0 + 4, 18, 'wood_d')
    sp.rect(bx0 - 2, 77, bx1 - bx0 + 4, 1, 'wood_l')
    sp.rect(bx1, 77, 2, 18, 'wood_dd')
    sp.rect(bx0, 79, bx1 - bx0, 14, bg)
    sp.rect(bx0, 91, bx1 - bx0, 2, bgd)
    for ox in (bx0 + 6, bx1 - 7):                                 # adornos en rombo
        for dx_, dy_ in ((0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)):
            sp.px(ox + dx_, 85 + dy_, deco)
    n = len(cfg['icons'])
    for i, name in enumerate(cfg['icons']):
        rows, legend = ICONS[name]
        draw_icon(sp, rows, (bx0 + bx1) // 2 + (i - (n - 1) / 2) * 20, 86, legend)
    icon, legend = ICONS[cfg['icons'][0]]
    # toldo a rayas con orilla festoneada
    c, ac_d, ac_l = cfg['awning']
    for y in range(95, 111):
        for x in range(2, W - 2):
            stripe = ((x - 2) // 12) % 2 == 0
            if y <= 96:
                col = ac_l if stripe else 'cream_l'
            elif y >= 108:
                col = ac_d if stripe else 'cream_d'
            else:
                col = c if stripe else 'cream'
            if (x - 2) % 12 == 11:
                col = ac_d if stripe else 'cream_d'
            sp.px(x, y, col)
    for x in range(2, W - 2):
        dx = (x - 2) % 12 - 5.5
        stripe = ((x - 2) // 12) % 2 == 0
        for dy in range(4):
            if (dx / 6) ** 2 + (dy / 4) ** 2 <= 1:
                sp.px(x, 111 + dy, (ac_d if stripe else 'cream_d') if dy < 3 else 'roof_dd')
    sp.rect(2, 95, 2, 16, ac_d)
    sp.rect(W - 4, 95, 2, 16, ac_d)
    # planta baja: aparador y puerta
    goods(sp, kind, 14, 118, 98, 40, rng)
    sp.rect(11, 115, 104, 3, trims[0])
    sp.rect(11, 115, 3, 45, trims[0])
    sp.rect(112, 115, 3, 45, trims[1])
    sp.rect(46, 118, 2, 40, trims[0])
    sp.rect(80, 118, 2, 40, trims[0])
    sp.rect(11, 158, 104, 2, 'cream_l')
    dc, dcd = cfg['door']
    zc, zcd = {'panaderia': ('wood_d', 'wood_dd'), 'floreria': ('leaf_d', 'leaf_dd'),
               'farmacia': ('roof_d', 'roof_dd'), 'salon_belleza': ('blue_d', 'roof_dd')}[kind]
    sp.rect(11, 160, 104, 10, zc)                                # zoclo neutro (no color de marca)
    sp.rect(11, 160, 104, 1, 'cream_d')
    for x in range(14, 112, 16):
        sp.rect(x, 162, 12, 1, zcd)
    dx, dy, dw, dh = 136, base - 6 - 57, 30, 58
    sp.rect(dx - 3, dy - 3, dw + 6, dh + 3, trims[0])
    sp.rect(dx + dw, dy - 3, 3, dh + 3, trims[1])
    sp.rect(dx, dy, dw, dh, dc)
    sp.rect(dx + dw - 2, dy, 2, dh, dcd)
    sp.rect(dx + 5, dy + 8, 20, 26, 'glass')
    sp.rect(dx + 5, dy + 8, 20, 1, 'glass_d')
    sp.rect(dx + 5, dy + 8, 1, 26, 'glass_d')
    for k in range(6):
        sp.px(dx + 8 + k, dy + 26 - k * 2, 'glass_l')
        sp.px(dx + 9 + k, dy + 26 - k * 2, 'glass_l')
    sp.rect(dx + 8, dy + 14, 14, 7, 'cream_l')                  # letrero "abierto"
    sp.rect(dx + 8, dy + 20, 14, 1, 'cream_d')
    sp.rect(dx + 10, dy + 16, 10, 1, 'pink')
    sp.rect(dx + 10, dy + 18, 6, 1, 'stone_d')
    sp.px(dx + 15, dy + 12, 'stone_d')
    sp.px(dx + 14, dy + 13, 'stone_d')
    sp.px(dx + 16, dy + 13, 'stone_d')
    sp.rect(dx + 4, dy + 38, 22, 1, dcd)
    sp.rect(dx + 4, dy + 38, 1, 14, dcd)
    sp.rect(dx + 22, dy + 30, 2, 8, 'gold')                     # jaladera
    sp.px(dx + 22, dy + 30, 'gold_l')
    sp.rect(dx - 6, base - 5, dw + 12, 3, 'stone')               # escalon
    sp.rect(dx - 6, base - 5, dw + 12, 1, 'stone_l')
    sp.rect(dx - 6, base - 2, dw + 12, 3, 'stone_d')
    sp.rect(dx - 6, base - 2, dw + 12, 1, 'stone')
    # letrero colgante de bandera
    sp.rect(166, 44, 24, 2, 'lamp_d')
    sp.rect(166, 46, 3, 3, 'lamp_d')
    sp.px(189, 43, 'lamp_l')
    for x in (176, 186):
        sp.rect(x, 46, 1, 4, 'lamp')
    for (x, y) in disc(181, 58, 8):
        sp.px(x, y, 'wood_d')
    for (x, y) in disc(181, 58, 7):
        sp.px(x, y, 'cream_l' if (x - 181) + (y - 58) < 6 else 'cream_d')
    draw_icon(sp, icon, 181, 58, legend)
    # hiedra
    x, y = rng.choice([wx0 + 3, wx1 - 4]), base - 4
    for _ in range(rng.randint(40, 90)):
        if not 95 <= y <= 114:
            sp.px(x, y, 'leaf_dd')
            for ddx, ddy, col in ((-1, 0, 'leaf'), (1, 0, 'leaf_d'), (0, -1, 'leaf_l'), (2, 0, 'leaf')):
                if rng.random() < .6 and wx0 <= x + ddx < wx1:
                    sp.px(x + ddx, y + ddy, col)
        y -= 1
        if rng.random() < .3:
            x = max(wx0, min(wx1 - 3, x + rng.choice([-1, 1])))
    # techo
    top, roof_h, inset_top = 2, 40, 22
    for r in range(roof_h):
        y = top + r
        ins = max(0, inset_top - r)
        x0, x1 = ins, W - ins
        rb = roof_h - 1 - r
        course, k = rb // 6, rb % 6
        for x in range(x0, x1):
            m = (x + course * 7) % 14
            col = 'roof'
            if r == 0:
                col = 'roof_l'
            elif rb == 0:
                col = 'roof_dd'
            elif rb in (1, 2) or k == 0 or (m == 0 and k > 1):
                col = 'roof_d'
            elif k == 5 and 1 <= m <= 6:
                col = 'roof_l'
            if x - x0 < 2 and 0 < r and rb > 2:
                col = 'roof_l'
            if x1 - x <= 2 and r > 0:
                col = 'roof_dd'
            elif x1 - x <= 10 and col == 'roof' and r > 0:
                col = 'roof_d'
            sp.px(x, y, col)
    for _ in range(rng.randint(6, 10)):
        mx, my = rng.randrange(10, W - 10), top + roof_h - 4 - rng.randint(0, 14)
        for (x, y) in disc(mx, my, rng.randint(1, 3)):
            if sp.get(x, y) in (P['roof'], P['roof_d'], P['roof_l']):
                sp.px(x, y, 'bush' if (x + y) % 3 else 'bush_l')
    if cfg['roof_vent']:   # ventila / chimenea metalica
        vx = rng.choice([40, 140])
        sp.rect(vx, 0, 10, 16, 'lamp')
        sp.rect(vx, 0, 3, 16, 'lamp_l')
        sp.rect(vx + 8, 0, 2, 16, 'lamp_d')
        sp.rect(vx - 2, 0, 14, 3, 'lamp_d')
        sp.rect(vx - 2, 0, 13, 1, 'lamp_l')
    else:
        for i, x in enumerate(range(70, 122, 4)):   # tragaluz
            for y in range(6, 18):
                sp.px(x + y // 4, y, 'glass' if (x + y) % 8 else 'glass_l')
    return sp


# ---------------------------------------------------------------- mobiliario urbano
def make_bench():
    sp = Sprite(64, 38)
    base = 37
    shadow_ellipse(sp, 34, base, 30, 2)
    for lx in (5, 55):
        sp.rect(lx, 2, 4, 35, 'lamp')
        sp.rect(lx, 2, 1, 35, 'lamp_l')
        sp.rect(lx + 3, 2, 1, 35, 'lamp_d')
    for i, y in enumerate((4, 9)):                       # respaldo
        sp.rect(2, y, 60, 4, 'wood')
        sp.rect(2, y, 60, 1, 'wood_l')
        sp.rect(2, y + 3, 60, 1, 'wood_d')
    for i, y in enumerate((18, 21, 24)):                 # asiento visto desde arriba
        sp.rect(1, y, 62, 3, 'wood_l' if i < 2 else 'wood')
        sp.rect(1, y + 2, 62, 1, 'wood')
    sp.rect(1, 27, 62, 2, 'wood_d')
    sp.rect(1, 29, 62, 1, 'wood_dd')
    for x in (12, 30, 47):                               # vetas
        sp.px(x, 19, 'wood')
        sp.px(x + 1, 22, 'wood')
    for lx in (2, 58):                                   # descansabrazos
        sp.rect(lx, 14, 4, 3, 'lamp_l')
        sp.rect(lx, 16, 4, 1, 'lamp_d')
    return sp


def make_trash(body=('roof', 'roof_d', 'roof_l'), recycle=False):
    c, cd, cl = body
    sp = Sprite(22, 36)
    base = 35
    shadow_ellipse(sp, 13, base, 9, 2)
    for y in range(9, base - 1):
        for x in range(3, 19):
            col = c
            if x < 6:
                col = cl
            elif x > 15:
                col = cd
            elif (x - 3) % 4 == 3:
                col = cd
            sp.px(x, y, col)
    for y in (13, 28):
        sp.rect(3, y, 16, 2, 'lamp_l')
        sp.rect(3, y + 1, 16, 1, 'lamp_d')
    sp.rect(4, base - 1, 14, 2, 'lamp_d')
    for (x, y) in ellipse(11, 7, 9, 3):
        sp.px(x, y, cl if y < 6 else c)
    for (x, y) in ellipse(11, 7, 6, 1):
        sp.px(x, y, 'hole_d')
    sp.rect(2, 9, 18, 1, cd)
    if recycle:
        for (x, y) in ((10, 18), (11, 18), (12, 19), (9, 20), (13, 21), (10, 22), (11, 22), (12, 22)):
            sp.px(x, y, 'cream_l')
    else:
        sp.rect(8, 3, 4, 4, 'cream_l')                   # papel asomando
        sp.px(11, 3, 'cream_d')
        sp.px(13, 5, 'olive_l')
    return sp


def make_planter():
    rng = random.Random(4)
    sp = Sprite(44, 40)
    base = 39
    shadow_ellipse(sp, 24, base, 20, 2)
    sp.rect(2, 22, 40, 18, 'stone')
    sp.rect(2, 22, 40, 2, 'stone_l')
    sp.rect(38, 22, 4, 18, 'stone_d')
    sp.rect(2, 38, 40, 2, 'stone_d')
    for x in range(4, 38, 8):
        sp.rect(x, 26, 1, 12, 'stone_d')
    b = make_bush(40, 20, rng, flowers='pink').a
    hh = b.shape[0] - 4
    for yy in range(hh):
        for xx in range(b.shape[1]):
            if b[yy, xx, 3] == 255:
                sp.px(2 + xx, 24 - hh + yy, tuple(int(v) for v in b[yy, xx, :3]))
    return sp


def make_chalkboard():
    sp = Sprite(24, 34)
    base = 33
    shadow_ellipse(sp, 13, base, 10, 1)
    for k in range(32):                                  # patas en A
        sp.px(2 + k // 6, 1 + k, 'wood_d')
        sp.px(21 - k // 6, 1 + k, 'wood_dd')
    sp.rect(3, 2, 18, 22, 'wood')
    sp.rect(3, 2, 18, 1, 'wood_l')
    sp.rect(5, 4, 14, 18, 'roof_dd')
    for y, w, c in ((7, 10, 'cream_l'), (10, 8, 'cream_d'), (13, 11, 'cream_l'), (17, 6, 'gold')):
        sp.rect(7, y, w, 1, c)
    sp.px(15, 17, 'pink')
    return sp


def make_flower_buckets():
    rng = random.Random(8)
    sp = Sprite(48, 30)
    base = 29
    shadow_ellipse(sp, 25, base, 22, 2)
    for i, (bx, flower) in enumerate(((2, 'pink'), (17, 'cream_l'), (32, 'olive_l'))):
        for y in range(16, base + 1):
            for x in range(bx, bx + 13):
                sp.px(x, y, 'blue_l' if x < bx + 3 else 'blue_d' if x > bx + 10 else 'blue')
        sp.rect(bx, 16, 13, 1, 'cream_l')
        for (x, y) in disc(bx + 6, 10, 6):
            if y <= 16:
                sp.px(x, y, 'leaf' if (x * 3 + y) % 4 else 'leaf_d')
        for _ in range(9):
            x, y = bx + rng.randint(1, 11), rng.randint(5, 14)
            sp.px(x, y, flower)
            sp.px(x + 1, y, flower if rng.random() < .5 else 'gold' if flower == 'pink' else flower)
        if i == 0:
            sp.px(bx + 6, 4, 'purple')
            sp.px(bx + 7, 4, 'purple')
    return sp


def make_meter():
    sp = Sprite(12, 44)
    base = 43
    shadow_ellipse(sp, 7, base, 4, 1)
    sp.rect(5, 14, 3, 30, 'lamp')
    sp.rect(5, 14, 1, 30, 'lamp_l')
    sp.rect(1, 0, 10, 15, 'lamp')
    sp.rect(1, 0, 10, 2, 'lamp_l')
    sp.rect(9, 0, 2, 15, 'lamp_d')
    sp.rect(3, 3, 6, 5, 'glass')
    sp.px(3, 3, 'glass_l')
    sp.rect(4, 10, 3, 1, 'gold')
    return sp


def make_car_back(col, cold, coll):
    sp = Sprite(52, 84)
    base = 83
    shadow_ellipse(sp, 28, base, 24, 3)
    for y in range(4, 80):
        x0 = 4 if 8 < y < 76 else 6
        for x in range(x0, 52 - x0):
            sp.px(x, y, col)
        sp.px(x0, y, coll)
        sp.px(x0 + 1, y, coll)
        sp.px(51 - x0, y, cold)
        sp.px(50 - x0, y, cold)
    sp.rect(8, 6, 36, 10, coll)            # cofre lejano
    sp.rect(10, 16, 32, 6, 'glass_d')      # parabrisas visto desde arriba
    sp.rect(10, 22, 32, 18, coll)          # techo
    sp.rect(10, 22, 32, 1, 'cream_l')
    sp.rect(38, 22, 4, 18, col)
    for y in range(40, 56):                # medallon trasero
        ins = max(0, (y - 40) // 3)
        for x in range(8 + ins, 44 - ins):
            sp.px(x, y, 'glass_l' if (x + y) % 15 < 3 else 'glass')
    sp.rect(6, 56, 40, 12, col)            # cajuela
    sp.rect(6, 56, 40, 1, coll)
    sp.rect(6, 68, 40, 7, cold)
    sp.rect(6, 68, 7, 4, 'pink')           # calaveras
    sp.rect(39, 68, 7, 4, 'pink')
    sp.rect(6, 71, 7, 1, 'pink_d')
    sp.rect(39, 71, 7, 1, 'pink_d')
    sp.rect(20, 69, 12, 5, 'cream_l')      # placa
    sp.rect(22, 71, 8, 1, 'stone_d')
    sp.rect(4, 75, 44, 4, 'stone')
    sp.rect(4, 75, 44, 1, 'stone_l')
    sp.rect(4, 78, 44, 1, 'stone_d')
    for wx in (2, 46):
        sp.rect(wx, 62, 4, 18, 'hole_d')
    sp.rect(0, 36, 4, 4, cold)
    sp.rect(48, 36, 4, 4, cold)
    return sp


def make_van():
    rng = random.Random(3)
    sp = Sprite(120, 62)
    base = 61
    shadow_ellipse(sp, 62, base, 56, 3)
    for y in range(4, 52):                         # caja
        for x in range(2, 84):
            col = 'cream'
            if y < 9:
                col = 'cream_l'
            elif x < 4:
                col = 'cream_l'
            elif x > 81 or y > 48:
                col = 'cream_d'
            sp.px(x, y, col)
    sp.rect(2, 4, 82, 1, 'fence_l')
    sp.rect(4, 32, 78, 4, 'olive')
    sp.rect(4, 35, 78, 1, 'olive_d')
    for (x, y) in disc(42, 20, 9):                     # logo redondo con flor (sin texto)
        sp.px(x, y, 'leaf_d')
    for (x, y) in disc(42, 20, 8):
        sp.px(x, y, 'cream_l' if (x - 42) + (y - 20) < 7 else 'cream_d')
    draw_icon(sp, ICONS['flor'][0], 42, 20, ICONS['flor'][1])
    for y in range(22, 52):                        # cabina
        x1 = 116 if y > 34 else 104 + (y - 22)
        for x in range(84, x1):
            col = 'olive'
            if y < 25:
                col = 'olive_l'
            elif x > x1 - 3 or y > 48:
                col = 'olive_d'
            sp.px(x, y, col)
    for y in range(25, 34):
        for x in range(90, 102 + (y - 25)):
            sp.px(x, y, 'glass_l' if (x - y) % 11 < 2 else 'glass')
    sp.rect(104, 38, 3, 1, 'olive_l')
    sp.rect(113, 38, 4, 4, 'gold')
    sp.rect(0, 48, 6, 3, 'stone')
    sp.rect(112, 46, 6, 4, 'stone')
    sp.rect(2, 40, 2, 4, 'pink')
    for wx in (24, 98):
        for (x, y) in disc(wx, 51, 10):
            if y <= 51 and sp.get(x, y):
                sp.px(x, y, 'hole')
        for (x, y) in disc(wx, 52, 8):
            sp.px(x, y, 'lamp_d' if (x - wx) + (y - 52) < 6 else 'hole_d')
        for (x, y) in disc(wx, 52, 4):
            sp.px(x, y, 'stone' if x <= wx else 'stone_d')
    return sp


def make_street_sign():
    """Senal de calle sin texto: la placa lleva una flecha."""
    sp = make_sign()
    sp.rect(1, 7, 41, 7, 'purple')
    sp.rect(10, 10, 22, 1, 'cream_l')
    for k in range(4):
        sp.px(31 - k, 10 - k, 'cream_l')
        sp.px(31 - k, 10 + k, 'cream_l')
    sp.rect(16, 21, 12, 4, 'pink')           # placa cruzada lisa
    return sp


def build_props():
    rng = random.Random(31)
    props = {f'tienda_{k}': make_shop(k, i + 1) for i, k in enumerate(SHOPS)}
    props.update({
        'banca': make_bench(),
        'bote_basura': make_trash(),
        'bote_reciclaje': make_trash(('leaf', 'leaf_d', 'leaf_l'), recycle=True),
        'jardinera': make_planter(),
        'pizarra_panaderia': make_chalkboard(),
        'cubetas_flores': make_flower_buckets(),
        'parquimetro': make_meter(),
        'farol': make_lamp(),
        'senal_calle': make_street_sign(),
        'arbol_banqueta': make_tree(52, 70, 22, random.Random(7)),
        'arbol_grande': make_tree(64, 84, 28, random.Random(5)),
        'arbusto_lejano': make_bush(40, 18, rng, pal=('mid_d', 'mid_d', 'mid', 'mid_l')),
        'seto': make_bush(64, 22, rng),
        'arbusto_flores': make_bush(48, 24, rng, flowers='pink'),
        'auto_lateral_teal': make_car_side('roof', 'roof_dd', 'roof_l', 1),
        'auto_lateral_terracota': make_car_side('wood', 'wood_dd', 'wood_l', 2),
        'auto_lateral_oliva': make_car_side('olive', 'olive_d', 'olive_l', 3),
        'auto_lateral_azul': make_car_side('blue', 'blue_d', 'blue_l', 4),
        'auto_lateral_crema': make_car_side('cream', 'cream_d', 'cream_l', 5),
        'auto_frontal_azul': make_car_front('blue', 'blue_d', 'blue_l'),
        'auto_trasero_terracota': make_car_back('wood', 'wood_dd', 'wood_l'),
        'camioneta_reparto': make_van(),
    })
    return {k: trim(v) for k, v in props.items()}


# ---------------------------------------------------------------- tiles extra
def t_parking(rng):
    def f(cv, r):
        for y in range(T):
            cv.px(0, y, 'stripe')
            cv.px(1, y, 'stripe_d')
        for _ in range(6):
            cv.px_on(rng.randrange(2), rng.randrange(T), 'road_l', {P['stripe'], P['stripe_d']})
    return t_road(rng, f)


def t_pit(rng):
    a = t_sidewalk(rng)
    cv = tcanvas()
    cv.a[:] = a
    cv.rect(3, 3, 26, 26, 'curb')
    cv.rect(3, 3, 26, 1, 'curb_l')
    cv.rect(3, 3, 1, 26, 'curb_l')
    g = t_grass(rng)                       # alcorque con pasto (sin tierra morada)
    cv.a[5:27, 5:27] = g[5:27, 5:27]
    for x in range(5, 27):
        cv.px(x, 5, 'grass_sh')            # sombra del bordillo
    return cv.a


def t_drain(rng):
    def f(cv, r):
        for x in range(T):
            cv.px(x, 0, 'hole_d')
            cv.px(x, 1, 'road_d')
        cv.rect(7, 2, 18, 9, 'curb')
        cv.rect(8, 3, 16, 7, 'hole_d')
        for x in range(9, 24, 2):
            cv.rect(x, 3, 1, 7, 'lamp')
        cv.rect(7, 10, 18, 1, 'curb_l')
    return t_road(rng, f)


def t_paver(rng):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'side')
    for y in range(T):
        for x in range(T):
            row = y // 4
            if y % 4 == 3 or (x + row % 2 * 4) % 8 == 7:
                cv.px(x, y, 'side_d')
            else:
                b = hash(((x + row % 2 * 4) // 8, row, 'p')) % 6
                if b == 0:
                    cv.px(x, y, 'side_l')
                elif b == 1:
                    cv.px(x, y, 'stone_l')
                elif y % 4 == 0:
                    cv.px(x, y, 'side_l')
    for _ in range(5):
        cv.px_on(rng.randrange(T), rng.randrange(T), 'grass_d', {P['side_d']})
    return cv.a


def build_tileset_comercial():
    _, _, base_tiles = build_tileset()
    rng = random.Random(12)
    skip = {'tierra_morada', 'tierra_surcos'}    # sin cuadros de tierra en esta zona
    tiles = [(k, v) for k, v in base_tiles.items() if k not in skip] + [
        ('asfalto_cajon', t_parking(rng)), ('banqueta_alcorque', t_pit(rng)),
        ('asfalto_coladera', t_drain(rng)), ('banqueta_adoquin', t_paver(rng)),
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
    return sheet, meta, dict(tiles)


# ---------------------------------------------------------------- escena
def build_reference(tiles, props, sky, far):
    COLS, ROWS = 96, 30
    Wm, Hm = COLS * T, ROWS * T
    rng = random.Random(17)
    m = np.zeros((Hm, Wm, 3), np.uint8)
    for x in range(0, Wm, sky.shape[1]):
        m[0:160, x:x + sky.shape[1]] = sky[:, :min(sky.shape[1], Wm - x)]

    def paste(a, x0, y0):
        for yy, xx in zip(*np.nonzero(a[:, :, 3])):
            X, Y = x0 + xx, y0 + yy
            if 0 <= X < Wm and 0 <= Y < Hm:
                if a[yy, xx, 3] == 255:
                    m[Y, X] = a[yy, xx, :3]
                else:
                    c = tuple(int(v) for v in m[Y, X])
                    m[Y, X] = SHADOW.get(c, tuple(int(v * .78) for v in c))

    for x in range(0, Wm, far.shape[1]):
        paste(far, x, 160 - far.shape[0])

    VC = 46                     # columnas 44-45 banqueta, 46-50 calle, 51-52 banqueta
    side_cols = {VC - 2, VC - 1, VC + 5, VC + 6}
    grid = {}
    for r in range(5, ROWS):
        for c in range(COLS):
            v = rng.random()
            grid[r, c] = 'flores_a' if v < .03 else 'pasto_a' if v < .55 else 'pasto_b' if v < .82 else 'pasto_c'
    # banqueta ancha 15-18 | arroyo 19-25 (estacionamiento 19-20 y 24-25, carriles 21-23) | banqueta 26-28
    for c in range(COLS):
        grid[15, c] = grid[16, c] = grid[17, c] = 'banqueta_adoquin'
        grid[18, c] = 'banqueta_bordillo_s'
        grid[19, c] = 'asfalto_coladera' if c % 12 == 6 else 'asfalto_sombra_bordillo'
        for r in range(20, 26):
            grid[r, c] = rng.choice(['asfalto_a', 'asfalto_a', 'asfalto_b'])
        if c % 4 == 0 and not VC - 4 <= c <= VC + 9:
            for r in (20, 24, 25):
                grid[r, c] = 'asfalto_cajon'
        if not VC - 4 <= c <= VC + 9:
            grid[22, c] = 'linea_h'
        grid[26, c] = 'banqueta_bordillo_n'
        grid[27, c] = grid[28, c] = 'banqueta'
    for r in range(5, ROWS):
        grid[r, VC - 2], grid[r, VC - 1] = 'banqueta', 'banqueta_bordillo_e'
        grid[r, VC + 5], grid[r, VC + 6] = 'banqueta_bordillo_w', 'banqueta'
        for c in range(VC, VC + 5):
            grid[r, c] = rng.choice(['asfalto_a', 'asfalto_b'])
        if not 13 <= r <= 29:
            grid[r, VC + 2] = 'linea_v'
    grid[18, VC - 2], grid[18, VC - 1] = 'banqueta_bordillo_s', 'banqueta_esquina_se'
    grid[18, VC + 5], grid[18, VC + 6] = 'banqueta_esquina_sw', 'banqueta_bordillo_s'
    grid[26, VC - 2], grid[26, VC - 1] = 'banqueta_bordillo_n', 'banqueta_esquina_ne'
    grid[26, VC + 5], grid[26, VC + 6] = 'banqueta_esquina_nw', 'banqueta_bordillo_n'
    for c in range(VC, VC + 5):
        for r in (15, 16, 17, 18, 26, 27, 28):
            grid[r, c] = 'cebra_v'
        grid[19, c] = 'asfalto_a'
    for r in range(19, 26):
        for c in side_cols:
            grid[r, c] = 'cebra_h'
    for (r, c, n) in ((21, 9, 'bache_grande'), (23, 70, 'bache_chico'), (8, VC + 3, 'bache_grande'),
                      (21, 33, 'asfalto_grieta'), (23, 84, 'asfalto_grieta'), (23, 58, 'bache_chico')):
        grid[r, c] = n

    shops_order = ['panaderia', 'floreria', 'salon_belleza', 'farmacia', 'floreria', 'panaderia',
                   'farmacia', 'salon_belleza', 'panaderia', 'floreria', 'salon_belleza', 'farmacia']
    shop_cols = [1 + 7 * i for i in range(6)] + [VC + 8 + 7 * i for i in range(6)]
    alleys = [c + 6 for c in shop_cols if c + 6 < COLS and c + 6 not in range(VC - 2, VC + 7)]
    pits = alleys[::2]
    for c in pits:
        grid[17, c] = 'banqueta_alcorque'
    low_pits = [5, 19, 33, 63, 77, 91]
    for c in low_pits:
        grid[27, c] = 'banqueta_alcorque'

    for (r, c), n in grid.items():
        m[r * T:(r + 1) * T, c * T:(c + 1) * T] = tiles[n]

    objs = []

    def put(name, cx, base, flip=False):
        objs.append((base, name, cx, flip))

    x = 8
    while x < Wm:
        if not (VC - 3) * 32 <= x <= (VC + 8) * 32:
            put('arbusto_lejano', x, rng.randint(176, 192))
        x += rng.randint(24, 40)
    for c in alleys + [0]:
        put('arbol_grande', c * 32 + 16, rng.randint(250, 300))
    for i, c in enumerate(shop_cols):
        kind = shops_order[i]
        x0 = c * 32
        put(f'tienda_{kind}', x0 + 96, 479)
        if kind == 'panaderia':
            put('pizarra_panaderia', x0 + 122, 502)
        if kind == 'floreria':
            put('cubetas_flores', x0 + 64, 502)
        else:
            put('banca', x0 + 64, 540)
        if i % 2 == 0:
            put('farol', x0 + 16, 598)
        put('bote_basura' if i % 3 else 'bote_reciclaje', x0 + 104, 536)
    for c in pits:
        put('arbol_banqueta', c * 32 + 16, 572)
    for c in alleys:
        if c not in pits:
            put('jardinera', c * 32 + 16, 532)
    for c in range(4, COLS, 8):
        if not VC - 4 <= c <= VC + 9:
            put('parquimetro', c * 32 + 4, 600)
    put('senal_calle', (VC + 5) * 32 + 4, 598)
    put('farol', (VC - 2) * 32 + 14, 598)
    put('farol', (VC + 6) * 32 + 18, 598)

    # banqueta inferior y franja de pasto
    for c in low_pits:
        put('arbol_banqueta', c * 32 + 16, 892)
    for x in (300, 700, 1100, 1300, 1900, 2300, 2700):
        put('farol', x, 850)
    for x in (420, 980, 2100, 2550):
        put('bote_basura', x, 884)
    for x in (560, 1220, 2000, 2850):
        put('jardinera', x, 910)
    put('senal_calle', (VC - 1) * 32 + 4, 862)
    for x in range(0, Wm, 64):
        if not (VC - 2) * 32 <= x < (VC + 7) * 32:
            put('seto', x + 32, 958)
    for x in (120, 520, 900, 1250, 1850, 2250, 2650, 3000):
        put('arbol_grande', x + rng.randint(-20, 20), rng.randint(955, 959))

    # trafico (carriles 716 y 764) y autos en cajones (666 y 826), sin traslaparse
    sides = ['auto_lateral_teal', 'auto_lateral_terracota', 'auto_lateral_oliva', 'auto_lateral_azul',
             'auto_lateral_crema']
    for i, x in enumerate((180, 560, 940, 1260, 1900, 2300, 2700, 2990)):    # carril bajo -> derecha
        put('camioneta_reparto' if i == 4 else sides[i % 5], x, 764)
    for i, x in enumerate((380, 760, 1110, 1820, 2150, 2520, 2860)):         # carril alto <- izquierda
        put(sides[(i + 2) % 5], x, 716, flip=True)
    stalls = [k * 128 + 64 for k in range(24) if not 1280 <= k * 128 <= 1792]
    for i, x in enumerate(stalls):
        if i % 5 != 2:
            put(sides[(i + 1) % 5], x, 666, flip=i % 2 == 1)
        if i % 4 != 1:
            put(sides[(i + 3) % 5], x, 826, flip=i % 2 == 0)
    put('auto_frontal_azul', VC * 32 + 40, 420)
    put('auto_trasero_terracota', VC * 32 + 120, 958)

    for base, name, cx, flip in sorted(objs, key=lambda o: o[0]):
        a = props[name][:, ::-1] if flip else props[name]
        h, w = a.shape[:2]
        paste(a, cx - w // 2, base - h + 1)
    return m


def save(a, name):
    Image.fromarray(a).save(os.path.join(OUT, name))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    sheet, tmeta, tiles = build_tileset_comercial()
    save(sheet, 'tileset_suelo.png')
    with open(os.path.join(OUT, 'tileset_suelo.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen='tileset_suelo.png', tile=T, columnas=8, origen='arriba-izquierda',
                       tiles=tmeta), f, indent=2, ensure_ascii=False)
    props = build_props()
    atlas, meta = pack_atlas(props)
    save(atlas, 'props_atlas.png')
    with open(os.path.join(OUT, 'props_atlas.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen='props_atlas.png', ancho=atlas.shape[1], alto=atlas.shape[0],
                       origen='arriba-izquierda (y_unity = desde abajo)', pivote=[0.5, 0.0],
                       separacion=4, sprites=meta), f, indent=2, ensure_ascii=False)
    rng = random.Random(4)
    sky, far = build_sky(rng), build_far_trees(rng)
    save(sky, 'fondo_cielo.png')
    save(far, 'fondo_arboles.png')
    ref = build_reference(tiles, props, sky, far)
    save(ref, 'mapa_referencia.png')
    crop = ref[300:540, 1120:1504]
    Image.fromarray(crop).resize((crop.shape[1] * 4, crop.shape[0] * 4), Image.NEAREST) \
        .save(os.path.join(OUT, 'preview_x4.png'))

    assert sheet.shape[0] % T == 0 and sheet.shape[1] % T == 0
    for m_ in meta:
        assert props[m_['name']][-1, :, 3].any(), f"{m_['name']} sin base en la fila inferior"
    flat = atlas[atlas[:, :, 3] == 255][:, :3]
    brand = sum(int(np.all(flat == P[c], axis=1).sum()) for c in ('pink', 'purple', 'gold'))
    print(f'atlas: {brand / len(flat) * 100:.2f}% pixeles de color de marca')
    print('tiles:', len(tmeta), '| props:', len(meta), '| atlas', atlas.shape[1], 'x', atlas.shape[0],
          '| mapa', ref.shape[1], 'x', ref.shape[0])
