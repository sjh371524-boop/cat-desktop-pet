"""Split a transparent 4x2 directional walk sheet into stable 256px frames.

Each cell keeps only its largest connected alpha component, which removes small
pieces of a neighbouring sprite that cross an imperfect generated grid edge.
All retained cats share one square canvas size, horizontal centre, and paw
baseline before a nearest-neighbour resize.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def largest_component(image: Image.Image) -> tuple[Image.Image, tuple[int, int, int, int]]:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    alpha = rgba.getchannel("A")
    alpha_pixels = alpha.load()
    visited = bytearray(width * height)
    largest: list[tuple[int, int]] = []

    for y in range(height):
        for x in range(width):
            index = y * width + x
            if visited[index] or alpha_pixels[x, y] <= 20:
                continue
            visited[index] = 1
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                current_x, current_y = queue.popleft()
                component.append((current_x, current_y))
                for next_x, next_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if next_x < 0 or next_y < 0 or next_x >= width or next_y >= height:
                        continue
                    next_index = next_y * width + next_x
                    if visited[next_index] or alpha_pixels[next_x, next_y] <= 20:
                        continue
                    visited[next_index] = 1
                    queue.append((next_x, next_y))
            if len(component) > len(largest):
                largest = component

    if not largest:
        raise RuntimeError("No non-transparent sprite component was found.")

    keep = bytearray(width * height)
    min_x = width
    min_y = height
    max_x = 0
    max_y = 0
    for x, y in largest:
        keep[y * width + x] = 1
        min_x = min(min_x, x)
        min_y = min(min_y, y)
        max_x = max(max_x, x)
        max_y = max(max_y, y)

    pixels = rgba.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if not keep[row + x]:
                red, green, blue, _ = pixels[x, y]
                pixels[x, y] = (red, green, blue, 0)

    bounds = (min_x, min_y, max_x + 1, max_y + 1)
    return rgba.crop(bounds), bounds


def split_sheet(
    source: Path,
    output_root: Path,
    size: int,
    padding: int,
    overlap: int,
    rows: int,
    columns: int,
    row_names: tuple[str, ...],
) -> None:
    sheet = Image.open(source).convert("RGBA")
    frames: list[tuple[int, int, Image.Image]] = []
    largest_width = 0
    largest_height = 0

    for row in range(rows):
        top = round(row * sheet.height / rows)
        bottom = round((row + 1) * sheet.height / rows)
        for column in range(columns):
            left = round(column * sheet.width / columns)
            right = round((column + 1) * sheet.width / columns)
            expanded = (
                max(0, left - overlap),
                max(0, top - overlap),
                min(sheet.width, right + overlap),
                min(sheet.height, bottom + overlap),
            )
            sprite, _ = largest_component(sheet.crop(expanded))
            frames.append((row, column, sprite))
            largest_width = max(largest_width, sprite.width)
            largest_height = max(largest_height, sprite.height)

    canvas_size = max(largest_width, largest_height) + padding * 2
    for row, column, sprite in frames:
        canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
        left = (canvas_size - sprite.width) // 2
        top = canvas_size - padding - sprite.height
        canvas.alpha_composite(sprite, (left, top))
        canvas = canvas.resize((size, size), Image.Resampling.NEAREST)
        group = row_names[row]
        destination = output_root / group / f"{column:03d}.png"
        destination.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(destination, "PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output_root", type=Path)
    parser.add_argument("--size", type=int, default=256)
    parser.add_argument("--padding", type=int, default=10)
    parser.add_argument("--overlap", type=int, default=48, help="Expand generated cells before selecting the largest cat component.")
    parser.add_argument("--rows", type=int, default=2)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--row-names", nargs="+", default=("open", "blink"))
    args = parser.parse_args()
    if len(args.row_names) != args.rows:
        parser.error("--row-names must provide exactly one name for each row")
    split_sheet(
        args.source,
        args.output_root,
        args.size,
        args.padding,
        args.overlap,
        args.rows,
        args.columns,
        tuple(args.row_names),
    )


if __name__ == "__main__":
    main()
