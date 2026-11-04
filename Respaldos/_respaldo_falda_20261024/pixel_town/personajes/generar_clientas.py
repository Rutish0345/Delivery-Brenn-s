"""Clientas que salen de su casa a recibir el pedido (versión 2, con más detalle) y la flecha guía.

Clientas: 8 diseños con el nivel de detalle de los peatones y de "referencia clienta 1.png" (solo como
guía de estilo): cada prenda con 3 tonos (luz, base y sombra) y contorno oscuro, pelo con mechones y
reflejo, cara con ojos, chapitas, boca y pestañas, y la sombra suave de los pies del repartidor.
Mismo formato que repartidor_sheet.png (280x240, celdas de 56x80, fila 0 frente, 1 espaldas, 2 lado
mirando a la derecha; columna 0 quieta y 1-4 caminando). El cuerpo y la caminata salen del repartidor,
igual que los peatones (generar_personajes.py), así todos tienen el mismo tamaño y proporciones.

Flecha guía: 32x32, apunta a la derecha, dorada con contorno oscuro y un brillo (la usa el HUD).

Uso: python generar_clientas.py
"""
import colorsys
import json
import math
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from generar_personajes import (AQUI, COLS, FILAS, H, VISTA, W, cabezas, frames_meta, hexrgb, hsv,
                                leer_base, mezcla_color, redondear, referencias_material, tono)

PERSONAJES = os.environ.get('PERSONAJES_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Personajes')
DESTINO = os.path.join(PERSONAJES, 'Clientas')
RUTA_FLECHA = os.environ.get('FLECHA_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI\flecha_guia.png')
# flecha del mundo (FlechaGuia): chevron fino; la del HUD (GuiaPedido) sigue siendo flecha_guia.png
RUTA_CHEVRON = os.environ.get('CHEVRON_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI\flecha_chevron.png')

ROSA, MORADO, DORADO, TEAL, VERDE, NARANJA = '#ff6fa5', '#8c52ff', '#ffd166', '#5ed3d1', '#7bd88f', '#ff9a5c'
CREMA, CONTORNO = '#fff1d0', '#2a1f40'

CLIENTAS = [
    dict(n=1, pelo=('ondulado', '#6b4128'), piel='#f1c9a5', chaqueta='#2a2630', camisa='#a9a6b0',
         piernas=('jeans', '#a4c0dc'), zapatos=('tenis', '#25212b', '#f2efe8'),
         nota='pelo largo ondulado castaño, chamarra negra, blusa gris, jeans claros y tenis negros'),
    dict(n=2, pelo=('rizado', '#1f1a22'), piel='#a86e4a', chaqueta='#d9a43a', camisa='#f2efe8', botones='#6b4128',
         piernas=('falda_larga', '#5fa86e', '#a86e4a'), zapatos=('tenis', '#f2efe8', '#d8d2c8'), bolsa=('#efe3c4', VERDE),
         nota='pelo corto rizado negro, cárdigan mostaza, falda larga verde, tenis blancos y bolsa de tela'),
    dict(n=3, pelo=('chongo', '#bdb8c6'), piel='#eac0a0', chaqueta='#f2e6c9', camisa='#f2e6c9', flores=(ROSA, VERDE),
         piernas=('vestido', '#f2e6c9', '#eac0a0'), rebozo='#b48ad6', lentes='redondos',
         zapatos=('pantuflas', '#9b7fd1', '#c9b6f0'),
         nota='abuelita de chongo gris, lentes redondos, rebozo lila, vestido de flores y pantuflas'),
    dict(n=4, pelo=('tubos', '#8a5a3c'), piel='#f6d3b3', chaqueta='#ff8fbf', camisa='#ffd1e4',
         piernas=('bata', '#ff8fbf', '#f6d3b3'), cinto='#e0558f', zapatos=('pantuflas', '#fff1d0', ROSA),
         nota='bata rosa, tubos en el pelo y pantuflas: recién se levantó'),
    dict(n=5, pelo=('coletas', '#2b1d2e', ROSA), piel='#d9a07a', chaqueta=MORADO, camisa=MORADO, sudadera=True,
         piernas=('shorts', '#3d5a80', '#d9a07a'), zapatos=('tenis', '#f2efe8', ROSA), audifonos=('#3a3550', TEAL),
         nota='adolescente de coletas teñidas de rosa, sudadera morada grande, shorts y audífonos al cuello'),
    dict(n=6, pelo=('cola', '#c4472a'), piel='#f6d3b3', chaqueta=DORADO, camisa='#ffe39a', pecas=True,
         piernas=('jeans', '#4a6a9a'), delantal=(ROSA, CREMA), zapatos=('zapato', '#6b4128', '#8a5a3c'),
         nota='pelirroja con cola de caballo, delantal de lunares sobre blusa amarilla y jeans'),
    dict(n=7, pelo=('trenzas', '#3a2418', NARANJA), piel='#8d5a3b', chaqueta=TEAL, camisa='#9be7e4',
         flores=('#ffffff', ROSA), overol='#4f6fa8', piernas=('jeans', '#4f6fa8'),
         zapatos=('tenis', NARANJA, '#ffffff'),
         nota='trenzas, blusa teal con flores, overol de mezclilla y tenis de colores'),
    dict(n=8, pelo=('recto', '#23202e'), piel='#e9b48f', chaqueta='#2f8f8c', camisa='#f7f3ea', saco=True,
         piernas=('falda_lapiz', '#3b3f4a', '#e9b48f'), lentes='rectos', zapatos=('tacon', '#c2417a', '#e06a9c'),
         nota='oficinista de pelo corto recto, saco teal, falda lápiz, lentes y zapatos de tacón'),
]


