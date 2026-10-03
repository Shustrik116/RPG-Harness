"""
Каталог предметов сеттинга «Современность»: иконки рисуются кодом (pixelkit) в приземлённой палитре —
воронёный металл, чёрный полимер, дерево, олива и хаки, без неона.

Результат:
  wwwroot/sprites/modern-items.png  — атлас 32x32-тайлов
  wwwroot/sprites/modern-items.json — каталог с уроном/бронёй/ценой/категорией торговли (как cyber-items.json)
  wwwroot/css/pixel-modern.css      — классы .pix-md_<id>

id и порядок записей — только дописывать (id сохраняются в кампаниях, позиция в атласе = порядок в CATALOG).

Запуск: python tools/sprites/build_modern_items.py [--preview <png>]
"""
import json
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import *  # noqa: E402,F403
import build_cyber_items as cy  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TILE = 32
COLS = 16

# ---- Приземлённая палитра ----
BLUED = (40, 42, 48)        # воронёная сталь
POLY = (30, 31, 34)         # чёрный полимер
WOOD = (120, 78, 44)
WOOD_D = (86, 54, 30)
OLV = (86, 92, 58)
TAN = (176, 150, 104)
KHAKI = (128, 116, 84)
DENIM = (52, 70, 104)
BROWN = (96, 60, 40)
GRAYC = (98, 100, 104)
HL = (150, 154, 160)        # блик вместо неона
ORANGE_S = (220, 110, 30)
RED_S = (176, 40, 40)
PAPER = (226, 220, 200)
GOLD_S = (210, 168, 60)
SILVER = (196, 200, 206)


def I(angle=0):
    return cy.I(angle)


# ======================= Оружие =======================

def pistol(body=POLY, kind="std"):
    return cy.pistol(body, HL, kind)


def smg(body=POLY):
    return cy.smg(body, HL)


def rifle(body=BLUED, kind="assault", stock=POLY):
    i = cy.rifle(body, HL, kind)
    return i


def ak():
    """Автомат: деревянное цевьё и приклад, изогнутый магазин."""
    i = I(-45)
    i.poly([(-20, -2), (-12, -3), (-12, 3), (-20, 4)], WOOD)
    i.rect(-12, -3, 4, 2, BLUED)
    i.rect(4, -2, 11, 1, WOOD)
    i.rect(11, -1, 20, 0, STEEL)
    i.rect(13, -3, 14, -1, BLUED)
    i.poly([(-3, 2), (1, 2), (4, 7), (3, 10), (-1, 9)], BLUED)
    i.poly([(-9, 2), (-6, 2), (-7, 6), (-10, 6)], WOOD_D)
    i.line(-10, -1, 2, -1, HL)
    return i


def hunting(body=WOOD):
    i = I(-45)
    i.poly([(-20, -2), (-10, -2), (-10, 3), (-20, 5)], body)
    i.rect(-10, -2, 0, 1, BLUED)
    i.rect(0, -2, 20, -1, STEEL)
    i.rect(0, 0, 14, 1, STEEL)
    i.rect(-4, 1, 6, 2, body)
    i.line(-8, 2, -6, 4, BLUED)
    return i


def sawn():
    i = I(-45)
    i.poly([(-11, -2), (-5, -2), (-5, 3), (-12, 4)], WOOD)
    i.rect(-5, -2, 3, 1, BLUED)
    i.rect(3, -2, 11, -1, STEEL)
    i.rect(3, 0, 11, 1, STEEL)
    return i


def mg():
    i = I(-45)
    i.poly([(-20, -2), (-12, -3), (-12, 3), (-20, 4)], POLY)
    i.rect(-12, -4, 6, 2, BLUED)
    i.rect(6, -2, 20, 0, STEEL)
    i.rect(8, -4, 12, -2, BLUED)
    i.rect(-6, 2, 2, 7, OLV)
    i.line(14, 1, 11, 7, BLUED); i.line(14, 1, 17, 7, BLUED)
    i.poly([(-10, 2), (-7, 2), (-8, 6), (-11, 6)], POLY)
    return i


def launcher():
    return cy.launcher(OLV, TAN)


def knife(handle=POLY, steel=STEEL):
    return cy.knife(steel, None, handle)


def machete():
    i = I(-45)
    i.poly([(-3, -2), (12, -3), (16, -1), (14, 2), (-3, 2)], STEEL)
    i.line(-2, 2, 13, 2, HL)
    i.rect(-12, -1, -3, 1, WOOD_D)
    return i


def fire_axe():
    i = I(-45)
    i.rect(-18, -1, 14, 1, (200, 40, 40))
    i.rect(-18, -1, -12, 1, POLY)
    i.poly([(8, -1), (12, -8), (17, -8), (16, -1)], STEEL)
    i.poly([(8, 1), (13, 1), (12, 5), (9, 5)], STEEL)
    i.line(12, -8, 17, -8, HL)
    return i


def crowbar():
    i = I(-45)
    i.rect(-16, -1, 14, 1, (60, 60, 64))
    i.poly([(14, -1), (18, -4), (19, -2), (16, 1)], (60, 60, 64))
    i.poly([(-16, -1), (-19, 2), (-18, 3), (-15, 1)], (60, 60, 64))
    i.line(-12, -1, 10, -1, HL)
    return i


def bat():
    i = I(-45)
    i.poly([(-16, -1), (2, -1), (16, -3), (17, 0), (16, 3), (2, 1), (-16, 1)], (176, 130, 80))
    i.rect(-16, -1, -9, 1, POLY)
    return i


def police_baton():
    i = I(-45)
    i.rect(-16, -1, 15, 1, POLY)
    i.rect(-10, -4, -8, 0, POLY)
    i.line(-6, -1, 13, -1, (70, 70, 76))
    return i


def taser():
    i = I()
    i.rect(6, 11, 25, 17, (230, 200, 30))
    i.rect(25, 12, 28, 16, POLY)
    i.poly([(9, 17), (14, 17), (13, 25), (8, 25)], POLY)
    i.px(27, 13, (120, 180, 255)); i.px(27, 15, (120, 180, 255))
    i.line(8, 13, 22, 13, POLY)
    return i


def laptop():
    i = I()
    i.poly([(5, 6), (27, 6), (27, 20), (5, 20)], (54, 56, 62))
    i.rect(7, 8, 25, 18, (20, 30, 40))
    for y in (10, 12, 14):
        i.line(9, y, 9 + (y * 3) % 12, y, (90, 200, 120))
    i.poly([(3, 21), (29, 21), (31, 26), (1, 26)], (80, 82, 88))
    for x in range(5, 28, 3):
        i.px(x, 23, (50, 52, 56))
    return i


