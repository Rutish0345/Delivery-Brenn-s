"""Genera la hoja de sprites del repartidor (diseño final) a partir de
repartidor_frente_modern.png. Usa colores extraídos de la referencia más los de la mochila de reparto."""
import json, os, sys
from collections import Counter
from PIL import Image

REF = r'C:\Users\kangu\DeliveryBrenns\Assets\Sprits\Personajes\repartidor_frente_modern.png'
OUT = sys.argv[1] if len(sys.argv) > 1 else r'C:\Users\kangu\pixel_town\personajes'
W, H = 56, 80
X0 = 13            # columna 0 de las plantillas ASCII = x 13 del canvas
SHADOW_RGB = (0x1a, 0x14, 0x28)

ref = Image.open(REF).convert('RGBA')
rpx = ref.load()

# ---- paleta: mismo orden/símbolos que dump.py -------------------------------
cnt = Counter(rpx[x, y] for y in range(H) for x in range(W) if rpx[x, y][3] > 0)
cols = sorted(cnt, key=lambda k: (k[3] < 255, sum(k[:3])))
SYM = [chr(i) for i in range(33, 127) if chr(i) != '.']
PAL = {SYM[i]: c for i, c in enumerate(cols)}
INV = {c: s for s, c in PAL.items()}
REF_COLORS = set(cols)

def ref_grid():
    return [[INV[rpx[x, y]] if rpx[x, y][3] > 0 else '.' for x in range(W)] for y in range(H)]

# ---- sombra fija -------------------------------------------------------------
def build_shadow():
    s = [['.'] * W for _ in range(H)]
    g = ref_grid()
    for y in range(H):
        for x in range(W):
            c = rpx[x, y]
            if c[3] and c[:3] == SHADOW_RGB:
                s[y][x] = g[y][x]
    # huecos tapados por los zapatos en la referencia
    t, r = INV[SHADOW_RGB + (120,)], INV[SHADOW_RGB + (80,)]
    fill = {73: {16: r, **{x: t for x in range(17, 24)}, **{x: t for x in range(27, 34)}, 34: r},
            74: {**{x: t for x in range(18, 24)}, **{x: t for x in range(29, 33)}, 33: r, 34: r}}
    for y, row in fill.items():
        for x, ch in row.items():
            if s[y][x] == '.':
                s[y][x] = ch
    return s

SHADOW = build_shadow()

def is_shadow(ch):
    return ch != '.' and PAL[ch][:3] == SHADOW_RGB and PAL[ch][3] < 255

# ---- utilidades de lienzo ----------------------------------------------------
def blank():
    return [['.'] * W for _ in range(H)]

def stamp(canvas, rows, top, left=X0):
    """Pega filas ASCII (con '.' transparente)."""
    for i, row in enumerate(rows):
        for j, ch in enumerate(row):
            if ch != '.' and ch != ' ':
                y, x = top + i, left + j
                if 0 <= y < H and 0 <= x < W:
                    canvas[y][x] = ch

def to_image(canvas):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        for x in range(W):
            if SHADOW[y][x] != '.':
                px[x, y] = PAL[SHADOW[y][x]]
    for y in range(H):
        for x in range(W):
            ch = canvas[y][x]
            if ch != '.':
                px[x, y] = PAL[ch]
    return im

# =============================================================================
# FRENTE: la referencia es el reposo; la caminata reutiliza sus pixeles
# =============================================================================
RG = ref_grid()
def ref_rows(y0, y1):
    return [''.join('.' if is_shadow(c) else c for c in RG[y][X0:X0 + 26]) for y in range(y0, y1 + 1)]

FRONT_UPPER = ref_rows(8, 53)      # gorra..caderas+manos
REF_LEGS = ref_rows(54, 74)        # piernas y zapatos (sin sombra)

def split_legs(rows):
    left = [r[:13] + '.' * 13 for r in rows]
    right = ['.' * 13 + r[13:] for r in rows]
    return left, right

def drop_rows(rows, drop_abs):
    """Quita filas (y absolutas de la referencia) del tramo de pierna 54..68."""
    leg, shoe = rows[:15], rows[15:]
    leg = [r for i, r in enumerate(leg) if 54 + i not in drop_abs]
    return leg, shoe

