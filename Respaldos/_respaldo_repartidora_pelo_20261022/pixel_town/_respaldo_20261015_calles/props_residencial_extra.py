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
P.setdefault('noche', (0x2A, 0x1F, 0x40))   # fondo del letrero de la empresa

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
FUENTE = {   # letras de 5x7 para el letrero
    'B': ['11110', '10001', '10001', '11110', '10001', '10001', '11110'],
    'R': ['11110', '10001', '10001', '11110', '10100', '10010', '10001'],
    'E': ['11111', '10000', '10000', '11110', '10000', '10000', '11111'],
    'N': ['10001', '11001', '10101', '10011', '10001', '10001', '10001'],
    'S': ['01111', '10000', '10000', '01110', '00001', '00001', '11110'],
    "'": ['1', '1', '0', '0', '0', '0', '0'],
}


def texto(sp, x, y, s, esc, c, c_sombra):
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
                    sp.px(x + i * esc, y + j * esc, 'gold_l')
        x += (len(g[0]) + 1) * esc


def ancho_texto(s, esc):
    return sum((len(FUENTE[ch][0]) + 1) * esc for ch in s) - esc


def make_company(seed=40):
    rng = random.Random(seed)
    W, H = 320, 224
    sp = Sprite(W, H)
    base = H - 1
    x0, x1 = 4, W - 4
    # muro de ladrillo morado
    for y in range(10, base - 7):
        fila = (y - 10) // 6
        for x in range(x0, x1):
            c = 'purple'
            if (y - 10) % 6 == 5:
                c = 'purple_d'
            elif (x + (fila % 2) * 8) % 16 == 0:
                c = 'purple_d'
            elif (y - 10) % 6 == 0 and rng.random() < .25:
                c = 'purple_l'
            sp.px(x, y, c)
    for y in range(10, base - 7):   # esquinas de la fachada
        sp.rect(x0, y, 5, 1, 'purple_l')
        sp.rect(x1 - 5, y, 5, 1, 'purple_d')
    # pretil del techo
    sp.rect(0, 0, W, 10, 'noche')
    sp.rect(0, 0, W, 2, 'purple_l')
    sp.rect(0, 8, W, 2, 'pink_d')
    # letrero BRENN'S
    esc = 4
    tw = ancho_texto("BRENN'S", esc)
    lx0, ly0, lw, lh = (W - tw) // 2 - 14, 18, tw + 28, 7 * esc + 18
    sp.rect(lx0 - 3, ly0 - 3, lw + 6, lh + 6, 'pink')
    sp.rect(lx0 - 3, ly0 - 3, lw + 6, 1, 'pink_l')
    sp.rect(lx0 - 3, ly0 + lh + 2, lw + 6, 1, 'pink_d')
    sp.rect(lx0, ly0, lw, lh, 'noche')
    texto(sp, lx0 + 14, ly0 + 9, "BRENN'S", esc, 'gold', 'pink_d')
    for (bx, by) in ((lx0 - 3, ly0 - 3), (lx0 + lw + 2, ly0 - 3), (lx0 - 3, ly0 + lh + 2), (lx0 + lw + 2, ly0 + lh + 2)):
        sp.px(bx, by, 'gold_l')   # tornillos
    # ventanas del piso de arriba
    for wx in (20, 84, 196, 260):
        wy = 74
        sp.rect(wx - 2, wy - 2, 44, 30, 'pink_d')
        sp.rect(wx, wy, 40, 26, 'gold_l')
        sp.rect(wx, wy, 40, 3, 'gold')
        sp.rect(wx + 19, wy, 2, 26, 'pink_d')
        sp.rect(wx, wy + 12, 40, 2, 'pink_d')
        sp.rect(wx + 3, wy + 4, 6, 1, 'cream_l')
        sp.rect(wx - 3, wy + 26, 46, 3, 'cream_l')
        sp.rect(wx - 3, wy + 28, 46, 1, 'cream_d')
    # placa con el logo (una caja con corazón) entre las ventanas centrales
    sp.rect(142, 72, 36, 32, 'gold')
    sp.rect(142, 72, 36, 2, 'gold_l')
    sp.rect(144, 74, 32, 28, 'noche')
    sp.rect(150, 84, 20, 14, 'olive')
    sp.rect(150, 80, 20, 4, 'cream')
    sp.rect(159, 80, 2, 18, 'gold_l')
    for (hx, hy) in ((156, 89), (157, 88), (158, 89), (159, 90), (160, 89), (161, 88), (162, 89), (159, 91)):
        sp.px(hx, hy, 'pink')
    # toldo rayado rosa y crema
    ty0, ty1 = 116, 134
    for y in range(ty0, ty1):
        ins = max(0, (ty0 + 4 - y))
        for x in range(x0 - 2 + ins, x1 + 2 - ins):
            raya = ((x - x0) // 12) % 2
            c = ('pink' if raya else 'cream_l')
            if y < ty0 + 3:
                c = 'pink_l' if raya else 'gold_l'
            elif y > ty1 - 4:
                c = 'pink_d' if raya else 'cream_d'
            sp.px(x, y, c)
    for x in range(x0 - 2, x1 + 2):   # orilla festoneada
        if (x - x0) % 12 in range(2, 10):
            sp.px(x, ty1, 'pink_d' if ((x - x0) // 12) % 2 else 'cream_d')
            if (x - x0) % 12 in range(4, 8):
                sp.px(x, ty1 + 1, 'pink_d' if ((x - x0) // 12) % 2 else 'cream_d')
    for y in range(ty1, ty1 + 8):   # sombra del toldo sobre el muro
        for x in range(x0, x1):
            if sp.get(x, y) in (P['purple'], P['purple_l']):
                sp.px(x, y, 'purple_d')
    # escaparate a la izquierda
    ex, ey, ew, eh = 14, 146, 104, 60
    sp.rect(ex - 3, ey - 3, ew + 6, eh + 3, 'noche')
    sp.rect(ex, ey, ew, eh, 'gold_l')
    sp.rect(ex, ey, ew, 6, 'gold')
    for k in range(0, ew, 26):
        sp.rect(ex + k, ey, 2, eh, 'noche')
    for k in range(4):   # reflejos diagonales
        for i in range(10):
            sp.px(ex + 8 + k * 26 + i, ey + 40 - i * 3, 'cream_l')
    for (bx, by) in ((ex + 10, ey + 44), (ex + 36, ey + 40), (ex + 64, ey + 44)):   # cajas en exhibición
        sp.rect(bx, by, 16, 14, 'olive')
        sp.rect(bx, by, 16, 3, 'cream')
        sp.rect(bx + 7, by, 2, 14, 'pink')
    sp.rect(ex - 4, ey + eh, ew + 8, 3, 'cream_l')
    # puerta (centrada en el sprite)
    dw, dh = 44, 66
    dx, dy = W // 2 - dw // 2, base - 8 - dh
    sp.rect(dx - 5, dy - 6, dw + 10, dh + 6, 'gold')
    sp.rect(dx - 5, dy - 6, dw + 10, 2, 'gold_l')
    sp.rect(dx + dw + 3, dy - 4, 2, dh + 4, 'gold_d')
    sp.rect(dx, dy, dw, dh, 'pink')
    sp.rect(dx + dw // 2 - 1, dy, 2, dh, 'pink_d')
    for k in (0, dw // 2 + 1):
        sp.rect(dx + k + 4, dy + 6, dw // 2 - 9, 30, 'glass')
        sp.rect(dx + k + 4, dy + 6, dw // 2 - 9, 2, 'glass_d')
        sp.px(dx + k + 6, dy + 12, 'glass_l')
        sp.px(dx + k + 7, dy + 11, 'glass_l')
        sp.px(dx + k + 8, dy + 10, 'glass_l')
    sp.rect(dx + dw // 2 - 5, dy + 40, 2, 6, 'gold_l')   # jaladeras
    sp.rect(dx + dw // 2 + 3, dy + 40, 2, 6, 'gold_l')
    sp.rect(dx + 6, dy - 16, dw - 12, 8, 'noche')         # letrerito "abierto"
    for i in range(5):
        sp.rect(dx + 10 + i * 6, dy - 14, 4, 4, 'gold' if i % 2 else 'pink_l')
    # cortina de la bodega a la derecha
    cx0, cy0, cw, ch = 210, 142, 96, base - 8 - 142
    sp.rect(cx0 - 4, cy0 - 4, cw + 8, ch + 4, 'noche')
    for y in range(cy0, cy0 + ch):
        c = 'stone_l' if (y - cy0) % 5 == 0 else 'stone' if (y - cy0) % 5 < 4 else 'stone_d'
        sp.rect(cx0, y, cw, 1, c)
    sp.rect(cx0 + cw // 2 - 6, cy0 + ch - 6, 12, 2, 'lamp_d')
    sp.rect(cx0, cy0, cw, 2, 'gold')                       # franja de seguridad
    for x in range(cx0, cx0 + cw, 8):
        sp.rect(x, cy0 + ch - 3, 4, 3, 'gold')
    sp.rect(cx0 + 8, cy0 - 14, 80, 8, 'gold')              # letrero "CARGA"
    sp.rect(cx0 + 8, cy0 - 14, 80, 1, 'gold_l')
    for i in range(5):
        sp.rect(cx0 + 18 + i * 13, cy0 - 12, 8, 4, 'noche')
    # faroles de pared junto a la puerta
    for lx in (dx - 16, dx + dw + 10):
        sp.rect(lx, dy + 6, 6, 2, 'lamp_d')
        sp.rect(lx, dy + 8, 6, 8, 'gold')
        sp.rect(lx + 1, dy + 9, 2, 4, 'gold_l')
    # cimiento y escalón
    for y in range(base - 7, base + 1):
        r = y - (base - 7)
        for x in range(x0, x1):
            c = 'stone_l' if r == 0 else 'stone_d' if r == 4 or (x + (r // 4) * 6) % 12 == 0 else 'stone'
            sp.px(x, y, c)
    sp.rect(dx - 8, base - 5, dw + 16, 6, 'side')
    sp.rect(dx - 8, base - 5, dw + 16, 1, 'side_l')
    sp.rect(dx - 8, base, dw + 16, 1, 'side_d')
    # macetas a los lados de la puerta
    for mx in (dx - 30, dx + dw + 14):
        b = make_bush(18, 12, rng, flowers='pink')
        a = b.a
        hh, ww = a.shape[:2]
        for yy in range(hh - 3):
            for xx in range(ww):
                if a[yy, xx, 3] == 255:
                    sp.px(mx + xx, base - 8 - (hh - 3) + yy, tuple(int(v) for v in a[yy, xx, :3]))
        sp.rect(mx + 2, base - 8, 14, 6, 'wood')
        sp.rect(mx + 2, base - 8, 14, 1, 'wood_l')
    return sp


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
    }
    for i, (nombre, (c, cd, cl)) in enumerate(COLORES_AUTO.items()):
        props[f'carro_lateral_{nombre}'] = make_car_side(c, cd, cl, 50 + i)
        props[f'carro_frontal_{nombre}'] = make_car_front(c, cd, cl)
    return {k: trim(v) for k, v in props.items()}
