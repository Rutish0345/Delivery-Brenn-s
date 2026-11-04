"""Props nuevos del Residencial (octubre 2026): semáforo de 4 estados, juegos infantiles, jardinera y puesto de
elotes. Mismo estilo que props_residencial_extra.py: luz desde arriba-izquierda, sin contornos negros, sombra
plana de un solo tono y la paleta P de generate_town (más los tres colores del semáforo).
"""
import random

from generate_town import P, disc, shade
from generate_unity import Sprite, shadow_ellipse

# luces del semáforo (encendida, halo) y su vidrio apagado
P.setdefault('pink_l', (0xFF, 0x9C, 0xC4))
P.setdefault('sem_verde', (0x7B, 0xD8, 0x8F))
P.setdefault('sem_ambar', (0xFF, 0xD1, 0x66))
P.setdefault('sem_rojo', (0xFF, 0x5D, 0x73))
P.setdefault('sem_verde_l', (0xC4, 0xF2, 0xCD))
P.setdefault('sem_ambar_l', (0xFF, 0xEC, 0xBF))
P.setdefault('sem_rojo_l', (0xFF, 0xB4, 0xBE))
P.setdefault('sem_verde_o', (0x3E, 0x5E, 0x4C))
P.setdefault('sem_ambar_o', (0x6B, 0x5A, 0x3E))
P.setdefault('sem_rojo_o', (0x6B, 0x3B, 0x4A))

ESTADOS_SEMAFORO = ('verde', 'ambar', 'rojo', 'apagado')
SEMAFORO_W, SEMAFORO_H = 32, 64


def make_semaforo(estado):
    """32x64, pivote abajo-centro: poste gris con la caja de tres luces arriba (rojo, ámbar, verde)."""
    sp = Sprite(SEMAFORO_W, SEMAFORO_H)
    base = SEMAFORO_H - 1
    shadow_ellipse(sp, 17, base, 8, 2)
    sp.rect(11, base - 4, 10, 5, 'lamp')            # pie
    sp.rect(11, base - 4, 10, 1, 'lamp_l')
    sp.rect(19, base - 3, 2, 4, 'lamp_d')
    for y in range(30, base - 4):                     # poste
        for i, c in enumerate(('lamp_l', 'lamp', 'lamp', 'lamp_d')):
            sp.px(14 + i, y, c)
    sp.rect(13, 44, 6, 1, 'lamp_l')
    sp.rect(13, 45, 6, 1, 'lamp_d')
    # caja
    sp.rect(8, 1, 16, 30, 'lamp_d')
    sp.rect(9, 2, 14, 28, 'lamp')
    sp.rect(9, 2, 14, 1, 'lamp_l')
    sp.rect(9, 2, 1, 28, 'lamp_l')
    sp.rect(22, 3, 1, 27, 'lamp_d')
    sp.rect(11, 0, 10, 1, 'lamp_d')
    for i, color in enumerate(('rojo', 'ambar', 'verde')):
        cy = 7 + i * 9
        sp.rect(9, cy - 4, 14, 1, 'lamp_d')          # visera
        encendida = estado == color
        filas = ('  ###  ', ' ##### ', '#######', '#######', '#######', ' ##### ', '  ###  ')
        for j, fila in enumerate(filas):                # lente redonda de 7x7
            for i2, ch in enumerate(fila):
                if ch == '#':
                    sp.px(13 + i2, cy - 3 + j, f'sem_{color}' if encendida else f'sem_{color}_o')
        if encendida:
            for (x, y) in ((14, cy - 2), (15, cy - 2), (14, cy - 1)):
                sp.px(x, y, f'sem_{color}_l')
            for (x, y) in ((11, cy), (21, cy)):         # halo
                sp.px(x, y, f'sem_{color}_o')
        else:
            sp.px(14, cy - 2, 'lamp_l')
    return sp


