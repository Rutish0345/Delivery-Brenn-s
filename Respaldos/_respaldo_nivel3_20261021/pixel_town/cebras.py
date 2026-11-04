"""Pasos peatonales (cebras) del Residencial grande, completos de banqueta a banqueta (octubre 2026).

Antes las cebras eran tiles enteros (cebra_v / cebra_h) que solo se ponían sobre asfalto liso: las filas con
la sombra del bordillo o junto al camellón quedaban sin rayas y el camellón cortaba el paso. Ahora se pintan
por píxel encima del suelo ya armado:
  - cada cebra cubre todo el ancho de la calle que cruza (de la cara de un bordillo a la del otro), con rayas
    de 7 px + 1 px de canto (las de siempre) cada 16 px, centradas en el paso y de 96 px de largo (3 tiles);
  - donde cruza un camellón, el pasto del paso se vuelve una isleta de concreto de banqueta del ancho de la
    cebra, con sus bordillos (misma regla que los demás), y las rayas siguen por encima de la isleta;
  - antes de cada cebra, en el sentido de la circulación, una línea de alto (franja blanca de 6 px) de lado a
    lado del carril, a 8 px de la cebra; solo donde el carril llega por una calle recta (no a la salida de un
    cruce ni fuera del mapa).
Las celdas tocadas se vuelven tiles nuevos: cebra_v_NN / cebra_h_NN (todas las del paso, así Unity sigue
sabiendo qué celdas son cebra y de qué eje) y linea_alto_NN. No usa el azar del mapa.
Coordenadas en px del mapa (x a la derecha, y hacia abajo); el raster del generador tiene MG tiles de margen.
"""
import numpy as np

from generate_town import P

T = 32
RAYA, PERIODO = 8, 16          # 7 px de raya + 1 de canto, cada 16 px (las de cebra_h / cebra_v)
ALTO_GROSOR, ALTO_SEP = 6, 8   # línea de alto: grosor y separación de la cebra
LIBRE = T                      # alrededor de cada cebra: sin árboles, arbustos ni faroles
QUITABLES = ('arbol_', 'pino', 'arbusto_', 'seto', 'farol')


def cruces(cruces_v, cruces_h):
    """[(eje, (s0, s1), semilla)]: eje 'v' = cruza una calle vertical (se camina en x, rayas verticales),
    'h' = cruza una horizontal. (s0, s1) es el largo de las rayas en px a lo largo de la calle."""
    res = []
    for rs, cs in cruces_v:
        res.append(dict(eje='v', span=(min(rs) * T, (max(rs) + 1) * T), filas=rs, cols=cs))
    for rs, cs in cruces_h:
        res.append(dict(eje='h', span=(min(cs) * T, (max(cs) + 1) * T), filas=rs, cols=cs))
    return res


def _linea(m, mg, eje, u, w):
    """Valor del raster en el punto de la línea u (a lo largo de la calle) y w (a lo ancho, donde se camina)."""
    x, y = (w, u) if eje == 'v' else (u, w)
    return m[y + mg, x + mg]


