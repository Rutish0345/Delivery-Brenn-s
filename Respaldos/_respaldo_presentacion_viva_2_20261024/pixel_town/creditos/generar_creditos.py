"""Arte de la escena de créditos de DeliveryBrenns (Creditos.unity, la arma ConstruirCreditos).

- creditos_cielo.png 480x270 (a x4 llena 1920x1080): cielo nocturno azul oscuro en bandas con tramado (dither) de
  #0B1030 arriba a #2B3C78 abajo, unas pocas estrellas fijas muy tenues, la luna en cuarto a la derecha y una fila de
  colinas y techos del pueblo en silueta (#0A0F25) con ventanitas encendidas (#FFD166), como un cielo de pixel art.
- estrella_grande.png 5x5 (cruz con centro blanco y brazos #BFD4FF) y estrella_chica.png 3x3 (cruz pequeña): las
  que titilan en Unity (Creditos.cs les cambia la alfa).
- nube_noche_1.png 48x14 y nube_noche_2.png 64x18: nubes de noche #33447F con borde de luz de luna #5468A8 y base
  #26346A; Unity las mueve despacio de izquierda a derecha.

Escribe en DeliveryBrenns/Assets/Sprits/Creditos. Vista previa: creditos_vista_previa.png (aquí).
Uso: python generar_creditos.py
"""
import random
from pathlib import Path

from PIL import Image

AQUI = Path(__file__).parent
SALIDA = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Creditos")

W, H = 480, 270
BANDAS = [(11, 16, 48), (15, 22, 60), (20, 29, 74), (26, 37, 90), (33, 47, 106), (43, 60, 120)]
SILUETA = (10, 15, 37)
SILUETA_LUZ = (18, 25, 56)
VENTANA = (255, 209, 102)
LUNA = (255, 241, 208)
LUNA_SOMBRA = (214, 200, 170)

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def cielo():
    im = Image.new("RGB", (W, H))
    px = im.load()
    alto_banda = (H - 40) / (len(BANDAS) - 1)
    for y in range(H):
        f = min(y / alto_banda, len(BANDAS) - 1 - 1e-6)
        i = int(f)
        t = f - i
        for x in range(W):
            # tramado ordenado entre dos bandas: transición a lo pixel art, sin degradado suave
            c = BANDAS[i + 1] if t * 16 > BAYER[y % 4][x % 4] else BANDAS[i]
            px[x, y] = c

    rnd = random.Random(11)
    for _ in range(60):   # estrellas fijas muy tenues (las que titilan van aparte, en Unity)
        x, y = rnd.randrange(W), rnd.randrange(int(H * 0.7))
        b = px[x, y]
        px[x, y] = tuple(min(255, c + 70) for c in b)

    # luna en cuarto creciente, arriba a la derecha
    cx, cy, r = 400, 44, 14
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r and (x - cx + 7) ** 2 + (y - cy - 3) ** 2 > (r - 2) ** 2:
                px[x, y] = LUNA if (x - cx) + (y - cy) < 6 else LUNA_SOMBRA

    # colinas al fondo y el pueblo en silueta abajo
    for x in range(W):
        alto = int(30 + 8 * abs(((x / 70.0) % 2) - 1) + 4 * ((x // 23) % 2))
        for y in range(H - alto, H):
            px[x, y] = SILUETA_LUZ
    casas = [(10, 40, 26), (56, 28, 18), (92, 54, 34), (160, 36, 22), (206, 70, 42), (290, 32, 20), (330, 46, 30),
             (386, 38, 24), (432, 44, 28)]
    for x0, ancho, alto in casas:
        base = H - 6
        for x in range(x0, min(W, x0 + ancho)):
            for y in range(base - alto, H):
                px[x, y] = SILUETA
        for k in range(ancho // 2 + 1):   # techo de dos aguas
            for x in (x0 + k, x0 + ancho - 1 - k):
                for y in range(base - alto - k // 1 - 1, base - alto):
                    if 0 <= x < W and y >= base - alto - ancho // 3:
                        px[x, y] = SILUETA
        for wy in range(base - alto + 6, base - 4, 9):   # ventanitas encendidas
            for wx in range(x0 + 4, x0 + ancho - 5, 8):
                if rnd.random() < 0.55:
                    for dx in range(2):
                        for dy in range(3):
                            px[wx + dx, wy + dy] = VENTANA
    return im


def estrella(n):
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = im.load()
    c = n // 2
    for k in range(n):
        px[c, k] = px[k, c] = (191, 212, 255, 255)
    px[c, c] = (255, 255, 255, 255)
    return im


def nube(w, h):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    rnd = random.Random(w)
    bultos = [(w * 0.25, h * 0.62, h * 0.38), (w * 0.5, h * 0.45, h * 0.55), (w * 0.75, h * 0.6, h * 0.4)]
    bultos += [(rnd.uniform(w * 0.15, w * 0.85), h * 0.65, h * 0.3) for _ in range(2)]
    def dentro(x, y):
        return any((x - bx) ** 2 + ((y - by) * 1.3) ** 2 <= br ** 2 for bx, by, br in bultos) and y < h - 1
    for y in range(h):
        for x in range(w):
            if not dentro(x, y):
                continue
            if not dentro(x, y - 1):
                px[x, y] = (84, 104, 168, 235)    # borde de luz de luna
            elif not dentro(x, y + 1) or y >= h - 3:
                px[x, y] = (38, 52, 106, 235)     # base
            else:
                px[x, y] = (51, 68, 127, 235)
    return im


if __name__ == "__main__":
    SALIDA.mkdir(parents=True, exist_ok=True)
    piezas = {
        "creditos_cielo": cielo(), "estrella_grande": estrella(5), "estrella_chica": estrella(3),
        "nube_noche_1": nube(48, 14), "nube_noche_2": nube(64, 18),
    }
    for nombre, im in piezas.items():
        im.save(SALIDA / f"{nombre}.png")
        print(f"{nombre}.png", im.size)
    # vista previa a x2 con algunas estrellas y nubes encima
    vista = piezas["creditos_cielo"].convert("RGBA")
    rnd = random.Random(3)
    for _ in range(18):
        e = piezas["estrella_grande" if rnd.random() < 0.4 else "estrella_chica"]
        vista.alpha_composite(e, (rnd.randrange(W - 5), rnd.randrange(150)))
    vista.alpha_composite(piezas["nube_noche_1"], (60, 70))
    vista.alpha_composite(piezas["nube_noche_2"], (250, 110))
    vista.resize((W * 2, H * 2), Image.NEAREST).save(AQUI / "creditos_vista_previa.png")
