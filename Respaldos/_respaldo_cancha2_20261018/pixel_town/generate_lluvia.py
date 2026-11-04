"""Zona "Lluvia": el vecindario residencial con el suelo mojado (resolucion nativa, tiles 32x32).

Reutiliza los tiles y props de generate_unity.py; aqui solo se genera lo nuevo:
asfalto y banquetas mojadas, charcos con reflejo del cielo, baches con agua, cielo nublado
y arboles de fondo mas frios y oscuros. No se dibujan gotas: la lluvia va como particulas en Unity.
Salidas en unity/lluvia/:
  tileset_suelo.png + .json   solo tiles mojados nuevos (mismos nombres base + sufijo _mojado)
  props_atlas.png + .json     calcomanias de charcos y baches (4 px separacion, base abajo)
  fondo_cielo.png, fondo_arboles.png
  mapa_referencia.png         escena armada con los props residenciales (solo preview)
"""
import json
import os
import random
import numpy as np
from PIL import Image

from generate_town import P, Canvas, ellipse, hx
from generate_unity import (T, Sprite, trim, build_tileset, build_props, pack_atlas, build_far_trees,
                            build_reference)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'lluvia')

# ---------------------------------------------------------------- paleta mojada
WET = {P[k]: hx(v) for k, v in dict(
    road='#463e5c', road_d='#3b3450', road_l='#524a6c', hole='#2f283d', hole_d='#272134',
    side='#948da3', side_d='#7b7490', side_l='#a6a0b8', curb='#665f78', curb_l='#b0aac0',
    stripe='#c4bfb4', stripe_d='#a9a499', dash='#b4a263', dash_d='#93834a',
).items()}
GLINT, GLINT_L = hx('#6b6789'), hx('#8b89a8')        # brillo del asfalto mojado
SIDE_GLINT = hx('#b8b4ca')
WATER, WATER_L, WATER_D = hx('#8e9bb3'), hx('#b4c0d0'), hx('#5d6884')   # cielo nublado reflejado
GRASS_WET = P['grass_sh']

ROAD_WET = {WET[P['road']], WET[P['road_d']], WET[P['road_l']]}
SIDE_WET = {WET[P['side']], WET[P['side_l']]}


def remap(a, mp):
    out = a.copy()
    for src, dst in mp.items():
        out[np.all(a[..., :3] == src, axis=-1), :3] = dst
    return out


def glints(cv, rng, allowed, n, colors):
    for _ in range(n):
        x, y = rng.randrange(T), rng.randrange(T)
        c = rng.choice(colors)
        for k in range(rng.randint(2, 4)):
            if cv.get(x + k, y) in allowed:
                cv.px(x + k, y, c)


