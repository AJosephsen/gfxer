# CardArt — Card Art Generation Guide

Generate card art for the strategy game using OpenAI's `gpt-image-1` model and composite it into a card frame template.

## Prerequisites

- **OpenAI API key** stored in `../api.token` (one directory up from CardArt)
  ```bash
  echo 'sk-...' > /workspaces/gfxer/api.token
  ```
- **Python 3** with **Pillow** (`pip3 install Pillow`)
- **curl** and **jq** (pre-installed in the dev container)

## Quick Start

```bash
cd /workspaces/gfxer/CardArt

# 1. Generate art from a prompt file
./generate.sh plains.txt

# 2. Composite the art into the card frame
python3 compose.py generated/plains-20260328-091135-1.png

# Result: generated/plains-20260328-091135-1-card.png
```

## Directory Structure

```
CardArt/
├── generate.sh      # Text-to-image generation script
├── compose.py       # Composites art into card frame
├── make_frame.py    # Generates frame.png and mask.png
├── edit.sh          # Inpainting script (experimental, see notes)
├── frame.png        # Card frame template (1024×1536)
├── mask.png         # Inpainting mask (kept for reference)
├── plains.txt       # Example prompt file
└── generated/       # Output directory (gitignored)
    ├── plains-20260328-091135-1.png          # Raw art
    ├── plains-20260328-091135-1.prompt.txt   # Saved prompt
    └── plains-20260328-091135-1-card.png     # Final card
```

## Step-by-Step Workflow

### 1. Write a Prompt

Create a `.txt` file with your prompt. Keep it focused on the scene — no text or borders.

```bash
echo "A dense pine forest on rocky hills, misty atmosphere, fantasy painting style, rich greens and grays. No text, no border." > forest.txt
```

**Tips for good prompts:**
- Specify art style: "painterly", "oil painting", "watercolor", "fantasy illustration"
- Mention the purpose: "for a fantasy card game", "suitable as card art background"
- End with: "No text, no border." to avoid unwanted overlays

### 2. Generate Art

```bash
./generate.sh <prompt_file>
```

**Output naming:** `{promptname}-{YYYYMMDD-HHMMSS}-{version}.png`

If you run it multiple times in the same second, the version auto-increments.

**Environment variables:**

| Variable | Default       | Options                                          |
|----------|---------------|--------------------------------------------------|
| `SIZE`   | `1024x1024`   | `1024x1024`, `1024x1536` (portrait), `1536x1024` (landscape) |
| `MODEL`  | `gpt-image-1` | Any OpenAI image model                           |

```bash
# Generate a portrait image (matches card frame aspect ratio best)
SIZE=1024x1536 ./generate.sh plains.txt

# Square image (will be center-cropped by compose.py)
./generate.sh plains.txt
```

**Recommendation:** Use `SIZE=1024x1536` for best results since the card frame is 1024×1536 and the art window is portrait-oriented (960×1004).

### 3. Composite into Card Frame

```bash
python3 compose.py <art_image> [output_path]
```

- If no output path is given, saves as `{art_image}-card.png`
- Accepts any image size — it scales and center-crops to fit the 960×1004px art window
- The card frame (border, title, stats panel) is overlaid on top

```bash
# Auto-named output
python3 compose.py generated/forest-20260328-120000-1.png

# Custom output path
python3 compose.py generated/forest-20260328-120000-1.png my-card.png
```

### 4. Regenerate the Frame (Optional)

If you want to change the card layout (border size, colors, stats text):

```bash
python3 make_frame.py
```

This regenerates `frame.png` and `mask.png`. Edit `make_frame.py` to customize:

- **`BORDER`** — outer border thickness (default: 32px)
- **`TITLE_H`** — title bar height (default: 100px)
- **`H - 400`** — stats panel height (bottom 400px)
- **Colors** — `BORDER_COLOR`, `TITLE_BG`, `STATS_BG`, `DIVIDER_COLOR`, `TEXT_COLOR`
- **Stats text** — the `stats_lines` list for placeholder card data

### Card Layout (frame.png)

```
┌──────────────────────────┐
│         Card Name        │  ← Title bar (100px)
├──────────────────────────┤  ← Gold divider
│                          │
│                          │
│       Art Window         │  ← 960×1004px
│    (filled by compose)   │
│                          │
│                          │
├──────────────────────────┤  ← Gold divider
│  Type:    Building       │
│  Cost:    4 Focus        │
│  Workers: 0–3            │  ← Stats panel (400px)
│  Production: +2 People   │
│  Upkeep: 1 Food          │
│  Terrain: Any            │
│  Description text...     │
└──────────────────────────┘
     32px border all around
```

## Example: Full Pipeline

```bash
cd /workspaces/gfxer/CardArt

# Create prompts for each card type
echo "A lush green plains landscape, rolling grasslands under a warm golden sky, painterly fantasy style. No text, no border." > plains.txt
echo "A dense dark forest with towering pines, mossy rocks, dappled sunlight, fantasy painting. No text, no border." > forest.txt
echo "A rocky hillside with exposed stone and sparse vegetation, dramatic sky, fantasy illustration. No text, no border." > hills.txt
echo "A small medieval settlement with thatched-roof cottages, village square, warm lighting, fantasy art. No text, no border." > settlement.txt

# Generate art (portrait size for best fit)
SIZE=1024x1536 ./generate.sh plains.txt
SIZE=1024x1536 ./generate.sh forest.txt
SIZE=1024x1536 ./generate.sh hills.txt
SIZE=1024x1536 ./generate.sh settlement.txt

# Composite each into cards
for img in generated/*-1.png; do
  [[ "$img" == *-card.png ]] && continue
  python3 compose.py "$img"
done
```

## Notes

- **`edit.sh`** is an experimental inpainting script that sends `frame.png` + `mask.png` + prompt to OpenAI's `/v1/images/edits` endpoint. Currently `gpt-image-1` doesn't respect the mask well and regenerates the entire image. Kept for future use if the API improves.
- **`generated/`** is gitignored. Committed outputs should be moved elsewhere.
- Each generation saves the prompt alongside the image as `.prompt.txt` for reproducibility.
- API costs: `gpt-image-1` charges per image. Portrait (1024×1536) costs more than square (1024×1024).
