"""Mapa Residencial grande (128x64 tiles de 32 px = 4096x2048 px) para Unity.

Usa el mismo tileset_suelo y el mismo props_atlas que generate_unity.py. Los tiles y props nuevos se
agregan al final de cada imagen: los que ya existían no se mueven ni cambian.

Trazado (filas desde arriba, columnas desde la izquierda), inspirado en Ciudadpersonalizada.jpg:
  Av. Las Flores   avenida con camellón y árboles (banqueta 3 + asfalto 3 + camellón 1 + asfalto 3 +
                   banqueta 3). Corre recta en las filas 11-23 frente a Brenn's y en la columna 96
                   da vuelta en curva hacia abajo; sigue por las columnas 99-111 hasta salir del mapa.
  Calle Olmo       calle normal con línea (banqueta 3 + asfalto 5 + banqueta 3) en las filas 39-49;
                   cruza la avenida (el camellón se corta en el cruce).
  Calle Empedrada  calle de adoquín en las columnas 30-38, de la avenida a Calle Olmo.
  Calle Pino       calle angosta sin líneas en las mismas columnas, de Calle Olmo a la orilla de abajo.
  Esquinas redondeadas en todos los cruces, cebras, letreros con el nombre de cada calle.
  Manzanas: Brenn's y 9 casas arriba; plaza con fuente junto a la calle empedrada; bosquecito en la
  manzana chica de abajo a la izquierda; parque grande con lago arriba a la derecha, por fuera de la
  curva. 28 casas en total, cada una con su puerta y su punto de entrega en la banqueta de enfrente.

El suelo se dibuja primero como un raster de materiales a 1 px (pasto, banqueta, asfalto, adoquín,
losa de la plaza). Los bordillos y la sombra del bordillo salen de la distancia a la calle con la misma
regla que los tiles de siempre, así que las rectas reproducen exactamente banqueta_bordillo_s/n/e/w y
asfalto_sombra_bordillo, y las curvas, esquinas redondeadas y puntas del camellón quedan del mismo
estilo. Cada celda de 32x32 que coincide con un tile existente usa ese tile; las demás se vuelven
tiles nuevos (curva_avenida_NN, esquina_NN, camellon_NN, ...) que se agregan al tileset.

Salidas (unity/residencial/):
  tileset_suelo.png / .json           el tileset de siempre con los tiles nuevos agregados al final
  props_atlas.png / props_atlas.json  el atlas de siempre con los props nuevos agregados abajo
  mapa_residencial_grande.json        lo lee Assets/Editor/ConstruirMapa.cs
  mapa_referencia_grande.png          la escena armada completa (vista previa)
  mapa_entregas_grande.png            vista a 1/2 con el spawn y los puntos de entrega marcados
También copia tileset_suelo.png y props_atlas.png al proyecto de Unity.
"""
import json
import math
import os
import random
import shutil

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from generate_town import P, SHADOW
from generate_unity import OUT, T, DINAMICOS, build_tileset, build_props, guardar_mapa, tcanvas
from props_negocios import court_tiles, CANCHA_W, CANCHA_H, PIVOTES
from props_residencial_extra import build_props_extra, NOMBRES_CALLE, COLORES_AUTO as TONOS_AUTO
import props_setos
import cebras

ATLAS_UNITY = r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Escenarios\Residencial\props_atlas.png'
TILESET_UNITY = r'C:\Users\kangu\DeliveryBrenns\Assets\Tiles\tileset_suelo.png'

COLS, ROWS = 128, 64
W_PX, H_PX = COLS * T, ROWS * T
MG = 2                          # tiles de más alrededor del raster: las calles siguen fuera del mapa

# materiales del raster
PASTO, BANQ_M, ASFALTO, ADOQUIN, LOSA = 0, 1, 2, 3, 4
CALLE_M = (ASFALTO, ADOQUIN)
TEXTURA = {PASTO: 'pasto_a', BANQ_M: 'banqueta', ASFALTO: 'asfalto_a', ADOQUIN: 'adoquin_a', LOSA: 'plaza_losa'}
# capa de bordillos: código -> color
B_CURB, B_CURB_L, B_STONE_D, B_SIDE_D, B_HOLE_D, B_ROAD_D = 1, 2, 3, 4, 5, 6
COLOR_BORDE = {B_CURB: 'curb', B_CURB_L: 'curb_l', B_STONE_D: 'stone_d', B_SIDE_D: 'side_d',
               B_HOLE_D: 'hole_d', B_ROAD_D: 'road_d'}

# ---------------------------------------------------------------- trazado (en tiles)
AV_N = 11                       # orilla norte de la avenida en la recta
CURVA = (96, 27)                # centro de la vuelta; radios desde el centro:
AV_BANDAS = [(3, 6, BANQ_M), (6, 9, ASFALTO), (9, 10, PASTO), (10, 13, ASFALTO), (13, 16, BANQ_M)]
AV_X = [CURVA[0] + a for a in (3, 6, 9, 10, 13, 16)]     # columnas de la avenida en el tramo vertical
OLMO = 39                       # banqueta 39-41, asfalto 42-46, banqueta 47-49
V_COL = 30                      # calle vertical: banqueta 30-32, calle 33-35, banqueta 36-38
BANQ_SUR = 61                   # banqueta de la orilla de abajo (filas 61-63)
RADIO_ESQUINA = 2               # radio de las esquinas redondeadas, en tiles

ESQUINAS = [   # (x, y) de la esquina de la manzana y hacia dónde queda la banqueta
    (33, 21, -1, 1), (36, 21, 1, 1),                                      # empedrada con la avenida
    (33, 42, -1, -1), (36, 42, 1, -1), (33, 47, -1, 1), (36, 47, 1, 1),   # Olmo con empedrada / Pino
    (102, 42, -1, -1), (109, 42, 1, -1), (102, 47, -1, 1), (109, 47, 1, 1),   # Olmo con la avenida
    (33, 64, -1, -1), (36, 64, 1, -1), (102, 64, -1, -1), (109, 64, 1, -1),   # orilla de abajo
]
PLAZA = dict(c0=39, c1=46, r0=24, r1=38)
PARQUE = dict(c0=93, r1=26)                  # todo el pasto de ahí a la derecha y arriba, fuera de la curva
BOSQUE = dict(c0=0, c1=29, r0=50, r1=60)     # manzana chica con árboles

CRUCES_V = [   # cebras para cruzar una calle vertical: (filas, columnas)
    (range(21, 24), range(33, 36)), (range(39, 42), range(33, 36)), (range(47, 50), range(33, 36)),
    (range(61, 64), range(33, 36)),
    (range(39, 42), range(102, 109)), (range(47, 50), range(102, 109)), (range(61, 64), range(102, 109)),
]
CRUCES_H = [   # cebras para cruzar una calle horizontal
    (range(43, 47), range(30, 33)), (range(43, 47), range(36, 39)),     # la fila 42 lleva la sombra
    (range(43, 47), range(99, 102)), (range(43, 47), range(109, 112)),  # del bordillo redondeado
    (range(14, 21), range(30, 33)), (range(14, 21), range(36, 39)),
]
ZONAS_CRUCE = [(27 * T, 42 * T), (96 * T, 115 * T)]   # x donde la calle Olmo cruza


def carriles_cruce(c):
    """Carriles que atraviesa una cebra, para su línea de alto: (desde, hasta) en px a lo ancho de la calle,
    sentido (+1 si los autos van hacia x o y creciente) y la orilla del carril que da a la banqueta. Los mismos
    sentidos que las rutas de ConstruirMapa.cs (se circula por la derecha)."""
    if c['eje'] == 'h':
        if min(c['filas']) < AV_N + 13:                                   # avenida: arriba al oeste, abajo al este
            return [((AV_N + 3) * T, (AV_N + 6) * T, -1, (AV_N + 3) * T),
                    ((AV_N + 7) * T, (AV_N + 10) * T, 1, (AV_N + 10) * T)]
        mitad = (OLMO + 5) * T + T // 2                                   # Olmo, sin camellón: línea al centro
        return [((OLMO + 3) * T, mitad - 2, -1, (OLMO + 3) * T), (mitad + 2, (OLMO + 8) * T, 1, (OLMO + 8) * T)]
    if min(c['cols']) >= AV_X[0]:                                         # tramo vertical: baja por la izquierda
        return [(AV_X[1] * T, AV_X[2] * T, 1, AV_X[1] * T), (AV_X[3] * T, AV_X[4] * T, -1, AV_X[4] * T)]
    mitad = (V_COL + 4) * T + T // 2                                      # empedrada / Pino: baja por la izquierda
    return [((V_COL + 3) * T, mitad - 2, 1, (V_COL + 3) * T), (mitad + 2, (V_COL + 6) * T, -1, (V_COL + 6) * T)]

COLORES_CASA = ['casa_madera', 'casa_crema', 'casa_azul', 'casa_oliva']
COLORES_AUTO = list(TONOS_AUTO)            # rosa, morado, dorado, teal, azul, oliva

# Lotes por manzana: fila de la cerca del frente y (columna de la puerta, lado de la rampa).
# La casa ocupa las columnas k-2..k+2 y la rampa las dos columnas pegadas a ese costado.
LOTES = {
    'A': (10, [(17, 'der'), (26, 'der'), (35, 'izq'), (44, 'der'), (53, 'der'), (62, 'izq'),
               (71, 'der'), (80, 'der'), (89, 'izq')]),
    'B_oeste': (38, [(4, 'der'), (14, 'izq'), (24, 'der')]),
    'B_centro': (38, [(51, 'der'), (60, 'izq'), (68, 'der'), (77, 'der'), (86, 'izq'), (94, 'der')]),
    'B_este': (38, [(115, 'der'), (123, 'der')]),
    'C_centro': (60, [(43, 'der'), (52, 'der'), (61, 'izq'), (70, 'der'), (80, 'der'), (90, 'izq')]),
    'C_este': (60, [(115, 'der'), (123, 'der')]),
}
N_CASAS = 28
# Todas las manzanas: el seto de atrás va justo detrás de las casas (lote de 8.8 tiles de la banqueta del frente
# al seto de atrás, en línea recta a la misma altura) y el pasto de atrás (el parque, la zona abierta) queda libre
# y se camina por todo él. Así lo acomodó la usuaria a mano en SampleScene (B_centro) y lo pidió para todas.
LOTES_PEGADOS = set(LOTES)

# Edificio Brenn's: 320x224 px, pegado a la esquina de arriba a la izquierda (no se mueve)
EMPRESA = dict(x_izq=32, fila_base=7, carga=(1, 12))      # la zona de carga ocupa esas columnas


def casa_cols(k):
    return range(k - 2, k + 3)


def rampa_cols(k, lado):
    return (k + 3, k + 4) if lado == 'der' else (k - 4, k - 3)


def lote_cols(k, lado):
    return range(k - 3, k + 5) if lado == 'der' else range(k - 4, k + 4)


# ================================================================ tiles nuevos de textura
def t_adoquin(rng):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'road_d')                      # juntas
    for fila in range(4):
        off = 4 if fila % 2 else 0
        for k in range(4):
            x0, y0 = k * 8 + off, fila * 8
            c = rng.choice(['stone', 'stone', 'side_d', 'stone_l'])
            for yy in range(y0, y0 + 7):
                for xx in range(x0, x0 + 7):
                    esquina = (xx in (x0, x0 + 6)) and (yy in (y0, y0 + 6))
                    if not esquina:
                        cv.px(xx, yy, c)
            for xx in range(x0 + 1, x0 + 6):           # luz arriba, sombra abajo
                cv.px(xx, y0, 'stone_l' if c != 'stone_l' else 'side_l')
                cv.px(xx, y0 + 6, 'stone_d')
            cv.px(x0 + 6, y0 + 5, 'stone_d')
            if rng.random() < .25:
                cv.px(x0 + rng.randint(2, 4), y0 + rng.randint(2, 4), 'stone_d')
    return cv.a


def t_losa(rng):
    cv = tcanvas()
    cv.rect(0, 0, T, T, 'beige_l')
    for k in range(T):
        for j in (0, 16):
            cv.px(k, j, 'beige_d')
            cv.px(j, k, 'beige_d')
            cv.px(k, j + 1, 'cream_l')
            cv.px(j + 1, k, 'cream_l')
    for (x0, y0) in ((2, 2), (18, 18)):                  # losas un poco más oscuras en damero
        for yy in range(y0, y0 + 14):
            for xx in range(x0, x0 + 14):
                cv.px(xx, yy, 'beige')
        for xx in range(x0, x0 + 14):
            cv.px(xx, y0 + 13, 'beige_d')
    for (x, y) in ((9, 9), (25, 25)):                     # rombito de barro al centro
        for dx, dy in ((0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)):
            cv.px(x + dx, y + dy, 'barro_l')
    for _ in range(16):
        cv.px(rng.randrange(T), rng.randrange(T), rng.choice(['beige', 'beige_d']))
    return cv.a


def tiles_textura():
    rng = random.Random(77)
    return [('adoquin_a', t_adoquin(rng)), ('adoquin_b', t_adoquin(rng)), ('plaza_losa', t_losa(rng))]


