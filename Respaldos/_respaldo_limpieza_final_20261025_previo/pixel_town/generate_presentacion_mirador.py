"""Arte y acomodo de la presentación "EL MIRADOR" del menú principal (octubre 2026; reemplaza a la presentación viva
del letrero colgante, que se queda en Assets/Sprits/Presentacion sin usarse).

Dos jóvenes de espaldas en una colina al atardecer miran la ciudad: él con la mano de visera en la frente, ella señala
el letrero de neón "SALÓN BRENN'S" que se enciende entre las luces. Lienzo de 480x270 a x4 (1920x1080), por capas para
animar (PresentacionMirador.cs), todo en Assets/Sprits/Mirador:
  - cielo.png: degradado #2A1F40 → #8C52FF → #FF6FA5 → #FF9A5C → #FFD166 en bandas con una franja de tramado entre
    cada una, y estrellas arriba (fuera del lugar del título). estrella_0..1: las que titilan.
  - sol.png: sol grande con franjas, medio oculto tras la ciudad (baja un poco en 40 s).
  - nube_0..2.png: nubes violetas con el canto rosa (parallax lento).
  - ciudad_lejos.png y ciudad_cerca.png (488 de ancho: 4 px de más por lado para el parallax): siluetas violeta oscuro
    de casitas, comercios y edificios con ventanas doradas; ventana_encendida/apagada: las 10 que parpadean.
  - letrero_apagado/encendido.png y halo.png: el neón rosa "SALÓN BRENN'S" con tijeras y moño dorados, sobre un
    edificio de la capa de cerca; se enciende con 2 parpadeos a los 1.5 s.
  - colina.png: la colina verde oscuro violáceo con pastos y florecitas.
  - repartidor_visera_0..2, repartidora_espera_0..2, repartidora_medio, repartidora_senala_0..2 (56x80): de espaldas,
    salen de la fila de espalda de las hojas de Assets/Sprits/Personajes (siguen cualquier cambio de ropa). Él con la
    mano de visera; ella espera con los brazos abajo y, cuando se enciende el letrero, levanta el brazo y lo señala.
    Los 3 cuadros de cada pose son el viento: 1 px en la gorra, el cabello y la falda.
  - luciernaga_{oro,verde}_{0,1}.png, destello.png (el que recorre el título), adorno_tijeras/adorno_mono.png (a los
    lados del título), franja.png (bajo "HAZ CLIC PARA CONTINUAR") y caja_menu.png (9-slice de la caja crema del
    PanelMenu: borde rosa y contorno #2A1F40).
  - mirador.json: cada pieza (sprite, padre y esquina de arriba a la izquierda respecto al centro de su padre, en
    unidades del Canvas, y hacia arriba) y los datos de la animación y del menú. Lo lee ConstruirMenuPrincipal.
  - intro/mirador_vista_previa.png y intro/mirador_vista_menu.png: la composición quieta a 1920x1080 (con el letrero
    encendido) y con la caja del menú, para revisar.
Uso: python generate_presentacion_mirador.py   (idempotente: siempre escribe lo mismo)
"""
import json
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, 'intro'))
import generar_intro as g  # noqa: E402
from generar_intro import (BLANCO, CREMA, DORADO, DORADO_OSC, LAVANDA, MORADO, MORADO_OSC, OSCURO, ROSA,  # noqa: E402
                           ROSA_CLARO, ROSA_OSC, ROSA_PASTEL, rgba)

DESTINO = os.path.join(g.SPRITS, 'Mirador')
PERSONAJES = os.path.join(g.SPRITS, 'Personajes')
W, H = 480, 270
ESC = 4
MARGEN = 4                         # px de más a cada lado de las capas de ciudad (su vaivén de parallax)
NARANJA = (0xFF, 0x9A, 0x5C)

# ---- acomodo (px del lienzo de 480x270, y hacia abajo) ----
CEL_W, CEL_H = 56, 80
X_CHICO, X_CHICA = 40, 112         # columna izquierda de la celda de cada personaje
PIE = 237                          # fila de la sombra de los pies (la fila 76 de la celda)
TOP_PERSONAJES = PIE - 76
CRESTA = 233                       # la cresta de la colina bajo los personajes
LETRERO_CY = 112                   # centro del neón (en la línea del dedo de la repartidora)
TITULO_Y = 400                     # centro del título en el Canvas
SUBTITULO_Y = 318
CONTINUAR_Y = -500                 # "HAZ CLIC PARA CONTINUAR"
FRANJA_Y = 248                     # fila donde empieza la franja oscura
CAJA = dict(x=568, y=-125, w=760, h=690)   # caja del PanelMenu (centro y tamaño en el Canvas)
N_VENTANAS, N_LUCIERNAGAS, N_ESTRELLAS = 10, 16, 4


