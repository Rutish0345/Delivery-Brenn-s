"""Arte de la pantalla de selección de nivel (DeliveryBrenns, escena SeleccionNivel).

- miniatura_nivel1/2/3: 240x135 (16:9), recortes de 960x540 de la captura del mapa de cada nivel
  (unity/<zona>/captura_unity.png, 32 px por tile) reducidos a 1/4. En Unity van a x2 con filtro Point.
  Nivel 2 con el tinte del atardecer (la luz global FFB880 del LevelManager, suavizada); nivel 3 ya viene de noche y
  lleva unas gotas de lluvia diagonales.
- candado: 12x14, cuerpo dorado #FFD166 con contorno #2A1F40, arco gris y cerradura oscura (para BLOQUEADO).
- placa_estado: 16x12 para 9-slice (borde 4): relleno blanco (Unity la tiñe: verde NUEVO, dorado COMPLETADO, gris
  BLOQUEADO), brillo arriba, contorno #2A1F40 y esquinas en escalón.

Escribe en DeliveryBrenns/Assets/Sprits/UI/Niveles. Uso: python generate_miniaturas_niveles.py
"""
import random
from pathlib import Path

from PIL import Image

AQUI = Path(__file__).parent / "unity"
SALIDA = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI\Niveles")

# (zona, esquina superior izquierda del recorte en px de la captura)
RECORTES = [("residencial", (0, 0)), ("comercial", (1560, 520)), ("lluvia", (1700, 560))]
ANCHO, ALTO, REDUCCION = 960, 540, 4

OSCURO = (42, 31, 64, 255)
DORADO = (255, 209, 102, 255)
DORADO_SOMBRA = (214, 160, 60, 255)
GRIS = (170, 160, 190, 255)
GRIS_OSCURO = (110, 100, 135, 255)


def multiplicar(im, color, cuanto):
    r, g, b = (1 - cuanto + cuanto * c / 255 for c in color)
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            px[x, y] = (int(p[0] * r), int(p[1] * g), int(p[2] * b), 255)


def lluvia(im):
    rnd = random.Random(7)   # siempre las mismas gotas
    px = im.load()
    for _ in range(70):
        x, y = rnd.randrange(im.width), rnd.randrange(im.height)
        for k in range(3):
            xx, yy = x - k, y + k * 2
            if 0 <= xx < im.width and 0 <= yy < im.height:
                p = px[xx, yy]
                px[xx, yy] = tuple(min(255, int(c * 0.5 + 200 * 0.5)) for c in p[:3]) + (255,)


def miniaturas():
    SALIDA.mkdir(parents=True, exist_ok=True)
    for i, (zona, (x, y)) in enumerate(RECORTES, 1):
        cap = Image.open(AQUI / zona / "captura_unity.png").convert("RGB")
        assert x + ANCHO <= cap.width and y + ALTO <= cap.height, (zona, cap.size)
        im = cap.crop((x, y, x + ANCHO, y + ALTO)).reduce(REDUCCION).convert("RGBA")
        if zona == "comercial":
            multiplicar(im, (255, 184, 128), 0.45)
        if zona == "lluvia":
            lluvia(im)
        im.save(SALIDA / f"miniatura_nivel{i}.png")
        print(f"miniatura_nivel{i}.png", im.size, zona)


CANDADO = [
    "...oooooo...",
    "..oggggggo..",
    ".ogGooooGgo.",
    ".ogo....ogo.",
    ".ogo....ogo.",
    ".ogo....ogo.",
    "oooooooooooo",
    "oddddddddddo",
    "oddddoodddd" + "o",
    "oddddoodddd" + "o",
    "odddddoddddo",
    "ossssssssss" + "o",
    "ossssssssss" + "o",
    "oooooooooooo",
]
COLORES = {"o": OSCURO, "g": GRIS, "G": GRIS_OSCURO, "d": DORADO, "s": DORADO_SOMBRA}


def candado():
    im = Image.new("RGBA", (12, len(CANDADO)), (0, 0, 0, 0))
    px = im.load()
    for y, fila in enumerate(CANDADO):
        assert len(fila) == 12, (y, fila)
        for x, c in enumerate(fila):
            if c in COLORES:
                px[x, y] = COLORES[c]
    im.save(SALIDA / "candado.png")
    print("candado.png", im.size)


def placa_estado():
    w, h = 16, 12
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    for y in range(h):
        for x in range(w):
            dx, dy = min(x, w - 1 - x), min(y, h - 1 - y)
            if dx + dy < 1:
                continue
            borde = dx == 0 or dy == 0 or (dx + dy == 1)
            if borde:
                px[x, y] = OSCURO
            elif dy == 1 and y < h // 2:
                px[x, y] = (255, 255, 255, 255)
            elif y >= h - 3:
                px[x, y] = (205, 200, 215, 255)   # sombra de abajo (se tiñe con el color)
            else:
                px[x, y] = (238, 236, 242, 255)
    im.save(SALIDA / "placa_estado.png")
    print("placa_estado.png", im.size)


if __name__ == "__main__":
    miniaturas()
    candado()
    placa_estado()
