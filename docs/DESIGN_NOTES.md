# Where 1.0 differs from design doc v1.0

Each difference was a user decision or a simplification made to keep 1.0 finished and stable.

| Design doc | 1.0 | Why |
|---|---|---|
| An HQ in Cypress Flats with a door, fade, yard guards and HQ upgrades | No HQ; the Office opens from the phone (or an optional hotkey) | User decision ("maybe no HQ"); the phone contact covers every HQ function, and reserve guards simply wait off the map |
| Six fronts | 29 real businesses, crews holding their own turf, five for sale | User decisions: fronts must be real buildings, and the city should be full of fronts with rivals gunning for them. Doors come from published positions (docs/locations_found.md); posts are placed from the navmesh the first time anyone comes near |
| Crews only attack you | Crews also grab neutral fronts and fight each other each night (Turf) | User request: rivals gunning for every front |
| Rival front guards fight on sight | Calm until you walk up on foot, shoot nearby or hurt one | With 24 crew fronts around the city, fighting on sight would turn every drive into a gunfight |
| Fronts at least 150 m from story properties | Rule dropped | User decision; the pause gate removes all mod peds during missions |
| Lost fronts keep half their upgrades | They keep all of them | The doc lists this as an open question; on/off upgrades can't be halved, and keeping them avoids chores |
| Guard posts scouted by hand | Placed from the navmesh (GET_SAFE_COORD_FOR_PED) and shipped in fronts.json | User request |
| Guard portraits in the Office | Names, traits and ranks only | LemonUI menus can't show live headshots; the feed still uses real mugshots where a ped exists |
| Arrival camera | Not in 1.0 | Cut to keep the live defense simple and safe; the INI option was removed |
| Workers, customers and ownership cars at fronts | Staff only, at the nearest front, while 12 ped slots stay free | Makes owned fronts look open without risking the 28-ped budget; customers and cars still cut |
| Laying low: guards walk inside | Guards on screen walk to the door, others despawn | As designed |
| Rival fronts show once seen or tipped off | Shown when Map > ShowRivalFronts is on | Simpler and clearer |
| Fixed attack pace however big you get | Empire pressure: shorter cooldown and bigger squads as you hold more fronts (INI EmpirePressure) | With 29 fronts the late game went quiet; pressure keeps holding a big empire a job |
| Pistol, SMG, rifle ($3,000 / $8,000 upgrades) | Knife start and a four-gun ladder: pistol $750, shotgun $1,500, SMG $3,000, carbine $6,000 | User request: progressive with only big steps (micro SMG and assault rifle cut as +4-8% steps); prices balanced against front income |
| Leader bonus lieutenant 25, boss 45; wages 500-2,000; fronts 6 days; roster 30; 3 recruits a day | Lieutenant 10, boss 30; wages 300-1,200; 5 days; roster 60; 4 a day; doors +20 | Balance pass: with the doc's numbers a fully prepared front won under half its fights and four-guard tier-0 fronts barely paid. Target win chances are locked in by RulesTests.BalanceTests |
| Office: two levels, separate screens | Three side-by-side panels | User request: submenus visible at the same time |

## Needs in-game confirmation

- Convoys reaching the door on every front (AI driving is the doc's biggest risk)
- CREATE_INCIDENT_WITH_ENTITY with service 1 sending police (community notes, not Rockstar)
- Crew soldiers at rival fronts engaging the player at a sensible range
- The Office banner texture (commonmenu/interaction_bgd) and timer bars on Enhanced
