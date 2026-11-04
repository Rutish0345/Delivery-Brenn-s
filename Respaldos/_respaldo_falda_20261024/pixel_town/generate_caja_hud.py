"""Marco de la caja del HUD (DeliveryBrenns): CajaHUD, arriba a la izquierda, junta Pedidos, el reloj, las quejas y
la placa del turbo.

- marco_caja_hud: 16x16 para 9-slice (borde de 6 px), el mismo estilo que marco_panel (panel final) pero más fino:
  contorno #B03A74, banda rosa #FF6FA5 de 2 px con brillo #FFB3D1 arriba, contorno interior #B03A74, sombra crema
  #EEDAB0 arriba e izquierda y el centro crema #FFF1D0. Esquinas pixel (recortadas en escalón).
  En Unity va a x2 (ConstruirCajaHUD): el marco mide 10 unidades y el relleno de 12 deja 2 de crema libre.

Escribe en DeliveryBrenns/Assets/Sprits/UI. Uso: python generate_caja_hud.py
"""
from pathlib import Path

from PIL import Image

UI = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI")

CONTORNO = (176, 58, 116, 255)   # B03A74
ROSA = (255, 111, 165, 255)      # FF6FA5
BRILLO = (255, 179, 209, 255)    # FFB3D1
SOMBRA = (238, 218, 176, 255)    # EEDAB0
CREMA = (255, 241, 208, 255)     # FFF1D0
LADO = 16


def distancia(x, y):
    """Píxeles desde la orilla; en las esquinas el escalón recorta 3 píxeles (negativo = transparente)."""
    dx, dy = min(x, LADO - 1 - x), min(y, LADO - 1 - y)
    if dx < 2 and dy < 2:
        return dx + dy - 2
    return min(dx, dy)


def marco():
    im = Image.new("RGBA", (LADO, LADO), (0, 0, 0, 0))
    px = im.load()
    for y in range(LADO):
        for x in range(LADO):
            d = distancia(x, y)
            if d < 0:
                continue
            if d == 0 or d == 3:
                c = CONTORNO
            elif d in (1, 2):
                c = BRILLO if d == 1 and y == 1 and 1 < x < LADO - 2 else ROSA
            elif d == 4 and (y == 4 or x == 4):
                c = SOMBRA
            else:
                c = CREMA
            px[x, y] = c
    im.save(UI / "marco_caja_hud.png")
    return im


if __name__ == "__main__":
    im = marco()
    centro = im.getpixel((LADO // 2, LADO // 2))
    assert centro == CREMA, centro
    assert im.getpixel((0, 0))[3] == 0 and im.getpixel((2, 0)) == CONTORNO
    print("marco_caja_hud.png", im.size, "centro", centro)
