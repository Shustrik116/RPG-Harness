"""
Портреты сеттинга «Киберпанк», нарисованные кодом: враги (люди, киборги, машины, программы),
лица спутников/NPC и аватары героя. Каждый портрет — 64x64 пикселя, увеличенный x5 (клетка 320 px),
листы 4x4 (герой — 3x2) кладутся в wwwroot/sprites и регистрируются в build_portraits.py.

Манера: ночной мегаполис с неоновыми окнами на фоне, персонаж с контровым светом двух цветов
(холодный слева, тёплый справа), импланты и визоры светятся.

Порядок клеток и id — только дописывать (id хранятся в кампаниях).

Запуск: python tools/sprites/build_cyber_portraits.py
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import (BLACK, BONE, CHROME, CRIMSON, CYAN, DARK, GOLD, GREEN, GRIP, LEATHER, MAGENTA, METAL,  # noqa: E402
                      METAL_L, NAVY, OLIVE, ORANGE, RED, STEEL, VIOLET, WHITE, YELLOW, mix, shade)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
S = 64
K = 5
OUT = (6, 6, 12)

THEMES = {
    "pink": ((40, 10, 60), (10, 6, 22), [MAGENTA, CYAN, (255, 120, 200)], CYAN, MAGENTA),
    "teal": ((6, 40, 56), (4, 10, 20), [CYAN, (60, 255, 200), ORANGE], (90, 255, 230), ORANGE),
    "red": ((60, 8, 20), (14, 4, 10), [RED, ORANGE, (255, 80, 140)], (255, 120, 60), RED),
    "acid": ((20, 46, 20), (6, 12, 8), [GREEN, YELLOW, CYAN], GREEN, YELLOW),
    "blue": ((14, 24, 70), (4, 6, 20), [(80, 140, 255), CYAN, VIOLET], (120, 180, 255), VIOLET),
    "gold": ((56, 34, 6), (16, 8, 4), [GOLD, ORANGE, RED], GOLD, (255, 90, 40)),
    "violet": ((36, 12, 70), (8, 4, 20), [VIOLET, MAGENTA, CYAN], VIOLET, CYAN),
}

SKINS = [(232, 190, 160), (214, 160, 120), (176, 118, 84), (124, 80, 56), (88, 58, 42), (226, 200, 180)]


# ======================= Фон =======================

def background(theme, seed):
    top, bot, neons, _, _ = THEMES[theme]
    rng = random.Random(seed)
    img = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(img)
    for y in range(S):
        d.line([(0, y), (S, y)], fill=mix(top, bot, y / S) + (255,))
    # дальние дома
    for layer, (dark, hmin, hmax, wmin, wmax, lit) in enumerate(((.55, 16, 38, 4, 9, .18), (.8, 10, 26, 6, 13, .1))):
        x = -rng.randint(0, 4)
        while x < S:
            w = rng.randint(wmin, wmax)
            h = rng.randint(hmin, hmax) + layer * 4
            col = mix(bot, (0, 0, 0), dark)
            d.rectangle([x, S - h, x + w - 1, S], fill=col + (255,))
            if rng.random() < .3:
                d.line([(x + w // 2, S - h - rng.randint(2, 6)), (x + w // 2, S - h)], fill=col + (255,))
            for wy in range(S - h + 2, S, 3):
                for wx in range(x + 1, x + w - 1, 2):
                    if rng.random() < lit:
                        n = rng.choice(neons)
                        d.point((wx, wy), fill=mix(n, bot, .45 + layer * .2) + (255,))
            x += w + rng.randint(0, 2)
    # неоновая вывеска
    nx = rng.choice([rng.randint(2, 12), rng.randint(46, 56)])
    ny = rng.randint(6, 22)
    n = rng.choice(neons)
    glow = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(glow)
    gd.rectangle([nx - 3, ny - 3, nx + 9, ny + 4], fill=n + (90,))
    glow = glow.filter(ImageFilter.GaussianBlur(3))
    img.alpha_composite(glow)
    d.rectangle([nx, ny, nx + 6, ny + 1], fill=n + (255,))
    # дождь
    for _ in range(14):
        rx, ry = rng.randint(0, S), rng.randint(0, S)
        d.line([(rx, ry), (rx - 1, ry + 3)], fill=(150, 170, 220, 42))
    return img


# ======================= Слой персонажа =======================

class Fig:
    def __init__(self):
        self.img = Image.new("RGBA", (S, S))
        self.d = ImageDraw.Draw(self.img)
        self.glows = []

    def poly(self, pts, col):
        self.d.polygon(pts, fill=tuple(col[:3]) + (255,))

    def rect(self, x0, y0, x1, y1, col):
        self.d.rectangle([x0, y0, x1, y1], fill=tuple(col[:3]) + (255,))

    def ell(self, x0, y0, x1, y1, col, outline=None, width=1):
        self.d.ellipse([x0, y0, x1, y1], fill=tuple(col[:3]) + (255,) if col else None,
                       outline=tuple(outline[:3]) + (255,) if outline else None, width=width)

    def line(self, pts, col, w=1):
        self.d.line(pts, fill=tuple(col[:3]) + (255,), width=w)

    def px(self, x, y, col):
        if 0 <= x < S and 0 <= y < S:
            self.img.putpixel((x, y), tuple(col[:3]) + (255,))

    def glow(self, x, y, col, r=4, a=120):
        self.glows.append((x, y, col, r, a))


def shade_region(fig, pred, k):
    px = fig.img.load()
    for y in range(S):
        for x in range(S):
            p = px[x, y]
            if p[3] and pred(x, y, p):
                px[x, y] = shade(p, k) + (255,)


def compose(bg, fig, rim_l, rim_r):
    img = fig.img
    px = img.load()
    src = img.copy().load()

    def solid(x, y):
        return 0 <= x < S and 0 <= y < S and src[x, y][3] > 0

    # контровой свет: холодный слева, тёплый справа
    for y in range(S):
        for x in range(S):
            if not src[x, y][3]:
                continue
            p = src[x, y]
            if not solid(x - 1, y):
                px[x, y] = mix(p, rim_l, .6) + (255,)
            elif not solid(x - 2, y):
                px[x, y] = mix(p, rim_l, .25) + (255,)
            if not solid(x + 1, y):
                px[x, y] = mix(p, rim_r, .6) + (255,)
            elif not solid(x + 2, y):
                px[x, y] = mix(p, rim_r, .25) + (255,)
            if not solid(x, y - 1):
                px[x, y] = mix(px[x, y], (255, 255, 255), .12)[:3] + (255,)
    # тёмный контур
    src2 = img.copy().load()
    for y in range(S):
        for x in range(S):
            if src2[x, y][3]:
                continue
            if any(0 <= x + dx < S and 0 <= y + dy < S and src2[x + dx, y + dy][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = OUT + (255,)
    out = bg.copy()
    # свечения под и над фигурой
    glow = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(glow)
    for x, y, col, r, a in fig.glows:
        gd.ellipse([x - r, y - r, x + r, y + r], fill=tuple(col[:3]) + (a,))
    glow = glow.filter(ImageFilter.GaussianBlur(2))
    out.alpha_composite(img)
    out.alpha_composite(glow)
    return out


# ======================= Человек =======================

def person(theme="pink", seed=0, gender="m", skin=0, hair="buzz", hair_col=(30, 26, 30), eyes=None, mods=(),
           clothes="jacket", cloth=LEATHER, accent=None, beard=None, lips=None, extra=(), backdrop=None):
    top, bot, neons, rim_l, rim_r = THEMES[theme]
    accent = accent or neons[0]
    sk = SKINS[skin] if isinstance(skin, int) else skin
    f = Fig()
    hood = hair == "hood"

    # --- волосы сзади / капюшон ---
    if hair in ("long", "pony", "dreads"):
        f.poly([(19, 16), (26, 8), (38, 8), (45, 16), (47, 50), (40, 52), (24, 52), (17, 50)], shade(hair_col, -.15))
    if hood:
        f.poly([(14, 52), (16, 18), (24, 5), (40, 5), (48, 18), (50, 52)], cloth)

    # --- торс ---
    torso(f, clothes, cloth, accent, sk, gender)

    # --- шея и голова ---
    f.rect(28, 36, 36, 47, shade(sk, -.25))
    if "helmet" in mods or "robot_head" in mods:
        pass
    else:
        if gender == "f":
            face = [(24, 17), (40, 17), (42, 25), (41, 33), (37, 39), (32, 41), (27, 39), (23, 33), (22, 25)]
        else:
            face = [(23, 17), (41, 17), (43, 25), (42, 34), (38, 40), (32, 42), (26, 40), (22, 34), (21, 25)]
        f.ell(19, 24, 24, 32, shade(sk, -.1)); f.ell(40, 24, 45, 32, shade(sk, -.1))
        f.ell(22, 9, 42, 31, sk)
        f.poly(face, sk)
        # свет слева, тень справа и под скулами
        shade_region(f, lambda x, y, p: p[:3] == sk and x >= 36, -.18)
        shade_region(f, lambda x, y, p: p[:3] == shade(sk, -.18) and x >= 40, -.15)
        shade_region(f, lambda x, y, p: p[:3] == sk and y >= 37, -.12)
        shade_region(f, lambda x, y, p: p[:3] == sk and x <= 27 and y <= 30, .06)
        # глаза
        ey = 27
        eye_col = eyes
        for ex in (26, 36):
            if eye_col:
                f.rect(ex, ey, ex + 2, ey + 1, eye_col)
                f.glow(ex + 1, ey, eye_col, 2, 140)
            else:
                f.rect(ex, ey, ex + 2, ey + 1, (226, 222, 214))
                f.px(ex + 1, ey, (40, 30, 30)); f.px(ex + 1, ey + 1, (30, 22, 22))
            if gender == "f":
                f.px(ex - 1 if ex == 26 else ex + 3, ey - 1, (20, 16, 20))
        brow = shade(hair_col, -.1) if hair not in ("bald",) else shade(sk, -.35)
        f.line([(25, 24), (29, 24 if gender == "f" else 25)], brow); f.line([(35, 24 if gender == "f" else 25), (39, 24)], brow)
        # нос и рот
        f.px(32, 31, shade(sk, -.2)); f.px(32, 32, shade(sk, -.2)); f.px(31, 33, shade(sk, -.4)); f.px(33, 33, shade(sk, -.4))
        if lips:
            f.line([(29, 36), (35, 36)], lips); f.line([(30, 37), (34, 37)], shade(lips, -.2))
        else:
            f.line([(29, 36), (35, 36)], shade(sk, -.45))
        if beard == "stubble":
            for y in range(33, 41):
                for x in range(24, 41):
                    if f.img.getpixel((x, y))[:3] in (sk, shade(sk, -.18), shade(sk, -.12)) and (x + y) % 2 == 0:
                        f.px(x, y, mix(sk, hair_col, .45))
        elif beard == "full":
            f.poly([(23, 32), (27, 36), (37, 36), (41, 32), (40, 38), (35, 43), (29, 43), (24, 38)], hair_col)
            f.line([(29, 36), (35, 36)], shade(hair_col, -.5))
        elif beard == "goatee":
            f.poly([(29, 37), (35, 37), (34, 42), (30, 42)], hair_col)

    # --- волосы спереди ---
    hairs(f, hair, hair_col, accent, cloth)

    # --- импланты и снаряжение ---
    for m in mods:
        mod(f, m, accent, sk, hair_col)
    for e in extra:
        e(f)
    return compose((backdrop or background)(theme, seed), f, rim_l, rim_r)


def torso(f, clothes, cloth, accent, sk, gender):
    sh = [(4, 64), (8, 53), (19, 46), (45, 46), (56, 53), (60, 64)]
    if clothes == "tank":
        f.poly(sh, sk)
        shade_region(f, lambda x, y, p: p[:3] == sk and x > 40, -.2)
        f.poly([(20, 64), (21, 50), (27, 47), (37, 47), (43, 50), (44, 64)], cloth)
        f.poly([(44, 64), (46, 49), (55, 52), (60, 64)], METAL_L)
        for y in (54, 58, 62):
            f.line([(47, y), (57, y)], DARK)
        f.px(51, 56, accent)
        return
    if clothes == "tshirt":
        f.poly(sh, sk)
        shade_region(f, lambda x, y, p: p[:3] == sk and x > 40, -.2)
        f.poly([(12, 64), (14, 50), (22, 46), (42, 46), (50, 50), (52, 64)], cloth)
        f.poly([(27, 46), (32, 50), (37, 46)], shade(cloth, -.3))
        return
    f.poly(sh, cloth)
    if clothes == "tracksuit":
        f.line([(10, 52), (19, 47)], (235, 235, 235), 2); f.line([(54, 52), (45, 47)], (235, 235, 235), 2)
        f.line([(8, 57), (18, 51)], (235, 235, 235)); f.line([(56, 57), (46, 51)], (235, 235, 235))
        f.poly([(28, 46), (32, 52), (36, 46)], (235, 235, 235))
        f.line([(32, 52), (32, 64)], shade(cloth, -.35))
        return
    if clothes == "police":
        f.poly([(27, 46), (32, 54), (37, 46)], (220, 224, 230))
        f.poly([(31, 48), (33, 48), (34, 60), (32, 63), (30, 60)], (24, 26, 36))
        f.poly([(38, 52), (43, 52), (43, 57), (40, 59), (38, 57)], (210, 170, 60))
        f.rect(12, 50, 18, 52, (210, 170, 60))
        return
    if clothes == "camo":
        import random as _r
        rr = _r.Random(5)
        for _ in range(26):
            x, y = rr.randint(6, 56), rr.randint(47, 62)
            f.ell(x, y, x + rr.randint(3, 6), y + rr.randint(2, 4), rr.choice([shade(cloth, -.3), shade(cloth, .2), (70, 60, 40)]))
        f.poly([(27, 46), (32, 51), (37, 46)], shade(cloth, -.4))
        return
    if clothes == "scrubs":
        f.poly([(27, 46), (32, 54), (37, 46)], shade(cloth, -.25))
        f.rect(38, 53, 44, 58, shade(cloth, -.15))
        f.line([(41, 53), (41, 50)], (40, 40, 44))
        return
    if clothes == "plate":
        f.poly([(20, 48), (44, 48), (46, 64), (18, 64)], (86, 92, 58))
        for x in (22, 29, 36):
            f.rect(x, 55, x + 5, 61, (66, 72, 44))
        f.rect(26, 49, 38, 53, (66, 72, 44))
        return
    dark = shade(cloth, -.3)
    if clothes == "jacket":
        f.poly([(24, 46), (32, 58), (40, 46)], (24, 24, 30))
        f.poly([(19, 46), (24, 46), (31, 60), (27, 64), (22, 52)], shade(cloth, .18))
        f.poly([(45, 46), (40, 46), (33, 60), (37, 64), (42, 52)], shade(cloth, .1))
        f.line([(23, 47), (30, 61)], accent)
        f.glow(26, 54, accent, 2, 70)
    elif clothes == "armor":
        f.poly([(22, 48), (42, 48), (44, 64), (20, 64)], METAL)
        f.ell(4, 46, 22, 60, METAL_L); f.ell(42, 46, 60, 60, METAL_L)
        f.line([(24, 54), (40, 54)], accent); f.glow(32, 54, accent, 3, 80)
        f.line([(24, 58), (40, 58)], dark)
    elif clothes == "suit":
        f.poly([(25, 46), (32, 58), (39, 46)], (230, 232, 236))
        f.poly([(31, 49), (33, 49), (34, 60), (32, 63), (30, 60)], accent)
        f.poly([(19, 46), (25, 46), (31, 62), (24, 58)], shade(cloth, .2))
        f.poly([(45, 46), (39, 46), (33, 62), (40, 58)], shade(cloth, .2))
    elif clothes == "hoodie":
        f.poly([(18, 46), (46, 46), (42, 52), (22, 52)], shade(cloth, .15))
        f.line([(28, 52), (28, 60)], accent); f.line([(36, 52), (36, 60)], accent)
    elif clothes == "vest":
        f.poly([(20, 48), (44, 48), (46, 64), (18, 64)], OLIVE if cloth != OLIVE else DARK)
        for x in (22, 34):
            f.rect(x, 54, x + 7, 60, shade(OLIVE, -.25))
        f.line([(21, 50), (43, 50)], accent)
    elif clothes == "coat":
        f.poly([(16, 40), (24, 46), (24, 58), (14, 52)], shade(cloth, .2))
        f.poly([(48, 40), (40, 46), (40, 58), (50, 52)], shade(cloth, .1))
        f.poly([(26, 46), (32, 60), (38, 46)], (20, 20, 26))
        f.line([(17, 41), (14, 52)], accent)
    elif clothes == "robe":
        f.poly([(24, 46), (32, 64), (40, 46)], shade(cloth, -.35))
        f.line([(32, 50), (32, 64)], accent)
        f.ell(29, 54, 35, 60, None, outline=accent)


def hairs(f, hair, col, accent, cloth):
    hi = shade(col, .3)
    if hair == "buzz":
        f.poly([(22, 20), (22, 13), (27, 9), (37, 9), (42, 13), (42, 20), (40, 16), (24, 16)], col)
    elif hair == "mohawk":
        f.poly([(22, 20), (23, 14), (41, 14), (42, 20), (40, 17), (24, 17)], shade(col, -.5))
        f.poly([(28, 1), (36, 1), (37, 17), (27, 17)], accent)
        f.line([(30, 2), (30, 15)], shade(accent, .4))
    elif hair == "bob":
        f.poly([(20, 33), (20, 15), (26, 8), (38, 8), (44, 15), (44, 33), (41, 35), (41, 19), (23, 19), (23, 35)], col)
        f.line([(26, 10), (24, 18)], hi)
    elif hair == "long":
        f.poly([(20, 36), (20, 15), (26, 8), (38, 8), (44, 15), (44, 36), (41, 30), (41, 19), (23, 19), (23, 30)], col)
        f.line([(27, 10), (23, 22)], hi)
    elif hair == "pony":
        f.poly([(22, 21), (22, 13), (28, 8), (38, 8), (42, 13), (42, 21), (39, 15), (25, 15)], col)
        f.line([(28, 10), (36, 10)], hi)
    elif hair == "undercut":
        f.poly([(22, 20), (22, 15), (41, 15), (42, 20)], shade(col, -.4))
        f.poly([(21, 22), (22, 11), (30, 6), (40, 8), (45, 15), (44, 22), (40, 16), (30, 15), (24, 20)], col)
        f.line([(30, 8), (42, 12)], hi)
    elif hair == "slick":
        f.poly([(22, 19), (23, 11), (32, 7), (41, 11), (42, 19), (38, 13), (26, 13)], col)
        f.line([(26, 10), (38, 9)], hi)
    elif hair == "spiky":
        f.poly([(21, 20), (20, 8), (25, 12), (27, 3), (31, 10), (34, 2), (36, 10), (41, 4), (40, 12), (45, 9), (43, 20), (40, 15), (24, 15)], col)
    elif hair == "dreads":
        f.poly([(21, 20), (22, 11), (32, 6), (42, 11), (43, 20), (39, 15), (25, 15)], col)
        for x in (20, 23, 41, 44):
            f.line([(x, 18), (x - (1 if x < 32 else -1), 44)], col, 2)
            f.px(x, 30, accent)
    elif hair == "hood":
        f.poly([(16, 52), (18, 20), (26, 8), (38, 8), (46, 20), (48, 52), (44, 50), (43, 22), (36, 14), (28, 14), (21, 22), (20, 50)], shade(cloth, .12))
    elif hair == "cap":
        f.poly([(21, 19), (22, 11), (28, 7), (36, 7), (42, 11), (43, 19)], col)
        f.poly([(20, 18), (44, 18), (46, 21), (18, 21)], shade(col, -.3))
        f.px(31, 12, accent); f.px(32, 12, accent)
    elif hair == "bald":
        f.px(27, 13, (255, 255, 255)); f.px(28, 13, (255, 255, 255))


def mod(f, m, accent, sk, hair_col):
    if m == "visor":
        f.rect(21, 25, 43, 29, accent)
        f.line([(23, 26), (31, 26)], (255, 255, 255))
        f.glow(32, 27, accent, 6, 110)
    elif m == "goggles":
        f.line([(19, 27), (45, 27)], DARK, 2)
        for x in (24, 34):
            f.ell(x, 23, x + 7, 31, METAL, outline=DARK)
            f.ell(x + 2, 25, x + 5, 29, accent)
        f.glow(28, 27, accent, 3, 90); f.glow(38, 27, accent, 3, 90)
    elif m == "cybereye":
        f.ell(34, 24, 41, 31, METAL_L, outline=DARK)
        f.ell(36, 26, 39, 29, RED)
        f.glow(37, 27, RED, 3, 150)
    elif m == "plate":
        f.poly([(33, 17), (41, 17), (43, 25), (42, 34), (38, 40), (33, 42)], CHROME)
        shade_region(f, lambda x, y, p: p[:3] == CHROME and x > 38, -.25)
        for y in (22, 30, 36):
            f.line([(33, y), (42, y)], STEEL)
        f.line([(33, 17), (33, 42)], DARK)
        f.rect(36, 27, 38, 28, RED); f.glow(37, 27, RED, 3, 150)
    elif m == "jaw":
        f.poly([(23, 33), (41, 33), (39, 40), (32, 43), (25, 40)], METAL_L)
        for x in (27, 30, 33, 36):
            f.line([(x, 35), (x, 39)], DARK)
        f.line([(23, 33), (41, 33)], accent)
    elif m == "mask":
        f.poly([(23, 31), (41, 31), (40, 39), (32, 43), (24, 39)], (50, 54, 64))
        f.ell(19, 32, 26, 39, DARK); f.ell(38, 32, 45, 39, DARK)
        f.ell(20, 33, 25, 38, METAL_L); f.ell(39, 33, 44, 38, METAL_L)
        f.line([(28, 37), (36, 37)], accent)
    elif m == "tattoo":
        f.line([(24, 30), (26, 35)], accent); f.line([(25, 21), (28, 22)], accent); f.px(24, 32, accent)
        f.glow(25, 32, accent, 2, 70)
    elif m == "dots":
        for x, y in ((24, 20), (23, 23), (40, 20), (41, 23)):
            f.px(x, y, accent)
        f.glow(24, 21, accent, 2, 60); f.glow(40, 21, accent, 2, 60)
    elif m == "cables":
        for x0, x1 in ((22, 12), (24, 18), (42, 50)):
            f.line([(x0, 26), (x1, 48)], DARK, 2)
            f.px(x1, 48, accent)
    elif m == "antenna":
        f.line([(44, 27), (50, 10)], STEEL); f.px(50, 9, RED); f.glow(50, 9, RED, 2, 120)
    elif m == "scar":
        f.line([(35, 22), (40, 33)], shade(sk, -.35))
    elif m == "headset":
        f.ell(42, 24, 47, 33, DARK)
        f.line([(44, 32), (36, 38)], DARK)
        f.px(36, 38, accent)
    elif m == "chain":
        for x in range(24, 41, 2):
            f.px(x, 47 + abs(32 - x) // 4, GOLD)
    elif m == "shades":
        f.rect(22, 25, 31, 29, (12, 12, 16)); f.rect(33, 25, 42, 29, (12, 12, 16))
        f.line([(31, 26), (33, 26)], (12, 12, 16))
        f.line([(23, 26), (27, 26)], accent); f.line([(34, 26), (38, 26)], accent)
    elif m == "katana":
        f.line([(10, 62), (17, 36)], GRIP, 3)
        f.line([(17, 36), (21, 20)], STEEL, 2)
        f.rect(15, 37, 20, 39, GOLD)
    elif m == "helmet":
        f.ell(19, 7, 45, 41, METAL)
        f.poly([(20, 22), (44, 22), (44, 40), (32, 44), (20, 40)], METAL)
        f.rect(21, 24, 43, 30, accent)
        f.line([(23, 25), (32, 25)], (255, 255, 255))
        f.glow(32, 27, accent, 7, 110)
        shade_region(f, lambda x, y, p: p[:3] == METAL and x > 37, -.25)
        f.line([(26, 35), (38, 35)], DARK); f.line([(28, 38), (36, 38)], DARK)
        f.px(41, 15, RED)
    elif m == "balaclava":
        f.ell(21, 8, 43, 34, (26, 26, 30))
        f.poly([(22, 18), (42, 18), (43, 30), (39, 40), (32, 43), (25, 40), (21, 30)], (26, 26, 30))
        f.rect(24, 25, 40, 30, sk)
        for ex in (26, 36):
            f.rect(ex, 27, ex + 2, 28, (226, 222, 214)); f.px(ex + 1, 27, (40, 30, 30))
    elif m == "beanie":
        f.ell(21, 7, 43, 27, accent)
        f.rect(21, 17, 43, 20, shade(accent, -.3))
    elif m == "glasses":
        f.ell(24, 25, 30, 30, None, outline=(40, 40, 44)); f.ell(34, 25, 40, 30, None, outline=(40, 40, 44))
        f.line([(30, 27), (34, 27)], (40, 40, 44))
    elif m == "earpiece":
        f.px(44, 28, (30, 30, 30)); f.line([(44, 29), (43, 38)], (200, 200, 200))
    elif m == "mil_helmet":
        f.ell(19, 6, 45, 28, (86, 92, 58))
        f.rect(19, 17, 45, 20, shade((86, 92, 58), -.25))
        f.line([(23, 12), (41, 12)], shade((86, 92, 58), .15))
        f.line([(21, 20), (24, 38)], (40, 40, 40)); f.line([(43, 20), (40, 38)], (40, 40, 40))
    elif m == "bandana":
        f.poly([(22, 31), (42, 31), (41, 38), (32, 44), (23, 38)], accent)
        f.line([(26, 35), (38, 35)], shade(accent, -.3))
    elif m == "cigarette":
        f.line([(34, 37), (39, 39)], (236, 236, 230)); f.px(39, 39, (255, 120, 40))
        f.glow(39, 39, (255, 120, 40), 2, 90)
    elif m == "bruise":
        for x, y in ((36, 26), (37, 26), (38, 26), (36, 29), (38, 29), (35, 28), (39, 28)):
            f.px(x, y, (110, 70, 120))
    elif m == "headphones":
        f.line([(20, 24), (24, 10), (40, 10), (44, 24)], (30, 30, 34), 2)
        f.ell(17, 22, 23, 32, (30, 30, 34)); f.ell(41, 22, 47, 32, (30, 30, 34))
    elif m == "robot_head":
        f.poly([(21, 12), (43, 12), (45, 30), (40, 42), (24, 42), (19, 30)], METAL_L)
        shade_region(f, lambda x, y, p: p[:3] == METAL_L and x > 36, -.25)
        f.rect(22, 22, 42, 27, (10, 10, 14))
        f.rect(25, 23, 30, 26, accent); f.rect(34, 23, 39, 26, accent)
        f.glow(28, 24, accent, 3, 140); f.glow(36, 24, accent, 3, 140)
        for x in (26, 29, 32, 35, 38):
            f.line([(x, 33), (x, 39)], DARK)
        f.line([(21, 12), (43, 12)], STEEL)


# ======================= Машины, программы, мутанты =======================

def machine(theme, seed, draw, backdrop=None):
    _, _, _, rim_l, rim_r = THEMES[theme]
    f = Fig()
    draw(f)
    return compose((backdrop or background)(theme, seed), f, rim_l, rim_r)


def drone_sec(f, eye=RED, body=METAL_L):
    for x0, x1 in ((6, 20), (44, 58)):
        f.line([(x0 + 7, 30), (32, 32)], DARK, 3)
        f.ell(x0, 24, x1, 30, None, outline=STEEL)
        f.line([(x0, 27), (x1, 27)], (120, 130, 150))
    f.ell(18, 18, 46, 46, body)
    shade_region(f, lambda x, y, p: p[:3] == body and x + y > 70, -.3)
    f.ell(24, 24, 40, 40, DARK)
    f.ell(27, 27, 37, 37, eye)
    f.ell(30, 30, 34, 34, (255, 255, 255))
    f.glow(32, 32, eye, 9, 120)
    f.line([(32, 18), (32, 10)], STEEL); f.px(32, 9, eye)
    f.rect(26, 46, 38, 50, DARK)


def drone_combat(f):
    for cx, cy in ((12, 16), (52, 16), (12, 48), (52, 48)):
        f.line([(cx, cy), (32, 32)], DARK, 3)
        f.ell(cx - 9, cy - 3, cx + 9, cy + 3, None, outline=STEEL)
    f.poly([(20, 22), (44, 22), (48, 38), (16, 38)], (60, 64, 50))
    f.rect(24, 38, 40, 44, DARK)
    f.line([(26, 44), (24, 56)], STEEL, 2); f.line([(38, 44), (40, 56)], STEEL, 2)
    f.rect(26, 26, 38, 32, (10, 10, 14))
    f.rect(28, 27, 36, 30, ORANGE); f.glow(32, 28, ORANGE, 5, 120)


def turret(f):
    f.poly([(14, 64), (18, 46), (46, 46), (50, 64)], METAL)
    f.rect(20, 26, 44, 46, METAL_L)
    shade_region(f, lambda x, y, p: p[:3] == METAL_L and x > 36, -.25)
    for y in (31, 39):
        f.rect(44, y - 2, 62, y + 1, DARK)
        f.rect(58, y - 3, 63, y + 2, METAL)
    f.rect(24, 30, 32, 36, (10, 10, 14)); f.rect(26, 32, 30, 34, RED); f.glow(28, 33, RED, 4, 140)
    for x in range(22, 44, 4):
        f.px(x, 44, YELLOW)


def mech(f, eye=YELLOW, body=(70, 74, 60), boss=False):
    f.poly([(2, 64), (6, 46), (22, 40), (42, 40), (58, 46), (62, 64)], shade(body, -.2))
    f.poly([(16, 10), (48, 10), (52, 22), (50, 40), (14, 40), (12, 22)], body)
    shade_region(f, lambda x, y, p: p[:3] == body and x > 38, -.25)
    f.rect(18, 20, 46, 28, (10, 10, 14))
    for x in (22, 30, 38):
        f.rect(x, 22, x + 4, 25, eye)
    f.glow(32, 23, eye, 7, 110)
    for x in (20, 26, 32, 38, 44):
        f.line([(x, 31), (x, 38)], DARK)
    f.rect(2, 36, 12, 46, METAL); f.rect(52, 36, 62, 46, METAL)
    if boss:
        f.rect(0, 26, 10, 34, DARK); f.rect(54, 26, 64, 34, DARK)
        f.px(1, 30, RED); f.px(62, 30, RED)
        f.line([(18, 10), (24, 2)], STEEL, 2); f.line([(46, 10), (40, 2)], STEEL, 2)


def android(f, accent=CYAN, skin=(228, 232, 240)):
    f.poly([(4, 64), (8, 53), (19, 46), (45, 46), (56, 53), (60, 64)], (40, 44, 56))
    f.line([(19, 46), (45, 46)], accent)
    f.rect(28, 36, 36, 47, shade(skin, -.2))
    for y in (39, 42, 45):
        f.line([(28, y), (36, y)], STEEL)
    f.ell(22, 9, 42, 31, skin)
    f.poly([(23, 17), (41, 17), (42, 26), (41, 33), (37, 39), (32, 41), (27, 39), (23, 33), (22, 26)], skin)
    shade_region(f, lambda x, y, p: p[:3] == skin and x >= 36, -.15)
    f.line([(22, 22), (42, 22)], STEEL); f.line([(32, 10), (32, 22)], STEEL)
    f.line([(25, 33), (28, 39)], STEEL); f.line([(39, 33), (36, 39)], STEEL)
    for ex in (26, 36):
        f.rect(ex, 27, ex + 2, 28, accent)
    f.glow(32, 27, accent, 6, 90)
    f.line([(30, 36), (34, 36)], STEEL)


def cyberdog(f):
    body = METAL
    f.poly([(6, 64), (12, 44), (28, 38), (40, 44), (44, 64)], shade(body, -.2))
    f.poly([(14, 30), (28, 16), (44, 18), (58, 30), (60, 36), (48, 40), (30, 44), (16, 40)], body)
    f.poly([(26, 18), (24, 4), (32, 14)], body); f.poly([(36, 17), (40, 4), (42, 18)], body)
    shade_region(f, lambda x, y, p: p[:3] == body and y > 34, -.25)
    f.poly([(46, 34), (60, 34), (58, 40), (48, 40)], (12, 12, 16))
    for x in (49, 52, 55):
        f.line([(x, 34), (x, 37)], WHITE)
    f.rect(36, 24, 40, 27, RED); f.glow(38, 25, RED, 4, 150)
    for x in (22, 28):
        f.line([(x, 22), (x + 3, 40)], DARK)
    f.px(59, 31, (10, 10, 10))


def rat(f):
    fur = (96, 84, 80)
    f.poly([(4, 64), (10, 42), (30, 34), (44, 44), (50, 64)], shade(fur, -.2))
    f.ell(14, 14, 50, 46, fur)
    f.poly([(40, 26), (62, 34), (60, 40), (42, 42)], fur)
    f.ell(14, 4, 26, 18, fur); f.ell(16, 6, 24, 16, (180, 110, 120))
    f.ell(34, 4, 46, 18, fur); f.ell(36, 6, 44, 16, (180, 110, 120))
    shade_region(f, lambda x, y, p: p[:3] == fur and y > 38, -.25)
    f.rect(30, 24, 34, 27, GREEN); f.glow(32, 25, GREEN, 4, 150)
    f.line([(52, 40), (52, 44)], WHITE); f.line([(55, 39), (55, 43)], WHITE)
    for y in (32, 34, 36):
        f.line([(60, y), (64, y - 2)], BONE)
    f.line([(16, 30), (24, 32)], (60, 200, 90)); f.line([(18, 36), (26, 36)], (60, 200, 90))


def mutant(f):
    sk = (130, 160, 110)
    f.poly([(2, 64), (6, 48), (20, 42), (46, 42), (60, 50), (62, 64)], (60, 56, 50))
    f.ell(40, 40, 62, 60, sk)
    f.rect(28, 36, 36, 46, shade(sk, -.2))
    f.ell(20, 8, 44, 32, sk)
    f.poly([(22, 18), (42, 18), (44, 28), (40, 38), (32, 42), (24, 38), (20, 28)], sk)
    f.ell(34, 6, 48, 20, shade(sk, .1))
    shade_region(f, lambda x, y, p: p[:3] == sk and x > 36, -.2)
    f.rect(25, 26, 28, 28, YELLOW); f.rect(36, 25, 40, 28, YELLOW)
    f.glow(32, 27, YELLOW, 5, 90)
    f.poly([(27, 34), (37, 34), (35, 38), (29, 38)], (60, 20, 20))
    for x in (29, 31, 33, 35):
        f.px(x, 34, BONE)
    f.line([(16, 30), (8, 50)], (180, 80, 90), 2); f.line([(46, 20), (56, 40)], (180, 80, 90), 2)


def ai_ghost(f, col=CYAN):
    rng = random.Random(7)
    for y in range(8, 46, 3):
        w = int(14 * math.sin(math.pi * (y - 6) / 42)) + 2
        off = rng.choice([0, 0, 0, 2, -2])
        f.line([(32 - w + off, y), (32 + w + off, y)], mix((0, 0, 0), col, .55))
    for x in range(20, 46, 4):
        f.line([(x, 12), (x, 42)], mix((0, 0, 0), col, .3))
    f.rect(24, 22, 29, 25, (255, 255, 255)); f.rect(35, 22, 40, 25, (255, 255, 255))
    f.glow(26, 23, col, 4, 160); f.glow(37, 23, col, 4, 160)
    f.line([(27, 34), (37, 34)], col)
    for _ in range(6):
        y = rng.randint(10, 44)
        f.rect(rng.randint(10, 40), y, rng.randint(44, 56), y, MAGENTA)
    f.poly([(12, 64), (20, 50), (44, 50), (52, 64)], mix((0, 0, 0), col, .25))


def ice_daemon(f, col=MAGENTA):
    pts = [(32, 44), (22, 38), (16, 28), (22, 18), (32, 12), (42, 18), (48, 28), (42, 38)]
    f.poly(pts, mix((10, 0, 20), col, .3))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        f.line([a, b], col)
        f.line([a, (32, 28)], mix((0, 0, 0), col, .5))
    f.ell(26, 22, 38, 34, (10, 4, 16))
    f.ell(29, 25, 35, 31, col); f.ell(31, 27, 33, 29, (255, 255, 255))
    f.glow(32, 28, col, 10, 110)
    for k in range(5):
        x = 8 + k * 12
        f.poly([(x, 64), (x + 3, 50 - k % 2 * 6), (x + 6, 64)], mix((10, 0, 20), col, .5))


def spider(f):
    for side in (-1, 1):
        for k, y in enumerate((24, 32, 40)):
            x0 = 32 + side * 12
            xm = 32 + side * (22 + k * 2)
            f.line([(x0, y), (xm, y - 10)], DARK, 2)
            f.line([(xm, y - 10), (32 + side * (28 + k * 2), y + 14)], STEEL, 2)
    f.ell(18, 20, 46, 44, METAL)
    shade_region(f, lambda x, y, p: p[:3] == METAL and y > 34, -.25)
    for x, y in ((25, 28), (32, 26), (39, 28), (28, 33), (36, 33)):
        f.rect(x - 1, y - 1, x + 1, y + 1, RED)
    f.glow(32, 29, RED, 7, 110)


def ai_core(f):
    for r, a in ((28, .25), (22, .45), (16, .7)):
        f.ell(32 - r, 32 - r, 32 + r, 32 + r, None, outline=mix((0, 0, 0), RED, a), width=2)
    f.ell(20, 20, 44, 44, (20, 6, 10))
    f.ell(24, 24, 40, 40, RED)
    f.ell(28, 28, 36, 36, (255, 220, 200))
    f.glow(32, 32, RED, 14, 120)
    for a in range(0, 360, 45):
        x, y = 32 + 28 * math.cos(math.radians(a)), 32 + 28 * math.sin(math.radians(a))
        f.rect(int(x) - 2, int(y) - 2, int(x) + 2, int(y) + 2, METAL_L)


def titan(f):
    body = (90, 92, 104)
    f.poly([(0, 64), (2, 44), (18, 36), (46, 36), (62, 44), (64, 64)], body)
    f.ell(0, 34, 20, 52, METAL_L); f.ell(44, 34, 64, 52, METAL_L)
    f.rect(26, 30, 38, 40, shade(SKINS[3], -.2))
    f.ell(20, 4, 44, 30, SKINS[3])
    f.poly([(21, 14), (43, 14), (44, 26), (40, 36), (24, 36), (20, 26)], SKINS[3])
    f.poly([(21, 26), (43, 26), (41, 38), (32, 42), (23, 38)], METAL_L)
    for x in (25, 29, 33, 37):
        f.line([(x, 29), (x, 36)], DARK)
    f.rect(24, 19, 29, 21, RED); f.rect(35, 19, 40, 21, RED)
    f.glow(32, 20, RED, 6, 120)
    f.line([(22, 8), (42, 8)], DARK)
    shade_region(f, lambda x, y, p: p[:3] == SKINS[3] and x > 36, -.2)


# ======================= Листы =======================

def L(f):
    return f


ENEMIES_STREET = [
    # (id, функция) — порядок = клетки листа
    ("ganger", lambda: person("pink", 1, "m", 2, "undercut", (40, 30, 30), None, ("tattoo",), "jacket", (90, 30, 40), MAGENTA, beard="stubble")),
    ("ganger_f", lambda: person("teal", 2, "f", 0, "bob", (240, 60, 160), None, ("dots",), "jacket", (40, 40, 56), CYAN, lips=(170, 40, 80))),
    ("punk", lambda: person("acid", 3, "m", 5, "mohawk", (30, 30, 30), None, ("tattoo", "chain"), "tank", (30, 30, 34), GREEN)),
    ("bruiser", lambda: person("red", 4, "m", 3, "bald", (20, 20, 20), None, ("jaw", "scar"), "tank", (40, 40, 40), RED)),
    ("corp_guard", lambda: person("blue", 5, "m", 1, "bald", (0, 0, 0), None, ("helmet",), "armor", NAVY, (80, 160, 255))),
    ("corp_soldier", lambda: person("blue", 6, "m", 1, "bald", (0, 0, 0), None, ("helmet",), "armor", (40, 44, 56), RED)),
    ("cop", lambda: person("blue", 7, "m", 2, "cap", (20, 24, 50), None, ("headset",), "vest", NAVY, (80, 160, 255), beard="stubble")),
    ("netrunner", lambda: person("violet", 8, "f", 5, "hood", (20, 20, 20), None, ("goggles", "cables"), "hoodie", (36, 30, 50), VIOLET)),
    ("cyberpsycho", lambda: person("red", 9, "m", 4, "spiky", (200, 200, 210), RED, ("plate", "cables"), "tank", (30, 20, 20), RED)),
    ("cyber_ninja", lambda: person("violet", 10, "m", 1, "hood", (0, 0, 0), MAGENTA, ("mask", "katana"), "hoodie", (20, 20, 28), MAGENTA)),
    ("mercenary", lambda: person("gold", 11, "m", 2, "buzz", (60, 50, 40), None, ("scar", "cybereye"), "vest", OLIVE, ORANGE, beard="full")),
    ("sniper", lambda: person("teal", 12, "f", 1, "pony", (30, 26, 24), None, ("cybereye", "headset"), "vest", (50, 54, 44), CYAN)),
    ("tech_priest", lambda: person("acid", 13, "m", 5, "hood", (0, 0, 0), GREEN, ("cables", "jaw"), "robe", (40, 40, 30), GREEN)),
    ("raider", lambda: person("gold", 14, "m", 3, "dreads", (40, 30, 20), None, ("goggles", "mask"), "coat", (90, 66, 40), ORANGE)),
    ("fixer_boss", lambda: person("gold", 15, "m", 4, "slick", (20, 20, 20), None, ("shades", "chain", "dots"), "suit", (24, 24, 28), GOLD, beard="goatee")),
    ("corp_exec", lambda: person("pink", 16, "f", 0, "slick", (220, 220, 230), CYAN, ("dots",), "suit", (230, 230, 236), RED, lips=(150, 20, 50))),
]

ENEMIES_MACHINES = [
    ("drone_sec", lambda: machine("blue", 21, drone_sec)),
    ("drone_combat", lambda: machine("gold", 22, drone_combat)),
    ("turret", lambda: machine("red", 23, turret)),
    ("combat_robot", lambda: person("red", 24, "m", 0, "bald", (0, 0, 0), None, ("robot_head",), "armor", (50, 50, 60), RED)),
    ("android", lambda: machine("teal", 25, android)),
    ("mech", lambda: machine("acid", 26, mech)),
    ("cyberdog", lambda: machine("red", 27, cyberdog)),
    ("mutant_rat", lambda: machine("acid", 28, rat)),
    ("mutant", lambda: machine("acid", 29, mutant)),
    ("ai_ghost", lambda: machine("teal", 30, ai_ghost)),
    ("ice_daemon", lambda: machine("violet", 31, ice_daemon)),
    ("spider_bot", lambda: machine("red", 32, spider)),
    ("borg_titan", lambda: machine("red", 33, titan)),
    ("war_mech", lambda: machine("red", 34, lambda f: mech(f, RED, (60, 60, 70), True))),
    ("ai_core", lambda: machine("red", 35, ai_core)),
    ("android_assassin", lambda: machine("violet", 36, lambda f: android(f, MAGENTA, (40, 40, 48)))),
]

PERSONS = [
    ("cy-solo-m", lambda: person("red", 41, "m", 2, "buzz", (30, 24, 20), None, ("cybereye",), "jacket", (40, 40, 46), RED, beard="stubble")),
    ("cy-solo-f", lambda: person("pink", 42, "f", 1, "undercut", (20, 20, 24), None, ("tattoo",), "jacket", (60, 20, 30), MAGENTA, lips=(160, 50, 70))),
    ("cy-netrunner-f", lambda: person("violet", 43, "f", 0, "bob", (110, 230, 255), None, ("visor", "dots"), "hoodie", (40, 34, 60), CYAN)),
    ("cy-netrunner-m", lambda: person("violet", 44, "m", 4, "dreads", (20, 20, 20), None, ("goggles", "cables"), "hoodie", (30, 30, 44), VIOLET)),
    ("cy-techie-m", lambda: person("acid", 45, "m", 1, "spiky", (220, 140, 40), None, ("goggles",), "vest", (60, 50, 40), YELLOW)),
    ("cy-techie-f", lambda: person("acid", 46, "f", 3, "pony", (30, 20, 20), None, ("headset", "dots"), "vest", (50, 46, 40), GREEN)),
    ("cy-medtech-f", lambda: person("teal", 47, "f", 0, "pony", (200, 180, 140), None, ("headset",), "jacket", (220, 224, 230), RED, lips=(170, 80, 90))),
    ("cy-medtech-m", lambda: person("teal", 48, "m", 4, "buzz", (20, 20, 20), None, ("visor",), "jacket", (200, 210, 220), (60, 255, 200))),
    ("cy-nomad-m", lambda: person("gold", 49, "m", 2, "long", (70, 50, 30), None, ("goggles",), "coat", (100, 74, 50), ORANGE, beard="full")),
    ("cy-nomad-f", lambda: person("gold", 50, "f", 3, "dreads", (40, 26, 20), None, ("tattoo",), "coat", (110, 80, 50), ORANGE)),
    ("cy-fixer-m", lambda: person("gold", 51, "m", 3, "slick", (30, 30, 30), None, ("shades", "chain"), "suit", (60, 30, 40), GOLD, beard="goatee")),
    ("cy-corpo-f", lambda: person("blue", 52, "f", 0, "slick", (40, 30, 26), None, ("dots",), "suit", NAVY, (80, 160, 255), lips=(150, 40, 60))),
    ("cy-rocker-m", lambda: person("pink", 53, "m", 0, "long", (20, 20, 20), None, ("shades", "plate"), "jacket", (30, 30, 30), MAGENTA, beard="stubble")),
    ("cy-bartender-f", lambda: person("pink", 54, "f", 2, "long", (190, 40, 60), None, ("tattoo",), "tank", (30, 30, 36), MAGENTA, lips=(180, 40, 70))),
    ("cy-cop-m", lambda: person("blue", 55, "m", 1, "cap", (20, 24, 50), None, ("headset",), "vest", NAVY, (80, 160, 255))),
    ("cy-punk-f", lambda: person("acid", 56, "f", 5, "mohawk", (20, 20, 20), None, ("tattoo", "dots"), "jacket", (30, 30, 34), GREEN, lips=(40, 40, 40))),
]

HEROES = [
    ("cy-hero-solo-m", lambda: person("red", 61, "m", 1, "undercut", (26, 22, 20), None, ("cybereye",), "jacket", (36, 36, 44), RED, beard="stubble")),
    ("cy-hero-runner-m", lambda: person("pink", 62, "m", 3, "hood", (20, 20, 20), None, ("tattoo",), "hoodie", (30, 30, 40), MAGENTA)),
    ("cy-hero-netrunner-m", lambda: person("violet", 63, "m", 0, "spiky", (220, 220, 240), None, ("visor", "cables"), "coat", (34, 30, 50), CYAN)),
    ("cy-hero-solo-f", lambda: person("red", 64, "f", 2, "undercut", (180, 30, 50), None, ("plate",), "jacket", (40, 30, 34), RED, lips=(150, 40, 60))),
    ("cy-hero-runner-f", lambda: person("pink", 65, "f", 0, "bob", (20, 20, 24), None, ("dots",), "jacket", (50, 20, 60), MAGENTA, lips=(170, 40, 90))),
    ("cy-hero-netrunner-f", lambda: person("violet", 66, "f", 1, "long", (150, 110, 255), None, ("goggles", "cables"), "hoodie", (30, 26, 50), VIOLET, lips=(120, 60, 120))),
]

SHEETS = [
    ("cyber-enemies-street.png", ENEMIES_STREET, 4),
    ("cyber-enemies-machines.png", ENEMIES_MACHINES, 4),
    ("cyber-persons.png", PERSONS, 4),
    ("cyber-hero-avatars.png", HEROES, 3),
]


def build_sheet(entries, cols):
    rows = (len(entries) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * S * K, rows * S * K))
    for n, (_, draw) in enumerate(entries):
        img = draw().convert("RGB").resize((S * K, S * K), Image.NEAREST)
        sheet.paste(img, ((n % cols) * S * K, (n // cols) * S * K))
    return sheet


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    for file, entries, cols in SHEETS:
        if only and only not in file:
            continue
        sheet = build_sheet(entries, cols)
        sheet.save(os.path.join(ROOT, "wwwroot", "sprites", file), optimize=True)
        print(file, len(entries))


if __name__ == "__main__":
    main()
