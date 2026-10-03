"""
Цветовые вариации портретов врагов — новые существа в точности в стилистике исходных листов.

Берём клетку исходного листа 4x4 (1254x1254), маской выделяем пиксели нужного оттенка (например, красную
чешую дракона) и поворачиваем их тон / меняем насыщенность и яркость. Фон и контуры почти не трогаются,
поэтому вариация выглядит как родная картинка того же набора.

Результат: wwwroot/sprites/enemy-portraits-variants*.png (листы 4x4 того же размера, что исходные).
Каталог портретов (wwwroot/sprites/portraits.json) ссылается на них по номеру клетки — порядок VARIANTS
менять нельзя, только дописывать в конец (иначе сохранённые кампании получат чужие портреты).

Запуск: python tools/sprites/build_variants.py [--preview out.png]   (нужны Pillow и numpy)
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SPRITES = os.path.join(ROOT, "wwwroot", "sprites")
SHEET = 1254
GRID = 4

SHEETS = {
    "classic": "enemy-portraits.png",
    "humanoid": "enemy-portraits-humanoids.png",
    "monster": "enemy-portraits-monsters.png",
}

# (id, лист, клетка, [операции]); операция: (от_тона, до_тона, сдвиг_тона, множ_насыщ, множ_яркости, мин_насыщ)
# Тон в градусах 0..360; диапазон может переходить через 0 (340..20). Пустой диапазон (None) — весь кадр.
VARIANTS = [
    ("dragon_green", "classic", 12, [(330, 50, 110, 0.9, 0.95, 0.25)]),
    ("dragon_blue", "classic", 12, [(330, 50, 205, 0.95, 1.0, 0.25)]),
    ("dragon_black", "classic", 12, [(330, 50, 260, 0.35, 0.55, 0.25)]),
    ("dragon_white", "classic", 12, [(330, 50, 190, 0.18, 1.35, 0.25)]),
    ("dragon_gold", "classic", 12, [(330, 50, 25, 1.0, 1.25, 0.25)]),
    ("slime_blue", "classic", 6, [(60, 170, 100, 1.0, 1.0, 0.2)]),
    ("slime_red", "classic", 6, [(60, 170, -95, 1.35, 0.85, 0.2)]),
    ("slime_violet", "classic", 6, [(60, 170, 170, 0.9, 0.95, 0.2)]),
    ("slime_amber", "classic", 6, [(60, 170, -60, 1.0, 1.05, 0.2)]),
    ("wolf_snow", "classic", 3, [(None, None, 0, 0.35, 1.45, 0.0)]),
    ("hellhound", "classic", 3, [(None, None, 0, 1.0, 1.0, 0.0), (0, 360, 0, 1.0, 1.0, 0.0)], "tint_fire"),
    ("spider_venom", "classic", 4, [(330, 30, 110, 1.2, 1.1, 0.2)]),
    ("spider_frost", "classic", 4, [(330, 30, 200, 1.0, 1.25, 0.15)]),
    ("wraith_green", "classic", 7, [(180, 260, -100, 1.6, 1.0, 0.03)]),
    ("wraith_crimson", "classic", 7, [(180, 260, 140, 1.8, 0.95, 0.03)]),
    ("demon_frost", "classic", 13, [(330, 50, 200, 0.95, 1.05, 0.25)]),
    ("demon_void", "classic", 13, [(330, 50, 265, 0.9, 0.95, 0.25)]),
    ("horror_green", "classic", 14, [(250, 330, -150, 1.0, 1.0, 0.15)]),
    ("golem_iron", "classic", 15, [(15, 60, 0, 0.15, 0.9, 0.15)]),
    ("troll_cave", "classic", 8, [(35, 160, 0, 0.1, 0.85, 0.03)]),
    ("troll_frost", "classic", 8, [(35, 160, 115, 1.1, 1.15, 0.03)]),
    ("lich_frost", "classic", 11, [(330, 30, 195, 1.0, 1.15, 0.3)]),
    ("lich_plague", "classic", 11, [(330, 30, 110, 1.0, 1.0, 0.3)]),
    ("fire_giant", "monster", 13, [(170, 270, 160, 2.2, 0.9, 0.04)]),
    ("fire_elemental", "monster", 12, [(170, 280, 165, 2.0, 1.05, 0.04)]),
    ("toxic_elemental", "monster", 12, [(180, 270, -110, 1.4, 1.0, 0.05)]),
    ("crystal_ruby", "monster", 10, [(180, 260, 150, 1.0, 0.95, 0.15)]),
    ("crystal_amethyst", "monster", 10, [(180, 260, 60, 0.9, 1.0, 0.15)]),
    ("crystal_emerald", "monster", 10, [(180, 260, -90, 1.0, 1.0, 0.15)]),
    ("scorpion_black", "monster", 0, [(0, 50, 0, 0.25, 0.6, 0.15)]),
    ("scorpion_fire", "monster", 0, [(0, 50, -15, 1.5, 1.2, 0.15)]),
    ("serpent_sea", "monster", 1, [(40, 140, 110, 1.0, 1.05, 0.15)]),
    ("serpent_crimson", "monster", 1, [(40, 140, -70, 1.25, 0.95, 0.15)]),
    ("banshee_green", "monster", 5, [(190, 290, -110, 2.2, 1.0, 0.03)]),
    ("myconid_violet", "monster", 11, [(0, 45, -85, 1.0, 1.0, 0.25)]),
    ("myconid_glow", "monster", 11, [(0, 45, 170, 1.1, 1.15, 0.25)]),
    ("eldritch_green", "monster", 15, [(240, 330, -150, 1.0, 1.0, 0.15)]),
    ("salamander", "humanoid", 15, [(40, 150, -62, 1.4, 1.0, 0.1)]),
    ("orc_grey", "humanoid", 13, [(40, 150, 0, 0.08, 0.9, 0.05)]),
    ("goblin_blue", "classic", 0, [(40, 150, 110, 0.9, 1.0, 0.15)]),
]


def cell_box(index):
    col, row = index % GRID, index // GRID
    step = SHEET / GRID
    return (round(col * step), round(row * step), round((col + 1) * step), round((row + 1) * step))


def rgb_to_hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(-1), a.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rc = np.where(m & (mx == r), ((g - b) / np.where(m, d, 1)) % 6, 0)
    gc = np.where(m & (mx == g) & (mx != r), (b - r) / np.where(m, d, 1) + 2, 0)
    bc = np.where(m & (mx == b) & (mx != r) & (mx != g), (r - g) / np.where(m, d, 1) + 4, 0)
    h = (rc + gc + bc) * 60.0
    s = np.where(mx > 1e-6, d / np.where(mx > 1e-6, mx, 1), 0)
    return h % 360, s, mx


def hsv_to_rgb(h, s, v):
    c = v * s
    hp = (h % 360) / 60.0
    x = c * (1 - np.abs(hp % 2 - 1))
    z = np.zeros_like(h)
    conds = [(hp < 1, (c, x, z)), (hp < 2, (x, c, z)), (hp < 3, (z, c, x)), (hp < 4, (z, x, c)), (hp < 5, (x, z, c)), (hp >= 5, (c, z, x))]
    r = np.zeros_like(h); g = np.zeros_like(h); b = np.zeros_like(h)
    done = np.zeros(h.shape, bool)
    for cond, (rr, gg, bb) in conds:
        sel = cond & ~done
        r = np.where(sel, rr, r); g = np.where(sel, gg, g); b = np.where(sel, bb, b)
        done |= sel
    mm = v - c
    return np.stack([r + mm, g + mm, b + mm], -1)


def hue_mask(h, s, lo, hi, smin, soft=12.0):
    if lo is None:
        return np.ones_like(h)
    if lo <= hi:
        dist = np.maximum(lo - h, h - hi)
    else:  # диапазон через 0
        inside = (h >= lo) | (h <= hi)
        dist = np.where(inside, -1, np.minimum(np.abs(h - lo), np.abs(h - hi)))
        dist = np.minimum(dist, np.minimum(np.abs(h + 360 - lo), np.abs(h - 360 - hi)))
    mh = np.clip(1 - dist / soft, 0, 1)
    ms = np.clip((s - smin) / 0.12 + 0.5, 0, 1) if smin > 0 else np.ones_like(s)
    return mh * ms


def recolor(img, ops, special=None):
    a = np.asarray(img.convert("RGB"), dtype=np.float64) / 255.0
    h, s, v = rgb_to_hsv(a)
    out = a.copy()
    if special == "tint_fire":
        # «Адская гончая»: тёмная шерсть наливается углём и жаром, светлые места — пламенем.
        lum = v
        warm = hsv_to_rgb(np.full_like(h, 12.0), np.clip(0.55 + 0.4 * lum, 0, 1), np.clip(lum * 1.25, 0, 1))
        k = np.clip((lum - 0.12) / 0.5, 0, 1)[..., None] * 0.85
        out = a * (1 - k) + warm * k
    else:
        for lo, hi, dh, ks, kv, smin in ops:
            m = hue_mask(h, s, lo, hi, smin)[..., None]
            nh, ns, nv = (h + dh) % 360, np.clip(s * ks, 0, 1), np.clip(v * kv, 0, 1)
            shifted = hsv_to_rgb(nh, ns, nv)
            out = out * (1 - m) + shifted * m
    return Image.fromarray((np.clip(out, 0, 1) * 255).round().astype(np.uint8), "RGB").convert("RGBA")


def main():
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    sources = {k: Image.open(os.path.join(SPRITES, v)).convert("RGBA") for k, v in SHEETS.items()}
    tiles = []
    for entry in VARIANTS:
        vid, sheet, cell, ops = entry[:4]
        special = entry[4] if len(entry) > 4 else None
        tile = sources[sheet].crop(cell_box(cell))
        tiles.append((vid, recolor(tile, ops, special)))

    per_sheet = GRID * GRID
    for n in range(0, len(tiles), per_sheet):
        page = Image.new("RGBA", (SHEET, SHEET), (11, 14, 18, 255))
        for i, (_, tile) in enumerate(tiles[n:n + per_sheet]):
            box = cell_box(i)
            page.paste(tile.resize((box[2] - box[0], box[3] - box[1])), box[:2])
        suffix = "" if n == 0 else str(n // per_sheet + 1)
        page.save(os.path.join(SPRITES, f"enemy-portraits-variants{suffix}.png"), optimize=True)

    if preview:
        w = 200
        cols = 8
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * w, rows * w), (0, 0, 0))
        for i, (_, t) in enumerate(tiles):
            sheet.paste(t.convert("RGB").resize((w, w)), ((i % cols) * w, (i // cols) * w))
        sheet.save(preview)
    print(f"{len(tiles)} вариаций")


if __name__ == "__main__":
    main()
