"""Auditoría de los pasos peatonales del Residencial sobre el suelo que pinta Unity (sin autos ni gente encima).
Uso: python auditar_cebras.py <carpeta con mapa_residencial_grande.json y tileset_suelo.*> [salida.json]
Imprime una tabla (posición, orientación, ancho de la calle, rayas dibujadas y esperadas, qué falta)."""
import json
import sys

import numpy as np
from PIL import Image

import cebras
import generate_residencial_grande as g


def nombre_calle(c):
    if c['eje'] == 'h':
        return 'Av. Las Flores' if min(c['filas']) < g.AV_N + 13 else 'Olmo'
    if min(c['cols']) >= g.AV_X[0]:
        return 'Av. Las Flores (tramo vertical)'
    r = min(c['filas'])
    return {g.AV_N + 10: 'Empedrada (boca en la avenida)', g.OLMO: 'Empedrada (en Olmo)',
            g.OLMO + 8: 'Pino (en Olmo)', g.BANQ_SUR: 'Pino (orilla sur)'}[r]


def suelo_unity(data, tileset_png, tileset_json):
    """El suelo tal como lo pinta ConstruirMapa: los tiles del JSON y, si aún existe en Unity, el parche de
    AbrirPasosCamellon (la celda de camellón con cebra a los dos lados, a 1 o 2 celdas, se vuelve esa cebra)."""
    suelo = [list(f) for f in data['suelo']]
    alto, ancho = len(suelo), len(suelo[0])
    t = lambda r, c: suelo[r][c] if 0 <= r < alto and 0 <= c < ancho else None
    cebra = lambda *cs: next((t(r, c) for r, c in cs if (t(r, c) or '').startswith('cebra')), None)
    cambios = []
    for r in range(alto):
        for c in range(ancho):
            if not suelo[r][c].startswith('camellon'):
                continue
            arr, aba = cebra((r - 1, c), (r - 2, c)), cebra((r + 1, c), (r + 2, c))
            izq, der = cebra((r, c - 1), (r, c - 2)), cebra((r, c + 1), (r, c + 2))
            n = arr if arr and aba else izq if izq and der else None
            if n:
                cambios.append((r, c, n))
    for r, c, n in cambios:
        suelo[r][c] = n
    meta = {x['name']: x for x in json.load(open(tileset_json, encoding='utf-8'))['tiles']}
    hoja = np.asarray(Image.open(tileset_png).convert('RGB'))
    img = np.zeros((alto * g.T, ancho * g.T, 3), np.uint8)
    for r in range(alto):
        for c in range(ancho):
            x = meta[suelo[r][c]]
            img[r * g.T:(r + 1) * g.T, c * g.T:(c + 1) * g.T] = hoja[x['y']:x['y'] + g.T, x['x']:x['x'] + g.T]
    return img, len(cambios)


def main(carpeta, salida=None):
    data = json.load(open(carpeta + '/mapa_residencial_grande.json', encoding='utf-8'))
    img, parche = suelo_unity(data, carpeta + '/tileset_suelo.png', carpeta + '/tileset_suelo.json')
    print('celdas de camellón parchadas por Unity:', parche)
    props = {**g.build_props(), **g.build_props_extra()}
    try:
        import props_setos
        for p in data['props']:
            if p['nombre'].startswith('seto_lote_'):
                props.update(props_setos.sprites([(p['nombre'], 0, 0)]))
    except ImportError:
        pass
    objs = []
    for p in data['props']:
        n = p['nombre']
        h, w = props[n].shape[:2]
        cx = p['x'] * g.T + w // 2 - w / 2
        base = round(g.H_PX - (p['y'] - g.PIVOTES.get(n, 0) * h / g.T) * g.T - 1)
        objs.append((base, n, cx, p.get('voltear', False)))
    lz = g.trazar_raster()
    lista = cebras.cruces(g.CRUCES_V, g.CRUCES_H)
    filas = cebras.auditar(img, lz.m, g.MG * g.T, lista, (g.ASFALTO, g.ADOQUIN), g.PASTO, objs, props, nombre_calle)
    print('| # | Calle | Posición (cols × filas) | Orientación | Ancho calle | Rayas dibujadas | Deberían ser | Qué falta |')
    print('|---|---|---|---|---|---|---|---|')
    for f in filas:
        ori = 'cruza calle vertical (rayas verticales)' if f['eje'] == 'v' else 'cruza calle horizontal (rayas horizontales)'
        print(f"| {f['n']} | {f['calle']} | {f['cols'][0]}-{f['cols'][1]} × {f['filas'][0]}-{f['filas'][1]} | {ori} | "
              f"{f['ancho']:g} | {f['dibujadas']} | {f['esperadas']} | {'; '.join(f['falta'])} |")
    if salida:
        json.dump(filas, open(salida, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main(*sys.argv[1:])
