"""Arte del GAME OVER y del final de la historia de Delivery Brenn's (misma estética que las viñetas de la intro).

Escribe en Assets/Sprits/Intro (480x270, en el juego a x4; lo importante por encima de VISIBLE = 180, abajo va el
degradado oscuro con el titular):
  - despido.png: la dueña detrás del mostrador con las manos en la cabeza, preocupada, y cajas de pedidos
    acumuladas (pantalla "¡TE DESPIDIERON!" de los 3 niveles).
  - final_1.png: la tormenta pasó: el salón al amanecer, charcos en la calle y un arcoíris pequeño.
  - final_2.png: la dueña sonriendo al teléfono y clientas felices con corazoncitos.
  - final_3_estrella[_repartidora].png: FELICITACIÓN: la dueña, detrás del mostrador, entrega el gafete DORADO al
    personaje, que sonríe; las clientas aplauden detrás con globos de corazones; letrero "FELICIDADES" dorado y mucho
    confeti (3 estrellas).
  - final_3_oficial[_repartidora].png: lo mismo con el gafete y el letrero rosas y menos confeti (2 estrellas).
  - final_4[_<moto>][_repartidora].png: el salón al atardecer con luces y el personaje frente a la puerta, a pie
    (sin sufijo) o en su moto equipada (scooter_naranja, clasica_azul, chopper_verde, deportiva_roja).
  - final_amable_1.png: la dueña tranquila con un café frente al mostrador (1 estrella).
  - final_amable_2[_repartidora].png: el salón con el pizarrón "SE BUSCA" y el personaje que lo mira.
_repartidora: la misma con la repartidora (Progreso.Genero). Usa los sprites actuales del proyecto.
Deja final_vista_previa.png aquí al lado. Los .meta (Point, sin compresión) los pone ConstruirQuejas/ConstruirFinal.

Uso: python generar_final.py
"""
import os
import random
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generar_intro as g  # noqa: E402  (paleta, sprites del proyecto y pintura de la intro)
from generar_intro import (W, H, VISIBLE, rgba, OSCURO, ROSA, ROSA_OSC, VINO, ROSA_CLARO, ROSA_PASTEL, MORADO,  # noqa: E402
                           MORADO_OSC, LAVANDA, DORADO, DORADO_OSC, CREMA, CREMA_OSC, BLANCO, GRIS, GRIS_OSC)

AQUI = os.path.dirname(os.path.abspath(__file__))
MOTOS = ('scooter_naranja', 'clasica_azul', 'chopper_verde', 'deportiva_roja')   # el orden de Progreso.Motos

MANGA, MANGA_OSC, MANGA_LINEA = (0xF2, 0x66, 0x99), (0xAC, 0x45, 0x6A), (0x99, 0x23, 0x4E)
PIEL, PIEL_OSC = (0xFF, 0xDC, 0xC0), (0xC5, 0x7E, 0x5E)
BOCA = (0x7A, 0x1F, 0x3D)
GOTA = ((0xBF, 0xE6, 0xFF), (0x8F, 0xC8, 0xF5), (0x5F, 0x9E, 0xE0))


# ============================================================ la dueña
def _duena():
    return Image.open(os.path.join(g.SPRITS, 'Personajes', 'duena_frente_modern.png')).convert('RGBA')


def _antebrazo(d, codo, mano, espejo=False):
    """Antebrazo de 4 px de ancho del codo a la mano (manga rosa con línea oscura por fuera)."""
    (x0, y0), (x1, y1) = codo, mano
    for y in range(y1, y0 + 1):
        x = round(x0 + (x1 - x0) * (y0 - y) / max(1, y0 - y1))
        d.line([(x - 1, y), (x + 2, y)], fill=rgba(MANGA))
        d.point((x + 3 if espejo else x - 2, y), fill=rgba(MANGA_LINEA))
        d.point((x - 2 if espejo else x + 2, y), fill=rgba(MANGA_OSC))


def _mano(d, x, y):
    d.rectangle((x, y, x + 3, y + 3), fill=rgba(PIEL), outline=rgba(PIEL_OSC))


def _gotas(d, puntos):
    for x, y in puntos:
        d.point((x, y), fill=rgba(GOTA[0]))
        d.line([(x - 1, y + 1), (x + 1, y + 1)], fill=rgba(GOTA[1]))
        d.point((x, y + 2), fill=rgba(GOTA[2]))


def _boca_sonrisa(d):
    d.point((24, 28), fill=rgba(BOCA))
    d.line([(25, 29), (27, 29)], fill=rgba(BOCA))
    d.point((28, 28), fill=rgba(BOCA))


def _brazo(d, puntos):
    """Brazo de 4 px (manga rosa, contorno oscuro) por los puntos dados: hombro, codo, muñeca."""
    for ancho, color in ((6, MANGA_LINEA), (4, MANGA)):
        d.line(puntos, fill=rgba(color), width=ancho, joint='curve')


