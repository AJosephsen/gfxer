using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyGame.Core.Catalog;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var savesDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "StrategyGame", "saves");

var catalog = new CardCatalog();

// List all games
app.MapGet("/api/games", () =>
{
    if (!Directory.Exists(savesDir))
        return Results.Json(Array.Empty<object>());

    var games = Directory.GetFiles(savesDir, "*.json")
        .Select(path =>
        {
            var raw = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            return new
            {
                gameId = root.GetProperty("gameId").GetString(),
                playerName = root.GetProperty("playerName").GetString(),
                round = root.GetProperty("round").GetInt32(),
                lastPlayedAt = root.GetProperty("lastPlayedAt").GetString()
            };
        })
        .OrderByDescending(g => g.lastPlayedAt)
        .ToList();

    return Results.Json(games);
});

// Get full game state — returns raw JSON from the save file plus resolved card definitions
app.MapGet("/api/games/{gameId}", (string gameId) =>
{
    var path = Path.Combine(savesDir, $"{gameId}.json");
    if (!File.Exists(path))
        return Results.NotFound(new { error = $"Game '{gameId}' not found." });

    var raw = File.ReadAllText(path);
    using var doc = JsonDocument.Parse(raw);
    var root = doc.RootElement;

    return Results.Content(raw, "application/json");
});

// Get the card catalog (definitions for rendering)
app.MapGet("/api/catalog", () =>
{
    var defs = catalog.All.Select<CardDefinition, object>(d => d switch
    {
        BuildingDefinition b => new
        {
            id = b.Id,
            type = "building",
            name = b.Name,
            description = b.Description,
            buildingType = b.BuildingType.ToString(),
            allowedTerrains = b.AllowedTerrains.Select(t => t.ToString()).ToArray(),
            occupies = b.Occupies,
            production = new { food = b.Production.Food, people = b.Production.People, wood = b.Production.Wood },
            upkeep = new { food = b.Upkeep.Food, people = b.Upkeep.People, wood = b.Upkeep.Wood },
            playCost = new { food = b.PlayCost.Food, people = b.PlayCost.People, wood = b.PlayCost.Wood },
            fluxCost = b.FluxCost,
            investCost = new { food = b.InvestCost.Food, people = b.InvestCost.People, wood = b.InvestCost.Wood }
        },
        LandDefinition l => new
        {
            id = l.Id,
            type = "land",
            name = l.Name,
            description = l.Description,
            terrain = l.Terrain.ToString(),
            fluxCost = (int?)l.FluxCost,
            investCost = new { food = l.InvestCost.Food, people = l.InvestCost.People, wood = l.InvestCost.Wood }
        } as object,
        _ => new { id = d.Id, type = "unknown", name = d.Name }
    }).ToList();

    return Results.Json(defs);
});

// Serve UI assets (backgrounds etc.)
app.MapGet("/assets/ui/{filename}", (string filename) =>
{
    if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
        return Results.BadRequest();
    var path = Path.Combine(AppContext.BaseDirectory, "Assets", "UI", filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/webp");
});

// Serve terrain art assets (for board cells)
app.MapGet("/assets/lands/{filename}", (string filename) =>
{
    if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
        return Results.BadRequest();
    var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Lands", filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/webp");
});

// Serve card art assets
app.MapGet("/assets/cards/{filename}", (string filename) =>
{
    // Prevent path traversal
    if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
        return Results.BadRequest();
    var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Cards", filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/webp");
});

// Version info — version.txt and changelog.json built at deploy time by rebuild.sh
var changelogPath = Path.Combine(AppContext.BaseDirectory, "changelog.json");
var changelogJson = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "[]";
var versionPath = Path.Combine(AppContext.BaseDirectory, "version.txt");
var buildVersion = File.Exists(versionPath) ? File.ReadAllText(versionPath).Trim() : "unknown";

app.MapGet("/api/version", () =>
{
    return Results.Content($$"""
    {
      "name": "Ironhold \u2014 Wars of the Realm",
      "version": "{{buildVersion}}",
      "framework": "{{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}}",
      "changelog": {{changelogJson}}
    }
    """, "application/json");
});

// Serve the viewer HTML
app.MapGet("/", () => Results.Content(ViewerHtml.Content, "text/html"));

app.Run("http://0.0.0.0:5050");

