"""Arte del turbo "Café exprés" (DeliveryBrenns).

En el mapa (Assets/Sprits/Comun/Turbo, PPU 32, Point, sin compresión; los importa Assets/Editor/ConstruirTurbos.cs):
  - cafe_expres_0..3.png (16x16): taza crema con vapor y un rayito dorado, contorno #2A1F40; 4 cuadros que flotan
    (sube y baja 1 px) con un destello que da la vuelta.
  - brillo_turbo.png (32x32): halo suave dorado debajo de la taza (de noche, en el nivel 3, se ve más) y el destello
    al reaparecer.
  - estela_turbo_0..2.png (8x8): partícula de la estela que sale detrás del personaje con el turbo: estrellita
    dorada, chispa rosa y punto que se apaga.
En el HUD (Assets/Sprits/UI, con la importación de la píldora como el reloj):
  - placa_turbo.png: la píldora de boton_pill_normal (24x18) con la rampa en lavanda.
  - icono_rayo.png (12x12): rayo dorado con contorno #2A1F40.
  - barra_turbo_fondo.png (8x4) / barra_turbo_relleno.png (8x2): la barra delgada que se vacía (canal oscuro con borde
    y relleno dorado con brillo arriba).
Deja turbos_vista_previa.png aquí al lado. Uso: python generate_turbos.py
"""
from pathlib import Path

from PIL import Image

PROYECTO = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits")
MAPA = PROYECTO / "Comun" / "Turbo"
UI = PROYECTO / "UI"
AQUI = Path(__file__).resolve().parent

OSCURO = (0x2A, 0x1F, 0x40, 255)
CREMA = (0xFF, 0xF1, 0xD0, 255)
CREMA_OSC = (0xE8, 0xD2, 0xA8, 255)
CREMA_SOMBRA = (0xC9, 0xAE, 0x86, 255)
CAFE = (0x6B, 0x3A, 0x22, 255)
CAFE_LUZ = (0x9A, 0x5A, 0x34, 255)
DORADO = (0xFF, 0xD1, 0x66, 255)
DORADO_OSC = (0xC8, 0x8A, 0x1E, 255)
DORADO_LUZ = (0xFF, 0xF0, 0xB0, 255)
BLANCO = (0xFF, 0xFF, 0xFF, 255)
VAPOR = (0xF4, 0xEE, 0xFF, 220)
VAPOR_OSC = (0xC8, 0xBC, 0xE8, 200)
ROSA = (0xFF, 0x6F, 0xA5, 255)
ROSA_CLARO = (0xFF, 0xB3, 0xD1, 255)

# taza de 11x8 (fila 0 = borde de arriba); "o" contorno, "c" crema, "s" sombra crema, "k" café, "l" luz del café,
# "a" asa; el rayito se pinta encima
TAZA = [
    "ooooooooo..",
    "okkkkklko..",
    "occccccco..",
    "occccccsooo",
    "occccccso.o",
    "occccccsooo",
    ".occcccso..",
    "..ooooooo..",
]
PLATO = ".ooooooooooo."
RAYO = [(4, 2), (5, 2), (3, 3), (4, 3), (2, 4), (3, 4), (4, 4), (5, 4), (4, 5), (3, 6)]   # zigzag, coordenadas de la taza


def taza(img, x0, y0):
    px = img.load()
    for j, fila in enumerate(TAZA):
        for i, c in enumerate(fila):
            col = {"o": OSCURO, "c": CREMA, "s": CREMA_SOMBRA, "k": CAFE, "l": CAFE_LUZ}.get(c)
            if col:
                px[x0 + i, y0 + j] = col
    for i, c in enumerate(PLATO):                     # platito
        if c == "o":
            px[x0 - 1 + i, y0 + 8] = OSCURO
    for i in range(1, 11):
        px[x0 - 1 + i, y0 + 8] = CREMA_OSC if i not in (1, 10) else OSCURO
    for i in range(0, 12):
        if 0 < i < 12:
            px[x0 - 1 + i, y0 + 9] = OSCURO if i in (1, 11) or True else px[x0 - 1 + i, y0 + 9]
    for x, y in RAYO:                                 # el rayito dorado en el frente
        px[x0 + x, y0 + y] = DORADO
    px[x0 + 4, y0 + 2] = DORADO_LUZ
    for x, y in ((5, 4), (4, 5), (3, 6)):
        px[x0 + x, y0 + y] = DORADO_OSC
    px[x0 + 1, y0 + 2] = BLANCO                       # brillo de la loza


def vapor(img, x0, y0, fase):
    """Dos hilitos de vapor ondulados que suben; la fase los corre un píxel."""
    px = img.load()
    for k, xb in enumerate((x0 + 2, x0 + 6)):
        for j in range(4):
            dx = ((j + fase + k) % 4 in (1, 2))
            x, y = xb + dx, y0 - 1 - j
            if 0 <= y < img.height:
                px[x, y] = VAPOR if j < 3 else VAPOR_OSC


def destello(img, x, y, grande):
    px = img.load()
    pts = [(0, 0)] + ([(1, 0), (-1, 0), (0, 1), (0, -1)] if grande else [])
    for dx, dy in pts:
        if 0 <= x + dx < img.width and 0 <= y + dy < img.height:
            px[x + dx, y + dy] = BLANCO if (dx, dy) == (0, 0) else DORADO_LUZ


