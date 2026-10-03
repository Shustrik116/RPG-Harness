"""
Каталог предметов сеттинга «Киберпанк»: иконки рисуются кодом (pixelkit), без внешних тайлов.

Результат:
  wwwroot/sprites/cyber-items.png  — атлас 32x32-тайлов, COLS колонок
  wwwroot/sprites/cyber-items.json — каталог: id, название, категория, размер, слот, а также урон/броня,
                                     базовая цена и категория торговли (C# берёт их отсюда, а не из таблиц кода)
  wwwroot/css/pixel-cyber.css      — классы .pix-cy_<id> со своим атласом

id и порядок записей — только дописывать: id сохраняются в кампаниях, позиция в атласе = порядок в CATALOG.

Запуск: python tools/sprites/build_cyber_items.py [--preview <png>]
"""
import json
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import *  # noqa: E402,F403

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TILE = 32
COLS = 16


def I(angle=0):
    return Icon(angle=angle, cx=0, cy=0) if angle == 0 else Icon(angle=angle)


# ======================= Оружие =======================

def pistol(body=METAL, accent=CYAN, kind="std"):
    i = I()
    if kind == "revolver":
        i.rect(17, 11, 28, 13, STEEL)
        i.rect(11, 9, 18, 15, shade(body, .25))
        i.line(12, 12, 17, 12, shade(body, -.4))
        i.rect(8, 10, 11, 14, body)
        i.px(8, 8, DARK); i.px(9, 9, DARK)
    else:
        top = 9 if kind == "heavy" else 10
        i.rect(6, top, 26 if kind == "heavy" else 25, 14, body)
        i.rect(26, 11, 27, 13, DARK)
        i.line(9, 12, 22, 12, accent, neon=True)
    i.rect(9, 15, 21, 16, shade(body, -.3))
    i.poly([(10, 16), (15, 16), (14, 25), (8, 25)], GRIP)
    i.rect(16, 17, 18, 18, DARK)
    if kind == "smart":
        i.rect(12, 7, 18, 9, DARK); i.line(13, 8, 17, 8, accent, neon=True)
    if kind == "laser":
        i.rect(25, 10, 28, 14, accent, neon=True); i.glow(29, 12, accent, 3)
    if kind != "revolver":
        i.px(11, 20, accent, neon=True)
    return i


def smg(body=METAL, accent=CYAN, compact=False):
    i = I()
    i.rect(3 if not compact else 6, 11, 7, 13, DARK)
    i.rect(6, 10, 23 if not compact else 21, 16, body)
    i.rect(23 if not compact else 21, 12, 28, 14, STEEL if not compact else DARK)
    i.rect(13, 16, 16, 25, DARK)
    i.poly([(8, 16), (11, 16), (10, 23), (7, 23)], GRIP)
    i.rect(10, 8, 15, 9, DARK)
    i.line(8, 12, 20, 12, accent, neon=True)
    i.px(18, 14, accent, neon=True)
    return i


def rifle(body=METAL, accent=CYAN, kind="assault"):
    i = I(-45)
    i.poly([(-20, -2), (-12, -3), (-12, 3), (-20, 4)], GRIP)
    i.rect(-12, -3, 5, 2, body)
    if kind == "shotgun":
        i.rect(5, -3, 20, 0, STEEL)
        i.rect(5, 1, 16, 2, DARK)
        i.rect(7, 0, 13, 3, shade(body, -.35))
    elif kind == "sniper":
        i.rect(5, -2, 21, -1, STEEL)
        i.rect(-8, -7, 5, -4, DARK)
        i.line(-7, -6, 4, -6, accent, neon=True)
        i.line(2, 2, 6, 7, DARK); i.line(4, 2, 9, 6, DARK)
    elif kind == "rail":
        i.rect(5, -3, 20, 1, shade(body, -.2))
        for u in (7, 11, 15, 19):
            i.line(u, -4, u, 2, accent, neon=True)
        i.glow(20, -1, accent, 3)
    else:
        i.rect(5, -2, 13, 1, shade(body, -.3))
        i.rect(13, -1, 20, 0, STEEL)
        i.rect(-7, -6, 1, -4, DARK)
    if kind != "shotgun":
        i.poly([(-3, 2), (1, 2), (2, 8), (-2, 8)], DARK)
    i.poly([(-9, 2), (-6, 2), (-7, 6), (-10, 6)], GRIP)
    i.line(-10, -1, 3, -1, accent, neon=True)
    return i


def launcher(body=OLIVE, accent=YELLOW):
    i = I(-45)
    i.rect(-18, -4, 18, 3, body)
    i.rect(16, -5, 19, 4, DARK)
    i.rect(-19, -3, -17, 2, DARK)
    i.poly([(-6, 3), (-2, 3), (-3, 8), (-7, 8)], GRIP)
    i.rect(-4, -8, 2, -5, DARK)
    for u in (-12, 6):
        i.line(u, -4, u, 3, accent)
    i.line(-14, -1, 12, -1, shade(body, .3))
    return i


def blade(steel=STEEL, edge=None, guard=DARK, handle=GRIP, length=20, width=1, wrap=None, lo=-15):
    i = I(-45)
    i.poly([(-3, -width), (length - 2, -width), (length, 0), (length - 2, width), (-3, width)], steel)
    if edge:
        i.line(-2, width, length - 2, width, edge, neon=True)
        i.glow(length, 0, edge, 2)
    i.rect(-5, -3, -3, 3, guard)
    i.rect(lo, -1, -5, 1, handle)
    if wrap:
        for u in range(lo + 1, -5, 2):
            i.px(u, 0, wrap)
    i.rect(lo - 1, -1, lo, 1, guard)
    return i


def knife(steel=STEEL, edge=None, handle=GRIP):
    i = I(-45)
    i.poly([(-2, -1), (8, -1), (11, 1), (-2, 2)], steel)
    if edge:
        i.line(-1, 2, 9, 1, edge, neon=True)
    i.rect(-4, -2, -2, 3, DARK)
    i.rect(-11, -1, -4, 2, handle)
    return i


def machete(steel=STEEL, edge=ORANGE):
    i = I(-45)
    i.poly([(-3, -2), (12, -3), (16, -1), (14, 2), (-3, 2)], steel)
    i.line(-2, 2, 13, 2, edge, neon=True)
    i.rect(-12, -1, -3, 1, GRIP)
    i.px(-8, 0, METAL_L)
    return i


def baton(body=DARK, tip=CYAN):
    i = I(-45)
    i.rect(-16, -1, 13, 1, body)
    i.rect(-16, -2, -8, 2, GRIP)
    i.rect(13, -2, 17, 2, tip, neon=True)
    i.glow(18, 0, tip, 3)
    i.line(-6, -1, 12, -1, shade(body, .4))
    return i


def pipe():
    i = I(-45)
    i.rect(-17, -1, 17, 1, RUST)
    i.rect(-2, -2, 1, 2, METAL)
    i.rect(14, -2, 17, 2, METAL)
    i.line(-16, -1, 12, -1, shade(RUST, .35))
    return i


def bat(tape=MAGENTA):
    i = I(-45)
    i.poly([(-16, -1), (2, -1), (16, -3), (17, 0), (16, 3), (2, 1), (-16, 1)], (120, 82, 52))
    i.rect(-16, -1, -9, 1, tape, neon=True)
    for u in (8, 12):
        i.px(u, -3, STEEL); i.px(u, 3, STEEL)
    return i


def hammer(body=METAL, accent=RED):
    i = I(-45)
    i.rect(-18, -1, 8, 1, DARK)
    i.rect(-18, -2, -11, 2, GRIP)
    i.rect(7, -7, 16, 7, body)
    i.rect(16, -5, 18, 5, STEEL)
    i.line(9, -5, 9, 5, accent, neon=True)
    i.line(12, -5, 12, 5, accent, neon=True)
    i.glow(18, 0, accent, 2)
    return i


def knuckles(col=CHROME, accent=CYAN):
    i = I()
    i.rect(6, 13, 26, 20, col)
    for x in (7, 12, 17, 22):
        i.ell(x, 9, x + 4, 14, col)
        i.ell(x + 1, 10, x + 3, 13, (0, 0, 0))
    i.clear(8, 11, 9, 12); i.clear(13, 11, 14, 12); i.clear(18, 11, 19, 12); i.clear(23, 11, 24, 12)
    i.line(8, 17, 24, 17, accent, neon=True)
    i.rect(9, 20, 23, 24, shade(col, -.3))
    return i


def mantis(blade_col=CHROME, accent=RED):
    i = I()
    i.rect(4, 18, 16, 26, METAL)
    i.rect(4, 18, 6, 26, DARK)
    i.line(8, 22, 15, 22, accent, neon=True)
    i.poly([(14, 19), (20, 10), (27, 3), (25, 9), (19, 17), (16, 22)], blade_col)
    i.line(16, 19, 26, 5, accent, neon=True)
    i.glow(27, 3, accent, 2)
    return i


# ======================= Броня и одежда =======================

def torso(i, col, trim=None, top=6, bottom=27, sleeves=True):
    i.poly([(7, top + 2), (12, top), (20, top), (25, top + 2), (24, bottom), (8, bottom)], col)
    if sleeves:
        i.poly([(4, top + 4), (8, top + 2), (8, top + 12), (5, top + 12)], shade(col, -.15))
        i.poly([(28, top + 4), (24, top + 2), (24, top + 12), (27, top + 12)], shade(col, -.15))
    if trim:
        i.line(8, bottom, 24, bottom, trim, neon=True)


