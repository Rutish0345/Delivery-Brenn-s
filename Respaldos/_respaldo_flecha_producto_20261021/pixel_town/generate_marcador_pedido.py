"""Arte del marcador del pedido (reemplaza a las estrellas de entrega): la burbuja con el producto encima del
punto de entrega, los 6 íconos de producto y el aro del suelo (dorado y verde "listo para entregar").

Salida: unity/comun/marcador/*.png y una copia en el proyecto (Assets/Sprits/Comun/Marcador), donde
ConstruirMarcadorPedido los importa con Point, sin compresión y PPU 32.
 - burbuja_pedido.png   40x44: globo crema #FFF1D0, borde rosa #FF6FA5 de 2 px, contorno #2A1F40 y colita
                        abajo (como los globos de las clientas en la intro). Pivote en la punta de la colita.
 - producto_<tipo>.png  16x16 con contorno oscuro: esmalte, labial, tijeras, crema, tinte, accesorio.
 - aro_dorado_0..2.png, aro_verde_0..2.png  48x20: elipse semitransparente que pulsa (3 cuadros).
 - asoma_<tipo>.png     16x11: la parte de arriba del ícono, la que asoma por la mochila del repartidor
                        (el resto queda dentro). Pivote abajo-centro.
Uso: python generate_marcador_pedido.py
"""
import json
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(AQUI, 'unity', 'comun', 'marcador')
PROYECTO = os.environ.get('PROYECTO_UNITY', r'C:\Users\kangu\DeliveryBrenns')
DESTINO = os.path.join(PROYECTO, 'Assets', 'Sprits', 'Comun', 'Marcador')

OSCURO = (0x2A, 0x1F, 0x40)
ROSA = (0xFF, 0x6F, 0xA5)
ROSA_OSC = (0xC2, 0x41, 0x7A)
ROSA_CLARO = (0xFF, 0xB3, 0xD1)
VINO = (0xB0, 0x3A, 0x74)
CREMA = (0xFF, 0xF1, 0xD0)
CREMA_OSC = (0xEE, 0xDA, 0xB0)
BLANCO = (0xFF, 0xFF, 0xFF)
DORADO = (0xFF, 0xD1, 0x66)
DORADO_OSC = (0xE0, 0x9A, 0x3A)
MORADO = (0x8C, 0x52, 0xFF)
MORADO_OSC = (0x5B, 0x34, 0xB0)
LAVANDA = (0xC9, 0xB2, 0xFF)
GRIS = (0xA8, 0x9F, 0xB8)
GRIS_CLARO = (0xD8, 0xD2, 0xE4)
VERDE = (0x7B, 0xD8, 0x8F)
VERDE_OSC = (0x4F, 0xA8, 0x66)

ALTO_ASOMA = 11   # filas del ícono que asoman por la mochila
PRODUCTOS = ['esmalte', 'labial', 'tijeras', 'crema', 'tinte', 'accesorio']   # el orden del enum Producto


def c(color, a=255):
    return tuple(color) + (a,)


def contornear(img, color=OSCURO):
    """Agrega 1 px de contorno (vecinos en cruz) alrededor de todo lo que no es transparente."""
    a = np.array(img)
    lleno = a[..., 3] > 0
    borde = ndimage.binary_dilation(lleno, structure=ndimage.generate_binary_structure(2, 1)) & ~lleno
    a[borde] = c(color)
    return Image.fromarray(a)


