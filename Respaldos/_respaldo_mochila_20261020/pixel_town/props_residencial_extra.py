"""Props nuevos para el mapa Residencial grande.

Se agregan al final de props_atlas.png sin mover ni redibujar los que ya existen.
Mismo estilo que generate_unity.py: luz desde arriba-izquierda, sin contornos negros,
sombra plana de un solo tono y la paleta P de generate_town.
"""
import random

import numpy as np

from generate_town import P, disc, ellipse, shade
from generate_unity import Sprite, shadow_ellipse, make_car_side, make_car_front, trim

# tonos claros que faltaban en la paleta para los autos rosa y morado
P.setdefault('pink_l', (0xFF, 0x9C, 0xC4))
P.setdefault('purple_l', (0xA9, 0x83, 0xFF))

COLORES_AUTO = {
    'rosa': ('pink', 'pink_d', 'pink_l'),
    'morado': ('purple', 'purple_d', 'purple_l'),
    'dorado': ('gold', 'gold_d', 'gold_l'),
    'teal': ('roof', 'roof_dd', 'roof_l'),
    'azul': ('blue', 'blue_d', 'blue_l'),
    'oliva': ('olive', 'olive_d', 'olive_l'),
}


# ---------------------------------------------------------------- cerca baja del frente
def make_low_fence():
    sp = Sprite(32, 26)
    base = 25
    for x in range(32):
        sp.shadow(x, base)
        for y in (base - 16, base - 7):   # travesaños
            sp.px(x, y, 'fence')
            sp.px(x, y + 1, 'fence_d')
    cols = ('fence_l', 'fence', 'fence', 'fence_d')
    for i in range(4):
        x0 = 2 + 8 * i
        for y in range(base - 20, base + 1):
            for k in range(4):
                sp.px(x0 + k, y, cols[k])
        sp.px(x0 + 1, base - 21, 'fence_l')
        sp.px(x0 + 2, base - 21, 'fence')
    rng = random.Random(31)
    for x in range(32):   # pasto en la base
        for k in range(rng.randint(0, 2)):
            sp.px(x, base - k, rng.choice(['grass_d', 'grass', 'leaf_d']))
    return sp


def low_post(sp, x, base, h):
    for y in range(base - h + 3, base + 1):
        for i, c in enumerate(('fence_l', 'fence', 'fence', 'fence', 'fence_d', 'fence_d')):
            sp.px(x + i, y, c)
    sp.rect(x - 1, base - h, 8, 3, 'stone')
    sp.rect(x - 1, base - h, 7, 1, 'stone_l')
    sp.rect(x - 1, base - h + 2, 8, 1, 'stone_d')


