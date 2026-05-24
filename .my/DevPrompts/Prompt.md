# Wair of Dots Clone — Development Prompt

## Project Overview

Build a minimalist real-time strategy game inspired by War of Dots, implemented in C# using a hierarchical multi-agent AI architecture powered by SharpNEAT evolutionary neural networks.

Loosely based on: https://store.steampowered.com/app/3902430/War_of_Dots/

---

## Game Design

### Core Mechanics

- Top-down 2D map with city nodes connected by paths
- Two unit types: **Light** (cheap, fast, versatile) and **Heavy** (slow, high damage/health, open terrain only)
- Capture cities to generate production points
- Each city supports up to 5 units before attrition kicks in
- Short match durations (5–15 minutes)
- Win condition: eliminate enemy General or capture all cities

### Units

- Move in real-time along paths between cities
- Have **health** and **morale** (both 0–1 float)
- React locally within a small visibility radius
- Can rout when morale collapses
- Rally when friendlies are nearby

### Command Units

- **Commanders** are physical entities on the map with health, morale, and a protection detail
- **Generals** are hidden physical entities in the rear, heavily guarded
- Killing a Commander causes regional morale collapse and a leaderless window
- Killing the General is a victory condition and causes army-wide collapse

---

## Architecture

### Four-Tier Hierarchy

```

General (full map, degraded detail)

    ↓ assigns regions, troops, replenishment budget

Commander (region + border visibility)

    ↓ issues tactical orders, requests replenishment

Unit Nets (local tactical, per unit type)

    ↓ directs individual dots

Dots (tiny radius, reactive)

```

### Information Flow

- **Upward**: status reports, resource requests, threat assessments
- **Downward**: region assignments, budgets, priority orders
- Each tier is blind to detail below its own level

---

## AI Architecture — SharpNEAT

### Tier 1: Dot Nets (per unit type)

**Two separate nets**: LightUnitNet, HeavyUnitNet

**Inputs (~20 floats):**

```

- My position (x, y normalized)

- My health (0-1)

- My morale (0-1)

- Nearest enemy: distance, direction, type

- Nearest friendly: distance, count in radius

- Recent friendly deaths in radius (last 5s)

- Nearest city: distance, ownership

- Commander direction + distance

- Commander morale signal (broadcast)

- Current commander order (attack/defend/retreat/scout)

```

**Outputs:**

```

- Move direction (angle or x/y vector)

- Action: attack / hold / retreat / scout

```

**Fitness:**

```csharp

double fitness =

    (enemiesDefeated * 1.0) +

    (citiesReached * 0.5) +

    (survivalTime * 0.1) +

    (commanderProtected * 0.3) +

    (routPenalty * -2.0);

```

---

### Tier 2: Commander Nets

**Inputs (~30 floats):**

```

- Cities in region: count, ownership ratio

- Unit count in region (own vs enemy)

- Average morale in region

- Casualty rate (last 30s)

- Replenishment budget remaining

- Border pressure (enemy units approaching)

- Time since last reinforcement

- Own health + morale

- General's current directive (encoded)

- Threat level from enemy decapitation attempts

```

**Outputs:**

```

- Aggression level (0-1)

- Target city priority (index)

- Request replenishment amount

- Protection detail size (0-5 units)

- Fallback trigger (bool)

- Unit type preference (light/heavy ratio)

```

**Fitness:**

```csharp

double fitness =

    (citiesHeld * 2.0) +

    (regionStability * 1.0) +

    (unitEfficiency * 0.5) +  // kills per unit lost

    (survived ? 3.0 : -1.0) +

    (replenishmentEfficiency * 0.3);

```

---

### Tier 3: General Net

**Inputs (~40 floats):**

```

- Per commander: status (winning/losing/stalemate), casualty rate, resource request, morale

- Total production points available

- Map control percentage

- Reserve pool size

- Time pressure (match duration elapsed)

- Own position security level

- Enemy general activity detected (bool)

- Front stability per region

```

**Outputs:**