def make_swings():
    """Columpios: marco de madera en A con dos asientos colgando de cadenas."""
    sp = Sprite(76, 58)
    base = 57
    shadow_ellipse(sp, 39, base, 34, 3)
    for xc in (9, 67):                                # patas en A: dos postes que se juntan arriba
        for y in range(5, base):
            abre = (y - 5) * 7 // (base - 5)
            for x in (xc - abre, xc + abre):
                sp.px(x - 1, y, 'wood_l')
                sp.px(x, y, 'wood')
                sp.px(x + 1, y, 'wood_d')
        sp.rect(xc - 5, 34, 11, 2, 'wood_d')           # refuerzo
    sp.rect(4, 3, 70, 4, 'wood')                      # travesaño
    sp.rect(4, 3, 70, 1, 'wood_l')
    sp.rect(4, 6, 70, 1, 'wood_dd')
    for cx, asiento, asiento_d in ((26, 'pink', 'pink_d'), (50, 'blue', 'blue_d')):
        for y in range(7, 40):                        # cadenas
            c = 'lamp_l' if y % 2 else 'lamp'
            sp.px(cx - 6, y, c)
            sp.px(cx + 6, y, c)
        sp.rect(cx - 8, 40, 17, 3, asiento)
        sp.rect(cx - 8, 40, 17, 1, 'cream_l')
        sp.rect(cx - 8, 42, 17, 1, asiento_d)
        for (x, y) in ((cx - 6, base - 3), (cx + 6, base - 3)):
            sp.shadow(x, y)
    return sp


def make_slide():
    """Resbaladilla: escalera a la izquierda, plataforma y la rampa amarilla bajando a la derecha."""
    sp = Sprite(64, 56)
    base = 55
    shadow_ellipse(sp, 33, base, 29, 3)
    for x in (4, 14):                                 # largueros de la escalera
        sp.rect(x, 10, 3, base - 10, 'lamp')
        sp.rect(x, 10, 1, base - 10, 'lamp_l')
        sp.rect(x + 2, 10, 1, base - 10, 'lamp_d')
    for y in range(16, base - 2, 6):                  # peldaños
        sp.rect(7, y, 7, 2, 'lamp_l')
        sp.rect(7, y + 1, 7, 1, 'lamp_d')
    sp.rect(2, 8, 20, 4, 'roof')                      # plataforma
    sp.rect(2, 8, 20, 1, 'roof_l')
    sp.rect(2, 11, 20, 1, 'roof_dd')
    sp.rect(3, 2, 2, 6, 'lamp')                       # barandal
    sp.rect(19, 2, 2, 6, 'lamp')
    sp.rect(3, 2, 18, 2, 'lamp_l')
    for i in range(0, 40):                            # rampa
        x = 21 + i
        y = 9 + int(i * 0.95)
        if y > base - 4:
            break
        sp.rect(x, y, 2, 6, 'gold')
        sp.px(x, y, 'gold_l')
        sp.px(x + 1, y, 'gold_l')
        sp.px(x, y + 5, 'gold_d')
        sp.px(x, y + 6, 'gold_d')
    sp.rect(57, base - 5, 5, 4, 'gold_d')             # remate
    sp.rect(57, base - 5, 5, 1, 'gold')
    sp.rect(56, base - 8, 3, 8, 'lamp')               # soporte bajo la rampa
    sp.rect(40, 28, 3, base - 28, 'lamp')
    sp.rect(40, 28, 1, base - 28, 'lamp_l')
    return sp


