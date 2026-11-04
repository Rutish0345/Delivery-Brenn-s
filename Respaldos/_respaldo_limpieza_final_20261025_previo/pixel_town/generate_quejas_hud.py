"""Arte de las quejas del HUD (DeliveryBrenns): la fila de iconos debajo del reloj del pedido.

- queja_llena: la carita enojada roja (#FF5D73, contorno #2A1F40) de 18x18, la misma de carita_1 del medidor.
- queja_vacia: la misma silueta en gris (#A89FB8 con brillo y sombra) y contorno #2A1F40, sin cara.

En el juego van a x3 (54 unidades del Canvas de 1920x1080): píxeles enteros, sin borroso.
Escribe en DeliveryBrenns/Assets/Sprits/UI. Uso: python generate_quejas_hud.py
"""
from pathlib import Path

from PIL import Image

UI = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Sprits\UI")

OSCURO = (42, 31, 64)
ROJO, ROJO_SOMBRA, ROJO_BRILLO = (255, 93, 115), (208, 79, 104), (255, 182, 171)
GRIS, GRIS_SOMBRA, GRIS_BRILLO = (168, 159, 184), (126, 116, 148), (214, 208, 224)


def main():
    carita = Image.open(UI / "carita_1.png").convert("RGBA")
    colores = {c[:3] for c in carita.getdata() if c[3]}
    esperados = {OSCURO, ROJO, ROJO_SOMBRA, ROJO_BRILLO}
    if colores != esperados:
        raise SystemExit(f"carita_1 cambió de paleta: {sorted(colores)}")
    carita.save(UI / "queja_llena.png")

    # vacía: el contorno exterior se queda, todo lo de adentro pasa a gris (los rasgos también: sin cara)
    vacia = Image.new("RGBA", carita.size)
    fuente, px = carita.load(), vacia.load()
    w, h = carita.size
    for y in range(h):
        fila = [x for x in range(w) if fuente[x, y][3]]
        if not fila:
            continue
        x0, x1 = fila[0], fila[-1]
        for x in range(x0, x1 + 1):
            borde = x in (x0, x1) or y in (0, h - 1) or not fuente[x, y - 1][3] or not fuente[x, y + 1][3]
            if borde and fuente[x, y][:3] == OSCURO:
                px[x, y] = OSCURO + (255,)
            elif fuente[x, y][:3] == ROJO_SOMBRA:
                px[x, y] = GRIS_SOMBRA + (255,)
            elif fuente[x, y][:3] == ROJO_BRILLO:
                px[x, y] = GRIS_BRILLO + (255,)
            else:
                px[x, y] = GRIS + (255,)
    vacia.save(UI / "queja_vacia.png")
    print("Listo: queja_llena.png y queja_vacia.png en", UI)


if __name__ == "__main__":
    main()
