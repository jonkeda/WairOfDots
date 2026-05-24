# How To Play Wair of Dots

## Goal

You are the Human General. Capture the map's city points, outscore the AI generals, or eliminate their general units.

## Start A Match

1. Launch `WairOfDots.Windows`.
2. Enter a seed if you want a reproducible match.
3. Enter the AI player count. The default is `4`.
4. Select `Start Match`.

## Read The HUD

- The left side of the play screen is the tactical map.
- Green is open land, blue is water, gray is hill/rock, dark lines are road/connection cues, and dark green patches are forest.
- City diamonds are clickable map targets.
- Infantry, tanks, commanders, and generals are shown as circles on their grid cells.
- `Tick`: current simulation tick.
- `Seed`: the match seed.
- `AI`: number of AI players.
- `Human score`: your current score from cities, units, and resources.
- `Resources`: production points used to create infantry and tanks.
- `General`: your general health.
- `Directive`: your current order.
- `Target`: the city your army is prioritizing.
- `Infantry`: your current infantry production preference.

## Commands

- Select any city button to make it your target.
- `Attack`: send units toward the target.
- `Hold`: stop issuing movement orders while cities keep producing when space is available.
- `Defend`: prioritize threatened owned cities.
- `More Infantry`: prefer cheap, fast infantry.
- `More Tanks`: prefer slower, stronger tanks.
- `Pause`: freeze the full simulation.
- `Restart`: restart the same seed and AI count.
- `Menu`: return to the start menu.

## Unit Types

- Infantry are cheap, fast, and good for fast captures.
- Tanks are slower, more expensive, and stronger in fights.
- Commanders and generals are special units. They occupy grid cells, can move, and can be attacked.
- Losing your general eliminates you.

## Cities

Cities show:

- City id and name.
- Owner: Human, AI, or Neutral.
- Human units in the city.
- Enemy units in the city.

Cities generate resources and produce infantry or tanks onto adjacent empty cells. If no adjacent cell is free, production pauses.

Only one unit can occupy a city cell at a time. You can select a city either from the command-panel city list or directly from the map diamond.

## Movement

Units travel across the map grid instead of jumping along city edges. Water blocks movement, roads are fastest, grass is normal, and forest, hill, and rock slow units down.

Each grid cell can hold only one unit. Friendly units block movement, and enemies attack from adjacent cells instead of stacking into the same cell. Terrain also changes combat attack and defense.

## Winning

You win when you are the last active general, when you control all cities, or when the timer ends and you have the best score.

You lose if your general unit is defeated, or if an AI has the highest score when time expires.
