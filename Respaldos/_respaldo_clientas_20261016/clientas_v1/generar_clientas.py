"""Clientas que salen de su casa a recibir el pedido, y la flecha guía del repartidor.

Clientas: 8 diseños nuevos (peinados y ropa que no usan el repartidor, los peatones ni la dueña) con la
paleta del juego: rosa #FF6FA5, morado #8C52FF, dorado #FFD166, teal #5ED3D1, verde #7BD88F y naranja
#FF9A5C. Mismo formato que repartidor_sheet.png (280x240, celdas de 56x80, fila 0 frente, 1 espaldas,
2 lado mirando a la derecha; columna 0 quieta y 1-4 caminando). El cuerpo y la caminata salen del
repartidor, igual que los peatones (generar_personajes.py), para que todos tengan el mismo tamaño.

Flecha guía: 32x32, apunta a la derecha, dorada con contorno oscuro y un brillo.

Uso: python generar_clientas.py
"""
import json
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from generar_personajes import (AQUI, CARA, COLS, FILAS, H, VISTA, W, cabezas, en_cara, frames_meta, hexrgb,
                                leer_base, mezcla_color, redondear, referencias_material, textura_pelo, tono)

PERSONAJES = os.environ.get('PERSONAJES_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Personajes')
DESTINO = os.path.join(PERSONAJES, 'Clientas')
RUTA_FLECHA = os.environ.get('FLECHA_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI\flecha_guia.png')

ROSA, MORADO, DORADO, TEAL, VERDE, NARANJA = '#ff6fa5', '#8c52ff', '#ffd166', '#5ed3d1', '#7bd88f', '#ff9a5c'
CREMA, CONTORNO = '#fff1d0', '#2a1f40'

# estilo de pelo, prenda principal y colores de cada clienta
CLIENTAS = [
    dict(n=1, pelo_estilo='chongo', pelo='#4a2c22', piel='#f2c8a2', prenda='delantal',
         blusa=TEAL, interior=CREMA, falda=MORADO, piernas='#f2c8a2', delantal=ROSA, zapatos='#5b3a2e',
         nota='chongo alto, blusa teal, falda morada y delantal rosa'),
    dict(n=2, pelo_estilo='rulos', pelo='#7a4a2c', piel='#e9b48f', prenda='bata',
         blusa=MORADO, interior=ROSA, bata=MORADO, cinto=ROSA, piernas='#e9b48f', rulos=ROSA, pantuflas=ROSA,
         nota='bata de casa morada con cinto rosa, rulos rosas y pantuflas'),
    dict(n=3, pelo_estilo='trenzas', pelo='#2b1d18', piel='#b97a52', prenda='vestido',
         blusa=VERDE, interior=CREMA, falda=VERDE, piernas='#b97a52', ligas=NARANJA, zapatos='#8c52ff',
         nota='dos trenzas con ligas naranjas, vestido verde con cuello crema'),
    dict(n=4, pelo_estilo='bob', pelo='#141019', piel='#f6d3b3', prenda='diario',
         blusa=NARANJA, interior=NARANJA, pantalon='#3d5a80', zapatos='#f2efe8',
         nota='bob negro con fleco, playera naranja y jeans'),
    dict(n=5, pelo_estilo='panoleta', pelo='#5a3424', piel='#c98d68', prenda='delantal',
         blusa=ROSA, interior=ROSA, falda='#6b4a8a', piernas='#c98d68', delantal=TEAL, panoleta=DORADO,
         zapatos='#3a2a24', nota='pañoleta dorada, blusa rosa y delantal teal'),
    dict(n=6, pelo_estilo='chonguitos', pelo='#a8542f', piel='#f0c8a0', prenda='vestido',
         blusa=ROSA, interior='#ffffff', falda=ROSA, lunares='#ffffff', piernas='#f0c8a0', zapatos=MORADO,
         nota='dos chonguitos pelirrojos, vestido rosa con lunares'),
    dict(n=7, pelo_estilo='abuela', pelo='#c9c3d6', piel='#eac0a0', prenda='bata', lentes=True,
         blusa=TEAL, interior=CREMA, bata=TEAL, cinto=DORADO, cuadros='#3fa8a6', piernas='#eac0a0', pantuflas=VERDE,
         nota='abuela de pelo canoso con chongo bajo, lentes y bata teal a cuadros'),
    dict(n=8, pelo_estilo='melena', pelo='#8a3b22', piel='#d9a07a', prenda='diario', diadema=DORADO,
         blusa=VERDE, interior=VERDE, falda=MORADO, piernas='#3a2f4a', zapatos='#2a1f2a',
         nota='media melena con diadema dorada, suéter verde, falda morada y medias'),
]