# ------------------------------------------------------------------ burbuja
def burbuja():
    W, H = 40, 44
    forma = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(forma)
    d.rounded_rectangle((0, 0, W - 1, 35), radius=9, fill=255)
    d.polygon([(13, 33), (26, 33), (20, 43), (19, 43)], fill=255)      # colita, punta de 2 px al centro
    m = np.array(forma) > 0
    cruz = ndimage.generate_binary_structure(2, 1)
    borde = ndimage.binary_erosion(m, cruz)                             # dentro del contorno oscuro
    crema = ndimage.binary_erosion(borde, cruz, iterations=2)           # dentro del borde rosa de 2 px
    a = np.zeros((H, W, 4), np.uint8)
    a[m] = c(OSCURO)
    a[borde] = c(ROSA)
    a[crema] = c(CREMA)
    # sombra crema abajo del cuerpo y brillo arriba a la izquierda
    filas = np.where(crema.any(axis=1))[0]
    abajo = filas[filas <= 32].max()
    a[abajo, :][crema[abajo, :]] = c(CREMA_OSC)
    a[4, 6:11] = c(BLANCO)
    a[5, 5] = c(BLANCO)
    return Image.fromarray(a)


# ------------------------------------------------------------------ íconos 16x16 (dibujo en 1..14)
def icono(dibujar):
    img = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    dibujar(ImageDraw.Draw(img))
    return contornear(img)


def esmalte(d):
    d.rectangle((6, 1, 9, 5), fill=c(MORADO))                 # tapa
    d.line([(7, 2), (7, 4)], fill=c(LAVANDA))
    d.rectangle((5, 6, 10, 6), fill=c(DORADO))                # cuello
    d.rounded_rectangle((3, 7, 12, 14), radius=2, fill=c(ROSA))
    d.rectangle((4, 12, 11, 13), fill=c(ROSA_OSC))            # el esmalte, más oscuro abajo
    d.line([(5, 8), (5, 10)], fill=c(BLANCO))                 # brillo del vidrio


def labial(d):
    d.polygon([(6, 6), (6, 3), (9, 1), (9, 6)], fill=c(ROSA))  # barra en bisel
    d.line([(7, 3), (7, 5)], fill=c(ROSA_CLARO))
    d.rectangle((5, 7, 10, 8), fill=c(DORADO))                # aro dorado
    d.point((6, 7), fill=c(CREMA))
    d.rectangle((5, 9, 10, 14), fill=c(MORADO))               # tubo
    d.line([(6, 10), (6, 13)], fill=c(LAVANDA))


def tijeras(d):
    d.line([(3, 1), (9, 8)], fill=c(GRIS), width=2)           # hojas
    d.line([(12, 1), (6, 8)], fill=c(GRIS), width=2)
    d.line([(3, 1), (8, 7)], fill=c(GRIS_CLARO))
    d.point((7, 6), fill=c(DORADO))                           # tornillo
    d.point((8, 6), fill=c(DORADO))
    d.ellipse((1, 9, 6, 14), fill=c(ROSA))                    # aros
    d.ellipse((9, 9, 14, 14), fill=c(ROSA))
    d.rectangle((3, 11, 4, 12), fill=c(OSCURO))               # hueco de los aros
    d.rectangle((11, 11, 12, 12), fill=c(OSCURO))


def crema(d):
    d.rectangle((3, 3, 12, 5), fill=c(LAVANDA))               # tapa
    d.line([(4, 4), (11, 4)], fill=c(MORADO))
    d.rounded_rectangle((2, 6, 13, 14), radius=2, fill=c(BLANCO))
    d.rectangle((3, 9, 12, 11), fill=c(ROSA))                 # etiqueta
    d.point((7, 10), fill=c(CREMA))
    d.point((8, 10), fill=c(CREMA))
    d.rectangle((3, 13, 12, 13), fill=c(GRIS_CLARO))
    d.point((4, 7), fill=c(CREMA_OSC))


def tinte(d):
    d.rectangle((7, 1, 8, 2), fill=c(GRIS))                   # boquilla
    d.rectangle((6, 3, 9, 5), fill=c(OSCURO))                 # tapa
    d.rectangle((4, 6, 11, 14), fill=c(VINO))                 # frasco
    d.rectangle((5, 8, 10, 12), fill=c(CREMA))                # etiqueta con el color del tinte
    d.rectangle((6, 9, 9, 11), fill=c(MORADO))
    d.line([(5, 7), (5, 7)], fill=c(ROSA_CLARO))


