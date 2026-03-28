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
            focusCost = b.FocusCost,
            investCost = new { food = b.InvestCost.Food, people = b.InvestCost.People, wood = b.InvestCost.Wood }
        },
        LandDefinition l => new
        {
            id = l.Id,
            type = "land",
            name = l.Name,
            description = l.Description,
            terrain = l.Terrain.ToString(),
            focusCost = (int?)l.FocusCost,
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
    padding: 20px;
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
  .header, .game-picker, #viewer { position: relative; z-index: 1; }
  .header {
    text-align: center;
    margin-bottom: 16px;
  }
  .header h1 {
    font-family: 'Cinzel Decorative', serif;
    color: var(--gold-bright);
    font-size: 36px;
    letter-spacing: 6px;
    text-shadow: 0 0 30px rgba(232,201,106,0.5), 0 2px 6px rgba(0,0,0,0.9);
  }
  .header-subtitle {
    font-family: 'Cinzel', serif;
    color: var(--gold-dim);
    font-size: 12px;
    letter-spacing: 4px;
    text-transform: uppercase;
    margin-top: 4px;
    margin-bottom: 2px;
    text-shadow: 0 1px 4px rgba(0,0,0,0.8);
  }
  .header .status { color: var(--gold-dim); font-size: 10px; margin-top: 4px; letter-spacing: 1px; }
  .header .status.live { color: #6dbf7e; }

  .game-picker {
    text-align: center;
    margin-bottom: 16px;
  }
  .game-picker select {
    background: var(--panel-bg);
    color: var(--gold);
    border: 1px solid var(--panel-border);
    padding: 8px 16px;
    font-family: 'Cinzel', serif;
    font-size: 13px;
    border-radius: 6px;
    cursor: pointer;
  }

  /* ── Compact stats bar ── */
  .stats-bar {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 8px;
    margin-bottom: 10px;
    flex-wrap: wrap;
    font-size: 12px;
  }
  .stat-pill {
    background: var(--panel-bg);
    border: 1px solid var(--panel-border);
    border-radius: 20px;
    padding: 4px 12px;
    display: flex;
    align-items: center;
    gap: 5px;
    white-space: nowrap;
    font-family: 'Cinzel', serif;
  }
  .stat-pill .emoji { font-size: 13px; }
  .stat-pill .val { font-weight: 700; font-size: 13px; color: var(--gold-bright); }
  .stat-pill .lbl { color: var(--gold-dim); font-size: 9px; text-transform: uppercase; letter-spacing: 1px; }

  .info-bar {
    text-align: center;
    margin-bottom: 8px;
    color: var(--gold-dim);
    font-size: 11px;
    letter-spacing: 1px;
  }
  .info-bar .round { color: var(--gold-bright); font-weight: 700; font-size: 14px; font-family: 'Cinzel Decorative', serif; letter-spacing: 2px; }
  .info-bar .player { color: var(--parchment); }

  .board-container {
    display: flex;
    justify-content: center;
    margin-bottom: 24px;
  }
  .board {
    display: grid;
    gap: 4px;
  }
  .cell {
    width: 180px;
    height: 144px;
    border-radius: 8px;
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
  /* terrain cells — art background set via JS; tint overlay for readability */
  .cell.has-art {
    border: 1px solid var(--panel-border);
    background-size: cover;
    background-position: center;
  }
  .cell.has-art::before {
    content: '';
    position: absolute;
    inset: 0;
    background: rgba(0,0,0,0.35);
    border-radius: inherit;
  }
  /* keep text above the tint overlay */
  .cell > * { position: relative; z-index: 1; }
  .cell .terrain-name { font-family: 'Cinzel', serif; font-size: 11px; font-weight: 600; color: var(--gold-bright); text-shadow: 0 1px 4px #000, 0 0 8px rgba(0,0,0,0.8); letter-spacing: 1px; }
  .cell .building-name {
    font-family: 'Cinzel', serif;
    font-size: 10px;
    font-weight: 700;
    background: rgba(10,8,4,0.7);
    color: var(--parchment);
    border: 1px solid var(--gold-dark);
    padding: 2px 8px;
    border-radius: 3px;
    margin-top: 4px;
    letter-spacing: 0.5px;
  }
  .cell .building-name.disabled { color: #c0392b; text-decoration: line-through; }
  .cell .fertility { font-family: 'Cinzel', serif; font-size: 9px; color: var(--gold); opacity: 0.85; position: absolute; top: 5px; right: 6px; text-shadow: 0 1px 3px #000; }
  .cell .workers { font-family: 'Cinzel', serif; font-size: 9px; color: var(--parchment); opacity: 0.9; position: absolute; bottom: 5px; right: 6px; text-shadow: 0 1px 3px #000; }

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
    gap: 10px;
    flex-wrap: wrap;
    justify-content: center;
  }
  /* ── Art-backed card ───────────────────────────────── */
  .card {
    position: relative;
    width: 160px;
    height: 240px;
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
    font-size: 8px;
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
    top: 178px;
    left: 8px;
    right: 8px;
    bottom: 6px;
    font-size: 8px;
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
  .card .card-stats .stat-label { font-family: 'Cinzel', serif; color: var(--gold-dim); font-size: 7px; letter-spacing: 0.5px; }
  .card .card-stats .stat-value { font-family: 'Cinzel', serif; color: var(--gold-bright); font-size: 7px; }

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
<div class="header">
  <h1>Ironhold</h1>
  <p class="header-subtitle">Wars of the Realm</p>
  <div class="status" id="status">Connecting...</div>
</div>

<div class="game-picker">
  <select id="gamePicker"><option value="">Choose a chronicle...</option></select>
</div>

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
    document.getElementById('status').textContent = `Live — polling every 1s — ${new Date().toLocaleTimeString()}`;
    document.getElementById('status').className = 'status live';
  } catch (e) {
    document.getElementById('status').textContent = `Error: ${e.message}`;
    document.getElementById('status').className = 'status';
  }
}

function render(game) {
  const v = document.getElementById('viewer');
  v.innerHTML = '';

  // Info bar
  const info = el('div', 'info-bar');
  info.innerHTML = `<span class="round">Round ${game.round}</span> &nbsp;·&nbsp; <span class="player">${game.playerName}</span>`;
  v.appendChild(info);

  // Compact stats bar
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
  const statsBar = el('div', 'stats-bar');
  statsBar.innerHTML = [
    pill('food',   '🌾', res.food,              'Food'),
    pill('wood',   '🪵', res.wood,              'Wood'),
    pill('people', '👥', `${res.people}/${popCap}`, 'Pop'),
    pill('focus',  '⚡', `${res.focus}/14`,     'Focus'),
    pill('deck',   '🃏', game.landDeck ? game.landDeck.length : '?', 'Deck'),
    pill('people', '🏠', available,             'Free'),
  ].join('');
  v.appendChild(statsBar);

  // Board — show first 10 cells in a 5×2 grid
  const boardWrap = el('div', 'board-container');
  const board = el('div', 'board');
  const cells = game.board.cells;
  const COLS = 5, ROWS = 2;
  board.style.gridTemplateColumns = `repeat(${COLS}, 180px)`;
  board.style.gridTemplateRows    = `repeat(${ROWS}, 144px)`;

  // Take only the first COLS*ROWS cells (top-left of the grid)
  const visibleCells = cells
    .filter(c => c.row < ROWS && c.col < COLS)
    .slice(0, COLS * ROWS);

  for (const cell of visibleCells) {
    const div = el('div', 'cell');
    if (cell.isLocked) {
      div.classList.add('locked');
      div.innerHTML = '🔒';
    } else if (!cell.land) {
      div.classList.add('empty');
      div.innerHTML = '···';
    } else {
      const landDef = catalog[cell.land.definitionId];
      div.classList.add('has-art');
      div.style.backgroundImage = `url(/assets/lands/${cell.land.definitionId}.webp)`;
      let html = `<span class="terrain-name">${landDef ? landDef.name : cell.land.definitionId}</span>`;
      // Fertility
      if (cell.land.fertility) {
        html += `<span class="fertility">×${(cell.land.fertility / 10).toFixed(1)}</span>`;
      }
      // Building: switch background to building art
      if (cell.building) {
        div.style.backgroundImage = `url(/assets/lands/${cell.building.definitionId}.webp)`;
        const bDef = catalog[cell.building.definitionId];
        const bName = bDef ? bDef.name : cell.building.definitionId;
        const cls = cell.building.isActive ? '' : ' disabled';
        html += `<span class="building-name${cls}">${bName}</span>`;
        // Show worker assignment
        if (bDef && bDef.occupies > 0) {
          const assigned = cell.building.assignedWorkers || 0;
          html += `<span class="workers">${assigned}/${bDef.occupies} 👷</span>`;
        }
        if (cell.building.populationCapacity > 0) {
          html += `<span class="workers">Cap: ${cell.building.populationCapacity} 🏘️</span>`;
        }
      }
      div.innerHTML = html;
    }
    div.style.gridRow = cell.row + 1;
    div.style.gridColumn = cell.col + 1;
    board.appendChild(div);
  }
  boardWrap.appendChild(board);
  v.appendChild(boardWrap);

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
    rows.push(['Focus', def.focusCost]);
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
    rows.push(['Focus', def.focusCost]);
  }

  stats.innerHTML = rows.map(([l, v]) =>
    `<div class="stat-row"><span class="stat-label">${l}</span><span class="stat-value">${v}</span></div>`
  ).join('');
  cardDiv.appendChild(stats);

  return cardDiv;
}

function pill(cls, emoji, value, label) {
  return `<div class="stat-pill ${cls}"><span class="emoji">${emoji}</span><span class="val">${value}</span><span class="lbl">${label}</span></div>`;
}

function el(tag, cls) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  return e;
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
