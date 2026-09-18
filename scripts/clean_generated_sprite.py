"""Convert an ImageGen pixel sprite with a baked light checkerboard into RGBA.

The cleanup is deliberately conservative: only near-neutral, very light pixels
connected to the canvas border are removed. The foreground is then scaled with
nearest-neighbour sampling and aligned to a stable bottom baseline.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def is_background_candidate(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _alpha = pixel
    return min(red, green, blue) >= 225 and max(red, green, blue) - min(red, green, blue) <= 14


def connected_background(image: Image.Image) -> bytearray:
    width, height = image.size
    pixels = image.load()
    seen = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if seen[index] or not is_background_candidate(pixels[x, y]):
            return
        seen[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)
    return seen


def clean_sprite(source: Path, destination: Path, size: int, padding: int, preserve_size: bool = False) -> None:
    image = Image.open(source).convert("RGBA")
    width, height = image.size
    mask = connected_background(image)
    pixels = image.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if mask[row + x]:
                red, green, blue, _alpha = pixels[x, y]
                pixels[x, y] = (red, green, blue, 0)

    if preserve_size:
        destination.parent.mkdir(parents=True, exist_ok=True)
        image.save(destination, "PNG", optimize=True)
        return

    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise RuntimeError("Background cleanup removed the entire image.")

    foreground = image.crop(bounds)
    available = size - padding * 2
    scale = min(available / foreground.width, available / foreground.height)
    target_width = max(1, round(foreground.width * scale))
    target_height = max(1, round(foreground.height * scale))
    foreground = foreground.resize((target_width, target_height), Image.Resampling.NEAREST)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    left = (size - target_width) // 2
    top = size - padding - target_height
    canvas.alpha_composite(foreground, (left, top))
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, "PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--size", type=int, default=256)
    parser.add_argument("--padding", type=int, default=10)
    parser.add_argument("--preserve-size", action="store_true", help="Only remove the connected background; retain the original sheet dimensions.")
    args = parser.parse_args()
    clean_sprite(args.source, args.destination, args.size, args.padding, args.preserve_size)


if __name__ == "__main__":
    main()
