# Fronts & Rivals 1.3.0

A single-player script mod for GTA V Enhanced (Story Mode): take Los Santos businesses from three rival crews, staff them with your own crew, and hold them when the crews come back. It runs alongside the whole story and pauses during missions.

**Made with AI:** designed, directed and playtested by Retch. The code, data and documentation were written with Claude, an AI assistant from Anthropic.

## Download

`FrontsAndRivals-1.3.0.zip` below. It contains only this mod's own files: `FrontsAndRivals.dll`, `FrontsAndRivals.ini` and the `FrontsAndRivals` data folder, plus the readme and changelog.

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

## What's new in 1.3.0

Safeguards, no new content:

- Every clothing piece is checked against your copy of the game the first time you play, and anything missing is dropped instead of showing broken
- The cops at street crime are the game's real police, so shooting them gets you a wanted level
- Street groups thin out automatically when your frame rate drops
- A copy of your empire is kept before a new version uses it
- A clear on-screen message if LemonUI is missing

## What came in 1.2.0

- Bodyguards are earned later: the first at 8 fronts, the second at 14, and the extra one heads home if you drop below
- Your first front comes empty, so you hire and post your own crew, with a help box showing how
- The Office key is held back from the game while the Office uses it

## What came in 1.1.0

A fix and balance release after a full code review and a fact check of every game function, model and clothing ID against primary sources.

- Car kills count, but the front opens fire and calls in a crew car of backup, and it isn't yours until they're down
- Street cops have their own police faction and go after the crews (they were landing on your side)
- Bodyguards change colour, style and gun on the spot
- Hiring costs $150-$700 by skill and trait, and every recruit is rolled fresh each game
- The Messages page is saved with your empire
- Reset All Fronts and Start a New Empire ask Yes or No first
- Crew vehicles use documented health values and don't blow up from one explosion
- Fixes to street groups, mugger behaviour, combat settings, paint codes and the Dead Kings' green hoodies
- Patrol points and bodyguard arrivals never pick a spot indoors or in water

Full details are in CHANGELOG.md.

## Highlights

- 29 real businesses, every one held by a rival crew, taken by force on foot or with a raid
- Three original crews (Dead Kings, Hellfire MC, Vinewood Elite), each in its own colour on its clothes, cars and map icons
- Guards with names, skills and ranks who patrol, earn better guns and wear your crew's colour in the style you pick
- Attacks with no warning, street heat, revenge that scales up to armoured convoys
- Crews hanging out, walking, cruising, fighting each other and the police, and the odd mugging, carjacking or kidnapping
- The Office, a GTA-style menu on your phone or held Back/View (M)
- Balanced against Story Mode money and length

The full list is in the README and CHANGELOG.

## Known issues

- Some fronts have fewer guard posts where the street layout leaves no safe spot
- Not yet tested on the Legacy edition (it should work with the Legacy requirements)

## Reporting a problem

Open an issue with `scripts/FrontsAndRivals/FrontsAndRivals.log` attached and what you were doing when it happened.

## Credits

- Clothing names and IDs: [root-cause/v-clothingnames](https://github.com/root-cause/v-clothingnames), [root-cause/v-besttorso](https://github.com/root-cause/v-besttorso)
- Ped and vehicle data: [DurtyFree/gta-v-data-dumps](https://github.com/DurtyFree/gta-v-data-dumps)
- LemonUI, iFruitAddon2, Script Hook V, Script Hook V .NET Enhanced (not bundled)

Single-player only: it switches itself off in GTA Online. Free. Not affiliated with Rockstar Games.
