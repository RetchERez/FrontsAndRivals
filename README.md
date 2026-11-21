# Fronts & Rivals 1.0

A free single-player script mod for Grand Theft Auto V. Not affiliated with or endorsed by Rockstar Games.

Run real Los Santos businesses as fronts, post guards with names, faces, traits and ranks, and hold them against three rival crews who already own most of the city and whose leaders remember you. Attacks resolve on their own while you're away and turn into real fights when you're close. Michael, Franklin and Trevor share one empire.

## What's in 1.0

- 29 real businesses as fronts across Los Santos: bars, clubs, liquor stores, motels, diners, markets, factories and garages
- Three crews holding their own turf: the Crown Kings in South LS, Ironside MC on the east side, the Velvet Syndicate out west; five fronts start for sale
- A city that moves without you: crews grab fronts that are for sale and take each other's at night
- The Office on your phone: three side-by-side panels for fronts, guards, hiring, rivals and settings
- Guards with traits, ranks, experience and permanent death; a memorial for the fallen
- One-click front management: Fill empty posts and Arm this front on every front's page
- First-time tips in GTA's help box (Office > Settings > Show tips again)
- Bodyguards: call up to two of your guards from the Office to follow you around the city, on foot or in your car,
  and fight at your side. Their front is a guard short while they're with you
- Guard skills like the player's: Shooting (aim and power), Strength (power and health), Stamina (fewer injuries,
  quicker recovery) and Stealth (fewer deaths). They grow on the job: every midnight on a post, every fight, every kill,
  every promotion. Better recruits cost more; fire anyone from their page in the Office
- Staff at the front you're standing near: a cashier on the phone, a janitor sweeping, a chef on a smoke break.
  They only appear when there's ped budget to spare and leave the moment guards or a fight need it
- A big, mixed cast: guards and crews are ordinary Los Santos street people, women and men from every part of the city,
  with 87 men's and 65 women's first names, 95 surnames, and nicknames for only some of them
- The call to come defend: warnings say when the hit lands and what defending it yourself pays, with a GPS route
- Crews never die out: they recruit off the street every night and come back for what you took
- A weapon ladder: every guard starts with a knife and is armed one gun at a time, each a big step up:
  pistol $750 (x1.0), pump shotgun $1,500 (x1.3), SMG $3,000 (x1.6), carbine rifle $6,000 (x2.0); the knife is x0.6
- Three original crews (Crown Kings, Ironside MC, Velvet Syndicate), each with a named boss and three lieutenants
- Attacks with a warning text first; within 300 m they become a live defense with convoys, timer bars and a door to hold
- Take rival fronts by clearing their guards and claiming the door
- A nemesis loop: leaders who escape come back with scars and text you about it; dead lieutenants are replaced by promoted survivors
- Midnight payouts with wages, front upgrades, and a protected mode for a lower-stakes game
- The game's own alert and cash sounds, a message queue that never spams, and full controller support
- Nothing to collect and no upkeep; good guards win without you

## Requirements

Install these yourself; the mod never bundles them:

- BattlEye off for Story Mode (Rockstar Games Launcher setting, or the `-nobattleye` launch argument)
- ScriptHookV for your game build
- ScriptHookVDotNet: a nightly build (3.6.0-nightly.89 or later) on Legacy, or SHVDN Enhanced on Enhanced
- LemonUI (SHVDN3 build)
- iFruitAddon2 3.x, for the phone contact

After a GTA V update, wait for ScriptHookV to update before reporting bugs.

## Install

