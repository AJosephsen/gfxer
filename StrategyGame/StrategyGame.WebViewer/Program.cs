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
<title>Strategy Game Viewer</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
    background: #1a1a2e;
    color: #e0e0e0;
    min-height: 100vh;
    padding: 20px;
  }
  .header {
    text-align: center;
    margin-bottom: 20px;
  }
  .header h1 { color: #e94560; font-size: 24px; }
  .header .status { color: #888; font-size: 13px; margin-top: 4px; }
  .header .status.live { color: #4ade80; }

  .game-picker {
    text-align: center;
    margin-bottom: 20px;
  }
  .game-picker select {
    background: #16213e;
    color: #e0e0e0;
    border: 1px solid #0f3460;
    padding: 8px 16px;
    font-size: 14px;
    border-radius: 6px;
    cursor: pointer;
  }

  .resources {
    display: flex;
    justify-content: center;
    gap: 24px;
    margin-bottom: 16px;
    flex-wrap: wrap;
  }
  .resource {
    background: #16213e;
    border: 1px solid #0f3460;
    border-radius: 8px;
    padding: 10px 18px;
    text-align: center;
    min-width: 90px;
  }
  .resource .value { font-size: 28px; font-weight: bold; }
  .resource .label { font-size: 11px; color: #888; text-transform: uppercase; letter-spacing: 1px; }
  .resource.food .value { color: #f59e0b; }
  .resource.people .value { color: #3b82f6; }
  .resource.wood .value { color: #22c55e; }
  .resource.focus .value { color: #a855f7; }
  .resource.deck .value { color: #ef4444; }

  .info-bar {
    text-align: center;
    margin-bottom: 16px;
    color: #888;
    font-size: 13px;
  }
  .info-bar .round { color: #e94560; font-weight: bold; font-size: 16px; }
  .info-bar .player { color: #ccc; }

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
    width: 100px;
    height: 80px;
    border-radius: 6px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 600;
    position: relative;
    transition: all 0.3s ease;
  }
  .cell.locked { background: #111; border: 1px dashed #333; color: #444; }
  .cell.empty { background: #1e293b; border: 1px solid #334155; color: #64748b; }
  .cell.forest { background: #14532d; border: 1px solid #166534; color: #4ade80; }
  .cell.plains { background: #713f12; border: 1px solid #92400e; color: #fbbf24; }
  .cell.hill { background: #44403c; border: 1px solid #57534e; color: #d6d3d1; }
  .cell.beach { background: #164e63; border: 1px solid #155e75; color: #67e8f9; }
  .cell.wasteland { background: #2d1b1b; border: 1px solid #4a2020; color: #888; }
  .cell .terrain-name { font-size: 10px; opacity: 0.7; }
  .cell .building-name {
    font-size: 11px;
    font-weight: bold;
    background: rgba(0,0,0,0.4);
    padding: 2px 6px;
    border-radius: 3px;
    margin-top: 2px;
  }
  .cell .building-name.disabled { color: #ef4444; text-decoration: line-through; }
  .cell .fertility { font-size: 9px; opacity: 0.6; position: absolute; top: 3px; right: 5px; }

  .hand-section {
    max-width: 800px;
    margin: 0 auto;
  }
  .hand-section h2 {
    color: #e94560;
    font-size: 16px;
    margin-bottom: 10px;
    text-align: center;
  }
  .hand {
    display: flex;
    gap: 10px;
    flex-wrap: wrap;
    justify-content: center;
  }
  .card {
    background: #16213e;
    border: 1px solid #0f3460;
    border-radius: 8px;
    padding: 12px;
    width: 140px;
    font-size: 12px;
  }
  .card .card-name { font-weight: bold; color: #fff; margin-bottom: 4px; }
  .card .card-type { color: #888; font-size: 10px; text-transform: uppercase; }
  .card .card-detail { color: #aaa; font-size: 10px; margin-top: 4px; }
  .card.land-forest { border-left: 3px solid #4ade80; }
  .card.land-plains { border-left: 3px solid #fbbf24; }
  .card.land-hill { border-left: 3px solid #d6d3d1; }
  .card.land-beach { border-left: 3px solid #67e8f9; }
  .card.land-wasteland { border-left: 3px solid #666; }
  .card.building { border-left: 3px solid #e94560; }

  .discard-info {
    text-align: center;
    margin-top: 12px;
    color: #666;
    font-size: 12px;
  }

  .empty-state {
    text-align: center;
    padding: 60px;
    color: #555;
  }
</style>
</head>
<body>
<div class="header">
  <h1>Strategy Game Viewer</h1>
  <div class="status" id="status">Connecting...</div>
</div>

<div class="game-picker">
  <select id="gamePicker"><option value="">Select a game...</option></select>
</div>

<div id="viewer" class="empty-state">Select a game to watch</div>

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

  // Resources
  const res = game.resources;
  const resBar = el('div', 'resources');
  resBar.appendChild(resourceBox('food', 'Food', res.food, '🌾'));
  resBar.appendChild(resourceBox('people', 'People', res.people, '👤'));
  resBar.appendChild(resourceBox('wood', 'Wood', res.wood, '🪵'));
  resBar.appendChild(resourceBox('focus', 'Focus', `${res.focus}/14`, '⚡'));
  resBar.appendChild(resourceBox('deck', 'Deck', game.landDeck ? game.landDeck.length : '?', '🃏'));
  v.appendChild(resBar);

  // Board
  const boardWrap = el('div', 'board-container');
  const board = el('div', 'board');
  // Detect grid size from cells
  const cells = game.board.cells;
  const rows = Math.max(...cells.map(c => c.row)) + 1;
  const cols = Math.max(...cells.map(c => c.col)) + 1;
  board.style.gridTemplateColumns = `repeat(${cols}, 100px)`;
  board.style.gridTemplateRows = `repeat(${rows}, 80px)`;

  for (const cell of cells) {
    const div = el('div', 'cell');
    if (cell.isLocked) {
      div.classList.add('locked');
      div.innerHTML = '🔒';
    } else if (!cell.land) {
      div.classList.add('empty');
      div.innerHTML = '···';
    } else {
      const landDef = catalog[cell.land.definitionId];
      const terrain = landDef ? landDef.terrain.toLowerCase() : 'unknown';
      div.classList.add(terrain);
      let html = `<span class="terrain-name">${landDef ? landDef.name : cell.land.definitionId}</span>`;
      // Fertility
      if (cell.land.fertility) {
        html += `<span class="fertility">×${(cell.land.fertility / 10).toFixed(1)}</span>`;
      }
      // Building
      if (cell.building) {
        const bDef = catalog[cell.building.definitionId];
        const bName = bDef ? bDef.name : cell.building.definitionId;
        const cls = cell.building.isActive ? '' : ' disabled';
        html += `<span class="building-name${cls}">${bName}</span>`;
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
      const def = catalog[card.definitionId];
      const cardDiv = el('div', 'card');
      if (card.$type === 'land') {
        const terrain = def ? def.terrain.toLowerCase() : '';
        cardDiv.classList.add(`land-${terrain}`);
        let detail = '';
        if (card.fertility) detail += `Fertility ×${(card.fertility / 10).toFixed(1)}`;
        if (card.accessibilityCost) detail += ` · Cost ×${(card.accessibilityCost / 10).toFixed(1)}`;
        cardDiv.innerHTML = `
          <div class="card-name">${def ? def.name : card.definitionId}</div>
          <div class="card-type">Land</div>
          <div class="card-detail">${detail}</div>
          <div class="card-detail" style="color:#666">${card.instanceId}</div>
        `;
      } else {
        cardDiv.classList.add('building');
        let detail = '';
        if (def) {
          if (def.production) {
            const parts = [];
            if (def.production.food) parts.push(`${def.production.food} Food`);
            if (def.production.people) parts.push(`${def.production.people} People`);
            if (def.production.wood) parts.push(`${def.production.wood} Wood`);
            if (parts.length) detail = `+${parts.join(', ')}/rnd`;
          }
        }
        cardDiv.innerHTML = `
          <div class="card-name">${def ? def.name : card.definitionId}</div>
          <div class="card-type">Building</div>
          <div class="card-detail">${detail}</div>
          <div class="card-detail" style="color:#666">${card.instanceId}</div>
        `;
      }
      handDiv.appendChild(cardDiv);
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

function el(tag, cls) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  return e;
}

function resourceBox(cls, label, value, emoji) {
  const div = el('div', `resource ${cls}`);
  div.innerHTML = `<div class="value">${emoji} ${value}</div><div class="label">${label}</div>`;
  return div;
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
