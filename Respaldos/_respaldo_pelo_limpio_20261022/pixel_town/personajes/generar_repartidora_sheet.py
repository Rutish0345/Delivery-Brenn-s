"""Hoja de la repartidora (octubre 2026; rediseño: cabello suelto y suéter lavanda): el mismo formato exacto que la del
repartidor (celdas de 56x80, 5 columnas x 3 filas: frente, espalda, lado derecho; columna 0 quieta, 1-4 caminando;
pivote abajo-centro) porque sale de los mismos cuadros de generar_repartidor_sheet.py (mismo ciclo de caminata y mismo
rebote) con su ropa y su cabello encima (vestir()):
  - suéter lavanda tejido en lugar de la chamarra morada: cuello redondo, mangas holgadas hasta la muñeca, moño
    dorado en el pecho y punto en 2 tonos de lavanda; el pantalón oscuro, los tenis y la mochila rosa no cambian
  - cabello castaño largo y suelto que sale de la gorra, con contorno #2A1F40:
      frente: cae a los dos lados de la cara sobre los hombros, flequillo ladeado bajo la gorra
      espalda: largo y ondulado sobre la espalda; la mochila se ve por los lados y por debajo (con su emblema)
      lado (mira a la derecha): cae detrás del hombro, con un mechón al frente
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
K = {'o': CONTORNO, 's': C_SOMBRA, 'b': C_BASE, 'm': C_MEDIO, 'h': C_BRILLO}

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

# ---- frente: mechón izquierdo (x 12..18) desde y 20 hasta los hombros; el derecho es su espejo (x 31..37) ----
MECHON_IZQ = [
    "...obbm",   # 20
    "..obbmm",   # 21
    ".osbbhm",   # 22
    ".osbbhm",   # 23
    "osbbhmo",   # 24
    "osbbhmo",   # 25
    "osbhmmo",   # 26
    "osbhmo.",   # 27
    "osbbmo.",   # 28
    "osbbmo.",   # 29
    "osbhmo.",   # 30
    "osbhmmo",   # 31 hombro
    "osbbhmo",   # 32
    "osbbhmo",   # 33
    ".osbhmo",   # 34
    ".osbhmo",   # 35
    ".osbbmo",   # 36
    "..osbmo",   # 37
    "..osbmo",   # 38
    "..osbo.",   # 39
    "...oso.",   # 40 puntas
    "...o.o.",   # 41
]
X_MECHON, ESPEJO = 12, 49           # x' = ESPEJO - x
# flequillo ladeado: de la sien izquierda hacia la derecha, bajo la visera (no tapa las cejas, fila 22)
FLEQUILLO = [(20, 22, "bbmmbbo"), (21, 20, "obbmbo")]

# ---- espalda: largo y ondulado (x 13..35); más angosto que la mochila (x 18..33) y termina antes del emblema ----
ONDA = [0, 0, 0, 1, 1, 1, 1, 0, 0, 0, -1, -1, -1, -1]   # vaivén suave de los mechones, fila a fila
TEXTURA = "sbmhmb"                                     # tonos de un mechón, de izquierda a derecha


def _espalda_bordes(y):
    """Bordes del cabello de espaldas en la fila y (sin rebote): (izquierda, derecha) o None."""
    if 20 <= y <= 27:
        return 13, 35                       # sale de la gorra un poco más ancho que la cabeza
    if 28 <= y <= 30:
        return 15, 34
    if 31 <= y <= 39:                       # sobre la mochila: deja ver sus lados
        o = ONDA[y % len(ONDA)]
        return 20 + o, 31 + o
    return None


# ---- lado (mira a la derecha): detrás del hombro (x 11..19) y un mechón al frente (x 22..26) ----
LADO_ATRAS = [
    "......obb",   # 20
    ".....obbm",   # 21
    "....osbbm",   # 22
    "...osbbhm",   # 23
    "...osbhmo",   # 24
    "..osbbhmo",   # 25
    "..osbhmmo",   # 26
    "..osbhmo.",   # 27
    "..osbhmo.",   # 28
    "..osbbmo.",   # 29
    "..osbhmo.",   # 30
    "..osbhmo.",   # 31 sobre la mochila: angosto, se ve su lado
    "...osbmo.",   # 32
    "...osbmo.",   # 33
    "...osbmo.",   # 34
    "...osbmo.",   # 35
    "....osbo.",   # 36
    "....osbo.",   # 37
    "....oso..",   # 38
    ".....o...",   # 39
]
X_LADO = 11
# sobre la oreja y el mechón que cae por delante del hombro
LADO_FRENTE = [
    "obbo...",   # 20
    "obmbo..",   # 21
    "osbmo..",   # 22
    "osbmmo.",   # 23
    ".osbmo.",   # 24
    ".osbhmo",   # 25
    "..osbmo",   # 26
    "..osbmo",   # 27
    "..osbmo",   # 28
    "...osbo",   # 29
    "...osbo",   # 30
    "...osbo",   # 31
    "...oso.",   # 32
    "....o..",   # 33
]
X_LADO_FRENTE = 19

# ---- en moto, de lado: el cabello suelto al viento, hacia atrás (x 1..19 desde y 20); 2 cuadros que ondean ----
VIENTO = [
    [
        "...........ooobbbbo",   # 20
        "......ooooobbbbhmmo",   # 21
        "..oooobbbbhhmmmmmo.",   # 22
        "oobbbbhhhmmmmbbmo..",   # 23
        ".osssbbbmmmbbbsso..",   # 24
        "..oooosssbbbsoo....",   # 25
        "......ooooooo......",   # 26
    ],
    [
        "...........ooobbbbo",   # 20
        ".........oobbbbhmmo",   # 21
        "...ooooobbbbhhmmmmo",   # 22
        ".oobbbbhhhmmmmbbmo.",   # 23
        "osssbbbbmmmbbbsso..",   # 24
        ".ooosssbbbbsooo....",   # 25
        "....ooooooo........",   # 26
    ],
]
X_VIENTO = 1


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


def poner_cabello(im, vista, col, viento=None):
    """vista: 0 frente, 1 espalda, 2 lado derecho; col: columna de la hoja (rebote del torso, el mismo para el cabello).
    viento (en moto, generar_motos.py): None a pie; 0 parada; 1 o 2 rodando (el cabello ondea hacia atrás)."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    if vista == 0:
        _pintar(px, MECHON_IZQ, X_MECHON, 20 + bob)
        _pintar(px, [f[::-1] for f in MECHON_IZQ], ESPEJO - X_MECHON - len(MECHON_IZQ[0]) + 1, 20 + bob)
        for y, x0, fila in FLEQUILLO:
            _pintar(px, [fila], x0, y + bob)
        px[21, 23 + bob] = NEGRO                  # pestañas
        px[30, 23 + bob] = NEGRO
        for x in (20, 21, 30, 31):                # mejillas
            if px[x, 26 + bob][:3] == (0xDB, 0xB1, 0xB5):
                px[x, 26 + bob] = RUBOR
    elif vista == 1:
        vaiven = (0, 1, -1)[viento] if viento else 0
        for y in range(20, 41):
            bordes = _espalda_bordes(y)
            if bordes is None:
                continue
            izq, der = bordes
            if y >= 31:
                izq, der = izq + vaiven, der + vaiven
            centro = (izq + der) / 2
            for x in range(izq, der + 1):
                # puntas en V: más largas al centro (hasta la fila 39, justo arriba del emblema de la mochila)
                d = abs(x - centro)
                fin = 39 if d < 2.5 else 38 if d < 4.5 else 37
                if y > fin:
                    continue
                if x in (izq, der) or y == fin:
                    c = CONTORNO
                else:
                    c = K[TEXTURA[(x - ONDA[y % len(ONDA)]) % len(TEXTURA)]]
                px[x, y + bob] = c
    else:
        if viento:
            _pintar(px, VIENTO[viento - 1], X_VIENTO, 20 + bob)
        else:
            _pintar(px, LADO_ATRAS, X_LADO, 20 + bob)
        _pintar(px, LADO_FRENTE, X_LADO_FRENTE, 20 + bob)
    return im


def vestir(im, vista, col, viento=None):
    """Suéter y cabello de la repartidora sobre un cuadro del repartidor (con o sin mochila)."""
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
    """En cada vista, los 5 cuadros usan los mismos colores de cabello y suéter, y el cabello ocupa las mismas filas
    (corridas solo por el rebote del torso): no cambia de color ni de tamaño al caminar."""
    pelo = {C_SOMBRA, C_BASE, C_MEDIO, C_BRILLO}
    sueter = {L_LUZ, L_BASE, L_PUNTO, L_SOMBRA, L_LINEA}
    for r in range(3):
        datos = []
        for col in range(5):
            cel = hoja.crop((col * W, r * H, col * W + W, r * H + H))
            px = cel.load()
            colores = {px[x, y] for x in range(W) for y in range(H)}
            filas = [y for y in range(H) if any(px[x, y] in pelo for x in range(W))]
            datos.append((frozenset(colores & pelo), frozenset(colores & sueter),
                          min(filas) - base.BOB_COLUMNA[col], max(filas) - base.BOB_COLUMNA[col]))
        assert len(set(datos)) == 1, (r, datos)
    print('verificado: mismos colores y tamaño de cabello y suéter en los 5 cuadros de cada vista')


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