def front_like(upper, legs_rows, step):
    """step: None (reposo/pasada), 'L' o 'R' = pierna que se levanta."""
    c = blank()
    bob = 1 if step else 0
    L, R = split_legs(legs_rows)
    for side, rows in (('L', L), ('R', R)):
        if step is None:
            stamp(c, rows, 54)
            continue
        lifted = (side == step)
        if side == 'L':
            drops = {65, 61, 57} if lifted else {65}
        else:
            drops = {64, 65, 61} if lifted else {64}
        leg, shoe = drop_rows(rows, drops)
        lift = 2 if lifted else 0
        shoe_top = 69 - lift
        stamp(c, shoe, shoe_top)
        stamp(c, leg, shoe_top - len(leg))
    stamp(c, upper, 8 + bob)
    return c

# =============================================================================
# ESPALDA
# =============================================================================
BACK_UPPER = [
    "........6006..............",  # 8
    "....6666XddX666660........",  # 9
    "..66Xd^d]]ddd]]]]660......",  # 10
    ".6Xd]dd]P]]]]P]]]]]]B0....",  # 11
    ".6X]dd]]P]]]]]P]]]]]J0....",  # 12
    ".6X]d]]P]]]]]]P]]]]]]J0...",  # 13
    ".6X]]]]P]]]]]]]P]]]]]J0...",  # 14
    ".6XP]]]P]]]]]]]P]]]]PJ0...",  # 15
    ".6XPP]P]]]]]]]]]P]]PPJ0...",  # 16
    ".6XPPBP]]6666666P]PBBJ0...",  # 17
    ".6XPPBBP6'/>>//'6BBBBJ0...",  # 18
    ".6D666666PPPPPPB66666J0...",  # 19
    ".'/>>>>7>>>>>>7>>>>/''....",  # 20
    ".'/>7>>>/>>>7>>>/>>//'%...",  # 21
    ".Gj'//>>/>>>>//>>>/'jG....",  # 22
    ".Gm'//>//>>>//>>//'%mG....",  # 23
    ".G`'////>//////>//'%`G....",  # 24
    "..''/////'////'///'%......",  # 25
    "...''//'///'///'//'%......",  # 26
    "....''//'//'//'//''.......",  # 27
    ".....'%'GSZZZSG'%'........",  # 28
    "........GSZmZSG...........",  # 29
    "........GSZZZSG...........",  # 30
    "......<<<<<<<<<<1*........",  # 31
    "......<\\bbWWWWWQE*........",  # 32
    ".....<\\Q<<<<<<<<<1*.......",  # 33
    "....<\\QWWWWWWWWWWQE*......",  # 34
    "....<\\WWWWWWWWWWQQE*......",  # 35
    "...<bW<WWWWWWWWWQ<QE*.....",  # 36
    "...<bW<WWWWWWWWWQ<WQ1*....",  # 37
    "...<bW<WWWWWWWWQQ<QQE*....",  # 38
    "...<bW<WWWWWWWWQQ<QQE*....",  # 39
    "...<bW<WWWWWWWWQQ<QQE*....",  # 40
    "..<bWW<WWWWWWWWWQQ<WWE*...",  # 41
    "..<bWQ<WWWWWWWWWQQ<WQE*...",  # 42
    "..<bWQ<WWWWWWWWWQQ<WQE*...",  # 43
    "..<bWW<WWWWWWWWQQQ<QQE*...",  # 44
    ".<\\WW<WWWWWWWWWQQQQ<WQ1*..",  # 45
    ".<\\WW<WWWWWWWWWQQQQ<WQE*..",  # 46
    "<\\QWQ<WWWWWWWWWQQQQ<WWQE*.",  # 47
    "1bWWQ<WWWWWWWWWQQQQ<WWWE*.",  # 48
    ".<O<<<QQWWWWWWQQQQQ<<<1*..",  # 49
    ".GjjGQQQQQQQQQQQQQQQ<jjG..",  # 50
    ".GmmG<<<<<<<<<<<<<<<<mmG..",  # 51
] + FRONT_UPPER[52 - 8:]           # 52-53: caderas y manos

# piernas de espalda: las de la referencia sin brillo de puntera en zapatos
BACK_LEGS = [r.replace(',', '&') for r in REF_LEGS]