def drone_ctrl():
    i = I()
    i.poly([(4, 14), (28, 14), (30, 25), (22, 27), (18, 23), (14, 23), (10, 27), (2, 25)], (60, 62, 66))
    i.ell(7, 16, 12, 21, POLY); i.ell(20, 16, 25, 21, POLY)
    i.rect(12, 6, 20, 13, (40, 44, 50)); i.rect(13, 7, 19, 12, (90, 140, 180))
    i.line(26, 14, 28, 6, STEEL)
    return i


def knuckles():
    return cy.knuckles(SILVER, HL)


# ======================= Одежда и броня =======================

def jacket(col=BROWN):
    return cy.jacket(col, HL)


def hoodie(col=(84, 86, 92)):
    return cy.hoodie(col, (220, 220, 220))


def suit(col=(34, 36, 44)):
    return cy.suit(col, (120, 30, 40))


def vest_hidden():
    i = cy.vest(POLY, (50, 50, 54), POLY)
    return i


def plate_carrier(col=OLV):
    i = I()
    cy.torso(i, col, None, sleeves=False)
    for x in (9, 14, 19):
        i.rect(x, 17, x + 4, 23, shade(col, -.25))
        i.line(x, 18, x + 4, 18, shade(col, .2))
    i.rect(10, 9, 22, 15, shade(col, -.15))
    i.rect(13, 4, 19, 7, shade(col, -.3))
    return i


def police_vest():
    i = I()
    cy.torso(i, (30, 36, 60), None, sleeves=False)
    i.rect(10, 10, 22, 22, (36, 44, 72))
    i.rect(12, 13, 20, 15, (230, 230, 230))
    i.poly([(18, 7), (21, 7), (21, 10), (19, 11)], GOLD_S)
    return i


def heavy_armor():
    i = I()
    cy.torso(i, (44, 48, 40), None)
    i.rect(3, 7, 10, 14, (70, 74, 62)); i.rect(22, 7, 29, 14, (70, 74, 62))
    i.rect(10, 9, 22, 17, (70, 74, 62))
    i.rect(10, 19, 22, 26, shade((70, 74, 62), -.3))
    for y in (21, 24):
        i.line(11, y, 21, y, POLY)
    return i


def coat(col=(92, 70, 50)):
    i = I()
    i.poly([(8, 5), (12, 3), (20, 3), (24, 5), (27, 30), (5, 30)], col)
    i.poly([(11, 3), (14, 3), (14, 12), (10, 8)], shade(col, .2))
    i.poly([(21, 3), (18, 3), (18, 12), (22, 8)], shade(col, .2))
    i.rect(15, 6, 17, 30, shade(col, -.35))
    for y in (12, 17, 22):
        i.px(13, y, POLY); i.px(19, y, POLY)
    i.rect(6, 18, 26, 19, shade(col, -.2))
    return i


def army_helmet():
    i = I()
    i.ell(5, 6, 27, 26, OLV)
    i.clear(4, 18, 28, 28)
    i.rect(4, 17, 28, 19, shade(OLV, -.25))
    i.line(8, 12, 24, 12, shade(OLV, .15))
    i.rect(9, 9, 13, 11, POLY); i.rect(19, 9, 23, 11, POLY)
    return i


def balaclava():
    i = I()
    i.ell(8, 4, 24, 28, POLY)
    i.rect(10, 12, 22, 16, SKIN)
    i.px(13, 14, (20, 20, 20)); i.px(19, 14, (20, 20, 20))
    i.ell(14, 20, 18, 23, (10, 10, 10))
    return i


def cap(col=(40, 44, 54)):
    return cy.helmet(col, HL, "cap")


def sunglasses():
    i = I()
    i.rect(4, 13, 28, 14, (20, 20, 22))
    i.rect(6, 14, 14, 19, (30, 34, 40)); i.rect(18, 14, 26, 19, (30, 34, 40))
    i.line(7, 15, 9, 15, HL); i.line(19, 15, 21, 15, HL)
    return i


def gloves():
    return cy.gloves(POLY, HL)


def boots(col=POLY):
    return cy.boots(col, (60, 60, 60))


def sneakers():
    i = I()
    i.poly([(6, 14), (16, 12), (22, 16), (28, 20), (28, 25), (5, 25)], (230, 230, 230))
    i.rect(5, 25, 28, 27, (60, 60, 60))
    i.line(10, 16, 18, 18, (200, 40, 40)); i.line(9, 19, 22, 21, (200, 40, 40))
    return i


def rig(col=OLV):
    return cy.belt(col, TAN)


def holster():
    i = I()
    i.rect(2, 12, 30, 16, BROWN)
    i.rect(14, 11, 18, 17, SILVER)
    i.poly([(20, 14), (27, 14), (26, 27), (21, 27)], shade(BROWN, -.2))
    i.rect(21, 12, 25, 16, POLY)
    return i


def riot_shield():
    i = I()
    i.rect(7, 3, 25, 29, (150, 170, 190))
    i.rect(9, 5, 23, 27, (170, 190, 210))
    i.rect(9, 8, 23, 11, POLY)
    i.line(10, 9, 22, 9, (230, 230, 230))
    i.line(11, 14, 14, 24, (230, 240, 250))
    return i


def ballistic():
    return cy.ballistic((52, 54, 58), (120, 160, 200), True)


# ======================= Аксессуары =======================

