"""Hoja de la repartidora (octubre 2026; rediseño: cabello suelto y suéter lavanda): el mismo formato exacto que la del
repartidor (celdas de 56x80, 5 columnas x 3 filas: frente, espalda, lado derecho; columna 0 quieta, 1-4 caminando;
pivote abajo-centro) porque sale de los mismos cuadros de generar_repartidor_sheet.py (mismo ciclo de caminata y mismo
rebote) con su ropa y su cabello encima (vestir()):
  - suéter lavanda tejido en lugar de la chamarra morada: cuello redondo, mangas holgadas hasta la muñeca, moño
    dorado en el pecho y punto en 2 tonos de lavanda; la mochila rosa no cambia
  - falda plisada morada hasta la rodilla en lugar del pantalón, medias largas oscuras y botitas con suela crema
    (poner_falda; la misma silueta en los 5 cuadros, sube y baja con el rebote)
  - cabello castaño suelto a la altura de los hombros (termina sobre el cuello del suéter), ~4 px por lado y pegado
    a la cabeza; contorno #2A1F40 continuo y 3 tonos (sombra, base, brillo) en mechones de 4 px:
      frente: raya ladeada con el flequillo bajo la gorra, detrás de la oreja izquierda (cara despejada), puntas
              curvadas hacia dentro
      espalda: sobre la parte alta de la espalda; la mochila se ve por los lados y por debajo
      lado (mira a la derecha): detrás del hombro, con un mechón corto al frente de la oreja
    sube y baja con el rebote del torso (el mismo bob de cada columna, sin desfase)
generar_motos.py --chica usa vestir() con viento (en moto, el cabello ondea hacia atrás) y generar_intro.py lee la
hoja de Assets, así que con regenerar todo se queda el diseño nuevo.
Escribe repartidora_sheet.png (con la mochila rosa), repartidora_sheet_sin_mochila.png y repartidora_sheet.json;
con --unity también copia la hoja a Assets/Sprits/Personajes con su .meta.
Uso: python generar_repartidora_sheet.py [carpeta] [--unity]
Coordenadas: x, y del cuadro de 56x80 (y hacia abajo), las mismas del repartidor."""
import json
import os
import re
import shutil
import sys
import uuid

from PIL import Image

import generar_repartidor_sheet as base

W, H = base.W, base.H
UNITY = r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Personajes'

CONTORNO = (0x2A, 0x1F, 0x40, 255)
# cabello castaño (los tonos del pelo del repartidor y un brillo)
C_SOMBRA = (0x32, 0x12, 0x12, 255)
C_BASE = (0x41, 0x20, 0x1A, 255)
C_MEDIO = (0x5B, 0x35, 0x2E, 255)
C_BRILLO = (0x7B, 0x4A, 0x38, 255)
NEGRO = (0, 0, 0, 255)
RUBOR = (0xF2, 0xA0, 0xB0, 255)
K = {'o': CONTORNO, 's': C_BASE, 'b': C_MEDIO, 'h': C_BRILLO}   # sombra, base y brillo del cabello

# suéter lavanda: 2 tonos del punto, luz, sombra y la línea de los pliegues; moño dorado
L_LUZ = (0xE2, 0xD6, 0xFA, 255)
L_BASE = (0xC9, 0xB7, 0xF0, 255)
L_PUNTO = (0xB8, 0xA4, 0xE6, 255)
L_SOMBRA = (0x96, 0x80, 0xCC, 255)
L_LINEA = (0x6F, 0x5A, 0xA8, 255)
MONO = (0xFF, 0xD1, 0x66, 255)
MONO_OSC = (0xE0, 0x9A, 0x3A, 255)
S = {'o': CONTORNO, 'L': L_LUZ, 'B': L_BASE, 'P': L_PUNTO, 's': L_SOMBRA, 'l': L_LINEA, 'G': MONO, 'g': MONO_OSC}

COLORES_NUEVOS = {CONTORNO, C_SOMBRA, C_BASE, C_MEDIO, C_BRILLO, NEGRO, RUBOR,
                  L_LUZ, L_BASE, L_PUNTO, L_SOMBRA, L_LINEA, MONO, MONO_OSC}


def _rgb(h):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16))


