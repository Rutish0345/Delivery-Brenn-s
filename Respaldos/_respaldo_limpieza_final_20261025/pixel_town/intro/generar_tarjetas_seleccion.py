"""Piezas de las dos tarjetas de "ELIGE A TU REPARTIDOR" (escena Seleccion, la arma ConstruirSeleccion.cs).

Escribe en Assets/Sprits/UI:
  seleccion_tarjeta.png          32x32, #2A1F40 con borde rosa #FF6FA5 de 4 px y esquinas redondeadas (9-slice 10)
  seleccion_tarjeta_elegida.png  igual con el borde dorado #FFD166
  seleccion_halo.png             48x48, halo dorado en 3 anillos de 4 px que se desvanecen (9-slice 16), va detrás
  seleccion_piso.png             64x14, el piso suave bajo los pies (elipse en dos tonos), a x6
  seleccion_circulo.png          56x56, el círculo detrás del personaje, a x6
Las tarjetas y el halo van a x1 (cada píxel, un píxel a 1920x1080); el piso y el círculo a x6 como el personaje.
Uso: python generar_tarjetas_seleccion.py
"""
import os
from PIL import Image

PROYECTO = os.environ.get('PROYECTO_UNITY', r'C:\Users\kangu\DeliveryBrenns')
SALIDA = os.path.join(PROYECTO, 'Assets', 'Sprits', 'UI')

OSCURO = (0x2A, 0x1F, 0x40, 255)
ROSA = (0xFF, 0x6F, 0xA5, 255)
DORADO = (0xFF, 0xD1, 0x66, 255)
LILA = (0x3A, 0x2C, 0x58, 255)
LILA_CLARO = (0x4A, 0x3A, 0x70, 255)
LILA_PISO = (0x55, 0x44, 0x80, 255)


def redondeado(lado, radio, x, y):
    """True si (x, y) cae dentro del cuadrado de 'lado' con esquinas de 'radio' (centros de píxel)."""
    cx = min(max(x + 0.5, radio), lado - radio)
    cy = min(max(y + 0.5, radio), lado - radio)
    return (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= radio ** 2


def tarjeta(borde, lado=32, grueso=4, radio=7):
    im = Image.new('RGBA', (lado, lado), (0, 0, 0, 0))
    for y in range(lado):
        for x in range(lado):
            if not redondeado(lado, radio, x, y):
                continue
            dentro = (grueso <= x < lado - grueso and grueso <= y < lado - grueso
                      and redondeado(lado - 2 * grueso, max(radio - grueso, 1), x - grueso, y - grueso))
            im.putpixel((x, y), OSCURO if dentro else borde)
    return im


def halo(lado=48, anillos=(170, 105, 50), ancho=4, radio=14):
    """Anillos de afuera hacia adentro más opacos: el de adentro pegado a la tarjeta."""
    im = Image.new('RGBA', (lado, lado), (0, 0, 0, 0))
    n = len(anillos)
    for k, alfa in enumerate(reversed(anillos)):   # del más claro (afuera) al más fuerte (adentro)
        m = k * ancho
        sub = lado - 2 * m
        r = max(radio - m, 2)
        for y in range(sub):
            for x in range(sub):
                if redondeado(sub, r, x, y):
                    im.putpixel((x + m, y + m), DORADO[:3] + (alfa,))
    # el centro, transparente (lo tapa la tarjeta)
    m = n * ancho
    for y in range(m, lado - m):
        for x in range(m, lado - m):
            im.putpixel((x, y), (0, 0, 0, 0))
    return im


def elipse(ancho, alto, color, borde=None):
    im = Image.new('RGBA', (ancho, alto), (0, 0, 0, 0))
    a, b = ancho / 2, alto / 2
    for y in range(alto):
        for x in range(ancho):
            d = ((x + 0.5 - a) / a) ** 2 + ((y + 0.5 - b) / b) ** 2
            if d <= 1:
                im.putpixel((x, y), borde if borde and d > 0.72 else color)
    return im


def main():
    os.makedirs(SALIDA, exist_ok=True)
    piezas = {
        'seleccion_tarjeta': tarjeta(ROSA),
        'seleccion_tarjeta_elegida': tarjeta(DORADO),
        'seleccion_halo': halo(),
        'seleccion_piso': elipse(64, 14, LILA_PISO, LILA_CLARO),
        'seleccion_circulo': elipse(56, 56, LILA, LILA_CLARO),
    }
    for nombre, im in piezas.items():
        ruta = os.path.join(SALIDA, nombre + '.png')
        im.save(ruta)
        print(ruta, im.size)
    # vista previa a x4
    ancho = sum(im.width for im in piezas.values()) + 8 * (len(piezas) + 1)
    vista = Image.new('RGBA', (ancho, 72), (0xF6, 0xD8, 0xE6, 255))
    x = 8
    for im in piezas.values():
        vista.alpha_composite(im, (x, 4))
        x += im.width + 8
    vista = vista.resize((vista.width * 4, vista.height * 4), Image.NEAREST)
    vista.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tarjetas_seleccion_vista_previa.png'))


if __name__ == '__main__':
    main()
