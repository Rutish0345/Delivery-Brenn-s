"""Nivel 3 "Lluvia": el mapa Residencial grande (nivel 1) de noche y con lluvia, sin rehacer el mapa.

Lee lo que exporta generate_residencial_grande.py (unity/residencial) y escribe en unity/lluvia:
  mapa_lluvia.json      el mismo mapa con el suelo mojado (tiles <nombre>_mojado), 10 baches con charco (Bache.cs),
                        charcos decorativos sin efecto y las ventanas de las casas (luces tenues)
  tileset_lluvia.png/.json   tileset_suelo + las variantes _mojado (la paleta mojada de generate_lluvia.py con brillos)
  props_lluvia.png/.json     props_atlas + baches con charco, gotas, charcos decorativos y salpicaduras (3 cuadros)
  lluvia_1..4.png       la capa de lluvia en pantalla (gotas diagonales, tileable 128x128), 4 cuadros
y copia las imágenes a Unity (Assets/Tiles, Assets/Sprits/Escenarios/Lluvia, Assets/Sprits/Efectos/Lluvia).
Los baches son oscuros con borde roto (props_baches.py, versión charco); los charcos decorativos son claros, lisos y
semitransparentes (reflejan el cielo nublado), así se distinguen a simple vista.
"""
import json
import math
import os
import random
import shutil
import zlib

import numpy as np
from PIL import Image
from scipy import ndimage

from generate_lluvia import remap, WET, ROAD_WET, SIDE_WET, GLINT, GLINT_L, SIDE_GLINT, decal_puddle
from generate_unity import trim
from props_baches import build_baches, TAMANOS as TAM_BACHE

T = 32
BASE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(BASE, 'unity', 'residencial')
OUT = os.path.join(BASE, 'unity', 'lluvia')
UNITY = r'C:\Users\kangu\DeliveryBrenns\Assets'
TILESET_UNITY = os.path.join(UNITY, 'Tiles', 'tileset_lluvia.png')
ATLAS_UNITY = os.path.join(UNITY, 'Sprits', 'Escenarios', 'Lluvia', 'props_lluvia.png')
EFECTOS_UNITY = os.path.join(UNITY, 'Sprits', 'Efectos', 'Lluvia')

# Baches con charco (Bache.cs, misma sacudida y penalización que el nivel 2): posiciones fijas (tamaño, x, y) en
# Unity, en el centro de la calle a mitad de cuadra, lejos de cruces, cebras y líneas de alto (2 tiles o más),
# con 2 tiles libres de asfalto a un lado para esquivarlos. Salieron de buscar_baches() y se revisan en cada corrida.
BACHES = [
    ('grande', 5.5, 44.5), ('chico', 123.5, 17.5), ('grande', 76.5, 48.5), ('chico', 35.5, 6.5), ('grande', 87.5, 17.5),
    ('chico', 41.5, 43.5), ('grande', 59.5, 20.5), ('chico', 10.5, 17.5), ('grande', 92.5, 43.5), ('chico', 106.5, 28.5),
]
DISTANCIA_BACHE = 2.0
SEPARACION_BACHES = 10.0
N_CHARCOS = 18
# Banqueta que tapaban los setos laterales de B_este y C_este cuando los lotes llegaban hasta atrás (antes del 22/10/2026):
# se sigue saltando para que, con los lotes cortos, los charcos queden donde estaban
SIN_CHARCO = {(111.5, 12.5), (111.5, 34.5), (111.5, 35.5), (111.5, 36.5)}

# Ventanas de las casas (casa_*: 160x150 px, pivote abajo-centro): dos a los lados de la puerta
VENTANAS_CASA = [(-1.5, 1.45), (1.5, 1.45)]

ROAD = ('asfalto_a', 'asfalto_b')


def leer(nombre):
    with open(os.path.join(RES, nombre), encoding='utf-8') as f:
        return json.load(f)


def semilla(nombre):
    return zlib.crc32(nombre.encode())


