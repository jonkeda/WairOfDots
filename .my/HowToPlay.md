# How To Play Wair of Dots

## Goal

You are the Human General. Capture the map's city points, outscore the AI generals, or eliminate their generals by taking their home citadels.

## Start A Match

1. Launch `WairOfDots.Windows`.
2. Enter a seed if you want a reproducible match.
3. Enter the AI player count. The default is `4`.
4. Select `Start Match`.

## Read The HUD

- The left side of the play screen is the tactical map.
- Green is open land, blue is water, gray is hill/rock, dark lines are road/connection cues, and dark green patches are forest.
- City circles are clickable map targets.
- Unit groups, commanders, and generals are shown as circles near their city or grid route.
- `Tick`: current simulation tick.
- `Seed`: the match seed.
- `AI`: number of AI players.
- `Human score`: your current score from cities, units, and resources.
- `Resources`: production points used to create Light and Heavy units.
- `General`: your general health.
- `Directive`: your current order.
- `Target`: the city your army is prioritizing.
- `Light`: your current light-unit preference.

## Commands

- Select any city button to make it your target.
- `Attack`: send more units toward the target.
- `Hold`: keep armies in owned cities and build strength.
- `Defend`: prioritize threatened owned cities.
- `More Light`: prefer cheap, fast Light units.
- `More Heavy`: prefer slower, stronger Heavy units.
- `Pause`: freeze the full simulation.
- `Restart`: restart the same seed and AI count.
- `Menu`: return to the start menu.

## Unit Types

- Light units are cheap, fast, and good for fast captures.
- Heavy units are slower, more expensive, and stronger in fights.

## Cities

Cities show:

- City id and name.
- Owner: Human, AI, or Neutral.
- Human units in the city.
- Enemy units in the city.

Cities generate resources and new units when owned and not under attack. Over-stacked cities lose morale and may suffer attrition.

You can select a city either from the command-panel city list or directly from the map circle.

## Movement

Units travel across the map grid instead of jumping along city edges. Water blocks movement, roads are fastest, grass is normal, and forest, hill, and rock slow units down.

## Winning

You win when you are the last active general, when you control all cities, or when the timer ends and you have the best score.

You lose if an AI captures the city holding your general and eliminates you, or if an AI has the highest score when time expires.