def jacket(col=LEATHER, trim=CYAN, collar=True):
    i = I()
    torso(i, col, None)
    i.rect(15, 8, 17, 27, shade(col, -.35))
    i.line(16, 9, 16, 26, trim, neon=True)
    if collar:
        i.poly([(12, 6), (15, 6), (15, 13)], shade(col, .25))
        i.poly([(20, 6), (17, 6), (17, 13)], shade(col, .25))
    i.rect(9, 18, 13, 21, shade(col, -.2)); i.rect(19, 18, 23, 21, shade(col, -.2))
    return i


def vest(col=(64, 70, 58), plate=METAL, trim=YELLOW, heavy=False):
    i = I()
    torso(i, col, None, sleeves=heavy)
    i.rect(10, 10, 22, 16, plate)
    i.rect(10, 18, 22, 24, plate)
    i.line(10, 17, 22, 17, DARK)
    i.line(11, 13, 21, 13, trim, neon=True)
    if heavy:
        i.rect(4, 9, 9, 13, plate); i.rect(23, 9, 28, 13, plate)
        i.rect(14, 4, 18, 6, DARK)
    return i


def exo(col=(44, 46, 54), core=CYAN, gold=False):
    i = I()
    plate = GOLD if gold else METAL_L
    torso(i, col, None)
    i.rect(3, 7, 10, 14, plate); i.rect(22, 7, 29, 14, plate)
    i.rect(10, 9, 22, 17, shade(plate, -.25))
    i.ell(13, 10, 19, 16, core, neon=True)
    i.glow(16, 13, core, 4)
    i.rect(10, 19, 22, 26, shade(plate, -.4))
    for y in (21, 24):
        i.line(11, y, 21, y, DARK)
    return i


def hoodie(col=(48, 44, 64), trim=MAGENTA):
    i = I()
    torso(i, col, None)
    i.poly([(10, 2), (22, 2), (24, 10), (8, 10)], shade(col, .15))
    i.ell(12, 4, 20, 11, (12, 12, 18))
    i.rect(12, 18, 20, 23, shade(col, -.25))
    i.line(13, 12, 13, 17, trim, neon=True); i.line(19, 12, 19, 17, trim, neon=True)
    return i


def suit(col=NAVY, tie=RED):
    i = I()
    torso(i, col, None)
    i.poly([(13, 6), (19, 6), (16, 16)], WHITE)
    i.poly([(15, 8), (17, 8), (17, 18), (16, 20), (15, 18)], tie)
    i.poly([(12, 6), (14, 6), (16, 16), (13, 12)], shade(col, .2))
    i.poly([(20, 6), (18, 6), (16, 16), (19, 12)], shade(col, .2))
    i.px(16, 22, GOLD); i.px(16, 25, GOLD)
    return i


def coat(col=(36, 34, 46), trim=CYAN, camo=False):
    i = I()
    i.poly([(8, 5), (12, 3), (20, 3), (24, 5), (27, 30), (5, 30)], col)
    i.poly([(11, 3), (14, 3), (14, 12), (10, 8)], shade(col, .25))
    i.poly([(21, 3), (18, 3), (18, 12), (22, 8)], shade(col, .25))
    i.rect(15, 6, 17, 30, shade(col, -.4))
    i.line(6, 30, 26, 30, trim, neon=True)
    i.line(16, 8, 16, 29, trim, neon=True)
    if camo:
        for y in range(5, 30, 3):
            for x in range(7 + (y % 2), 26, 4):
                i.px(x, y, mix(col, trim, .5))
    return i


def helmet(col=METAL, visor=CYAN, kind="tac"):
    i = I()
    if kind == "shades":
        i.rect(4, 13, 28, 14, DARK)
        i.rect(6, 14, 14, 19, visor, neon=True)
        i.rect(18, 14, 26, 19, visor, neon=True)
        i.line(6, 15, 9, 15, WHITE); i.line(18, 15, 21, 15, WHITE)
        i.glow(10, 17, visor, 3); i.glow(22, 17, visor, 3)
        return i
    if kind == "mask":
        i.ell(8, 8, 24, 26, col)
        i.ell(4, 15, 11, 23, DARK); i.ell(21, 15, 28, 23, DARK)
        i.ell(5, 16, 10, 22, shade(col, .2)); i.ell(22, 16, 27, 22, shade(col, .2))
        i.rect(10, 11, 22, 14, visor, neon=True)
        i.line(13, 20, 19, 20, DARK); i.line(13, 22, 19, 22, DARK)
        return i
    if kind == "cap":
        i.ell(8, 8, 24, 22, col)
        i.clear(6, 16, 26, 26)
        i.rect(8, 15, 24, 17, shade(col, -.2))
        i.poly([(18, 15), (29, 16), (28, 19), (18, 18)], shade(col, -.35))
        i.px(15, 10, visor, neon=True); i.px(16, 10, visor, neon=True)
        return i
    i.ell(7, 5, 25, 25, col)
    if kind == "full":
        i.rect(7, 15, 25, 27, col)
        i.rect(14, 12, 18, 24, visor, neon=True)
        i.rect(10, 12, 22, 15, visor, neon=True)
        i.glow(16, 14, visor, 3)
    else:
        i.clear(6, 21, 26, 28)
        i.rect(8, 13, 24, 17, visor, neon=True)
        i.glow(16, 15, visor, 3)
        i.rect(6, 17, 9, 22, shade(col, -.3)); i.rect(23, 17, 26, 22, shade(col, -.3))
        i.px(23, 9, RED, neon=True)
    return i


def gloves(col=GRIP, accent=CYAN, power=False):
    i = I()
    plate = METAL_L if power else shade(col, .3)
    i.rect(9, 12, 23, 24, col)
    for x in (9, 13, 17, 21):
        i.rect(x, 5 if x in (13, 17) else 7, x + 2, 12, col)
    i.poly([(23, 14), (27, 11), (28, 13), (24, 19)], col)
    i.rect(9, 11, 23, 13, plate)
    i.rect(9, 24, 23, 28, shade(col, -.2) if not power else METAL)
    i.px(11, 12, accent, neon=True); i.px(15, 12, accent, neon=True); i.px(19, 12, accent, neon=True)
    if power:
        i.rect(12, 16, 20, 21, plate)
        i.line(13, 18, 19, 18, accent, neon=True)
    return i


def boots(col=GRIP, accent=CYAN, heavy=False):
    i = I()
    i.rect(10, 4, 20, 22, col)
    i.poly([(10, 18), (21, 18), (28, 22), (29, 26), (10, 26)], col)
    i.rect(9, 26, 29, 28, DARK if not heavy else METAL)
    i.line(11, 9, 19, 9, accent, neon=True)
    if heavy:
        i.rect(10, 12, 20, 16, METAL_L)
        i.rect(22, 21, 28, 25, METAL_L)
    else:
        for y in (12, 15, 18):
            i.px(15, y, STEEL)
    return i


def belt(col=(60, 56, 48), accent=YELLOW):
    i = I()
    i.rect(2, 13, 30, 18, col)
    for x in (4, 10, 21):
        i.rect(x, 11, x + 5, 21, shade(col, -.25))
        i.line(x, 12, x + 5, 12, shade(col, .2))
    i.rect(15, 12, 19, 19, METAL_L)
    i.rect(16, 14, 18, 17, accent, neon=True)
    return i


# ======================= Щиты =======================

def ballistic(col=(52, 56, 64), window=CYAN, stripes=True):
    i = I()
    i.rect(7, 3, 25, 29, col)
    i.rect(9, 7, 23, 11, window, neon=True)
    i.line(10, 8, 15, 8, WHITE)
    if stripes:
        for x in range(8, 25, 4):
            i.poly([(x, 24), (x + 2, 24), (x, 28)], YELLOW)
    i.line(16, 13, 16, 22, shade(col, .25))
    return i


def projector(col=METAL, field=CYAN):
    i = I()
    pts = [(16, 2), (28, 9), (28, 23), (16, 30), (4, 23), (4, 9)]
    i.poly(pts, mix((10, 12, 20), field, .25))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        i.line(a[0], a[1], b[0], b[1], field, neon=True)
    i.line(16, 2, 16, 30, mix(field, (10, 12, 20), .5)); i.line(4, 16, 28, 16, mix(field, (10, 12, 20), .5))
    i.rect(10, 13, 22, 19, col)
    i.rect(14, 14, 18, 18, field, neon=True)
    return i


# ======================= Импланты =======================

def chip_implant(col=DARK, core=CYAN, pins=GOLD):
    i = I()
    for p in range(11, 22, 3):
        i.line(p, 6, p, 10, pins); i.line(p, 22, p, 26, pins)
        i.line(6, p, 10, p, pins); i.line(22, p, 26, p, pins)
    i.rect(9, 9, 23, 23, col)
    i.rect(13, 13, 19, 19, core, neon=True)
    i.glow(16, 16, core, 2)
    return i


def optic(col=METAL_L, iris=RED):
    i = I()
    i.ell(5, 9, 27, 23, col)
    i.ell(10, 10, 22, 22, DARK)
    i.ell(12, 12, 20, 20, iris, neon=True)
    i.ell(14, 14, 18, 18, (10, 10, 10))
    i.px(13, 13, WHITE)
    i.line(3, 16, 6, 16, DARK); i.line(26, 16, 29, 16, DARK)
    return i