# ============================================================ color
def rampa(c):
    """(contorno, sombra, base, luz, brillo): luz más cálida y sombra más fría, como el repartidor."""
    c = hexrgb(c) if isinstance(c, str) else c
    h, s, v = hsv(c)

    def hsv2(dh, ds, dv):
        r, g, b = colorsys.hsv_to_rgb((h + dh) % 1, min(1, max(0, s + ds)), min(1, max(0, v + dv)))
        return int(round(r * 255)), int(round(g * 255)), int(round(b * 255))

    oscuro = hexrgb(CONTORNO)
    sombra = hsv2(+0.02, +0.06, -0.17)
    contorno = tuple(int(a * 0.35 + b * 0.65) for a, b in zip(hsv2(0, 0.1, -0.35), oscuro))
    return contorno, sombra, c, hsv2(-0.015, -0.08, +0.11), hsv2(-0.02, -0.2, +0.22)


def borde(m):
    return m & ~ndimage.binary_erosion(m)


def desplazar(m, dy, dx):
    out = np.zeros_like(m)
    ys = slice(max(0, dy), H + min(0, dy))
    yo = slice(max(0, -dy), H + min(0, -dy))
    xs = slice(max(0, dx), W + min(0, dx))
    xo = slice(max(0, -dx), W + min(0, -dx))
    out[ys, xs] = m[yo, xo]
    return out


def sombrear(out, m, color, contorno=True, patron=None, pliegues=None, bob=0):
    """Pinta la máscara con 3 tonos: luz en la orilla de arriba/izquierda, sombra abajo/derecha y en
    los pliegues, contorno oscuro alrededor. patron(x, yr) puede devolver otro color (lunares, flores)."""
    con, som, base, luz, _ = rampa(color)
    b = borde(m) if contorno else np.zeros_like(m)
    adentro = m & ~b
    luz_m = adentro & (~desplazar(adentro, 1, 0) | ~desplazar(adentro, 0, 1))      # arriba o a la izquierda libre
    som_m = adentro & (~desplazar(adentro, -1, 0) | ~desplazar(adentro, 0, -1) |
                       ~desplazar(adentro, 0, -2))                                   # abajo o a la derecha
    for y, x in zip(*np.nonzero(m)):
        yr = y - bob
        if b[y, x]:
            col = con
        elif patron is not None and (p := patron(x, yr)) is not None:
            col = p
        elif som_m[y, x] or (pliegues is not None and pliegues(x, yr)):
            col = som
        elif luz_m[y, x]:
            col = luz
        else:
            col = base
        out[y, x] = (*col, 255)


def poner(out, y, x, col):
    if 0 <= y < H and 0 <= x < W:
        out[y, x] = (*col, 255)


def circulo(m, cx, cy, r):
    Y, X = np.mgrid[0:H, 0:W]
    m |= (X + .5 - cx) ** 2 + (Y + .5 - cy) ** 2 <= r * r
    return m


# ============================================================ pelo
def recogido(cabeza, vista):
    Y, X = np.mgrid[0:H, 0:W]
    m = ndimage.binary_erosion(cabeza, iterations=1) | (cabeza & (Y >= 18) & (Y <= 22))
    if vista == 'frente':
        m &= ~((Y >= 19) & (X >= 19) & (X <= 30))
    m &= Y <= 25
    return redondear(m, 7)


def ola(y, amp=1.3, periodo=2.6, fase=0.0):
    return int(round(amp * math.sin(y / periodo + fase)))