def duena_preocupada():
    """Las dos manos en la cabeza con los codos hacia afuera, cejas arqueadas hacia arriba al centro, boca chica
    ondulada y gotas de sudor: preocupada, no enojada. La mitad de abajo la tapa el mostrador."""
    im = _duena()
    px = im.load()
    for y in range(38, im.height):                           # las mangas que colgaban (el vestido queda)
        for x in list(range(0, 19)) + list(range(33, im.width)):
            px[x, y] = (0, 0, 0, 0)
    d = ImageDraw.Draw(im)
    _brazo(d, [(17, 37), (9, 31), (13, 19)])
    _brazo(d, [(32, 37), (41, 31), (37, 19)])
    _mano(d, 11, 15)
    _mano(d, 35, 15)
    for x, y in ((20, 21), (21, 20), (31, 20), (32, 21)):
        d.point((x, y), fill=rgba(OSCURO))
    for i, x in enumerate(range(24, 29)):                    # boca ondulada
        d.point((x, 29 - i % 2), fill=rgba(BOCA))
    _gotas(d, ((44, 20), (5, 22)))
    return im


def duena_feliz_telefono():
    """Al teléfono, sonriendo (la llamada de las clientas para felicitar)."""
    im = _duena()
    d = ImageDraw.Draw(im)
    d.rectangle((10, 43, 17, 56), fill=(0, 0, 0, 0))
    _antebrazo(d, (13, 43), (15, 31))
    _mano(d, 13, 28)
    d.rectangle((15, 21, 18, 29), fill=rgba(OSCURO))      # teléfono en la oreja
    d.rectangle((16, 22, 17, 27), fill=rgba(MORADO))
    d.point((16, 22), fill=rgba(LAVANDA))
    _boca_sonrisa(d)
    return im


def duena_entrega():
    """Sonriendo, con el brazo derecho estirado hacia el personaje (a la derecha), a la altura del hombro: sostiene el
    gafete. Va detrás del mostrador (la mitad de abajo no se ve)."""
    im = _duena()
    px = im.load()
    for y in range(38, im.height):
        for x in range(33, im.width):
            px[x, y] = (0, 0, 0, 0)
    d = ImageDraw.Draw(im)
    _brazo(d, [(32, 37), (41, 35), (51, 32)])
    _mano(d, 50, 30)
    _boca_sonrisa(d)
    return im


def duena_cafe():
    """Tranquila, con una taza de café en las manos frente al pecho y los ojos entrecerrados."""
    im = _duena()
    d = ImageDraw.Draw(im)
    d.rectangle((10, 43, 17, 56), fill=(0, 0, 0, 0))
    d.rectangle((33, 43, 40, 56), fill=(0, 0, 0, 0))
    for x in range(14, 36):                                 # los dos antebrazos hacia el centro
        if 22 <= x <= 28:
            continue
        d.line([(x, 43), (x, 45)], fill=rgba(MANGA))
        d.point((x, 42), fill=rgba(MANGA_LINEA))
        d.point((x, 46), fill=rgba(MANGA_LINEA))
    _mano(d, 19, 42)
    _mano(d, 29, 42)
    d.rectangle((22, 38, 29, 46), fill=rgba(CREMA), outline=rgba(OSCURO))      # taza
    d.line([(23, 39), (28, 39)], fill=rgba((0x6B, 0x3E, 0x2A)))                  # café
    d.rectangle((23, 42, 28, 43), fill=rgba(ROSA))                                 # franja rosa
    d.arc((28, 40, 32, 44), 270, 90, fill=rgba(OSCURO))                            # asa
    for x, y in ((24, 35), (25, 34), (24, 33), (27, 35), (28, 34), (27, 33)):     # vapor
        d.point((x, y), fill=rgba(BLANCO, 200))
    for x0 in (21, 29):                                     # ojos entrecerrados (tranquila)
        d.rectangle((x0, 22, x0 + 3, 25), fill=rgba(PIEL))
        d.line([(x0, 24), (x0 + 3, 24)], fill=rgba(OSCURO))
    _boca_sonrisa(d)
    return im


# ============================================================ piezas
VERDE_MENTA = (0x7B, 0xD8, 0x8F)
CELESTE = (0x8F, 0xC8, 0xF5)
NARANJA = (0xFF, 0xB8, 0x80)
ROJO = (0xFF, 0x5D, 0x73)