# ---------------------------------------------------------------- suelo mojado
def brillos(a, rng, permitidos, n, colores):
    """Trazos cortos horizontales de brillo, solo sobre los colores permitidos (asfalto o banqueta)."""
    h, w = a.shape[:2]
    perm = np.zeros((h, w), bool)
    for c in permitidos:
        perm |= np.all(a[..., :3] == c, axis=-1)
    for _ in range(n):
        x, y, c = rng.randrange(w), rng.randrange(h), rng.choice(colores)
        for k in range(rng.randint(2, 4)):
            if perm[y, (x + k) % w]:
                a[y, (x + k) % w, :3] = c


def tileset_mojado(tmeta, img):
    """Cada tile del Residencial que cambia con la paleta mojada gana su variante _mojado."""
    nuevos = {}
    for d in tmeta['tiles']:
        a = img[d['y']:d['y'] + T, d['x']:d['x'] + T].copy()
        b = remap(a, WET)
        if not np.any(a != b):
            continue
        rng = random.Random(semilla(d['name']))
        brillos(b, rng, ROAD_WET, 9, [GLINT, GLINT, GLINT_L])
        brillos(b, rng, SIDE_WET, 3, [SIDE_GLINT])
        nuevos[d['name'] + '_mojado'] = b
    cols = tmeta['columnas']
    n0 = len(tmeta['tiles'])
    total = n0 + len(nuevos)
    filas = (total + cols - 1) // cols
    hoja = np.zeros((filas * T, cols * T, 4), np.uint8)
    hoja[:img.shape[0], :img.shape[1]] = img
    tiles = [dict(t) for t in tmeta['tiles']]
    for i, (n, a) in enumerate(nuevos.items(), start=n0):
        x, y = (i % cols) * T, (i // cols) * T
        hoja[y:y + T, x:x + T] = a
        tiles.append(dict(name=n, index=i, x=x, y=y, w=T, h=T))
    meta = dict(tmeta, imagen='tileset_lluvia.png', tiles=tiles,
                nota='tileset_suelo del Residencial + variantes _mojado (nivel 3)')
    return hoja, meta, set(nuevos)


# ---------------------------------------------------------------- calcomanías y efectos
def charco_decorativo(w, h, a_, b_, seed):
    """Charco sin efecto: el de generate_lluvia.py (cielo nublado reflejado, orilla lisa) un poco transparente."""
    s = trim(decal_puddle(w, h, a_, b_, seed)).copy()
    s[..., 3] = np.where(s[..., 3] > 0, 205, 0)
    return s


def salpicadura(k):
    """Onda de lluvia en el suelo, 3 cuadros: crece y se apaga; en el 2 saltan dos gotitas."""
    w, h = 13, 7
    s = np.zeros((h, w, 4), np.uint8)
    col = (200, 224, 240)
    (rx, ry), alfa = [(2, 1), (4, 2), (6, 3)][k], [235, 185, 120][k]
    cx, cy = w // 2, h // 2
    for t in np.linspace(0, 2 * math.pi, 64, endpoint=False):
        x, y = int(round(cx + rx * math.cos(t))), int(round(cy + ry * math.sin(t)))
        if 0 <= x < w and 0 <= y < h:
            s[y, x] = (*col, alfa)
    if k == 1:
        for x, y in ((cx - 2, 0), (cx + 2, 1)):
            s[y, x] = (*col, 220)
    return s


def lluvia(k, n=64, lado=128):
    """Gotas diagonales (caen hacia la izquierda), tileable; cada cuadro corre las gotas un poco y cambia algunas."""
    rng = random.Random(1000)
    gotas = [(rng.randrange(lado), rng.randrange(lado), rng.randint(4, 7), rng.random()) for _ in range(n)]
    s = np.zeros((lado, lado, 4), np.uint8)
    rng_k = random.Random(77 + k)
    for (x, y, largo, r) in gotas:
        if rng_k.random() < .2:                 # una de cada cinco cambia de lugar en cada cuadro (parpadeo)
            x, y = rng_k.randrange(lado), rng_k.randrange(lado)
        y += k * 6
        x -= k * 2
        for i in range(largo):
            px, py = int(round(x - i * .35)) % lado, (y + i) % lado
            a = 120 + int(100 * i / max(1, largo - 1)) if r > .25 else 70 + int(60 * i / max(1, largo - 1))
            s[py, px] = (214, 226, 246, min(255, a))
    return s


def extender_atlas(ameta, img, nuevos, pivotes):
    """props_atlas + los sprites nuevos en estantes debajo (4 px de separación)."""
    sep = ameta.get('separacion', 4)
    ancho = ameta['ancho']
    x, y, alto_fila = sep, ameta['alto'] + sep, 0
    pos = {}
    for n, a in nuevos.items():
        h, w = a.shape[:2]
        if x + w + sep > ancho:
            x, y, alto_fila = sep, y + alto_fila + sep, 0
        pos[n] = (x, y)
        x += w + sep
        alto_fila = max(alto_fila, h)
    alto = y + alto_fila + sep
    hoja = np.zeros((alto, ancho, 4), np.uint8)
    hoja[:img.shape[0], :img.shape[1]] = img
    sprites = [dict(s, y_unity=alto - s['y'] - s['h']) for s in ameta['sprites']]
    for n, a in nuevos.items():
        (px, py), (h, w) = pos[n], a.shape[:2]
        hoja[py:py + h, px:px + w] = a
        sprites.append(dict(name=n, x=px, y=py, w=w, h=h, y_unity=alto - py - h, pivote=pivotes[n]))
    return hoja, dict(ameta, imagen='props_lluvia.png', alto=alto, sprites=sprites)


# ---------------------------------------------------------------- dónde van
def rejilla(data):
    return np.array(data['suelo'], dtype=object)          # [fila desde arriba, columna]


def carreras(es_calle):
    """Largo de la carrera de calle horizontal y vertical que pasa por cada celda."""
    R, C = es_calle.shape
    hor = np.zeros((R, C), int)
    ver = np.zeros((R, C), int)
    for r in range(R):
        c = 0
        while c < C:
            if es_calle[r, c]:
                d = c
                while d < C and es_calle[r, d]:
                    d += 1
                hor[r, c:d] = d - c
                c = d
            else:
                c += 1
    for c in range(C):
        r = 0
        while r < R:
            if es_calle[r, c]:
                d = r
                while d < R and es_calle[d, c]:
                    d += 1
                ver[r:d, c] = d - r
                r = d
            else:
                r += 1
    return hor, ver


def contexto(data):
    g = rejilla(data)
    R, C = g.shape
    calle = np.vectorize(lambda t: t.startswith(('asfalto', 'linea', 'cebra', 'curva_avenida')))(g)
    hor, ver = carreras(calle)
    cruce = (hor >= 9) & (ver >= 9)                       # las dos carreras largas: un cruce de calles
    raya = np.vectorize(lambda t: t.startswith(('cebra', 'linea_alto', 'curva_avenida')))(g)
    prohibido = cruce | raya
    # distancia (en tiles, de borde de celda a borde) a lo prohibido
    dist = ndimage.distance_transform_edt(~prohibido)
    return g, calle, hor, ver, prohibido, dist


def caja(tam, x, y):
    w, h = (v / T for v in TAM_BACHE[tam])
    return x - w / 2, y - h / 2, x + w / 2, y + h / 2


def celdas_de(c, R):
    x0, y0, x1, y1 = c
    for cx in range(int(math.floor(x0)), int(math.ceil(x1))):
        for cy in range(int(math.floor(y0)), int(math.ceil(y1))):
            yield R - 1 - cy, cx


def dist_rect_celda(c, r, col, R):
    x0, y0, x1, y1 = c
    cy0 = R - 1 - r
    dx = max(col - x1, x0 - (col + 1), 0)
    dy = max(cy0 - y1, y0 - (cy0 + 1), 0)
    return math.hypot(dx, dy)


def revisar_bache(data, ctx, tam, x, y, otros):
    """Error (texto) si el bache no cumple; None si está bien."""
    g, calle, hor, ver, prohibido, dist = ctx
    R, C = g.shape
    c = caja(tam, x, y)
    for r, col in celdas_de(c, R):
        if not (0 <= r < R and 0 <= col < C) or g[r, col] not in ROAD:
            return f'pisa {g[r, col] if 0 <= r < R and 0 <= col < C else "fuera"}'
    # lo prohibido (cruces, cebras, líneas de alto, curvas) a DISTANCIA_BACHE o más del borde del bache
    r0, c0 = R - 1 - int(y), int(x)
    for r in range(max(0, r0 - 5), min(R, r0 + 6)):
        for col in range(max(0, c0 - 5), min(C, c0 + 6)):
            if prohibido[r, col] and dist_rect_celda(c, r, col, R) < DISTANCIA_BACHE:
                return f'a menos de {DISTANCIA_BACHE} de {g[r, col]} ({col}, {R - 1 - r})'
    # carriles: la calle (o cada lado de la avenida) mide 3 o más de ancho donde está, con 2 tiles libres de un lado para esquivarlo
    horizontal = hor[r0, c0] > ver[r0, c0]
    ancho = ver[r0, c0] if horizontal else hor[r0, c0]
    if not 3 <= ancho <= 8:
        return f'ancho de calle {ancho}'
    if horizontal:
        arriba = sum(1 for k in range(1, 4) if calle[r0 - k, c0])
        abajo = sum(1 for k in range(1, 4) if calle[r0 + k, c0])
    else:
        arriba = sum(1 for k in range(1, 4) if calle[r0, c0 - k])
        abajo = sum(1 for k in range(1, 4) if calle[r0, c0 + k])
    if max(arriba, abajo) < 2:
        return 'sin 2 tiles libres a un lado'
    for p in data['props']:                               # autos estacionados: su carrocería a 2 tiles o más
        if p['nombre'].startswith('carro_'):
            medio = 1.5 if 'lateral' in p['nombre'] else 0.85
            alto = 1.5 if 'lateral' in p['nombre'] else 3.0
            auto = (p['x'] - medio, p['y'], p['x'] + medio, p['y'] + alto)
            dx = max(auto[0] - c[2], c[0] - auto[2], 0)
            dy = max(auto[1] - c[3], c[1] - auto[3], 0)
            if math.hypot(dx, dy) < DISTANCIA_BACHE:
                return f'junto a {p["nombre"]} estacionado'
    if not (4 <= x <= C - 4 and 3 <= y <= R - 3):
        return 'junto al borde del mapa (ahí entran los autos)'
    if math.hypot(data['spawn']['x'] - x, data['spawn']['y'] - y) < 6:
        return 'junto al spawn'
    for o in otros:
        if math.hypot(o[1] - x, o[2] - y) < SEPARACION_BACHES:
            return f'a menos de {SEPARACION_BACHES} de otro bache'
    return None


def buscar_baches(data, ctx, n=10):
    """Candidatos válidos repartidos por el mapa (el más lejos de los ya elegidos), para fijar BACHES."""
    g = ctx[0]
    R, C = g.shape
    elegidos = []
    cand = [(c + .5, R - 1 - r + .5) for r in range(R) for c in range(C) if g[r, c] in ROAD]
    while len(elegidos) < n:
        tam = 'grande' if len(elegidos) % 2 == 0 else 'chico'
        mejor = None
        for (x, y) in cand:
            if revisar_bache(data, ctx, tam, x, y, elegidos) is None:
                d = min([math.hypot(x - e[1], y - e[2]) for e in elegidos] or [1e9 - abs(x - 6) - abs(y - 48)])
                if mejor is None or d > mejor[0]:
                    mejor = (d, (tam, x, y))
        if mejor is None:
            break
        elegidos.append(mejor[1])
    return elegidos


def colocar_baches(data, ctx):
    lista, ok = [], []
    for i, (tam, x, y) in enumerate(BACHES):
        err = revisar_bache(data, ctx, tam, x, y, ok)
        assert err is None, (f'Bache_{i + 1}', tam, x, y, err)
        ok.append((tam, x, y))
        w, h = (v / T for v in TAM_BACHE[tam])
        lista.append(dict(nombre=f'Bache_{i + 1}', tamano=tam, agua=True, x=x, y=y, ancho=round(w, 4), alto=round(h, 4)))
    return lista


def colocar_charcos(data, ctx, baches, n=N_CHARCOS):
    """Charcos decorativos (sin collider) en banquetas y a la orilla del asfalto, lejos de baches, puertas y props."""
    g = ctx[0]
    R, C = g.shape
    rng = random.Random(31)
    estorbos = [(p['x'], p['y']) for p in data['props']]
    puertas = [(p['x'], p['y']) for p in data['puntos_entrega']] + [(c['puerta']['x'], c['puerta']['y']) for c in data['casas']]
    cand = []
    for r in range(R):
        for c in range(C):
            t = g[r, c]
            if t not in ('banqueta', 'asfalto_sombra_bordillo'):
                continue
            x, y = c + .5, R - 1 - r + .5
            if (x, y) in SIN_CHARCO:
                continue
            if any(math.hypot(x - b['x'], y - b['y']) < 4 for b in baches):
                continue
            if any(math.hypot(x - px, y - py) < 2.5 for px, py in puertas):
                continue
            if any(abs(x - px) < 1.2 and -0.3 < y - py < 1.0 for px, py in estorbos):
                continue
            if math.hypot(x - data['spawn']['x'], y - data['spawn']['y']) < 4:
                continue
            cand.append((x, y, t))
    rng.shuffle(cand)
    elegidos = []
    for x, y, t in cand:
        if all(math.hypot(x - e['x'], y - e['y']) >= 9 for e in elegidos):
            sprite = 'charco_grande' if t.startswith('asfalto') else rng.choice(['charco_mediano', 'charco_chico'])
            elegidos.append(dict(nombre=f'Charco_{len(elegidos) + 1}', sprite=sprite, x=x, y=y + .1))
        if len(elegidos) >= n:
            break
    return elegidos


def ventanas(data):
    res = []
    for p in data['props']:
        if p['nombre'].startswith('casa_'):
            res += [dict(x=round(p['x'] + dx, 4), y=round(p['y'] + dy, 4)) for dx, dy in VENTANAS_CASA]
    return res


# ---------------------------------------------------------------- tiempos con la velocidad de referencia
def comprobar_tiempos(data, vel_ref=5.5, velocidades=(5.0, 5.5, 6.5)):
    """El tiempo ideal de GameManager (max(10, recta x 1.5 / vel_ref + 4)) contra el camino más corto por calle y
    banqueta (Dijkstra en 8 direcciones, rejilla de 0.5 tiles, sin pasto) entre cada par de puntos de entrega y desde
    el spawn. El GameManager del Residencial elige al azar: se revisan todos los pares. Devuelve el peor cociente."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import dijkstra
    g = rejilla(data)
    R, C = g.shape
    F = 2
    piso = np.zeros((R * F, C * F), bool)
    for r in range(R):
        for c in range(C):
            if not g[r, c].startswith(('pasto', 'flores', 'camellon', 'orilla')):
                piso[(R - 1 - r) * F:(R - r) * F, c * F:(c + 1) * F] = True
    for b in data['lotes_solidos']:                          # setos y cercas de los patios
        piso[max(0, int(b['y'] * F)):int(math.ceil((b['y'] + b['alto']) * F)),
             max(0, int(b['x'] * F)):int(math.ceil((b['x'] + b['ancho']) * F))] = False
    pts = [('spawn', data['spawn']['x'], data['spawn']['y'])] + [(p['nombre'], p['x'], p['y']) for p in data['puntos_entrega']]
    for _, x, y in pts:
        piso[int(y * F) - 1:int(y * F) + 2, int(x * F) - 1:int(x * F) + 2] = True
    idx = -np.ones(piso.shape, int)
    idx[piso] = np.arange(piso.sum())
    fi, co, pe = [], [], []
    H, W = piso.shape
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        for y in range(H - dy):
            for x in range(max(0, -dx), W - max(0, dx)):
                if piso[y, x] and piso[y + dy, x + dx]:
                    fi.append(idx[y, x]); co.append(idx[y + dy, x + dx]); pe.append(math.hypot(dx, dy) / F)
    n = piso.sum()
    grafo = coo_matrix((pe, (fi, co)), shape=(n, n)).tocsr()
    fuentes = [idx[int(y * F), int(x * F)] for _, x, y in pts]
    dist = dijkstra(grafo, directed=False, indices=fuentes)
    res = {}
    injustos = []
    for v in velocidades:
        peor, cocientes, peor_justo = (0, None), [], 0
        for i, (na, xa, ya) in enumerate(pts):
            for j, (nb, xb, yb) in enumerate(pts):
                if j == 0 or i == j:
                    continue
                recta = math.hypot(xb - xa, yb - ya)
                camino = dist[i][fuentes[j]]
                assert np.isfinite(camino), ('no se llega', na, nb)
                ideal = max(10.0, recta * 1.5 / vel_ref + 4)
                q = camino / v / ideal
                cocientes.append(q)
                if v == min(velocidades) and q > 1.0:
                    injustos.append([na, nb])
                elif [na, nb] not in injustos:
                    peor_justo = max(peor_justo, q)
                if q > peor[0]:
                    peor = (q, (na, nb, round(recta, 1), round(camino, 1), round(camino / v, 1), round(ideal, 1)))
        res[v] = dict(peor=(round(peor[0], 2), peor[1]), media=round(float(np.mean(cocientes)), 2), pares=len(cocientes),
                      peor_justo=round(peor_justo, 2))
    return res, injustos


# ---------------------------------------------------------------- salida
def guardar(a, ruta):
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    Image.fromarray(a).save(ruta)


def vista(data, tiles_img, tiles_meta, atlas_img, atlas_meta, archivo):
    """Vista previa (sin props) del suelo mojado con baches y charcos, a 1/2."""
    pos = {t['name']: (t['x'], t['y']) for t in tiles_meta['tiles']}
    R, C = len(data['suelo']), len(data['suelo'][0])
    m = np.zeros((R * T, C * T, 4), np.uint8)
    for r in range(R):
        for c in range(C):
            x, y = pos[data['suelo'][r][c]]
            m[r * T:(r + 1) * T, c * T:(c + 1) * T] = tiles_img[y:y + T, x:x + T]
    spr = {s['name']: s for s in atlas_meta['sprites']}

    def pegar(nombre, x, y):
        s = spr[nombre]
        a = atlas_img[s['y']:s['y'] + s['h'], s['x']:s['x'] + s['w']].astype(float)
        px, py = int(x * T - s['w'] / 2), int((R - y) * T - s['h'] / 2)
        zona = m[py:py + s['h'], px:px + s['w']].astype(float)
        al = a[..., 3:4] / 255
        m[py:py + s['h'], px:px + s['w'], :3] = (zona[..., :3] * (1 - al) + a[..., :3] * al).astype(np.uint8)
    for b in data['baches']:
        pegar(f"bache_charco_{b['tamano']}", b['x'], b['y'])
    for ch in data['charcos']:
        pegar(ch['sprite'], ch['x'], ch['y'])
    Image.fromarray(m).resize((C * T // 2, R * T // 2), Image.NEAREST).save(archivo)


if __name__ == '__main__':
    import sys
    os.makedirs(OUT, exist_ok=True)
    data = leer('mapa_residencial_grande.json')
    tmeta = leer('tileset_suelo.json')
    ameta = leer('props_atlas.json')
    timg = np.array(Image.open(os.path.join(RES, tmeta['imagen'])).convert('RGBA'))
    aimg = np.array(Image.open(os.path.join(RES, ameta['imagen'])).convert('RGBA'))

    ctx = contexto(data)
    if '--buscar' in sys.argv:
        for b in buscar_baches(data, ctx):
            print(b)
        sys.exit()

    hoja_t, meta_t, mojados = tileset_mojado(tmeta, timg)
    data['suelo'] = [[t + '_mojado' if t + '_mojado' in mojados else t for t in fila] for fila in data['suelo']]
    data['variante'] = 'lluvia'
    data['baches'] = colocar_baches(data, ctx)
    data['charcos'] = colocar_charcos(data, ctx, data['baches'])
    import turbos
    data['turbos'] = turbos.colocar(data, turbos.CANTIDAD['lluvia'])   # 6 (el residencial trae 4): lejos de los baches
    data['ventanas'] = ventanas(data)
    # SeleccionPedidos no encadena estos pares (de -> a): ni con la moto más lenta (5.0) se llega en el tiempo ideal
    data['seleccion'] = dict(cantidad=8, distancia_minima=10.0, y_avenida=33.0, pares_injustos=comprobar_tiempos(data)[1])

    baches, piv = build_baches()
    nuevos = {k: v for k, v in baches.items() if k.startswith(('bache_charco', 'particula_gota'))}
    # de noche el agua del bache es turbia y oscura (más que el asfalto mojado y que los charcos decorativos, que son
    # claros): el mismo hoyo con borde roto de props_baches, con la paleta del agua cambiada solo aquí
    import props_baches as pb
    antes = pb.AGUA, pb.AGUA_D, pb.AGUA_L
    pb.AGUA, pb.AGUA_D, pb.AGUA_L = (0x24, 0x27, 0x40), (0x15, 0x16, 0x28), (0x7C, 0x88, 0xAE)
    nuevos.update(bache_charco_chico=pb.bache('chico', agua=True), bache_charco_grande=pb.bache('grande', agua=True))
    pb.AGUA, pb.AGUA_D, pb.AGUA_L = antes
    nuevos.update(charco_grande=charco_decorativo(48, 16, 18, 5, 1), charco_mediano=charco_decorativo(32, 12, 11, 4, 2),
                  charco_chico=charco_decorativo(20, 9, 6, 2, 3))
    for k in range(3):
        nuevos[f'salpicadura_{k + 1}'] = salpicadura(k)
    pivotes = {n: [0.5, 0.5] for n in nuevos}
    hoja_a, meta_a = extender_atlas(ameta, aimg, nuevos, pivotes)

    guardar(hoja_t, os.path.join(OUT, 'tileset_lluvia.png'))
    guardar(hoja_a, os.path.join(OUT, 'props_lluvia.png'))
    for nombre, obj in (('tileset_lluvia.json', meta_t), ('props_lluvia.json', meta_a), ('mapa_lluvia.json', data)):
        with open(os.path.join(OUT, nombre), 'w', encoding='utf-8') as f:
            json.dump(obj, f, ensure_ascii=False, indent=1 if nombre != 'mapa_lluvia.json' else None)
    for k in range(4):
        guardar(lluvia(k), os.path.join(OUT, f'lluvia_{k + 1}.png'))

    os.makedirs(os.path.dirname(ATLAS_UNITY), exist_ok=True)
    os.makedirs(EFECTOS_UNITY, exist_ok=True)
    shutil.copyfile(os.path.join(OUT, 'tileset_lluvia.png'), TILESET_UNITY)
    shutil.copyfile(os.path.join(OUT, 'props_lluvia.png'), ATLAS_UNITY)
    for k in range(4):
        shutil.copyfile(os.path.join(OUT, f'lluvia_{k + 1}.png'), os.path.join(EFECTOS_UNITY, f'lluvia_{k + 1}.png'))

    vista(data, hoja_t, meta_t, hoja_a, meta_a, os.path.join(OUT, 'vista_lluvia.png'))
    t, injustos = comprobar_tiempos(data)
    print(f'tileset {len(meta_t["tiles"])} tiles ({len(mojados)} mojados) | atlas {len(meta_a["sprites"])} sprites, alto {meta_a["alto"]}')
    print('baches:', [(b['nombre'], b['tamano'], b['x'], b['y']) for b in data['baches']])
    print('charcos:', len(data['charcos']), '| ventanas:', len(data['ventanas']))
    for v, r in t.items():
        print(f'tiempos a {v} tiles/s (referencia 5.5): {r["pares"]} pares, camino/ideal medio {r["media"]}, peor {r["peor"]}; '
              f'sin los pares que SeleccionPedidos evita, peor {r["peor_justo"]}')
    print('pares que no se encadenan:', len(injustos), 'de', t[5.0]['pares'])
