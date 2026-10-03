"""
Лист иконок навыков сеттинга «Киберпанк» (8x8, тот же порядок клеток, что у combat-icons-loot.png:
SkillIcons.cs адресует клетки по индексу). Рисуется кодом в манере HUD-кнопок: тёмная плашка,
неоновая рамка цвета категории, символ в центре. Клетка 32 px, лист увеличен x4 (1024x1024).

Запуск: python tools/sprites/build_cyber_skills.py
"""
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import *  # noqa: E402,F403
import build_cyber_items as it  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))


def plate(col):
    """Плашка-кнопка: тёмный фон со скошенными углами и неоновой рамкой."""
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    px = img.load()
    for y in range(32):
        for x in range(32):
            if x + y < 4 or (31 - x) + (31 - y) < 4:
                continue
            edge = x in (0, 31) or y in (0, 31) or x + y == 4 or (31 - x) + (31 - y) == 4
            base = mix((8, 10, 18), (24, 20, 40), y / 32)
            px[x, y] = (col if edge else base) + (255,)
    return img


def sym(draw):
    i = Icon(angle=0, cx=0, cy=0)
    draw(i)
    return i.finish()


def item(draw, scale=.8):
    """Иконка предмета, уменьшенная до символа."""
    img = draw().finish()
    s = int(32 * scale)
    return img.resize((s, s), Image.NEAREST)


def on(col, symbol):
    base = plate(col)
    off = (32 - symbol.width) // 2
    base.alpha_composite(symbol, (off, off))
    return base


# ---- символы ----

def s_slash(i):
    i.line(8, 24, 23, 7, CHROME, 2); i.line(9, 25, 24, 8, CYAN, neon=True)
    for k in range(6):
        a = math.radians(200 + k * 14)
        i.px(16 + 11 * math.cos(a), 16 + 11 * math.sin(a), MAGENTA, neon=True)


def s_flame(col=ORANGE, inner=YELLOW):
    def d(i):
        i.poly([(16, 4), (22, 13), (24, 20), (20, 27), (12, 27), (8, 20), (11, 12), (14, 15)], col)
        i.poly([(16, 13), (19, 20), (17, 25), (14, 25), (13, 20)], inner, neon=True)
    return d


def s_snow(i):
    for a in range(0, 180, 60):
        r = math.radians(a)
        i.line(16 - 11 * math.cos(r), 16 - 11 * math.sin(r), 16 + 11 * math.cos(r), 16 + 11 * math.sin(r), CYAN, neon=True)
    i.ell(13, 13, 19, 19, WHITE)


def s_bolt(i):
    i.ell(5, 5, 27, 27, None, outline=mix((0, 0, 0), CYAN, .6))
    i.poly([(18, 4), (10, 17), (15, 17), (13, 28), (22, 13), (17, 13)], CYAN, neon=True)


