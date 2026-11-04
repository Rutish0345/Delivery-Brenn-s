"""Arte y acomodo de la PRESENTACIÓN VIVA del menú principal (octubre 2026).

Encima de menu_fondo.png (que no cambia), todo en Assets/Sprits/Presentacion:
  - letrero_0..2.png (480x100, a x4 arriba): letrero crema con borde rosa y contorno #2A1F40 colgado con dos cables de
    los postes, con el medallón de tijeras y moño dorado arriba; 3 cuadros del vaivén de 1 px (-1, 0, +1): el cable se
    vuelve a trazar en cada uno para que siga amarrado al poste. El título (Press Start) y "ENTREGAS CON ESTILO" los
    pone ConstruirMenuPrincipal encima.
  - postes.png (480x270): los dos postes de los cables.
  - repartidor_*.png y repartidora_*.png (56x80, a x7): de lado mirando al centro (ella espejada): idle, respira (el
    torso 1 px abajo), parpadeo y saludo_0..2 (el brazo de atrás levantado, la mano en 3 posiciones). Salen de las
    hojas de Assets/Sprits/Personajes, así que siguen cualquier cambio de ropa.
  - moto_<id>.png (y moto_<id>_izq.png espejada) y camioneta.png (a x3): estacionadas en fila sobre la banqueta,
    alternando orientación. Las motos son su ícono de la tienda con la caja del emblema, un moño dorado en la tapa y su
    sombra; la camioneta es la de reparto del Comercial pintada con la marca Brenn's (crema y rosa, contorno #2A1F40 y
    el medallón de tijeras con moño). faro.png: el destello del faro.
  - cerezo.png (a x4, dos, uno espejado), petalo_0..3.png (el pétalo en 4 giros), brillo_0..1.png, nube_0..2.png,
    mariposa_0..1.png, globo_{esmalte,labial,tijeras}.png y ventana_{encendida,apagada}.png.
  - presentacion.json: dónde va cada pieza en el Canvas (1920x1080, origen al centro, y hacia arriba; x,y = esquina de
    arriba a la izquierda) y los lugares de las ventanas, techos (globos), copas (brillos) y el faro. Lo lee
    ConstruirMenuPrincipal.
  - presentacion_vista_previa.png (aquí): la composición quieta a 1920x1080, como la arma Unity.
Uso: python generar_presentacion.py
"""
import json
import os
import random

from PIL import Image, ImageDraw, ImageOps

import generar_intro as g
from generar_intro import (BLANCO, CREMA, CREMA_OSC, DORADO, DORADO_OSC, LAVANDA, MORADO, OSCURO, ROSA, ROSA_CLARO,
                           ROSA_OSC, ROSA_PASTEL, VINO, rgba)

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.join(g.SPRITS, 'Presentacion')
PERSONAJES = os.path.join(g.SPRITS, 'Personajes')
MOTOS = os.path.join(g.SPRITS, 'Motos')
COMERCIAL = os.path.join(AQUI, '..', 'unity', 'comercial')
MOTOS_FILA = [('scooter_naranja', False), ('clasica_azul', True), (None, False), ('chopper_verde', True),
              ('deportiva_roja', False)]      # None: la camioneta; True: mirando a la izquierda

ESC_FONDO, ESC_PERSONAJE, ESC_VEHICULO = 4, 7, 3
PISO_VEHICULOS = -440      # y del Canvas donde apoyan las llantas
PIE_PERSONAJES = -500      # y del Canvas de la orilla de abajo de la celda de 56x80
X_PERSONAJES = 800         # centro de cada personaje (a la izquierda el repartidor, a la derecha la repartidora)
LIBRE = 690                # los vehículos van entre -LIBRE y LIBRE (no tocan a los personajes)
Y_CONTINUAR = -490         # "HAZ CLIC PARA CONTINUAR" (para revisar que nada lo tape)
CAFE = (0x7A, 0x45, 0x5A)
CAFE_OSC = (0x55, 0x2E, 0x45)


