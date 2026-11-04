"""Música y efectos 8 bits de DeliveryBrenns, sintetizados (numpy + wave, nada externo).

Exporta WAV mono 44.1 kHz, 16 bits:
  Assets/Audio/Musica  musica_menu, musica_nivel1_residencial, musica_nivel2_comercial, musica_nivel3_lluvia
                       (loops) y fanfarria_final (una vez)
  Assets/Audio/SFX     clics, pausa, tienda, pedidos, reloj, choques, claxon, motor (loop), lluvia (loop), trueno,
                       jingles

Instrumentos tipo consola: pulso (onda cuadrada de ciclo variable) hecho por suma de armónicos hasta ~9 kHz para que
no suene estridente, triángulo para el bajo y ruido LFSR para la percusión. Los loops se mezclan en un búfer
"circular": las colas de las notas, el eco y los filtros dan la vuelta al principio, así el último sample empata con
el primero y no hay corte al repetir. Todo lleva envolvente (ataque y salida de unos ms) para evitar clics.

Uso: python generate_audio.py            (escribe los WAV y un resumen con duraciones)
     python generate_audio.py --revisar  (solo revisa los WAV que ya existen)
     python generate_audio.py --efecto queja  (solo ese efecto; los demás no se tocan)
"""
import sys
import wave
from pathlib import Path

import numpy as np

SR = 44100
RAIZ = Path(r"C:\Users\kangu\DeliveryBrenns\Assets\Audio")
MUSICA = RAIZ / "Musica"
SFX = RAIZ / "SFX"
TECHO_ARMONICOS = 9000.0   # Hz: arriba de esto el pulso ya no suma armónicos (suave, sin aliasing)


# ---------------------------------------------------------------------------------------------- básicos

def hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def tiempo(seg):
    return np.arange(int(seg * SR)) / SR


def fase_de(frec, n, vibrato=0.0, vel_vibrato=5.5, retraso_vibrato=0.25):
    """Ciclos acumulados (fase/2π) de una nota; frec puede ser un número o un arreglo (barridos)."""
    f = np.broadcast_to(np.asarray(frec, dtype=float), (n,)).copy()
    if vibrato > 0:
        t = np.arange(n) / SR
        rampa = np.clip((t - retraso_vibrato) / 0.2, 0, 1)
        f *= 1 + vibrato * rampa * np.sin(2 * np.pi * vel_vibrato * t)
    return np.cumsum(f) / SR, f


def pulso(frec, seg, ciclo=0.5, vibrato=0.0):
    """Onda de pulso limitada en banda (serie de Fourier hasta TECHO_ARMONICOS)."""
    n = int(seg * SR)
    fase, f = fase_de(frec, n, vibrato)
    x = np.zeros(n)
    fmax = float(np.max(f))
    k = 1
    while k * fmax < TECHO_ARMONICOS and k < 60:
        a = 2.0 / (k * np.pi) * np.sin(k * np.pi * ciclo)
        if abs(a) > 1e-6:
            # los armónicos cerca del techo se atenúan un poco (más redondo, menos "zumbido")
            x += a * np.cos(2 * np.pi * k * fase) * (1 - 0.5 * (k * fmax / TECHO_ARMONICOS) ** 2)
        k += 1
    return x * 1.6


def triangulo(frec, seg, pasos=16):
    """Triángulo con 16 escalones de volumen, como el canal de bajo de las consolas."""
    n = int(seg * SR)
    fase, _ = fase_de(frec, n)
    p = fase % 1.0
    tri = 4 * np.abs(p - 0.5) - 1
    if pasos:
        tri = np.round(tri * (pasos / 2)) / (pasos / 2)
        tri = paso_bajo(tri, 6000)   # quita el filo de los escalones
    return tri


def seno(frec, seg):
    n = int(seg * SR)
    fase, _ = fase_de(frec, n)
    return np.sin(2 * np.pi * fase)


_lfsr_cache = {}


def ruido(seg, corto=False, semilla=1):
    """Ruido LFSR de 15 bits (modo corto = 'metálico' de 93 pasos). Se re-muestrea a una tasa de reloj fija."""
    n = int(seg * SR)
    clave = (corto, semilla)
    if clave not in _lfsr_cache:
        reg = 1 + semilla
        sal = np.empty(32767)
        for i in range(32767):
            b = (reg ^ (reg >> (6 if corto else 1))) & 1
            reg = (reg >> 1) | (b << 14)
            sal[i] = 1.0 if reg & 1 else -1.0
        _lfsr_cache[clave] = sal
    tabla = _lfsr_cache[clave]
    paso = 0.45   # ~20 kHz de reloj de ruido
    idx = (np.arange(n) * paso + semilla * 997).astype(int) % len(tabla)
    return tabla[idx]


def ruido_blanco(seg, semilla=0):
    return np.random.default_rng(semilla).uniform(-1, 1, int(seg * SR))


def envolvente(n, ataque=0.005, decaimiento=0.0, sostenido=1.0, salida=0.02):
    t = np.arange(n) / SR
    dur = n / SR
    e = np.ones(n)
    if ataque > 0:
        e = np.minimum(e, t / ataque)
    if decaimiento > 0:
        d = np.clip((t - ataque) / decaimiento, 0, 1)
        e = np.where(t > ataque, np.minimum(e, 1 - (1 - sostenido) * d), e)
    if salida > 0:
        e = np.minimum(e, np.clip((dur - t) / salida, 0, 1))
    return np.clip(e, 0, 1)