def neural_link(col=METAL, accent=MAGENTA):
    i = I()
    i.ell(9, 4, 23, 18, col)
    i.ell(12, 7, 20, 15, accent, neon=True)
    for x in (11, 16, 21):
        i.line(16, 16, x, 29, DARK, w=2)
        i.px(x, 29, accent, neon=True)
    return i


def spine(col=CHROME, accent=CYAN):
    i = I()
    for k, y in enumerate((3, 10, 17, 24)):
        i.rect(11, y, 21, y + 4, col)
        i.rect(8, y + 1, 11, y + 3, shade(col, -.3)); i.rect(21, y + 1, 24, y + 3, shade(col, -.3))
        i.px(16, y + 2, accent, neon=True)
        if k < 3:
            i.rect(14, y + 5, 18, y + 6, DARK)
    return i


def hexplate(col=METAL_L, accent=CYAN):
    i = I()
    pts = [(16, 4), (27, 10), (27, 22), (16, 28), (5, 22), (5, 10)]
    i.poly(pts, col)
    inner = [(16, 9), (22, 12), (22, 20), (16, 23), (10, 20), (10, 12)]
    i.poly(inner, shade(col, -.3))
    for a, b in zip(inner, inner[1:] + inner[:1]):
        i.line(a[0], a[1], b[0], b[1], accent, neon=True)
    return i


def muscle(col=(170, 40, 60), clamp=CHROME):
    i = I()
    for k, x in enumerate((8, 12, 16, 20)):
        i.poly([(x, 6), (x + 3, 6), (x + 4, 26), (x + 1, 26)], shade(col, (k % 2) * .15))
    i.rect(6, 4, 26, 7, clamp); i.rect(7, 25, 27, 28, clamp)
    i.line(8, 16, 24, 16, CYAN, neon=True)
    return i


def heart(col=METAL_L, glow_col=RED):
    i = I()
    i.ell(5, 6, 17, 18, col); i.ell(15, 6, 27, 18, col)
    i.poly([(6, 14), (26, 14), (16, 28)], col)
    i.ell(12, 11, 20, 19, glow_col, neon=True)
    i.glow(16, 15, glow_col, 3)
    i.rect(10, 3, 12, 8, DARK); i.rect(19, 2, 21, 7, DARK)
    return i


def capsule(col=CHROME, liquid=ORANGE):
    i = I(-45)
    i.rect(-10, -4, 10, 4, col)
    i.rect(-7, -3, 7, 3, liquid, neon=True)
    i.rect(-12, -2, -10, 2, DARK); i.rect(10, -2, 12, 2, DARK)
    i.line(-6, -2, 5, -2, WHITE)
    return i


# ======================= Деки и гаджеты =======================

def deck(col=DARK, screen=CYAN, antenna=False):
    i = I()
    i.rect(3, 15, 29, 26, col)
    i.rect(5, 6, 27, 14, shade(col, .2))
    i.rect(7, 8, 25, 12, mix((0, 0, 0), screen, .35), neon=True)
    for y in (9, 11):
        for x in range(8, 24, 3):
            i.line(x, y, x + 1, y, screen, neon=True)
    for y in (17, 20, 23):
        for x in range(5, 27, 3):
            i.px(x, y, METAL_L)
    i.px(25, 23, screen, neon=True)
    if antenna:
        i.line(26, 6, 29, 1, STEEL); i.px(29, 1, RED, neon=True)
    return i


def remote(col=(60, 64, 76), accent=GREEN):
    i = I()
    i.poly([(4, 12), (28, 12), (30, 24), (22, 26), (18, 22), (14, 22), (10, 26), (2, 24)], col)
    i.ell(7, 14, 12, 19, DARK); i.ell(20, 14, 25, 19, DARK)
    i.px(9, 16, accent, neon=True); i.px(22, 16, accent, neon=True)
    i.rect(13, 13, 19, 18, mix((0, 0, 0), accent, .4), neon=True)
    i.line(26, 12, 28, 4, STEEL)
    return i


# ======================= Расходники =======================

def injector(liquid=RED, body=CHROME):
    i = I(-45)
    i.rect(-9, -3, 7, 3, body)
    i.rect(-6, -2, 4, 2, liquid, neon=True)
    i.line(7, 0, 13, 0, STEEL)
    i.rect(-13, -2, -9, 2, DARK)
    i.rect(-15, -3, -13, 3, METAL)
    i.line(-5, -1, 2, -1, WHITE)
    return i


def medkit(col=WHITE, cross=RED):
    i = I()
    i.rect(4, 9, 28, 27, col)
    i.rect(12, 5, 20, 9, DARK)
    i.rect(14, 12, 18, 24, cross, neon=True); i.rect(10, 16, 22, 20, cross, neon=True)
    i.line(4, 21, 28, 21, shade(col, -.25))
    return i


def grenade(col=OLIVE, band=YELLOW, glow=None):
    i = I()
    i.ell(8, 9, 24, 27, col)
    i.rect(8, 16, 24, 19, band, neon=bool(glow))
    i.rect(13, 5, 19, 10, METAL)
    i.ell(19, 3, 25, 9, None, outline=STEEL)
    i.line(19, 7, 26, 15, METAL_L)
    if glow:
        i.glow(16, 18, glow, 4)
    return i


def smoke():
    i = I()
    i.rect(10, 8, 22, 27, (110, 116, 124))
    i.rect(10, 12, 22, 14, DARK)
    i.rect(13, 4, 19, 8, METAL)
    i.ell(18, 2, 24, 8, None, outline=STEEL)
    i.line(12, 20, 20, 20, WHITE)
    return i


def chip(col=CYAN, body=DARK):
    i = I()
    i.rect(9, 6, 23, 26, body)
    i.poly([(18, 6), (23, 6), (23, 11)], (0, 0, 0, 0))
    i.clear(19, 5, 24, 9)
    i.rect(11, 9, 21, 15, col, neon=True)
    for x in range(11, 22, 3):
        i.rect(x, 21, x + 1, 26, GOLD)
    i.line(11, 17, 20, 17, shade(col, -.3))
    return i


def shard(col=CYAN):
    i = I()
    pts = [(16, 3), (24, 12), (19, 29), (13, 29), (8, 12)]
    i.poly(pts, mix((10, 12, 20), col, .55))
    i.line(16, 4, 16, 28, col, neon=True)
    i.line(9, 12, 23, 12, col, neon=True)
    i.line(16, 12, 13, 28, shade(col, .4)); i.line(16, 12, 19, 28, shade(col, .4))
    i.glow(16, 16, col, 2)
    return i


def skillchip(col=VIOLET):
    i = I()
    i.rect(6, 8, 26, 24, (26, 26, 36))
    i.rect(6, 8, 26, 11, col, neon=True)
    i.rect(10, 14, 18, 21, GOLD)
    i.line(19, 15, 24, 15, METAL_L); i.line(19, 18, 24, 18, METAL_L); i.line(19, 21, 22, 21, METAL_L)
    return i


def databank(col=(40, 44, 58), led=GREEN):
    i = I()
    i.rect(5, 6, 27, 27, col)
    for y in (9, 14, 19, 24):
        i.rect(7, y, 25, y + 3, shade(col, -.3))
        i.px(23, y + 1, led, neon=True)
        i.line(9, y + 1, 18, y + 1, METAL_L)
    return i


def holomap(col=CYAN):
    i = I()
    i.rect(4, 9, 28, 25, (20, 24, 36))
    i.rect(6, 11, 26, 23, mix((0, 0, 0), col, .2), neon=True)
    for x in range(8, 26, 4):
        i.line(x, 11, x, 23, mix((0, 0, 0), col, .55))
    for y in range(13, 23, 4):
        i.line(6, y, 26, y, mix((0, 0, 0), col, .55))
    i.px(18, 16, RED, neon=True); i.glow(18, 16, RED, 2)
    return i


# ======================= Еда =======================

def noodles():
    i = I()
    i.poly([(8, 12), (24, 12), (22, 28), (10, 28)], WHITE)
    i.rect(10, 18, 22, 22, RED)
    i.line(13, 2, 17, 13, (200, 160, 100)); i.line(17, 1, 19, 13, (200, 160, 100))
    i.rect(8, 10, 24, 12, (230, 220, 180))
    return i


def can(col=RED, accent=WHITE, neon=False):
    i = I()
    i.rect(10, 6, 22, 27, col)
    i.rect(10, 5, 22, 7, CHROME); i.rect(10, 26, 22, 28, CHROME)
    i.rect(10, 13, 22, 18, accent, neon=neon)
    if neon:
        i.glow(16, 15, accent, 3)
    return i


def burger():
    i = I()
    i.ell(5, 6, 27, 18, (200, 130, 60))
    i.rect(5, 14, 27, 16, GREEN)
    i.rect(5, 17, 27, 20, (110, 60, 40))
    i.rect(6, 21, 26, 25, (200, 130, 60))
    for x in (10, 15, 20):
        i.px(x, 9, BONE)
    return i


def ration(col=STEEL, label=ORANGE):
    i = I()
    i.poly([(5, 8), (27, 8), (28, 25), (4, 25)], col)
    i.rect(8, 12, 24, 20, label)
    i.line(5, 9, 27, 9, WHITE)
    i.line(10, 15, 22, 15, shade(label, -.4)); i.line(10, 17, 18, 17, shade(label, -.4))
    return i


