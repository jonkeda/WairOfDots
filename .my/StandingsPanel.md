# Standings Panel

The standings panel is the compact spectator readout for AI-only matches. It lets you compare players without covering the battlefield with numbers.

The panel is split into two comparison bands:

- `Battlefield` compares `Score`, `Cities`, `Units`, and `General`.
- `Economy` compares `Treasury`, `Tax`, `Upkeep`, and `Deficit`.

Each band repeats the same players in the same order:

- The colored dot at the far left is the player color used on the map.
- Each player row has four bars.
- The number above each bar is the actual value.
- The bar length is relative to the highest current value in that column, so bars compare players within one metric. A long `Tax` bar and a long `Units` bar do not use the same scale.

## Battlefield Band

`Score` is the overall match score. It is a summary of owned cities, living units, saved resources, and elimination penalties.

`Cities` is the number of cities owned by that player.

`Units` is the number of living units still active for that player.

`General` is the player's General health. A full bar usually means the General is still healthy. A low bar means the player is close to elimination risk, because General death eliminates that player.

## Economy Band

`Treasury` is saved spendable money after taxes, upkeep, and purchases.

`Tax` is income from owned territory and cities. Higher tax means the player can sustain more units and recover faster.

`Upkeep` is the recurring army maintenance cost. A high upkeep bar usually means the player has a large or expensive army.

`Deficit` is unpaid upkeep. `0` means the army was paid. A nonzero deficit means the player could not cover payroll, which can damage morale and signal economic collapse.

## How To Read The Screenshot

In the screenshot, each AI has a colored row in both bands. For example, the purple player has:

- `Score 68`, `Cities 2`, `Units 8`, and a healthy `General 32`.
- `Treasury 161`, `Tax 67`, `Upkeep 2`, and `Deficit 0`.

That means purple is economically stable: it owns some cities, has enough tax income, has money saved, and is paying its army.

The red player has `Deficit 31`, which is the warning sign in the image. Even with units on the board, that player is failing payroll and may start losing morale.

## Quick Patterns

- High `Score` plus high `Tax`: the player is probably winning for real, not just surviving.
- High `Units` plus high `Upkeep`: the player has force, but may be expensive to sustain.
- High `Treasury` plus low `Units`: the player may be saving money or failing to convert economy into army strength.
- Any nonzero `Deficit`: watch for morale trouble, routs, and collapse.
- Low `General`: the player is strategically fragile even if its economy looks good.

The Economy map overlay complements this panel by showing tax heat, spawn pips, payroll stress, and recent territory swings directly on the battlefield.
