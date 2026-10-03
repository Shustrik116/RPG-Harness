"""Устанавливает живописные портреты поверх процедурных листов.

Полноразмерные мастера хранятся отдельно, потому что процедурные генераторы
остаются полезными как быстрый полностью воспроизводимый запасной вариант.
"""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
MASTER_DIR = Path(__file__).with_name("painterly")
OUTPUT_DIR = ROOT / "wwwroot" / "sprites"

SHEETS = {
    "cyber-hero-avatars": (960, 640),
    "cyber-persons": (1280, 1280),
    "cyber-enemies-street": (1280, 1280),
    "cyber-enemies-machines": (1280, 1280),
    "modern-hero-avatars": (960, 640),
    "modern-persons": (1280, 1280),
    "modern-enemies-street": (1280, 1280),
    "modern-enemies-threats": (1280, 1280),
}


def main() -> None:
    for name, size in SHEETS.items():
        source = MASTER_DIR / f"{name}-master.png"
        target = OUTPUT_DIR / f"{name}.png"
        with Image.open(source) as image:
            image.convert("RGB").resize(size, Image.Resampling.LANCZOS).save(
                target,
                optimize=True,
            )
        print(f"{target.name}: {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
