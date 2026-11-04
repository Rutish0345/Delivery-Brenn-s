"""Mapa Residencial grande (128x64 tiles de 32 px = 4096x2048 px) para Unity.

Usa el mismo tileset_suelo que generate_unity.py y el mismo props_atlas, al que se le agregan
al final los props nuevos de props_residencial_extra.py (los que ya existían no se mueven ni cambian).

Trazado (filas desde arriba, columnas desde la izquierda):
  avenida ancha en la fila 11 (banqueta 3 + asfalto 7 + banqueta 3) y calle normal en la fila 39
  calles verticales en las columnas 30 y 84 (banqueta 3 + asfalto 5 + banqueta 3)
  hilera A (filas 0-10): edificio Brenn's en la esquina de arriba a la izquierda y casas
  hilera B (filas 24-38): casas con patio trasero
  hilera C (filas 50-63): parque con lago y áreas verdes

Cada casa tiene cerca baja solo al frente (con portón), rampa de concreto a un costado y,
en más o menos la mitad, un auto en la cochera.

Salidas (unity/residencial/):
  props_atlas.png / props_atlas.json   el atlas de siempre con los props nuevos agregados abajo
  mapa_residencial_grande.json         lo lee Assets/Editor/ConstruirMapa.cs
  mapa_referencia_grande.png           la escena armada completa (vista previa)
  mapa_entregas_grande.png             vista a 1/2 con el spawn y los puntos de entrega marcados
También copia props_atlas.png al proyecto de Unity.
"""
import json
import os
import random
import shutil

import numpy as np
from PIL import Image, ImageDraw

from generate_town import SHADOW
from generate_unity import OUT, T, DINAMICOS, build_tileset, build_props, guardar_mapa
from props_residencial_extra import build_props_extra

ATLAS_UNITY = r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Escenarios\Residencial\props_atlas.png'

COLS, ROWS = 128, 64
W_PX, H_PX = COLS * T, ROWS * T

CALLES_H = [(11, 7), (39, 5)]   # (fila de arriba, filas de asfalto); la primera es la avenida ancha
CALLES_V = [30, 84]             # columna izquierda de cada calle vertical (asfalto de 5)
BANQ = 3                        # ancho de cada banqueta en tiles

MANZANAS_X = [(0, 29), (41, 83), (95, 127)]
HILERAS = [(0, 10), (24, 38)]   # (fila inicial, fila de la cerca del frente)

COLORES_CASA = ['casa_madera', 'casa_crema', 'casa_azul', 'casa_oliva']
COLORES_AUTO = ['rosa', 'morado', 'dorado', 'teal']

# Lotes por (hilera, manzana): (columna de la puerta, lado de la rampa).
# La casa ocupa las columnas k-2..k+2 y la rampa las dos columnas pegadas a ese costado.
LOTES = {
    (0, 0): [(24, 'der')],
    (0, 1): [(45, 'der'), (55, 'der'), (66, 'izq'), (76, 'der')],
    (0, 2): [(99, 'der'), (109, 'der'), (120, 'izq')],
    (1, 0): [(4, 'der'), (14, 'izq'), (24, 'der')],
    (1, 1): [(45, 'der'), (55, 'der'), (66, 'izq'), (76, 'der')],
    (1, 2): [(99, 'der'), (109, 'der'), (120, 'izq')],
}

# Edificio Brenn's: 320x224 px, pegado a la esquina de arriba a la izquierda
EMPRESA = dict(x_izq=32, fila_base=7, carga=(1, 12))      # la zona de carga ocupa esas columnas
PARQUE = dict(c0=52, c1=71, r0=50, r1=63)                 # 20x14 tiles


def ancho_calle(na):
    return 2 * BANQ + na


def filas_calle(t, na):
    return range(t, t + ancho_calle(na))


def es_calle_h(r):
    return any(r in filas_calle(t, na) for t, na in CALLES_H)


def cerca_cruce_x(x_px, margen=48):
    return any(v * T - margen <= x_px <= (v + ancho_calle(5)) * T + margen for v in CALLES_V)


def cerca_cruce_y(r, margen=2):
    return any(t - margen <= r < t + ancho_calle(na) + margen for t, na in CALLES_H)


def casa_cols(k):
    return range(k - 2, k + 3)


def rampa_cols(k, lado):
    return (k + 3, k + 4) if lado == 'der' else (k - 4, k - 3)


