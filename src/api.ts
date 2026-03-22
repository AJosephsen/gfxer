// Direct browser → OpenAI (CORS is supported by api.openai.com)
const BASE = 'https://api.openai.com/v1';

export type ImageSize = '256x256' | '512x512' | '1024x1024';

export interface GenerateResult {
  dataUrl: string;
}

export async function generateImage(
  prompt: string,
  size: ImageSize = '1024x1024',
  apiKey: string,
): Promise<GenerateResult> {
  const res = await fetch(`${BASE}/images/generations`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model: 'gpt-image-1',
      prompt,
      n: 1,
      size,
    }),
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { error?: { message?: string } }).error?.message ?? `HTTP ${res.status}`);
  }

  const json = await res.json() as { data: Array<{ b64_json?: string; url?: string }> };
  const item = json.data[0];

  if (item.b64_json) {
    return { dataUrl: `data:image/png;base64,${item.b64_json}` };
  }
  if (item.url) {
    // Fetch the URL and convert to data URL so we own the bytes
    const imgRes = await fetch(item.url);
    const blob = await imgRes.blob();
    return { dataUrl: await blobToDataUrl(blob) };
  }
  throw new Error('No image data in response');
}

export async function editImage(
  imageBlob: Blob,
  maskBlob: Blob,
  prompt: string,
  size: ImageSize = '1024x1024',
  apiKey: string,
): Promise<GenerateResult> {
  const fd = new FormData();
  fd.append('model', 'gpt-image-1');
  fd.append('image[]', imageBlob, 'image.png');
  fd.append('mask', maskBlob, 'mask.png');
  fd.append('prompt', prompt);
  fd.append('n', '1');
  fd.append('size', size);

  const res = await fetch(`${BASE}/images/edits`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${apiKey}` },
    body: fd,
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { error?: { message?: string } }).error?.message ?? `HTTP ${res.status}`);
  }

  const json = await res.json() as { data: Array<{ b64_json?: string; url?: string }> };
  const item = json.data[0];

  if (item.b64_json) {
    return { dataUrl: `data:image/png;base64,${item.b64_json}` };
  }
  if (item.url) {
    const imgRes = await fetch(item.url);
    const blob = await imgRes.blob();
    return { dataUrl: await blobToDataUrl(blob) };
  }
  throw new Error('No image data in response');
}

// ---- helpers ----

function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}

/** Convert a data URL or image URL to a PNG Blob (needed for the edit API). */
export async function dataUrlToPngBlob(dataUrl: string): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth;
      canvas.height = img.naturalHeight;
      canvas.getContext('2d')!.drawImage(img, 0, 0);
      canvas.toBlob(
        (b) => (b ? resolve(b) : reject(new Error('toBlob failed'))),
        'image/png',
      );
    };
    img.onerror = () => reject(new Error('Image load failed'));
    img.src = dataUrl;
  });
}
