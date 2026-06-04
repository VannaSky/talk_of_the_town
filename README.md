# Talk of the Town

A 3D village simulation where a local LLM coordinates villagers, manages resources, and grows a settlement — all through natural language reasoning.

## What Is This?

Talk of the Town is a Unity game that replaces traditional AI scripting with a Large Language Model (via [Ollama](https://ollama.com/)). Instead of hand-coded behavior trees, the LLM reads the full village state — inventory levels, villager positions, available resources, active goals — and decides what every villager should do next.

The result is emergent village management: the LLM balances food production, resource gathering, construction, and population growth by reasoning about the situation in plain language.

## Gameplay Loop

1. A procedurally generated tile-based map spawns with trees, stone deposits, seed nodes, and a small starting village (core building, well, warehouse).
2. Villagers spawn when houses are completed.
3. The LLM evaluates all idle villagers in a single batch call, assigning jobs and targets based on the current state.
4. Villagers pathfind to their targets and execute jobs (chopping trees, mining stone, farming, building).
5. As resources change, buildings finish, or goals complete, the LLM is triggered again to re-evaluate and adapt its strategy.
6. The village grows from a handful of villagers into a coordinated settlement.

## Core Systems

### LLM-Driven Coordination
- **Batch decisions**: All idle villagers are assigned in one LLM call, preventing conflicts (no two villagers sent to the same tree).
- **Event-driven triggers**: Decisions fire when villagers go idle, buildings complete, goals are met, or resources drop critically — not just on a timer.
- **Conversation history**: The LLM retains a rolling context of recent decisions with pinned system messages, maintaining strategic continuity across calls.
- **Mini-goals**: The LLM can set precise gather amounts (e.g., "chop 20 wood, then stop") for fine-grained control.
- **Performance metrics**: Token usage and response times are tracked and displayed in the HUD for observability.

### Jobs
| Job | Description |
|-----|-------------|
| **Lumberjack** | Chops trees for wood. Trees regrow over time. |
| **Miner** | Mines stone deposits (fast) or mine shafts on mountain tiles (slow, infinite yield). |
| **Farmer** | Plants crops near Farm buildings using seeds, harvests food + seeds. Primary food source. |
| **SeedGatherer** | Collects seeds from pumpkin/wheat nodes. |
| **Builder** | Constructs Houses, Stockpiles, or Farms using wood and stone. |

### Buildings
- **House** — Spawns a new villager on completion. Tracks occupancy.
- **Stockpile** — Increases village inventory capacity.
- **Farm** — Unlocks crop planting in its radius. Farmers cannot plant without one.

All buildings progress through visual construction stages and require resource investment.

### Villager Energy
Each villager has an energy pool that drains while working or walking and recovers during rest. As energy falls, work speed drops proportionally. Villagers showing their energy level in their name tag give the player a clear read on stamina across the settlement.

### Resources & Farming
- **Inventory**: Wood, Stone, Seeds, Food with shared capacity (expandable via Stockpiles).
- **Farming cycle**: Seeds + Farm building + grass tiles = crops. Harvesting yields food *and* seeds, making farming self-sustaining.
- **Renewable resources**: Trees regrow from stumps (stump visuals appear during regrowth); crops regrow after harvest.
- **Natural seed spawning**: Seed nodes grow organically on empty tiles over time, supplementing gathered seed stock.
- **Mine shafts**: Placed on mountain tiles at map generation; provide infinite stone at a very slow extraction rate.

### Goals
- **Global Goals** (researcher-defined): Set before the simulation starts — e.g., "accumulate 200 wood", "reach population 8", or "build 3 stockpiles". Progress is tracked in real time and the simulation stops automatically when all goals are met.
- **LLM mini-goals**: The LLM can also set its own precise gather targets mid-session (e.g., "chop 20 wood, then stop") to coordinate short-term bursts of work.

## Tech Stack

- **Unity** (URP) — 3D rendering, NavMesh pathfinding
- **Ollama** — Local LLM backend (no cloud API required)
- **TileWorldCreator** — Procedural terrain generation
- **Newtonsoft.Json** — JSON serialization for LLM communication
- **Polygon Fantasy Kingdom** — Visual assets

## Prerequisites

1. Install [Ollama](https://ollama.com/) and pull a model:
   ```bash
   ollama pull gemma3:4b
   ```
2. Open the project in Unity (6.x with URP).
3. Ensure `com.unity.nuget.newtonsoft-json` is installed via Package Manager.

## Controls

| Key / UI | Action |
|----------|--------|
| **1-4** | Game speed presets (1x, 2x, 10x, 50x) |
| **Speed slider** | Fine-grained speed control (1x – 50x) |
| **Save / Load buttons** | Persist and restore the current map state |

## Project Structure

```
Assets/Scripts/
  LLMController.cs          # Central LLM coordinator
  LLMPrompts/               # Prompt variants (normal, caveman)
  Villagers/                 # Villager entity, brain, jobs, energy
  Tiles/                     # Tile grid, map spawning, save/load
  Buildings/                 # Building progression & data
  Environment/Resources/     # Resource nodes, regrowth, seed spawner
  UI/                        # HUD, name tags, goals display, metrics
  GlobalGoals/               # Researcher-defined win conditions
```

## What Makes This Different

Most game AI uses scripted rules or behavior trees. Talk of the Town hands the *entire coordination problem* to an LLM that sees village state as structured text and responds with JSON job assignments. The LLM must learn to balance short-term needs (food is low) against long-term growth (build houses for more villagers), coordinate multiple agents without conflicts, and adapt as the village scales from 1 to 10+ villagers.
