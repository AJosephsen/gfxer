#!/usr/bin/env python3
"""
Composite generated art into the card frame template.

Usage:
  python3 compose.py <art_image> [output_image]

Takes a generated landscape image, crops/scales it to fit the art window
in frame.png, and composites the frame on top. The result is a complete
card with art in the window and the frame border/title/stats visible.

If output_image is omitted, saves next to the art image with '-card' suffix.
"""

import sys
import os
from PIL import Image

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
FRAME_PATH = os.path.join(SCRIPT_DIR, "frame.png")

# Must match make_frame.py layout
BORDER = 32
TITLE_H = 100
ART_TOP = BORDER + TITLE_H       # 132
ART_BOTTOM = 1536 - 400          # 1136
ART_LEFT = BORDER                # 32
ART_RIGHT = 1024 - BORDER        # 992
ART_W = ART_RIGHT - ART_LEFT     # 960
ART_H = ART_BOTTOM - ART_TOP     # 1004


def compose(art_path: str, output_path: str | None = None) -> str:
    """Composite art into the card frame and return the output path."""

    if not os.path.exists(FRAME_PATH):
        print(f"ERROR: Frame not found at {FRAME_PATH}")
        print("Run: python3 make_frame.py")
        sys.exit(1)

    # Load art and frame
    art = Image.open(art_path).convert("RGBA")
    frame = Image.open(FRAME_PATH).convert("RGBA")

    # Scale art to fill the art window (cover crop)
    art_aspect = art.width / art.height
    window_aspect = ART_W / ART_H

    if art_aspect > window_aspect:
        # Art is wider — scale by height, crop sides
        new_h = ART_H
        new_w = int(art.width * (ART_H / art.height))
    else:
        # Art is taller — scale by width, crop top/bottom
        new_w = ART_W
        new_h = int(art.height * (ART_W / art.width))

    art_scaled = art.resize((new_w, new_h), Image.LANCZOS)

    # Center crop to exact art window size
    left = (new_w - ART_W) // 2
    top = (new_h - ART_H) // 2
    art_cropped = art_scaled.crop((left, top, left + ART_W, top + ART_H))

    # Create the composite: start with card-sized canvas
    card = Image.new("RGBA", (1024, 1536), (0, 0, 0, 255))

    # Paste art into the art window area
    card.paste(art_cropped, (ART_LEFT, ART_TOP))

    # Paste frame on top (frame has opaque border/title/stats, parchment art area)
    # We need to use the frame as an overlay — its art area should be transparent
    # so the art shows through. Reload frame with transparent art window for compositing.
    frame_overlay = frame.copy()

    # Make the art window area transparent in the frame overlay
    # so the pasted art shows through
    overlay_pixels = frame_overlay.load()
    for y in range(ART_TOP, ART_BOTTOM):
        for x in range(ART_LEFT, ART_RIGHT):
            overlay_pixels[x, y] = (0, 0, 0, 0)

    card = Image.alpha_composite(card, card)  # flatten
    # Paste art
    card.paste(art_cropped, (ART_LEFT, ART_TOP))
    # Composite frame on top
    card = Image.alpha_composite(card, frame_overlay)

    # Determine output path
    if output_path is None:
        base, ext = os.path.splitext(art_path)
        output_path = f"{base}-card{ext}"

    card.save(output_path)
    print(f"Card saved: {output_path}  ({card.width}×{card.height})")
    print(f"  Art source: {art_path} ({art.width}×{art.height})")
    print(f"  Art window: {ART_W}×{ART_H}px")
    return output_path


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python3 compose.py <art_image> [output_image]")
        sys.exit(1)

    art_file = sys.argv[1]
    out_file = sys.argv[2] if len(sys.argv) > 2 else None
    compose(art_file, out_file)