```

- Resource allocation per commander (softmax → percentages)

- Priority per region (attack/hold/defend/sacrifice)

- Reserve release trigger

- New troop assignment per commander

- Strategic pause trigger (rebuild mode)

- General position change trigger

```

**Fitness:**

```csharp

double fitness =

    (matchWon ? 20.0 : 0.0) +

    (mapControlAtEnd * 2.0) +

    (armyEfficiency * 1.0) +

    (commandersSurvived * 0.5) +

    (matchDurationBonus);  // win faster = better

```

---

## Information Contracts (C# Interfaces)

```csharp

public struct DotPerception

{

    public float Health;

    public float Morale;

    public float NearestEnemyDistance;

    public float NearestEnemyDirection;

    public int NearestEnemyType;        // 0=light, 1=heavy

    public int NearbyFriendlyCount;

    public int RecentFriendlyDeaths;

    public float NearestCityDistance;

    public int NearestCityOwner;        // -1=enemy, 0=neutral, 1=friendly

    public float CommanderDirection;

    public float CommanderDistance;

    public float CommanderMoraleSignal;

    public int CurrentOrder;            // 0=attack, 1=defend, 2=retreat, 3=scout

}



public struct CommanderStatusReport

{

    public int CommanderId;

    public float RegionControl;         // 0-1

    public float AverageMorale;

    public float CasualtyRate;

    public int ReplenishmentRequest;

    public float ThreatLevel;

    public float OwnHealth;

    public float OwnMorale;

    public bool UnderAttack;

}



public struct GeneralDirective

{

    public int CommanderId;

    public float ResourceBudget;

    public int Priority;                // 0=attack, 1=hold, 2=defend, 3=sacrifice

    public int NewTroopAssignment;

    public float AggressionBias;

    public bool EmergencyReplenishment;

}

```

---

## Morale System

### Morale Events

| Event | Morale Delta |

|---|---|

| Taking damage | -0.05 per hit |

| Nearby friendly death | -0.08 |

| Outnumbered 2:1 | -0.02/s |

| Dealing damage | +0.03 |

| Reinforcements arriving | +0.10 |

| Commander nearby | +0.02/s |

| Commander under attack | -0.15 |

| Commander dead | -0.40 immediate |

| General dead | -0.80 immediate |

### Morale Thresholds

```

> 0.7 → aggressive, bonus damage

0.4–0.7 → normal behavior

0.2–0.4 → cautious, defensive preference

< 0.2 → rout risk

< 0.1 → forced retreat

```

---

## Visibility Radii

| Tier | Visibility |

|---|---|

| Dot | 3 cells radius |

| Commander | Full region + 2 cell border |

| General | Full map (unit counts only, not positions) |

---

## Command Unit Stats

### Commander

```

Health: 100

Speed: 0.6x normal unit

Visibility to enemy: medium range (5 cells)

Protection detail: 0–5 units (net decides)

On death: regional morale -0.40, leaderless for 15–30s

```

### General

```

Health: 200

Speed: 0.3x (rarely moves)

Visibility to enemy: only with scout within 3 cells

Starting position: hidden, rear of map

On death: victory condition, army morale -0.80

```

---

## Training Order (Bottom-Up)

```

Phase 1: Evolve Dot Nets

  - Headless simulation, fixed enemy (rule-based)

  - 200+ generations

  - Goal: survive, capture, protect commander



Phase 2: Freeze Dot Nets, Evolve Commander Nets

  - Use Phase 1 dots

  - 150+ generations

  - Goal: hold region, manage budget efficiently



Phase 3: Freeze Commander Nets, Evolve General Net

  - Use Phase 1+2 nets

  - 100+ generations

  - Goal: win matches, allocate resources well



Phase 4: Unfreeze all, co-evolve

  - Self-play: AI vs AI

  - Fine-tune emergent coordination

  - Watch for decapitation strategies emerging

```

---

## Emergent Behaviors to Watch For