def bottle(col=(110, 60, 30), label=YELLOW):
    i = I()
    i.rect(13, 3, 19, 10, col)
    i.poly([(13, 10), (19, 10), (22, 14), (22, 28), (10, 28), (10, 14)], col)
    i.rect(10, 17, 22, 22, label)
    i.line(12, 13, 12, 26, shade(col, .4))
    return i


def cup(col=WHITE, sleeve=(110, 70, 40)):
    i = I()
    i.poly([(8, 8), (24, 8), (22, 28), (10, 28)], col)
    i.rect(7, 6, 25, 9, shade(col, -.2))
    i.rect(9, 15, 23, 21, sleeve)
    i.line(14, 2, 15, 5, (180, 180, 190)); i.line(18, 1, 17, 5, (180, 180, 190))
    return i


# ======================= Инструменты и мелочь =======================

def multitool():
    i = I(-45)
    i.rect(-12, -3, 6, 3, CRIMSON)
    i.rect(6, -1, 14, 1, STEEL)
    i.poly([(-6, -3), (-2, -8), (0, -7), (-3, -3)], STEEL)
    i.px(-9, 0, WHITE)
    return i


def flashlight():
    i = I(-45)
    i.rect(-12, -2, 6, 2, METAL)
    i.rect(6, -4, 10, 4, METAL_L)
    i.rect(10, -3, 11, 3, WHITE, neon=True)
    i.glow(13, 0, YELLOW, 4)
    for u in (-8, -5, -2):
        i.line(u, -2, u, 2, DARK)
    return i


def keycard(col=CYAN):
    i = I()
    i.rect(4, 9, 28, 24, (220, 224, 232))
    i.rect(4, 12, 28, 15, (24, 24, 30))
    i.rect(6, 17, 12, 22, GOLD)
    i.rect(16, 18, 26, 19, col, neon=True)
    i.rect(16, 21, 22, 22, METAL_L)
    return i


def decoder(col=DARK, accent=GREEN):
    i = I()
    i.rect(8, 6, 24, 24, col)
    i.rect(10, 8, 22, 14, mix((0, 0, 0), accent, .4), neon=True)
    i.line(11, 10, 17, 10, accent, neon=True); i.line(11, 12, 20, 12, accent, neon=True)
    for x in (11, 15, 19):
        i.rect(x, 17, x + 2, 19, METAL_L)
    i.line(12, 24, 9, 30, RED); i.line(19, 24, 23, 30, YELLOW)
    return i


def scanner(col=(50, 56, 70), screen=CYAN):
    i = I()
    i.rect(8, 3, 24, 29, col)
    i.rect(10, 5, 22, 18, mix((0, 0, 0), screen, .3), neon=True)
    i.line(11, 12, 14, 9, screen, neon=True); i.line(14, 9, 17, 14, screen, neon=True); i.line(17, 14, 21, 7, screen, neon=True)
    i.ell(13, 21, 19, 27, DARK)
    return i


def cable(col=(30, 30, 40), plug=CYAN):
    i = I()
    for r in (12, 9, 6):
        i.ell(16 - r, 16 - r, 16 + r, 16 + r, None, outline=col)
        i.ell(16 - r + 1, 16 - r + 1, 16 + r - 1, 16 + r - 1, None, outline=shade(col, .3))
    i.rect(24, 22, 29, 26, METAL_L)
    i.px(28, 24, plug, neon=True)
    return i


def phone(col=DARK, screen=MAGENTA):
    i = I()
    i.rect(10, 3, 22, 29, col)
    i.rect(11, 5, 21, 25, mix((0, 0, 0), screen, .4), neon=True)
    for y in (8, 12, 16, 20):
        i.line(12, y, 19, y, screen, neon=True)
    i.px(16, 27, METAL_L)
    return i


def minidrone(col=METAL, eye=RED):
    i = I()
    for x, y in ((7, 9), (25, 9), (7, 23), (25, 23)):
        i.ell(x - 5, y - 2, x + 5, y + 2, None, outline=METAL_L)
        i.line(x, y, 16, 16, DARK, w=2)
    i.ell(11, 11, 21, 21, col)
    i.ell(14, 14, 18, 18, eye, neon=True)
    i.glow(16, 16, eye, 2)
    return i


def bag(col=(42, 44, 52), accent=YELLOW):
    i = I()
    i.ell(3, 10, 29, 28, col)
    i.rect(7, 10, 25, 27, col)
    i.line(9, 10, 12, 4, DARK, w=2); i.line(23, 10, 20, 4, DARK, w=2); i.line(12, 4, 20, 4, DARK, w=2)
    i.line(7, 16, 25, 16, accent, neon=True)
    return i


def lighter():
    i = I()
    i.rect(11, 12, 21, 28, CHROME)
    i.rect(11, 9, 21, 12, METAL)
    i.poly([(15, 9), (17, 2), (19, 9)], ORANGE, neon=True)
    i.glow(17, 5, ORANGE, 3)
    return i


def toolbox(col=RED):
    i = I()
    i.rect(3, 12, 29, 27, col)
    i.rect(3, 12, 29, 15, shade(col, .25))
    i.rect(11, 6, 21, 9, DARK); i.rect(11, 6, 12, 12, DARK); i.rect(20, 6, 21, 12, DARK)
    i.rect(14, 15, 18, 18, CHROME)
    return i


def binoculars(col=DARK, lens=CYAN):
    i = I()
    i.rect(5, 9, 14, 25, col); i.rect(18, 9, 27, 25, col)
    i.rect(13, 12, 19, 18, METAL)
    i.ell(5, 21, 14, 29, METAL); i.ell(18, 21, 27, 29, METAL)
    i.ell(7, 23, 12, 28, lens, neon=True); i.ell(20, 23, 25, 28, lens, neon=True)
    return i


def watch(col=GOLD, face=(20, 20, 26)):
    i = I()
    i.rect(12, 2, 20, 30, shade(col, -.25))
    i.ell(8, 8, 24, 24, col)
    i.ell(10, 10, 22, 22, face)
    i.line(16, 16, 16, 12, CYAN, neon=True); i.line(16, 16, 19, 17, CYAN, neon=True)
    return i


def holofig(col=CYAN):
    i = I()
    i.rect(8, 24, 24, 28, METAL)
    i.poly([(10, 24), (22, 24), (19, 8), (13, 8)], mix((10, 12, 20), col, .3))
    i.ell(13, 4, 19, 10, mix((10, 12, 20), col, .55))
    i.line(16, 10, 16, 22, col, neon=True)
    i.line(12, 14, 20, 14, col, neon=True)
    i.glow(16, 14, col, 4)
    return i


def jewel_ring(col=GOLD, gem=(80, 200, 255)):
    i = I()
    i.ell(7, 10, 25, 28, None, outline=col)
    i.ell(8, 11, 24, 27, None, outline=shade(col, -.3))
    i.poly([(16, 3), (21, 8), (16, 13), (11, 8)], gem, neon=True)
    i.glow(16, 8, gem, 2)
    return i


def crystal(col=VIOLET):
    i = I()
    for x0, h in ((9, 14), (14, 22), (19, 16)):
        i.poly([(x0, 28), (x0 + 5, 28), (x0 + 5, 28 - h), (x0 + 2, 28 - h - 4), (x0, 28 - h)], mix((20, 10, 40), col, .7))
        i.line(x0 + 2, 27, x0 + 2, 28 - h - 2, shade(col, .5), neon=True)
    i.glow(16, 12, col, 3)
    return i


def module(col=(40, 44, 56), accent=ORANGE):
    i = I()
    i.rect(5, 8, 27, 24, col)
    i.rect(8, 11, 24, 21, shade(col, -.3))
    i.rect(11, 13, 21, 19, accent, neon=True)
    i.line(5, 26, 27, 26, GOLD)
    for x in range(7, 27, 3):
        i.px(x, 27, GOLD)
    return i


def vinyl():
    i = I()
    i.ell(3, 3, 29, 29, (16, 16, 20))
    for r in (11, 8):
        i.ell(16 - r, 16 - r, 16 + r, 16 + r, None, outline=(40, 40, 50))
    i.ell(12, 12, 20, 20, MAGENTA)
    i.px(16, 16, (0, 0, 0))
    return i


def toy():
    i = I()
    i.rect(10, 4, 22, 13, (210, 210, 220))
    i.rect(12, 7, 20, 10, CYAN, neon=True)
    i.rect(8, 14, 24, 25, RED)
    i.rect(4, 15, 7, 22, (210, 210, 220)); i.rect(25, 15, 28, 22, (210, 210, 220))
    i.rect(10, 26, 14, 30, DARK); i.rect(18, 26, 22, 30, DARK)
    return i


def photo():
    i = I()
    i.rect(6, 4, 26, 28, WHITE)
    i.rect(8, 6, 24, 21, (40, 30, 60))
    i.rect(8, 15, 24, 21, (200, 60, 120))
    i.ell(16, 8, 21, 13, YELLOW)
    return i


def cigs(col=(200, 30, 50)):
    i = I()
    i.rect(8, 8, 24, 28, col)
    i.rect(8, 8, 24, 12, WHITE)
    for x in (10, 14, 18):
        i.rect(x, 4, x + 2, 8, (230, 220, 200)); i.px(x, 4, ORANGE)
    i.rect(11, 16, 21, 22, GOLD)
    return i


# ======================= Материалы =======================

