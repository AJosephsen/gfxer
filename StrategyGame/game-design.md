# Strategy Game — Design Notes

## What it is

A single-player, turn-based resource-management game played on a 4×5 grid, controlled entirely through AI chat via MCP tools.

Each turn you draw cards, place land tiles on the board, build structures on top of them, and end the round to collect production and pay upkeep. The goal is to grow a self-sustaining economy over many rounds.

## Data model

The internal data structures that drive all card definitions, resources, technologies, placement rules, and production are described in [data-structure.md](data-structure.md). The game is designed to be fully data-driven: adding new resources, building types, events, or mechanics requires only new catalog entries — no code changes.

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

### Stack unfold / detail view

In the aggregated board view, each stack should be openable. Selecting a stack unfolds it into a detailed view that shows the underlying cells one by one (land fertility, building present/disabled state, worker allocation, per-cell production). This keeps the default board compact while still giving full tactical visibility on demand.

### Luck / unluck event deck (single-player)

Add a separate event deck that introduces uncertainty each round to make single-player runs less predictable.

**Concept**
- At a fixed cadence (for example every round or every second round), draw one event card.
- Most events should be setbacks or costs, with a smaller share of neutral or positive outcomes.
- Events should force adaptation, not instant defeat.

**Example negative events**
- Robbed: lose a percentage of stored Food/Wood.
- Harvest failed: Farms produce less Food this round.
- Fire in storehouse: lose a flat amount of one resource.
- Worker illness: temporarily reduce available People for one round.

**Design goals**
- Add tension and replayability to solo play.
- Create short-term strategic pivots (save buffers, diversify production, keep reserve workers).
- Keep outcomes readable and fair (clear text, bounded penalties, no unwinnable spikes).

### Technology tree progression

Add a tech tree that unlocks stronger cards, mechanics, and efficiency upgrades when specific requirements are met.

**Concept**
- Technologies are arranged in tiers/branches (economy, agriculture, military, logistics, governance).
- Each technology has prerequisites (earlier tech, minimum round, specific buildings, or resource thresholds).
- Unlocking a tech grants permanent effects for the run (new cards in market, cheaper costs, better production, higher caps, etc.).

**Examples**
- Irrigation: requires Farm + enough Food stored; increases food output on Plains.
- Forestry Methods: requires Lumber Camp + Forest cells; increases wood production efficiency.
- Granary: requires Settlement + Food threshold; reduces food-loss events from negative luck cards.

**Design goals**
- Provide long-term planning milestones beyond turn-to-turn optimization.
- Create multiple viable build paths instead of one dominant strategy.
- Keep prerequisites explicit in UI so players understand what to do next.

### Multiplayer with trade, diplomacy, and war

Expand the game from solo to multiplayer where each player controls a realm on the same world map and competes/cooperates over many rounds.

**Core pillars**
- Trading: players exchange resources, cards, or temporary access rights through negotiated deals.
- Diplomacy: alliances, non-aggression pacts, tribute agreements, and reputation/trust effects.
- War: military buildup, invasions/raids, territorial capture, and peace treaties.

**Concept**
- Every round includes both economy actions and player-to-player actions.
- Diplomacy directly affects war and trade (allies can share defense; broken treaties hurt trust).
- War should be costly, so peaceful economic growth remains a viable path.

**Design goals**
- Create social strategy and negotiation depth beyond pure optimization.
- Allow multiple victory paths (economic dominance, territorial control, alliance victory).
- Keep conflict fair and readable with clear declarations, resolution rules, and consequences.

### Single-player modes: bots vs. peaceful solitaire

Single-player can support two different play styles so the game appeals to both competitive and relaxed sessions.

**Mode A: Bot rivals**
- Add AI-controlled realms that follow the same economy rules as the player.
- Bots can trade, form/break alliances, and compete for strategic goals.
- Difficulty comes from bot behavior profiles (aggressive, economic, diplomatic, opportunistic).

**Mode B: Peaceful solitaire (event-driven)**
- No enemy factions on the map.
- Primary pressure comes from the luck/unluck event deck and long-term optimization constraints.
- Focus is city-building efficiency, resilience planning, and score/challenge milestones rather than warfare.

**Design goal**
- Let players choose between strategic rivalry and calm optimization without changing core mechanics.

### Armies and territorial defense