1. Copy `FrontsAndRivals.dll` and `FrontsAndRivals.ini` into `<GTA V>\scripts\`
2. Copy the `FrontsAndRivals` folder into `<GTA V>\scripts\` too
3. Start Story Mode; about ten minutes in, the Broker texts you

Windows may need to allow mods to create files in the GTA V folder. iFruitAddon2 also writes its settings to `scripts\iFruitAddon2\`.

## Playing

- Call **The Office** on your phone to open the menu; it's near the end of your contacts list
- In the Office, **Enter** opens the next panel and **Backspace** goes back; **Left/Right** changes a guard's post or a setting
- Buy the car wash from the Broker, hire recruits, post them, and you're in business
- Income arrives at every in-game midnight, after wages; sleeping across midnights pays each one (up to three)
- When a warning text arrives, you can go and fight beside your guards or leave it to them
- Rival fronts show on the map in their crew's colour; their soldiers ignore you while you drive past, but walk up on foot, shoot nearby or hurt one of them and the whole front fights
- Clear a rival front's soldiers, stand at the door and press **E** to claim it
- Fronts that are for sale are bought in person at their door, like GTA's own properties, for five days of their takings (the car wash is the Broker's $25,000 starter)

## Settings

Most players never need to touch these. The everyday ones are in the game under **The Office > Settings**:
attack frequency, rival turf moves, notifications, live defenses, sound effects and map blips.

Everything else is in `scripts\FrontsAndRivals.ini`, each line explained in the file:

- `OfficeHotkey` (keyboard key, e.g. `F7`) and `OfficeButton` (true, the default = hold the controller's Back/View button, or M, for half a second to open the Office)
- Attacks: `Frequency`, `CooldownMinutes` (exact minutes between attacks), `FirstAttackDelayMinutes`, `WarningHours`, `LiveDefense`, `PoliceRespond`, `EmpirePressure` (the more fronts you hold, the sooner and bigger the attacks)
- Turf: `Frequency` for how often crews grab and fight over fronts at night
- Notifications: `Level` (All, Important, Minimal), `MinSecondsBetween` (default 20 s so messages never pile up), `Taunts`, `StreetWord`, `MidnightReport`
- Sound: `Effects` for the game's alert and cash sounds
- Economy: income and wage multipliers, `FrontPriceDays`
- Guards: `PermanentDeath`, `ProtectedMode` (fronts can't be lost)
- Rivals: `AlertRange` for how close you can walk to a rival front before its soldiers fight
- Map: `ShowRivalFronts`, `ShowForSaleFronts`

## Controller

Everything works on a gamepad. **Hold Back/View for half a second** to open or close the Office (a quick tap still changes the camera), or call The Office from your phone. In the Office, the D-pad moves, **A** opens or selects, **B** goes back, and left/right on the D-pad changes a post or a setting. The claim prompt at a rival front's door shows your controller's button. With `DevTools=true`, the dev menu is under Office > Settings > Dev tools, so it needs no keyboard either.

## Uninstall

Delete `FrontsAndRivals.dll`, `FrontsAndRivals.pdb` (if present), `FrontsAndRivals.ini` and the `scripts\FrontsAndRivals` folder. The mod never touches GTA V's own save files; cash it paid out stays with your characters.

## Compatibility

- Single-player only; the mod switches itself off if a GTA Online session starts
- It pauses during story missions, random events, cutscenes and character switches, and removes its people so missions never see them
- Other gang mods are fine; all of this mod's groups are prefixed `FR_`
- At most 28 people and 8 vehicles at once, all cleaned up when you leave, reload scripts or start a mission

## Known issues

- The Office contact sits at the end of the phone's contact list; that's where iFruitAddon2 places mod contacts so story contacts are never overwritten
- A few fronts have fewer than four guard posts where the street layout leaves no safe spot

## For developers

- `cd src; dotnet build -c Release` builds the DLL and, if `Directory.Build.props.user` sets `GtaVDir`, deploys it to `<GtaVDir>\scripts` (start from `Directory.Build.props.user.example`)
- `cd tests; dotnet run` runs the unit tests for every pure system
- `powershell -File tools\package.ps1` builds the release zip in `dist\`
- `DevTools=true` in the INI turns on F9 (copy position), F10 (dev menu: scout fronts, force attack, test spawns) and an entity counter
- Content lives in `scripts\FrontsAndRivals\`: `fronts.json`, `crews.json`, `lines.json`, `names.json`, `models.json`