def caida(n, tau):
    return np.exp(-np.arange(n) / SR / tau)


def paso_bajo(x, corte, circular=False):
    """Filtro de un polo. circular=True: arranca con el estado del final (para loops sin costura)."""
    a = np.exp(-2 * np.pi * corte / SR)
    b = 1 - a
    from_scipy = None
    try:
        from scipy.signal import lfilter
        from_scipy = lfilter
    except ImportError:
        pass
    if circular:
        doble = np.concatenate([x, x])
        y = _iir(doble, a, b, from_scipy)
        return y[len(x):]
    return _iir(x, a, b, from_scipy)


def _iir(x, a, b, lfilter):
    if lfilter is not None:
        return lfilter([b], [1, -a], x)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = b * v + a * s
        y[i] = s
    return y


def paso_alto(x, corte, circular=False):
    return x - paso_bajo(x, corte, circular)


def suavizar_bordes(x, entrada=0.003, salida=0.012):
    x = x.copy()
    ne, ns = int(entrada * SR), int(salida * SR)
    if ne:
        x[:ne] *= np.linspace(0, 1, ne)
    if ns:
        x[-ns:] *= np.linspace(1, 0, ns) ** 2
    return x


def normalizar(x, pico=0.8):
    m = np.max(np.abs(x))
    return x * (pico / m) if m > 0 else x


def mezclar_en(dest, x, inicio, circular):
    """Suma x en dest desde la muestra 'inicio'; en un loop lo que sobra da la vuelta al principio."""
    n = len(dest)
    if circular:
        idx = (np.arange(len(x)) + inicio) % n
        np.add.at(dest, idx, x)
    else:
        fin = min(n, inicio + len(x))
        if fin > inicio:
            dest[inicio:fin] += x[:fin - inicio]


def eco(x, retraso, ganancia, repeticiones=3, circular=True):
    d = int(retraso * SR)
    y = x.copy()
    for r in range(1, repeticiones + 1):
        if circular:
            y += (ganancia ** r) * np.roll(x, d * r)
        else:
            s = np.zeros_like(x)
            if d * r < len(x):
                s[d * r:] = x[:len(x) - d * r]
            y += (ganancia ** r) * s
    return y


def escribir(ruta, x):
    ruta.parent.mkdir(parents=True, exist_ok=True)
    datos = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(ruta), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(datos.tobytes())


# ---------------------------------------------------------------------------------------------- instrumentos de la música

def nota_melodia(midi, seg, ciclo=0.5, vel=1.0, vibrato=0.004, corte=4200):
    x = pulso(hz(midi), seg, ciclo, vibrato if seg > 0.3 else 0.0)
    x *= envolvente(len(x), 0.006, 0.12, 0.65, min(0.06, seg * 0.3))
    return paso_bajo(x, corte) * vel


def nota_arpegio(midi, seg, vel=1.0, ciclo=0.125):
    x = pulso(hz(midi), seg, ciclo)
    x *= envolvente(len(x), 0.003, 0.05, 0.35, min(0.03, seg * 0.3))
    return paso_bajo(x, 3200) * vel


def nota_bajo(midi, seg, vel=1.0):
    x = triangulo(hz(midi), seg)
    return x * envolvente(len(x), 0.004, 0.0, 1.0, min(0.04, seg * 0.25)) * vel


def bombo(vel=1.0):
    seg = 0.18
    n = int(seg * SR)
    t = np.arange(n) / SR
    f = 50 + 110 * np.exp(-t / 0.03)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * caida(n, 0.06)
    nc = int(0.006 * SR)
    x[:nc] += paso_bajo(ruido(0.006), 3000) * np.hanning(nc) * 0.25   # golpecito del parche, con envolvente
    return suavizar_bordes(x, 0.001, 0.02) * vel


def caja(vel=1.0):
    seg = 0.16
    n = int(seg * SR)
    r = paso_alto(paso_bajo(ruido(seg, semilla=3), 5000), 600) * caida(n, 0.05)
    tono = seno(190, seg) * caida(n, 0.03) * 0.5
    return suavizar_bordes(r * 1.2 + tono, 0.001, 0.02) * vel


def platillo(vel=1.0, abierto=False):
    seg = 0.25 if abierto else 0.05
    n = int(seg * SR)
    r = paso_alto(ruido(seg, corto=False, semilla=7), 6000) * caida(n, 0.09 if abierto else 0.015)
    return suavizar_bordes(r, 0.001, 0.01) * vel


# ---------------------------------------------------------------------------------------------- composición

ESCALA_MAYOR = [0, 2, 4, 5, 7, 9, 11]
ESCALA_MENOR = [0, 2, 3, 5, 7, 8, 10]


