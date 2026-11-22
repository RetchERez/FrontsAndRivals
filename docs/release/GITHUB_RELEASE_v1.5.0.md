# Fronts & Rivals 1.5.0

A single-player script mod for GTA V Enhanced (Story Mode): take Los Santos businesses from three rival crews, staff them with your own crew, and hold them when the crews come back. It runs alongside the whole story and pauses during missions.

**Made with AI:** designed, directed and playtested by Retch. The code, data and documentation were written with Claude, an AI assistant from Anthropic.

## Download

`FrontsAndRivals-1.5.0.zip` below. It contains only this mod's own files: `FrontsAndRivals.dll`, `FrontsAndRivals.ini` and the `FrontsAndRivals` data folder, plus the readme and changelog.

## Requirements

- GTA V Enhanced, Story Mode, BattlEye off
- [Script Hook V](http://www.dev-c.com/gtav/scripthookv/)
- [Script Hook V .NET Enhanced](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases)
- [LemonUI](https://github.com/LemonUIbyLemon/LemonUI/releases) (SHVDN3 build)
- [iFruitAddon2](https://github.com/Bob74/iFruitAddon2/releases) (optional, adds the Office to your phone)

## Install

1. Install the requirements
2. Copy the contents of the zip's `scripts` folder into your GTA V `scripts` folder
3. Start Story Mode: after Franklin's first mission a help box points you to your first front

## What's new in 1.5.0

Rival soldiers are on alert, like GTA V's own gangs on their turf:

- **Notice:** walk up on foot and the patrols stop and the soldiers turn to watch you
- **Warn:** get within about 25 m and they raise their guns, get blips in the crew's colour and tell you to leave, with a last warning halfway through
- **Fight:** they open fire when the warning runs out, or straight away if you walk right up, aim at one, shoot nearby or hurt one
- **Back off:** walk away during a warning and they lower their guns and go back to work
- **Tempers:** small fronts warn for about 8 seconds and bigger ones about 5, hot streets cut it to 3 and blazing streets skip it, and Hellfire MC have the shortest fuse
- **Sneak up:** calm soldiers only notice what they can see, crouching (better with your Stealth skill) and darkness let you get much closer, and silenced shots are only heard within 20 m
- **Street crews too:** crews hanging out on their turf warn you off the same way, at 15 m and on a shorter fuse
- Distances count from the nearest soldier as well as the door, so walking up behind a patroller counts
- A help box explains it the first time a crew warns you
- Driving past never bothers them, and before your first front they only warn

The Office still opens with RB + D-pad Left on a controller or F4 on keyboard. An old INI still set to F11 now opens the Office with F4. If you're updating, replace FrontsAndRivals.ini too (AlertRange is now the warning distance, 25 m). Your empire save carries over.
