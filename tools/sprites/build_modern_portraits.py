"""
Портреты сеттинга «Современность», нарисованные кодом: уличные враги и силовики, звери и техника,
лица спутников/NPC и аватары героя. Манера общая с киберпанком (build_cyber_portraits.py: те же лица,
причёски и одежда), но фон приземлённый — вечерний город с жёлтыми окнами и натриевыми фонарями или лес,
а контровой свет — тёплый фонарный и холодный уличный, без неона.

Порядок клеток и id — только дописывать (id хранятся в кампаниях).

Запуск: python tools/sprites/build_modern_portraits.py [street|threats|persons|hero]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_cyber_portraits as cp  # noqa: E402
from build_cyber_portraits import Fig, S, K, shade_region  # noqa: E402
from pixelkit import BONE, mix, shade  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

# Темы: (верх неба, низ, цвета окон/фонарей, контровой слева, справа)
cp.THEMES.update({
    "dusk": ((58, 46, 62), (18, 16, 22), [(255, 196, 110), (240, 170, 90), (210, 210, 200)], (140, 168, 205), (255, 172, 92)),
    "night": ((22, 26, 40), (8, 9, 14), [(255, 206, 120), (236, 232, 200), (130, 160, 210)], (122, 152, 214), (255, 180, 104)),
    "police": ((26, 28, 46), (8, 8, 16), [(70, 120, 255), (255, 60, 60), (230, 230, 230)], (90, 140, 255), (255, 70, 70)),
    "overcast": ((76, 80, 88), (30, 32, 36), [(236, 226, 196), (206, 206, 206), (255, 206, 130)], (196, 206, 216), (236, 196, 146)),
    "forest": ((46, 58, 50), (14, 18, 14), [(236, 224, 176), (206, 186, 126), (130, 170, 130)], (186, 206, 176), (240, 196, 126)),
    "ember": ((64, 30, 24), (18, 8, 8), [(255, 176, 96), (236, 100, 70), (240, 220, 180)], (255, 196, 126), (210, 70, 56)),
})


def backdrop(theme, seed):
    """Приземлённый фон: панельки и кирпич с тёплыми окнами, фонари; для «forest» — ельник."""
    top, bot, warm, _, _ = cp.THEMES[theme]
    rng = random.Random(seed * 7 + 3)
    img = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(img)
    for y in range(S):
        d.line([(0, y), (S, y)], fill=mix(top, bot, y / S) + (255,))
    if theme == "forest":
        for layer, dark in enumerate((.55, .8)):
            x = -4
            while x < S + 4:
                h = rng.randint(22, 40) + layer * 6
                w = rng.randint(7, 12)
                col = mix(bot, (0, 0, 0), dark)
                d.polygon([(x, S), (x + w // 2, S - h), (x + w, S)], fill=col + (255,))
                x += w - rng.randint(1, 4)
        for _ in range(10):
            sx, sy = rng.randint(0, S), rng.randint(0, 30)
            d.point((sx, sy), fill=(230, 230, 220, 120))
        return img
    # дома: панельки с ровной сеткой окон, часть горит тёплым
    for layer, (dark, hmin, hmax, lit) in enumerate(((.5, 18, 36, .22), (.78, 10, 24, .12))):
        x = -rng.randint(0, 6)
        while x < S:
            w = rng.randint(8, 16)
            h = rng.randint(hmin, hmax) + layer * 4
            col = mix(bot, (0, 0, 0), dark)
            d.rectangle([x, S - h, x + w - 1, S], fill=col + (255,))
            for wy in range(S - h + 2, S - 2, 3):
                for wx in range(x + 1, x + w - 1, 2):
                    if rng.random() < lit:
                        c = rng.choice(warm[:2])
                        d.point((wx, wy), fill=mix(c, bot, .35 + layer * .25) + (255,))
            x += w + rng.randint(0, 3)
    # провода и фонарь
    d.line([(0, rng.randint(8, 14)), (S, rng.randint(10, 18))], fill=mix(bot, (0, 0, 0), .4) + (255,))
    lx = rng.choice([rng.randint(3, 12), rng.randint(50, 60)])
    d.line([(lx, 20), (lx, S)], fill=mix(bot, (0, 0, 0), .5) + (255,))
    glow = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(glow)
    lamp = warm[0] if theme != "police" else warm[0]
    gd.ellipse([lx - 9, 11, lx + 9, 29], fill=lamp + (80,))
    if theme == "police":
        gd.ellipse([54, 30, 70, 46], fill=(255, 50, 50, 70))
        gd.ellipse([-6, 30, 10, 46], fill=(60, 110, 255, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(3))
    img.alpha_composite(glow)
    d.rectangle([lx - 2, 19, lx + 2, 21], fill=lamp + (255,))
    if theme in ("night", "overcast", "police"):
        for _ in range(10):
            rx, ry = rng.randint(0, S), rng.randint(0, S)
            d.line([(rx, ry), (rx - 1, ry + 3)], fill=(170, 180, 200, 40))
    return img


def P(theme, seed, *a, **k):
    return cp.person(theme, seed, *a, backdrop=backdrop, **k)


def M(theme, seed, draw):
    return cp.machine(theme, seed, draw, backdrop=backdrop)


# ======================= Звери и техника =======================

def dog(f, coat=(40, 34, 30), tan=(150, 96, 52), eye=(230, 150, 60), ears="up"):
    f.poly([(6, 64), (12, 44), (28, 38), (42, 44), (46, 64)], shade(coat, -.15))
    f.poly([(14, 30), (28, 16), (44, 18), (56, 28), (60, 36), (48, 42), (30, 44), (16, 40)], coat)
    if ears == "up":
        f.poly([(26, 18), (24, 4), (32, 14)], coat); f.poly([(36, 17), (40, 4), (42, 18)], coat)
    else:
        f.poly([(24, 18), (18, 30), (28, 22)], shade(coat, -.2))
    f.poly([(44, 30), (60, 34), (58, 40), (46, 42)], tan)
    f.ell(55, 31, 60, 35, (14, 12, 12))
    f.rect(36, 24, 39, 26, eye); f.px(37, 24, (10, 10, 10))
    f.poly([(24, 30), (30, 34), (24, 38)], tan)
    f.line([(48, 40), (56, 39)], (230, 220, 210))
    shade_region(f, lambda x, y, p: p[:3] == coat and y > 34, -.25)


def wolf(f):
    dog(f, (110, 110, 112), (186, 180, 170), (230, 200, 90))


def bear(f):
    fur = (88, 62, 44)
    f.poly([(0, 64), (6, 40), (22, 32), (44, 32), (60, 40), (64, 64)], shade(fur, -.15))
    f.ell(14, 12, 50, 46, fur)
    f.ell(12, 8, 22, 18, fur); f.ell(42, 8, 52, 18, fur)
    f.ell(14, 10, 20, 16, shade(fur, -.3)); f.ell(44, 10, 50, 16, shade(fur, -.3))
    f.ell(24, 28, 40, 42, (130, 100, 74))
    f.ell(29, 28, 35, 32, (20, 16, 14))
    f.line([(28, 38), (36, 38)], (40, 26, 20))
    f.rect(22, 22, 24, 24, (20, 16, 14)); f.rect(40, 22, 42, 24, (20, 16, 14))
    shade_region(f, lambda x, y, p: p[:3] == fur and x > 40, -.2)


def boar(f):
    fur = (70, 58, 50)
    f.poly([(2, 64), (8, 40), (30, 30), (50, 36), (60, 64)], shade(fur, -.15))
    f.poly([(12, 30), (30, 18), (48, 22), (60, 34), (58, 44), (40, 46), (18, 42)], fur)
    f.poly([(24, 20), (22, 10), (30, 18)], fur)
    f.ell(52, 32, 62, 42, (150, 110, 100)); f.px(55, 36, (20, 20, 20)); f.px(58, 36, (20, 20, 20))
    f.poly([(48, 40), (54, 34), (52, 42)], BONE)
    f.rect(38, 26, 40, 28, (20, 14, 12))
    for x in range(18, 44, 3):
        f.line([(x, 20 + abs(30 - x) // 4), (x - 1, 16 + abs(30 - x) // 4)], shade(fur, -.3))


def suv(f, body=(26, 28, 32)):
    f.poly([(4, 64), (4, 40), (12, 22), (52, 22), (60, 40), (60, 64)], body)
    f.poly([(14, 26), (50, 26), (56, 38), (8, 38)], (60, 74, 90))
    f.line([(16, 28), (30, 28)], (140, 160, 180))
    f.rect(8, 44, 56, 52, shade(body, -.3))
    for x in range(14, 52, 4):
        f.line([(x, 45), (x, 51)], (70, 72, 76))
    f.rect(6, 42, 13, 47, (240, 236, 210)); f.rect(51, 42, 58, 47, (240, 236, 210))
    f.glow(9, 44, (255, 240, 200), 5, 110); f.glow(55, 44, (255, 240, 200), 5, 110)
    f.rect(24, 54, 40, 58, (200, 200, 200))
    shade_region(f, lambda x, y, p: p[:3] == body and x > 44, -.2)


def technical(f):
    body = (150, 140, 120)
    f.poly([(2, 64), (2, 46), (8, 40), (24, 40), (30, 30), (44, 30), (48, 40), (62, 42), (62, 64)], body)
    f.poly([(31, 32), (43, 32), (46, 40), (30, 40)], (70, 84, 96))
    f.ell(6, 52, 20, 64, (20, 20, 22)); f.ell(44, 52, 58, 64, (20, 20, 22))
    f.ell(10, 56, 16, 62, (90, 90, 94)); f.ell(48, 56, 54, 62, (90, 90, 94))
    f.rect(10, 28, 16, 40, (40, 40, 44))
    f.rect(16, 26, 36, 29, (30, 30, 34))
    f.rect(6, 22, 14, 30, (60, 64, 56))
    shade_region(f, lambda x, y, p: p[:3] == body and y > 48, -.2)


def police_car(f):
    body = (230, 232, 236)
    f.poly([(2, 64), (2, 46), (10, 40), (20, 30), (44, 30), (54, 40), (62, 46), (62, 64)], body)
    f.poly([(21, 33), (43, 33), (50, 41), (14, 41)], (50, 66, 90))
    f.rect(2, 48, 62, 54, (30, 50, 120))
    f.rect(24, 25, 31, 29, (60, 110, 255)); f.rect(33, 25, 40, 29, (255, 60, 60))
    f.glow(27, 27, (60, 110, 255), 6, 120); f.glow(36, 27, (255, 60, 60), 6, 120)
    f.ell(6, 54, 20, 64, (20, 20, 22)); f.ell(44, 54, 58, 64, (20, 20, 22))


def helicopter(f):
    body = (60, 66, 58)
    f.line([(2, 12), (62, 8)], (40, 40, 44), 2)
    f.rect(30, 10, 34, 18, (40, 40, 44))
    f.ell(16, 16, 48, 44, body)
    f.ell(22, 20, 40, 34, (90, 120, 140))
    f.poly([(44, 30), (62, 28), (62, 32), (46, 36)], body)
    f.line([(20, 46), (44, 46)], (40, 40, 44), 2)
    f.line([(24, 40), (22, 46)], (40, 40, 44)); f.line([(40, 40), (42, 46)], (40, 40, 44))
    f.rect(12, 34, 18, 37, (40, 40, 44))
    shade_region(f, lambda x, y, p: p[:3] == body and y > 34, -.25)


def quad(f):
    body = (230, 230, 232)
    for cx, cy in ((12, 18), (52, 18), (12, 44), (52, 44)):
        f.line([(cx, cy), (32, 31)], (60, 60, 64), 3)
        f.ell(cx - 9, cy - 2, cx + 9, cy + 2, None, outline=(160, 160, 166))
    f.ell(22, 22, 42, 40, body)
    f.ell(28, 34, 36, 42, (30, 30, 34)); f.ell(30, 36, 34, 40, (90, 140, 200))
    f.px(24, 26, (255, 60, 60))


# ======================= Листы =======================
TRACK = (34, 36, 70)
STREET = [
    ("gopnik", lambda: P("night", 101, "m", 5, "cap", (40, 40, 44), None, ("cigarette",), "tracksuit", TRACK, (230, 230, 230), beard="stubble")),
    ("robber", lambda: P("night", 102, "m", 1, "bald", (0, 0, 0), None, ("balaclava",), "jacket", (30, 30, 34), (90, 90, 90))),
    ("biker", lambda: P("dusk", 103, "m", 2, "long", (60, 50, 40), None, ("bandana", "chain"), "jacket", (36, 30, 28), (150, 40, 40), beard="full")),
    ("dealer", lambda: P("night", 104, "m", 3, "hood", (20, 20, 20), None, ("chain",), "hoodie", (60, 60, 66), (200, 200, 200), beard="goatee")),
    ("enforcer", lambda: P("ember", 105, "m", 0, "bald", (20, 20, 20), None, ("chain", "scar"), "jacket", (24, 24, 26), (210, 168, 60), beard="stubble")),
    ("hitman", lambda: P("overcast", 106, "m", 0, "slick", (30, 26, 24), None, ("shades", "earpiece"), "suit", (28, 28, 32), (40, 40, 48))),
    ("sec_guard", lambda: P("night", 107, "m", 2, "cap", (30, 34, 50), None, ("earpiece",), "police", (60, 70, 90), (230, 230, 230))),
    ("police_officer", lambda: P("police", 108, "m", 1, "cap", (24, 30, 60), None, (), "police", (40, 60, 110), (230, 230, 230), beard="stubble")),
    ("swat_officer", lambda: P("police", 109, "m", 1, "bald", (0, 0, 0), None, ("mil_helmet", "balaclava"), "plate", (30, 30, 34), (40, 40, 44))),
    ("pmc_operator", lambda: P("dusk", 110, "m", 2, "cap", (90, 84, 70), None, ("headphones", "glasses"), "plate", (70, 66, 56), (86, 92, 58), beard="full")),
    ("army_soldier", lambda: P("forest", 111, "m", 0, "buzz", (60, 50, 40), None, ("mil_helmet",), "camo", (86, 92, 58), (86, 92, 58))),
    ("marksman", lambda: P("forest", 112, "f", 1, "pony", (40, 30, 24), None, ("headphones",), "camo", (100, 96, 70), (86, 92, 58), lips=(150, 90, 90))),
    ("mob_boss", lambda: P("ember", 113, "m", 0, "slick", (180, 180, 186), None, ("chain", "glasses"), "suit", (24, 24, 30), (150, 30, 40), beard="goatee")),
    ("kingpin", lambda: P("dusk", 114, "m", 3, "bald", (0, 0, 0), None, ("shades", "chain", "cigarette"), "suit", (230, 230, 226), (210, 168, 60))),
    ("corrupt_official", lambda: P("overcast", 115, "m", 0, "slick", (120, 110, 100), None, ("glasses",), "suit", (40, 46, 60), (40, 60, 110))),
    ("prisoner", lambda: P("night", 116, "m", 0, "buzz", (60, 50, 44), None, ("scar", "bruise"), "tshirt", (180, 180, 176), (60, 60, 60))),
]

THREATS = [
    ("guard_dog", lambda: M("night", 121, lambda f: dog(f, (30, 26, 24), (176, 110, 56), (220, 140, 60), "down"))),
    ("doberman", lambda: M("dusk", 122, lambda f: dog(f, (24, 20, 20), (150, 90, 46), (240, 140, 50)))),
    ("timber_wolf", lambda: M("forest", 123, wolf)),
    ("brown_bear", lambda: M("forest", 124, bear)),
    ("wild_boar", lambda: M("forest", 125, boar)),
    ("armored_suv", lambda: M("night", 126, suv)),
    ("technical", lambda: M("ember", 127, technical)),
    ("police_car", lambda: M("police", 128, police_car)),
    ("helicopter", lambda: M("overcast", 129, helicopter)),
    ("quad_drone", lambda: M("dusk", 130, quad)),
    ("maniac", lambda: P("ember", 131, "m", 0, "bald", (0, 0, 0), None, ("balaclava", "bruise"), "jacket", (60, 40, 30), (120, 30, 30))),
    ("sect_leader", lambda: P("ember", 132, "m", 0, "long", (200, 200, 196), None, (), "coat", (40, 30, 30), (150, 30, 30), beard="full")),
    ("killer_f", lambda: P("night", 133, "f", 0, "bob", (20, 20, 22), None, ("earpiece",), "coat", (24, 24, 28), (120, 30, 40), lips=(150, 30, 40))),
    ("heavy_gunner", lambda: P("ember", 134, "m", 4, "bald", (0, 0, 0), None, ("bandana",), "plate", (50, 50, 46), (60, 60, 56), beard="full")),
    ("riot_cop", lambda: P("police", 135, "m", 1, "bald", (0, 0, 0), None, ("mil_helmet",), "police", (30, 34, 50), (230, 230, 230))),
    ("warlord", lambda: P("ember", 136, "m", 2, "cap", (90, 80, 60), None, ("shades", "scar"), "camo", (90, 84, 60), (86, 92, 58), beard="full")),
]

PERSONS = [
    ("md-soldier-m", lambda: P("forest", 141, "m", 1, "buzz", (50, 40, 30), None, ("scar",), "camo", (86, 92, 58), (86, 92, 58), beard="stubble")),
    ("md-soldier-f", lambda: P("forest", 142, "f", 2, "pony", (30, 24, 20), None, (), "plate", (70, 70, 60), (86, 92, 58), lips=(150, 90, 90))),
    ("md-medic-f", lambda: P("overcast", 143, "f", 0, "bob", (150, 100, 60), None, (), "scrubs", (80, 150, 160), (60, 120, 130), lips=(170, 90, 100))),
    ("md-medic-m", lambda: P("overcast", 144, "m", 3, "buzz", (20, 20, 20), None, ("glasses",), "scrubs", (70, 110, 170), (50, 80, 130))),
    ("md-hacker-m", lambda: P("night", 145, "m", 0, "hood", (30, 30, 30), None, ("glasses", "headphones"), "hoodie", (40, 40, 46), (90, 200, 120))),
    ("md-hacker-f", lambda: P("night", 146, "f", 1, "undercut", (190, 60, 90), None, ("glasses",), "hoodie", (60, 50, 70), (200, 200, 200), lips=(160, 70, 90))),
    ("md-detective-m", lambda: P("overcast", 147, "m", 0, "slick", (90, 70, 50), None, ("cigarette",), "coat", (120, 96, 66), (60, 50, 40), beard="stubble")),
    ("md-journalist-f", lambda: P("dusk", 148, "f", 0, "long", (120, 80, 40), None, ("glasses",), "jacket", (90, 110, 130), (200, 200, 200), lips=(170, 80, 90))),
    ("md-driver-m", lambda: P("dusk", 149, "m", 2, "cap", (60, 40, 30), None, (), "jacket", (40, 60, 90), (200, 200, 200), beard="full")),
    ("md-mechanic-f", lambda: P("dusk", 150, "f", 3, "beanie", (30, 24, 20), None, ("beanie",), "tshirt", (90, 100, 110), (200, 110, 40), lips=(140, 80, 80))),
    ("md-cop-m", lambda: P("police", 151, "m", 0, "buzz", (60, 50, 40), None, (), "police", (40, 60, 110), (230, 230, 230), beard="stubble")),
    ("md-lawyer-f", lambda: P("overcast", 152, "f", 0, "slick", (40, 30, 26), None, (), "suit", (36, 36, 44), (230, 230, 230), lips=(150, 40, 60))),
    ("md-boxer-m", lambda: P("ember", 153, "m", 4, "buzz", (20, 20, 20), None, ("bruise",), "tshirt", (180, 40, 40), (230, 230, 230))),
    ("md-bartender-f", lambda: P("ember", 154, "f", 2, "long", (30, 20, 20), None, (), "tshirt", (30, 30, 34), (200, 160, 60), lips=(170, 50, 70))),
    ("md-veteran-m", lambda: P("dusk", 155, "m", 0, "buzz", (190, 190, 186), None, ("scar",), "jacket", (70, 74, 56), (200, 200, 200), beard="full")),
    ("md-student-f", lambda: P("night", 156, "f", 5, "pony", (60, 40, 30), None, ("headphones",), "hoodie", (150, 60, 80), (230, 230, 230), lips=(160, 90, 100))),
]

HEROES = [
    ("md-hero-fighter-m", lambda: P("ember", 161, "m", 1, "buzz", (40, 30, 24), None, ("scar",), "jacket", (36, 30, 28), (200, 120, 50), beard="stubble")),
    ("md-hero-rogue-m", lambda: P("night", 162, "m", 3, "hood", (20, 20, 20), None, (), "hoodie", (40, 42, 48), (200, 200, 200), beard="stubble")),
    ("md-hero-expert-m", lambda: P("overcast", 163, "m", 0, "slick", (60, 44, 30), None, ("glasses",), "coat", (60, 66, 76), (200, 200, 200))),
    ("md-hero-fighter-f", lambda: P("ember", 164, "f", 2, "pony", (20, 16, 14), None, ("scar",), "plate", (60, 60, 54), (86, 92, 58), lips=(150, 70, 70))),
    ("md-hero-rogue-f", lambda: P("night", 165, "f", 0, "bob", (20, 20, 22), None, (), "jacket", (30, 30, 34), (200, 200, 200), lips=(160, 50, 70))),
    ("md-hero-expert-f", lambda: P("overcast", 166, "f", 1, "long", (140, 90, 50), None, ("glasses",), "coat", (110, 90, 70), (200, 200, 200), lips=(170, 80, 90))),
]

SHEETS = [
    ("modern-enemies-street.png", STREET, 4),
    ("modern-enemies-threats.png", THREATS, 4),
    ("modern-persons.png", PERSONS, 4),
    ("modern-hero-avatars.png", HEROES, 3),
]


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    for file, entries, cols in SHEETS:
        if only and only not in file:
            continue
        cp.build_sheet(entries, cols).save(os.path.join(ROOT, "wwwroot", "sprites", file), optimize=True)
        print(file, len(entries))


if __name__ == "__main__":
    main()
