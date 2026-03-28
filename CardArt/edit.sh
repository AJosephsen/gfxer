#!/usr/bin/env bash
set -euo pipefail

# ── Config ───────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROMPT_FILE="${1:-$SCRIPT_DIR/prompt.txt}"
TOKEN_FILE="$SCRIPT_DIR/../api.token"
OUT_DIR="$SCRIPT_DIR/generated"
FRAME="$SCRIPT_DIR/frame.png"
MASK="$SCRIPT_DIR/mask.png"
SIZE="${SIZE:-1024x1536}"
MODEL="${MODEL:-gpt-image-1}"

# ── Validate ─────────────────────────────────────────────────────────────────
if [[ ! -f "$TOKEN_FILE" ]]; then
  echo "ERROR: API token not found at $TOKEN_FILE"
  echo "Create it with: echo 'sk-...' > $TOKEN_FILE"
  exit 1
fi

if [[ ! -f "$PROMPT_FILE" ]]; then
  echo "ERROR: Prompt file not found: $PROMPT_FILE"
  exit 1
fi

if [[ ! -f "$FRAME" ]]; then
  echo "ERROR: Frame template not found: $FRAME"
  echo "Run: python3 make_frame.py"
  exit 1
fi

if [[ ! -f "$MASK" ]]; then
  echo "ERROR: Mask not found: $MASK"
  echo "Run: python3 make_frame.py"
  exit 1
fi

API_KEY="$(cat "$TOKEN_FILE" | tr -d '[:space:]')"
PROMPT="$(cat "$PROMPT_FILE")"

if [[ -z "$PROMPT" ]]; then
  echo "ERROR: Prompt file is empty"
  exit 1
fi

# ── Generate output name: {promptname}-{datetime}-{version}.png ──────────────
PROMPT_BASE="$(basename "$PROMPT_FILE" .txt)"
TAG="$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT_DIR"

VER=1
while [[ -f "$OUT_DIR/${PROMPT_BASE}-${TAG}-${VER}.png" ]]; do
  ((VER++))
done
PREFIX="${PROMPT_BASE}-${TAG}-${VER}"

echo "=== Inpainting card art ==="
echo "  Model:  $MODEL"
echo "  Size:   $SIZE"
echo "  Frame:  $FRAME"
echo "  Mask:   $MASK"
echo "  Prompt: $PROMPT"
echo "  Output: $PREFIX"
echo ""

# ── Call OpenAI edits API (multipart form) ───────────────────────────────────
RESPONSE=$(curl -s -w "\n%{http_code}" \
  "https://api.openai.com/v1/images/edits" \
  -H "Authorization: Bearer $API_KEY" \
  -F "model=$MODEL" \
  -F "image=@$FRAME" \
  -F "mask=@$MASK" \
  -F "prompt=$PROMPT" \
  -F "n=1" \
  -F "size=$SIZE")

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | sed '$d')

if [[ "$HTTP_CODE" != "200" ]]; then
  echo "ERROR: API returned HTTP $HTTP_CODE"
  echo "$BODY" | jq . 2>/dev/null || echo "$BODY"
  exit 1
fi

# ── Save outputs ─────────────────────────────────────────────────────────────
IMAGE_FILE="$OUT_DIR/${PREFIX}.png"
PROMPT_COPY="$OUT_DIR/${PREFIX}.prompt.txt"

B64=$(echo "$BODY" | jq -r '.data[0].b64_json // empty')
URL=$(echo "$BODY" | jq -r '.data[0].url // empty')

if [[ -n "$B64" ]]; then
  echo "$B64" | base64 -d > "$IMAGE_FILE"
elif [[ -n "$URL" ]]; then
  curl -s -o "$IMAGE_FILE" "$URL"
else
  echo "ERROR: No image data in response"
  echo "$BODY" | jq .
  exit 1
fi

echo "$PROMPT" > "$PROMPT_COPY"

echo "=== Done ==="
echo "  Image:  $IMAGE_FILE"
echo "  Prompt: $PROMPT_COPY"
echo "  Size:   $(du -h "$IMAGE_FILE" | cut -f1)"
