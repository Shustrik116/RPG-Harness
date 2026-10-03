"""
Лист иконок навыков (8x8) и меню «Личного дела» (3x3) сеттинга «Современность».
Порядок клеток тот же, что у фэнтезийного листа (SkillIcons.cs адресует клетки по индексу).
Символы общие с киберпанком, но приглушены: матовые плашки, без неона, цвет рамки — по назначению.

Запуск: python tools/sprites/build_modern_skills.py
"""
import colorsys
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import *  # noqa: E402,F403
import build_cyber_skills as cs  # noqa: E402
import build_modern_items as mi  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

ATK, SUP, HEAL, DEF, CTRL, ITEM = (176, 72, 52), (204, 150, 62), (104, 150, 90), (92, 122, 160), (128, 104, 150), (112, 112, 112)


def mute(img, sat=.5, val=.95):
    """Приглушает неон: меньше насыщенности, чуть темнее. Альфа сохраняется."""
    img = img.copy()
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            r2, g2, b2 = colorsys.hsv_to_rgb(h, s * sat, v * val)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return img


def plate(col):
    """Матовая плашка: тёмно-серый фон, тонкая рамка, скруглённые углы по одному пикселю."""
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    px = img.load()
    for y in range(32):
        for x in range(32):
            if (x, y) in ((0, 0), (31, 0), (0, 31), (31, 31)):
                continue
            edge = x in (0, 31) or y in (0, 31)
            base = mix((30, 31, 34), (22, 23, 26), y / 32)
            px[x, y] = (shade(col, -.1) if edge else base) + (255,)
    for x in range(2, 30):
        px[x, 1] = shade(col, -.45) + (255,)
    return img


def on(col, symbol):
    base = plate(col)
    off = (32 - symbol.width) // 2
    base.alpha_composite(symbol, (off, off))
    return base


def sym(draw, muted=True):
    img = cs.sym(draw)
    return mute(img) if muted else img


def item(draw, scale=.8):
    return cs.item(draw, scale)


def s_dollar(i):
    i.ell(6, 6, 26, 26, (196, 160, 70))
    i.ell(9, 9, 23, 23, (150, 120, 50))
    for a_, b_ in (((20, 11), (13, 11)), ((13, 11), (12, 15)), ((12, 15), (20, 16)), ((20, 16), (20, 20)), ((20, 20), (12, 20))):
        i.line(a_[0], a_[1], b_[0], b_[1], (240, 230, 200))
    i.line(16, 9, 16, 23, (240, 230, 200))


def s_bullet(i):
    i.poly([(6, 14), (20, 14), (26, 16), (20, 18), (6, 18)], (200, 160, 70))
    i.rect(4, 14, 7, 18, (170, 130, 60))
    i.line(8, 15, 18, 15, (240, 210, 140))
    for x in (10, 13):
        i.line(x - 8, 24, x, 21, (150, 150, 150))


def s_newspaper(i):
    i.rect(5, 5, 27, 27, (226, 222, 208))
    i.rect(7, 7, 25, 11, (40, 40, 40))
    i.rect(7, 13, 15, 20, (120, 120, 120))
    for y in (14, 17, 20, 23):
        i.line(17, y, 25, y, (90, 90, 90))
    i.line(7, 23, 15, 23, (90, 90, 90))


def s_people(i):
    for x, c in ((6, (120, 124, 130)), (18, (120, 124, 130)), (12, (170, 172, 176))):
        i.ell(x, 6, x + 8, 14, c)
        i.poly([(x - 1, 27), (x + 1, 17), (x + 7, 17), (x + 9, 27)], c)


def s_talk(i):
    i.poly([(4, 6), (28, 6), (28, 20), (14, 20), (8, 26), (9, 20), (4, 20)], (210, 206, 196))
    for x in (10, 16, 22):
        i.rect(x - 1, 12, x, 13, (60, 60, 60))


def s_card(i):
    i.rect(3, 8, 29, 25, (210, 206, 196))
    i.ell(6, 12, 13, 19, (110, 110, 116))
    i.rect(5, 19, 14, 22, (110, 110, 116))
    for y in (13, 17, 21):
        i.line(16, y, 26, y, (90, 90, 90))


def s_city(i):
    for x0, x1, top in ((3, 9, 12), (10, 16, 5), (17, 22, 9), (23, 29, 14)):
        i.rect(x0, top, x1, 28, (60, 62, 68))
        for y in range(top + 2, 27, 3):
            for x in range(x0 + 1, x1, 2):
                if (x + y) % 3:
                    i.px(x, y, (230, 196, 110))


