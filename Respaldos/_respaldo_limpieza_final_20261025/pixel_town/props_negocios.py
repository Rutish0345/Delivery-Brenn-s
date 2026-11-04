"""Negocios de barrio y relleno del Residencial (octubre 2026).

- Tiendas, camioneta de reparto, pizarrón, parquímetro y cubetas de flores: se toman tal cual del atlas de la
  zona Comercial (unity/comercial/props_atlas.png + .json, que genera generate_comercial.py). Usan la misma
  paleta P que las casas (techo teal, muros madera/ladrillo/crema/olivo), así que no se retocan.
- Arte nuevo con el estilo del proyecto (luz arriba-izquierda, sin contorno negro, sombra plana):
  aros de básquet (dos iguales, uno espejado), huerto comunitario y mesa de picnic. La cancha es de suelo:
  6x4 tiles 'cancha_NN'.
"""
import json
import os

import numpy as np
from PIL import Image

from generate_town import P
from generate_unity import Sprite, shadow_ellipse, trim

COMERCIAL = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'comercial')
DEL_COMERCIAL = ('tienda_farmacia', 'tienda_panaderia', 'tienda_floreria', 'tienda_salon_belleza',
                 'camioneta_reparto', 'pizarra_panaderia', 'parquimetro', 'cubetas_flores')
T = 32
CANCHA_W, CANCHA_H = 6, 4          # tiles (192 x 128 px)
# Los dos aros son el mismo arte: el sur con la red hacia arriba y el norte espejado verticalmente. Su pivote
# es la base del poste (PIE_ARO px desde el pie del sprite en el sur, desde arriba en el norte): así el
# Y-sorting y el collider se toman de la base y no del tablero.
ARO_W, ARO_H, PIE_ARO = 28, 36, 3
PIVOTES = {'aro_basquet_sur': PIE_ARO / ARO_H, 'aro_basquet_norte': 1 - PIE_ARO / ARO_H}

P.setdefault('cancha', (0xB0, 0x6A, 0x5A))       # piso de la cancha (arcilla apagada, entre roof y wood)
P.setdefault('cancha_d', (0x96, 0x58, 0x4C))
P.setdefault('cancha_l', (0xC2, 0x7E, 0x6C))
P.setdefault('raya', (0xEE, 0xE6, 0xD2))


def props_comercial():
    meta = json.load(open(os.path.join(COMERCIAL, 'props_atlas.json'), encoding='utf-8'))
    im = np.array(Image.open(os.path.join(COMERCIAL, 'props_atlas.png')).convert('RGBA'))
    sprites = {s['name']: s for s in meta['sprites']}
    res = {}
    for n in DEL_COMERCIAL:
        s = sprites[n]
        res[n] = im[s['y']:s['y'] + s['h'], s['x']:s['x'] + s['w']].copy()
    return res


