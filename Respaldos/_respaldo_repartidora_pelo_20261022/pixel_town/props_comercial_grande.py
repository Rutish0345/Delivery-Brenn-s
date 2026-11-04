"""Arte nuevo del nivel 2 (Comercial grande, octubre 2026), con la paleta P y el estilo del proyecto
(luz arriba-izquierda, sin contorno negro, sombra plana):
  - los cuatro salones de belleza con su letrero: SALÓN BRENN'S (tabla morada con marco rosa y letras crema,
    como el edificio de la intro) y tres de la competencia (ESTÉTICA LOLA, SALÓN PERLA, ESTÉTICA ROSY), cada
    uno sobre la tienda_salon_belleza del atlas Comercial;
  - dos kioscos para la plaza (café y flores) con toldo de rayas, mostrador y su mercancía;
  - letreros con el nombre de las calles del nivel (AV. COMERCIO, CALLE ROBLE, CALLE CEDRO).
Lo pintado sobre el suelo (cajones del estacionamiento y líneas entre carriles) lo hace generate_comercial_grande.py
sobre los tiles ya armados.
"""
import numpy as np

from generate_town import P
from generate_unity import Sprite, shadow_ellipse, trim
from props_residencial_extra import FUENTE, make_street_sign

# letras de 5x7 que le faltan a FUENTE (las de siempre no se tocan)
LETRAS = dict(FUENTE)
LETRAS.update({
    'T': ['11111', '00100', '00100', '00100', '00100', '00100', '00100'],
    'I': ['111', '010', '010', '010', '010', '010', '111'],
    'C': ['01111', '10000', '10000', '10000', '10000', '10000', '01111'],
    'P': ['11110', '10001', '10001', '11110', '10000', '10000', '10000'],
    'Y': ['10001', '10001', '01010', '00100', '00100', '00100', '00100'],
    'É': ['00010', '00100', '11111', '10000', '10000', '11110', '10000', '10000', '11111'],
})

# letrero_calle_<clave>: "comercio" y no "avenida", que en el nivel 1 es otro sprite de otro ancho (y otro collider)
NOMBRES_CALLE = {'comercio': 'AV. COMERCIO', 'roble': 'CALLE ROBLE', 'cedro': 'CALLE CEDRO'}

# salón -> (texto, tabla, tabla clara, tabla oscura, marco, letras, sombra de las letras)
SALONES = {
    'tienda_salon_brenns': ("SALÓN BRENN'S", 'morado', 'morado_l', 'morado_d', 'pink', 'cream_l', 'morado_dd'),
    'tienda_salon_lola': ('ESTÉTICA LOLA', 'roof_d', 'roof', 'roof_dd', 'cream_l', 'cream_l', 'roof_dd'),
    'tienda_salon_perla': ('SALÓN PERLA', 'lila_d', 'lila', 'purple_d', 'plata', 'white', 'purple_d'),
    'tienda_salon_rosy': ('ESTÉTICA ROSY', 'pink_d', 'pink', 'roof_dd', 'cream_l', 'cream_l', 'pink_d'),
}
# tabla del letrero sobre la fachada del salón (192x174): entre las ventanas y el toldo, encima de la tabla
# chica de los íconos (la tapa completa)
TABLA_Y0, TABLA_Y1 = 77, 92
TABLA_CX = 96


def ancho(s):
    return sum(len(LETRAS[ch][0]) + 1 for ch in s) - 1


def escribir(sp, x, y, s, c, sombra):
    for ch in s:
        g = LETRAS[ch]
        yg = y - (len(g) - 7)                  # las letras con acento suben sus filas de más
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.px(x + i + 1, yg + j + 1, sombra)
        for j, fila in enumerate(g):
            for i, v in enumerate(fila):
                if v == '1':
                    sp.px(x + i, yg + j, c)
        x += len(g[0]) + 1


def salon(base, nombre):
    """El salón del atlas Comercial con su letrero propio."""
    texto, tabla, tabla_l, tabla_d, marco_c, letra, sombra = SALONES[nombre]
    sp = Sprite(base.shape[1], base.shape[0])
    sp.a[:] = base
    tw = ancho(texto)
    w = tw + 14
    x0 = TABLA_CX - w // 2
    sp.rect(x0 - 2, TABLA_Y0 - 2, w + 4, TABLA_Y1 - TABLA_Y0 + 4, marco_c)          # marco
    sp.rect(x0 - 2, TABLA_Y1 + 1, w + 4, 1, tabla_d)                                  # sombra del marco
    sp.rect(x0, TABLA_Y0, w, TABLA_Y1 - TABLA_Y0, tabla)
    sp.rect(x0, TABLA_Y0, w, 1, tabla_l)
    sp.rect(x0, TABLA_Y1 - 1, w, 1, tabla_d)
    for x in (x0 + 2, x0 + w - 3):                                                   # clavos
        sp.px(x, TABLA_Y0 + 2, tabla_l)
        sp.px(x, TABLA_Y1 - 3, tabla_l)
    escribir(sp, x0 + 7, TABLA_Y0 + 5, texto, letra, sombra)
    return sp.a