Players should be able to build and maintain armies to defend themselves and their territories.

**Concept**
- Military units are raised using core resources (for example Food/People/Wood) and have ongoing upkeep.
- Armies can be stationed on owned territory to provide defensive strength.
- Territory defense should reduce losses from raids and increase the cost/risk for invading players.

**Strategic impact**
- Choosing military investment trades off against economic growth.
- Border regions become high-priority defense zones.
- A credible defensive army should act as deterrence, not only as combat power.

### Expanded resource economy and specialization

Introduce additional resource types so players cannot optimize everything at once and must specialize.

**Possible resource types**
- Gold / Money
- Oil
- Electricity
- Nuclear power
- Ore / metals
- Advanced materials (future tier)

**Core principle**
- A single realm should not be able to produce every resource efficiently.
- Terrain, technology path, and building choices should push each player toward a distinct economic identity.
- Strong output in one domain should come with opportunity cost in others.

**Specialization examples**
- Agriculture-focused realm: extremely efficient food production, weaker military industry.
- Military-focused realm: strong army production and upkeep support, weaker civilian economy.
- Mining/industry-focused realm: high ore/energy output, must import food and population support.

**Trade implications**
- Trading becomes structurally required for top performance, not optional.
- Players exchange surpluses for deficits (for example ore/energy for food).
- Economic interdependence creates diplomacy leverage and strategic pressure points.

**Design goals**
- Encourage diverse empire builds and replayability.
- Prevent one "best" all-in-one economy strategy.
- Make markets, treaties, and trade routes central to long-term success.

### Real-time asynchronous rounds (4-hour cadence)

Support a persistent multiplayer mode where rounds resolve on a real-time schedule instead of waiting for every player to be online at once.

**Round timing**
- One round completes every 4 hours.
- Optional nightly quiet window (server-level setting) can pause round resolution for a few hours.

**Daily sleep-round rule**
- Each player must take one "sleep round" per day.
- Players choose which round to mark as sleep based on their own schedule.
- If a player misses submitting actions for a round, that round is automatically counted as their sleep round.

**Design intent**
- Keep the game competitive but humane across time zones.
- Reduce pressure to check in constantly.
- Preserve momentum while acknowledging real-life rest/schedules.

### Agentic play (advanced automation scripts)

Allow advanced players to submit markdown-based agent scripts that server-side agents can execute on their behalf.

**Concept**
- A player writes a strategy script in markdown describing priorities, rules, and fallback behavior.
- The server runs the script through an in-game agent executor.
- Execution can be scheduled or event-driven.

**Possible execution triggers**
- Start of round
- End of round
- On-demand (manual "run my agent" action)
- Conditional triggers (for example: if Food > X, if Flux < Y, if army strength below threshold)

**Flexibility model**
- Scripts should support broad intent and conditional logic, not just rigid macros.
- The system should allow partial completion and fallback actions when ideal actions are not possible.
- A run log should explain what the agent attempted, what succeeded, what failed, and why.

**Agentic reliability expectation**
- Results may not be perfectly deterministic or perfect every run.
- Players opt in to "best-effort" automation and keep the ability to override manually.
- Safety guardrails should prevent destructive or invalid actions.

**Design goals**
- Enable deeper strategic planning for advanced players.
- Reduce repetitive micromanagement in longer campaigns.
- Preserve fairness by keeping automation transparent, bounded, and auditable.

### Government control center

Add a dedicated government layer where players manage high-level state decisions beyond normal card placement.

**Scope**
- Military administration: army organization, defense posture, mobilization, and attack planning.
- Diplomacy administration: treaties, alliances, sanctions, trade pacts, and conflict escalation controls.
- Strategic defense/offense policies: border priorities, deterrence spending, and emergency response settings.

**Long-term national investments**
- Infrastructure projects (roads, ports, logistics hubs, grid expansion).
- Research facilities and institutional science capacity.
- Public health systems.
- Social security / welfare stability systems.

**Gameplay role**
- Functions as a macro-planning center for multi-round commitments.
- Introduces policy tradeoffs between short-term output and long-term resilience.
- Connects military, economy, and population stability into one strategic layer.

**Design goals**
- Increase strategic depth for advanced play.
- Provide meaningful national identity through policy choices.
- Reward long-horizon planning without removing tactical gameplay.

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