# chamarra y playera del repartidor → suéter (tono de destino); solo en el torso (TORSO), así la cara no cambia
SUETER = {
    # morados de la chamarra
    '#A979D3': L_LUZ, '#9562C6': L_LUZ, '#7E4FCB': L_BASE, '#653AB1': L_PUNTO, '#6D1EB9': L_SOMBRA,
    '#561DB9': L_SOMBRA, '#442A92': L_SOMBRA, '#341074': L_LINEA,
    '#18064E': CONTORNO, '#1F0A5F': CONTORNO, '#1D0646': CONTORNO,
    # botones dorados → tejido
    '#FCD560': L_BASE,
    # playera blanca (asoma al frente y de lado) → el mismo suéter, cerrado
    '#F2EDE9': L_BASE, '#FFFFFF': L_BASE, '#E3D5C7': L_BASE, '#D5CABF': L_PUNTO, '#D1D1D1': L_PUNTO,
    '#A3A0AE': L_SOMBRA, '#9BADB7': L_SOMBRA,
}
SUETER = {_rgb(k): v for k, v in SUETER.items()}
TORSO = (31, 50)   # filas del suéter (sin rebote): de los hombros a los puños

# moño en el pecho (frente, centrado en x 27) y asomando de lado (x 29)
MONO_FRENTE = ["gG.Gg", ".gGg.", "gG.Gg"]
MONO_LADO = ["Gg", "gG"]

# ---- cabello (afinado: silueta limpia, a la altura de los hombros, ~4 px por lado) ----
# Cada vista es una máscara {fila: [(x0, x1), ...]} (sin rebote); el contorno #2A1F40 sale solo en el borde que da al
# aire o al cuerpo (no hacia la cara ni hacia la gorra), así no quedan pixeles sueltos. Tonos por mechones verticales
# de 4 px: sombra, base, base, brillo. Termina en la fila 33, justo arriba del cuello del suéter (fila 34).
MECHON = "sbbh"
PELO_CHICO = {_rgb(h): t for h, t in (          # el pelo del repartidor que asoma bajo la gorra → los 3 tonos
    ('#250B11', 'o'), ('#190F0E', 'o'), ('#321212', 's'), ('#3D1E1F', 's'), ('#41201A', 's'),
    ('#492D28', 'b'), ('#4C2223', 'b'), ('#4F3333', 'b'), ('#583131', 'b'), ('#5B352E', 'b'), ('#885B53', 'h'))}


def _filas(*tramos):
    """tramos: (y0, y1, [(x0, x1), ...]) → {y: [(x0, x1), ...]}"""
    m = {}
    for y0, y1, xs in tramos:
        for y in range(y0, y1 + 1):
            m.setdefault(y, []).extend(xs)
    return m