# =============================================================================
# LADO DERECHO
# =============================================================================
SIDE_UPPER = [
    ".........6000.............",  # 8
    "......666Xdd66660.........",  # 9
    "....66Xd^dd]]]]]660.......",  # 10
    "...6Xd]dd]]]]]]]]]]0......",  # 11
    "..6X]dd]]]]]]]]BPB]J0.....",  # 12
    "..6X]]d]]]]]]VBBV]]J0.....",  # 13
    "..6X]]]]]]]]]]VV]]]]J0....",  # 14
    "..6XP]]]]]]]]]]]]]]]J0....",  # 15
    "..6XPP]]]]]]]]]]]]]]J666..",  # 16
    "..6XPPBP]]]]]]]]]]P]ddd]]6",  # 17
    "..666BBBPPPPPPPPBBBBBBBB0.",  # 18
    "..'666666666666666000000..",  # 19
    "..'/>>>>>//>>>/>/'emmG....",  # 20
    "..'/>7>>>/>>>>/>'emmmG....",  # 21
    "..'//>/GjjjG/>/'e!!!mG....",  # 22
    "..'/>>/Gm_mG//'egi((mmG...",  # 23
    "..'/>//Gm_mG/'emmlo:mmG...",  # 24
    "..'//>/'GmmG'emmmmmmG.....",  # 25
    "...'//'//'emmmmmhhhG......",  # 26
    "....''/'GemmmmmmmmG.......",  # 27
    ".....'%'GSZmSGGGGG........",  # 28
    "........GSZZSG............",  # 29
    "........GUSSSG............",  # 30
    "......<\\Q<SZSG<<*.........",  # 31
    "......<\\QW<SZG<E*.........",  # 32
    ".....<\\QWWQ<<<nk<*........",  # 33
    ".....<\\WWWWWWWQ<nk<.......",  # 34
    ".....<bWWWWWWWQ<nk<.......",  # 35
    ".....<bWWWWWWWQ<nk<.......",  # 36
    ".....<bWWWWWWWQ<nk<.......",  # 37
    ".....<bWWWWWWWf<nk<.......",  # 38
    ".....<bWWWWWWQQ<nk<.......",  # 39
    ".....<bWWWWWWQQ<nk<.......",  # 40
    ".....<bWWWWWWQQ<nk<.......",  # 41
    ".....<bWWWWWWQQ<nk<.......",  # 42
    ".....<bWWWWWWQf<nk<.......",  # 43
    ".....<bWWWWWWQQ<nk<.......",  # 44
    ".....<bWWWWWWQQ<nk<.......",  # 45
    ".....<bWWWWWWQQ<nk<.......",  # 46
    ".....<bWWWWWWQQ<na<.......",  # 47
    ".....<bWWWWWWQQ<cc<.......",  # 48
    ".....<WQQQQQQQQ<99\".......",  # 49
    ".....<QQQQQQQQQ<99\".......",  # 50
    ".....<<<<<<<<<<<99\".......",  # 51
    "......\"9L999999@8\".......",  # 52
    ".......\"9L9999@8\"........",  # 53
]

ARM = [  # brazo cercano, hombro en y=32, columna local 0 = x de anclaje
    ".<<<<.",
    "<\\WWQ*",
    "<bWWQ*",
    "<bWWQ*",
    "<bWWQ*",
    "<bWWE*",
    "<bWQE*",
    "<bWWQ*",
    "<bWWQ*",
    "<bWWE*",
    "<bWQE*",
    "<bWWQ*",
    "<bWWQ*",
    "<bWQE*",
    "<bWQE*",
    "<QQQE*",
    "<QQQ1*",
    "<<<<1*",
    ".GjjG.",
    ".GmmG.",
    ".GmmG.",
    "..GG..",
]
ARM_X = 8 + X0

def sheared(sprite, shift, lift=0):
    """Desplaza cada fila proporcionalmente (balanceo) sin antialias."""
    n = len(sprite) - 1
    out = []
    for i, row in enumerate(sprite):
        off = round(shift * i / n)
        out.append((off, row))
    return out

def stamp_offsets(canvas, rows, top, left):
    for i, (off, row) in enumerate(rows):
        stamp(canvas, [row], top + i, left + off)

# piernas de lado: tiras de 7 px estampadas a lo largo de cadera->rodilla->tobillo
NEAR = {'thigh': '"9LFF@8"', 'shin': '"9L992+"', 'hem': '"2TFF@8"', 'cuff': '"' * 8}
FAR = {'thigh': '"29@@8+"', 'shin': '"29982+"', 'hem': '"+9@@8+"', 'cuff': '"' * 8}
NEAR_SHOE = ['"$,,,$#"....',
             '"&,,,,$$#"..',
             '"&,,,,$$$$#"',
             '"&&&$$$$$$$"',
             '.""""""""""".'[:12]]
