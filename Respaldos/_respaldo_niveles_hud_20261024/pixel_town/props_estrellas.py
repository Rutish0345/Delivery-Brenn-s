"""Marcador del punto de entrega: 6 cuadros de destellos de 48x48 (estrellas_entrega_0..5).

Como la referencia "Sparkling Pixel" (Assets/Sprits/referencias/estrellitas.jpg): tres destellos de 4 puntas
de distinto tamaño (el grande al centro) con chispitas "+" alrededor, en dorado #FFD166 con núcleo crema
#FFF1D0 y un par de píxeles rosa #FF6FA5. De un cuadro al siguiente los destellos crecen, se encogen y se
alternan (cuando el grande late, los chicos se apagan), y las chispitas se prenden por turnos.
Sombra suave de un píxel abajo a la derecha. Sin flecha ni círculo.
Van al atlas sin recortar (48x48 exactos) con el pivote al centro; Unity los usa en el marcador hijo de cada
PuntoEntrega con BrilloEstrellas.
"""
import numpy as np

from props_negocios import PIVOTES

LADO = 48
N_CUADROS = 6
NOMBRES = [f'estrellas_entrega_{i}' for i in range(N_CUADROS)]

DORADO = (0xFF, 0xD1, 0x66, 255)
DORADO_OSC = (0xE0, 0x9A, 0x3A, 255)
CREMA = (0xFF, 0xF1, 0xD0, 255)
ROSA = (0xFF, 0x6F, 0xA5, 255)
SOMBRA = (0x2A, 0x1F, 0x40, 80)

# (centro x, centro y, largo de cada brazo por cuadro); 0 = apagado
GRANDE = (24, 23, (9, 11, 13, 12, 10, 8))
MEDIANO = (11, 12, (6, 4, 0, 3, 5, 7))
CHICO = (37, 35, (3, 5, 6, 4, 0, 2))
# chispitas "+": (x, y, cuadros en que se ven, tamaño 1 = punto, 2 = cruz de 3x3)
CHISPAS = [
    (39, 10, (0, 1, 2), 2), (8, 32, (1, 2, 3), 2), (25, 42, (3, 4, 5), 2), (42, 22, (4, 5, 0), 2),
    (17, 4, (2, 3), 1), (4, 21, (5, 0), 1), (33, 4, (4,), 1), (13, 41, (0, 5), 1), (45, 39, (1, 2), 1),
    (30, 15, (5,), 1), (16, 30, (2,), 1),
]
# un par de píxeles rosa por cuadro: el centro de una chispa encendida
ROSAS = {0: [(39, 10)], 1: [(8, 32)], 2: [(39, 10), (17, 4)], 3: [(25, 42)], 4: [(42, 22)], 5: [(25, 42), (4, 21)]}


def destello(cx, cy, r):
    """Píxeles de un destello de 4 puntas: dos barras que se afinan hacia las puntas. Devuelve
    {(x, y): 'cuerpo' | 'nucleo' | 'orilla'}."""
    if r <= 0:
        return {}
    k = max(1, r // 4)                    # medio grosor en el centro
    px = {}
    for d in range(-r, r + 1):
        hw = int(round(k * (1 - abs(d) / (r + 1)) ** 1.6))
        for t in range(-hw, hw + 1):
            px[(cx + d, cy + t)] = 'cuerpo'
            px[(cx + t, cy + d)] = 'cuerpo'
    if r >= 6:                            # el centro se abulta un poco en diagonal
        for dx in (-1, 1):
            for dy in (-1, 1):
                px[(cx + dx * k, cy + dy * k)] = 'cuerpo'
    # núcleo crema: el centro y el primer tramo de cada brazo
    largo_nucleo = max(1, r // 3)
    for d in range(-largo_nucleo, largo_nucleo + 1):
        px[(cx + d, cy)] = 'nucleo'
        px[(cx, cy + d)] = 'nucleo'
    if r >= 8:
        for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            px[(cx + dx, cy + dy)] = 'nucleo'
    # las puntas en dorado oscuro para que el destello se lea sobre suelos claros
    if r >= 4:
        for dx, dy in ((r, 0), (-r, 0), (0, r), (0, -r)):
            px[(cx + dx, cy + dy)] = 'orilla'
    return px


def chispa(x, y, tam):
    if tam == 1:
        return {(x, y): 'cuerpo'}
    return {(x, y): 'nucleo', (x - 1, y): 'cuerpo', (x + 1, y): 'cuerpo', (x, y - 1): 'cuerpo', (x, y + 1): 'cuerpo'}


def cuadro(i):
    a = np.zeros((LADO, LADO, 4), np.uint8)
    px = {}
    for (cx, cy, radios) in (MEDIANO, CHICO, GRANDE):   # el grande al final: queda encima
        px.update(destello(cx, cy, radios[i]))
    for (x, y, cuadros, tam) in CHISPAS:
        if i in cuadros:
            for p, v in chispa(x, y, tam).items():
                px.setdefault(p, v)
    colores = {'cuerpo': DORADO, 'nucleo': CREMA, 'orilla': DORADO_OSC}
    # sombra suave: un píxel abajo a la derecha de todo lo que brilla
    for (x, y) in px:
        sx, sy = x + 1, y + 1
        if (sx, sy) not in px and 0 <= sx < LADO and 0 <= sy < LADO:
            a[sy, sx] = SOMBRA
    for (x, y), v in px.items():
        assert 0 <= x < LADO and 0 <= y < LADO, ('destello fuera del cuadro', i, x, y)
        a[y, x] = colores[v]
    for (x, y) in ROSAS[i]:
        assert (x, y) in px, ('el rosa cae fuera de una chispa', i, x, y)
        a[y, x] = ROSA
    return a


def build_estrellas():
    """{nombre: arreglo RGBA 48x48}; registra el pivote al centro de cada cuadro."""
    cuadros = {n: cuadro(i) for i, n in enumerate(NOMBRES)}
    for n in NOMBRES:
        PIVOTES[n] = 0.5
    # que de verdad titile: ningún cuadro igual al siguiente
    for a, b in zip(NOMBRES, NOMBRES[1:] + NOMBRES[:1]):
        assert not np.array_equal(cuadros[a], cuadros[b]), ('cuadros repetidos', a, b)
    return cuadros


if __name__ == '__main__':
    import os
    from PIL import Image
    tira = np.concatenate([np.pad(c, ((4, 4), (4, 4), (0, 0))) for c in build_estrellas().values()], axis=1)
    fondo = Image.new('RGBA', (tira.shape[1], tira.shape[0]), (0x6E, 0xA8, 0x6A, 255))   # sobre pasto
    fondo.alpha_composite(Image.fromarray(tira))
    fondo = fondo.resize((fondo.width * 6, fondo.height * 6), Image.NEAREST)
    ruta = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'residencial', 'estrellas_vista_previa.png')
    fondo.save(ruta)
    print(ruta)