- **Flanking** — dots pathfind around enemy concentrations
- **Decapitation strikes** — light units ignore cities, target commanders
- **Rout exploitation** — AI detects low morale, doubles down
- **Commander hiding** — commander net learns to go dark under threat
- **Scout roles** — light units assigned to find enemy general
- **Budget negotiation** — commanders learn to signal distress accurately
- **Strategic feints** — general allocates pressure on one front to expose another

---

## Tech Stack

| Layer | Technology |

|---|---|

| Language | C# (.NET 8) |

| Engine | Stride3D or Godot (C#) |

| AI Evolution | SharpNEAT |

| Pathfinding | DotRecast or A* (simple grid) |

| Serialization | System.Text.Json |

| Training runner | Headless console app |

| ONNX export | Optional, for inference-only build |

---

## Human Player Modes

The four-tier hierarchy maps directly to four distinct ways a human can play.

---

### Mode 1: Play as General (Default / Recommended)

The most natural entry point. You see the full map in degraded detail, allocate budgets, assign regions, and issue directives. NEAT commanders and units execute under you.

**Controls:**

```

- Click region → assign budget slider

- Click commander → issue priority directive (attack/hold/defend/sacrifice)

- Drag units from reserve pool → assign to commander

- Monitor commander health → reinforce if threatened

- Relocate your General unit manually if threatened

```

**Feel:** Classic RTS macro strategy. You set intent, AI handles micro. Your creativity vs the enemy General's evolved efficiency.

---

### Mode 2: Play as Commander

You take one region. The AI General manages overall strategy and feeds you resources. Other regions run on AI commanders.

**Controls:**

```

- Click cities in your region → set attack priority

- Set unit type preference (light/heavy slider)

- Request replenishment from General (button + amount)

- Manage protection detail size

- Trigger fallback manually

```

**Feel:** Tactical RTS. Smaller scope, higher intensity. Good for co-op — multiple human players each commanding a region under a shared AI General.

**Co-op variant:** 2–4 human commanders, one AI General coordinating. Players must cooperate on resource requests or the General starves everyone.

---

### Mode 3: General + One Commander

You control the strategic layer but can also directly possess one commander for hands-on tactical control. Other regions run on AI commanders.

**Switching:**

```

- Press Tab → toggle between General view and Commander view

- In General view: full map, budget allocation

- In Commander view: region only, tactical orders

```

**Feel:** Best of both. Intervene on the front that matters, delegate the rest.

---

### Mode 4: Play as a Dot (Chaos Mode)

First-person unit in the middle of battle. Commanders issue orders as UI prompts. You choose to follow or ignore them — at cost to morale if you ignore.

**Controls:**

```

- WASD / joystick → move

- Attack nearest enemy automatically or manual target

- Receive commander orders as screen prompts ("ADVANCE", "RETREAT", "PROTECT COMMANDER")

- Ignore order → your morale drops, nearby dots affected

- Follow order → morale bonus

```

**Feel:** Action game. Chaotic, ground-level, visceral. You experience routs, reinforcements arriving, commander death — all from inside the swarm.

---

### Human vs AI Asymmetry

| Mode | Human Advantage | AI Advantage |

|---|---|---|

| General | Creativity, opportunism, decapitation timing | Evolved resource efficiency, never panics |

| Commander | Adaptive tactics, unit micro | Consistent execution, no fatigue |

| Dot | Unpredictability | Perfect morale threshold awareness |

A human General spotting a decapitation opportunity the AI never evolved to defend against is the intended power fantasy.

---

### IHumanPlayer Interface

Replace any tier's NEAT net with a human input handler via a shared interface:

```csharp

public interface IGeneralController

{

    GeneralDirective GetDirective(GeneralPerception perception);

}



public interface ICommanderController

{

    CommanderOrder GetOrder(CommanderPerception perception);

}



public interface IDotController

{

    DotAction GetAction(DotPerception perception);

}



// Implementations:

// NeatGeneralController : IGeneralController

// HumanGeneralController : IGeneralController (reads input)

// LlmGeneralController : IGeneralController (calls API)

```

This makes human, NEAT, and LLM controllers fully interchangeable at any tier.
