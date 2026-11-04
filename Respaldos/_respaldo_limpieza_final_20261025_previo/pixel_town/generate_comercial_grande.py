"""Mapa Comercial grande del nivel 2 (128x64 tiles de 32 px, como el Residencial) para Unity, octubre 2026.

Mismo pipeline que generate_residencial_grande.py: raster de materiales a 1 px -> bordillos con la misma regla
-> tiles (los de siempre donde coinciden, nuevos donde no) -> cebras pintadas por píxel (cebras.py) -> props ->
JSON que lee Assets/Editor/ConstruirMapaComercial.cs (a través de ConstruirMapa con el perfil Comercial).
No toca nada del nivel 1: escribe su propio tileset (tileset_comercial) y su propio atlas (props_comercial).

Trazado aprobado (filas desde arriba, columnas desde la izquierda):
  Av. Comercio    filas 24-39: banqueta 3 (24-26) + dos carriles al oeste de 2 tiles (27-28, 29-30) + camellón 2
                  (31-32) + dos carriles al este (33-34, 35-36) + banqueta 3 (37-39). 3+4+2+4+3 = 16 filas.
  Calle Roble     columnas 38-48 (banqueta 38-40, asfalto 41-45, banqueta 46-48), de orilla a orilla.
  Calle Cedro     columnas 84-94 (banqueta 84-86, asfalto 87-91, banqueta 92-94), de orilla a orilla.
  Cuatro cruces con semáforo sobre la avenida: los dos cruces con las transversales y dos pasos a media cuadra
  (columnas 17-19 y 108-110). Solo ahí se corta el camellón.
  Norte: 8 negocios mirando a la avenida (3 + 2 + 3), un pasaje de banqueta hacia el estacionamiento de atrás
  (filas 2-15, con cajones) que tiene entradas desde las transversales.
  Sur: 3 negocios abajo a la izquierda mirando a la banqueta de la orilla de abajo (filas 61-63), con su
  estacionamiento detrás; la plaza al centro (fuente, bancas, jardineras, faroles y dos kioscos); y un
  estacionamiento grande abajo a la derecha con la camioneta de reparto.
  Lo único que se pisa es calle, banqueta, estacionamiento y plaza: el resto (pasto, jardines, setos, el
  camellón y el suelo de los negocios) va en "lotes_solidos" (sólido a pie y en moto).

Salidas (unity/comercial/):
  tileset_comercial.png / .json      solo los tiles que usa el mapa (copia en Assets/Tiles)
  props_comercial.png / .json        solo los props que usa el mapa + autos y semáforos (copia en Assets/Sprits)
  mapa_comercial.json                lo lee ConstruirMapaComercial.cs
  mapa_referencia_comercial.png, mapa_entregas_comercial.png, mapa_autos_comercial.png   vistas previas
"""
import json
import math
import os
import random
import shutil

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from generate_town import P
import generate_residencial_grande as R
from generate_residencial_grande import (Lienzo, bordillos, firmas_canonicas, PASTO, BANQ_M, ASFALTO, ADOQUIN, LOSA,
                                         TEXTURA, COLOR_BORDE, B_HOLE_D, B_ROAD_D, MG, COLS, ROWS, W_PX, H_PX,
                                         cuerpo_auto, caja_auto, se_enciman, PIE)
import cebras
import turbos
from props_negocios import props_comercial, PIVOTES
from props_comercial_grande import build_props_comercial_grande
from props_baches import build_baches, TAMANOS as TAM_BACHE

T = 32
AQUI = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(AQUI, 'unity', 'residencial')
OUT = os.path.join(AQUI, 'unity', 'comercial')
UNITY = r'C:\Users\kangu\DeliveryBrenns'
TILESET_UNITY = os.path.join(UNITY, 'Assets', 'Tiles', 'tileset_comercial.png')
ATLAS_UNITY = os.path.join(UNITY, 'Assets', 'Sprits', 'Escenarios', 'Comercial', 'props_comercial.png')
TILESET, ATLAS, MAPA = 'tileset_comercial', 'props_comercial', 'mapa_comercial.json'

# ---------------------------------------------------------------- trazado (en tiles, filas desde arriba)
BANQ_N, CARRIL_O, CAMELLON, CARRIL_E, BANQ_S = (24, 27), (27, 31), (31, 33), (33, 37), (37, 40)   # [a, b)
CALLES_V = {'roble': 38, 'cedro': 84}      # banqueta c..c+2, asfalto c+3..c+7, banqueta c+8..c+10
MEDIA_CUADRA = {'oeste': 17, 'este': 108}  # columnas c..c+2 del paso a media cuadra sobre la avenida
BANQ_ABAJO = 61                            # banqueta de la orilla de abajo (filas 61-63)
RADIO_ESQUINA = 2
NOMBRES_CALLE = {'comercio': 'Av. Comercio', 'roble': 'Calle Roble', 'cedro': 'Calle Cedro'}


def asfalto_v(c):
    return c + 3, c + 8                    # [a, b)


ESQUINAS = []
for _c in CALLES_V.values():
    a, b = asfalto_v(_c)
    ESQUINAS += [(a, BANQ_N[1], -1, -1), (b, BANQ_N[1], 1, -1), (a, BANQ_S[0], -1, 1), (b, BANQ_S[0], 1, 1)]

# Estacionamientos: asfalto c0..c1 x r0..r1 (incluidos), filas de cajones, entradas (columnas, filas) por la
# banqueta de la transversal y columnas sin cajones (por donde llega un pasaje)
LOTES = [
    dict(nombre='noroeste', c0=1, c1=36, r0=2, r1=15, cajones=[(2, 4), (9, 11)],
         entradas=[((37, 40), (5, 8)), ((37, 40), (12, 15))], sin_cajon=()),
    dict(nombre='norte_centro', c0=50, c1=82, r0=2, r1=15, cajones=[(2, 4), (9, 11)],
         entradas=[((46, 49), (5, 8)), ((46, 49), (12, 15)), ((83, 86), (5, 8)), ((83, 86), (12, 15))], sin_cajon=()),
    dict(nombre='noreste', c0=96, c1=126, r0=2, r1=15, cajones=[(2, 4), (9, 11)],
         entradas=[((92, 95), (5, 8)), ((92, 95), (12, 15))], sin_cajon=()),
    dict(nombre='suroeste', c0=1, c1=36, r0=41, r1=52, cajones=[(41, 43), (50, 52)],
         entradas=[((37, 40), (45, 48))], sin_cajon=(16, 21)),
    dict(nombre='sureste', c0=96, c1=126, r0=41, r1=59, cajones=[(41, 43), (49, 51), (57, 59)],
         entradas=[((92, 95), (44, 48)), ((92, 95), (52, 56))], sin_cajon=()),
]
# pasajes de banqueta entre negocios: (columnas, filas), incluidos
PASAJES = [((17, 19), (16, 23)), ((63, 65), (16, 23)), ((108, 110), (16, 23)), ((17, 19), (53, 60)), ((17, 19), (40, 40))]
PLAZA = dict(c0=49, c1=83, r0=40, r1=60)
JARDINES_PLAZA = [(50, 53, 41, 42), (79, 82, 41, 42), (50, 53, 58, 59), (79, 82, 58, 59)]   # c0, c1, r0, r1

# Negocios: (sprite, columna izquierda, fila de la base, nombre). El sprite mide 192 px = 6 tiles.
NEGOCIOS = [
    ('tienda_farmacia', 1, 23, 'Farmacia del Centro'),
    ('tienda_salon_lola', 10, 23, 'Estética Lola'),
    ('tienda_panaderia', 21, 23, 'Panadería La Espiga'),
    ('tienda_floreria', 56, 23, 'Florería Jazmín'),
    ('tienda_salon_brenns', 67, 23, "Salón Brenn's"),
    ('tienda_panaderia_b', 101, 23, 'Panadería San Juan'),
    ('tienda_salon_perla', 112, 23, 'Salón Perla'),
    ('tienda_farmacia_b', 121, 23, 'Farmacia Roble'),
    ('tienda_floreria_b', 1, 60, 'Florería Las Rosas'),
    ('tienda_salon_rosy', 10, 60, 'Estética Rosy'),
    ('tienda_farmacia_c', 21, 60, 'Farmacia Cedro'),
]
EMPRESA = 'tienda_salon_brenns'
PUERTA_DX = (136 + 164) / 2 / T - 3        # centro de la puerta (columnas 136-163 del sprite) desde el centro
# Baches (Bache.cs): posiciones fijas en el centro de un carril (el otro carril del mismo sentido queda libre para
# esquivarlo), a 2 tiles o más de los cruces, las cebras, los semáforos y los cajones. (tamaño, x, y) en Unity.
BACHES = [
    ('grande', 8.0, 36.0), ('chico', 29.5, 30.0), ('chico', 60.0, 34.0), ('grande', 73.5, 28.0),
    ('chico', 100.5, 36.0), ('grande', 121.0, 30.0), ('chico', 42.25, 8.0), ('grande', 88.25, 11.0),
]
DISTANCIA_BACHE = 2.0