def dogtag():
    i = I()
    for x in range(8, 24, 2):
        i.px(x, 4 + abs(16 - x) // 2, SILVER)
    i.poly([(10, 12), (22, 12), (23, 15), (23, 25), (22, 28), (10, 28), (9, 25), (9, 15)], SILVER)
    for y in (16, 19, 22):
        i.line(12, y, 20, y, GRAYC)
    return i


def cross():
    i = I()
    for x in range(8, 24, 2):
        i.px(x, 3 + abs(16 - x) // 2, GOLD_S)
    i.rect(14, 10, 18, 29, GOLD_S)
    i.rect(9, 14, 23, 18, GOLD_S)
    return i


def chain():
    i = I()
    for k in range(18):
        import math as _m
        a = _m.pi * (k / 17)
        x, y = 16 + 11 * _m.cos(a), 8 + 16 * _m.sin(a)
        i.ell(x - 2, y - 2, x + 2, y + 2, None, outline=GOLD_S)
    return i


def pendant():
    i = I()
    i.line(8, 4, 16, 14, SILVER); i.line(24, 4, 16, 14, SILVER)
    i.ell(10, 13, 22, 27, SILVER)
    i.ell(13, 16, 19, 24, (40, 120, 200))
    return i


def watch(col=SILVER, face=(20, 20, 26)):
    i = I()
    i.rect(12, 2, 20, 30, (50, 40, 34))
    i.ell(8, 8, 24, 24, col)
    i.ell(10, 10, 22, 22, face)
    i.line(16, 16, 16, 12, (230, 230, 230)); i.line(16, 16, 19, 17, (230, 230, 230))
    return i


def ring_signet():
    i = I()
    i.ell(8, 12, 24, 28, None, outline=GOLD_S)
    i.ell(9, 13, 23, 27, None, outline=shade(GOLD_S, -.3))
    i.rect(11, 6, 21, 14, GOLD_S)
    i.rect(13, 8, 19, 12, (30, 30, 30))
    return i


def bracelet():
    i = I()
    i.ell(5, 8, 27, 24, None, outline=SILVER)
    i.ell(6, 9, 26, 23, None, outline=shade(SILVER, -.3))
    for x in (9, 15, 21):
        i.rect(x, 7, x + 2, 9, SILVER)
    return i


def fitness():
    i = I()
    i.rect(12, 2, 20, 30, (30, 30, 34))
    i.rect(10, 10, 22, 22, (20, 20, 24))
    i.rect(12, 12, 20, 20, (40, 120, 90))
    i.line(13, 16, 15, 14, (180, 240, 200)); i.line(15, 14, 17, 18, (180, 240, 200)); i.line(17, 18, 19, 15, (180, 240, 200))
    return i


# ======================= Расходники и мелочь =======================

def bandage():
    i = I()
    i.ell(5, 9, 21, 25, (236, 236, 228))
    i.ell(10, 14, 16, 20, (200, 200, 190))
    i.poly([(18, 10), (28, 14), (27, 18), (19, 16)], (236, 236, 228))
    i.rect(22, 20, 28, 28, (230, 230, 230)); i.rect(22, 22, 28, 23, RED_S)
    return i


def pills(cap_col=(240, 240, 240), body=(220, 140, 40)):
    i = I()
    i.rect(10, 8, 22, 28, body)
    i.rect(9, 4, 23, 8, cap_col)
    i.rect(11, 14, 21, 22, PAPER)
    i.line(13, 17, 19, 17, (100, 100, 100)); i.line(13, 19, 17, 19, (100, 100, 100))
    return i


def syringe(liquid=(230, 200, 40)):
    return cy.injector(liquid, (220, 224, 230))


def energy_can():
    return cy.can((30, 30, 34), (90, 220, 70))


def frag():
    return cy.grenade(OLV, (40, 40, 40))


def flashbang():
    i = cy.smoke()
    return i


def smoke_grenade():
    i = I()
    i.rect(10, 8, 22, 27, (70, 90, 70))
    i.rect(10, 12, 22, 14, POLY)
    i.rect(13, 4, 19, 8, STEEL)
    i.line(12, 20, 20, 20, (230, 230, 230))
    return i


def molotov():
    i = I()
    i.rect(13, 6, 19, 12, (60, 120, 60))
    i.poly([(13, 12), (19, 12), (22, 16), (22, 28), (10, 28), (10, 16)], (60, 120, 60))
    i.rect(10, 18, 22, 26, (180, 130, 40))
    i.poly([(14, 2), (18, 2), (17, 7), (15, 7)], (230, 220, 200))
    i.poly([(15, 0), (18, -1), (17, 3)], ORANGE_S)
    i.glow(16, 1, ORANGE_S, 3)
    return i


def pepper():
    i = I()
    i.rect(12, 8, 20, 28, (30, 30, 34))
    i.rect(12, 14, 20, 20, (200, 40, 40))
    i.rect(13, 4, 19, 8, POLY)
    i.rect(18, 5, 21, 6, POLY)
    return i


def flash_drive():
    i = I()
    i.rect(8, 10, 24, 22, (40, 70, 140))
    i.rect(24, 12, 29, 20, STEEL)
    i.rect(26, 14, 27, 15, POLY); i.rect(26, 17, 27, 18, POLY)
    i.ell(10, 14, 13, 17, None, outline=SILVER)
    return i


def docs():
    i = I()
    i.rect(5, 6, 27, 26, (200, 170, 100))
    i.poly([(5, 6), (13, 6), (15, 4), (5, 4)], (200, 170, 100))
    i.rect(7, 9, 25, 24, PAPER)
    for y in (12, 15, 18, 21):
        i.line(9, y, 22, y, (120, 120, 120))
    i.ell(18, 17, 24, 23, None, outline=RED_S)
    return i


def paper_map():
    i = I()
    i.poly([(3, 7), (11, 5), (20, 8), (29, 6), (29, 25), (20, 27), (11, 24), (3, 26)], (220, 210, 170))
    i.line(11, 5, 11, 24, (170, 160, 120)); i.line(20, 8, 20, 27, (170, 160, 120))
    i.line(5, 18, 14, 12, (60, 120, 200)); i.line(14, 12, 26, 16, (60, 120, 200))
    i.px(22, 15, RED_S); i.px(23, 15, RED_S)
    return i


def notebook():
    i = I()
    i.rect(7, 4, 25, 28, (40, 40, 44))
    i.rect(9, 4, 10, 28, (80, 80, 84))
    i.rect(22, 4, 23, 28, (180, 40, 40))
    return i


def manual():
    i = I()
    i.rect(6, 5, 26, 27, (40, 80, 140))
    i.rect(8, 7, 24, 12, PAPER)
    i.line(9, 9, 22, 9, (60, 60, 60))
    return i


def case_file():
    i = docs()
    i.rect(8, 6, 16, 8, RED_S)
    return i


def burger():
    return cy.burger()


def shawarma():
    i = I(-45)
    i.poly([(-12, -4), (8, -4), (12, 0), (8, 4), (-12, 4)], (230, 200, 150))
    i.rect(-12, -4, -4, 4, (240, 240, 240))
    i.line(-2, -2, 8, -2, (90, 160, 60)); i.line(-2, 1, 8, 1, (180, 90, 50))
    return i


def coffee():
    return cy.cup()


def soda():
    return cy.can((180, 30, 40), (240, 240, 240))


def water():
    return cy.bottle((90, 150, 210), (240, 240, 240))


def beer():
    return cy.bottle()


def chips():
    i = I()
    i.poly([(8, 4), (24, 4), (26, 28), (6, 28)], (230, 180, 40))
    i.poly([(8, 4), (24, 4), (24, 7), (8, 7)], (200, 40, 40))
    i.ell(11, 13, 21, 22, (240, 210, 120))
    return i


def mre():
    return cy.ration((120, 110, 80), (90, 80, 50))


def smartphone():
    i = I()
    i.rect(10, 3, 22, 29, (24, 24, 28))
    i.rect(11, 5, 21, 26, (40, 70, 110))
    i.rect(13, 8, 19, 10, (180, 210, 240))
    i.px(16, 28, (80, 80, 84))
    return i


def burner():
    i = I()
    i.rect(10, 4, 22, 29, (60, 60, 64))
    i.rect(11, 6, 21, 13, (110, 140, 110))
    for y in range(16, 27, 3):
        for x in (12, 15, 18):
            i.rect(x, y, x + 1, y + 1, (30, 30, 30))
    return i


def car_keys():
    i = I()
    i.rect(5, 7, 15, 20, (30, 30, 34))
    i.ell(7, 9, 13, 15, (60, 60, 64))
    i.px(10, 12, RED_S)
    i.ell(14, 10, 20, 16, None, outline=SILVER)
    i.rect(19, 12, 28, 14, SILVER)
    for x in (22, 25):
        i.rect(x, 14, x + 1, 16, SILVER)
    return i


def keychain():
    i = I()
    i.ell(6, 4, 16, 14, None, outline=SILVER)
    for k, (x, y) in enumerate(((14, 14), (18, 12), (11, 16))):
        i.line(x, y, x + 4 + k * 2, y + 12, (200, 170, 80), 2)
    return i


def lottery():
    i = I()
    i.rect(5, 8, 27, 24, (240, 230, 160))
    i.rect(8, 11, 24, 18, (180, 180, 180))
    i.rect(8, 11, 14, 18, (240, 230, 160))
    i.px(10, 14, RED_S); i.px(12, 15, RED_S)
    return i


def photo():
    return cy.photo()


def cigs():
    return cy.cigs((210, 210, 210))


def lighter():
    return cy.lighter()


def flashlight():
    return cy.flashlight()


def multitool():
    return cy.multitool()


def lockpicks():
    i = I()
    i.rect(6, 18, 26, 24, BROWN)
    for k, x in enumerate((9, 13, 17, 21)):
        i.line(x, 18, x + 2, 4 + k, STEEL)
        i.px(x + 2, 4 + k, HL)
    return i


def radio():
    i = I()
    i.rect(10, 8, 22, 29, (30, 30, 34))
    i.rect(17, 1, 19, 8, POLY)
    i.rect(12, 11, 20, 16, (90, 120, 90))
    for y in (19, 22, 25):
        i.line(12, y, 20, y, (60, 60, 64))
    i.px(11, 9, RED_S)
    return i


def binoculars():
    return cy.binoculars(POLY, (60, 90, 120))


def rope():
    i = I()
    for r in (12, 9, 6):
        i.ell(16 - r, 16 - r, 16 + r, 16 + r, None, outline=(170, 140, 90))
        i.ell(17 - r, 17 - r, 15 + r, 15 + r, None, outline=(130, 100, 60))
    return i


def toolbox():
    return cy.toolbox((40, 90, 160))


def handcuffs():
    i = I()
    i.ell(3, 10, 15, 22, None, outline=SILVER)
    i.ell(4, 11, 14, 21, None, outline=shade(SILVER, -.3))
    i.ell(17, 10, 29, 22, None, outline=SILVER)
    i.ell(18, 11, 28, 21, None, outline=shade(SILVER, -.3))
    i.line(14, 16, 18, 16, SILVER, 2)
    return i


def canister():
    i = I()
    i.rect(6, 8, 26, 29, (180, 30, 30))
    i.rect(9, 4, 16, 8, (180, 30, 30))
    i.rect(18, 5, 22, 8, POLY)
    i.line(8, 11, 24, 26, shade((180, 30, 30), -.3)); i.line(24, 11, 8, 26, shade((180, 30, 30), -.3))
    return i


def sport_bag():
    return cy.bag((40, 44, 70), (220, 220, 220))


def jewelry_pile():
    i = I()
    i.ell(4, 16, 28, 28, (120, 80, 40))
    for x, y, c in ((9, 14, GOLD_S), (14, 12, SILVER), (19, 15, GOLD_S), (12, 18, (200, 40, 60)), (20, 19, (60, 140, 220))):
        i.ell(x - 3, y - 3, x + 3, y + 3, c)
    return i


def diamond():
    i = I()
    i.poly([(8, 11), (12, 6), (20, 6), (24, 11), (16, 27)], (190, 230, 250))
    i.line(8, 11, 24, 11, WHITE); i.line(12, 6, 14, 11, WHITE); i.line(20, 6, 18, 11, WHITE)
    i.line(14, 11, 16, 27, (150, 200, 230)); i.line(18, 11, 16, 27, (150, 200, 230))
    return i


def painting():
    i = I()
    i.rect(3, 5, 29, 27, (170, 130, 50))
    i.rect(6, 8, 26, 24, (60, 90, 120))
    i.rect(6, 18, 26, 24, (70, 110, 60))
    i.ell(18, 10, 23, 15, (230, 200, 90))
    return i


def cigar_box():
    i = I()
    i.rect(4, 10, 28, 26, (120, 70, 40))
    i.rect(4, 10, 28, 13, (150, 90, 50))
    i.rect(11, 16, 21, 21, GOLD_S)
    return i


def parts():
    i = I()
    i.ell(4, 6, 18, 20, STEEL)
    i.ell(8, 10, 14, 16, (60, 60, 64))
    for x in range(18, 28, 3):
        i.line(x, 16, x + 2, 26, (150, 150, 150))
    i.rect(16, 24, 29, 27, (100, 100, 104))
    return i


def car_battery():
    i = I()
    i.rect(4, 10, 28, 27, (30, 30, 34))
    i.rect(7, 6, 11, 10, RED_S); i.rect(21, 6, 25, 10, (60, 60, 64))
    i.rect(8, 15, 24, 19, (230, 200, 40))
    return i


def ammo_box():
    i = I()
    i.rect(4, 10, 28, 26, OLV)
    i.rect(4, 10, 28, 13, shade(OLV, .2))
    i.rect(12, 7, 20, 10, POLY)
    i.rect(9, 17, 23, 21, (230, 200, 40))
    return i


def hide():
    i = I()
    i.poly([(6, 8), (12, 4), (20, 4), (26, 8), (28, 14), (24, 18), (27, 26), (20, 28), (16, 25), (12, 28), (5, 26), (8, 18), (4, 14)], (140, 100, 62))
    i.poly([(12, 9), (20, 9), (22, 18), (16, 22), (10, 18)], (164, 124, 82))
    return i


def meat():
    i = I()
    i.ell(5, 9, 25, 25, (170, 50, 50))
    i.ell(9, 12, 19, 20, (220, 140, 140))
    i.rect(23, 14, 29, 18, (240, 230, 210))
    return i


def fang():
    i = I()
    for x in (9, 17):
        i.poly([(x, 6), (x + 6, 6), (x + 3, 26)], (240, 236, 220))
    return i


def chem():
    return cy.chem((230, 200, 30))


def electronics():
    return cy.circuit()


# ======================= Каталог =======================
W, SH, A, J, P, SC, B, FO, V, MAT, T, MI = "weapon", "shield", "armor", "jewelry", "potion", "scroll", "book", "food", "valuable", "material", "tool", "misc"
CATALOG = [
    # ---- Огнестрел ----
    ("md_pistol", "Пистолет 9 мм", W, 2, 2, "Hand1", False, False, "пистолет ствол пушк глок макаров пм", lambda: pistol(), "1d6 огнестрельный", 0, 15, "ranged", "R"),
    ("md_compact", "Компактный пистолет", W, 2, 2, "Hand1", False, False, "компактн карманн", lambda: pistol(BLUED), "1d6 огнестрельный", 0, 12, "ranged", "R"),
    ("md_revolver", "Револьвер", W, 2, 2, "Hand1", False, False, "револьвер наган", lambda: pistol(SILVER, "revolver"), "1d8 огнестрельный", 0, 25, "ranged", "R"),
    ("md_magnum", "Тяжёлый пистолет", W, 2, 2, "Hand1", False, False, "тяжёл пистолет магнум кольт", lambda: pistol(BLUED, "heavy"), "1d8 огнестрельный", 0, 30, "ranged", "R"),
    ("md_smg", "Пистолет-пулемёт", W, 2, 2, "Hand1", False, False, "пистолет-пулем пп узи", lambda: smg(), "1d6 огнестрельный", 0, 35, "ranged", "R"),
    ("md_rifle", "Автомат", W, 2, 4, "Hand1", True, True, "автомат калаш ак штурмов винтовк", lambda: ak(), "1d10 огнестрельный", 0, 60, "ranged", "R"),
    ("md_carbine", "Карабин", W, 2, 4, "Hand1", True, True, "карабин", lambda: rifle(POLY), "1d8 огнестрельный", 0, 45, "ranged", "R"),
    ("md_shotgun", "Помповое ружьё", W, 2, 4, "Hand1", True, True, "помпов дробовик ружь", lambda: rifle(POLY, "shotgun"), "1d10 огнестрельный", 0, 40, "ranged", "R"),
    ("md_hunting", "Охотничье ружьё", W, 2, 4, "Hand1", True, True, "охотнич двустволк", lambda: hunting(), "1d10 огнестрельный", 0, 30, "ranged", "R"),
    ("md_sawn", "Обрез", W, 1, 3, "Hand1", False, True, "обрез", lambda: sawn(), "1d8 огнестрельный", 0, 15, "ranged", "R"),
    ("md_sniper", "Снайперская винтовка", W, 2, 4, "Hand1", True, True, "снайпер винтовк", lambda: rifle(OLV, "sniper"), "1d12 огнестрельный", 0, 120, "ranged", "R"),
    ("md_mg", "Ручной пулемёт", W, 2, 4, "Hand1", True, True, "пулемёт пулемет", lambda: mg(), "2d6 огнестрельный", 0, 200, "polearms", "R"),
    ("md_launcher", "Гранатомёт", W, 2, 4, "Hand1", True, True, "гранатом рпг", lambda: launcher(), "2d6 взрыв", 0, 220, "polearms", "R"),
    # ---- Холодное и подручное ----
    ("md_knife", "Нож", W, 1, 2, "Hand1", False, True, "нож ножик", lambda: knife(), "1d4 режущий", 0, 3, "blades", "F"),
    ("md_combat_knife", "Боевой нож", W, 1, 2, "Hand1", False, True, "боев нож", lambda: knife(OLV, SILVER), "1d4 режущий", 0, 10, "blades", "F"),
    ("md_machete", "Мачете", W, 1, 3, "Hand1", False, True, "мачете тесак", lambda: machete(), "1d8 режущий", 0, 8, "blades", ""),
    ("md_axe", "Пожарный топор", W, 2, 4, "Hand1", True, True, "топор пожарн", lambda: fire_axe(), "1d12 режущий", 0, 20, "weapons", ""),
    ("md_bat", "Бита", W, 1, 3, "Hand1", False, True, "бит бейсбол", lambda: bat(), "1d6 ударный", 0, 3, "weapons", ""),
    ("md_crowbar", "Монтировка", W, 1, 3, "Hand1", False, True, "монтировк фомк лом", lambda: crowbar(), "1d6 ударный", 0, 2, "weapons", ""),
    ("md_baton", "Полицейская дубинка", W, 1, 3, "Hand1", False, True, "дубинк тонфа", lambda: police_baton(), "1d6 ударный", 0, 5, "weapons", ""),
    ("md_knuckles", "Кастет", W, 1, 1, "Hand1", False, False, "кастет", lambda: knuckles(), "1d4 ударный", 0, 3, "weapons", ""),
    ("md_taser", "Электрошокер", W, 2, 2, "Hand1", False, False, "шокер тазер электрошок", lambda: taser(), "1d6 электричество", 0, 20, "weapons", ""),
    # ---- Техника ----
    ("md_laptop", "Ноутбук хакера", W, 2, 2, "Hand1", False, False, "ноутбук лэптоп компьютер", lambda: laptop(), "1d6 электричество", 0, 60, "magic", ""),
    ("md_drone_ctrl", "Квадрокоптер с пультом", W, 2, 2, "Hand1", False, False, "квадрокоптер дрон пульт", lambda: drone_ctrl(), "1d6 огнестрельный", 0, 80, "magic", "R"),
    # ---- Броня и одежда ----
    ("md_leather", "Кожаная куртка", A, 2, 3, "Body", False, False, "кожан куртк косух", lambda: jacket(), "", 1, 10, "light_armor", ""),
    ("md_hoodie", "Худи", A, 2, 3, "Body", False, False, "худи толстовк", lambda: hoodie(), "", 0, 3, "clothing", ""),
    ("md_suit", "Деловой костюм", A, 2, 3, "Body", False, False, "костюм пиджак", lambda: suit(), "", 0, 40, "clothing", ""),
    ("md_vest_hidden", "Скрытый бронежилет", A, 2, 3, "Body", False, False, "скрыт бронежилет", lambda: vest_hidden(), "", 3, 40, "light_armor", "M"),
    ("md_police_vest", "Полицейский бронежилет", A, 2, 3, "Body", False, False, "полицейск бронежилет", lambda: police_vest(), "", 4, 70, "heavy_armor", "M"),
    ("md_plate_carrier", "Плитник", A, 2, 3, "Body", False, False, "плитник плитоноск бронеплит", lambda: plate_carrier(), "", 5, 120, "heavy_armor", "M"),
    ("md_heavy_armor", "Штурмовой бронекостюм", A, 2, 3, "Body", False, False, "штурмов бронекостюм сапёр", lambda: heavy_armor(), "", 7, 300, "heavy_armor", "H"),
    ("md_coat", "Пальто", A, 2, 3, "Cloak", False, False, "пальто", lambda: coat(), "", 0, 25, "clothing", ""),
    ("md_raincoat", "Плащ", A, 2, 3, "Cloak", False, False, "плащ тренч дождевик", lambda: coat(TAN), "", 0, 10, "clothing", ""),
    ("md_helmet", "Армейская каска", A, 2, 2, "Helmet", False, False, "каска шлем", lambda: army_helmet(), "", 1, 20, "heavy_armor", ""),
    ("md_balaclava", "Балаклава", A, 2, 2, "Helmet", False, False, "балаклав маск", lambda: balaclava(), "", 0, 2, "clothing", ""),
    ("md_cap", "Кепка", A, 2, 2, "Helmet", False, False, "кепк бейсболк", lambda: cap(), "", 0, 1, "clothing", ""),
    ("md_sunglasses", "Солнцезащитные очки", A, 2, 2, "Helmet", False, False, "очки солнцезащит", lambda: sunglasses(), "", 0, 5, "clothing", ""),
    ("md_gloves", "Тактические перчатки", A, 2, 2, "Gloves", False, False, "перчатк", lambda: gloves(), "", 0, 4, "light_armor", ""),
    ("md_boots", "Берцы", A, 2, 2, "Boots", False, False, "берц ботинк сапог", lambda: boots(), "", 0, 8, "light_armor", ""),
    ("md_sneakers", "Кроссовки", A, 2, 2, "Boots", False, False, "кроссовк кеды", lambda: sneakers(), "", 0, 5, "clothing", ""),
    ("md_rig", "Разгрузка", A, 2, 1, "Belt", False, False, "разгрузк пояс", lambda: rig(), "", 0, 10, "light_armor", ""),
    ("md_holster", "Пояс с кобурой", A, 2, 1, "Belt", False, False, "кобур ремень", lambda: holster(), "", 0, 6, "light_armor", ""),
    # ---- Щиты ----
    ("md_riot_shield", "Полицейский щит", SH, 2, 3, "Hand2", False, False, "щит полицейск", lambda: riot_shield(), "", 2, 30, "shields", ""),
    ("md_ballistic", "Баллистический щит", SH, 2, 3, "Hand2", False, False, "баллистич щит", lambda: ballistic(), "", 3, 80, "shields", ""),
    # ---- Аксессуары ----
    ("md_dogtag", "Армейский жетон", J, 1, 1, "Amulet", False, False, "жетон", lambda: dogtag(), "", 0, 15, "jewelry", ""),
    ("md_cross", "Нательный крест", J, 1, 1, "Amulet", False, False, "крест крестик", lambda: cross(), "", 0, 20, "jewelry", ""),
    ("md_chain", "Золотая цепь", J, 1, 1, "Amulet", False, False, "цеп цепочк", lambda: chain(), "", 0, 60, "jewelry", ""),
    ("md_pendant", "Кулон", J, 1, 1, "Amulet", False, False, "кулон медальон", lambda: pendant(), "", 0, 40, "jewelry", ""),
    ("md_watch", "Часы", J, 1, 1, "Ring1", False, False, "часы", lambda: watch(), "", 0, 30, "jewelry", ""),
    ("md_watch_lux", "Дорогие часы", J, 1, 1, "Ring1", False, False, "дорог часы ролекс", lambda: watch(GOLD_S), "", 0, 120, "jewelry", ""),
    ("md_ring", "Перстень", J, 1, 1, "Ring1", False, False, "перстень печатк кольц", lambda: ring_signet(), "", 0, 50, "jewelry", ""),
    ("md_bracelet", "Браслет", J, 1, 1, "Ring1", False, False, "браслет", lambda: bracelet(), "", 0, 25, "jewelry", ""),
    ("md_fitness", "Фитнес-браслет", J, 1, 1, "Ring1", False, False, "фитнес трекер смарт-час", lambda: fitness(), "", 0, 20, "jewelry", ""),
    # ---- Лекарства ----
    ("md_bandage", "Бинты и обезболивающее", P, 1, 1, None, False, False, "бинт перевяз", lambda: bandage(), "", 0, 6, "potions", ""),
    ("md_medkit", "Аптечка", P, 2, 2, None, False, False, "аптечк", lambda: cy.medkit((236, 236, 230), RED_S), "", 0, 25, "potions", ""),
    ("md_army_medkit", "Армейская аптечка", P, 2, 2, None, False, False, "армейск аптечк", lambda: cy.medkit(OLV, (236, 236, 230)), "", 0, 60, "potions", ""),
    ("md_painkillers", "Обезболивающее", P, 1, 1, None, False, False, "обезбол таблетк", lambda: pills(), "", 0, 8, "potions", ""),
    ("md_energy", "Энергетик", P, 1, 1, None, False, False, "энергетик кофеин", lambda: energy_can(), "", 0, 7, "potions", ""),
    ("md_adrenaline", "Шприц адреналина", P, 1, 1, None, False, False, "адреналин шприц", lambda: syringe(), "", 0, 28, "potions", ""),
    ("md_antidote", "Противоядие", P, 1, 1, None, False, False, "противояд антидот", lambda: syringe((90, 200, 110)), "", 0, 8, "potions", ""),
    ("md_sedative", "Успокоительное", P, 1, 1, None, False, False, "успокоит седатив", lambda: pills((240, 240, 240), (90, 130, 200)), "", 0, 12, "potions", ""),
    # ---- Гранаты, документы, носители ----
    ("md_frag", "Граната", SC, 1, 1, None, False, False, "граната осколочн лимонк", lambda: frag(), "", 0, 15, "scrolls", ""),
    ("md_flashbang", "Светошумовая граната", SC, 1, 1, None, False, False, "светошум", lambda: flashbang(), "", 0, 12, "scrolls", ""),
    ("md_smoke", "Дымовая шашка", SC, 1, 1, None, False, False, "дымов шашк", lambda: smoke_grenade(), "", 0, 8, "scrolls", ""),
    ("md_molotov", "Коктейль Молотова", SC, 1, 1, None, False, False, "молотов зажигат бутылк", lambda: molotov(), "", 0, 5, "scrolls", ""),
    ("md_pepper", "Перцовый баллончик", SC, 1, 1, None, False, False, "перцов баллончик", lambda: pepper(), "", 0, 6, "scrolls", ""),
    ("md_flash_drive", "Флешка", SC, 1, 1, None, False, False, "флешк носител", lambda: flash_drive(), "", 0, 2, "scrolls", ""),
    ("md_docs", "Документы", SC, 1, 1, None, False, False, "документ бумаг договор паспорт", lambda: docs(), "", 0, 5, "scrolls", ""),
    ("md_map", "Карта местности", SC, 1, 1, None, False, False, "карт схем", lambda: paper_map(), "", 0, 3, "scrolls", ""),
    # ---- Записи ----
    ("md_notebook", "Блокнот с записями", B, 1, 1, None, False, False, "блокнот записн дневник", lambda: notebook(), "", 0, 5, "books", ""),
    ("md_manual", "Справочник", B, 1, 1, None, False, False, "справочник учебник руководств", lambda: manual(), "", 0, 15, "books", ""),
    ("md_case_file", "Папка с делом", B, 2, 2, None, False, False, "дело папк досье", lambda: case_file(), "", 0, 20, "books", ""),
    # ---- Еда ----
    ("md_burger", "Бургер", FO, 1, 1, None, False, False, "бургер гамбургер", lambda: burger(), "", 0, 2, "food", ""),
    ("md_shawarma", "Шаурма", FO, 1, 1, None, False, False, "шаурм шаверм", lambda: shawarma(), "", 0, 2, "food", ""),
    ("md_coffee", "Кофе", FO, 1, 1, None, False, False, "кофе", lambda: coffee(), "", 0, 1, "food", ""),
    ("md_soda", "Газировка", FO, 1, 1, None, False, False, "газиров кол", lambda: soda(), "", 0, 1, "food", ""),
    ("md_water", "Вода", FO, 1, 1, None, False, False, "вода", lambda: water(), "", 0, 1, "food", ""),
    ("md_beer", "Пиво", FO, 1, 1, None, False, False, "пив", lambda: beer(), "", 0, 2, "food", ""),
    ("md_chips", "Чипсы", FO, 1, 1, None, False, False, "чипс", lambda: chips(), "", 0, 1, "food", ""),
    ("md_mre", "Сухпаёк", FO, 1, 1, None, False, False, "сухпаёк сухпаек паёк ирп", lambda: mre(), "", 0, 3, "food", ""),
    # ---- Инструменты и гаджеты ----
    ("md_phone", "Смартфон", T, 1, 1, None, False, False, "смартфон телефон мобильн", lambda: smartphone(), "", 0, 15, "tools", ""),
    ("md_burner", "Одноразовый телефон", T, 1, 1, None, False, False, "одноразов кнопочн", lambda: burner(), "", 0, 5, "tools", ""),
    ("md_car_keys", "Ключи от машины", T, 1, 1, None, False, False, "ключи от машин автоключ брелок сигнализац", lambda: car_keys(), "", 0, 1, "tools", ""),
    ("md_keychain", "Связка ключей", T, 1, 1, None, False, False, "связк ключ", lambda: keychain(), "", 0, 1, "tools", ""),
    ("md_flashlight", "Фонарик", T, 1, 1, None, False, False, "фонар фонарик", lambda: flashlight(), "", 0, 3, "tools", ""),
    ("md_multitool", "Мультитул", T, 1, 1, None, False, False, "мультитул", lambda: multitool(), "", 0, 6, "tools", ""),
    ("md_lockpicks", "Отмычки", T, 1, 1, None, False, False, "отмычк", lambda: lockpicks(), "", 0, 15, "tools", ""),
    ("md_radio", "Рация", T, 1, 1, None, False, False, "рация рацию", lambda: radio(), "", 0, 20, "tools", ""),
    ("md_binoculars", "Бинокль", T, 1, 1, None, False, False, "бинокл", lambda: binoculars(), "", 0, 15, "tools", ""),
    ("md_rope", "Верёвка", T, 1, 1, None, False, False, "верёвк веревк трос", lambda: rope(), "", 0, 1, "tools", ""),
    ("md_lighter", "Зажигалка", T, 1, 1, None, False, False, "зажигалк", lambda: lighter(), "", 0, 1, "tools", ""),
    ("md_toolbox", "Ящик с инструментами", T, 2, 2, None, False, False, "ящик инструмент", lambda: toolbox(), "", 0, 12, "tools", ""),
    ("md_handcuffs", "Наручники", T, 1, 1, None, False, False, "наручник", lambda: handcuffs(), "", 0, 5, "tools", ""),
    ("md_canister", "Канистра бензина", T, 1, 1, None, False, False, "канистр бензин топлив", lambda: canister(), "", 0, 4, "tools", ""),
    ("md_bag", "Спортивная сумка", T, 2, 2, None, False, False, "сумк рюкзак", lambda: sport_bag(), "", 0, 2, "tools", ""),
    # ---- Ценности ----
    ("md_jewelry", "Ювелирные украшения", V, 1, 1, None, False, False, "ювелир украшен золот серёжк", lambda: jewelry_pile(), "", 0, 50, "gems", ""),
    ("md_diamond", "Бриллиант", V, 1, 1, None, False, False, "бриллиант алмаз камен", lambda: diamond(), "", 0, 150, "gems", ""),
    ("md_painting", "Картина", V, 2, 2, None, False, False, "картин полотн", lambda: painting(), "", 0, 120, "gems", ""),
    ("md_cigar_box", "Коробка сигар", V, 1, 1, None, False, False, "сигар", lambda: cigar_box(), "", 0, 30, "gems", ""),
    # ---- Мелочи ----
    ("md_cigs", "Сигареты", MI, 1, 1, None, False, False, "сигарет курев", lambda: cigs(), "", 0, 2, "curios", ""),
    ("md_photo", "Фотография", MI, 1, 1, None, False, False, "фото снимок", lambda: photo(), "", 0, 1, "curios", ""),
    ("md_lottery", "Лотерейный билет", MI, 1, 1, None, False, False, "лотер билет", lambda: lottery(), "", 0, 1, "curios", ""),
    # ---- Материалы ----
    ("md_parts", "Автозапчасти", MAT, 1, 1, None, False, False, "запчаст детал", lambda: parts(), "", 0, 8, "materials", ""),
    ("md_electronics", "Электроника", MAT, 1, 1, None, False, False, "электрон плат микросхем", lambda: electronics(), "", 0, 10, "materials", ""),
    ("md_battery", "Аккумулятор", MAT, 1, 1, None, False, False, "аккумулятор батаре", lambda: car_battery(), "", 0, 10, "materials", ""),
    ("md_chem", "Химикаты", MAT, 1, 1, None, False, False, "химикат реагент", lambda: chem(), "", 0, 8, "materials", ""),
    ("md_ammo_box", "Ящик патронов", MAT, 1, 1, None, False, False, "патрон боеприпас", lambda: ammo_box(), "", 0, 15, "materials", ""),
    ("md_hide", "Шкура", MAT, 1, 1, None, False, False, "шкур мех", lambda: hide(), "", 0, 6, "curios", ""),
    ("md_meat", "Мясо", MAT, 1, 1, None, False, False, "мяс", lambda: meat(), "", 0, 1, "food", ""),
    ("md_fang", "Клыки", MAT, 1, 1, None, False, False, "клык зуб", lambda: fang(), "", 0, 4, "curios", ""),
    # ---- Именные (★) ----
    ("md_art_pistol", "Пистолет «Последний аргумент»", W, 2, 2, "Hand1", False, False, "последн аргумент", lambda: pistol(GOLD_S, "heavy"), "1d8 огнестрельный", 0, 380, "ranged", "RA"),
    ("md_art_revolver", "Револьвер «Шериф»", W, 2, 2, "Hand1", False, False, "шериф", lambda: pistol((220, 220, 226), "revolver"), "1d8 огнестрельный", 0, 360, "ranged", "RA"),
    ("md_art_rifle", "Автомат «Ветеран»", W, 2, 4, "Hand1", True, True, "ветеран", lambda: ak(), "1d10 огнестрельный", 0, 450, "ranged", "RA"),
    ("md_art_knife", "Нож «Тайга»", W, 1, 2, "Hand1", False, True, "тайга", lambda: knife(WOOD, SILVER), "1d4 режущий", 0, 300, "blades", "FA"),
    ("md_art_sniper", "Винтовка «Тихий»", W, 2, 4, "Hand1", True, True, "тихий", lambda: rifle(TAN, "sniper"), "1d12 огнестрельный", 0, 520, "ranged", "RA"),
    ("md_art_watch", "Часы «Счастливые»", J, 1, 1, "Ring1", False, False, "счастлив", lambda: watch(GOLD_S, (30, 20, 10)), "", 0, 500, "jewelry", "A"),
    ("md_art_dogtag", "Жетон «Седьмой»", J, 1, 1, "Amulet", False, False, "седьмой", lambda: dogtag(), "", 0, 480, "jewelry", "A"),
    ("md_art_vest", "Бронежилет «Последний рубеж»", A, 2, 3, "Body", False, False, "последний рубеж", lambda: plate_carrier((60, 50, 40)), "", 6, 800, "heavy_armor", "MA"),
    ("md_art_coat", "Плащ «Нуар»", A, 2, 3, "Cloak", False, False, "нуар", lambda: coat((30, 30, 34)), "", 1, 600, "clothing", "A"),
]


def main():
    out = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    ids = [c[0] for c in CATALOG]
    assert len(ids) == len(set(ids)), "дублирующиеся id"
    rows = (len(CATALOG) + COLS - 1) // COLS
    atlas = Image.new("RGBA", (COLS * TILE, rows * TILE), (0, 0, 0, 0))
    items, css = [], [
        "/* Пиксельные иконки сеттинга «Современность» (сгенерировано tools/sprites/build_modern_items.py — не править руками).",
        "   Рисуются кодом (tools/sprites/pixelkit.py). Позиции в процентах — .pix можно растянуть до любого размера. */",
    ]
    tiles = []
    for n, (iid, name, cat, w, h, slot, two, diag, kw, draw, dmg, armor, price, trade, flags) in enumerate(CATALOG):
        tile = draw().finish()
        tiles.append((iid, tile))
        col, row = n % COLS, n // COLS
        atlas.paste(tile, (col * TILE, row * TILE))
        x = 0 if COLS == 1 else col * 100 / (COLS - 1)
        y = 0 if rows == 1 else row * 100 / (rows - 1)
        css.append(f".pix-{iid} {{ background-image: url('../sprites/modern-items.png'); background-size: {COLS * 100}% {rows * 100}%; "
                   f"background-position: {x:.4g}% {y:.4g}%; }}")
        entry = {"id": iid, "name": name, "cat": cat, "w": w, "h": h, "slot": slot, "twoHanded": two, "art": "A" in flags,
                 "diag": diag, "kw": kw.split(), "genre": "modern", "price": price, "trade": trade}
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
        items.append(entry)

    sprites = os.path.join(ROOT, "wwwroot", "sprites")
    atlas.save(os.path.join(sprites, "modern-items.png"))
    with open(os.path.join(sprites, "modern-items.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"cols": COLS, "rows": rows, "items": items}, f, ensure_ascii=False, indent=1)
    with open(os.path.join(ROOT, "wwwroot", "css", "pixel-modern.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(css) + "\n")
    print(f"{len(items)} items, atlas {COLS}x{rows}")

    if out:
        k, cw, ch, per = 3, 150, 118, 10
        prow = (len(tiles) + per - 1) // per
        sheet = Image.new("RGB", (per * cw, prow * ch), (26, 27, 30))
        dr = ImageDraw.Draw(sheet)
        for n, (iid, tile) in enumerate(tiles):
            x, y = (n % per) * cw, (n // per) * ch
            big = upscale(tile, k)
            sheet.paste(big, (x + (cw - big.width) // 2, y + 4), big)
            dr.text((x + 4, y + 102), iid[3:][:22], fill=(170, 180, 200))
        sheet.save(out)


if __name__ == "__main__":
    main()
