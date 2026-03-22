import { useRef, useState } from 'react';
import MaskCanvas, { type MaskCanvasHandle } from './MaskCanvas';
import { generateImage, editImage, HARDCODED_API_KEY, type ImageSize } from './api';

// ── Types ────────────────────────────────────────────────────────────────────

interface HistoryEntry {
  dataUrl: string;
  label: string;
}

// ── App ──────────────────────────────────────────────────────────────────────

export default function App() {
  // API key (defaults to the hardcoded value; user can override in UI)
  const [apiKey, setApiKey] = useState(
    () => localStorage.getItem('gfxer_api_key') ?? HARDCODED_API_KEY,
  );
  const [showKeyPanel, setShowKeyPanel] = useState(false);

  // Current image displayed in the editor
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [imageSize] = useState<ImageSize>('1024x1024');

  // Prompts
  const [genPrompt, setGenPrompt] = useState('');
  const [editPrompt, setEditPrompt] = useState('');

  // Brush
  const [brushSize, setBrushSize] = useState(40);
  const [isEraser, setIsEraser] = useState(false);

  // Loading / error
  const [loading, setLoading] = useState<'generate' | 'edit' | null>(null);
  const [error, setError] = useState<string | null>(null);

  // History (newest first)
  const [history, setHistory] = useState<HistoryEntry[]>([]);

  // Ref into MaskCanvas
  const maskRef = useRef<MaskCanvasHandle>(null);

  // ── Helpers ───────────────────────────────────────────────────────────────

  const saveKey = (key: string) => {
    setApiKey(key);
    localStorage.setItem('gfxer_api_key', key);
    setShowKeyPanel(false);
  };

  const pushHistory = (dataUrl: string, label: string) => {
    setHistory((h) => [{ dataUrl, label }, ...h].slice(0, 12));
  };

  // ── Handlers ──────────────────────────────────────────────────────────────

  const handleGenerate = async () => {
    if (!genPrompt.trim()) return;
    setError(null);
    setLoading('generate');
    try {
      const { dataUrl } = await generateImage(genPrompt.trim(), imageSize, apiKey);
      setCurrentImage(dataUrl);
      pushHistory(dataUrl, genPrompt.trim().slice(0, 40));
    } catch (e) {
      setError(String((e as Error).message ?? e));
    } finally {
      setLoading(null);
    }
  };

  const handleEdit = async () => {
    if (!currentImage || !editPrompt.trim()) return;
    if (!maskRef.current) return;
    setError(null);
    setLoading('edit');
    try {
      const [imageBlob, maskBlob] = await Promise.all([
        maskRef.current.getImageBlob(),
        maskRef.current.getMaskBlob(),
      ]);
      const { dataUrl } = await editImage(imageBlob, maskBlob, editPrompt.trim(), imageSize, apiKey);
      setCurrentImage(dataUrl);
      pushHistory(dataUrl, `edit: ${editPrompt.trim().slice(0, 32)}`);
      maskRef.current.clearMask();
      setEditPrompt('');
    } catch (e) {
      setError(String((e as Error).message ?? e));
    } finally {
      setLoading(null);
    }
  };

  const isLoading = loading !== null;

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="app">
      {/* ── Header ── */}
      <header className="header">
        <div className="header-left">
          <span className="logo">gfxer</span>
          <span className="logo-sub">sprite editor</span>
        </div>
        <div className="header-right">
          <button
            className={`key-btn ${apiKey && apiKey !== HARDCODED_API_KEY ? 'key-set' : 'key-unset'}`}
            onClick={() => setShowKeyPanel((v) => !v)}
            title="Configure API key"
          >
            {apiKey && apiKey !== HARDCODED_API_KEY ? '🔑 key set' : '🔑 set api key'}
          </button>
        </div>
      </header>

      {/* ── API Key panel ── */}
      {showKeyPanel && (
        <ApiKeyPanel
          current={apiKey}
          onSave={saveKey}
          onClose={() => setShowKeyPanel(false)}
        />
      )}

      {/* ── Error banner ── */}
      {error && (
        <div className="error-banner">
          <span>{error}</span>
          <button onClick={() => setError(null)}>×</button>
        </div>
      )}

      {/* ── Main layout ── */}
      <div className="workspace">
        {/* Left column: controls */}
        <aside className="sidebar">
          {/* Generate section */}
          <section className="panel">
            <h2 className="panel-title">
              <span className="step-badge">1</span> Generate
            </h2>
            <label className="field-label">Prompt</label>
            <textarea
              className="prompt-textarea"
              placeholder="pixel art knight, transparent background, game sprite, 2D side-view…"
              value={genPrompt}
              rows={4}
              disabled={isLoading}
              onChange={(e) => setGenPrompt(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault();
                  handleGenerate();
                }
              }}
            />
            <button
              className="btn btn-primary"
              disabled={isLoading || !genPrompt.trim()}
              onClick={handleGenerate}
            >
              {loading === 'generate' ? (
                <><Spinner /> Generating…</>
              ) : (
                'Generate Image'
              )}
            </button>
          </section>

          {/* Brush tools (only when image is loaded) */}
          {currentImage && (
            <section className="panel">
              <h2 className="panel-title">
                <span className="step-badge">2</span> Paint Mask
              </h2>
              <p className="hint">
                Paint over the area you want to change. Red = edit zone.
              </p>

              <div className="tool-row">
                <button
                  className={`tool-btn ${!isEraser ? 'active' : ''}`}
                  onClick={() => setIsEraser(false)}
                  title="Brush"
                >
                  ✏️ Brush
                </button>
                <button
                  className={`tool-btn ${isEraser ? 'active' : ''}`}
                  onClick={() => setIsEraser(true)}
                  title="Eraser"
                >
                  ⬜ Erase
                </button>
                <button
                  className="tool-btn"
                  onClick={() => maskRef.current?.clearMask()}
                  title="Clear mask"
                >
                  🗑 Clear
                </button>
              </div>

              <label className="field-label">
                Brush size: <strong>{brushSize}px</strong>
              </label>
              <input
                type="range"
                min={4}
                max={120}
                value={brushSize}
                onChange={(e) => setBrushSize(Number(e.target.value))}
                className="brush-slider"
              />
            </section>
          )}

          {/* Edit section (only when image is loaded) */}
          {currentImage && (
            <section className="panel">
              <h2 className="panel-title">
                <span className="step-badge">3</span> Edit
              </h2>
              <label className="field-label">Edit prompt</label>
              <textarea
                className="prompt-textarea"
                placeholder="describe what should appear in the painted area…"
                value={editPrompt}
                rows={3}
                disabled={isLoading}
                onChange={(e) => setEditPrompt(e.target.value)}
              />
              <button
                className="btn btn-accent"
                disabled={isLoading || !editPrompt.trim()}
                onClick={handleEdit}
              >
                {loading === 'edit' ? (
                  <><Spinner /> Editing…</>
                ) : (
                  'Generate Edit'
                )}
              </button>
            </section>
          )}
        </aside>

        {/* Center: canvas */}
        <main className="canvas-area">
          {currentImage ? (
            <div className="canvas-frame">
              <MaskCanvas
                ref={maskRef}
                imageSrc={currentImage}
                canvasSize={1024}
                brushSize={brushSize}
                isEraser={isEraser}
              />
              {isLoading && (
                <div className="canvas-overlay-loading">
                  <div className="loading-text">
                    <Spinner large />
                    {loading === 'generate' ? 'Generating…' : 'Applying edit…'}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="canvas-placeholder">
              {loading === 'generate' ? (
                <>
                  <Spinner large />
                  <p>Generating image…</p>
                </>
              ) : (
                <>
                  <div className="placeholder-icon">🖼</div>
                  <p>Enter a prompt and click <strong>Generate Image</strong></p>
                </>
              )}
            </div>
          )}
        </main>
      </div>

      {/* ── History strip ── */}
      {history.length > 0 && (
        <div className="history-strip">
          {history.map((entry, i) => (
            <button
              key={i}
              className={`history-thumb ${entry.dataUrl === currentImage ? 'active' : ''}`}
              onClick={() => setCurrentImage(entry.dataUrl)}
              title={entry.label}
            >
              <img src={entry.dataUrl} alt={entry.label} />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function ApiKeyPanel({
  current,
  onSave,
  onClose,
}: {
  current: string;
  onSave: (k: string) => void;
  onClose: () => void;
}) {
  const [value, setValue] = useState(current === HARDCODED_API_KEY ? '' : current);

  return (
    <div className="api-key-panel">
      <div className="api-key-inner">
        <h3>OpenAI API Key</h3>
        <p className="hint">
          Stored in localStorage, never sent to any server other than api.openai.com.
        </p>
        <input
          type="password"
          className="key-input"
          placeholder="sk-…"
          value={value}
          autoFocus
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && value.trim() && onSave(value.trim())}
        />
        <div className="key-actions">
          <button
            className="btn btn-primary"
            disabled={!value.trim()}
            onClick={() => onSave(value.trim())}
          >
            Save
          </button>
          <button className="btn" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

function Spinner({ large }: { large?: boolean }) {
  return <span className={`spinner ${large ? 'spinner-lg' : ''}`} />;
}
