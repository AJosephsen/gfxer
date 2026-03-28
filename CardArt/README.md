# CardArt — Card Art Generation Guide

Generate card art for **Ironhold: Wars of the Realm** using OpenAI's `gpt-image-1` model, composite it into the card frame, and export game-ready assets.

## Prerequisites

- **OpenAI API key** stored in `../api.token` (one directory up from CardArt)
  ```bash
  echo 'sk-...' > /workspaces/gfxer/api.token
  ```
- **Python 3** with **Pillow** (`pip3 install Pillow`)
- **curl** (pre-installed in the dev container)

## Quick Start

```bash
cd /workspaces/gfxer/CardArt

# 1. Generate art from a prompt file
./generate.sh plains.txt

# 2. Composite the art into the card frame
python3 compose.py generated/plains-20260328-215433-1.png

# 3. Copy the card to assets
cp generated/plains-20260328-215433-1-card.png assets/

# Result: assets/plains-20260328-215433-1-card.png
```

## Directory Structure

```
CardArt/
├── generate.sh         # Text-to-image generation script
├── compose.py          # Composites art into card frame
├── make_frame.py       # Generates frame.png (structural only — no text)
├── frame.png           # Card frame template (1024×1536)
├── mask.png            # Inpainting mask (kept for reference)
├── plains.txt          # Prompt files for each card type
├── forest.txt
├── hill.txt
├── beach.txt
├── wasteland.txt
├── settlement.txt
├── farm.txt
├── lumber-camp.txt
├── fishing-camp.txt
├── sheep-pasture.txt
├── board-background.txt
├── assets/             # Full-resolution composed cards (1024×1536)
└── generated/          # Raw output directory (gitignored)
    ├── plains-20260328-215433-1.png          # Raw art
    ├── plains-20260328-215433-1.prompt.txt   # Saved prompt
    └── plains-20260328-215433-1-card.png     # Composed card
```

## Step-by-Step Workflow

### 1. Write a Prompt

Create a `.txt` file with your scene description. No text or borders — those are layered by the UI.

```bash
cat plains.txt
# A lush green plains landscape for a fantasy card game. Rolling grasslands under a warm golden
# sky, painterly style, rich colors, suitable as card art background. No text, no border.
```

**Tips:**
- Specify art style: `painterly style`, `oil painting`, `fantasy illustration`
- Mention the purpose: `for a fantasy card game`, `suitable as card art background`
- Always end with: `No text, no border.`

### 2. Generate Art

```bash
./generate.sh <prompt_file>
```

**Output naming:** `{promptname}-{YYYYMMDD-HHMMSS}-{version}.png`

**Environment variables:**

| Variable | Default       | Options                                               |
|----------|---------------|-------------------------------------------------------|
| `SIZE`   | `1024x1024`   | `1024x1024`, `1024x1536` (portrait), `1536x1024` (landscape) |
| `MODEL`  | `gpt-image-1` | Any OpenAI image model                                |

```bash
# Standard card art (square, cropped to fit by compose.py)
./generate.sh plains.txt

# Wide landscape for background images
SIZE=1536x1024 ./generate.sh board-background.txt
```

### 3. Composite into Card Frame

```bash
python3 compose.py <art_image> [output_path]
```

The frame provides dark border, title bar, art window, and stats panel — all text and attributes are rendered by the UI layer at runtime, not baked into the image.

```bash
python3 compose.py generated/forest-20260328-215955-1.png
# → generated/forest-20260328-215955-1-card.png  (1024×1536)
```

### 4. Copy to Assets

Full-resolution composed cards go to `CardArt/assets/`:

```bash
cp generated/forest-20260328-215955-1-card.png assets/
```

### 5. Export Game-Ready WebPs

The game uses two sets of smaller WebPs:

| Path | Size | Use |
|------|------|-----|
| `StrategyGame/Assets/Cards/` | 256×384 | Hand card display |
| `StrategyGame/Assets/Lands/` | 200×160 | Board cell backgrounds |

Use this Python snippet to convert a single card:

```python
from PIL import Image

src = 'generated/forest-20260328-215955-1.png'   # raw art (for land WebP)
card_src = 'generated/forest-20260328-215955-1-card.png'  # composed (for card WebP)
card_id = 'land_forest'

# Board cell (200×160, cover-crop from raw art)
img = Image.open(src).convert('RGB')
scale = max(200/img.width, 160/img.height)
img = img.resize((int(img.width*scale), int(img.height*scale)), Image.LANCZOS)
img = img.crop(((img.width-200)//2, (img.height-160)//2,
                (img.width-200)//2+200, (img.height-160)//2+160))
img.save(f'../StrategyGame/Assets/Lands/{card_id}.webp', 'WEBP', quality=82)

# Hand card (256×384, from composed card)
card = Image.open(card_src).convert('RGBA')
card.resize((256, 384), Image.LANCZOS).save(
    f'../StrategyGame/Assets/Cards/{card_id}.webp', 'WEBP', quality=85)
```

**Card IDs** match the catalog: `land_plains`, `land_forest`, `land_hill`, `land_beach`, `land_wasteland`, `building_settlement`, `building_farm`, `building_lumber_camp`, `building_fishing_camp`, `building_sheep_pasture`.

## Regenerating All Cards at Once

```bash
cd /workspaces/gfxer/CardArt

for prompt in plains forest hill beach wasteland settlement farm lumber-camp fishing-camp sheep-pasture; do
  echo "=== $prompt ==="
  out=$(bash generate.sh ${prompt}.txt | grep "Image:" | awk '{print $2}')
  python3 compose.py "$out"
  cp "${out%.png}-card.png" assets/
done
```

Then re-run the WebP export script to update `StrategyGame/Assets/`.

## Card Frame Layout

The frame is purely structural — **no text is baked in**. All card attributes are rendered by the web viewer at runtime.

```
┌──────────────────────────┐
│       [title bar]        │  ← 100px — name rendered by UI
├──────────────────────────┤  ← Gold divider
│                          │
│       Art Window         │  ← 960×1004px
│   (filled by compose)    │
│                          │
├──────────────────────────┤  ← Gold divider
│  [stats panel]           │  ← 400px — stats rendered by UI
└──────────────────────────┘
     32px border all around
```

To change frame colours or proportions: edit `make_frame.py` and run `python3 make_frame.py` to regenerate `frame.png`.

## Notes

- `generated/` is gitignored. Committed outputs live in `assets/` and `StrategyGame/Assets/`.
- Each generation saves the prompt alongside the image as `.prompt.txt` for reproducibility.
- API costs: `gpt-image-1` charges per image. Landscape (`1536x1024`) costs more than square (`1024x1024`).
- After updating game WebPs, copy them to the running server's build output to hot-reload without restart:
  ```bash
  cp StrategyGame/Assets/Lands/land_beach.webp \
     StrategyGame/StrategyGame.WebViewer/bin/Release/net10.0/Assets/Lands/
  ```


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
