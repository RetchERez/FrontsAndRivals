# Changelog

## 1.0.0 (2026-11-20)

First full release.

- 29 real businesses as fronts across Los Santos, with guard posts placed from the game's navigation data
- Crew territories (South LS, the east side, the west) and nightly turf moves: crews grab fronts for sale and fight each other for fronts
- Rival front soldiers stay calm while you drive past and fight when you walk up, shoot nearby or hurt one
- Any front for sale can be bought in person at its door (the starter car wash is $10,000)
- The Office: phone contact, three side-by-side panels (sections, lists, details), native GTA menu style, control hints
- Guards: hiring from the Broker's daily recruits, traits, ranks and experience, permanent death, memorial
- Weapon ladder: guards start with a knife and step up through four guns, each at least +23% power
  (pistol $350, pump shotgun $550, SMG $1,300, carbine rifle $2,100); older saves keep each guard's gun
- Buying a front plays a short establishing shot of the building with the banner, like buying Story Mode property
  (3.5 seconds, HUD hidden, any button skips, never blocks the game, always hands the camera back)
- Story Mode return rate: fronts for sale are priced like Story Mode property (about 20 weeks to pay back, ~5% a week;
  Smoke on the Water is ~4.6%). Buying is a late-story investment; early on you grow by taking fronts by force
- Safety nets: midnight wages never take your cash below $0; with no phone contact and no hotkey the controller hold is
  switched back on so the Office is always reachable; after two in-game days with no fronts the Broker buys the car wash
  back and offers it again (with its starter crew if you have nobody left)
- Playing alongside the story: the Broker's car wash comes with two of the old owner's people already posted, so a
  broke early Franklin can hold it; simulated from early-story ($10,000), mid ($50,000) and post-heist ($5M) starts:
  early players grow slower, rich ones still have to fight for 24 of the 29 fronts
- Story Mode economy by default: a front pays about what a Story Mode property does (small ~$1,000 a night, about $7,000
  a week; big ~$3,000), and wages, hiring and guns (Ammu-Nation prices) scale with it; fronts cost three days of their
  design income (12 nights at Story Mode scale; the car wash $10,000). Story Mode properties are untouched and separate.
  IncomeMultiplier in the INI raises it for anyone who wants more
- Map: each front shows the game's own icon for its kind of business (Car Wash, Bar, Store, Nightclub, Jewel, Repair,
  Warehouse, Business), the for-sale icons while it's for sale, and flashes for a few seconds when it changes hands
- Monte Carlo balance pass (tools/BalanceSim, the mod's own rules, 150 games per setting): raids now only take guards
  waiting in the reserve and run once a day; a front taken by raid can be staffed straight away (its posts are placed on
  your first visit); grudge is gentler (+12 per front taken, -8 a day, up to +25% strength and +2 attackers); empire
  pressure adds attackers at 10 and 22 fronts. Normal: a staffed front holds ~57%, Relaxed ~70%, Brutal ~47%
- Every menu item, setting and label in GTA-style Title Case; Settings ordered everyday first, rules next, rare last;
  quick actions first on a front's page and bought upgrades on one line
- Raids: Send a Crew on any rival front's page (your four strongest; odds shown; 3 minutes; win = the front is yours with
  the usual trust, dug-in and grace rules; lose = hurt or killed guards and an angrier crew); Call Them Back; one at a time
- GTA's map: a waypoint on a front makes the Office open on that front's page, with a text for rival fronts
- Guard posts by size: 2 for small fronts, 3 mid-size, 4 big (guards on removed posts go to the reserve); small fronts
  face one fewer attacker
- Difficulty (Relaxed, Easy, Normal, Hard, Brutal): crew attack strength and front defense x0.7 to x1.45, and the wait
  between attacks x1.5 to x0.7