def acorde(raiz_tonalidad, escala, grado):
    """Tríada sobre el grado (0 = I) de la escala: notas como semitonos desde la tónica."""
    return [escala[(grado + s) % 7] + 12 * ((grado + s) // 7) for s in (0, 2, 4)]


def tonos_escala(tonica, escala, bajo, alto):
    return [m for m in range(bajo, alto + 1) if (m - tonica) % 12 in escala]


class Cancion:
    def __init__(self, bpm, compases, circular=True, cola=0.0):
        self.bpm = bpm
        self.beat = 60.0 / bpm
        self.compases = compases
        self.circular = circular
        self.dur = compases * 4 * self.beat
        self.n = int(round((self.dur + cola) * SR))
        self.pistas = {}

    def pista(self, nombre):
        if nombre not in self.pistas:
            self.pistas[nombre] = np.zeros(self.n)
        return self.pistas[nombre]

    def poner(self, nombre, x, beat):
        mezclar_en(self.pista(nombre), x, int(round(beat * self.beat * SR)), self.circular)


def melodia(rng, tonica, escala, progresion, ritmos, rango, beats_por_acorde=4, motivo_cada=2):
    """Frases a partir de patrones de ritmo: en tiempos fuertes notas del acorde, en débiles pasos de la escala,
    buscando moverse poco (más cantable). Cada 'motivo_cada' acordes se repite el motivo transportado al acorde,
    para que tenga forma y no suene al azar. Devuelve [(beat, duración, midi)]."""
    notas = []
    tonos = tonos_escala(tonica, escala, *rango)
    previa = tonos[len(tonos) // 2]
    motivo = None
    for i, grado in enumerate(progresion):
        base = i * beats_por_acorde
        ac = [(tonica + s) % 12 for s in acorde(tonica, escala, grado)]
        ritmo = ritmos[i % len(ritmos)]
        if i % motivo_cada == 0 or motivo is None:
            motivo = []
            for (b, d) in ritmo:
                fuerte = abs(b - round(b)) < 1e-6 and int(round(b)) % 2 == 0
                if fuerte:
                    candidatos = [m for m in tonos if m % 12 in ac]
                else:
                    candidatos = [m for m in tonos if abs(m - previa) <= 3 and m != previa]
                    if not candidatos:
                        candidatos = tonos
                pesos = np.array([1.0 / (1 + abs(m - previa)) ** 1.6 for m in candidatos])
                m = int(rng.choice(candidatos, p=pesos / pesos.sum()))
                motivo.append((b, d, m))
                previa = m
            notas += [(base + b, d, m) for (b, d, m) in motivo]
        else:
            # mismo ritmo y contorno, ajustado a las notas del nuevo acorde
            sal = []
            for (b, d, m) in motivo:
                fuerte = abs(b - round(b)) < 1e-6 and int(round(b)) % 2 == 0
                if fuerte:
                    m2 = min((t for t in tonos if t % 12 in ac), key=lambda t: abs(t - m))
                else:
                    m2 = min(tonos, key=lambda t: abs(t - m))
                sal.append((base + b, d, m2))
            previa = sal[-1][2]
            notas += sal
    return notas


def musica(nombre, bpm, compases, tonica, escala, progresion, ritmos, rango, ciclo_mel, estilo, semilla,
           ganancias, eco_mel=(0.0, 0.0), corte_final=6000):
    rng = np.random.default_rng(semilla)
    c = Cancion(bpm, compases)
    total_acordes = compases  # un acorde por compás
    prog = [progresion[i % len(progresion)] for i in range(total_acordes)]

    # melodía: se compone por bloques de 8 compases y el bloque B cambia de ritmo y sube de registro
    mel = []
    bloques = (total_acordes + 7) // 8
    for blq in range(bloques):
        sub = prog[blq * 8:(blq + 1) * 8]
        es_b = blq % 3 == 1
        r = ritmos["B"] if es_b else ritmos["A"]
        rg = (rango[0] + (2 if es_b else 0), rango[1] + (3 if es_b else 0))
        frase = melodia(rng, tonica, escala, sub, r, rg)
        mel += [(b + blq * 32, d, m) for (b, d, m) in frase]
    # el último compás descansa en la tónica para enlazar con el principio del loop
    mel = [(b, d, m) for (b, d, m) in mel if b < (compases - 1) * 4]
    final = min((m for m in range(rango[0], rango[1] + 1) if m % 12 == tonica % 12), key=lambda m: abs(m - mel[-1][2]))
    mel.append(((compases - 1) * 4, 2.5, final))
    for (b, d, m) in mel:
        c.poner("melodia", nota_melodia(m, d * c.beat * 0.92, ciclo_mel), b)

    # acompañamiento
    for i, grado in enumerate(prog):
        base = i * 4
        ac = [tonica + s for s in acorde(tonica, escala, grado)]
        raiz_bajo = ac[0] - 24 if ac[0] - 24 >= 36 else ac[0] - 12
        quinta = raiz_bajo + (ac[2] - ac[0])
        if estilo == "tranquilo":       # menú: bajo raíz-quinta en negras, arpegio en corcheas
            for k, nota in enumerate([raiz_bajo, quinta, raiz_bajo + 12, quinta]):
                c.poner("bajo", nota_bajo(nota, c.beat * 0.9), base + k)
            arp = [ac[0], ac[1], ac[2], ac[1] + 12 - 12, ac[0] + 12, ac[2], ac[1], ac[2]]
            for k, nota in enumerate(arp):
                c.poner("arpegio", nota_arpegio(nota, c.beat * 0.45), base + k * 0.5)
            for k in range(4):
                if k in (0, 2):
                    c.poner("bateria", bombo(0.8), base + k)
                if k in (1, 3):
                    c.poner("bateria", caja(0.35), base + k)
                c.poner("bateria", platillo(0.25), base + k + 0.5)
        elif estilo == "relajado":      # nivel 1: bajo en blancas que camina, acordes en negras suaves
            c.poner("bajo", nota_bajo(raiz_bajo, c.beat * 1.9), base)
            c.poner("bajo", nota_bajo(quinta if i % 2 == 0 else raiz_bajo + 12, c.beat * 1.4), base + 2)
            c.poner("bajo", nota_bajo(quinta - 2 if i % 2 else quinta, c.beat * 0.45), base + 3.5)
            for k in range(4):
                for nota in ac:
                    c.poner("arpegio", nota_arpegio(nota + 12, c.beat * 0.3, 0.45, 0.25), base + k + 0.5)
            c.poner("bateria", bombo(0.7), base)
            c.poner("bateria", bombo(0.45), base + 2.5)
            c.poner("bateria", caja(0.25), base + 2)
            for k in range(4):
                c.poner("bateria", platillo(0.2), base + k)
        elif estilo == "movido":        # nivel 2: bajo en corcheas de octava, batería con semicorcheas
            for k in range(8):
                nota = raiz_bajo + (12 if k % 2 else 0)
                c.poner("bajo", nota_bajo(nota, c.beat * 0.42), base + k * 0.5)
            arp = [ac[0], ac[1], ac[2], ac[0] + 12]
            for k in range(16):
                c.poner("arpegio", nota_arpegio(arp[k % 4] + 12, c.beat * 0.22, 0.7), base + k * 0.25)
            for b in (0, 1.5, 2.5):
                c.poner("bateria", bombo(0.85), base + b)
            for b in (1, 3):
                c.poner("bateria", caja(0.5), base + b)
            for k in range(8):
                c.poner("bateria", platillo(0.3 if k % 2 == 0 else 0.18), base + k * 0.5)
            if i % 4 == 3:
                c.poner("bateria", platillo(0.25, abierto=True), base + 3.5)
        elif estilo == "cuento":        # intro (la historia): arpegio suave de pulso que manda, bajo en blancas
            arp = [ac[0], ac[1], ac[2], ac[1] + 12, ac[2] + 12, ac[1] + 12, ac[2], ac[1]]
            for k, nota in enumerate(arp):
                c.poner("arpegio", nota_arpegio(nota, c.beat * 0.48, 0.9, 0.25), base + k * 0.5)
            c.poner("bajo", nota_bajo(raiz_bajo, c.beat * 1.9), base)
            c.poner("bajo", nota_bajo(quinta, c.beat * 1.9), base + 2)
            c.poner("bateria", bombo(0.35), base)                     # percusión muy leve
            for k in (1, 2, 3):
                c.poner("bateria", platillo(0.12), base + k)
            if i % 4 == 3:
                c.poner("bateria", platillo(0.1, abierto=True), base + 3.5)
        elif estilo == "nocturno":      # nivel 3: notas largas, arpegio lento de triángulo, casi sin batería
            c.poner("bajo", nota_bajo(raiz_bajo, c.beat * 3.8, 0.9), base)
            arp = [ac[0], ac[2], ac[1] + 12, ac[2]]
            for k in range(8):
                c.poner("arpegio", nota_arpegio(arp[k % 4], c.beat * 0.8, 0.6, 0.25), base + k * 0.5)
            c.poner("bateria", bombo(0.5), base)
            c.poner("bateria", platillo(0.12), base + 1)
            c.poner("bateria", platillo(0.12), base + 3)
            if i % 2 == 1:
                c.poner("bateria", caja(0.15), base + 3)

    if eco_mel[0] > 0:
        c.pistas["melodia"] = eco(c.pistas["melodia"], eco_mel[0], eco_mel[1])
    mezcla = sum(c.pistas[k] * ganancias[k] for k in c.pistas)
    mezcla = paso_bajo(mezcla, corte_final, circular=True)
    mezcla -= np.mean(mezcla)
    # misma sonoridad para todas las pistas (RMS), con techo de pico
    mezcla *= 0.13 / np.sqrt(np.mean(mezcla ** 2))
    if np.max(np.abs(mezcla)) > 0.7:
        mezcla = np.tanh(mezcla / 0.7) * 0.7
    escribir(MUSICA / f"{nombre}.wav", mezcla)
    return mezcla


def fanfarria():
    c = Cancion(140, 3, circular=False, cola=1.2)
    b = c.beat
    # ta-ta-ta-táaa y acorde final con arpegio hacia arriba
    frase = [(0, 0.33, 67), (0.33, 0.33, 67), (0.66, 0.33, 67), (1, 1, 72), (2, 0.5, 70), (2.5, 0.5, 72),
             (3, 0.5, 74), (3.5, 0.5, 76), (4, 0.75, 77), (4.75, 0.25, 76), (5, 0.5, 74), (5.5, 0.5, 76), (6, 4, 79)]
    for (bt, d, m) in frase:
        c.poner("melodia", nota_melodia(m, d * b * 0.95, 0.5, vibrato=0.006), bt)
        c.poner("arpegio", nota_arpegio(m - 12, d * b * 0.9, 0.5, 0.25), bt)
    for (bt, notas) in [(0, (48, 55)), (1, (48, 52, 55)), (2, (46, 53)), (4, (53, 57, 60)), (6, (48, 55, 64))]:
        for m in notas:
            c.poner("arpegio", nota_arpegio(m + 12, (2 if bt < 6 else 4) * b, 0.35, 0.5), bt)
        c.poner("bajo", nota_bajo(notas[0] - 12, (2 if bt < 6 else 4) * b * 0.95), bt)
    for k, m in enumerate([60, 64, 67, 72, 76, 79, 84]):
        c.poner("arpegio", nota_arpegio(m, 0.25 * b, 0.6, 0.125), 6 + k * 0.125)
    for bt in (0, 1, 2, 3, 4, 5):
        c.poner("bateria", caja(0.4), bt + 0.66 if bt == 0 else bt)
    c.poner("bateria", bombo(1.0), 6)
    c.poner("bateria", platillo(0.5, abierto=True), 6)
    mezcla = c.pistas["melodia"] * 0.32 + c.pistas["arpegio"] * 0.22 + c.pistas["bajo"] * 0.4 + c.pistas["bateria"] * 0.35
    mezcla = eco(mezcla, 0.18, 0.25, 2, circular=False)
    mezcla = suavizar_bordes(paso_bajo(mezcla, 6000), 0.002, 0.4)
    mezcla *= 0.15 / np.sqrt(np.mean(mezcla[: int(3 * b * 2 * SR)] ** 2))
    if np.max(np.abs(mezcla)) > 0.75:
        mezcla = np.tanh(mezcla / 0.75) * 0.75
    escribir(MUSICA / "fanfarria_final.wav", mezcla)


def todas_las_musicas():
    # menú e intro: alegre, pastel, tranquila (Do mayor, 100 bpm, 20 compases = 48 s)
    ritmos_menu = {"A": [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 2)]],
                   "B": [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 2), (2, 1), (3, 1)]]}
    musica("musica_menu", 100, 20, 60, ESCALA_MAYOR, [0, 5, 3, 4], ritmos_menu, (67, 81), 0.5, "tranquilo", 11,
           {"melodia": 0.30, "arpegio": 0.10, "bajo": 0.42, "bateria": 0.22}, eco_mel=(0.3, 0.2))
    # nivel 1, Residencial de día: relajado (Sol mayor, 88 bpm, 16 compases = 43.6 s)
    ritmos_n1 = {"A": [[(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1), (1, 1), (2, 2)]],
                 "B": [[(0, 0.5), (0.5, 1), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 3), (3, 1)]]}
    musica("musica_nivel1_residencial", 88, 16, 67, ESCALA_MAYOR, [0, 2, 3, 0, 5, 1, 4, 4], ritmos_n1, (66, 79),
           0.25, "relajado", 23, {"melodia": 0.30, "arpegio": 0.12, "bajo": 0.45, "bateria": 0.22}, eco_mel=(0.34, 0.18))
    # nivel 2, Comercial al atardecer: más movida (Re menor / Fa, 132 bpm, 28 compases = 50.9 s)
    ritmos_n2 = {"A": [[(0, 0.5), (0.5, 0.5), (1, 0.75), (1.75, 0.75), (2.5, 0.5), (3, 1)],
                       [(0, 0.75), (0.75, 0.75), (1.5, 0.5), (2, 2)]],
                 "B": [[(0, 0.25), (0.25, 0.25), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 0.5), (3.5, 0.5)],
                       [(0, 1.5), (1.5, 1.5), (3, 1)]]}
    musica("musica_nivel2_comercial", 132, 28, 62, ESCALA_MENOR, [0, 5, 2, 6], ritmos_n2, (65, 79), 0.5, "movido",
           37, {"melodia": 0.27, "arpegio": 0.08, "bajo": 0.40, "bateria": 0.26}, corte_final=6500)
    # nivel 3, lluvia de noche: melancólica y lenta (La menor, 72 bpm, 15 compases = 50 s)
    ritmos_n3 = {"A": [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)]],
                 "B": [[(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)], [(0, 4)]]}
    musica("musica_nivel3_lluvia", 72, 15, 57, ESCALA_MENOR, [0, 5, 2, 6, 0, 3, 5, 4], ritmos_n3, (64, 76), 0.25,
           "nocturno", 51, {"melodia": 0.30, "arpegio": 0.14, "bajo": 0.45, "bateria": 0.18}, eco_mel=(0.42, 0.32),
           corte_final=4200)
    # intro, la historia: tono de cuento, tempo moderado (Fa mayor, 90 bpm, 18 compases = 48 s), distinta del menú
    ritmos_cuento = {"A": [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 1), (1, 1), (2, 1), (3, 1)]],
                     "B": [[(0, 2), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)]]}
    musica("musica_historia", 90, 18, 65, ESCALA_MAYOR, [0, 5, 3, 4, 0, 2, 3, 4], ritmos_cuento, (69, 81), 0.5, "cuento",
           63, {"melodia": 0.24, "arpegio": 0.20, "bajo": 0.42, "bateria": 0.15}, eco_mel=(0.33, 0.25), corte_final=5000)
    fanfarria()