# ================================================================ suelo
def trazar_suelo(rng):
    grid = {}
    for r in range(ROWS):
        for c in range(COLS):
            v = rng.random()
            grid[r, c] = 'flores_a' if v < .04 else 'flores_b' if v < .06 else \
                'pasto_a' if v < .55 else 'pasto_b' if v < .82 else 'pasto_c'
    asfalto = lambda: rng.choice(['asfalto_a', 'asfalto_a', 'asfalto_b'])

    for t, na in CALLES_H:
        a0, a1 = t + BANQ, t + BANQ + na - 1
        for c in range(COLS):
            grid[t, c] = grid[t + 1, c] = 'banqueta'
            grid[t + 2, c] = 'banqueta_bordillo_s'
            grid[a0, c] = 'asfalto_sombra_bordillo'
            for r in range(a0 + 1, a1 + 1):
                grid[r, c] = asfalto()
            if not cerca_cruce_x(c * T, margen=64):
                grid[(a0 + a1) // 2 + (na % 2 == 0), c] = 'linea_h'
            grid[a1 + 1, c] = 'banqueta_bordillo_n'
            grid[a1 + 2, c] = grid[a1 + 3, c] = 'banqueta'

    for v in CALLES_V:
        for r in range(ROWS):
            grid[r, v] = grid[r, v + 1] = 'banqueta'
            grid[r, v + 2] = 'banqueta_bordillo_e'
            for c in range(v + 3, v + 8):
                grid[r, c] = asfalto()
            if not cerca_cruce_y(r):
                grid[r, v + 5] = 'linea_v'
            grid[r, v + 8] = 'banqueta_bordillo_w'
            grid[r, v + 9] = grid[r, v + 10] = 'banqueta'

    # cruces: asfalto liso al centro, cebras donde las banquetas cruzan y esquinas con bordillo
    for t, na in CALLES_H:
        a0, a1 = t + BANQ, t + BANQ + na - 1
        b0, b1 = a1 + 1, a1 + 3          # banqueta de abajo
        for v in CALLES_V:
            for r in range(a0, a1 + 1):
                for c in range(v + 3, v + 8):
                    grid[r, c] = asfalto()
            for r in list(range(t, t + 3)) + list(range(b0, b1 + 1)):
                for c in range(v + 3, v + 8):
                    grid[r, c] = 'cebra_v'
            for r in range(a0, a1 + 1):
                for c in list(range(v, v + 3)) + list(range(v + 8, v + 11)):
                    grid[r, c] = 'cebra_h'
            for (rs, cs, rb, cb, nb_r, nb_c, esq) in (
                    (range(t, t + 3), range(v, v + 3), t + 2, v + 2, 'banqueta_bordillo_s', 'banqueta_bordillo_e', 'banqueta_esquina_se'),
                    (range(t, t + 3), range(v + 8, v + 11), t + 2, v + 8, 'banqueta_bordillo_s', 'banqueta_bordillo_w', 'banqueta_esquina_sw'),
                    (range(b0, b1 + 1), range(v, v + 3), b0, v + 2, 'banqueta_bordillo_n', 'banqueta_bordillo_e', 'banqueta_esquina_ne'),
                    (range(b0, b1 + 1), range(v + 8, v + 11), b0, v + 8, 'banqueta_bordillo_n', 'banqueta_bordillo_w', 'banqueta_esquina_nw')):
                for r in rs:
                    for c in cs:
                        grid[r, c] = 'banqueta'
                for c in cs:
                    grid[rb, c] = nb_r
                for r in rs:
                    grid[r, cb] = nb_c
                grid[rb, cb] = esq

    # rampas de concreto: desde la banqueta hasta el costado de cada casa
    for (h, m), lotes in LOTES.items():
        rf = HILERAS[h][1]
        hb = rf - 3                          # fila donde apoya la casa
        for k, lado in lotes:
            ci, cd = rampa_cols(k, lado)
            for r in range(hb - 2, rf + 1):
                grid[r, ci] = 'banqueta_bordillo_w'
                grid[r, cd] = 'banqueta_bordillo_e'
            grid[hb - 2, ci] = 'banqueta_esquina_nw'
            grid[hb - 2, cd] = 'banqueta_esquina_ne'

    # zona de carga del edificio
    c0, c1 = EMPRESA['carga']
    for r in range(EMPRESA['fila_base'] + 1, HILERAS[0][1] + 1):
        for c in range(c0, c1 + 1):
            grid[r, c] = 'banqueta_bordillo_w' if c == c0 else 'banqueta_bordillo_e' if c == c1 else 'banqueta'

    # parque: caminos de losas de piedra y flores
    p = PARQUE
    camino = set()
    for r in range(p['r0'], 57):                 # entrada desde la calle
        camino |= {(r, 61), (r, 62)}
    for c in range(p['c0'], p['c1'] + 1):        # camino de este a oeste
        camino |= {(55, c), (56, c)}
    for r in range(57, p['r1'] + 1):             # bajada hacia el fondo
        camino |= {(r, 66), (r, 67)}
    for rc in camino:
        grid[rc] = 'banqueta'
    for r in range(p['r0'], p['r1'] + 1):
        for c in range(p['c0'], p['c1'] + 1):
            if (r, c) not in camino and rng.random() < .16:
                grid[r, c] = rng.choice(['flores_a', 'flores_b'])

    # grietas fijas y baches (los baches son dinámicos: debajo queda asfalto)
    bajo_bache = {}
    for (r, c, n) in ((16, 12, 'bache_grande'), (19, 70, 'bache_chico'), (44, 20, 'bache_chico'),
                      (45, 110, 'bache_grande'), (26, 88, 'bache_chico'), (56, 34, 'bache_chico'),
                      (15, 52, 'asfalto_grieta'), (43, 66, 'asfalto_grieta'), (20, 118, 'asfalto_grieta'),
                      (3, 36, 'asfalto_grieta'), (58, 90, 'asfalto_grieta')):
        assert grid[r, c].startswith('asfalto') or grid[r, c].startswith('linea'), (r, c, grid[r, c])
        if n.startswith('bache'):
            bajo_bache[r, c] = grid[r, c]
        grid[r, c] = n
    return grid, bajo_bache, camino


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
    autos = 0
    for (h, m), lotes in sorted(LOTES.items()):
        rf = HILERAS[h][1]
        hb = rf - 3
        house_base = (hb + 1) * T - 1
        fence_base = rf * T + 30
        acera = rf + 1                      # primera fila de banqueta de la calle de abajo
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
            # auto en la cochera más o menos en la mitad de las casas
            con_auto = (len(casas) % 2 == 0) != (rng.random() < .2)
            color_auto = None
            if con_auto:
                color_auto = COLORES_AUTO[autos % 4]
                autos += 1
                es.put(f'carro_frontal_{color_auto}', (ci + 1) * T, house_base - 2)
            casas.append(dict(nombre=nombre, k=k, d=d, puerta_base=house_base - 8, acera=acera,
                              rampa=[ci, cd], auto=color_auto))
    return casas


def colocar_empresa(es):
    x_izq, fb = EMPRESA['x_izq'], EMPRESA['fila_base']
    base = (fb + 1) * T - 1
    centro = x_izq + 160
    es.put('empresa_brenns', centro, base)
    es.reservar(range(0, HILERAS[0][1] + 1), range(0, EMPRESA['carga'][1] + 2))
    # zona de carga: cajas frente a la cortina de la bodega, sin tapar la puerta
    es.put('cajas_apiladas', x_izq + 236, base + 46)
    es.put('cajas_apiladas', x_izq + 296, base + 70)
    es.put('caja_carga', x_izq + 262, base + 80)
    es.put('caja_carga', x_izq + 212, base + 92)
    es.put('caja_carga', x_izq + 40, base + 40)
    es.put('cajas_apiladas', x_izq + 70, base + 64)
    return dict(nombre="Brenn's", centro_px=centro, puerta_base=base - 8)


def colocar_parque(es, camino):
    p = PARQUE
    rng = es.rng
    X = lambda c: c * T + T // 2
    B = lambda r: r * T + 28
    es.reservar(range(p['r0'], p['r1'] + 1), range(p['c0'], p['c1'] + 1))
    # setos en la orilla de la calle, con la entrada libre
    for c in range(p['c0'], p['c1'], 2):
        if c + 1 < 60 or c > 63:
            es.put('seto', (c + 1) * T, B(p['r0']) - 4)
    es.put('hidrante', X(59), B(p['r0'] + 1) - 4)
    # lago con orilla de flores
    es.put('lago_parque', 57 * T + 8, B(62))
    for (c, r) in ((53, 57), (61, 58), (52, 62), (62, 62)):
        es.put(rng.choice(['arbusto_flores', 'arbusto_flores_crema']), X(c), B(r))
    # 4 bancas mirando al camino, un bote de basura y faroles
    for c in (54, 58, 66, 70):
        es.put('banca_parque', X(c), B(54) - 2)
    es.put('bote_basura', X(64), B(54) - 4)
    es.put('farol', X(60) - 8, B(54))
    es.put('farol', X(68) + 6, B(58))
    # árboles alrededor
    for (c, r, t) in ((53, 52, 'arbol_grande'), (57, 52, 'arbol_mediano'), (66, 52, 'arbol_grande_b'),
                      (70, 52, 'pino'), (70, 60, 'arbol_grande'), (64, 62, 'arbol_mediano'),
                      (70, 63, 'pino'), (53, 63, 'arbol_mediano')):
        es.put(t, X(c) + rng.randint(-6, 6), B(r) - rng.randint(0, 6))
    for (c, r) in ((63, 51), (69, 57), (64, 59)):
        es.put(rng.choice(['arbusto_flores', 'arbusto_chico', 'arbusto_flores_crema']), X(c), B(r))


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


def colocar_calle(es, casas, empresa):
    """Faroles, señales y autos estacionados junto al bordillo."""
    rng = es.rng
    puertas = [c['d'] for c in casas] + [empresa['centro_px']]
    rampas = [(min(c['rampa']) * T, (max(c['rampa']) + 1) * T) for c in casas]
    carga = (EMPRESA['carga'][0] * T, (EMPRESA['carga'][1] + 1) * T)
    entrada_parque = (60 * T, 64 * T)

    def estorba(x, mitad, top):
        """True si algo de ancho 2*mitad en x tapa una puerta, una rampa o una entrada."""
        if any(abs(x - d) < mitad + 48 for d in puertas) and top:
            return True
        zonas = (rampas + [carga]) if top else [entrada_parque]
        return any(a - mitad - 8 < x < b + mitad + 8 for a, b in zonas)

    # faroles en la orilla exterior de las banquetas
    for i, (t, na) in enumerate(CALLES_H):
        for x in range(176 + (i % 2) * 128, W_PX, 320):
            if not cerca_cruce_x(x, 40) and not estorba(x, 16, True):
                es.put('farol', x, t * T + 8)
        for x in range(336 + (i % 2) * 96, W_PX, 384):
            if not cerca_cruce_x(x, 40) and not estorba(x, 16, False):
                es.put('farol', x, (t + ancho_calle(na)) * T - 3)
    for v in CALLES_V:
        for r in range(2, ROWS, 7):
            if cerca_cruce_y(r, 1) or cerca_cruce_y(r + 3, 1):
                continue
            es.put('farol', v * T + 10, r * T + 28)
            es.put('farol', (v + ancho_calle(5)) * T - 10, (r + 3) * T + 28)
    for (t, na), v in ((CALLES_H[0], CALLES_V[0]), (CALLES_H[1], CALLES_V[1]), (CALLES_H[0], CALLES_V[1])):
        es.put('senal_calle', v * T + 12, t * T + 10)
        es.put('senal_calle', (v + ancho_calle(5)) * T - 12, (t + ancho_calle(na)) * T - 4)

    # autos estacionados (estáticos, con collider): nunca en cebras, esquinas, puertas ni rampas
    baches = {(r, c) for (r, c), n in es.grid.items() if n.startswith('bache')}
    estacionados = []
    k = 0
    for t, na in CALLES_H:
        a0, a1 = t + BANQ, t + BANQ + na - 1
        for top in (True, False):
            base = (a0 + 1) * T + 28 if top else (a1 + 1) * T - 2
            filas = (a0, a0 + 1) if top else (a1 - 1, a1)
            x = 120 + rng.randint(0, 200)
            while x < W_PX - 60:
                libre = not cerca_cruce_x(x, 112) and not estorba(x, 48, top) \
                    and not any((r, c) in baches for r in filas for c in range((x - 48) // T, (x + 48) // T + 1))
                if libre and rng.random() < .55:
                    color = COLORES_AUTO[k % 4]
                    k += 1
                    es.put(f'carro_lateral_{color}', x, base, flip=not top)
                    estacionados.append((color, 'h'))
                    x += 96 + rng.randint(140, 420)
                else:
                    x += 48
    for v in CALLES_V:
        for izq in (True, False):
            cx = (v + 3) * T + 28 if izq else (v + 8) * T - 28
            col = (cx - 26) // T
            r = 3 + rng.randint(0, 3)
            while r < ROWS:
                rs = range(r - 2, r + 1)
                libre = not any(cerca_cruce_y(rr, 2) for rr in rs) \
                    and not any((rr, cc) in baches for rr in rs for cc in (col, col + 1))
                if libre and rng.random() < .5:
                    color = COLORES_AUTO[k % 4]
                    k += 1
                    es.put(f'carro_frontal_{color}', cx, r * T + 28)
                    estacionados.append((color, 'v'))
                    r += 3 + rng.randint(3, 9)
                else:
                    r += 1
    # dos autos que el juego moverá por código (no se pintan en el mapa)
    es.put('auto_lateral_teal', 1500, (CALLES_H[0][0] + BANQ + 4) * T + 28)
    es.put('auto_lateral_oliva', 3000, (CALLES_H[1][0] + BANQ + 2) * T + 28, flip=True)
    return estacionados


# ================================================================ atlas
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
    for s in sprites:
        s['y_unity'] = H - s['y'] - s['h']
    sprites.sort(key=lambda s: s['name'])
    meta.update(alto=H, sprites=sprites)
    Image.fromarray(nuevo).save(os.path.join(OUT, 'props_atlas.png'))
    with open(os.path.join(OUT, 'props_atlas.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    shutil.copyfile(os.path.join(OUT, 'props_atlas.png'), ATLAS_UNITY)
    return H


# ================================================================ render y export
def render(tiles, props, grid, objs):
    m = np.zeros((H_PX, W_PX, 3), np.uint8)
    for (r, c), n in grid.items():
        m[r * T:(r + 1) * T, c * T:(c + 1) * T] = tiles[n]
    for base, name, cx, flip in sorted(objs, key=lambda o: o[0]):
        if name.startswith(DINAMICOS):
            continue
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


def a_unity_y(base_px):
    return (H_PX - (base_px + 1)) / T


def exportar(grid, bajo_bache, objs, casas, empresa, props):
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
        y = a_unity_y(base)
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
        lista_casas.append(dict(id=i + 1, nombre=c['nombre'],
                                puerta=dict(x=c['d'] / T, y=a_unity_y(c['puerta_base'])),
                                entrega=entrega, cochera=dict(columnas=c['rampa'], auto=c['auto'])))
        puntos.append(dict(nombre=f'PuntoEntrega_{i + 1}', casa=i + 1, **entrega))

    acera_empresa = HILERAS[0][1] + 2   # fila del medio de la banqueta de la avenida
    puerta = dict(x=empresa['centro_px'] / T, y=a_unity_y(empresa['puerta_base']))
    spawn = dict(x=puerta['x'], y=ROWS - acera_empresa - 0.5,
                 nota="banqueta de la avenida, frente a la puerta de Brenn's")
    assert suelo[ROWS - 1 - int(spawn['y'])][int(spawn['x'])].startswith('banqueta')
    return dict(columnas=COLS, filas=ROWS, tile=T,
                coordenadas='tiles; origen abajo-izquierda, y hacia arriba; '
                            'props/autos = pivote abajo-centro, baches = celda',
                suelo_orden='suelo[0] es la fila de arriba',
                spawn=spawn, empresa=dict(nombre=empresa['nombre'], puerta=puerta),
                casas=lista_casas, puntos_entrega=puntos,
                suelo=suelo, props=lista, dinamicos=dinamicos)


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
    img.save(os.path.join(OUT, 'mapa_entregas_grande.png'))


if __name__ == '__main__':
    _, tmeta, tiles = build_tileset()
    viejos = build_props()
    extra = build_props_extra()
    alto_atlas = extender_atlas(viejos, extra)
    props = {**viejos, **extra}

    rng = random.Random(12)
    grid, bajo_bache, camino = trazar_suelo(rng)
    es = Escena(rng, grid)
    empresa = colocar_empresa(es)
    casas = colocar_casas(es)
    colocar_parque(es, camino)
    estacionados = colocar_calle(es, casas, empresa)
    colocar_vegetacion(es)
    assert set(grid.values()) <= {t['name'] for t in tmeta}
    assert all(o[1] in props for o in es.objs)

    data = exportar(grid, bajo_bache, es.objs, casas, empresa, props)
    ref = render(tiles, props, grid, es.objs)
    Image.fromarray(ref).save(os.path.join(OUT, 'mapa_referencia_grande.png'))
    guardar_mapa(data, 'mapa_residencial_grande.json')
    preview_entregas(ref, data)
    print('atlas 1024 x', alto_atlas, '| mapa', W_PX, 'x', H_PX, '| props:', len(data['props']),
          '| dinamicos:', len(data['dinamicos']), '| casas:', len(casas),
          '| con auto en cochera:', sum(1 for c in casas if c['auto']),
          '| autos estacionados:', len(estacionados))