# columnas (x) donde están los ojos en cada vista: solo ahí se conserva el café del iris
OJOS = {'frente': (21, 32), 'lado': (28, 35), 'espalda': (0, -1)}


# ============================================================ peinados
def recogido(cabeza, vista, Y, X):
    """Pelo pegado al cráneo, sin fleco: frente despejada (la base de chongo, rulos, trenzas...)."""
    m = ndimage.binary_erosion(cabeza, iterations=1) | (cabeza & (Y >= 18) & (Y <= 22))
    if vista == 'frente':
        m &= ~((Y >= 19) & (X >= 19) & (X <= 30))
    m &= Y <= 25
    return redondear(m, 7)


def circulo(m, cx, cy, r):
    Y, X = np.mgrid[0:H, 0:W]
    m |= (X + .5 - cx) ** 2 + (Y + .5 - cy) ** 2 <= r * r


def peinado(estilo, vista, cabeza):
    Y, X = np.mgrid[0:H, 0:W]
    if estilo == 'bob':
        m = cabeza.copy()
        if vista == 'frente':
            m |= (Y >= 19) & (Y <= 28) & (((X >= 14) & (X <= 17)) | ((X >= 31) & (X <= 35)))
            m &= ~((Y >= 21) & (X >= 18) & (X <= 30))         # fleco recto hasta la fila 20
        elif vista == 'espalda':
            m |= (Y >= 19) & (Y <= 28) & (X >= 14) & (X <= 35)
        else:
            m |= (Y >= 17) & (Y <= 28) & (X >= 15) & (X <= 23)
        return redondear(m, 8)
    if estilo == 'melena':
        m = cabeza.copy()
        ola = np.sin(Y / 1.8) > 0
        if vista == 'frente':
            m |= (Y >= 19) & (Y <= 32) & (((X >= 13) & (X <= 16)) | ((X >= 32) & (X <= 36)))
            m |= (Y >= 22) & (Y <= 33) & (((X == 12) | (X == 37)) & ola)
            m &= ~((Y >= 21) & (X >= 17) & (X <= 31))
        elif vista == 'espalda':
            m |= (Y >= 19) & (Y <= 32) & (X >= 14) & (X <= 35)
            m |= (Y == 33) & (X >= 15) & (X <= 34) & ((X % 2) == 0)
        else:
            m |= (Y >= 17) & (Y <= 32) & (X >= 14) & (X <= 23)
            m |= (Y == 33) & (X >= 15) & (X <= 22) & ((X % 2) == 0)
        return redondear(m, 8)
    if estilo == 'panoleta':
        return recogido(cabeza, vista, Y, X) & (Y >= 17) & (Y <= 21)   # solo el pelo que asoma bajo la pañoleta
    m = recogido(cabeza, vista, Y, X)
    if estilo == 'chongo':
        cx = {'frente': 24.5, 'espalda': 24.5, 'lado': 19}[vista]
        circulo(m, cx, 7.5, 4.2)
    elif estilo == 'chonguitos':
        if vista == 'lado':
            circulo(m, 19, 9, 3.6)
        else:
            circulo(m, 15.5, 10, 3.6)
            circulo(m, 33.5, 10, 3.6)
    elif estilo == 'abuela':
        if vista == 'espalda':
            circulo(m, 24.5, 21, 4.2)
        elif vista == 'lado':
            circulo(m, 14.5, 18, 4.2)
    elif estilo == 'trenzas':
        if vista == 'frente':
            puntas = [(15, 22, 40), (33, 22, 40)]
        elif vista == 'espalda':
            puntas = [(19, 22, 40), (29, 22, 40)]
        else:
            puntas = [(16, 20, 38)]
        for x0, y0, y1 in puntas:
            for y in range(y0, y1 + 1):
                desv = 1 if (y // 2) % 2 else 0           # el tejido: cada dos filas se corre un píxel
                m[y, x0 + desv:x0 + desv + 3] = True
    return m


def accesorios(out, v, vista, bob, cabeza):
    """Rulos, pañoleta, diadema, ligas de las trenzas y lentes, encima del pelo."""
    def poner(y, x, col):
        if 0 <= y < H and 0 <= x < W:
            out[y, x] = (*col, 255)

    if v.get('rulos'):
        c, c2 = hexrgb(v['rulos']), tono(hexrgb(v['rulos']), -0.2)
        filas = {'frente': [(10, (17, 22, 27)), (14, (15, 20, 25, 30))],
                 'espalda': [(10, (17, 22, 27)), (14, (15, 20, 25, 30)), (18, (17, 22, 27))],
                 'lado': [(10, (18, 23)), (14, (16, 21, 26))]}[vista]
        for y, xs in filas:
            for x in xs:
                for dx in range(4):
                    poner(y + bob, x + dx, c)
                    poner(y + 1 + bob, x + dx, c2)
    if v.get('panoleta'):
        c = hexrgb(v['panoleta'])
        oscuro, claro = tono(c, -0.22), tono(c, +0.06, -0.15)
        m = np.roll(redondear(cabeza & (np.mgrid[0:H, 0:W][0] <= 18), 8), bob, axis=0)
        borde = m & ~ndimage.binary_erosion(m)
        doblez = m & ~np.roll(m, -1, axis=0)                 # la orilla doblada de la tela, sobre la frente
        doblez |= np.roll(doblez, -1, axis=0) & m
        lunar = hexrgb(CREMA)
        for y, x in zip(*np.nonzero(m)):
            if borde[y, x]:
                col = oscuro
            elif doblez[y, x]:
                col = tono(c, -0.1)
            elif (x + 3 * (y // 3)) % 5 == 0 and y % 3 == 1:
                col = lunar                                  # lunares crema
            elif (x + y) % 9 == 0:
                col = claro
            else:
                col = c
            poner(y, x, col)
        if vista == 'espalda':                       # nudo y puntas atrás
            for dy, dx in ((18, 22), (18, 23), (18, 26), (18, 27), (19, 23), (19, 24), (19, 25), (19, 26),
                           (20, 24), (20, 25), (21, 23), (22, 23), (21, 26), (22, 27)):
                poner(dy + bob, dx, c if (dx + dy) % 3 else oscuro)
        elif vista == 'lado':
            izq = np.nonzero(cabeza[14])[0].min()
            for dy, dx in ((15, 0), (16, -1), (16, 0), (17, -1), (17, -2), (18, -2), (19, -2), (19, -3)):
                poner(dy + bob, izq + dx, c if dy % 2 else oscuro)
    if v.get('diadema'):
        c = hexrgb(v['diadema'])
        ys, xs = np.nonzero(cabeza)
        fila = 13 if vista != 'lado' else 12
        cols = np.nonzero(cabeza[fila])[0]
        for x in range(cols.min(), cols.max() + 1):
            poner(fila + bob, x, c)
            poner(fila + 1 + bob, x, tono(c, -0.2))
    if v.get('ligas') and v['pelo_estilo'] == 'trenzas':
        c = hexrgb(v['ligas'])
        ligas = {'frente': [(15, 38), (33, 38)], 'espalda': [(19, 38), (29, 38)], 'lado': [(16, 36)]}[vista]
        for x0, y in ligas:
            for dx in range(4):
                poner(y + bob, x0 + dx, c)
    if v.get('lentes') and vista in ('frente', 'lado'):
        armazon = (0x8a, 0x6a, 0x3a)                 # dorado viejo, lentes de abuela
        lentes = {'frente': [(21, 22, 26, 25), (28, 22, 33, 25)], 'lado': [(29, 22, 34, 25)]}[vista]
        for x0, y0, x1, y1 in lentes:
            for x in range(x0 + 1, x1):
                poner(y0 + bob, x, armazon)
                poner(y1 + bob, x, armazon)
            for y in range(y0 + 1, y1):
                poner(y + bob, x0, armazon)
                poner(y + bob, x1, armazon)
        if vista == 'frente':
            poner(23 + bob, 27, armazon)
        else:
            for x in range(24, 29):
                poner(23 + bob, x, armazon)


# ============================================================ ropa
def mascara_falda(vista, yr, X, y0, y1, vuelo):
    """Falda/bata/vestido desde la cintura (y0) hasta y1, abriéndose 1 px cada `vuelo` filas."""
    if vista == 'lado':
        izq = 18 - (yr - y0) // vuelo
        der = 29 + (yr - y0) // (vuelo + 2)
    else:
        izq = 17 - (yr - y0) // vuelo
        der = 33 + (yr - y0) // vuelo
    return (yr >= y0) & (yr <= y1) & (X >= izq) & (X <= der)


def dibujar_clienta(celdas, ref, cabeza, v, f, c):
    celda, mat, bob = celdas[f, c]
    vista = VISTA[f]
    out = np.zeros_like(celda)
    Y, X = np.mgrid[0:H, 0:W]
    YR = Y - bob
    piel = hexrgb(v['piel'])
    arriba = hexrgb(v['bata'] if v['prenda'] == 'bata' else v['blusa'])
    interior = hexrgb(v['interior'])
    piernas = hexrgb(v.get('pantalon') or v['piernas'])
    zapatos = v.get('pantuflas') or v.get('zapatos')
    cols = {'J': arriba, 'W': interior, 'P': piernas, 'S': piel, 'k': piel}
    for y in range(H):
        for x in range(W):
            m = mat[y, x]
            px = celda[y, x]
            yr = y - bob
            if m in ('C', 'h', '.'):
                continue
            if m == 'i' and not (OJOS[vista][0] <= x <= OJOS[vista][1]):
                # mechones del repartidor a los lados de la cara: aquí van recogidos, queda piel
                out[y, x] = (*tono(piel, -0.1, 0.04), 255)
                continue
            if m == 'Y':                                    # botones de la chaqueta: van del color de la prenda
                out[y, x] = (*mezcla_color(px[:3], ref['J'], arriba), 255)
                continue
            if m in cols:
                obj = cols[m]
                if m == 'W' and v['prenda'] == 'vestido' and yr > 34:
                    obj = arriba                             # el vestido es de una pieza; cuello crema arriba
                if m == 'k' and en_cara(vista, x, yr + 1):
                    out[y, x] = (*tono(piel, -0.1, 0.04), 255)
                    continue
                ref_m = ref['W'] if m == 'W' else ref[m]
                out[y, x] = (*mezcla_color(px[:3], ref_m, obj, 0.5 if m == 'k' else 0.9), 255)
            elif zapatos and m == 'O' and yr >= 69 and px[3] == 255:
                out[y, x] = (*mezcla_color(px[:3], (0, 0, 0.09), hexrgb(zapatos), 0.8), 255)   # zapatos de color
            else:
                out[y, x] = px

    def tela(mask, base, rayas=None, lunares=None, cuadros=None, orla=None):
        borde = mask & ~ndimage.binary_erosion(mask)
        base = hexrgb(base)
        for y, x in zip(*np.nonzero(mask)):
            yr = y - bob
            col = base
            if lunares and (x + 3 * (yr // 3)) % 5 == 0 and yr % 3 == 1:
                col = hexrgb(lunares)
            elif cuadros and (x % 4 == 0 or yr % 4 == 0):
                col = hexrgb(cuadros)
            elif (x + yr // 3) % 6 == 0:
                col = tono(base, -0.06)
            if orla and yr >= orla[0]:
                col = hexrgb(orla[1])
            if borde[y, x]:
                col = tono(base, -0.2)
            out[y, x] = (*col, 255)

    tapa_piernas = np.isin(mat, ['P', '.', 's']) | ((mat == 'O') & (YR >= 52))
    prenda = v['prenda']
    if prenda == 'vestido' or (prenda in ('delantal', 'diario') and v.get('falda')):
        fm = mascara_falda(vista, YR, X, 49, 60 if prenda != 'vestido' else 61, 5)
        tela(fm & tapa_piernas, v['falda'], lunares=v.get('lunares'))
    if prenda == 'bata':
        fm = mascara_falda(vista, YR, X, 49, 64, 6)
        tela(fm & tapa_piernas, v['bata'], cuadros=v.get('cuadros'))
        if v.get('cuadros'):                                # la parte de arriba de la bata también a cuadros
            arriba_m = (mat == 'J') & (YR < 49) & ((X % 4 == 0) | (YR % 4 == 0))
            for y, x in zip(*np.nonzero(arriba_m)):
                out[y, x] = (*mezcla_color(celda[y, x][:3], ref['J'], hexrgb(v['cuadros'])), 255)
        if vista == 'frente':                               # cruce de la bata: borde que baja en diagonal
            for yr in range(49, 64):
                x = 26 + (yr - 49) // 5
                if out[yr + bob, x, 3]:
                    out[yr + bob, x] = (*tono(hexrgb(v['bata']), -0.22), 255)
        cint = hexrgb(v['cinto'])                           # cinto a la cintura, con moño al frente o atrás
        for y, x in zip(*np.nonzero((YR >= 47) & (YR <= 48) & (out[..., 3] > 0) & (np.isin(mat, ['J', 'W'])))):
            out[y, x] = (*cint, 255)
        if vista in ('frente', 'espalda'):
            for dy, dx in ((49, 23), (50, 22), (49, 26), (50, 27), (51, 22), (51, 27)):
                out[dy + bob, dx] = (*cint, 255)
    if prenda == 'delantal':
        d = hexrgb(v['delantal'])
        if vista == 'frente':
            peto = (YR >= 35) & (YR <= 46) & (X >= 20) & (X <= 30)
            falda = (YR >= 47) & (YR <= 60) & (X >= 18) & (X <= 32)
            tir = (YR >= 31) & (YR <= 34) & ((X == 21) | (X == 29))
            dm = (peto | falda | tir) & ~np.isin(mat, ['S', 'k', '.']) | (falda & (out[..., 3] == 0) & (YR <= 60))
            borde = dm & ~ndimage.binary_erosion(dm)
            for y, x in zip(*np.nonzero(dm)):
                yr = y - bob
                col = tono(d, -0.22) if borde[y, x] else (tono(d, +0.08, -0.1) if x < 23 else d)
                if yr == 47 or (yr == 52 and 21 <= x <= 29 and (x % 2)):
                    col = tono(d, -0.15)                    # cintura y bolsillo
                if yr >= 59 and not borde[y, x]:
                    col = hexrgb(CREMA)                     # holán
                out[y, x] = (*col, 255)
        elif vista == 'espalda':                            # solo el moño atrás
            for dy, dx in ((47, 21), (47, 22), (47, 23), (47, 26), (47, 27), (47, 28), (48, 22), (48, 23),
                           (48, 24), (48, 25), (48, 26), (48, 27), (49, 23), (49, 26), (50, 22), (51, 22),
                           (50, 27), (51, 28)):
                out[dy + bob, dx] = (*(d if (dx + dy) % 3 else tono(d, -0.2)), 255)
        else:                                               # de lado: el delantal asoma al frente
            dm = (YR >= 35) & (YR <= 60) & (X >= 28) & (X <= 31) & ((out[..., 3] > 0) | (YR >= 49))
            for y, x in zip(*np.nonzero(dm)):
                out[y, x] = (*(tono(d, -0.2) if x == 31 else d), 255)
            for dy, dx in ((47, 18), (47, 19), (48, 17), (48, 18), (49, 17)):
                out[dy + bob, dx] = (*d, 255)

    # pelo (y la piel que destapa el peinado recogido), luego accesorios
    pm = np.roll(peinado(v['pelo_estilo'], vista, cabeza[vista]), bob, axis=0)
    for (y, x), col in textura_pelo(pm, 'largo', hexrgb(v['pelo'])).items():
        out[y, x] = (*col, 255)
    cab = np.roll(cabeza[vista], bob, axis=0)
    hueco = cab & ~pm & (YR >= 17) & (out[..., 3] == 0)
    for y, x in zip(*np.nonzero(hueco)):
        out[y, x] = (*(tono(piel, -0.04) if YR[y, x] == 17 else piel), 255)
    accesorios(out, v, vista, bob, cabeza[vista])
    return out


# ============================================================ flecha guía
def flecha():
    """Flecha de 32x32 apuntando a la derecha: cuerpo y punta dorados, contorno oscuro y brillo arriba."""
    m = np.zeros((32, 32), bool)
    m[13:19, 5:18] = True                                   # cuerpo (6 px de grueso)
    for x in range(16, 29):                                 # punta
        medio = (28 - x) * 10 / 12
        m[int(round(15.5 - medio)):int(round(15.5 + medio)) + 1, x] = True
    contorno = ndimage.binary_dilation(m, structure=np.ones((3, 3))) & ~m
    dor = hexrgb(DORADO)
    claro, oscuro, brillo = tono(dor, +0.0, -0.25), tono(dor, -0.18, +0.05), (255, 250, 228)
    out = np.zeros((32, 32, 4), np.uint8)
    out[contorno] = (*hexrgb(CONTORNO), 255)
    ys, xs = np.nonzero(m)
    for y, x in zip(ys, xs):
        col = dor
        abajo = not m[min(31, y + 1), x] or (x >= 16 and y >= 15.5 + (28 - x) * 10 / 12 - 1.5)
        if abajo or y >= 17 and x < 17:
            col = oscuro
        elif y <= 14 and x < 17 or (x >= 16 and y <= 15.5 - (28 - x) * 10 / 12 + 1.5):
            col = claro
        out[y, x] = (*col, 255)
    for x in range(7, 14):                                  # brillo
        out[14, x] = (*brillo, 255)
    out[11, 19] = out[12, 19] = out[12, 20] = (*brillo, 255)
    return out


# ============================================================ hojas y vista previa
def main():
    os.makedirs(DESTINO, exist_ok=True)
    celdas = leer_base()
    ref = referencias_material(celdas)
    cabeza = cabezas(celdas)
    hojas, previas = [], []
    for v in CLIENTAS:
        nombre = f"clienta_{v['n']}"
        imgs = {(f, c): dibujar_clienta(celdas, ref, cabeza, v, f, c) for f in range(FILAS) for c in range(COLS)}
        hoja = np.zeros((H * FILAS, W * COLS, 4), np.uint8)
        for (f, c), im in imgs.items():
            hoja[f * H:(f + 1) * H, c * W:(c + 1) * W] = im
        Image.fromarray(hoja).save(os.path.join(DESTINO, f'{nombre}.png'))
        hojas.append(dict(imagen=f'{nombre}.png', ancho=W * COLS, alto=H * FILAS,
                          sprites=frames_meta(nombre, range(FILAS)), personaje=nombre, variante=v['n'], nota=v['nota']))
        previas.append((nombre, imgs))
    meta = dict(formato='como repartidor_sheet.png: celdas de 56x80, 5 columnas x 3 filas; fila 0 frente, '
                        '1 espaldas, 2 lado (mira a la derecha); columna 0 quieta, 1-4 caminando',
                origen='arriba-izquierda (Unity mide desde abajo)', pivote=[0.5, 0.0], ppu=32,
                celda=dict(w=W, h=H), hojas=hojas)
    with open(os.path.join(DESTINO, 'clientas.json'), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=2, ensure_ascii=False)

    Image.fromarray(flecha()).save(RUTA_FLECHA)

    # vista previa: las 8 lado a lado; arriba quietas (frente, espaldas, lado) y abajo un paso de cada vista
    esc, sep = 3, 8
    ancho = len(previas) * (W * 3 + sep) + sep
    lienzo = Image.new('RGBA', (ancho, H * 2 + 16), (178, 196, 160, 255))
    d = ImageDraw.Draw(lienzo)
    x = sep
    for nombre, imgs in previas:
        for f in range(FILAS):
            lienzo.alpha_composite(Image.fromarray(imgs[f, 0]), (x + f * W, 0))
            lienzo.alpha_composite(Image.fromarray(imgs[f, 1]), (x + f * W, H))
        d.text((x + 4, H * 2 + 2), nombre, fill=(30, 30, 40, 255))
        x += W * 3 + sep
    fl = Image.fromarray(flecha())
    lienzo.alpha_composite(fl, (ancho - 40, H * 2 - 40))
    lienzo = lienzo.resize((lienzo.width * esc, lienzo.height * esc), Image.NEAREST)
    lienzo.save(os.path.join(DESTINO, 'clientas_vista_previa.png'))
    shutil.copyfile(os.path.join(DESTINO, 'clientas_vista_previa.png'), os.path.join(AQUI, 'clientas_vista_previa.png'))
    print('clientas:', [h['imagen'] for h in hojas], '| flecha:', RUTA_FLECHA)


if __name__ == '__main__':
    main()