def kiosco(tipo, seed=3):
    """Puesto de la plaza visto de frente: toldo de rayas, postes, mostrador y la mercancía encima. El dueño
    sale de atrás del mostrador. tipo: 'cafe' o 'flores'."""
    rng = np.random.default_rng(seed)
    W, H = 76, 88
    sp = Sprite(W, H)
    base = H - 1
    shadow_ellipse(sp, W // 2 + 2, base, 34, 4)
    if tipo == 'cafe':
        raya, raya_d, cuerpo, cuerpo_l, cuerpo_d = 'wood_d', 'wood_dd', 'wood', 'wood_l', 'wood_d'
    else:
        raya, raya_d, cuerpo, cuerpo_l, cuerpo_d = 'pink', 'pink_d', 'salvia', 'salvia_l', 'salvia_d'
    # postes
    for x in (7, W - 10):
        sp.rect(x, 22, 3, 40, 'lamp_d')
        sp.rect(x, 22, 1, 40, 'lamp_l')
    # techo y toldo de rayas con orilla de picos
    sp.rect(2, 4, W - 4, 4, 'roof_d')
    sp.rect(2, 4, W - 4, 1, 'roof')
    for x in range(2, W - 2):
        franja = ((x - 2) // 8) % 2 == 0
        c, cd = (raya, raya_d) if franja else ('cream_l', 'cream_d')
        for y in range(8, 22):
            sp.px(x, y, cd if y >= 20 else c)
        if (x - 2) % 8 in (2, 3, 4, 5):
            sp.px(x, 22, cd)
            if (x - 2) % 8 in (3, 4):
                sp.px(x, 23, cd)
    sp.rect(2, 8, W - 4, 1, 'cream_l')
    # letrerito redondo arriba del toldo
    cx = W // 2
    for y in range(-6, 7):
        for x in range(-6, 7):
            if x * x + y * y <= 36:
                sp.px(cx + x, 2 + y + 3, 'cream_l' if x * x + y * y <= 20 else raya_d)
    if tipo == 'cafe':                                          # taza
        sp.rect(cx - 3, 3, 6, 5, 'wood_d')
        sp.rect(cx - 2, 4, 4, 1, 'wood_l')
        sp.px(cx + 3, 5, 'wood_d')
        sp.px(cx + 4, 5, 'wood_d')
    else:                                                       # flor
        for dx, dy in ((0, -2), (-2, 0), (2, 0), (0, 2)):
            sp.px(cx + dx, 5 + dy, 'pink')
        sp.px(cx, 5, 'gold')
    # mostrador
    sp.rect(3, 54, W - 6, 4, 'wood_l')
    sp.rect(3, 54, W - 6, 1, 'cream_l')
    sp.rect(5, 58, W - 10, base - 60, cuerpo)
    sp.rect(5, 58, 2, base - 60, cuerpo_l)
    sp.rect(W - 7, 58, 2, base - 60, cuerpo_d)
    sp.rect(5, base - 3, W - 10, 2, cuerpo_d)
    for x in range(12, W - 12, 12):                             # tablones
        sp.rect(x, 60, 1, base - 64, cuerpo_d)
    # mercancía
    if tipo == 'cafe':
        sp.rect(10, 40, 16, 14, 'stone_d')                       # cafetera
        sp.rect(11, 41, 14, 3, 'stone_l')
        sp.rect(14, 46, 8, 5, 'hole')
        sp.rect(16, 50, 3, 4, 'stone')
        for x in range(32, W - 12, 7):                           # vasos
            sp.rect(x, 47, 5, 7, 'cream_l')
            sp.rect(x, 49, 5, 2, 'wood_d')
            sp.px(x, 47, 'white')
        sp.rect(14, 64, 46, 10, 'cream_l')                       # pizarrita con el menú
        for y in (66, 69, 72):
            sp.rect(18, y, int(rng.integers(20, 36)), 1, 'wood_dd')
    else:
        for x in range(8, W - 10, 10):                           # cubetas con ramos
            sp.rect(x, 46, 8, 8, 'stone_l')
            sp.rect(x, 46, 8, 1, 'white')
            sp.rect(x + 7, 47, 1, 7, 'stone_d')
            col = ['pink', 'gold', 'flor_roja', 'lila', 'white'][(x // 10) % 5]
            for _ in range(9):
                fx, fy = x + int(rng.integers(-1, 9)), int(rng.integers(36, 46))
                sp.px(fx, fy + 1, 'leaf_d')
                sp.px(fx, fy, col)
        for x in range(12, W - 14, 9):                           # macetitas en el frente
            sp.rect(x, 70, 6, 6, 'barro')
            sp.rect(x, 70, 6, 1, 'barro_l')
            sp.rect(x + 1, 66, 4, 4, 'leaf')
            sp.px(x + 2, 66, 'pink_l')
    return trim(sp)


def letreros_calle():
    return {f'letrero_calle_{k}': trim(make_street_sign(v)) for k, v in NOMBRES_CALLE.items()}


def build_props_comercial_grande(salon_base):
    props = {n: salon(salon_base, n) for n in SALONES}
    props['kiosco_cafe'] = kiosco('cafe')
    props['kiosco_flores'] = kiosco('flores', seed=5)
    props.update(letreros_calle())
    return props
