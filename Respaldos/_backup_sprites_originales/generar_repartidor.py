"""Delivery Brenn's - repartidor chibi. Genera frente, espaldas, lado y walk (4 frames)."""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw

W, H = 56, 80
X, Y = np.meshgrid(np.arange(W) + 0.5, np.arange(H) + 0.5)
GROUND = 76  # first row below the soles

def hx(s): return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))

# ---- palette: [highlight, base, shadow, deep shadow, outline] ----
PAL = {
    "skin":   ["fff1e4", "ffdcc0", "f4b9a0", "dc9888", "8c4c46"],
    "hair":   ["a8714a", "7a4a30", "5a3324", "40221a", "24110c"],
    "cap":    ["ffa3c6", "ff6fa5", "e04888", "b02a6c", "5a0e3a"],
    "jacket": ["b08bff", "8c52ff", "6a36d8", "4c22a8", "23104f"],
    "shirt":  ["ffffff", "f4f1f7", "d6d0e6", "aaa2c4", "4a3e66"],
    "gold":   ["fff0b0", "ffd166", "e8a840", "b87a2a", "6a3a14"],
    "pants":  ["5a5678", "403c5c", "2e2a46", "1f1c33", "0e0b1c"],
    "shoe":   ["ffffff", "ece8f2", "c4bcd6", "948aaa", "352a4a"],
}
PAL = {k: [hx(c) for c in v] for k, v in PAL.items()}
SHINE = hx("e8c09a")
LASH, IRIS_D, IRIS_M, IRIS_L, IRIS_B = hx("2a1630"), hx("3c1f40"), hx("6a3458"), hx("9a5a78"), hx("c48aa0")
WHITE, SPARK2 = (255, 255, 255), hx("f6dcea")
EYE_W = hx("fbf6f8")
BLUSH, MOUTH, TONGUE = hx("f7a3ae"), hx("7a2e3a"), hx("e8707e")
BROW = hx("40221a")

