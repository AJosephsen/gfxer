#!/usr/bin/env python3
"""
Generate a card frame template (frame.png) and matching mask (mask.png).

The frame has:
  - A dark border with rounded-ish corners
  - A title bar at the top
  - A transparent art window in the center
  - A stats/description panel at the bottom

The mask marks the art window as transparent (= "paint here")
and everything else as white (= "keep").

Size: 1024×1536 (portrait, supported by gpt-image-1)
"""

from PIL import Image, ImageDraw
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = SCRIPT_DIR

W, H = 1024, 1536

# Layout constants
BORDER = 32          # outer border thickness
TITLE_H = 100        # title bar height
ART_TOP = BORDER + TITLE_H
ART_BOTTOM = H - 400  # leave 400px for stats panel
STATS_TOP = ART_BOTTOM
DIVIDER = 4          # line thickness between sections

# Colors
BORDER_COLOR = (30, 25, 20, 255)       # dark brown/black
TITLE_BG = (45, 38, 32, 255)           # dark wood
STATS_BG = (40, 35, 30, 255)           # slightly different dark
DIVIDER_COLOR = (120, 100, 70, 255)    # gold-ish accent
ART_AREA = (245, 235, 220, 255)        # light parchment (opaque placeholder)

# ── Build the frame ─────────────────────────────────────────────────────────

frame = Image.new("RGBA", (W, H), BORDER_COLOR)
draw = ImageDraw.Draw(frame)

# Title bar
draw.rectangle([BORDER, BORDER, W - BORDER, ART_TOP], fill=TITLE_BG)

# Art window (transparent)
draw.rectangle([BORDER, ART_TOP, W - BORDER, ART_BOTTOM], fill=ART_AREA)

# Stats panel
draw.rectangle([BORDER, STATS_TOP, W - BORDER, H - BORDER], fill=STATS_BG)

# Horizontal dividers (gold accents)
draw.rectangle([BORDER, ART_TOP - DIVIDER, W - BORDER, ART_TOP], fill=DIVIDER_COLOR)
draw.rectangle([BORDER, ART_BOTTOM, W - BORDER, ART_BOTTOM + DIVIDER], fill=DIVIDER_COLOR)

# Inner border accent line
accent_inset = BORDER - 6
draw.rectangle(
    [accent_inset, accent_inset, W - accent_inset, H - accent_inset],
    outline=DIVIDER_COLOR, width=2
)

# Corner decorations (small gold squares)
corner_size = 12
for x in [accent_inset, W - accent_inset - corner_size]:
    for y in [accent_inset, H - accent_inset - corner_size]:
        draw.rectangle([x, y, x + corner_size, y + corner_size], fill=DIVIDER_COLOR)

# Text (title, stats, description) is intentionally omitted — rendered by the UI layer.

frame_path = os.path.join(OUT_DIR, "frame.png")
frame.save(frame_path)
print(f"Frame saved: {frame_path}  ({W}×{H})")

# ── Build the mask ──────────────────────────────────────────────────────────
# White = keep, Transparent = generate
mask = Image.new("RGBA", (W, H), (255, 255, 255, 255))
mask_draw = ImageDraw.Draw(mask)

# Art window is transparent in the mask (= area to paint)
mask_draw.rectangle([BORDER, ART_TOP, W - BORDER, ART_BOTTOM], fill=(0, 0, 0, 0))

mask_path = os.path.join(OUT_DIR, "mask.png")
mask.save(mask_path)
print(f"Mask saved:  {mask_path}  ({W}×{H})")

print(f"\nArt area: {W - 2*BORDER}×{ART_BOTTOM - ART_TOP}px  (y: {ART_TOP}–{ART_BOTTOM})")
print("Ready for inpainting with edit.sh")
