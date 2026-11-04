"""Arte del turbo "Café exprés" (DeliveryBrenns).

En el mapa (Assets/Sprits/Comun/Turbo, PPU 32, Point, sin compresión; los importa Assets/Editor/ConstruirTurbos.cs):
  - cafe_expres_0..3.png (16x16): taza crema con vapor y un rayito dorado, contorno #2A1F40; 4 cuadros que flotan
    (sube y baja 1 px) con un destello que da la vuelta.
  - brillo_turbo.png (32x32): halo suave dorado debajo de la taza (de noche, en el nivel 3, se ve más) y el destello
    al reaparecer.
  - estela_turbo_0..4.png (10x10): los 5 cuadros que recorre cada partícula de la estela detrás del personaje con el
    turbo: estrella dorada grande, chispa rosa, estrella dorada chica, chispa rosa chica (con contorno #2A1F40, para
    que se lean sobre la banqueta clara) y un destello que se apaga.
En el HUD (Assets/Sprits/UI, con la importación de la píldora como el reloj):
  - placa_turbo.png: la píldora de boton_pill_normal (24x18) con la rampa en lavanda.
  - icono_rayo.png (12x12): rayo dorado con contorno #2A1F40.
  - barra_turbo_fondo.png (8x6) / barra_turbo_relleno.png (8x4): la barra que se vacía (canal oscuro con borde y
    relleno dorado con brillo blanco arriba y sombra abajo).
  - lineas_velocidad_0..2.png (320x180): líneas de velocidad suaves en la orilla de la pantalla (TurboHUD las estira a
    pantalla completa y las alterna mientras dura el turbo).
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


def _con_contorno(img, puntos, colores):
    """Pinta los puntos con sus colores y un contorno #2A1F40 de 1 px (se lee sobre banqueta clara y de noche)."""
    px = img.load()
    for (x, y), c in zip(puntos, colores):
        px[x, y] = c
    puestos = set(puntos)
    for x, y in puntos:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in puestos and 0 <= q[0] < img.width and 0 <= q[1] < img.height:
                px[q] = OSCURO
    return img


def _estrella(cx, cy, brazo, cuerpo, centro):
    pts, cols = [], []
    for d in range(-brazo, brazo + 1):
        for q in ((cx + d, cy), (cx, cy + d)):
            if q not in pts:
                pts.append(q)
                cols.append(centro if q == (cx, cy) else cuerpo)
    return pts, cols


def estela(k):
    """5 cuadros de 10x10 que va recorriendo cada partícula: estrella dorada grande, chispa rosa, estrella dorada chica,
    chispa rosa chica y destello que se apaga. Con contorno oscuro los 4 primeros."""
    img = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    if k == 0:
        pts, cols = _estrella(4, 4, 3, DORADO, BLANCO)
        for q in ((3, 3), (5, 3), (3, 5), (5, 5)):
            pts.append(q); cols.append(DORADO_LUZ)
        return _con_contorno(img, pts, cols)
    if k == 1:
        pts, cols = _estrella(4, 4, 2, ROSA, BLANCO)
        for q in ((3, 3), (5, 5)):
            pts.append(q); cols.append(ROSA_CLARO)
        return _con_contorno(img, pts, cols)
    if k == 2:
        pts, cols = _estrella(4, 4, 2, DORADO, DORADO_LUZ)
        return _con_contorno(img, pts, cols)
    if k == 3:
        pts, cols = _estrella(4, 4, 1, ROSA, ROSA_CLARO)
        return _con_contorno(img, pts, cols)
    px = img.load()
    px[4, 4] = (0xFF, 0xF0, 0xB0, 220)
    px[5, 4] = (0xFF, 0xB3, 0xD1, 150)
    return img


def lineas_velocidad(k):
    """Líneas de velocidad suaves en la orilla de la pantalla (320x180, el HUD la estira x6): trazos blancos de 1 px
    que apuntan al centro, solo fuera de una elipse central (el centro queda libre). Tres cuadros con otras líneas."""
    import math
    import random
    W, H = 320, 180
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    rnd = random.Random(40 + k)
    for _ in range(46):
        ang = rnd.uniform(0, 2 * math.pi)
        dx, dy = math.cos(ang), math.sin(ang)
        # del borde de la pantalla hacia adentro
        t_borde = min((W / 2) / abs(dx) if dx else 1e9, (H / 2) / abs(dy) if dy else 1e9)
        largo = rnd.uniform(0.18, 0.34) * t_borde
        alfa = rnd.choice((90, 120, 150))
        t = t_borde
        while t > t_borde - largo:
            x, y = int(W / 2 + dx * t), int(H / 2 + dy * t)
            if 0 <= x < W and 0 <= y < H:
                f = (t - (t_borde - largo)) / largo          # más fuerte hacia la orilla
                px[x, y] = (255, 250, 235, int(alfa * f))
            t -= 0.5
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
    """Canal de 8x6 con borde de 1 px (9-slice) y relleno de 8x4 (luz arriba, sombra abajo) que cabe justo dentro."""
    img = Image.new("RGBA", (8, 4 if relleno else 6), (0, 0, 0, 0))
    px = img.load()
    for x in range(8):
        for y in range(img.height):
            if relleno:
                px[x, y] = BLANCO if y == 0 else DORADO if y < 3 else DORADO_OSC
            else:
                px[x, y] = OSCURO if y in (0, 5) or x in (0, 7) else (0x3A, 0x2C, 0x5C, 255)
    return img


def main():
    MAPA.mkdir(parents=True, exist_ok=True)
    salida = {}
    for k in range(4):
        salida[MAPA / f"cafe_expres_{k}.png"] = cafe(k)
    salida[MAPA / "brillo_turbo.png"] = brillo()
    for k in range(5):
        salida[MAPA / f"estela_turbo_{k}.png"] = estela(k)
    salida[UI / "placa_turbo.png"] = placa()
    salida[UI / "icono_rayo.png"] = icono_rayo()
    salida[UI / "barra_turbo_fondo.png"] = barra(False)
    salida[UI / "barra_turbo_relleno.png"] = barra(True)
    for ruta, img in salida.items():
        img.save(ruta)
    for k in range(3):   # pantalla completa: aparte de la vista previa
        lineas_velocidad(k).save(UI / f"lineas_velocidad_{k}.png")
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
    print("Listo:", ", ".join(r.name for r in salida), "+ lineas_velocidad_0..2")


if __name__ == "__main__":
    main()
