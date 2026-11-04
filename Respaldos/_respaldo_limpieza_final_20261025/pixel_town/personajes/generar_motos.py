"""Motos del repartidor: 4 modelos sacados de moto1.jpg (Descargas), con el repartidor montado.

Modelos: clasica_azul, deportiva_roja, chopper_verde y scooter_naranja. Cada uno tiene:
  - <nombre>.png: hoja de 216x240, celdas de 72x80; fila 0 frente, 1 espaldas, 2 lado (mira a la
    derecha); columna 0 parada y 1-2 rodando (las ruedas giran y el cuerpo rebota 1 px).
  - <nombre>_icono.png: 64x64, la moto de lado sin repartidor, para la tienda.
Las 4 miden lo mismo de lado: 64 px = 2 tiles de largo (sin contar la sombra). En la parte trasera
llevan la caja de pedidos rosa (#FF6FA5) con detalles dorados (#FFD166) y el emblema de tijeras de la
mochila. El repartidor va sin la mochila a pie (repartidor_sheet_sin_mochila.png).

El repartidor sale de repartidor_sheet.png (la vista de lado, sin el brazo que cuelga, sale de
generar_repartidor_sheet.py): mismo tamaño, gorra roja y chamarra morada. Lo que es nuevo (piernas
sentadas, brazos al manubrio y las motos) se pinta como las clientas: 3 tonos por superficie
(luz arriba/izquierda, base, sombra abajo/derecha) y contorno oscuro, con la sombra suave del
repartidor bajo las ruedas.

También escribe los .meta (Sprite Multiple, PPU 32, Filter Point, sin compresión, pivote
abajo-centro), motos.json y motos_vista_previa.png.

Con --chica (octubre 2026) hace las mismas 4 motos con la repartidora encima (generar_repartidora_sheet.py: su suéter y su cabello
sobre el mismo cuerpo; el cabello suelto al viento cuando rueda): <nombre>/<nombre>_chica.png con los cuadros
<nombre>_chica_<vista>_<n> y su .meta, y motos_chica_vista_previa.png aquí al lado. Los íconos de la tienda no llevan
jinete, así que son los mismos para los dos; las motos del repartidor no se tocan.

Con --lluvia (octubre 2026, solo Nivel3_Lluvia) el jinete lleva el atuendo de lluvia de generar_impermeable.py (correr
ese antes: lee repartidor_sheet_lluvia_sin_mochila.png): manga dorada, faldón del impermeable sobre el asiento y botas de
lluvia moradas; con --chica --lluvia, la repartidora con su falda asomando bajo el faldón y la piel entre la falda y
la bota. Escribe <nombre>_lluvia.png o
<nombre>_chica_lluvia.png (cuadros <nombre>_lluvia_<vista>_<n>, ...) con su .meta; no toca las otras hojas ni motos.json.

Uso: python generar_motos.py [--chica] [--lluvia]
"""
import colorsys
import json
import math
import os
import shutil
import sys
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.environ.get('MOTOS_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Motos')
CHICA = '--chica' in sys.argv       # la repartidora encima (el mismo cuerpo con su suéter y su cabello)
LLUVIA = '--lluvia' in sys.argv     # atuendo de lluvia (generar_impermeable.py): impermeable dorado y botas moradas
HOJA_REPARTIDOR = os.path.join(AQUI, 'repartidor_sheet_lluvia_sin_mochila.png' if LLUVIA    # en moto, sin la mochila
                               else 'repartidor_sheet_sin_mochila.png')
SUFIJO = ('_chica' if CHICA else '') + ('_lluvia' if LLUVIA else '')

W, H = 72, 80                       # celda
FILAS = ('frente', 'espalda', 'lado')
COLS = 3
SUELO = 74                          # última fila de las llantas
CX = W // 2                         # el pivote abajo-centro cae aquí
LARGO = 64                          # largo de todas las motos de lado
ICONO = 64

CONTORNO = '#2a1f40'
ROSA, DORADO = '#ff6fa5', '#ffd166'
SOMBRA_RGB = (0x1a, 0x14, 0x28)     # la misma sombra de los pies del repartidor


# ============================================================ color
def hexrgb(c):
    c = c.lstrip('#')
    return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))


def rampa(c):
    """(contorno, sombra, base, luz, brillo), igual que las clientas."""
    c = hexrgb(c)
    h, s, v = colorsys.rgb_to_hsv(*(x / 255 for x in c))

    def hsv2(dh, ds, dv):
        r, g, b = colorsys.hsv_to_rgb((h + dh) % 1, min(1, max(0, s + ds)), min(1, max(0, v + dv)))
        return int(round(r * 255)), int(round(g * 255)), int(round(b * 255))

    oscuro = hexrgb(CONTORNO)
    sombra = hsv2(+0.02, +0.06, -0.17)
    contorno = tuple(int(a * 0.35 + b * 0.65) for a, b in zip(hsv2(0, 0.1, -0.35), oscuro))
    return contorno, sombra, c, hsv2(-0.015, -0.08, +0.11), hsv2(-0.02, -0.2, +0.22)


def fija(con, som, base, luz, brillo=None):
    t = [hexrgb(x) for x in (con, som, base, luz)]
    return (*t, hexrgb(brillo) if brillo else t[3])


# los del repartidor (tomados de repartidor_sheet.png)
PANTALON = fija('#0d0220', '#2a243a', '#353046', '#544a63')
ZAPATO = fija('#0d0220', '#171413', '#2e2620', '#41201a')
CHAMARRA = fija('#18064e', '#653ab1', '#7e4fcb', '#a979d3')
FALDA = CALCETA = None
MUSLO, PIERNA = 7, 6     # grosor del muslo y de la pierna (de lado; de frente 1 menos)
if CHICA:   # la manga del brazo al manubrio con el suéter lavanda de la repartidora (generar_repartidora_sheet.py)
    CHAMARRA = fija('#2a1f40', '#9680cc', '#c9b7f0', '#e2d6fa')
    # y su falda plisada morada (2 tonos), las piernas al aire (su piel), calcetas crema y botitas oscuras, como en
    # generar_repartidora_sheet.py; las piernas son más delgadas que el pantalón
    FALDA = fija(CONTORNO, '#451f8c', '#7442d6', '#7442d6')
    PANTALON = fija(CONTORNO, '#f8c2a8', '#ffdcc0', '#ffdcc0')
    CALCETA = fija(CONTORNO, '#c9b7f0', '#fff1d0', '#fff1d0')
    ZAPATO = fija('#0d0220', '#3b2338', '#3b2338', '#5c3a55')
    MUSLO, PIERNA = 6, 5
