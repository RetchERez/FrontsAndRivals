# Fronts & Rivals

A single-player script mod for Grand Theft Auto V. Not affiliated with or endorsed by Rockstar Games.
Design doc v1.0: https://claude.ai/artifact/KAYesFun6qhyBrRT8d5BBr

Status: milestone 2 (one front, guards streamed). Fronts are scouted in-game with the dev tools.

## Requirements (install these yourself; the mod never bundles them)
- Disable BattlEye for Story Mode (Rockstar Games Launcher setting, or the `-nobattleye` launch argument)
- ScriptHookV for your game build
- ScriptHookVDotNet: a nightly build (v3.6.0-nightly.89 or later) on Legacy, or SHVDN Enhanced on Enhanced
- LemonUI (SHVDN3 build)
- iFruitAddon2 3.x (from milestone 3, for the phone contact)

## Install
Copy `FrontsAndRivals.dll` and `FrontsAndRivals.ini` into `<GTA V>\scripts\`.
The mod writes its log and save to `<GTA V>\scripts\FrontsAndRivals\`. Windows may need to allow
mods to create files in the GTA V folder.

## Uninstall
Delete `FrontsAndRivals.dll`, `FrontsAndRivals.pdb`, `FrontsAndRivals.ini` and the `FrontsAndRivals` folder.

## Developing
- `cd src; dotnet build -c Release` builds the DLL and, if `Directory.Build.props.user` sets `GtaVDir`,
  copies it into `<GtaVDir>\scripts`. Start from `Directory.Build.props.user.example`.
  The INI is only copied when the scripts folder has none, so your settings survive rebuilds.
- `cd tests; dotnet run` runs the unit tests for every pure system (rules, scheduler, save, config, markers).
- In game, SHVDN's console (F4) reloads scripts with `Reload()`. Every reload doubles as a cleanup test.
- Set `DevTools=true` in `scripts\FrontsAndRivals.ini`: F9 copies your position for fronts.json,
  F10 opens the dev menu, and an entity counter shows at the top of the screen.
- Content lives in `scripts\FrontsAndRivals\`: `fronts.json` (written by F10 > Scout fronts, never overwritten
  by a build), `names.json` and `models.json` (updated by every build from `src\Content\`).
  After scouting, copy the scouted `fronts.json` back into `src\Content\` and commit it.

Compiling proves every API name, type and signature exists. It does not prove in-game behaviour.
