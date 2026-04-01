# Strategy Game — Data Structure (Placement Tech Tree)

This document is a cleaned, placement-first model for the current game direction.

Core intent:

- Land control is represented by the board and the land cards you have successfully placed.
- The deck represents exploration of unknown territories.
- Placement progression starts from Empty, then moves to terrain, then to terrain-specific implementations.

---

## UML Class Diagram (Step 2: Terrain Subclasses)

This UML step models the no-null invariant and adds concrete terrain subclasses under abstract `Terrain`.

```mermaid
classDiagram
    class Card {
        <<abstract>>
        +id: string
        +type: string
        +tags: string[]
    }

    class Terrain {
        <<abstract>>
        +terrainType: string
    }

    class PlainsTerrain
    class ForestTerrain
    class BeachTerrain
    class HillTerrain

    class EmptyCard {
        +isPlaceholder: bool = true
    }

    class BoardSlot {
        +index: int
        +currentCard: Card
    }

    Card <|-- EmptyCard
    Card <|-- Terrain
    Terrain <|-- PlainsTerrain
    Terrain <|-- ForestTerrain
    Terrain <|-- BeachTerrain
    Terrain <|-- HillTerrain
    BoardSlot *-- Card : currentCard (always present)
```

Step-2 intent:

- `BoardSlot.currentCard` is never null.
- New slots are initialized with `EmptyCard`.
- `Terrain` is abstract and inherits from `Card`.
- Concrete terrain implementations are `PlainsTerrain`, `ForestTerrain`, `BeachTerrain`, and `HillTerrain`.
- Future steps can add building subclasses with terrain-based placement constraints.

---

## Type Table (outside the diagram)

| Type | Purpose |
|---|---|
| Empty | Root state of a controllable board slot. No production by itself. Accepts terrain placement. |
| Terrain (abstract) | Conceptual parent type for concrete terrain variants. Not a direct card in hand. |
| Plains | Concrete Terrain implementation. Fertile baseline terrain for agriculture-focused development. |
| Forest | Concrete Terrain implementation. Wood-focused terrain for forestry and industry chains. |
| Beach | Concrete Terrain implementation. Coastal terrain for trade and shoreline building chains. |
| Hill | Concrete Terrain implementation. Elevated terrain for pasture, defense, and upland chains. |
| Plains Settlement | Settlement implementation that can only be placed on Plains. Visually and mechanically tied to plains terrain. |
| Beach Settlement | Settlement implementation that can only be placed on Beach. Visually and mechanically tied to beach terrain. |

Note: The deck is the exploration mechanism that yields terrain cards. Playing those cards converts Empty slots into controlled territory.
Note: In JSON, concrete terrain cards use type = land and then specify terrain = Plains, Forest, Beach, or Hill.

---

## Card Placement Tech Tree

```mermaid
flowchart TD
    A[Empty\nRoot board slot] --> B[Terrain\nAbstract placement layer]
    B --> C[Plains]
    B --> D[Forest]
    B --> E[Beach]
    B --> F[Hill]

    C --> G[Farm]
    C --> H[Plains Settlement]
    D --> I[Lumber Camp]
    E --> J[Fishing Camp]
    E --> L[Beach Settlement]
    F --> K[Sheep Pasture]
```

Interpretation:

- Empty is the root node and initial state of board ownership potential.
- Terrain is abstract and documents the transition rule: only terrain cards can fill Empty.
- Plains, Forest, Beach, and Hill are concrete implemented terrain outcomes.
- Building layer is shown as current implementation rules: Farm on Plains, Lumber Camp on Forest, Fishing Camp on Beach, Sheep Pasture on Hill, Plains Settlement on Plains, and Beach Settlement on Beach.

---

## Minimal Placement Rules

1. A board slot begins as Empty.
2. Only a land card can replace Empty.
3. A land card resolves to one concrete terrain type: Plains, Forest, Beach, or Hill.
4. Once a terrain exists, buildings can be placed only if their terrain requirements are met.
5. Settlement placement is terrain-specific: Plains Settlement only on Plains, Beach Settlement only on Beach.

This gives a clear ownership pipeline:

- Explore from deck
- Draw terrain opportunity
- Convert Empty into controlled terrain
- Build on top of that controlled terrain

---

## Current JSON Shape (placement relevant fields)

The catalog remains card-definition driven. For placement tech tree purposes, these fields are the key ones:

- id: stable definition identifier.
- type: land or building.
- terrain: concrete terrain enum for land cards.
- tags: placement and compatibility markers (for example terrain:beach, buildable, coastal).
- fluxCost: flux spent to play from hand.
- statRanges (land): roll ranges used at creation time (fertility, fluxScale).
- allowedTerrains (building): where this building may be placed.
- playCost (building): non-flux resource cost paid when placing.

Settlement-specific examples:

- plains-settlement: type = building, allowedTerrains = [Plains]
- beach-settlement: type = building, allowedTerrains = [Beach]

---

## Placement-Oriented Example

Abstract progression:

- Empty -> Terrain (abstract) -> Beach (concrete)

Gameplay meaning:

- You explored and drew a land option from the deck.
- You spent flux to place it on Empty.
- The slot is now controlled coastal land, enabling beach-compatible building chains such as Beach Settlement.

---

## Glossary

- Control: a board slot is considered controlled once Empty has been replaced by concrete terrain.
- Exploration: drawing from the deck to discover playable territory cards.
- Placement Tech Tree: the dependency graph of what card states can legally follow other card states.