# ---------------------------------------------------------------------------------------------- efectos

def campana(midi, seg, vel=1.0, brillo=1.0):
    """Campanita: seno + parciales inarmónicos que se apagan antes (sin onda cuadrada: no chilla)."""
    f = hz(midi)
    n = int(seg * SR)
    x = seno(f, seg) * caida(n, seg * 0.45)
    x += 0.35 * brillo * seno(f * 2.76, seg) * caida(n, seg * 0.18)
    x += 0.15 * brillo * seno(f * 5.4, seg) * caida(n, seg * 0.08)
    x += 0.25 * pulso(f, seg, 0.25) * caida(n, 0.04)   # "ataque" 8 bits
    return suavizar_bordes(x, 0.002, 0.03) * vel


def secuencia(eventos, seg):
    x = np.zeros(int(seg * SR))
    for (t, s) in eventos:
        mezclar_en(x, s, int(t * SR), False)
    return x


def golpe(f0, f1, seg, ruido_vel, tau, corte, semilla=1):
    n = int(seg * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / (seg * 0.25))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * caida(n, tau)
    r = paso_bajo(ruido(seg, semilla=semilla), corte) * caida(n, tau * 0.6) * ruido_vel
    return x + r


def triturar(x, bits=5, cada=3):
    """Bitcrush suave: menos niveles y retención de muestra (sonido 8 bits del choque)."""
    y = np.repeat(x[::cada], cada)[: len(x)]
    q = 2 ** (bits - 1)
    return np.round(y * q) / q