def accesorio(d):
    d.polygon([(1, 3), (6, 6), (6, 9), (1, 12)], fill=c(ROSA))      # moño: dos lazos
    d.polygon([(14, 3), (9, 6), (9, 9), (14, 12)], fill=c(ROSA))
    d.line([(2, 5), (2, 9)], fill=c(ROSA_CLARO))
    d.line([(13, 5), (13, 9)], fill=c(ROSA_OSC))
    d.rectangle((6, 5, 9, 10), fill=c(DORADO))                      # nudo
    d.line([(7, 6), (7, 8)], fill=c(CREMA))
    d.line([(9, 6), (9, 9)], fill=c(DORADO_OSC))


DIBUJOS = dict(esmalte=esmalte, labial=labial, tijeras=tijeras, crema=crema, tinte=tinte, accesorio=accesorio)


# ------------------------------------------------------------------ aro del suelo
def aro(color, oscuro, k):
    """Elipse de 2 px semitransparente con el centro apenas teñido; k = 0..2 crece para el pulso."""
    W, H = 48, 20
    w, h = 36 + 4 * k, 12 + 2 * k
    x0, y0 = (W - w) // 2, (H - h) // 2
    afuera = Image.new('L', (W, H), 0)
    ImageDraw.Draw(afuera).ellipse((x0, y0, x0 + w - 1, y0 + h - 1), fill=255)
    adentro = Image.new('L', (W, H), 0)
    ImageDraw.Draw(adentro).ellipse((x0 + 2, y0 + 2, x0 + w - 3, y0 + h - 3), fill=255)
    m, i = np.array(afuera) > 0, np.array(adentro) > 0
    a = np.zeros((H, W, 4), np.uint8)
    a[i] = c(color, 60)
    a[m & ~i] = c(color, 215)
    abajo = m & ~i & (np.arange(H)[:, None] > H // 2)            # el canto de abajo más oscuro (volumen)
    a[abajo] = c(oscuro, 215)
    return Image.fromarray(a)


def main():
    arte = {'burbuja_pedido': burbuja()}
    for p in PRODUCTOS:
        arte['producto_' + p] = icono(DIBUJOS[p])
        arte['asoma_' + p] = arte['producto_' + p].crop((0, 0, 16, ALTO_ASOMA))
    for k in range(3):
        arte[f'aro_dorado_{k}'] = aro(DORADO, DORADO_OSC, k)
        arte[f'aro_verde_{k}'] = aro(VERDE, VERDE_OSC, k)
    os.makedirs(SALIDA, exist_ok=True)
    os.makedirs(DESTINO, exist_ok=True)
    for nombre, img in arte.items():
        img.save(os.path.join(SALIDA, nombre + '.png'))
        shutil.copy(os.path.join(SALIDA, nombre + '.png'), os.path.join(DESTINO, nombre + '.png'))
    json.dump({'productos': PRODUCTOS, 'sprites': {n: list(i.size) for n, i in arte.items()}},
              open(os.path.join(SALIDA, 'marcador.json'), 'w', encoding='utf-8'), indent=1)

    # vista previa a x6: la burbuja con cada producto y los aros
    prev = Image.new('RGBA', (6 * 44 * 6, 6 * 70), c(GRIS_CLARO))
    for i, p in enumerate(PRODUCTOS):
        b = arte['burbuja_pedido'].copy()
        b.alpha_composite(arte['producto_' + p], (12, 10))
        prev.alpha_composite(b.resize((40 * 6, 44 * 6), Image.NEAREST), (i * 44 * 6 + 12, 0))
    for k in range(3):
        for j, col in enumerate(('dorado', 'verde')):
            prev.alpha_composite(arte[f'aro_{col}_{k}'].resize((48 * 4, 20 * 4), Image.NEAREST), (k * 220 + j * 700, 44 * 6 + 20))
    prev.save(os.path.join(SALIDA, 'marcador_vista_previa.png'))
    print('Listo:', DESTINO, '|', len(arte), 'sprites')


if __name__ == '__main__':
    main()