# ================================================================ raster de materiales
class Lienzo:
    """Materiales a 1 px; (x, y) en tiles, con MG tiles de margen alrededor del mapa."""

    def __init__(self):
        self.m = np.full(((ROWS + 2 * MG) * T, (COLS + 2 * MG) * T), PASTO, np.uint8)

    def _caja(self, x0, y0, x1, y1):
        H, W = self.m.shape
        xa, xb = sorted((int(round((x0 + MG) * T)), int(round((x1 + MG) * T))))
        ya, yb = sorted((int(round((y0 + MG) * T)), int(round((y1 + MG) * T))))
        return max(0, ya), min(H, yb), max(0, xa), min(W, xb)

    def rect(self, x0, y0, x1, y1, v, solo=None):
        ya, yb, xa, xb = self._caja(x0, y0, x1, y1)
        sl = self.m[ya:yb, xa:xb]
        if solo is None:
            sl[:] = v
        else:
            sl[np.isin(sl, solo)] = v

    def _rho(self, ya, yb, xa, xb, cx, cy):
        yy, xx = np.mgrid[ya:yb, xa:xb].astype(np.float32)
        return np.hypot(xx + .5 - (cx + MG) * T, yy + .5 - (cy + MG) * T)

    def anillo(self, cx, cy, r0, r1, v, sx=1, sy=-1):
        """Cuarto de anillo con radios r0..r1 en el cuadrante (sx, sy) del centro."""
        ya, yb, xa, xb = self._caja(cx, cy, cx + sx * r1, cy + sy * r1)
        rho = self._rho(ya, yb, xa, xb, cx, cy)
        sl = self.m[ya:yb, xa:xb]
        sl[(rho >= r0 * T) & (rho < r1 * T)] = v

    def esquina(self, x, y, sx, sy, R, v=ASFALTO):
        """Redondea la esquina (x, y) de una banqueta que queda hacia (sx, sy)."""
        cx, cy = x + sx * R, y + sy * R
        ya, yb, xa, xb = self._caja(x, y, cx, cy)
        rho = self._rho(ya, yb, xa, xb, cx, cy)
        sl = self.m[ya:yb, xa:xb]
        sl[(rho > R * T) & (sl != PASTO)] = v

    def punta(self, col, y, sentido):
        """Punta redonda del camellón (1 tile de ancho) que termina en la fila y; sentido -1 si el
        camellón queda arriba de y, +1 si queda abajo."""
        cx, cy = col + .5, y + sentido * .5
        ya, yb, xa, xb = self._caja(col, y, col + 1, y + sentido)
        rho = self._rho(ya, yb, xa, xb, cx, cy)
        yy = np.mgrid[ya:yb, xa:xb][0] + .5 - (cy + MG) * T
        sl = self.m[ya:yb, xa:xb]
        sl[(rho > T / 2) & (yy * sentido < 0)] = ASFALTO

    def celda(self, a, r, c):
        return a[(r + MG) * T:(r + MG + 1) * T, (c + MG) * T:(c + MG + 1) * T]

    def mat(self, x_px, y_px):
        return int(self.m[int(y_px) + MG * T, int(x_px) + MG * T])


def trazar_raster():
    lz = Lienzo()
    cx, cy = CURVA
    av_y = [AV_N + d for d in (0, 3, 6, 7, 10, 13)]     # 11 14 17 18 21 24
    # banquetas
    lz.rect(-MG, av_y[0], cx, av_y[5], BANQ_M)
    lz.anillo(cx, cy, 3, 16, BANQ_M)
    lz.rect(AV_X[0], cy, AV_X[5], ROWS + MG, BANQ_M)
    lz.rect(-MG, OLMO, COLS + MG, OLMO + 11, BANQ_M)
    lz.rect(V_COL, av_y[5], V_COL + 9, ROWS + MG, BANQ_M)
    lz.rect(-MG, BANQ_SUR, COLS + MG, ROWS, BANQ_M)
    # calles
    lz.rect(-MG, av_y[1], cx, av_y[4], ASFALTO)
    lz.anillo(cx, cy, 6, 13, ASFALTO)
    lz.rect(AV_X[1], cy, AV_X[4], ROWS + MG, ASFALTO)
    lz.rect(-MG, OLMO + 3, COLS + MG, OLMO + 8, ASFALTO)
    lz.rect(V_COL + 3, AV_N + 10, V_COL + 6, av_y[5], ASFALTO)        # cebra sobre la banqueta de la avenida
    lz.rect(V_COL + 3, av_y[5], V_COL + 6, OLMO, ADOQUIN)              # calle empedrada
    lz.rect(V_COL + 3, OLMO, V_COL + 6, ROWS + MG, ASFALTO)            # cruce con Olmo y calle Pino
    lz.rect(-MG, ROWS, COLS + MG, ROWS + MG, ASFALTO)                  # la calle que sigue fuera del mapa
    # camellón de la avenida: se corta en el cruce con Olmo y termina en punta redonda
    lz.rect(-MG, av_y[2], cx, av_y[3], PASTO)
    lz.anillo(cx, cy, 9, 10, PASTO)
    lz.rect(AV_X[2], cy, AV_X[3], OLMO + 3, PASTO)
    lz.rect(AV_X[2], OLMO + 8, AV_X[3], ROWS, PASTO)
    lz.punta(AV_X[2], OLMO + 3, -1)
    lz.punta(AV_X[2], OLMO + 8, 1)
    # esquinas redondeadas
    for x, y, sx, sy in ESQUINAS:
        lz.esquina(x, y, sx, sy, RADIO_ESQUINA)
    # plaza, caminos del parque y del bosquecito (solo sobre pasto)
    p = PLAZA
    lz.rect(p['c0'], p['r0'], p['c1'] + 1, p['r1'] + 1, LOSA)
    for (x0, y0, x1, y1) in CAMINOS_PARQUE + CAMINOS_BOSQUE:
        lz.rect(x0, y0, x1, y1, BANQ_M, solo=[PASTO])
    return lz


CAMINOS_PARQUE = [   # (x0, y0, x1, y1) en tiles
    (100, 3, 102, 14),       # entrada desde la banqueta de la curva
    (100, 3, 126, 5),        # paseo de arriba
    (124, 3, 126, 23),       # paseo de la derecha
    (106, 21, 126, 23),      # entrada desde la banqueta de la avenida
    (110, 5, 114, 7),        # mirador sobre el lago
]
CAMINOS_BOSQUE = [(13, 50, 15, 61)]


