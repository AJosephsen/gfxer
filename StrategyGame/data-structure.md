# Strategy Game — Data Structure (Placement Tech Tree)

This document is a cleaned, placement-first model for the current game direction.

Core intent:

- Land control is represented by the board and the land cards you have successfully placed.
- The deck represents exploration of unknown territories.
- Placement progression starts from Empty, then moves to terrain, then to terrain-specific implementations.

---

## Type Table (outside the diagram)

| Type | Purpose |
|---|---|
| Empty | Root state of a controllable board slot. No production by itself. Accepts terrain placement. |
| Terrain (abstract) | Conceptual transition layer between Empty and concrete terrain variants. Not a direct card in hand. |
| Plains | Fertile baseline terrain. Supports broad expansion and agriculture-focused development. |
| Forest | Wood-focused terrain. Supports forestry and industry chains. |
| Beach | Coastal terrain. Supports coastal and trade-oriented placement chains. |
| Hill | Elevated terrain. Supports pasture/defensive and specialized upland chains. |

Note: The deck is the exploration mechanism that yields terrain cards. Playing those cards converts Empty slots into controlled territory.

---

## Card Placement Tech Tree

```mermaid
flowchart TD
    A[Empty\nRoot board slot] --> B[Terrain\nAbstract placement layer]
    B --> C[Plains]
    B --> D[Forest]
    B --> E[Beach]
    B --> F[Hill]
```

Interpretation:

- Empty is the root node and initial state of board ownership potential.
- Terrain is abstract and documents the transition rule: only terrain cards can fill Empty.
- Plains, Forest, Beach, and Hill are concrete implemented terrain outcomes.

---

## Minimal Placement Rules

1. A board slot begins as Empty.
2. Only a land card can replace Empty.
3. A land card resolves to one concrete terrain type: Plains, Forest, Beach, or Hill.
4. Once a terrain exists, buildings can be placed only if their terrain requirements are met.

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

---

## Placement-Oriented Example

Abstract progression:

- Empty -> Terrain (abstract) -> Beach (concrete)

Gameplay meaning:

- You explored and drew a land option from the deck.
- You spent flux to place it on Empty.
- The slot is now controlled coastal land, enabling beach-compatible building chains.

---

## Glossary

- Control: a board slot is considered controlled once Empty has been replaced by concrete terrain.
- Exploration: drawing from the deck to discover playable territory cards.
- Placement Tech Tree: the dependency graph of what card states can legally follow other card states.