def s_waves(i):
    for r in (4, 8, 12):
        i.ell(6 - r // 2, 16 - r, 6 + r + r // 2, 16 + r, None, outline=VIOLET)
    i.clear(0, 0, 9, 31)
    i.rect(5, 13, 8, 19, METAL_L)


def s_burst(i):
    pts = []
    for k in range(16):
        r = 13 if k % 2 == 0 else 6
        a = math.radians(k * 22.5)
        pts.append((16 + r * math.cos(a), 16 + r * math.sin(a)))
    i.poly(pts, ORANGE)
    i.ell(12, 12, 20, 20, YELLOW, neon=True)


def s_code(i):
    i.line(11, 9, 5, 16, MAGENTA, 2, neon=True); i.line(5, 16, 11, 23, MAGENTA, 2, neon=True)
    i.line(21, 9, 27, 16, MAGENTA, 2, neon=True); i.line(27, 16, 21, 23, MAGENTA, 2, neon=True)
    i.line(18, 7, 14, 25, WHITE, 2)


def s_laser(i):
    i.ell(4, 13, 10, 19, WHITE, neon=True)
    for a in (-20, -8, 0, 8, 20):
        r = math.radians(a)
        i.line(8, 16, 8 + 22 * math.cos(r), 16 + 22 * math.sin(r), RED if a == 0 else mix((0, 0, 0), RED, .6), neon=a == 0)


def s_nanoheal(i):
    pts = [(16, 3), (27, 9), (27, 23), (16, 29), (5, 23), (5, 9)]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        i.line(a[0], a[1], b[0], b[1], GREEN)
    i.rect(14, 9, 18, 23, GREEN, neon=True); i.rect(9, 14, 23, 18, GREEN, neon=True)


def s_reboot(i):
    for k in range(0, 300, 12):
        a = math.radians(k - 60)
        i.px(16 + 10 * math.cos(a), 16 + 10 * math.sin(a), CYAN, neon=True)
        i.px(16 + 9 * math.cos(a), 16 + 9 * math.sin(a), CYAN, neon=True)
    i.poly([(21, 3), (27, 8), (20, 11)], CYAN, neon=True)


def s_defib(i):
    i.ell(6, 6, 17, 17, RED); i.ell(15, 6, 26, 17, RED)
    i.poly([(7, 14), (25, 14), (16, 26)], RED)
    for a, b in (((3, 18), (10, 18)), ((10, 18), (13, 11)), ((13, 11), (17, 24)), ((17, 24), (20, 16)), ((20, 16), (29, 16))):
        i.line(a[0], a[1], b[0], b[1], WHITE, neon=True)


def s_field(i):
    pts = [(16, 3), (27, 9), (27, 23), (16, 29), (5, 23), (5, 9)]
    i.poly(pts, mix((8, 10, 18), CYAN, .25))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        i.line(a[0], a[1], b[0], b[1], CYAN, neon=True)
    i.line(16, 3, 16, 29, mix((0, 0, 0), CYAN, .6)); i.line(5, 16, 27, 16, mix((0, 0, 0), CYAN, .6))


def s_fist(i):
    i.rect(8, 10, 24, 22, METAL_L)
    for x in (8, 12, 16, 20):
        i.rect(x, 8, x + 3, 12, CHROME)
    i.rect(8, 22, 20, 27, METAL)
    i.line(9, 16, 23, 16, ORANGE, neon=True)


def s_chevrons(i):
    for x in (5, 12, 19):
        i.line(x, 8, x + 7, 16, YELLOW, 2, neon=True); i.line(x + 7, 16, x, 24, YELLOW, 2, neon=True)


def s_camo(i):
    for y in range(6, 28, 2):
        for x in range(9 + (y // 2) % 2, 24, 2):
            if (x - 16) ** 2 / 64 + (y - 16) ** 2 / 121 <= 1:
                i.px(x, y, mix((8, 10, 18), CYAN, .7))
    i.ell(12, 6, 20, 13, None, outline=CYAN)


def s_armor(i):
    i.poly([(16, 4), (26, 8), (25, 19), (16, 28), (7, 19), (6, 8)], METAL_L)
    i.poly([(16, 8), (22, 10), (21, 18), (16, 23), (11, 18), (10, 10)], METAL)
    i.line(16, 9, 16, 22, YELLOW, neon=True)


def s_drop(col, n=1):
    def d(i):
        for k in range(n):
            x = 16 + (k - (n - 1) / 2) * 9
            i.poly([(x, 5 + k * 3), (x + 6, 17 + k * 3), (x, 23 + k * 3), (x - 6, 17 + k * 3)], col)
            i.ell(x - 6, 12 + k * 3, x + 6, 24 + k * 3, col)
            i.px(x - 2, 15 + k * 3, WHITE)
    return d


def s_toxin(i):
    s_drop(GREEN)(i)
    i.ell(13, 14, 19, 20, (10, 30, 10))
    i.px(16, 17, GREEN, neon=True)


def s_burn(i):
    s_flame(RED, ORANGE)(i)
    i.line(4, 28, 10, 22, ORANGE, neon=True); i.line(28, 28, 22, 22, ORANGE, neon=True)


def s_short(i):
    i.rect(8, 8, 24, 24, DARK)
    for p in (11, 16, 21):
        i.line(p, 4, p, 8, GOLD); i.line(p, 24, p, 28, GOLD)
    i.poly([(18, 6), (11, 17), (16, 17), (13, 27), (22, 14), (17, 14)], YELLOW, neon=True)


def s_power(i):
    for k in range(40, 320, 10):
        a = math.radians(k - 90)
        i.px(16 + 10 * math.cos(a), 16 + 10 * math.sin(a), VIOLET, neon=True)
    i.rect(15, 3, 17, 15, VIOLET, neon=True)
    i.px(23, 6, WHITE); i.px(25, 4, WHITE)


def s_warn(i):
    i.poly([(16, 4), (29, 27), (3, 27)], YELLOW)
    i.rect(15, 11, 17, 20, BLACK); i.rect(15, 22, 17, 24, BLACK)


def s_eye_static(i):
    i.ell(4, 9, 28, 23, WHITE)
    i.ell(11, 10, 21, 22, RED, neon=True)
    for k in range(10):
        y = 8 + k * 2
        i.line(2 + (k * 7) % 9, y, 10 + (k * 5) % 20, y, (60, 60, 70))


def s_jammer(i):
    i.line(16, 26, 16, 10, METAL_L, 2)
    for r in (5, 9):
        i.ell(16 - r, 10 - r, 16 + r, 10 + r, None, outline=mix((0, 0, 0), CYAN, .7))
    i.clear(0, 11, 31, 31)
    i.line(16, 26, 16, 10, METAL_L, 2)
    i.line(5, 27, 27, 5, RED, 2, neon=True)


def s_freeze(i):
    i.rect(6, 6, 26, 26, mix((8, 10, 18), CYAN, .45))
    i.rect(11, 10, 14, 22, WHITE, neon=True); i.rect(18, 10, 21, 22, WHITE, neon=True)


def s_pierce(i):
    i.rect(14, 4, 18, 28, METAL_L)
    i.line(14, 15, 18, 17, DARK)
    i.poly([(3, 15), (10, 13), (13, 16), (10, 19), (3, 17)], GOLD)
    i.line(13, 16, 29, 16, ORANGE, neon=True)


def s_down(i):
    i.rect(13, 4, 19, 18, RED, neon=True)
    i.poly([(6, 17), (26, 17), (16, 28)], RED, neon=True)


def s_hourglass(i):
    i.poly([(8, 5), (24, 5), (16, 16)], ORANGE)
    i.poly([(16, 16), (24, 27), (8, 27)], mix((8, 10, 18), ORANGE, .5))
    i.rect(7, 3, 25, 5, METAL_L); i.rect(7, 27, 25, 29, METAL_L)


def s_virus(i):
    for a in range(0, 360, 45):
        r = math.radians(a)
        i.line(16, 16, 16 + 12 * math.cos(r), 16 + 12 * math.sin(r), GREEN)
        i.ell(16 + 12 * math.cos(r) - 2, 16 + 12 * math.sin(r) - 2, 16 + 12 * math.cos(r) + 2, 16 + 12 * math.sin(r) + 2, GREEN, neon=True)
    i.ell(9, 9, 23, 23, (30, 90, 40))
    i.px(14, 14, GREEN); i.px(18, 17, GREEN)


def s_crosshair(i):
    i.ell(5, 5, 27, 27, None, outline=RED)
    i.ell(10, 10, 22, 22, None, outline=RED)
    i.line(16, 2, 16, 11, RED, neon=True); i.line(16, 21, 16, 30, RED, neon=True)
    i.line(2, 16, 11, 16, RED, neon=True); i.line(21, 16, 30, 16, RED, neon=True)


def s_net(i):
    for k in range(5, 28, 5):
        i.line(k, 4, k + 4, 28, CYAN); i.line(4, k, 28, k + 3, CYAN)
    i.ell(26, 2, 30, 6, YELLOW, neon=True)


def s_spiral(i):
    pts = []
    for k in range(60):
        a = k * .32
        r = 1 + k * .2
        pts.append((16 + r * math.cos(a), 16 + r * math.sin(a)))
    for a, b in zip(pts, pts[1:]):
        i.line(a[0], a[1], b[0], b[1], MAGENTA, neon=True)


def s_coin(i):
    i.ell(6, 6, 26, 26, GOLD)
    i.ell(9, 9, 23, 23, shade(GOLD, -.25))
    i.line(13, 11, 13, 21, WHITE); i.line(12, 13, 18, 13, WHITE); i.line(12, 17, 18, 17, WHITE)


# Порядок = индексы SkillIcons.All. Цвет рамки — по назначению: атака красная, поддержка жёлтая,
# лечение и очищение зелёные, защита голубая, контроль фиолетовый, вещи — тускло-стальные.
ATK, SUP, HEAL, DEF, CTRL, ITEM = RED, YELLOW, GREEN, CYAN, VIOLET, (90, 100, 124)
CELLS = [
    (ATK, sym(s_slash)), (ATK, sym(s_flame())), (ATK, sym(s_snow)), (ATK, sym(s_bolt)),
    (ATK, sym(s_waves)), (ATK, sym(s_burst)), (ATK, sym(s_code)), (ATK, sym(s_laser)),
    (HEAL, sym(s_nanoheal)), (HEAL, sym(s_reboot)), (HEAL, sym(s_defib)), (DEF, sym(s_field)),
    (SUP, sym(s_fist)), (SUP, sym(s_chevrons)), (SUP, sym(s_camo)), (DEF, sym(s_armor)),
    (CTRL, sym(s_toxin)), (CTRL, sym(s_drop(RED, 2))), (CTRL, sym(s_burn)), (CTRL, sym(s_short)),
    (CTRL, sym(s_power)), (CTRL, sym(s_warn)), (CTRL, sym(s_eye_static)), (CTRL, sym(s_jammer)),
    (CTRL, sym(s_freeze)), (CTRL, sym(s_pierce)), (CTRL, sym(s_down)), (CTRL, sym(s_hourglass)),
    (CTRL, sym(s_virus)), (CTRL, sym(s_crosshair)), (CTRL, sym(s_net)), (CTRL, sym(s_spiral)),
    # 32–39: чипы программ вместо книг и свитков
    (ITEM, item(lambda: it.chip(ORANGE))), (ITEM, item(lambda: it.chip(CYAN))), (ITEM, item(lambda: it.chip(GREEN))),
    (ITEM, item(lambda: it.chip(MAGENTA))), (ITEM, item(lambda: it.chip(WHITE))), (ITEM, item(lambda: it.chip((120, 120, 130)))),
    (ITEM, item(lambda: it.shard(RED))), (ITEM, item(lambda: it.shard((60, 120, 255)))),
    # 40–44: кристаллы памяти вместо самоцветов
    (ITEM, item(lambda: it.crystal(RED))), (ITEM, item(lambda: it.crystal((60, 120, 255)))), (ITEM, item(lambda: it.crystal(GREEN))),
    (ITEM, item(lambda: it.crystal(VIOLET))), (ITEM, item(lambda: it.crystal(WHITE))),
    # 45–55: импланты и детали вместо трофеев и материалов
    (ITEM, item(lambda: it.mantis())), (ITEM, item(lambda: it.helmet(DARK, RED, "full"))), (ITEM, item(lambda: it.hexplate())),
    (ITEM, item(lambda: it.biojar())), (ITEM, item(lambda: it.chem())), (ITEM, item(lambda: it.grenade())),
    (ITEM, item(lambda: it.scrap())), (ITEM, item(lambda: it.wires())), (ITEM, item(lambda: it.polymer())),
    (ITEM, item(lambda: it.cable())), (ITEM, item(lambda: it.circuit())),
    # 56–63: стимы, карта, ключ, эдди, имплант
    (HEAL, item(lambda: it.injector(RED))), (DEF, item(lambda: it.injector(CYAN))), (SUP, item(lambda: it.injector(ORANGE))),
    (CTRL, item(lambda: it.injector((40, 40, 40)))), (ITEM, item(lambda: it.holomap())), (ITEM, item(lambda: it.keycard())),
    (ITEM, sym(s_coin)), (ITEM, item(lambda: it.chip_implant())),
]


# ---- Меню книги героя (3x3, порядок как у hero-book-menu.png): инвентарь, группа, рядом, задания, арка,
#      важные персонажи, города, мир, запас ----

def m_party(i):
    for x, c in ((6, METAL_L), (18, METAL_L), (12, CHROME)):
        i.ell(x, 6, x + 8, 14, c)
        i.poly([(x - 1, 27), (x + 1, 17), (x + 7, 17), (x + 9, 27)], c)
    i.line(4, 28, 28, 28, CYAN, neon=True)


def m_talk(i):
    i.poly([(4, 6), (28, 6), (28, 20), (14, 20), (8, 26), (9, 20), (4, 20)], (20, 26, 40))
    for a, b in (((4, 6), (28, 6)), ((28, 6), (28, 20)), ((28, 20), (14, 20)), ((14, 20), (8, 26)), ((8, 26), (9, 20)), ((9, 20), (4, 20)), ((4, 20), (4, 6))):
        i.line(a[0], a[1], b[0], b[1], CYAN, neon=True)
    for x in (10, 16, 22):
        i.rect(x - 1, 12, x, 13, CYAN, neon=True)


def m_contract(i):
    i.rect(7, 4, 25, 28, (24, 28, 40))
    i.rect(7, 4, 25, 7, YELLOW, neon=True)
    for y in (11, 15, 19):
        i.line(10, y, 22, y, METAL_L)
    i.rect(17, 22, 22, 25, RED, neon=True)


def m_card(i):
    i.rect(3, 8, 29, 25, (24, 28, 40))
    i.line(3, 8, 29, 8, MAGENTA, neon=True)
    i.ell(6, 12, 13, 19, METAL_L)
    i.rect(5, 19, 14, 22, METAL_L)
    for y in (13, 17, 21):
        i.line(16, y, 26, y, CYAN)


def m_city(i):
    for x0, x1, top in ((3, 9, 12), (10, 16, 5), (17, 22, 9), (23, 29, 14)):
        i.rect(x0, top, x1, 28, (26, 30, 44))
        for y in range(top + 2, 27, 3):
            for x in range(x0 + 1, x1, 2):
                if (x + y) % 3:
                    i.px(x, y, YELLOW if (x * y) % 5 else CYAN)
    i.line(13, 5, 13, 1, STEEL); i.px(13, 1, RED, neon=True)


def m_globe(i):
    i.ell(4, 4, 28, 28, (14, 22, 34))
    i.ell(4, 4, 28, 28, None, outline=CYAN)
    i.ell(11, 4, 21, 28, None, outline=mix((0, 0, 0), CYAN, .6))
    i.line(4, 16, 28, 16, mix((0, 0, 0), CYAN, .6))
    for x, y in ((9, 10), (21, 9), (17, 21), (10, 22)):
        i.rect(x - 1, y - 1, x + 1, y + 1, MAGENTA, neon=True)
    i.line(9, 10, 21, 9, MAGENTA); i.line(21, 9, 17, 21, MAGENTA); i.line(17, 21, 10, 22, MAGENTA)


MENU = [
    (YELLOW, item(lambda: it.bag(), .9)), (CYAN, sym(m_party)), (CYAN, sym(m_talk)),
    (YELLOW, sym(m_contract)), (RED, sym(s_crosshair)), (MAGENTA, sym(m_card)),
    (YELLOW, sym(m_city)), (CYAN, sym(m_globe)), (ITEM, sym(lambda i: None)),
]


def main():
    menu = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    for n, (col, symbol) in enumerate(MENU):
        menu.alpha_composite(on(col, symbol), ((n % 3) * 32, (n // 3) * 32))
    upscale(menu, 4).save(os.path.join(ROOT, "wwwroot", "sprites", "cyber-book-menu.png"), optimize=True)
    assert len(CELLS) == 64, len(CELLS)
    sheet = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    for n, (col, symbol) in enumerate(CELLS):
        sheet.alpha_composite(on(col, symbol), ((n % 8) * 32, (n // 8) * 32))
    sheet = upscale(sheet, 4)
    sheet.save(os.path.join(ROOT, "wwwroot", "sprites", "cyber-skill-icons.png"), optimize=True)
    print("cyber-skill-icons.png 64")


if __name__ == "__main__":
    main()