def puddle(px, get, cx, cy, a, b, rng, allowed=None):
    """Charco: refleja el cielo nublado; orilla lejana oscura, orilla cercana clara, ondas."""
    pts = ellipse(cx, cy, a, b) | ellipse(cx + a // 2, cy + 1, max(2, a // 2), max(1, b - 1))
    ok = (lambda x, y: True) if allowed is None else (lambda x, y: get(x, y) in allowed)
    for (x, y) in pts:
        if ok(x, y):
            c = WATER
            if (x, y - 1) not in pts:
                c = WATER_D
            elif (x, y + 1) not in pts:
                c = WATER_L
            px(x, y, c)
    for k in range(max(2, a - 2)):                       # reflejo de nube clara
        x, y = cx - a // 2 + k, cy - max(0, b // 2 - 1)
        if (x, y) in pts and ok(x, y) and k % 4 != 3:
            px(x, y, WATER_L)
    for _ in range(rng.randint(1, 2)):                   # ondas
        rx, ry = cx + rng.randint(-a // 3, a // 3), cy + rng.randint(0, max(0, b // 2))
        for (x, y) in ellipse(rx, ry, 3, 1) - ellipse(rx, ry, 2, 0):
            if (x, y) in pts and ok(x, y):
                px(x, y, WATER_L)
    return pts


# ---------------------------------------------------------------- tiles
def build_tiles_wet():
    _, _, base = build_tileset()
    rng = random.Random(41)
    wanted = ['asfalto_a', 'asfalto_b', 'asfalto_sombra_bordillo', 'asfalto_grieta', 'linea_h', 'linea_v',
              'cebra_h', 'cebra_v', 'banqueta', 'banqueta_bordillo_s', 'banqueta_bordillo_n',
              'banqueta_bordillo_e', 'banqueta_bordillo_w', 'banqueta_esquina_se', 'banqueta_esquina_sw',
              'banqueta_esquina_ne', 'banqueta_esquina_nw']
    tiles = {}
    for name in wanted:
        cv = Canvas(T, T, wrapy=True)
        cv.a[:] = remap(base[name], WET)
        if name.startswith('banqueta'):
            glints(cv, rng, SIDE_WET, 4, [SIDE_GLINT])
        else:
            glints(cv, rng, ROAD_WET, 10, [GLINT, GLINT, GLINT_L])
        tiles[name + '_mojado'] = cv.a
    for name, big in (('bache_agua_grande', True), ('bache_agua_chico', False)):
        cv = Canvas(T, T, wrapy=True)
        cv.a[:] = remap(base['bache_grande' if big else 'bache_chico'], WET)
        a_, b_ = (6, 2) if big else (3, 1)
        for (x, y) in ellipse(16, 17, a_, b_):
            cv.px(x, y, WATER)
        for k in range(a_):
            cv.px(13 + k, 17, WATER_L if k % 3 != 2 else WATER)
        glints(cv, rng, ROAD_WET, 6, [GLINT])
        tiles[name] = cv.a
    for name, src, a_, b_ in (('asfalto_charco_a', 'asfalto_a_mojado', 10, 4),
                              ('asfalto_charco_b', 'asfalto_b_mojado', 7, 3),
                              ('banqueta_charco', 'banqueta_mojado', 7, 3)):
        cv = Canvas(T, T, wrapy=True)
        cv.a[:] = tiles[src]
        puddle(cv.px, cv.get, 14, 16, a_, b_, rng)
        tiles[name] = cv.a
    return tiles


def sheet_from(tiles):
    names = list(tiles)
    cols = 8
    rows = (len(names) + cols - 1) // cols
    sheet = np.zeros((rows * T, cols * T, 4), np.uint8)
    meta = []
    for i, n in enumerate(names):
        x, y = (i % cols) * T, (i // cols) * T
        sheet[y:y + T, x:x + T, :3] = tiles[n]
        sheet[y:y + T, x:x + T, 3] = 255
        meta.append(dict(name=n, index=i, x=x, y=y, w=T, h=T))
    return sheet, meta


# ---------------------------------------------------------------- calcomanias (props)
def decal_puddle(w, h, a_, b_, seed, grass_rim=False):
    rng = random.Random(seed)
    sp = Sprite(w, h)
    cx, cy = w // 2 - a_ // 4, h // 2
    if grass_rim:   # pasto aplastado y mas oscuro alrededor (sin tierra)
        for (x, y) in ellipse(cx + a_ // 4, cy, a_ + a_ // 2 + 2, b_ + 2):
            sp.px(x, y, GRASS_WET if rng.random() < .85 else P['grass_d'])
    puddle(sp.px, sp.get, cx, cy, a_, b_, rng)
    return sp


def decal_pothole(big, seed):
    rng = random.Random(seed)
    w, h = (30, 14) if big else (18, 9)
    sp = Sprite(w, h)
    cx, cy = w // 2 - 2, h // 2
    a_, b_ = (9, 4) if big else (5, 2)
    pts = ellipse(cx, cy, a_, b_) | ellipse(cx + a_ // 2 + 1, cy + 1, max(2, a_ // 2), max(1, b_ - 1))
    for (x, y) in pts:   # borde roto y brillante del asfalto mojado
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            if (x + dx, y + dy) not in pts:
                sp.px(x + dx, y + dy, GLINT if dy >= 0 else WET[P['road_d']])
    for (x, y) in pts:
        c = WET[P['hole']]
        if (x, y - 1) not in pts or (x, y - 2) not in pts:
            c = WET[P['hole_d']]
        sp.px(x, y, c)
    for (x, y) in ellipse(cx + 1, cy + 1, a_ - 3, max(1, b_ - 2)):   # agua dentro del bache
        sp.px(x, y, WATER_D if (x, y - 1) not in pts else WATER)
    for k in range(max(2, a_ - 4)):
        sp.px(cx - a_ // 2 + 2 + k, cy + 1, WATER_L if k % 3 != 2 else WATER)
    return sp


def build_props_wet():
    props = {
        'charco_grande': decal_puddle(48, 16, 18, 5, 1),
        'charco_mediano': decal_puddle(32, 12, 11, 4, 2),
        'charco_chico': decal_puddle(20, 9, 6, 2, 3),
        'charco_pasto': decal_puddle(44, 18, 12, 4, 4, grass_rim=True),
        'bache_agua_grande': decal_pothole(True, 5),
        'bache_agua_chico': decal_pothole(False, 6),
    }
    return {k: trim(v) for k, v in props.items()}


# ---------------------------------------------------------------- fondos
def build_cloudy_sky(rng, w=1024, h=160):
    cv = Canvas(w, h)
    top, low = hx('#77839a'), hx('#8692a7')
    for y in range(h):
        for x in range(w):
            band = y > 64 or (y > 54 and (x + y) % 2 == 0) or (y > 44 and (x + y) % 4 == 0)
            cv.px(x, y, low if band else top)
    for c, cs, cl, ymin, ymax, rmin, rmax in (
            (hx('#8c97ab'), hx('#79849b'), hx('#a3adbe'), 6, 56, 10, 20),
            (hx('#9ea8b9'), hx('#87929f'), hx('#b4bdca'), 30, 100, 8, 16)):
        x = rng.randint(0, 60)
        while x < w:
            cy, cw = rng.randint(ymin, ymax), rng.randint(100, 220)
            pts, bx = set(), x
            while bx < x + cw:
                r = rng.randint(rmin, rmax)
                pts |= {(bx + dx, cy - r // 2 + dy) for dx in range(-r, r + 1) for dy in range(-r, r + 1)
                        if dx * dx + dy * dy <= r * r + r}
                bx += rng.randint(8, 14)
            pts = {(px_, py) for (px_, py) in pts if py <= cy + 8}
            blocks = {(px_ // 3, py // 3) for (px_, py) in pts}
            pts = {(bx_ * 3 + dx, by * 3 + dy) for (bx_, by) in blocks for dx in range(3) for dy in range(3)}
            for (px_, py) in pts:
                col = c
                if (px_, py - 3) not in pts:
                    col = cl
                elif (px_, py + 3) not in pts or (px_, py + 6) not in pts:
                    col = cs
                cv.px(px_, py, col)
            x += cw + rng.randint(-40, 30)
    return cv.a


def build_cold_trees(rng):
    far = build_far_trees(rng)
    return remap(far, {P['far1']: hx('#6c809c'), P['far2']: hx('#56698a')})


# ---------------------------------------------------------------- mapa de referencia
def build_reference_wet(wet_tiles, decals, sky, far):
    _, _, base = build_tileset()
    tiles = dict(base)
    for name in list(base):   # el mapa residencial pide nombres base: se sustituyen por los mojados
        if name + '_mojado' in wet_tiles:
            tiles[name] = wet_tiles[name + '_mojado']
    tiles['bache_grande'] = wet_tiles['bache_agua_grande']
    tiles['bache_chico'] = wet_tiles['bache_agua_chico']
    rng = random.Random(23)

    def hook(m, grid):
        road_cells = [(r, c) for (r, c), n in grid.items() if n in ('asfalto_a', 'asfalto_b')]
        side_cells = [(r, c) for (r, c), n in grid.items() if n == 'banqueta']
        grass_cells = [(r, c) for (r, c), n in grid.items() if n.startswith('pasto') and r > 6]
        for (r, c) in rng.sample(road_cells, 22):
            m[r * T:(r + 1) * T, c * T:(c + 1) * T] = wet_tiles[rng.choice(
                ['asfalto_charco_a', 'asfalto_charco_b', 'bache_agua_grande', 'bache_agua_chico'])]
        for (r, c) in rng.sample(side_cells, 10):
            m[r * T:(r + 1) * T, c * T:(c + 1) * T] = wet_tiles['banqueta_charco']

        def stamp(name, cells, n):
            a = decals[name]
            h, w = a.shape[:2]
            for (r, c) in rng.sample(cells, n):
                x0, y0 = c * T + rng.randint(0, max(0, T - w // 2)), r * T + rng.randint(4, 20)
                for yy, xx in zip(*np.nonzero(a[:, :, 3])):
                    if 0 <= y0 + yy < m.shape[0] and 0 <= x0 + xx < m.shape[1]:
                        m[y0 + yy, x0 + xx] = a[yy, xx, :3]
        stamp('bache_agua_grande', road_cells, 10)     # mas baches
        stamp('bache_agua_chico', road_cells, 14)
        stamp('charco_grande', road_cells, 8)
        stamp('charco_mediano', side_cells, 6)
        stamp('charco_pasto', grass_cells, 12)
        stamp('charco_chico', grass_cells, 10)

    return build_reference(tiles, build_props(), sky, far, beds=False, ground_hook=hook)


def save(a, name):
    Image.fromarray(a).save(os.path.join(OUT, name))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    wet = build_tiles_wet()
    sheet, tmeta = sheet_from(wet)
    assert sheet.shape[0] % T == 0 and sheet.shape[1] % T == 0
    save(sheet, 'tileset_suelo.png')
    with open(os.path.join(OUT, 'tileset_suelo.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen='tileset_suelo.png', tile=T, columnas=8, origen='arriba-izquierda',
                       nota='solo tiles nuevos; el resto se toma de unity/residencial/tileset_suelo.png', tiles=tmeta),
                  f, indent=2, ensure_ascii=False)
    decals = build_props_wet()
    atlas, meta = pack_atlas(decals, max_w=128)
    for m_ in meta:
        assert decals[m_['name']][-1, :, 3].any(), m_['name']
    save(atlas, 'props_atlas.png')
    with open(os.path.join(OUT, 'props_atlas.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen='props_atlas.png', ancho=atlas.shape[1], alto=atlas.shape[0],
                       origen='arriba-izquierda (y_unity = desde abajo)', pivote=[0.5, 0.0], separacion=4,
                       nota='calcomanias de suelo; los demas props se toman de unity/residencial/props_atlas.png',
                       sprites=meta), f, indent=2, ensure_ascii=False)
    rng = random.Random(4)
    sky, far = build_cloudy_sky(rng), build_cold_trees(rng)
    save(sky, 'fondo_cielo.png')
    save(far, 'fondo_arboles.png')
    ref = build_reference_wet(wet, decals, sky, far)
    save(ref, 'mapa_referencia.png')
    crop = ref[400:640, 848:1232]
    Image.fromarray(crop).resize((crop.shape[1] * 4, crop.shape[0] * 4), Image.NEAREST) \
        .save(os.path.join(OUT, 'preview_x4.png'))
    print('tiles:', len(tmeta), sheet.shape[1], 'x', sheet.shape[0], '| props:', len(meta),
          '| atlas', atlas.shape[1], 'x', atlas.shape[0], '| mapa', ref.shape[1], 'x', ref.shape[0])