IMPERMEABLE = BOTA = None
if LLUVIA:  # la manga dorada del impermeable, su faldón sobre el asiento y las botas de lluvia moradas
    CHAMARRA = IMPERMEABLE = fija(CONTORNO, '#d9962b', '#ffd166', '#ffd166')
    ZAPATO = BOTA = fija('#0d0220', '#5329ae', '#7a45e6', '#a97cff')
PIEL = fija('#5b352e', '#c57e5e', '#f8c2a8', '#ffdcc0')

# piezas de las motos
LLANTA = fija(CONTORNO, '#211c2b', '#36303f', '#575066')
RIN = rampa('#c9ccd6')
CROMO = fija(CONTORNO, '#7d8394', '#b9bfcc', '#e6e9f0', '#ffffff')
MOTOR = rampa('#6f6b7d')
ASIENTO = fija(CONTORNO, '#1f1a28', '#2f2a3a', '#48405a')
FARO = fija('#7a5a14', '#f0b93a', '#ffe27a', '#fff6c8', '#ffffff')
CALAVERA = fija('#5a0f1e', '#b8202f', '#ff3b3b', '#ff8a7a')
PLACA = rampa('#e9e4d6')
MICA = fija(CONTORNO, '#3a4e6e', '#557099', '#8fb3d9', '#d8ecff')
CAJA = rampa(ROSA)
TAPA = rampa('#ff8fbf')
ORO = rampa(DORADO)


# ============================================================ máscaras
def vacia():
    return np.zeros((H, W), bool)


def _dibujo():
    im = Image.new('L', (W, H), 0)
    return im, ImageDraw.Draw(im)


def poli(pts):
    im, d = _dibujo()
    d.polygon([tuple(p) for p in pts], fill=1, outline=1)
    return np.array(im, bool)


def linea(p0, p1, ancho):
    im, d = _dibujo()
    d.line([tuple(p0), tuple(p1)], fill=1, width=ancho)
    m = np.array(im, bool)
    if ancho >= 3:                                   # puntas redondas
        m |= disco(*p0, (ancho - 1) / 2) | disco(*p1, (ancho - 1) / 2)
    return m


def disco(cx, cy, r):
    Y, X = np.mgrid[0:H, 0:W]
    return (X - cx) ** 2 + (Y - cy) ** 2 <= (r + 0.45) ** 2


def elipse(cx, cy, rx, ry):
    Y, X = np.mgrid[0:H, 0:W]
    return ((X - cx) / (rx + 0.45)) ** 2 + ((Y - cy) / (ry + 0.45)) ** 2 <= 1


def rect(x0, y0, x1, y1):
    m = vacia()
    m[max(0, y0):y1 + 1, max(0, x0):x1 + 1] = True
    return m


def arco(cx, cy, r0, r1, a0, a1):
    """Anillo entre r0 y r1, de a0 a a1 grados (0 = derecha, 90 = arriba)."""
    Y, X = np.mgrid[0:H, 0:W]
    dx, dy = X - cx, cy - Y
    d = np.hypot(dx, dy)
    a = np.degrees(np.arctan2(dy, dx)) % 360
    return (d >= r0) & (d <= r1 + 0.45) & (((a - a0) % 360) <= (a1 - a0) % 360)


def desplazar(m, dy, dx):
    out = np.zeros_like(m)
    h, w = m.shape
    out[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)] = m[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)]
    return out


def borde(m):
    return m & ~ndimage.binary_erosion(m)


# ============================================================ pintar
def pintar(out, m, r, contorno=True):
    """3 tonos y contorno oscuro, como sombrear() de las clientas: luz en la orilla de arriba/izquierda,
    sombra en la de abajo/derecha (2 px a la derecha)."""
    if not m.any():
        return
    con, som, base, luz, _ = r
    b = borde(m) if contorno else np.zeros_like(m)
    ad = m & ~b
    luz_m = ad & (~desplazar(ad, 1, 0) | ~desplazar(ad, 0, 1))
    som_m = ad & (~desplazar(ad, -1, 0) | ~desplazar(ad, 0, -1) | ~desplazar(ad, 0, -2))
    out[m] = (*base, 255)
    out[ad & luz_m] = (*luz, 255)
    out[ad & som_m & ~luz_m] = (*som, 255)
    out[ad & som_m & luz_m & ~desplazar(ad, 1, 0)] = (*luz, 255)     # el canto de arriba siempre con luz
    out[b] = (*con, 255)


def punto(out, x, y, col):
    if 0 <= y < H and 0 <= x < W:
        out[y, x] = (*col, 255)


def sombra(out, cx, rx, nucleos=()):
    """Sombra suave en el piso: una elipse clara y otra más oscura bajo cada rueda."""
    capa = np.zeros((H, W), np.uint8)
    capa[elipse(cx, SUELO + 1, rx, 2)] = 60
    for nx, nrx in nucleos:
        capa[elipse(nx, SUELO + 1, nrx, 1.4)] = 110
    for y, x in zip(*np.nonzero(capa)):
        out[y, x] = (*SOMBRA_RGB, capa[y, x])


# ============================================================ piezas comunes
def rueda(out, cx, r, ang, rin_r=None, rayos=True):
    cy = SUELO - r
    pintar(out, disco(cx, cy, r), LLANTA)
    rin_r = r - 3 if rin_r is None else rin_r
    pintar(out, disco(cx, cy, rin_r), RIN, contorno=False)
    con, som, base, luz, _ = RIN
    if rayos:
        for k in range(3):
            a = math.radians(ang + 60 * k)
            for t in np.linspace(-rin_r, rin_r, 4 * rin_r + 1):
                punto(out, int(round(cx + t * math.cos(a))), int(round(cy - t * math.sin(a))), som)
    pintar(out, disco(cx, cy, 1), MOTOR)
    punto(out, cx, cy, CROMO[3])
    # una marca en la llanta para que se note el giro
    a = math.radians(ang * 2 + 135)
    punto(out, int(round(cx + (r - 1) * math.cos(a))), int(round(cy - (r - 1) * math.sin(a))), LLANTA[3])


