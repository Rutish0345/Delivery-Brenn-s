"""Arte de la intro y del menú principal de Delivery Brenn's.

Escribe en Assets/Sprits/Intro:
  - vineta_1..5.png: ilustraciones de 480x270 (en el juego se ven a x4, 1920x1080), armadas con los
    sprites del proyecto (fachada del Salón Brenn's, casas y props del atlas residencial, clientas,
    repartidor y scooter) más algunos detalles pintados aquí (cielo, nubes, globos de pedido, caja
    rosa, teclas, monedas) con la paleta: rosa #FF6FA5, morado #8C52FF, dorado #FFD166, crema
    #FFF1D0 y el contorno #2A1F40.
  - menu_fondo.png: 480x270, cielo rosa pastel con nubes, arbustos con flores a los lados y una
    franja de encaje abajo.
y en Assets/Sprits/UI:
  - boton_pastel_normal/presionado.png y boton_pastel_dorado_normal/presionado.png: pastillas de
    24x18 tipo "piedra" (crema o dorada, contorno morado), mismo tamaño que boton_pill_normal para que
    usen el mismo 9-slice.
Los .meta (Point, sin compresión, 9-slice) los pone ConstruirIntro / ConstruirMenuPrincipal en Unity.
Deja también intro_vista_previa.png (todo junto) aquí al lado.

Uso: python generar_intro.py
"""
import json
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
PROYECTO = os.environ.get('PROYECTO_UNITY', r'C:\Users\kangu\DeliveryBrenns')
SPRITS = os.path.join(PROYECTO, 'Assets', 'Sprits')
DESTINO = os.path.join(SPRITS, 'Intro')
DESTINO_UI = os.path.join(SPRITS, 'UI')
ATLAS = os.path.join(AQUI, '..', 'unity', 'residencial', 'props_atlas.png')
ATLAS_JSON = os.path.join(AQUI, '..', 'unity', 'residencial', 'props_atlas.json')
FUENTE = os.path.join(PROYECTO, 'Assets', 'Fonts', 'static', 'PixelifySans-SemiBold.ttf')

W, H = 480, 270
VISIBLE = 180         # el tercio de abajo lleva el degradado oscuro con el titular y el texto: lo importante va arriba

# ---- paleta ----
OSCURO = (0x2A, 0x1F, 0x40)
ROSA = (0xFF, 0x6F, 0xA5)
ROSA_OSC = (0xC2, 0x41, 0x7A)
VINO = (0xB0, 0x3A, 0x74)
ROSA_CLARO = (0xFF, 0xB3, 0xD1)
ROSA_PASTEL = (0xFF, 0xD6, 0xE6)
MORADO = (0x8C, 0x52, 0xFF)
MORADO_OSC = (0x5B, 0x34, 0xB0)
LAVANDA = (0xC9, 0xB2, 0xFF)
DORADO = (0xFF, 0xD1, 0x66)
DORADO_OSC = (0xE0, 0x9A, 0x3A)
CREMA = (0xFF, 0xF1, 0xD0)
CREMA_OSC = (0xEE, 0xDA, 0xB0)
BLANCO = (0xFF, 0xFF, 0xFF)
VERDE = (0x6E, 0xA8, 0x6A)
VERDE_OSC = (0x3F, 0x6E, 0x4A)
GRIS = (0xA8, 0x9F, 0xB8)
GRIS_OSC = (0x7E, 0x74, 0x94)
ASFALTO = (0x5A, 0x4E, 0x6E)


def rgba(c, a=255):
    return c + (a,)


# ============================================================ sprites del proyecto
_atlas = Image.open(ATLAS).convert('RGBA')
_props = {s.get('nombre', s.get('name')): s for s in json.load(open(ATLAS_JSON, encoding='utf-8'))['sprites']}


def prop(nombre):
    s = _props[nombre]
    return _atlas.crop((s['x'], s['y'], s['x'] + s['w'], s['y'] + s['h']))


def fachada_salon():
    """La fachada del Salón Brenn's del mapa, pero con una segunda vitrina (espejo y sillones) en lugar de
    la cortina de CARGA de la bodega. Sale del mismo generador que el atlas (props_residencial_extra)."""
    sys.path.insert(0, os.path.join(AQUI, '..'))
    from props_residencial_extra import make_company
    from generate_unity import trim
    return Image.fromarray(trim(make_company(carga=False)))