FAR_SHOE = [r.replace(',', '&') for r in NEAR_SHOE]

def leg_path(hip_y, hip_x, knee_y, knee_x, ank_y, ank_x):
    pts = []
    for y in range(hip_y, ank_y + 1):
        if y <= knee_y:
            t = (y - hip_y) / max(1, knee_y - hip_y)
            x = hip_x + (knee_x - hip_x) * t
        else:
            t = (y - knee_y) / max(1, ank_y - knee_y)
            x = knee_x + (ank_x - knee_x) * t
        pts.append((y, int(round(x))))
    return pts

def draw_leg(c, strips, shoe, hip_y, hip_x, knee_y, knee_x, ank_y, ank_x):
    """hip_x etc. = columna izquierda de la tira (en coords de plantilla)."""
    pts = leg_path(hip_y, hip_x, knee_y, knee_x, ank_y, ank_x)
    n = len(pts)
    for i, (y, x) in enumerate(pts):
        if i == n - 1:
            key = 'cuff'
        elif i == n - 2:
            key = 'hem'
        elif y <= knee_y:
            key = 'thigh'
        else:
            key = 'shin'
        stamp(c, [strips[key]], y, X0 + x)
    stamp(c, shoe, ank_y + 1, X0 + ank_x)

GROUND = 74  # última fila de suela

def side_frame(near, far, bob, arm_shift):
    """near/far: (dx_rodilla, dx_tobillo, elevación)."""
    c = blank()
    hip_y = 54 + bob
    for strips, shoe, (kx, ax, lift), base in ((FAR, FAR_SHOE, far, 8), (NEAR, NEAR_SHOE, near, 10)):
        ank_y = GROUND - 5 - lift
        knee_y = hip_y + (ank_y - hip_y) // 2
        draw_leg(c, strips, shoe, hip_y, base, knee_y, base + kx, ank_y, base + ax)
    stamp(c, SIDE_UPPER, 8 + bob)
    stamp_offsets(c, sheared(ARM, arm_shift), 32 + bob, ARM_X)
    return c


# =============================================================================
# MOCHILA de reparto del Salón Brenn's (rosa, contorno oscuro, cierre dorado, tijeras doradas y moño).
# Va encima de cada cuadro ya armado y baja 1 px con el torso en los cuadros de paso (bob), sin desfase.
#  - espalda: completa, encima del personaje
#  - frente: correas sobre los hombros; el cuerpo de la mochila va detrás (solo asoma por los lados)
#  - lado (mira a la derecha): detrás de la espalda, con la correa sobre el hombro
# Los colores nuevos son solo estos; el resto sigue saliendo de la referencia.
# =============================================================================
M_OSCURO = (0x2A, 0x1F, 0x40, 255)
M_ROSA = (0xFF, 0x6F, 0xA5, 255)
M_ROSA_OSC = (0xC2, 0x41, 0x7A, 255)
M_ROSA_CLARO = (0xFF, 0xB3, 0xD1, 255)
M_DORADO = (0xFF, 0xD1, 0x66, 255)
M_DORADO_OSC = (0xE0, 0x9A, 0x3A, 255)
M_CREMA = (0xFF, 0xF1, 0xD0, 255)
MOCHILA_COLORES = {M_OSCURO, M_ROSA, M_ROSA_OSC, M_ROSA_CLARO, M_DORADO, M_DORADO_OSC, M_CREMA}
M = {'X': M_OSCURO, 'R': M_ROSA, 'r': M_ROSA_OSC, 'c': M_ROSA_CLARO, 'G': M_DORADO, 'g': M_DORADO_OSC, 'C': M_CREMA}