def mascara_pelo(estilo, vista, cabeza):
    Y, X = np.mgrid[0:H, 0:W]
    m = np.zeros((H, W), bool)
    if estilo == 'ondulado':                     # largo ondulado con volumen hasta el pecho
        m = redondear(cabeza.copy() | (cabeza & (Y <= 20)), 8)
        for y in range(18, 45):
            o = ola(y)
            if vista == 'frente':
                m[y, 11 + o:17] = True
                m[y, 32:39 + o] = True
            elif vista == 'espalda':
                m[y, 12 + o:38 + ola(y, fase=1.5)] = True
            else:
                m[y, 11 + o:23] = True
        if vista == 'frente':
            m &= ~((Y >= 21) & (X >= 17) & (X <= 31))          # la cara descubierta
            m |= (Y >= 19) & (Y <= 21) & (X >= 17) & (X <= 31) & (X + Y < 41 + 9)   # fleco de lado
        fondo = Y >= 44
        m &= ~(fondo & (((X + Y) % 3) == 0))                    # puntas irregulares
    elif estilo == 'rizado':                    # corto, rizado, con volumen
        m = cabeza.copy()
        for y, x in zip(*np.nonzero(borde(cabeza))):
            if (x + 2 * y) % 3 == 0:
                circulo(m, x + .5, y + .5, 2.2)
        if vista == 'frente':
            m &= ~((Y >= 21) & (X >= 17) & (X <= 32))
            m &= Y <= 27
        else:
            m &= Y <= 28
    elif estilo == 'chongo':
        m = recogido(cabeza, vista)
        if vista == 'frente':
            circulo(m, 24.5, 8.5, 3.6)
        elif vista == 'espalda':
            circulo(m, 24.5, 12.5, 4.6)
        else:
            circulo(m, 16.5, 11.5, 4.3)
    elif estilo == 'tubos':
        m = recogido(cabeza, vista)
    elif estilo == 'coletas':                   # fleco y dos coletas a los lados
        m = cabeza.copy()
        if vista == 'frente':
            m &= ~((Y >= 21) & (X >= 17) & (X <= 32))
            m &= Y <= 24
            for y in range(16, 37):
                o = ola(y, 1, 2.2)
                ancho = 4 if y < 30 else 3
                m[y, 13 - ancho + o:14 + o] = True
                m[y, 36 - o:37 + ancho - o] = True
        elif vista == 'espalda':
            m &= Y <= 27
            for y in range(16, 37):
                o = ola(y, 1, 2.2)
                ancho = 4 if y < 30 else 3
                m[y, 13 - ancho + o:14 + o] = True
                m[y, 36 - o:37 + ancho - o] = True
        else:
            m &= Y <= 27
            for y in range(14, 36):
                o = ola(y, 1, 2.2)
                m[y, 11 - (y - 14) // 6 + o:16 - (y - 14) // 6 + o] = True
    elif estilo == 'cola':                      # cola de caballo alta, fleco de lado
        m = recogido(cabeza, vista)
        if vista == 'frente':
            m |= cabeza & (Y >= 18) & (Y <= 21) & (X >= 16) & (X <= 27) & (X - Y < 9)
            circulo(m, 31.5, 9.5, 2.6)                          # la cola asoma por arriba
        elif vista == 'espalda':
            for y in range(10, 40):
                medio = 3 if y < 26 else 2 if y < 34 else 1
                cx = 24 + ola(y, 1, 3)
                m[y, cx - medio:cx + medio + 2] = True
        else:
            for y in range(10, 36):
                x = 15 - min(6, (y - 10) // 2) + ola(y, 1, 3)
                ancho = 4 if y < 24 else 3 if y < 30 else 2
                m[y, x - ancho:x + 1] = True
    elif estilo == 'trenzas':
        m = recogido(cabeza, vista)
        puntas = {'frente': [14, 33], 'espalda': [18, 29], 'lado': [15]}[vista]
        for x0 in puntas:
            for y in range(21 if vista != 'lado' else 19, 42):
                m[y, x0:x0 + 3] = True
    elif estilo == 'recto':                     # bob recto a la quijada, raya de lado
        m = cabeza.copy()
        if vista == 'frente':
            m |= (Y >= 19) & (Y <= 28) & (((X >= 14) & (X <= 17)) | ((X >= 32) & (X <= 35)))
            m &= ~((Y >= 22) & (X >= 18) & (X <= 31))
            m &= ~((Y >= 20) & (X >= 24) & (X <= 31) & (X - Y > 4))   # el fleco cae hacia un lado
        elif vista == 'espalda':
            m |= (Y >= 19) & (Y <= 28) & (X >= 14) & (X <= 35)
        else:
            m |= (Y >= 17) & (Y <= 28) & (X >= 14) & (X <= 23)
        m = redondear(m, 8)
    return m


def pintar_pelo(out, m, estilo, color, bob, color2=None, vista='frente'):
    """Mechones que bajan, sombra en la orilla de abajo/derecha, reflejo en la coronilla y contorno."""
    con, som, base, luz, brillo = rampa(color)
    b = borde(m)
    adentro = m & ~b
    som_m = adentro & (~desplazar(adentro, -1, 0) | ~desplazar(adentro, 0, -1))
    ys, xs = np.nonzero(m)
    if not len(ys):
        return
    top, x0, x1 = ys.min(), xs.min(), xs.max()
    cx = (x0 + x1) / 2
    for y, x in zip(ys, xs):
        yr = y - bob
        c = base
        if color2 and estilo == 'coletas' and yr >= 22 and (x < 16 or x > 33 or vista == 'lado' and x < 17):
            con2, som2, base2, luz2, _ = rampa(color2)       # puntas teñidas
            c = som2 if (x + ola(y, 1, 2.5)) % 3 == 0 else base2
            if b[y, x]:
                c = con2
            out[y, x] = (*c, 255)
            continue
        if estilo == 'rizado':
            k = (x + 2 * (yr // 2)) % 4
            c = som if k == 0 else (luz if k == 2 and yr % 2 == 0 and yr < top - bob + 9 else base)
        elif estilo == 'trenzas' and yr >= 21 and (x < 18 or x > 31 or (vista == 'espalda' and (18 <= x <= 20 or 29 <= x <= 31)) or vista == 'lado' and x < 18):
            c = luz if (x + yr) % 3 == 0 else (som if (x - yr) % 3 == 0 else base)   # tejido de la trenza
        else:
            if (x + ola(y, 1, 2.8, x * 0.4)) % 3 == 0:
                c = som                                        # mechón
            elif (x + ola(y, 1, 2.8, x * 0.4)) % 5 == 1 and yr > top - bob + 5:
                c = luz
        if som_m[y, x] and estilo != 'rizado':
            c = som
        # reflejo: un arco claro en la coronilla, del lado de la luz
        d = y - top
        if 2 <= d <= 4 and cx - 8 <= x <= cx + 1 and (x + d) % 2 == 0 and estilo not in ('tubos',):
            c = luz if d != 3 else brillo
        if b[y, x]:
            c = con
        out[y, x] = (*c, 255)


# ============================================================ cara
def cara(out, mat, celda, v, vista, bob, piel):
    """Quita la sombra dura de la mandíbula del repartidor y pone chapitas, boca, pestañas y pecas."""
    piel = hexrgb(piel)
    con, som, base, luz, _ = rampa(piel)
    for y in range(H):
        for x in range(W):
            if mat[y, x] != 'k':
                continue
            yr = y - bob
            if 20 <= yr <= 21:
                out[y, x] = (*base, 255)                       # frente (era sombra de la gorra)
            elif 26 <= yr <= 28 and 15 <= x <= 34:
                out[y, x] = (*tono(piel, -0.05), 255)
            elif yr == 29 and vista == 'frente':
                out[y, x] = (*tono(piel, -0.12), 255)          # quijada
            elif 29 <= yr <= 34:
                out[y, x] = (*tono(piel, -0.1), 255)           # cuello
    rubor = tuple(int(a * .5 + b * .5) for a, b in zip(piel, hexrgb('#ff7f9c')))
    boca = tuple(int(a * .4 + b * .6) for a, b in zip(piel, hexrgb('#b8455c')))
    pest = hexrgb('#2a1f30')
    if vista == 'frente':
        for x in (21, 22, 31, 32):
            poner(out, 26 + bob, x, rubor)
        poner(out, 28 + bob, 25, boca)
        poner(out, 28 + bob, 26, boca)
        poner(out, 22 + bob, 21, pest)
        poner(out, 22 + bob, 32, pest)
        if v.get('pecas'):
            for x, y in ((20, 25), (22, 25), (21, 27), (30, 25), (32, 25), (31, 27)):
                poner(out, y + bob, x, tono(piel, -0.18, 0.15))
    elif vista == 'lado':
        poner(out, 26 + bob, 29, rubor)
        poner(out, 26 + bob, 30, rubor)
        poner(out, 28 + bob, 31, boca)
        poner(out, 21 + bob, 33, pest)
        if v.get('pecas'):
            for x, y in ((28, 25), (30, 25), (29, 27)):
                poner(out, y + bob, x, tono(piel, -0.18, 0.15))


# ============================================================ ropa y accesorios
def mascara_falda(vista, YR, X, y0, y1, vuelo, ancho_extra=0):
    if vista == 'lado':
        izq = 18 - ancho_extra - (YR - y0) // vuelo
        der = 29 + ancho_extra + (YR - y0) // (vuelo + 2)
    else:
        izq = 17 - ancho_extra - (YR - y0) // vuelo
        der = 33 + ancho_extra + (YR - y0) // vuelo
    return (YR >= y0) & (YR <= y1) & (X >= izq) & (X <= der)


def patron_flores(c_flor, c_hoja):
    f, h = hexrgb(c_flor), hexrgb(c_hoja)

    def p(x, yr):
        cx, cy = (x + 2 * (yr // 5)) % 5, yr % 5
        if (cx, cy) in ((2, 1), (1, 2), (3, 2), (2, 3)):
            return f
        if (cx, cy) == (2, 2):
            return hexrgb(DORADO)
        if (cx, cy) == (4, 4):
            return h
        return None
    return p


def patron_lunares(c):
    col = hexrgb(c)
    return lambda x, yr: col if (x + 3 * (yr // 3)) % 4 == 0 and yr % 3 == 1 else None


def dibujar_clienta(celdas, ref, cabeza, v, f, c):
    celda, mat, bob = celdas[f, c]
    vista = VISTA[f]
    out = np.zeros_like(celda)
    Y, X = np.mgrid[0:H, 0:W]
    YR = Y - bob
    piel = hexrgb(v['piel'])
    tipo_piernas, color_piernas = v['piernas'][0], v['piernas'][1]
    piernas_desnudas = tipo_piernas in ('falda_larga', 'vestido', 'bata', 'falda_lapiz', 'shorts')
    chaqueta, camisa = hexrgb(v['chaqueta']), hexrgb(v['camisa'])
    zap_tipo, zap_base, zap_detalle = v['zapatos']

    # ---- cuerpo del repartidor recoloreado (ya trae luces, sombras, pliegues y contorno)
    for y in range(H):
        for x in range(W):
            m = mat[y, x]
            px = celda[y, x]
            yr = y - bob
            if m in ('C', 'h', '.'):
                continue
            if m == 'i' and not (21 <= x <= 33):
                out[y, x] = (*tono(piel, -0.08), 255)
                continue
            if m == 'Y':
                col = hexrgb(v['botones']) if v.get('botones') else mezcla_color(px[:3], ref['J'], chaqueta)
                out[y, x] = (*col, 255)
            elif m == 'J':
                out[y, x] = (*mezcla_color(px[:3], ref['J'], chaqueta), 255)
            elif m == 'W':
                out[y, x] = (*mezcla_color(px[:3], ref['W'], camisa), 255)
            elif m == 'P':
                if tipo_piernas == 'shorts' and yr <= 57:
                    out[y, x] = (*mezcla_color(px[:3], ref['P'], hexrgb(color_piernas)), 255)
                elif piernas_desnudas:
                    out[y, x] = (*mezcla_color(px[:3], ref['P'], piel, 0.35), 255)
                else:
                    out[y, x] = (*mezcla_color(px[:3], ref['P'], hexrgb(color_piernas)), 255)
            elif m in ('S', 'k'):
                out[y, x] = (*mezcla_color(px[:3], ref['S'], piel, 0.5 if m == 'k' else 0.9), 255)
            elif m == 'O' and yr >= 69 and px[3] == 255:
                out[y, x] = (*mezcla_color(px[:3], (0, 0, 0.09), hexrgb(zap_base), 0.8), 255)
            else:
                out[y, x] = px

    # ---- zapatos: suela clara de los tenis, pantuflas esponjadas, tacón brillante
    zap = (mat == 'O') & (YR >= 69) & (celda[..., 3] == 255)
    con_z, som_z, base_z, luz_z, brillo_z = rampa(zap_base)
    for y, x in zip(*np.nonzero(zap)):
        yr = y - bob
        abajo = not zap[min(H - 1, y + 1), x]
        arriba = not zap[max(0, y - 1), x]
        if zap_tipo == 'tenis':
            col = hexrgb(zap_detalle) if abajo else (luz_z if arriba else base_z)
            if not abajo and not arriba and (x + yr) % 3 == 0:
                col = hexrgb(zap_detalle) if zap_base != zap_detalle else som_z    # agujetas
        elif zap_tipo == 'pantuflas':
            col = som_z if abajo else (hexrgb(zap_detalle) if arriba and x % 2 == 0 else luz_z if arriba else base_z)
        elif zap_tipo == 'tacon':
            col = con_z if abajo else (brillo_z if arriba and x % 3 == 0 else base_z)
        else:
            col = som_z if abajo else (luz_z if arriba else base_z)
        out[y, x] = (*col, 255)

    # ---- prendas que tapan las piernas
    tapa = np.isin(mat, ['P', '.', 's']) | ((mat == 'O') & (YR >= 52) & (YR < 69))
    pliegue = lambda x, yr: yr > 52 and (x + yr // 6) % 5 == 0
    if tipo_piernas == 'falda_larga':
        sombrear(out, mascara_falda(vista, YR, X, 49, 68, 4) & tapa, color_piernas, pliegues=pliegue, bob=bob)
    elif tipo_piernas == 'vestido':
        fl = v.get('flores')
        sombrear(out, mascara_falda(vista, YR, X, 49, 62, 4) & tapa, color_piernas, pliegues=pliegue, bob=bob,
                 patron=patron_flores(*fl) if fl else None)
    elif tipo_piernas == 'bata':
        bm = mascara_falda(vista, YR, X, 49, 65, 6) & tapa
        sombrear(out, bm, color_piernas, pliegues=pliegue, bob=bob)
        if vista == 'frente':                                # cruce de la bata y bolsa
            for yr in range(49, 66):
                poner(out, yr + bob, 26 + (yr - 49) // 6, rampa(color_piernas)[1])
            for x in range(18, 23):
                poner(out, 55 + bob, x, rampa(color_piernas)[1])
    elif tipo_piernas == 'falda_lapiz':
        lm = (YR >= 49) & (YR <= 61) & (X >= (18 if vista != 'lado' else 19)) & (X <= (31 if vista != 'lado' else 29))
        sombrear(out, lm & tapa, color_piernas, bob=bob, pliegues=lambda x, yr: yr > 55 and x == 25 and vista == 'espalda')
    elif tipo_piernas == 'shorts':
        for y, x in zip(*np.nonzero((YR == 57) & (mat == 'P'))):
            out[y, x] = (*rampa(color_piernas)[1], 255)       # dobladillo
    elif tipo_piernas == 'jeans':                             # dobladillo claro y costura
        for y, x in zip(*np.nonzero((YR >= 66) & (YR <= 67) & (mat == 'P'))):
            out[y, x] = (*rampa(color_piernas)[3], 255)

    # ---- torso: estampado, sudadera, saco, delantal, overol, cinto, rebozo
    torso = np.isin(mat, ['J', 'W', 'Y']) & (YR <= 51)
    if v.get('flores') and tipo_piernas != 'vestido':
        p = patron_flores(*v['flores'])
        for y, x in zip(*np.nonzero(torso & (mat == 'J'))):
            col = p(x, y - bob)
            if col and out[y, x, 3]:
                out[y, x] = (*col, 255)
    elif v.get('flores'):
        p = patron_flores(*v['flores'])
        for y, x in zip(*np.nonzero(torso)):
            col = p(x, y - bob)
            if col:
                out[y, x] = (*col, 255)
    if v.get('sudadera'):
        # más larga y holgada: baja a la cadera; bolsa canguro, cordones y gorro
        larga = (YR >= 47) & (YR <= 55) & (X >= (15 if vista != 'lado' else 18)) & (X <= (34 if vista != 'lado' else 30))
        cuerpo = (torso | larga) & ~np.isin(mat, ['S', 'k'])
        sombrear(out, cuerpo & ((out[..., 3] > 0) | larga), v['chaqueta'], bob=bob,
                 pliegues=lambda x, yr: yr >= 53 and x % 2 == 0)                     # resorte de abajo
        con, som, base, luz, _ = rampa(v['chaqueta'])
        if vista == 'frente':
            for x in range(20, 31):
                poner(out, 45 + bob, x, som)
            for y in range(46, 52):
                poner(out, y + bob, 20, som)
                poner(out, y + bob, 30, som)
            for y in range(33, 39):
                poner(out, y + bob, 23, hexrgb(CREMA))
                poner(out, y + bob, 27, hexrgb(CREMA))
            for x in list(range(17, 21)) + list(range(29, 33)):
                poner(out, 31 + bob, x, luz)
        elif vista == 'espalda':
            gm = np.zeros((H, W), bool)
            gm[29 + bob:37 + bob, 18:32] = True
            gm &= ~((YR >= 35) & ((X < 20) | (X > 29)))
            sombrear(out, gm, tono(hexrgb(v['chaqueta']), -0.06), bob=bob)
        else:
            gm = np.zeros((H, W), bool)
            gm[28 + bob:35 + bob, 16:21] = True
            sombrear(out, gm, tono(hexrgb(v['chaqueta']), -0.06), bob=bob)
    if v.get('saco'):                                           # solapas y botón del saco
        con, som, base, luz, _ = rampa(v['chaqueta'])
        if vista == 'frente':
            for i, y in enumerate(range(33, 42)):
                poner(out, y + bob, 24 - min(i, 1), som)
                poner(out, y + bob, 29 + min(i, 1), som)
                poner(out, y + bob, 23 - min(i, 1), luz)
            poner(out, 44 + bob, 28, hexrgb('#1e1a22'))
            poner(out, 44 + bob, 21, hexrgb('#1e1a22'))
        elif vista == 'lado':
            for y in range(33, 41):
                poner(out, y + bob, 29, som)
    if v.get('delantal'):
        d, punto = v['delantal']
        if vista == 'frente':
            peto = (YR >= 35) & (YR <= 46) & (X >= 20) & (X <= 30)
            falda = (YR >= 47) & (YR <= 60) & (X >= 18) & (X <= 32)
            dm = (peto & (out[..., 3] > 0)) | falda
            sombrear(out, dm, d, bob=bob, patron=patron_lunares(punto))
            for y in range(31, 35):
                poner(out, y + bob, 21, rampa(d)[1])
                poner(out, y + bob, 29, rampa(d)[1])
            for x in range(21, 30):
                poner(out, 52 + bob, x, rampa(d)[1])           # bolsillo
        elif vista == 'espalda':
            for dy, dx in ((47, 21), (47, 22), (47, 23), (47, 26), (47, 27), (47, 28), (48, 22), (48, 23),
                           (48, 24), (48, 25), (48, 26), (48, 27), (49, 23), (49, 26), (50, 22), (51, 22),
                           (50, 27), (51, 28)):
                poner(out, dy + bob, dx, hexrgb(d) if (dx + dy) % 3 else rampa(d)[1])
        else:
            dm = (YR >= 35) & (YR <= 60) & (X >= 27) & (X <= 31) & ((out[..., 3] > 0) | (YR >= 49))
            sombrear(out, dm, d, bob=bob, patron=patron_lunares(punto))
    if v.get('overol'):
        o = v['overol']
        con, som, base, luz, _ = rampa(o)
        if vista == 'frente':
            peto = (YR >= 37) & (YR <= 51) & (X >= 20) & (X <= 30) & (out[..., 3] > 0)
            sombrear(out, peto, o, bob=bob)
            for y in range(31, 37):
                poner(out, y + bob, 21, base)
                poner(out, y + bob, 29, base)
            poner(out, 38 + bob, 21, hexrgb(DORADO))
            poner(out, 38 + bob, 29, hexrgb(DORADO))
            for x in range(22, 29):
                poner(out, 43 + bob, x, som)                  # bolsillo del peto
        elif vista == 'espalda':
            for y in range(31, 48):
                t = (y - 31) / 16
                poner(out, y + bob, int(round(19 + t * 9)), base)
                poner(out, y + bob, int(round(30 - t * 9)), base)
            sombrear(out, (YR >= 48) & (YR <= 51) & (X >= 17) & (X <= 32) & (out[..., 3] > 0), o, bob=bob)
        else:
            for y in range(31, 50):
                poner(out, y + bob, 26, base)
    if v.get('cinto'):
        cint = hexrgb(v['cinto'])
        for y, x in zip(*np.nonzero((YR >= 47) & (YR <= 48) & (out[..., 3] > 0) & np.isin(mat, ['J', 'W']))):
            out[y, x] = (*cint, 255)
        if vista in ('frente', 'espalda'):
            for dy, dx in ((49, 23), (50, 22), (49, 26), (50, 27), (51, 22), (51, 27)):
                poner(out, dy + bob, dx, cint)
        if vista == 'frente':                                   # solapa de la bata, de pijama abajo
            for i, y in enumerate(range(33, 47)):
                poner(out, y + bob, 24 + i // 5, rampa(v['chaqueta'])[1])
    if v.get('rebozo'):
        r = v['rebozo']
        cuerpo = (out[..., 3] > 0) & (mat != '.') & ~np.isin(mat, ['C', 'h'])
        if vista == 'frente':
            rm = (YR >= 30) & (YR <= 42) & (X >= 15) & (X <= 34)
            abierto = (X >= 24 - (YR - 30) // 3) & (X <= 25 + (YR - 30) // 3) & (YR >= 32)
            rm &= ~abierto & ~(np.isin(mat, ['S', 'k']) & (YR <= 33))
        elif vista == 'espalda':
            rm = (YR >= 30) & (YR <= 44) & (X >= 15) & (X <= 34) & (YR <= 44 - np.abs(X - 24.5) // 2)
        else:
            rm = (YR >= 30) & (YR <= 42) & (X >= 17) & (X <= 31)
        rm &= ndimage.binary_dilation(cuerpo, iterations=1)
        fleco = rm & ~desplazar(rm, -1, 0)
        sombrear(out, rm & ~fleco, r, bob=bob, pliegues=lambda x, yr: yr % 3 == 0)
        for y, x in zip(*np.nonzero(fleco)):
            if x % 2 == 0:
                poner(out, y, x, rampa(r)[1])
                poner(out, y + 1, x, rampa(r)[2])

    # ---- pelo (y la piel que destapa un peinado recogido)
    estilo, color_pelo = v['pelo'][0], v['pelo'][1]
    pm = np.roll(mascara_pelo(estilo, vista, cabeza[vista]), bob, axis=0)
    cab = np.roll(cabeza[vista], bob, axis=0)
    hueco = cab & ~pm & (YR >= 17) & (out[..., 3] == 0)
    for y, x in zip(*np.nonzero(hueco)):
        out[y, x] = (*(tono(piel, -0.04) if YR[y, x] == 17 else piel), 255)
    cara(out, mat, celda, v, vista, bob, v['piel'])
    pintar_pelo(out, pm, estilo, color_pelo, bob, v['pelo'][2] if len(v['pelo']) > 2 else None, vista)
    accesorios(out, v, vista, bob, cabeza[vista], pm)
    return out


def accesorios(out, v, vista, bob, cabeza, pm):
    estilo = v['pelo'][0]
    if estilo == 'tubos':                                         # tubos de colores, cilindros de 3 tonos
        colores = [TEAL, DORADO, VERDE, ROSA]
        filas = {'frente': [(9, (17, 23, 29)), (13, (14, 20, 26, 32))],
                 'espalda': [(9, (17, 23, 29)), (13, (14, 20, 26, 32)), (17, (17, 23, 29))],
                 'lado': [(9, (17, 23)), (13, (14, 20, 26))]}[vista]
        k = 0
        for y, xs in filas:
            for x in xs:
                con, som, base, luz, _ = rampa(colores[k % 4])
                k += 1
                for dx in range(5):
                    poner(out, y + bob, x + dx, con if dx in (0, 4) else luz)
                    poner(out, y + 1 + bob, x + dx, con if dx in (0, 4) else base)
                    poner(out, y + 2 + bob, x + dx, con if dx in (0, 4) else som)
                    poner(out, y + 3 + bob, x + dx, con)
    if estilo == 'coletas' and vista in ('frente', 'espalda'):  # ligas
        for x in (12, 13, 36, 37):
            poner(out, 16 + bob, x, hexrgb(MORADO))
            poner(out, 17 + bob, x, hexrgb(MORADO))
    if estilo == 'cola':
        liga = hexrgb(DORADO)
        if vista == 'espalda':
            for x in range(22, 28):
                poner(out, 12 + bob, x, liga)
        elif vista == 'lado':
            for y in range(10, 13):
                poner(out, y + bob, 15, liga)
                poner(out, y + bob, 16, liga)
    if estilo == 'trenzas':
        liga = hexrgb(v['pelo'][2])
        puntas = {'frente': [14, 33], 'espalda': [18, 29], 'lado': [15]}[vista]
        for x0 in puntas:
            for dx in range(3):
                poner(out, 40 + bob, x0 + dx, liga)
            for dx in range(-1, 4):
                poner(out, 42 + bob, x0 + dx, rampa(v['pelo'][1])[2] if dx % 2 else rampa(v['pelo'][1])[0])
        if vista == 'frente':
            for y in range(10, 19):
                poner(out, y + bob, 24, rampa(v['pelo'][1])[0])     # raya en medio
    if v.get('lentes') and vista in ('frente', 'lado'):
        redondos = v['lentes'] == 'redondos'
        armazon = hexrgb('#9a7a3a') if redondos else hexrgb('#1e1a22')
        brillo = (235, 240, 255)
        lentes = {'frente': [(23.5, 23.5), (30.5, 23.5)], 'lado': [(32, 23.5)]}[vista]
        for cx, cy in lentes:
            if redondos:
                for a in range(0, 360, 20):
                    x = int(round(cx + 2.6 * math.cos(math.radians(a)) - .5))
                    y = int(round(cy + 2.6 * math.sin(math.radians(a)) - .5))
                    poner(out, y + bob, x, armazon)
            else:
                for x in range(int(cx - 3), int(cx + 3)):
                    poner(out, int(cy - 2) + bob, x, armazon)
                    poner(out, int(cy + 1) + bob, x, tono(armazon, 0.25))
                poner(out, int(cy - 1) + bob, int(cx - 3), armazon)
                poner(out, int(cy) + bob, int(cx - 3), armazon)
                poner(out, int(cy - 1) + bob, int(cx + 2), armazon)
                poner(out, int(cy) + bob, int(cx + 2), armazon)
            poner(out, int(cy - 1) + bob, int(cx - 1), brillo)
        if vista == 'frente':
            poner(out, 22 + bob, 27, armazon)
        else:
            for x in range(25, 30):
                poner(out, 23 + bob, x, armazon)
    if v.get('audifonos'):
        banda, copa = v['audifonos']
        con, som, base, luz, _ = rampa(copa)
        bc = hexrgb(banda)
        if vista == 'frente':
            for x in range(19, 31):
                poner(out, 31 + bob, x, bc)
            for x0 in (16, 31):
                for y in range(30, 34):
                    for dx in range(3):
                        poner(out, y + bob, x0 + dx, con if y in (30, 33) or dx in (0, 2) and x0 == 16 and dx == 0 or
                              (x0 == 31 and dx == 2) else (luz if dx == 1 and y == 31 else base))
        elif vista == 'espalda':
            for x in range(18, 32):
                poner(out, 30 + bob, x, bc)
        else:
            for y in range(29, 34):
                for x in range(22, 26):
                    poner(out, y + bob, x, con if y in (29, 33) or x in (22, 25) else (luz if y == 30 else base))
    if v.get('bolsa'):                                           # bolsa de tela al hombro
        tela, estampa = v['bolsa']
        bm = np.zeros((H, W), bool)
        if vista == 'frente':
            x0, x1, correa = 35, 42, [(32 + i // 3, 33 + i // 3) for i in range(12)]
        elif vista == 'espalda':
            x0, x1, correa = 7, 14, [(32 + i // 3, 16 - i // 3) for i in range(12)]
        else:
            x0, x1, correa = 24, 31, [(32 + i, 26) for i in range(12)]
        bm[44 + bob:56 + bob, x0:x1 + 1] = True
        sombrear(out, bm, tela, bob=bob)
        for y, x in correa:
            poner(out, y + bob, x, rampa(tela)[1])
        con, som, base, luz, _ = rampa(estampa)
        cx = (x0 + x1) // 2
        for dy, dx, col in ((48, 0, base), (49, -1, base), (49, 1, luz), (50, 0, som), (51, 0, som)):
            poner(out, dy + bob, cx + dx, col)


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


def chevron():
    """La flecha guía del mundo ya no se dibuja aquí: es la de pixel_town/generate_flecha.py (dorada, opaca, con borde
    oscuro y blanco). Se deja este nombre para que correr este script la siga escribiendo igual."""
    import sys
    sys.path.insert(0, os.path.dirname(AQUI) if os.path.basename(AQUI) == 'personajes' else AQUI)
    import generate_flecha
    return generate_flecha.chevron()


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
    Image.fromarray(chevron()).save(RUTA_CHEVRON)

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
    lienzo = lienzo.resize((lienzo.width * esc, lienzo.height * esc), Image.NEAREST)
    lienzo.save(os.path.join(DESTINO, 'clientas_vista_previa.png'))
    shutil.copyfile(os.path.join(DESTINO, 'clientas_vista_previa.png'), os.path.join(AQUI, 'clientas_vista_previa.png'))
    print('clientas:', [h['imagen'] for h in hojas], '| flecha:', RUTA_FLECHA)


if __name__ == '__main__':
    main()
