"""Hoja de la repartidora (octubre 2026): el mismo formato exacto que la del repartidor (celdas de 56x80, 5 columnas x
3 filas: frente, espalda, lado derecho; columna 0 quieta, 1-4 caminando; pivote abajo-centro) porque sale de los
mismos cuadros de generar_repartidor_sheet.py (mismo uniforme, mismo ciclo de caminata y mismo rebote) con el cabello
encima:
  - frente: mechones castaños largos a los lados de la cara que caen hasta los hombros, una pestaña y mejillas rosadas
  - espalda: coleta que sale por la abertura de la gorra (liga rosa) y cae sobre la mochila
  - lado (mira a la derecha): coleta que cuelga detrás de la cabeza y se mece un poco al caminar
Escribe repartidora_sheet.png (con la mochila rosa), repartidora_sheet_sin_mochila.png (para generar_motos.py) y
repartidora_sheet.json; con --unity también copia la hoja a Assets/Sprits/Personajes con su .meta.
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

# cabello castaño: los tonos del pelo del repartidor y un brillo; liga rosa del Salón
C_OSC = (0x25, 0x0B, 0x11, 255)      # contorno
C_SOMBRA = (0x32, 0x12, 0x12, 255)
C_BASE = (0x41, 0x20, 0x1A, 255)
C_MEDIO = (0x5B, 0x35, 0x2E, 255)
C_BRILLO = (0x7B, 0x4A, 0x38, 255)
LIGA = base.M_ROSA
LIGA_OSC = base.M_ROSA_OSC
NEGRO = (0, 0, 0, 255)
RUBOR = (0xF2, 0xA0, 0xB0, 255)
CABELLO_COLORES = {C_OSC, C_SOMBRA, C_BASE, C_MEDIO, C_BRILLO, LIGA, LIGA_OSC, NEGRO, RUBOR}
K = {'o': C_OSC, 's': C_SOMBRA, 'b': C_BASE, 'm': C_MEDIO, 'h': C_BRILLO, 'L': LIGA, 'l': LIGA_OSC}

# vaivén de la coleta de lado en cada columna (px en x a partir de la mitad de la coleta): atrasa el paso
VAIVEN = [0, -1, 0, 1, 0]

# ---- frente: mechón izquierdo (x 13..16) desde y 20; el derecho es su espejo (x 33..36) ----
MECHON_IZQ = [
    ".obb",   # 20
    ".obm",   # 21
    "osbm",   # 22
    "osbm",   # 23
    "osbh",   # 24
    "osbm",   # 25
    "osbm",   # 26
    "osbh",   # 27
    "osbm",   # 28
    "osbm",   # 29
    ".osb",   # 30
    ".osb",   # 31
    ".osm",   # 32
    "..ob",   # 33
    "..o.",   # 34
]

# ---- espalda: coleta (x 21..28) desde y 18 (liga en la abertura de la gorra) hasta y 43 ----
COLETA_ESPALDA = [
    ".oLLLLo.",   # 18 liga
    ".olllll.",   # 19
    "..obmo..",   # 20
    "..obmmo.",   # 21
    ".osbhmo.",   # 22
    ".osbhmmo",   # 23
    "osbbhmmo",   # 24
    "osbbhmmo",   # 25
    "osbbhmmo",   # 26
    "osbbhmmo",   # 27
    ".osbhmmo",   # 28
    ".osbhmo.",   # 29
    ".osbhmo.",   # 30
    ".osbhmo.",   # 31
    ".osbmmo.",   # 32
    ".osbmmo.",   # 33
    ".osbmo..",   # 34
    "..osbmo.",   # 35
    "..osbmo.",   # 36
    "..osbmo.",   # 37
    "..osbo..",   # 38
    "..osbo..",   # 39
    "...obo..",   # 40
    "...obo..",   # 41
    "...oo...",   # 42
]
X_COLETA_ESPALDA, Y_COLETA_ESPALDA = 21, 18

# ---- lado: coleta (x 9..16) detrás de la cabeza, desde y 18 ----
COLETA_LADO = [
    "....oLLo",   # 18 liga
    "....ollo",   # 19
    "...obmmo",   # 20
    "..obbhmo",   # 21
    ".osbbhmo",   # 22
    ".osbhmo.",   # 23
    "osbbhmo.",   # 24
    "osbbhmo.",   # 25
    "osbhmo..",   # 26
    "osbhmo..",   # 27
    "osbhmo..",   # 28
    "osbmo...",   # 29
    "osbmo...",   # 30
    "osbmo...",   # 31
    ".obmo...",   # 32
    ".obo....",   # 33
    "..oo....",   # 34
]
X_COLETA_LADO, Y_COLETA_LADO = 9, 18

# ---- en moto, de lado: la coleta al viento, casi horizontal hacia atrás (x 1..16 desde y 18); 2 cuadros que ondean ----
COLETA_VIENTO = [
    [
        "..........ooLLo.",   # 18 liga
        ".....ooooobbllo.",   # 19
        "..oooobbbhhmmmo.",   # 20
        "oobbbbhhmmmmoo..",   # 21
        ".osssbbmmoo.....",   # 22
        "..oooso.........",   # 23
    ],
    [
        "..........ooLLo.",   # 18 liga
        "........oobbllo.",   # 19
        "...ooobbbhhmmmo.",   # 20
        ".oobbbhhhmmmoo..",   # 21
        "osbbbmmmoo......",   # 22
        ".ooo............",   # 23
    ],
]
X_VIENTO, Y_VIENTO = 1, 18


def _pintar(px, filas, x0, y0, solo_vacio=False, vaiven=0, desde_vaiven=99):
    for j, fila in enumerate(filas):
        dx = vaiven if j >= desde_vaiven else 0
        for i, ch in enumerate(fila):
            if ch == '.':
                continue
            x, y = x0 + i + dx, y0 + j
            if 0 <= x < W and 0 <= y < H and (not solo_vacio or px[x, y][3] < 255):
                px[x, y] = K[ch]


def poner_cabello(im, vista, col, viento=None):
    """vista: 0 frente, 1 espalda, 2 lado derecho; col: columna de la hoja (rebote y vaivén).
    viento (en moto, generar_motos.py): None a pie; 0 parada; 1 o 2 rodando (la coleta ondea al viento)."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    if viento:
        if vista == 1:     # de espaldas: la punta se mece de lado a lado
            _pintar(px, COLETA_ESPALDA, X_COLETA_ESPALDA, Y_COLETA_ESPALDA + bob, vaiven=(1, -1)[viento - 1], desde_vaiven=10)
            return im
        if vista == 2:     # de lado: hacia atrás, casi horizontal
            _pintar(px, COLETA_VIENTO[viento - 1], X_VIENTO, Y_VIENTO + bob)
            return im
    if vista == 0:
        _pintar(px, MECHON_IZQ, 13, 20 + bob)
        _pintar(px, [f[::-1] for f in MECHON_IZQ], 33, 20 + bob)
        px[21, 22 + bob] = NEGRO                  # pestaña del ojo izquierdo (el derecho ya la tiene en el pelo)
        for x in (20, 21, 30, 31):                # mejillas
            if px[x, 26 + bob][:3] == (0xDB, 0xB1, 0xB5):
                px[x, 26 + bob] = RUBOR
    elif vista == 1:
        _pintar(px, COLETA_ESPALDA, X_COLETA_ESPALDA, Y_COLETA_ESPALDA + bob)
    else:
        # detrás de la cabeza: no tapa la gorra ni la cara (solo donde hay hueco, pelo o mochila)
        _pintar(px, COLETA_LADO, X_COLETA_LADO, Y_COLETA_LADO + bob, vaiven=VAIVEN[col], desde_vaiven=8)
    return im


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else os.path.dirname(os.path.abspath(__file__))
    frames = base.build_frames()
    hoja = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    sin_mochila = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    meta_frames = []
    nombres = ['frente', 'espalda', 'lado_derecho']
    permitidos = base.REF_COLORS | base.MOCHILA_COLORES | CABELLO_COLORES
    for r, fila in enumerate(frames):
        for col, cv in enumerate(fila):
            im = base.to_image(cv)
            sin_mochila.paste(poner_cabello(im.copy(), r, col), (col * W, r * H))
            con = poner_cabello(base.poner_mochila(im.copy(), r, base.BOB_COLUMNA[col]), r, col)
            malos = {con.getpixel((x, y)) for x in range(W) for y in range(H) if con.getpixel((x, y))[3]} - permitidos
            assert not malos, (r, col, malos)
            hoja.paste(con, (col * W, r * H))
            meta_frames.append({
                'name': f'repartidora_{nombres[r]}_{"idle" if col == 0 else "walk_%d" % (col - 1)}',
                'row': r, 'col': col, 'x': col * W, 'y': r * H, 'w': W, 'h': H,
                'unity_rect_y': (2 - r) * H, 'pivot': {'x': 0.5, 'y': 0.0},
            })
    hoja.save(os.path.join(out, 'repartidora_sheet.png'))
    sin_mochila.save(os.path.join(out, 'repartidora_sheet_sin_mochila.png'))
    hoja.resize((hoja.width * 4, hoja.height * 4), Image.NEAREST).save(os.path.join(out, 'repartidora_preview_x4.png'))
    meta = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'repartidor_sheet.json'), encoding='utf-8'))
    meta.update(image='repartidora_sheet.png', frames=meta_frames,
                nota='Mismo formato que repartidor_sheet.png (generar_repartidora_sheet.py): solo cambia el cabello.')
    with open(os.path.join(out, 'repartidora_sheet.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    if '--unity' in sys.argv:
        shutil.copyfile(os.path.join(out, 'repartidora_sheet.png'), os.path.join(UNITY, 'repartidora_sheet.png'))
        meta_unity()
    print('ok', out)


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
