# Strategy Game — Data Structure

This document defines the data model that drives all game content. The design is **fully data-driven**: resources, technologies, card capabilities, and placement rules are not hardcoded — they emerge from the card and definition catalog alone.

---

## Design principles

- **Implicit resource existence**: any resource ID referenced anywhere in the catalog (costs, production, upkeep) is a valid resource. No separate registration step is required.
- **Implicit technology existence**: any technology ID referenced in a placement requirement implies that technology is a dimension that can be leveled up.
- **Tag-based compatibility**: cards do not hardcode terrain names. They declare tags they require; cells declare tags they satisfy. Matching is done at runtime.
- **Uniform card schema**: all card types (land, building, event, army, tech, infrastructure) share the same base schema. Type-specific fields are additive.
- **Levels on all cards**: every card starts at level 1 and can be upgraded. Level affects capacity, production, and requirement thresholds.

---

## Resource definition

Resources are implied by use. An optional catalog entry adds display metadata.

```json
{
  "id": "gold",
  "name": "Gold",
  "icon": "gold.webp",
  "category": "economic"
}
```

| Field      | Type   | Description                                        |
|------------|--------|----------------------------------------------------|
| `id`       | string | Unique identifier used everywhere in the catalog   |
| `name`     | string | Display name                                       |
| `icon`     | string | Asset path for UI icon                             |
| `category` | string | `economic` · `agricultural` · `industrial` · `social` · `military` · `special` |

**Examples**: `food`, `wood`, `gold`, `ore`, `oil`, `electricity`, `bricks`, `sugar`, `happiness`, `research`, `flux`, `people`

---

## Technology definition

Technologies are leveled capabilities that gate card placement and unlock new behaviors.

```json
{
  "id": "industry",
  "name": "Industry",
  "description": "Unlocks industrial production chains and large-scale manufacturing.",
  "maxLevel": 5,
  "icon": "industry.webp"
}
```

| Field         | Type    | Description                                        |
|---------------|---------|----------------------------------------------------|
| `id`          | string  | Unique identifier referenced in placement requirements |
| `name`        | string  | Display name                                       |
| `description` | string  | Flavour and gameplay description                   |
| `maxLevel`    | integer | Highest level achievable                           |
| `icon`        | string  | Asset path                                         |

**Examples**: `industry`, `agriculture`, `military`, `logistics`, `governance`, `medicine`, `energy`

---

## Card definition (universal schema)

All cards — land tiles, buildings, events, army units, infrastructure, tech nodes — share this schema.

```json
{
  "id": "candy-shop",
  "name": "Candy Shop",
  "type": "building",
  "level": 1,
  "tags": ["commerce", "food-retail"],
  "description": "A cheerful shop trading in sweets, raising morale and consuming sugar.",
  "art": "candy-shop.webp",
  "fluxCost": 5,

  "placementCost": {
    "gold": 100,
    "wood": 40,
    "bricks": 10
  },

  "placementRequirements": {
    "cellTags": ["settlement", "harbour"],
    "technology": {
      "industry": 3
    }
  },

  "workers": {
    "min": 0,
    "max": 4
  },

  "production": {
    "happiness": 10,
    "food": 3
  },

  "upkeep": {
    "gold": 10,
    "sugar": 100
  },

  "levelScaling": {
    "production": { "happiness": 5, "food": 1 },
    "upkeep":     { "gold": 4, "sugar": 30 },
    "workers":    { "max": 2 }
  }
}
```

### Core fields

| Field         | Type    | Description |
|---------------|---------|-------------|
| `id`          | string  | Unique identifier |
| `name`        | string  | Display name |
| `type`        | string  | `land` · `building` · `event` · `army` · `infrastructure` · `technology` · `diplomacy` |
| `level`       | integer | Current level; starts at 1 |
| `tags`        | string[] | Capability/compatibility tags (see Tag system) |
| `description` | string  | Flavour text and gameplay note |
| `art`         | string  | Art asset path |
| `fluxCost`    | integer | Flux spent to play this card |

### Placement cost

```json
"placementCost": {
  "<resourceId>": <amount>
}
```

Any resource ID is valid. The engine charges these resources when the card is placed. Absence of a key means no cost for that resource.

### Placement requirements

```json
"placementRequirements": {
  "cellTags": ["<tagId>", ...],
  "technology": {
    "<techId>": <minLevel>
  }
}
```

| Sub-field    | Description |
|--------------|-------------|
| `cellTags`   | The target cell must have **at least one** matching tag (OR logic). Use `cellTagsAll` for AND. |
| `cellTagsAll`| The target cell must have **all** listed tags. |
| `technology` | Each listed tech must be at or above the specified level in the player's state. |

### Workers

```json
"workers": { "min": 0, "max": 4 }
```

Production scales linearly with assigned workers between `min` and `max`. A building with 0 assigned workers produces 0 output (unless `min` is 0 and base production applies at 0 workers).