# ---- shape helpers ----
def ellipse(cx, cy, rx, ry): return ((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2 <= 1
def seg(a, b, r):
    (ax, ay), (bx, by) = a, b
    dx, dy = bx - ax, by - ay
    t = np.clip(((X - ax) * dx + (Y - ay) * dy) / (dx * dx + dy * dy + 1e-9), 0, 1)
    return (X - ax - t * dx) ** 2 + (Y - ay - t * dy) ** 2 <= r * r
def chain(pts, r):
    m = np.zeros((H, W), bool)
    for a, b in zip(pts, pts[1:]): m |= seg(a, b, r)
    return m
def rrect(x0, y0, x1, y1, r):
    cx = np.clip(X, x0 + r, x1 - r); cy = np.clip(Y, y0 + r, y1 - r)
    return (X - cx) ** 2 + (Y - cy) ** 2 <= r * r
def poly(pts):
    img = Image.new("L", (W, H)); ImageDraw.Draw(img).polygon([(x - 0.5, y - 0.5) for x, y in pts], fill=1)
    return np.array(img) > 0
def tri(t):  # 0..1..0 triangle wave, period 1
    t = t % 1.0; return 1 - abs(2 * t - 1)

class Part:
    def __init__(self, name, mask, mat, shade="sphere", center=None, th=(0.45, -0.3, -0.75),
                 casters=(), dark=0, lines=True, light=(0.6, 0.7), sep=False):
        self.__dict__.update(locals()); del self.__dict__["self"]

def render(parts, details=(), hair_fx=None, shadow_cx=28.0):
    label = np.full((H, W), -1)
    for i, p in enumerate(parts): label[p.mask] = i
    tone = np.ones((H, W), int)
    for i, p in enumerate(parts):
        m = label == i
        if not m.any(): continue
        ys, xs = np.where(p.mask)
        cx, cy = p.center or ((xs.min() + xs.max() + 1) / 2, (ys.min() + ys.max() + 1) / 2)
        rx, ry = max(1, (xs.max() - xs.min() + 1) / 2), max(1, (ys.max() - ys.min() + 1) / 2)
        nx, ny = (X - cx) / rx, (Y - cy) / ry
        if p.shade == "cyl": v = -(1.0 * nx + 0.15 * ny)
        elif p.shade == "flat": v = np.where(nx > 0.55, -0.5, 0.0)
        else: v = -(p.light[0] * nx + p.light[1] * ny)
        t = np.where(v > p.th[0], 0, np.where(v > p.th[1], 1, np.where(v > p.th[2], 2, 3)))
        for names, dist in p.casters:
            idx = [j for j, q in enumerate(parts) if q.name in names]
            cast = np.zeros((H, W), bool)
            for k in range(1, dist + 1):
                up = np.full((H, W), -1); up[k:] = label[:-k]
                cast |= np.isin(up, idx)
            t = np.where(cast, np.maximum(t, 2), t)
        t = np.minimum(t + p.dark, 3)
        tone[m] = t[m]
    if hair_fx: hair_fx(label, tone, parts)
    opaque = label >= 0
    out = np.zeros((H, W, 4), np.uint8)
    shine = getattr(render, "_shine", None)
    for i, p in enumerate(parts):
        m = label == i
        for k in range(4):
            out[m & (tone == k)] = (*PAL[p.mat][k], 255)
    # internal lines: a part tucked behind a later part gets its deep tone on the seam
    for y in range(H):
        for x in range(W):
            i = label[y, x]
            if i < 0 or not parts[i].lines: continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < W and 0 <= yy < H and label[yy, xx] > i and (parts[label[yy, xx]].mat != parts[i].mat or parts[label[yy, xx]].sep):
                    out[y, x, :3] = PAL[parts[i].mat][3]; break
    # selective outline: lighter on the lit (top/left) side
    for y in range(H):
        for x in range(W):
            if not opaque[y, x]: continue
            air = [(dx, dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                   if not (0 <= x + dx < W and 0 <= y + dy < H) or not opaque[y + dy, x + dx]]
            if air:
                mat = parts[label[y, x]].mat
                lit = all(d in ((-1, 0), (0, -1)) for d in air)
                out[y, x, :3] = PAL[mat][3] if lit else PAL[mat][4]
    for (x, y, c) in details:
        if 0 <= x < W and 0 <= y < H:
            out[y, x] = (*c, 255) if len(c) == 3 else c
    # light AA on stair-step corners (transparent pixel inside an L of solid pixels)
    solid = out[:, :, 3] == 255
    add = []
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if solid[y, x] or y >= GROUND - 1: continue
            l, r, u, d = solid[y, x - 1], solid[y, x + 1], solid[y - 1, x], solid[y + 1, x]
            if (l + r + u + d) == 2 and not (l and r) and not (u and d):
                nb = [out[y + b, x + a, :3].astype(int) for a, b, s in ((-1, 0, l), (1, 0, r), (0, -1, u), (0, 1, d)) if s]
                add.append((x, y, tuple(((nb[0] + nb[1]) // 2).tolist())))
    for x, y, c in add: out[y, x] = (*c, 110)
    img = Image.fromarray(out, "RGBA")
    # soft oval ground shadow, behind the sprite
    sh = np.zeros((H, W, 4), np.uint8)
    d = ((X - shadow_cx) / 12.5) ** 2 + ((Y - (GROUND - 0.6)) / 2.7) ** 2
    a = np.where(d < 0.35, 120, np.where(d < 0.7, 78, np.where(d < 1, 38, 0)))
    sh[..., :3] = hx("241a38"); sh[..., 3] = a
    base = Image.fromarray(sh, "RGBA"); base.alpha_composite(img)
    return base

def hair_effects(crown, r0, strands=9, valleys=()):
    """Strand lines radiating from the crown + a two-level shine arc."""
    def fx(label, tone, parts):
        idx = [i for i, p in enumerate(parts) if p.mat == "hair"]
        hm = np.isin(label, idx)
        ang = np.arctan2(X - crown[0], Y - crown[1])
        r = np.hypot(X - crown[0], (Y - crown[1]) * 1.1)
        strand = ((ang * strands / math.pi) % 1.0 < 0.2) & (r > r0 - 3)
        ring = (r >= r0) & (r < r0 + 1.9) & (X < crown[0] + 6)
        core = (r >= r0 + 0.4) & (r < r0 + 1.3) & (X < crown[0] + 2)
        tone[hm & strand & ~ring] = np.maximum(tone[hm & strand & ~ring], 2)
        tone[hm & ring & ~strand] = 0
        render._core = hm & core & ~strand
        for (vx, vy, n) in valleys:  # dark separators between bang spikes
            for k in range(n):
                if 0 <= vy - k < H and hm[vy - k, vx]: tone[vy - k, vx] = 3
    return fx

def finish(img, core_mask):
    px = img.load()
    if core_mask is not None:
        for y, x in zip(*np.where(core_mask)):
            if px[x, y][3] == 255: px[x, y] = (*SHINE, 255)
    return img

def eye(cx0, y0, side, iris_w=4, white=True):
    """Big chibi eye. cx0 = leftmost column, side = 'L'/'R' (which side the sclera/outer flick is)."""
    d = []
    w = iris_w + (1 if white else 0)
    ix0 = cx0 + (1 if (white and side == "L") else 0)
    for c in range(w): d.append((cx0 + c, y0, LASH))
    d.append((cx0 - 1 if side == "L" else cx0 + w, y0, LASH))          # outer lash flick
    d.append((cx0 - 1 if side == "L" else cx0 + w, y0 - 1, LASH))
    rows = [IRIS_D, IRIS_D, IRIS_M, IRIS_M, IRIS_L, IRIS_B]
    for k, col in enumerate(rows):
        for c in range(iris_w): d.append((ix0 + c, y0 + 1 + k, col))
        if white:
            wx = cx0 if side == "L" else cx0 + iris_w
            if 1 <= k <= 4: d.append((wx, y0 + 1 + k, EYE_W))
    # catchlights: 2x2 top-left + tiny one bottom-right
    for a, b in ((0, 1), (1, 1), (0, 2), (1, 2)): d.append((ix0 + a, y0 + b, WHITE))
    d.append((ix0 + iris_w - 1, y0 + 5, SPARK2))
    return d

def gold_B(x0, y0):
    G, S = PAL["gold"][1], PAL["gold"][3]
    pat = ["GGG.", "G.SG", "GGG.", "G.SG", "GGGS"]
    return [(x0 + c, y0 + r, G if ch == "G" else S) for r, row in enumerate(pat) for c, ch in enumerate(row) if ch != "."]

# =====================================================================
def front():
    P = []
    P.append(Part("hairback", ellipse(28, 22.5, 13.6, 12) & (Y < 31), "hair", center=(27, 20)))
    P.append(Part("legL", rrect(19.6, 56, 27.4, 72, 1.5), "pants", "cyl", casters=[(("jacket",), 2)]))
    P.append(Part("legR", rrect(28.6, 56, 36.4, 72, 1.5), "pants", "cyl", casters=[(("jacket",), 2)]))
    P.append(Part("shoeL", ellipse(23.2, 73.6, 4.7, 2.6) & (Y < GROUND), "shoe", casters=[(("legL",), 1)]))
    P.append(Part("shoeR", ellipse(32.8, 73.6, 4.7, 2.6) & (Y < GROUND), "shoe", casters=[(("legR",), 1)]))
    P.append(Part("shirt", rrect(22, 36, 34, 58.5, 2), "shirt", "flat", casters=[(("neck", "face"), 2)]))
    jacket = rrect(17.4, 37, 38.6, 58.5, 4.5) & ~((np.abs(X - 28) < 3.6 + (Y - 37) * 0.04) & (Y > 36.5))
    P.append(Part("jacket", jacket, "jacket", "cyl", center=(28, 46), casters=[(("face",), 2)]))
    P.append(Part("armL", seg((19, 40.5), (16.4, 52.6), 3.3), "jacket", "cyl", center=(17.5, 46), sep=True))
    P.append(Part("armR", seg((37, 40.5), (39.6, 52.6), 3.3), "jacket", "cyl", center=(38.5, 46), sep=True))
    P.append(Part("handL", ellipse(16.2, 55.4, 2.6, 2.6), "skin", casters=[(("armL",), 1)]))
    P.append(Part("handR", ellipse(39.8, 55.4, 2.6, 2.6), "skin", casters=[(("armR",), 1)]))
    P.append(Part("neck", rrect(24.6, 32, 31.4, 38.5, 1), "skin", "flat", th=(9, 9, -9)))
    P.append(Part("earL", ellipse(16.6, 26.8, 1.8, 2.4), "skin", lines=False))
    P.append(Part("earR", ellipse(39.4, 26.8, 1.8, 2.4), "skin", lines=False))
    P.append(Part("face", ellipse(28, 25.3, 11.6, 10.4), "skin", "face", th=(0.62, -0.5, -9),
                  casters=[(("bangs", "brim", "side"), 1)], lines=False, light=(0.6, 0.5)))
    P.append(Part("cap", ellipse(28, 14.8, 13.2, 9.6) & (Y < 14.8), "cap"))
    # bangs: spikes pointing down, slightly swept
    b = 19.7 + 2.0 * tri((X - 15.3) / 3.6)
    bangs = (Y > 13.5) & (Y < b) & ellipse(28, 22.5, 13.2, 12)
    side = ((X < 19.2 - np.maximum(0, Y - 24) * 0.45) | (X > 36.8 + np.maximum(0, Y - 24) * 0.45)) & (Y < 30) & (Y > 13.5) \
        & ellipse(28, 23, 12.8, 12)
    tufts = ellipse(15.2, 16.8, 2.4, 3.2) | ellipse(40.8, 16.8, 2.4, 3.2)
    P.append(Part("side", (side | tufts) & ~bangs, "hair", center=(28, 20)))
    P.append(Part("bangs", bangs, "hair", center=(25, 16), casters=[(("brim",), 1)]))
    P.append(Part("brim", ellipse(28, 15.5, 14.6, 2.1), "cap", "cyl", center=(27, 15.5), th=(0.55, -0.35, -0.8)))
    D = []
    D += eye(20, 25, "L") + eye(31, 25, "R")
    D += [(x, y, BROW) for x, y in ((20, 22), (21, 22), (22, 22), (23, 23), (32, 23), (33, 22), (34, 22), (35, 22))]
    D += [(x, 32, BLUSH) for x in (18, 19, 20, 35, 36, 37)]
    D += [(27, 31, PAL["skin"][2])]
    D += [(x, 33, MOUTH) for x in (26, 27, 28, 29)] + [(27, 34, TONGUE), (28, 34, TONGUE)]
    D += gold_B(26, 44)
    D += [(27, 6, PAL["gold"][1]), (28, 6, PAL["gold"][2])]             # cap button
    D += [(x, 52, PAL["jacket"][3]) for x in (20, 21, 22)] + [(x, 52, PAL["jacket"][3]) for x in (34, 35, 36)]  # pockets
    D += [(22, 72, PAL["cap"][2]), (23, 72, PAL["cap"][1]), (32, 72, PAL["cap"][2]), (33, 72, PAL["cap"][1])]  # laces
    D += [(x, GROUND - 1, PAL["cap"][3]) for x in range(20, 27)] + [(x, GROUND - 1, PAL["cap"][3]) for x in range(30, 37)]  # soles
    fx = hair_effects((24, 4), 15.6, strands=10)
    img = render(P, D, fx)
    return finish(img, render._core)

def back():
    P = []
    P.append(Part("brim", ellipse(28, 15.5, 14.6, 2.1), "cap", "cyl", dark=1, lines=False))
    P.append(Part("legL", rrect(19.6, 56, 27.4, 72, 1.5), "pants", "cyl", casters=[(("jacket",), 2)]))
    P.append(Part("legR", rrect(28.6, 56, 36.4, 72, 1.5), "pants", "cyl", casters=[(("jacket",), 2)]))
    P.append(Part("shoeL", ellipse(23.2, 73.4, 4.5, 2.6) & (Y < GROUND), "shoe", casters=[(("legL",), 1)]))
    P.append(Part("shoeR", ellipse(32.8, 73.4, 4.5, 2.6) & (Y < GROUND), "shoe", casters=[(("legR",), 1)]))
    P.append(Part("neck", rrect(24.6, 30, 31.4, 38.5, 1), "skin", "flat", th=(9, 9, -9), dark=1))
    P.append(Part("jacket", rrect(17.4, 37, 38.6, 58.5, 4.5), "jacket", "cyl", center=(28, 46), casters=[(("hair",), 2)]))
    P.append(Part("armL", seg((19, 40.5), (16.4, 52.6), 3.3), "jacket", "cyl", center=(17.5, 46), sep=True))
    P.append(Part("armR", seg((37, 40.5), (39.6, 52.6), 3.3), "jacket", "cyl", center=(38.5, 46), sep=True))
    P.append(Part("handL", ellipse(16.2, 55.4, 2.6, 2.6), "skin", casters=[(("armL",), 1)]))
    P.append(Part("handR", ellipse(39.8, 55.4, 2.6, 2.6), "skin", casters=[(("armR",), 1)]))
    P.append(Part("earL", ellipse(15.8, 26.8, 1.8, 2.4), "skin", lines=False))
    P.append(Part("earR", ellipse(40.2, 26.8, 1.8, 2.4), "skin", lines=False))
    nape = 32.2 + 2.4 * tri((X - 17.5) / 3.5)
    hair = (ellipse(28, 23.6, 13.4, 12) & (Y < nape)) | ellipse(15.2, 16.8, 2.4, 3.2) | ellipse(40.8, 16.8, 2.4, 3.2)
    P.append(Part("hair", hair, "hair", center=(26, 20), casters=[(("cap",), 1)]))
    opening = ellipse(28, 14.8, 3.2, 3.4) & (Y > 11.2)
    P.append(Part("cap", ellipse(28, 14.8, 13.2, 9.6) & (Y < 14.8) & ~opening, "cap"))
    D = [(x, 12, PAL["cap"][3]) for x in range(26, 30)]                      # snapback strap
    D += [(27, 6, PAL["gold"][1]), (28, 6, PAL["gold"][2])]
    D += [(x, 38, PAL["jacket"][3]) for x in range(23, 33)]                  # collar seam
    D += [(28, y, PAL["jacket"][2]) for y in range(40, 57)]                  # back seam
    D += [(x, 60, PAL["pants"][3]) for x in (21, 22, 23, 24)] + [(x, 60, PAL["pants"][3]) for x in (31, 32, 33, 34)]
    D += [(x, 71, PAL["cap"][1]) for x in (22, 23, 24)] + [(x, 71, PAL["cap"][1]) for x in (31, 32, 33)]  # heel tabs
    D += [(x, GROUND - 1, PAL["cap"][3]) for x in range(20, 27)] + [(x, GROUND - 1, PAL["cap"][3]) for x in range(30, 37)]
    fx = hair_effects((28, 6), 16.0, strands=11)
    img = render(P, D, fx)
    return finish(img, render._core)

# ---- side view (facing right) ----
def side(pose="stand"):
    # pose: (body bob, near leg (knee, ankle), far leg, near arm swing, far arm swing)
    POSES = {
        "stand": (0, ((29.5, 66), (29.5, 72.6)), ((26.8, 66), (26.8, 72.6)), 0.05, -0.05),
        "c1":    (0, ((31.5, 66), (33.6, 72.6)), ((24.6, 66), (22.4, 72.2)), -0.45, 0.45),
        "p1":    (-1, ((28.4, 65), (28.2, 72.6)), ((32.2, 65.2), (28.0, 70.2)), -0.08, 0.08),
        "c2":    (0, ((24.6, 66), (22.4, 72.2)), ((31.5, 66), (33.6, 72.6)), 0.45, -0.45),
        "p2":    (-1, ((32.2, 65.2), (28.0, 70.2)), ((28.4, 65), (28.2, 72.6)), 0.08, -0.08),
    }
    dy, near, far, a_near, a_far = POSES[pose]
    hip = (28.0, 59.0 + dy)
    def leg(name, kn, an, dark, z):
        k = (kn[0], kn[1] + (dy if an[1] < 72 else 0)); a = an
        pants = chain([hip, k, (a[0], a[1] - 1.0)], 3.2)
        lift = a[1] < 72
        sc = (a[0] + 1.9, min(a[1] + 1.1, GROUND - 2.4))
        shoe = ellipse(sc[0], sc[1], 4.4, 2.4) & (Y < GROUND)
        return [Part(name, pants, "pants", "cyl", center=(hip[0], 66), dark=dark, casters=[(("jacket",), 2)]),
                Part(name + "_shoe", shoe, "shoe", dark=dark, casters=[((name,), 1)])]
    def arm(name, ang, dark):
        sh = (28.6, 40.2 + dy)
        el = (sh[0] + 11.0 * math.sin(ang), sh[1] + 11.0 * math.cos(ang))
        hd = (sh[0] + 13.8 * math.sin(ang), sh[1] + 13.8 * math.cos(ang))
        return [Part(name, seg(sh, el, 3.3), "jacket", "cyl", center=(sh[0], sh[1] + 6), dark=dark, sep=True),
                Part(name + "_hand", ellipse(hd[0], hd[1], 2.5, 2.5), "skin", dark=dark, casters=[((name,), 1)])]
    P = []
    P += arm("armF", a_far, 1)
    P += leg("legF", far[0], far[1], 1, 0)
    P += leg("legN", near[0], near[1], 0, 1)
    P.append(Part("neck", rrect(25.2, 32 + dy, 30.6, 38.5 + dy, 1), "skin", "flat", th=(9, 9, -9), dark=1))
    P.append(Part("shirt", rrect(29, 37 + dy, 35, 58.5 + dy, 2) & (X > 32.4), "shirt", "flat", casters=[(("face",), 2)]))
    P.append(Part("jacket", rrect(21.2, 37 + dy, 34.2, 58.5 + dy, 4.2) & ~((X > 32.4) & (Y > 37 + dy)), "jacket", "cyl",
                  center=(28, 46 + dy), casters=[(("face", "hair"), 2)]))
    P += arm("armN", a_near, 0)
    hc = (28.2, 25.3 + dy)
    P.append(Part("face", ellipse(hc[0], hc[1], 11.4, 10.4), "skin", "face", center=(30, 25 + dy), th=(0.62, -0.5, -9),
                  casters=[(("bangs", "brim"), 1)], lines=False, light=(0.6, 0.5)))
    back = ellipse(27, 23 + dy, 12.9, 12) & ((X < 26.6 + np.maximum(0, (21 + dy) - Y) * 1.2) | (Y < 18.5 + dy))
    nape = (X < 27) & (Y < 31.5 + dy + 1.8 * tri((X - 14) / 3.4))
    sideburn = (X > 25.6) & (X < 29.2 - np.maximum(0, Y - 25 - dy) * 0.5) & (Y < 29.5 + dy) & (Y > 18 + dy)
    hair = (back & (Y < 33 + dy) & (nape | (X >= 27))) | (sideburn & ellipse(hc[0], hc[1], 11.4, 10.4)) \
        | ellipse(14.4, 17 + dy, 2.4, 3.2)
    b = 19.9 + dy + 2.0 * tri((X - 27.5) / 3.6)
    bangs = (X > 27.5) & (Y > 13.5 + dy) & (Y < b) & ellipse(hc[0], hc[1] - 0.6, 12.2, 11)
    P.append(Part("hair", hair & ~bangs, "hair", center=(24, 19 + dy)))
    P.append(Part("ear", ellipse(24.6, 26.8 + dy, 1.8, 2.4), "skin", lines=False))
    P.append(Part("bangs", bangs, "hair", center=(30, 16 + dy), casters=[(("brim",), 1)]))
    P.append(Part("cap", ellipse(27.4, 14.8 + dy, 13.2, 9.6) & (Y < 14.8 + dy), "cap"))
    brim = poly([(33, 13.2 + dy), (43, 13.5 + dy), (47.6, 14.8 + dy), (46.6, 16.3 + dy), (33, 17.0 + dy)])
    P.append(Part("brim", brim, "cap", "cyl", center=(38, 16 + dy), th=(0.55, -0.35, -0.8)))
    D = []
    e0 = round(18 + 0) + 0
    yb = int(25 + dy)
    # profile eye: 3-wide iris, lash flick toward the back of the head
    D += [(x, yb, LASH) for x in range(33, 37)] + [(32, yb, LASH), (32, yb - 1, LASH)]
    for k, col in enumerate([IRIS_D, IRIS_D, IRIS_M, IRIS_M, IRIS_L, IRIS_B]):
        for x in (33, 34, 35): D.append((x, yb + 1 + k, col))
    D += [(33, yb + 1, WHITE), (34, yb + 1, WHITE), (33, yb + 2, WHITE), (34, yb + 2, WHITE), (35, yb + 5, SPARK2)]
    D += [(x, y + int(dy), BROW) for x, y in ((33, 22), (34, 22), (35, 23), (36, 23))]
    D += [(x, 32 + int(dy), BLUSH) for x in (32, 33, 34)]
    D += [(33, 33 + int(dy), MOUTH), (34, 33 + int(dy), MOUTH), (33, 34 + int(dy), TONGUE)]
    D += [(39, 29 + int(dy), PAL["skin"][4])]
    D += [(27, 6 + int(dy), PAL["gold"][1])]
    D += [(x, 52 + int(dy), PAL["jacket"][3]) for x in (24, 25, 26)]
    D += [(33, 45 + int(dy), PAL["gold"][1]), (33, 46 + int(dy), PAL["gold"][2])]  # edge of chest logo
    D += [(x, 12 + int(dy), PAL["cap"][3]) for x in (15, 16)]                      # snapback hint
    for qi, q in enumerate(P):
        if q.mat == "shoe":
            later = np.zeros((H, W), bool)
            for r in P[qi + 1:]: later |= r.mask
            col = PAL["cap"][3] if not q.dark else PAL["cap"][4]
            for x in range(W):
                ys = np.where(q.mask[:, x])[0]
                if len(ys) and not later[ys.max(), x]: D.insert(0, (x, int(ys.max()), col))
    fx = hair_effects((24, 5 + dy), 16.5, strands=10)
    img = render(P, D, fx, shadow_cx=29)
    return finish(img, render._core)

if __name__ == "__main__":
    out = sys.argv[1]
    front().save(os.path.join(out, "repartidor_frente.png"))
    back().save(os.path.join(out, "repartidor_espaldas.png"))
    side("stand").save(os.path.join(out, "repartidor_lado.png"))
    sheet = Image.new("RGBA", (W * 4, H), (0, 0, 0, 0))
    for i, p in enumerate(["c1", "p1", "c2", "p2"]):
        sheet.alpha_composite(side(p), (i * W, 0))
    sheet.save(os.path.join(out, "repartidor_lado_walk.png"))
    print("ok")