def extension(m, mg, c, calle, isla):
    """(w0, w1): de la cara de un bordillo a la del otro, en las líneas rectas del paso (la menor de todas las
    líneas: junto a una esquina redondeada la calle se ensancha y ahí las rayas no siguen la curva)."""
    rs, cs = c['filas'], c['cols']
    if c['eje'] == 'v':
        ws = [cc * T + T // 2 for cc in cs]
    else:
        ws = [r * T + T // 2 for r in rs]
    w0s, w1s = [], []
    for u in range(*c['span']):
        semilla = next(w for w in ws if _linea(m, mg, c['eje'], u, w) in calle)
        ok = lambda w: _linea(m, mg, c['eje'], u, w) in calle or _linea(isla, mg, c['eje'], u, w)
        a = semilla
        while ok(a - 1):
            a -= 1
        b = semilla
        while ok(b):
            b += 1
        w0s.append(a)
        w1s.append(b)
    return max(w0s), min(w1s)


def rayas_esperadas(w0, w1):
    return (w1 - w0 + PERIODO - RAYA) // PERIODO


def mascara_isla(m, mg, lista, pasto):
    isla = np.zeros(m.shape, bool)
    for c in lista:
        r0, r1 = min(c['filas']) * T + mg, (max(c['filas']) + 1) * T + mg
        c0, c1 = min(c['cols']) * T + mg, (max(c['cols']) + 1) * T + mg
        isla[r0:r1, c0:c1] = m[r0:r1, c0:c1] == pasto
    return isla


# ---------------------------------------------------------------- auditoría sobre una captura
def contar_rayas(img, c, w0, w1):
    """Rayas pintadas en la captura (RGB, px del mapa) por la línea central del paso, el hueco más largo sin
    rayas entre la primera y la última, y cuánto falta en cada orilla hasta el bordillo."""
    u = (c['span'][0] + c['span'][1]) // 2
    pintura = [np.array(P['stripe']), np.array(P['stripe_d'])]
    run, runs, inicio = 0, [], None
    for w in range(w0, w1):
        x, y = (w, u) if c['eje'] == 'v' else (u, w)
        px = img[y, x, :3].astype(int)
        es = min(np.abs(px - p).sum() for p in pintura) < 40
        if es:
            if run == 0:
                inicio = w
            run += 1
        elif run:
            if run >= 3:
                runs.append((inicio, inicio + run))
            run = 0
    if run >= 3:
        runs.append((inicio, inicio + run))
    if not runs:
        return 0, w1 - w0, w1 - w0, w1 - w0
    hueco = max([b[0] - a[1] for a, b in zip(runs, runs[1:])] or [0])
    return len(runs), hueco, runs[0][0] - w0, w1 - runs[-1][1]


def auditar(img, m, mg, lista, calle, pasto, objs, props, nombre_calle):
    """Una fila por cebra (dicts) con lo que tiene la captura y lo que le falta."""
    filas = []
    isla = mascara_isla(m, mg, lista, pasto)
    for i, c in enumerate(lista, 1):
        w0, w1 = extension(m, mg, c, calle, isla)
        dib, hueco, ini, fin = contar_rayas(img, c, w0, w1)
        esp = rayas_esperadas(w0, w1)
        falta = []
        u = (c['span'][0] + c['span'][1]) // 2
        en_camellon = [w for w in range(w0, w1) if _linea(m, mg, c['eje'], u, w) == pasto]
        def pixel(w):
            return img[(u, w) if c['eje'] == 'v' else (w, u)][:3].astype(int)
        verde = sum(1 for w in en_camellon if pixel(w)[1] > pixel(w)[0] + 15 and pixel(w)[1] > pixel(w)[2] + 15)
        concreto = sum(1 for w in en_camellon if np.abs(pixel(w) - np.array(P['side'])).sum() < 30)
        if hueco > PERIODO:
            falta.append(f'hueco de {hueco} px')
        if dib < esp:
            falta.append(f'{esp - dib} rayas de menos')
        if en_camellon and not concreto:
            falta.append('camellón sin isleta' + (' (pasto en el paso)' if verde else ' (parche de cebra sobre el pasto)'))
        if ini > PERIODO - RAYA + 4 or fin > PERIODO - RAYA + 4:
            falta.append(f'no llega a la banqueta ({ini} / {fin} px)')
        cerca = estorbos(c, w0, w1, objs, props)
        if cerca:
            falta.append('pegados: ' + ', '.join(sorted({o[1] for o in cerca})))
        filas.append(dict(n=i, calle=nombre_calle(c), eje=c['eje'], cols=(min(c['cols']), max(c['cols'])),
                          filas=(min(c['filas']), max(c['filas'])), ancho=(w1 - w0) / T, dibujadas=dib,
                          esperadas=esp, falta=falta or ['nada']))
    return filas


def zona(c, w0, w1):
    """Caja (x0, y0, x1, y1) del paso con su tile libre alrededor."""
    s0, s1 = c['span']
    if c['eje'] == 'v':
        return (w0 - LIBRE, s0 - LIBRE, w1 + LIBRE, s1 + LIBRE)
    return (s0 - LIBRE, w0 - LIBRE, s1 + LIBRE, w1 + LIBRE)


def caja_paso(c, w0, w1):
    s0, s1 = c['span']
    return (w0, s0, w1, s1) if c['eje'] == 'v' else (s0, w0, s1, w1)


def pie(nombre, cx, base, props):
    """Huella de un prop: todo su ancho, los 10 px de abajo."""
    w = props[nombre].shape[1]
    return (cx - w / 2, base - 9, cx + w / 2, base + 1)


def copa(nombre, cx, base, props):
    h, w = props[nombre].shape[:2]
    return (cx - w / 2, base - h + 1, cx + w / 2, base + 1)


def toca(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def estorbos(c, w0, w1, objs, props):
    """Árboles, arbustos y faroles con el pie a menos de un tile del paso o la copa encima de las rayas."""
    z, p = zona(c, w0, w1), caja_paso(c, w0, w1)
    return [o for o in objs if o[1].startswith(QUITABLES)
            and (toca(pie(o[1], o[2], o[0], props), z) or (not o[1].startswith('farol') and toca(copa(o[1], o[2], o[0], props), p)))]


# ---------------------------------------------------------------- pintura en el generador
def pintar(grid, tiles, lz, ov, bordillos, carriles, mats):
    """Pinta todas las cebras y líneas de alto sobre el grid ya armado. Devuelve (tiles nuevos [(nombre, arr)],
    geometría de cada cebra para la auditoría y el despeje de props)."""
    ASFALTO, ADOQUIN, PASTO, BANQ_M = mats['ASFALTO'], mats['ADOQUIN'], mats['PASTO'], mats['BANQ_M']
    TEXTURA, COLOR_BORDE, sombras = mats['TEXTURA'], mats['COLOR_BORDE'], mats['SOMBRAS']
    mg = mats['MG'] * T
    lista = mats['lista']
    calle = (ASFALTO, ADOQUIN)

    isla = mascara_isla(lz.m, mg, lista, PASTO)
    m2 = lz.m.copy()
    m2[isla] = BANQ_M
    ov2 = bordillos(m2)
    distinto = (ov2 != ov) | isla
    pintable = (np.isin(m2, calle) | isla) & np.isin(ov2, (0,) + tuple(sombras))
    color = np.zeros(m2.shape + (3,), np.uint8)
    pinta = np.zeros(m2.shape, bool)
    nombre_celda = {}

    # textura de la raya: la de los tiles de siempre (con su pintura gastada), por fila/columna de la raya
    tex = {'h': tiles['cebra_h'][4:4 + RAYA, :], 'v': tiles['cebra_v'][:, 4:4 + RAYA]}

    def poner(x, y, rgb):
        if pintable[y + mg, x + mg]:
            k = ov2[y + mg, x + mg]
            pinta[y + mg, x + mg] = True
            color[y + mg, x + mg] = (np.asarray(rgb, float) * sombras.get(k, 1.0)).astype(np.uint8)

    geo = []
    for c in lista:
        w0, w1 = extension(lz.m, mg, c, calle, isla)
        n = rayas_esperadas(w0, w1)
        o = w0 + ((w1 - w0) - (PERIODO * n - (PERIODO - RAYA))) // 2
        s0, s1 = c['span']
        for u in range(s0, s1):
            for k in range(PERIODO * n - (PERIODO - RAYA)):
                j = k % PERIODO
                if j >= RAYA:
                    continue
                w = o + k
                if c['eje'] == 'v':
                    poner(w, u, tex['v'][u % T, j])
                else:
                    poner(u, w, tex['h'][j, u % T])
        cel = caja_paso(c, w0, w1)
        for r in range(cel[1] // T, (cel[3] - 1) // T + 1):
            for cc in range(cel[0] // T, (cel[2] - 1) // T + 1):
                nombre_celda[r, cc] = 'cebra_' + c['eje']
        geo.append(dict(c, w0=w0, w1=w1, rayas=n, inicio=o))

        # líneas de alto
        for lo, hi, sentido, exterior in carriles(c):
            # sentido: +1 si los autos avanzan hacia s creciente (llegan por s0), -1 si llegan por s1
            a = s0 - ALTO_SEP - ALTO_GROSOR if sentido > 0 else s1 + ALTO_SEP
            tramo = range(a, s0) if sentido > 0 else range(s1, a + ALTO_GROSOR)
            fuera = exterior + (8 if exterior >= hi else -8)
            mapa_w = (lz.m.shape[1] if c['eje'] == 'h' else lz.m.shape[0]) - 2 * mg
            if a < 0 or a + ALTO_GROSOR > mapa_w:
                continue                                    # el carril llega desde fuera del mapa
            if any(_linea(lz.m, mg, c['eje'], u, fuera) in calle for u in tramo):
                continue                                    # llega saliendo de un cruce
            for u in range(a, a + ALTO_GROSOR):
                canto = u == a + ALTO_GROSOR - 1
                for w in range(lo, hi):
                    rgb = P['stripe_d'] if canto else P['stripe']
                    if c['eje'] == 'h':
                        poner(u, w, rgb)
                    else:
                        poner(w, u, rgb)
            for u in (a, a + ALTO_GROSOR - 1):
                for w in list(range(lo, hi, 8)) + [hi - 1]:
                    x, y = (u, w) if c['eje'] == 'h' else (w, u)
                    nombre_celda.setdefault((y // T, x // T), 'linea_alto')
            geo[-1].setdefault('altos', []).append((a, lo, hi))

    # celdas: las del paso y las de las líneas de alto, armadas de nuevo con la pintura encima
    filas, cols = lz.m.shape[0] // T - 2 * mats['MG'], lz.m.shape[1] // T - 2 * mats['MG']
    marcadas = np.zeros(m2.shape, bool)
    for (r, cc) in nombre_celda:
        marcadas[r * T + mg:(r + 1) * T + mg, cc * T + mg:(cc + 1) * T + mg] = True
    assert not (pinta & ~marcadas).any(), 'hay pintura fuera de las celdas de las cebras'
    for (r, cc) in list(nombre_celda):
        if not (0 <= r < filas and 0 <= cc < cols):
            del nombre_celda[r, cc]
    sl = lambda a, r, cc: a[(r * T) + mg:(r + 1) * T + mg, cc * T + mg:(cc + 1) * T + mg]
    afuera = distinto.copy()
    nuevos, por_bytes, cuenta = [], {}, {}
    for (r, cc), pref in sorted(nombre_celda.items()):
        if sl(distinto, r, cc).any():
            bm, bo = sl(m2, r, cc), sl(ov2, r, cc)
            a = np.zeros((T, T, 3), np.uint8)
            for v, nombre in TEXTURA.items():
                a[bm == v] = tiles[nombre][bm == v]
            for k, col in COLOR_BORDE.items():
                a[bo == k] = P[col]
            for k, f_osc in mats['OSCURO_ADOQUIN']:
                sel = (bo == k) & (bm == ADOQUIN)
                a[sel] = (tiles['adoquin_a'][sel] * f_osc).astype(np.uint8)
            sl(afuera, r, cc)[:] = False
        else:
            a = tiles[grid[r, cc]].copy()
        sel = sl(pinta, r, cc)
        a[sel] = sl(color, r, cc)[sel]
        clave = (pref, a.tobytes())
        if clave not in por_bytes:
            cuenta[pref] = cuenta.get(pref, 0) + 1
            por_bytes[clave] = f'{pref}_{cuenta[pref]:02d}'
            nuevos.append((por_bytes[clave], a))
        grid[r, cc] = por_bytes[clave]
    assert not afuera[mg:-mg, mg:-mg].any(), 'el bordillo cambió fuera de las isletas de las cebras'
    return nuevos, geo


# ---------------------------------------------------------------- despeje alrededor de las cebras
def despejar(objs, props, geo, m, mg):
    """Árboles, arbustos y faroles a menos de un tile de una cebra (o con la copa encima de las rayas): se
    corren a lo largo de la calle, alejándose del paso, al lugar libre más cercano sobre el mismo suelo (el
    camellón o la banqueta) sin encimarse con otro prop; si no hay lugar, se quitan. Devuelve (movidos,
    quitados) como listas de (nombre, antes, después)."""
    zonas = [zona(c, c['w0'], c['w1']) for c in geo]
    pasos = [caja_paso(c, c['w0'], c['w1']) for c in geo]

    def estorba(n, cx, base):
        return any(toca(pie(n, cx, base, props), z) for z in zonas) or \
            (not n.startswith('farol') and any(toca(copa(n, cx, base, props), p) for p in pasos))

    def suelo(n, cx, base):
        x0, _, x1, _ = pie(n, cx, base, props)
        return {int(m[base + mg, x + mg]) for x in range(int(x0) + 2, int(x1) - 2)}

    movidos, quitados = [], []
    for i, (base, n, cx, flip) in enumerate(list(objs)):
        if not n.startswith(QUITABLES) or not estorba(n, cx, base):
            continue
        c = next(g for g, z in zip(geo, zonas) if toca(pie(n, cx, base, props), z) or toca(copa(n, cx, base, props), caja_paso(g, g['w0'], g['w1'])))
        horizontal = c['eje'] == 'h'                     # la calle corre en x: se mueve en x
        centro = sum(c['span']) / 2
        lejos = 1 if (cx if horizontal else base) >= centro else -1
        piso = suelo(n, cx, base)
        otros = [pie(o[1], o[2], o[0], props) for j, o in enumerate(objs) if j != i]
        nuevo = None
        for d in range(8, 8 * T, 4):
            ncx, nbase = (cx + lejos * d, base) if horizontal else (cx, base + lejos * d)
            if estorba(n, ncx, nbase) or suelo(n, ncx, nbase) != piso:
                continue
            caja = pie(n, ncx, nbase, props)
            if any(toca((caja[0] - 4, caja[1] - 4, caja[2] + 4, caja[3] + 4), o) for o in otros):
                continue
            nuevo = (nbase, n, ncx, flip)
            break
        if nuevo:
            objs[i] = nuevo
            movidos.append((n, (cx, base), (nuevo[2], nuevo[0])))
        else:
            objs[i] = None
            quitados.append((n, (cx, base), None))
    objs[:] = [o for o in objs if o is not None]
    return movidos, quitados