def scrap():
    i = I()
    i.poly([(4, 18), (14, 12), (18, 20), (8, 26)], RUST)
    i.poly([(14, 6), (26, 8), (24, 16), (16, 13)], METAL_L)
    i.poly([(18, 18), (28, 20), (26, 28), (17, 26)], METAL)
    i.px(20, 10, DARK); i.px(9, 20, DARK)
    return i


def circuit(col=(20, 110, 60)):
    i = I()
    i.rect(4, 6, 28, 26, col)
    i.rect(10, 10, 18, 18, DARK)
    for x, y, x2, y2 in ((18, 12, 26, 12), (18, 16, 24, 22), (6, 22, 14, 22), (14, 18, 14, 24)):
        i.line(x, y, x2, y2, GOLD)
    i.px(25, 9, RED, neon=True); i.px(7, 9, CYAN, neon=True)
    return i


def battery(col=METAL, level=GREEN):
    i = I()
    i.rect(9, 6, 23, 28, col)
    i.rect(13, 3, 19, 6, METAL_L)
    i.rect(11, 16, 21, 26, level, neon=True)
    i.rect(11, 8, 21, 15, DARK)
    i.line(15, 10, 17, 10, WHITE); i.line(16, 9, 16, 11, WHITE)
    return i


def wires():
    i = I()
    for col, pts in ((RED, [(3, 8), (12, 14), (20, 9), (29, 16)]), (YELLOW, [(3, 20), (11, 12), (20, 22), (29, 12)]),
                     (CYAN, [(4, 26), (14, 20), (22, 26), (29, 22)])):
        for a, b in zip(pts, pts[1:]):
            i.line(a[0], a[1], b[0], b[1], col, w=2)
    return i


def servo():
    i = I()
    i.ell(4, 6, 20, 22, METAL_L)
    for a in range(8):
        import math as _m
        x = 12 + 9 * _m.cos(a * _m.pi / 4); y = 14 + 9 * _m.sin(a * _m.pi / 4)
        i.rect(int(x) - 1, int(y) - 1, int(x) + 1, int(y) + 1, METAL_L)
    i.ell(9, 11, 15, 17, DARK)
    i.rect(16, 16, 28, 28, METAL)
    i.px(22, 22, ORANGE, neon=True)
    return i


def lens(col=CYAN):
    i = I()
    i.ell(5, 5, 27, 27, METAL)
    i.ell(8, 8, 24, 24, mix((0, 0, 0), col, .5), neon=True)
    i.ell(12, 12, 20, 20, mix((0, 0, 0), col, .8), neon=True)
    i.px(12, 11, WHITE); i.px(13, 11, WHITE)
    return i


def chem(col=(230, 200, 30)):
    i = I()
    i.rect(8, 6, 24, 28, METAL_L)
    i.rect(11, 3, 21, 6, DARK)
    i.poly([(16, 10), (23, 23), (9, 23)], col)
    i.px(16, 14, BLACK); i.px(16, 16, BLACK); i.px(16, 20, BLACK)
    return i


def biojar(col=GREEN):
    i = I()
    i.rect(8, 6, 24, 28, mix((10, 20, 15), col, .35))
    i.rect(8, 4, 24, 7, METAL)
    i.ell(11, 13, 21, 23, (170, 70, 90))
    i.line(10, 10, 10, 26, shade(col, .5), neon=True)
    i.glow(16, 18, col, 3)
    return i


def codecube(col=CYAN):
    i = I()
    i.poly([(16, 4), (27, 10), (16, 16), (5, 10)], mix((0, 0, 0), col, .55))
    i.poly([(5, 10), (16, 16), (16, 28), (5, 22)], mix((0, 0, 0), col, .3))
    i.poly([(27, 10), (16, 16), (16, 28), (27, 22)], mix((0, 0, 0), col, .2))
    for a, b in (((16, 4), (27, 10)), ((27, 10), (16, 16)), ((16, 16), (5, 10)), ((5, 10), (16, 4)), ((16, 16), (16, 28))):
        i.line(a[0], a[1], b[0], b[1], col, neon=True)
    i.glow(16, 16, col, 3)
    return i


def polymer(col=(60, 160, 200)):
    i = I()
    i.ell(4, 8, 14, 26, shade(col, -.2))
    i.rect(9, 8, 26, 26, col)
    i.ell(21, 8, 30, 26, shade(col, .2))
    i.ell(23, 13, 28, 21, shade(col, -.4))
    return i


def core(col=CYAN):
    i = I()
    i.rect(10, 4, 22, 28, METAL)
    i.rect(12, 8, 20, 24, col, neon=True)
    for y in (6, 13, 19, 26):
        i.rect(9, y, 23, y + 1, METAL_L)
    i.glow(16, 16, col, 5)
    return i


def tissue(col=(200, 80, 100)):
    i = I()
    i.ell(5, 8, 27, 26, col)
    i.ell(9, 11, 18, 19, shade(col, .3))
    i.line(18, 20, 26, 14, (90, 30, 50), w=2)
    i.px(12, 14, WHITE)
    return i


def dice():
    i = I()
    i.rect(4, 12, 15, 23, WHITE); i.rect(17, 7, 28, 18, RED)
    for x, y in ((7, 15), (12, 20), (9, 17)):
        i.px(x, y, BLACK)
    for x, y in ((20, 10), (25, 15)):
        i.px(x, y, WHITE)
    return i


