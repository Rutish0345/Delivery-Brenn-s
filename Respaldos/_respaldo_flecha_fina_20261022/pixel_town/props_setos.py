"""Setos bajos que marcan el lote de cada casa del Residencial (octubre 2026).

Cada casa tenía cerca solo al frente; ahora su terreno queda cerrado por setos podados:
  - una línea lateral en cada límite del lote, desde la cerca del frente hasta el fondo, y
  - una línea trasera que cierra el lote.
Límites (columnas de LOTES): entre dos casas vecinas, la mitad del espacio entre el borde de un lote y el
del otro (cada borde es el fin de la cerca o la orilla de la cochera: lote_cols). En las orillas de la
manzana, el borde del lote (alineado con el poste del fin de la cerca, o por fuera de la cochera); si ahí
ya empieza la banqueta, la calle o la orilla del mapa, no hace falta seto. Entre dos cocheras pegadas el
seto empieza detrás de ellas: nunca va sobre el concreto. La cerca del frente se completa hasta cada límite
y lleva un poste de remate donde llega el seto.
Fondo: lo más atrás posible sin pisar la banqueta, la calle, la cancha o la plaza que haya detrás y sin
tapar nada que no sea árbol o arbusto (bancas, jardineras, juegos, huerto...). En las manzanas 'junto_a_la_casa'
(las que tienen el parque detrás, octubre 2026: así las acomodó la usuaria en SampleScene) el seto de atrás va
justo detrás de la casa y el parque queda libre, sin límite de lote. Los árboles y arbustos sueltos que quedan
debajo de un seto se quitan.
Cada tramo es un solo sprite del largo exacto (seto_lote_h_<px>, seto_lote_v_<px>): se ven continuos, sin
costuras, y el lateral tapa la cara del trasero en la esquina. Paleta y luz del 'seto' del atlas (bush_*):
luz arriba-izquierda, sombra plana. No usa el azar compartido del mapa.
Coordenadas en px de la imagen del mapa (y hacia abajo), como Escena.put: (sprite, centro x, fila del pie).
"""
import math
import random

from generate_town import P
from generate_unity import Sprite

T = 32
ALTO_H = 16           # seto horizontal: 7 px de copa + 8 de frente + 1 de sombra
FRENTE_H = 8
ANCHO_V = 12          # seto lateral visto desde arriba: 12 px de copa
SOMBRA_V = 2          # su sombra, a la derecha
SEP_COCHERA = 7       # px del centro del seto a la orilla de una cochera (6 de medio seto + 1)
MARGEN_OBST = 4       # px libres entre un seto y lo que no se puede quitar
VEGETALES = ('arbol_', 'pino', 'arbusto_', 'seto')      # los sueltos del mapa (los seto_lote se ponen después)
PROPIOS_LOTE = ('casa_', 'piedra_camino', 'cerca_baja', 'buzon_', 'carro_')
PAL = ('bush_dd', 'bush_d', 'bush', 'bush_l')


