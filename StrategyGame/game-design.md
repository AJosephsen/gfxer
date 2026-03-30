# Strategy Game — Design Notes

## What it is

A single-player, turn-based resource-management game played on a 4×5 grid, controlled entirely through AI chat via MCP tools.

Each turn you draw cards, place land tiles on the board, build structures on top of them, and end the round to collect production and pay upkeep. The goal is to grow a self-sustaining economy over many rounds.

## Resources

| Resource | Role |
|----------|------|
| **Food** | Consumed by upkeep; spent to buy cards from the market |
| **People** | Spent to place buildings; consumed by upkeep |
| **Wood** | Produced by Lumber Camps; spent to place buildings (e.g. Farm costs 10 Wood) |
| **Flux** | Action points — spent to play cards; refills each round (capped at 14) |

## Core loop

1. Draw a card (costs Flux)
2. Play land cards onto empty cells (costs Flux)
3. Play building cards onto cells that already have land (costs Flux + resources)
4. Buy new cards from the market (costs resources, added to hand)
5. End the round → all buildings produce, upkeep is paid, Flux refills

Buildings that can't pay upkeep become disabled (`!`) until you can afford them again.

## Cards

### Land (place on empty cell)
| Card | Terrain | Restrictions |
|------|---------|--------------|
| Forest | Forest | — |
| Plains | Plains | — |
| Hill | Hill | — |
| Beach | Beach | — |

### Buildings (place on land)
| Card | Terrain | Produces | Upkeep | Notes |
|------|---------|----------|--------|-------|
| Settlement | any | +2 People/round | −1 Food/round | — |
| Farm | Plains | +6 Food/round (max, scales with workers) | −1 People/round | 10 Wood to place |
| Lumber Camp | Forest | +4 Wood/round (max, scales with workers) | — | 2 People to place |
| Fishing Camp | Beach | +4 Food/round (max, scales with workers) | — | 2 People to place |
| Sheep Pasture | Hill | +4 Food/round (max, scales with workers) | — | 2 People to place |

---

## Future ideas

<!-- Add your ideas below -->

### Card icons

Replace text labels (resource costs, terrain type, building type) with small AI-generated PNG icons — same pipeline as card/land art. One icon per concept, rendered at ~32px, composited onto the card layout.

### Aggregated board view

Instead of one slot per cell, group board cells by land type and show a single aggregate card per group (summed production, averaged fertility etc.). For land types that can hold buildings, split into two stacks: empty cells and cells with buildings. Fewer cards, more signal.

### Core concept: strategy board game as a card game

Classic grand-strategy abstracted into cards. Multiple players, each managing a territory. The central tension: armies consume resources (food, people) that would otherwise fuel production — going unarmed lets you grow fast but leaves you exposed; a large army lets you seize other players' land and snowball that way. Conquest is an alternative growth path to economic development.

### Diplomacy and alliances

Players can form alliances — pooling armies for joint attacks or shared defence. Alliances shift the calculus: a small military player stays viable by sheltering under a stronger ally; a dominant player can be checked by a defensive coalition. Betrayal and alliance-breaking should carry costs.

### Worker occupation system

Replace the current flat People upkeep with a proper worker-allocation model:

**Concept**
- Each building that needs labour has a **worker capacity** (e.g. Farm = 6 workers).
- At the start of each End-Round phase, available `People` are distributed across buildings that still need workers.
- A building's output scales linearly with how many of its slots are filled:
  - Farm: 0 workers → 0 Food, 3 workers → 3 Food, 6 workers → 6 Food.
- Workers assigned to a building are "occupied" for that round — they cannot be assigned elsewhere.
- Occupied count is tracked on the game state. The UI/board render should show `n/max` workers per building.

**Distribution algorithm (round-robin)**
1. Build a list of all active buildings that have unfilled worker slots, ordered by board position (row-major).
2. Assign **one worker at a time**, cycling through the list from the start.
3. After each pass, remove buildings whose slots are now full.
4. Stop when there are no `People` left to assign, or all slots are filled.

Example with 5 People and two buildings each needing 6 workers:
- Pass 1: Building A gets 1, Building B gets 1 → 3 People remaining.
- Pass 2: A gets 1, B gets 1 → 1 remaining.
- Pass 3: A gets 1, B gets 0 (ran out).
- Result: A=3 occupied (50% → 3 Food), B=2 occupied (33% → 2 Food).

### Settlements produces some basic resources depending on placement
* On plains it produces four food each round ( vegetables can be grown)
* on Hills it produces one food each round ( sheeps )
* in woods it produces two wood each round
on beach it produces two food each round