# ======================= Каталог =======================
# (id, название, категория, w, h, слот, двуручное, диагональный, ключевые слова, рисовалка,
#  урон, броня, цена, категория торговли, флаги)
# флаги: R — огнестрел/стрелковое (атака от ЛОВ), F — фехтовальное (лучшая из СИЛ/ЛОВ),
#        H — тяжёлая броня (ЛОВ к КБ не идёт), M — средняя (ЛОВ ≤ +2), A — именная (★), S — только сюжет
W, SH, A, J, P, SC, B, FO, V, MAT, T, MI = "weapon", "shield", "armor", "jewelry", "potion", "scroll", "book", "food", "valuable", "material", "tool", "misc"
CATALOG = [
    # ---- Огнестрел ----
    ("cy_pistol", "Пистолет", W, 2, 2, "Hand1", False, False, "пистолет ствол пушк глок", lambda: pistol(), "1d6 кинетический", 0, 15, "ranged", "R"),
    ("cy_revolver", "Револьвер", W, 2, 2, "Hand1", False, False, "револьвер барабан", lambda: pistol(METAL_L, kind="revolver"), "1d8 кинетический", 0, 25, "ranged", "R"),
    ("cy_heavy_pistol", "Тяжёлый пистолет", W, 2, 2, "Hand1", False, False, "тяжёл пистолет магнум", lambda: pistol((70, 50, 50), RED, "heavy"), "1d8 кинетический", 0, 35, "ranged", "R"),
    ("cy_smart_pistol", "Умный пистолет", W, 2, 2, "Hand1", False, False, "умн смарт пистолет самонавод", lambda: pistol(NAVY, CYAN, "smart"), "1d6 кинетический", 0, 60, "ranged", "R"),
    ("cy_laser_pistol", "Лазерный пистолет", W, 2, 2, "Hand1", False, False, "лазер бластер", lambda: pistol((200, 204, 214), MAGENTA, "laser"), "1d6 энергетический", 0, 80, "ranged", "R"),
    ("cy_smg", "Пистолет-пулемёт", W, 2, 2, "Hand1", False, False, "пистолет-пулем пп узи автомат smg", lambda: smg(), "1d6 кинетический", 0, 35, "ranged", "R"),
    ("cy_compact_smg", "Компактный ПП", W, 2, 2, "Hand1", False, False, "компактн пп", lambda: smg((60, 40, 70), MAGENTA, True), "1d6 кинетический", 0, 25, "ranged", "R"),
    ("cy_rifle", "Штурмовая винтовка", W, 2, 4, "Hand1", True, True, "штурмов винтовк автомат калаш ак", lambda: rifle(), "1d10 кинетический", 0, 60, "ranged", "R"),
    ("cy_carbine", "Карабин", W, 2, 4, "Hand1", True, True, "карабин", lambda: rifle(OLIVE, YELLOW), "1d8 кинетический", 0, 45, "ranged", "R"),
    ("cy_shotgun", "Помповый дробовик", W, 2, 4, "Hand1", True, True, "дробовик помпов обрез ружь", lambda: rifle((90, 60, 40), ORANGE, "shotgun"), "1d10 кинетический", 0, 40, "ranged", "R"),
    ("cy_auto_shotgun", "Автоматический дробовик", W, 2, 4, "Hand1", True, True, "автоматическ дробовик", lambda: rifle(DARK, RED, "shotgun"), "2d6 кинетический", 0, 75, "ranged", "R"),
    ("cy_sniper", "Снайперская винтовка", W, 2, 4, "Hand1", True, True, "снайпер винтовк", lambda: rifle(NAVY, CYAN, "sniper"), "1d12 кинетический", 0, 120, "ranged", "R"),
    ("cy_railgun", "Рельсотрон", W, 2, 4, "Hand1", True, True, "рельсотрон гаусс тех-винтовк", lambda: rifle((50, 54, 70), VIOLET, "rail"), "1d12 энергетический", 0, 250, "polearms", "R"),
    ("cy_launcher", "Гранатомёт", W, 2, 4, "Hand1", True, True, "гранатом ракетниц базук", lambda: launcher(), "2d6 термический", 0, 200, "polearms", "R"),
    # ---- Клинки и ударное ----
    ("cy_knife", "Нож", W, 1, 2, "Hand1", False, True, "нож кинжал заточк", lambda: knife(), "1d4 режущий", 0, 3, "blades", "F"),
    ("cy_vibroknife", "Виброклинок", W, 1, 2, "Hand1", False, True, "вибро вибронож", lambda: knife(CHROME, CYAN), "1d4 режущий", 0, 18, "blades", "F"),
    ("cy_machete", "Термомачете", W, 1, 3, "Hand1", False, True, "мачете тесак термо", lambda: machete(), "1d8 режущий", 0, 15, "blades", ""),
    ("cy_katana", "Катана", W, 1, 3, "Hand1", False, True, "катан клинок меч", lambda: blade(wrap=RED), "1d8 режущий", 0, 40, "blades", "F"),
    ("cy_monokatana", "Мономолекулярная катана", W, 1, 3, "Hand1", False, True, "моно мономолекул катан", lambda: blade(CHROME, CYAN, wrap=CYAN), "1d8 режущий", 0, 120, "blades", "F"),
    ("cy_mantis", "Клинки-богомолы", W, 2, 3, "Hand1", False, False, "богомол клинки-имплант руки-лезви", lambda: mantis(), "1d6 режущий", 0, 150, "blades", "F"),
    ("cy_baton", "Шоковая дубинка", W, 1, 3, "Hand1", False, True, "дубинк шокер электродубинк", lambda: baton(), "1d6 ударный", 0, 12, "weapons", ""),
    ("cy_pipe", "Обрезок трубы", W, 1, 3, "Hand1", False, True, "труб арматур обрезок", lambda: pipe(), "1d4 ударный", 0, 1, "weapons", ""),
    ("cy_bat", "Бита", W, 1, 3, "Hand1", False, True, "бит бейсбол", lambda: bat(), "1d6 ударный", 0, 3, "weapons", ""),
    ("cy_hammer", "Гидравлический молот", W, 2, 4, "Hand1", True, True, "молот кувалд гидравлич", lambda: hammer(), "2d6 ударный", 0, 45, "weapons", ""),
    ("cy_knuckles", "Кастет", W, 1, 1, "Hand1", False, False, "кастет", lambda: knuckles(), "1d4 ударный", 0, 4, "weapons", ""),
    # ---- Кибердеки ----
    ("cy_deck", "Кибердека", W, 2, 2, "Hand1", False, False, "кибердек дека деки нетран", lambda: deck(), "1d6 кибератака", 0, 60, "magic", ""),
    ("cy_deck_mil", "Военная кибердека", W, 2, 2, "Hand1", False, False, "военн дека", lambda: deck(OLIVE, GREEN), "1d6 кибератака", 0, 140, "magic", ""),
    ("cy_deck_pro", "Кибердека нетраннера-профи", W, 2, 2, "Hand1", False, False, "профи дека", lambda: deck((40, 20, 50), MAGENTA, True), "1d6 кибератака", 0, 250, "magic", ""),
    ("cy_drone_remote", "Пульт боевого дрона", W, 2, 2, "Hand1", False, False, "пульт дрон техник", lambda: remote(), "1d6 кинетический", 0, 90, "magic", "R"),
    # ---- Броня и одежда ----
    ("cy_jacket", "Кожанка с бронепластинами", A, 2, 3, "Body", False, False, "кожанк куртк", lambda: jacket(), "", 1, 12, "light_armor", ""),
    ("cy_jacket_neon", "Неоновая куртка", A, 2, 3, "Body", False, False, "неонов куртк бомбер", lambda: jacket((40, 30, 60), MAGENTA), "", 1, 30, "clothing", ""),
    ("cy_vest", "Кевларовый жилет", A, 2, 3, "Body", False, False, "кевлар жилет бронежилет", lambda: vest(), "", 3, 30, "light_armor", "M"),
    ("cy_tactical", "Тактическая броня", A, 2, 3, "Body", False, False, "тактическ броня", lambda: vest((40, 44, 54), METAL_L, CYAN, True), "", 5, 90, "heavy_armor", "M"),
    ("cy_combat_armor", "Боевой бронекостюм", A, 2, 3, "Body", False, False, "бронекостюм боев брон", lambda: exo((36, 40, 48), RED), "", 6, 160, "heavy_armor", "H"),
    ("cy_riot", "Броня спецназа", A, 2, 3, "Body", False, False, "спецназ омон штурмов брон", lambda: vest(NAVY, METAL_L, WHITE, True), "", 6, 130, "heavy_armor", "H"),
    ("cy_exo", "Силовой экзоскелет", A, 2, 3, "Body", False, False, "экзоскелет силов брон", lambda: exo((50, 50, 58), YELLOW), "", 8, 450, "heavy_armor", "H"),
    ("cy_hoodie", "Худи", A, 2, 3, "Body", False, False, "худи толстовк капюшон", lambda: hoodie(), "", 0, 3, "clothing", ""),
    ("cy_suit", "Корпоративный костюм", A, 2, 3, "Body", False, False, "костюм корпорат пиджак", lambda: suit(), "", 1, 45, "clothing", ""),
    ("cy_coat", "Длинный плащ", A, 2, 3, "Cloak", False, False, "плащ пальто тренч", lambda: coat(), "", 0, 20, "clothing", ""),
    ("cy_coat_armored", "Бронированный плащ", A, 2, 3, "Cloak", False, False, "бронирован плащ", lambda: coat((50, 40, 34), ORANGE), "", 1, 60, "light_armor", ""),
    ("cy_helmet", "Тактический шлем", A, 2, 2, "Helmet", False, False, "шлем тактическ", lambda: helmet(), "", 1, 15, "heavy_armor", ""),
    ("cy_helmet_full", "Закрытый шлем", A, 2, 2, "Helmet", False, False, "закрыт шлем", lambda: helmet(DARK, RED, "full"), "", 1, 45, "heavy_armor", ""),
    ("cy_shades", "Тактические очки", A, 2, 2, "Helmet", False, False, "очки визор", lambda: helmet(kind="shades"), "", 0, 12, "clothing", ""),
    ("cy_mask", "Респиратор", A, 2, 2, "Helmet", False, False, "респиратор маск противогаз", lambda: helmet((70, 74, 86), GREEN, "mask"), "", 0, 8, "light_armor", ""),
    ("cy_cap", "Кепка", A, 2, 2, "Helmet", False, False, "кепк бейсболк", lambda: helmet((40, 40, 50), MAGENTA, "cap"), "", 0, 1, "clothing", ""),
    ("cy_gloves", "Тактические перчатки", A, 2, 2, "Gloves", False, False, "перчатк", lambda: gloves(), "", 0, 4, "light_armor", ""),
    ("cy_power_gloves", "Силовые перчатки", A, 2, 2, "Gloves", False, False, "силов перчатк", lambda: gloves(DARK, ORANGE, True), "", 1, 30, "heavy_armor", ""),
    ("cy_boots", "Ботинки", A, 2, 2, "Boots", False, False, "ботинк кроссовк обув", lambda: boots(), "", 0, 3, "light_armor", ""),
    ("cy_combat_boots", "Штурмовые ботинки", A, 2, 2, "Boots", False, False, "штурмов берц", lambda: boots(DARK, YELLOW, True), "", 0, 15, "heavy_armor", ""),
    ("cy_belt", "Разгрузочный пояс", A, 2, 1, "Belt", False, False, "пояс разгрузк ремень", lambda: belt(), "", 0, 4, "light_armor", ""),
    # ---- Щиты ----
    ("cy_ballistic", "Баллистический щит", SH, 2, 3, "Hand2", False, False, "баллистич щит", lambda: ballistic(), "", 2, 30, "shields", ""),
    ("cy_riot_shield", "Щит спецназа", SH, 2, 3, "Hand2", False, False, "щит спецназ", lambda: ballistic(NAVY, WHITE, False), "", 3, 50, "shields", ""),
    ("cy_projector", "Проектор силового поля", SH, 2, 2, "Hand2", False, False, "проектор пол силов", lambda: projector(), "", 2, 90, "shields", ""),
    # ---- Импланты ----
    ("cy_neuroport", "Нейропорт", J, 1, 1, "Amulet", False, False, "нейропорт порт", lambda: chip_implant(), "", 0, 40, "jewelry", ""),
    ("cy_cortex", "Кортикальный чип", J, 1, 1, "Amulet", False, False, "кортикал чип мозг", lambda: chip_implant((40, 20, 40), MAGENTA), "", 0, 90, "jewelry", ""),
    ("cy_optic", "Оптический имплант", J, 1, 1, "Amulet", False, False, "оптическ глаз оптик", lambda: optic(), "", 0, 70, "jewelry", ""),
    ("cy_neural_link", "Нейроинтерфейс", J, 1, 1, "Amulet", False, False, "нейроинтерфейс нейролинк", lambda: neural_link(), "", 0, 110, "jewelry", ""),
    ("cy_reflex", "Рефлекс-бустер", J, 1, 1, "Ring1", False, False, "рефлекс бустер позвоноч", lambda: spine(), "", 0, 90, "jewelry", ""),
    ("cy_subdermal", "Подкожная броня", J, 1, 1, "Ring1", False, False, "подкож броня пластин", lambda: hexplate(), "", 0, 80, "jewelry", ""),
    ("cy_muscle", "Синтетические мышцы", J, 1, 1, "Ring1", False, False, "мышц синтетическ волокн", lambda: muscle(), "", 0, 70, "jewelry", ""),
    ("cy_heart", "Биомеханическое сердце", J, 1, 1, "Ring1", False, False, "сердц биомехан", lambda: heart(), "", 0, 120, "jewelry", ""),
    ("cy_adrenal", "Адреналиновый насос", J, 1, 1, "Ring1", False, False, "адренал насос", lambda: capsule(), "", 0, 60, "jewelry", ""),
    ("cy_nano", "Капсула наноботов", J, 1, 1, "Ring1", False, False, "нано наноботы", lambda: capsule(METAL_L, GREEN), "", 0, 100, "jewelry", ""),
    ("cy_cyberlimb", "Киберпротез руки", J, 1, 1, "Ring1", False, False, "протез киберрук рука", lambda: mantis(METAL_L, CYAN), "", 0, 60, "jewelry", ""),
    # ---- Стимы и медицина ----
    ("cy_medstim", "Медстим", P, 1, 1, None, False, False, "медстим стим лечени аптечк", lambda: injector(RED), "", 0, 6, "potions", ""),
    ("cy_ram_stim", "Стим ОЗУ", P, 1, 1, None, False, False, "озу память", lambda: injector(CYAN), "", 0, 7, "potions", ""),
    ("cy_antitox", "Антитоксин", P, 1, 1, None, False, False, "антитокс противояд", lambda: injector(GREEN), "", 0, 8, "potions", ""),
    ("cy_combat_stim", "Боевой стим", P, 1, 1, None, False, False, "боев стим ярост берсерк", lambda: injector(ORANGE), "", 0, 14, "potions", ""),
    ("cy_stealth_stim", "Стим-невидимка", P, 1, 1, None, False, False, "невидим камуфляж", lambda: injector((150, 150, 170)), "", 0, 28, "potions", ""),
    ("cy_shield_stim", "Инъекция бронегеля", P, 1, 1, None, False, False, "бронегел щит", lambda: injector((60, 110, 255)), "", 0, 10, "potions", ""),
    ("cy_reflex_stim", "Стим рефлексов", P, 1, 1, None, False, False, "рефлекс скорост", lambda: injector(YELLOW), "", 0, 15, "potions", ""),
    ("cy_neuro_stim", "Нейроусилитель", P, 1, 1, None, False, False, "нейроусил усилител", lambda: injector(MAGENTA), "", 0, 18, "potions", ""),
    ("cy_cleanser", "Очиститель систем", P, 1, 1, None, False, False, "очистител перезагруз", lambda: injector(WHITE), "", 0, 12, "potions", ""),
    ("cy_toxin", "Нейротоксин", P, 1, 1, None, False, False, "токсин яд", lambda: injector((40, 40, 40)), "", 0, 18, "potions", ""),
    ("cy_cheap_stim", "Дешёвый стим", P, 1, 1, None, False, False, "дешёв уличн стим", lambda: injector((140, 110, 80), METAL), "", 0, 3, "potions", ""),
    ("cy_medkit", "Аптечка", P, 2, 2, None, False, False, "аптечк медкомплект", lambda: medkit(), "", 0, 25, "potions", ""),
    ("cy_trauma_kit", "Реанимационный набор", P, 2, 2, None, False, False, "реанимац травма-набор", lambda: medkit(GOLD, WHITE), "", 0, 60, "potions", ""),
    # ---- Гранаты и чипы ----
    ("cy_grenade", "Осколочная граната", SC, 1, 1, None, False, False, "граната осколочн", lambda: grenade(), "", 0, 15, "scrolls", ""),
    ("cy_emp_grenade", "ЭМИ-граната", SC, 1, 1, None, False, False, "эми электромагн", lambda: grenade(NAVY, CYAN, CYAN), "", 0, 25, "scrolls", ""),
    ("cy_flash_grenade", "Светошумовая граната", SC, 1, 1, None, False, False, "светошум флешк", lambda: grenade((180, 184, 190), WHITE, WHITE), "", 0, 12, "scrolls", ""),
    ("cy_smoke_grenade", "Дымовая шашка", SC, 1, 1, None, False, False, "дымов шашк", lambda: smoke(), "", 0, 8, "scrolls", ""),
    ("cy_chip_attack", "Чип боевой программы", SC, 1, 1, None, False, False, "чип программ боев взлом", lambda: chip(RED), "", 0, 30, "scrolls", ""),
    ("cy_chip_shield", "Чип защитного протокола", SC, 1, 1, None, False, False, "чип защит протокол", lambda: chip((60, 110, 255)), "", 0, 25, "scrolls", ""),
    ("cy_chip_util", "Чип утилиты", SC, 1, 1, None, False, False, "чип утилит", lambda: chip(GREEN), "", 0, 25, "scrolls", ""),
    ("cy_shard", "Шард данных", SC, 1, 1, None, False, False, "шард данн запис", lambda: shard(), "", 0, 2, "scrolls", ""),
    ("cy_holomap", "Голокарта района", SC, 2, 2, None, False, False, "голокарт карт", lambda: holomap(), "", 0, 20, "scrolls", ""),
    # ---- Базы данных ----
    ("cy_skillchip", "Обучающий чип", B, 1, 1, None, False, False, "обучающ навык скилчип", lambda: skillchip(), "", 0, 60, "books", ""),
    ("cy_databank", "Накопитель с базой данных", B, 2, 2, None, False, False, "накопител баз архив", lambda: databank(), "", 0, 40, "books", ""),
    ("cy_manual", "Техническое руководство", B, 1, 1, None, False, False, "руководств мануал инструкц", lambda: skillchip(YELLOW), "", 0, 15, "books", ""),
    # ---- Еда ----
    ("cy_noodles", "Лапша в коробке", FO, 1, 1, None, False, False, "лапш рамен еда", lambda: noodles(), "", 0, 2, "food", ""),
    ("cy_soda", "Газировка", FO, 1, 1, None, False, False, "газиров кол банк", lambda: can(), "", 0, 1, "food", ""),
    ("cy_energy", "Энергетик", FO, 1, 1, None, False, False, "энергетик", lambda: can((20, 24, 30), GREEN, True), "", 0, 2, "food", ""),
    ("cy_burger", "Синтбургер", FO, 1, 1, None, False, False, "бургер синт", lambda: burger(), "", 0, 2, "food", ""),
    ("cy_ration", "Синтепаёк", FO, 1, 1, None, False, False, "паёк паек рацион", lambda: ration(), "", 0, 2, "food", ""),
    ("cy_coffee", "Синтекофе", FO, 1, 1, None, False, False, "кофе", lambda: cup(), "", 0, 1, "food", ""),
    ("cy_beer", "Пиво", FO, 1, 1, None, False, False, "пив бутылк выпивк", lambda: bottle(), "", 0, 2, "food", ""),
    ("cy_water", "Очищенная вода", FO, 1, 1, None, False, False, "вода", lambda: bottle((70, 140, 200), WHITE), "", 0, 1, "food", ""),
    # ---- Инструменты ----
    ("cy_multitool", "Мультитул", T, 1, 1, None, False, False, "мультитул инструмент", lambda: multitool(), "", 0, 6, "tools", ""),
    ("cy_flashlight", "Фонарь", T, 1, 1, None, False, False, "фонар фонарик", lambda: flashlight(), "", 0, 3, "tools", ""),
    ("cy_keycard", "Ключ-карта", T, 1, 1, None, False, False, "ключ-карт пропуск карт доступ", lambda: keycard(), "", 0, 2, "tools", ""),
    ("cy_decoder", "Электронная отмычка", T, 1, 1, None, False, False, "отмычк декодер взломщик", lambda: decoder(), "", 0, 25, "tools", ""),
    ("cy_scanner", "Сканер", T, 1, 1, None, False, False, "сканер детектор", lambda: scanner(), "", 0, 20, "tools", ""),
    ("cy_cable", "Кабель", T, 1, 1, None, False, False, "кабел провод трос", lambda: cable(), "", 0, 1, "tools", ""),
    ("cy_phone", "Холофон", T, 1, 1, None, False, False, "холофон телефон коммуникатор", lambda: phone(), "", 0, 10, "tools", ""),
    ("cy_minidrone", "Разведдрон", T, 1, 1, None, False, False, "разведдрон дрон-разведчик", lambda: minidrone(), "", 0, 40, "tools", ""),
    ("cy_bag", "Сумка", T, 2, 2, None, False, False, "сумк рюкзак мешок", lambda: bag(), "", 0, 2, "tools", ""),
    ("cy_lighter", "Зажигалка", T, 1, 1, None, False, False, "зажигалк", lambda: lighter(), "", 0, 1, "tools", ""),
    ("cy_toolbox", "Ящик с инструментами", T, 2, 2, None, False, False, "ящик инструмент", lambda: toolbox(), "", 0, 12, "tools", ""),
    ("cy_binoculars", "Цифровой бинокль", T, 1, 1, None, False, False, "бинокл", lambda: binoculars(), "", 0, 15, "tools", ""),
    # ---- Ценности ----
    ("cy_watch", "Золотые часы", V, 1, 1, None, False, False, "часы", lambda: watch(), "", 0, 80, "gems", ""),
    ("cy_holofig", "Голофигурка", V, 1, 1, None, False, False, "голофигур статуэтк", lambda: holofig(), "", 0, 35, "gems", ""),
    ("cy_jewel", "Кольцо с бриллиантом", V, 1, 1, None, False, False, "бриллиант кольц драгоцен", lambda: jewel_ring(), "", 0, 120, "gems", ""),
    ("cy_crystal", "Кристалл памяти", V, 1, 1, None, False, False, "кристалл памят", lambda: crystal(), "", 0, 60, "gems", ""),
    ("cy_proto_module", "Модуль-прототип", V, 1, 1, None, False, False, "прототип модул", lambda: module(), "", 0, 150, "gems", ""),
    ("cy_rare_chip", "Коллекционный чип", V, 1, 1, None, False, False, "коллекцион чип", lambda: chip(GOLD, (40, 30, 10)), "", 0, 90, "gems", ""),
    # ---- Мелочь и диковины ----
    ("cy_vinyl", "Виниловая пластинка", MI, 1, 1, None, False, False, "винил пластинк", lambda: vinyl(), "", 0, 25, "curios", ""),
    ("cy_toy", "Игрушечный робот", MI, 1, 1, None, False, False, "игрушк робот", lambda: toy(), "", 0, 6, "curios", ""),
    ("cy_photo", "Старое фото", MI, 1, 1, None, False, False, "фото фотограф снимок", lambda: photo(), "", 0, 1, "curios", ""),
    ("cy_cigs", "Пачка сигарет", MI, 1, 1, None, False, False, "сигарет курев", lambda: cigs(), "", 0, 2, "curios", ""),
    ("cy_dice", "Игральные кости", MI, 1, 1, None, False, False, "кости кубик", lambda: dice(), "", 0, 2, "curios", ""),
    # ---- Материалы ----
    ("cy_scrap", "Металлолом", MAT, 1, 1, None, False, False, "металлолом лом железк", lambda: scrap(), "", 0, 2, "materials", ""),
    ("cy_circuit", "Печатная плата", MAT, 1, 1, None, False, False, "плат схем микросхем", lambda: circuit(), "", 0, 8, "materials", ""),
    ("cy_battery", "Аккумулятор", MAT, 1, 1, None, False, False, "аккумулятор батаре элемент питан", lambda: battery(), "", 0, 10, "materials", ""),
    ("cy_wires", "Пучок проводов", MAT, 1, 1, None, False, False, "провод", lambda: wires(), "", 0, 2, "materials", ""),
    ("cy_servo", "Сервопривод", MAT, 1, 1, None, False, False, "серво привод мотор", lambda: servo(), "", 0, 12, "materials", ""),
    ("cy_lens", "Оптическая линза", MAT, 1, 1, None, False, False, "линз оптик", lambda: lens(), "", 0, 14, "materials", ""),
    ("cy_chem", "Химреагенты", MAT, 1, 1, None, False, False, "химреагент химикат реагент", lambda: chem(), "", 0, 8, "materials", ""),
    ("cy_biosample", "Образец тканей", MAT, 1, 1, None, False, False, "образец ткан биообраз", lambda: biojar(), "", 0, 15, "materials", ""),
    ("cy_code", "Фрагмент кода", MAT, 1, 1, None, False, False, "фрагмент код ключ шифров", lambda: codecube(), "", 0, 20, "materials", ""),
    ("cy_polymer", "Рулон полимера", MAT, 1, 1, None, False, False, "полимер пластик", lambda: polymer(), "", 0, 4, "materials", ""),
    ("cy_core", "Силовое ядро", MAT, 1, 1, None, False, False, "ядро реактор силов", lambda: core(), "", 0, 40, "materials", ""),
    ("cy_tissue", "Мутировавшая ткань", MAT, 1, 1, None, False, False, "мутаген мутир ткан", lambda: tissue(), "", 0, 10, "materials", ""),
    # ---- Именные (★): облик прототипов и культовых вещей ----
    ("cy_art_katana", "Моноклинок «Тихий шторм»", W, 1, 3, "Hand1", False, True, "тихий шторм", lambda: blade((60, 40, 90), VIOLET, VIOLET, wrap=VIOLET), "1d8 режущий", 0, 400, "blades", "FA"),
    ("cy_art_revolver", "Револьвер «Последний довод»", W, 2, 2, "Hand1", False, False, "последн довод", lambda: pistol(GOLD, RED, "revolver"), "1d8 кинетический", 0, 380, "ranged", "RA"),
    ("cy_art_sniper", "Винтовка «Око бога»", W, 2, 4, "Hand1", True, True, "око бога", lambda: rifle((210, 214, 226), CYAN, "sniper"), "1d12 кинетический", 0, 500, "ranged", "RA"),
    ("cy_art_smg", "ПП «Рой»", W, 2, 2, "Hand1", False, False, "рой", lambda: smg((60, 56, 20), YELLOW), "1d6 кинетический", 0, 360, "ranged", "RA"),
    ("cy_art_deck", "Кибердека «Призрак-7»", W, 2, 2, "Hand1", False, False, "призрак-7 призрак", lambda: deck((12, 10, 16), RED, True), "1d6 кибератака", 0, 520, "magic", "A"),
    ("cy_art_hammer", "Молот «Землетряс»", W, 2, 4, "Hand1", True, True, "землетряс", lambda: hammer(GOLD, ORANGE), "2d6 ударный", 0, 420, "weapons", "A"),
    ("cy_art_armor", "Броня «Цитадель»", A, 2, 3, "Body", False, False, "цитадел", lambda: exo((30, 26, 20), ORANGE, True), "", 8, 900, "heavy_armor", "HA"),
    ("cy_art_coat", "Плащ «Невидимка»", A, 2, 3, "Cloak", False, False, "невидимк хамелеон", lambda: coat((24, 30, 44), CYAN, True), "", 1, 600, "clothing", "A"),
    ("cy_art_heart", "Имплант «Сердце феникса»", J, 1, 1, "Ring1", False, False, "сердце феникса феникс", lambda: heart(GOLD, ORANGE), "", 0, 650, "jewelry", "A"),
    ("cy_art_chip", "Нейрочип «Оракул»", J, 1, 1, "Amulet", False, False, "оракул", lambda: chip_implant((30, 24, 10), CYAN, GOLD), "", 0, 600, "jewelry", "A"),
    ("cy_art_shield", "Щит «Эгида»", SH, 2, 2, "Hand2", False, False, "эгид", lambda: projector(GOLD, ORANGE), "", 3, 550, "shields", "A"),
]