KIOSCOS = [('kiosco_cafe', 57.0, 17.0, 'Café de la Plaza'), ('kiosco_flores', 76.0, 17.0, 'Flores de la Plaza')]
FUENTE = (66.5, 9.0)
# quién atiende cada negocio (en el orden de NEGOCIOS sin Brenn's, luego los kioscos); vecinas distintas
DUENOS = ['clienta_1', 'clienta_2', 'peaton_2', 'clienta_3', 'clienta_4', 'clienta_5', 'peaton_4',
          'clienta_6', 'clienta_7', 'clienta_8', 'peaton_2', 'clienta_8']
VARIANTES_PEATON = [1, 3, 5, 6]            # los de la calle; 2 y 4 son dueños de negocio

# Variantes de color de las tiendas repetidas: rampa del muro -> otra rampa de la paleta
VARIANTES = {
    'tienda_farmacia_b': ('tienda_farmacia', {'blue_l': 'salvia_l', 'blue': 'salvia', 'blue_d': 'salvia_d'}),
    'tienda_farmacia_c': ('tienda_farmacia', {'blue_l': 'lila_l', 'blue': 'lila', 'blue_d': 'lila_d'}),
    'tienda_floreria_b': ('tienda_floreria', {'olive_l': 'beige_l', 'olive': 'beige', 'olive_d': 'beige_d'}),
    'tienda_panaderia_b': ('tienda_panaderia', {'wood_l': 'barro_l', 'wood': 'barro', 'wood_d': 'barro_d'}),
}

# Cruces de la avenida y de las transversales (filas, columnas) para cebras.py
CRUCES_H = [(range(*CARRIL_O[:1] + CARRIL_E[1:]), range(c, c + 3)) for c in MEDIA_CUADRA.values()] + \
           [(range(CARRIL_O[0], CARRIL_E[1]), range(c, c + 3)) for v in CALLES_V.values() for c in (v, v + 8)]
CRUCES_V = [(range(*f), range(*asfalto_v(v))) for v in CALLES_V.values() for f in (BANQ_N, BANQ_S)]


def unity_y(fila):
    """Orilla de abajo de la fila del generador, en y de Unity."""
    return ROWS - 1 - fila


# Zonas de cruce de los autos (Unity: x, y abajo-izquierda, ancho, alto). En los cruces con las transversales la
# zona abarca también las cebras, así los autos se detienen antes de pisarlas.
ZONAS = {}
for _n, _c in CALLES_V.items():
    ZONAS[f'cruce_{_n}'] = (_c, unity_y(BANQ_S[1] - 1), 11, BANQ_S[1] - BANQ_N[0])
for _n, _c in MEDIA_CUADRA.items():
    ZONAS[f'paso_{_n}'] = (_c, unity_y(CARRIL_E[1] - 1), 3, CARRIL_E[1] - CARRIL_O[0])


def carriles_cruce(c):
    """Carriles que atraviesa cada cebra, para su línea de alto (como en el Residencial): (desde, hasta) en px a lo
    ancho de la calle, sentido (+1 si los autos van hacia x o y creciente en px) y la orilla del lado de la banqueta."""
    if c['eje'] == 'h':
        return [(CARRIL_O[0] * T, CARRIL_O[1] * T, -1, CARRIL_O[0] * T),
                (CARRIL_E[0] * T, CARRIL_E[1] * T, 1, CARRIL_E[1] * T)]
    a, b = asfalto_v(min(v for v in CALLES_V.values() if v <= min(c['cols'])))
    mitad = (a + b) * T // 2
    return [(a * T, mitad - 2, 1, a * T), (mitad + 2, b * T, -1, b * T)]


# ================================================================ arte de origen
def cargar_tiles():
    meta = json.load(open(os.path.join(RES, 'tileset_suelo.json'), encoding='utf-8'))
    img = np.asarray(Image.open(os.path.join(RES, 'tileset_suelo.png')).convert('RGB'))
    return {t['name']: img[t['y']:t['y'] + T, t['x']:t['x'] + T].copy() for t in meta['tiles']}, [t['name'] for t in meta['tiles']]


def cargar_props():
    meta = json.load(open(os.path.join(RES, 'props_atlas.json'), encoding='utf-8'))
    img = np.asarray(Image.open(os.path.join(RES, 'props_atlas.png')).convert('RGBA'))
    props = {s['name']: img[s['y']:s['y'] + s['h'], s['x']:s['x'] + s['w']].copy() for s in meta['sprites']}
    pivotes = {s['name']: s['pivote'] for s in meta['sprites'] if 'pivote' in s}
    com = props_comercial()
    for n in ('tienda_farmacia', 'tienda_floreria', 'tienda_panaderia', 'tienda_salon_belleza'):
        props[n] = com[n]
    props.update(build_props_comercial_grande(com['tienda_salon_belleza']))
    for nuevo, (base, mapa) in VARIANTES.items():
        a = props[base].copy()
        for de, a_c in mapa.items():
            sel = np.all(a[:, :, :3] == P[de], axis=2) & (a[:, :, 3] == 255)
            a[sel, :3] = P[a_c]
        assert not np.array_equal(a, props[base]), nuevo
        props[nuevo] = a
    baches, piv_baches = build_baches()
    props.update(baches)
    pivotes.update(piv_baches)
    return props, pivotes


# ================================================================ raster
def trazar_raster():
    lz = Lienzo()
    lz.rect(-MG, BANQ_N[0], COLS + MG, BANQ_S[1], BANQ_M)
    lz.rect(-MG, BANQ_ABAJO, COLS + MG, ROWS + MG, BANQ_M)
    for c in CALLES_V.values():
        lz.rect(c, -MG, c + 11, ROWS + MG, BANQ_M)
    lz.rect(-MG, CARRIL_O[0], COLS + MG, CARRIL_E[1], ASFALTO)
    for c in CALLES_V.values():
        lz.rect(*asfalto_v(c)[:1], -MG, asfalto_v(c)[1], ROWS + MG, ASFALTO)
    cortes = sorted(asfalto_v(c) for c in CALLES_V.values())
    x = -MG
    for a, b in cortes + [(COLS + MG, None)]:
        lz.rect(x, CAMELLON[0], a, CAMELLON[1], PASTO)       # el camellón solo se corta en las transversales
        x = b
    for e in ESQUINAS:
        lz.esquina(*e, RADIO_ESQUINA)
    for l in LOTES:
        lz.rect(l['c0'], l['r0'], l['c1'] + 1, l['r1'] + 1, ASFALTO)
        for (ca, cb), (ra, rb) in l['entradas']:
            lz.rect(ca, ra, cb + 1, rb + 1, ASFALTO)
    for (ca, cb), (ra, rb) in PASAJES:
        lz.rect(ca, ra, cb + 1, rb + 1, BANQ_M)
    p = PLAZA
    lz.rect(p['c0'], p['r0'], p['c1'] + 1, p['r1'] + 1, LOSA)
    for c0, c1, r0, r1 in JARDINES_PLAZA:
        lz.rect(c0, r0, c1 + 1, r1 + 1, PASTO)
    return lz


def en_lote(r, c):
    for l in LOTES:
        if l['c0'] <= c <= l['c1'] and l['r0'] <= r <= l['r1']:
            return True
        if any(ca <= c <= cb and ra <= r <= rb for (ca, cb), (ra, rb) in l['entradas']):
            return True
    return False


def prefijo_tile(r, c, bm):
    if (bm == LOSA).any():
        return 'plaza'
    if CAMELLON[0] <= r < CAMELLON[1]:
        return 'camellon'
    if en_lote(r, c):
        return 'estacionamiento'
    if any(x - 3 <= c < x + 3 and y - 3 <= r < y + 3 for x, y, _, _ in ESQUINAS):
        return 'esquina'
    return 'orilla'


