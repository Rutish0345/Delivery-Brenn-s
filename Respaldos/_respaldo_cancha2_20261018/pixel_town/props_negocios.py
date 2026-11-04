"""Negocios de barrio y relleno del Residencial (octubre 2026).

- Tiendas, camioneta de reparto, pizarrón, parquímetro y cubetas de flores: se toman tal cual del atlas de la
  zona Comercial (unity/comercial/props_atlas.png + .json, que genera generate_comercial.py). Usan la misma
  paleta P que las casas (techo teal, muros madera/ladrillo/crema/olivo), así que no se retocan.
- Arte nuevo con el estilo del proyecto (luz arriba-izquierda, sin contorno negro, sombra plana):
  aro de básquet, huerto comunitario y mesa de picnic. La cancha es de suelo: 5x3 tiles 'cancha_NN'.
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
CANCHA_W, CANCHA_H = 5, 3          # tiles

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


def make_hoop():
    """Aro de básquet visto de frente: poste gris, tablero blanco con cuadro rojo y red."""
    sp = Sprite(36, 84)
    base = 83
    shadow_ellipse(sp, 19, base, 9, 2)
    sp.rect(15, 30, 5, base - 32, 'lamp')
    sp.rect(15, 30, 1, base - 32, 'lamp_l')
    sp.rect(19, 30, 1, base - 32, 'lamp_d')
    sp.rect(12, base - 3, 11, 3, 'lamp_d')
    sp.rect(12, base - 3, 11, 1, 'lamp')
    sp.rect(2, 2, 32, 22, 'lamp_d')                 # tablero
    sp.rect(3, 3, 30, 20, 'raya')
    sp.rect(3, 3, 30, 1, 'cream_l')
    sp.rect(12, 11, 12, 9, 'pink_d')                 # cuadro
    sp.rect(13, 12, 10, 7, 'raya')
    sp.rect(11, 22, 14, 2, 'roof')                   # aro
    sp.rect(11, 22, 14, 1, 'roof_l')
    for j in range(8):                               # red
        for x in range(12 + j // 3, 25 - j // 3, 2):
            sp.px(x + (j % 2), 24 + j, 'raya' if j % 2 == 0 else 'cream')
    sp.rect(16, 24, 3, 6, 'lamp')                    # brazo
    return sp


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
    """Cancha chica de básquet en 5x3 tiles: piso, borde de concreto y rayas (media cancha, aro arriba)."""
    w, h = CANCHA_W * T, CANCHA_H * T
    sp = Sprite(w, h)
    sp.rect(0, 0, w, h, 'side_d')                    # borde de concreto
    sp.rect(2, 2, w - 4, h - 4, 'cancha')
    for y in range(4, h - 4, 6):                     # textura del piso
        for x in range(4 + (y % 12), w - 4, 12):
            sp.px(x, y, 'cancha_d')
            sp.px(x + 5, y + 3, 'cancha_l')
    # marcas de la mitad de arriba (fondo, zona y arco) y se espejan tal cual hacia abajo
    raya = Sprite(w, h)
    raya.rect(6, 6, w - 12, 2, 'raya')               # línea de fondo
    raya.rect(6, 6, 2, h // 2 - 6, 'raya')           # laterales hasta media cancha
    raya.rect(w - 8, 6, 2, h // 2 - 6, 'raya')
    cx = w // 2
    raya.rect(cx - 14, 8, 2, 20, 'raya')             # zona (área)
    raya.rect(cx + 12, 8, 2, 20, 'raya')
    raya.rect(cx - 14, 26, 28, 2, 'raya')
    import math
    for a_ in range(0, 181, 2):                      # semicírculo
        x = cx + int(round(math.cos(math.radians(a_)) * 44))
        y = 8 + int(round(math.sin(math.radians(a_)) * 30))
        if 8 <= x < w - 8:
            raya.rect(x - 1, y - 1, 2, 2, 'raya')
    m = raya.a[..., 3] > 0
    m = m | m[::-1]                                  # espejo vertical exacto: la otra mitad de la cancha
    m[h // 2 - 1:h // 2 + 1, 6:w - 6] = True         # media cancha
    for a_ in range(0, 360, 6):                      # círculo central
        x = cx + int(round(math.cos(math.radians(a_)) * 10))
        y = h // 2 + int(round(math.sin(math.radians(a_)) * 9))
        m[y - 1:y + 1, x - 1:x + 1] = True
    sp.a[m, :3] = P['raya']
    a = sp.a
    a[..., 3] = 255
    return {f'cancha_{i:02d}': a[r * T:(r + 1) * T, c * T:(c + 1) * T, :3].copy()
            for i, (r, c) in enumerate((r, c) for r in range(CANCHA_H) for c in range(CANCHA_W))}


def make_hoop_sur(aro):
    """El aro del otro extremo: tablero, aro y red espejados verticalmente (la red apunta hacia la cancha,
    el tablero da la espalda) y solo un trozo del poste con su base, que queda afuera de la línea de fondo.
    El poste completo no cabe: cruzaría los 3 tiles de la cancha hasta el otro aro."""
    tablero = aro[0:30][::-1]                        # tablero, aro y red
    poste = aro[30:34]                               # el mismo poste (columnas 13-17)
    base = aro[78:82]                                # el mismo pie y su sombra
    return np.concatenate([tablero, poste, base]).copy()


def build_props_negocios():
    props = props_comercial()
    aro = trim(make_hoop())
    props.update({'aro_basquet': aro, 'aro_basquet_sur': make_hoop_sur(aro), 'huerto_comunitario': trim(make_garden()), 'mesa_picnic': trim(make_picnic_table())})
    return props
