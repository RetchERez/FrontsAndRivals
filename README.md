# Fronts & Rivals 1.0

A free single-player script mod for Grand Theft Auto V Enhanced (Story Mode). Not affiliated with or endorsed by Rockstar Games.

Buy real Los Santos businesses as fronts, post guards with names, faces, skills and ranks, and hold them against three rival crews who already own most of the city and fight each other for the rest. Attacks play out on their own while you're away and turn into real fights when you're close. Michael, Franklin and Trevor share one empire, and it runs alongside the whole story.

## Requirements

Install these yourself first. The mod never bundles them.

| What | Why | Where to get it |
|---|---|---|
| **GTA V Enhanced**, Story Mode | The game | Steam, Epic or the Rockstar Games Launcher |
| **BattlEye off** | Script mods can't load with it on | Rockstar Games Launcher > Settings > GTA V Enhanced, or add `-nobattleye` to the launch options |
| **Script Hook V** | Loads script mods into the game | http://www.dev-c.com/gtav/scripthookv/ |
| **Script Hook V .NET Enhanced** | Runs .NET script mods like this one | https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases |
| **LemonUI** (the SHVDN3 build, 2.2 or later) | The game-style menus | https://github.com/LemonUIbyLemon/LemonUI/releases |
| **iFruitAddon2** (3.x) | The Office contact on your phone (optional: without it, hold Back/View or M to open the Office) | https://github.com/Bob74/iFruitAddon2/releases |
| **.NET Framework 4.8** | Needed by Script Hook V .NET (already on most Windows 10 and 11 PCs) | https://dotnet.microsoft.com/download/dotnet-framework/net48 |
| **Visual C++ Redistributable** (x64, latest) | Needed by Script Hook V .NET (already on most PCs) | https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist |

Tested with Script Hook V v3889.0, Script Hook V .NET Enhanced 1.1.0.6, LemonUI 2.2 and iFruitAddon2 3.1.1. After a GTA V update, wait for Script Hook V to update before playing or reporting bugs.

Playing on the older Legacy edition? Use the Script Hook V .NET nightly build instead (3.6.0-nightly.89 or later): https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases

## Install

1. Install everything in Requirements
2. Copy the contents of this zip's `scripts` folder into `<GTA V>\scripts\`: `FrontsAndRivals.dll`, `FrontsAndRivals.ini` and the `FrontsAndRivals` folder
3. Start Story Mode; once you have $10,000, the Broker texts you about a car wash for sale

Windows may need to let mods create files in the GTA V folder; the mod saves to `scripts\FrontsAndRivals\empire.json`.

## Getting started

- **Open the Office:** hold Back/View on a controller (or M) for half a second, or call The Office on your phone
- **Buy your first front:** go to the Hands On Car Wash in Strawberry and buy it at the door, like GTA's own properties; $10,000, and two of the old crew come with it
- **Follow the Next Step line** at the top of the Office: it always says what to do next
- **Hire and post guards** from the Office, then arm them and give them body armour
- **Get paid at midnight:** every front pays out at in-game midnight, after wages
- **Defend:** when a front is hit, a timer shows how long your guards can hold; race there and fight beside them, or leave it to them
- **Take rival fronts:** clear a crew's soldiers, stand at the door and press the prompt's button; or send a crew from the Office

## What's in 1.0

- 29 real businesses across Los Santos: bars, clubs, liquor stores, motels, diners, markets, factories and garages
- Three original crews holding their own turf: the Dead Kings in South LS, Hellfire MC on the east side and the Vinewood Elite out west; each has a boss and three lieutenants
- A city that moves without you: crews buy fronts that come up for sale, feud with each other and take each other's fronts at night
- Attacks with no warning: one alert when a front is hit, a timer showing how long your guards can hold (about 5 minutes, more with Cameras), a GPS route and a bonus for holding the door yourself
- Live defenses within 300 m: crews arrive in convoys in their own vehicles (Hellfire MC on motorcycles), with their own guns and the odd explosive
- A nemesis loop: leaders who escape come back with scars and text you about it; dead lieutenants are replaced by promoted survivors
- Guards with skills like the player's (Shooting, Strength, Stamina and Stealth) that grow on the job; ranks, experience, traits and a memorial for the fallen
- A weapon ladder: every guard starts with a knife and is armed one gun at a time at Ammu-Nation's prices; better guns unlock with rank
- Body armour that stays on: guards heal and re-vest after every fight
- Bodyguards: call up to two guards to follow you around the city and fight at your side
- Raids: send up to four reserve guards to take a rival front without you, with the odds shown first
- Guard posts and upgrades by size: small fronts take 2 guards, mid-size 3 and the biggest 4; Cameras, Floodlights, Reinforced Doors and an Armory
- A Story Mode economy: fronts cost and pay about what Story Mode properties do; they pay more the longer you hold them and the more you own
- Progression money can't skip: guns unlock with rank, bigger fronts need the Broker's trust (3 and 8 fronts held) and new fronts get two quiet days
- Staff at the front you're near: a cashier on the phone, a janitor sweeping, a chef on a smoke break
- A big, mixed cast of ordinary Los Santos street people, women and men, with nicknames for only some of them
- Difficulty from Relaxed to Brutal, Reset the City to take Los Santos again with the crew you built, and Start a New Empire for a new story game
- The game's own sounds, a message queue that never spams, first-time tips in GTA's help box and full controller support
- Nothing to collect and no upkeep chores; good guards win without you