def trazar_suelo(rng, tiles):
    """Igual que el Residencial: celdas lisas con los tiles de siempre, las de bordillo recto con el tile canónico
    y el resto como tiles nuevos; luego cebras, líneas de carril, línea central de las transversales y cajones."""
    lz = trazar_raster()
    ov = bordillos(lz.m)
    firmas = firmas_canonicas(tiles)
    cls = lz.m.astype(np.uint16) * 16 + ov
    grid, nuevos, tiles_nuevos, cuenta = {}, {}, [], {}
    for r in range(ROWS):
        for c in range(COLS):
            bm, bo = lz.celda(lz.m, r, c), lz.celda(ov, r, c)
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
                else:
                    grid[r, c] = 'plaza_losa'
                continue
            f = lz.celda(cls, r, c).tobytes()
            if f in firmas:
                grid[r, c] = firmas[f]
                continue
            a = np.zeros((T, T, 3), np.uint8)
            for v, nombre in TEXTURA.items():
                a[bm == v] = tiles[nombre][bm == v]
            for k, col in COLOR_BORDE.items():
                a[bo == k] = P[col]
            clave = a.tobytes()
            if clave not in nuevos:
                pref = prefijo_tile(r, c, bm)
                cuenta[pref] = cuenta.get(pref, 0) + 1
                nuevos[clave] = f'{pref}_{cuenta[pref]:02d}'
                tiles_nuevos.append((nuevos[clave], a))
            grid[r, c] = nuevos[clave]

    todos = dict(tiles)
    todos.update(dict(tiles_nuevos))
    nuevos_cebra, lz.cebras = cebras.pintar(grid, todos, lz, ov, bordillos, carriles_cruce, dict(
        ASFALTO=ASFALTO, ADOQUIN=ADOQUIN, PASTO=PASTO, BANQ_M=BANQ_M, TEXTURA=TEXTURA, COLOR_BORDE=COLOR_BORDE,
        SOMBRAS={B_HOLE_D: .7, B_ROAD_D: .84}, OSCURO_ADOQUIN=((B_HOLE_D, .7), (B_ROAD_D, .84)), MG=MG,
        lista=cebras.cruces(CRUCES_V, CRUCES_H)))
    tiles_nuevos += nuevos_cebra
    todos.update(dict(nuevos_cebra))

    # línea central de las transversales (lejos de la avenida y de las entradas de los estacionamientos)
    for c in CALLES_V.values():
        a, b = asfalto_v(c)
        col = (a + b) // 2
        for r in range(ROWS):
            if BANQ_N[0] - 3 <= r < BANQ_S[1] + 3 or grid[r, col] not in ('asfalto_a', 'asfalto_b'):
                continue
            grid[r, col] = 'linea_v'

    pintura = {}

    def pinta(x, y, color):
        r, c = y // T, x // T
        if 0 <= r < ROWS and 0 <= c < COLS:
            pintura.setdefault((r, c), []).append((y % T, x % T, P[color]))

    # líneas discontinuas entre los dos carriles de cada sentido (no en los cruces ni junto a las cebras)
    libres = [(min(cs) - 2, max(cs) + 3) for _, cs in CRUCES_H] + [(c - 1, c + 12) for c in CALLES_V.values()]
    for y in (CARRIL_O[0] + 2, CARRIL_E[0] + 2):
        for x in range(W_PX):
            if (x % T) >= 18 or any(a * T <= x < b * T for a, b in libres):
                continue
            for yy in (y * T - 1, y * T):
                if grid[yy // T, x // T].startswith('asfalto'):
                    pinta(x, yy, 'stripe')
    # cajones: separadores de 2 px cada 2 tiles en las filas de cajones (no en los extremos, que son pasillo)
    lugares = []
    for l in LOTES:
        for ra, rb in l['cajones']:
            xs = [x for x in range(l['c0'] + 3, l['c1'] - 3, 2)
                  if not (l['sin_cajon'] and l['sin_cajon'][0] <= x + 1 and x <= l['sin_cajon'][1])]
            for x in xs:
                lugares.append(dict(lote=l['nombre'], x=x, filas=(ra, rb)))
            for x in sorted({x for x in xs} | {x + 2 for x in xs}):
                for y in range(ra * T + 3, (rb + 1) * T - 3):
                    pinta(x * T - 1, y, 'stripe')
                    pinta(x * T, y, 'stripe')
    por_bytes = {}
    for (r, c), pts in sorted(pintura.items()):
        assert not grid[r, c].startswith(('cebra', 'linea_alto')), (r, c, grid[r, c])
        a = todos[grid[r, c]].copy()
        for yy, xx, rgb in pts:
            a[yy, xx] = rgb
        pref = 'cajon' if en_lote(r, c) else 'carril'
        k = (pref, a.tobytes())
        if k not in por_bytes:
            cuenta[pref] = cuenta.get(pref, 0) + 1
            por_bytes[k] = f'{pref}_{cuenta[pref]:02d}'
            tiles_nuevos.append((por_bytes[k], a))
            todos[por_bytes[k]] = a
        grid[r, c] = por_bytes[k]
    return grid, lz, ov, tiles_nuevos, lugares


# ================================================================ props
class Escena(R.Escena):
    def u(self, name, x, y, flip=False):
        """Pone un prop con su pivote (abajo-centro) en (x, y) de Unity."""
        self.put(name, round(x * T), H_PX - 1 - round(y * T), flip)


def colocar_negocios(es, props):
    negocios = []
    for sprite, a, fila, nombre in NEGOCIOS:
        cx = a + 3
        es.put(sprite, cx * T, (fila + 1) * T - 1)
        norte = fila < BANQ_N[0]
        puerta = dict(x=round(cx + PUERTA_DX, 4), y=round(unity_y(fila) - .05, 4))
        # el punto va en la fila del medio de la banqueta de enfrente
        entrega = dict(x=puerta['x'], y=unity_y(BANQ_N[0] + 1) + .5 if norte else unity_y(BANQ_ABAJO + 1) + .5)
        negocios.append(dict(sprite=sprite, nombre=nombre, puerta=puerta, entrega=entrega, lado='norte' if norte else 'sur',
                             cols=(a, a + 5), fila=fila))
    for sprite, x, y, nombre in KIOSCOS:
        es.u(sprite, x, y)
        negocios.append(dict(sprite=sprite, nombre=nombre, puerta=dict(x=x + 1.8, y=y + .2),
                             entrega=dict(x=x, y=y - 1.5), lado='sur', cols=None, fila=None))
    return negocios


def colocar_vegetacion(es, rng, props, lugares):
    """Setos en las franjas de pasto que cierran los estacionamientos, jardines entre los negocios, árboles en el
    camellón, la plaza y los autos estacionados."""
    g = es.grid
    def pasto(r, c):
        return 0 <= r < ROWS and 0 <= c < COLS and g[r, c].startswith(('pasto', 'flores'))
    ocup = set()
    for sprite, a, fila, _ in NEGOCIOS:                       # el suelo de cada negocio
        ocup |= {(r, c) for r in range(fila - 5, fila + 1) for c in range(a, a + 6)}
    # franjas horizontales de 2 o 1 fila: setos de 2 tiles
    for r in list(range(ROWS)):
        c = 0
        while c < COLS - 1:
            if pasto(r, c) and pasto(r, c + 1) and (r, c) not in ocup and (r, c + 1) not in ocup and \
                    not pasto(r + 1, c) and (r + 1, c) not in ocup and not (pasto(r - 1, c) and pasto(r - 2, c) and pasto(r - 3, c)):
                es.put('seto', (c + 1) * T, (r + 1) * T - 3)
                c += 2
            else:
                c += 1
    # franjas verticales de 1 columna: arbustos chicos
    for c in range(COLS):
        for r in range(1, ROWS - 1, 1):
            if pasto(r, c) and not pasto(r, c - 1) and not pasto(r, c + 1) and r % 2 == 0:
                es.put('arbusto_chico', c * T + 16, (r + 1) * T - 4)
    # jardines entre negocios y a los lados (filas de la fachada): árboles atrás, arbustos y flores al frente
    for sprite, a, fila, _ in NEGOCIOS:
        pass
    for fila in (23, 60):
        c = 0
        while c < COLS:
            if pasto(fila, c) and (fila, c) not in ocup:
                c1 = c
                while c1 + 1 < COLS and pasto(fila, c1 + 1) and (fila, c1 + 1) not in ocup:
                    c1 += 1
                w = c1 - c + 1
                if w >= 3:
                    for k, x in enumerate(np.arange(c + 1.5, c1 + .6, 3.0)):
                        es.u(rng.choice(['arbol_mediano', 'arbol_grande_b', 'arbol_mediano', 'pino']), x, unity_y(fila) + 2.4)
                        es.u(rng.choice(['arbusto_flores', 'arbusto_flores_crema', 'arbusto_grande']), x + (0.6 if k % 2 else -0.4), unity_y(fila) + .25)
                else:
                    es.u('arbusto_chico', (c + c1 + 1) / 2, unity_y(fila) + .3)
                c = c1 + 1
            else:
                c += 1
    # camellón: árboles cada ~5 tiles, lejos de los cruces
    y = unity_y(CAMELLON[1] - 1) + .35
    lejos = [(min(cs) - 3, max(cs) + 4) for _, cs in CRUCES_H] + [(c - 1, c + 12) for c in CALLES_V.values()]
    x = 2.5
    while x < COLS - 1:
        if not any(a <= x <= b for a, b in lejos):
            es.u(rng.choice(['arbol_mediano', 'arbol_mediano', 'arbol_grande_b']), x + rng.uniform(-.15, .15), y)
        x += 5
    # jardines de la plaza: arbustos y flores
    for c0, c1, r0, r1 in JARDINES_PLAZA:
        for k, xx in enumerate(np.arange(c0 + .8, c1 + 1, 1.4)):
            es.u(['arbusto_flores', 'arbusto_grande', 'arbusto_flores_crema'][k % 3], xx, unity_y(r1) + .2 + (k % 2) * .5)
    # autos estacionados en ~45 % de los cajones
    colores = list(R.COLORES_AUTO)
    estacionados = []
    for lg in lugares:
        if rng.random() > .45:
            continue
        ra, rb = lg['filas']
        vista = rng.choice(['frontal', 'trasero'])
        color = colores[len(estacionados) % len(colores)]
        nombre = f'carro_{vista}_{color}'
        es.u(nombre, lg['x'] + 1, unity_y(rb) + .1)
        estacionados.append(dict(nombre=nombre, x=lg['x'] + 1, y=unity_y(rb) + .1))
    rng.shuffle(colores)
    # la camioneta de reparto, estacionada como obstáculo en el pasillo de abajo del estacionamiento grande
    es.u('camioneta_reparto', 122.0, unity_y(56) + .05)
    return estacionados


def colocar_plaza(es):
    p = PLAZA
    es.u('fuente_plaza', *FUENTE)
    for x in (62.0, 71.0):
        es.u('banca_parque', x, FUENTE[1] + 3.6)
    es.u('banca_parque', 60.0, 4.2)
    es.u('banca_parque', 73.0, 4.2)
    for x, y in ((53.0, 6.0), (80.0, 6.0), (61.0, 20.6), (72.0, 20.6)):
        es.u('arbol_mediano', x, y)
    for x, y in ((p['c0'] + .5, unity_y(p['r0']) + .7), (p['c1'] + .5, unity_y(p['r0']) + .7),
                 (p['c0'] + .5, unity_y(p['r1']) + .15), (p['c1'] + .5, unity_y(p['r1']) + .15), (66.5, 20.6)):
        es.u('farol', x, y)
    for x, y in ((64.4, 17.0), (68.6, 17.0)):
        es.u('jardinera_flores', x, y)
    es.u('bote_basura', 59.4, 13.8)
    es.u('bote_basura', 74.2, 13.8)


def colocar_calle(es, negocios):
    """Faroles en las banquetas (lejos de los cruces, las puertas y los puntos) y letreros con el nombre de cada calle."""
    puntos = [n['entrega']['x'] for n in negocios if n['lado'] == 'norte']
    puntos_s = [n['entrega']['x'] for n in negocios if n['lado'] == 'sur' and n['cols']]
    cruces = [(min(cs) - 1.5, max(cs) + 2.5) for _, cs in CRUCES_H] + [(c - 1, c + 12) for c in CALLES_V.values()]
    pasajes = [(ca - .5, cb + 1.5) for (ca, cb), (ra, rb) in PASAJES]

    def libre(x, cerca):
        return not any(a <= x <= b for a, b in cruces) and not any(abs(x - p) < 2.6 for p in cerca)
    for x in np.arange(3.0, COLS - 1, 9.0):
        if libre(x, puntos):
            es.u('farol', x, unity_y(BANQ_N[1] - 1) + .12)          # junto al bordillo de la banqueta norte
    for x in np.arange(7.5, COLS - 1, 9.0):
        if libre(x, []) and not PLAZA['c0'] - 1 <= x <= PLAZA['c1'] + 1:
            es.u('farol', x, unity_y(BANQ_S[0]) + .72)              # banqueta sur, junto al bordillo
    for x in np.arange(4.0, COLS - 1, 10.0):
        if libre(x, puntos_s) and not any(a <= x <= b for a, b in pasajes) and not PLAZA['c0'] - 1 <= x <= PLAZA['c1'] + 1:
            es.u('farol', x, unity_y(BANQ_ABAJO) + .72)             # banqueta de la orilla de abajo
    for c in CALLES_V.values():                                    # transversales, lejos de la avenida y las entradas
        entradas = [rr for l in LOTES for (ca, cb), rr in l['entradas'] if ca <= c + 10 and cb >= c]
        for r in range(4, ROWS - 3, 7):
            if BANQ_N[0] - 3 <= r <= BANQ_S[1] + 2 or any(ra - 1 <= r <= rb + 1 for ra, rb in entradas):
                continue
            es.u('farol', c + .35, unity_y(r) + .2)
            es.u('farol', c + 10.65, unity_y(r) + .2)
    for clave, x, y in (('comercio', 35.4, unity_y(BANQ_N[0]) + .3), ('roble', 39.3, unity_y(BANQ_N[0] - 4) + .3),
                        ('comercio', 97.6, unity_y(BANQ_N[0]) + .3), ('cedro', 93.7, unity_y(BANQ_N[0] - 4) + .3),
                        ('roble', 47.7, unity_y(BANQ_S[1] + 3) + .3), ('cedro', 85.3, unity_y(BANQ_S[1] + 3) + .3),
                        ('comercio', 50.6, unity_y(BANQ_S[0]) + .72)):
        es.u(f'letrero_calle_{clave}', x, y)


# ================================================================ autos
VEL = {'avenida': (2.6, 2.9), 'transversal': (2.3, 2.6)}
CUERPO_LADO, CUERPO_VERT = (2.875, 1.5), (1.375, 2.9375)
CAMARA = (10.5, 6.5)        # media vista de la cámara (tamaño 5, 16:9) con margen: ahí no arranca ningún auto


def rutas_autos(spawn, rng):
    f = 8.0
    rutas = []
    def ruta(carril, puntos, autos, cruces, vel):
        rutas.append(dict(carril=carril, principal=True, recta=True, velocidad=round(rng.uniform(*vel), 2), autos=autos,
                          puntos=[list(p) for p in puntos], cruces=cruces))
    todas = list(ZONAS)
    for k, y in enumerate((unity_y(CARRIL_O[0]), unity_y(CARRIL_O[0] + 2))):        # al oeste: y 36 y 34
        ruta(f'comercio_oeste_{k + 1}', [(COLS + f, y), (-f, y)], 5, todas, VEL['avenida'])
    for k, y in enumerate((unity_y(CARRIL_E[0]), unity_y(CARRIL_E[0] + 2))):        # al este: y 30 y 28
        ruta(f'comercio_este_{k + 1}', [(-f, y), (COLS + f, y)], 5, todas, VEL['avenida'])
    for n, c in CALLES_V.items():
        a, b = asfalto_v(c)
        ruta(f'{n}_baja', [(a + 1.25, ROWS + f), (a + 1.25, -f)], 3, [f'cruce_{n}'], VEL['transversal'])
        ruta(f'{n}_sube', [(b - 1.25, -f), (b - 1.25, ROWS + f)], 3, [f'cruce_{n}'], VEL['transversal'])
    # dónde arranca cada auto (distancia a lo largo de la ruta): parejos, dentro del mapa, fuera de los cruces y
    # fuera de lo que ve la cámara al empezar
    for r in rutas:
        (x0, y0), (x1, y1) = r['puntos']
        largo = math.hypot(x1 - x0, y1 - y0)
        d = ((x1 - x0) / largo, (y1 - y0) / largo)
        lado = abs(d[0]) > abs(d[1])
        cuerpo = CUERPO_LADO if lado else CUERPO_VERT

        def bien(s):
            cx, cy = x0 + d[0] * s, y0 + d[1] * s
            caja = (cx - cuerpo[0] / 2 - 1.5, cy - cuerpo[1] / 2 - 1.5, cx + cuerpo[0] / 2 + 1.5, cy + cuerpo[1] / 2 + 1.5)
            if not (0 <= cx <= COLS and 0 <= cy <= ROWS):
                return False
            if any(se_enciman(caja, (z[0], z[1], z[0] + z[2], z[1] + z[3])) for z in (ZONAS[k] for k in r['cruces'])):
                return False
            return not (abs(cx - spawn[0]) < CAMARA[0] + cuerpo[0] / 2 and abs(cy - spawn[1]) < CAMARA[1] + cuerpo[1] / 2)
        paso = largo / r['autos']
        for fase in np.arange(0, 1, .01):
            ss = [paso * (i + fase) for i in range(r['autos'])]
            if all(bien(s) for s in ss):
                break
        else:
            # se corre cada auto hasta un lugar bueno, guardando 10.5 tiles con el anterior
            ss, s = [], 0.0
            for i in range(r['autos']):
                s = max(s, paso * i)
                while not bien(s) or (ss and s - ss[-1] < 10.5):
                    s += .25
                ss.append(s)
            assert ss[-1] - ss[0] <= largo - 10.5, r['carril']
        r['inicios'] = [round(s, 3) for s in ss]
    return rutas


def semaforos():
    lista = []
    for k, (n, c) in enumerate(CALLES_V.items()):
        a, b = asfalto_v(c)
        lista.append(dict(nombre=f'Comercio_{n.capitalize()}', zona=f'cruce_{n}',
                          esquina_eo=[a, unity_y(BANQ_N[1] - 1)], hacia_eo=[-1, 1],
                          esquina_ns=[b, unity_y(BANQ_S[0]) + 1], hacia_ns=[1, -1], desfase=round(2.3 * (2 * k), 2)))
    for k, (n, c) in enumerate(MEDIA_CUADRA.items()):
        lista.append(dict(nombre=f'Paso_{n.capitalize()}', zona=f'paso_{n}',
                          esquina_eo=[c + 3, unity_y(BANQ_N[1] - 1)], hacia_eo=[1, 1],
                          esquina_ns=[c + 3, unity_y(BANQ_S[0]) + 1], hacia_ns=[1, -1], desfase=round(2.3 * (2 * k + 1), 2)))
    return lista


# ================================================================ peatones y dueños
def colocar_peatones(data, props, cruces_h, cruces_v, n_banqueta=12, n_cruce=4):
    """Como en el Residencial: peatones que van y vienen por tramos rectos de banqueta libres (no en la fila de los
    PuntoEntrega) y otros que cruzan por el centro de una cebra. Su propio azar."""
    rng = random.Random(4243)
    suelo = data['suelo']

    def tile(x, y):
        r, c = ROWS - 1 - int(math.floor(y)), int(math.floor(x))
        return suelo[r][c] if 0 <= r < ROWS and 0 <= c < COLS else None

    cajas = R.cajas_obstaculos(data, props)
    puntos = [(p['x'], p['y']) for p in data['puntos_entrega']]
    filas_punto = {int(math.floor(y)) for _, y in puntos}
    spawn = (data['spawn']['x'], data['spawn']['y'])
    usados = []

    def corredor(a, b):
        return (min(a[0], b[0]) - PIE[0] / 2, min(a[1], b[1]), max(a[0], b[0]) + PIE[0] / 2, max(a[1], b[1]) + PIE[1])

    def libre(a, b, piso):
        caja = corredor(a, b)
        if any(se_enciman(caja, o, .05) for o in cajas) or any(se_enciman(caja, u, .4) for u in usados):
            return False
        if any(caja[0] - 1 < px < caja[2] + 1 and caja[1] - 1 < py < caja[3] + .6 for px, py in puntos):
            return False
        if caja[0] - 3 < spawn[0] < caja[2] + 3 and caja[1] - 3 < spawn[1] < caja[3] + 3:
            return False
        x0, y0, x1, y1 = caja
        for x in list(np.arange(x0 + .02, x1, .25)) + [x1 - .02]:
            for y in list(np.arange(y0 + .02, y1, .25)) + [y1 - .02]:
                t = tile(x, y)
                if t is None or not t.startswith(piso):
                    return False
        return True

    peatones = []

    def nuevo(tipo, pts):
        n = len(peatones) + 1
        peatones.append(dict(nombre=f'Peaton_{n}', tipo=tipo, variante=VARIANTES_PEATON[(n - 1) % len(VARIANTES_PEATON)],
                             puntos=[[round(x, 3), round(y, 3)] for x, y in pts],
                             velocidad=round(rng.uniform(1.0, 1.5), 2), pausa=round(rng.uniform(1.0, 3.0), 2)))
        usados.append(corredor(pts[0], pts[-1]))

    celdas = [(c, ROWS - 1 - r) for r in range(ROWS) for c in range(COLS) if suelo[r][c].startswith('banqueta')]
    intentos = 0
    while sum(p['tipo'] == 'banqueta' for p in peatones) < n_banqueta:
        intentos += 1
        assert intentos < 40000, 'no cupieron los peatones de banqueta'
        c, fy = rng.choice(celdas)
        horizontal = rng.random() < .7
        largo = rng.uniform(5, 12)
        a = (c + .5, fy + .35)
        if horizontal:
            if fy in filas_punto:
                continue
            b = (a[0] + largo * rng.choice((-1, 1)), a[1])
        else:
            b = (a[0], a[1] + largo * rng.choice((-1, 1)))
        if not libre(a, b, 'banqueta'):
            continue
        pts = [a, b] if rng.random() < .5 else [a, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), b]
        nuevo('banqueta', pts)

    cruces = []
    for filas, cols in cruces_h:                    # cruzan la avenida: camino vertical
        x = (min(cols) + max(cols) + 1) / 2
        cruces.append(((x, ROWS - 1 - (min(filas) - 1) + .35), (x, ROWS - 1 - (max(filas) + 1) + .35)))
    for filas, cols in cruces_v:                    # cruzan una transversal: camino horizontal
        y = ROWS - 1 - (min(filas) + max(filas)) / 2 + .35 - .5
        cruces.append(((min(cols) - .5, y), (max(cols) + 1.5, y)))
    rng.shuffle(cruces)
    motivos = []
    for a, b in cruces:
        if sum(p['tipo'] == 'cruce' for p in peatones) == n_cruce:
            break
        d = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
        for _ in range(2):
            if not str(tile(*a)).startswith('banqueta'):
                a = (a[0] - d[0], a[1] - d[1])
            if not str(tile(*b)).startswith('banqueta'):
                b = (b[0] + d[0], b[1] + d[1])
        n = max(2, int(math.dist(a, b) * 4))
        medio = [str(tile(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n)) for i in range(1, n)]
        if not (str(tile(*a)).startswith('banqueta') and str(tile(*b)).startswith('banqueta')):
            motivos.append((a, b, 'puntas'))
            continue
        if not any(t.startswith('cebra') for t in medio) or \
                not all(t.startswith(('banqueta', 'asfalto', 'cebra', 'linea', 'carril', 'esquina', 'orilla')) for t in medio):
            motivos.append((a, b, 'medio', sorted(set(medio))))
            continue
        caja = corredor(a, b)
        if any(se_enciman(caja, o, .05) for o in cajas) or any(se_enciman(caja, u, .4) for u in usados):
            motivos.append((a, b, 'obstáculo'))
            continue
        nuevo('cruce', [a, b])
    assert sum(p['tipo'] == 'cruce' for p in peatones) == n_cruce, ('no hay cebras libres suficientes', motivos)
    return peatones


