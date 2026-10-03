"""
Мини-набор для процедурного пиксель-арта киберпанка (иконки предметов, навыков, портреты).

Рисуем многоугольниками в локальных координатах (u — вдоль предмета, v — поперёк), чтобы одно и то же
оружие можно было положить горизонтально или по диагонали (как мечи DCSS — в сетке инвентаря их
разворачивают вертикально). После рисования — затенение кромок, тёмный контур и неоновое свечение.
"""
import math

from PIL import Image, ImageDraw

# ---- Палитра ----
OUT = (8, 9, 14, 255)
BLACK = (14, 15, 22)
DARK = (30, 33, 44)
METAL = (58, 64, 80)
METAL_L = (96, 106, 126)
STEEL = (150, 160, 178)
CHROME = (205, 214, 228)
GRIP = (24, 24, 31)
CYAN = (0, 236, 255)
MAGENTA = (255, 46, 214)
YELLOW = (246, 240, 0)
RED = (255, 44, 80)
GREEN = (60, 255, 140)
ORANGE = (255, 140, 20)
VIOLET = (164, 96, 255)
WHITE = (236, 248, 255)
GOLD = (232, 182, 52)
OLIVE = (82, 92, 58)
NAVY = (32, 40, 74)
LEATHER = (82, 44, 40)
CRIMSON = (150, 28, 52)
BONE = (214, 210, 196)
RUST = (140, 72, 40)
SKIN = (196, 140, 110)


def shade(c, k):
    """Светлее (k>0) или темнее (k<0) на долю k."""
    r, g, b = c[:3]
    if k >= 0:
        return (int(r + (255 - r) * k), int(g + (255 - g) * k), int(b + (255 - b) * k))
    k = -k
    return (int(r * (1 - k)), int(g * (1 - k)), int(b * (1 - k)))


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


class Icon:
    """Холст size×size. angle — поворот локальных осей (−45 — предмет по диагонали вверх-вправо)."""

    def __init__(self, size=32, angle=0, cx=None, cy=None):
        self.size = size
        self.img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
        self.neon = Image.new("L", (size, size), 0)
        self.nd = ImageDraw.Draw(self.neon)
        self.glows = []
        self.a = math.radians(angle)
        self.cx = size / 2 if cx is None else cx
        self.cy = size / 2 if cy is None else cy
        self.axis = angle == 0 and cx == 0 and cy == 0

    def T(self, u, v):
        if self.axis:
            return (u, v)
        ca, sa = math.cos(self.a), math.sin(self.a)
        return (self.cx + u * ca - v * sa, self.cy + u * sa + v * ca)

    def poly(self, pts, col, neon=False):
        P = [self.T(u, v) for u, v in pts]
        self.d.polygon(P, fill=tuple(col[:3]) + (255,))
        if neon:
            self.nd.polygon(P, fill=255)

    def rect(self, u0, v0, u1, v1, col, neon=False):
        if self.axis:
            self.d.rectangle([u0, v0, u1, v1], fill=tuple(col[:3]) + (255,))
            if neon:
                self.nd.rectangle([u0, v0, u1, v1], fill=255)
            return
        self.poly([(u0, v0), (u1, v0), (u1, v1), (u0, v1)], col, neon)

    def line(self, u0, v0, u1, v1, col, w=1, neon=False):
        P = [self.T(u0, v0), self.T(u1, v1)]
        self.d.line(P, fill=tuple(col[:3]) + (255,), width=w)
        if neon:
            self.nd.line(P, fill=255, width=w)

    def ell(self, u0, v0, u1, v1, col, neon=False, outline=None):
        box = [self.T(u0, v0), self.T(u1, v1)]
        box = [min(box[0][0], box[1][0]), min(box[0][1], box[1][1]), max(box[0][0], box[1][0]), max(box[0][1], box[1][1])]
        self.d.ellipse(box, fill=tuple(col[:3]) + (255,) if col else None,
                       outline=tuple(outline[:3]) + (255,) if outline else None)
        if neon:
            self.nd.ellipse(box, fill=255)

    def px(self, u, v, col, neon=False):
        x, y = self.T(u, v)
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.size and 0 <= y < self.size:
            self.img.putpixel((x, y), tuple(col[:3]) + (255,))
            if neon:
                self.neon.putpixel((x, y), 255)

    def clear(self, u0, v0, u1, v1):
        self.d.rectangle([u0, v0, u1, v1], fill=(0, 0, 0, 0))

    def glow(self, u, v, col, r=3):
        x, y = self.T(u, v)
        self.glows.append((x, y, col, r))

    def finish(self, outline=True, edge=True):
        img = self.img
        px = img.load()
        nm = self.neon.load()
        n = self.size
        if edge:
            src = img.copy().load()

            def opaque(x, y):
                return 0 <= x < n and 0 <= y < n and src[x, y][3] > 0

            for y in range(n):
                for x in range(n):
                    p = src[x, y]
                    if p[3] == 0 or nm[x, y]:
                        continue
                    k = 0
                    if not opaque(x, y - 1):
                        k += .32
                    if not opaque(x - 1, y):
                        k += .12
                    if not opaque(x, y + 1):
                        k -= .32
                    if not opaque(x + 1, y):
                        k -= .12
                    if k:
                        px[x, y] = shade(p, k) + (255,)
        if outline:
            src = img.copy().load()
            for y in range(n):
                for x in range(n):
                    if src[x, y][3]:
                        continue
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        xx, yy = x + dx, y + dy
                        if 0 <= xx < n and 0 <= yy < n and src[xx, yy][3]:
                            px[x, y] = OUT
                            break
        # Свечение: полупрозрачный ореол на пустых клетках вокруг источника.
        for gx, gy, col, r in self.glows:
            for y in range(int(gy - r - 1), int(gy + r + 2)):
                for x in range(int(gx - r - 1), int(gx + r + 2)):
                    if not (0 <= x < n and 0 <= y < n):
                        continue
                    dist = math.hypot(x - gx, y - gy)
                    if dist > r or px[x, y][3] == 255:
                        continue
                    a = int(150 * (1 - dist / (r + .5)))
                    if a > px[x, y][3]:
                        px[x, y] = tuple(col[:3]) + (a,)
        return img


def upscale(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)