- Reset the City: every front back to its start, crews back at full strength, your guards kept with ranks, skills and guns
- More fronts, more money: a front pays +3% for every day you hold it (up to +30%, reset if you lose it), and every
  front you hold adds +1.5% to all of them (up to +20%); upgrades by front size (small: Cameras, Floodlights; mid-size adds Reinforced Doors; big adds the Armory), costing
  x1 / x1.5 / x2.5; rival crews defend
  their own fronts at x1.5 against raids; wages are cheap (150 / 250 / 350 / 500) and only bite with a
  big crew: past 12 guards each extra one adds 4% to everyone's wage (up to x3); the defend-it-yourself bonus is tonight's take, bonuses included
- A front you lose is dug in until the next midnight before you can claim it back
- Guards gain 1 XP for every night on a post (promotions are texted), so ranks and the guns they unlock come with time served
- Progression: guns unlock with rank (Recruits up to the shotgun, Soldiers the SMG, Veterans the carbine); the Broker
  only sells or backs claims on mid-size fronts once you hold 3 and the biggest once you hold 8; a front you just bought
  or took is left alone for two in-game days
- Early game: rival soldiers ignore you until you own a front (unless you hurt one), and the Broker's first text waits
  until your character has the $10,000 for the car wash
- Office > Settings > Start a New Empire (two presses; the old empire is kept as empire.old.json)
- Fill Empty Posts (strongest free guards first) and Arm This Front (everyone's next gun, total shown) on each front's page
- First-time tips for the Office, posting, attacks, buying at the door and bodyguards; Show Tips Again in Settings
- The Office shrinks to fit 4:3 and 5:4 screens, and shows 12 rows so a whole front page fits
- Killing your own guard or bodyguard (shot, blown up or run over) is recognised: its own texts, the usual death or
  injury rules, and recruits ask 25% more until the next day
- Bodyguards: up to two guards join your own group (follow, ride along, fight); blue blips; their post waits for them;
  killed or hurt the same as at a post; gone during missions and back afterwards
- Guard skills (Shooting, Strength, Stamina, Stealth, 0-100, average 50) that grow with days on duty, fights, kills and
  promotions; they move power x0.8-x1.2, injury and death chances x0.7-x1.3, recovery time, and live-fight aim and health.
  Hire prices follow skills (x0.86-x1.18). Older saves get rank-based skills. Guards can be fired from their page
- Staff at your fronts (the nearest one only, budget-safe, neutral FR_STAFF group, women and men in every trade)
- A bigger, mixed NPC pool: guards are street people instead of uniformed security (35 men's and 18 women's looks, recruits
  are women about 4 times in 10), each crew has 23-30 looks including women, and names come from a much bigger pool;
  only about a third of recruits and half of new crew lieutenants go by a nickname, and full names never repeat
- Attacks come without warning: the alert arrives as the hit starts, and your guards hold about 3-7 minutes (5 at even
  odds) depending on their strength (Cameras add 2; the Lookout trait is gone, every guard keeps watch), shown on a GTA-style timer bar (HoldMinutes in the INI)
- The call to come defend: the alert tells you when the hit lands and what holding the door pays (one day of the
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
- Attacks: cooldown by frequency, the attack alert and taunts, the hold timer, auto-resolve, live defenses with convoys, timer bars,
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
- Empire pressure: past 3 fronts, attacks come sooner (down to 55% of the wait at 23+ fronts) and squads grow at 10 and 22 fronts;
  the Office Overview shows your street heat
- Options: attack frequency or exact cooldown, first-attack delay, hold minutes, live defenses on/off, police response, empire pressure,
  turf frequency, rival alert range, front price, income and wage multipliers, permanent death, protected mode, map blips,
  skip intro, Office hotkey; the everyday ones are also in the Office's Settings

## Development milestones

- 0.1 (M1): skeleton, pause gate, crash-safe save, dev tools (confirmed in game)
- 0.2 (M2): fronts, streamed guards, in-game scouting
- 0.3 (M3): the Office, hiring, payouts
