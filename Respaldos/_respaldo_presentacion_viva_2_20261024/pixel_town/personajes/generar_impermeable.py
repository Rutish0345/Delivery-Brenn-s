"""Atuendo de LLUVIA (octubre 2026) para el repartidor y la repartidora, solo para Nivel3_Lluvia: impermeable dorado
(#FFD166 en 2 tonos, contorno #2A1F40) con la capucha hacia atrás, botas de lluvia moradas y la mochila rosa encima.
La repartidora conserva su falda (asoma bajo el impermeable), sus medias y su cabello suelto.

Sale de los mismos cuadros que las hojas normales (generar_repartidor_sheet.build_frames: mismo ciclo de caminata y
mismo rebote) y con el mismo formato exacto: celdas de 56x80, 5 columnas x 3 filas (frente, espalda, lado derecho),
pivote abajo-centro. El impermeable se pone sobre el cuadro del repartidor ANTES de la mochila (así la mochila y sus
correas quedan encima); a la repartidora después se le pone la falda y el cabello (vestir(..., sueter=False)):
  - la chamarra (o el suéter) pasa a impermeable: tonos claros de la chamarra → #FFD166, oscuros → sombra dorada,
    contorno #2A1F40; más largo (hasta la fila 56; a la repartidora hasta la 54, para que asome su falda), con la tira
    de broches rosas al frente
  - la capucha, echada hacia atrás: de frente asoma detrás del cuello, de espaldas sobre los hombros (arriba de la
    mochila) y de lado detrás de la nuca
  - botas de lluvia moradas: el zapato y 5 filas de caña, con el borde de arriba más claro
Escribe repartidor_sheet_lluvia.png, repartidora_sheet_lluvia.png (con la mochila), sus _sin_mochila (para
generar_motos.py --lluvia), los .json y una vista previa; con --unity copia las dos hojas a Assets/Sprits/Personajes con
su .meta (el del repartidor: mismo corte, pivotes, PPU y filtro; guid propio que se conserva si ya existe).
Uso: python generar_impermeable.py [carpeta] [--unity]"""
import json
import os
import re
import shutil
import sys
import uuid

from PIL import Image

import generar_repartidor_sheet as base
import generar_repartidora_sheet as chica

W, H = base.W, base.H
AQUI = os.path.dirname(os.path.abspath(__file__))
UNITY = chica.UNITY

CONTORNO = (0x2A, 0x1F, 0x40, 255)
ORO = (0xFF, 0xD1, 0x66, 255)
ORO_SOMBRA = (0xD9, 0x96, 0x2B, 255)
BROCHE = (0xFF, 0x6F, 0xA5, 255)
BOTA_LUZ = (0xA9, 0x7C, 0xFF, 255)
BOTA = (0x7A, 0x45, 0xE6, 255)
BOTA_SOMBRA = (0x53, 0x29, 0xAE, 255)
COLORES = {CONTORNO, ORO, ORO_SOMBRA, BROCHE, BOTA_LUZ, BOTA, BOTA_SOMBRA}
IMPERMEABLE = {ORO, ORO_SOMBRA}


def _rgb(h):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16))


# chamarra y playera del repartidor → impermeable
A_ORO = {_rgb(h) for h in ('#A979D3', '#9562C6', '#7E4FCB', '#FCD560', '#F2EDE9', '#FFFFFF', '#E3D5C7')}
A_SOMBRA = {_rgb(h) for h in ('#653AB1', '#6D1EB9', '#561DB9', '#442A92', '#341074', '#D5CABF', '#D1D1D1', '#A3A0AE',
                              '#9BADB7')}
A_CONTORNO = {_rgb(h) for h in ('#18064E', '#1F0A5F', '#1D0646')}
TORSO = (31, 51)
PIERNA = chica.PANTALON | {chica.LINEA_PIERNA}
PIEL = {_rgb(h) for h in ('#90482B', '#FFDCC0', '#F8C2A8', '#C57E5E', '#B06E50', '#D3AB92', '#E98594')}

# el faldón del impermeable (filas sin rebote → tramos), sobre el pantalón y el aire; el bajo en la fila 56
BAJO = 56
BAJO_CHICA = 54   # a ella el faldón le llega más arriba: la falda (bajo en la fila 58) asoma debajo
FALDON = {
    0: chica._filas((49, 52, [(17, 34)]), (53, 56, [(16, 35)])),
    1: chica._filas((52, 52, [(17, 34)]), (53, 56, [(16, 35)])),
    2: chica._filas((49, 52, [(19, 31)]), (53, 56, [(18, 32)])),
}
# la capucha echada hacia atrás: solo donde hay aire (de frente queda detrás del cuello) o impermeable
CAPUCHA = {
    0: chica._filas((28, 28, [(17, 34)]), (29, 31, [(15, 36)])),
    1: chica._filas((27, 27, [(17, 34)]), (28, 32, [(15, 36)])),
    2: chica._filas((26, 26, [(14, 18)]), (27, 33, [(12, 20)])),
}
PLACA_X = 26                  # la tira de broches al frente
BROCHES = (38, 43, 48, 53)
CANA = 5                      # filas de caña de la bota sobre el zapato