def make_low_gate():
    """Dos postes bajos y el hueco del portón (96 px = 3 tiles: postes en las orillas)."""
    sp = Sprite(96, 30)
    base = 29
    for x in list(range(0, 10)) + list(range(86, 96)):
        sp.shadow(x, base)
    low_post(sp, 2, base, 28)
    low_post(sp, 88, base, 28)
    # hojas del portón abiertas hacia adentro (vistas casi de canto)
    for x0, paso in ((8, 1), (87, -1)):
        for i in range(10):
            x = x0 + i * paso
            for y in range(base - 18 + i // 3, base - 4 + i // 4):
                sp.px(x, y, 'fence_d' if i % 3 == 2 else 'fence')
    return sp


def make_low_post():
    sp = Sprite(10, 30)
    sp.shadow(1, 29)
    sp.shadow(8, 29)
    low_post(sp, 2, 29, 28)
    return sp


# ---------------------------------------------------------------- parque
def make_bench():
    sp = Sprite(46, 30)
    base = 29
    shadow_ellipse(sp, 24, base, 21, 2)
    for x0 in (4, 38):   # patas
        sp.rect(x0, base - 9, 4, 10, 'lamp_d')
        sp.rect(x0, base - 9, 1, 10, 'lamp')
    for x0 in (6, 36):   # soportes del respaldo
        sp.rect(x0, 2, 3, 14, 'lamp_d')
    for y0 in (3, 9):    # tablas del respaldo
        sp.rect(1, y0, 44, 4, 'wood')
        sp.rect(1, y0, 44, 1, 'wood_l')
        sp.rect(1, y0 + 3, 44, 1, 'wood_d')
        sp.rect(43, y0, 2, 4, 'wood_d')
    sp.rect(0, 16, 46, 5, 'wood')   # asiento
    sp.rect(0, 16, 46, 2, 'wood_l')
    sp.rect(0, 20, 46, 1, 'wood_dd')
    for x in range(4, 44, 11):
        sp.px(x, 18, 'wood_d')
    return sp


def make_hydrant():
    sp = Sprite(18, 26)
    base = 25
    shadow_ellipse(sp, 10, base, 7, 1)
    sp.rect(3, base - 3, 12, 3, 'wood_d')           # base
    sp.rect(3, base - 3, 12, 1, 'wood')
    sp.rect(4, 8, 10, base - 10, 'wood')            # cuerpo
    sp.rect(4, 8, 3, base - 10, 'wood_l')
    sp.rect(12, 8, 2, base - 10, 'wood_d')
    sp.rect(0, 12, 18, 4, 'wood')                   # bocas laterales
    sp.rect(0, 12, 18, 1, 'wood_l')
    sp.rect(0, 15, 18, 1, 'wood_dd')
    sp.rect(7, 13, 4, 4, 'stone')                   # boca frontal
    sp.px(7, 13, 'stone_l')
    sp.rect(3, 5, 12, 3, 'wood_l')                  # tapa
    sp.rect(5, 2, 8, 3, 'wood')
    sp.rect(8, 0, 2, 2, 'wood_d')
    sp.rect(4, 19, 10, 1, 'wood_dd')
    return sp


def make_trash_can():
    sp = Sprite(18, 24)
    base = 23
    shadow_ellipse(sp, 10, base, 7, 1)
    for y in range(6, base):
        sp.px(2, y, 'lamp_l')
        sp.rect(3, y, 11, 1, 'lamp')
        sp.rect(14, y, 2, 1, 'lamp_d')
    for y in range(9, base - 2, 5):                  # aros
        sp.rect(2, y, 14, 1, 'lamp_d')
    sp.rect(1, 3, 16, 3, 'lamp_l')                   # tapa
    sp.rect(1, 5, 16, 1, 'lamp_d')
    sp.rect(7, 1, 4, 2, 'lamp')
    sp.px(5, 4, 'leaf_l')                            # algo de basura asomándose
    sp.px(6, 3, 'cream_l')
    return sp


def make_pond(seed=17):
    rng = random.Random(seed)
    W, H = 256, 128
    sp = Sprite(W, H)
    cx, cy = W // 2, H // 2
    pts = ellipse(cx, cy, 120, 56) | ellipse(cx - 52, cy + 10, 70, 48) | ellipse(cx + 60, cy - 6, 62, 44)
    pts = {p for p in pts if 0 <= p[0] < W and 0 <= p[1] < H - 1}
    # orilla de tierra con piedritas
    for (x, y) in pts:
        sp.px(x, y, 'gnd_l')
    inner = {(x, y) for (x, y) in pts
             if all((x + dx, y + dy) in pts for dx, dy in ((-4, 0), (4, 0), (0, -4), (0, 4)))}
    for (x, y) in pts - inner:
        if (x, y + 1) not in pts or (x + 1, y) not in pts:
            sp.px(x, y, 'gnd')
    # agua: más oscura arriba (sombra de la orilla) y reflejos claros
    for (x, y) in inner:
        top = (x, y - 3) not in inner or (x, y - 6) not in inner
        sp.px(x, y, 'glass_d' if top else 'glass')
    for _ in range(26):
        x, y = rng.randrange(20, W - 20), rng.randrange(16, H - 16)
        n = rng.randint(4, 9)
        if all((x + k, y) in inner for k in range(n)):
            sp.rect(x, y, n, 1, 'glass_l')
            sp.rect(x + 2, y + 1, max(1, n - 4), 1, 'glass_l')
    # piedras de la orilla
    borde = sorted(pts - inner)
    for _ in range(18):
        x, y = rng.choice(borde)
        shade(sp, ellipse(x, y, rng.randint(2, 4), 2), 'stone_d', 'stone', 'stone_l')
    # nenúfares y una flor rosa
    for (x, y) in ((70, 70), (96, 84), (176, 52), (150, 76), (200, 70)):
        if (x, y) in inner:
            hoja = {p for p in ellipse(x, y, 6, 3) if p != (x + 3, y) and p != (x + 4, y) and p != (x + 5, y)}
            shade(sp, hoja, 'leaf_d', 'leaf', 'leaf_l')
    for (x, y) in ((96, 84), (176, 52)):
        for dx, dy in ((0, -2), (-2, 0), (2, 0), (0, 0), (-1, -1), (1, -1)):
            sp.px(x + dx, y + dy - 1, 'pink' if dy < 0 or dx else 'gold_l')
    return sp


# ---------------------------------------------------------------- zona de carga
def make_box(w=20, h=18, seed=1):
    rng = random.Random(seed)
    sp = Sprite(w, h + 2)
    base = h + 1
    for x in range(w):
        sp.shadow(x + 1, base)
    top = 5
    sp.rect(0, 0, w, top, 'cream')                         # tapa vista desde arriba
    sp.rect(0, 0, w, 1, 'cream_l')
    sp.rect(0, top, w, h - top, 'olive')                   # frente de cartón
    sp.rect(0, top, w, 1, 'olive_l')
    sp.rect(w - 2, top, 2, h - top, 'olive_d')
    sp.rect(w // 2 - 1, 0, 3, top, 'gold_l')               # cinta
    sp.rect(w // 2 - 1, top, 3, h - top, 'olive_l')
    sp.rect(3, top + 4, 5, 3, 'cream_l')                   # etiqueta
    sp.rect(3, top + 5, 5, 1, 'purple')
    for _ in range(4):
        sp.px_on(rng.randrange(w), rng.randrange(top + 1, h), 'olive_d', {P['olive']})
    return sp


def make_box_stack():
    sp = Sprite(44, 44)
    base = 43
    for x in range(44):
        sp.shadow(x, base)
    sp.rect(0, base - 5, 44, 5, 'wood')                    # tarima
    sp.rect(0, base - 5, 44, 1, 'wood_l')
    for x in (2, 20, 38):
        sp.rect(x, base - 3, 4, 3, 'wood_dd')
    for (bx, by, w, h, s) in ((1, 20, 20, 18, 2), (22, 20, 21, 18, 3), (11, 2, 22, 18, 4)):
        b = make_box(w, h, s)
        a = b.a
        for yy in range(h):
            for xx in range(w):
                if a[yy, xx, 3] == 255:
                    sp.px(bx + xx, by + yy, tuple(int(v) for v in a[yy, xx, :3]))
    return sp


# ---------------------------------------------------------------- edificio de la empresa
# Paleta del edificio de antes (la florería de EMPRESABRENNS.jpg): beige, salvia, madera oscura y barro.
# La siguen usando los letreros de calle y algunos tiles; el salón usa la paleta de abajo.
for _k, _v in dict(
        beige='#e3cda3', beige_l='#efdfbd', beige_d='#cdb388', beige_dd='#b0956c',
        salvia='#879f7b', salvia_l='#a2b893', salvia_d='#6b8463', salvia_dd='#536a4f',
        madera_os='#5e3d2c', madera_os_l='#7b543d', madera_os_d='#40291f',
        verde_puerta='#557258', verde_puerta_l='#6f8e70', verde_puerta_d='#3e5643',
        pizarra='#3f5966', pizarra_l='#58737f',
        barro='#c4875e', barro_l='#d9a37a', barro_d='#9f6a49',
        flor_roja='#cf5a4e', flor_roja_d='#a8443d').items():
    P.setdefault(_k, tuple(int(_v[i:i + 2], 16) for i in (1, 3, 5)))

# Paleta del Salón Brenn's: rosa #FF6FA5, morado #8C52FF y crema #FFF1D0 del juego, con sus tonos
for _k, _v in dict(
        morado='#8c52ff', morado_l='#a983ff', morado_d='#6a3bd1', morado_dd='#4a2a8f',
        crema_s='#fff1d0', crema_sd='#efdcb4', rosa_p='#ffd6e6', rosa_pd='#f5b8d0',
        espejo='#cfe8f2', espejo_d='#a9cfe0', plata='#d8d4e4', plata_d='#9c96b0',
        lila='#c9b8dc', lila_l='#ddd0ec', lila_d='#a593bd', white='#ffffff').items():
    P.setdefault(_k, tuple(int(_v[i:i + 2], 16) for i in (1, 3, 5)))

FUENTE = {   # letras de 5x7 para el letrero
    'B': ['11110', '10001', '10001', '11110', '10001', '10001', '11110'],
    'R': ['11110', '10001', '10001', '11110', '10100', '10010', '10001'],
    'E': ['11111', '10000', '10000', '11110', '10000', '10000', '11111'],
    'N': ['10001', '11001', '10101', '10011', '10001', '10001', '10001'],
    'S': ['01111', '10000', '10000', '01110', '00001', '00001', '11110'],
    "'": ['1', '1', '0', '0', '0', '0', '0'],
    'A': ['01110', '10001', '10001', '11111', '10001', '10001', '10001'],
    'L': ['10000', '10000', '10000', '10000', '10000', '10000', '11111'],
    'O': ['01110', '10001', '10001', '10001', '10001', '10001', '01110'],
    # con acento: dos filas más arriba de la línea
    'Ó': ['00010', '00100', '01110', '10001', '10001', '10001', '10001', '10001', '01110'],
    ' ': ['00', '00', '00', '00', '00', '00', '00'],
}

# letras de 5 px de alto para los letreros de calle y los letreritos de la fachada
FUENTE_CHICA = {
    'A': ['010', '101', '111', '101', '101'], 'B': ['110', '101', '110', '101', '110'],
    'C': ['011', '100', '100', '100', '011'], 'D': ['110', '101', '101', '101', '110'],
    'E': ['111', '100', '110', '100', '111'], 'F': ['111', '100', '110', '100', '100'],
    'G': ['011', '100', '101', '101', '011'], 'I': ['111', '010', '010', '010', '111'],
    'L': ['100', '100', '100', '100', '111'], 'M': ['10001', '11011', '10101', '10001', '10001'],
    'N': ['1001', '1101', '1011', '1001', '1001'], 'O': ['010', '101', '101', '101', '010'],
    'P': ['110', '101', '110', '100', '100'], 'R': ['110', '101', '110', '101', '101'],
    'S': ['011', '100', '010', '001', '110'], 'T': ['111', '010', '010', '010', '010'],
    'V': ['101', '101', '101', '101', '010'], '.': ['0', '0', '0', '0', '1'],
    ' ': ['00', '00', '00', '00', '00'],
}


def texto(sp, x, y, s, esc, c, c_sombra, c_brillo='gold_l'):
    for ch in s:
        g = FUENTE[ch]
        yg = y - (len(g) - 7) * esc          # las letras con acento suben sus filas de más
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.rect(x + i * esc + 1, yg + j * esc + 1, esc, esc, c_sombra)
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.rect(x + i * esc, yg + j * esc, esc, esc, c)
                    sp.px(x + i * esc, yg + j * esc, c_brillo)
        x += (len(g[0]) + 1) * esc


def ancho_texto(s, esc):
    return sum((len(FUENTE[ch][0]) + 1) * esc for ch in s) - esc


def texto_chico(sp, x, y, s, c):
    for ch in s:
        g = FUENTE_CHICA[ch]
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.px(x + i, y + j, c)
        x += len(g[0]) + 1


def ancho_chico(s):
    return sum(len(FUENTE_CHICA[ch][0]) + 1 for ch in s) - 1


def pegar(sp, a, x, y, hasta=None):
    """Copia los píxeles opacos de un arreglo RGBA en (x, y); 'hasta' corta las filas de abajo."""
    h = a.shape[0] if hasta is None else hasta
    for yy in range(h):
        for xx in range(a.shape[1]):
            if a[yy, xx, 3] == 255:
                sp.px(x + xx, y + yy, tuple(int(v) for v in a[yy, xx, :3]))


MARCO_MORADO = ('morado', 'morado_l', 'morado_d')


def marco(sp, x, y, w, h, g=3, pal=MARCO_MORADO):
    """Marco de grosor g alrededor del hueco (x, y, w, h), con luz arriba-izquierda."""
    c, cl, cd = pal
    sp.rect(x - g, y - g, w + 2 * g, g, c)
    sp.rect(x - g, y - g, w + 2 * g, 1, cl)
    sp.rect(x - g, y + h, w + 2 * g, g, cd)
    sp.rect(x - g, y, g, h, c)
    sp.rect(x - g, y, 1, h, cl)
    sp.rect(x + w, y, g, h, cd)


def vidrio(sp, x, y, w, h, rng, reflejos=True):
    sp.rect(x, y, w, h, 'glass')
    sp.rect(x, y, w, 3, 'glass_d')
    sp.rect(x, y + h - 2, w, 2, 'glass_d')
    if reflejos:
        for k in range(0, w + h, 22):               # reflejos diagonales
            for i in range(min(8, h)):
                xx, yy = x + k - i, y + 4 + i
                if x <= xx < x + w and yy < y + h - 2:
                    sp.px(xx, yy, 'glass_l')


def linea(sp, x0, y0, x1, y1, c, grosor=1):
    n = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(n + 1):
        x = round(x0 + (x1 - x0) * i / n)
        y = round(y0 + (y1 - y0) * i / n)
        sp.rect(x, y, grosor, grosor, c)


def aro(sp, cx, cy, r, c):
    for (x, y) in disc(cx, cy, r) - disc(cx, cy, r - 1):
        sp.px(x, y, c)


def tijeras(sp, x, y, hoja='plata', borde='morado_dd'):
    """Tijeras abiertas de 15x17 (hojas arriba, aros abajo); (x, y) es la esquina de arriba a la izquierda."""
    linea(sp, x + 2, y, x + 9, y + 10, borde, 2)
    linea(sp, x + 12, y, x + 5, y + 10, borde, 2)
    linea(sp, x + 2, y, x + 8, y + 9, hoja)
    linea(sp, x + 12, y, x + 6, y + 9, hoja)
    sp.px(x + 7, y + 6, 'gold_l')                    # el tornillo
    aro(sp, x + 4, y + 13, 3, borde)
    aro(sp, x + 10, y + 13, 3, borde)


def peine(sp, x, y, w=16, c='gold', cd='gold_d'):
    """Peine de w px: lomo de 3 px y dientes de 5 hacia abajo."""
    sp.rect(x, y, w, 3, c)
    sp.rect(x, y, w, 1, 'gold_l')
    for k in range(0, w, 2):
        sp.rect(x + k, y + 3, 1, 5 if k % 4 == 0 else 4, cd)


def espejo(sp, cx, y0, a, b, fondo):
    """Espejo ovalado con marco dorado: semiejes a x b, arriba en y0."""
    cy = y0 + b
    for (x, y) in ellipse(cx, cy, a + 2, b + 2):
        sp.px(x, y, 'gold_d')
    for (x, y) in ellipse(cx, cy, a + 1, b + 1):
        sp.px(x, y, 'gold')
    for (x, y) in ellipse(cx, cy, a, b):
        sp.px(x, y, 'espejo' if (x - cx) + (y - cy) < a // 2 else 'espejo_d')
    for i in range(b):                               # brillo diagonal
        sp.px_on(cx - a // 2 + i // 2, cy - b + 2 + i, 'white', {P['espejo'], P['espejo_d']})
    sp.px(cx, y0 - 3, 'gold_l')                      # el clavito


def sillon(sp, cx, pie):
    """Sillón de salón (16 px de ancho): respaldo y asiento rosas, base de cromo; pie = fila del piso."""
    sp.rect(cx - 6, pie - 25, 12, 12, 'pink_d')      # respaldo
    sp.rect(cx - 5, pie - 24, 10, 10, 'pink')
    sp.rect(cx - 5, pie - 24, 10, 2, 'pink_l')
    sp.rect(cx - 3, pie - 28, 6, 3, 'pink_d')        # cabecera
    sp.rect(cx - 2, pie - 27, 4, 1, 'pink_l')
    sp.rect(cx - 8, pie - 14, 16, 4, 'pink_d')       # asiento con brazos
    sp.rect(cx - 7, pie - 14, 14, 2, 'pink')
    sp.rect(cx - 8, pie - 16, 3, 3, 'morado_d')
    sp.rect(cx + 5, pie - 16, 3, 3, 'morado_d')
    sp.rect(cx - 1, pie - 10, 3, 7, 'plata')         # pie hidráulico
    sp.px(cx - 1, pie - 10, 'white')
    sp.rect(cx - 6, pie - 3, 13, 3, 'plata_d')
    sp.rect(cx - 6, pie - 3, 13, 1, 'plata')


def repisa_productos(sp, rng, x, y, w):
    """Repisa con frascos de esmalte, tintes y cremas de colores; y es la fila de la tabla."""
    sp.rect(x, y, w, 2, 'morado_l')
    sp.rect(x, y + 2, w, 1, 'morado_d')
    colores = ['pink', 'morado', 'gold', 'cream_l', 'pink_d', 'purple_l', 'flor_roja']
    xx = x + 2
    while xx < x + w - 3:
        c = rng.choice(colores)
        alto = rng.choice((4, 5, 6))
        sp.rect(xx, y - alto, 3, alto, c)
        sp.rect(xx + 1, y - alto - 1, 1, 1, 'morado_dd')   # tapita
        sp.px(xx, y - alto + 1, 'white')
        xx += 4 + rng.randint(0, 1)


def poste_salon(sp, x, y0, y1):
    """Poste giratorio de peluquería (7 px de ancho): capuchones dorados, franjas rosa y blanco en espiral."""
    sp.rect(x + 2, y0 - 4, 3, 1, 'gold_l')           # remate
    sp.rect(x + 1, y0 - 3, 5, 3, 'gold')
    sp.rect(x + 1, y0 - 3, 2, 1, 'gold_l')
    sp.rect(x, y0, 7, 2, 'gold_d')
    for y in range(y0 + 2, y1 - 2):
        for i in range(7):
            k = (i + y) % 8
            c = 'pink' if k < 3 else 'white' if k < 7 else 'pink_l'
            if i == 0 or i == 6:
                c = 'pink_d' if c == 'pink' else 'glass'     # orillas del vidrio
            sp.px(x + i, y, c)
        sp.px(x + 1, y, 'white')                         # brillo del cilindro
    sp.rect(x, y1 - 2, 7, 2, 'gold_d')
    sp.rect(x + 1, y1, 5, 3, 'gold')
    sp.rect(x + 2, y1 + 3, 3, 1, 'gold_d')
    sp.rect(x + 2, y0 + 1, 3, 1, 'gold_l')


def vitrina(sp, rng, x, y, w, h, sillones):
    """Vitrina del salón: pared rosa al fondo, un espejo ovalado por paño, sillón frente a los espejos de
    'sillones' (índices de paño) y repisa de productos en los demás; reflejos del vidrio encima."""
    marco(sp, x, y, w, h, 4)
    sp.rect(x, y, w, h, 'rosa_p')
    sp.rect(x, y + h - 9, w, 9, 'rosa_pd')            # piso a cuadros
    for xx in range(x, x + w, 4):
        for yy in range(y + h - 9, y + h, 3):
            if ((xx - x) // 4 + (yy - y) // 3) % 2:
                sp.rect(xx, yy, 4, 3, 'cream_l')
    panos = max(1, round(w / 35))
    ancho = w / panos
    for i in range(panos):
        cx = int(x + ancho * i + ancho / 2)
        espejo(sp, cx, y + 6, 9, 13, 'rosa_p')
        if i in sillones:
            sillon(sp, cx, y + h - 3)
        else:
            repisa_productos(sp, rng, cx - 12, y + h - 13, 24)
    for k in range(1, panos):                       # parteluces
        xx = int(x + ancho * k)
        sp.rect(xx - 1, y, 3, h, 'morado')
        sp.px(xx - 1, y, 'morado_l')
    for k in range(0, w + h, 26):                   # reflejo del vidrio
        for i in range(min(6, h)):
            xx, yy = x + k - i, y + 3 + i
            if x <= xx < x + w and yy < y + h - 2:
                sp.px(xx, yy, 'white')
    sp.rect(x - 5, y + h + 4, w + 10, 3, 'morado_l')      # repisa de afuera
    sp.rect(x - 5, y + h + 6, w + 10, 1, 'morado_d')


def make_company(seed=40, carga=True):
    """El Salón Brenn's. Mismo tamaño (320x224) y acomodo que el edificio de antes: letrero SALÓN BRENN'S
    arriba al centro, puerta doble al centro (misma posición), vitrina con espejos y sillón a la izquierda,
    postes de peluquería rosa y blanco a los lados de la puerta, cartel de tijeras y peine entre las
    ventanas, y a la derecha la cortina de la bodega con CARGA (la zona de carga del mapa queda enfrente).
    Con carga=False (la intro) la cortina se cambia por otra vitrina con espejo y sillón.
    Tonos rosa, morado y crema."""
    rng = random.Random(seed)
    W, H = 320, 224
    sp = Sprite(W, H)
    base = H - 1
    x0, x1 = 4, W - 4
    # muro crema con papel tapiz de rayas rosa pálido
    sp.rect(x0, 10, x1 - x0, base - 7 - 10, 'crema_s')
    for x in range(x0, x1):
        if (x - x0) % 12 < 4:
            sp.rect(x, 12, 1, base - 7 - 12, 'rosa_p')
    for _ in range(220):
        sp.px_on(rng.randrange(x0, x1), rng.randrange(12, base - 8), 'crema_sd', {P['crema_s'], P['rosa_p']})
    # pilastras moradas en las esquinas y pretil del techo con orilla rosa
    for xs in (x0, x1 - 7):
        sp.rect(xs, 10, 7, base - 17, 'morado')
        sp.rect(xs, 10, 2, base - 17, 'morado_l')
        sp.rect(xs + 5, 10, 2, base - 17, 'morado_d')
    sp.rect(0, 0, W, 10, 'morado_d')
    sp.rect(0, 0, W, 2, 'morado')
    sp.rect(0, 7, W, 1, 'pink')
    sp.rect(0, 8, W, 2, 'morado_dd')
    for x in range(x0, x1, 8):                       # festón bajo el pretil
        sp.rect(x + 1, 10, 6, 1, 'pink_d')
        sp.rect(x + 2, 11, 4, 1, 'pink_d')
    # letrero SALÓN BRENN'S: tabla morada con marco rosa, letras crema
    esc = 3
    titulo = "SALÓN BRENN'S"
    tw = ancho_texto(titulo, esc)
    lx0, ly0, lw, lh = (W - tw) // 2 - 12, 20, tw + 24, 7 * esc + 18
    marco(sp, lx0, ly0, lw, lh, 3, ('pink', 'pink_l', 'pink_d'))
    sp.rect(lx0, ly0, lw, lh, 'morado')
    sp.rect(lx0, ly0, lw, 2, 'morado_l')
    sp.rect(lx0, ly0 + lh - 3, lw, 3, 'morado_d')
    texto(sp, lx0 + 12, ly0 + 10, titulo, esc, 'cream_l', 'morado_dd', 'cream_l')
    for (bx, by) in ((lx0 - 2, ly0 - 2), (lx0 + lw + 1, ly0 - 2), (lx0 - 2, ly0 + lh + 1), (lx0 + lw + 1, ly0 + lh + 1)):
        sp.px(bx, by, 'gold_l')                      # clavos
    for (sx, sy) in ((lx0 + 5, ly0 + 5), (lx0 + lw - 6, ly0 + lh - 7)):   # destellitos en las esquinas
        for dx, dy in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
            sp.px(sx + dx, sy + dy, 'gold_l' if (dx, dy) != (0, 0) else 'white')
    # ventanas del piso de arriba con cortinas rosas recogidas
    for wx in (20, 84, 196, 260):
        wy = 74
        marco(sp, wx, wy, 40, 24, 3)
        vidrio(sp, wx, wy, 40, 24, rng)
        sp.rect(wx + 19, wy, 2, 24, 'morado')
        for lado in (0, 1):                          # cortinas
            for y in range(wy, wy + 24):
                ancho = max(2, 8 - abs(y - (wy + 14)) // 2)
                xs = wx if lado == 0 else wx + 40 - ancho
                sp.rect(xs, y, ancho, 1, 'pink')
                sp.px(xs + (ancho - 1 if lado == 0 else 0), y, 'pink_d')
            sp.px(wx + (3 if lado == 0 else 36), wy + 14, 'gold_l')
        sp.rect(wx - 4, wy + 27, 48, 3, 'cream_l')    # alféizar
        sp.rect(wx - 4, wy + 29, 48, 1, 'crema_sd')
    # cartel de tijeras y peine entre las ventanas centrales
    cx0, cy0, cw, chh = 139, 72, 42, 32
    marco(sp, cx0, cy0, cw, chh, 3, ('pink', 'pink_l', 'pink_d'))
    sp.rect(cx0, cy0, cw, chh, 'cream_l')
    sp.rect(cx0, cy0, cw, 2, 'white')
    tijeras(sp, cx0 + 4, cy0 + 6)
    peine(sp, cx0 + 22, cy0 + 9, 16)
    sp.px(cx0 + 30, cy0 + 24, 'pink')                # corazoncito
    for (hx, hy) in ((29, 23), (31, 23), (28, 24), (32, 24), (29, 25), (31, 25), (30, 26)):
        sp.px(cx0 + hx, cy0 + hy, 'pink')
    # toldo a rayas rosa y crema con orilla de festón
    ty0, ty1 = 116, 134
    for y in range(ty0, ty1):
        ins = max(0, (ty0 + 4 - y))
        for x in range(x0 - 2 + ins, x1 + 2 - ins):
            rosa = ((x - x0) // 10) % 2 == 0
            c = 'pink' if rosa else 'cream_l'
            if y < ty0 + 3:
                c = 'pink_l' if rosa else 'white'
            elif y > ty1 - 4:
                c = 'pink_d' if rosa else 'crema_sd'
            sp.px(x, y, c)
    for x in range(x0 - 2, x1 + 2, 10):              # festón
        rosa = ((x - x0) // 10) % 2 == 0
        for (xx, yy) in ellipse(x + 5, ty1, 4, 3):
            if yy >= ty1:
                sp.px(xx, yy, 'pink_d' if rosa else 'crema_sd')
    for y in range(ty1 + 4, ty1 + 9):                # sombra del toldo sobre el muro
        for x in range(x0 + 7, x1 - 7):
            sp.px_on(x, y, 'crema_sd', {P['crema_s']})
            sp.px_on(x, y, 'rosa_pd', {P['rosa_p']})
    # foquitos de tocador bajo el toldo
    for x in range(x0 + 12, x1 - 10, 12):
        sp.rect(x, ty1 + 6, 3, 3, 'gold')
        sp.px(x, ty1 + 6, 'white')
        sp.px(x + 1, ty1 + 5, 'morado_d')
    # vitrina a la izquierda: espejos, sillón y productos
    ex, ey, ew, eh = 14, 146, 104, 52
    vitrina(sp, rng, ex, ey, ew, eh, sillones={1})
    # puerta doble morada (centrada en el sprite, la de siempre), con letrerito ABIERTO
    dw, dh = 44, 66
    dx, dy = W // 2 - dw // 2, base - 8 - dh
    marco(sp, dx, dy, dw, dh, 5, ('pink_d', 'pink', 'morado_d'))
    sp.rect(dx - 5, dy - 5, dw + 10, 1, 'pink_l')
    sp.rect(dx, dy, dw, dh, 'morado')
    sp.rect(dx, dy, 1, dh, 'morado_l')
    sp.rect(dx + dw // 2 - 1, dy, 2, dh, 'morado_d')
    for k in (0, dw // 2 + 1):
        vidrio(sp, dx + k + 4, dy + 5, dw // 2 - 9, 30, rng)
        sp.rect(dx + k + 3, dy + 35, dw // 2 - 7, 2, 'morado_d')
        sp.rect(dx + k + 4, dy + 41, dw // 2 - 9, 16, 'morado_d')   # tablero de abajo
        sp.rect(dx + k + 5, dy + 42, dw // 2 - 11, 14, 'morado')
        sp.rect(dx + k + 5, dy + 42, dw // 2 - 11, 1, 'morado_l')
    sp.rect(dx + dw // 2 - 5, dy + 37, 2, 4, 'gold_l')   # jaladeras
    sp.rect(dx + dw // 2 + 3, dy + 37, 2, 4, 'gold_l')
    ta = ancho_chico('ABIERTO')                           # letrerito colgado en el vidrio
    lx = dx + (dw - ta - 4) // 2
    sp.rect(lx, dy + 14, ta + 4, 9, 'pink_d')
    sp.rect(lx + 1, dy + 15, ta + 2, 7, 'cream_l')
    texto_chico(sp, lx + 2, dy + 16, 'ABIERTO', 'morado_dd')
    for k in (4, ta - 1):
        sp.px(lx + k, dy + 12, 'gold_d')
        sp.px(lx + k + (1 if k > 4 else -1), dy + 13, 'gold_d')
    # postes de peluquería a los lados de la puerta
    poste_salon(sp, dx - 13, dy + 8, dy + 46)
    poste_salon(sp, dx + dw + 6, dy + 8, dy + 46)
    # a la derecha: cortina de la bodega (zona de carga del mapa) u otra vitrina (intro)
    cx0, cy0, cw, ch = 210, 142, 96, base - 8 - 142
    if carga:
        marco(sp, cx0, cy0, cw, ch, 4)
        for y in range(cy0, cy0 + ch):
            c = 'cream_l' if (y - cy0) % 5 == 0 else 'lila' if (y - cy0) % 5 < 4 else 'lila_d'
            sp.rect(cx0, y, cw, 1, c)
        sp.rect(cx0, cy0, cw, 3, 'morado_d')
        sp.rect(cx0 + cw // 2 - 6, cy0 + ch - 6, 12, 2, 'morado_d')
        for x in range(cx0, cx0 + cw, 8):                 # franja de seguridad
            sp.rect(x, cy0 + ch - 3, 4, 3, 'pink')
        sp.rect(cx0 + 18, cy0 + 6, 60, 10, 'morado_d')    # letrero CARGA sobre la cortina
        sp.rect(cx0 + 19, cy0 + 7, 58, 8, 'cream_l')
        texto_chico(sp, cx0 + 19 + (58 - ancho_chico('CARGA')) // 2, cy0 + 8, 'CARGA', 'morado_dd')
    else:
        vitrina(sp, rng, cx0, cy0 + 4, cw, ch - 16, sillones={0, 2})
    # cimiento de piedra lila y escalón
    for y in range(base - 7, base + 1):
        r = y - (base - 7)
        for x in range(x0, x1):
            c = 'lila_l' if r == 0 else 'lila_d' if r == 4 or (x + (r // 4) * 6) % 12 == 0 else 'lila'
            sp.px(x, y, c)
    sp.rect(dx - 8, base - 5, dw + 16, 6, 'side')
    sp.rect(dx - 8, base - 5, dw + 16, 1, 'side_l')
    sp.rect(dx - 8, base, dw + 16, 1, 'side_d')
    return sp


# ---------------------------------------------------------------- plaza y calles
def make_fountain(seed=8):
    """Fuente redonda de piedra para la plaza (vista desde arriba en 3/4)."""
    rng = random.Random(seed)
    W, H = 96, 76
    sp = Sprite(W, H)
    base = H - 1
    cx = W // 2
    shadow_ellipse(sp, cx + 3, base, 45, 6)
    cy = base - 22                      # centro del pilón
    rx, ry = 44, 18
    out = ellipse(cx, cy, rx, ry)
    inner = ellipse(cx, cy - 2, rx - 6, ry - 5)
    for (x, y) in out:                  # pared exterior del pilón (8 px de alto)
        for k in range(9):
            if (x, y + k) not in out:
                sp.px(x, y + k, 'stone_d' if k > 6 or x > cx + rx - 8 else 'stone')
    shade(sp, out, 'stone_d', 'stone_l', 'cream_l')
    for (x, y) in inner:
        top = (x, y - 3) not in inner
        sp.px(x, y, 'glass_d' if top else 'glass')
    for _ in range(14):
        x, y = rng.randrange(cx - 30, cx + 30), rng.randrange(cy - 10, cy + 8)
        n = rng.randint(3, 6)
        if all((x + k, y) in inner for k in range(n)):
            sp.rect(x, y, n, 1, 'glass_l')
    # columna con plato y chorro
    for y in range(cy - 30, cy + 2):
        for i, c in enumerate(('stone_l', 'stone_l', 'stone', 'stone', 'stone_d', 'stone_d')):
            sp.px(cx - 3 + i, y, c)
    plato = ellipse(cx, cy - 30, 14, 5)
    shade(sp, plato, 'stone_d', 'stone_l', 'cream_l')
    for (x, y) in ellipse(cx, cy - 31, 10, 3):
        sp.px(x, y, 'glass')
    for y in range(cy - 46, cy - 31):   # chorro
        sp.px(cx - 1, y, 'glass_l')
        sp.px(cx, y, 'cream_l' if y % 3 else 'glass_l')
        sp.px(cx + 1, y, 'glass')
    for k in range(1, 12):              # agua que cae del plato
        for s in (-1, 1):
            x = cx + s * (12 + k // 3)
            sp.px(x, cy - 30 + k, 'glass_l' if k % 3 else 'cream_l')
    for (x, y) in ((cx - 3, cy - 47), (cx + 3, cy - 47), (cx - 5, cy - 45), (cx + 5, cy - 45)):
        sp.px(x, y, 'glass_l')
    return sp


def make_street_sign(nombre):
    """Poste con la placa del nombre de la calle (verde con letras crema)."""
    tw = ancho_chico(nombre)
    w = tw + 10
    W = max(w, 16)
    sp = Sprite(W, 58)
    base = 57
    cx = W // 2
    shadow_ellipse(sp, cx + 2, base, 5, 1)
    for y in range(13, base + 1):
        for i, c in enumerate(('lamp_l', 'lamp', 'lamp', 'lamp_d')):
            sp.px(cx - 2 + i, y, c)
    sp.rect(cx - 4, base - 2, 8, 3, 'lamp_d')
    x0 = (W - w) // 2
    sp.rect(x0, 1, w, 12, 'salvia_dd')
    sp.rect(x0 + 1, 2, w - 2, 10, 'roof_d')
    sp.rect(x0 + 1, 2, w - 2, 1, 'roof')
    sp.rect(x0 + 1, 11, w - 2, 1, 'roof_dd')
    texto_chico(sp, x0 + 5, 4, nombre, 'cream_l')
    sp.rect(cx - 1, 0, 2, 1, 'lamp_l')
    return sp


# nombre de cada calle -> texto del letrero
NOMBRES_CALLE = {
    'avenida': 'AV. LAS FLORES',
    'olmo': 'CALLE OLMO',
    'empedrada': 'CALLE EMPEDRADA',
    'pino': 'CALLE PINO',
}


def make_car_back(col, cold, coll):
    """El mismo auto que make_car_front visto por detrás: es el que se usa cuando el auto sube.
    Arriba queda el cofre (lejos), luego parabrisas, techo, medallón, cajuela y las calaveras abajo."""
    sp = Sprite(52, 84)
    base = 83
    shadow_ellipse(sp, 28, base, 24, 3)
    for y in range(4, 80):   # silueta de la carroceria, igual que la vista de frente
        x0 = 4 if 8 < y < 76 else 6
        for x in range(x0, 52 - x0):
            sp.px(x, y, col)
        sp.px(x0, y, coll)
        sp.px(x0 + 1, y, coll)
        sp.px(51 - x0, y, cold)
        sp.px(50 - x0, y, cold)
    sp.rect(10, 6, 32, 5, coll)           # cofre visto desde arriba
    sp.rect(10, 6, 32, 1, 'cream_l')
    sp.rect(10, 11, 32, 4, 'glass_d')     # parabrisas, del otro lado del techo
    sp.rect(10, 15, 32, 16, coll)         # techo
    sp.rect(10, 15, 32, 1, 'cream_l')
    sp.rect(38, 15, 4, 16, col)
    for y in range(31, 45):               # medallon trasero
        ins = max(0, 6 - (y - 31) // 2)
        for x in range(8 + ins, 44 - ins):
            c = 'glass'
            if (x + y) % 15 in (0, 1, 2):
                c = 'glass_l'
            elif y == 31:
                c = 'glass_d'
            sp.px(x, y, c)
    for y in range(45, 64):               # cajuela
        for x in range(6, 46):
            sp.px(x, y, coll if x < 14 or y < 47 else col)
    sp.rect(10, 58, 32, 1, cold)          # orilla de la tapa
    sp.rect(6, 64, 40, 11, cold)          # panel trasero
    for x0 in (7, 37):                    # calaveras
        sp.rect(x0, 66, 8, 5, 'pink')
        sp.rect(x0, 66, 8, 1, 'pink_l')
        sp.rect(x0, 70, 8, 1, 'pink_d')
    sp.rect(19, 67, 14, 6, 'cream_l')     # placa
    sp.rect(21, 69, 10, 1, 'stone_d')
    sp.rect(21, 71, 7, 1, 'stone_d')
    sp.rect(4, 75, 44, 4, 'stone')        # defensa
    sp.rect(4, 75, 44, 1, 'stone_l')
    sp.rect(4, 78, 44, 1, 'stone_d')
    sp.rect(38, 79, 3, 2, 'hole')         # escape
    for wx in (2, 46):                     # llantas
        sp.rect(wx, 64, 4, 18, 'hole_d')
        sp.rect(wx, 64, 4, 2, 'lamp_d')
    sp.rect(0, 12, 4, 4, cold)            # espejos, junto al parabrisas
    sp.rect(48, 12, 4, 4, cold)
    return sp


# Escala común de los autos: todos miden lo mismo a lo largo, sin importar si van de lado, de frente o de
# espaldas, estacionados, en cocheras o circulando. 96 px = 3 tiles (con PPU 32), lo que ya mide el de lado.
LARGO_AUTO_PX = 96


def alargar_auto_vertical(a, largo=LARGO_AUTO_PX):
    """Alarga un auto visto de frente o de espaldas hasta `largo` px de alto repitiendo filas lisas del techo
    y del cofre (tramos de filas idénticas), así el pixel art no se deforma ni se mueven faros ni llantas."""
    falta = largo - a.shape[0]
    assert falta >= 0, ('el auto ya es más largo que la escala común', a.shape)
    iguales = [i for i in range(1, a.shape[0]) if np.array_equal(a[i], a[i - 1])]
    tramos, actual = [], []
    for i in iguales:
        if actual and i != actual[-1] + 1:
            tramos.append(actual)
            actual = []
        actual.append(i)
    tramos.append(actual)
    candidatas = [i for t in tramos if len(t) >= 6 for i in t]
    assert len(candidatas) >= falta, ('no hay filas lisas para alargar el auto', len(candidatas), falta)
    elegidas = {candidatas[round(k * (len(candidatas) - 1) / max(1, falta - 1))] for k in range(falta)}
    assert len(elegidas) == falta
    filas = []
    for i in range(a.shape[0]):
        filas.append(a[i])
        if i in elegidas:
            filas.append(a[i])
    return np.stack(filas)


def build_props_extra():
    props = {
        'cerca_baja': make_low_fence(),
        'cerca_baja_porton': make_low_gate(),
        'cerca_baja_poste': make_low_post(),
        'banca_parque': make_bench(),
        'hidrante': make_hydrant(),
        'bote_basura': make_trash_can(),
        'lago_parque': make_pond(),
        'caja_carga': make_box(),
        'cajas_apiladas': make_box_stack(),
        'empresa_brenns': make_company(),
        'fuente_plaza': make_fountain(),
    }
    for clave, nombre in NOMBRES_CALLE.items():
        props[f'letrero_calle_{clave}'] = make_street_sign(nombre)
    for i, (nombre, (c, cd, cl)) in enumerate(COLORES_AUTO.items()):
        props[f'carro_lateral_{nombre}'] = make_car_side(c, cd, cl, 50 + i)
        props[f'carro_frontal_{nombre}'] = make_car_front(c, cd, cl)
        props[f'carro_trasero_{nombre}'] = make_car_back(c, cd, cl)
    props = {k: trim(v) for k, v in props.items()}
    for k in props:
        if k.startswith(('carro_frontal_', 'carro_trasero_')):
            props[k] = alargar_auto_vertical(props[k])
    for k, a in props.items():   # todos los autos con el mismo largo en tiles
        if k.startswith('carro_'):
            largo = a.shape[1] if k.startswith('carro_lateral_') else a.shape[0]
            assert largo == LARGO_AUTO_PX, ('auto fuera de escala', k, a.shape)
    # juegos, jardinera y puesto de elotes de la manzana del centro; los semáforos van a 32x64 exactos (sin recortar)
    from props_relleno import build_props_relleno, build_semaforos, SEMAFORO_W, SEMAFORO_H
    props.update({k: trim(v) for k, v in build_props_relleno().items()})
    from props_negocios import build_props_negocios     # tiendas del Comercial, huerto, mesa de picnic, aro
    props.update(build_props_negocios())
    for k, a in build_semaforos().items():
        assert a.shape[:2] == (SEMAFORO_H, SEMAFORO_W), (k, a.shape)
        props[k] = a
    from props_estrellas import build_estrellas, LADO   # marcador del punto de entrega: 48x48 sin recortar
    for k, a in build_estrellas().items():
        assert a.shape[:2] == (LADO, LADO), (k, a.shape)
        props[k] = a
    return props