def colocar_clientes(data, negocios, props):
    clientes = []
    for i, (n, p, quien) in enumerate(zip([n for n in negocios if n['sprite'] != EMPRESA], data['puntos_entrega'], DUENOS)):
        puerta = n['puerta']
        hacia = (puerta['x'] - p['x'], puerta['y'] - p['y'])
        largo = math.hypot(*hacia)
        espera = dict(x=round(p['x'] + hacia[0] / largo * R.ESPERA_HACIA_CASA, 4),
                      y=round(p['y'] + hacia[1] / largo * R.ESPERA_HACIA_CASA, 4))
        tipo, var = quien.rsplit('_', 1)
        clientes.append(dict(nombre=f'Cliente_{i + 1}', casa=i + 1, punto=p['nombre'], personaje=quien, variante=int(var),
                             puerta=puerta, espera=espera))
    cajas = R.cajas_obstaculos(data, props)
    for cl in clientes:
        e = cl['espera']
        pie = (e['x'] - .3, e['y'], e['x'] + .3, e['y'] + .3)
        assert not any(se_enciman(pie, o) for o in cajas), ('el dueño espera sobre un obstáculo', cl)
    # vecinos (misma fila de negocios, a menos de 12 tiles) con distinto personaje
    for a, b in zip(clientes, clientes[1:]):
        if abs(a['puerta']['y'] - b['puerta']['y']) < 1e-3 and abs(a['puerta']['x'] - b['puerta']['x']) < 12:
            assert a['personaje'] != b['personaje'], (a['nombre'], b['nombre'])
    return clientes


