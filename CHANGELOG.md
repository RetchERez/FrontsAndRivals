# Changelog

## 1.0.0 (2026-11-20)

First full release.

- 29 real businesses as fronts across Los Santos, with guard posts placed from the game's navigation data
- Crew territories (South LS, the east side, the west) and nightly turf moves: crews grab fronts for sale and fight each other for fronts
- Rival front soldiers stay calm while you drive past and fight when you walk up, shoot nearby or hurt one
- Any front for sale can be bought (five days of its income; the starter car wash is $25,000)
- The Office: phone contact, three side-by-side panels (sections, lists, details), native GTA menu style, control hints
- Guards: hiring from the Broker's daily recruits, traits, ranks and experience, permanent death, memorial
- Weapon ladder: guards start with a knife and step up through four guns, each at least +23% power
  (pistol $750, pump shotgun $1,500, SMG $3,000, carbine rifle $6,000); older saves keep each guard's gun
- The call to come defend: every warning tells you when the hit lands and what holding the door pays (one day of the
  front's takings), with a GPS route to the front (Office setting); if you're within 1 km when the window closes, the crew
  waits 90 more seconds for you
- Guards switch to a newly bought gun on the spot
- The endgame: crews recruit off the street every night (4 a night with no fronts), so they always come back for what you took;
  holding every front in the city gets its own moment
- Permanent death can be switched off in the Office (deaths become two-day injuries)
- Balance pass: a prepared front is now favoured (about 55-70%) and a neglected one loses; attack leaders add less
  (lieutenant +10, boss +30), wages are $300-$1,200, fronts cost five days of income, reinforced doors give +20 for $20,000,
  the Broker sends 4 recruits a day, the roster holds 60, and empire pressure never brings attacks closer than 20 minutes
- Fronts: buying the starter car wash, four upgrades (cameras, reinforced doors, floodlights, armory), defense and income bars
- Rival crews: Crown Kings, Ironside MC and Velvet Syndicate, each with a named boss and three lieutenants
- Attacks: cooldown by frequency, warning texts and taunts, attack window, auto-resolve, live defenses with convoys, timer bars,
  door integrity, a fleeing leader, police response, red blips on attackers on foot, a 6-minute cap, and a hand-off to auto-resolve
  if you die, leave or a mission starts (the whole squad is resolved if the convoy never arrived)
- Guard posts: on walkable outdoor ground, off roads, same floor as the door, in sight of the door, on solid ground with clear headroom
- Taking fronts: clear a crew front's soldiers and claim the door
- Nemesis loop: rep, scars, return texts, promotions, boss succession, collapse and rebuild
- Economy: midnight payouts (wages first), unpaid guards quit (Loyal ones never do), up to three skipped midnights paid
- Map: front blips with threatened and under-attack states, rival fronts in crew colours
- Stability: pause during missions, entity budget (28 peds, 8 vehicles), full cleanup, crash-safe saves with backups,
  every system self-disables after three errors in a minute
- Notifications go through one queue: a minimum gap (20 s default), important messages first, stale gossip dropped,
  and a level (All, Important, Minimal) with switches for taunts, street word and the midnight report
- Sound effects from the game's own HUD sound sets (attack warning and start, purchases, midnight take, banners), with an on/off setting
- Controller support: hold Back/View (or M) for half a second to open the Office, like GTA Online (a tap still changes the camera);
  every menu shows the pad's buttons, and with DevTools=true the dev menu opens from Office > Settings
- Empire pressure: past 3 fronts, attacks come sooner (down to 55% of the wait at 23+ fronts) and squads grow at 6, 12 and 20 fronts;
  the Office Overview shows your street heat
- Options: attack frequency or exact cooldown, first-attack delay, warning hours, live defenses on/off, police response, empire pressure,
  turf frequency, rival alert range, front price, income and wage multipliers, permanent death, protected mode, map blips,
  skip intro, Office hotkey; the everyday ones are also in the Office's Settings

## Development milestones

- 0.1 (M1): skeleton, pause gate, crash-safe save, dev tools (confirmed in game)
- 0.2 (M2): fronts, streamed guards, in-game scouting
- 0.3 (M3): the Office, hiring, payouts
