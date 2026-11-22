# Fronts & Rivals 2.0.0

A single-player script mod for GTA V Enhanced (Story Mode): take Los Santos businesses from three rival crews, staff them with your own crew, and hold them when the crews come back. It runs alongside the whole story and pauses during missions.

**Made with AI:** designed, directed and playtested by Retch. The code, data and documentation were written with Claude, an AI assistant from Anthropic.

## What you do

- **Take 29 real businesses by force:** every front starts in one of three rival crews' hands, and nothing is for sale
- **Build your crew:** hire recruits with their own skills and ranks, arm them and post them at your fronts
- **Hold what you take:** crews hit back with surprise attacks, so race back to fight or trust your guards
- **Three rival crews:** each has its own colours, cars, boss, lieutenants and grudge against you
- **Balanced for the whole story:** income and pacing match Story Mode, and everything pauses during missions
- **The Office:** one GTA-style menu for everything, opened with RB + D-pad Left on a controller or O on keyboard

## Download

`FrontsAndRivals-2.0.0.zip` below. It contains only this mod's own files: `FrontsAndRivals.dll`, `FrontsAndRivals.ini` and the `FrontsAndRivals` data folder, plus the readme and changelog.

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

## What's new in 2.0.0

**Crews remember who hurts them**

- Kill a crew's people on the street, yourself or through your guards and bodyguards, and that crew gets angrier
- Each crew's anger shows on its Rivals page: Calm, Angry, Furious or Vendetta
- An angry crew's soldiers barely warn you before they shoot, even when the rest of the city is calm
- You get a text when the streets heat up and when they cool down

**Soldiers on alert** (new since 1.4)

- Rival soldiers and street crews notice you, raise their guns and warn you off, and open fire if you stay, walk right up, aim at one or shoot nearby
- Back off and they go back to work
- Sneak up if you'd rather: they only notice what they can see, crouching and darkness let you get closer, and silenced shots are only heard up close

**Fixes**

- Crouching no longer resets a warning (sneaking only helps before they've noticed you)
- A warning only runs out while you're still in range, so walking away or a long pause never sets off a fight
- Standing near the door while every soldier is round the back no longer counts as being heard
- Patrollers no longer snap back to their posts when a crew calms down
- A soldier who turns up mid-warning joins the warning
- Single and silenced shots are always heard
- Controller lock-on aiming counts as aiming
- No "cooled down" text after starting a new empire

**New keyboard key: O, for Office**

F4 also opened the Script Hook V .NET console, so the Office key is now O. An old settings file set to F4 or F11 switches to O by itself.

**Balance:** checked over 300 simulated games per play style. A steady player still reaches about 8 fronts by day 14, 15 by day 33 and 22 by day 48 and holds two thirds of attacks, and hunting street crews costs only a few more attacks.

The project has moved to github.com/RetchERez/GTAV_FrontsAndRivals (old links redirect). The Office opens with RB + D-pad Left on a controller or O on keyboard. If you're updating, replace FrontsAndRivals.ini too. Your empire save carries over.