def llanta_frente(out, cx, r, ancho, fase, bob=0):
    """Rueda vista de frente o de espaldas: una banda vertical redondeada con dibujo que avanza."""
    cy = SUELO - r
    m = elipse(cx - 0.5, cy, ancho / 2 - 0.5, r)
    pintar(out, m, LLANTA)
    for y in range(cy - r + 2, SUELO - 1):
        if (y + fase) % 3 == 0:
            for x in range(cx - ancho // 2 + 1, cx + (ancho + 1) // 2 - 1):
                if m[y, x] and not borde(m)[y, x]:
                    punto(out, x, y, LLANTA[1])


# el emblema de tijeras de la mochila del repartidor (5x5, dorado)
EMBLEMA = ["G...G",
           ".G.G.",
           "..G..",
           ".G.G.",
           "GG.GG"]


def caja_pedidos(out, x0, y0, w, h):
    """Caja térmica rosa con tapa más clara, franja dorada al medio, el broche dorado y el emblema de
    tijeras al centro (la franja se abre alrededor)."""
    pintar(out, rect(x0, y0 + 3, x0 + w - 1, y0 + h - 1), CAJA)
    pintar(out, rect(x0 - 1, y0, x0 + w, y0 + 3), TAPA)
    yb = y0 + 3 + (h - 3) // 2
    for x in range(x0 + 1, x0 + w - 1):
        punto(out, x, yb, ORO[3] if x < x0 + 3 else ORO[2])
        punto(out, x, yb + 1, ORO[1])
    xm = x0 + w // 2
    for dx in (-1, 0):
        punto(out, xm + dx, y0 + 3, ORO[2])
        punto(out, xm + dx, y0 + 4, ORO[1])
    punto(out, xm - 1, y0 + 3, ORO[4])
    xe, ye = x0 + (w - 5) // 2, yb - 2
    for x in range(xe - 1, xe + 6):
        for y in (yb, yb + 1):
            punto(out, x, y, CAJA[2])
    for j, fila in enumerate(EMBLEMA):
        for i, ch in enumerate(fila):
            if ch == 'G':
                punto(out, xe + i, ye + j, ORO[2])


def faro(out, cx, cy, r):
    pintar(out, disco(cx, cy, r + 1), CROMO)
    pintar(out, disco(cx, cy, r), FARO)
    punto(out, cx - 1, cy - 1, FARO[4])


# ============================================================ repartidor
def leer_repartidor(viento=None):
    """viento: None el repartidor; 0, 1 o 2 la repartidora (suéter y cabello suelto) como en esa columna (0 parada, 1-2 rodando)."""
    hoja = np.array(Image.open(HOJA_REPARTIDOR).convert('RGBA'))
    frente = hoja[0:80, 0:56].copy()
    espalda = hoja[80:160, 0:56].copy()
    # de lado sin el brazo que cuelga: el torso tal cual lo arma el generador del repartidor
    import generar_repartidor_sheet as g
    lienzo = g.blank()
    g.stamp(lienzo, g.SIDE_UPPER, 8)
    lado = np.array(g.to_image(lienzo))
    if LLUVIA:   # el torso de lado no sale de la hoja: se le pone aquí el impermeable
        import generar_impermeable as imp
        lado = np.array(imp.impermeable(Image.fromarray(lado).copy(), 2, 0))
    if viento is not None:
        import generar_repartidora_sheet as ch
        frente, espalda, lado = (np.array(ch.vestir(Image.fromarray(im).copy(), vista, 0, viento, sueter=not LLUVIA))
                                 for vista, im in ((0, frente), (1, espalda), (2, lado)))
    for im in (frente, espalda, lado):
        im[im[:, :, 3] < 255] = 0              # fuera la sombra del piso
        im[54:] = 0                           # solo de la gorra a la cadera
    return dict(frente=frente, espalda=espalda, lado=lado)


def pegar(out, src, dx, dy, inclinar=0, filas=range(8, 54), columnas=range(56)):
    """Pega el repartidor (filas 8-53 de su celda) movido dx, dy; inclinar > 0 lo echa hacia adelante."""
    for y in filas:
        off = int(round(inclinar * (53 - y) / 45))
        for x in columnas:
            if src[y, x, 3]:
                yy, xx = y + dy, x + dx + off
                if 0 <= yy < H and 0 <= xx < W:
                    out[yy, xx] = src[y, x]


def _hacia(p0, p1, t):
    return (round(p0[0] + (p1[0] - p0[0]) * t), round(p0[1] + (p1[1] - p0[1]) * t))


def pierna_lado(out, cadera, rodilla, pie):
    pintar(out, linea(cadera, rodilla, MUSLO) | linea(rodilla, pie, PIERNA), PANTALON)
    if CALCETA and not BOTA:                                    # la calceta corta hasta el tobillo
        pintar(out, linea(_hacia(rodilla, pie, 0.7), pie, PIERNA), CALCETA)
    if BOTA:                                                    # la caña de la bota de lluvia
        pintar(out, linea(_hacia(rodilla, pie, 0.4), pie, 6), BOTA)
    x, y = pie
    pintar(out, poli([(x - 3, y - 1), (x + 3, y - 1), (x + 6, y + 2), (x + 6, y + 3), (x - 3, y + 3)]), ZAPATO)
    if FALDA:
        falda_lado(out, cadera, rodilla)
    if IMPERMEABLE:
        faldon_lado(out, cadera, rodilla)


def pliegues(out, m, cada):
    """Líneas de pliegue (el tono oscuro de la falda) cada 'cada' columnas dentro de la máscara, sin el contorno."""
    dentro = m & ~borde(m)
    for y, x in zip(*np.nonzero(dentro)):
        if x % cada == 0:
            out[y, x] = (*FALDA[1], 255)


def falda_lado(out, cadera, rodilla):
    """De lado, sentada: la falda cae sobre el muslo hasta la mitad y asoma un poco hacia atrás sobre el asiento."""
    (hx, hy), (kx, ky) = cadera, rodilla
    mx, my = hx + (kx - hx) * 0.55, hy + (ky - hy) * 0.55
    m = poli([(hx - 5, hy - 3), (hx + 2, hy - 4), (mx + 1, my - 4), (mx + 2, my + 3), (hx - 1, hy + 4), (hx - 6, hy + 2)])
    pintar(out, m, FALDA)
    pliegues(out, m, 3)


def faldon_lado(out, cadera, rodilla):
    """De lado, sentado: el faldón del impermeable sobre el asiento y el principio del muslo (más corto que la falda)."""
    (hx, hy), (kx, ky) = cadera, rodilla
    t = 0.1 if FALDA else 0.3                                  # con falda, el faldón apenas pasa la cadera
    mx, my = hx + (kx - hx) * t, hy + (ky - hy) * t
    pintar(out, poli([(hx - 6, hy - 4), (hx + 1, hy - 5), (mx + 1, my - 4), (mx + 1, my + 2), (hx - 2, hy + 3),
                      (hx - 7, hy + 2)]), IMPERMEABLE)


def falda_frente(out, cx, cadera_y):
    """De frente o de espaldas, sentada: asoma a los dos lados de la cadera, sobre el inicio de los muslos."""
    m = poli([(cx - 9, cadera_y - 2), (cx + 8, cadera_y - 2), (cx + 13, cadera_y + 4), (cx - 14, cadera_y + 4)])
    pintar(out, m, FALDA)
    pliegues(out, m, 3)


def brazo_lado(out, hombro, codo, mano):
    pintar(out, linea(hombro, codo, 6) | linea(codo, mano, 5), CHAMARRA)
    x, y = mano
    pintar(out, disco(x, y, 1.6), PIEL)


def piernas_frente(out, cx, cadera_y, pie_y, abre=10, de_espaldas=False):
    for s in (-1, 1):
        rod = (cx + s * (abre + 1), cadera_y + 3)
        pie = (cx + s * (abre - 1), pie_y)
        pintar(out, linea((cx + s * 5, cadera_y - 1), rod, MUSLO - 1) | linea(rod, pie, PIERNA - 1), PANTALON)
        if CALCETA and not BOTA:
            pintar(out, linea(_hacia(rod, pie, 0.6), pie, PIERNA - 1), CALCETA)
        if BOTA:
            pintar(out, linea(_hacia(rod, pie, 0.35), pie, 5), BOTA)
        pintar(out, elipse(pie[0], pie[1] + 1, 2.6, 1.8 if de_espaldas else 2.2), ZAPATO)
    if FALDA:
        falda_frente(out, cx, cadera_y)
    if IMPERMEABLE:   # el faldón del impermeable: a los lados de la cadera, más corto (la falda asoma debajo)
        pintar(out, poli([(cx - 9, cadera_y - 2), (cx + 8, cadera_y - 2), (cx + 11, cadera_y + 1), (cx - 12, cadera_y + 1)]),
               IMPERMEABLE)


# ============================================================ modelos
# Cada modelo: color, ruedas y dónde quedan el asiento, las manos y los pies; las funciones de abajo
# dibujan sus piezas de lado, de frente y de espaldas.
MODELOS = {
    'clasica_azul': dict(ruedas=(15, 56), color='#2f6fdb', r=9, asiento=51, cadera_x=24, inclinar=2,
                         rodilla=(36, 55), pie=(34, 66), hombro=(25, 31), codo=(35, 39), mano=(45, 43),
                         nota='clásica estándar azul: tanque redondo, motor cromado, faro redondo'),
    'deportiva_roja': dict(ruedas=(14, 57), color='#e0282e', r=9, asiento=50, cadera_x=24, inclinar=5,
                           rodilla=(37, 52), pie=(31, 61), hombro=(28, 30), codo=(37, 38), mano=(46, 45),
                           nota='deportiva roja: carenado completo, cola levantada, piloto inclinado'),
    'chopper_verde': dict(ruedas=(15, 59), color='#43a854', r=9, rf=8, asiento=56, cadera_x=22, inclinar=-1,
                          rodilla=(35, 57), pie=(45, 64), hombro=(22, 36), codo=(31, 33), mano=(42, 33),
                          nota='chopper verde: horquilla larga, manubrio alto, asiento bajo, doble escape'),
    'scooter_naranja': dict(ruedas=(15, 60), color='#f59a23', r=6, asiento=51, cadera_x=21, inclinar=0,
                            rodilla=(32, 54), pie=(38, 64), hombro=(21, 30), codo=(30, 38), mano=(45, 38),
                            nota='scooter naranja: carrocería redonda, escudo frontal, ruedas chicas'),
}


def lado_clasica(out, p, bob, ang, jinete, rep):
    c = rampa(p['color'])
    r = p['r']
    xr, xf = p['ruedas']
    rueda(out, xr, r, ang)
    rueda(out, xf, r, ang)
    b = bob
    pintar(out, linea((31, 67 + b), (8, 63 + b), 3), CROMO)                         # escape
    pintar(out, linea((15, 65), (28, 63 + b), 3), MOTOR)                            # basculante
    pintar(out, poli([(24, 58 + b), (40, 57 + b), (43, 64 + b), (38, 70 + b), (27, 70 + b), (22, 64 + b)]), CROMO)
    for y in (61, 64, 67):                                                          # aletas del motor
        pintar(out, rect(27, y + b, 37, y + b), MOTOR, contorno=False)
    pintar(out, arco(xr, SUELO - r, r + 1, r + 2, 75, 190), c)                       # salpicadera trasera
    pintar(out, poli([(17, 53 + b), (30, 53 + b), (31, 61 + b), (21, 62 + b)]), c)   # tapa lateral
    pintar(out, rect(4, 53 + b, 6, 55 + b), CALAVERA)
    caja_pedidos(out, 5, 39 + b, 13, 13)
    pintar(out, poli([(12, 52 + b), (31, 51 + b), (32, 54 + b), (13, 56 + b)]), ASIENTO)
    if jinete:
        pegar(out, rep['lado'], p['cadera_x'] - 24, p['asiento'] - 54 + b, p['inclinar'])
    pintar(out, poli([(30, 51 + b), (36, 47 + b), (44, 46 + b), (48, 49 + b), (47, 55 + b), (31, 57 + b)]), c)  # tanque
    punto(out, 37, 49 + b, c[4]); punto(out, 38, 49 + b, c[4])
    if jinete:
        pierna_lado(out, (p['cadera_x'] + 2, p['asiento'] - 3 + b), (p['rodilla'][0], p['rodilla'][1] + b), (p['pie'][0], p['pie'][1] + b))
    pintar(out, arco(xf, SUELO - r, r + 1, r + 2, 15, 140), c)                       # salpicadera delantera
    pintar(out, linea((49, 46 + b), (xf, SUELO - r), 3), CROMO)                     # horquilla
    pintar(out, linea((44, 42 + b), (49, 45 + b), 2), ASIENTO)                       # manubrio
    faro(out, 52, 48 + b, 2)
    if jinete:
        brazo_lado(out, (p['hombro'][0], p['hombro'][1] + b), (p['codo'][0], p['codo'][1] + b), (p['mano'][0], p['mano'][1] + b))


def lado_deportiva(out, p, bob, ang, jinete, rep):
    c = rampa(p['color'])
    r = p['r']
    xr, xf = p['ruedas']
    rueda(out, xr, r, ang)
    rueda(out, xf, r, ang)
    b = bob
    pintar(out, linea((14, 65), (30, 62 + b), 4), MOTOR)                            # basculante
    pintar(out, poli([(26, 55 + b), (42, 55 + b), (44, 66 + b), (30, 68 + b), (25, 62 + b)]), MOTOR)
    pintar(out, linea((26, 60 + b), (12, 55 + b), 3), CROMO)                        # escape bajo la cola
    pintar(out, poli([(4, 45 + b), (14, 46 + b), (26, 50 + b), (27, 55 + b), (14, 55 + b), (5, 49 + b)]), c)  # cola
    pintar(out, rect(3, 46 + b, 5, 48 + b), CALAVERA)
    caja_pedidos(out, 5, 33 + b, 13, 13)
    pintar(out, poli([(16, 49 + b), (31, 50 + b), (31, 53 + b), (16, 52 + b)]), ASIENTO)
    if jinete:
        pegar(out, rep['lado'], p['cadera_x'] - 24, p['asiento'] - 54 + b, p['inclinar'])
    pintar(out, poli([(29, 48 + b), (37, 45 + b), (44, 46 + b), (43, 52 + b), (30, 54 + b)]), c)  # tanque
    if jinete:
        pierna_lado(out, (p['cadera_x'] + 2, p['asiento'] - 3 + b), (p['rodilla'][0], p['rodilla'][1] + b), (p['pie'][0], p['pie'][1] + b))
    pintar(out, linea((52, 50 + b), (xf, SUELO - r), 3), CROMO)                     # horquilla
    pintar(out, arco(xf, SUELO - r, r + 1, r + 2, 30, 135), c)
    carenado = poli([(38, 46 + b), (49, 41 + b), (58, 44 + b), (63, 50 + b), (58, 56 + b), (47, 61 + b), (38, 61 + b), (35, 54 + b)])
    pintar(out, carenado, c)
    pintar(out, poli([(36, 59 + b), (46, 61 + b), (44, 66 + b), (35, 64 + b)]), rampa('#f2efe8'))   # quilla blanca
    pintar(out, poli([(47, 40 + b), (53, 37 + b), (58, 43 + b), (51, 44 + b)]), MICA)               # parabrisas
    for x, y in ((50, 50), (51, 50), (52, 51), (53, 51)):                                            # franja blanca
        punto(out, x, y + b, (242, 239, 232))
    pintar(out, poli([(58, 47 + b), (62, 49 + b), (61, 51 + b), (57, 49 + b)]), FARO)
    if jinete:
        brazo_lado(out, (p['hombro'][0], p['hombro'][1] + b), (p['codo'][0], p['codo'][1] + b), (p['mano'][0], p['mano'][1] + b))


def lado_chopper(out, p, bob, ang, jinete, rep):
    c = rampa(p['color'])
    r, rf = p['r'], p['rf']
    xr, xf = p['ruedas']
    rueda(out, xr, r, ang)
    rueda(out, xf, rf, ang)
    b = bob
    pintar(out, linea((15, 65), (28, 64 + b), 3), MOTOR)
    pintar(out, linea((35, 63 + b), (7, 61 + b), 3), CROMO)                         # escape de arriba
    pintar(out, linea((35, 67 + b), (8, 66 + b), 3), CROMO)                         # escape de abajo
    pintar(out, poli([(27, 58 + b), (39, 57 + b), (41, 68 + b), (29, 69 + b), (26, 64 + b)]), CROMO)  # motor V
    pintar(out, linea((29, 66 + b), (31, 57 + b), 2) | linea((38, 66 + b), (36, 57 + b), 2), MOTOR, contorno=False)
    pintar(out, linea((24, 56 + b), (46, 47 + b), 3) | linea((46, 47 + b), (41, 68 + b), 3), MOTOR)   # cuadro
    pintar(out, arco(xr, SUELO - r, r + 1, r + 2, 60, 190), c)
    pintar(out, rect(4, 56 + b, 6, 58 + b), CALAVERA)
    caja_pedidos(out, 5, 42 + b, 13, 13)
    pintar(out, poli([(14, 54 + b), (24, 56 + b), (31, 56 + b), (31, 59 + b), (14, 58 + b)]), ASIENTO)
    if jinete:
        pegar(out, rep['lado'], p['cadera_x'] - 24, p['asiento'] - 54 + b, p['inclinar'])
    pintar(out, elipse(37, 52 + b, 7, 3.5), c)                                       # tanque gota
    punto(out, 34, 50 + b, c[4]); punto(out, 35, 50 + b, c[4])
    if jinete:
        pierna_lado(out, (p['cadera_x'] + 2, p['asiento'] - 3 + b), (p['rodilla'][0], p['rodilla'][1] + b), (p['pie'][0], p['pie'][1] + b))
    pintar(out, arco(xf, SUELO - rf, rf + 1, rf + 2, 40, 130), c)
    pintar(out, linea((47, 41 + b), (xf, SUELO - rf), 3), CROMO)                    # horquilla larga
    pintar(out, linea((47, 42 + b), (45, 33 + b), 2) | linea((45, 33 + b), (41, 33 + b), 2), CROMO)  # manubrio alto
    faro(out, 51, 45 + b, 2)
    if jinete:
        brazo_lado(out, (p['hombro'][0], p['hombro'][1] + b), (p['codo'][0], p['codo'][1] + b), (p['mano'][0], p['mano'][1] + b))


def lado_scooter(out, p, bob, ang, jinete, rep):
    c = rampa(p['color'])
    r = p['r']
    xr, xf = p['ruedas']
    rueda(out, xr, r, ang, rin_r=r - 2)
    rueda(out, xf, r, ang, rin_r=r - 2)
    b = bob
    pintar(out, linea((20, 69), (8, 68), 3), CROMO)                                 # escape corto
    cuerpo = poli([(7, 52 + b), (28, 52 + b), (33, 57 + b), (33, 65 + b), (26, 67 + b), (22, 64 + b),
                   (12, 63 + b), (8, 66 + b), (4, 62 + b), (4, 56 + b)])
    pintar(out, cuerpo, c)
    pintar(out, rect(3, 55 + b, 5, 57 + b), CALAVERA)
    caja_pedidos(out, 5, 37 + b, 12, 13)
    pintar(out, poli([(8, 50 + b), (29, 50 + b), (30, 53 + b), (8, 53 + b)]), ASIENTO)
    pintar(out, rect(31, 64 + b, 47, 67 + b), MOTOR)                                  # piso
    if jinete:
        pegar(out, rep['lado'], p['cadera_x'] - 24, p['asiento'] - 54 + b, p['inclinar'])
        pierna_lado(out, (p['cadera_x'] + 2, p['asiento'] - 3 + b), (p['rodilla'][0], p['rodilla'][1] + b), (p['pie'][0], p['pie'][1] + b))
    pintar(out, linea((50, 40 + b), (xf, SUELO - r), 3), CROMO)                     # dirección
    escudo = poli([(45, 43 + b), (50, 40 + b), (54, 46 + b), (52, 58 + b), (49, 66 + b), (44, 67 + b), (46, 58 + b)])
    pintar(out, escudo, c)
    pintar(out, poli([(53, 61 + b), (62, 60 + b), (66, 63 + b), (66, 65 + b), (53, 64 + b)]), c)     # salpicadera
    pintar(out, poli([(43, 37 + b), (51, 35 + b), (55, 38 + b), (47, 40 + b)]), c)    # cubierta del manubrio
    faro(out, 54, 38 + b, 1)
    if jinete:
        brazo_lado(out, (p['hombro'][0], p['hombro'][1] + b), (p['codo'][0], p['codo'][1] + b), (p['mano'][0], p['mano'][1] + b))


LADO = dict(clasica_azul=lado_clasica, deportiva_roja=lado_deportiva, chopper_verde=lado_chopper,
            scooter_naranja=lado_scooter)


# ------------------------------------------------------------ frente y espaldas
# El repartidor de frente/espaldas tiene las manos en x 15-16 / 34-35 y y 50-52 de su celda; aquí va
# corrido +10 en x (su centro cae en CX) y su cadera en la fila del asiento.
DX_FRENTE = CX - 26


def frente(out, nombre, p, bob, fase, rep):
    c = rampa(p['color'])
    r = p.get('rf', p['r'])
    dy = p['asiento'] - 54 + bob
    mano_y = 51 + dy
    sombra(out, CX, 11, ((CX, 5),))
    # la caja va detrás del repartidor: de frente asoma por encima de los hombros, a los lados del cuello
    caja_pedidos(out, CX - 12, p['asiento'] - 24 + bob, 24, 13)
    pintar(out, elipse(CX - 0.5, p['asiento'] + 2 + bob, 8, 4), c)                  # cuerpo bajo el asiento
    if nombre == 'scooter_naranja':
        piernas_frente(out, CX, p['asiento'] + bob, 63 + bob, abre=8)
    else:
        piernas_frente(out, CX, p['asiento'] + bob, (p['pie'][1] + 1) + bob, abre=11)
    pegar(out, rep['frente'], DX_FRENTE, dy)
    if nombre != 'scooter_naranja':                                                  # motor entre las piernas
        pintar(out, rect(CX - 6, p['asiento'] + 4 + bob, CX + 5, p['asiento'] + 11 + bob),
               MOTOR if nombre == 'deportiva_roja' else CROMO)
    llanta_frente(out, CX, r, 5 if nombre != 'scooter_naranja' else 4, fase)
    cy = SUELO - r
    if nombre == 'clasica_azul':
        pintar(out, elipse(CX - 0.5, p['asiento'] + 1 + bob, 7, 4), c)               # tanque
        pintar(out, rect(CX - 4, cy - r - 2, CX + 3, cy - r + 2), c)                 # salpicadera
        pintar(out, rect(CX - 5, mano_y + 3, CX - 3, cy) | rect(CX + 2, mano_y + 3, CX + 4, cy), CROMO)
        faro(out, CX, mano_y + 5, 3)
    elif nombre == 'deportiva_roja':
        pintar(out, rect(CX - 4, cy - r - 2, CX + 3, cy - r + 2), c)
        pintar(out, rect(CX - 5, cy - r - 1, CX - 3, cy) | rect(CX + 2, cy - r - 1, CX + 4, cy), CROMO)
        pintar(out, poli([(CX - 10, mano_y + 1), (CX - 6, mano_y - 5), (CX + 5, mano_y - 5), (CX + 9, mano_y + 1),
                          (CX + 7, mano_y + 10), (CX + 2, mano_y + 13), (CX - 3, mano_y + 13), (CX - 8, mano_y + 10)]), c)
        pintar(out, poli([(CX - 5, mano_y - 4), (CX + 4, mano_y - 4), (CX + 3, mano_y - 9), (CX - 4, mano_y - 9)]), MICA)
        for s in (-1, 1):
            pintar(out, rect(CX + (s * 5 if s > 0 else -7), mano_y + 3, CX + (s * 6 if s > 0 else -6) + (1 if s > 0 else 0), mano_y + 4), FARO)
        punto(out, CX - 1, mano_y + 8, (242, 239, 232)); punto(out, CX, mano_y + 8, (242, 239, 232))
    elif nombre == 'chopper_verde':
        pintar(out, elipse(CX - 0.5, p['asiento'] - 1 + bob, 6, 3), c)
        pintar(out, rect(CX - 4, cy - r - 2, CX + 3, cy - r + 1), c)
        pintar(out, rect(CX - 6, mano_y + 2, CX - 4, cy) | rect(CX + 3, mano_y + 2, CX + 5, cy), CROMO)
        faro(out, CX, mano_y + 2, 3)
    else:
        pintar(out, rect(CX - 4, cy - r - 2, CX + 3, cy - r + 2), c)
        escudo = poli([(CX - 10, mano_y + 2), (CX + 9, mano_y + 2), (CX + 8, 64 + bob), (CX + 4, 67 + bob),
                       (CX - 5, 67 + bob), (CX - 9, 64 + bob)])
        pintar(out, escudo, c)
        pintar(out, rect(CX - 1, mano_y + 6, CX, 64 + bob), rampa('#f2efe8'), contorno=False)   # franja del escudo
    # manubrio de mano a mano, puños negros y espejos; luego las manos encima
    pintar(out, rect(CX - 11, mano_y, CX + 10, mano_y + 1), CROMO if nombre != 'scooter_naranja' else c)
    if nombre == 'scooter_naranja':
        pintar(out, rect(CX - 6, mano_y - 2, CX + 5, mano_y + 2), c)
        faro(out, CX, mano_y, 2)
    for s in (-1, 1):
        x = CX + (s * 13 if s > 0 else -14)
        pintar(out, linea((x, mano_y), (x + s, mano_y - 6), 1), CROMO, contorno=False)
        pintar(out, rect(x + s - 1, mano_y - 9, x + s + 1, mano_y - 6), CROMO)
    pegar(out, rep['frente'], DX_FRENTE, dy, filas=range(48, 54), columnas=list(range(13, 19)) + list(range(32, 39)))


def espalda(out, nombre, p, bob, fase, rep):
    c = rampa(p['color'])
    r = p['r']
    dy = p['asiento'] - 54 + bob
    mano_y = 51 + dy
    sombra(out, CX, 11, ((CX, 5),))
    # puños y espejos asoman por los lados (están detrás del repartidor)
    for s in (-1, 1):
        x = CX + (s * 13 if s > 0 else -14)
        pintar(out, rect(min(x, x + s * 2), mano_y, max(x, x + s * 2), mano_y + 1), ASIENTO)
        pintar(out, linea((x, mano_y), (x + s, mano_y - 6), 1), CROMO, contorno=False)
        pintar(out, rect(x + s - 1, mano_y - 9, x + s + 1, mano_y - 6), CROMO)
    pintar(out, elipse(CX - 0.5, p['asiento'] + 2 + bob, 8, 4), c)
    piernas_frente(out, CX, p['asiento'] + bob, (p['pie'][1] + 1 if nombre != 'scooter_naranja' else 63) + bob,
                   abre=11 if nombre != 'scooter_naranja' else 8, de_espaldas=True)
    pegar(out, rep['espalda'], DX_FRENTE, dy)
    ancho = dict(clasica_azul=7, deportiva_roja=7, chopper_verde=9, scooter_naranja=5)[nombre]
    llanta_frente(out, CX, r, ancho, fase)
    cy = SUELO - r
    if nombre == 'scooter_naranja':
        pintar(out, poli([(CX - 12, p['asiento'] + 2 + bob), (CX + 11, p['asiento'] + 2 + bob), (CX + 11, 62 + bob),
                          (CX + 6, 66 + bob), (CX - 7, 66 + bob), (CX - 12, 62 + bob)]), c)
        tope = p['asiento'] + 3 + bob
    else:
        pintar(out, rect(CX - 5, cy - r - 3, CX + 4, cy - r + 3), c)                  # salpicadera
        tope = cy - r - 4
        if nombre == 'deportiva_roja':
            pintar(out, poli([(CX - 9, p['asiento'] + bob), (CX + 8, p['asiento'] + bob), (CX + 4, cy - r),
                              (CX - 5, cy - r)]), c)
            tope = p['asiento'] + 1 + bob
    # escapes cromados
    if nombre == 'chopper_verde':
        for s in (-1, 1):
            pintar(out, elipse(CX + s * 9 - 0.5, cy + 1, 2, 2), CROMO)
    elif nombre != 'scooter_naranja':
        pintar(out, elipse(CX + 8, cy + (2 if nombre == 'clasica_azul' else -6), 2, 2), CROMO)
    else:
        pintar(out, elipse(CX + 8, 68, 2, 1.5), CROMO)
    ycaja = dict(clasica_azul=39, deportiva_roja=33, chopper_verde=42, scooter_naranja=37)[nombre] + bob
    caja_pedidos(out, CX - 11, ycaja, 22, 13)
    pintar(out, rect(CX - 3, ycaja + 13, CX + 2, ycaja + 14), CALAVERA)             # calavera bajo la caja
    pintar(out, rect(CX - 3, max(tope, ycaja + 16), CX + 2, max(tope, ycaja + 16) + 2), PLACA)


# ============================================================ celdas y hojas
def celda(nombre, fila, col, rep, jinete=True):
    p = MODELOS[nombre]
    out = np.zeros((H, W, 4), np.uint8)
    bob = 1 if col == 2 else 0
    if fila == 'lado':
        ang = (0, 0, 30)[col]
        if jinete:
            xr, xf = p['ruedas']
            sombra(out, (xr + xf) / 2, 31, ((xr, p['r']), (xf, p.get('rf', p['r']))))
        LADO[nombre](out, p, bob, ang, jinete, rep)
    elif fila == 'frente':
        frente(out, nombre, p, bob, col, rep)
    else:
        espalda(out, nombre, p, bob, col, rep)
    return out


def recorrer(im, dx):
    """Corre la imagen dx columnas sin que nada dé la vuelta."""
    if dx == 0:
        return im
    out = np.zeros_like(im)
    if dx > 0:
        assert not im[:, W - dx:, 3].any()
        out[:, dx:] = im[:, :W - dx]
    else:
        assert not im[:, :-dx, 3].any()
        out[:, :W + dx] = im[:, -dx:]
    return out


def caja_alfa(im):
    ys, xs = np.nonzero(im[:, :, 3] == 255)
    return xs.min(), xs.max(), ys.min(), ys.max()


def hacer_modelo(nombre, rep):
    # la moto sola de lado fija el largo y el corrimiento para centrarla en el pivote
    sola = celda(nombre, 'lado', 0, rep, jinete=False)
    x0, x1, y0, y1 = caja_alfa(sola)
    largo = x1 - x0 + 1
    assert largo == LARGO, f'{nombre}: mide {largo} px de largo, no {LARGO}'
    dx = (CX - LARGO // 2) - x0
    imgs = {}
    for f, fila in enumerate(FILAS):
        for c in range(COLS):
            im = celda(nombre, fila, c, rep[c] if isinstance(rep, list) else rep)
            imgs[f, c] = recorrer(im, dx) if fila == 'lado' else im
    sola = recorrer(sola, dx)
    # icono 64x64: la moto sola centrada en vertical, sin sombra ni repartidor
    x0, x1, y0, y1 = caja_alfa(sola)
    alto = y1 - y0 + 1
    icono = np.zeros((ICONO, ICONO, 4), np.uint8)
    top = (ICONO - alto) // 2
    icono[top:top + alto, :] = sola[y0:y1 + 1, x0:x0 + ICONO]
    return imgs, icono, alto


# ============================================================ .meta de Unity
def guid_de(ruta_meta):
    if os.path.exists(ruta_meta):
        for linea_ in open(ruta_meta, encoding='utf-8'):
            if linea_.startswith('guid:'):
                return linea_.split()[1]
    return uuid.uuid4().hex


def id_de(texto):
    """internalID estable a partir del nombre (Unity solo pide que no se repita)."""
    h = 0
    for ch in texto:
        h = (h * 131 + ord(ch)) % (2 ** 62)
    return h or 1


def meta_textura(ruta_png, sprites, multiple):
    """sprites: [(nombre, x, y_unity, w, h, (px, py))]."""
    ruta_meta = ruta_png + '.meta'
    guid = guid_de(ruta_meta)
    tabla = ''.join(f'  - first:\n      213: {id_de(n)}\n    second: {n}\n' for n, *_ in sprites) if multiple else ''
    hojas = ''
    for n, x, y, w, h, (px, py) in (sprites if multiple else []):
        hojas += (f'    - serializedVersion: 2\n      name: {n}\n      rect:\n        serializedVersion: 2\n'
                  f'        x: {x}\n        y: {y}\n        width: {w}\n        height: {h}\n'
                  f'      alignment: 9\n      pivot: {{x: {px}, y: {py}}}\n      border: {{x: 0, y: 0, z: 0, w: 0}}\n'
                  f'      customData: \n      outline: []\n      physicsShape: []\n      tessellationDetail: 0\n'
                  f'      bones: []\n      spriteID: {uuid.uuid5(uuid.NAMESPACE_URL, guid + n).hex}\n'
                  f'      internalID: {id_de(n)}\n      vertices: []\n      indices: \n      edges: []\n      weights: []\n')
    pivote = sprites[0][5] if not multiple else (0.5, 0.0)
    texto = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
{tabla if multiple else ''}  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: {2 if multiple else 1}
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: {0 if multiple else 9}
  spritePivot: {{x: {pivote[0]}, y: {pivote[1]}}}
  spritePixelsToUnits: 32
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:{chr(10) + hojas.rstrip(chr(10)) if hojas else ' []'}
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid5(uuid.NAMESPACE_URL, guid).hex if not multiple else ''}
    internalID: {id_de(os.path.basename(ruta_png)) if not multiple else 0}
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
{''.join(f'      {n}: {id_de(n)}' + chr(10) for n, *_ in sprites) if multiple else '      {}' + chr(10)}  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    texto = texto.replace('nameFileIdTable:\n      {}\n', 'nameFileIdTable: {}\n')
    with open(ruta_meta, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(texto)


# ============================================================ main
def main():
    os.makedirs(DESTINO, exist_ok=True)
    rep = [leer_repartidor(c) for c in range(COLS)] if CHICA else leer_repartidor()
    hojas, previas = [], []
    for nombre, p in MODELOS.items():
        carpeta = os.path.join(DESTINO, nombre)
        os.makedirs(carpeta, exist_ok=True)
        imgs, icono, alto = hacer_modelo(nombre, rep)
        hoja = np.zeros((H * len(FILAS), W * COLS, 4), np.uint8)
        sprites = []
        for (f, c), im in imgs.items():
            hoja[f * H:(f + 1) * H, c * W:(c + 1) * W] = im
            sprites.append((f'{nombre}{SUFIJO}_{FILAS[f]}_{c}', c * W, (len(FILAS) - 1 - f) * H, W, H, (0.5, 0.0)))
        ruta = os.path.join(carpeta, f'{nombre}{SUFIJO}.png')
        Image.fromarray(hoja).save(ruta)
        meta_textura(ruta, sprites, multiple=True)
        if CHICA or LLUVIA:                         # el ícono (sin jinete) es el mismo para todos
            previas.append((nombre, imgs, icono))
            continue
        ruta_icono = os.path.join(carpeta, f'{nombre}_icono.png')
        Image.fromarray(icono).save(ruta_icono)
        meta_textura(ruta_icono, [(f'{nombre}_icono', 0, 0, ICONO, ICONO, (0.5, 0.5))], multiple=False)
        hojas.append(dict(imagen=f'{nombre}/{nombre}.png', icono=f'{nombre}/{nombre}_icono.png',
                          ancho=W * COLS, alto=H * len(FILAS), largo_lado_px=LARGO, alto_moto_px=int(alto),
                          sprites=[dict(name=n, x=x, y=(len(FILAS) - 1) * H - y, w=w, h=h) for n, x, y, w, h, _ in sprites],
                          nota=p['nota']))
        previas.append((nombre, imgs, icono))
    if CHICA or LLUVIA:
        vista_previa(previas, os.path.join(AQUI, f'motos{SUFIJO}_vista_previa.png'))
        print('motos' + SUFIJO + ':', [f'{n}/{n}{SUFIJO}.png' for n, *_ in previas])
        return
    meta = dict(formato=f'celdas de {W}x{H}, {COLS} columnas x {len(FILAS)} filas; fila 0 frente, 1 espaldas, '
                        '2 lado (mira a la derecha; para la izquierda se usa flipX); columna 0 parada, 1-2 rodando',
                origen='arriba-izquierda (Unity mide desde abajo)', pivote=[0.5, 0.0], ppu=32,
                celda=dict(w=W, h=H), largo_lado_px=LARGO, icono=dict(w=ICONO, h=ICONO, pivote=[0.5, 0.5]), hojas=hojas)
    with open(os.path.join(DESTINO, 'motos.json'), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=2, ensure_ascii=False)

    vista_previa(previas, os.path.join(DESTINO, 'motos_vista_previa.png'))
    shutil.copyfile(os.path.join(DESTINO, 'motos_vista_previa.png'), os.path.join(AQUI, 'motos_vista_previa.png'))
    print('motos:', [h['imagen'] for h in hojas])


def vista_previa(previas, ruta):
    """Una columna por moto con las 9 celdas y el icono, más una fila con las 4 de lado sobre una cuadrícula de tiles
    de 32 px para comparar el largo."""
    sep, esc = 8, 3
    ancho = len(previas) * (W * COLS + sep) + sep
    alto_tot = H * len(FILAS) + ICONO + 24 + H + 16
    lienzo = Image.new('RGBA', (ancho, alto_tot), (178, 196, 160, 255))
    d = ImageDraw.Draw(lienzo)
    x = sep
    for nombre, imgs, icono in previas:
        for (f, c), im in imgs.items():
            lienzo.alpha_composite(Image.fromarray(im), (x + c * W, f * H))
        lienzo.alpha_composite(Image.fromarray(icono), (x, H * len(FILAS) + 4))
        d.text((x + ICONO + 6, H * len(FILAS) + 4 + ICONO // 2 - 6), nombre, fill=(30, 30, 40, 255))
        x += W * COLS + sep
    y_cmp = H * len(FILAS) + ICONO + 16
    for xx in range(0, ancho, 32):
        d.line([(xx, y_cmp), (xx, y_cmp + H)], fill=(160, 178, 142, 255))
    for i, (nombre, imgs, _) in enumerate(previas):
        lienzo.alpha_composite(Image.fromarray(imgs[2, 0]), (sep + i * (W * COLS + sep) + 72, y_cmp))
    lienzo = lienzo.resize((lienzo.width * esc, lienzo.height * esc), Image.NEAREST)
    lienzo.save(ruta)


if __name__ == '__main__':
    main()
