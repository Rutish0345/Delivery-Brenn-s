"""Hojas de personajes del mapa Residencial: 6 peatones, la dueña y el jefe de Brenn's.

Mismo formato que repartidor_sheet.png: 280x240, celdas de 56x80, 5 columnas x 3 filas
(fila 0 de frente, 1 de espaldas, 2 de lado mirando a la derecha; columna 0 quieto, 1-4 caminando),
pivote abajo-centro.

- Peatones: se parte del cuerpo y la caminata del repartidor (mismo tamaño, proporciones y animación);
  se quita la gorra, se recolorean piel y ropa y se dibuja encima el peinado y los accesorios, con el
  estilo de las referencias extras1/2/3 (a las que antes se les quita el fondo verde o de cuadros).
- Dueña: se toma de dueña.jpg (maqueta de Aseprite de 56x80 con fondo gris, marco, paleta y capas): se
  quita el fondo, se muestrea a la misma altura que el repartidor (67 px) y la sombra pasa a ser la
  sombra semitransparente del repartidor. Solo fila de frente, sin animar.
- Jefe: dueña.jpg trae un solo personaje, así que el jefe usa el uniforme y la paleta de esa maqueta
  (camisa rosa #FF6FA5, delantal morado #8C52FF, insignia dorada #FFD166, café #785037) sobre el cuerpo
  del repartidor. Solo fila de frente, sin animar.

Uso: python generar_personajes.py   (escribe en Assets/Sprits/Personajes y en pixel_town/personajes)
"""
import colorsys
import json
import os
import shutil

import numpy as np
from PIL import Image
from scipy import ndimage

AQUI = os.path.dirname(os.path.abspath(__file__))
REF = r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\referencias'
DESTINO = os.environ.get('PERSONAJES_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Personajes')
LIMPIAS = os.path.join(AQUI, 'referencias_sin_fondo')
SHEET_REPARTIDOR = os.path.join(AQUI, 'repartidor_sheet.png')
W, H, COLS, FILAS = 56, 80, 5, 3
FILA_NOMBRE = ['frente', 'espalda', 'lado']
SOMBRA = (0x1a, 0x14, 0x28)
VISTA = {0: 'frente', 1: 'espalda', 2: 'lado'}


# ============================================================ fondos de las referencias
def conectado_al_borde(mask):
    lab, _ = ndimage.label(mask)
    b = set(lab[0]) | set(lab[-1]) | set(lab[:, 0]) | set(lab[:, -1])
    b.discard(0)
    return np.isin(lab, list(b))


def quitar_fondo_extras(nombre):
    """extras1/2: fondo verde; extras3: cuadros grises falsos. Se borra solo el fondo conectado al
    exterior (y los huecos grandes del mismo color), así el contorno oscuro queda intacto; en las
    orillas se quita el halo verde que deja el JPG."""
    a = np.asarray(Image.open(os.path.join(REF, nombre)).convert('RGB')).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(2), a.min(2)
    cuadros = nombre == 'extras3.jpg'
    cand = ((mx - mn < 26) & (mx > 185)) if cuadros else ((g > r + 35) & (g > b + 35))
    fondo = conectado_al_borde(cand)
    lab, n = ndimage.label(cand & ~fondo)
    if n:
        tam = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
        fondo |= np.isin(lab, [i + 1 for i, t in enumerate(tam) if t > 400])
    alfa = ~fondo
    out = a.copy()
    if not cuadros:
        orilla = alfa & ndimage.binary_dilation(fondo, iterations=2)
        out[..., 1] = np.where(orilla, np.minimum(g, np.maximum(r, b) + 8), g)
    lab, n = ndimage.label(alfa)
    tam = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    alfa &= np.isin(lab, [i + 1 for i, t in enumerate(tam) if t >= 60])
    return np.dstack([out.astype(np.uint8), (alfa * 255).astype(np.uint8)])