def comprobar_curva(lz):
    """La curva debe empatar píxel por píxel con las rectas: misma banda en las dos orillas de cada unión."""
    cx, cy = CURVA
    m = lz.m
    xa = (cx + MG) * T
    for y in range((AV_N + MG) * T, (AV_N + 13 + MG) * T):
        assert m[y, xa - 1] == m[y, xa], ('curva cortada en la recta', y // T - MG)
    ya = (cy + MG) * T
    for x in range((AV_X[0] + MG) * T, (AV_X[5] + MG) * T):
        assert m[ya - 1, x] == m[ya, x], ('curva cortada en la vertical', x // T - MG)
    # cada banda del cuarto de anillo debe medir lo mismo que en la recta
    for (r0, r1, v) in AV_BANDAS:
        for ang in np.linspace(.02, math.pi / 2 - .02, 25):
            for r in (r0 + .05, (r0 + r1) / 2, r1 - .05):
                x = (cx + MG) * T + r * T * math.sin(ang)
                y = (cy + MG) * T - r * T * math.cos(ang)
                assert m[int(y), int(x)] == v, ('banda desalineada en la curva', r0, r1, ang)


# ================================================================ bordillos
def bordillos(m):
    """Capa de bordillos y sombra con la misma regla que los tiles de siempre:
    banqueta arriba de la calle (cara al sur): stone_d, 4 de curb con juntas cada 16 px y curb_l (6 px);
    banqueta abajo de la calle: curb, 2 de curb_l y side_d (4 px); de lado: 2 de curb y 2 de curb_l;
    en la calle, debajo de un bordillo con cara al sur: hole_d y 2 de road_d."""
    calle = np.isin(m, CALLE_M)
    d_fuera = ndimage.distance_transform_edt(~calle)
    d_dentro = ndimage.distance_transform_edt(calle)
    s = ndimage.gaussian_filter(calle.astype(np.float32), 4.0)
    gy, gx = np.gradient(s)
    ny = gy / (np.hypot(gx, gy) + 1e-9)        # > 0: la calle queda abajo
    L = np.ceil(np.where(calle, d_dentro, d_fuera) - 1e-6).astype(np.int16)
    ov = np.zeros(m.shape, np.uint8)
    x = np.arange(m.shape[1])[None, :]
    sur = ~calle & (ny > .7)
    norte = ~calle & (ny < -.7)
    lado = ~calle & ~sur & ~norte
    ov[sur & (L == 1)] = B_STONE_D
    ov[sur & (L >= 2) & (L <= 5)] = B_CURB
    ov[sur & (L >= 2) & (L <= 5) & (x % 16 == 0) & (ny > .97)] = B_STONE_D
    ov[sur & (L == 6)] = B_CURB_L
    ov[norte & (L == 1)] = B_CURB
    ov[norte & (L >= 2) & (L <= 3)] = B_CURB_L
    ov[norte & (L == 4)] = B_SIDE_D
    ov[lado & (L <= 2)] = B_CURB
    ov[lado & (L >= 3) & (L <= 4)] = B_CURB_L
    sombra = calle & (ny > .7)
    ov[sombra & (L == 1)] = B_HOLE_D
    ov[sombra & (L >= 2) & (L <= 3)] = B_ROAD_D
    # orilla fina donde una banqueta curva toca el pasto (en las rectas no hace falta)
    pasto = m == PASTO
    piso = np.isin(m, (BANQ_M, LOSA))
    toca = piso & ndimage.binary_dilation(pasto)
    sg = ndimage.gaussian_filter(pasto.astype(np.float32), 2.0)
    py, px = np.gradient(sg)
    n = np.hypot(px, py) + 1e-9
    diagonal = np.minimum(abs(px), abs(py)) / n > .3
    ov[toca & diagonal & (ov == 0)] = B_SIDE_D
    return ov


def firmas_canonicas(tiles):
    """Firma (material + bordillo) de los tiles rectos de siempre, sacada con la misma regla."""
    N = 7

    def sint(f):
        m = np.full((N * T, N * T), PASTO, np.uint8)
        f(m)
        return m, bordillos(m)

    def celda(a, r, c):
        return a[r * T:(r + 1) * T, c * T:(c + 1) * T]

    def firma(m, ov, r, c):
        return (celda(m, r, c).astype(np.uint16) * 16 + celda(ov, r, c)).tobytes()

    def arriba(a):   # banqueta arriba, calle abajo
        a[:] = BANQ_M
        a[3 * T:] = ASFALTO

    def abajo(a):
        a[:] = ASFALTO
        a[3 * T:] = BANQ_M

    def izq(a):      # banqueta a la izquierda, calle a la derecha
        a[:] = BANQ_M
        a[:, 3 * T:] = ASFALTO

    def der(a):
        a[:] = ASFALTO
        a[:, 3 * T:] = BANQ_M

    casos = [(arriba, [(2, 3, 'banqueta_bordillo_s'), (3, 3, 'asfalto_sombra_bordillo')]),
             (abajo, [(3, 3, 'banqueta_bordillo_n')]),
             (izq, [(3, 2, 'banqueta_bordillo_e')]),
             (der, [(3, 3, 'banqueta_bordillo_w')])]
    firmas = {}
    clase = {P['curb']: B_CURB, P['curb_l']: B_CURB_L, P['stone_d']: B_STONE_D, P['hole_d']: B_HOLE_D}
    for f, celdas in casos:
        m, ov = sint(f)
        for r, c, nombre in celdas:
            firmas[firma(m, ov, r, c)] = nombre
            # la regla debe reproducir el tile de siempre en cada píxel de bordillo
            t = tiles[nombre]
            o = celda(ov, r, c)
            real = np.zeros((T, T), np.uint8)
            for col, k in clase.items():
                real[np.all(t == col, axis=2)] = k
            esperado = np.where(np.isin(o, list(clase.values())), o, 0)
            assert np.array_equal(real, esperado), f'la regla de bordillos no reproduce {nombre}'
    return firmas


# ================================================================ suelo
def trazar_suelo(rng, tiles):
    lz = trazar_raster()
    comprobar_curva(lz)
    ov = bordillos(lz.m)
    firmas = firmas_canonicas(tiles)
    cls = lz.m.astype(np.uint16) * 16 + ov

    grid = {}
    nuevos = {}          # bytes del tile -> nombre
    tiles_nuevos = []    # (nombre, arreglo) en orden de aparición
    cuenta = {}
    for r in range(ROWS):
        for c in range(COLS):
            bm = lz.celda(lz.m, r, c)
            bo = lz.celda(ov, r, c)
            if not bo.any() and (bm == bm[0, 0]).all():
                v = int(bm[0, 0])
                if v == PASTO:
                    x = rng.random()
                    grid[r, c] = 'flores_a' if x < .04 else 'flores_b' if x < .06 else \
                        'pasto_a' if x < .55 else 'pasto_b' if x < .82 else 'pasto_c'
                elif v == BANQ_M:
                    grid[r, c] = 'banqueta'
                elif v == ASFALTO:
                    grid[r, c] = rng.choice(['asfalto_a', 'asfalto_a', 'asfalto_b'])
                elif v == ADOQUIN:
                    grid[r, c] = rng.choice(['adoquin_a', 'adoquin_b'])
                else:
                    grid[r, c] = 'plaza_losa'
                continue
            f = lz.celda(cls, r, c).tobytes()
            if f in firmas:
                grid[r, c] = firmas[f]
                continue
            # tile nuevo: texturas de siempre + bordillos dibujados píxel por píxel
            a = np.zeros((T, T, 3), np.uint8)
            for v, nombre in TEXTURA.items():
                a[bm == v] = tiles[nombre][bm == v]
            for k, col in COLOR_BORDE.items():
                a[bo == k] = P[col]
            for k, f_osc in ((B_HOLE_D, .7), (B_ROAD_D, .84)):   # sombra sobre adoquín: el mismo adoquín más oscuro
                sel = (bo == k) & (bm == ADOQUIN)
                a[sel] = (tiles['adoquin_a'][sel] * f_osc).astype(np.uint8)
            clave = a.tobytes()
            if clave not in nuevos:
                pref = prefijo_tile(r, c, bm)
                cuenta[pref] = cuenta.get(pref, 0) + 1
                nombre = f'{pref}_{cuenta[pref]:02d}'
                nuevos[clave] = nombre
                tiles_nuevos.append((nombre, a))
            grid[r, c] = nuevos[clave]

    # línea de la calle Olmo (solo sobre asfalto liso, lejos de los cruces)
    es_asfalto = lambda rc: grid[rc] in ('asfalto_a', 'asfalto_b')
    for c in range(COLS):
        if es_asfalto((OLMO + 5, c)) and not any(a - 64 <= c * T <= b + 64 for a, b in ZONAS_CRUCE):
            grid[OLMO + 5, c] = 'linea_h'
    # cebras de banqueta a banqueta, isletas en el camellón y líneas de alto (cebras.py): se pintan encima del
    # suelo ya armado, sin usar el azar
    todos = dict(tiles)
    todos.update(dict(tiles_nuevos))
    nuevos_cebra, lz.cebras = cebras.pintar(grid, todos, lz, ov, bordillos, carriles_cruce, dict(
        ASFALTO=ASFALTO, ADOQUIN=ADOQUIN, PASTO=PASTO, BANQ_M=BANQ_M, TEXTURA=TEXTURA, COLOR_BORDE=COLOR_BORDE,
        SOMBRAS={B_HOLE_D: .7, B_ROAD_D: .84}, OSCURO_ADOQUIN=((B_HOLE_D, .7), (B_ROAD_D, .84)), MG=MG,
        lista=cebras.cruces(CRUCES_V, CRUCES_H)))
    tiles_nuevos += nuevos_cebra

    # rampas de concreto: desde la banqueta hasta el costado de cada casa
    for rf, lotes in LOTES.values():
        hb = rf - 3
        for k, lado in lotes:
            ci, cd = rampa_cols(k, lado)
            for r in range(hb - 2, rf + 1):
                assert grid[r, ci].startswith(('pasto', 'flores')) and grid[r, cd].startswith(('pasto', 'flores')), (k, r)
                grid[r, ci] = 'banqueta_bordillo_w'
                grid[r, cd] = 'banqueta_bordillo_e'
            grid[hb - 2, ci] = 'banqueta_esquina_nw'
            grid[hb - 2, cd] = 'banqueta_esquina_ne'

    # zona de carga del edificio
    c0, c1 = EMPRESA['carga']
    for r in range(EMPRESA['fila_base'] + 1, AV_N):
        for c in range(c0, c1 + 1):
            grid[r, c] = 'banqueta_bordillo_w' if c == c0 else 'banqueta_bordillo_e' if c == c1 else 'banqueta'

    # flores sueltas en el parque y el bosquecito
    for r in range(ROWS):
        for c in range(COLS):
            if (en_parque(lz, r, c) or en_bosque(r, c)) and grid[r, c].startswith('pasto') and rng.random() < .14:
                grid[r, c] = rng.choice(['flores_a', 'flores_b'])

    # grietas fijas y baches (los baches son dinámicos: debajo queda asfalto)
    bajo_bache = {}
    for (r, c, n) in ((16, 12, 'bache_grande'), (19, 70, 'bache_chico'), (44, 20, 'bache_chico'),
                      (45, 120, 'bache_grande'), (35, 107, 'bache_chico'), (56, 34, 'bache_chico'),
                      (15, 52, 'asfalto_grieta'), (43, 66, 'asfalto_grieta'), (55, 103, 'asfalto_grieta'),
                      (19, 45, 'asfalto_grieta'), (58, 107, 'asfalto_grieta')):
        assert grid[r, c] in ('asfalto_a', 'asfalto_b', 'linea_h'), (r, c, grid[r, c])
        if n.startswith('bache'):
            bajo_bache[r, c] = grid[r, c]
        grid[r, c] = n
    return grid, bajo_bache, lz, ov, tiles_nuevos


def prefijo_tile(r, c, bm):
    cx, cy = CURVA
    if (bm == ADOQUIN).any():
        return 'empedrado'
    if (bm == LOSA).any():
        return 'plaza'
    if cx <= c < cx + 16 and AV_N <= r < cy:
        return 'curva_avenida'
    if c == AV_X[2] or r == AV_N + 6:
        return 'camellon'
    if any(x - 3 <= c < x + 3 and y - 3 <= r < y + 3 for x, y, _, _ in ESQUINAS):
        return 'esquina'
    return 'orilla'


def en_parque(lz, r, c):
    return c >= PARQUE['c0'] and r <= PARQUE['r1'] and not (r > 10 and c < CURVA[0]) and \
        (lz.celda(lz.m, r, c) == PASTO).all()


def en_bosque(r, c):
    b = BOSQUE
    return b['c0'] <= c <= b['c1'] and b['r0'] <= r <= b['r1']


# ================================================================ props
class Escena:
    def __init__(self, rng, grid):
        self.rng, self.grid = rng, grid
        self.objs = []           # (base_y_px, nombre, centro_x_px, voltear)
        self.ocupado = set()     # celdas (r, c) donde no se puede poner vegetación

    def put(self, name, cx, base, flip=False):
        self.objs.append((int(base), name, int(cx), flip))

    def reservar(self, rs, cs):
        for r in rs:
            for c in cs:
                self.ocupado.add((r, c))

    def libre(self, rs, cs):
        return all(0 <= r < ROWS and 0 <= c < COLS and (r, c) not in self.ocupado
                   and self.grid[r, c].startswith(('pasto', 'flores')) for r in rs for c in cs)


def colocar_casas(es):
    rng = es.rng
    casas = []
    k_color = 0
    for manzana, (rf, lotes) in LOTES.items():
        hb = rf - 3
        house_base = (hb + 1) * T - 1
        fence_base = rf * T + 30
        acera = rf + 1                      # primera fila de la banqueta de enfrente
        for k, lado in lotes:
            d = k * T + T // 2
            nombre = COLORES_CASA[k_color % 4]
            k_color += 1 if rng.random() < .8 else 2
            es.put(nombre, d, house_base)
            es.reservar(range(hb - 5, hb + 1), casa_cols(k))
            # camino de piedras de la puerta al portón y hasta la orilla de la banqueta
            y = house_base + 3
            while y < fence_base + 2:
                es.put('piedra_camino', d + rng.randint(-2, 2), y + 8)
                y += 11
            es.reservar(range(hb + 1, rf + 1), (k,))
            # cerca baja solo al frente: portón en la puerta y hueco en la rampa
            ci, cd = rampa_cols(k, lado)
            cerca = [c for c in range(k - 3, k + 4) if c not in (ci, cd) and not k - 1 <= c <= k + 1]
            for c in cerca:
                es.put('cerca_baja', c * T + 16, fence_base)
            es.put('cerca_baja_porton', d, fence_base)
            for c in (min(cerca), max(cerca)):
                lado_x = c * T + (3 if c == min(cerca) else T - 4)
                es.put('cerca_baja_poste', lado_x, fence_base + 1)
            es.reservar((rf,), range(k - 3, k + 4))
            es.reservar(range(hb - 2, rf + 1), (ci, cd))
            # buzón dentro del patio, a un lado del camino
            bx = d + (56 if lado == 'izq' else -56)
            es.put('buzon_teal' if len(casas) % 2 else 'buzon_terracota', bx, fence_base - 22)
            es.reservar((rf - 1,), (bx // T,))
            rng.random()   # antes decidía el auto de la cochera; se conserva para no mover el resto del mapa
            casas.append(dict(nombre=nombre, k=k, d=d, puerta_base=house_base - 8, acera=acera,
                              rampa=[ci, cd], auto=None, manzana=manzana, house_base=house_base))
    return casas


def colocar_empresa(es):
    x_izq, fb = EMPRESA['x_izq'], EMPRESA['fila_base']
    base = (fb + 1) * T - 1
    centro = x_izq + 160
    es.put('empresa_brenns', centro, base)
    es.reservar(range(0, AV_N), range(0, EMPRESA['carga'][1] + 2))
    # zona de carga: cajas frente a la cortina de la bodega, sin tapar la puerta
    es.put('cajas_apiladas', x_izq + 236, base + 46)
    es.put('cajas_apiladas', x_izq + 296, base + 70)
    es.put('caja_carga', x_izq + 262, base + 80)
    es.put('caja_carga', x_izq + 212, base + 92)
    es.put('caja_carga', x_izq + 40, base + 40)
    es.put('cajas_apiladas', x_izq + 70, base + 64)
    return dict(nombre="Brenn's", centro_px=centro, puerta_base=base - 8)


class Repartidor:
    """Pone props sueltos sin que se encimen (círculos en px) y fuera de las celdas prohibidas."""

    def __init__(self, es):
        self.es, self.puestos = es, []

    def choca(self, x, base, radio):
        return any((x - px) ** 2 + (base - pb) ** 2 < (radio + pr) ** 2 for px, pb, pr in self.puestos)

    def poner(self, name, x, base, radio, flip=False):
        self.es.put(name, x, base, flip)
        self.puestos.append((x, base, radio))


def colocar_parque(es, lz):
    """Parque grande por fuera de la curva: lago al centro del circuito de paseos, bancas, faroles,
    árboles y arbustos repartidos sin formar filas, y un seto donde colinda con los patios."""
    rng = es.rng
    celdas = {(r, c) for r in range(ROWS) for c in range(COLS) if en_parque(lz, r, c)}
    es.reservar(range(0, PARQUE['r1'] + 1), range(PARQUE['c0'], COLS))
    rp = Repartidor(es)

    def dentro(x, base, m):
        return all((r, c) in celdas for r in range((base - m) // T, (base + 4) // T + 1)
                   for c in range((x - m) // T, (x + m) // T + 1))

    lago_x, lago_base = 115 * T + 8, 15 * T + 4
    rp.poner('lago_parque', lago_x, lago_base, 0)
    lago = (lago_x - 124, lago_base - 115, lago_x + 124, lago_base)
    # bancas mirando a los paseos y al lago
    for x, base in ((108 * T + 8, 2 * T + 30), (118 * T + 4, 2 * T + 30), (123 * T + 8, 10 * T + 4),
                    (123 * T + 8, 17 * T + 4), (116 * T + 20, 20 * T + 30), (112 * T + 4, 8 * T + 4)):
        rp.poner('banca_parque', x + rng.randint(-4, 4), base, 26)
    rp.poner('bote_basura', 104 * T + 10, 2 * T + 28, 12)
    rp.poner('bote_basura', 123 * T + 2, 14 * T + 20, 12)
    for x, base in ((102 * T + 12, 6 * T + 26), (102 * T + 12, 2 * T + 30), (126 * T + 10, 2 * T + 30),
                    (126 * T + 10, 20 * T + 30), (114 * T + 30, 7 * T + 26)):
        rp.poner('farol', x, base, 14)
    # flores en la orilla del lago
    cxl, cyl = lago_x, lago_base - 52
    ang = rng.uniform(0, 2 * math.pi)
    for _ in range(9):
        ang += rng.uniform(.5, 1.1)
        x = int(cxl + math.cos(ang) * 138)
        base = int(cyl + math.sin(ang) * 66) + 10
        if dentro(x, base, 12) and not rp.choca(x, base, 20):
            rp.poner(rng.choice(['arbusto_flores', 'arbusto_flores_crema', 'arbusto_chico']), x, base, 20)
    # árboles: puntos al azar con distancia mínima, lejos del lago y de los paseos
    arboles = 0
    for _ in range(3000):
        if arboles >= 22:
            break
        x = rng.randint(PARQUE['c0'] * T + 20, COLS * T - 20)
        base = rng.randint(T + 8, (PARQUE['r1'] + 1) * T - 4)
        if lago[0] - 30 < x < lago[2] + 30 and lago[1] - 10 < base < lago[3] + 44:
            continue
        if not dentro(x, base, 26) or rp.choca(x, base, 44):
            continue
        rp.poner(rng.choice(['arbol_grande', 'arbol_grande_b', 'arbol_mediano', 'arbol_mediano', 'pino']), x, base, 44)
        arboles += 1
    for _ in range(1500):
        x = rng.randint(PARQUE['c0'] * T + 16, COLS * T - 16)
        base = rng.randint(T, (PARQUE['r1'] + 1) * T - 4)
        if lago[0] - 10 < x < lago[2] + 10 and lago[1] < base < lago[3] + 18:
            continue
        if dentro(x, base, 18) and not rp.choca(x, base, 32):
            rp.poner(rng.choice(['arbusto_chico', 'arbusto_flores', 'arbusto_grande', 'arbusto_flores_crema']), x, base, 32)
    # seto donde el parque colinda con los patios de las casas de abajo
    for x in range(113 * T, COLS * T - 16, 66):
        es.put('seto', x + 32, (PARQUE['r1'] + 1) * T - 2)


def colocar_plaza(es):
    """Plaza de losas junto a la calle empedrada: fuente al centro, cuatro árboles, bancas y faroles."""
    p = PLAZA
    es.reservar(range(p['r0'], p['r1'] + 1), range(p['c0'], p['c1'] + 2))
    cx = (p['c0'] + p['c1'] + 1) * T // 2
    cy = (p['r0'] + p['r1'] + 1) * T // 2
    es.put('fuente_plaza', cx, cy + 34)
    for dx, dy in ((-80, -132), (80, -132), (-80, 190), (80, 190)):
        es.put('arbol_mediano', cx + dx, cy + dy)
    es.put('banca_parque', cx, cy - 66)
    es.put('banca_parque', cx, cy + 116)
    es.put('arbusto_flores', cx - 92, cy + 40)
    es.put('arbusto_flores_crema', cx + 92, cy + 40)
    es.put('farol', p['c0'] * T + 14, (p['r0'] + 1) * T + 4)
    es.put('farol', (p['c1'] + 1) * T - 14, (p['r1']) * T + 26)
    es.put('bote_basura', (p['c1'] + 1) * T - 18, (p['r0'] + 1) * T + 24)
    # arbustos altos que separan la plaza del primer patio
    for r in range(p['r0'] + 1, p['r1'] - 4, 2):
        es.put('arbusto_grande', (p['c1'] + 1) * T + 18, r * T + 30)


def colocar_bosque(es):
    """Manzana chica de abajo a la izquierda: un bosquecito con un sendero y dos bancas."""
    rng = es.rng
    b = BOSQUE
    es.reservar(range(b['r0'], b['r1'] + 1), range(b['c0'], b['c1'] + 1))
    rp = Repartidor(es)
    sendero = 14 * T
    rp.poner('banca_parque', sendero - 58, 54 * T + 20, 24)
    rp.poner('banca_parque', sendero + 58, 57 * T + 26, 24)
    rp.poner('farol', sendero + 46, 52 * T + 12, 14)
    arboles = 0
    for _ in range(4000):
        if arboles >= 17:
            break
        x = rng.randint(b['c0'] * T + 18, (b['c1'] + 1) * T - 18)
        base = rng.randint((b['r0'] + 2) * T, (b['r1'] + 1) * T - 4)
        if abs(x - sendero) < 56 or rp.choca(x, base, 40):
            continue
        rp.poner(rng.choice(['arbol_grande', 'arbol_grande_b', 'arbol_mediano', 'pino', 'pino']), x, base, 40)
        arboles += 1
    for _ in range(800):
        x = rng.randint(b['c0'] * T + 14, (b['c1'] + 1) * T - 14)
        base = rng.randint((b['r0'] + 1) * T, (b['r1'] + 1) * T - 4)
        if abs(x - sendero) < 46 or rp.choca(x, base, 30):
            continue
        rp.poner(rng.choice(['arbusto_chico', 'arbusto_flores', 'arbusto_grande', 'arbusto_flores_crema']), x, base, 30)


def colocar_camellon(es):
    """Árboles a lo largo del camellón de la avenida (recta, curva y tramo vertical)."""
    rng = es.rng
    fila = AV_N + 6
    base_h = fila * T + 26
    x = 3 * T
    while x < CURVA[0] * T - 40:
        if not 28 * T < x < 41 * T:                     # libre el cruce de peatones
            es.put(rng.choice(['arbol_mediano', 'arbol_mediano', 'arbol_grande_b']), x + rng.randint(-5, 5), base_h)
        else:
            es.put('arbusto_flores', x, base_h - 2)
        x += 5 * T + rng.randint(-24, 24)
    cx, cy = CURVA
    for ang in (14, 42, 70):
        a = math.radians(ang)
        es.put('arbol_mediano', cx * T + 9.5 * T * math.sin(a), cy * T - 9.5 * T * math.cos(a) + 12)
    col = AV_X[2]
    for r in list(range(cy + 2, OLMO - 2, 5)) + list(range(OLMO + 13, ROWS - 4, 5)):
        es.put(rng.choice(['arbol_mediano', 'arbol_grande']), col * T + 16 + rng.randint(-2, 2), r * T + 26)
    es.reservar(range(ROWS), (col,))
    es.reservar((fila,), range(COLS))


def colocar_vegetacion(es, intentos=7000):
    """Árboles, setos y arbustos en los costados, fondos de patio y áreas verdes libres."""
    rng = es.rng
    for _ in range(intentos):
        r, c = rng.randrange(ROWS), rng.randrange(COLS)
        if rng.random() < .45:
            if es.libre(range(r - 2, r + 1), range(c - 1, c + 2)):
                tipo = rng.choice(['arbol_grande', 'arbol_grande_b', 'arbol_mediano', 'arbol_mediano', 'pino'])
                es.put(tipo, c * T + 16 + rng.randint(-6, 6), r * T + 28 - rng.randint(0, 6))
                es.reservar(range(r - 3, r + 2), range(c - 2, c + 3))
        else:
            tipo = rng.choice(['arbusto_grande', 'arbusto_chico', 'arbusto_flores', 'arbusto_flores_crema', 'seto'])
            ancho = 2 if tipo in ('seto', 'arbusto_grande', 'arbusto_flores') else 1
            if es.libre(range(r - 1, r + 1), range(c - 1, c + ancho + 1)):
                es.put(tipo, c * T + 16 * ancho, r * T + 28 - rng.randint(0, 4))
                es.reservar(range(r - 1, r + 2), range(c - 1, c + ancho + 1))


# Relleno de la franja de pasto de la manzana del centro (entre la banqueta de la avenida y los patios), en
# coordenadas de Unity (x, y del pivote abajo-centro). Va al final para no cambiar nada de lo que ya se
# colocaba: quita los arbustos sueltos de esa franja (se cambian por grupos) y los árboles que estorben.
RELLENO_FRANJA = dict(y0=34.2, y1=40.0, xs=((0, 29.5), (48, 98), (111, 128)))
RELLENO_CENTRO = [
    ('puesto_elotes', 49.9, 38.5),                                           # esquina de la plaza
    ('columpios', 54.54, 36.31), ('resbaladilla', 60.2, 35.6),                # juegos infantiles (columpios: donde los dejó la usuaria)
    ('banca_parque', 52.8, 38.4), ('banca_parque', 63.0, 38.4), ('banca_parque', 79.5, 38.4),
    ('banca_parque', 94.5, 38.4), ('banca_parque', 11.5, 38.4), ('banca_parque', 23.0, 38.4),
    ('banca_parque', 119.5, 38.4),
    ('bote_basura', 57.6, 38.5), ('bote_basura', 81.8, 38.5), ('bote_basura', 13.8, 38.5), ('bote_basura', 121.7, 38.5),
    ('jardinera_flores', 67.0, 39.2), ('jardinera_flores', 75.0, 39.2), ('jardinera_flores', 85.5, 39.2),
    ('jardinera_flores', 91.0, 39.2), ('jardinera_flores', 5.5, 39.2), ('jardinera_flores', 17.5, 39.2),
    ('arbol_grande', 78.0, 35.0), ('arbol_mediano', 65.26, 35.88), ('pino', 83.5, 36.6), ('arbol_grande_b', 95.0, 35.0),
    ('arbol_mediano', 8.0, 35.4), ('pino', 19.5, 36.8), ('arbol_grande_b', 27.6, 35.5), ('arbol_mediano', 124.5, 35.5),
]
GRUPOS_ARBUSTOS = [(71.0, 36.2), (89.0, 36.0), (2.5, 36.4), (14.5, 35.8), (24.5, 36.6), (115.5, 36.0)]


def rellenar_manzana_centro(es):
    def base_px(y):
        return int(round(H_PX - 1 - y * T))

    def en_franja(x, y):
        f = RELLENO_FRANJA
        return f['y0'] <= y <= f['y1'] and any(a <= x <= b for a, b in f['xs'])

    nuevos = list(RELLENO_CENTRO)
    for cx, cy in GRUPOS_ARBUSTOS:
        nuevos += [('arbusto_grande', cx, cy), ('arbusto_chico', cx - 1.1, cy - 0.35), ('arbusto_flores', cx + 1.2, cy - 0.3)]
    sueltos = ('arbusto_', 'seto')
    arboles = ('arbol_', 'pino')
    quedan = []
    for o in es.objs:
        base, name, cx, flip = o
        x, y = cx / T, a_unity_y(base)
        if en_franja(x, y) and name.startswith(sueltos):
            continue                                  # arbusto suelto: lo reemplazan los grupos
        if en_franja(x, y) and name.startswith(arboles) and                 any(abs(x - nx) < 1.8 and abs(y - ny) < 1.6 for _, nx, ny in nuevos):
            continue                                  # árbol que estorba a algo nuevo
        quedan.append(o)
    es.objs[:] = quedan
    for name, x, y in nuevos:
        r, c = ROWS - 1 - int(y), int(x)
        assert es.grid[r, c].startswith(('pasto', 'flores')), ('relleno fuera del pasto', name, x, y, es.grid[r, c])
        es.put(name, int(round(x * T)), base_px(y))


# Negocios de barrio (arte de la zona Comercial) en el bosquecito de abajo a la izquierda: es el único pasto
# libre de casas con una banqueta al sur, así que la entrada de cada tienda da a la banqueta de la orilla de
# abajo (detrás sigue la calle que sale del mapa) y el lote del lado derecho toca la calle Pino.
# Todo en coordenadas de Unity: (x, y) del pivote abajo-centro. Van al final, sin usar el rng compartido.
NEGOCIOS = [   # (sprite, x, y)
    ('tienda_floreria', 3.5, 3.0),
    ('tienda_panaderia', 18.0, 3.0),
    ('tienda_farmacia', 27.0, 3.0),
]
ACCESOS = [(7, 13, 3, 7), (21, 24, 3, 7)]      # concreto para estacionarse: celdas x0..x1-1, y0..y1-1
JUNTO_A_NEGOCIOS = [
    ('camioneta_reparto', 10.0, 3.25, False),   # la de la florería
    ('carro_lateral_teal', 10.0, 5.3, True),
    ('carro_frontal_rosa', 22.5, 3.2, False),   # entre la panadería y la farmacia
    ('cubetas_flores', 1.6, 2.15, False), ('jardinera_flores', 5.5, 2.1, False),
    ('farol', 12.7, 6.9, False), ('banca_parque', 8.6, 0.25, False),
    ('pizarra_panaderia', 15.7, 2.1, False), ('parquimetro', 21.3, 2.6, False),
    ('banca_parque', 24.3, 0.25, False), ('jardinera_flores', 29.0, 2.1, False),
]
BANCA_BOSQUE = ((15.81, 6.16), (16.6, 10.3))     # la banca del sendero que queda debajo de la panadería se recorre

# Cancha de básquet de 6x4 tiles con 1 tile de pasto alrededor. La franja sur (y 11-13) solo tiene 3 filas de
# pasto, así que va en el único hueco del mapa donde cabe sin tocar casas, faroles ni banquetas: la franja de
# pasto de la manzana del centro, junto a la banqueta de la avenida.
CANCHA = (86, 34)                                 # celda de abajo-izquierda
# los aros: (sprite, x, y del pivote = base del poste), a 12 px de su borde de la cancha, los dos iguales
AROS = [('aro_basquet_norte', CANCHA[0] + CANCHA_W / 2, CANCHA[1] + CANCHA_H - 12 / T),
        ('aro_basquet_sur', CANCHA[0] + CANCHA_W / 2, CANCHA[1] + 12 / T)]

# Relleno de la franja de pasto entre la banqueta de Olmo y los patios de las casas de abajo (y 11-13)
RELLENO_FRANJA_SUR = [
    ('huerto_comunitario', 71.0, 11.1),
    ('mesa_picnic', 89.5, 11.35), ('mesa_picnic', 96.3, 11.35),
    ('banca_parque', 92.9, 12.6), ('bote_basura', 86.9, 11.5), ('bote_basura', 98.4, 11.6),
]
GRUPOS_ARBUSTOS_SUR = [(44.0, 11.6), (51.0, 12.0), (65.0, 12.2), (77.5, 11.8), (83.0, 12.1)]
GRUPOS_ARBUSTOS_NORTE = [(48.5, 61.4), (54.5, 62.0), (12.8, 57.5), (95.6, 59.2)]   # detrás de las casas de arriba


def colocar_negocios(es, props):
    def base_px(y):
        return int(round(H_PX - 1 - y * T))

    def caja(name, x, y):
        h, w = props[name].shape[:2]
        return (x - w / T / 2, y, x + w / T / 2, y + h / T)

    def se_enciman(a, b, m=0.0):
        return a[0] < b[2] - m and b[0] < a[2] - m and a[1] < b[3] - m and b[1] < a[3] - m

    nuevos = [(n, x, y, False) for n, x, y in NEGOCIOS] + JUNTO_A_NEGOCIOS + [(n, x, y, False) for n, x, y in RELLENO_FRANJA_SUR]
    for cx, cy in GRUPOS_ARBUSTOS_SUR + GRUPOS_ARBUSTOS_NORTE:
        nuevos += [('arbusto_grande', cx, cy, False), ('arbusto_chico', cx - 1.1, cy - 0.35, False),
                   ('arbusto_flores', cx + 1.2, cy - 0.3, False)]
    for n, x, y in AROS:                                  # se colocan por el pie del sprite, como todos
        nuevos.append((n, x, y - PIVOTES[n] * props[n].shape[0] / T, False))
    tiendas = [caja(n, x, y) for n, x, y in NEGOCIOS]
    pisos = [(x0, y0, x1, y1) for x0, x1, y0, y1 in ACCESOS] + \
            [(CANCHA[0] - 1, CANCHA[1] - 1, CANCHA[0] + CANCHA_W + 1, CANCHA[1] + CANCHA_H + 1)]   # con su margen
    ocupan = [caja(n, x, y) for n, x, y, _ in nuevos]

    quitados, quedan = [], []
    for o in es.objs:
        base, name, cx, flip = o
        x, y = cx / T, a_unity_y(base)
        pie = caja(name, x, y)
        pie = (pie[0], pie[1], pie[2], pie[1] + 1)            # donde se apoya
        vegetal = name.startswith(('arbol_', 'pino', 'arbusto_', 'seto'))
        if name == 'banca_parque' and abs(x - BANCA_BOSQUE[0][0]) < .05 and abs(y - BANCA_BOSQUE[0][1]) < .05:
            quitados.append(name)
            nuevos.append(('banca_parque', *BANCA_BOSQUE[1], False))
            continue
        if any(se_enciman(pie, t) for t in tiendas + pisos):
            assert vegetal, ('un negocio tapa algo que no es vegetación', name, x, y)
            quitados.append(name)
            continue
        if any(se_enciman(pie, c, .05) for c in ocupan):
            # para el relleno solo se quitan arbustos sueltos; los árboles solo si estorban a un negocio
            assert name.startswith(('arbusto_', 'seto')) or any(se_enciman(pie, caja(n, nx, ny)) for n, nx, ny in NEGOCIOS) \
                or any(se_enciman(pie, caja(n, nx, ny)) for n, nx, ny, _ in JUNTO_A_NEGOCIOS), ('el relleno taparía', name, x, y)
            quitados.append(name)
            continue
        quedan.append(o)
    es.objs[:] = quedan
    for name, x, y, flip in nuevos:
        r, c = ROWS - 1 - int(y), int(x)
        assert es.grid[r, c].startswith(('pasto', 'flores', 'banqueta', 'orilla')), ('fuera del pasto', name, x, y, es.grid[r, c])
        es.put(name, int(round(x * T)), base_px(y), flip)

    # concreto delante de las tiendas y cancha (celdas enteras: no aparecen bordes nuevos)
    for x0, x1, y0, y1 in ACCESOS:
        for x in range(x0, x1):
            for y in range(y0, y1):
                assert es.grid[ROWS - 1 - y, x].startswith(('pasto', 'flores')), (x, y)
                es.grid[ROWS - 1 - y, x] = 'banqueta'
    for x in range(CANCHA[0] - 1, CANCHA[0] + CANCHA_W + 1):        # cancha y su margen, todo pasto
        for y in range(CANCHA[1] - 1, CANCHA[1] + CANCHA_H + 1):
            assert es.grid[ROWS - 1 - y, x].startswith(('pasto', 'flores')), ('cancha fuera del pasto', x, y)
    for i in range(CANCHA_W * CANCHA_H):
        f, c = divmod(i, CANCHA_W)
        r = ROWS - 1 - (CANCHA[1] + CANCHA_H - 1 - f)
        es.grid[r, CANCHA[0] + c] = f'cancha_{i:02d}'
    return quitados


def colocar_calle(es, casas, empresa, lz):
    """Faroles y letreros con el nombre de cada calle (los autos van en colocar_autos)."""
    rng = es.rng
    puertas = [c['d'] for c in casas] + [empresa['centro_px']]
    rampas = [(min(c['rampa']) * T, (max(c['rampa']) + 1) * T) for c in casas]
    carga = (EMPRESA['carga'][0] * T, (EMPRESA['carga'][1] + 1) * T)
    sendero = (13 * T, 15 * T)

    def estorba(x, mitad, zonas_extra=()):
        if any(abs(x - d) < mitad + 48 for d in puertas):
            return True
        return any(a - mitad - 8 < x < b + mitad + 8 for a, b in rampas + [carga] + list(zonas_extra))

    def en_cruce(x, m=40):
        return any(a - m <= x <= b + m for a, b in ZONAS_CRUCE)

    def sobre(nombre, x, base):
        assert lz.mat(x, base) in (BANQ_M, LOSA), (nombre, x / T, base / T)
        es.put(nombre, x, base)

    # faroles
    for x in range(176, (CURVA[0] - 3) * T, 320):                      # avenida, banqueta norte
        if not estorba(x, 16):
            sobre('farol', x, AV_N * T + 8)
    for x in range(336, (CURVA[0] - 1) * T, 384):                      # avenida, banqueta sur
        if not 27 * T < x < 48 * T:
            sobre('farol', x, (AV_N + 13) * T - 3)
    cx, cy = CURVA
    for ang in (22, 52, 80):                                           # banqueta de afuera de la curva
        a = math.radians(ang)
        sobre('farol', cx * T + 15.6 * T * math.sin(a), cy * T - 15.6 * T * math.cos(a))
    a = math.radians(45)
    sobre('farol', cx * T + 3.6 * T * math.sin(a), cy * T - 3.6 * T * math.cos(a))
    for r in list(range(cy + 3, OLMO - 2, 7)) + list(range(OLMO + 13, BANQ_SUR - 2, 7)):
        sobre('farol', AV_X[0] * T + 10, r * T + 28)
        sobre('farol', AV_X[5] * T - 10, (r + 3) * T + 28)
    for r in (27, 34):                                                 # calle empedrada
        sobre('farol', V_COL * T + 10, r * T + 28)
        sobre('farol', (V_COL + 9) * T - 10, (r + 3) * T + 28)
    sobre('farol', V_COL * T + 10, 54 * T + 28)                        # calle Pino
    sobre('farol', (V_COL + 9) * T - 10, 57 * T + 28)
    for x in range(208, W_PX, 320):                                    # calle Olmo
        if not en_cruce(x) and not estorba(x, 16):
            sobre('farol', x, OLMO * T + 8)
    for x in range(400, W_PX, 384):
        if not en_cruce(x):
            sobre('farol', x, (OLMO + 11) * T - 3)
    for x in range(240, W_PX, 352):                                    # banqueta de abajo
        if not en_cruce(x) and not estorba(x, 16, [sendero]) and not 99 * T - 40 < x < 112 * T + 40:
            sobre('farol', x, BANQ_SUR * T + 8)

    # letreros con el nombre de la calle, en las esquinas
    for clave, x, base in (('avenida', 13 * T + 16, AV_N * T + 10),
                           ('avenida', 37 * T + 4, (AV_N + 13) * T - 4),
                           ('empedrada', V_COL * T + 14, (AV_N + 13) * T + 26),
                           ('olmo', V_COL * T + 12, OLMO * T + 14),
                           ('empedrada', (V_COL + 9) * T - 14, OLMO * T + 14),
                           ('pino', V_COL * T + 12, (OLMO + 11) * T - 4),
                           ('olmo', (V_COL + 9) * T - 14, (OLMO + 11) * T - 4),
                           ('avenida', AV_X[0] * T + 12, OLMO * T + 14),
                           ('olmo', AV_X[5] * T - 14, OLMO * T + 14),
                           ('olmo', AV_X[0] * T + 12, (OLMO + 11) * T - 4),
                           ('avenida', AV_X[5] * T - 14, (OLMO + 11) * T - 4),
                           ('pino', (V_COL + 9) * T - 14, BANQ_SUR * T + 14),
                           ('avenida', cx * T + 15 * T * math.sin(math.radians(36)),
                            cy * T - 15 * T * math.cos(math.radians(36)))):
        sobre(f'letrero_calle_{clave}', x, base)


# ================================================================ autos
# Todo en unidades de Unity (1 tile = 1 unidad, y hacia arriba, desde la esquina de abajo-izquierda
# del mapa); los autos usan el pivote abajo-centro, igual que los props.
# Cada carril es un tramo recto: TrafficCar avanza de limiteMin a limiteMax (o al revés) y al llegar al
# final reaparece al inicio. Los carriles verticales se cortan antes de Calle Olmo: TrafficCar frena ante
# cualquier auto que tenga enfrente, y dos autos que llegaran a la vez a un cruce se frenarían el uno al
# otro para siempre.
def unity_y(fila):
    """Orilla de abajo de la fila del generador, en y de Unity."""
    return ROWS - 1 - fila


ALTO_LATERAL = 1.5           # alto del cuerpo del auto visto de lado (48 px)
CARRILES = [
    # clave, sentido, eje fijo (y del pivote o x del centro), (limiteMin, limiteMax)
    # avenida recta: el carril va pegado al camellón; del lado del bordillo queda la franja para estacionarse
    ('avenida_izquierda', 'izquierda', unity_y(AV_N + 5), (-1.6, CURVA[0] - 1.0)),
    ('avenida_derecha', 'derecha', unity_y(AV_N + 7) + 1 - ALTO_LATERAL, (-1.6, CURVA[0] - 1.0)),
    # Calle Olmo: dos carriles de 2 tiles separados por la línea, de orilla a orilla del mapa
    ('olmo_izquierda', 'izquierda', unity_y(OLMO + 4) + .25, (-1.6, COLS + 1.6)),
    ('olmo_derecha', 'derecha', unity_y(OLMO + 7) + .25, (-1.6, COLS + 1.6)),
    # avenida vertical: se baja por el lado oeste y se sube por el este (se circula por la derecha)
    ('avenida_curva_baja', 'abajo', AV_X[1] + 1.5, (24.0, 37.0)),
    ('avenida_curva_sube', 'arriba', AV_X[3] + 1.5, (24.0, 37.0)),
    ('avenida_sur_baja', 'abajo', AV_X[1] + 1.5, (-2.6, 12.5)),
    ('avenida_sur_sube', 'arriba', AV_X[3] + 1.5, (-2.6, 12.0)),   # al subir, el frente va 3 tiles arriba
    # calle empedrada (de la avenida a Olmo) y calle Pino (de Olmo a la orilla): 3 tiles, 1.5 por carril
    ('empedrada_baja', 'abajo', V_COL + 3.75, (23.5, 40.0)),
    ('empedrada_sube', 'arriba', V_COL + 5.25, (23.5, 39.5)),
    ('pino_baja', 'abajo', V_COL + 3.75, (-2.6, 12.5)),
    ('pino_sube', 'arriba', V_COL + 5.25, (-2.6, 12.0)),
]
VECTOR = {'derecha': (1, 0), 'izquierda': (-1, 0), 'arriba': (0, 1), 'abajo': (0, -1)}
# franjas para estacionarse junto a los bordillos de la avenida: (lado, y del pivote, voltear)
BORDILLO_AV = [('norte', unity_y(AV_N + 3) + 1 - ALTO_LATERAL, True),   # mira a la izquierda, como su carril
               ('sur', unity_y(AV_N + 9), False)]
# Lo que usa TrafficCar para frenar (sus valores por defecto): caja de 1.8 x 0.6 que empieza 0.4 delante
# del pivote, a alturaCentro sobre él. Al subir, el frente del auto queda arriba del cuerpo.
FRENADO, ANCHO_DETECCION, ALTURA_CENTRO = 1.8, 0.6, 0.5
# al subir, el detector arranca justo en el frente del cuerpo: se calcula del sprite (alto del cuerpo - 0.4)


def sprite_auto(color, sentido):
    if sentido in ('derecha', 'izquierda'):
        return f'carro_lateral_{color}', sentido == 'izquierda'   # el de lado mira a la derecha
    return (f'carro_frontal_{color}' if sentido == 'abajo' else f'carro_trasero_{color}'), False


def cuerpo_auto(a, voltear):
    """Caja del cuerpo (sin sombra, espejos ni defensas sueltas) en unidades, relativa al pivote abajo-centro."""
    h, w = a.shape[:2]
    op = a[:, :, 3] == 255
    filas = np.nonzero(op.any(axis=1))[0]
    alto = filas[-1] - filas[0] + 1
    cols = np.nonzero(op.sum(axis=0) >= .3 * alto)[0]
    x0, x1 = cols[0], cols[-1]
    ox = ((x0 + x1 + 1) / 2 - w / 2) / T
    return dict(ancho=round((x1 - x0 + 1) / T, 4), alto=round(alto / T, 4),
                x=round(-ox if voltear else ox, 4), y=round((h - (filas[0] + filas[-1] + 1) / 2) / T, 4))


def caja_auto(x, y, c):
    return (x + c['x'] - c['ancho'] / 2, y + c['y'] - c['alto'] / 2, x + c['x'] + c['ancho'] / 2, y + c['y'] + c['alto'] / 2)


def se_enciman(a, b, m=0.0):
    return a[0] < b[2] + m and b[0] < a[2] + m and a[1] < b[3] + m and b[1] < a[3] + m


def autos_calle(rng, props):
    """2 o 3 autos por carril, de colores distintos, repartidos al azar sin encimarse
    (también al dar la vuelta: el último no queda encima del primero cuando reaparece)."""
    autos = []
    for clave, sentido, fijo, (lo, hi) in CARRILES:
        horizontal = sentido in ('derecha', 'izquierda')
        n = rng.choice([2, 3])
        colores = rng.sample(COLORES_AUTO, n)
        c0 = cuerpo_auto(props[sprite_auto('rosa', sentido)[0]], False)
        hueco = (c0['ancho'] if horizontal else c0['alto']) + 1.25   # la comprobación pide al menos 1.0
        libre = (hi - lo) - n * hueco
        assert libre > 0, ('carril muy corto', clave)
        cortes = sorted(rng.uniform(0, libre) for _ in range(n))
        inicio = rng.uniform(0, hi - lo)
        for i, color in enumerate(colores):
            pos = lo + (inicio + cortes[i] + i * hueco) % (hi - lo)
            nombre, voltear = sprite_auto(color, sentido)
            auto = dict(carril=clave, direccion=sentido, vector=list(VECTOR[sentido]),
                        limite_min=lo, limite_max=hi, color=color, sprite=nombre, voltear=voltear,
                        x=round(pos if horizontal else fijo, 3), y=round(fijo if horizontal else pos, 3),
                        velocidad=round(rng.uniform(2, 3), 2), cuerpo=cuerpo_auto(props[nombre], voltear))
            if sentido == 'arriba':
                c = auto['cuerpo']
                auto['altura_detector'] = round(c['y'] + c['alto'] / 2 - .4, 4)
            autos.append(auto)
    return autos


def colocar_autos(es, casas, lz, props):
    """Autos de la calle (los mueve TrafficCar) y pocos estacionados: dos junto a cada bordillo de la
    avenida y unos cuantos en cocheras, con colores, sprites y posiciones al azar.
    Usa su propio azar para que mover los autos no cambie el resto del mapa."""
    rng = random.Random(2026)
    calle = autos_calle(rng, props)
    estacionados = []
    colores = COLORES_AUTO[:]
    rng.shuffle(colores)

    def otro_color():
        colores.append(colores.pop(0))
        return colores[-1]

    # en x de tiles: cebras y cruce de la calle empedrada, puertas y rampas de enfrente, baches
    cebras = [(min(cs), max(cs) + 1) for rs, cs in CRUCES_H + CRUCES_V if min(rs) < AV_N + 13] + [(27, 42)]
    frente = [(c['d'] / T - 1.5, c['d'] / T + 1.5) for c in casas if c['manzana'] == 'A'] + \
             [(min(c['rampa']), max(c['rampa']) + 1) for c in casas if c['manzana'] == 'A'] + \
             [(EMPRESA['carga'][0], EMPRESA['carga'][1] + 1), (EMPRESA['x_izq'] / T + 3, EMPRESA['x_izq'] / T + 7)]
    baches = [(c, ROWS - 1 - r) for (r, c), n in es.grid.items() if n.startswith('bache')]

    for lado, y, voltear in BORDILLO_AV:
        cuerpo = cuerpo_auto(props['carro_lateral_rosa'], voltear)
        puestos = 0
        for _ in range(400):
            if puestos == 2:
                break
            x = round(rng.uniform(3, CURVA[0] - 5) * T) / T
            b = caja_auto(x, y, cuerpo)
            if any(b[0] - 1 < hi and lo < b[2] + 1 for lo, hi in cebras):
                continue
            if lado == 'norte' and any(b[0] < hi and lo < b[2] for lo, hi in frente):
                continue
            if any(b[0] - 1 < bx + 1 and bx < b[2] + 1 and b[1] < by + 1 and by < b[3] for bx, by in baches):
                continue
            if any(abs(x - e['x']) < 12 for e in estacionados if e['lugar'] == lado):
                continue
            color = otro_color()
            estacionados.append(dict(lugar=lado, x=x, y=y, nombre=f'carro_lateral_{color}', color=color, voltear=voltear))
            puestos += 1
        assert puestos == 2, ('no cupieron los autos junto al bordillo', lado)

    # cocheras: unas cuantas casas al azar, de frente o metidos de reversa, más adentro o más afuera
    for c in rng.sample(casas, 7):
        color = otro_color()
        nombre = f"carro_{rng.choice(['frontal', 'trasero'])}_{color}"
        base = c['house_base'] + 14 + rng.randint(0, 40)   # el auto mide 96 px: cabe completo en la rampa
        x = min(c['rampa']) + 1
        c['auto'] = color
        estacionados.append(dict(lugar='cochera', x=x, y=a_unity_y(base), nombre=nombre, color=color, voltear=False))

    for e in estacionados:
        es.put(e['nombre'], round(e['x'] * T), H_PX - 1 - round(e['y'] * T), flip=e['voltear'])
    comprobar_autos(es, calle, estacionados, lz, props)
    return calle, estacionados


def comprobar_autos(es, calle, estacionados, lz, props):
    def material(x, y):   # x, y en unidades de Unity
        return lz.mat(x * T, (ROWS - y) * T - .5)

    carriles = {}
    for a in calle:
        carriles.setdefault(a['carril'], a)
        lo, hi, c = a['limite_min'], a['limite_max'], a['cuerpo']
        horizontal = a['direccion'] in ('derecha', 'izquierda')
        # el cuerpo va sobre la calle en todo el recorrido que queda dentro del mapa
        for t in np.arange(lo, hi + .01, .25):
            b = caja_auto(t, a['y'], c) if horizontal else caja_auto(a['x'], t, c)
            for px, py in ((b[0] + .05, b[1] + .05), (b[2] - .05, b[1] + .05), (b[0] + .05, b[3] - .05), (b[2] - .05, b[3] - .05)):
                if 0 <= px < COLS and 0 <= py < ROWS:
                    assert material(px, py) in CALLE_M, ('auto fuera de la calle', a['carril'], round(t, 2), material(px, py))

    # por dónde pasa el cuerpo de cada carril y hasta dónde mira su detector de frenado
    def barrido(a):
        lo, hi, c = a['limite_min'], a['limite_max'], a['cuerpo']
        if a['direccion'] in ('derecha', 'izquierda'):
            cuerpo = (lo + c['x'] - c['ancho'] / 2, a['y'] + c['y'] - c['alto'] / 2,
                      hi + c['x'] + c['ancho'] / 2, a['y'] + c['y'] + c['alto'] / 2)
            yc = a['y'] + a.get('altura_detector', ALTURA_CENTRO)
            ojo = (lo - .4 - FRENADO, yc - ANCHO_DETECCION / 2, hi + .4 + FRENADO, yc + ANCHO_DETECCION / 2)
        else:
            cuerpo = (a['x'] + c['x'] - c['ancho'] / 2, lo + c['y'] - c['alto'] / 2,
                      a['x'] + c['x'] + c['ancho'] / 2, hi + c['y'] + c['alto'] / 2)
            yc = a.get('altura_detector', ALTURA_CENTRO)
            ojo = (a['x'] - ANCHO_DETECCION / 2, lo + yc - .4 - FRENADO, a['x'] + ANCHO_DETECCION / 2, hi + yc + .4 + FRENADO)
        return cuerpo, ojo

    franjas = {k: barrido(a) for k, a in carriles.items()}
    for k1, (cuerpo1, ojo1) in franjas.items():
        for k2, (cuerpo2, _) in franjas.items():
            if k1 != k2:
                # ningún carril se cruza con otro ni frena por los autos de otro carril
                assert not se_enciman(cuerpo1, cuerpo2, -.01), ('carriles que se cruzan', k1, k2)
                assert not se_enciman(ojo1, cuerpo2, -.01), ('un carril frena por los autos de otro', k1, k2)
    # los de un mismo carril arrancan separados (contando la vuelta)
    for k, a in carriles.items():
        horizontal = a['direccion'] in ('derecha', 'izquierda')
        largo = a['cuerpo']['ancho' if horizontal else 'alto']
        pos = sorted(x['x' if horizontal else 'y'] for x in calle if x['carril'] == k)
        L = a['limite_max'] - a['limite_min']
        gaps = [b - p for p, b in zip(pos, pos[1:])] + [pos[0] + L - pos[-1]]
        assert min(gaps) >= largo + 1.0, ('autos encimados en el carril', k, gaps)
        assert len(set(x['color'] for x in calle if x['carril'] == k)) == len(pos), ('colores repetidos', k)
    # estacionados: fuera de los carriles y sin encimarse entre ellos
    cajas = [(e, caja_auto(e['x'], e['y'], cuerpo_auto(props[e['nombre']], e['voltear']))) for e in estacionados]
    for e, b in cajas:
        for k, (cuerpo, ojo) in franjas.items():
            assert not se_enciman(cuerpo, b, -.01) and not se_enciman(ojo, b, -.01), ('estacionado en el carril', k, e)
    for i in range(len(cajas)):
        for j in range(i + 1, len(cajas)):
            assert not se_enciman(cajas[i][1], cajas[j][1]), ('estacionados encimados', cajas[i][0], cajas[j][0])
    # junto al bordillo: sobre asfalto liso, sin tocar cebras, baches, líneas ni la banqueta
    for e, b in cajas:
        if e['lugar'] == 'cochera':
            assert all(es.grid[r, int(e['x']) + d].startswith('banqueta')
                       for d in (-1, 0) for r in range(ROWS - 1 - int(b[3] - .05), ROWS - int(b[1] + .05))), ('auto fuera de la rampa', e)
            continue
        for px in np.arange(b[0] + .05, b[2], .25):
            for py in np.arange(b[1] + .05, b[3], .25):
                r, c = ROWS - 1 - int(py), int(px)
                assert material(px, py) == ASFALTO and \
                    es.grid[r, c] in ('asfalto_a', 'asfalto_b', 'asfalto_sombra_bordillo', 'asfalto_grieta'), \
                    ('estacionado sobre', es.grid[r, c], e)


def comprobar_fuentes(es, lz):
    """Las fuentes van en la plaza o en el pasto, nunca sobre la calle ni la banqueta."""
    fuentes = [(name, cx, base) for base, name, cx, _ in es.objs if name.startswith('fuente')]
    assert fuentes, 'falta la fuente de la plaza'
    for name, cx, base in fuentes:
        assert lz.mat(cx, base) in (LOSA, PASTO), ('fuente fuera de la plaza o el pasto', name, cx / T, base / T)


def preview_autos(ref, props, calle):
    """Vista a 1/2 con los autos de la calle en su lugar de arranque y la flecha de cada carril."""
    m = ref.copy()
    objs = [(H_PX - 1 - round(a['y'] * T), a['sprite'], round(a['x'] * T), a['voltear']) for a in calle]
    m = render_sobre(m, props, objs)
    img = Image.fromarray(m).resize((W_PX // 2, H_PX // 2), Image.NEAREST)
    dr = ImageDraw.Draw(img)
    vistos = set()
    for a in calle:
        if a['carril'] in vistos:
            continue
        vistos.add(a['carril'])
        lo, hi = a['limite_min'], a['limite_max']
        if a['direccion'] in ('derecha', 'izquierda'):
            y = (ROWS - a['y'] - .75) * T / 2
            p0, p1 = (max(lo, 0) * T / 2, y), (min(hi, COLS) * T / 2, y)
        else:
            x = a['x'] * T / 2
            p0, p1 = (x, (ROWS - max(lo, 0)) * T / 2), (x, (ROWS - min(hi, ROWS)) * T / 2)
        if a['direccion'] in ('izquierda', 'abajo'):
            p0, p1 = p1, p0
        dr.line((p0, p1), fill=(255, 230, 0), width=2)
        dr.ellipse((p1[0] - 5, p1[1] - 5, p1[0] + 5, p1[1] + 5), fill=(255, 230, 0))
    img.save(os.path.join(OUT, 'mapa_autos_grande.png'))


# ================================================================ atlas y tileset
def extender_atlas(props_viejos, extra):
    """Agrega los props nuevos debajo del atlas actual sin mover ni cambiar los existentes."""
    with open(os.path.join(OUT, 'props_atlas.json'), encoding='utf-8') as f:
        meta = json.load(f)
    atlas = np.asarray(Image.open(os.path.join(OUT, 'props_atlas.png')).convert('RGBA'))
    originales = [s for s in meta['sprites'] if s['name'] in props_viejos]
    alto_orig = max(s['y'] + s['h'] for s in originales) + meta.get('separacion', 4)
    for s in originales:   # los de siempre siguen idénticos
        a = atlas[s['y']:s['y'] + s['h'], s['x']:s['x'] + s['w']]
        assert np.array_equal(a, props_viejos[s['name']]), f"{s['name']} cambió en el atlas"
    assert not set(extra) & set(props_viejos), 'un prop nuevo usa un nombre que ya existía'

    pad, ancho = 4, atlas.shape[1]
    items = sorted(extra.items(), key=lambda kv: (-kv[1].shape[0], kv[0]))
    x, y, alto_fila, puestos = pad, alto_orig, 0, []
    for name, a in items:
        h, w = a.shape[:2]
        if x + w + pad > ancho:
            x, y, alto_fila = pad, y + alto_fila + pad, 0
        puestos.append((name, x, y, w, h))
        x += w + pad
        alto_fila = max(alto_fila, h)
    H = y + alto_fila + pad
    nuevo = np.zeros((H, ancho, 4), np.uint8)
    nuevo[:alto_orig] = atlas[:alto_orig]
    sprites = [dict(s) for s in originales]
    for name, x, y, w, h in puestos:
        nuevo[y:y + h, x:x + w] = extra[name]
        sprites.append(dict(name=name, x=x, y=y, w=w, h=h))
        if name in PIVOTES:
            sprites[-1]['pivote'] = [0.5, PIVOTES[name]]
    for s in sprites:
        s['y_unity'] = H - s['y'] - s['h']
    sprites.sort(key=lambda s: s['name'])
    meta.update(alto=H, sprites=sprites)
    Image.fromarray(nuevo).save(os.path.join(OUT, 'props_atlas.png'))
    with open(os.path.join(OUT, 'props_atlas.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    shutil.copyfile(os.path.join(OUT, 'props_atlas.png'), ATLAS_UNITY)
    return H


# Piezas que solo pueden ir completas y una sola vez cada una (lagos y fuentes), y los autos viejos del
# mapa chico (auto_*), que tienen otra escala: si el mapa no las usa, se quitan del JSON del atlas para que
# Unity no corte pedazos sueltos de agua ni autos de otro tamaño.
PIEZAS_PODABLES = ('lago', 'fuente', 'auto_')


def podar_atlas(data):
    """Quita de props_atlas.json las piezas de PIEZAS_PODABLES que el mapa no usa y comprueba que cada lago y
    cada fuente del mapa sea un solo objeto con su sprite completo. Los píxeles se quedan en el PNG."""
    ruta = os.path.join(OUT, 'props_atlas.json')
    with open(ruta, encoding='utf-8') as f:
        meta = json.load(f)
    usados = [p['nombre'] for p in data['props']] + [a['sprite'] for a in data['autos_calle']] + \
             [d['nombre'] for d in data['dinamicos'] if d['tipo'] == 'sprite']
    for n in set(usados):
        if n.startswith(('lago', 'fuente')):
            assert usados.count(n) == 1, ('el lago o la fuente está repetido o partido en piezas', n, usados.count(n))
    quitados = [s['name'] for s in meta['sprites'] if s['name'].startswith(PIEZAS_PODABLES) and s['name'] not in usados]
    meta['sprites'] = [s for s in meta['sprites'] if s['name'] not in quitados]
    assert set(usados) <= {s['name'] for s in meta['sprites']}, set(usados) - {s['name'] for s in meta['sprites']}
    with open(ruta, 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    return quitados


def extender_tileset(tmeta, tiles, nuevos):
    """Agrega los tiles nuevos después de los de siempre, en la misma cuadrícula de 8 columnas:
    los que ya existían conservan su lugar (y en Unity su sprite y su Tile)."""
    with open(os.path.join(OUT, 'tileset_suelo.json'), encoding='utf-8') as f:
        meta = json.load(f)
    actual = np.asarray(Image.open(os.path.join(OUT, 'tileset_suelo.png')).convert('RGB'))
    cols = meta['columnas']
    for t in tmeta:   # los de siempre siguen idénticos
        assert np.array_equal(actual[t['y']:t['y'] + T, t['x']:t['x'] + T], tiles[t['name']]), \
            f"el tile {t['name']} cambió"
    nombres = {t['name'] for t in tmeta}
    assert not nombres & {n for n, _ in nuevos}, 'un tile nuevo usa un nombre que ya existía'
    todos = [(t['name'], tiles[t['name']]) for t in tmeta] + list(nuevos)
    filas = (len(todos) + cols - 1) // cols
    hoja = np.zeros((filas * T, cols * T, 4), np.uint8)
    lista = []
    for i, (name, a) in enumerate(todos):
        x, y = (i % cols) * T, (i // cols) * T
        hoja[y:y + T, x:x + T, :3] = a
        hoja[y:y + T, x:x + T, 3] = 255
        lista.append(dict(name=name, index=i, x=x, y=y, w=T, h=T))
    meta['tiles'] = lista
    Image.fromarray(hoja).save(os.path.join(OUT, 'tileset_suelo.png'))
    with open(os.path.join(OUT, 'tileset_suelo.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    shutil.copyfile(os.path.join(OUT, 'tileset_suelo.png'), TILESET_UNITY)
    return len(todos), filas * T


# ================================================================ render y export
def render(tiles, props, grid, objs):
    m = np.zeros((H_PX, W_PX, 3), np.uint8)
    for (r, c), n in grid.items():
        m[r * T:(r + 1) * T, c * T:(c + 1) * T] = tiles[n]
    return render_sobre(m, props, [o for o in objs if not o[1].startswith(DINAMICOS)])


def render_sobre(m, props, objs):
    for base, name, cx, flip in sorted(objs, key=lambda o: o[0]):
        a = props[name][:, ::-1] if flip else props[name]
        h, w = a.shape[:2]
        x0, y0 = cx - w // 2, base - h + 1
        for yy, xx in zip(*np.nonzero(a[:, :, 3])):
            X, Y = x0 + xx, y0 + yy
            if not (0 <= X < W_PX and 0 <= Y < H_PX):
                continue
            if a[yy, xx, 3] == 255:
                m[Y, X] = a[yy, xx, :3]
            else:
                c = tuple(int(v) for v in m[Y, X])
                m[Y, X] = SHADOW.get(c, tuple(int(v * .78) for v in c))
    return m


def comprobar_bordillos(tiles, grid, lz, ov):
    """El suelo armado con tiles debe tener cada píxel de bordillo donde lo pide el raster:
    si una curva o una esquina quedara cortada o desalineada aparecería aquí."""
    clase = {P['curb']: B_CURB, P['curb_l']: B_CURB_L, P['stone_d']: B_STONE_D}
    ignorar = ('cebra', 'linea', 'bache', 'asfalto_grieta', 'banqueta_esquina')
    malas = []
    rampas_carga = set()
    for rf, lotes in LOTES.values():
        for k, lado in lotes:
            for c in rampa_cols(k, lado):
                for r in range(rf - 5, rf + 1):
                    rampas_carga.add((r, c))
    c0, c1 = EMPRESA['carga']
    rampas_carga |= {(r, c) for r in range(EMPRESA['fila_base'] + 1, AV_N) for c in range(c0, c1 + 1)}
    for (r, c), n in grid.items():
        if n.startswith(ignorar) or (r, c) in rampas_carga:
            continue
        t = tiles[n]
        real = np.zeros((T, T), np.uint8)
        for col, kk in clase.items():
            real[np.all(t == col, axis=2)] = kk
        o = lz.celda(ov, r, c)
        esperado = np.where(np.isin(o, list(clase.values())), o, 0)
        fuera = lz.celda(lz.m, r, c) == ADOQUIN
        if not np.array_equal(np.where(fuera, 0, real), np.where(fuera, 0, esperado)):
            malas.append((r, c, n))
    assert not malas, f'bordillos que no empatan con el trazado: {malas[:10]}'


def a_unity_y(base_px):
    return (H_PX - (base_px + 1)) / T


def exportar(grid, bajo_bache, objs, casas, empresa, props, calle):
    suelo, dinamicos = [], []
    for r in range(ROWS):
        fila = []
        for c in range(COLS):
            n = grid[r, c]
            if n.startswith(DINAMICOS):
                dinamicos.append(dict(nombre=n, tipo='tile', x=c, y=ROWS - 1 - r))
                n = bajo_bache[r, c]
            fila.append(n)
        suelo.append(fila)
    lista = []
    for base, name, cx, flip in sorted(objs, key=lambda o: o[0]):
        w = props[name].shape[1]
        x = (cx - w // 2 + w / 2) / T
        y = a_unity_y(base) + PIVOTES.get(name, 0) * props[name].shape[0] / T   # pivote propio (aros)
        if name.startswith(DINAMICOS):
            dinamicos.append(dict(nombre=name, tipo='sprite', x=x, y=y, voltear=flip))
        else:
            o = dict(nombre=name, x=x, y=y)
            if flip:
                o['voltear'] = True
            lista.append(o)

    lista_casas, puntos = [], []
    for i, c in enumerate(casas):
        entrega = dict(x=c['d'] / T, y=ROWS - (c['acera'] + 1) - 0.5)   # fila del medio de la banqueta
        tile = suelo[ROWS - 1 - int(entrega['y'])][int(entrega['x'])]
        assert tile.startswith('banqueta'), (i + 1, tile)
        # la banqueta de enfrente del portón: misma columna que la puerta, justo abajo de la cerca
        assert ROWS - 1 - int(entrega['y']) == c['acera'] + 1 and int(entrega['x']) == c['k'], (i + 1, entrega)
        lista_casas.append(dict(id=i + 1, nombre=c['nombre'],
                                puerta=dict(x=c['d'] / T, y=a_unity_y(c['puerta_base'])),
                                entrega=entrega, cochera=dict(columnas=c['rampa'], auto=c['auto'])))
        puntos.append(dict(nombre=f'PuntoEntrega_{i + 1}', casa=i + 1, **entrega))

    acera_empresa = AV_N + 1            # fila del medio de la banqueta de la avenida
    puerta = dict(x=empresa['centro_px'] / T, y=a_unity_y(empresa['puerta_base']))
    spawn = dict(x=puerta['x'], y=ROWS - acera_empresa - 0.5,
                 nota="banqueta de la avenida, frente a la puerta de Brenn's")
    assert suelo[ROWS - 1 - int(spawn['y'])][int(spawn['x'])].startswith('banqueta')
    return dict(columnas=COLS, filas=ROWS, tile=T,
                coordenadas='tiles; origen abajo-izquierda, y hacia arriba; '
                            'props/autos = pivote abajo-centro, baches = celda',
                suelo_orden='suelo[0] es la fila de arriba',
                calles=[dict(clave=k, nombre=v) for k, v in NOMBRES_CALLE.items()],
                spawn=spawn, empresa=dict(nombre=empresa['nombre'], puerta=puerta),
                casas=lista_casas, puntos_entrega=puntos,
                suelo=suelo, props=lista, dinamicos=dinamicos, autos_calle=calle)


# ================================================================ setos de los lotes
def cercar_lotes(es, props):
    """Cierra el lote de cada casa con setos bajos (props_setos.py): laterales, trasero, la cerca del frente
    completada hasta el límite y un poste de remate. Quita los árboles y arbustos que quedan debajo de un seto.
    Va al final de la escena y no usa el azar del mapa: lo demás queda donde estaba."""
    r = props_setos.calcular(LOTES, rampa_cols, lote_cols, es.grid, es.objs, props, COLISIONES, ROWS, COLS, LOTES_PEGADOS)
    # la vegetación que quedaba debajo de los setos viejos (cuando subían hasta la banqueta de atrás) sigue fuera:
    # la usuaria no la volvió a poner y así no aparecen árboles nuevos en el pasto que se libera
    viejo = props_setos.calcular(LOTES, rampa_cols, lote_cols, es.grid, es.objs, props, COLISIONES, ROWS, COLS)
    r['quitar'] += [i for i in viejo['quitar'] if i not in r['quitar']]
    quitados = [es.objs[i][1] for i in r['quitar']]
    es.objs[:] = [o for i, o in enumerate(es.objs) if i not in set(r['quitar'])]
    for nombre, cx, base in r['setos'] + r['cercas'] + r['postes']:
        es.put(nombre, cx, base)
    r['arte'] = props_setos.sprites(r['setos'])
    print('setos:', len(r['tramos']), 'setos en', len(r['setos']), 'tramos de 1 tile,', len(r['lotes']), 'lotes |', len(r['cercas']), 'cercas y',
          len(r['postes']), 'postes de remate | quitados debajo de un seto:', len(quitados), sorted(quitados))
    assert len(r['lotes']) == N_CASAS, len(r['lotes'])
    return r


def caja_unity(c):
    """Caja (x0, y0, x1, y1) en px del mapa (y hacia abajo) -> esquina de abajo a la izquierda y tamaño en tiles."""
    x0, y0, x1, y1 = c
    return dict(x=round(x0 / T, 4), y=round((H_PX - y1) / T, 4), ancho=round((x1 - x0) / T, 4), alto=round((y1 - y0) / T, 4))


def exportar_lotes(setos, casas):
    """Lo que cierra los patios (ConstruirMapa lo arma en "LotesSolidos": bloquea a pie y en moto) y, por casa,
    la caja del patio y la de su cochera (para comprobar que a pie no se entra al patio)."""
    solidos = [dict(tipo=t, **caja_unity(c)) for t, c in setos['solidos']]
    patios = []
    for c, l in zip(casas, setos['lotes']):
        assert c['k'] == l['k'] and c['manzana'] == l['manzana'], (c['k'], l['k'])
        patios.append(dict(casa=len(patios) + 1, patio=caja_unity(l['patio']), cochera=caja_unity(l['cochera'])))
    return solidos, patios


def comprobar_setos(data, setos):
    """Ningún seto tapa la banqueta ni la calle, ni se atraviesa en el camino de una clienta (de su puerta al
    lugar donde espera) o en el recorrido de un peatón o de la dueña."""
    suelo = data['suelo']
    for x0, y0, x1, y1 in setos['rects']:
        for r in range(int(y0) // T, (int(math.ceil(y1)) - 1) // T + 1):
            for c in range(max(0, int(x0) // T), min(COLS, (int(math.ceil(x1)) - 1) // T + 1)):
                assert suelo[r][c].startswith(('pasto', 'flores')), ('seto fuera del pasto', (x0, y0, x1, y1), r, c, suelo[r][c])
    # en tiles de Unity (y hacia arriba), con el ancho de los pies de una persona
    cajas = [(x0 / T, (H_PX - y1) / T, x1 / T, (H_PX - y0) / T) for x0, y0, x1, y1 in setos['rects']]

    def cruza(a, b, ancho=PIE[0] / 2):
        pasos = max(1, int(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.05))
        for i in range(pasos + 1):
            x, y = a[0] + (b[0] - a[0]) * i / pasos, a[1] + (b[1] - a[1]) * i / pasos
            pie = (x - ancho, y, x + ancho, y + PIE[1])
            if any(se_enciman(pie, c) for c in cajas):
                return (x, y)
        return None
    for cl in data['clientes']:
        p = cruza((cl['puerta']['x'], cl['puerta']['y']), (cl['espera']['x'], cl['espera']['y']))
        assert p is None, ('un seto cierra el paso de la clienta', cl['nombre'], p)
    for pe in data['peatones']:
        for a, b in zip(pe['puntos'], pe['puntos'][1:]):
            assert cruza(a, b) is None, ('un seto cierra el paso del peatón', pe['nombre'], a, b)
    assert cruza((data['duena']['x'], data['duena']['y']), (data['duena']['x'], data['duena']['y'])) is None


# ================================================================ peatones y dueña
# Franja de colisión de cada prop (px desde el borde izquierdo del sprite y alto desde abajo), la misma
# tabla que usa ConstruirMapa.cs: sirve para que ningún peatón camine a través de un farol, un buzón o un
# auto estacionado.
COLISIONES = [
    ('casa_', [(4, 155)], 16), ('arbol_grande', [(24, 31)], 8), ('arbol_mediano', [(18, 25)], 8),
    ('pino', [(17, 21)], 6), ('farol', [(2, 13)], 7), ('barda_porton', [(1, 8), (55, 62)], 8),
    ('barda_poste_final', [(1, 8)], 8), ('barda', [(0, 31)], 8), ('buzon_', [(7, 10)], 6),
    ('empresa_brenns', [(4, 315)], 22), ('cerca_baja_porton', [(1, 8), (87, 94)], 6),
    ('cerca_baja_poste', [(0, 7)], 6), ('cerca_baja', [(0, 31)], 5), ('lago_parque', [(6, 242)], 104),
    ('banca_parque', [(1, 44)], 7), ('hidrante', [(2, 15)], 6), ('bote_basura', [(2, 15)], 6),
    ('cajas_apiladas', [(0, 43)], 10), ('caja_carga', [(0, 19)], 8), ('fuente_plaza', [(3, 88)], 24),
    ('letrero_calle_avenida', [(28, 31)], 4), ('letrero_calle_empedrada', [(33, 36)], 4),
    ('letrero_calle_olmo', [(23, 26)], 4), ('letrero_calle_pino', [(22, 25)], 4),
    ('columpios', [(0, 16), (56, 72)], 6), ('resbaladilla', [(0, 60)], 8), ('jardinera_flores', [(0, 44)], 10),
    ('puesto_elotes', [(2, 53)], 12), ('semaforo_', [(12, 19)], 5),
    ('tienda_', [(2, 189)], 20), ('camioneta_reparto', [(6, 112)], 14), ('pizarra_panaderia', [(2, 19)], 5),
    ('parquimetro', [(3, 8)], 4), ('cubetas_flores', [(0, 45)], 8), ('huerto_comunitario', [(0, 127)], 56),
    ('mesa_picnic', [(0, 75)], 30), ('aro_basquet', [(10, 17)], 6),
]
N_PEATONES_BANQUETA, N_PEATONES_CRUCE, N_VARIANTES = 10, 4, 6
PIE = (0.6, 0.3)          # BoxCollider2D de los pies del peatón (ancho, alto), sobre el pivote
BANQUETA_T = ('banqueta',)
CALLE_T = ('asfalto', 'cebra', 'linea', 'camellon', 'adoquin', 'esquina', 'curva_avenida')


def cajas_obstaculos(data, props):
    """Cajas (x0, y0, x1, y1) en tiles de todo lo que choca: props de la tabla y autos estacionados."""
    cajas = []
    for p in data['props']:
        n = p['nombre']
        if n.startswith('carro_'):
            cajas.append(caja_auto(p['x'], p['y'], cuerpo_auto(props[n], p.get('voltear', False))))
            continue
        for prefijo, tramos, alto in COLISIONES:
            if n.startswith(prefijo):
                w = props[n].shape[1]
                izq = p['x'] - w / 2 / T
                for x0, x1 in tramos:
                    cajas.append((izq + x0 / T, p['y'], izq + (x1 + 1) / T, p['y'] + alto / T))
                break
    return cajas


def colocar_peatones(data, props):
    """10 peatones que van y vienen por las filas de banqueta libres (las de en medio llevan los
    PuntoEntrega) y 4 que cruzan la calle por las cebras. Coordenadas en tiles, origen abajo-izquierda,
    pivote en los pies. Usa su propio azar para no mover el resto del mapa."""
    rng = random.Random(4242)
    suelo = data['suelo']

    def tile(x, y):
        r, c = ROWS - 1 - int(math.floor(y)), int(math.floor(x))
        return suelo[r][c] if 0 <= r < ROWS and 0 <= c < COLS else None

    cajas = cajas_obstaculos(data, props)
    puntos = [(p['x'], p['y']) for p in data['puntos_entrega']]
    filas_punto = {int(math.floor(y)) for _, y in puntos}
    spawn = (data['spawn']['x'], data['spawn']['y'])
    usados = []                                   # corredores de los peatones ya puestos

    def corredor(a, b):
        return (min(a[0], b[0]) - PIE[0] / 2, min(a[1], b[1]), max(a[0], b[0]) + PIE[0] / 2, max(a[1], b[1]) + PIE[1])

    def libre(a, b, piso):
        caja = corredor(a, b)
        if any(se_enciman(caja, o, .05) for o in cajas):
            return False
        if any(se_enciman(caja, u, .4) for u in usados):
            return False
        if any(caja[0] - 1 < px < caja[2] + 1 and caja[1] - 1 < py < caja[3] + .6 for px, py in puntos):
            return False
        if caja[0] - 3 < spawn[0] < caja[2] + 3 and caja[1] - 3 < spawn[1] < caja[3] + 3:
            return False
        # todo lo que pisa el pie cae en el piso pedido (las orillas también)
        x0, y0, x1, y1 = caja
        for x in list(np.arange(x0 + .02, x1, .25)) + [x1 - .02]:
            for y in list(np.arange(y0 + .02, y1, .25)) + [y1 - .02]:
                t = tile(x, y)
                if t is None or not t.startswith(piso):
                    return False
        return True

    peatones = []
    variantes = list(range(1, N_VARIANTES + 1))
    rng.shuffle(variantes)

    def nuevo(tipo, pts):
        n = len(peatones) + 1
        peatones.append(dict(nombre=f'Peaton_{n}', tipo=tipo, variante=variantes[(n - 1) % N_VARIANTES],
                             puntos=[[round(x, 3), round(y, 3)] for x, y in pts],
                             velocidad=round(rng.uniform(1.0, 1.5), 2), pausa=round(rng.uniform(1.0, 3.0), 2)))
        usados.append(corredor(pts[0], pts[-1]))

    # ---- banquetas: tramos rectos en filas (o columnas) de banqueta sin PuntoEntrega
    celdas = [(c, ROWS - 1 - r) for r in range(ROWS) for c in range(COLS) if suelo[r][c].startswith(BANQUETA_T)]
    intentos = 0
    while sum(p['tipo'] == 'banqueta' for p in peatones) < N_PEATONES_BANQUETA:
        intentos += 1
        assert intentos < 20000, 'no cupieron los peatones de banqueta'
        c, fy = rng.choice(celdas)
        horizontal = rng.random() < .7
        largo = rng.uniform(5, 12)
        y = fy + .35                                # el pie a media fila
        x = c + .5
        if horizontal:
            if fy in filas_punto:
                continue                            # la fila de en medio lleva los PuntoEntrega
            a, b = (x, y), (x + largo * rng.choice((-1, 1)), y)
        else:
            a, b = (x, y), (x, y + largo * rng.choice((-1, 1)))
        if not libre(a, b, 'banqueta'):
            continue
        pts = [a, b]
        if rng.random() < .5:                       # algunos se detienen también a medio camino
            pts = [a, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), b]
        nuevo('banqueta', pts)

    # ---- cruces: de una banqueta a la de enfrente por el centro de una cebra
    cruces = []
    for filas, cols in CRUCES_H:                    # se cruza una calle horizontal: camino vertical
        x = (min(cols) + max(cols) + 1) / 2
        arriba, abajo = ROWS - 1 - (min(filas) - 1), ROWS - 1 - (max(filas) + 1)
        cruces.append(((x, arriba + .35), (x, abajo + .35)))
    for filas, cols in CRUCES_V:                    # se cruza una calle vertical: camino horizontal
        y = ROWS - 1 - (min(filas) + max(filas)) / 2 + .35 - .5
        cruces.append(((min(cols) - .5, y), (max(cols) + 1.5, y)))
    rng.shuffle(cruces)
    motivos = []
    for a, b in cruces:
        if sum(p['tipo'] == 'cruce' for p in peatones) == N_PEATONES_CRUCE:
            break
        # las puntas en banqueta (en las esquinas redondeadas se alargan hasta 2 tiles hacia afuera)
        # y todo lo de en medio en la calle (cebra, asfalto, línea) o en esa misma banqueta
        d = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
        for _ in range(2):
            if not str(tile(*a)).startswith(BANQUETA_T):
                a = (a[0] - d[0], a[1] - d[1])
            if not str(tile(*b)).startswith(BANQUETA_T):
                b = (b[0] + d[0], b[1] + d[1])
        if min(a[1], b[1]) < 3:
            motivos.append((a, b, 'orilla del mapa'))      # en la orilla de abajo casi no se verían
            continue
        if not (str(tile(*a)).startswith(BANQUETA_T) and str(tile(*b)).startswith(BANQUETA_T)):
            motivos.append((a, b, 'puntas', tile(*a), tile(*b)))
            continue
        n = max(2, int(math.dist(a, b) * 4))
        medio = [tile(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n) for i in range(1, n)]
        if not any(str(t).startswith('cebra') for t in medio):
            motivos.append((a, b, 'sin cebra', sorted(set(map(str, medio)))))
            continue
        if not all(str(t).startswith(BANQUETA_T + CALLE_T) for t in medio):
            motivos.append((a, b, 'pisa otra cosa', sorted(set(map(str, medio)))))
            continue
        caja = corredor(a, b)
        if any(se_enciman(caja, o, .05) for o in cajas) or any(se_enciman(caja, u, .4) for u in usados):
            motivos.append((a, b, 'obstáculo'))
            continue
        nuevo('cruce', [a, b])
    assert sum(p['tipo'] == 'cruce' for p in peatones) == N_PEATONES_CRUCE, ('no hay cebras libres suficientes', motivos)
    assert len({p['variante'] for p in peatones}) == N_VARIANTES

    # ---- dueña: fija en la puerta de Brenn's, justo delante de la fachada (se ordena delante del edificio)
    puerta = data['empresa']['puerta']
    base_empresa = next(p['y'] for p in data['props'] if p['nombre'] == 'empresa_brenns')
    duena = dict(x=puerta['x'], y=round(base_empresa - .1, 3), nota="personaje fijo en la puerta de Brenn's")
    return peatones, duena


N_CLIENTAS = 8
ESPERA_HACIA_CASA = 1.2      # tiles desde el PuntoEntrega hacia la puerta, sobre el camino de piedras


def colocar_clientes(data, casas, props):
    """Una clienta por casa: sale de la puerta y espera el pedido en el camino de piedras, 1.2 tiles
    hacia la casa desde su PuntoEntrega. Las 8 variantes se reparten al azar, parejas, y dos casas
    vecinas (lotes seguidos de la misma manzana) nunca llevan la misma. Usa su propio azar."""
    rng = random.Random(8080)
    usos = {v: 0 for v in range(1, N_CLIENTAS + 1)}
    clientes = []
    anterior = {}
    for i, (c, p) in enumerate(zip(casas, data['puntos_entrega'])):
        assert p['casa'] == i + 1
        prohibida = anterior.get(c['manzana'])
        opciones = [v for v in usos if v != prohibida]
        menos = min(usos[v] for v in opciones)
        variante = rng.choice([v for v in opciones if usos[v] == menos])
        usos[variante] += 1
        anterior[c['manzana']] = variante
        # la puerta en el escalón, apenas delante de la fachada para que se dibuje delante de la casa
        puerta = dict(x=c['d'] / T, y=round(a_unity_y(c['house_base']) - .05, 4))
        hacia = (puerta['x'] - p['x'], puerta['y'] - p['y'])
        largo = math.hypot(*hacia)
        espera = dict(x=round(p['x'] + hacia[0] / largo * ESPERA_HACIA_CASA, 4),
                      y=round(p['y'] + hacia[1] / largo * ESPERA_HACIA_CASA, 4))
        clientes.append(dict(nombre=f'Cliente_{i + 1}', casa=i + 1, punto=p['nombre'], variante=variante,
                             puerta=puerta, espera=espera))
    # nadie espera encima de un poste del portón, un buzón o un auto en la cochera
    cajas = cajas_obstaculos(data, props)
    for cl in clientes:
        e = cl['espera']
        pie = (e['x'] - .3, e['y'], e['x'] + .3, e['y'] + .3)
        assert not any(se_enciman(pie, o) for o in cajas), ('la clienta espera sobre un obstáculo', cl)
    # vecinas distintas
    for a, b, ca, cb in zip(clientes, clientes[1:], casas, casas[1:]):
        if ca['manzana'] == cb['manzana']:
            assert a['variante'] != b['variante'], ('vecinas con la misma clienta', a['casa'], b['casa'])
    assert set(usos.values()) <= {3, 4} and all(usos.values()), usos
    return clientes


def preview_entregas(ref, data):
    img = Image.fromarray(ref).resize((W_PX // 2, H_PX // 2), Image.NEAREST)
    dr = ImageDraw.Draw(img)
    for p in data['puntos_entrega']:
        x, y = p['x'] * T / 2, (ROWS - p['y']) * T / 2
        dr.ellipse((x - 9, y - 9, x + 9, y + 9), fill=(255, 111, 165), outline=(80, 20, 50))
        dr.text((x - 4, y - 6), str(p['casa']), fill=(0, 0, 0))
    s = data['spawn']
    x, y = s['x'] * T / 2, (ROWS - s['y']) * T / 2
    dr.rectangle((x - 10, y - 10, x + 10, y + 10), outline=(255, 255, 0), width=3)
    # recorridos de los peatones (cian en banqueta, naranja los que cruzan) y la dueña (morado)
    for p in data.get('peatones', []):
        pts = [(px * T / 2, (ROWS - py) * T / 2) for px, py in p['puntos']]
        col = (40, 220, 255) if p['tipo'] == 'banqueta' else (255, 140, 0)
        dr.line(pts, fill=col, width=4)
        for qx, qy in pts:
            dr.ellipse((qx - 4, qy - 4, qx + 4, qy + 4), fill=col)
        dr.text((pts[0][0] + 5, pts[0][1] - 14), p['nombre'].split('_')[1], fill=(0, 0, 0))
    if 'duena' in data:
        x, y = data['duena']['x'] * T / 2, (ROWS - data['duena']['y']) * T / 2
        dr.rectangle((x - 7, y - 7, x + 7, y + 7), fill=(140, 82, 255), outline=(0, 0, 0))
    img.save(os.path.join(OUT, 'mapa_entregas_grande.png'))


if __name__ == '__main__':
    _, tmeta, tiles = build_tileset()
    tiles = dict(tiles)
    tiles.update(dict(tiles_textura()))
    viejos = build_props()
    extra = build_props_extra()
    props = {**viejos, **extra}       # el atlas se extiende después, cuando ya se saben los largos de los setos

    rng = random.Random(12)
    grid, bajo_bache, lz, ov, tiles_curvas = trazar_suelo(rng, tiles)
    tiles.update(dict(tiles_curvas))
    tiles_cancha = list(court_tiles().items())          # cancha de básquet de la franja sur (5x3 tiles)
    tiles.update(dict(tiles_cancha))
    n_tiles, alto_tileset = extender_tileset(tmeta, tiles, tiles_textura() + tiles_curvas + tiles_cancha)
    comprobar_bordillos(tiles, grid, lz, ov)

    es = Escena(rng, grid)
    empresa = colocar_empresa(es)
    casas = colocar_casas(es)
    colocar_plaza(es)
    colocar_bosque(es)
    colocar_parque(es, lz)
    colocar_camellon(es)
    colocar_calle(es, casas, empresa, lz)
    calle, estacionados = colocar_autos(es, casas, lz, props)
    colocar_vegetacion(es)
    rellenar_manzana_centro(es)
    quitados = colocar_negocios(es, props)
    print('negocios y relleno: se quitaron', len(quitados), 'props sueltos:', sorted(set(quitados)))
    comprobar_fuentes(es, lz)
    movidos, quitados_cebra = cebras.despejar(es.objs, props, lz.cebras, lz.m, MG * T)
    print('cebras:', ', '.join(f"{c['eje']} {min(c['cols'])}-{max(c['cols'])}x{min(c['filas'])}-{max(c['filas'])} "
                               f"{c['rayas']} rayas, {len(c.get('altos', []))} de alto" for c in lz.cebras))
    print('junto a las cebras: movidos', movidos, '| quitados', quitados_cebra)
    setos = cercar_lotes(es, props)
    extra.update(setos['arte'])
    props.update(setos['arte'])
    alto_atlas = extender_atlas(viejos, extra)
    assert set(grid.values()) <= set(tiles), set(grid.values()) - set(tiles)
    assert all(o[1] in props for o in es.objs)
    assert len(casas) == N_CASAS, len(casas)
    usadas = set()
    for rf, lotes in LOTES.values():                      # los lotes caben en pasto y no se enciman
        for k, lado in lotes:
            cs = set(lote_cols(k, lado))
            celdas = {(r, c) for r in range(rf - 8, rf + 1) for c in cs}
            assert not celdas & usadas, ('lotes encimados', k, rf)
            usadas |= celdas
            assert all(0 <= c < COLS and (lz.celda(lz.m, r, c) == PASTO).all() for r, c in celdas), ('lote fuera del pasto', k, rf)

    data = exportar(grid, bajo_bache, es.objs, casas, empresa, props, calle)
    data['peatones'], data['duena'] = colocar_peatones(data, props)
    data['clientes'] = colocar_clientes(data, casas, props)
    comprobar_setos(data, setos)
    data['lotes_solidos'], data['patios'] = exportar_lotes(setos, casas)
    ref = render(tiles, props, grid, es.objs)
    Image.fromarray(ref).save(os.path.join(OUT, 'mapa_referencia_grande.png'))
    guardar_mapa(data, 'mapa_residencial_grande.json')
    print('quitados del atlas por no usarse:', podar_atlas(data))
    preview_entregas(ref, data)
    preview_autos(ref, props, calle)
    print('tileset', n_tiles, 'tiles (', len(tiles_curvas), 'de curvas/esquinas/camellón ) alto', alto_tileset,
          '| atlas 1024 x', alto_atlas, '| mapa', W_PX, 'x', H_PX, '| props:', len(data['props']),
          '| dinamicos:', len(data['dinamicos']), '| casas:', len(casas),
          '| con auto en cochera:', sum(1 for c in casas if c['auto']),
          '| autos estacionados junto al bordillo:', sum(1 for e in estacionados if e['lugar'] != 'cochera'),
          '| autos en la calle:', len(calle), 'en', len({a['carril'] for a in calle}), 'carriles')