# frente: raya ladeada a la izquierda (se ve la frente en x 19-20), flequillo hacia la derecha bajo la visera; a la
# izquierda el cabello va detrás de la oreja (x 16-18 queda a la vista) y a la derecha cae junto a la cara
FRENTE = _filas(
    (20, 20, [(13, 35)]),
    (21, 21, [(12, 18), (21, 35)]),
    (22, 22, [(12, 15), (21, 24), (32, 36)]),   # x 21..24: el flequillo baja junto a la raya, sobre la ceja
    (23, 25, [(12, 15), (33, 36)]),
    (26, 28, [(12, 17), (32, 36)]),
    (29, 31, [(12, 16), (32, 36)]),
    (32, 32, [(13, 16), (33, 36)]),     # puntas que se curvan hacia dentro
    (33, 33, [(14, 15), (34, 35)]),
)
# espalda: sobre la parte alta de la espalda; sobre la mochila (x 18..33) es más angosto para que se vean sus lados
ESPALDA = _filas(
    (20, 27, [(13, 35)]),
    (28, 30, [(15, 33)]),
    (31, 31, [(19, 31)]),
)
PUNTAS_ESPALDA = [(x, 32 if ((x - 19) // 3) % 2 == 0 else 33) for x in range(19, 32)]   # ondas: cada mechón acaba en 32 o 33
# lado (mira a la derecha): detrás del hombro, y un mechón corto al frente de la oreja
LADO = _filas(
    (20, 27, [(13, 19), (25, 28)]),
    (28, 28, [(13, 18), (25, 27)]),
    (29, 31, [(13, 18)]),
)
PUNTAS_LADO = [(x, 32 if x % 2 else 33) for x in range(13, 18)]
# en moto, de lado: el mismo cabello recogido en volumen hacia atrás, sin exagerar (2 cuadros que ondean)
VIENTO = [
    _filas((20, 20, [(12, 19)]), (21, 21, [(10, 19)]), (22, 22, [(8, 19)]), (23, 23, [(7, 18)]),
           (24, 24, [(8, 17)]), (25, 25, [(10, 16)]), (21, 27, [(25, 28)])),
    _filas((20, 20, [(12, 19)]), (21, 21, [(9, 19)]), (22, 22, [(7, 19)]), (23, 23, [(8, 18)]),
           (24, 24, [(9, 17)]), (25, 25, [(11, 16)]), (21, 27, [(25, 28)])),
]


def _pintar(px, filas, x0, y0, paleta=K):
    for j, fila in enumerate(filas):
        for i, ch in enumerate(fila):
            if ch == '.':
                continue
            x, y = x0 + i, y0 + j
            if 0 <= x < W and 0 <= y < H:
                px[x, y] = paleta[ch]


def poner_sueter(im, vista, col):
    """La chamarra y la playera pasan a suéter lavanda: tonos por color (solo en las filas del torso), punto en 2 tonos
    (columnas alternas del tono base), cuello redondo, mangas holgadas que cubren la muñeca y el moño dorado."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    y0, y1 = TORSO[0] + bob, TORSO[1] + bob
    for y in range(y0, y1 + 1):
        for x in range(W):
            p = px[x, y]
            if p[3] == 0:
                continue
            nuevo = SUETER.get(p[:3])
            if nuevo is None:
                continue
            if nuevo == L_BASE and x % 2:        # punto: canalé vertical, fijo en x (no parpadea al caminar)
                nuevo = L_PUNTO
            px[x, y] = (nuevo[0], nuevo[1], nuevo[2], p[3])
    lavanda = {L_LUZ, L_BASE, L_PUNTO, L_SOMBRA, L_LINEA}
    piel = {_rgb(h) for h in ('#90482B', '#FFDCC0', '#F8C2A8', '#C57E5E', '#B06E50', '#D3AB92', '#E98594')}
    # puños holgados: la fila de arriba de cada mano (contorno de piel justo bajo la manga) se vuelve manga
    for y in range(y1 - 2, y1 + 3):
        for x in range(W):
            if px[x, y][3] and px[x, y][:3] in piel and y > 0 and px[x, y - 1][:4] in lavanda | {CONTORNO}:
                arriba = px[x, y - 1][:4]
                if arriba in lavanda:
                    px[x, y] = L_SOMBRA if px[x, y][:3] != (0x90, 0x48, 0x2B) else CONTORNO
    # mangas holgadas: 1 px más hacia fuera en el antebrazo (de frente y de espaldas)
    if vista in (0, 1):
        for y in range(y0 + 13, y1 + 1):
            for lado in (1, -1):
                xs = range(W) if lado == 1 else range(W - 1, -1, -1)
                borde = next((x for x in xs if px[x, y][3] == 255), None)
                if borde is None or px[borde, y][:4] not in lavanda | {CONTORNO}:
                    continue
                afuera = borde - lado
                if 0 <= afuera < W and px[afuera, y][3] < 255:
                    px[afuera, y] = CONTORNO
                    px[borde, y] = L_SOMBRA
    # cuello redondo (frente): el borde de la playera se cierra en una curva de punto bajo el cuello
    if vista == 0:
        for x, y in ((23, 33), (24, 34), (25, 34), (26, 34), (27, 34), (28, 34), (29, 33)):
            px[x, y + bob] = L_LUZ if x in (25, 26, 27) else L_SOMBRA
        _pintar(px, MONO_FRENTE, 25, 37 + bob, S)
    elif vista == 2:
        _pintar(px, MONO_LADO, 29, 36 + bob, S)
    return im


def _mascara(filas, puntas=()):
    """Los píxeles de la máscara; puntas: (x, fila final) alarga esa columna hasta su fila final."""
    m = {(x, y) for y, tramos in filas.items() for x0, x1 in tramos for x in range(x0, x1 + 1)}
    for x, fin in puntas:
        m |= {(x, y) for y in range(20, fin + 1)}
    return m


def _cabello(px, mascara, bob):
    """Pinta la máscara (corrida por el rebote) con mechones de 3 tonos y contorno en el borde exterior."""
    for x, y in mascara:
        yy = y + bob
        if not (0 <= x < W and 0 <= yy < H):
            continue
        borde = False
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            vx, vy = x + dx, y + dy
            if (vx, vy) in mascara or vy < 20:          # hacia la gorra no lleva contorno
                continue
            vecino = px[vx, vy + bob] if 0 <= vx < W and 0 <= vy + bob < H else (0, 0, 0, 0)
            if vecino[3] < 255 or vy >= 31:             # al aire o sobre el cuerpo: contorno; junto a la cara, no
                borde = True
                break
        px[x, yy] = CONTORNO if borde else K[MECHON[x % len(MECHON)]]


def poner_cabello(im, vista, col, viento=None):
    """vista: 0 frente, 1 espalda, 2 lado derecho; col: columna de la hoja (el cabello sube y baja 1 px con el mismo
    rebote del torso). viento (en moto, generar_motos.py): None a pie; 0 parada; 1 o 2 rodando."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    for y in range(19 + bob, 31 + bob):                 # el pelo del repartidor que asoma, a la paleta de 3 tonos
        for x in range(W):
            t = PELO_CHICO.get(px[x, y][:3]) if px[x, y][3] == 255 else None
            if t:
                px[x, y] = K[t]
    if vista == 0:
        _cabello(px, _mascara(FRENTE), bob)
        px[21, 23 + bob] = NEGRO                  # pestañas
        px[30, 23 + bob] = NEGRO
        for x in (20, 21, 30, 31):                # mejillas
            if px[x, 26 + bob][:3] == (0xDB, 0xB1, 0xB5):
                px[x, 26 + bob] = RUBOR
    elif vista == 1:
        _cabello(px, _mascara(ESPALDA, PUNTAS_ESPALDA), bob)
    elif viento:
        _cabello(px, _mascara(VIENTO[viento - 1]), bob)
    else:
        _cabello(px, _mascara(LADO, PUNTAS_LADO), bob)
    return im


# ---- falda plisada (octubre 2026: falda en lugar del pantalón) ----
# Morada (#8C52FF más oscuro) en 2 tonos: base y la línea de cada pliegue, que se abre hacia el bajo; contorno #2A1F40.
# Hasta la rodilla (bajo en la fila 61); debajo, medias largas oscuras (#2A1F40 con un tono más claro de relieve) y
# botitas oscuras con suela crema. La silueta es fija por vista (las mismas filas y columnas en los 5 cuadros) y sube y
# baja con el rebote del torso, igual que las piernas.
F_BASE = (0x74, 0x42, 0xD6, 255)
F_PLIEGUE = (0x45, 0x1F, 0x8C, 255)
MEDIA = (0x2A, 0x1F, 0x40, 255)
MEDIA_LUZ = (0x3F, 0x30, 0x5E, 255)
BOTA = (0x3B, 0x23, 0x38, 255)
BOTA_LUZ = (0x5C, 0x3A, 0x55, 255)
SUELA = (0xFF, 0xF1, 0xD0, 255)
COLORES_NUEVOS |= {F_BASE, F_PLIEGUE, MEDIA, MEDIA_LUZ, BOTA, BOTA_LUZ, SUELA}
FALDA_COLORES = {F_BASE, F_PLIEGUE}

# el pantalón y los zapatos del repartidor
PANTALON = {_rgb(h) for h in ('#353046', '#544A63', '#625775', '#3E3853', '#312A45', '#2A243A', '#201B31',
                              '#7D6F88', '#504060')}
PANTALON_LUZ = {_rgb(h) for h in ('#544A63', '#625775', '#7D6F88', '#504060')}
LINEA_PIERNA = _rgb('#0D0220')
ZAPATO = {_rgb(h) for h in ('#171413', '#231916', '#2E2620', '#190F0E')}
ZAPATO_LUZ = {_rgb('#2E2620')}

DOBLADILLO = 61     # la fila del bajo (sin rebote): la rodilla
# filas (sin rebote) → (x0, x1) de la falda; la cintura (49-51) solo asoma bajo el suéter, en el hueco del pantalón
FALDA = {
    0: _filas((49, 51, [(24, 30)]), (52, 53, [(17, 34)]), (54, 57, [(16, 35)]), (58, 61, [(15, 36)])),
    2: _filas((49, 51, [(28, 31)]), (52, 53, [(18, 31)]), (54, 55, [(18, 32)]), (56, 57, [(17, 32)]),
              (58, 61, [(16, 33)])),
}
FALDA[1] = {y: t for y, t in FALDA[0].items() if y >= 52}   # de espaldas la tapa el suéter hasta la fila 51
PLIEGUES = {0: 6, 1: 6, 2: 4}


def poner_falda(im, vista, col):
    """Cambia el pantalón por la falda plisada, las medias y las botitas (vista 0 frente, 1 espalda, 2 lado)."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    pierna = PANTALON | {LINEA_PIERNA}
    # 1) la falda: solo sobre el pantalón o el aire (nunca sobre el suéter ni las manos)
    pliegues = PLIEGUES[vista]
    for y, tramos in FALDA[vista].items():
        yy = y + bob
        for x0, x1 in tramos:
            ancho = x1 - x0 + 1
            for x in range(x0, x1 + 1):
                p = px[x, yy]
                if p[3] == 255 and p[:3] not in pierna:
                    continue
                if x in (x0, x1) and y >= 52 or y == DOBLADILLO:
                    c = CONTORNO
                elif y <= 52:
                    c = F_PLIEGUE                                       # pretina
                else:
                    k = (x - x0) * pliegues // ancho                     # pliegues que se abren hacia el bajo
                    c = F_PLIEGUE if x > x0 + 1 and (x - 1 - x0) * pliegues // ancho != k else F_BASE
                px[x, yy] = c
    # 2) medias: el resto del pantalón, afinado 1 px por fuera (sin tocar los tobillos junto al zapato)
    for y in range(DOBLADILLO + 1 + bob, H):
        fila = [px[x, y] for x in range(W)]
        x = 0
        while x < W:
            if fila[x][3] == 255 and fila[x][:3] in pierna:
                x1 = x
                while x1 + 1 < W and fila[x1 + 1][3] == 255 and fila[x1 + 1][:3] in pierna:
                    x1 += 1
                cerca = [px[i, j] for i in range(max(0, x - 1), min(W, x1 + 2)) for j in range(y, min(H, y + 3))]
                if x1 - x >= 6 and not any(c[3] == 255 and c[:3] in ZAPATO for c in cerca):
                    px[x, y] = px[x1, y] = (0, 0, 0, 0)
                    px[x + 1, y] = px[x1 - 1, y] = (*LINEA_PIERNA, 255)
                x = x1 + 1
            else:
                x += 1
    for y in range(DOBLADILLO + 1 + bob, H):
        for x in range(W):
            p = px[x, y]
            if p[3] == 255 and p[:3] in PANTALON:
                px[x, y] = MEDIA_LUZ if p[:3] in PANTALON_LUZ else MEDIA
    # 3) botitas: el zapato y 2 filas de caña encima, con la suela crema (no en las puntas del contorno)
    for x in range(W):
        zap = [y for y in range(H) if px[x, y][3] == 255 and px[x, y][:3] in ZAPATO]
        if not zap:
            continue
        for y in zap:
            px[x, y] = BOTA_LUZ if px[x, y][:3] in ZAPATO_LUZ else BOTA
        for y in range(zap[0] - 3, zap[0]):
            if px[x, y][:4] in (MEDIA, MEDIA_LUZ):
                px[x, y] = BOTA
        suelo = max(y for y in range(H) if px[x, y][3] == 255)
        if (px[x, suelo][:3] == LINEA_PIERNA and suelo > zap[-1]
                and px[x - 1, suelo][3] == 255 and px[x + 1, suelo][3] == 255):
            px[x, suelo] = SUELA
    return im


def vestir(im, vista, col, viento=None):
    """Suéter, falda y cabello de la repartidora sobre un cuadro del repartidor (con o sin mochila). En moto (viento)
    no lleva la falda de pie: generar_motos.py la dibuja sobre el asiento."""
    if viento is None:
        im = poner_falda(im, vista, col)
    return poner_cabello(poner_sueter(im, vista, col), vista, col, viento)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else os.path.dirname(os.path.abspath(__file__))
    frames = base.build_frames()
    hoja = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    sin_mochila = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    meta_frames = []
    nombres = ['frente', 'espalda', 'lado_derecho']
    permitidos = base.REF_COLORS | base.MOCHILA_COLORES | COLORES_NUEVOS
    for r, fila in enumerate(frames):
        for col, cv in enumerate(fila):
            im = base.to_image(cv)
            sin_mochila.paste(vestir(im.copy(), r, col), (col * W, r * H))
            con = vestir(base.poner_mochila(im.copy(), r, base.BOB_COLUMNA[col]), r, col)
            malos = {con.getpixel((x, y)) for x in range(W) for y in range(H) if con.getpixel((x, y))[3]} - permitidos
            malos = {m for m in malos if m[:3] + (255,) not in permitidos and m[:3] not in {c[:3] for c in permitidos}}
            assert not malos, (r, col, malos)
            hoja.paste(con, (col * W, r * H))
            meta_frames.append({
                'name': f'repartidora_{nombres[r]}_{"idle" if col == 0 else "walk_%d" % (col - 1)}',
                'row': r, 'col': col, 'x': col * W, 'y': r * H, 'w': W, 'h': H,
                'unity_rect_y': (2 - r) * H, 'pivot': {'x': 0.5, 'y': 0.0},
            })
    verificar(hoja)
    hoja.save(os.path.join(out, 'repartidora_sheet.png'))
    sin_mochila.save(os.path.join(out, 'repartidora_sheet_sin_mochila.png'))
    hoja.resize((hoja.width * 4, hoja.height * 4), Image.NEAREST).save(os.path.join(out, 'repartidora_preview_x4.png'))
    meta = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'repartidor_sheet.json'), encoding='utf-8'))
    meta.update(image='repartidora_sheet.png', frames=meta_frames,
                nota='Mismo formato que repartidor_sheet.png (generar_repartidora_sheet.py): cabello suelto y suéter lavanda.')
    with open(os.path.join(out, 'repartidora_sheet.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    if '--unity' in sys.argv:
        shutil.copyfile(os.path.join(out, 'repartidora_sheet.png'), os.path.join(UNITY, 'repartidora_sheet.png'))
        meta_unity()
    print('ok', out)


def verificar(hoja):
    """En cada vista, los 5 cuadros usan los mismos colores de cabello y suéter, y el cabello y la falda ocupan las
    mismas filas y columnas (corridas solo por el rebote del torso): no cambian de color ni de tamaño al caminar."""
    pelo = {C_SOMBRA, C_BASE, C_MEDIO, C_BRILLO}
    sueter = {L_LUZ, L_BASE, L_PUNTO, L_SOMBRA, L_LINEA}
    for r in range(3):
        datos = []
        for col in range(5):
            cel = hoja.crop((col * W, r * H, col * W + W, r * H + H))
            px = cel.load()
            colores = {px[x, y] for x in range(W) for y in range(H)}
            filas = [y for y in range(H) if any(px[x, y] in pelo for x in range(W))]
            falda = [(x, y - base.BOB_COLUMNA[col]) for x in range(W) for y in range(H) if px[x, y] in FALDA_COLORES]
            datos.append((frozenset(colores & pelo), frozenset(colores & sueter),
                          min(filas) - base.BOB_COLUMNA[col], max(filas) - base.BOB_COLUMNA[col],
                          min(f[0] for f in falda), max(f[0] for f in falda), min(f[1] for f in falda),
                          max(f[1] for f in falda), SUELA in colores))
        assert len(set(datos)) == 1, (r, datos)
    print('verificado: mismos colores y tamaño de cabello, suéter y falda (y la suela crema) en los 5 cuadros de cada vista')


def meta_unity():
    """.meta de repartidora_sheet.png: el del repartidor (mismo corte, pivotes, PPU y filtro) con su propio guid,
    los nombres repartidora_sheet_N y spriteIDs nuevos. Si ya existe, conserva el guid (no rompe referencias)."""
    origen = os.path.join(UNITY, 'repartidor_sheet.png.meta')
    destino = os.path.join(UNITY, 'repartidora_sheet.png.meta')
    texto = open(origen, encoding='utf-8').read()
    guid_chico = next(l.split()[1] for l in texto.splitlines() if l.startswith('guid:'))
    guid = None
    if os.path.exists(destino):
        guid = next((l.split()[1] for l in open(destino, encoding='utf-8') if l.startswith('guid:')), None)
    guid = guid or uuid.uuid5(uuid.NAMESPACE_URL, 'repartidora_sheet.png').hex
    texto = texto.replace(guid_chico, guid).replace('repartidor_sheet_', 'repartidora_sheet_')
    texto = re.sub(r'spriteID: ([0-9a-f]{32})', lambda m: 'spriteID: ' + uuid.uuid5(uuid.NAMESPACE_URL, guid + m.group(1)).hex, texto)
    with open(destino, 'w', encoding='utf-8', newline='\n') as f:
        f.write(texto)


if __name__ == '__main__':
    main()
