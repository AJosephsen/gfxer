import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useCallback,
} from 'react';

export interface MaskCanvasHandle {
  /** Returns the original image as a PNG Blob (1024×1024). */
  getImageBlob(): Promise<Blob>;
  /** Returns the mask as a PNG Blob: transparent = edit, white = keep. */
  getMaskBlob(): Promise<Blob>;
  /** Clears all painted strokes. */
  clearMask(): void;
}

interface Props {
  /** base64 data URL of the image to edit */
  imageSrc: string;
  /** Internal resolution (should match the image; displayed at 512px) */
  canvasSize?: number;
  brushSize: number;
  isEraser: boolean;
}

const MaskCanvas = forwardRef<MaskCanvasHandle, Props>(
  ({ imageSrc, canvasSize = 1024, brushSize, isEraser }, ref) => {
    // imgCanvas: renders the source image
    const imgCanvasRef = useRef<HTMLCanvasElement>(null);
    // paintCanvas: transparent background, user draws red strokes here
    const paintCanvasRef = useRef<HTMLCanvasElement>(null);

    const isDrawing = useRef(false);
    const lastPos = useRef<{ x: number; y: number } | null>(null);

    // Load image into imgCanvas whenever imageSrc changes
    useEffect(() => {
      const canvas = imgCanvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d')!;
      ctx.clearRect(0, 0, canvasSize, canvasSize);

      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => ctx.drawImage(img, 0, 0, canvasSize, canvasSize);
      img.src = imageSrc;
    }, [imageSrc, canvasSize]);

    // Clear paint layer when image changes (new image = fresh mask)
    useEffect(() => {
      const canvas = paintCanvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d')!;
      ctx.clearRect(0, 0, canvasSize, canvasSize);
    }, [imageSrc, canvasSize]);

    // Imperative handle exposed to parent
    useImperativeHandle(
      ref,
      () => ({
        getImageBlob() {
          return canvasToBlob(imgCanvasRef.current!);
        },
        getMaskBlob() {
          return buildMaskBlob(paintCanvasRef.current!, canvasSize);
        },
        clearMask() {
          const ctx = paintCanvasRef.current!.getContext('2d')!;
          ctx.clearRect(0, 0, canvasSize, canvasSize);
        },
      }),
      [canvasSize],
    );

    // Convert canvas (x,y) from a mouse event relative to the element
    const getPos = (e: React.MouseEvent<HTMLCanvasElement>) => {
      const canvas = paintCanvasRef.current!;
      const r = canvas.getBoundingClientRect();
      return {
        x: ((e.clientX - r.left) / r.width) * canvasSize,
        y: ((e.clientY - r.top) / r.height) * canvasSize,
      };
    };

    const paintAt = useCallback(
      (x: number, y: number, fromX?: number, fromY?: number) => {
        const ctx = paintCanvasRef.current!.getContext('2d')!;
        const r = brushSize / 2;

        if (isEraser) {
          ctx.globalCompositeOperation = 'destination-out';
          ctx.strokeStyle = 'rgba(0,0,0,1)';
          ctx.fillStyle = 'rgba(0,0,0,1)';
        } else {
          ctx.globalCompositeOperation = 'source-over';
          ctx.strokeStyle = 'rgba(239,68,68,0.55)';
          ctx.fillStyle = 'rgba(239,68,68,0.55)';
        }

        ctx.lineWidth = brushSize;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        ctx.beginPath();
        if (fromX !== undefined && fromY !== undefined) {
          ctx.moveTo(fromX, fromY);
          ctx.lineTo(x, y);
          ctx.stroke();
        }
        // Always fill a circle at the current point for clean dots
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();

        ctx.globalCompositeOperation = 'source-over';
      },
      [brushSize, isEraser],
    );

    const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
      isDrawing.current = true;
      const pos = getPos(e);
      lastPos.current = pos;
      paintAt(pos.x, pos.y);
    };

    const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
      if (!isDrawing.current) return;
      const pos = getPos(e);
      paintAt(pos.x, pos.y, lastPos.current?.x, lastPos.current?.y);
      lastPos.current = pos;
    };

    const handleMouseUp = () => {
      isDrawing.current = false;
      lastPos.current = null;
    };

    const displaySize = Math.min(canvasSize, 512);

    return (
      <div
        className="mask-canvas-root"
        style={{ width: displaySize, height: displaySize, position: 'relative' }}
      >
        {/* Base layer: the image */}
        <canvas
          ref={imgCanvasRef}
          width={canvasSize}
          height={canvasSize}
          style={{
            position: 'absolute',
            inset: 0,
            width: displaySize,
            height: displaySize,
            imageRendering: 'pixelated',
          }}
        />
        {/* Paint layer: red strokes */}
        <canvas
          ref={paintCanvasRef}
          width={canvasSize}
          height={canvasSize}
          style={{
            position: 'absolute',
            inset: 0,
            width: displaySize,
            height: displaySize,
            cursor: isEraser ? 'cell' : 'crosshair',
          }}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={handleMouseUp}
        />
      </div>
    );
  },
);

MaskCanvas.displayName = 'MaskCanvas';
export default MaskCanvas;

// ---- helpers ----

function canvasToBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob(
      (b) => (b ? resolve(b) : reject(new Error('toBlob failed'))),
      'image/png',
    ),
  );
}

/**
 * Build a mask PNG:
 *   - transparent (alpha=0) where the user painted → OpenAI edits here
 *   - opaque white elsewhere → OpenAI keeps this
 */
function buildMaskBlob(paintCanvas: HTMLCanvasElement, size: number): Promise<Blob> {
  const maskCanvas = document.createElement('canvas');
  maskCanvas.width = size;
  maskCanvas.height = size;
  const maskCtx = maskCanvas.getContext('2d')!;

  // Start fully opaque white
  maskCtx.fillStyle = 'white';
  maskCtx.fillRect(0, 0, size, size);

  const paintCtx = paintCanvas.getContext('2d')!;
  const paintData = paintCtx.getImageData(0, 0, size, size);
  const maskData = maskCtx.getImageData(0, 0, size, size);

  for (let i = 0; i < paintData.data.length; i += 4) {
    if (paintData.data[i + 3] > 0) {
      // Painted pixel → transparent in mask (edit area)
      maskData.data[i] = 0;
      maskData.data[i + 1] = 0;
      maskData.data[i + 2] = 0;
      maskData.data[i + 3] = 0;
    }
  }

  maskCtx.putImageData(maskData, 0, 0);
  return canvasToBlob(maskCanvas);
}