## Controls

| Action | Controller | Keyboard |
|---|---|---|
| Open or close the Office | Hold Back/View for half a second (a tap still changes the camera) | Hold M, or set `OfficeHotkey` |
| Move in the Office | D-pad | Arrow keys |
| Select or open | A | Enter |
| Back | B | Backspace |
| Change a post or a setting | D-pad left/right | Left/Right |
| Buy or claim at a door | The button shown in the prompt | E |

## Playing alongside the story

Fronts & Rivals is built to run for your whole Story Mode playthrough. Everything pauses during missions, cutscenes, random events and character switches, and attacks only count free-roam time, so a long mission never costs you a front.

Money is at Story Mode scale. Early Franklin can afford the car wash and grow slowly; after the Jewel Store Job he can buy a second front. Michael after the heists can buy whatever is for sale, but the crews' fronts still have to be taken by force.

## Settings

The everyday ones are in the game under **The Office > Settings**: notifications, sound effects, GPS routes to attacks, map blips, difficulty, attack frequency, rival turf moves, live defenses and permanent death.

Everything else is in `scripts\FrontsAndRivals.ini`, with each line explained in the file:

- General: `OfficeHotkey`, `OfficeButton`, `PurchaseShot` (the establishing shot when you buy a front), `Tips`
- Attacks: `Difficulty`, `Frequency`, `CooldownMinutes`, `HoldMinutes`, `LiveDefense`, `PoliceRespond`, `EmpirePressure`
- Turf: `Frequency` for how often crews move at night
- Notifications: `Level`, `MinSecondsBetween`, `Taunts`, `StreetWord`, `MidnightReport`
- Economy: `IncomeMultiplier`, `WageMultiplier`, `FrontPriceDays`
- Guards: `PermanentDeath`, `ProtectedMode` (fronts can't be lost), `Raids`, `Bodyguards`
- Map: `ShowRivalFronts`, `ShowForSaleFronts`, `FrontStaff`

## Compatibility

- Single-player only; the mod switches itself off if a GTA Online session starts
- It never changes story missions, characters or GTA V's own save files
- It removes its people during missions, so missions never see them
- At most 28 people and 8 vehicles at once, all cleaned up when you leave, reload scripts or start a mission
- Job and money mods should be fine; they just give you more cash (lower `IncomeMultiplier` to compensate)
- Don't run it alongside other gang or turf-war overhauls; both would compete for the same people and gangs

## Uninstall

Delete `FrontsAndRivals.dll`, `FrontsAndRivals.ini` and the `scripts\FrontsAndRivals` folder. Cash the mod paid out stays with your characters.

## Known issues

- The Office contact sits at the end of the phone's contact list; that's where iFruitAddon2 puts mod contacts so story contacts are never overwritten
- A few fronts have fewer than four guard posts where the street layout leaves no safe spot

## For developers

- `cd src; dotnet build -c Release` builds the DLL and, if `Directory.Build.props.user` sets `GtaVDir`, deploys it to `<GtaVDir>\scripts` (start from `Directory.Build.props.user.example`)
- `cd tests; dotnet run` runs the unit tests for every pure system
- `dotnet run --project tools/BalanceSim -c Release` runs the Monte Carlo balance simulator
- `powershell -File tools\package.ps1` builds the release zip in `dist\`
- `DevTools=true` in the INI turns on F9 (copy position), F10 (dev menu) and an entity counter; it's off in the release
- Content lives in `scripts\FrontsAndRivals\`: `fronts.json`, `crews.json`, `lines.json`, `names.json`, `models.json`