# pico de cada efecto (lo suave suena suave: el hover y el tick casi no se notan; el choque fuerte sí)
PICOS = {"ui_hover": 0.2, "ui_clic": 0.5, "tick": 0.45, "pausa_abrir": 0.5, "pausa_cerrar": 0.5, "choque_fuerte": 0.85,
         "choque_medio": 0.7, "choque_leve": 0.5, "bache": 0.7, "pedido_cancelado": 0.5, "claxon": 0.55, "trueno": 0.7,
         "pedido_nuevo": 0.6, "entrega": 0.65, "propina": 0.55, "compra_moto": 0.65, "equipar_moto": 0.5,
         "jingle_victoria": 0.6, "jingle_derrota": 0.55,
         "historia_pop": 0.5, "maquina_tic_1": 0.22, "maquina_tic_2": 0.22, "maquina_tic_3": 0.22, "historia_clic": 0.35,
         "historia_remate": 0.6, "moto_arranque": 0.6, "queja": 0.5}


def efectos(solo=None):
    """solo: lista de nombres a escribir (los demás no se tocan); None escribe todos y los loops."""
    s = {}
    s["ui_clic"] = suavizar_bordes(pulso(np.linspace(1250, 900, int(0.045 * SR)), 0.045, 0.25)
                                   * caida(int(0.045 * SR), 0.018), 0.001, 0.008) * 0.55
    s["ui_hover"] = suavizar_bordes(paso_bajo(triangulo(1568, 0.035, 0), 5000) * caida(int(0.035 * SR), 0.012),
                                    0.002, 0.01) * 0.22
    s["pausa_abrir"] = secuencia([(0, nota_arpegio(76, 0.07, 1, 0.25)), (0.07, nota_arpegio(81, 0.12, 1, 0.25))], 0.22) * 0.6
    s["pausa_cerrar"] = secuencia([(0, nota_arpegio(81, 0.07, 1, 0.25)), (0.07, nota_arpegio(76, 0.12, 1, 0.25))], 0.22) * 0.6
    cha = paso_alto(ruido(0.09, corto=True, semilla=5), 2500) * caida(int(0.09 * SR), 0.025)
    ching = campana(96, 0.7, 0.9) + 0.6 * campana(100, 0.7, 0.7)
    s["compra_moto"] = secuencia([(0, cha * 0.7), (0.0, campana(84, 0.12, 0.4)), (0.11, cha * 0.5), (0.13, ching)], 0.9)
    s["equipar_moto"] = secuencia([(i * 0.045, nota_arpegio(m, 0.07, 1, 0.25)) for i, m in enumerate([72, 76, 79, 84])], 0.3) * 0.6
    s["pedido_nuevo"] = secuencia([(0, campana(88, 0.5)), (0.22, campana(84, 0.65))], 0.9) * 0.8
    s["entrega"] = secuencia([(0, campana(84, 0.35)), (0.09, campana(88, 0.35)), (0.18, campana(91, 0.7))], 0.9) * 0.8
    moneda = lambda: secuencia([(0, pulso(hz(83), 0.06, 0.5) * 0.6), (0.06, pulso(hz(88), 0.28, 0.5)
                               * caida(int(0.28 * SR), 0.08) * 0.6)], 0.34)
    s["propina"] = paso_bajo(secuencia([(0, moneda()), (0.1, moneda() * 0.7), (0.19, moneda() * 0.5)], 0.6), 6000)
    zumb = pulso(np.linspace(110, 82, int(0.5 * SR)), 0.5, 0.5, vibrato=0.02)
    s["pedido_cancelado"] = paso_bajo(zumb * envolvente(len(zumb), 0.01, 0.0, 1.0, 0.12), 1400) * 0.7
    s["tick"] = suavizar_bordes((seno(1760, 0.04) + 0.4 * seno(3520, 0.04)) * caida(int(0.04 * SR), 0.008), 0.001, 0.008) * 0.55
    fuerte = golpe(140, 38, 0.55, 1.2, 0.16, 1800, 2) + 0.5 * paso_alto(ruido(0.55, semilla=9), 1500) * caida(int(0.55 * SR), 0.05)
    s["choque_fuerte"] = suavizar_bordes(paso_bajo(triturar(np.tanh(fuerte * 1.5), 5, 3), 7000), 0.001, 0.08)
    s["choque_medio"] = suavizar_bordes(paso_bajo(triturar(golpe(260, 90, 0.3, 0.5, 0.08, 2500, 4), 6, 2), 7000), 0.001, 0.05) * 0.8
    s["choque_leve"] = suavizar_bordes(golpe(330, 160, 0.14, 0.3, 0.03, 3000, 6), 0.001, 0.03) * 0.5
    s["bache"] = secuencia([(0, golpe(180, 55, 0.18, 0.7, 0.05, 1500, 8)), (0.13, golpe(220, 80, 0.1, 0.3, 0.03, 2000, 8) * 0.45),
                            (0.21, golpe(240, 100, 0.07, 0.2, 0.02, 2000, 8) * 0.2)], 0.35) * 0.85
    pii = pulso(415, 0.5, 0.5) + 0.8 * pulso(523, 0.5, 0.5)
    s["claxon"] = paso_bajo(pii * envolvente(len(pii), 0.01, 0.05, 0.85, 0.06), 2600) * 0.45
    s["jingle_victoria"] = secuencia(
        [(i * 0.11, nota_melodia(m, 0.1, 0.5, 1, 0)) for i, m in enumerate([72, 76, 79])]
        + [(0.33, nota_melodia(84, 0.9, 0.5, 1, 0.006)), (0.33, nota_arpegio(76, 0.9, 0.5, 0.25)),
           (0.33, nota_arpegio(79, 0.9, 0.5, 0.25)), (0.33, nota_bajo(48, 0.9))], 1.4) * 0.6
    s["jingle_derrota"] = secuencia(
        [(0, nota_melodia(67, 0.3, 0.5, 1, 0)), (0.32, nota_melodia(66, 0.3, 0.5, 1, 0)),
         (0.64, nota_melodia(65, 0.3, 0.5, 1, 0)), (0.96, nota_melodia(64, 1.0, 0.5, 1, 0.02)),
         (0.96, nota_bajo(40, 1.0))], 2.1) * 0.6
    s["trueno"] = trueno()
    # ---- intro (la historia) ----
    sube = pulso(np.linspace(380, 980, int(0.07 * SR)), 0.07, 0.25) * envolvente(int(0.07 * SR), 0.003, 0.0, 1.0, 0.03)
    brillo = secuencia([(0.04 + i * 0.035, campana(m, 0.22, 0.5, 1.2)) for i, m in enumerate([96, 100, 103])], 0.36)
    s["historia_pop"] = secuencia([(0, paso_bajo(sube, 5000))], 0.36) + brillo
    for i, f in enumerate([2100, 2450, 2800], start=1):   # tres tonos para que la máquina no suene repetida
        n = int(0.03 * SR)
        golpecito = paso_alto(ruido(0.03, semilla=10 + i), 2500) * caida(n, 0.004)
        s[f"maquina_tic_{i}"] = golpecito + 0.5 * seno(f, 0.03) * caida(n, 0.006)
    n = int(0.06 * SR)
    s["historia_clic"] = paso_bajo(triangulo(np.linspace(1050, 820, n), 0.06, 0), 4000) * caida(n, 0.02)
    subida = [(i * 0.045, nota_arpegio(m, 0.09, 1, 0.25)) for i, m in enumerate([72, 76, 79, 84, 88, 91])]
    acorde_final = [(0.28, campana(m, 1.2, 0.6)) for m in (84, 88, 91)]
    destellos = [(0.36 + i * 0.09, campana(m, 0.3, 0.25, 1.4)) for i, m in enumerate([103, 100, 105, 108])]
    s["historia_remate"] = secuencia(subida + acorde_final + destellos + [(0.28, nota_bajo(48, 0.9, 0.8))], 1.5)
    s["moto_arranque"] = arranque_moto()
    # queja (HUD): dos "buu" cortos que bajan, cuadrados y apagados; más corto y agudo que pedido_cancelado
    s["queja"] = secuencia([(0, paso_bajo(pulso(hz(62), 0.09, 0.5) * envolvente(int(0.09 * SR), 0.004, 0.0, 1.0, 0.03), 2600)),
                            (0.1, paso_bajo(pulso(np.linspace(hz(57), hz(54), int(0.16 * SR)), 0.16, 0.5)
                                            * envolvente(int(0.16 * SR), 0.004, 0.0, 1.0, 0.07), 2200))], 0.3) * 0.7
    for nombre, x in s.items():
        if solo is not None and nombre not in solo:
            continue
        x = suavizar_bordes(x - np.mean(x), 0.002, 0.01)
        escribir(SFX / f"{nombre}.wav", normalizar(x, PICOS.get(nombre, 0.7)))
    if solo is not None:
        return
    # loops (sin suavizar los bordes: empatan por construcción)
    # el juego le pone tono 0.8-1.6 según la velocidad: 200 Hz -> 160-320 Hz; la Chopper, 165 Hz -> 132-264 Hz
    escribir(SFX / "motor.wav", motor(200, 28, 0.6))
    escribir(SFX / "motor_grave.wav", motor(165, 22, 0.9))
    escribir(SFX / "lluvia.wav", lluvia())