# espalda: 18x17, esquina arriba-izquierda en (17, 31)
MOCHILA_ESPALDA = [
    "..XXXXXXXXXXXXXX..",
    ".XcRRRCC.CCRRRRrX.",
    "XcRRRRCCGCCRRRRRrX",
    "XcRRRRRC.CRRRRRRrX",
    "XcRRRRRRRRRRRRRRrX",
    "XcRRRRRRRRRRRRRRrX",
    "XrrrrrrrrrrrrrrrrX",
    "XcGGGGGGGGGGGGGgrX",
    "XcRRRRRRRRRRRRRgrX",
    "XcRRRRRGRRRGRRRRrX",
    "XcRRRRRRGRGRRRRRrX",
    "XcRRRRRRRGRRRRRRrX",
    "XcRRRRRRGRGRRRRRrX",
    "XcRRRRRGGRGGRRRRrX",
    "XcRRRRRRRRRRRRRRrX",
    ".XrrrrrrrrrrrrrrX.",
    "..XXXXXXXXXXXXXX..",
]
# frente: el cuerpo de la mochila detrás (24x14 en (13, 31)): solo se ve donde el personaje no tapa
MOCHILA_FRENTE_ATRAS = ["..XXXXXXXXXXXXXXXXXXXX.."] + ["XrRRRRRRRRRRRRRRRRRRRRrX"] * 12 + [".XXXXXXXXXXXXXXXXXXXXXX."]
# correas sobre los hombros (encima): columnas x de cada correa y de dónde a dónde bajan
CORREAS_FRENTE = [(20, 32, 42), (30, 31, 41)]
# lado: 9x16 en (10, 31), detrás de la espalda; correa sobre el hombro encima
MOCHILA_LADO_ATRAS = [
    ".XXXXXXX.",
    "XcRRRRRrX",
    "XcRRRRRrX",
    "XcRRRRRrX",
    "XrrrrrrrX",
    "XGGGGGGgX",
    "XcRRRRRrX",
    "XcRRGRRrX",
    "XcRGRGRrX",
    "XcRRGRRrX",
    "XcRRRRRrX",
    "XcRRRRRrX",
    "XcRRRRRrX",
    "XcRRRRRrX",
    "XrrrrrrrX",
    ".XXXXXXX.",
]
CORREA_LADO = (19, 31, 37)


def _pegar(px, filas, x0, y0, atras):
    for j, fila in enumerate(filas):
        for i, ch in enumerate(fila):
            if ch == '.':
                continue
            x, y = x0 + i, y0 + j
            if 0 <= x < W and 0 <= y < H and (not atras or px[x, y][3] < 255):
                px[x, y] = M[ch]


def _correa(px, x, y0, y1):
    for y in range(y0, y1 + 1):
        px[x, y] = M_ROSA
        px[x + 1, y] = M_ROSA_OSC
    px[x, y1 + 1] = M_OSCURO
    px[x + 1, y1 + 1] = M_OSCURO
    px[x, y1 - 3] = M_DORADO          # hebilla
    px[x + 1, y1 - 3] = M_DORADO_OSC


def poner_mochila(im, vista, bob):
    """vista: 0 frente, 1 espalda, 2 lado derecho; bob: 1 en los cuadros de paso (el torso bajó 1 px)."""
    px = im.load()
    if vista == 1:
        _pegar(px, MOCHILA_ESPALDA, 17, 31 + bob, atras=False)
    elif vista == 0:
        _pegar(px, MOCHILA_FRENTE_ATRAS, 13, 31 + bob, atras=True)
        for x, y0, y1 in CORREAS_FRENTE:
            _correa(px, x, y0 + bob, y1 + bob)
    else:
        _pegar(px, MOCHILA_LADO_ATRAS, 10, 31 + bob, atras=True)
        x, y0, y1 = CORREA_LADO
        _correa(px, x, y0 + bob, y1 + bob)
    return im


# bob de cada columna de la hoja (igual que front_like y side_frame): 1 en los pasos (columnas 1 y 3)
BOB_COLUMNA = [0, 1, 0, 1, 0]

