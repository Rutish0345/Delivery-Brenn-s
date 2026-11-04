"""Baches de calle (octubre 2026) para los niveles 2 y 3, con la paleta P y la luz arriba-izquierda del proyecto:
  - bache_chico (32x20) y bache_grande (48x28): hoyo de borde roto (trozos de asfalto claro levantados, alguno
    suelto alrededor), pared de adentro en sombra arriba-izquierda y con luz abajo-derecha, fondo #2A1F40 y grava;
  - bache_charco_chico / bache_charco_grande: el mismo hoyo lleno de agua azul-teal #5ED3D1 con orilla más oscura
    y reflejos claros (nivel 3, con lluvia);
  - particula_polvo (4x4) y particula_gota (3x4): lo que salta al pasar la moto.
Son calcomanías del suelo (sin sombra proyectada) con el pivote al centro: Bache.cs pone el trigger encima.
"""
import math
import random

import numpy as np

from generate_town import P

FONDO = (0x2A, 0x1F, 0x40)
AGUA = (0x5E, 0xD3, 0xD1)
AGUA_D = (0x3B, 0xA0, 0xA8)
AGUA_L = (0xC8, 0xF4, 0xEE)
TAMANOS = {'chico': (32, 20), 'grande': (48, 28)}


def _forma(w, h, rng, margen):
    """Radio irregular (suma de senos con fase al azar) de una elipse que deja "margen" px para el borde roto."""
    fases = [rng.uniform(0, 2 * math.pi) for _ in range(3)]

    def r(t):
        return 1 + .10 * math.sin(3 * t + fases[0]) + .07 * math.sin(5 * t + fases[1]) + .05 * math.sin(8 * t + fases[2])
    cx, cy = (w - 1) / 2, (h - 1) / 2
    ax, ay = w / 2 - margen, h / 2 - margen
    d = np.zeros((h, w))
    ang = np.zeros((h, w))
    for y in range(h):
        for x in range(w):
            dx, dy = (x - cx) / ax, (y - cy) / ay
            t = math.atan2(dy, dx)
            d[y, x] = math.hypot(dx, dy) / r(t)
            ang[y, x] = t
    return d, ang


def bache(tam, agua=False, semilla=0):
    w, h = TAMANOS[tam]
    rng = random.Random(semilla + (7 if tam == 'grande' else 3))
    a = np.zeros((h, w, 4), np.uint8)
    margen = 3 if tam == 'grande' else 2.5
    d, ang = _forma(w, h, rng, margen)

    def pon(x, y, c):
        if 0 <= x < w and 0 <= y < h:
            a[y, x, :3] = c
            a[y, x, 3] = 255
    hoyo = d <= 1.0
    for y in range(h):
        for x in range(w):
            if not hoyo[y, x]:
                continue
            # posición en la pared: cerca de la orilla, arriba-izquierda en sombra, abajo-derecha con luz
            orilla = d[y, x] > .72
            luz = math.cos(ang[y, x] - math.atan2(1, 1))       # +1 hacia abajo-derecha (y crece hacia abajo)
            if agua:
                if orilla and luz < -.2:
                    c = AGUA_D
                elif orilla and luz > .5:
                    c = P['hole']
                else:
                    c = AGUA
            else:
                if orilla and luz > .35:
                    c = P['hole']                                # pared con luz
                elif orilla and luz < -.3:
                    c = P['hole_d']
                else:
                    c = FONDO
            pon(x, y, c)
    # grava en el fondo (seca) o reflejos (agua)
    dentro = [(x, y) for y in range(h) for x in range(w) if d[y, x] < .65]
    if agua:
        cx, cy = w // 2, h // 2
        for k, (ox, oy, largo) in enumerate(((-w // 6, -h // 6, w // 5), (w // 10, h // 8, w // 8))):
            for i in range(largo):
                x, y = cx + ox + i, cy + oy - i // 3
                if d[y, x] < .8:
                    pon(x, y, AGUA_L)
        for x, y in rng.sample(dentro, max(2, len(dentro) // 60)):
            pon(x, y, (0x9E, 0xE8, 0xE2))
    else:
        for x, y in rng.sample(dentro, max(3, len(dentro) // 30)):
            pon(x, y, rng.choice([P['stone_d'], P['stone_d'], P['road_d'], P['stone']]))
    # borde roto: anillo de trozos de asfalto claro levantados, con huecos, y la sombra del filo
    escala = min(w / 2 - margen, h / 2 - margen)               # px por unidad de d (aprox.)
    anillo = [(x, y) for y in range(h) for x in range(w) if 1.0 < d[y, x] <= 1.0 + 1.7 / escala]
    for x, y in anillo:
        if rng.random() < .22:
            continue                                             # huecos: el borde se ve roto
        luz = math.cos(ang[y, x] - math.atan2(1, 1))
        pon(x, y, P['road_l'] if luz < .2 else P['road'])
    # trozos y grava sueltos alrededor
    fuera = [(x, y) for y in range(h) for x in range(w) if 1.0 + 1.7 / escala < d[y, x] < 1.0 + 3.2 / escala]
    for x, y in rng.sample(fuera, min(len(fuera), 6 if tam == 'grande' else 4)):
        pon(x, y, rng.choice([P['road_l'], P['stone'], P['stone_d']]))
    return a


def particula_polvo():
    a = np.zeros((4, 4, 4), np.uint8)
    for (x, y), c in {(1, 0): P['stone_l'], (2, 0): P['stone_l'], (0, 1): P['stone_l'], (1, 1): P['stone'],
                      (2, 1): P['stone'], (3, 1): P['stone_d'], (0, 2): P['stone'], (1, 2): P['stone'],
                      (2, 2): P['stone_d'], (3, 2): P['stone_d'], (1, 3): P['stone_d'], (2, 3): P['stone_d']}.items():
        a[y, x, :3] = c
        a[y, x, 3] = 255
    return a


def particula_gota():
    a = np.zeros((4, 3, 4), np.uint8)
    for (x, y), c in {(1, 0): AGUA_L, (0, 1): AGUA_L, (1, 1): AGUA, (2, 1): AGUA, (0, 2): AGUA, (1, 2): AGUA,
                      (2, 2): AGUA_D, (1, 3): AGUA_D}.items():
        a[y, x, :3] = c
        a[y, x, 3] = 255
    return a


def build_baches():
    """Sprites (RGBA) y sus pivotes (centro)."""
    sprites = {
        'bache_chico': bache('chico'),
        'bache_grande': bache('grande'),
        'bache_charco_chico': bache('chico', agua=True),
        'bache_charco_grande': bache('grande', agua=True),
        'particula_polvo': particula_polvo(),
        'particula_gota': particula_gota(),
    }
    return sprites, {n: [0.5, 0.5] for n in sprites}


if __name__ == '__main__':
    from PIL import Image
    s, _ = build_baches()
    ancho = sum(v.shape[1] for v in s.values()) + 8 * (len(s) + 1)
    hoja = np.zeros((40, ancho, 4), np.uint8)
    hoja[:, :, :3] = P['road']
    hoja[:, :, 3] = 255
    x = 8
    for n, v in s.items():
        h, w = v.shape[:2]
        m = v[:, :, 3] > 0
        hoja[6:6 + h, x:x + w][m] = v[m]
        x += w + 8
    Image.fromarray(hoja).resize((ancho * 6, 240), Image.NEAREST).save('unity/comercial/baches_vista.png')
    print({n: v.shape for n, v in s.items()})