# ============================================================ dueña desde dueña.jpg
def duena_desde_jpg():
    a = np.asarray(Image.open(os.path.join(REF, 'dueña.jpg')).convert('RGB')).astype(int)
    y0, y1, x0, x1 = 240, 935, 175, 480          # lienzo con el personaje (sin paleta, capas ni texto)
    z = a[y0:y1, x0:x1]
    mx, mn = z.max(2), z.min(2)
    gris = (mx - mn < 22) & (mx > 196)
    fondo = conectado_al_borde(gris)
    m = ~fondo
    # se queda con el personaje (la componente grande) y su sombra; fuera restos de la paleta
    lab, n = ndimage.label(m)
    tam = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    m = lab == (int(np.argmax(tam)) + 1)
    sin_fondo = np.dstack([z.astype(np.uint8), (m * 255).astype(np.uint8)])
    ys, xs = np.nonzero(m)
    top, bot, lft, rgt = ys.min(), ys.max(), xs.min(), xs.max()
    paso = (bot - top + 1) / 67                    # misma altura que el repartidor (pelo a sombra)
    h = 67
    w = int(round((rgt - lft + 1) / paso))
    out = np.zeros((h, w, 4), np.uint8)
    rr = max(1, int(paso * .25))
    for j in range(h):
        for i in range(w):
            cy, cx = int(top + (j + .5) * paso), int(lft + (i + .5) * paso)
            blk = z[cy - rr:cy + rr + 1, cx - rr:cx + rr + 1].reshape(-1, 3)
            mk = m[cy - rr:cy + rr + 1, cx - rr:cx + rr + 1].reshape(-1)
            if mk.mean() < .5:
                continue
            out[j, i, :3] = np.median(blk[mk], 0)
            out[j, i, 3] = 255
    # la sombra gris del lienzo -> sombra semitransparente del repartidor
    rgb = out[..., :3].astype(int)
    sat = rgb.max(2) - rgb.min(2)
    sombra = (out[..., 3] > 0) & (sat < 20) & (rgb.max(2) > 120)
    sombra[:h - 8] = False
    nucleo = ndimage.binary_erosion(sombra)
    out[sombra] = (*SOMBRA, 80)
    out[nucleo] = (*SOMBRA, 120)
    # pocos colores, como pixel art (solo lo opaco)
    op = out[..., 3] == 255
    pix = out[op][:, :3]
    q = Image.fromarray(pix.reshape(1, -1, 3)).quantize(28, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    out[op, :3] = np.asarray(q.convert('RGB')).reshape(-1, 3)
    celda = np.zeros((H, W, 4), np.uint8)
    ox = (W - w) // 2
    celda[76 - h + 1:77, ox:ox + w] = out      # la sombra termina en la fila 76, como el repartidor
    return celda, sin_fondo


# ============================================================ cuerpo base (repartidor)
def hsv(c):
    return colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)


def clasificar(c, y):
    """Material de un píxel del repartidor (y relativa: sin el balanceo de la caminata)."""
    r, g, b, a = (int(v) for v in c)
    if a == 0:
        return '.'
    if (r, g, b) == SOMBRA and a < 255:
        return 's'
    h, s, v = hsv((r, g, b))
    h *= 360
    if v < 0.16:
        return 'O'
    if s < 0.16 and v > 0.55:
        return 'W'
    if 240 <= h <= 290 and s > 0.35:
        return 'J'
    if 230 <= h <= 300 and s <= 0.35:
        return 'P'
    if 40 <= h <= 60 and s > 0.5 and v > 0.8:
        return 'Y'
    if (h >= 330 or h <= 8) and s > 0.45:
        return 'C' if y <= 22 else 'c'
    if 8 < h < 45 and v > 0.62:
        return 'S'
    if (h <= 45 or h >= 330) and v <= 0.62:
        return 'H'
    return '?'


CARA = {'frente': (16, 32, 21, 29), 'lado': (21, 35, 20, 28), 'espalda': None}   # x0, x1, y0, y1


def en_cara(vista, x, y):
    c = CARA[vista]
    return c is not None and c[0] <= x <= c[1] and c[2] <= y <= c[3]


def leer_base():
    im = np.asarray(Image.open(SHEET_REPARTIDOR).convert('RGBA'))
    celdas = {}
    for f in range(FILAS):
        for c in range(COLS):
            celda = im[f * H:(f + 1) * H, c * W:(c + 1) * W].copy()
            ys = [y for y in range(H) for x in range(W)
                  if celda[y, x, 3] and clasificar(celda[y, x], y) == 'C']
            bob = min(ys) - 8
            mat = np.full((H, W), '.', dtype='<U1')
            for y in range(H):
                for x in range(W):
                    m = clasificar(celda[y, x], y - bob)
                    yr = y - bob
                    vista = VISTA[f]
                    if m == '?':
                        m = 'C' if yr <= 21 else 'S'
                    if m in ('H', 'c'):
                        if yr <= 28 and not en_cara(vista, x, yr):
                            m = 'h'                       # pelo
                        elif yr >= 60:
                            m = 'O'                       # brillo de zapato: queda como está
                        elif en_cara(vista, x, yr) and m == 'H' and 22 <= yr <= 25:
                            m = 'i'                       # iris: se quedan
                        else:
                            m = 'k'                       # sombra de piel (cuello, manos, orejas)
                    if m == 'J' and yr >= 53:
                        m = 'P'
                    if m == 'W' and en_cara(vista, x, yr):
                        m = 'e'                           # blanco de los ojos
                    mat[y, x] = m
            celdas[f, c] = (celda, mat, bob)
    return celdas