def make_planter(seed=5):
    """Jardinera de piedra con flores rosas y crema."""
    rng = random.Random(seed)
    sp = Sprite(46, 30)
    base = 29
    shadow_ellipse(sp, 24, base, 22, 2)
    sp.rect(1, 16, 44, 13, 'stone')
    sp.rect(1, 16, 44, 2, 'stone_l')
    sp.rect(1, 16, 2, 13, 'stone_l')
    sp.rect(42, 18, 3, 11, 'stone_d')
    sp.rect(1, base - 1, 44, 1, 'stone_d')
    for x in range(8, 42, 9):
        sp.px(x, 22, 'stone_d')
        sp.px(x + 4, 25, 'stone_d')
    sp.rect(3, 14, 40, 3, 'gnd')                      # tierra
    for x in range(3, 43):                            # follaje bajo
        alto = 4 + int(2 * abs(((x * 7) % 11) - 5) / 5)
        for y in range(15 - alto, 15):
            sp.px(x, y, 'leaf_l' if y == 15 - alto else ('leaf' if (x + y) % 3 else 'leaf_d'))
    for k in range(14):                               # flores de 2x2 rosas y crema
        x, y = rng.randrange(4, 41), rng.randrange(6, 13)
        c, cl = (('pink', 'pink_l'), ('cream_l', 'cream'), ('gold', 'gold_l'))[k % 3]
        sp.rect(x, y, 2, 2, c)
        sp.px(x, y, cl)
    return sp


def make_corn_cart():
    """Puesto de elotes: carrito con ruedas, olla, elotes y sombrilla de rayas rosa y crema."""
    sp = Sprite(58, 64)
    base = 63
    shadow_ellipse(sp, 30, base, 26, 3)
    for cx in (14, 44):                               # ruedas
        shade(sp, set(disc(cx, base - 6, 5)), 'lamp_d', 'lamp', 'lamp_l')
        sp.px(cx, base - 6, 'lamp_d')
    sp.rect(4, 36, 50, 18, 'cream')                   # caja del carrito
    sp.rect(4, 36, 50, 2, 'cream_l')
    sp.rect(4, 36, 2, 18, 'cream_l')
    sp.rect(51, 38, 3, 16, 'cream_d')
    sp.rect(4, 53, 50, 1, 'cream_d')
    sp.rect(8, 42, 42, 7, 'roof')                     # franja con "ELOTES" sugerido
    sp.rect(8, 42, 42, 1, 'roof_l')
    for x in range(11, 47, 6):
        sp.rect(x, 44, 3, 3, 'gold')
    sp.rect(7, 30, 14, 6, 'lamp')                     # olla
    sp.rect(7, 30, 14, 1, 'lamp_l')
    sp.rect(6, 29, 16, 1, 'lamp_d')
    for i, x in enumerate(range(26, 50, 5)):          # elotes
        sp.rect(x, 31, 4, 5, 'gold')
        sp.px(x, 31, 'gold_l')
        sp.px(x + 3, 35, 'gold_d')
        sp.rect(x + 1, 30, 2, 1, 'leaf_l' if i % 2 else 'leaf')
    sp.rect(28, 12, 2, 18, 'lamp')                    # palo de la sombrilla
    for y in range(2, 14):                            # sombrilla
        medio = 26 - (13 - y) * 2 if y < 13 else 26
        x0, x1 = max(1, 29 - (y + 1) * 2), min(57, 29 + (y + 1) * 2)
        for x in range(x0, x1):
            c = 'pink' if ((x - 29) // 6) % 2 == 0 else 'cream_l'
            sp.px(x, y, c)
        del medio
    for x in range(5, 54):
        sp.px(x, 14, 'pink_d' if ((x - 29) // 6) % 2 == 0 else 'cream_d')
    sp.rect(28, 0, 2, 2, 'lamp_d')
    return sp


def build_props_relleno():
    """Props que sí se recortan al contenido (los semáforos van aparte, a 32x64 exactos)."""
    return {
        'columpios': make_swings(),
        'resbaladilla': make_slide(),
        'jardinera_flores': make_planter(),
        'puesto_elotes': make_corn_cart(),
    }


def build_semaforos():
    return {f'semaforo_{e}': make_semaforo(e).a for e in ESTADOS_SEMAFORO}
