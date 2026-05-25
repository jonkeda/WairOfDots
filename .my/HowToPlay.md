# How To Play Wair of Dots

## Goal

You are the Human General. Capture the map's city points, outscore the AI generals, or eliminate their general units.

## Start A Match

1. Launch `WairOfDots.Windows`.
2. Enter a seed if you want a reproducible match.
3. Enter the AI player count. The default is `4`.
4. Leave `Human` checked for the normal human-vs-AI match, or uncheck it for AI-only spectator mode.
5. Leave `Role` on `General` for the normal command mode, or switch it to `Commander` to try the first alternate human mode.
6. Select `Start Match`.

## Read The HUD

- The left side of the play screen is the tactical map.
- Green is open land, blue is water, gray is hill/rock, dark lines are road/connection cues, and dark green patches are forest.
- Thin colored borders show where controlled land changes owner.
- City squares are clickable map targets and use the controlling player's color. Neutral cities are yellow.
- Infantry, tanks, commanders, and generals are shown as circles on their grid cells.
- `Tick`: current simulation tick.
- `Seed`: the match seed.
- `AI`: number of AI players.
- `Human score`: your current score from cities, units, treasury, and territory.
- `Treasury`: taxes saved after upkeep and purchases.
- `Tax`: income from controlled cells.
- `Upkeep`: the recurring cost of maintaining your army.
- `General`: your general health.
- `Directive`: your current order.
- `Target`: the city your army is prioritizing.
- `Infantry`: your current infantry production preference.
- `Role`: the active human control mode.

## AI-Only Mode

Uncheck `Human` on the start menu to watch an AI-only match. Player `0` becomes `AI 0`, every player uses a deterministic genome, and you watch as a spectator.

AI-only mode replaces human command buttons with speed controls and standings. City markers inspect cities instead of issuing orders.

Standings show each AI as a compact row of bars for score, cities, units, and general health. Labels above the columns identify the metrics, each player row starts with a colored dot, and the raw numbers stay visible above the bars.

Use:

- `Slower`: reduce spectator speed.
- `Normal`: return to 1x speed.
- `Faster`: increase spectator speed.
- `Pause`: freeze the simulation.
- `Restart`: restart the same seed, AI count, and mode.

## Commands

- Select any city marker on the map to make it your target.
- `Attack`: send units toward the target.
- `Hold`: stop issuing movement orders while cities keep producing when space is available.
- `Defend`: prioritize threatened owned cities.
- `More Infantry`: prefer cheap, fast infantry.
- `More Tanks`: prefer slower, stronger tanks.
- `Pause`: freeze the full simulation.
- `Restart`: restart the same seed and AI count.
- `Menu`: return to the start menu.

## Human Roles

- `General` is the main mode. You set the broad directive, target city, and infantry/tank preference.
- `Commander` is the first alternate mode. You still use the same simple controls, but the simulation treats the General strategy layer as automated while your commands act as region/commander intent.
- `GeneralAndCommander` and `DotChaos` are supported in the core model as future hooks, but they do not have full player-facing controls yet.

## Unit Types

- Infantry are cheap, fast, and capture only the cell they stand on.
- Tanks are slower, more expensive, stronger in fights, and capture their own cell plus nearby passable cells.
- Commanders and generals are special units. They occupy grid cells, can move, and can be attacked.
- Losing your general eliminates you and immediately turns every city and controlled cell you owned neutral.

## Territory And Economy

Every passable grid cell can be controlled. Controlled cells generate tax value, and cities are high-value cells that also act as spawn anchors.

- Infantry capture only their occupied cell.
- Tanks capture their occupied cell and adjacent passable, uncontested cells.
- Water cannot be captured or taxed.
- Territory borders appear between neighboring cells controlled by different players or between player territory and neutral land.
- Taxes are added to your treasury on the economy tick.
- Upkeep is deducted from treasury for existing units.
- If upkeep cannot be paid, units lose morale instead of dying immediately.
- New infantry and tanks are bought from treasury and spawn near owned cities when an adjacent passable cell is open.

## Cities

Cities show ownership directly on the map:

- Human-owned cities use the human player color.
- AI-owned cities use that AI player's color.
- Neutral cities are yellow.
- The selected city name and owner appear in the target text.

Cities are spawn anchors for treasury-funded infantry and tanks. If no adjacent cell is free, spawning pauses.

Only one unit can occupy a city cell at a time. Select cities directly from the map markers.

When a player's General dies, all cities and territory cells controlled by that player become neutral and can be captured again by the remaining players.

## Movement

Units travel across the map grid instead of jumping along city edges. They can move north, south, east, west, and diagonally. Diagonal movement costs a little more than straight movement, so it feels natural without becoming a free speed boost.

Water blocks movement, roads are fastest, grass is normal, and forest, hill, and rock slow units down. Units cannot cut diagonally through blocked or occupied corners.

Unit markers glide smoothly across the map. When the path ahead is clear, units follow natural-looking straight lines instead of stepping cell by cell. The underlying tactics still use grid cells: occupancy, city capture, and combat resolve from the unit's current cell.

Each grid cell can hold only one unit. Friendly units block movement, and enemies attack from adjacent cells, including diagonal neighbors, instead of stacking into the same cell. Terrain also changes combat attack and defense.

## Winning

You win when you are the last active general, when you control all cities, or when the timer ends and you have the best score.

You lose if your general unit is defeated, or if an AI has the highest score when time expires.