def cabezas(celdas):
    """Silueta de la cabeza (gorra + pelo, sin la visera) de cada vista, sin balanceo."""
    out = {}
    for f in range(FILAS):
        celda, mat, bob = celdas[f, 0]
        m = np.isin(mat, ['C', 'h'])
        m = np.roll(m, -bob, axis=0)
        vista = VISTA[f]
        if vista == 'frente':
            m[:, 36:] = False
        elif vista == 'lado':
            m[16:20, 35:] = False
            m[:, 37:] = False
        m = ndimage.binary_fill_holes(m)
        out[vista] = redondear(m)
    return out


def redondear(m, alto=9):
    """La gorra es cuadrada arriba; la cabeza no: se recortan las esquinas de arriba con una elipse."""
    ys, xs = np.nonzero(m)
    top, x0, x1 = ys.min(), xs.min(), xs.max()
    cx, rx = (x0 + x1) / 2, (x1 - x0 + 1) / 2
    m = m.copy()
    for y in range(top, top + alto):
        t = (top + alto - y - 0.5) / alto
        medio = rx * np.sqrt(max(0.0, 1 - t * t))
        for x in range(W):
            if m[y, x] and abs(x + 0.5 - cx - 0.5) > medio:
                m[y, x] = False
    return m


# ============================================================ color
def mezcla_color(c_src, ref, objetivo, contraste=0.9):
    """Recolorea conservando el sombreado: tono del objetivo, valor y saturación desplazados igual
    que el píxel original respecto al promedio del material."""
    h0, s0, v0 = hsv(c_src)
    _, rs, rv = ref
    th, ts, tv = hsv(objetivo)
    v = min(1, max(0.04, tv + (v0 - rv) * contraste))
    s = min(1, max(0, ts * (s0 / rs if rs > 1e-3 else 1)))
    r, g, b = colorsys.hsv_to_rgb(th, s, v)
    return int(round(r * 255)), int(round(g * 255)), int(round(b * 255))


def referencias_material(celdas):
    acum = {}
    for (f, c), (celda, mat, _) in celdas.items():
        for m in 'JPWSkY':
            sel = celda[mat == m][:, :3]
            if len(sel):
                acum.setdefault(m, []).append(sel)
    ref = {}
    for m, l in acum.items():
        a = np.concatenate(l) / 255
        hs = np.array([colorsys.rgb_to_hsv(*p) for p in a])
        ref[m] = (hs[:, 0].mean(), hs[:, 1].mean(), hs[:, 2].mean())
    ref['k'] = ref['S']   # la sombra de piel se mueve junto con la piel
    return ref