# =============================================================================
# HOJA
# =============================================================================
def build_frames():
    rows = []
    # frente: reposo = referencia exacta
    front = [None] * 5
    idle = blank()
    for y in range(H):
        for x in range(W):
            ch = RG[y][x]
            if ch != '.' and not is_shadow(ch):
                idle[y][x] = ch
    front[0] = idle
    front[1] = front_like(FRONT_UPPER, REF_LEGS, 'R')
    front[2] = front_like(FRONT_UPPER, REF_LEGS, None)
    front[3] = front_like(FRONT_UPPER, REF_LEGS, 'L')
    front[4] = front[2]
    rows.append(front)

    back = [front_like(BACK_UPPER, BACK_LEGS, None),
            front_like(BACK_UPPER, BACK_LEGS, 'R'),
            front_like(BACK_UPPER, BACK_LEGS, None),
            front_like(BACK_UPPER, BACK_LEGS, 'L'),
            front_like(BACK_UPPER, BACK_LEGS, None)]
    rows.append(back)

    side = [side_frame(near=(0, 0, 0), far=(0, 0, 0), bob=0, arm_shift=0),
            # paso: pierna cercana adelante, brazo cercano atrás
            side_frame(near=(2, 4, 0), far=(-2, -4, 0), bob=1, arm_shift=-2),
            # pasada: cercana apoyada, lejana levantada atrás
            side_frame(near=(0, 0, 0), far=(1, -3, 2), bob=0, arm_shift=0),
            # paso: pierna lejana adelante, brazo cercano adelante
            side_frame(near=(-2, -4, 0), far=(2, 4, 0), bob=1, arm_shift=2),
            # pasada: lejana apoyada, cercana levantada atrás
            side_frame(near=(1, -3, 2), far=(0, 0, 0), bob=0, arm_shift=0)]
    rows.append(side)
    return rows

def main():
    os.makedirs(OUT, exist_ok=True)
    frames = build_frames()
    sheet = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))
    sin_mochila = Image.new('RGBA', (W * 5, H * 3), (0, 0, 0, 0))   # para generar_motos.py
    meta_frames = []
    names = ['frente', 'espalda', 'lado_derecho']
    for r, row in enumerate(frames):
        for col, cv in enumerate(row):
            im = to_image(cv)
            bad = {im.getpixel((x, y)) for x in range(W) for y in range(H)
                   if im.getpixel((x, y))[3]} - REF_COLORS
            assert not bad, (r, col, bad)
            sin_mochila.paste(im, (col * W, r * H))
            im = poner_mochila(im.copy(), r, BOB_COLUMNA[col])
            bad = {im.getpixel((x, y)) for x in range(W) for y in range(H)
                   if im.getpixel((x, y))[3]} - REF_COLORS - MOCHILA_COLORES
            assert not bad, ('mochila', r, col, bad)
            sheet.paste(im, (col * W, r * H))
            meta_frames.append({
                'name': f'repartidor_{names[r]}_{"idle" if col == 0 else "walk_%d" % (col - 1)}',
                'row': r, 'col': col,
                'x': col * W, 'y': r * H, 'w': W, 'h': H,
                'unity_rect_y': (2 - r) * H,
                'pivot': {'x': 0.5, 'y': 0.0},
            })
    sheet.save(os.path.join(OUT, 'repartidor_sheet.png'))
    sin_mochila.save(os.path.join(OUT, 'repartidor_sheet_sin_mochila.png'))
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(os.path.join(OUT, 'preview_x4.png'))
    meta = {
        'image': 'repartidor_sheet.png',
        'size': {'w': W * 5, 'h': H * 3},
        'frame_size': {'w': W, 'h': H},
        'grid': {'columns': 5, 'rows': 3},
        'pixels_per_unit_sugerido': 16,
        'filter': 'Point (no filter)',
        'compression': 'None',
        'pivot_note': 'Pivote abajo-centro (0.5, 0). y = origen arriba-izquierda; unity_rect_y = origen abajo-izquierda (Sprite Editor).',
        'animations': {
            'idle_frente': {'frames': [0], 'fps': 8, 'loop': True},
            'walk_frente': {'frames': [1, 2, 3, 4], 'fps': 8, 'loop': True},
            'idle_espalda': {'frames': [5], 'fps': 8, 'loop': True},
            'walk_espalda': {'frames': [6, 7, 8, 9], 'fps': 8, 'loop': True},
            'idle_lado_derecho': {'frames': [10], 'fps': 8, 'loop': True},
            'walk_lado_derecho': {'frames': [11, 12, 13, 14], 'fps': 8, 'loop': True},
            'idle_lado_izquierdo': {'frames': [10], 'fps': 8, 'loop': True, 'flipX': True},
            'walk_lado_izquierdo': {'frames': [11, 12, 13, 14], 'fps': 8, 'loop': True, 'flipX': True},
        },
        'fps_sugeridos': 8,
        'frames': meta_frames,
    }
    with open(os.path.join(OUT, 'repartidor_sheet.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    print('ok', OUT)

if __name__ == '__main__':
    main()
