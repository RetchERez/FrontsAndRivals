# Location coordinates found so far (2026-11-20)

Coordinates are GTA V map positions (x, y, z). "Confirmed" means two or more independent sources agree,
or a source matched an in-game F9 reading. Positions are facts about the game map; no code was taken.

## Front candidates

| Front | Building | Coordinates | Status | Source |
|---|---|---|---|---|
| Car wash | Little Seoul car wash (Lindsay Circus, by the LTD) | -699.84, -934.00, 19.00 | Confirmed (2 sources) | FiveM car wash configs (Mercy-Collective/mercy-framework, ixhal/ixhal-carwash) |
| Bar | Mirror Park Tavern | door 1217.94, -416.80, 67.78 | One source | dydrmr/gtav-enhancedtaximissions (SP taxi mod location list) |
| Pawn shop | Pawn & Jewelry, Strawberry Ave | not found | | Only the Downtown Vinewood pawn shop turned up (412.34, 314.81, 103.13), which is a different building |
| Laundromat | Avalon Laundry & Cleaners, Rancho | not found | | |
| Chop shop | Otto's Auto Parts / Auto Glass, Popular St, La Mesa | not found | | |
| Truck depot | Walker warehouse, Signal St, Elysian Island | not found | | Post OP Depository area ambient points: -373.84, -2762.32, 5.00 (gta.wiki footnote) |

Mirror Park Tavern: taxi stop on the road at 1209.94, -415.29, 67.26; door at 1217.94, -416.80, 67.78, heading 78.

## Story locations (for the 150 m rule)

| Place | Coordinates | Status | Source |
|---|---|---|---|
| Franklin's house door, Forum Drive | -14.87, -1441.18, 31.19 | Confirmed (matches in-game F9 -14.38, -1438.51) | Stuyk/rebar-altv door list |
| Vanilla Unicorn main door | 127.96, -1298.50, 29.42 | Confirmed (3 sources) | rebar-altv doors, mercy-framework blips, NPCTalk example |
| Hands On Car Wash (Strawberry) | 26.59, -1392.02, 27.36 | Confirmed (several sources) | FiveM car wash configs; about 60 m from Franklin's door |
| Premium Deluxe Motorsport | -56.2, -1095.39, 26.42 | One source | mercy-framework blips |

## Still needed
- Exact door positions for the pawn shop, laundromat, chop shop and truck depot
- Coordinates for Grove Street, the LSPD impound in Davis, Downtown Cab Co., and Floyd's apartment, for the 150 m checks

## Final front doors (2026-11-20, user chose known-door buildings)

| # | Front | Door (x, y, z, h) | Zone (DurtyFree zones.json) | Source |
|---|---|---|---|---|
| 1 | Hands On Car Wash | 25.20, -1391.98, 28.91, 0 | Strawberry | FiveM car wash configs; alt:V anchor 55.7, -1391.3 confirms the site |
| 2 | Brewer's Drop Liquor | 98.90, -1309.50, 29.30, 122 | Strawberry | dydrmr/gtav-enhancedtaximissions (passenger walk-to point) |
| 3 | Discount Jewels | 130.90, -1771.80, 29.70, 320 | Davis | same taxi list |
| 4 | Mirror Park Tavern | 1217.94, -416.80, 67.78, 78 | Mirror Park | same taxi list |
| 5 | Atomic Service Center | 487.00, -1878.30, 26.20, 272 | Rancho | same taxi list |
| 6 | Fridgit Cold Storage | 867.80, -1639.70, 30.20, 103 | La Mesa | same taxi list |

Guard posts are generated in-game from the navmesh (F10 > Scout fronts > Auto-scout all fronts), then copied into src/Content/fronts.json and shipped.