def canvas(px, py):
    """Píxel del fondo (480x270, arriba a la izquierda) → Canvas (centro, y hacia arriba)."""
    return (px - g.W // 2) * ESC_FONDO, (g.H // 2 - py) * ESC_FONDO


# ============================================================ letrero y postes
LETRERO_W, LETRERO_H = 480, 100
TABLA = (40, 22, 439, 83)          # la tabla dentro del sprite del letrero (x0, y0, x1, y1)
POSTES_X = (4, 473)                # columna izquierda de cada poste (3 px de ancho)
POSTE_ARRIBA = 6


def medallon(d, cx, cy):
    """Medallón redondo rosa con las tijeras y el moño dorado (21x21)."""
    d.ellipse((cx - 10, cy - 10, cx + 10, cy + 10), fill=rgba(OSCURO))
    d.ellipse((cx - 9, cy - 9, cx + 9, cy + 9), fill=rgba(ROSA))
    d.ellipse((cx - 7, cy - 7, cx + 7, cy + 7), fill=rgba(CREMA))
    d.arc((cx - 7, cy - 7, cx + 7, cy + 7), 200, 340, fill=rgba(BLANCO))
    # tijeras abiertas hacia arriba
    d.line([(cx - 4, cy - 5), (cx + 1, cy + 1)], fill=rgba(OSCURO), width=2)
    d.line([(cx + 4, cy - 5), (cx - 1, cy + 1)], fill=rgba(OSCURO), width=2)
    d.line([(cx - 4, cy - 5), (cx, cy)], fill=rgba(g.GRIS))
    d.line([(cx + 4, cy - 5), (cx, cy)], fill=rgba(g.GRIS))
    d.ellipse((cx - 5, cy + 1, cx - 1, cy + 5), outline=rgba(ROSA_OSC))
    d.ellipse((cx + 1, cy + 1, cx + 5, cy + 5), outline=rgba(ROSA_OSC))
    # moño dorado arriba, entre las puntas
    moño(d, cx, cy - 5)


def moño(d, cx, cy):
    """Moño dorado de 7x4 con el nudo al centro."""
    d.polygon([(cx - 3, cy - 2), (cx, cy), (cx - 3, cy + 1)], fill=rgba(DORADO), outline=rgba(DORADO_OSC))
    d.polygon([(cx + 3, cy - 2), (cx, cy), (cx + 3, cy + 1)], fill=rgba(DORADO), outline=rgba(DORADO_OSC))
    d.point((cx, cy), fill=rgba(DORADO_OSC))


def letrero(dx):
    im = Image.new('RGBA', (LETRERO_W, LETRERO_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = TABLA[0] + dx, TABLA[1], TABLA[2] + dx, TABLA[3]
    # los cables: de la punta de cada poste a la esquina de arriba de la tabla (con un remache dorado)
    for poste, esquina in ((POSTES_X[0] + 1, x0 + 6), (POSTES_X[1] + 1, x1 - 6)):
        d.line([(poste, POSTE_ARRIBA + 1), (esquina, y0)], fill=rgba(OSCURO))
    # la tabla: contorno, borde rosa con su luz, el crema y una línea crema oscura de relieve abajo
    d.rounded_rectangle((x0, y0, x1, y1), radius=4, fill=rgba(OSCURO))
    d.rounded_rectangle((x0 + 1, y0 + 1, x1 - 1, y1 - 1), radius=3, fill=rgba(ROSA))
    d.line([(x0 + 4, y0 + 2), (x1 - 4, y0 + 2)], fill=rgba(ROSA_CLARO))
    d.line([(x0 + 4, y1 - 2), (x1 - 4, y1 - 2)], fill=rgba(ROSA_OSC))
    d.rectangle((x0 + 4, y0 + 4, x1 - 4, y1 - 4), fill=rgba(OSCURO))
    d.rectangle((x0 + 5, y0 + 5, x1 - 5, y1 - 5), fill=rgba(CREMA))
    d.line([(x0 + 5, y1 - 6), (x1 - 5, y1 - 6)], fill=rgba(CREMA_OSC))
    d.line([(x0 + 6, y0 + 6), (x0 + 30, y0 + 6)], fill=rgba(BLANCO))
    # remaches dorados en las cuatro esquinas del borde y donde amarra el cable
    for x, y in ((x0 + 2, y0 + 2), (x1 - 2, y0 + 2), (x0 + 2, y1 - 2), (x1 - 2, y1 - 2), (x0 + 6, y0), (x1 - 6, y0)):
        d.point((x, y), fill=rgba(DORADO))
    # corazoncitos a los lados del título
    for cx in (x0 + 16, x1 - 16):
        corazon(d, cx, (y0 + y1) // 2 - 4)
    medallon(d, (x0 + x1) // 2, y0 - 2)   # arriba al centro, encima del borde (el título queda debajo)
    return im


def corazon(d, cx, cy):
    for x, y in ((-2, -1), (-1, -2), (1, -2), (2, -1), (-2, 0), (-1, -1), (0, -1), (1, -1), (2, 0), (-1, 0), (0, 0),
                 (1, 0), (-1, 1), (0, 1), (1, 1), (0, 2)):
        d.point((cx + x, cy + y), fill=rgba(ROSA))
    d.point((cx - 1, cy - 1), fill=rgba(BLANCO))


def postes():
    im = Image.new('RGBA', (g.W, g.H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for x in POSTES_X:
        d.rectangle((x, POSTE_ARRIBA, x + 2, 222), fill=rgba(VINO))
        d.line([(x, POSTE_ARRIBA), (x, 222)], fill=rgba(ROSA_OSC))
        d.rectangle((x - 1, POSTE_ARRIBA - 2, x + 3, POSTE_ARRIBA), fill=rgba(OSCURO))
        d.point((x + 1, POSTE_ARRIBA + 1), fill=rgba(DORADO))
    return im


# ============================================================ personajes
CEL_W, CEL_H = 56, 80
OJO = {'000000', 'DFC0A0', 'D5CABF', '251F1F', 'E3D5C7', 'FFFFFF', '4F3333', '5B352E'}
PIEL = (0xFF, 0xDC, 0xC0)
MANO, MANO_SOMBRA, MANO_LINEA = (0xC5, 0x7E, 0x5E), (0xB0, 0x6E, 0x50), (0x90, 0x48, 0x2B)
MANGA = {False: ((0x7E, 0x4F, 0xCB), (0x65, 0x3A, 0xB1), (0x34, 0x10, 0x74)),     # base, sombra, contorno
         True: ((0xB8, 0xA4, 0xE6), (0x96, 0x80, 0xCC), (0x6F, 0x5A, 0xA8))}
# la mano del saludo en 3 posiciones: (codo, muñeca, desplazamiento de la palma y su inclinación)
MANOS = [((31, 34), (38, 28), (0, 0, 0)), ((31, 34), (38, 28), (1, 1, 1)), ((31, 34), (38, 28), (0, 0, -1))]


def hex6(p):
    return '%02X%02X%02X' % p[:3]


def cuadro_lado(chica):
    hoja = Image.open(os.path.join(PERSONAJES, ('repartidora' if chica else 'repartidor') + '_sheet.png')).convert('RGBA')
    return hoja.crop((0, 2 * CEL_H, CEL_W, 3 * CEL_H))


def respira(im):
    """El torso, los brazos y la cabeza (filas 0..47) 1 px abajo; las piernas quedan."""
    out = im.copy()
    arriba = im.crop((0, 0, CEL_W, 48))
    out.paste((0, 0, 0, 0), (0, 0, CEL_W, 49))
    out.alpha_composite(arriba, (0, 1))
    return out


def parpadeo(im):
    out = im.copy()
    px = out.load()
    for y in range(21, 26):
        for x in range(28, 34):
            if px[x, y][3] and hex6(px[x, y]) in OJO:
                px[x, y] = rgba(PIEL)
    for x in range(29, 33):
        px[x, 24] = rgba((0x25, 0x1F, 0x1F))
    return out


def saludo(im, chica, k):
    """El brazo de atrás levantado (se pinta detrás del personaje: solo asoman el antebrazo y la mano)."""
    base, sombra, linea = MANGA[chica]
    capa = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    codo, muñeca, (dx, dy, incl) = MANOS[k]
    hombro = (25, 36)
    for w, c in ((5, linea), (3, sombra)):
        d.line([hombro, codo, muñeca], fill=rgba(c), width=w, joint='curve')
    d.line([codo, muñeca], fill=rgba(base))
    # puño de la manga
    d.rectangle((muñeca[0] - 1, muñeca[1] - 1, muñeca[0] + 1, muñeca[1] + 1), fill=rgba(linea))
    # la palma abierta (4x5) con el pulgar, inclinada con incl
    mx, my = muñeca[0] + 1 + dx, muñeca[1] - 6 + dy
    filas = [(1, 3), (0, 4), (0, 4), (0, 4), (1, 3)]
    for j, (a, b) in enumerate(filas):
        corr = incl if j < 2 else 0
        for i in range(a - 1, b + 2):
            capa.putpixel((mx + i + corr, my + j), rgba(MANO_LINEA))
    for j, (a, b) in enumerate(filas):
        corr = incl if j < 2 else 0
        for i in range(a, b + 1):
            capa.putpixel((mx + i + corr, my + j), rgba(MANO if i < b else MANO_SOMBRA))
    for i in range(1, 4):
        capa.putpixel((mx + i, my - 1 + (1 if incl < 0 and i == 3 else 0)), rgba(MANO_LINEA))
    capa.putpixel((mx - 1, my + 3), rgba(MANO))                 # el pulgar
    capa.putpixel((mx - 2, my + 3), rgba(MANO_LINEA))
    capa.alpha_composite(im)
    return capa


def personajes():
    res = {}
    for chica in (False, True):
        quien = 'repartidora' if chica else 'repartidor'
        base = cuadro_lado(chica)
        cuadros = {'idle': base, 'respira': respira(base), 'parpadeo': parpadeo(base)}
        for k in range(3):
            cuadros[f'saludo_{k}'] = saludo(base, chica, k)
        for n, im in cuadros.items():
            res[f'{quien}_{n}'] = ImageOps.mirror(im) if chica else im   # ella, a la derecha, mira a la izquierda
    return res


# ============================================================ vehículos
def recortar(im):
    return im.crop(im.getbbox())


def sombra_bajo(im, alto=3):
    """La moto con su sombra plana debajo de las llantas."""
    w, h = im.size
    out = Image.new('RGBA', (w, h + alto - 1), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    d.ellipse((2, h - 2, w - 3, h + alto - 2), fill=(0x3B, 0x2F, 0x4A, 110))
    out.alpha_composite(im, (0, 0))
    return out


def moto(id_):
    im = recortar(Image.open(os.path.join(MOTOS, id_, f'{id_}_icono.png')).convert('RGBA'))
    # el moño dorado sobre la tapa de la caja (la caja ya lleva las tijeras: el emblema de Brenn's)
    px = im.load()
    rosa = [(x, y) for y in range(im.height) for x in range(im.width) if px[x, y][3] and px[x, y][:3] == ROSA]
    x0 = min(x for x, _ in rosa)
    x1 = max(x for x, _ in rosa if x < x0 + 30)
    tapa = min(y for x, y in rosa if x <= x1)
    cx = (x0 + x1) // 2
    d = ImageDraw.Draw(im)
    moño(d, cx, tapa - 2)
    return sombra_bajo(im)


CAMIONETA_COLORES = {   # la camioneta de reparto (Comercial) → Brenn's
    'D8CDB0': CREMA, 'E7DECB': (0xFF, 0xF8, 0xE6), 'EEE8D3': BLANCO, 'B9AD90': CREMA_OSC,
    'B2A35B': ROSA, 'CABC79': ROSA_CLARO, '8B7F47': ROSA_OSC,
}


def camioneta():
    meta = json.load(open(os.path.join(COMERCIAL, 'props_atlas.json'), encoding='utf-8'))
    s = next(x for x in meta['sprites'] if x['name'] == 'camioneta_reparto')
    im = Image.open(os.path.join(COMERCIAL, 'props_atlas.png')).convert('RGBA').crop(
        (s['x'], s['y'], s['x'] + s['w'], s['y'] + s['h']))
    w, h = im.size
    out = Image.new('RGBA', (w + 2, h + 2), (0, 0, 0, 0))
    out.alpha_composite(im, (1, 1))
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] == 255 and hex6(p) in CAMIONETA_COLORES:
                px[x, y] = rgba(CAMIONETA_COLORES[hex6(p)])
    # contorno #2A1F40 alrededor de lo opaco (no de la sombra del piso)
    solido = {(x, y) for y in range(out.height) for x in range(out.width) if px[x, y][3] == 255}
    for x, y in list(solido):
        for vx, vy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + vx, y + vy)
            if 0 <= q[0] < out.width and 0 <= q[1] < out.height and q not in solido and px[q][3] < 255:
                px[q] = rgba(OSCURO)
    # el medallón de la florería → el de Brenn's (círculo verde 20x20 en la caja)
    verdes = [(x, y) for (x, y) in solido if hex6(px[x, y]) in ('56764F', '86A571')]
    cx = (min(x for x, _ in verdes) + max(x for x, _ in verdes)) // 2
    cy = (min(y for _, y in verdes) + max(y for _, y in verdes)) // 2
    d = ImageDraw.Draw(out)
    d.rectangle((cx - 11, cy - 11, cx + 11, cy + 11), fill=rgba(CREMA))
    medallon(d, cx, cy)
    return out


def faro():
    im = Image.new('RGBA', (7, 5), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse((0, 0, 6, 4), fill=rgba(DORADO, 150))
    d.ellipse((2, 1, 4, 3), fill=rgba(CREMA))
    return im


# ============================================================ entorno
def cerezo():
    w, h = 72, 84
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # tronco y ramas
    d.polygon([(33, h - 1), (39, h - 1), (38, 50), (34, 50)], fill=rgba(CAFE))
    d.line([(34, h - 1), (34, 52)], fill=rgba(CAFE_OSC))
    for rama in ([(36, 56), (24, 42), (16, 36)], [(37, 54), (48, 40), (56, 34)], [(36, 52), (36, 32)]):
        d.line(rama, fill=rgba(CAFE), width=2)
    d.ellipse((26, h - 3, 46, h + 1), fill=(0x3B, 0x2F, 0x4A, 90))
    # copa: bultos de flor (sombra, base, luz) y flores blancas
    rnd = random.Random(11)
    bultos = [(36, 24, 16), (20, 32, 12), (52, 30, 13), (28, 14, 11), (46, 14, 11), (12, 40, 8), (61, 40, 8),
              (36, 38, 12)]
    for cx, cy, r in bultos:
        d.ellipse((cx - r, cy - r + 2, cx + r, cy + r + 2), fill=rgba(ROSA_OSC))
    for cx, cy, r in bultos:
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgba(ROSA))
    for cx, cy, r in bultos:
        d.ellipse((cx - r + 2, cy - r + 1, cx + r - 3, cy + r - 4), fill=rgba(ROSA_CLARO))
    for _ in range(70):
        x, y = rnd.randint(6, 66), rnd.randint(4, 50)
        if im.getpixel((x, y))[3] and im.getpixel((x, y))[:3] in (ROSA, ROSA_CLARO):
            g.flor(d, x, y, rnd.choice((BLANCO, ROSA_PASTEL, ROSA_PASTEL)))
    # las ramas asoman entre las flores de abajo
    d.line([(36, 52), (36, 44)], fill=rgba(CAFE))
    return im


def petalos():
    formas = [[(1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (1, 2)],
              [(0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (2, 2)],
              [(1, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2)],
              [(0, 0), (1, 1), (2, 1), (0, 1), (1, 2), (2, 2)]]
    res = []
    for k, f in enumerate(formas):
        im = Image.new('RGBA', (3, 3), (0, 0, 0, 0))
        for i, (x, y) in enumerate(f):
            im.putpixel((x, y), rgba(ROSA_CLARO if i % 3 else ROSA))
        res.append(im)
    return res


def brillos():
    res = []
    for r in (1, 2):
        im = Image.new('RGBA', (5, 5), (0, 0, 0, 0))
        g.destello(ImageDraw.Draw(im), 2, 2, r, BLANCO, CREMA)
        res.append(im)
    return res


def nubes():
    res = []
    for ancho in (54, 70, 44):
        im = Image.new('RGBA', (ancho + 10, ancho // 2 + 8), (0, 0, 0, 0))
        g.nube(ImageDraw.Draw(im), im.width // 2, im.height // 2 + 2, ancho)
        res.append(recortar(im))
    return res


def mariposa():
    res = []
    for abierta in (True, False):
        im = Image.new('RGBA', (9, 7), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        if abierta:
            d.ellipse((0, 0, 3, 3), fill=rgba(ROSA), outline=rgba(ROSA_OSC))
            d.ellipse((5, 0, 8, 3), fill=rgba(ROSA), outline=rgba(ROSA_OSC))
            d.ellipse((1, 3, 3, 5), fill=rgba(LAVANDA))
            d.ellipse((5, 3, 7, 5), fill=rgba(LAVANDA))
            d.point((1, 1), fill=rgba(DORADO))
            d.point((7, 1), fill=rgba(DORADO))
        else:
            d.polygon([(3, 0), (4, 3), (2, 3)], fill=rgba(ROSA), outline=rgba(ROSA_OSC))
            d.polygon([(5, 0), (6, 3), (4, 3)], fill=rgba(ROSA), outline=rgba(ROSA_OSC))
        d.line([(4, 1), (4, 6)], fill=rgba(OSCURO))
        res.append(im)
    return res


def globos():
    res = {}
    for n, f in (('esmalte', g.icono_esmalte), ('labial', g.icono_labial), ('tijeras', g.icono_tijeras)):
        im = Image.new('RGBA', (25, 26), (0, 0, 0, 0))
        g.globo_pedido(im, 12, 25, f())
        res[n] = recortar(im)
    return res


def ventanas():
    enc = Image.new('RGBA', (4, 5), rgba(DORADO))
    ImageDraw.Draw(enc).line([(0, 0), (0, 1)], fill=rgba(CREMA))
    enc.putpixel((3, 4), rgba(DORADO_OSC))
    apa = Image.new('RGBA', (4, 5), rgba((0xE3, 0x8F, 0xB6)))
    apa.putpixel((0, 0), rgba(ROSA_PASTEL))
    return enc, apa


def casas():
    """Repite el barrio de menu_fondo (misma semilla): las ventanas (4x5) y la punta de cada techo."""
    rnd = random.Random(7)
    x, res = 0, []
    while x < g.W:
        w = rnd.randint(28, 46)
        h = rnd.randint(18, 34)
        res.append(dict(x=x, w=w, h=h, ventanas=[(wx, 210 - h + 8) for wx in range(x + 5, x + w - 5, 9)],
                        techo=(x + w // 2, 205 - h - 12)))
        x += w + rnd.randint(2, 10)
    return res


# ============================================================ acomodo
def acomodar(sprites):
    """Lista de piezas fijas (de atrás hacia adelante) y los lugares de lo animado, en el Canvas."""
    piezas = []

    def poner(nombre, sprite, x, y, escala):
        im = sprites[sprite]
        piezas.append(dict(nombre=nombre, sprite=sprite, x=int(x), y=int(y), escala=escala,
                           w=im.width * escala, h=im.height * escala))
        return piezas[-1]

    poner('Postes', 'postes', -960, 540, ESC_FONDO)
    # cerezos delante de las casas (base en la orilla de la banqueta)
    copas = []
    for nombre, sprite, px in (('CerezoIzq', 'cerezo', 74), ('CerezoDer', 'cerezo_der', 334)):
        cx, cy = canvas(px, 222 - sprites[sprite].height + 2)
        p = poner(nombre, sprite, cx, cy, ESC_FONDO)
        copas.append(dict(x=p['x'] + 16, y=p['y'] - 8, w=p['w'] - 32, h=48 * ESC_FONDO))
    # vehículos en fila, sobre la banqueta, con los huecos iguales
    fila = []
    for id_, izq in MOTOS_FILA:
        sprite = 'camioneta' if id_ is None else f'moto_{id_}' + ('_izq' if izq else '')
        fila.append((('Camioneta' if id_ is None else 'Moto_' + id_), sprite))
    total = sum(sprites[s].width * ESC_VEHICULO for _, s in fila)
    hueco = (2 * LIBRE - total) // (len(fila) + 1)
    x = -LIBRE + hueco
    vehiculos = []
    for nombre, sprite in fila:
        im = sprites[sprite]
        alto = im.height * ESC_VEHICULO
        # la camioneta trae 2 filas de sombra bajo las llantas; las motos, 2 de la suya
        p = poner(nombre, sprite, x, PISO_VEHICULOS + alto - 2 * ESC_VEHICULO, ESC_VEHICULO)
        vehiculos.append(p)
        x += im.width * ESC_VEHICULO + hueco
    van = next(p for p in vehiculos if p['nombre'] == 'Camioneta')
    faro_x, faro_y = FARO
    faro_pos = dict(x=van['x'] + (faro_x - 3) * ESC_VEHICULO, y=van['y'] - (faro_y - 2) * ESC_VEHICULO)
    # personajes (la celda de 56x80 a x7; los cuadros los cambia PresentacionViva)
    ancho = CEL_W * ESC_PERSONAJE
    poner('Repartidora', 'repartidora_idle', X_PERSONAJES - ancho // 2, PIE_PERSONAJES + CEL_H * ESC_PERSONAJE,
          ESC_PERSONAJE)
    repartidor = dict(x=-X_PERSONAJES - ancho // 2, y=PIE_PERSONAJES + CEL_H * ESC_PERSONAJE, escala=ESC_PERSONAJE)
    # ventanas: las del barrio que se ven (entre los arbustos, sin cerezos, vehículos ni personajes delante)
    tapan = [p for p in piezas if p['nombre'] != 'Postes']
    lugares, techos = [], []
    for casa in casas():
        for wx, wy in casa['ventanas']:
            x, y = canvas(wx, wy)
            if visible(x, y, 16, 20, tapan) and 150 <= wx <= 326:
                lugares.append(dict(x=x, y=y))
        tx, ty = canvas(*casa['techo'])
        # el globo sube desde la punta del techo: lo de arriba del techo tiene que estar libre
        if 150 <= casa['techo'][0] <= 330 and visible(tx - 48, ty + 110, 96, 110, tapan):
            techos.append(dict(x=tx, y=ty))
    return dict(piezas=piezas, repartidor=repartidor, ventanas=lugares, techos=techos, copas=copas, faro=faro_pos,
                letrero=dict(x=-960, y=540, escala=ESC_FONDO), continuar=Y_CONTINUAR)


def visible(x, y, w, h, piezas):
    """True si el rectángulo (esquina de arriba a la izquierda, en el Canvas) no choca con ninguna pieza (por su caja)."""
    return not any(x < p['x'] + p['w'] and x + w > p['x'] and y > p['y'] - p['h'] and y - h < p['y'] for p in piezas)


FARO = (0, 0)   # se llena con la posición del faro en camioneta.png


def main():
    global FARO
    os.makedirs(DESTINO, exist_ok=True)
    sprites = {}
    for k in range(3):
        sprites[f'letrero_{k}'] = letrero(k - 1)
    sprites['postes'] = postes()
    sprites.update(personajes())
    for id_, _ in MOTOS_FILA:
        if id_:
            sprites[f'moto_{id_}'] = moto(id_)
            sprites[f'moto_{id_}_izq'] = ImageOps.mirror(sprites[f'moto_{id_}'])
    van = camioneta()
    px = van.load()
    amarillos = [(x, y) for y in range(van.height) for x in range(van.width) if px[x, y][3] == 255 and px[x, y][:3] == DORADO
                 and x > van.width * 0.8]
    FARO = (max(x for x, _ in amarillos), min(y for _, y in amarillos))
    sprites['camioneta'] = van
    sprites['faro'] = faro()
    sprites['cerezo'] = cerezo()
    sprites['cerezo_der'] = ImageOps.mirror(sprites['cerezo'])
    for k, im in enumerate(petalos()):
        sprites[f'petalo_{k}'] = im
    for k, im in enumerate(brillos()):
        sprites[f'brillo_{k}'] = im
    for k, im in enumerate(nubes()):
        sprites[f'nube_{k}'] = im
    for k, im in enumerate(mariposa()):
        sprites[f'mariposa_{k}'] = im
    for n, im in globos().items():
        sprites[f'globo_{n}'] = im
    sprites['ventana_encendida'], sprites['ventana_apagada'] = ventanas()

    acomodo = acomodar(sprites)
    acomodo['tamanos'] = [dict(sprite=n, w=im.width, h=im.height) for n, im in sorted(sprites.items())]
    verificar(sprites, acomodo)
    for n, im in sprites.items():
        im.save(os.path.join(DESTINO, n + '.png'))
    with open(os.path.join(DESTINO, 'presentacion.json'), 'w', encoding='utf-8') as f:
        json.dump(acomodo, f, indent=1, ensure_ascii=False)
    vista_previa(sprites, acomodo).save(os.path.join(AQUI, 'presentacion_vista_previa.png'))
    print('ok', len(sprites), 'sprites;', len(acomodo['ventanas']), 'ventanas;', len(acomodo['techos']), 'techos')


def verificar(sprites, acomodo):
    # las piezas dibujadas aquí, sin alfa a medias (las sombras de personajes, motos, camioneta y cerezo sí lo llevan)
    for n, im in sprites.items():
        if n.split('_')[0] in ('letrero', 'postes', 'petalo', 'brillo', 'mariposa', 'globo', 'ventana', 'nube'):
            assert set(im.getchannel('A').getdata()) <= {0, 255}, f'{n}: alfa a medias (se vería borroso)'
    for chica in ('repartidor', 'repartidora'):
        for n in ('idle', 'respira', 'parpadeo', 'saludo_0', 'saludo_1', 'saludo_2'):
            assert sprites[f'{chica}_{n}'].size == (CEL_W, CEL_H), (chica, n)
    assert len(acomodo['ventanas']) >= 6, f"solo {len(acomodo['ventanas'])} ventanas a la vista"
    assert len(acomodo['techos']) >= 3, f"solo {len(acomodo['techos'])} techos para los globos"
    # nada encima de "HAZ CLIC PARA CONTINUAR" (1400x80 al centro) ni de la tabla del título
    for p in acomodo['piezas']:
        if p['nombre'] != 'Postes':     # ocupa la pantalla, pero solo tiene los dos postes de las orillas
            assert visible(-460, Y_CONTINUAR + 40, 920, 80, [p]), f"{p['nombre']} tapa HAZ CLIC"
    pv = [p for p in acomodo['piezas'] if p['nombre'] in ('Repartidora',)] + [dict(acomodo['repartidor'], w=CEL_W * 7, h=CEL_H * 7)]
    for p in pv:
        assert p['y'] - 8 * ESC_PERSONAJE < (g.H // 2 - TABLA[3]) * ESC_FONDO, 'un personaje toca el letrero'


def vista_previa(sprites, acomodo, t=0):
    lienzo = Image.open(os.path.join(g.DESTINO, 'menu_fondo.png')).convert('RGBA').resize((1920, 1080), Image.NEAREST)

    def pegar(sprite, x, y, escala):
        im = sprites[sprite]
        lienzo.alpha_composite(im.resize((im.width * escala, im.height * escala), Image.NEAREST), (x + 960, 540 - y))

    for k, n in enumerate(('nube_0', 'nube_1', 'nube_2')):
        pegar(n, -700 + k * 560, 160 - k * 30, ESC_FONDO)
    for v in acomodo['ventanas'][::2]:
        pegar('ventana_encendida', v['x'], v['y'], ESC_FONDO)
    for p in acomodo['piezas']:
        pegar(p['sprite'], p['x'], p['y'], p['escala'])
    r = acomodo['repartidor']
    pegar('repartidor_saludo_0', r['x'], r['y'], r['escala'])
    pegar('letrero_1', -960, 540, ESC_FONDO)
    t0 = acomodo['techos'][0]
    pegar('globo_labial', t0['x'] - 48, t0['y'] + 120, ESC_FONDO)
    pegar('faro', acomodo['faro']['x'], acomodo['faro']['y'], ESC_VEHICULO)
    rnd = random.Random(3)
    for k in range(14):
        pegar(f'petalo_{k % 4}', rnd.randint(-900, 880), rnd.randint(-500, 500), ESC_FONDO)
    for c in acomodo['copas']:
        pegar('brillo_1', c['x'] + c['w'] // 2, c['y'] - 40, ESC_FONDO)
    pegar('mariposa_0', 300, -120, ESC_FONDO)
    d = ImageDraw.Draw(lienzo)
    # dónde van el título y "HAZ CLIC" (los pone Unity)
    d.rectangle((960 - 704, 540 - 374, 960 + 704, 540 - 270), outline=(0, 160, 255, 255))
    d.rectangle((960 - 460, 540 - Y_CONTINUAR - 20, 960 + 460, 540 - Y_CONTINUAR + 20), outline=(0, 160, 255, 255))
    d.rectangle((960 - 266, 540 - 252, 960 + 266, 540 - 224), outline=(0, 160, 255, 255))
    return lienzo


if __name__ == '__main__':
    main()