def main():
    out = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    ids = [c[0] for c in CATALOG]
    assert len(ids) == len(set(ids)), "дублирующиеся id"
    rows = (len(CATALOG) + COLS - 1) // COLS
    atlas = Image.new("RGBA", (COLS * TILE, rows * TILE), (0, 0, 0, 0))
    items, css = [], [
        "/* Пиксельные иконки сеттинга «Киберпанк» (сгенерировано tools/sprites/build_cyber_items.py — не править руками).",
        "   Рисуются кодом (tools/sprites/pixelkit.py). Позиции в процентах — .pix можно растянуть до любого размера. */",
    ]
    tiles = []
    for n, (iid, name, cat, w, h, slot, two, diag, kw, draw, dmg, armor, price, trade, flags) in enumerate(CATALOG):
        tile = draw().finish()
        tiles.append((iid, name, tile))
        col, row = n % COLS, n // COLS
        atlas.paste(tile, (col * TILE, row * TILE))
        x = 0 if COLS == 1 else col * 100 / (COLS - 1)
        y = 0 if rows == 1 else row * 100 / (rows - 1)
        css.append(f".pix-{iid} {{ background-image: url('../sprites/cyber-items.png'); background-size: {COLS * 100}% {rows * 100}%; "
                   f"background-position: {x:.4g}% {y:.4g}%; }}")
        entry = {"id": iid, "name": name, "cat": cat, "w": w, "h": h, "slot": slot, "twoHanded": two, "art": "A" in flags,
                 "diag": diag, "kw": kw.split(), "genre": "cyberpunk", "price": price, "trade": trade}
        if dmg:
            entry["damage"] = dmg
        if armor:
            entry["armor"] = armor
        if "R" in flags:
            entry["ranged"] = True
        if "F" in flags:
            entry["finesse"] = True
        if "H" in flags:
            entry["weight"] = "heavy"
        if "M" in flags:
            entry["weight"] = "medium"
        if "S" in flags:
            entry["storyOnly"] = True
        items.append(entry)

    sprites = os.path.join(ROOT, "wwwroot", "sprites")
    atlas.save(os.path.join(sprites, "cyber-items.png"))
    with open(os.path.join(sprites, "cyber-items.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"cols": COLS, "rows": rows, "items": items}, f, ensure_ascii=False, indent=1)
    with open(os.path.join(ROOT, "wwwroot", "css", "pixel-cyber.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(css) + "\n")
    print(f"{len(items)} предметов, атлас {COLS}x{rows}")

    if out:
        # Превью: тайлы ×3 на тёмном фоне с подписями — для проверки глазами.
        k, cw, ch = 3, 150, 118
        per = 10
        prow = (len(tiles) + per - 1) // per
        sheet = Image.new("RGB", (per * cw, prow * ch), (18, 20, 28))
        dr = ImageDraw.Draw(sheet)
        for n, (iid, name, tile) in enumerate(tiles):
            x, y = (n % per) * cw, (n // per) * ch
            big = upscale(tile, k)
            sheet.paste(big, (x + (cw - big.width) // 2, y + 4), big)
            dr.text((x + 4, y + 102), iid[3:][:22], fill=(170, 180, 200))
        sheet.save(out)


if __name__ == "__main__":
    main()