def hexrgb(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def tono(c, dv=0.0, ds=0.0):
    h, s, v = hsv(c)
    r, g, b = colorsys.hsv_to_rgb(h, min(1, max(0, s + ds)), min(1, max(0, v + dv)))
    return int(round(r * 255)), int(round(g * 255)), int(round(b * 255))


# ============================================================ peinados y accesorios
def peinado(estilo, vista, cabeza):
    """Máscara del pelo (sin balanceo) para la vista. cabeza = silueta de gorra+pelo del repartidor."""
    m = cabeza.copy()
    Y, X = np.mgrid[0:H, 0:W]
    if estilo == 'corto':
        # más pegado al cráneo que la gorra: una fila menos arriba y a los lados, nuca descubierta
        m = ndimage.binary_erosion(m, iterations=1) | (m & (Y >= 18) & (Y <= 22))
        if vista == 'frente':
            m &= ~((Y >= 20) & (X >= 20) & (X <= 29))      # frente despejada: se ve la cara completa
        m &= Y <= 25
        m = redondear(m, 7)
    elif estilo == 'rizado':
        grande = ndimage.binary_dilation(m, iterations=1)
        bultos = grande & ~m & (((X + Y) % 3) != 0)
        m = m | bultos
        if vista in CARA and CARA[vista]:
            x0, x1, y0, y1 = CARA[vista]
            m[y0 + 1:y1 + 1, x0 + 1:x1] &= cabeza[y0 + 1:y1 + 1, x0 + 1:x1]
    elif estilo == 'largo':
        if vista == 'frente':
            ola = (np.sin(Y / 2.2) > 0.3)
            m |= (Y >= 19) & (Y <= 41) & (((X >= 12) & (X <= 15)) | ((X >= 33) & (X <= 36)))
            m |= (Y >= 19) & (Y <= 42) & (((X == 11) | (X == 37)) & ola)
            m &= ~((Y >= 40) & ((X == 12) | (X == 36)) & ~ola)
        elif vista == 'espalda':
            m |= (Y >= 19) & (Y <= 41) & (X >= 14) & (X <= 35)
            m |= (Y == 42) & (X >= 15) & (X <= 34) & ((X % 3) != 0)
            m |= (Y >= 22) & (Y <= 40) & ((X == 13) | (X == 36)) & (np.sin(Y / 2.2) > 0.3)
        else:
            m |= (Y >= 17) & (Y <= 41) & (X >= 14) & (X <= 22)
            m |= (Y == 42) & (X >= 15) & (X <= 21) & ((X % 3) != 0)
            m |= (Y >= 20) & (Y <= 40) & (X == 13) & (np.sin(Y / 2.2) > 0.3)
    elif estilo == 'cola':
        if vista == 'espalda':
            for y in range(20, 39):
                medio = 3 if y < 30 else 2 if y < 35 else 1
                m[y, 24 - medio:25 + medio] = True
        elif vista == 'lado':
            izq = np.nonzero(cabeza[14])[0].min()          # nuca: la coleta sale pegada a la cabeza
            for y in range(13, 33):
                x = izq + 1 - min(4, (y - 13) // 3)
                ancho = 4 if y < 22 else 3 if y < 28 else 2
                m[y, x - ancho:x + 1] = True
    return m


def textura_pelo(m, estilo, base):
    """Colores del pelo: orilla oscura, mechones y brillo arriba a la izquierda."""
    out = {}
    if not m.any():
        return out
    ys, xs = np.nonzero(m)
    top, x0, x1 = ys.min(), xs.min(), xs.max()
    borde = m & ~ndimage.binary_erosion(m)
    oscuro, medio, claro = tono(base, -0.24), tono(base, -0.1), tono(base, +0.12, -0.05)
    for y, x in zip(ys, xs):
        c = base
        if estilo == 'rizado':
            k = (x + 2 * (y // 2)) % 4
            if k == 0:
                c = oscuro
            elif k == 2 and y % 2 == 0:
                c = claro if y < top + 8 else medio
        else:
            if (2 * x + y // 4) % 5 == 0:
                c = medio
            if y < top + 4 and x < x0 + (x1 - x0) * 0.6 and (x + y) % 3 == 0:
                c = claro
        if borde[y, x]:
            c = oscuro
        out[y, x] = c
    return out


def flor(c, cx, cy, petalo, centro):
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1), (-1, -1), (1, 1)):
        c[(cy + dy, cx + dx)] = petalo
    c[(cy, cx)] = centro


LENTES = {'frente': [(21, 22, 26, 25), (28, 22, 33, 25)], 'lado': [(29, 22, 34, 25)]}


# ============================================================ variantes
PEATONES = [
    dict(n=1, estilo='largo', pelo='#553a2d', piel='#f3cea7', chaqueta='#2f2a28', camisa='#1f1a18',
         botones=None, pantalon='#45403a', nota='extras1: pelo largo ondulado café oscuro, ropa negra'),
    dict(n=2, estilo='rizado', pelo='#602d1f', piel='#dbb48f', chaqueta='#212d47', camisa='#e9dcc6',
         rayas='#4c515e', botones=None, pantalon='#313a52', lentes=True,
         nota='extras2: rizado con lentes, chaqueta marino, camisa a rayas y jeans'),
    dict(n=3, estilo='largo', pelo='#a1673f', piel='#e8c49a', chaqueta='#151313', camisa='#f4d96b',
         botones='#f4d96b', pantalon='#e8c49a', falda='#151313', orla='#f4d96b', flor=('#f4d96b', '#c98a1e'),
         nota='extras3: pelo largo castaño claro con flor, vestido negro con amarillo'),
    dict(n=4, estilo='rizado', pelo='#231c1c', piel='#a86e4a', chaqueta='#64753a', camisa='#efe6cf',
         botones='#d9c27a', pantalon='#a08a5e', nota='mezcla: rizado negro, chaqueta oliva, pantalón caqui'),
    dict(n=5, estilo='cola', pelo='#8a3b22', piel='#f0c8a0', chaqueta='#2f7f86', camisa='#f2efe8',
         botones=None, pantalon='#2a2a33', falda='#5d6470', orla='#3e434d', liga='#e75f87',
         nota='mezcla: coleta pelirroja, chaqueta turquesa, falda gris y medias'),
    dict(n=6, estilo='cola', pelo='#d9b25c', piel='#c58c64', chaqueta='#d9534f', camisa='#f7f3ea',
         botones='#f4d96b', pantalon='#3d5a80', liga='#8c52ff', flor=('#ff9cc4', '#e5207d'),
         nota='mezcla: coleta rubia con broche, chaqueta coral y jeans'),
]
JEFE = dict(estilo='corto', pelo='#4a3326', piel='#e9b48f', chaqueta='#ff6fa5', camisa='#ffa9c8',
            botones=None, pantalon='#3b2a24', delantal='#8c52ff', insignia='#ffd166', bigote=True,
            nota="jefe de Brenn's: uniforme de la maqueta dueña.jpg (camisa rosa, delantal morado, insignia dorada)")


def falda_mascara(vista, Y, X):
    if vista == 'lado':
        izq = 18 - (Y - 49) // 4      # pegada al cuerpo, con un poco de vuelo atrás
        der = 29 + (Y - 49) // 6
    else:
        izq = 17 - (Y - 49) // 5
        der = 33 + (Y - 49) // 5
    return (Y >= 49) & (Y <= 60) & (X >= izq) & (X <= der)


def dibujar(celdas, ref, cabeza, v, f, c):
    celda, mat, bob = celdas[f, c]
    vista = VISTA[f]
    out = np.zeros_like(celda)
    piel = hexrgb(v['piel'])
    cols = {'J': hexrgb(v['chaqueta']), 'W': hexrgb(v['camisa']), 'P': hexrgb(v['pantalon']),
            'S': piel, 'k': piel}
    for y in range(H):
        for x in range(W):
            m = mat[y, x]
            px = celda[y, x]
            if m in ('C', 'h', '.'):
                continue
            yr = y - bob
            if m == 'Y':
                if v.get('botones'):
                    out[y, x] = (*hexrgb(v['botones']), 255)
                else:
                    out[y, x] = (*mezcla_color(px[:3], ref['J'], cols['J']), 255)
                continue
            if m in cols:
                obj = cols[m]
                if m == 'W' and v.get('rayas') and yr % 2 == 0:
                    obj = hexrgb(v['rayas'])
                ref_m = ref['W'] if m == 'W' else ref[m]
                if m == 'P' and v['pantalon'] == v['piel']:
                    ref_m = ref['P']
                # la sombra de piel del repartidor es muy marcada (cuello y mandíbula): en la cara se
                # vuelve un solo tono apenas más oscuro que la piel para que no parezca barba
                if m == 'k' and en_cara(vista, x, yr + 1):
                    out[y, x] = (*tono(piel, -0.1, 0.04), 255)
                else:
                    out[y, x] = (*mezcla_color(px[:3], ref_m, obj, 0.5 if m == 'k' else 0.9), 255)
            else:
                out[y, x] = px
    Y, X = np.mgrid[0:H, 0:W]
    # falda / vestido: tapa el pantalón y el hueco entre las piernas, no la chaqueta ni las manos
    if v.get('falda'):
        fm = falda_mascara(vista, Y - bob, X)
        tapa = fm & (np.isin(mat, ['P', '.', 's']) | ((mat == 'O') & (Y - bob >= 52)))
        base = hexrgb(v['falda'])
        borde = fm & ~ndimage.binary_erosion(fm)
        for y, x in zip(*np.nonzero(tapa)):
            yr = y - bob
            col = base
            if yr >= 59 and v.get('orla'):
                col = hexrgb(v['orla'])
            elif borde[y, x]:
                col = tono(base, -0.12)
            elif (x + yr // 3) % 6 == 0:
                col = tono(base, -0.06)
            out[y, x] = (*col, 255)
    # delantal del jefe: peto, falda del delantal y tirantes, con la insignia dorada
    if v.get('delantal') and vista == 'frente':
        d = hexrgb(v['delantal'])
        peto = (Y - bob >= 34) & (Y - bob <= 46) & (X >= 20) & (X <= 31)
        falda = (Y - bob >= 47) & (Y - bob <= 62) & (X >= 18) & (X <= 33)
        tir = ((Y - bob >= 31) & (Y - bob <= 33) & ((X == 20) | (X == 31)))
        dm = (peto | falda | tir) & (mat != '.') & ~np.isin(mat, ['S', 'k'])
        dm |= falda & (mat == '.')
        borde = dm & ~ndimage.binary_erosion(dm)
        for y, x in zip(*np.nonzero(dm)):
            col = tono(d, -0.25) if borde[y, x] else (tono(d, +0.08) if x < 23 else d)
            if Y[y, x] - bob == 47:
                col = tono(d, -0.18)          # costura de la cintura
            out[y, x] = (*col, 255)
        for dy, dx, col in ((37, 28, hexrgb(v['insignia'])), (38, 27, hexrgb(v['insignia'])),
                            (38, 29, hexrgb(v['insignia'])), (39, 28, hexrgb(v['insignia'])),
                            (38, 28, tono(hexrgb(v['insignia']), -0.2))):
            out[dy + bob, dx] = (*col, 255)
    # pelo
    pm = np.roll(peinado(v['estilo'], vista, cabeza[vista]), bob, axis=0)
    for (y, x), col in textura_pelo(pm, v['estilo'], hexrgb(v['pelo'])).items():
        out[y, x] = (*col, 255)
    if v['estilo'] == 'corto':
        # lo que tapaba la gorra y el pelo corto ya no cubre (frente y sienes) se rellena con piel
        cab = np.roll(cabeza[vista], bob, axis=0)
        hueco = cab & ~pm & (Y - bob >= 17) & (out[..., 3] == 0)
        for y, x in zip(*np.nonzero(hueco)):
            out[y, x] = (*(tono(piel, -0.04) if Y[y, x] - bob == 17 else piel), 255)
    # liga de la coleta, flor, lentes y bigote
    if v.get('liga'):
        lc = hexrgb(v['liga'])
        if vista == 'espalda':
            for y in (27, 28):
                for x in range(22, 27):
                    out[y + bob, x] = (*lc, 255)
        elif vista == 'lado':
            izq = np.nonzero(cabeza['lado'][14])[0].min()
            for y in range(14, 18):
                out[y + bob, izq - 1] = (*lc, 255)
                out[y + bob, izq - 2] = (*lc, 255)
    if v.get('flor'):
        p, ce = hexrgb(v['flor'][0]), hexrgb(v['flor'][1])
        pos = {'frente': (31, 13), 'espalda': (18, 15), 'lado': (24, 12)}[vista]
        d = {}
        flor(d, pos[0], pos[1] + bob, p, ce)
        for (y, x), col in d.items():
            out[y, x] = (*col, 255)
    if v.get('lentes') and vista in LENTES:
        armazon = (0x16, 0x12, 0x1e, 255)
        for x0, y0, x1, y1 in LENTES[vista]:
            for x in range(x0, x1 + 1):
                out[y0 + bob, x] = armazon
                out[y1 + bob, x] = armazon
            for y in range(y0, y1 + 1):
                out[y + bob, x0] = armazon
                out[y + bob, x1] = armazon
        if vista == 'frente':
            out[23 + bob, 27] = armazon
        else:
            for x in range(24, 29):
                out[23 + bob, x] = armazon
    if v.get('bigote') and vista == 'frente':
        b = tono(hexrgb(v['pelo']), -0.05)
        for x in range(22, 30):
            out[27 + bob, x] = (*b, 255)
        out[28 + bob, 22] = (*b, 255)
        out[28 + bob, 29] = (*b, 255)
    return out


# ============================================================ hojas y JSON
def frames_meta(nombre, filas):
    meta = []
    for f in filas:
        for c in range(COLS):
            meta.append(dict(name=f'{nombre}_{FILA_NOMBRE[f]}_{c}', fila=f, columna=c,
                             x=c * W, y=f * H, w=W, h=H))
    return meta


def guardar_hoja(nombre, celdas_img, filas):
    hoja = np.zeros((H * FILAS, W * COLS, 4), np.uint8)
    for (f, c), im in celdas_img.items():
        hoja[f * H:(f + 1) * H, c * W:(c + 1) * W] = im
    Image.fromarray(hoja).save(os.path.join(DESTINO, f'{nombre}.png'))
    return dict(imagen=f'{nombre}.png', ancho=W * COLS, alto=H * FILAS, sprites=frames_meta(nombre, filas))


def main():
    os.makedirs(LIMPIAS, exist_ok=True)
    for n in ('extras1.jpg', 'extras2.jpg', 'extras3.jpg'):
        Image.fromarray(quitar_fondo_extras(n)).save(os.path.join(LIMPIAS, n.replace('.jpg', '_sin_fondo.png')))

    celdas = leer_base()
    ref = referencias_material(celdas)
    cabeza = cabezas(celdas)
    hojas = []
    vista_previa = []
    for v in PEATONES:
        nombre = f"peaton_{v['n']}"
        imgs = {(f, c): dibujar(celdas, ref, cabeza, v, f, c) for f in range(FILAS) for c in range(COLS)}
        hojas.append(dict(guardar_hoja(nombre, imgs, range(FILAS)), personaje=nombre, variante=v['n'], nota=v['nota']))
        vista_previa.append((nombre, imgs))

    # dueña (dueña.jpg) y jefe: solo fila de frente, la pose quieta en las 5 columnas
    duena, duena_sin_fondo = duena_desde_jpg()
    Image.fromarray(duena_sin_fondo).save(os.path.join(LIMPIAS, 'dueña_sin_fondo.png'))
    imgs = {(0, c): duena for c in range(COLS)}
    hojas.append(dict(guardar_hoja('duena', imgs, [0]), personaje='duena', nota='dueña.jpg sin fondo, 67 px de alto'))
    vista_previa.append(('duena', imgs))
    jefe = dibujar(celdas, ref, cabeza, JEFE, 0, 0)
    imgs = {(0, c): jefe for c in range(COLS)}
    hojas.append(dict(guardar_hoja('jefe', imgs, [0]), personaje='jefe', nota=JEFE['nota']))
    vista_previa.append(('jefe', imgs))

    meta = dict(formato='como repartidor_sheet.png: celdas de 56x80, 5 columnas x 3 filas; fila 0 frente, '
                        '1 espaldas, 2 lado (mira a la derecha); columna 0 quieto, 1-4 caminando',
                origen='arriba-izquierda (Unity mide desde abajo)', pivote=[0.5, 0.0], ppu=32,
                celda=dict(w=W, h=H), hojas=hojas)
    with open(os.path.join(DESTINO, 'personajes.json'), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=2, ensure_ascii=False)
    shutil.copyfile(os.path.join(DESTINO, 'personajes.json'), os.path.join(AQUI, 'personajes.json'))

    # vista previa: todos lado a lado (peatones con sus 3 vistas quietas y un paso; dueña y jefe de frente), x3
    esc, sep = 3, 6
    cols_prev = []
    for nombre, imgs in vista_previa:
        cols_prev.append([imgs[k] for k in sorted(imgs) if k[1] in (0, 2)])
    alto = H * 3
    ancho = len(cols_prev) * (W * 2 + sep) + sep
    lienzo = Image.new('RGBA', (ancho, alto + 14), (178, 196, 160, 255))
    x = sep
    from PIL import ImageDraw
    d = ImageDraw.Draw(lienzo)
    for (nombre, _), ims in zip(vista_previa, cols_prev):
        for i, im in enumerate(ims):
            lienzo.alpha_composite(Image.fromarray(im), (x + (i % 2) * W, (i // 2) * H))
        d.text((x + 4, alto + 1), nombre, fill=(30, 30, 40, 255))
        x += W * 2 + sep
    lienzo = lienzo.resize((lienzo.width * esc, lienzo.height * esc), Image.NEAREST)
    lienzo.save(os.path.join(DESTINO, 'personajes_vista_previa.png'))
    shutil.copyfile(os.path.join(DESTINO, 'personajes_vista_previa.png'), os.path.join(AQUI, 'personajes_vista_previa.png'))
    print('hojas:', [h['imagen'] for h in hojas])


if __name__ == '__main__':
    main()
