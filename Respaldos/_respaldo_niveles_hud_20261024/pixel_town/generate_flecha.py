"""Flecha guía del mundo (FlechaGuia, DeliveryBrenns): Assets/Sprits/UI/flecha_chevron.png.

Chevron "›" de 24x24 apuntando a la derecha, pivote al centro: trazo dorado #FFD166 sólido de 3 px, borde interior
#2A1F40 de 2 px y borde exterior blanco #FFFFFF de 1 px, todo opaco (se lee sobre banqueta, pasto, calle, de noche y
al atardecer). La línea del chevron es un 20% más grande que el chevron crema anterior (5.5x11 → 6.6x13.2 px); con los
dos bordes mide 16x22 px. FlechaGuia lo usa tal cual (alfa 1, el pulso solo cambia la escala) y la de la orilla de la
pantalla es el mismo sprite más chico. ConstruirMarcadorPedido.CrearFlecha lo importa y lo pone Unlit.
También lo llama personajes/generar_clientas.py (que antes dibujaba el chevron crema). Uso: python generate_flecha.py
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage

RUTA = os.environ.get('CHEVRON_DESTINO', r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI\flecha_chevron.png')
DORADO, CONTORNO, BLANCO = (0xFF, 0xD1, 0x66), (0x2A, 0x1F, 0x40), (0xFF, 0xFF, 0xFF)
N = 24
ESCALA = 1.2   # respecto al chevron anterior (punta (10.5, 8), brazos desde (5, 2.5) y (5, 13.5) en 16x16)


def chevron():
    yy, xx = np.mgrid[0:N, 0:N] + 0.5
    c0, c1 = np.array([7.75, 8.0]), np.array([N / 2, N / 2])

    def p(x, y):
        return c1 + (np.array([x, y]) - c0) * ESCALA

    def tramo(a, b):   # distancia de cada centro de pixel al segmento a-b
        d = b - a
        t = np.clip(((xx - a[0]) * d[0] + (yy - a[1]) * d[1]) / (d @ d), 0, 1)
        return np.hypot(xx - (a[0] + t * d[0]), yy - (a[1] + t * d[1]))

    punta = p(10.5, 8.0)
    oro = np.minimum(tramo(p(5.0, 2.5), punta), tramo(punta, p(5.0, 13.5))) <= 1.5          # 3 px
    oscuro = ndimage.binary_dilation(oro, structure=np.ones((3, 3)), iterations=2) & ~oro    # 2 px
    blanco = ndimage.binary_dilation(oro | oscuro, structure=np.ones((3, 3))) & ~(oro | oscuro)   # 1 px
    out = np.zeros((N, N, 4), np.uint8)
    out[blanco] = (*BLANCO, 255)
    out[oscuro] = (*CONTORNO, 255)
    out[oro] = (*DORADO, 255)
    return out


def main():
    img = Image.fromarray(chevron())
    img.save(RUTA)
    caja = img.getbbox()
    # vista previa x8 sobre banqueta, pasto, calle, noche y atardecer
    fondos = [(184, 180, 192), (122, 158, 104), (78, 72, 98), (34, 36, 62), (175, 123, 92)]
    prev = Image.new('RGBA', (len(fondos) * (N + 4), N + 4))
    for i, f in enumerate(fondos):
        caja_f = Image.new('RGBA', (N + 4, N + 4), (*f, 255))
        caja_f.alpha_composite(img, (2, 2))
        prev.paste(caja_f, (i * (N + 4), 0))
    prev.resize((prev.width * 8, prev.height * 8), Image.NEAREST).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'flecha_vista_previa.png'))
    print(f'Listo: {RUTA} ({img.width}x{img.height}, dibujo {caja[2] - caja[0]}x{caja[3] - caja[1]} px)')


if __name__ == '__main__':
    main()
