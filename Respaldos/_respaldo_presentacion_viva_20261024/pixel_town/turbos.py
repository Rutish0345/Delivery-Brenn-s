"""Dónde van los turbos "Café exprés" de cada mapa (lo llaman generate_residencial_grande.py, generate_comercial_grande.py
y generate_lluvia_grande.py antes de escribir su JSON; Assets/Editor/ConstruirTurbos.cs los pone en la escena).

colocar(data, cantidad) devuelve [{nombre, x, y, desvio}] (coordenadas en tiles, el centro de la taza) con estas reglas:
  - sobre banqueta, plaza o los caminos de adoquín del parque (centro de un tile), alcanzable desde el spawn;
  - a 4 tiles o más de cualquier punto de entrega, cruce (zonas_cruce y tiles de cebra / línea de alto), bache y
    semáforo, y a 5 o más de las cebras (los semáforos de los niveles 1 y 3 se ponen en sus esquinas);
  - nunca dentro de un patio privado ni de un lote sólido, y lejos del pie de los props (árboles, faroles, bancas...);
  - fuera de la ruta más directa entre puntos: el camino más corto (Dijkstra por calle y banqueta, rejilla de 0.5
    tiles, como comprobar_tiempos) entre el spawn y cada punto y entre cada par de puntos no pasa por él, pero queda
    a no más de DESVIO_MAX tiles (hay que desviarse un poco); "desvio" es esa distancia;
  - repartidos: se eligen uno a uno, cada vez el candidato más lejano de los ya elegidos y del spawn.
Sin azar: el mismo mapa da los mismos turbos.

Uso directo (sin regenerar los mapas): python turbos.py --aplicar   escribe "turbos" en los 3 JSON de unity/.
"""
import json
import math
import os
import sys

import numpy as np

CANTIDAD = {"residencial": 4, "comercial": 6, "lluvia": 6}
DIST_MIN = 4.0          # a puntos, baches, semáforos y zonas de cruce
DIST_CEBRA = 5.0        # a los tiles de cebra y línea de alto
DESVIO_MIN = 1.0        # fuera de la ruta directa...
DESVIO_MAX = 4.0        # ...pero cerca
ORILLA = 2.5            # lejos del borde del mapa
F = 2                   # rejilla de 0.5 tiles
SUELOS = ("banqueta", "plaza", "adoquin")


def _base(t):
    return t[:-len("_mojado")] if t.endswith("_mojado") else t


def _rejilla(data):
    return np.array([[_base(t) for t in fila] for fila in data["suelo"]])


def _dist_rect(x, y, r):
    dx = max(r["x"] - x, 0, x - (r["x"] + r["ancho"]))
    dy = max(r["y"] - y, 0, y - (r["y"] + r["alto"]))
    return math.hypot(dx, dy)


def _dentro(x, y, r, margen):
    return r["x"] - margen <= x <= r["x"] + r["ancho"] + margen and r["y"] - margen <= y <= r["y"] + r["alto"] + margen