# ================================================================ sólidos, revisiones y export
def lotes_solidos(lz, grid):
    """Todo lo que no se pisa (celdas de puro pasto que no son isleta de una cebra), en rectángulos grandes:
    tramos por fila estirados hacia abajo mientras la fila siguiente tenga el mismo tramo."""
    solido = np.zeros((ROWS, COLS), bool)
    for r in range(ROWS):
        for c in range(COLS):
            solido[r, c] = (lz.celda(lz.m, r, c) == PASTO).all() and not grid[r, c].startswith('cebra')
    libre = solido.copy()
    cajas = []
    for r in range(ROWS):
        c = 0
        while c < COLS:
            if not libre[r, c]:
                c += 1
                continue
            c1 = c
            while c1 < COLS and libre[r, c1]:
                c1 += 1
            r1 = r + 1
            while r1 < ROWS and libre[r1, c:c1].all():
                r1 += 1
            libre[r:r1, c:c1] = False
            cajas.append(dict(tipo='solido', x=c, y=ROWS - r1, ancho=c1 - c, alto=r1 - r))
            c = c1
    return cajas, solido


def piso_libre(data, props, solido, F):
    """Rejilla [y, x] (y hacia arriba, F celdas por tile) de lo que se pisa: ni sólido, ni pasto, ni props que chocan."""
    piso = np.zeros((ROWS * F, COLS * F), bool)
    for r in range(ROWS):
        for c in range(COLS):
            t = data['suelo'][r][c]
            if not solido[r, c] and not t.startswith(('pasto', 'flores', 'camellon')):
                piso[(ROWS - 1 - r) * F:(ROWS - r) * F, c * F:(c + 1) * F] = True
    for x0, y0, x1, y1 in R.cajas_obstaculos(data, props):
        piso[max(0, int(y0 * F)):int(math.ceil(y1 * F)), max(0, int(x0 * F)):int(math.ceil(x1 * F))] = False
    return piso


