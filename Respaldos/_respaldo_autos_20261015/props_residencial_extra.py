"""Props nuevos para el mapa Residencial grande.

Se agregan al final de props_atlas.png sin mover ni redibujar los que ya existen.
Mismo estilo que generate_unity.py: luz desde arriba-izquierda, sin contornos negros,
sombra plana de un solo tono y la paleta P de generate_town.
"""
import random

from generate_town import P, disc, ellipse, shade
from generate_unity import Sprite, shadow_ellipse, make_car_side, make_car_front, make_bush, trim

# tonos claros que faltaban en la paleta para los autos rosa y morado
P.setdefault('pink_l', (0xFF, 0x9C, 0xC4))
P.setdefault('purple_l', (0xA9, 0x83, 0xFF))

COLORES_AUTO = {
    'rosa': ('pink', 'pink_d', 'pink_l'),
    'morado': ('purple', 'purple_d', 'purple_l'),
    'dorado': ('gold', 'gold_d', 'gold_l'),
    'teal': ('roof', 'roof_dd', 'roof_l'),
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
# Paleta de la florería (EMPRESABRENNS.jpg), más sencilla y cálida: pared beige, toldo verde
# apagado, marcos de madera oscura y macetas de barro y de piedra con plantas.
for _k, _v in dict(
        beige='#e3cda3', beige_l='#efdfbd', beige_d='#cdb388', beige_dd='#b0956c',
        salvia='#879f7b', salvia_l='#a2b893', salvia_d='#6b8463', salvia_dd='#536a4f',
        madera_os='#5e3d2c', madera_os_l='#7b543d', madera_os_d='#40291f',
        verde_puerta='#557258', verde_puerta_l='#6f8e70', verde_puerta_d='#3e5643',
        pizarra='#3f5966', pizarra_l='#58737f',
        barro='#c4875e', barro_l='#d9a37a', barro_d='#9f6a49',
        flor_roja='#cf5a4e', flor_roja_d='#a8443d').items():
    P.setdefault(_k, tuple(int(_v[i:i + 2], 16) for i in (1, 3, 5)))

FUENTE = {   # letras de 5x7 para el letrero
    'B': ['11110', '10001', '10001', '11110', '10001', '10001', '11110'],
    'R': ['11110', '10001', '10001', '11110', '10100', '10010', '10001'],
    'E': ['11111', '10000', '10000', '11110', '10000', '10000', '11111'],
    'N': ['10001', '11001', '10101', '10011', '10001', '10001', '10001'],
    'S': ['01111', '10000', '10000', '01110', '00001', '00001', '11110'],
    "'": ['1', '1', '0', '0', '0', '0', '0'],
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
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.rect(x + i * esc + 1, y + j * esc + 1, esc, esc, c_sombra)
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.rect(x + i * esc, y + j * esc, esc, esc, c)
                    sp.px(x + i * esc, y + j * esc, c_brillo)
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


def planta(rng, w, h, flor):
    """Mata con flores para una maceta (sin la sombra del arbusto suelto)."""
    b = make_bush(w, h, rng, pal=('leaf_dd', 'leaf_d', 'leaf', 'leaf_l'), flowers=flor)
    hojas = {P['leaf'], P['leaf_l'], P['leaf_d']}
    centro = 'gold_l' if flor != 'cream_l' else 'olive_l'
    for _ in range(max(3, w * h // 40)):            # flores en racimos de cinco píxeles
        x, y = rng.randrange(2, w - 2), rng.randrange(2, h + 2)
        if b.get(x, y) in hojas and b.get(x, y - 1) in hojas:
            for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
                b.px(x + dx, y + dy, flor)
            b.px(x, y, centro)
    a = b.a
    return a[:a.shape[0] - 3]


def maceta(sp, rng, x, base, w, h, tipo, flor, alto_planta=None):
    """Maceta de barro o de piedra con su planta; x es la orilla izquierda y base la fila del piso."""
    c, cl, cd = (('barro', 'barro_l', 'barro_d') if tipo == 'barro' else
                 ('beige_l', 'cream_l', 'beige_d') if tipo == 'piedra' else ('madera_os_l', 'wood', 'madera_os_d'))
    pl = planta(rng, w + 6, alto_planta or h + 4, flor)
    pegar(sp, pl, x - 3, base - h - pl.shape[0] + 4)
    for k in range(h):                      # cuerpo un poco más angosto abajo
        ins = 1 if k > h * 2 // 3 else 0
        y = base - h + 1 + k
        sp.rect(x + ins, y, w - 2 * ins, 1, c)
        sp.px(x + ins, y, cl)
        sp.px(x + ins + 1, y, cl)
        sp.rect(x + w - ins - 2, y, 2, 1, cd)
    sp.rect(x - 1, base - h + 1, w + 2, 2, cl)  # borde
    sp.rect(x - 1, base - h + 2, w + 2, 1, cd)
    if tipo == 'caja':                      # jardinera de tablas
        for xx in range(x + 4, x + w - 2, 7):
            sp.rect(xx, base - h + 3, 1, h - 3, 'madera_os_d')


def maceta_colgante(sp, rng, cx, y_gancho, largo, flor):
    for y in range(y_gancho, y_gancho + largo):     # cadenitas
        sp.px(cx - 4 + (y - y_gancho) // 6, y, 'madera_os')
        sp.px(cx + 4 - (y - y_gancho) // 6, y, 'madera_os')
    yb = y_gancho + largo
    pl = planta(rng, 18, 10, flor)
    pegar(sp, pl, cx - 9, yb - pl.shape[0] + 5)
    sp.rect(cx - 7, yb, 14, 3, 'beige_l')
    sp.rect(cx - 6, yb + 3, 12, 2, 'beige_d')
    sp.rect(cx - 4, yb + 5, 8, 1, 'beige_dd')
    for k in range(3):                              # hojas que cuelgan
        sp.px(cx - 6 + k * 6, yb + 4 + k, 'leaf')
        sp.px(cx - 6 + k * 6, yb + 5 + k, 'leaf_d')


def marco_madera(sp, x, y, w, h, g=3):
    """Marco de madera oscura de grosor g alrededor del hueco (x, y, w, h)."""
    sp.rect(x - g, y - g, w + 2 * g, g, 'madera_os')
    sp.rect(x - g, y - g, w + 2 * g, 1, 'madera_os_l')
    sp.rect(x - g, y + h, w + 2 * g, g, 'madera_os_d')
    sp.rect(x - g, y, g, h, 'madera_os')
    sp.px(x - g, y, 'madera_os_l')
    sp.rect(x - g, y, 1, h, 'madera_os_l')
    sp.rect(x + w, y, g, h, 'madera_os_d')


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


def make_company(seed=40):
    """Brenn's con la paleta de la florería. Mismo tamaño (320x224) y mismo acomodo que antes:
    letrero BRENN'S arriba al centro, puerta doble al centro, escaparate a la izquierda y cortina
    de la bodega a la derecha (la zona de carga queda enfrente de ella)."""
    rng = random.Random(seed)
    W, H = 320, 224
    sp = Sprite(W, H)
    base = H - 1
    x0, x1 = 4, W - 4
    # muro de aplanado beige con manchas suaves
    sp.rect(x0, 10, x1 - x0, base - 7 - 10, 'beige')
    for _ in range(70):
        cx, cy = rng.randrange(x0, x1), rng.randrange(14, base - 10)
        for (x, y) in ellipse(cx, cy, rng.randint(3, 9), rng.randint(2, 5)):
            sp.px_on(x, y, 'beige_d' if rng.random() < .9 else 'beige_l', {P['beige']})
    for _ in range(260):
        sp.px_on(rng.randrange(x0, x1), rng.randrange(12, base - 8), rng.choice(['beige_l', 'beige_d']), {P['beige']})
    # pilastras de madera oscura en las esquinas y cornisa
    for xs in (x0, x1 - 7):
        sp.rect(xs, 10, 7, base - 17, 'madera_os')
        sp.rect(xs, 10, 2, base - 17, 'madera_os_l')
        sp.rect(xs + 5, 10, 2, base - 17, 'madera_os_d')
    # pretil del techo
    sp.rect(0, 0, W, 10, 'madera_os')
    sp.rect(0, 0, W, 2, 'madera_os_l')
    sp.rect(0, 8, W, 2, 'madera_os_d')
    sp.rect(x0, 10, x1 - x0, 2, 'beige_dd')
    # letrero BRENN'S: tabla crema con marco de madera y letras de pizarra
    esc = 4
    tw = ancho_texto("BRENN'S", esc)
    lx0, ly0, lw, lh = (W - tw) // 2 - 14, 18, tw + 28, 7 * esc + 18
    marco_madera(sp, lx0, ly0, lw, lh, 3)
    sp.rect(lx0, ly0, lw, lh, 'cream_l')
    sp.rect(lx0, ly0, lw, 2, 'beige_l')
    sp.rect(lx0, ly0 + lh - 3, lw, 3, 'cream')
    for _ in range(24):                              # manchitas de la tabla
        sp.px_on(rng.randrange(lx0, lx0 + lw), rng.randrange(ly0 + 2, ly0 + lh - 3), 'cream', {P['cream_l']})
    texto(sp, lx0 + 14, ly0 + 9, "BRENN'S", esc, 'pizarra', 'beige_d', 'pizarra_l')
    for (bx, by) in ((lx0 - 2, ly0 - 2), (lx0 + lw + 1, ly0 - 2), (lx0 - 2, ly0 + lh + 1), (lx0 + lw + 1, ly0 + lh + 1)):
        sp.px(bx, by, 'gold_l')                      # clavos
    # ventanas del piso de arriba con marco de madera y jardinera de flores
    flores = ['pink', 'cream_l', 'flor_roja', 'pink']
    for i, wx in enumerate((20, 84, 196, 260)):
        wy = 74
        marco_madera(sp, wx, wy, 40, 24, 3)
        vidrio(sp, wx, wy, 40, 24, rng)
        sp.rect(wx + 19, wy, 2, 24, 'madera_os')
        sp.rect(wx, wy + 11, 40, 2, 'madera_os')
        sp.rect(wx + 1, wy + 1, 7, 1, 'glass_l')
        # jardinera bajo la ventana
        maceta(sp, rng, wx - 1, wy + 33, 42, 6, 'caja', flores[i], alto_planta=10)
    # placa con el logo (una caja con corazón) entre las ventanas centrales
    marco_madera(sp, 145, 75, 30, 26, 3)
    sp.rect(145, 75, 30, 26, 'cream_l')
    sp.rect(150, 85, 20, 13, 'salvia')
    sp.rect(150, 85, 20, 1, 'salvia_l')
    sp.rect(150, 81, 20, 4, 'beige')
    sp.rect(159, 81, 2, 17, 'salvia_d')
    for (hx, hy) in ((156, 89), (157, 88), (158, 89), (159, 90), (160, 89), (161, 88), (162, 89), (159, 91)):
        sp.px(hx, hy, 'pink')
    # toldo verde apagado en paños, como el de la florería
    ty0, ty1 = 116, 136
    for y in range(ty0, ty1):
        ins = max(0, (ty0 + 4 - y))
        for x in range(x0 - 2 + ins, x1 + 2 - ins):
            k = (x - x0) % 39
            c = 'salvia'
            if y < ty0 + 3:
                c = 'salvia_l'
            elif y > ty1 - 4:
                c = 'salvia_d'
            elif k in (0, 1):
                c = 'salvia_d'                      # costura entre paños
            elif k == 2:
                c = 'salvia_l'
            sp.px(x, y, c)
    for _ in range(40):                              # manchas de sol del toldo
        cx, cy = rng.randrange(x0, x1), rng.randrange(ty0 + 4, ty1 - 4)
        for (x, y) in ellipse(cx, cy, rng.randint(1, 3), 1):
            sp.px_on(x, y, 'salvia_l', {P['salvia']})
    sp.rect(x0 - 2, ty1, x1 - x0 + 4, 2, 'salvia_dd')   # orilla del toldo
    for y in range(ty1 + 2, ty1 + 8):                # sombra del toldo sobre el muro
        for x in range(x0, x1):
            c = sp.get(x, y)
            if c in (P['beige'], P['beige_l'], P['beige_d']):
                sp.px(x, y, 'beige_dd' if c == P['beige_d'] else 'beige_d')
    # celosía de madera bajo el toldo
    sp.rect(x0 + 7, ty1 + 2, x1 - x0 - 14, 6, 'madera_os')
    for x in range(x0 + 9, x1 - 7, 6):
        sp.rect(x, ty1 + 3, 3, 4, 'madera_os_d')
    sp.rect(x0 + 7, ty1 + 2, x1 - x0 - 14, 1, 'madera_os_l')
    # escaparate a la izquierda con macetas colgantes
    ex, ey, ew, eh = 14, 146, 104, 52
    marco_madera(sp, ex, ey, ew, eh, 4)
    vidrio(sp, ex, ey, ew, eh, rng)
    for k in range(34, ew, 35):
        sp.rect(ex + k, ey, 3, eh, 'madera_os')
        sp.px(ex + k, ey, 'madera_os_l')
    for i, cx in enumerate((ex + 17, ex + 52, ex + 87)):
        maceta_colgante(sp, rng, cx, ey, 14 + 4 * (i % 2), ('pink', 'cream_l', 'flor_roja')[i])
    for (bx, by) in ((ex + 8, ey + 38), (ex + 62, ey + 36)):    # cajas de regalo en exhibición
        sp.rect(bx, by, 16, 12, 'salvia')
        sp.rect(bx, by, 16, 3, 'cream_l')
        sp.rect(bx + 7, by, 2, 12, 'pink')
    sp.rect(ex - 5, ey + eh + 4, ew + 10, 3, 'madera_os_l')      # repisa
    sp.rect(ex - 5, ey + eh + 6, ew + 10, 1, 'madera_os_d')
    # puerta doble verde (centrada en el sprite), con marco de madera y letrerito ABIERTO
    dw, dh = 44, 66
    dx, dy = W // 2 - dw // 2, base - 8 - dh
    marco_madera(sp, dx, dy, dw, dh, 5)
    sp.rect(dx - 5, dy - 5, dw + 10, 1, 'beige_l')
    sp.rect(dx, dy, dw, dh, 'verde_puerta')
    sp.rect(dx, dy, 1, dh, 'verde_puerta_l')
    sp.rect(dx + dw // 2 - 1, dy, 2, dh, 'verde_puerta_d')
    for k in (0, dw // 2 + 1):
        vidrio(sp, dx + k + 4, dy + 5, dw // 2 - 9, 30, rng)
        sp.rect(dx + k + 3, dy + 35, dw // 2 - 7, 2, 'verde_puerta_d')
        sp.rect(dx + k + 4, dy + 41, dw // 2 - 9, 16, 'verde_puerta_d')   # tablero de abajo
        sp.rect(dx + k + 5, dy + 42, dw // 2 - 11, 14, 'verde_puerta')
        sp.rect(dx + k + 5, dy + 42, dw // 2 - 11, 1, 'verde_puerta_l')
    sp.rect(dx + dw // 2 - 5, dy + 37, 2, 4, 'gold_l')   # jaladeras
    sp.rect(dx + dw // 2 + 3, dy + 37, 2, 4, 'gold_l')
    ta = ancho_chico('ABIERTO')                           # letrerito colgado en el vidrio
    lx = dx + (dw - ta - 4) // 2
    sp.rect(lx, dy + 14, ta + 4, 9, 'madera_os')
    sp.rect(lx + 1, dy + 15, ta + 2, 7, 'cream_l')
    texto_chico(sp, lx + 2, dy + 16, 'ABIERTO', 'pizarra')
    for k in (4, ta - 1):
        sp.px(lx + k, dy + 12, 'madera_os')
        sp.px(lx + k + (1 if k > 4 else -1), dy + 13, 'madera_os')
    # cortina de la bodega a la derecha (para la zona de carga)
    cx0, cy0, cw, ch = 210, 142, 96, base - 8 - 142
    marco_madera(sp, cx0, cy0, cw, ch, 4)
    for y in range(cy0, cy0 + ch):
        c = 'beige_l' if (y - cy0) % 5 == 0 else 'stone_l' if (y - cy0) % 5 < 4 else 'stone'
        sp.rect(cx0, y, cw, 1, c)
    sp.rect(cx0, cy0, cw, 3, 'madera_os_d')
    sp.rect(cx0 + cw // 2 - 6, cy0 + ch - 6, 12, 2, 'madera_os')
    for x in range(cx0, cx0 + cw, 8):                     # franja de seguridad
        sp.rect(x, cy0 + ch - 3, 4, 3, 'olive')
    sp.rect(cx0 + 18, cy0 + 6, 60, 10, 'madera_os')       # letrero CARGA sobre la cortina
    sp.rect(cx0 + 19, cy0 + 7, 58, 8, 'cream_l')
    texto_chico(sp, cx0 + 19 + (58 - ancho_chico('CARGA')) // 2, cy0 + 8, 'CARGA', 'pizarra')
    # faroles de pared de latón junto a la puerta
    for lx in (dx - 17, dx + dw + 11):
        sp.rect(lx, dy + 6, 6, 2, 'madera_os_d')
        sp.rect(lx, dy + 8, 6, 9, 'gold_d')
        sp.rect(lx + 1, dy + 9, 4, 6, 'gold')
        sp.rect(lx + 1, dy + 9, 2, 3, 'gold_l')
    # cimiento de piedra y escalón
    for y in range(base - 7, base + 1):
        r = y - (base - 7)
        for x in range(x0, x1):
            c = 'stone_l' if r == 0 else 'stone_d' if r == 4 or (x + (r // 4) * 6) % 12 == 0 else 'stone'
            sp.px(x, y, c)
    sp.rect(dx - 8, base - 5, dw + 16, 6, 'side')
    sp.rect(dx - 8, base - 5, dw + 16, 1, 'side_l')
    sp.rect(dx - 8, base, dw + 16, 1, 'side_d')
    # macetas: a los lados de la puerta y bajo el escaparate (ninguna tapa la puerta ni la cortina)
    pie = base - 6
    maceta(sp, rng, dx - 30, pie, 14, 13, 'piedra', 'cream_l', alto_planta=22)
    maceta(sp, rng, dx + dw + 15, pie, 14, 13, 'piedra', 'pink', alto_planta=22)
    maceta(sp, rng, ex - 2, pie, 14, 12, 'barro', 'flor_roja')
    maceta(sp, rng, ex + 22, pie, 22, 8, 'caja', 'pink')
    maceta(sp, rng, ex + 52, pie, 12, 10, 'barro', 'cream_l')
    maceta(sp, rng, ex + 72, pie, 16, 14, 'piedra', 'flor_roja', alto_planta=16)
    maceta(sp, rng, ex + 96, pie, 10, 9, 'barro', 'pink')
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
    return {k: trim(v) for k, v in props.items()}