def _caminos(data, g):
    """Dijkstra en 8 direcciones por todo lo que no es pasto (como comprobar_tiempos). Devuelve la rejilla de piso,
    la distancia desde el spawn y las celdas que usan las rutas más cortas entre spawn y puntos y entre puntos."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import dijkstra
    R, C = g.shape
    piso = np.zeros((R * F, C * F), bool)
    for r in range(R):
        for c in range(C):
            if not g[r, c].startswith(("pasto", "flores", "camellon", "orilla")):
                piso[(R - 1 - r) * F:(R - r) * F, c * F:(c + 1) * F] = True
    for b in data.get("lotes_solidos", []):
        piso[max(0, int(b["y"] * F)):int(math.ceil((b["y"] + b["alto"]) * F)),
             max(0, int(b["x"] * F)):int(math.ceil((b["x"] + b["ancho"]) * F))] = False
    pts = [(data["spawn"]["x"], data["spawn"]["y"])] + [(p["x"], p["y"]) for p in data["puntos_entrega"]]
    for x, y in pts:
        piso[int(y * F) - 1:int(y * F) + 2, int(x * F) - 1:int(x * F) + 2] = True
    H, W = piso.shape
    idx = -np.ones(piso.shape, int)
    idx[piso] = np.arange(piso.sum())
    celdas = np.argwhere(piso)
    fi, co, pe = [], [], []
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        ys, xs = np.nonzero(piso[:H - dy, max(0, -dx):W - max(0, dx)] & piso[dy:, max(0, dx):W - max(0, -dx)])
        xs = xs + max(0, -dx)
        fi.extend(idx[ys, xs]); co.extend(idx[ys + dy, xs + dx]); pe.extend([math.hypot(dx, dy) / F] * len(ys))
    n = int(piso.sum())
    grafo = coo_matrix((pe, (fi, co)), shape=(n, n)).tocsr()
    fuentes = [idx[int(y * F), int(x * F)] for x, y in pts]
    dist, pred = dijkstra(grafo, directed=False, indices=fuentes, return_predecessors=True)
    ruta = np.zeros(n, bool)
    for i in range(len(fuentes)):
        for j in range(1, len(fuentes)):
            if i == j:
                continue
            k = fuentes[j]
            while k >= 0 and k != fuentes[i]:
                ruta[k] = True
                k = pred[i][k]
    en_ruta = np.zeros(piso.shape, bool)
    en_ruta[celdas[:, 0], celdas[:, 1]] = ruta
    desde_spawn = np.full(piso.shape, np.inf)
    desde_spawn[celdas[:, 0], celdas[:, 1]] = dist[0]
    return piso, desde_spawn, en_ruta


def _semaforos(data):
    pts = []
    for s in data.get("semaforos", []):
        for k in ("esquina_eo", "esquina_ns"):
            if k in s:
                pts.append((s[k][0] + 0.5, s[k][1] + 0.5))
    return pts


def colocar(data, cantidad):
    from scipy.ndimage import distance_transform_edt
    g = _rejilla(data)
    R, C = g.shape
    piso, desde_spawn, en_ruta = _caminos(data, g)
    desvio = distance_transform_edt(~en_ruta) / F       # tiles hasta la ruta directa más cercana

    puntos = [(p["x"], p["y"]) for p in data["puntos_entrega"]]
    baches = [(b["x"] + b.get("ancho", 1) / 2, b["y"] + b.get("alto", 1) / 2) for b in data.get("baches", [])]
    baches += [(d["x"] + 0.5, d["y"] + 0.5) for d in data.get("dinamicos", []) if "bache" in d["nombre"]]
    semaforos = _semaforos(data)
    cebras = [(c + 0.5, R - 1 - r + 0.5) for r in range(R) for c in range(C) if g[r, c].startswith(("cebra", "linea_alto"))]
    cebras = np.array(cebras) if cebras else np.zeros((0, 2))
    cruces = list(data.get("zonas_cruce", {}).values())
    patios = [p["patio"] for p in data.get("patios", [])]
    solidos = data.get("lotes_solidos", [])
    props = np.array([(o["x"], o["y"]) for o in data.get("props", [])]) if data.get("props") else np.zeros((0, 2))
    grandes = np.array([(o["x"], o["y"]) for o in data.get("props", []) if o["nombre"].startswith(("arbol", "kiosco", "fuente"))]) \
        if data.get("props") else np.zeros((0, 2))
    spawn = (data["spawn"]["x"], data["spawn"]["y"])

    cand = []
    for r in range(R):
        for c in range(C):
            if not g[r, c].startswith(SUELOS):
                continue
            x, y = c + 0.5, R - 1 - r + 0.5
            if min(x, y, C - x, R - y) < ORILLA:
                continue
            gy, gx = int(y * F), int(x * F)
            if not np.isfinite(desde_spawn[gy, gx]):
                continue
            if not DESVIO_MIN <= desvio[gy, gx] <= DESVIO_MAX:
                continue
            if min((math.hypot(x - a, y - b) for a, b in puntos + baches + semaforos), default=99) < DIST_MIN:
                continue
            if len(cebras) and np.min(np.hypot(cebras[:, 0] - x, cebras[:, 1] - y)) < DIST_CEBRA:
                continue
            if any(_dist_rect(x, y, z) < DIST_MIN for z in cruces):
                continue
            if any(_dentro(x, y, p, 0.5) for p in patios) or any(_dentro(x, y, s, 0.8) for s in solidos):
                continue
            if len(props) and np.min(np.hypot(props[:, 0] - x, props[:, 1] - y)) < 1.3:
                continue
            if len(grandes) and np.min(np.hypot(grandes[:, 0] - x, grandes[:, 1] - y + 0.5)) < 2.2:
                continue
            if math.hypot(x - spawn[0], y - spawn[1]) < 6:
                continue
            cand.append((x, y, float(desvio[gy, gx])))
    if len(cand) < cantidad:
        raise SystemExit(f"turbos: solo {len(cand)} lugares posibles para {cantidad} turbos")

    elegidos = []
    for k in range(cantidad):
        refs = [spawn] + [(e[0], e[1]) for e in elegidos]
        mejor = max(cand, key=lambda q: (min(math.hypot(q[0] - a, q[1] - b) for a, b in refs), -q[1], -q[0]))
        elegidos.append(mejor)
        cand.remove(mejor)
    return [dict(nombre=f"Turbo_{k + 1}", x=round(x, 3), y=round(y, 3), desvio=round(d, 2))
            for k, (x, y, d) in enumerate(elegidos)]


def aplicar():
    """Agrega "turbos" a los JSON ya generados (sin regenerar los mapas) y conserva el formato de cada archivo."""
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity")
    for zona, archivo in (("residencial", "residencial/mapa_residencial_grande.json"),
                          ("comercial", "comercial/mapa_comercial.json"), ("lluvia", "lluvia/mapa_lluvia.json")):
        ruta = os.path.join(base, archivo)
        texto = open(ruta, encoding="utf-8").read()
        data = json.loads(texto)
        data["turbos"] = colocar(data, CANTIDAD[zona])
        print(zona, [(t["nombre"], t["x"], t["y"], t["desvio"]) for t in data["turbos"]])
        with open(ruta, "w", encoding="utf-8") as f:
            f.write(json.dumps(data, ensure_ascii=False) if zona == "lluvia" else por_renglones(data))


def por_renglones(data):
    """El formato de los JSON del residencial y el comercial (guardar_json): una entrada de lista por renglón."""
    partes = []
    for k, v in data.items():
        if isinstance(v, list):
            cuerpo = "[\n" + ",\n".join("    " + json.dumps(o, ensure_ascii=False) for o in v) + "\n  ]"
        else:
            cuerpo = json.dumps(v, ensure_ascii=False)
        partes.append(f"  {json.dumps(k)}: {cuerpo}")
    return "{\n" + ",\n".join(partes) + "\n}\n"


def vista(data, turbos, fondo, salida, escala=0.5):
    """Mapa de referencia con las rutas directas (cian), los puntos de entrega (azul) y los turbos (dorado)."""
    from PIL import Image, ImageDraw
    im = Image.open(fondo).convert("RGB")
    T = im.width / data["columnas"]
    g = _rejilla(data)
    _, _, en_ruta = _caminos(data, g)
    d = ImageDraw.Draw(im, "RGBA")
    alto = im.height
    for gy, gx in np.argwhere(en_ruta):
        x, y = gx / F * T, alto - (gy + 1) / F * T
        d.rectangle((x, y, x + T / F, y + T / F), fill=(0, 220, 255, 90))
    for p in data["puntos_entrega"]:
        x, y = p["x"] * T, alto - p["y"] * T
        d.ellipse((x - 10, y - 10, x + 10, y + 10), fill=(60, 90, 255, 230))
    for t in turbos:
        x, y = t["x"] * T, alto - t["y"] * T
        d.ellipse((x - 22, y - 22, x + 22, y + 22), outline=(42, 31, 64, 255), width=6, fill=(255, 209, 102, 240))
        d.text((x - 6, y - 8), t["nombre"].split("_")[1], fill=(42, 31, 64, 255))
    im.resize((int(im.width * escala), int(im.height * escala))).save(salida)


if __name__ == "__main__":
    if "--vista" in sys.argv:
        base = os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity")
        for zona, archivo, fondo in (("residencial", "residencial/mapa_residencial_grande.json", "residencial/mapa_referencia_grande.png"),
                                     ("comercial", "comercial/mapa_comercial.json", "comercial/mapa_referencia_comercial.png"),
                                     ("lluvia", "lluvia/mapa_lluvia.json", "residencial/mapa_referencia_grande.png")):
            data = json.load(open(os.path.join(base, archivo), encoding="utf-8"))
            t = data.get("turbos") or colocar(data, CANTIDAD[zona])
            vista(data, t, os.path.join(base, fondo), os.path.join(base, zona, "turbos_vista.png"))
            print(zona, [(x["nombre"], x["x"], x["y"], x["desvio"]) for x in t])
    elif "--aplicar" in sys.argv:
        aplicar()
    else:
        print(__doc__)