def comprobar_tiempos(data, props, solido, vel_moto=5.0, vel_pie=3.5):
    """El tiempo ideal de GameManager (max(10, distancia recta x 1.5 / velocidadReferencia + 4)) contra el camino
    real más corto (Dijkstra en 8 direcciones, rejilla de 0.25 tiles) en moto (hueco 1.2: holgura 0.6) y a pie
    (hueco 0.6: holgura 0.3), para el spawn -> cada punto y entre cada par de puntos a 20 tiles o más (los que
    puede pedir SeleccionPedidos). Devuelve el peor cociente camino/ideal de cada uno."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import dijkstra
    F = 4
    piso = piso_libre(data, props, solido, F)
    hol = ndimage.distance_transform_edt(piso) / F
    sel = data['seleccion']
    pts = [('spawn', data['spawn']['x'], data['spawn']['y'])] + [(p['nombre'], p['x'], p['y']) for p in data['puntos_entrega']]
    res = {}
    for modo, radio, vel in (('moto', .6, vel_moto), ('pie', .3, vel_pie)):
        ok = hol >= radio
        # los puntos quedan libres en un radio de 1 (BloqueoMoto) y la gente se para en ellos: se unen a lo libre
        for _, x, y in pts:
            yy, xx = np.ogrid[:ROWS * F, :COLS * F]
            ok |= ((xx + .5) / F - x) ** 2 + ((yy + .5) / F - y) ** 2 <= .36
            ok &= piso
        idx = -np.ones(ok.shape, int)
        idx[ok] = np.arange(ok.sum())
        filas, cols, pesos = [], [], []
        for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
            a = ok[max(0, -dy):ok.shape[0] - max(0, dy), max(0, -dx):ok.shape[1] - max(0, dx)]
            b = ok[max(0, dy):, max(0, dx):][:a.shape[0], :a.shape[1]] if dx >= 0 else ok[dy:, :ok.shape[1] + dx][:a.shape[0], :a.shape[1]]
            ia = idx[max(0, -dy):ok.shape[0] - max(0, dy), max(0, -dx):ok.shape[1] - max(0, dx)]
            ib = idx[max(0, dy):, max(0, dx):][:a.shape[0], :a.shape[1]] if dx >= 0 else idx[dy:, :ok.shape[1] + dx][:a.shape[0], :a.shape[1]]
            m = a & b
            filas.append(ia[m]); cols.append(ib[m]); pesos.append(np.full(m.sum(), math.hypot(dx, dy) / F))
        n = ok.sum()
        g = coo_matrix((np.concatenate(pesos), (np.concatenate(filas), np.concatenate(cols))), shape=(n, n)).tocsr()
        fuentes = [idx[int(y * F), int(x * F)] for _, x, y in pts]
        assert all(f >= 0 for f in fuentes), (modo, [p for p, f in zip(pts, fuentes) if f < 0])
        dist = dijkstra(g, directed=False, indices=fuentes)
        peor, filas_t = (0, None), []
        for i, (na, xa, ya) in enumerate(pts):
            for j, (nb, xb, yb) in enumerate(pts):
                if j == 0 or i == j:
                    continue
                recta = math.hypot(xb - xa, yb - ya)
                if i > 0 and recta < sel['distancia_minima']:
                    continue
                camino = dist[i][fuentes[j]]
                assert np.isfinite(camino), (modo, 'no se llega', na, nb)
                ideal = max(10.0, recta * 1.5 / vel_moto + 4)
                t = camino / vel
                filas_t.append((na, nb, recta, camino, t, ideal))
                if t / ideal > peor[0]:
                    peor = (t / ideal, (na, nb, round(recta, 1), round(camino, 1), round(t, 1), round(ideal, 1)))
        res[modo] = dict(peor=peor, pares=len(filas_t), media=float(np.mean([f[4] / f[5] for f in filas_t])))
    return res


def comprobar_pasillos(data, props, solido):
    """Pasillos de al menos 2 tiles: en cada estacionamiento y en la plaza, la parte con 1 tile libre a cada lado
    (sin props que chocan ni nada sólido) debe unir sus entradas con sus pasillos y la plaza sus cuatro lados con los
    dos kioscos. Rejilla de 0.1 tiles."""
    F = 10
    piso = np.zeros((ROWS * F, COLS * F), bool)                 # [y, x] con y hacia arriba
    for r in range(ROWS):
        for c in range(COLS):
            t = data['suelo'][r][c]
            if not solido[r, c] and not t.startswith(('pasto', 'flores')):
                piso[(ROWS - 1 - r) * F:(ROWS - r) * F, c * F:(c + 1) * F] = True
    for x0, y0, x1, y1 in R.cajas_obstaculos(data, props):
        piso[max(0, int(y0 * F)):int(math.ceil(y1 * F)), max(0, int(x0 * F)):int(math.ceil(x1 * F))] = False
    holgura = ndimage.distance_transform_edt(piso) / F            # distancia al obstáculo más cercano, en tiles
    ancho = holgura >= 1.0                                        # un círculo de 2 tiles de diámetro cabe
    etiqueta, _ = ndimage.label(ancho)

    def en(x, y):
        return etiqueta[int(y * F), int(x * F)]
    res = []
    for l in LOTES:
        pts = [((ca + cb + 1) / 2, unity_y((ra + rb) / 2) + .5) for (ca, cb), (ra, rb) in l['entradas']]
        aisles = []
        filas_caj = {r for ra, rb in l['cajones'] for r in range(ra, rb + 1)}
        banda = []                                                # el centro de cada pasillo (filas sin cajones)
        for r in range(l['r0'], l['r1'] + 2):
            if r <= l['r1'] and r not in filas_caj:
                banda.append(r)
            elif banda:
                aisles.append(((l['c0'] + l['c1'] + 1) / 2, unity_y((banda[0] + banda[-1]) / 2) + .5))
                banda = []
        ids = {en(*p) for p in pts + aisles}
        ok = 0 not in ids and len(ids) == 1
        res.append((l['nombre'], ok, ids))
        assert ok, ('pasillo de menos de 2 tiles o cortado', l['nombre'], ids)
    p = PLAZA
    lados = [((p['c0'] + p['c1'] + 1) / 2, unity_y(p['r0']) + .6), ((p['c0'] + p['c1'] + 1) / 2, unity_y(p['r1']) + .4),
             (p['c0'] + 1.2, 13.5), (p['c1'] - .2, 13.5)] + [(n['x'], n['y']) for n in data['puntos_entrega'] if n['casa'] > 10]
    ids = {en(*q) for q in lados}
    assert 0 not in ids and len(ids) == 1, ('la plaza no tiene caminos de 2 tiles entre sus lados y los kioscos', ids, lados)
    res.append(('plaza', True, ids))
    return res


def colocar_baches(data):
    """Los baches del JSON con su caja (en tiles) y la revisión de distancias: suelo de carril bajo toda la caja,
    2 tiles o más de las zonas de cruce, de toda celda de cebra, línea de alto o cajón, y entre baches; y que el
    otro carril del mismo sentido quede libre (2 tiles de ancho)."""
    suelo = data['suelo']

    def tile(x, y):
        r, c = ROWS - 1 - int(math.floor(y)), int(math.floor(x))
        return suelo[r][c] if 0 <= r < ROWS and 0 <= c < COLS else ''

    def dist_rect(a, b):
        dx = max(b[0] - a[2], a[0] - b[2], 0)
        dy = max(b[1] - a[3], a[1] - b[3], 0)
        return math.hypot(dx, dy)
    prohibidas = [(c, ROWS - 1 - r, c + 1, ROWS - r) for r in range(ROWS) for c in range(COLS)
                  if suelo[r][c].startswith(('cebra', 'linea_alto', 'cajon', 'estacionamiento'))]
    zonas = [(z['x'], z['y'], z['x'] + z['ancho'], z['y'] + z['alto']) for z in data['zonas_cruce'].values()]
    lista = []
    for i, (tam, x, y) in enumerate(BACHES):
        w, h = (v / T for v in TAM_BACHE[tam])
        caja = (x - w / 2, y - h / 2, x + w / 2, y + h / 2)
        for xx in np.linspace(caja[0] + .02, caja[2] - .02, 5):
            for yy in np.linspace(caja[1] + .02, caja[3] - .02, 3):
                t = tile(xx, yy)
                assert t.startswith(('asfalto', 'carril', 'linea_v')), (i + 1, tam, x, y, t)
        dz = min(dist_rect(caja, z) for z in zonas)
        dp = min(dist_rect(caja, z) for z in prohibidas)
        assert dz >= DISTANCIA_BACHE and dp >= DISTANCIA_BACHE, (i + 1, round(dz, 2), round(dp, 2))
        for o in lista:
            assert math.hypot(o['x'] - x, o['y'] - y) > 6, ('baches muy juntos', i + 1)
        # el carril de al lado (mismo sentido) libre: 2 tiles que no toca la caja
        horizontal = any(abs(y - c) < 1e-3 for c in (36, 34, 30, 28))
        libre = 2.0 - max(0.0, (h if horizontal else w) - 2.0)
        lista.append(dict(nombre=f'Bache_{i + 1}', tamano=tam, agua=False, x=x, y=y, ancho=round(w, 4), alto=round(h, 4),
                          lado_libre=round(libre, 2)))
    return lista


def exportar(grid, objs, negocios, props, rutas, peatones_fn):
    suelo = [[grid[r, c] for c in range(COLS)] for r in range(ROWS)]
    lista = []
    for base, name, cx, flip in sorted(objs, key=lambda o: o[0]):
        w = props[name].shape[1]
        o = dict(nombre=name, x=(cx - w // 2 + w / 2) / T, y=R.a_unity_y(base))
        if flip:
            o['voltear'] = True
        lista.append(o)
    casas, puntos = [], []
    for n in negocios:
        if n['sprite'] == EMPRESA:
            continue
        i = len(casas) + 1
        casas.append(dict(id=i, nombre=n['nombre'], negocio=n['sprite'], puerta=n['puerta'], entrega=n['entrega'], lado=n['lado']))
        puntos.append(dict(nombre=f'PuntoEntrega_{i}', casa=i, negocio=n['nombre'], lado=n['lado'], **n['entrega']))
    emp = next(n for n in negocios if n['sprite'] == EMPRESA)
    spawn = dict(x=emp['puerta']['x'], y=emp['entrega']['y'], nota="banqueta de la avenida, frente a la puerta del Salón Brenn's")
    return dict(columnas=COLS, filas=ROWS, tile=T, zona='Comercial',
                coordenadas='tiles; origen abajo-izquierda, y hacia arriba; props/autos = pivote abajo-centro',
                suelo_orden='suelo[0] es la fila de arriba',
                calles=[dict(clave=k, nombre=v) for k, v in NOMBRES_CALLE.items()],
                spawn=spawn, empresa=dict(nombre=emp['nombre'], puerta=emp['puerta']),
                duena=dict(x=emp['puerta']['x'], y=round(unity_y(emp['fila']) - .1, 3), nota="dueña fija en la puerta del Salón Brenn's"),
                casas=casas, puntos_entrega=puntos, suelo=suelo, props=lista, dinamicos=[], autos_calle=[],
                zonas_cruce={k: dict(x=v[0], y=v[1], ancho=v[2], alto=v[3]) for k, v in ZONAS.items()},
                rutas_autos=rutas, semaforos=semaforos(),
                cruces_moto=dict(y0=unity_y(CAMELLON[1] - 1), y1=unity_y(CAMELLON[0]) + 1, esperados=len(ZONAS)),
                seleccion=dict(cantidad=5, distancia_minima=20.0, y_avenida=unity_y(CAMELLON[0])),
                sin_autos_al_empezar=dict(x=spawn['x'] - CAMARA[0], y=spawn['y'] - CAMARA[1], ancho=2 * CAMARA[0], alto=2 * CAMARA[1]))


def guardar_json(data, name):
    partes = []
    for k, v in data.items():
        if isinstance(v, list):
            cuerpo = '[\n' + ',\n'.join('    ' + json.dumps(o, ensure_ascii=False) for o in v) + '\n  ]'
        else:
            cuerpo = json.dumps(v, ensure_ascii=False)
        partes.append(f'  {json.dumps(k)}: {cuerpo}')
    with open(os.path.join(OUT, name), 'w', encoding='utf-8') as f:
        f.write('{\n' + ',\n'.join(partes) + '\n}\n')


def escribir_tileset(tiles, usados, orden):
    nombres = [n for n in orden if n in usados] + sorted(n for n in usados if n not in orden)
    cols = 8
    filas = (len(nombres) + cols - 1) // cols
    hoja = np.zeros((filas * T, cols * T, 4), np.uint8)
    lista = []
    for i, n in enumerate(nombres):
        x, y = (i % cols) * T, (i // cols) * T
        hoja[y:y + T, x:x + T, :3] = tiles[n]
        hoja[y:y + T, x:x + T, 3] = 255
        lista.append(dict(name=n, index=i, x=x, y=y, w=T, h=T))
    Image.fromarray(hoja).save(os.path.join(OUT, TILESET + '.png'))
    with open(os.path.join(OUT, TILESET + '.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen=TILESET + '.png', tile=T, columnas=cols, origen='arriba-izquierda', tiles=lista), f, indent=2, ensure_ascii=False)
    os.makedirs(os.path.dirname(TILESET_UNITY), exist_ok=True)
    shutil.copyfile(os.path.join(OUT, TILESET + '.png'), TILESET_UNITY)
    return len(nombres), filas * T


def escribir_atlas(props, pivotes, usados):
    nombres = sorted(usados, key=lambda n: (-props[n].shape[0], n))
    ancho, pad = 1024, 4
    x, y, alto_fila, puestos = pad, pad, 0, []
    for n in nombres:
        h, w = props[n].shape[:2]
        if x + w + pad > ancho:
            x, y, alto_fila = pad, y + alto_fila + pad, 0
        puestos.append((n, x, y, w, h))
        x += w + pad
        alto_fila = max(alto_fila, h)
    H = y + alto_fila + pad
    hoja = np.zeros((H, ancho, 4), np.uint8)
    sprites = []
    for n, x, y, w, h in puestos:
        hoja[y:y + h, x:x + w] = props[n]
        s = dict(name=n, x=x, y=y, w=w, h=h, y_unity=H - y - h)
        if n in pivotes:
            s['pivote'] = pivotes[n]
        sprites.append(s)
    sprites.sort(key=lambda s: s['name'])
    Image.fromarray(hoja).save(os.path.join(OUT, ATLAS + '.png'))
    with open(os.path.join(OUT, ATLAS + '.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(imagen=ATLAS + '.png', ancho=ancho, alto=H, origen='arriba-izquierda (y_unity = desde abajo)',
                       pivote=[0.5, 0.0], separacion=pad, sprites=sprites), f, indent=2, ensure_ascii=False)
    os.makedirs(os.path.dirname(ATLAS_UNITY), exist_ok=True)
    shutil.copyfile(os.path.join(OUT, ATLAS + '.png'), ATLAS_UNITY)
    return len(sprites), H


def preview(ref, data, nombre, autos=False):
    img = Image.fromarray(ref).resize((W_PX // 2, H_PX // 2), Image.NEAREST)
    dr = ImageDraw.Draw(img)
    for k, z in data['zonas_cruce'].items():
        dr.rectangle((z['x'] * 16, (ROWS - z['y'] - z['alto']) * 16, (z['x'] + z['ancho']) * 16, (ROWS - z['y']) * 16), outline=(255, 200, 0), width=2)
    if autos:
        for r in data['rutas_autos']:
            (x0, y0), (x1, y1) = r['puntos']
            dr.line(((x0 * 16, (ROWS - y0) * 16), (x1 * 16, (ROWS - y1) * 16)), fill=(255, 230, 0), width=2)
            L = math.hypot(x1 - x0, y1 - y0)
            for s in r['inicios']:
                cx, cy = x0 + (x1 - x0) * s / L, y0 + (y1 - y0) * s / L
                dr.rectangle((cx * 16 - 18, (ROWS - cy) * 16 - 10, cx * 16 + 18, (ROWS - cy) * 16 + 10), outline=(255, 40, 40), width=3)
        z = data['sin_autos_al_empezar']
        dr.rectangle((z['x'] * 16, (ROWS - z['y'] - z['alto']) * 16, (z['x'] + z['ancho']) * 16, (ROWS - z['y']) * 16), outline=(0, 255, 255), width=2)
    else:
        for p in data['peatones']:
            pts = [(px * 16, (ROWS - py) * 16) for px, py in p['puntos']]
            dr.line(pts, fill=(40, 220, 255) if p['tipo'] == 'banqueta' else (255, 140, 0), width=4)
        for p in data['puntos_entrega']:
            x, y = p['x'] * 16, (ROWS - p['y']) * 16
            dr.ellipse((x - 9, y - 9, x + 9, y + 9), fill=(255, 111, 165) if p['lado'] == 'norte' else (140, 82, 255), outline=(0, 0, 0))
            dr.text((x - 4, y - 6), str(p['casa']), fill=(0, 0, 0))
        s = data['spawn']
        dr.rectangle((s['x'] * 16 - 10, (ROWS - s['y']) * 16 - 10, s['x'] * 16 + 10, (ROWS - s['y']) * 16 + 10), outline=(255, 255, 0), width=3)
    img.save(os.path.join(OUT, nombre))


# PIE de las colisiones nuevas (las mismas que la tabla de ConstruirMapa.cs)
def colisiones_nuevas(props):
    extra = [('kiosco_', [(3, 72)], 16)]
    for k in NOMBRES_CALLE:
        w = props[f'letrero_calle_{k}'].shape[1]
        extra.append((f'letrero_calle_{k}', [(w // 2 - 2, w // 2 + 1)], 4))
    return extra


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    tiles, orden = cargar_tiles()
    props, pivotes = cargar_props()
    nuevas = colisiones_nuevas(props)
    R.COLISIONES[:0] = nuevas                      # antes que 'letrero_calle_avenida' y los demás prefijos
    rng = random.Random(2027)
    grid, lz, ov, tiles_nuevos, lugares = trazar_suelo(rng, tiles)
    tiles.update(dict(tiles_nuevos))

    es = Escena(rng, grid)
    negocios = colocar_negocios(es, props)
    estacionados = colocar_vegetacion(es, random.Random(31), props, lugares)
    colocar_plaza(es)
    colocar_calle(es, negocios)
    movidos, quitados = cebras.despejar(es.objs, props, lz.cebras, lz.m, MG * T)
    print('cebras:', ', '.join(f"{c['eje']} {min(c['cols'])}-{max(c['cols'])}x{min(c['filas'])}-{max(c['filas'])} {c['rayas']} rayas, "
                               f"{len(c.get('altos', []))} de alto" for c in lz.cebras))
    print('junto a las cebras: movidos', len(movidos), '| quitados', [q[0] for q in quitados])
    assert all(o[1] in props for o in es.objs)

    emp = next(n for n in negocios if n['sprite'] == EMPRESA)
    spawn = (emp['puerta']['x'], emp['entrega']['y'])
    rutas = rutas_autos(spawn, random.Random(77))
    data = exportar(grid, es.objs, negocios, props, rutas, None)
    for p in data['puntos_entrega']:
        t = data['suelo'][ROWS - 1 - int(p['y'])][int(p['x'])]
        assert t.startswith(('banqueta', 'plaza')), (p['nombre'], t)
    assert data['suelo'][ROWS - 1 - int(spawn[1])][int(spawn[0])].startswith('banqueta')
    assert sum(p['lado'] == 'norte' for p in data['puntos_entrega']) == 7 and len(data['puntos_entrega']) == 12
    data['lotes_solidos'], solido = lotes_solidos(lz, grid)
    data['patios'] = []
    for o in data['props']:                         # nada que choque fuera de su suelo
        r, c = ROWS - 1 - int(o['y'] + .02), int(o['x'])
        t = grid[r, c]
        if o['nombre'].startswith(('farol', 'letrero', 'bote', 'banca', 'jardinera', 'kiosco', 'fuente')):
            assert t.startswith(('banqueta', 'plaza', 'esquina', 'orilla')), (o, t)
        if o['nombre'].startswith(('carro_', 'camioneta')):
            assert t.startswith(('asfalto', 'cajon', 'estacionamiento')), (o, t)
    data['peatones'] = colocar_peatones(data, props, CRUCES_H, CRUCES_V)
    data['clientes'] = colocar_clientes(data, negocios, props)
    data['baches'] = colocar_baches(data)
    data['turbos'] = turbos.colocar(data, turbos.CANTIDAD['comercial'])   # Café exprés (ConstruirTurbos)
    pasillos = comprobar_pasillos(data, props, solido)
    tiempos = comprobar_tiempos(data, props, solido)

    usados_t = set(grid.values())
    n_tiles, alto_t = escribir_tileset(tiles, usados_t, orden)
    usados_p = {o[1] for o in es.objs} | set(build_baches()[0]) | {f'carro_{v}_{c}' for v in ('lateral', 'frontal', 'trasero') for c in R.COLORES_AUTO} | \
               {f'semaforo_{e}' for e in ('verde', 'ambar', 'rojo', 'apagado')}
    n_props, alto_a = escribir_atlas(props, pivotes, usados_p)
    guardar_json(data, MAPA)
    ref = R.render(tiles, props, grid, es.objs)
    Image.fromarray(ref).save(os.path.join(OUT, 'mapa_referencia_comercial.png'))
    preview(ref, data, 'mapa_entregas_comercial.png')
    preview(ref, data, 'mapa_autos_comercial.png', autos=True)
    print('colisiones nuevas para ConstruirMapa.cs:', nuevas)
    print('baches:', [(b['nombre'], b['tamano'], b['x'], b['y']) for b in data['baches']])
    print('pasillos de 2 tiles:', [(n, ok) for n, ok, _ in pasillos])
    for modo, t in tiempos.items():
        print(f"tiempos {modo}: {t['pares']} pares, camino/ideal medio {t['media']:.2f}, peor {t['peor'][0]:.2f} "
              f"(de, a, recta, camino, segundos, ideal) = {t['peor'][1]}")
    print('tileset', n_tiles, 'tiles, alto', alto_t, '| atlas', n_props, 'sprites, alto', alto_a, '| props', len(data['props']),
          '| estacionados', len(estacionados), '| sólidos', len(data['lotes_solidos']), '| peatones', len(data['peatones']),
          '| dueños', len(data['clientes']), '| autos', sum(r['autos'] for r in rutas), 'en', len(rutas), 'carriles')