def celda(ruta, x, y, w, h):
    return Image.open(os.path.join(SPRITS, ruta)).convert('RGBA').crop((x, y, x + w, y + h))


def repartidor(fila=0, col=0):
    # celdas de 54x80 con 2 px de aire (0, 54, 110...); fila 0 frente
    xs = [0, 54, 110, 164, 220]
    return celda(r'Personajes\repartidor_sheet.png', xs[col], fila * 80, 54, 80)


def clienta(n, col=0):
    return celda(rf'Personajes\Clientas\clienta_{n}.png', col * 56, 0, 56, 80)


def scooter_lado():
    return celda(r'Motos\scooter_naranja\scooter_naranja.png', 0, 160, 72, 80)


def icono_moto(nombre):
    return Image.open(os.path.join(SPRITS, 'Motos', nombre, nombre + '_icono.png')).convert('RGBA')


def ui(nombre):
    return Image.open(os.path.join(SPRITS, 'UI', nombre + '.png')).convert('RGBA')


def pegar(lienzo, img, x, y_pie, escala=1, centro=True):
    """Pega con el pie (borde de abajo) en y_pie; x es el centro si centro=True."""
    if escala != 1:
        img = img.resize((img.width * escala, img.height * escala), Image.NEAREST)
    x0 = int(x - img.width // 2) if centro else int(x)
    lienzo.alpha_composite(img, (x0, int(y_pie - img.height)))
    return img


def recortar_alfa(img):
    return img.crop(img.getbbox())


def sombra_pies(d, cx, y, ancho):
    d.ellipse((cx - ancho // 2, y - 2, cx + ancho // 2, y + 2), fill=rgba(OSCURO, 70))


# ============================================================ pintura
def degradado(lienzo, y0, y1, colores):
    """Franjas horizontales con un borde tramado (2 filas en damero), a lo pixel art."""
    d = ImageDraw.Draw(lienzo)
    n = len(colores)
    alto = (y1 - y0) / n
    for i, c in enumerate(colores):
        a, b = int(y0 + i * alto), int(y0 + (i + 1) * alto)
        d.rectangle((0, a, W, b), fill=rgba(c))
        if i + 1 < n:
            for yy in (b - 1, b):
                for xx in range(0, W, 2):
                    d.point((xx + (yy % 2), yy), fill=rgba(colores[i + 1]))


def nube(d, cx, cy, ancho, luz=BLANCO, base=CREMA, sombra=ROSA_PASTEL):
    """Nube de bultos con la panza plana; d es el ImageDraw del lienzo (se compone encima)."""
    lienzo = d._image
    capa = Image.new('RGBA', lienzo.size, (0, 0, 0, 0))
    _nube(ImageDraw.Draw(capa), cx, cy, ancho, luz, base, sombra)
    lienzo.alpha_composite(capa)


def _nube(d, cx, cy, ancho, luz, base, sombra):
    r = ancho // 5
    bultos = [(-2 * r, 0, r), (-r, -r // 2, int(r * 1.2)), (0, -r, int(r * 1.5)), (r, -r // 3, int(r * 1.2)), (2 * r, 0, r)]
    for dx, dy, rr in bultos:
        d.ellipse((cx + dx - rr, cy + dy - rr + 2, cx + dx + rr, cy + dy + rr + 2), fill=rgba(sombra))
    for dx, dy, rr in bultos:
        d.ellipse((cx + dx - rr, cy + dy - rr, cx + dx + rr, cy + dy + rr), fill=rgba(base))
    d.ellipse((cx - r * 2, cy - r - r // 2, cx, cy - r // 3), fill=rgba(luz))
    d.rectangle((cx - 3 * r, cy + 1, cx + 3 * r, cy + r + 3), fill=(0, 0, 0, 0))


def flor(d, x, y, petalo, centro=DORADO, grande=False):
    """Flor de 5 px (o de 9 si es grande) con centro dorado."""
    if grande:
        for dx, dy in ((0, -2), (-2, 0), (2, 0), (0, 2), (-1, -1), (1, -1), (-1, 1), (1, 1)):
            d.rectangle((x + dx - 1, y + dy - 1, x + dx, y + dy), fill=rgba(petalo))
        d.rectangle((x - 1, y - 1, x, y), fill=rgba(centro))
    else:
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            d.point((x + dx, y + dy), fill=rgba(petalo))
        d.point((x, y), fill=rgba(centro))


def arbusto_flores(d, cx, cy, ancho, alto, semilla, colores=(BLANCO, ROSA_CLARO, ROSA)):
    rnd = random.Random(semilla)
    # follaje: manchas de rosa oscuro / vino (como los árboles rosas de la referencia) y verde
    for _ in range(int(ancho * alto / 60)):
        x = cx + rnd.randint(-ancho // 2, ancho // 2)
        y = cy + rnd.randint(-alto // 2, alto // 2)
        if ((x - cx) / (ancho / 2)) ** 2 + ((y - cy) / (alto / 2)) ** 2 > 1:
            continue
        r = rnd.randint(4, 9)
        c = rnd.choice((VINO, ROSA_OSC, ROSA_OSC, VERDE_OSC))
        d.ellipse((x - r, y - r, x + r, y + r), fill=rgba(c))
    for _ in range(int(ancho * alto / 90)):
        x = cx + rnd.randint(-ancho // 2, ancho // 2)
        y = cy + rnd.randint(-alto // 2, alto // 2)
        if ((x - cx) / (ancho / 2)) ** 2 + ((y - cy) / (alto / 2)) ** 2 > 0.85:
            continue
        flor(d, x, y, rnd.choice(colores), grande=rnd.random() < 0.35)


def encaje(d, y0, y1, fondo=ROSA_CLARO, dibujo=CREMA, borde=ROSA):
    """Franja de encaje como la del suelo de la referencia del menú."""
    d.rectangle((0, y0, W, y1), fill=rgba(fondo))
    d.rectangle((0, y0, W, y0 + 1), fill=rgba(borde))
    d.rectangle((0, y0 + 3, W, y0 + 3), fill=rgba(dibujo))
    for x in range(0, W + 12, 12):
        for y in range(y0 + 8, y1, 10):
            o = 6 if ((y - y0) // 10) % 2 else 0
            cx = x + o
            for dx, dy in ((0, -3), (-3, 0), (3, 0), (0, 3), (-1, -1), (1, -1), (-1, 1), (1, 1)):
                d.point((cx + dx, y + dy), fill=rgba(dibujo))
            d.point((cx, y), fill=rgba(borde))


def globo_pedido(lienzo, cx, y_pico, icono):
    """Globo crema con contorno rosa, pico hacia abajo y un ícono adentro (17x15 + pico)."""
    d = ImageDraw.Draw(lienzo)
    w, h = 23, 19
    x0, y0 = cx - w // 2, y_pico - 5 - h
    d.rounded_rectangle((x0, y0, x0 + w - 1, y0 + h - 1), radius=5, fill=rgba(CREMA), outline=rgba(ROSA_OSC))
    d.polygon([(cx - 3, y0 + h - 1), (cx + 3, y0 + h - 1), (cx, y_pico)], fill=rgba(CREMA))
    d.line([(cx - 3, y0 + h - 1), (cx, y_pico), (cx + 3, y0 + h - 1)], fill=rgba(ROSA_OSC))
    d.line([(x0 + 3, y0 + 2), (x0 + 6, y0 + 2)], fill=rgba(BLANCO))
    lienzo.alpha_composite(icono, (cx - icono.width // 2, y0 + (h - icono.height) // 2))


def icono_esmalte():
    """Frasco de esmalte rosa con tapa morada (11x13)."""
    img = Image.new('RGBA', (11, 13), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((3, 0, 7, 4), fill=rgba(MORADO), outline=rgba(OSCURO))      # tapa
    d.line([(4, 1), (4, 3)], fill=rgba(LAVANDA))
    d.rounded_rectangle((1, 4, 9, 12), radius=2, fill=rgba(ROSA), outline=rgba(OSCURO))
    d.rectangle((2, 9, 8, 11), fill=rgba(ROSA_OSC))
    d.line([(3, 6), (3, 8)], fill=rgba(BLANCO))                              # brillo del vidrio
    return img


def icono_labial():
    """Labial: barra rosa en bisel sobre el tubo dorado y morado (9x13)."""
    img = Image.new('RGBA', (9, 13), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(2, 3), (6, 0), (6, 5), (2, 5)], fill=rgba(ROSA), outline=rgba(OSCURO))
    d.line([(3, 3), (3, 4)], fill=rgba(ROSA_CLARO))
    d.rectangle((1, 5, 7, 7), fill=rgba(DORADO), outline=rgba(OSCURO))
    d.rectangle((1, 7, 7, 12), fill=rgba(MORADO), outline=rgba(OSCURO))
    d.line([(2, 8), (2, 11)], fill=rgba(LAVANDA))
    return img


def icono_tijeras():
    """Tijeras abiertas de 11x13 (hojas plateadas arriba, aros rosas abajo)."""
    img = Image.new('RGBA', (11, 13), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(1, 0), (6, 7)], fill=rgba(OSCURO), width=2)
    d.line([(9, 0), (4, 7)], fill=rgba(OSCURO), width=2)
    d.line([(1, 0), (5, 6)], fill=rgba(GRIS))
    d.line([(9, 0), (5, 6)], fill=rgba(GRIS))
    d.point((5, 5), fill=rgba(DORADO))
    d.ellipse((0, 7, 4, 11), outline=rgba(ROSA_OSC))
    d.ellipse((6, 7, 10, 11), outline=rgba(ROSA_OSC))
    return img


def destello(d, x, y, r, color=DORADO, nucleo=CREMA):
    """Destello de 4 puntas como el marcador de entrega: brazos de largo r con el centro crema."""
    d.line([(x - r, y), (x + r, y)], fill=rgba(color))
    d.line([(x, y - r), (x, y + r)], fill=rgba(color))
    if r >= 3:
        d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=rgba(color))
    d.point((x, y), fill=rgba(nucleo))


def caja_rosa(escala=1):
    """Caja de pedido rosa con cinta dorada, moño dorado arriba y etiqueta crema con un destello (22x23)."""
    img = Image.new('RGBA', (22, 23), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y = 5                                                    # la caja empieza bajo el moño
    d.rectangle((0, y + 3, 21, y + 17), fill=rgba(ROSA), outline=rgba(OSCURO))
    d.rectangle((1, y + 13, 20, y + 16), fill=rgba(ROSA_OSC))
    d.rectangle((0, y, 21, y + 4), fill=rgba(ROSA_CLARO), outline=rgba(OSCURO))
    d.rectangle((10, y, 11, y + 17), fill=rgba(DORADO))
    d.line([(10, y + 17), (11, y + 17)], fill=rgba(DORADO_OSC))
    # moño: dos lazos y el nudo
    d.polygon([(10, y + 1), (4, y - 4), (4, y + 2)], fill=rgba(DORADO), outline=rgba(OSCURO))
    d.polygon([(11, y + 1), (17, y - 4), (17, y + 2)], fill=rgba(DORADO), outline=rgba(OSCURO))
    d.point((6, y - 1), fill=rgba(CREMA))
    d.point((15, y - 1), fill=rgba(CREMA))
    d.rectangle((9, y - 1, 12, y + 2), fill=rgba(DORADO_OSC), outline=rgba(OSCURO))
    # etiqueta con destello
    d.rectangle((3, y + 7, 8, y + 12), fill=rgba(CREMA), outline=rgba(ROSA_OSC))
    destello(d, 5, y + 9, 1, ROSA_OSC, DORADO)
    if escala != 1:
        img = img.resize((22 * escala, 23 * escala), Image.NEAREST)
    return img


def moneda(d, cx, cy, r=5):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgba(DORADO_OSC), outline=rgba(OSCURO))
    d.ellipse((cx - r + 1, cy - r + 1, cx + r - 2, cy + r - 2), fill=rgba(DORADO))
    d.line([(cx - r + 3, cy - r + 2), (cx - r + 4, cy - r + 2)], fill=rgba(BLANCO))
    # el signo $
    d.line([(cx, cy - 3), (cx, cy + 3)], fill=rgba(DORADO_OSC))
    d.line([(cx - 1, cy - 2), (cx + 1, cy - 2)], fill=rgba(DORADO_OSC))
    d.line([(cx - 1, cy), (cx + 1, cy)], fill=rgba(DORADO_OSC))
    d.line([(cx - 1, cy + 2), (cx + 1, cy + 2)], fill=rgba(DORADO_OSC))


def pila_monedas(d, cx, y_pie, n):
    for i in range(n):
        y = y_pie - 3 - i * 3
        d.ellipse((cx - 6, y - 3, cx + 6, y + 3), fill=rgba(DORADO_OSC), outline=rgba(OSCURO))
        d.line([(cx - 4, y - 1), (cx + 4, y - 1)], fill=rgba(DORADO))


def texto_pixel(d, xy, s, tam, color, ancla='la'):
    f = ImageFont.truetype(FUENTE, tam)
    d.fontmode = '1'          # sin antialias: píxeles limpios
    d.text(xy, s, font=f, fill=rgba(color), anchor=ancla)


def tecla(lienzo, x, y, letra, ancho=26, alto=26, tam=16):
    d = ImageDraw.Draw(lienzo)
    d.rounded_rectangle((x, y + 3, x + ancho - 1, y + alto + 2), radius=4, fill=rgba(MORADO_OSC))   # canto
    d.rounded_rectangle((x, y, x + ancho - 1, y + alto - 1), radius=4, fill=rgba(CREMA), outline=rgba(OSCURO))
    d.rectangle((x + 3, y + 2, x + ancho - 4, y + 3), fill=rgba(BLANCO))
    texto_pixel(d, (x + ancho // 2, y + alto // 2 + 1), letra, tam, OSCURO, 'mm')


def suelo_calle(lienzo, y_banqueta, y_calle):
    d = ImageDraw.Draw(lienzo)
    d.rectangle((0, y_banqueta, W, y_calle), fill=rgba(GRIS))
    d.line([(0, y_banqueta), (W, y_banqueta)], fill=rgba(BLANCO))
    for x in range(0, W, 24):
        d.line([(x, y_banqueta + 1), (x, y_calle)], fill=rgba(GRIS_OSC))
    d.rectangle((0, y_calle, W, H), fill=rgba(ASFALTO))
    d.line([(0, y_calle), (W, y_calle)], fill=rgba(GRIS_OSC))
    for x in range(8, W, 40):
        d.rectangle((x, y_calle + 14, x + 18, y_calle + 15), fill=rgba(DORADO))


# ============================================================ viñetas
def vineta_1():
    """El Salón Brenn's al amanecer."""
    im = Image.new('RGBA', (W, H))
    degradado(im, 0, 200, [MORADO_OSC, MORADO, LAVANDA, ROSA_CLARO, ROSA_PASTEL, (0xFF, 0xE2, 0xB8), DORADO])
    d = ImageDraw.Draw(im)
    d.ellipse((370, 120, 450, 200), fill=rgba(DORADO))             # sol saliendo detrás de la tienda
    d.ellipse((380, 130, 440, 190), fill=rgba((0xFF, 0xE8, 0x9A)))
    nube(d, 70, 50, 70, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    nube(d, 300, 32, 54, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    nube(d, 430, 70, 46, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    for x, y in ((20, 14), (160, 22), (240, 10), (350, 18), (460, 30)):
        d.point((x, y), fill=rgba(CREMA))
    suelo_calle(im, 210, 236)
    pegar(im, prop('arbol_grande'), 34, 212)
    pegar(im, prop('arbol_grande_b'), 448, 212)
    pegar(im, fachada_salon(), 240, 218)
    pegar(im, prop('farol'), 64, 216)
    pegar(im, prop('farol'), 416, 216)
    d = ImageDraw.Draw(im)
    for x, y, r in ((52, 96, 4), (428, 92, 3), (24, 150, 2), (458, 140, 3), (76, 30, 2)):
        destello(d, x, y, r)
    return im


def vineta_2():
    """Calle residencial con globos de pedido sobre las casas."""
    im = Image.new('RGBA', (W, H))
    degradado(im, 0, 190, [LAVANDA, (0xD9, 0xC8, 0xFF), ROSA_PASTEL, (0xFF, 0xE6, 0xF0)])
    d = ImageDraw.Draw(im)
    nube(d, 90, 30, 60)
    nube(d, 380, 22, 70)
    pegar(im, prop('arbol_mediano'), 10, 195)
    pegar(im, prop('arbol_mediano'), 470, 195)
    suelo_calle(im, 193, 218)
    casas = ['casa_azul', 'casa_crema', 'casa_madera']
    puertas = [(80, icono_esmalte), (240, icono_labial), (400, icono_tijeras)]
    for i, nombre in enumerate(casas):
        pegar(im, prop(nombre), 80 + i * 160, 196)
    # clientas esperando en la banqueta, frente a cada puerta
    for i, (x, _) in enumerate(puertas):
        sombra_pies(d, x + 18, 210, 16)
        pegar(im, clienta((1, 4, 6)[i]), x + 18, 211)
    pegar(im, prop('buzon_teal'), 140, 204)
    pegar(im, prop('buzon_terracota'), 300, 204)
    # globos de pedido sobre los techos
    for i, (x, icono) in enumerate(puertas):
        globo_pedido(im, x + (0, 6, -4)[i], 40 + (0, -6, 4)[i], icono())
    return im


def vineta_3():
    """El repartidor con su caja rosa con moño, frente a la puerta del Salón Brenn's."""
    im = Image.new('RGBA', (W, H))
    # fondo: la fachada del salón al doble, recortada en la puerta, un poco apagada
    fachada = fachada_salon()
    fachada = fachada.resize((fachada.width * 2, fachada.height * 2), Image.NEAREST)
    im.alpha_composite(fachada.crop((90, 120, 90 + W, 120 + H)))
    velo = Image.new('RGBA', (W, H), rgba(MORADO_OSC, 90))
    im.alpha_composite(velo)
    d = ImageDraw.Draw(im)
    # resplandor rosa detrás del personaje
    for r, a in ((110, 60), (80, 80), (55, 90)):
        d.ellipse((240 - r, 150 - r, 240 + r, 150 + r), fill=rgba(ROSA_CLARO, a))
    d.ellipse((168, 234, 312, 248), fill=rgba(OSCURO, 90))
    pegar(im, repartidor(0, 0), 240, 242, escala=3)
    # la caja en los brazos, a la altura del pecho
    im.alpha_composite(caja_rosa(3), (240 - 33, 144 - 15))
    for x, y, r in ((150, 70, 5), (330, 60, 4), (120, 160, 3), (360, 150, 5), (200, 40, 3), (290, 30, 4)):
        destello(d, x, y, r, (DORADO, CREMA, ROSA_CLARO)[(x + y) % 3], BLANCO)
    return im


def vineta_4():
    """Las teclas (W A S D, E, Esc) y la barra de satisfacción con caritas. Todo por encima de VISIBLE (abajo
    van el titular y el texto); las caritas van en la fila de la barra, lejos de las etiquetas."""
    im = Image.new('RGBA', (W, H))
    degradado(im, 0, H, [ROSA_PASTEL, (0xFF, 0xE6, 0xF0), CREMA])
    d = ImageDraw.Draw(im)
    for x in range(0, W, 16):            # cuadrícula suave de fondo
        d.line([(x, 0), (x, H)], fill=rgba(ROSA_CLARO, 90))
    for y in range(0, H, 16):
        d.line([(0, y), (W, y)], fill=rgba(ROSA_CLARO, 90))
    # teclas: W A S D, E y Esc con su etiqueta debajo (y = 22 .. 100)
    bx, by = 52, 22
    tecla(im, bx + 30, by, 'W')
    for i, l in enumerate('ASD'):
        tecla(im, bx + i * 30, by + 32, l)
    texto_pixel(d, (bx + 43, by + 68), 'Moverte', 16, OSCURO, 'mt')
    tecla(im, 222, by + 16, 'E', 34, 34, 20)
    texto_pixel(d, (239, by + 68), 'Entregar', 16, OSCURO, 'mt')
    tecla(im, 352, by + 18, 'Esc', 44, 30, 14)
    texto_pixel(d, (374, by + 68), 'Pausa', 16, OSCURO, 'mt')
    # barra de satisfacción, como el HUD: caritas a la izquierda, la barra y la propina (y = 120 .. 156)
    y0 = 120
    for i, n in enumerate(('carita_1', 'carita_3', 'carita_5')):
        im.alpha_composite(ui(n).resize((36, 36), Image.NEAREST), (44 + i * 40, y0))
    x0, yb = 172, y0 + 6
    pista = ui('medidor_pista').resize((176, 24), Image.NEAREST)
    relleno = ui('medidor_relleno').resize((176, 24), Image.NEAREST)
    im.alpha_composite(pista, (x0, yb))
    lleno = relleno.crop((0, 0, 132, 24))
    verde = Image.new('RGBA', lleno.size, rgba((0x7B, 0xD8, 0x8F)))
    lleno = Image.composite(verde, Image.new('RGBA', lleno.size, (0, 0, 0, 0)), lleno.split()[3])
    im.alpha_composite(lleno, (x0, yb))
    moneda(d, x0 + 196, yb + 12, 9)
    texto_pixel(d, (x0 + 212, yb + 4), '+propina', 16, OSCURO)
    assert yb + 24 < VISIBLE
    return im


def vineta_5():
    """La moto y monedas."""
    im = Image.new('RGBA', (W, H))
    degradado(im, 0, 200, [MORADO, LAVANDA, ROSA_CLARO, ROSA_PASTEL, (0xFF, 0xE2, 0xB8)])
    d = ImageDraw.Draw(im)
    nube(d, 60, 40, 56, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    nube(d, 420, 30, 64, BLANCO, ROSA_PASTEL, ROSA_CLARO)
    suelo_calle(im, 196, 222)
    # líneas de velocidad detrás de la moto
    for y, l in ((134, 60), (154, 90), (174, 70), (112, 40)):
        d.line([(70, y), (70 + l, y)], fill=rgba(BLANCO, 200))
    d.ellipse((170, 206, 310, 218), fill=rgba(OSCURO, 90))
    pegar(im, scooter_lado(), 240, 216, escala=2)
    # las otras motos de la tienda, en exhibición
    for i, m in enumerate(('clasica_azul', 'deportiva_roja', 'chopper_verde')):
        ic = icono_moto(m)
        x = 330 + i * 48
        d.rounded_rectangle((x - 22, 40, x + 22, 84), radius=6, fill=rgba(CREMA), outline=rgba(ROSA_OSC))
        im.alpha_composite(ic.resize((40, 40), Image.NEAREST), (x - 20, 42))
    # monedas
    pila_monedas(d, 60, 200, 6)
    pila_monedas(d, 80, 200, 9)
    pila_monedas(d, 100, 200, 4)
    for x, y in ((140, 100), (160, 70), (120, 60), (190, 110)):
        moneda(d, x, y, 6)
    return im


# ============================================================ menú
def menu_fondo():
    im = Image.new('RGBA', (W, H))
    degradado(im, 0, 220, [LAVANDA, (0xE3, 0xD3, 0xFF), ROSA_PASTEL, (0xFF, 0xE3, 0xEE), (0xFF, 0xEC, 0xF2)])
    d = ImageDraw.Draw(im)
    for cx, cy, w in ((80, 40, 80), (250, 26, 60), (400, 50, 90), (170, 88, 50), (330, 100, 56)):
        nube(d, cx, cy, w)
    # techos lejanos en rosa claro (el barrio a lo lejos)
    rnd = random.Random(7)
    x = 0
    while x < W:
        w = rnd.randint(28, 46)
        h = rnd.randint(18, 34)
        d.rectangle((x, 205 - h, x + w, 210), fill=rgba(ROSA_CLARO))
        d.polygon([(x - 2, 205 - h), (x + w // 2, 205 - h - 12), (x + w + 2, 205 - h)], fill=rgba((0xF7, 0x9C, 0xC2)))
        for wx in range(x + 5, x + w - 5, 9):
            d.rectangle((wx, 210 - h + 8, wx + 3, 210 - h + 12), fill=rgba(CREMA))
        x += w + rnd.randint(2, 10)
    # postes con cable, como en la referencia
    for px in (150, 330):
        d.rectangle((px, 150, px + 2, 212), fill=rgba(VINO))
        d.rectangle((px - 6, 156, px + 8, 157), fill=rgba(VINO))
    d.line([(152, 157), (240, 170), (331, 157)], fill=rgba(VINO))
    # arbustos de flores a los lados
    arbusto_flores(d, 40, 150, 120, 150, 1)
    arbusto_flores(d, 95, 205, 120, 80, 2)
    arbusto_flores(d, 440, 150, 120, 150, 3)
    arbusto_flores(d, 385, 205, 120, 80, 4)
    encaje(d, 222, H)
    return im


def degradado_intro():
    """Velo para leer el texto sobre la viñeta (480x270, a x4 en pantalla): negro suave arriba que se
    desvanece y, en el tercio de abajo, negro al 50 %. En bandas de 6 px, a lo pixel art.
    Unity mezcla en espacio lineal: para que en pantalla se vea oscurecido a (1 - a), el alfa del PNG es
    1 - (1 - a) ** 2.2 (el 50 % queda en 78 %)."""
    im = Image.new('RGBA', (8, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    alfa = lambda a: round((1 - (1 - a) ** 2.2) * 255)
    arriba = [0.22, 0.16, 0.11, 0.06, 0.03]                       # velo de arriba (y = 0 .. 30)
    for k, a in enumerate(arriba):
        d.rectangle((0, k * 6, 7, k * 6 + 5), fill=(0, 0, 0, alfa(a)))
    rampa = [0.08, 0.17, 0.25, 0.33, 0.42]                         # sube de 0 a 50 % (y = 150 .. 180)
    for k, a in enumerate(rampa):
        y = VISIBLE - 30 + k * 6
        d.rectangle((0, y, 7, y + 5), fill=(0, 0, 0, alfa(a)))
    d.rectangle((0, VISIBLE, 7, H - 1), fill=(0, 0, 0, alfa(0.5)))  # el tercio de abajo, a 50 %
    return im


def placa_start():
    """Placa tipo START: el marco del proyecto (marco_panel: borde rosa con remaches en las esquinas) con el
    fondo #2A1F40 en vez de crema; el texto va en crema encima."""
    marco = ui('marco_panel').copy()
    px = marco.load()
    oscuro, oscuro_luz = rgba(OSCURO), rgba((0x3D, 0x2E, 0x5C))
    for y in range(marco.height):
        for x in range(marco.width):
            c = px[x, y]
            if c[3] and c[:3] == CREMA:
                px[x, y] = oscuro
            elif c[3] and c[:3] == CREMA_OSC:
                px[x, y] = oscuro_luz
    return marco


def boton(fill, sombra, luz, presionado):
    """Pastilla de 24x18: contorno morado, brillo arriba y canto abajo (el presionado baja 2 px)."""
    img = Image.new('RGBA', (24, 18), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if presionado:
        d.rounded_rectangle((0, 2, 23, 17), radius=5, fill=rgba(fill), outline=rgba(MORADO_OSC))
        d.line([(4, 4), (19, 4)], fill=rgba(sombra))
    else:
        d.rounded_rectangle((0, 2, 23, 17), radius=5, fill=rgba(MORADO_OSC))
        d.rounded_rectangle((0, 0, 23, 15), radius=5, fill=rgba(fill), outline=rgba(MORADO_OSC))
        d.line([(4, 2), (19, 2)], fill=rgba(luz))
        d.line([(3, 13), (20, 13)], fill=rgba(sombra))
    return img


def main():
    os.makedirs(DESTINO, exist_ok=True)
    vinetas = [vineta_1(), vineta_2(), vineta_3(), vineta_4(), vineta_5()]
    for i, v in enumerate(vinetas, 1):
        v.convert('RGB').save(os.path.join(DESTINO, f'vineta_{i}.png'))
    fondo = menu_fondo()
    fondo.convert('RGB').save(os.path.join(DESTINO, 'menu_fondo.png'))
    botones = {
        'boton_pastel_normal': boton(CREMA, CREMA_OSC, BLANCO, False),
        'boton_pastel_presionado': boton(CREMA, CREMA_OSC, BLANCO, True),
        'boton_pastel_dorado_normal': boton(DORADO, DORADO_OSC, (0xFF, 0xEC, 0xB0), False),
        'boton_pastel_dorado_presionado': boton(DORADO, DORADO_OSC, (0xFF, 0xEC, 0xB0), True),
    }
    for nombre, img in botones.items():
        img.save(os.path.join(DESTINO_UI, nombre + '.png'))
    degradado_intro().save(os.path.join(DESTINO, 'degradado_intro.png'))
    placa_start().save(os.path.join(DESTINO_UI, 'placa_start.png'))

    # vista previa: todo junto a x2
    prev = Image.new('RGB', (W * 2 * 2 + 12, H * 2 * 3 + 24), OSCURO)
    for i, img in enumerate(vinetas + [fondo]):
        prev.paste(img.convert('RGB').resize((W * 2, H * 2), Image.NEAREST), ((i % 2) * (W * 2 + 12), (i // 2) * (H * 2 + 12)))
    prev.save(os.path.join(AQUI, 'intro_vista_previa.png'))
    print('Listo:', DESTINO, '+', ', '.join(botones))


if __name__ == '__main__':
    main()