# ---------------------------------------------------------------- arte
def _moteado(sp, rng, x0, y0, w, h, base, motas):
    for _ in range(max(1, w * h // 6)):
        x, y = rng.randrange(x0, x0 + w), rng.randrange(y0, y0 + h)
        sp.px_on(x, y, rng.choice(motas), {P[base]})


def make_seto_h(largo, semilla=0):
    """Tramo trasero de 'largo' px: copa clara arriba, frente más oscuro y la sombra al pie. El sombreado es
    igual a lo largo, así cualquier tramo se pega con otro sin costura."""
    rng = random.Random(1000 + largo + 7919 * semilla)
    dd, d, b, l = PAL
    sp = Sprite(largo, ALTO_H)
    copa = ALTO_H - FRENTE_H - 1
    sp.rect(0, 0, largo, copa, b)
    sp.rect(0, 0, largo, 1, l)
    _moteado(sp, rng, 0, 1, largo, copa - 1, b, (l, l, d))
    sp.rect(0, copa, largo, FRENTE_H, d)
    sp.rect(0, copa, largo, 1, b)                        # canto de la poda
    _moteado(sp, rng, 0, copa + 1, largo, FRENTE_H - 2, d, (dd, b))
    sp.rect(0, ALTO_H - 2, largo, 1, dd)
    for x in range(largo):                               # sombra plana al pie
        sp.shadow(x, ALTO_H - 1)
    return sp.a


def make_seto_v(alto, semilla=0, punta=False, arriba=False):
    """Tramo lateral (visto desde arriba) de 'alto' px: copa con la orilla izquierda iluminada y la derecha en
    sombra, igual a lo largo (los tramos se apilan sin costura). 'punta': abajo lleva la punta que da a la
    cerca, con su frente y las esquinas redondeadas; 'arriba': la sombra de la derecha empieza 2 px abajo."""
    rng = random.Random(2000 + alto + 7919 * semilla + (500 if punta else 0))
    dd, d, b, l = PAL
    sp = Sprite(ANCHO_V + SOMBRA_V, alto)
    alto_copa = alto - FRENTE_H if punta else alto
    sp.rect(0, 0, ANCHO_V, alto_copa, b)
    sp.rect(0, 0, 2, alto_copa, l)
    sp.rect(ANCHO_V - 2, 0, 1, alto_copa, d)
    sp.rect(ANCHO_V - 1, 0, 1, alto_copa, dd)
    _moteado(sp, rng, 2, 0, ANCHO_V - 4, alto_copa, b, (l, d, d))
    if punta:
        y = alto_copa
        sp.rect(0, y, ANCHO_V, FRENTE_H, d)              # frente de la punta
        sp.rect(1, y, ANCHO_V - 2, 1, b)
        _moteado(sp, rng, 1, y + 1, ANCHO_V - 2, FRENTE_H - 3, d, (dd, b))
        sp.rect(0, alto - 2, ANCHO_V, 1, dd)
        for x in range(ANCHO_V):                         # la última fila es sombra; esquinas redondeadas
            sp.a[alto - 1, x] = 0
            if 0 < x < ANCHO_V - 1:
                sp.shadow(x, alto - 1)
        sp.a[alto - 2, 0] = sp.a[alto - 2, ANCHO_V - 1] = 0
    for yy in range(2 if arriba else 0, alto - 1 if punta else alto):   # sombra a la derecha
        for x in range(ANCHO_V, ANCHO_V + SOMBRA_V):
            sp.shadow(x, yy)
    return sp.a


TRAMO = T                 # los setos van en tramos de 1 tile, cada uno con su pivote en la base (orden por Y)
VARIANTES = 3


def segmentar(tramos):
    """Parte cada seto (('h', x0, x1, base) o ('v', x, arriba, pie)) en tramos de 1 tile: [(sprite, cx, base)].
    Horizontal: el pedazo sobrante (menos de 1 tile) va a la izquierda. Vertical: abajo la punta, encima los
    tramos de en medio y arriba el sobrante."""
    res = []
    for t in tramos:
        if t[0] == 'h':
            _, x0, x1, base = t
            largo = x1 - x0
            r = largo % TRAMO
            if r:
                res.append((f'seto_lote_h_r{r}', x0 + r // 2, base))
            for i in range(largo // TRAMO):
                res.append((f'seto_lote_h_{"abc"[(i + x0 // TRAMO) % VARIANTES]}', x0 + r + TRAMO // 2 + i * TRAMO, base))
        else:
            _, x, arriba, pie = t
            largo = pie - arriba + 1
            assert largo >= TRAMO, ('seto lateral muy corto', t)
            cx = x + SOMBRA_V // 2
            res.append(('seto_lote_v_punta', cx, pie))
            medios, r = divmod(largo - TRAMO, TRAMO)
            for i in range(medios):
                ultimo = i == medios - 1 and not r
                nombre = 'seto_lote_v_arriba' if ultimo else f'seto_lote_v_{"abc"[(i + pie // TRAMO) % VARIANTES]}'
                res.append((nombre, cx, pie - TRAMO * (i + 1)))
            if r:
                res.append((f'seto_lote_v_r{r}', cx, arriba + r - 1))
    return res


# ---------------------------------------------------------------- geometría
def huella(nombre, cx, base, props, colisiones):
    """Cajas (x0, y0, x1, y1) en px de lo que pisa un prop: su collider de la tabla o, si no tiene, una
    franja de 8 px al pie de todo su ancho."""
    w = props[nombre].shape[1]
    izq = cx - w / 2
    for prefijo, tramos, alto in colisiones:
        if nombre.startswith(prefijo):
            return [(izq + x0, base - alto + 1, izq + x1 + 1, base + 1) for x0, x1 in tramos]
    return [(izq, base - 7, izq + w, base + 1)]


def choca(a, b, m=0.0):
    return a[0] < b[2] + m and b[0] < a[2] + m and a[1] < b[3] + m and b[1] < a[3] + m


def _verde(grid, r, c, cols):
    return 0 <= c < cols and str(grid[r, c]).startswith(('pasto', 'flores'))


def calcular(lotes_def, rampa_cols, lote_cols, grid, objs, props, colisiones, rows, cols, junto_a_la_casa=()):
    """Lotes y setos de todas las casas. Devuelve dict con:
    lotes (uno por casa), setos [(sprite, cx, base)], cercas y postes [(sprite, cx, base)], rects (cajas en
    px de cada seto) y quitar (índices de objs: árboles y arbustos que quedan debajo de un seto)."""
    lotes, tramos, cercas, postes, rects, postes_viejos, solidos = [], [], [], [], [], [], []

    def poner_seto_v(x, top, pie):
        tramos.append(('v', x, top, pie))
        rects.append((x - ANCHO_V / 2, top, x + ANCHO_V / 2, pie + 1))

    for manzana, (rf, defs) in lotes_def.items():
        hb = rf - 3
        fila_casa = hb - 5                                # fila de arriba de la casa
        cerca_base = rf * T + 30
        bloque = []
        for k, lado in defs:
            lc = lote_cols(k, lado)
            ci = min(rampa_cols(k, lado))
            r = rf                                        # la cochera es de banqueta: de la cerca a la casa
            while r > 0 and str(grid[r - 1, ci]).startswith('banqueta'):
                r -= 1
            bloque.append(dict(manzana=manzana, k=k, lado=lado, izq=lc[0], der=lc[-1] + 1, cochera_arriba=r, rf=rf))
        for a, b in zip(bloque, bloque[1:]):
            assert a['der'] <= b['izq'], ('lotes encimados', a, b)
            a['lim_der'] = b['lim_izq'] = (a['der'] + b['izq']) / 2
        # orillas de la manzana: x del seto (o None si ya no hace falta)
        a, b = bloque[0], bloque[-1]
        if a['lado'] == 'der':
            a['x_izq'] = a['izq'] * T + 3                 # sobre el poste del fin de la cerca (c*T + 3)
            if not _verde(grid, rf - 1, a['izq'] - 1, cols):
                a['x_izq'] = a['izq'] * T + ANCHO_V // 2  # junto a la banqueta: todo el seto dentro del lote
        else:
            a['x_izq'] = a['izq'] * T - SEP_COCHERA if _verde(grid, rf - 1, a['izq'] - 1, cols) else None
        if b['lado'] == 'izq':
            b['x_der'] = b['der'] * T - 4                 # sobre el poste del fin de la cerca (c*T + T - 4)
            if not _verde(grid, rf - 1, b['der'], cols):
                b['x_der'] = b['der'] * T - ANCHO_V // 2
        else:
            b['x_der'] = b['der'] * T + SEP_COCHERA if _verde(grid, rf - 1, b['der'], cols) else None
        a['lim_izq'], b['lim_der'] = a['izq'], b['der']

        # fondo de cada lote
        for l in bloque:
            x0 = (l['x_izq'] if l.get('x_izq') is not None else l['lim_izq'] * T) - ANCHO_V / 2
            x1 = (l['x_der'] if l.get('x_der') is not None else l['lim_der'] * T) + ANCHO_V / 2
            # orilla de la manzana sin seto lateral (ahí ya empieza la banqueta o el mapa): el de atrás llega
            # justo a la orilla del lote y no pisa la banqueta
            if l is bloque[0] and l['x_izq'] is None:
                x0 = l['izq'] * T
            if l is bloque[-1] and l['x_der'] is None:
                x1 = l['der'] * T
            fila = 0
            for c in range(max(0, math.floor(x0 / T)), min(cols, math.ceil(x1 / T))):
                r = fila_casa - 1
                while r >= 0 and _verde(grid, r, c, cols):
                    r -= 1
                fila = max(fila, r + 1)
            if manzana in junto_a_la_casa:                     # justo detrás de la casa: lo de atrás queda libre
                fila = fila_casa
            base = fila * T + 20
            fijos = [h for (ob, n, cx, _) in objs if not n.startswith(VEGETALES + PROPIOS_LOTE)
                     for h in huella(n, cx, ob, props, colisiones) if h[2] > x0 and h[0] < x1 and h[3] <= fila_casa * T]
            while True:
                malos = [h for h in fijos if choca((x0, base - ALTO_H + 1, x1, base + 1), h, MARGEN_OBST)]
                if not malos:
                    break
                base = int(max(h[3] for h in malos)) + MARGEN_OBST + ALTO_H
            assert base < fila_casa * T + T, ('no cabe el seto de atrás', l)
            l['fondo'], l['x0'], l['x1'] = base, max(0, round(x0)), min(cols * T, round(x1))
            if (l['x1'] - l['x0']) % 2:                   # largo par: Unity y el render lo centran igual
                l['x1'] += -1 if l['x1'] == cols * T else 1
            tramos.append(('h', l['x0'], l['x1'], base))
            rects.append((l['x0'], base - ALTO_H + 1, l['x1'], base + 1))

        # laterales: arrancan en la copa del seto de atrás (el más lejano de los dos lotes) y bajan hasta la
        # cerca, o hasta donde empiezan dos cocheras pegadas
        def top(fondo):
            return fondo - FRENTE_H
        for l, lado in ((bloque[0], 'x_izq'), (bloque[-1], 'x_der')):
            if l[lado] is None:
                # ahí empieza la banqueta (o el mapa) junto a la cochera: el patio se cierra con un seto por
                # dentro del lote, de atrás hasta la cochera
                x = l['izq'] * T + ANCHO_V // 2 if lado == 'x_izq' else l['der'] * T - ANCHO_V // 2
                poner_seto_v(x, top(l['fondo']), l['cochera_arriba'] * T - 2)
                continue
            poner_seto_v(round(l[lado]), top(l['fondo']), cerca_base - 4)
            if (lado == 'x_izq') != (l['lado'] == 'der'):  # por fuera de la cochera: poste de remate
                postes.append(('cerca_baja_poste', round(l[lado]), cerca_base + 1))
        for a, b in zip(bloque, bloque[1:]):
            x = round(a['lim_der'] * T)
            t = top(min(a['fondo'], b['fondo']))
            pegados = a['der'] == b['izq']
            if pegados and (a['lado'], b['lado']) == ('der', 'izq'):     # dos cocheras pegadas
                poner_seto_v(x, t, min(a['cochera_arriba'], b['cochera_arriba']) * T - 2)
                continue
            if pegados:                                   # cochera contra la cerca del vecino: del lado de la cerca
                x += SEP_COCHERA if a['lado'] == 'der' else -SEP_COCHERA
                poner_seto_v(x, t, cerca_base - 4)        # ya tiene el poste del fin de esa cerca
                continue
            poner_seto_v(x, t, cerca_base - 4)
            postes.append(('cerca_baja_poste', x, cerca_base + 1))
            # cerca del frente hasta el límite (piezas de 32 px; con medio tile se enciman 16 px, múltiplo de
            # la separación de las tablas, así no se nota)
            # el poste del fin de la cerca vieja queda a media cerca: se quita (el remate va en el límite)
            if a['lado'] == 'izq':
                postes_viejos.append((a['der'] * T - 4, cerca_base + 1))
            if b['lado'] == 'der':
                postes_viejos.append((b['izq'] * T + 3, cerca_base + 1))
            if a['lado'] == 'izq':                        # el lado derecho de 'a' es cerca
                for j in range(math.ceil((x - a['der'] * T) / T)):
                    cercas.append(('cerca_baja', x - T // 2 - j * T, cerca_base))
            if b['lado'] == 'der':                        # el izquierdo de 'b' es cerca
                for j in range(math.ceil((b['izq'] * T - x) / T)):
                    cercas.append(('cerca_baja', x + T // 2 + j * T, cerca_base))
        # lo que cierra cada patio (en px): la cerca del frente de lado a lado del lote (con el hueco del portón
        # cerrado), sin la cochera, y la orilla de la cochera que da al patio y su remate de arriba
        for i, l in enumerate(bloque):
            izq = (l['x_izq'] if l['x_izq'] is not None else l['izq'] * T) if i == 0 else l['lim_izq'] * T
            der = (l['x_der'] if l['x_der'] is not None else l['der'] * T) if i == len(bloque) - 1 else l['lim_der'] * T
            ci, cd = rampa_cols(l['k'], l['lado'])
            c0, c1, arriba = ci * T, (cd + 1) * T, l['cochera_arriba'] * T
            for a0, a1 in ((izq, c0), (c1, der)):
                if a1 - a0 > 0.5:
                    solidos.append(('cerca', (a0, cerca_base - 5, a1, cerca_base + 1)))
            if l['lado'] == 'der':
                solidos.append(('cochera', (c0 - 4, arriba, c0, cerca_base + 1)))
            else:
                solidos.append(('cochera', (c1, arriba, c1 + 4, cerca_base + 1)))
            solidos.append(('cochera', (c0, arriba - 4, c1, arriba)))
            # del otro lado de la cochera, si hay pasto (una franja hasta el seto del límite), también se cierra
            fuera = cd + 1 if l['lado'] == 'der' else ci - 1
            if _verde(grid, rf - 1, fuera, cols):
                if l['lado'] == 'der':
                    solidos.append(('cochera', (c1, arriba - 4, c1 + 4, cerca_base + 1)))
                else:
                    solidos.append(('cochera', (c0 - 4, arriba - 4, c0, cerca_base + 1)))
            l['patio'] = (izq, l['fondo'] - ALTO_H + 1, der, cerca_base + 1)
            l['cochera'] = (c0, arriba, c1, (rf + 1) * T)
        lotes.extend(bloque)

    quitar = [i for i, (ob, n, cx, _) in enumerate(objs)
              if n.startswith(VEGETALES) and any(choca(h, r, 1) for h in huella(n, cx, ob, props, colisiones) for r in rects)]
    viejos = [i for i, (ob, n, cx, _) in enumerate(objs) if n == 'cerca_baja_poste' and (cx, ob) in postes_viejos]
    assert len(viejos) == len(postes_viejos), ('no se encontró un poste viejo', len(viejos), len(postes_viejos))
    quitar += viejos
    solidos = [('seto', r) for r in rects] + solidos
    return dict(lotes=lotes, setos=segmentar(tramos), tramos=tramos, cercas=cercas, postes=postes, rects=rects,
                quitar=quitar, solidos=solidos)


def sprites(setos):
    """El arte de cada tramo que se usa, por nombre."""
    arte = {}
    for nombre, _, _ in setos:
        if nombre in arte:
            continue
        if nombre.startswith('seto_lote_h_'):
            resto = nombre[len('seto_lote_h_'):]
            arte[nombre] = make_seto_h(int(resto[1:]), 9) if resto.startswith('r') else make_seto_h(TRAMO, 'abc'.index(resto))
            continue
        resto = nombre[len('seto_lote_v_'):]
        if resto == 'punta':
            arte[nombre] = make_seto_v(TRAMO, 0, punta=True)
        elif resto == 'arriba':
            arte[nombre] = make_seto_v(TRAMO, 4, arriba=True)
        elif resto.startswith('r'):
            arte[nombre] = make_seto_v(int(resto[1:]), 9, arriba=True)
        else:
            arte[nombre] = make_seto_v(TRAMO, 'abc'.index(resto))
    return arte