def canvas(px, py):
    """Píxel del lienzo (arriba a la izquierda) → Canvas (centro, y hacia arriba)."""
    return (px - W // 2) * ESC, (H // 2 - py) * ESC


def mezcla(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def contorno(im, color=OSCURO):
    """Contorno de 1 px alrededor de lo opaco (en el lienzo del mismo tamaño)."""
    px = im.load()
    out = im.copy()
    po = out.load()
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y][3]:
                continue
            if any(0 <= x + dx < im.width and 0 <= y + dy < im.height and px[x + dx, y + dy][3] == 255
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                po[x, y] = rgba(color)
    return out


# ============================================================ cielo, sol y nubes
PARADAS = [(0, OSCURO), (62, MORADO), (110, ROSA), (142, NARANJA), (172, DORADO)]
PASOS = 4                          # tonos entre cada par de colores
ZONA_TITULO = (52, 12, 428, 68)    # sin estrellas detrás del título (x0, y0, x1, y1)


def color_cielo(x, y):
    if y >= PARADAS[-1][0]:
        return DORADO
    for (y0, c0), (y1, c1) in zip(PARADAS, PARADAS[1:]):
        if y0 <= y < y1:
            v = (y - y0) / (y1 - y0) * PASOS
            lo = int(v)
            f = v - lo
            a, b = mezcla(c0, c1, lo / PASOS), mezcla(c0, c1, min(PASOS, lo + 1) / PASOS)
            if f < 0.4:
                return a
            if f > 0.6:
                return b
            return b if (x + y) % 2 == 0 else a      # la franja de tramado entre dos bandas


def cielo(estrellas_animadas):
    im = Image.new('RGBA', (W, H))
    px = im.load()
    for y in range(H):
        for x in range(W):
            px[x, y] = rgba(color_cielo(x, y))
    rnd = random.Random(5)
    d = ImageDraw.Draw(im)
    puestas = 0
    while puestas < 46:
        x, y = rnd.randint(3, W - 4), rnd.randint(3, 72)
        if ZONA_TITULO[0] <= x <= ZONA_TITULO[2] and ZONA_TITULO[1] <= y <= ZONA_TITULO[3]:
            continue
        if any(abs(x - ex) < 6 and abs(y - ey) < 6 for ex, ey in estrellas_animadas):
            continue
        brillo = CREMA if y < 40 else ROSA_PASTEL
        if rnd.random() < 0.15:
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                d.point((x + dx, y + dy), fill=rgba(brillo if (dx, dy) == (0, 0) else LAVANDA))
        else:
            d.point((x, y), fill=rgba(brillo if rnd.random() < 0.6 else LAVANDA))
        puestas += 1
    return im


def estrellas():
    grande = Image.new('RGBA', (5, 5), (0, 0, 0, 0))
    d = ImageDraw.Draw(grande)
    d.line([(2, 0), (2, 4)], fill=rgba(LAVANDA))
    d.line([(0, 2), (4, 2)], fill=rgba(LAVANDA))
    d.line([(2, 1), (2, 3)], fill=rgba(CREMA))
    d.line([(1, 2), (3, 2)], fill=rgba(CREMA))
    d.point((2, 2), fill=rgba(BLANCO))
    chica = Image.new('RGBA', (5, 5), (0, 0, 0, 0))
    chica.putpixel((2, 2), rgba(CREMA))
    return grande, chica


def sol():
    r = 28
    im = Image.new('RGBA', (2 * r + 1, 2 * r + 1), (0, 0, 0, 0))
    px = im.load()
    franjas = {6, 7, 12, 13, 14, 19, 20, 21, 22}            # las franjas del sol retro, más gruesas hacia abajo
    for y in range(2 * r + 1):
        for x in range(2 * r + 1):
            if (x - r) ** 2 + (y - r) ** 2 > r * r + r * 0.8 or (y - r) in franjas:
                continue
            t = y / (2 * r)
            c = CREMA if t < 0.22 else (0xFF, 0xE6, 0x9A) if t < 0.42 else DORADO if t < 0.62 else NARANJA
            px[x, y] = rgba(c)
    return im


NUBE_CANTO, NUBE_CUERPO, NUBE_ARRIBA = ROSA_OSC, MORADO_OSC, (0x48, 0x2C, 0x8A)


def nube_violeta(ancho, semilla):
    rnd = random.Random(semilla)
    alto = 16
    forma = Image.new('L', (ancho, alto), 0)
    d = ImageDraw.Draw(forma)
    d.rounded_rectangle((0, alto - 6, ancho - 1, alto - 2), radius=2, fill=255)
    x = 4
    while x < ancho - 8:
        rx, ry = rnd.randint(5, 9), rnd.randint(3, 6)
        d.ellipse((x, alto - 3 - 2 * ry, x + 2 * rx, alto - 3), fill=255)
        x += rx + rnd.randint(2, 5)
    im = Image.new('RGBA', (ancho, alto), (0, 0, 0, 0))
    canto = Image.new('RGBA', (ancho, alto), rgba(NUBE_CANTO))
    cuerpo = Image.new('RGBA', (ancho, alto), rgba(NUBE_CUERPO))
    arriba = Image.new('RGBA', (ancho, alto), rgba(NUBE_ARRIBA))
    sube = forma.transform(forma.size, Image.AFFINE, (1, 0, 0, 0, 1, 1))            # la forma 1 px más arriba
    baja = forma.transform(forma.size, Image.AFFINE, (1, 0, 0, 0, 1, -2))           # 2 px más abajo
    im.paste(canto, (0, 0), forma)                                                  # el canto de abajo, rosa
    im.paste(cuerpo, (0, 0), sube)
    im.paste(arriba, (0, 0), ImageChops.subtract(sube, baja))                       # la orilla de arriba, más oscura
    return im.crop(im.getbbox())


# ============================================================ ciudad
LEJOS, LEJOS_LUZ = (0x4A, 0x33, 0x78), (0x5C, 0x40, 0x8E)
CERCA, CERCA_LUZ = (0x2E, 0x21, 0x48), (0x45, 0x30, 0x68)
VENTANA_APAGADA = (0x3E, 0x2D, 0x5E)
VENTANA_LEJOS = (0xC9, 0x8A, 0x4A)
TOLDO = (0x8E, 0x3A, 0x6E)


def ciudad_lejos():
    w = W + 2 * MARGEN
    im = Image.new('RGBA', (w, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rnd = random.Random(21)
    x = 0
    while x < w:
        bw = rnd.randint(10, 26)
        top = rnd.randint(150, 168) if x < 200 else rnd.randint(130, 160)
        tipo = rnd.choice(('plano', 'plano', 'torre', 'casa', 'escalon'))
        d.rectangle((x, top, x + bw - 1, H - 1), fill=rgba(LEJOS))
        if tipo == 'casa':
            d.polygon([(x - 1, top), (x + bw // 2, top - bw // 3), (x + bw, top)], fill=rgba(LEJOS))
        elif tipo == 'torre':
            d.rectangle((x + bw // 3, top - 8, x + bw - bw // 3, top), fill=rgba(LEJOS))
            d.line([(x + bw // 2, top - 14), (x + bw // 2, top - 8)], fill=rgba(LEJOS))
            d.point((x + bw // 2, top - 15), fill=rgba(ROSA))
        elif tipo == 'escalon':
            d.rectangle((x + 2, top - 4, x + bw - 3, top), fill=rgba(LEJOS))
        for wy in range(top + 3, H, 4):
            for wx in range(x + 2, x + bw - 2, 3):
                if rnd.random() < 0.22:
                    d.point((wx, wy), fill=rgba(VENTANA_LEJOS if rnd.random() < 0.7 else DORADO))
        x += bw + rnd.randint(-3, 1)
    # el canto de arriba de las siluetas, con la luz del atardecer
    px = im.load()
    for xx in range(w):
        for y in range(H):
            if px[xx, y][3]:
                if px[xx, y][:3] == LEJOS:
                    px[xx, y] = rgba(LEJOS_LUZ)
                break
    return im


def letrero_caja():
    """Rectángulo del neón en el lienzo (x0, y0, x1, y1)."""
    ancho, alto = 104, 19
    cx = LETRERO_CX
    return cx - ancho // 2, LETRERO_CY - alto // 2, cx - ancho // 2 + ancho - 1, LETRERO_CY - alto // 2 + alto - 1


def ciudad_cerca():
    """La capa de cerca y las ventanas que pueden parpadear (esquina de arriba a la izquierda en el lienzo)."""
    w = W + 2 * MARGEN
    im = Image.new('RGBA', (w, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rnd = random.Random(33)
    ventanas = []
    lx0, ly0, lx1, ly1 = letrero_caja()

    def edificio(x, bw, top, tipo):
        d.rectangle((x, top, x + bw - 1, H - 1), fill=rgba(CERCA))
        if tipo == 'casa':
            d.polygon([(x - 2, top), (x + bw // 2, top - bw // 2 + 1), (x + bw + 1, top)], fill=rgba(CERCA))
        elif tipo == 'comercio':
            for i in range(x + 1, x + bw - 1):          # el toldo a rayas
                d.point((i, top + 5), fill=rgba(TOLDO if (i // 2) % 2 else CERCA_LUZ))
                d.point((i, top + 6), fill=rgba(TOLDO if (i // 2) % 2 else CERCA_LUZ))
        elif tipo == 'torre':
            d.rectangle((x + 3, top - 6, x + bw - 4, top), fill=rgba(CERCA))
            d.line([(x + bw // 2, top - 12), (x + bw // 2, top - 6)], fill=rgba(CERCA))
            d.point((x + bw // 2, top - 13), fill=rgba(ROSA))
        ventana_y0 = top + (9 if tipo == 'comercio' else 4)
        for wy in range(ventana_y0, H - 3, 6):
            for wx in range(x + 3, x + bw - 4, 5):
                encendida = rnd.random() < 0.38
                d.rectangle((wx, wy, wx + 1, wy + 2), fill=rgba(DORADO if encendida else VENTANA_APAGADA))
                if encendida:
                    d.point((wx, wy), fill=rgba(CREMA))
                ventanas.append((wx - MARGEN, wy))      # en el lienzo de 480

    x = 0
    while x < w:
        bw = rnd.randint(16, 30)
        cx = x - MARGEN + bw // 2
        if cx < 200:
            top, tipos = rnd.randint(190, 202), ('casa', 'casa', 'comercio')
        else:
            top, tipos = rnd.randint(160, 186), ('plano', 'torre', 'comercio', 'casa', 'plano')
        if not (x - MARGEN + bw < lx0 + 8 or x - MARGEN > lx1 - 8):
            top = max(top, ly1 + 10)                    # nada tapa el letrero
        edificio(x, bw, top, rnd.choice(tipos))
        x += bw + rnd.randint(-2, 2)
    # el edificio del letrero y sus dos patas
    bx0, bx1 = lx0 + 12 + MARGEN, lx1 - 12 + MARGEN
    btop = ly1 + 7
    d.rectangle((bx0, btop, bx1, H - 1), fill=rgba(CERCA))
    ventanas[:] = [(vx, vy) for vx, vy in ventanas if not (bx0 - MARGEN - 2 <= vx <= bx1 - MARGEN + 2 and vy >= btop - 2)]
    for wy in range(btop + 4, H - 3, 6):
        for wx in range(bx0 + 3, bx1 - 3, 5):
            encendida = rnd.random() < 0.4
            d.rectangle((wx, wy, wx + 1, wy + 2), fill=rgba(DORADO if encendida else VENTANA_APAGADA))
            if encendida:
                d.point((wx, wy), fill=rgba(CREMA))
            ventanas.append((wx - MARGEN, wy))
    for pata in (lx0 + 20 + MARGEN, lx1 - 21 + MARGEN):
        d.rectangle((pata, ly1 + 1, pata + 1, btop), fill=rgba(CERCA_LUZ))
    px = im.load()
    for xx in range(w):
        for y in range(H):
            if px[xx, y][3]:
                if px[xx, y][:3] == CERCA:
                    px[xx, y] = rgba(CERCA_LUZ)
                break
    return im, ventanas


def ventanas_sprites():
    enc = Image.new('RGBA', (2, 3), rgba(DORADO))
    enc.putpixel((0, 0), rgba(CREMA))
    apa = Image.new('RGBA', (2, 3), rgba(VENTANA_APAGADA))
    return enc, apa


# ============================================================ el neón
FUENTE = {
    'S': [".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###."],
    'A': [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    'L': ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    'O': [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    'N': ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    'B': ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    'R': ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    'E': ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "'": ["#", "#"],
}
TEXTO_NEON = "SALÓN BRENN'S"
NEON_ENC = dict(tubo=ROSA, nucleo=ROSA_PASTEL, letra=(0xFF, 0xE4, 0xF0), oro=DORADO, oro_osc=DORADO_OSC)
NEON_APA = dict(tubo=(0x6B, 0x33, 0x5C), nucleo=(0x7A, 0x3E, 0x68), letra=(0x8A, 0x4A, 0x74), oro=(0x7A, 0x66, 0x48),
                oro_osc=(0x5E, 0x4C, 0x3A))
TABLERO = (0x1C, 0x14, 0x2E)


def ancho_texto(s):
    return sum((3 if c == ' ' else len(FUENTE['O' if c == 'Ó' else c][0]) + 1) for c in s) - 1


def tijeras(d, x, y, c):
    """Tijeras de 9x9 (puntas arriba, argollas abajo)."""
    d.line([(x + 1, y), (x + 6, y + 5)], fill=rgba(c['oro']))
    d.line([(x + 7, y), (x + 2, y + 5)], fill=rgba(c['oro']))
    d.point((x + 4, y + 3), fill=rgba(c['oro_osc']))
    d.ellipse((x, y + 5, x + 3, y + 8), outline=rgba(c['oro']))
    d.ellipse((x + 5, y + 5, x + 8, y + 8), outline=rgba(c['oro']))


def moño(d, cx, cy, c):
    """Moño de 9x7 con el nudo al centro y dos colitas."""
    d.polygon([(cx - 4, cy - 2), (cx, cy), (cx - 4, cy + 2)], fill=rgba(c['oro']), outline=rgba(c['oro_osc']))
    d.polygon([(cx + 4, cy - 2), (cx, cy), (cx + 4, cy + 2)], fill=rgba(c['oro']), outline=rgba(c['oro_osc']))
    d.point((cx, cy), fill=rgba(c['oro_osc']))
    d.line([(cx - 1, cy + 1), (cx - 2, cy + 3)], fill=rgba(c['oro']))
    d.line([(cx + 1, cy + 1), (cx + 2, cy + 3)], fill=rgba(c['oro']))


def letrero(encendido):
    c = NEON_ENC if encendido else NEON_APA
    x0, y0, x1, y1 = letrero_caja()
    w, h = x1 - x0 + 1, y1 - y0 + 1
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, w - 1, h - 1), fill=rgba(OSCURO))
    d.rectangle((1, 1, w - 2, h - 2), fill=rgba(TABLERO))
    d.rectangle((2, 2, w - 3, h - 3), outline=rgba(c['tubo']))           # el tubo del borde
    for x in range(4, w - 4, 6):
        d.point((x, 2), fill=rgba(c['nucleo']))
    # tijeras a la izquierda, moño a la derecha, el texto al centro
    tijeras(d, 5, 5, c)
    moño(d, w - 10, 9, c)
    tx = (w - ancho_texto(TEXTO_NEON)) // 2
    ty = 7
    for ch in TEXTO_NEON:
        if ch == ' ':
            tx += 3
            continue
        glifo = FUENTE['O' if ch == 'Ó' else ch]
        for j, fila in enumerate(glifo):
            for i, v in enumerate(fila):
                if v == '#':
                    d.point((tx + i, ty + j), fill=rgba(c['letra'] if (i + j) % 3 else c['tubo']))
        if ch == 'Ó':
            d.point((tx + 3, ty - 3), fill=rgba(c['letra']))
            d.point((tx + 2, ty - 2), fill=rgba(c['letra']))
        tx += len(glifo[0]) + 1
    return im


def halo():
    """El resplandor rosa del neón encendido: 2 tonos de alfa alrededor del tablero."""
    x0, y0, x1, y1 = letrero_caja()
    w, h = x1 - x0 + 1 + 12, y1 - y0 + 1 + 12
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, w - 1, h - 1), radius=6, fill=rgba(ROSA, 56))
    d.rounded_rectangle((3, 3, w - 4, h - 4), radius=4, fill=rgba(ROSA_CLARO, 96))
    return im


# ============================================================ colina
COLINA_CIMA, COLINA_BASE = (0x5C, 0x7C, 0x66), (0x35, 0x4E, 0x4E)
COLINA_SOMBRA, COLINA_FONDO = (0x2A, 0x3A, 0x48), (0x22, 0x26, 0x40)
PASTO = (0x4A, 0x6A, 0x5A)


def cresta(x):
    if x < 215:
        return CRESTA + round(1.2 * math.sin(x / 19.0))
    return CRESTA + round(((x - 215) / 265) ** 1.4 * 22 + 1.2 * math.sin(x / 19.0))


def colina():
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for x in range(W):
        c0 = cresta(x)
        for y in range(c0, H):
            p = y - c0
            if p == 0:
                c = COLINA_CIMA
            elif p < 6:
                c = COLINA_BASE
            elif p < 9:
                c = COLINA_SOMBRA if (x + y) % 2 else COLINA_BASE
            elif p < 18:
                c = COLINA_SOMBRA
            elif p < 21:
                c = COLINA_FONDO if (x + y) % 2 else COLINA_SOMBRA
            else:
                c = COLINA_FONDO
            px[x, y] = rgba(c)
    rnd = random.Random(9)
    d = ImageDraw.Draw(im)
    for _ in range(150):                            # pastos que asoman sobre la cresta
        x = rnd.randint(0, W - 1)
        alto = rnd.choice((1, 1, 2, 2, 3))
        d.line([(x, cresta(x) - alto), (x, cresta(x))], fill=rgba(PASTO if alto > 1 else COLINA_CIMA))
    for _ in range(70):                             # florecitas
        x = rnd.randint(2, W - 3)
        y = cresta(x) + rnd.randint(2, 13)
        if y >= FRANJA_Y - 1:
            continue
        color = rnd.choice((ROSA, ROSA_CLARO, CREMA, LAVANDA))
        if rnd.random() < 0.3:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                d.point((x + dx, y + dy), fill=rgba(color))
            d.point((x, y), fill=rgba(DORADO))
        else:
            d.point((x, y), fill=rgba(color))
    return im


# ============================================================ personajes de espaldas
CONTORNO = (0x2A, 0x1F, 0x40)
CONTORNOS = {CONTORNO, (0x34, 0x10, 0x74), (0x18, 0x06, 0x4E), (0x0D, 0x02, 0x20), (0x1F, 0x0A, 0x5F), (0x90, 0x48, 0x2B)}
FALDA = {(0x74, 0x42, 0xD6), (0x45, 0x1F, 0x8C), (0x34, 0x10, 0x74)}
MANGA = {False: ((0x7E, 0x4F, 0xCB), (0x65, 0x3A, 0xB1), (0x18, 0x06, 0x4E)),       # base, sombra, contorno
         True: ((0xC9, 0xB7, 0xF0), (0x96, 0x80, 0xCC), CONTORNO)}
PIEL, PIEL_SOMBRA, PIEL_LINEA = (0xFF, 0xDC, 0xC0), (0xF8, 0xC2, 0xA8), (0x90, 0x48, 0x2B)
GORRA_LINEA = (0x74, 0x04, 0x26)
C_BASE = (0x41, 0x20, 0x1A)
F_BASE = (0x74, 0x42, 0xD6)
HOMBRO = (33, 36)


def espalda(chica):
    hoja = Image.open(os.path.join(PERSONAJES, ('repartidora' if chica else 'repartidor') + '_sheet.png')).convert('RGBA')
    return hoja.crop((0, CEL_H, CEL_W, 2 * CEL_H))


def quitar_brazo(im, chica):
    """Borra el brazo derecho (a la derecha de la imagen) que cuelga, sin tocar la mochila ni la falda, y cierra el
    contorno del costado."""
    out = im.copy()
    px = out.load()
    for y in range(41, 54):
        x0 = 35 if y < 50 else 34
        for x in range(x0, CEL_W):
            if px[x, y][3] and not (chica and y >= 51 and px[x, y][:3] in FALDA):
                px[x, y] = (0, 0, 0, 0)
        r = max((x for x in range(CEL_W) if px[x, y][3]), default=None)
        if r is not None and px[r, y][:3] not in CONTORNOS:
            px[r + 1, y] = rgba(MANGA[chica][2] if y < 50 else CONTORNO)
    return out


def mano(capa, x, y, señala):
    """Puño de 3x3 visto por detrás (piel con su contorno); señala: con el índice estirado hacia arriba a la derecha."""
    d = ImageDraw.Draw(capa)
    d.rectangle((x - 2, y - 2, x + 2, y + 2), fill=rgba(PIEL_LINEA))
    d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=rgba(PIEL))
    d.point((x + 1, y + 1), fill=rgba(PIEL_SOMBRA))
    if señala:
        for k in (1, 2, 3):
            d.point((x + 1 + k, y - 1 - k), fill=rgba(PIEL))
            d.point((x + 2 + k, y - 1 - k), fill=rgba(PIEL_LINEA))
            d.point((x + 1 + k, y - 2 - k), fill=rgba(PIEL_LINEA))
        d.point((x + 5, y - 5), fill=rgba(PIEL_LINEA))


def brazo(im, chica, puntos, señala=False, mano_visible=True):
    """El brazo levantado (detrás del cuerpo: la mochila, el cabello y la cabeza le quedan encima)."""
    base, sombra, linea = MANGA[chica]
    capa = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    d.line(puntos, fill=rgba(linea), width=5, joint='curve')
    d.line(puntos, fill=rgba(sombra), width=3, joint='curve')
    d.line(puntos, fill=rgba(base), width=1)
    if mano_visible:
        mano(capa, puntos[-1][0], puntos[-1][1], señala)
    capa.alpha_composite(im)
    return capa


def visera(base_im):
    """Él: la mano derecha de visera en la frente (el codo hacia fuera; la mano asoma junto a la gorra)."""
    im = brazo(quitar_brazo(base_im, False), False, [HOMBRO, (42, 32), (37, 24)], mano_visible=False)
    capa = Image.new('RGBA', im.size, (0, 0, 0, 0))
    mano(capa, 36, 22, False)
    capa.alpha_composite(im)
    return capa


SEÑALA = [HOMBRO, (45, 23)]           # el brazo estirado (la punta del dedo queda en (51, 17))
MEDIO = [HOMBRO, (41, 35), (44, 30)]


def señala(base_im):
    return brazo(quitar_brazo(base_im, True), True, SEÑALA, señala=True)


def medio(base_im):
    return brazo(quitar_brazo(base_im, True), True, MEDIO)


def viento(im, k, chica):
    """Cuadro k (0, 1, 2) del viento que sopla hacia la izquierda: 1 px en la tirita de la gorra, en las puntas del
    cabello y en el vuelo de la falda (solo se agrega, nunca deja huecos)."""
    if k == 0:
        return im
    out = im.copy()
    px = out.load()

    def alargar(y, color):
        izq = next((x for x in range(CEL_W) if px[x, y][3]), None)
        if izq is None or izq == 0:
            return
        px[izq - 1, y] = px[izq, y]
        px[izq, y] = rgba(color)

    # la tirita de atrás de la gorra
    for x, y in ((13, 18),) if k == 1 else ((13, 17), (12, 17)):
        px[x, y] = rgba(GORRA_LINEA)
    if chica:
        for y in ((26, 27, 28) if k == 1 else (28, 29, 30)):
            alargar(y, C_BASE)
        for y in ((55, 56, 57) if k == 1 else (56, 57)):
            alargar(y, F_BASE)
        izq = next(x for x in range(CEL_W) if px[x, 58][3])
        px[izq - 1, 58] = rgba(CONTORNO)
    return out


def personajes():
    res = {}
    chico, chica = espalda(False), espalda(True)
    pose_visera, pose_senala, pose_medio = visera(chico), señala(chica), medio(chica)
    for k in range(3):
        res[f'repartidor_visera_{k}'] = viento(pose_visera, k, False)
        res[f'repartidora_espera_{k}'] = viento(chica, k, True)
        res[f'repartidora_senala_{k}'] = viento(pose_senala, k, True)
    res['repartidora_medio'] = viento(pose_medio, 0, True)
    return res


# punta del dedo (en la celda) → el letrero se pone en esa línea
DEDO = (SEÑALA[-1][0] + 5, SEÑALA[-1][1] - 5)
_dx, _dy = DEDO[0] - HOMBRO[0], DEDO[1] - HOMBRO[1]
_hombro_lienzo = (X_CHICA + HOMBRO[0], TOP_PERSONAJES + HOMBRO[1])
LETRERO_CX = round(_hombro_lienzo[0] + _dx * (LETRERO_CY - _hombro_lienzo[1]) / _dy)


# ============================================================ detalles
def luciernagas():
    res = {}
    for nombre, nucleo, brillo in (('oro', CREMA, DORADO), ('verde', (0xEC, 0xFF, 0xB8), (0xA8, 0xE0, 0x5A))):
        grande = Image.new('RGBA', (3, 3), (0, 0, 0, 0))
        for dx, dy in ((1, 0), (0, 1), (2, 1), (1, 2)):
            grande.putpixel((dx, dy), rgba(brillo))
        grande.putpixel((1, 1), rgba(nucleo))
        chica = Image.new('RGBA', (3, 3), (0, 0, 0, 0))
        chica.putpixel((1, 1), rgba(brillo))
        res[f'luciernaga_{nombre}_0'], res[f'luciernaga_{nombre}_1'] = grande, chica
    return res


def destello():
    im = Image.new('RGBA', (9, 9), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([(4, 0), (4, 8)], fill=rgba(CREMA))
    d.line([(0, 4), (8, 4)], fill=rgba(CREMA))
    d.line([(3, 3), (5, 5)], fill=rgba(BLANCO))
    d.line([(5, 3), (3, 5)], fill=rgba(BLANCO))
    d.line([(4, 2), (4, 6)], fill=rgba(BLANCO))
    d.line([(2, 4), (6, 4)], fill=rgba(BLANCO))
    return im


def adornos():
    c = dict(oro=DORADO, oro_osc=DORADO_OSC)
    t = Image.new('RGBA', (13, 13), (0, 0, 0, 0))
    tijeras(ImageDraw.Draw(t), 2, 2, c)
    m = Image.new('RGBA', (13, 11), (0, 0, 0, 0))
    moño(ImageDraw.Draw(m), 6, 4, c)
    return contorno(t), contorno(m)


def franja():
    im = Image.new('RGBA', (W, H - FRANJA_Y), rgba(OSCURO))
    px = im.load()
    for x in range(W):
        if x % 2:
            px[x, 0] = (0, 0, 0, 0)
        if x % 4 == 0:
            px[x, 0] = (0, 0, 0, 0)
    for x in range(W):
        if (x + 1) % 2 == 0 and x % 4 == 1:
            px[x, 1] = (0, 0, 0, 0)
    return im


def caja_menu():
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, 15, 15), radius=2, fill=rgba(OSCURO))
    d.rounded_rectangle((1, 1, 14, 14), radius=1, fill=rgba(ROSA))
    d.line([(3, 1), (12, 1)], fill=rgba(ROSA_CLARO))
    d.rectangle((3, 3, 12, 12), fill=rgba(OSCURO))
    d.rectangle((4, 4, 11, 11), fill=rgba(CREMA))
    return im


BORDE_CAJA = 5   # 9-slice: px fijos por lado


# ============================================================ acomodo
def acomodar(sprites, ventanas_cerca):
    piezas = []

    def poner(nombre, sprite, x, y, padre='Fondo', centro=(0, 0)):
        cx, cy = canvas(x, y)
        piezas.append(dict(nombre=nombre, sprite=sprite, x=cx - centro[0], y=cy - centro[1], padre=padre,
                           w=sprites[sprite].width * ESC, h=sprites[sprite].height * ESC))
        return piezas[-1]

    rnd = random.Random(17)
    poner('Cielo', 'cielo', 0, 0)
    puntos_estrella = ESTRELLAS_ANIMADAS
    for k, (x, y) in enumerate(puntos_estrella):
        poner(f'Estrella{k}', 'estrella_0', x - 2, y - 2)
    poner('Sol', 'sol', SOL[0] - 28, SOL[1] - 28)
    for k, (x, y) in enumerate(((30, 84), (205, 66), (330, 92))):
        poner(f'Nube{k}', f'nube_{k}', x, y)
    poner('CiudadLejos', 'ciudad_lejos', -MARGEN, 0)
    poner('CiudadCerca', 'ciudad_cerca', -MARGEN, 0)
    # las ventanas que parpadean (hijas de la capa de cerca: su centro es el del Canvas), repartidas y a la vista
    lx0, ly0, lx1, ly1 = letrero_caja()
    libres = [(x, y) for x, y in ventanas_cerca if visible_lienzo(x, y, 2, 3)
              and not (lx0 - 8 <= x <= lx1 + 8 and y <= ly1 + 8)]
    libres.sort()
    elegidas = [libres[i * len(libres) // N_VENTANAS + len(libres) // (2 * N_VENTANAS)] for i in range(N_VENTANAS)]
    for k, (x, y) in enumerate(elegidas):
        poner(f'Ventana{k}', 'ventana_apagada', x, y, 'Fondo/CiudadCerca')
    poner('HaloNeon', 'halo', lx0 - 6, ly0 - 6, 'Fondo/CiudadCerca')
    poner('LetreroNeon', 'letrero_apagado', lx0, ly0, 'Fondo/CiudadCerca')
    poner('Colina', 'colina', 0, 0)
    poner('Repartidor', 'repartidor_visera_0', X_CHICO, TOP_PERSONAJES)
    poner('Repartidora', 'repartidora_espera_0', X_CHICA, TOP_PERSONAJES)
    for k in range(N_LUCIERNAGAS):
        x, y = LUCIERNAGAS_ZONA[0] + rnd.randint(0, LUCIERNAGAS_ZONA[2]), LUCIERNAGAS_ZONA[1] + rnd.randint(0, LUCIERNAGAS_ZONA[3])
        poner(f'Luciernaga{k}', 'luciernaga_oro_0' if k % 3 else 'luciernaga_verde_0', x, y)
    # el título: adornos y destello en TituloGrupo (centro en (0, TITULO_Y))
    centro_titulo = (0, TITULO_Y)
    ty = H // 2 - TITULO_Y // ESC
    poner('AdornoTijeras', 'adorno_tijeras', 240 - 196 - 13, ty - 7, 'TituloGrupo', centro_titulo)
    poner('AdornoMono', 'adorno_mono', 240 + 196, ty - 6, 'TituloGrupo', centro_titulo)
    poner('Destello', 'destello', 240 - 176 - 4, ty - 6, 'TituloGrupo', centro_titulo)
    poner('Franja', 'franja', 0, FRANJA_Y, 'Presentacion')
    lz = LUCIERNAGAS_ZONA
    zx, zy = canvas(lz[0], lz[1])
    return dict(
        escala=ESC, piezas=piezas,
        luciernagas=dict(x=zx, y=zy, w=lz[2] * ESC, h=lz[3] * ESC),
        sol=dict(bajada=13 * ESC, duracion=40),
        destello=dict(desde=-176 * ESC - 16, hasta=176 * ESC - 16),
        titulo=TITULO_Y, subtitulo=SUBTITULO_Y, continuar=CONTINUAR_Y,
        caja=CAJA, borde_caja=BORDE_CAJA,
        ventanas=[dict(x=p['x'], y=p['y']) for p in piezas if p['nombre'].startswith('Ventana')],
    )


SOL = (404, 158)
LUCIERNAGAS_ZONA = (8, 168, 300, 70)       # x, y, ancho, alto (en el lienzo)
ESTRELLAS_ANIMADAS = [(24, 22), (447, 16), (36, 58), (452, 62)]


def visible_lienzo(x, y, w, h):
    """True si el rectángulo no queda detrás de la colina, de un personaje ni de la franja."""
    if y + h >= FRANJA_Y or any(y + h >= cresta(i) - 1 for i in range(x, x + w)):
        return False
    for cx in (X_CHICO, X_CHICA):
        if x + w > cx + 6 and x < cx + CEL_W - 6 and y + h > TOP_PERSONAJES + 12:
            return False
    return True


# ============================================================ principal
def main():
    os.makedirs(DESTINO, exist_ok=True)
    sprites = {'cielo': cielo(ESTRELLAS_ANIMADAS)}
    sprites['estrella_0'], sprites['estrella_1'] = estrellas()
    sprites['sol'] = sol()
    for k, (ancho, semilla) in enumerate(((84, 1), (64, 2), (96, 3))):
        sprites[f'nube_{k}'] = nube_violeta(ancho, semilla)
    sprites['ciudad_lejos'] = ciudad_lejos()
    sprites['ciudad_cerca'], ventanas_cerca = ciudad_cerca()
    sprites['ventana_encendida'], sprites['ventana_apagada'] = ventanas_sprites()
    sprites['letrero_apagado'], sprites['letrero_encendido'] = letrero(False), letrero(True)
    sprites['halo'] = halo()
    sprites['colina'] = colina()
    sprites.update(personajes())
    sprites.update(luciernagas())
    sprites['destello'] = destello()
    sprites['adorno_tijeras'], sprites['adorno_mono'] = adornos()
    sprites['franja'] = franja()
    sprites['caja_menu'] = caja_menu()

    acomodo = acomodar(sprites, ventanas_cerca)
    acomodo['tamanos'] = [dict(sprite=n, w=im.width, h=im.height) for n, im in sorted(sprites.items())]
    verificar(sprites, acomodo)
    for n, im in sprites.items():
        im.save(os.path.join(DESTINO, n + '.png'))
    with open(os.path.join(DESTINO, 'mirador.json'), 'w', encoding='utf-8') as f:
        json.dump(acomodo, f, indent=1, ensure_ascii=False)
    previa = vista_previa(sprites, acomodo)
    previa.save(os.path.join(AQUI, 'intro', 'mirador_vista_previa.png'))
    vista_menu(sprites, acomodo).save(os.path.join(AQUI, 'intro', 'mirador_vista_menu.png'))
    print('ok', len(sprites), 'sprites;', len(acomodo['ventanas']), 'ventanas; letrero en x', LETRERO_CX)


def caja_canvas(p):
    return p['x'], p['y'] - p['h'], p['x'] + p['w'], p['y']     # x0, y0 (abajo), x1, y1 (arriba)


def choca(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def verificar(sprites, acomodo):
    # pixel art limpio: sin alfa a medias (salvo el halo del neón, que es un resplandor de 2 tonos, y la sombra de los
    # pies que traen las hojas de los personajes)
    for n, im in sprites.items():
        if n != 'halo' and not n.startswith('repartidor'):
            alfas = {a for a, k in enumerate(im.getchannel('A').histogram()) if k}
            assert alfas <= {0, 255}, f'{n}: alfa a medias'
    for n in ('repartidor_visera', 'repartidora_espera', 'repartidora_senala'):
        for k in range(3):
            assert sprites[f'{n}_{k}'].size == (CEL_W, CEL_H), n
        assert sprites[f'{n}_0'].tobytes() != sprites[f'{n}_1'].tobytes() != sprites[f'{n}_2'].tobytes(), f'{n}: sin viento'
    assert sprites['ciudad_lejos'].size == sprites['ciudad_cerca'].size == (W + 2 * MARGEN, H)
    assert len(acomodo['ventanas']) == N_VENTANAS
    assert 14 <= sum(p['nombre'].startswith('Luciernaga') for p in acomodo['piezas']) <= 20
    por_nombre = {p['nombre']: p for p in acomodo['piezas']}
    caja = (CAJA['x'] - CAJA['w'] // 2, CAJA['y'] - CAJA['h'] // 2, CAJA['x'] + CAJA['w'] // 2, CAJA['y'] + CAJA['h'] // 2)
    # la caja del menú no tapa a los personajes ni al letrero (con su halo), y queda en la mitad derecha
    assert caja[0] >= 0, 'la caja no está en la mitad derecha'
    for n in ('Repartidor', 'Repartidora', 'HaloNeon', 'LetreroNeon'):
        assert not choca(caja, caja_canvas(por_nombre[n])), f'la caja del menú tapa {n}'
    # ni el título ni el subtítulo (1408 de ancho, Press Start 88; el subtítulo 19 letras de 24)
    assert caja[3] < SUBTITULO_Y - 24, 'la caja tapa el subtítulo'
    # "HAZ CLIC" (1400x80 en y CONTINUAR_Y): solo la franja y lo de fondo quedan detrás; nada animado encima
    haz = (-700, CONTINUAR_Y - 40, 700, CONTINUAR_Y + 40)
    for p in acomodo['piezas']:
        if p['padre'] == 'Fondo' and p['nombre'] not in ('Cielo', 'CiudadLejos', 'CiudadCerca', 'Colina', 'Sol'):
            assert not choca(haz, caja_canvas(p)), f"{p['nombre']} queda en HAZ CLIC"
    lz = acomodo['luciernagas']
    assert lz['y'] - lz['h'] - 12 > CONTINUAR_Y + 40, 'las luciérnagas bajan hasta HAZ CLIC'
    # el dedo de la repartidora apunta al letrero
    x0, y0, x1, y1 = letrero_caja()
    ang_dedo = math.atan2(-_dy, _dx)
    ang_letrero = math.atan2(_hombro_lienzo[1] - (y0 + y1) / 2, (x0 + x1) / 2 - _hombro_lienzo[0])
    assert abs(math.degrees(ang_dedo - ang_letrero)) < 6, (math.degrees(ang_dedo), math.degrees(ang_letrero))
    # todas las posiciones caen en la rejilla de x4 (nítido)
    for p in acomodo['piezas']:
        assert p['x'] % ESC == 0 and p['y'] % ESC == 0, p['nombre']


def componer(sprites, acomodo, estado):
    """La escena a 1920x1080 (estado: 'apagado' o 'encendido')."""
    lienzo = Image.new('RGBA', (1920, 1080), (0, 0, 0, 255))
    centros = {'Fondo': (0, 0), 'Fondo/CiudadCerca': (0, 0), 'TituloGrupo': (0, TITULO_Y), 'Presentacion': (0, 0)}
    for p in acomodo['piezas']:
        sprite = p['sprite']
        if estado == 'encendido':
            sprite = {'letrero_apagado': 'letrero_encendido', 'repartidora_espera_0': 'repartidora_senala_1',
                      'repartidor_visera_0': 'repartidor_visera_1'}.get(sprite, sprite)
        elif p['nombre'] == 'HaloNeon':
            continue
        im = sprites[sprite]
        c = centros[p['padre']]
        lienzo.alpha_composite(im.resize((im.width * ESC, im.height * ESC), Image.NEAREST),
                               (960 + c[0] + p['x'], 540 - c[1] - p['y']))
    d = ImageDraw.Draw(lienzo)
    # dónde van el título, el subtítulo y "HAZ CLIC" (los pone Unity)
    d.rectangle((960 - 704, 540 - TITULO_Y - 48, 960 + 704, 540 - TITULO_Y + 48), outline=(0, 160, 255, 255))
    d.rectangle((960 - 228, 540 - SUBTITULO_Y - 14, 960 + 228, 540 - SUBTITULO_Y + 14), outline=(0, 160, 255, 255))
    return lienzo


def vista_previa(sprites, acomodo):
    lienzo = componer(sprites, acomodo, 'encendido')
    ImageDraw.Draw(lienzo).rectangle((960 - 460, 540 - CONTINUAR_Y - 20, 960 + 460, 540 - CONTINUAR_Y + 20),
                                     outline=(0, 160, 255, 255))
    return lienzo


def vista_menu(sprites, acomodo):
    lienzo = componer(sprites, acomodo, 'encendido')
    velo = Image.new('RGBA', lienzo.size, (0, 0, 0, 64))
    lienzo.alpha_composite(velo)
    c = sprites['caja_menu']
    w, h = CAJA['w'] // ESC, CAJA['h'] // ESC
    caja = Image.new('RGBA', (w, h), rgba(CREMA))
    b = BORDE_CAJA
    caja.paste(c.crop((0, 0, b, b)), (0, 0))
    caja.paste(c.crop((16 - b, 0, 16, b)), (w - b, 0))
    caja.paste(c.crop((0, 16 - b, b, 16)), (0, h - b))
    caja.paste(c.crop((16 - b, 16 - b, 16, 16)), (w - b, h - b))
    for x in range(b, w - b):
        caja.paste(c.crop((b, 0, b + 1, b)), (x, 0))
        caja.paste(c.crop((b, 16 - b, b + 1, 16)), (x, h - b))
    for y in range(b, h - b):
        caja.paste(c.crop((0, b, b, b + 1)), (0, y))
        caja.paste(c.crop((16 - b, b, 16, b + 1)), (w - b, y))
    lienzo.alpha_composite(caja.resize((CAJA['w'], CAJA['h']), Image.NEAREST),
                           (960 + CAJA['x'] - CAJA['w'] // 2, 540 - CAJA['y'] - CAJA['h'] // 2))
    return lienzo


if __name__ == '__main__':
    main()