CELLS = [
    (ATK, item(lambda: mi.knife(), .9)), (ATK, sym(cs.s_flame((220, 110, 30), (250, 200, 60)), False)), (ATK, sym(cs.s_snow)), (ATK, sym(cs.s_bolt)),
    (ATK, sym(cs.s_waves)), (ATK, sym(cs.s_burst, False)), (ATK, sym(cs.s_code)), (ATK, sym(s_bullet, False)),
    (HEAL, item(lambda: mi.bandage(), .85)), (HEAL, item(lambda: mi.pills(), .8)), (HEAL, sym(cs.s_defib)), (DEF, item(lambda: mi.riot_shield(), .85)),
    (SUP, sym(cs.s_fist)), (SUP, sym(cs.s_chevrons)), (SUP, sym(cs.s_camo)), (DEF, sym(cs.s_armor)),
    (CTRL, sym(cs.s_toxin)), (CTRL, sym(cs.s_drop((176, 40, 40), 2), False)), (CTRL, sym(cs.s_burn, False)), (CTRL, sym(cs.s_short)),
    (CTRL, item(lambda: mi.syringe((140, 160, 220)), .85)), (CTRL, sym(cs.s_warn)), (CTRL, sym(cs.s_eye_static)), (CTRL, sym(cs.s_jammer)),
    (CTRL, item(lambda: mi.handcuffs(), .85)), (CTRL, sym(cs.s_pierce, False)), (CTRL, sym(cs.s_down)), (CTRL, sym(cs.s_hourglass, False)),
    (CTRL, item(lambda: mi.docs(), .8)), (CTRL, sym(cs.s_crosshair)), (CTRL, item(lambda: mi.rope(), .85)), (CTRL, sym(cs.s_spiral)),
    # 32–39: записи, документы и носители вместо книг и свитков
    (ITEM, item(lambda: mi.notebook())), (ITEM, item(lambda: mi.manual())), (ITEM, item(lambda: mi.paper_map())),
    (ITEM, item(lambda: mi.case_file())), (ITEM, item(lambda: mi.docs())), (ITEM, item(lambda: mi.flash_drive())),
    (ITEM, item(lambda: mi.lottery())), (ITEM, item(lambda: mi.photo())),
    # 40–44: ценности вместо самоцветов
    (ITEM, item(lambda: mi.jewelry_pile())), (ITEM, item(lambda: mi.diamond())), (ITEM, item(lambda: mi.painting())),
    (ITEM, item(lambda: mi.cigar_box())), (ITEM, item(lambda: mi.watch(mi.GOLD_S))),
    # 45–55: трофеи и материалы
    (ITEM, item(lambda: mi.fang())), (ITEM, item(lambda: mi.army_helmet())), (ITEM, item(lambda: mi.plate_carrier())),
    (ITEM, item(lambda: mi.meat())), (ITEM, item(lambda: mi.chem())), (ITEM, item(lambda: mi.frag())),
    (ITEM, item(lambda: mi.parts())), (ITEM, item(lambda: mi.hide())), (ITEM, item(lambda: mi.car_battery())),
    (ITEM, item(lambda: mi.rope())), (ITEM, item(lambda: mi.electronics())),
    # 56–63: аптечка, энергетик, адреналин, яд, карта, отмычки, деньги, жетон
    (HEAL, item(lambda: mi.bandage())), (DEF, item(lambda: mi.energy_can())), (SUP, item(lambda: mi.syringe())),
    (CTRL, item(lambda: mi.pills((240, 240, 240), (60, 60, 60)))), (ITEM, item(lambda: mi.paper_map())), (ITEM, item(lambda: mi.lockpicks())),
    (ITEM, sym(s_dollar, False)), (ITEM, item(lambda: mi.dogtag())),
]

MENU = [
    (SUP, item(lambda: mi.sport_bag(), .9)), (DEF, sym(s_people, False)), (DEF, sym(s_talk, False)),
    (SUP, item(lambda: mi.case_file(), .9)), (ATK, sym(cs.s_crosshair)), (CTRL, sym(s_card, False)),
    (SUP, sym(s_city, False)), (DEF, sym(s_newspaper, False)), (ITEM, sym(lambda i: None, False)),
]


def main():
    assert len(CELLS) == 64, len(CELLS)
    sheet = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    for n, (col, symbol) in enumerate(CELLS):
        sheet.alpha_composite(on(col, symbol), ((n % 8) * 32, (n // 8) * 32))
    upscale(sheet, 4).save(os.path.join(ROOT, "wwwroot", "sprites", "modern-skill-icons.png"), optimize=True)
    menu = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    for n, (col, symbol) in enumerate(MENU):
        menu.alpha_composite(on(col, symbol), ((n % 3) * 32, (n // 3) * 32))
    upscale(menu, 4).save(os.path.join(ROOT, "wwwroot", "sprites", "modern-book-menu.png"), optimize=True)
    print("modern-skill-icons.png 64, modern-book-menu.png 9")


if __name__ == "__main__":
    main()