def interior(im, repisas=True):
    """El interior del Salón Brenn's de la intro (vineta_salon): tapiz a rayas, lambrín, piso de cuadros y repisas."""
    d = ImageDraw.Draw(im)
    for x in range(0, W, 12):
        d.rectangle((x, 0, x + 5, 160), fill=rgba(ROSA_PASTEL))
        d.rectangle((x + 6, 0, x + 11, 160), fill=rgba((0xFF, 0xE6, 0xF0)))
    d.rectangle((0, 120, W, 160), fill=rgba(ROSA_CLARO))
    d.line([(0, 120), (W, 120)], fill=rgba(ROSA_OSC))
    for x in range(6, W, 24):
        d.rectangle((x, 126, x + 15, 154), outline=rgba((0xF7, 0x9C, 0xC2)))
    for y in range(160, H, 10):
        for x in range(0, W, 10):
            d.rectangle((x, y, x + 9, y + 9), fill=rgba(CREMA_OSC if ((x // 10) + (y - 160) // 10) % 2 == 0 else CREMA))
    d.line([(0, 160), (W, 160)], fill=rgba(VINO))
    if repisas:
        iconos = (g.icono_esmalte, g.icono_labial, g.icono_tijeras)
        for fila, y in enumerate((40, 78)):
            d.rectangle((14, y, 466, y + 4), fill=rgba(VINO))
            d.line([(14, y), (466, y)], fill=rgba(ROSA))
            for i in range(10):
                ic = iconos[(i + fila) % 3]()
                im.alpha_composite(ic, (26 + fila * 20 + i * 44, y - ic.height))


def mostrador(im, x0, x1, y=150):
    """Cubierta crema y frente rosa con el emblema de tijeras dorado al centro."""
    d = ImageDraw.Draw(im)
    d.rectangle((x0, y, x1, y + 8), fill=rgba(CREMA), outline=rgba(OSCURO))
    d.rectangle((x0 + 6, y + 8, x1 - 6, y + 50), fill=rgba(ROSA), outline=rgba(OSCURO))
    d.line([(x0 + 7, y + 9), (x1 - 7, y + 9)], fill=rgba(ROSA_CLARO))
    for x in range(x0 + 30, x1 - 6, 40):
        d.line([(x, y + 12), (x, y + 48)], fill=rgba(ROSA_OSC))
    cx = (x0 + x1) // 2
    d.rectangle((cx - 12, y + 16, cx + 12, y + 40), fill=rgba(CREMA), outline=rgba(DORADO_OSC))
    ic = g.icono_tijeras()
    im.alpha_composite(ic, (cx - ic.width // 2, y + 28 - ic.height // 2))


def corazon(d, x, y, color=ROSA, escala=1):
    """Corazoncito de 7x6 (x escala) con brillo."""
    filas = (".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X...")
    for j, f in enumerate(filas):
        for i, c in enumerate(f):
            if c == 'X':
                d.rectangle((x + i * escala, y + j * escala, x + (i + 1) * escala - 1, y + (j + 1) * escala - 1), fill=rgba(color))
    d.rectangle((x + escala, y + escala, x + 2 * escala - 1, y + 2 * escala - 1), fill=rgba(BLANCO))


def gafete(dorado):
    """Gafete de 26x34 con su broche: dorado (repartidor/a estrella) o rosa (oficial); tijeras al centro y el nombre
    del salón en una franja."""
    img = Image.new('RGBA', (26, 34), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fondo, borde, luz = (DORADO, DORADO_OSC, (0xFF, 0xEC, 0xB0)) if dorado else (ROSA_CLARO, ROSA_OSC, ROSA_PASTEL)
    d.rectangle((10, 0, 15, 4), fill=rgba(GRIS), outline=rgba(OSCURO))              # broche
    d.rounded_rectangle((0, 3, 25, 33), radius=3, fill=rgba(fondo), outline=rgba(OSCURO))
    d.rounded_rectangle((2, 5, 23, 31), radius=2, outline=rgba(borde))
    d.line([(4, 7), (9, 7)], fill=rgba(luz))
    d.rectangle((10, 7, 15, 8), fill=rgba(OSCURO))                                   # ranura
    d.rectangle((5, 11, 20, 24), fill=rgba(CREMA), outline=rgba(borde))
    ic = g.icono_tijeras()
    img.alpha_composite(ic, (13 - ic.width // 2, 12))
    d.rectangle((3, 26, 22, 29), fill=rgba(MORADO if dorado else ROSA))               # franja
    d.line([(6, 27), (19, 27)], fill=rgba(CREMA))
    if dorado:
        g.destello(d, 21, 9, 2, BLANCO, CREMA)
    return img


def confeti(d, semilla, zona=(0, 0, W, 175), n=110):
    rng = random.Random(semilla)
    colores = (ROSA, DORADO, MORADO, LAVANDA, VERDE_MENTA, CELESTE, ROJO)
    for _ in range(n):
        x, y = rng.randrange(zona[0], zona[2]), rng.randrange(zona[1], zona[3])
        c = rgba(rng.choice(colores))
        if rng.random() < 0.5:
            d.rectangle((x, y, x + 1, y + 2), fill=c)
        else:
            d.rectangle((x, y, x + 2, y + 1), fill=c)


def personaje(chica, fila=0, escala=1):
    return g.repartidor(fila, 0, chica)


def moto_frente(moto, chica):
    """La moto equipada de frente con el personaje encima (cuadro 0 de la fila de frente de su hoja)."""
    nombre = f'{moto}_chica.png' if chica else f'{moto}.png'
    return g.celda(os.path.join('Motos', moto, nombre), 0, 0, 72, 80)


def cielo(im, colores, y1=200):
    g.degradado(im, 0, y1, colores)


# ============================================================ GAME OVER
def despido():
    """La dueña detrás del mostrador con las manos en la cabeza, preocupada, entre cajas de pedidos acumuladas."""
    im = Image.new('RGBA', (W, H))
    interior(im)
    d = ImageDraw.Draw(im)
    pegar_duena = g.pegar(im, duena_preocupada().crop((0, 0, 56, 47)), 240, 152, escala=3)
    del pegar_duena
    mostrador(im, 110, 370)
    # cajas acumuladas: sobre el mostrador a los dos lados y pilas en el piso
    for x, y, e in ((122, 150, 2), (166, 150, 1), (166, 127, 1), (306, 150, 2), (350, 150, 1)):
        im.alpha_composite(g.caja_rosa(e), (x, y - 23 * e))
    for cx, n in ((40, 4), (66, 3), (92, 2), (390, 3), (416, 4), (442, 2)):
        for i in range(n):
            im.alpha_composite(g.caja_rosa(1), (cx - 11 + (i % 2) * 2, 202 - 23 * (i + 1) + 4 * i))
    # notas de pedido pendientes pegadas en la pared
    for i, (x, y) in enumerate(((70, 92), (96, 100), (384, 94), (410, 88), (436, 100))):
        d.rectangle((x, y, x + 14, y + 16), fill=rgba(CREMA), outline=rgba(ROSA_OSC))
        d.line([(x + 3, y + 5), (x + 11, y + 5)], fill=rgba(GRIS_OSC))
        d.line([(x + 3, y + 9), (x + 9, y + 9)], fill=rgba(GRIS_OSC))
        d.point((x + 7, y), fill=rgba(ROJO))
    return im


# ============================================================ final
def final_1():
    """LA TORMENTA PASÓ: el salón al amanecer, charcos en la calle y un arcoíris pequeño."""
    im = Image.new('RGBA', (W, H))
    cielo(im, [LAVANDA, (0xD9, 0xC8, 0xFF), ROSA_PASTEL, (0xFF, 0xE2, 0xB8), (0xFF, 0xEC, 0xB0)])
    d = ImageDraw.Draw(im)
    d.ellipse((380, 112, 452, 184), fill=rgba(DORADO))
    d.ellipse((390, 122, 442, 174), fill=rgba((0xFF, 0xE8, 0x9A)))
    for i, c in enumerate((ROJO, NARANJA, DORADO, VERDE_MENTA, CELESTE, LAVANDA)):   # arcoíris pequeño
        r = 70 - i * 4
        d.arc((70 - r, 120 - r, 70 + r, 120 + r), 180, 360, fill=rgba(c), width=4)
    g.nube(d, 18, 112, 54, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    g.nube(d, 124, 112, 50, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    g.nube(d, 330, 30, 50, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    g.suelo_calle(im, 210, 236)
    g.pegar(im, g.prop('arbol_grande'), 34, 212)
    g.pegar(im, g.prop('arbol_grande_b'), 448, 212)
    g.pegar(im, g.fachada_salon(), 240, 218)
    g.pegar(im, g.prop('farol'), 64, 216)
    g.pegar(im, g.prop('farol'), 416, 216)
    d = ImageDraw.Draw(im)
    # charcos en la banqueta y en la calle, con el cielo reflejado
    for x0, y0, w in ((96, 222, 34), (300, 224, 26), (40, 246, 56), (200, 252, 70), (380, 244, 48)):
        d.ellipse((x0, y0, x0 + w, y0 + 7), fill=rgba((0xB8, 0xC8, 0xF0)), outline=rgba(GRIS_OSC))
        d.line([(x0 + 5, y0 + 2), (x0 + w // 3, y0 + 2)], fill=rgba(BLANCO))
        d.point((x0 + w - 8, y0 + 4), fill=rgba(ROSA_PASTEL))
    # gotas que quedaron brillando en los árboles
    for x, y in ((22, 150), (40, 136), (452, 140), (436, 158), (60, 166)):
        g.destello(d, x, y, 1, CELESTE, BLANCO)
    return im


def final_2():
    """LA DUEÑA TIENE NOTICIAS: la dueña sonriendo al teléfono y clientas felices con corazoncitos."""
    im = Image.new('RGBA', (W, H))
    interior(im)
    g.pegar(im, duena_feliz_telefono().crop((0, 0, 56, 47)), 330, 152, escala=3)
    mostrador(im, 220, 460)
    d = ImageDraw.Draw(im)
    for x, y, e in ((262, 50, 2), (238, 70, 1), (290, 30, 1), (392, 40, 2), (414, 66, 1)):   # del teléfono
        corazon(d, x, y, (ROSA, ROJO, ROSA_CLARO)[(x // 10) % 3], e)
    for i, n in enumerate((2, 5, 7)):                        # clientas felices frente al mostrador
        x = 52 + i * 62
        d.ellipse((x - 18, 205, x + 18, 211), fill=rgba(OSCURO, 70))
        g.pegar(im, g.clienta(n), x, 210, escala=2)
        corazon(d, x - 7, 24 + (i % 2) * 8, (ROSA, ROJO, ROSA_CLARO)[i], 2)
    return im


def personaje_sonriente(chica):
    """El personaje de frente con una sonrisa abierta bajo los ojos (el sprite no tiene boca)."""
    im = personaje(chica).copy()
    d = ImageDraw.Draw(im)
    d.line([(25, 27), (28, 27)], fill=rgba(BOCA))
    d.line([(26, 28), (27, 28)], fill=rgba(BOCA))
    d.point((26, 28), fill=rgba((0xE7, 0x5F, 0x87)))
    return im


def letrero_felicidades(im, dorado, cx=255, y=6):
    """Cartel "FELICIDADES" colgado con dos cordones, con banderines arriba; dorado (3 estrellas) o rosa (2)."""
    d = ImageDraw.Draw(im)
    for i, x in enumerate(range(-4, W, 16)):                             # banderines de lado a lado
        c = (DORADO, ROSA, LAVANDA, VERDE_MENTA, CELESTE)[i % 5] if dorado else (ROSA, ROSA_CLARO, LAVANDA, CREMA)[i % 4]
        d.polygon([(x, 2), (x + 12, 2), (x + 6, 11)], fill=rgba(c), outline=rgba(OSCURO))
    d.line([(0, 2), (W, 2)], fill=rgba(VINO))
    fondo, borde, letra, luz = ((DORADO, DORADO_OSC, VINO, (0xFF, 0xEC, 0xB0)) if dorado
                                else (ROSA, ROSA_OSC, CREMA, ROSA_CLARO))
    x0, x1, y0, y1 = cx - 92, cx + 92, y + 10, y + 36
    d.line([(x0 + 10, 2), (x0 + 10, y0)], fill=rgba(VINO))
    d.line([(x1 - 10, 2), (x1 - 10, y0)], fill=rgba(VINO))
    for lado in (-1, 1):                                                 # puntas del listón
        xb = x0 if lado < 0 else x1
        d.polygon([(xb, y0 + 4), (xb + 14 * lado, y0 + 4), (xb + 14 * lado, y1 + 4), (xb + 8 * lado, y1 - 2), (xb, y1 + 4)],
                  fill=rgba(borde), outline=rgba(OSCURO))
    d.rectangle((x0, y0, x1, y1), fill=rgba(fondo), outline=rgba(OSCURO))
    d.line([(x0 + 2, y0 + 2), (x1 - 2, y0 + 2)], fill=rgba(luz))
    d.line([(x0 + 2, y1 - 2), (x1 - 2, y1 - 2)], fill=rgba(borde))
    g.texto_pixel(d, (cx + 1, y0 + 6), 'FELICIDADES', 16, OSCURO if dorado else VINO, 'mt')
    g.texto_pixel(d, (cx, y0 + 5), 'FELICIDADES', 16, letra, 'mt')
    if dorado:
        for x, yy, r in ((x0 - 4, y0 - 2, 4), (x1 + 4, y0 - 2, 4), (cx - 70, y1 + 6, 3), (cx + 74, y1 + 4, 3)):
            g.destello(d, x, yy, r, DORADO, BLANCO)
    else:
        corazon(d, x0 + 4, y0 + 9, CREMA)
        corazon(d, x1 - 10, y0 + 9, CREMA)


def vitrina(im, x0, x1, y0=142, y1=208):
    """Vitrina de esmaltes al fondo (tapa a las clientas de la cintura para abajo)."""
    d = ImageDraw.Draw(im)
    d.rectangle((x0, y0, x1, y1), fill=rgba(VINO), outline=rgba(OSCURO))
    d.rectangle((x0 + 3, y0 + 5, x1 - 3, y1 - 4), fill=rgba((0xE6, 0xDC, 0xFF)), outline=rgba(ROSA_OSC))
    d.rectangle((x0 - 2, y0, x1 + 2, y0 + 4), fill=rgba(CREMA), outline=rgba(OSCURO))
    d.line([(x0 + 4, y0 + 22), (x1 - 4, y0 + 22)], fill=rgba(ROSA_OSC))
    iconos = (g.icono_esmalte, g.icono_labial, g.icono_esmalte)
    for fila, yb in enumerate((y0 + 21, y1 - 5)):
        for i, x in enumerate(range(x0 + 8, x1 - 10, 18)):
            ic = iconos[(i + fila) % 3]()
            im.alpha_composite(ic, (x + fila * 6, yb - ic.height))
    for x in range(x0 + 10, x1 - 6, 34):                                  # brillos del cristal
        d.line([(x, y0 + 8), (x + 5, y0 + 13)], fill=rgba(BLANCO))


def globo_corazon(im, gx, gy, color, y_atado=170):
    """Globo de corazón (x3) con su cordón hacia abajo; se pinta antes que las clientas, que tapan el cordón."""
    d = ImageDraw.Draw(im)
    d.line([(gx + 10, gy + 18), (gx + 10 + (gx % 5) - 2, y_atado)], fill=rgba(GRIS_OSC))
    corazon(d, gx, gy, color, 3)
    d.line([(gx + 3, gy + 2), (gx + 4, gy + 2)], fill=rgba(BLANCO))


def clienta_aplaudiendo(im, n, cx, y_pie):
    """Clienta (x2) al fondo; la vitrina se pinta después y le tapa los brazos colgados. Devuelve lo que necesita
    manos_aplauso (colores de piel y manga sacados del sprite)."""
    spr = g.clienta(n)
    px = spr.load()
    piel, piel_osc = px[22, 28][:3], px[18, 28][:3]
    manga, manga_osc = px[18, 40][:3], px[16, 40][:3]
    g.pegar(im, spr, cx, y_pie, escala=2)
    return (cx, y_pie, piel, piel_osc, manga, manga_osc)


def manos_aplauso(im, datos, y_vitrina):
    """Antebrazos que salen de la vitrina, manos juntas frente al pecho y rayitas de aplauso."""
    d = ImageDraw.Draw(im)
    cx, y_pie, piel, piel_osc, manga, manga_osc = datos
    hx, hy = cx - 4, y_pie - 160 + 66                                    # frente al pecho (fila 33 del sprite)
    for x_codo, x_mano in ((hx - 13, hx - 3), (hx + 15, hx + 5)):
        d.line([(x_codo, y_vitrina), (x_mano, hy + 4)], fill=rgba(manga_osc), width=5)
        d.line([(x_codo, y_vitrina), (x_mano, hy + 4)], fill=rgba(manga), width=3)
    d.rectangle((hx - 5, hy - 2, hx, hy + 5), fill=rgba(piel), outline=rgba(piel_osc))
    d.rectangle((hx + 1, hy - 2, hx + 6, hy + 5), fill=rgba(piel), outline=rgba(piel_osc))
    for lado in (-1, 1):
        xa = hx + (9 if lado > 0 else -8)
        for dy, dx in ((-6, 3), (-1, 5), (4, 3)):
            d.line([(xa, hy + dy), (xa + dx * lado, hy + dy - (2 if dy < 0 else 0))], fill=rgba(BLANCO))


def final_3(estrella, chica):
    """¡REPARTIDOR/A ESTRELLA! (3 estrellas) o ¡TE QUEDASTE CON EL PUESTO! (2): FELICITACIÓN frente al mostrador. La
    dueña, detrás del mostrador, le entrega el gafete al personaje, que sonríe; detrás, las clientas aplauden con
    globos de corazones; confeti y el letrero "FELICIDADES". En 3 estrellas el gafete y el letrero son dorados, con
    resplandor y más confeti; en 2, rosas y con menos confeti."""
    im = Image.new('RGBA', (W, H))
    interior(im, repisas=False)
    d = ImageDraw.Draw(im)
    for k, y in enumerate((64, 96)):                                     # repisas cortas en la pared de la izquierda
        d.rectangle((8, y, 150, y + 4), fill=rgba(VINO))
        d.line([(8, y), (150, y)], fill=rgba(ROSA))
        for i in range(5):
            ic = (g.icono_esmalte, g.icono_labial, g.icono_tijeras)[(i + k) % 3]()
            im.alpha_composite(ic, (16 + i * 27, y - ic.height))
    letrero_felicidades(im, estrella)
    # las clientas al fondo, detrás de la vitrina, con globos de corazones (atados detrás de ellas)
    for gx, gy, color in ((340, 42, ROJO), (382, 30, ROSA), (424, 40, ROSA_CLARO), (458, 26, ROJO)):
        globo_corazon(im, gx, gy, color)
    clientas = [clienta_aplaudiendo(im, n, x, 220) for n, x in zip((2, 4, 6), (372, 418, 462))]
    vitrina(im, 336, 486)
    for c in clientas:
        manos_aplauso(im, c, 142)
    d = ImageDraw.Draw(im)
    if estrella:                                                          # resplandor dorado detrás del gafete
        for r, a in ((56, 45), (40, 65), (24, 85)):
            d.ellipse((190 - r, 150 - r, 190 + r, 150 + r), fill=rgba((0xFF, 0xE8, 0x9A), a))
    x_duena, y_duena = 110, 160
    g.pegar(im, duena_entrega().crop((0, 0, 56, 47)), x_duena, y_duena, escala=3)
    mostrador(im, 0, 222, y_duena - 2)
    # el gafete cuelga de la mano de la dueña (mano en x 50..53, y 30..33 del sprite), con el cordón corto
    d = ImageDraw.Draw(im)
    mano_x, mano_y = x_duena - 84 + 52 * 3, y_duena - 141 + 33 * 3
    cordon = MORADO if estrella else ROSA_OSC
    d.line([(mano_x - 2, mano_y), (mano_x + 3, mano_y + 8)], fill=rgba(cordon), width=2)
    d.line([(mano_x + 9, mano_y), (mano_x + 6, mano_y + 8)], fill=rgba(cordon), width=2)
    g.pegar(im, gafete(estrella), mano_x + 5, mano_y + 8 + 68, escala=2)
    # el personaje, sonriendo, frente al mostrador
    d.ellipse((240, 252, 330, 262), fill=rgba(OSCURO, 90))
    g.pegar(im, personaje_sonriente(chica), 285, 258, escala=3)
    d = ImageDraw.Draw(im)
    confeti(d, 7 if estrella else 11, zona=(0, 40, W, 178), n=190 if estrella else 80)
    if estrella:
        for x, y, r in ((240, 70, 5), (150, 120, 4), (330, 110, 3), (224, 196, 3), (110, 46, 3)):
            g.destello(d, x, y, r, DORADO, BLANCO)
    else:
        for x, y, r in ((240, 80, 3), (150, 126, 3)):
            g.destello(d, x, y, r, ROSA_CLARO, BLANCO)
    return im


def final_4(moto, chica):
    """GRACIAS POR JUGAR: el salón al atardecer, con guirnalda de luces, y el personaje frente a la puerta, a pie
    (moto=None) o en su moto equipada."""
    im = Image.new('RGBA', (W, H))
    cielo(im, [MORADO_OSC, MORADO, (0xC0, 0x6E, 0xC8), ROSA, NARANJA, DORADO])
    d = ImageDraw.Draw(im)
    d.ellipse((60, 150, 130, 220), fill=rgba(DORADO))       # sol que se mete
    d.ellipse((70, 160, 120, 210), fill=rgba((0xFF, 0xE8, 0x9A)))
    for x, y in ((30, 12), (120, 24), (210, 8), (300, 18), (400, 10), (450, 30), (350, 40)):
        d.point((x, y), fill=rgba(CREMA))
    g.nube(d, 400, 60, 50, (0xFF, 0xD6, 0xE6), ROSA_CLARO, ROSA)
    g.suelo_calle(im, 210, 236)
    g.pegar(im, g.prop('arbol_grande'), 34, 212)
    g.pegar(im, g.prop('arbol_grande_b'), 448, 212)
    fachada = g.fachada_salon()
    tinte = Image.new('RGBA', fachada.size, rgba((0xFF, 0x9E, 0x7A), 60))   # luz cálida del atardecer
    fachada = Image.composite(Image.alpha_composite(fachada, tinte), fachada, fachada.split()[3])
    g.pegar(im, fachada, 240, 218)
    d = ImageDraw.Draw(im)
    for xf in (64, 416):                                     # faroles encendidos
        for r, a in ((26, 40), (16, 70)):
            d.ellipse((xf - r, 120 - r, xf + r, 120 + r), fill=rgba((0xFF, 0xD1, 0x66), a))
    for xf in (64, 416):
        g.pegar(im, g.prop('farol'), xf, 216)
    d = ImageDraw.Draw(im)
    # guirnalda de focos de lado a lado de la fachada
    import math
    focos = (DORADO, ROSA, CELESTE, VERDE_MENTA, CREMA)
    puntos = [(x, 34 + round(10 * math.sin((x - 90) / 300 * math.pi))) for x in range(90, 391, 2)]
    d.line(puntos, fill=rgba(OSCURO))
    for i, (x, y) in enumerate(puntos[::8]):
        d.ellipse((x - 4, y - 1, x + 4, y + 7), fill=rgba(focos[i % len(focos)], 70))
        d.rectangle((x - 1, y + 1, x + 1, y + 4), fill=rgba(focos[i % len(focos)]))
        d.point((x, y + 1), fill=rgba(BLANCO))
    # el personaje frente a la puerta
    d.ellipse((190, 232, 290, 242), fill=rgba(OSCURO, 100))
    if moto is None:
        g.pegar(im, personaje(chica), 240, 238, escala=2)
    else:
        g.pegar(im, moto_frente(moto, chica), 240, 240, escala=2)
    return im


def final_amable_1():
    """ESTA VEZ NO FUE: la dueña tranquila con un café frente al mostrador."""
    im = Image.new('RGBA', (W, H))
    interior(im)
    mostrador(im, 120, 420)
    d = ImageDraw.Draw(im)
    for x, y, e in ((150, 150, 1), (380, 150, 1)):       # unas cajas, ya sin prisa
        im.alpha_composite(g.caja_rosa(e), (x, y - 23 * e))
    # luz de la ventana sobre el piso
    luz = Image.new('RGBA', (W, H))
    ImageDraw.Draw(luz).polygon([(40, 0), (130, 0), (260, 270), (120, 270)], fill=rgba(BLANCO, 40))
    im.alpha_composite(luz)
    d = ImageDraw.Draw(im)
    d.ellipse((190, 244, 290, 254), fill=rgba(OSCURO, 90))
    g.pegar(im, duena_cafe(), 240, 250, escala=3)
    return im


def final_amable_2(chica):
    """¡EL PUESTO SIGUE DISPONIBLE!: el salón de día con el pizarrón "SE BUSCA REPARTIDOR/A" junto a la puerta y el
    personaje que lo mira, con ganas de volver a intentarlo."""
    im = Image.new('RGBA', (W, H))
    cielo(im, [LAVANDA, (0xD9, 0xC8, 0xFF), ROSA_PASTEL, (0xFF, 0xE6, 0xF0)], 190)
    d = ImageDraw.Draw(im)
    g.nube(d, 80, 40, 60, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    g.nube(d, 400, 28, 70, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    g.suelo_calle(im, 210, 236)
    g.pegar(im, g.prop('arbol_grande'), 34, 212)
    g.pegar(im, g.prop('arbol_grande_b'), 448, 212)
    g.pegar(im, g.fachada_salon(), 240, 218)
    d = ImageDraw.Draw(im)
    # pizarrón de pie (caballete) frente a la vitrina derecha
    x0, y0 = 316, 120
    d.line([(x0 + 6, y0 + 50), (x0 + 2, y0 + 96)], fill=rgba(VINO), width=3)
    d.line([(x0 + 80, y0 + 50), (x0 + 84, y0 + 96)], fill=rgba(VINO), width=3)
    d.rectangle((x0, y0, x0 + 86, y0 + 60), fill=rgba(VINO), outline=rgba(OSCURO))
    d.rectangle((x0 + 4, y0 + 4, x0 + 82, y0 + 56), fill=rgba(CREMA), outline=rgba(ROSA))
    g.texto_pixel(d, (x0 + 43, y0 + 9), 'SE BUSCA', 14, ROSA_OSC, 'mt')
    g.texto_pixel(d, (x0 + 43, y0 + 26), 'REPARTIDOR/A', 12, OSCURO, 'mt')
    ic = g.icono_tijeras()
    im.alpha_composite(ic, (x0 + 43 - ic.width // 2, y0 + 41))
    g.destello(d, x0 + 78, y0 + 2, 3, DORADO, CREMA)
    # el personaje, de frente, al lado del pizarrón
    d.ellipse((232, 232, 300, 240), fill=rgba(OSCURO, 90))
    g.pegar(im, personaje(chica), 266, 236, escala=2)
    d = ImageDraw.Draw(im)
    for x, y, r in ((236, 92, 3), (300, 84, 4), (250, 70, 2)):
        g.destello(d, x, y, r, DORADO, BLANCO)
    return im


def todas():
    """{nombre de archivo: imagen}"""
    imgs = {'despido': despido(), 'final_1': final_1(), 'final_2': final_2(), 'final_amable_1': final_amable_1()}
    for chica in (False, True):
        suf = '_repartidora' if chica else ''
        imgs['final_3_estrella' + suf] = final_3(True, chica)
        imgs['final_3_oficial' + suf] = final_3(False, chica)
        imgs['final_4' + suf] = final_4(None, chica)
        for m in MOTOS:
            imgs[f'final_4_{m}{suf}'] = final_4(m, chica)
        imgs['final_amable_2' + suf] = final_amable_2(chica)
    return imgs


def main():
    imgs = todas()
    os.makedirs(g.DESTINO, exist_ok=True)
    for nombre, img in imgs.items():
        assert img.size == (W, H), nombre
        img.convert('RGB').save(os.path.join(g.DESTINO, nombre + '.png'))
    # vista previa: todo junto (a x1), con el corte de VISIBLE marcado
    cols = 4
    filas = (len(imgs) + cols - 1) // cols
    prev = Image.new('RGB', (cols * (W + 8), filas * (H + 8)), OSCURO)
    dp = ImageDraw.Draw(prev)
    for i, (nombre, img) in enumerate(imgs.items()):
        x, y = (i % cols) * (W + 8), (i // cols) * (H + 8)
        prev.paste(img.convert('RGB'), (x, y))
        dp.line([(x, y + VISIBLE), (x + 12, y + VISIBLE)], fill=(255, 0, 0))
    prev.save(os.path.join(AQUI, 'final_vista_previa.png'))
    print('Listo:', len(imgs), 'imágenes en', g.DESTINO)


if __name__ == '__main__':
    main()