def arranque_moto():
    """0.7 s, limpio y de 8 bits: arpegio ascendente de 4 notas en onda cuadrada (Do-Mi-Sol-Do) y un "ding"
    brillante al final. Sin ruido ni graves (la versión de octubre con la marcha y el rugido sonaba a gas)."""
    notas = [(i * 0.085, nota_arpegio(m, 0.12, 0.9, 0.5)) for i, m in enumerate([72, 76, 79, 84])]
    ding = [(0.34, campana(96, 0.36, 0.8, 1.2)), (0.34, nota_arpegio(91, 0.08, 0.35, 0.5))]
    return secuencia(notas + ding, 0.7)


def motor(f, golpes, brillo):
    """Loop de 1 s de motor pequeño: diente de sierra limitado en banda (armónicos hasta ~5 kHz) con una segunda
    sierra una octava arriba, y modulación de amplitud rápida ('golpes' por segundo, el traqueteo del cilindro).
    Sin ruido y sin nada por debajo de f (>= 130 Hz aun con el tono mínimo de 0.8 del juego). Frecuencias enteras en
    Hz: cada onda da ciclos completos y el final empata con el inicio."""
    seg = 1.0
    t = tiempo(seg)
    def sierra(fr):
        x = np.zeros_like(t)
        for k in range(1, int(5000 // fr) + 1):
            x += np.sin(2 * np.pi * k * fr * t) / k
        return x
    x = sierra(f) + 0.35 * brillo * sierra(2 * f)
    am = 0.62 + 0.38 * np.sin(2 * np.pi * golpes * t) ** 2          # traqueteo rápido, sin "pop" grave
    x = paso_bajo(x * am, 3500, circular=True)
    x = paso_alto(x, 110, circular=True)
    x -= np.mean(x)
    return normalizar(x, 0.38)                                       # volumen bajo


def lluvia():
    """Loop de 8 s: ruido rosa/blanco filtrado (paso de banda) + gotitas, todo circular."""
    seg = 8.0
    rng = np.random.default_rng(77)
    n = int(seg * SR)
    blanco = rng.uniform(-1, 1, n)
    siseo = paso_bajo(paso_alto(blanco, 900, circular=True), 7000, circular=True)
    fondo = paso_bajo(blanco, 400, circular=True) * 2.5
    x = siseo * 0.8 + fondo
    lento = paso_bajo(rng.uniform(-1, 1, n), 0.8, circular=True)       # la intensidad sube y baja un poco
    x *= 1 + 0.25 * lento / (np.max(np.abs(lento)) + 1e-9)
    for _ in range(140):
        t0 = rng.uniform(0, seg)
        g = paso_alto(ruido_blanco(0.012, int(t0 * 1000)), 3000) * caida(int(0.012 * SR), 0.003) * rng.uniform(0.2, 0.6)
        mezclar_en(x, g, int(t0 * SR), True)
    x -= np.mean(x)
    return normalizar(x, 0.5)


def trueno():
    """Trueno lejano: ruido café muy filtrado, con un retumbo que crece y se apaga en ~4 s."""
    seg = 4.5
    n = int(seg * SR)
    t = np.arange(n) / SR
    cafe = np.cumsum(ruido_blanco(seg, 3))
    cafe = paso_alto(cafe, 25)
    cafe = paso_bajo(paso_bajo(cafe, 220), 220)
    env = np.clip(t / 0.35, 0, 1) * np.exp(-np.maximum(t - 0.35, 0) / 1.2)
    rugido = 1 + 0.6 * np.sin(2 * np.pi * 1.7 * t) * np.sin(2 * np.pi * 0.6 * t)
    x = cafe * env * rugido
    return normalizar(x, 0.75)


# ---------------------------------------------------------------------------------------------- revisión

def leer(ruta):
    with wave.open(str(ruta)) as w:
        assert w.getnchannels() == 1 and w.getframerate() == SR and w.getsampwidth() == 2, ruta
        return np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(float) / 32767


LOOPS = {"musica_menu", "musica_historia", "musica_nivel1_residencial", "musica_nivel2_comercial", "musica_nivel3_lluvia",
         "motor", "motor_grave", "lluvia"}


def revisar():
    errores = []
    filas = []
    for carpeta in (MUSICA, SFX):
        for ruta in sorted(carpeta.glob("*.wav")):
            x = leer(ruta)
            dur = len(x) / SR
            pico = np.max(np.abs(x))
            nombre = ruta.stem
            nota = ""
            if nombre in LOOPS:
                # costura: el salto del último al primer sample no debe ser mayor que los saltos normales
                salto = abs(x[0] - x[-1])
                normal = np.percentile(np.abs(np.diff(x)), 99.9)
                nota = f"loop, costura {salto:.4f} (p99.9 de saltos {normal:.4f})"
                if salto > normal * 1.5 + 1e-3:
                    errores.append(f"{nombre}: costura del loop {salto:.4f}")
            else:
                if abs(x[0]) > 0.01 or abs(x[-1]) > 0.01:
                    errores.append(f"{nombre}: empieza o termina fuera de cero ({x[0]:.3f}, {x[-1]:.3f})")
            if pico >= 0.999:
                errores.append(f"{nombre}: satura")
            if nombre.startswith("musica_") and not 40 <= dur <= 60:
                errores.append(f"{nombre}: dura {dur:.1f} s (40 a 60)")
            filas.append(f"{carpeta.name}/{ruta.name}: {dur:.2f} s, pico {pico:.2f}, RMS {np.sqrt(np.mean(x ** 2)):.3f} {nota}")
    print("\n".join(filas))
    print("REVISIÓN: " + ("sin errores" if not errores else "ERRORES:\n- " + "\n- ".join(errores)))
    return not errores


if __name__ == "__main__":
    if "--efecto" in sys.argv:   # python generate_audio.py --efecto queja: solo ese efecto (los demás quedan igual)
        efectos([sys.argv[sys.argv.index("--efecto") + 1]])
    elif "--revisar" not in sys.argv:
        todas_las_musicas()
        efectos()
    sys.exit(0 if revisar() else 1)