### Production and upkeep

```json
"production": { "<resourceId>": <amountPerRound> },
"upkeep":     { "<resourceId>": <amountPerRound> }
```

Both are applied each round after the End-Round action. Negative values in `production` are allowed (passive drains). `upkeep` entries are costs subtracted each round. If upkeep cannot be paid the card becomes disabled (`!`).

### Level scaling

```json
"levelScaling": {
  "production": { "<resourceId>": <addedPerLevel> },
  "upkeep":     { "<resourceId>": <addedPerLevel> },
  "workers":    { "max": <addedPerLevel> },
  "placementCost": { "<resourceId>": <addedPerLevel> }
}
```

At level N the effective value for a field = base value + `(N − 1) × scalingValue`. Level 1 uses base values unchanged.

---

## Tag system

Tags are free-form strings shared between cell definitions and card requirements.

### Cell tags (set on land cards)

| Tag            | Meaning |
|----------------|---------|
| `terrain:plains` | Plains terrain type |
| `terrain:forest` | Forest terrain type |
| `terrain:hill`   | Hill terrain type |
| `terrain:beach`  | Beach terrain type |
| `terrain:desert` | Desert terrain type |
| `settlement`     | Cell hosts a settlement building |
| `harbour`        | Cell hosts a harbour structure |
| `industrial`     | Cell hosts an industrial zone |
| `fortified`      | Cell has defensive fortifications |

Tags accumulate on a cell as buildings are placed on it.

### Card tags (declared on building/infrastructure cards)

Tags communicate what kind of node a placed card becomes, enabling chained requirements (e.g. a Harbour card adds the `harbour` tag to its cell, allowing a custom building that requires `harbour` to be placed there).

---

## Card types

### Land card

```json
{
  "type": "land",
  "tags": ["terrain:hill"],
  "fertility": 9,
  "accessibilityCost": 7
}
```

Additional fields: `fertility` (base multiplier ×0.1 per unit, so 9 = ×0.9), `accessibilityCost` (flux/resource modifier to place).

### Building card

Placed on a cell that already has a land card. Adds its tags to the cell and produces resources each round.

### Event card (luck / unluck)

```json
{
  "type": "event",
  "tags": ["negative", "agricultural"],
  "effect": {
    "resources": { "food": -20 },
    "message": "Harvest failed. Food stores reduced."
  },
  "condition": null
}
```

| Field       | Description |
|-------------|-------------|
| `effect.resources` | Immediate one-off resource deltas |
| `effect.message`   | Narrative text shown to the player |
| `condition`        | Optional expression string for conditional events |

### Army card

```json
{
  "type": "army",
  "tags": ["military", "infantry"],
  "strength": 10,
  "placementCost": { "gold": 50, "people": 5 },
  "upkeep": { "food": 3, "gold": 2 }
}
```

`strength` is used in attack/defense resolution.

### Infrastructure card

Long-duration investment projects (roads, research facilities, health systems).

```json
{
  "type": "infrastructure",
  "tags": ["logistics"],
  "buildRounds": 4,
  "placementCost": { "gold": 200, "ore": 50 },
  "completedEffect": {
    "production": { "logistics": 2 }
  }
}
```

`buildRounds` is the number of End-Round cycles before the card becomes active. Effect only applies after completion.

---

## Instance state

When a card is played it becomes a **card instance** on the board or in state. The instance stores runtime fields on top of the definition.

```json
{
  "instanceId": "uuid",
  "definitionId": "candy-shop",
  "level": 1,
  "assignedWorkers": 2,
  "isActive": true,
  "buildRoundsRemaining": 0
}
```

| Field                  | Description |
|------------------------|-------------|
| `instanceId`           | Unique runtime ID |
| `definitionId`         | Reference to the catalog entry |
| `level`                | Current level (upgradeable in place) |
| `assignedWorkers`      | Currently assigned worker count |
| `isActive`             | `false` if upkeep cannot be paid |
| `buildRoundsRemaining` | For infrastructure: rounds until active |

---

## Player technology state

```json
{
  "playerId": "uuid",
  "technologies": {
    "industry": 2,
    "agriculture": 1
  }
}
```

Technology levels are checked against `placementRequirements.technology` when a card is played. Levels increase through tech-tree cards, research facilities, or round milestones.

---

## Summary of implied existence rules

| What appears in data | What it implies |
|----------------------|-----------------|
| Resource ID in `placementCost`, `production`, or `upkeep` | That resource type exists and should be tracked in game state |
| Technology ID in `placementRequirements.technology`       | That technology is a trackable player-level dimension |
| Tag in `placementRequirements.cellTags`                   | That tag can be declared on land/building cards |
| `type: "event"` card in deck                              | An event deck exists |
| `type: "army"` card                                       | Military system is active |
| `type: "infrastructure"` card                             | Multi-round build pipeline is active |
