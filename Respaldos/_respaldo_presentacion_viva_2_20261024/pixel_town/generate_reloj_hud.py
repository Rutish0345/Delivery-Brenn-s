"""Arte del reloj por pedido del HUD (DeliveryBrenns).

- placa_reloj_verde / _amarilla / _roja: la misma píldora de 24x18 que boton_pill_normal (placas de "Pedidos"
  y "$"), con su rampa de 4 tonos cambiada a verde, amarillo y rojo. Así el texto sigue en #2A1F40 y legible.
- icono_reloj: reloj de 12x12 (carátula crema, borde y manecillas #2A1F40, dos campanitas arriba).

Escribe en DeliveryBrenns/Assets/Sprits/UI. Uso: python generate_reloj_hud.py
"""
from pathlib import Path

from PIL import Image

UI = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI")

# rampa de la píldora rosa: principal, borde oscuro, brillo, sombra de abajo
ROSA = [(255, 111, 165), (176, 58, 116), (255, 179, 209), (217, 84, 140)]
RAMPAS = {
    "verde":    [(123, 216, 143), (46, 125, 79), (196, 242, 204), (88, 180, 110)],
    "amarilla": [(255, 209, 102), (170, 116, 30), (255, 238, 176), (230, 170, 60)],
    "roja":     [(255, 93, 115), (156, 36, 60), (255, 176, 186), (220, 62, 88)],
}

OSCURO = (42, 31, 64, 255)
CREMA = (255, 241, 208, 255)


def placas():
    base = Image.open(UI / "boton_pill_normal.png").convert("RGBA")
    for nombre, rampa in RAMPAS.items():
        im = base.copy()
        px = im.load()
        for y in range(im.height):
            for x in range(im.width):
                r, g, b, a = px[x, y]
                if a == 0:
                    continue
                if (r, g, b) not in ROSA:
                    raise SystemExit(f"color inesperado {(r, g, b)} en la píldora")
                px[x, y] = rampa[ROSA.index((r, g, b))] + (a,)
        im.save(UI / f"placa_reloj_{nombre}.png")


def icono():
    im = Image.new("RGBA", (12, 12), (0, 0, 0, 0))
    px = im.load()
    cx, cy, r = 5.5, 6.5, 5.0
    for y in range(12):
        for x in range(12):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d <= r - 1.0:
                px[x, y] = CREMA
            elif d <= r + 0.35:
                px[x, y] = OSCURO
    # campanitas
    for x, y in [(1, 1), (2, 1), (1, 2), (9, 1), (10, 1), (10, 2)]:
        px[x, y] = OSCURO
    # manecillas: hacia arriba (12) y hacia la derecha (3)
    for y in range(4, 7):
        px[5, y] = OSCURO
    for x in range(6, 8):
        px[x, 6] = OSCURO
    im.save(UI / "icono_reloj.png")


if __name__ == "__main__":
    placas()
    icono()
    print("Listo: placa_reloj_{verde,amarilla,roja}.png e icono_reloj.png en", UI)