static class ViewerHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Ironhold — Wars of the Realm</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Cinzel+Decorative:wght@700&family=Cinzel:wght@400;600;700&display=swap" rel="stylesheet">
<style>
  :root {
    --gold:        #c8a84b;
    --gold-bright: #e8c96a;
    --gold-dim:    #8a6e2f;
    --gold-dark:   #4a3a18;
    --parchment:   #d4b896;
    --ink:         #1a160e;
    --panel-bg:    rgba(16, 12, 6, 0.72);
    --panel-border:rgba(200, 168, 75, 0.35);
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Cinzel', Georgia, serif;
    background-color: #0d0f1a;
    background-image: url(/assets/ui/board-background.webp);
    background-size: cover;
    background-position: center top;
    background-attachment: fixed;
    color: var(--gold);
    min-height: 100vh;
    padding: 8px;
  }
  /* Dark vignette overlay so UI elements stay readable over the art */
  body::before {
    content: '';
    position: fixed;
    inset: 0;
    background: radial-gradient(ellipse at center, rgba(13,15,26,0.55) 0%, rgba(13,15,26,0.82) 100%);
    pointer-events: none;
    z-index: 0;
  }
  #viewer { position: relative; z-index: 1; }

  /* ── Emblem overlay (top-left) ── */
  #emblem {
    position: fixed;
    top: 16px;
    left: 16px;
    z-index: 100;
    width: 46px;
    background: linear-gradient(170deg, rgba(14,10,4,0.97) 0%, rgba(24,18,7,0.98) 100%);
    border: 1px solid var(--gold-dim);
    box-shadow:
      0 0 0 3px rgba(0,0,0,0.65),
      0 0 0 4px var(--gold-dark),
      0 6px 30px rgba(0,0,0,0.9);
    border-radius: 4px;
    padding: 8px 6px;
    text-align: center;
    font-family: 'Cinzel', serif;
    cursor: pointer;
    overflow: hidden;
    transition: width 0.25s ease, padding 0.25s ease;
    user-select: none;
  }
  #emblem.open {
    width: 188px;
    padding: 12px 10px 10px;
    cursor: default;
  }
  .emblem-crest {
    font-size: 18px;
    opacity: 0.9;
    line-height: 1;
    cursor: pointer;
  }
  #emblem.open .emblem-crest { font-size: 15px; margin-bottom: 2px; cursor: default; }
  .emblem-body {
    display: none;
    opacity: 0;
    transition: opacity 0.2s ease 0.1s;
  }
  #emblem.open .emblem-body {
    display: block;
    opacity: 1;
  }
  .emblem-title {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 15px;
    letter-spacing: 3px;
    text-shadow: 0 0 14px rgba(232,201,106,0.5), 0 1px 4px rgba(0,0,0,0.9);
  }
  .emblem-subtitle {
    color: var(--gold-dim);
    font-size: 8px;
    letter-spacing: 2.5px;
    text-transform: uppercase;
    margin-top: 2px;
  }
  .emblem-rule {
    border: none;
    border-top: 1px solid var(--gold-dark);
    margin: 8px 4px;
  }
  #emblem select {
    background: rgba(10,7,2,0.85);
    color: var(--gold);
    border: 1px solid var(--gold-dark);
    padding: 4px 6px;
    font-family: 'Cinzel', serif;
    font-size: 9px;
    border-radius: 3px;
    cursor: pointer;
    width: 100%;
  }
  .emblem-status {
    color: var(--gold-dark);
    font-size: 8px;
    letter-spacing: 0.5px;
    margin-top: 6px;
  }
  .emblem-close {
    display: block;
    color: var(--gold-dark);
    font-size: 8px;
    letter-spacing: 1px;
    margin-top: 8px;
    text-transform: uppercase;
    cursor: pointer;
  }
  .emblem-close:hover { color: var(--gold-dim); }
  .emblem-status.live { color: #6dbf7e; }
  .emblem-info {
    display: block;
    color: var(--gold-dark);
    font-size: 8px;
    letter-spacing: 1px;
    margin-top: 6px;
    text-transform: uppercase;
    cursor: pointer;
  }
  .emblem-info:hover { color: var(--gold-dim); }

  /* ── Info popup overlay ── */
  .info-popup {
    position: fixed;
    inset: 0;
    z-index: 200;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0,0,0,0.72);
    backdrop-filter: blur(3px);
  }
  .info-popup.hidden { display: none; }
  .info-popup-panel {
    background: linear-gradient(170deg, rgba(14,10,4,0.99) 0%, rgba(24,18,7,0.99) 100%);
    border: 1px solid var(--gold-dim);
    box-shadow:
      0 0 0 3px rgba(0,0,0,0.65),
      0 0 0 4px var(--gold-dark),
      0 12px 60px rgba(0,0,0,0.95);
    border-radius: 6px;
    padding: 24px 28px;
    width: min(500px, 92vw);
    max-height: 80vh;
    overflow-y: auto;
    font-family: 'Cinzel', serif;
  }
  .info-popup-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 18px;
  }
  .info-popup-title {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 15px;
    letter-spacing: 2px;
    text-shadow: 0 0 14px rgba(232,201,106,0.4);
  }
  .info-popup-close {
    background: none;
    border: 1px solid var(--gold-dark);
    color: var(--gold-dim);
    font-family: 'Cinzel', serif;
    font-size: 10px;
    padding: 3px 10px;
    border-radius: 3px;
    cursor: pointer;
    letter-spacing: 1px;
  }
  .info-popup-close:hover { color: var(--gold-bright); border-color: var(--gold-dim); }
  .info-section-title {
    color: var(--gold);
    font-size: 9px;
    letter-spacing: 2.5px;
    text-transform: uppercase;
    border-bottom: 1px solid var(--gold-dark);
    padding-bottom: 5px;
    margin-bottom: 10px;
  }
  .info-section-gap { margin-bottom: 18px; }
  .info-kv {
    color: var(--gold-dim);
    font-size: 9px;
    letter-spacing: 0.5px;
    margin-bottom: 5px;
    display: flex;
    gap: 6px;
  }
  .info-kv .ik { min-width: 72px; text-transform: uppercase; letter-spacing: 1px; }
  .info-kv .iv { color: var(--gold); }
  .info-chronicle-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 7px 10px;
    margin-bottom: 5px;
    border: 1px solid var(--gold-dark);
    border-radius: 3px;
    cursor: pointer;
  }
  .info-chronicle-row:hover { border-color: var(--gold-dim); background: rgba(200,168,75,0.05); }
  .info-chronicle-row.active { border-color: var(--gold); background: rgba(200,168,75,0.08); }
  .info-chronicle-name { color: var(--gold); font-size: 10px; letter-spacing: 1px; }
  .info-chronicle-id  { color: var(--gold-dark); font-size: 8px; margin-top: 1px; }
  .info-chronicle-meta { color: var(--gold-dim); font-size: 8px; letter-spacing: 0.5px; text-align: right; }
  .info-changelog-row {
    display: flex;
    gap: 10px;
    align-items: baseline;
    padding: 4px 0;
    border-bottom: 1px solid rgba(74,58,24,0.4);
    font-size: 9px;
  }
  .info-changelog-row:last-child { border-bottom: none; }
  .info-cl-hash { color: var(--gold-dark); font-variant-numeric: tabular-nums; min-width: 52px; }
  .info-cl-date { color: var(--gold-dark); min-width: 72px; }
  .info-cl-msg  { color: var(--gold-dim); flex: 1; }

  /* ── Stats bar ── */
  #stats-bar {
    position: fixed;
    top: 0;
    left: 78px;
    right: 0;
    z-index: 99;
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 16px;
    background: linear-gradient(to bottom, rgba(10,7,3,0.92) 0%, rgba(10,7,3,0.0) 100%);
    font-family: 'Cinzel', serif;
    font-size: 11px;
    flex-wrap: nowrap;
    overflow: hidden;
  }
  .sp {
    background: rgba(14,10,4,0.82);
    border: 1px solid var(--gold-dark);
    border-radius: 14px;
    padding: 3px 11px;
    display: flex;
    align-items: center;
    gap: 5px;
    white-space: nowrap;
  }
  .sp .sv { font-weight: 700; color: var(--gold-bright); }
  .sp .sl { color: var(--gold-dim); font-size: 8px; text-transform: uppercase; letter-spacing: 1px; }
  #stats-bar .divider { color: var(--gold-dark); margin: 0 2px; }
  #stats-bar .round-label {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 13px;
    letter-spacing: 2px;
    margin-right: 4px;
  }
  #stats-bar .player-label {
    color: var(--parchment);
    font-size: 11px;
    margin-right: 8px;
  }



  .board-section {
    max-width: 1120px;
    margin: 0 auto 24px;
  }
  .board-section h2 {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 16px;
    margin-bottom: 10px;
    text-align: center;
    letter-spacing: 2px;
    text-shadow: 0 0 12px rgba(232,201,106,0.3);
  }
  .board-container {
    display: flex;
    justify-content: center;
    margin-bottom: 24px;
  }
  .board {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    justify-content: center;
  }
  .cell {
    width: 196px;
    height: 156px;
    border-radius: 8px;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 600;
    position: relative;
    transition: all 0.3s ease;
  }
  .cell.locked { background: rgba(0,0,0,0.6); border: 1px dashed var(--gold-dark); color: var(--gold-dark); }
  .cell.empty { background: rgba(13,15,26,0.5); border: 1px solid var(--panel-border); color: var(--gold-dim); }
  .cell.placeholder {
    background: rgba(13,15,26,0.22);
    border: 1px dashed var(--panel-border);
    box-shadow: inset 0 0 0 1px rgba(232,201,106,0.06);
  }
  .cell.placeholder .placeholder-label {
    font-family: 'Cinzel', serif;
    font-size: 11px;
    color: var(--gold-dark);
    letter-spacing: 1px;
    text-transform: uppercase;
  }
  /* terrain cells — art background set via JS; tint overlay for readability */
  .cell.has-art {
    border: 2px solid rgba(200, 168, 75, 0.62);
    background-color: rgba(228, 197, 126, 0.12);
    background-blend-mode: screen;
    background-size: 112% auto;
    background-position: center;
    background-repeat: no-repeat;
    box-shadow:
      inset 0 0 0 1px rgba(232,201,106,0.18),
      0 0 0 1px rgba(38,28,9,0.82);
  }
  .cell.has-art::before {
    content: '';
    position: absolute;
    inset: 0;
    background: linear-gradient(to bottom, rgba(8,6,2,0.06) 0%, rgba(8,6,2,0.18) 100%);
    border-radius: inherit;
  }
  /* keep text above the tint overlay */
  .cell > * { position: relative; z-index: 1; }
  .cell .terrain-name { font-family: 'Cinzel', serif; font-size: 11px; font-weight: 600; color: var(--gold-bright); text-shadow: 0 1px 4px #000, 0 0 8px rgba(0,0,0,0.8); letter-spacing: 1px; }
  .cell .building-name {
    font-family: 'Cinzel', serif;
    font-size: 10px;
    font-weight: 600;
    color: var(--gold-bright);
    text-shadow: 0 1px 4px #000, 0 0 8px rgba(0,0,0,0.8);
    letter-spacing: 0.8px;
    text-transform: uppercase;
  }
  .cell .building-name.disabled { color: #d98b75; text-decoration: line-through; }
  .cell .fertility { font-family: 'Cinzel', serif; font-size: 9px; color: var(--gold); opacity: 0.85; position: absolute; top: 5px; right: 6px; text-shadow: 0 1px 3px #000; }
  .cell .workers { font-family: 'Cinzel', serif; font-size: 9px; color: var(--parchment); opacity: 0.9; position: absolute; bottom: 5px; right: 6px; text-shadow: 0 1px 3px #000; }
  .cell .stack-header {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 24px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(10,8,4,0.9);
    border-bottom: 1px solid rgba(200,168,75,0.6);
    z-index: 2;
  }
  .cell .stack-title {
    font-family: 'Cinzel', serif;
    font-size: 10px;
    font-weight: 700;
    color: var(--gold-bright);
    letter-spacing: 0.9px;
    text-transform: uppercase;
    text-shadow: 0 1px 4px #000, 0 0 8px rgba(0,0,0,0.8);
    text-align: center;
    max-width: 74%;
  }
  .cell .stack-badge {
    position: absolute;
    top: 4px;
    left: 6px;
    font-family: 'Cinzel', serif;
    font-size: 7px;
    color: var(--parchment);
    background: rgba(10,8,4,0.78);
    border: 1px solid var(--gold-dark);
    border-radius: 3px;
    padding: 1px 5px;
    letter-spacing: 0.5px;
    z-index: 3;
  }
  .cell .stack-footer {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    min-height: 38px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    gap: 2px;
    padding: 5px 8px 6px;
    background: rgba(10,8,4,0.9);
    border-top: 1px solid rgba(200,168,75,0.6);
    z-index: 2;
  }
  .cell .stack-stats {
    position: static;
    font-family: 'Cinzel', serif;
    font-size: 9px;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px #000;
    line-height: 1.35;
    text-align: center;
  }

  .hand-section {
    max-width: 800px;
    margin: 0 auto;
  }
  .hand-section h2 {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 16px;
    margin-bottom: 10px;
    text-align: center;
    letter-spacing: 2px;
    text-shadow: 0 0 12px rgba(232,201,106,0.3);
  }
  .hand {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    justify-content: center;
  }
  /* ── Art-backed card ───────────────────────────────── */
  .card {
    position: relative;
    width: 184px;
    height: 276px;
    border-radius: 4px;
    overflow: hidden;
    background-size: cover;
    background-position: center;
    flex-shrink: 0;
  }
  /* Title bar overlay — sits in the frame's title area */
  .card .card-title {
    position: absolute;
    top: 5px;
    left: 5px;
    right: 5px;
    height: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-family: 'Cinzel', serif;
    font-size: 10px;
    font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px #000, 0 0 6px #000;
    text-align: center;
    letter-spacing: 1px;
    pointer-events: none;
  }
  /* Stats panel overlay — sits in the frame's bottom stats area */
  .card .card-stats {
    position: absolute;
    top: 210px;
    left: 8px;
    right: 8px;
    bottom: 6px;
    font-size: 11px;
    color: #c8b996;
    text-shadow: 0 1px 2px #000;
    line-height: 1.5;
    overflow: hidden;
    pointer-events: none;
  }
  .card .card-stats .stat-row {
    display: flex;
    justify-content: space-between;
  }
  .card .card-stats .stat-label { font-family: 'Cinzel', serif; color: var(--gold-bright); font-size: 10px; letter-spacing: 0.5px; }
  .card .card-stats .stat-value { font-family: 'Cinzel', serif; color: var(--gold-bright); font-size: 10px; }

  .discard-info {
    text-align: center;
    margin-top: 12px;
    color: var(--gold-dim);
    font-size: 11px;
    letter-spacing: 1px;
  }

  .empty-state {
    text-align: center;
    padding: 60px;
    color: var(--gold-dim);
    font-family: 'Cinzel', serif;
    letter-spacing: 1px;
  }
</style>
</head>
<body>
<div id="emblem" title="Open menu">
  <div class="emblem-crest">⚔</div>
  <div class="emblem-body">
    <div class="emblem-title">Ironhold</div>
    <div class="emblem-subtitle">Wars of the Realm</div>
    <hr class="emblem-rule">
    <select id="gamePicker"><option value="">Choose a chronicle…</option></select>
    <div id="status" class="emblem-status">Connecting…</div>
    <span class="emblem-info" id="emblem-info">📜 Chronicles</span>
    <span class="emblem-close" id="emblem-close">▲ close</span>
  </div>
</div>

<div id="info-popup" class="info-popup hidden">
  <div class="info-popup-panel">
    <div class="info-popup-header">
      <span class="info-popup-title">⚔ Chronicles</span>
      <button class="info-popup-close" id="info-popup-close">✕ close</button>
    </div>
    <div id="info-popup-body"><div style="color:var(--gold-dim);font-size:10px;text-align:center;padding:20px">Loading…</div></div>
  </div>
</div>

<div id="stats-bar"></div>

<div id="viewer" class="empty-state">Choose a chronicle to watch</div>

<script>
const API = '';
let catalog = {};
let currentGameId = null;
let pollTimer = null;
let lastJson = '';

async function loadCatalog() {
  const res = await fetch(`${API}/api/catalog`);
  const defs = await res.json();
  for (const d of defs) catalog[d.id] = d;
}

async function loadGames() {
  const res = await fetch(`${API}/api/games`);
  const games = await res.json();
  const picker = document.getElementById('gamePicker');
  // keep current selection
  const cur = picker.value;
  picker.innerHTML = '<option value="">Select a game...</option>';
  for (const g of games) {
    const opt = document.createElement('option');
    opt.value = g.gameId;
    opt.textContent = `${g.gameId} — ${g.playerName} — Round ${g.round}`;
    picker.appendChild(opt);
  }
  if (cur) picker.value = cur;
  // auto-select most recent if nothing selected
  if (!picker.value && games.length > 0) {
    picker.value = games[0].gameId;
    selectGame(games[0].gameId);
  }
}

function selectGame(gameId) {
  currentGameId = gameId;
  lastJson = '';
  if (pollTimer) clearInterval(pollTimer);
  if (!gameId) {
    document.getElementById('viewer').innerHTML = '<div class="empty-state">Select a game to watch</div>';
    return;
  }
  poll();
  pollTimer = setInterval(poll, 1000);
}

async function poll() {
  if (!currentGameId) return;
  try {
    const res = await fetch(`${API}/api/games/${currentGameId}`);
    if (!res.ok) throw new Error('Game not found');
    const text = await res.text();
    if (text === lastJson) return; // no changes
    lastJson = text;
    const game = JSON.parse(text);
    render(game);
    document.getElementById('status').textContent = `Live — ${new Date().toLocaleTimeString()}`;
    document.getElementById('status').className = 'emblem-status live';
  } catch (e) {
    document.getElementById('status').textContent = `Error: ${e.message}`;
    document.getElementById('status').className = 'emblem-status';
  }
}

function render(game) {
  const v = document.getElementById('viewer');
  v.innerHTML = '';

  // Resource calcs (used for both emblem and board render)
  const res = game.resources;
  let totalOccupied = 0;
  let popCap = 0;
  if (game.board && game.board.cells) {
    for (const cell of game.board.cells) {
      if (cell.building) {
        popCap += cell.building.populationCapacity || 0;
        if (cell.building.isActive) totalOccupied += cell.building.assignedWorkers || 0;
      }
    }
  }
  const available = res.people - totalOccupied;

  // Update stats bar
  document.getElementById('stats-bar').innerHTML =
    `<span class="round-label">Round ${game.round}</span>` +
    `<span class="player-label">${game.playerName}</span>` +
    `<span class="divider">·</span>` +
    sp('🌾', res.food, 'Food') +
    sp('🪵', res.wood, 'Wood') +
    sp('👥', `${res.people}/${popCap} · ${totalOccupied}⛏`, 'Pop') +
    sp('⚡', `${res.flux ?? res.focus ?? 0}/14`, 'Flux') +
    sp('🃏', game.landDeck ? game.landDeck.length : '?', 'Deck');

  // Board — aggregate by land type into unified terrain stacks
  const boardSection = el('div', 'board-section');
  const boardWrap = el('div', 'board-container');
  const board = el('div', 'board');
  const cells = game.board.cells;
  const placedCells = cells
    .filter(c => c.land)
    .sort((a, b) => (a.row * 100 + a.col) - (b.row * 100 + b.col));

  const stackMap = new Map();
  for (const cell of placedCells) {
    const landDef = catalog[cell.land.definitionId];
    const terrain = landDef && landDef.terrain ? landDef.terrain : 'Unknown';
    const stackKey = `${cell.land.definitionId}`;

    if (!stackMap.has(stackKey)) {
      stackMap.set(stackKey, {
        landId: cell.land.definitionId,
        landName: landDef ? landDef.name : cell.land.definitionId,
        terrain,
        cells: [],
      });
    }
    stackMap.get(stackKey).cells.push(cell);
  }

  const stacks = Array.from(stackMap.values())
    .sort((a, b) => a.landName.localeCompare(b.landName));

  boardSection.innerHTML = `<h2>Board Stacks (${stacks.length})</h2>`;

  for (const stack of stacks) {
    const div = el('div', 'cell');
    const stackCells = stack.cells;
    div.classList.add('has-art');
    div.style.backgroundImage = `url(/assets/lands/${stack.landId}.webp)`;

    const topBuildingCell = stackCells
      .filter(c => c.building)
      .sort((a, b) => (b.land.fertility || 0) - (a.land.fertility || 0))[0];
    if (topBuildingCell) {
      div.style.backgroundImage = `url(/assets/lands/${topBuildingCell.building.definitionId}.webp)`;
    }

    const stackCount = stackCells.length;
    const avgFertility = stackCells.reduce((sum, c) => sum + (c.land.fertility || 0), 0) / Math.max(stackCount, 1);
    const buildingCount = stackCells.filter(c => c.building).length;
    const estimatedProduction = stackCells.reduce((sum, c) => {
      if (!c.building) return sum;
      const bDef = catalog[c.building.definitionId];
      if (!bDef) return sum;
      const occupies = bDef.occupies || 0;
      const assigned = c.building.assignedWorkers || 0;
      const ratio = occupies > 0 ? Math.min(1, assigned / occupies) : 1;
      return {
        food: sum.food + Math.round((bDef.production?.food || 0) * ratio),
        wood: sum.wood + Math.round((bDef.production?.wood || 0) * ratio),
        people: sum.people + Math.round((bDef.production?.people || 0) * ratio),
      };
    }, { food: 0, wood: 0, people: 0 });

    const badge = `Cells ${buildingCount}/${stackCount}`;

    let statParts = [`avg fert ×${(avgFertility / 10).toFixed(1)}`];
    if (buildingCount > 0) statParts.push(`buildings ${buildingCount}`);
    const prodParts = [];
    if (estimatedProduction.food > 0) prodParts.push(`+${estimatedProduction.food}🌾`);
    if (estimatedProduction.wood > 0) prodParts.push(`+${estimatedProduction.wood}🪵`);
    if (estimatedProduction.people > 0) prodParts.push(`+${estimatedProduction.people}👥`);
    if (prodParts.length > 0) statParts.push(`prod ${prodParts.join(' ')}`);

    const displayTitle = stack.landName;

    let html = `<div class="stack-header"><span class="stack-badge">${badge}</span><span class="stack-title">${displayTitle}</span></div>`;
    html += `<div class="stack-footer">`;

    if (topBuildingCell) {
      const topDef = catalog[topBuildingCell.building.definitionId];
      const topName = topDef ? topDef.name : topBuildingCell.building.definitionId;
      html += `<span class="building-name">${topName}</span>`;
    }

    html += `<span class="stack-stats">${statParts.join(' · ')}</span>`;
    html += `</div>`;

    div.innerHTML = html;
    board.appendChild(div);
  }

  if (placedCells.length < cells.length) {
    const placeholder = el('div', 'cell placeholder');
    placeholder.innerHTML = `<span class="placeholder-label">Next Slot</span>`;
    board.appendChild(placeholder);
  }

  boardWrap.appendChild(board);
  boardSection.appendChild(boardWrap);
  v.appendChild(boardSection);

  // Hand
  const handSection = el('div', 'hand-section');
  const handCount = game.hand ? game.hand.length : 0;
  handSection.innerHTML = `<h2>Hand (${handCount}/7)</h2>`;
  const handDiv = el('div', 'hand');
  if (game.hand) {
    for (const card of game.hand) {
      handDiv.appendChild(makeCard(card));
    }
  }
  handSection.appendChild(handDiv);
  // Discard pile
  if (game.discardPile && game.discardPile.length > 0) {
    const disc = el('div', 'discard-info');
    disc.textContent = `Discard pile: ${game.discardPile.length} card(s)`;
    handSection.appendChild(disc);
  }
  v.appendChild(handSection);
}

function makeCard(card) {
  const def = catalog[card.definitionId];
  const cardDiv = el('div', 'card');
  cardDiv.style.backgroundImage = `url(/assets/cards/${card.definitionId}.webp)`;

  // Title bar
  const title = el('div', 'card-title');
  title.textContent = def ? def.name : card.definitionId;
  cardDiv.appendChild(title);

  // Stats panel
  const stats = el('div', 'card-stats');
  const rows = [];

  if (card.$type === 'land' && def) {
    if (card.fertility)
      rows.push(['Fertility', `×${(card.fertility / 10).toFixed(1)}`]);
    if (card.accessibilityCost)
      rows.push(['Access', `×${(card.accessibilityCost / 10).toFixed(1)}`]);
    rows.push(['Flux', def.fluxCost]);
  } else if (def) {
    if (def.occupies > 0)
      rows.push(['Workers', `0–${def.occupies}`]);
    const prod = [];
    if (def.production?.food)   prod.push(`${def.production.food}🌾`);
    if (def.production?.people) prod.push(`${def.production.people}👥`);
    if (def.production?.wood)   prod.push(`${def.production.wood}🪵`);
    if (prod.length) rows.push(['+', prod.join(' ')]);
    const up = [];
    if (def.upkeep?.food)   up.push(`${def.upkeep.food}🌾`);
    if (def.upkeep?.people) up.push(`${def.upkeep.people}👥`);
    if (up.length) rows.push(['–', up.join(' ')]);
    rows.push(['Flux', def.fluxCost]);
  }

  stats.innerHTML = rows.map(([l, v]) =>
    `<div class="stat-row"><span class="stat-label">${l}</span><span class="stat-value">${v}</span></div>`
  ).join('');
  cardDiv.appendChild(stats);

  return cardDiv;
}

function sp(emoji, value, label) {
  return `<div class="sp"><span>${emoji}</span><span class="sv">${value}</span><span class="sl">${label}</span></div>`;
}

function el(tag, cls) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  return e;
}

// Emblem expand/collapse
const emblemEl = document.getElementById('emblem');
document.getElementById('emblem-close').addEventListener('click', e => {
  e.stopPropagation();
  emblemEl.classList.remove('open');
  emblemEl.title = 'Open menu';
});
emblemEl.addEventListener('click', () => {
  if (!emblemEl.classList.contains('open')) {
    emblemEl.classList.add('open');
    emblemEl.title = '';
  }
});

// Info popup
const infoPopupEl = document.getElementById('info-popup');
document.getElementById('emblem-info').addEventListener('click', e => {
  e.stopPropagation();
  openInfoPopup();
});
document.getElementById('info-popup-close').addEventListener('click', () => closeInfoPopup());
infoPopupEl.addEventListener('click', e => { if (e.target === infoPopupEl) closeInfoPopup(); });

function openInfoPopup() {
  infoPopupEl.classList.remove('hidden');
  refreshInfoPopup();
}
function closeInfoPopup() {
  infoPopupEl.classList.add('hidden');
}

async function refreshInfoPopup() {
  const body = document.getElementById('info-popup-body');
  try {
    const [verRes, gamesRes] = await Promise.all([
      fetch(`${API}/api/version`),
      fetch(`${API}/api/games`)
    ]);
    const ver = await verRes.json();
    const games = await gamesRes.json();

    let html = `<div class="info-section-title">Version</div>`;
    html += kv('Game', ver.name);
    html += kv('Version', ver.version);
    html += kv('Runtime', ver.framework);
    html += `<div class="info-section-gap"></div>`;

    if (ver.changelog && ver.changelog.length > 0) {
      html += `<div class="info-section-title">Changelog (last ${ver.changelog.length})</div>`;
      for (const c of ver.changelog) {
        const msg = c.message.replace(/</g,'&lt;').replace(/>/g,'&gt;');
        html += `<div class="info-changelog-row"><span class="info-cl-hash">${c.hash}</span><span class="info-cl-date">${c.date}</span><span class="info-cl-msg">${msg}</span></div>`;
      }
      html += `<div class="info-section-gap"></div>`;
    }

    html += `<div class="info-section-title">Chronicles (${games.length})</div>`;
    if (games.length === 0) {
      html += `<div class="info-kv" style="justify-content:center;padding:12px">No saved chronicles found</div>`;
    } else {
      for (const g of games) {
        const isActive = g.gameId === currentGameId;
        const created = g.lastPlayedAt ? new Date(g.lastPlayedAt).toLocaleString() : '—';
        html += `
          <div class="info-chronicle-row${isActive ? ' active' : ''}" onclick="selectFromInfo('${g.gameId}')">
            <div>
              <div class="info-chronicle-name">${g.playerName}</div>
              <div class="info-chronicle-id">${g.gameId}</div>
            </div>
            <div class="info-chronicle-meta">
              <div>Round ${g.round}</div>
              <div>${created}</div>
            </div>
          </div>`;
      }
    }

    body.innerHTML = html;
  } catch (err) {
    body.innerHTML = `<div class="info-kv" style="color:#c44;text-align:center;padding:16px">Failed to load: ${err.message}</div>`;
  }
}

function kv(label, value) {
  return `<div class="info-kv"><span class="ik">${label}</span><span class="iv">${value ?? '—'}</span></div>`;
}

function selectFromInfo(gameId) {
  document.getElementById('gamePicker').value = gameId;
  selectGame(gameId);
  closeInfoPopup();
}

// Init
document.getElementById('gamePicker').addEventListener('change', e => selectGame(e.target.value));

(async () => {
  await loadCatalog();
  await loadGames();
  // Refresh game list every 10s
  setInterval(loadGames, 10000);
})();
</script>
</body>
</html>
""";
}