def _pintar_mascara(px, mascara, bob, puede, fondo_contorno=True):
    """Pinta la máscara (dict fila → tramos) en dorado con contorno en el borde exterior; 'puede(p)' dice qué píxeles
    se pueden cubrir. Devuelve el conjunto de píxeles pintados."""
    pts = {(x, y) for y, tramos in mascara.items() for x0, x1 in tramos for x in range(x0, x1 + 1)}
    hechos = set()
    for x, y in pts:
        yy = y + bob
        if not (0 <= x < W and 0 <= yy < H) or not puede(px[x, yy]):
            continue
        borde = any((x + dx, y + dy) not in pts for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        px[x, yy] = CONTORNO if borde and fondo_contorno else ORO
        hechos.add((x, yy))
    return hechos


def limpiar(px, bob):
    """Las líneas oscuras de adentro de la chamarra (costuras, bolsillos) y las puntas de las correas quedan como pixeles
    sueltos sobre el dorado: pasan a la sombra. El contorno de afuera (junto al aire) y el de las correas se quedan."""
    for y in range(TORSO[0] + bob, BAJO + bob):
        for x in range(1, W - 1):
            if px[x, y][:3] not in {CONTORNO[:3], _rgb('#0D0220')}:
                continue
            vecinos = [px[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if all(v[3] == 255 for v in vecinos) and sum(v in IMPERMEABLE for v in vecinos) >= 2:
                px[x, y] = ORO_SOMBRA


def impermeable(im, vista, col, bajo=BAJO):
    """El impermeable, la capucha y las botas sobre un cuadro del repartidor (sin mochila)."""
    px = im.load()
    bob = base.BOB_COLUMNA[col]
    # 1) chamarra → impermeable dorado
    for y in range(TORSO[0] + bob, TORSO[1] + bob + 1):
        for x in range(W):
            p = px[x, y]
            if p[3] != 255:
                continue
            c = p[:3]
            if c in A_ORO:
                px[x, y] = ORO
            elif c in A_SOMBRA:
                px[x, y] = ORO_SOMBRA
            elif c in A_CONTORNO:
                px[x, y] = CONTORNO
    limpiar(px, bob)
    # 2) el faldón, más largo que la chamarra (cubre el pantalón de la cadera; las manos quedan encima)
    mascara = {y: t for y, t in FALDON[vista].items() if y <= bajo}
    faldon = _pintar_mascara(px, mascara, bob, lambda p: p[3] < 255 or p[:3] in PIERNA)
    for x, y in faldon:                                  # sombra al pie del faldón, sobre el contorno del bajo
        if y == bajo - 1 + bob and px[x, y] == ORO:
            px[x, y] = ORO_SOMBRA
    # 3) la capucha hacia atrás
    _pintar_mascara(px, CAPUCHA[vista], bob, lambda p: p[3] < 255 or p[:4] in IMPERMEABLE | {CONTORNO})
    if vista == 1:                                       # el pliegue de la capucha sobre la espalda
        for x in range(18, 34):
            if px[x, 31 + bob] == ORO:
                px[x, 31 + bob] = ORO_SOMBRA
    # 4) la tira de broches al frente
    if vista == 0:
        for y in range(34 + bob, bajo + bob):
            if px[PLACA_X, y][:4] in IMPERMEABLE:
                px[PLACA_X, y] = BROCHE if y - bob in BROCHES else ORO_SOMBRA
    # 5) botas de lluvia moradas: el zapato y la caña, con el borde de arriba más claro
    for x in range(W):
        zap = [y for y in range(H) if px[x, y][3] == 255 and px[x, y][:3] in chica.ZAPATO]
        if not zap:
            continue
        for y in zap:
            px[x, y] = BOTA_SOMBRA if px[x, y][:3] in chica.ZAPATO_LUZ else BOTA
        cana = [y for y in range(zap[0] - CANA, zap[0]) if px[x, y][3] == 255 and px[x, y][:3] in chica.PANTALON]
        for y in cana:
            px[x, y] = BOTA_LUZ if y == zap[0] - CANA else BOTA
    return im


def cuadro(frames, r, col, es_chica, mochila=True):
    im = impermeable(base.to_image(frames[r][col]), r, col, BAJO_CHICA if es_chica else BAJO)
    if mochila:
        im = base.poner_mochila(im, r, base.BOB_COLUMNA[col])
        limpiar(im.load(), base.BOB_COLUMNA[col])
    if es_chica:
        im = chica.vestir(im, r, col, sueter=False)
    return im


def hoja(frames, es_chica, mochila=True):
    out = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    for r in range(3):
        for col in range(5):
            out.paste(cuadro(frames, r, col, es_chica, mochila), (col * W, r * H))
    return out


def verificar(img, es_chica):
    """En cada vista, los 5 cuadros usan los mismos colores del impermeable y las botas, y el impermeable ocupa las
    mismas filas y columnas (corridas solo por el rebote): no cambia de tamaño al caminar."""
    for r in range(3):
        datos = []
        for col in range(5):
            px = img.crop((col * W, r * H, col * W + W, r * H + H)).load()
            bob = base.BOB_COLUMNA[col]
            oro = [(x, y - bob) for x in range(W) for y in range(H) if px[x, y] in IMPERMEABLE]
            colores = {px[x, y] for x in range(W) for y in range(H)}
            datos.append((frozenset(colores & COLORES - {CONTORNO, BOTA_LUZ, BOTA, BOTA_SOMBRA}),
                          max(y for _, y in oro), min(x for x, _ in oro), max(x for x, _ in oro),
                          BOTA in colores, es_chica and chica.F_BASE in colores))
        assert len(set(datos)) == 1, (r, datos)


def meta_unity(nombre):
    """.meta de <nombre>.png: el de repartidor_sheet.png con su propio guid (se conserva si ya existe) y los sprites
    <nombre>_N con spriteIDs propios."""
    origen = os.path.join(UNITY, 'repartidor_sheet.png.meta')
    destino = os.path.join(UNITY, nombre + '.png.meta')
    texto = open(origen, encoding='utf-8').read()
    guid_chico = next(l.split()[1] for l in texto.splitlines() if l.startswith('guid:'))
    guid = None
    if os.path.exists(destino):
        guid = next((l.split()[1] for l in open(destino, encoding='utf-8') if l.startswith('guid:')), None)
    guid = guid or uuid.uuid5(uuid.NAMESPACE_URL, nombre + '.png').hex
    texto = texto.replace(guid_chico, guid).replace('repartidor_sheet_', nombre + '_')
    texto = re.sub(r'spriteID: ([0-9a-f]{32})', lambda m: 'spriteID: ' + uuid.uuid5(uuid.NAMESPACE_URL, guid + m.group(1)).hex, texto)
    with open(destino, 'w', encoding='utf-8', newline='\n') as f:
        f.write(texto)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else AQUI
    frames = base.build_frames()
    meta_base = json.load(open(os.path.join(AQUI, 'repartidor_sheet.json'), encoding='utf-8'))
    nombres = ['frente', 'espalda', 'lado_derecho']
    previa = []
    for es_chica, nombre in ((False, 'repartidor_sheet_lluvia'), (True, 'repartidora_sheet_lluvia')):
        con = hoja(frames, es_chica)
        verificar(con, es_chica)
        con.save(os.path.join(out, nombre + '.png'))
        hoja(frames, es_chica, mochila=False).save(os.path.join(out, nombre + '_sin_mochila.png'))
        persona = 'repartidora' if es_chica else 'repartidor'
        meta = dict(meta_base, image=nombre + '.png',
                    nota=f'Mismo formato que {persona}_sheet.png: atuendo de lluvia (generar_impermeable.py), solo Nivel3_Lluvia.',
                    frames=[{'name': f'{persona}_lluvia_{nombres[r]}_{"idle" if c == 0 else "walk_%d" % (c - 1)}',
                             'row': r, 'col': c, 'x': c * W, 'y': r * H, 'w': W, 'h': H,
                             'unity_rect_y': (2 - r) * H, 'pivot': {'x': 0.5, 'y': 0.0}} for r in range(3) for c in range(5)])
        with open(os.path.join(out, nombre + '.json'), 'w', encoding='utf-8') as f:
            json.dump(meta, f, indent=2, ensure_ascii=False)
        previa.append(con)
        if '--unity' in sys.argv:
            shutil.copyfile(os.path.join(out, nombre + '.png'), os.path.join(UNITY, nombre + '.png'))
            meta_unity(nombre)
    vista = Image.new('RGBA', (W * 5, H * 6), (60, 66, 96, 255))       # sobre un azul de noche lluviosa
    for i, im in enumerate(previa):
        vista.alpha_composite(im, (0, i * H * 3))
    vista.resize((vista.width * 4, vista.height * 4), Image.NEAREST).save(os.path.join(out, 'impermeable_vista_previa.png'))
    print('verificado y listo:', out)


if __name__ == '__main__':
    main()