def make_garden():
    """Huerto comunitario: dos camas de madera con hortalizas y un letrerito."""
    sp = Sprite(132, 70)
    base = 69
    shadow_ellipse(sp, 66, base, 62, 3)
    for y0 in (14, 42):
        sp.rect(2, y0, 128, 24, 'wood_d')            # cama
        sp.rect(3, y0 + 1, 126, 3, 'wood_l')
        sp.rect(3, y0 + 4, 126, 16, 'wood_dd')       # tierra
        for x in range(5, 127, 4):
            sp.px(x, y0 + 6 + (x % 3) * 4, 'wood_d')
        sp.rect(3, y0 + 20, 126, 3, 'wood')
        sp.rect(3, y0 + 22, 126, 1, 'wood_dd')
        for i, x in enumerate(range(6, 124, 12)):    # hortalizas en fila
            tipo = (i + y0 // 14) % 3
            if tipo == 0:                            # lechuga: mata redonda de 9x7
                for dy, fila in enumerate(('  ###  ', ' ##### ', '#######', '#######', ' ##### ')):
                    for dx, ch in enumerate(fila):
                        if ch == '#':
                            sp.px(x + dx, y0 + 6 + dy, 'leaf_l' if dy < 2 else 'leaf' if dy < 4 else 'leaf_d')
            elif tipo == 1:                          # jitomatera con su vara
                sp.rect(x + 3, y0 - 4, 1, 16, 'wood')
                for dx, dy in ((1, -2), (4, 0), (2, 2), (5, 4), (1, 6), (4, 8)):
                    sp.rect(x + dx, y0 + dy, 2, 2, 'leaf')
                for dx, dy in ((0, 1), (6, 3), (2, 7), (6, 9)):
                    sp.rect(x + dx, y0 + dy, 2, 2, 'pink_d')
                    sp.px(x + dx, y0 + dy, 'pink')
            else:                                    # zanahorias: tres penachos y la punta naranja
                for dx in (0, 3, 6):
                    sp.px(x + dx, y0 + 5, 'leaf_l')
                    sp.px(x + dx - 1, y0 + 6, 'leaf')
                    sp.px(x + dx + 1, y0 + 6, 'leaf')
                    sp.px(x + dx, y0 + 7, 'leaf_d')
                    sp.rect(x + dx, y0 + 8, 2, 2, 'gold')
    sp.rect(118, 2, 2, 14, 'wood')                   # letrero
    sp.rect(108, 1, 20, 9, 'wood_l')
    sp.rect(109, 2, 18, 7, 'cream')
    sp.rect(112, 4, 3, 3, 'leaf')
    sp.rect(117, 5, 7, 1, 'wood_d')
    return sp


def make_picnic_table():
    """Mesa de picnic de madera con sus dos bancas, vista de frente."""
    sp = Sprite(76, 46)
    base = 45
    shadow_ellipse(sp, 38, base, 34, 3)
    sp.rect(4, 6, 68, 8, 'wood')                     # cubierta
    sp.rect(4, 6, 68, 2, 'wood_l')
    sp.rect(4, 13, 68, 1, 'wood_dd')
    for x in range(14, 72, 14):
        sp.px(x, 9, 'wood_d')
        sp.px(x, 10, 'wood_d')
    for x0 in (12, 58):                              # patas en X
        for i in range(24):
            sp.px(x0 + i // 4, 14 + i, 'wood_d')
            sp.px(x0 + 5 - i // 4, 14 + i, 'wood')
    sp.rect(0, 26, 76, 6, 'wood')                    # banca delantera
    sp.rect(0, 26, 76, 2, 'wood_l')
    sp.rect(0, 31, 76, 1, 'wood_dd')
    for x in (4, 68):
        sp.rect(x, 32, 4, base - 33, 'wood_d')
    return sp


def court_tiles():
    """Cancha de básquet completa en 6x4 tiles. Las marcas de una mitad (fondo, laterales, área y arco de tres
    puntos) se dibujan una vez y se espejan exactas; la línea y el círculo central quedan con aire alrededor."""
    import math
    w, h = CANCHA_W * T, CANCHA_H * T
    sp = Sprite(w, h)
    sp.rect(0, 0, w, h, 'side_d')                    # borde de concreto
    sp.rect(2, 2, w - 4, h - 4, 'cancha')
    for y in range(4, h - 4, 6):                     # textura del piso
        for x in range(4 + (y % 12), w - 4, 12):
            sp.px(x, y, 'cancha_d')
            sp.px(x + 5, y + 3, 'cancha_l')
    mitad = np.zeros((h, w), bool)
    mitad[6:8, 6:w - 6] = True                       # línea de fondo
    mitad[6:h // 2, 6:8] = True                      # laterales hasta media cancha
    mitad[6:h // 2, w - 8:w - 6] = True
    cx = w // 2
    mitad[8:30, cx - 16:cx - 14] = True              # área
    mitad[8:30, cx + 14:cx + 16] = True
    mitad[28:30, cx - 16:cx + 16] = True
    for a_ in range(0, 1801):                        # arco de tres puntos (semielipse 50 x 32 px)
        t = math.radians(a_ / 10)
        x, y = cx + math.cos(t) * 50, 8 + math.sin(t) * 32
        mitad[int(y):int(y) + 2, int(x) - 1:int(x) + 1] = True
    mitad |= mitad[:, ::-1]                          # izquierda y derecha iguales (quita el redondeo del arco)
    m = mitad | mitad[::-1]                          # la otra mitad, espejo vertical exacto
    m[h // 2 - 1:h // 2 + 1, 6:w - 6] = True         # media cancha
    centro = np.zeros_like(m)
    for a_ in range(1800, 3601):                     # círculo central, radio 12: media vuelta y su espejo
        t = math.radians(a_ / 10)
        x, y = cx + math.cos(t) * 12, h / 2 + math.sin(t) * 12
        centro[int(math.floor(y)) - 1:int(math.floor(y)) + 1, int(x) - 1:int(x) + 1] = True
    centro |= centro[::-1]
    centro |= centro[:, ::-1]
    arcos = m & ~centro
    arcos[h // 2 - 1:h // 2 + 1, :] = False
    ys, xs = np.nonzero(arcos)
    cy_, cx_ = np.nonzero(centro)
    hueco = min(np.hypot(ys[:, None] - cy_[None, :], xs[:, None] - cx_[None, :]).min(axis=1)[np.abs(ys - h / 2) > 1])
    assert hueco >= 6, ('el arco toca el círculo central', hueco)
    sp.a[m | centro, :3] = P['raya']
    a = sp.a
    a[..., 3] = 255
    return {f'cancha_{i:02d}': a[r * T:(r + 1) * T, c * T:(c + 1) * T, :3].copy()
            for i, (r, c) in enumerate((r, c) for r in range(CANCHA_H) for c in range(CANCHA_W))}


def make_aro_sur():
    """Aro del extremo sur visto desde arriba: red hacia la cancha (arriba), aro, tablero, poste de 8 px y la
    base con su sombra (simétrica alrededor de la base, así el espejo del norte también queda bien)."""
    sp = Sprite(ARO_W, ARO_H)
    cx = ARO_W // 2
    for j in range(7):                               # red: se angosta hacia la punta
        mitad = 3 + j // 2
        for x in range(cx - mitad, cx + mitad):
            if (x + j) % 2 == 0:
                sp.px(x, j, 'raya')
            elif j % 2 == 0:
                sp.px(x, j, 'cream')
    sp.rect(cx - 7, 7, 14, 2, 'roof')                # aro
    sp.rect(cx - 7, 7, 14, 1, 'roof_l')
    sp.rect(0, 9, ARO_W, 14, 'lamp_d')               # tablero
    sp.rect(1, 10, ARO_W - 2, 12, 'raya')
    sp.rect(1, 10, ARO_W - 2, 1, 'cream_l')
    sp.rect(cx - 5, 12, 10, 8, 'pink_d')             # cuadro
    sp.rect(cx - 4, 13, 8, 6, 'raya')
    sp.rect(cx - 2, 23, 4, 7, 'lamp')                # poste corto
    sp.rect(cx - 2, 23, 1, 7, 'lamp_l')
    sp.rect(cx + 1, 23, 1, 7, 'lamp_d')
    pie = ARO_H - PIE_ARO                            # fila donde está la base del poste
    for dy in (-3, 2):                               # sombra arriba y abajo de la base, igual
        for x in range(cx - 6, cx + 6):
            sp.shadow(x, pie + dy)
    sp.rect(cx - 5, pie - 2, 10, 4, 'lamp_d')        # base
    sp.rect(cx - 4, pie - 1, 8, 2, 'lamp')
    return sp.a


def build_props_negocios():
    props = props_comercial()
    sur = make_aro_sur()
    props.update({'aro_basquet_sur': sur, 'aro_basquet_norte': sur[::-1].copy(), 'huerto_comunitario': trim(make_garden()), 'mesa_picnic': trim(make_picnic_table())})
    return props