def cafe(k):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    bote = (0, -1, -1, 0)[k]                           # flota
    x0, y0 = 2, 5 + bote
    taza(img, x0, y0)
    vapor(img, x0, y0, k)
    sitio = [(13, 3), (14, 9), (1, 11), (1, 4)][k]     # el destello da la vuelta
    destello(img, sitio[0], sitio[1], k % 2 == 0)
    return img


def brillo():
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    px = img.load()
    for y in range(32):
        for x in range(32):
            d = (((x - 15.5) / 15.5) ** 2 + ((y - 15.5) / 15.5) ** 2) ** 0.5
            a = 0 if d > 1 else (60 if d > 0.72 else 110 if d > 0.45 else 160)   # tres anillos, sin degradado fino
            if a:
                px[x, y] = (0xFF, 0xE8, 0x9A, a)
    return img


def estela(k):
    img = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    px = img.load()
    if k == 0:                                         # estrellita dorada
        for x, y in [(3, 1), (3, 2), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (3, 5), (2, 2), (4, 2), (2, 4), (4, 4)]:
            px[x, y] = DORADO
        px[3, 3] = DORADO_LUZ
        for x, y in [(3, 0), (0, 3), (6, 3), (3, 6)]:
            px[x, y] = DORADO_OSC
    elif k == 1:                                       # chispa rosa
        for x, y in [(3, 2), (2, 3), (3, 3), (4, 3), (3, 4)]:
            px[x, y] = ROSA
        px[3, 3] = ROSA_CLARO
    else:                                              # punto que se apaga
        px[3, 3] = (0xFF, 0xB3, 0xD1, 200)
        px[4, 3] = (0xFF, 0xD1, 0x66, 140)
    return img


# ---------------------------------------------------------------- HUD
ROSA_PILDORA = [(255, 111, 165), (176, 58, 116), (255, 179, 209), (217, 84, 140)]
LAVANDA = [(196, 176, 255), (98, 74, 168), (232, 224, 255), (160, 136, 230)]


def placa():
    base = Image.open(UI / "boton_pill_normal.png").convert("RGBA")
    px = base.load()
    for y in range(base.height):
        for x in range(base.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if (r, g, b) not in ROSA_PILDORA:
                raise SystemExit(f"color inesperado {(r, g, b)} en la píldora")
            px[x, y] = LAVANDA[ROSA_PILDORA.index((r, g, b))] + (a,)
    return base


def icono_rayo():
    img = Image.new("RGBA", (12, 12), (0, 0, 0, 0))
    px = img.load()
    cuerpo = [(6, 0), (7, 0), (5, 1), (6, 1), (4, 2), (5, 2), (3, 3), (4, 3), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4),
              (8, 4), (6, 5), (7, 5), (5, 6), (6, 6), (4, 7), (5, 7), (4, 8), (3, 9), (3, 10)]
    for x, y in cuerpo:
        px[x, y] = DORADO
    for x, y in [(6, 0), (5, 1), (4, 2), (3, 3), (3, 4)]:
        px[x, y] = DORADO_LUZ
    # contorno de 1 px alrededor
    puntos = set(cuerpo)
    for x, y in list(puntos):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in puntos and 0 <= q[0] < 12 and 0 <= q[1] < 12:
                px[q] = OSCURO
    return img


def barra(relleno):
    """Canal de 8x4 con borde de 1 px (9-slice) y relleno de 8x2 (luz arriba) que cabe justo dentro del canal."""
    img = Image.new("RGBA", (8, 2 if relleno else 4), (0, 0, 0, 0))
    px = img.load()
    for x in range(8):
        for y in range(img.height):
            if relleno:
                px[x, y] = DORADO_LUZ if y == 0 else DORADO
            else:
                px[x, y] = OSCURO if y in (0, 3) or x in (0, 7) else (0x4A, 0x3B, 0x6E, 255)
    return img


def main():
    MAPA.mkdir(parents=True, exist_ok=True)
    salida = {}
    for k in range(4):
        salida[MAPA / f"cafe_expres_{k}.png"] = cafe(k)
    salida[MAPA / "brillo_turbo.png"] = brillo()
    for k in range(3):
        salida[MAPA / f"estela_turbo_{k}.png"] = estela(k)
    salida[UI / "placa_turbo.png"] = placa()
    salida[UI / "icono_rayo.png"] = icono_rayo()
    salida[UI / "barra_turbo_fondo.png"] = barra(False)
    salida[UI / "barra_turbo_relleno.png"] = barra(True)
    for ruta, img in salida.items():
        img.save(ruta)
    # vista previa a x8 sobre banqueta gris y sobre noche
    piezas = list(salida.values())
    ancho = sum(i.width for i in piezas) + 4 * len(piezas)
    prev = Image.new("RGBA", (ancho, 2 * 36), (0, 0, 0, 255))
    x = 0
    for i in piezas:
        for fila, fondo in enumerate(((0xB8, 0xB4, 0xC0, 255), (0x1A, 0x1C, 0x30, 255))):
            caja = Image.new("RGBA", (i.width + 4, 36), fondo)
            caja.alpha_composite(i, (2, 2))
            prev.paste(caja, (x, fila * 36))
        x += i.width + 4
    prev.resize((prev.width * 6, prev.height * 6), Image.NEAREST).save(AQUI / "turbos_vista_previa.png")
    print("Listo:", ", ".join(r.name for r in salida))


if __name__ == "__main__":
    main()
