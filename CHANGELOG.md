# Changelog

## 1.4.1 (2026-11-21)

- The keyboard Office key is now F4. F11 turned out to be the game's helmet visor key (F9 and F10 drop your weapon and
  ammo), while F4 does nothing in GTA V
- RB + D-pad Left works in either order: hold D-pad Left first, then press RB, and you won't duck into cover

## 1.4.0 (2026-11-21)

- New Office controls: holding Back/View (or M) also opened the game's own Interaction Menu on top of the Office, so
  the Office no longer uses that button at all
  - Controller: press RB + D-pad Left to open or close the Office (the combo trainers like Menyoo use)
  - Keyboard: F11 by default, changeable with OfficeHotkey in the INI
  - The phone contact (with iFruitAddon2) works as before
- The game's Interaction Menu and the camera button behave exactly as in an unmodded game again
- Back up your saves note added to the install steps
- The release zip uses standard forward-slash folder paths, so every unzip tool and mod manager puts the files in
  the right folders

## 1.3.0 (2026-11-20)

Safeguards and a self-check, no new content.

- Outfit check: the first time you play each session, every clothing piece is checked against your copy of the game
  (on two hidden test characters, removed straight away). Any piece the game doesn't have is dropped before anyone
  wears it, so a mismatch between the Legacy-edition clothing data and Enhanced can never show as a broken outfit.
  The log says how many pieces were checked and dropped
- Real police: the cops who chase crew cars and break up street fights are now the game's own police, so shooting
  them gets you a wanted level like any cop. They're still counted and cleaned up by the mod
- Frame-rate safeguard: street groups are scenery, so when the game is struggling (under 40 FPS) only one is out, and
  under 30 none, until it recovers
- Before a different version of Fronts & Rivals uses your empire, a copy is kept as empire.before-<version>.json
- If LemonUI is missing from the scripts folder, a message says so on screen instead of the mod failing silently, and
  an older LemonUI than 2.2 is flagged in the log

## 1.2.0 (2026-11-20)

- Bodyguards are a perk of a real empire: the first at 8 fronts, the second at 14 (Soldiers or better). Lose fronts
  and drop below a threshold, and the extra bodyguard heads back to their post with a note saying why
- Your first front comes with nobody: you hire and post your own crew, and a help box shows how (hold Back/View or M
  to open the Office, Hire Recruits, then post them under Fronts, colour and style in Settings)
- The Office key is held back from the game while the Office uses it, so it can never open anything else at the same time

## 1.1.0 (2026-11-20)

Fixes and balance from a full code review and a fact check of every game function, model, scenario and clothing ID
against primary sources (citizenfx natives docs, alloc8or's native DB, DurtyFree's game data dumps, root-cause's clothing
datasets and the Script Hook V .NET source).

- Car kills count again, but the whole front opens fire and calls for backup once a visit: a crew car with two more
  soldiers about 20 seconds later, and the front isn't yours until they're down (it retries for 20 s if a car can't
  spawn yet, so a car kill never hands you a front for free)
- Street cops have their own police faction (they were landing on your side) and go after the crews
- Bodyguards change to a new crew colour, style or gun on the spot
- Hangouts: a car that fails to spawn never leaves an empty group blocking new ones, one bad group can't stop the
  system, street crimes are chosen after the group's car, and street crew you kill with your car count
- Crew vehicles: full documented health (1000 engine and tank) and no blowing up from a single explosion, instead of
  values past the documented maximum; paint codes fixed to match the crew colours (racing green, orange, bright blue)
- Attackers use the right "always fight" combat attribute (5, not 46)
- Muggers walk up behind their victim (the offset was going to the side)
- Dead Kings hoodies use the real Dark Green texture, so forest green differs from green
- Patrol points and bodyguard arrivals never pick a spot inside a building or in water
- The Office's Messages page is saved with your empire
- Reset All Fronts and Start a New Empire open a Yes/No confirmation page, No selected first
- Menu titles are fitted by their measured width, and menus are pinned top-left inside the safe zone
- Settings changes run inside the menu's safety net
- Recruits are rolled fresh every game (20-60 per skill, now and then a standout), and hiring costs $150-$700 by trait
  and skill, about $250-$450 for a typical recruit

Still to confirm in game (no source could settle these): that the clothing numbers from the Legacy edition match
Enhanced exactly, and the women's "no undershirt" slot. Any piece the game rejects is skipped and written to the log.

## 1.0.0 (2026-11-20)

First full release.

- The Office is one GTA-style menu with submenus, like the Interaction Menu: a single column top-left, one level on
  screen at a time (Office > Fronts > a front), Select to go in and Back to come out. The top row is always the next
  thing to do and goes straight there. Short banner titles that always fit; GTA's own menu arrow
  (the old arrow character drew as a white block); owners in their crew's colour on the Fronts list; a Messages page
  that keeps every text and alert to read again
- Crew colours on clothing, Saints Row style: every crew member wears their crew colour on a major piece (Dead Kings
  forest green hoodies, jackets and joggers; Hellfire MC orange vests and biker jackets; the Vinewood Elite in royal blue
  suits, shirts and cardigans), with varied looks, faces and hair. Your guards and bodyguards wear your colour in the
  style you pick (Office > Settings > Crew Style: Street, Smart, Biker or Mixed), and change on the spot when you switch.
  Built on GTA Online-style characters with the game's own documented clothing; any piece the game doesn't have is
  skipped rather than shown broken
- 19 crew colours to pick from (burgundy, gold, cyan, lime, lavender and bright blue added)
- Crews on their streets: anywhere on a crew's turf its people hang out (sometimes round a car), walk alone or in pairs,
  or cruise in a car together, two groups out at once on cool streets and three on warm ones. Around your fronts it's
  your own crew, friendly, in your colour. Rivals are calm until provoked; each kill costs their crew a soldier
  (INI Map Hangouts)
- Street crime, only ever by crew members and never more than one at a time (three minutes apart): muggings at
  gunpoint, carjackings, the odd shooting, crew cars chased by the police, and kidnappings into a crew car. Cops respond
  to 40% of street fights between crews (INI Attacks PoliceRespond turns police off)
- Street fights: where two crews' turfs meet, now and then a group from each runs into the other and they shoot it out
  (about 1 in 7 border groups); it costs both crews soldiers, never claims a front
- Your crew's street groups stay calm until you fight near them, and a front is only claimed if you're within 100 m
  when its last soldier drops
- Car kills: running a soldier over counts, but the whole front opens fire on you and calls for backup once a visit, a
  crew car with two more soldiers about 20 seconds later; the front isn't yours until they're down too
- Crew rides in three tiers, by revenge intensity (grudge plus half the street heat): Dead Kings Manana/Emperor, then
  Primo Custom/Tornado Custom, then Dominator/Buccaneer Custom; Hellfire MC Daemon/Hexer, then Zombie Chopper/Daemon
  Custom, then Nightblade/Gargoyle; Vinewood Elite Washington/Schafter, then Cognoscenti/Baller LE, then the armoured
  Cognoscenti and Schafter V12. All in their crew's paint (the Vinewood Elite now a bright metallic blue) and tougher than
  a street car, for every crew alike
- Bodyguards are earned: the first at 3 fronts, the second at 8, and only Soldiers or better. The guard page says
  what's needed for the next one
- Easier to read: the Office is 540 wide; the main menu shows free guards and street heat (Cool / Warm / Hot /
  Blazing, coloured); a front's defense reads Strong, Fair or Weak against a typical attack instead of a number
- No Broker: the first-front introduction is a GTA help box (and the car wash's map icon flashes); other messages come
  from the Office or the street. A front is yours the moment its last soldier drops, with no trip to the door
- Everyone in a fight (your guards, bodyguards, crew soldiers and attackers) has twice an ordinary ped's health
- Crew soldiers: 3 at small fronts (the car wash's 3 still only carry pistols and melee), 5 mid-size, 8 at the biggest.
  Soldiers in a fight and attackers wear a small blip in their crew's map colour; Hellfire MC is now bright red
- Takeovers only: every front starts in a crew's hands and nothing is for sale. The car wash is the easy way in: two
  Dead Kings kids with pistols, bats, knives and crowbars, no armour, and two of its workers join you when you take it
- Rivals scale with the front: small fronts hold 3 soldiers with everyday guns (aim 20, no armour), mid-size 5 (aim 27,
  25 armour, more rifles) and the biggest 7 (aim 34, 50 armour, the most heavy guns), standing in twos at their posts
- Street heat (Office > Overview, with a meter): +25 for every front you take, -15 every midnight. Cool streets mean
  calmer crews (attacks x1.25 further apart); blazing ones (70+) bring attacks sooner (down to x0.65), one more
  attacker, and crew fronts with up to +35 armour, heavier guns, +12 aim and x1.6 defense against raids (not the car wash)
- Taking a front plays like finishing a job in Story Mode: the gold mission-passed shard, the property chime and the
  establishing shot of the building
- The Broker's first text arrives when Franklin's first mission ends (or after ten minutes of free roam in a game
  that's already past it). If you lose every front, after two days the car wash is left with one soldier
- Balance (200 games per player type, 60 days, Normal): a steady player holds ~8 fronts by day 14 (about 11 hours),
  ~15 by day 33 and ~22 by day 52 (~40 hours, about the story's length); someone playing only the mod and hitting a
  front every few hours reaches 22 around day 14 (~11 hours) and wins about half their takeovers; staffed fronts hold
  ~70%; nobody is wiped out. Money stays below the story's: a mod-only player has ~$19k after 6 hours and ~$60k after
  12, next to the Jewel Store Job's ~$290k
- Quiet until you're in: before your first front the crews make no turf moves, no attacks and no texts, and their
  soldiers only fight if you hurt one
- Fixed: the takeover shot's hook was set before the Director existed, which stopped the mod loading

- 29 real businesses as fronts across Los Santos, with guard posts placed from the game's navigation data
- Crew territories (South LS, the east side, the west) and nightly turf moves: crews grab fronts for sale and fight each other for fronts
- Rival front soldiers stay calm while you drive past and fight when you walk up, shoot nearby or hurt one
- The Office: phone contact, native GTA menu style, control hints
- Guards: hiring from the Broker's daily recruits, traits, ranks and experience, permanent death, memorial
- Weapon ladder: guards start with a knife and step up through four guns, each at least +23% power
  (pistol $350, pump shotgun $550, SMG $1,300, carbine rifle $2,100); older saves keep each guard's gun
- The crews are renamed: the Dead Kings (South LS street gang), the Hellfire MC (east-side bikers) and the Vinewood Elite
  (west-side mob). Saves are unaffected
- Warring factions: the crews hate each other in the world as well as at night (rival soldiers shoot on sight if their
  paths cross), and Office > Rivals shows who each crew is at war with
- Crew arsenals: everyday and heavy guns per crew (Dead Kings: pistols, micro SMGs / sawn-offs, assault rifles; Hellfire:
  pistols, sawn-offs / pump shotguns, assault rifles; the Vinewood Elite: combat pistols, SMGs / carbines). Heavy guns are 10%
  of soldiers at small fronts, 35% mid-size, 60% big, more with grudge; front soldiers wear 0 / 25 / 50 armour; attack
  leaders carry the best gun and a full vest; about 1 attacker in 20 brings the crew's explosive (Dead Kings grenades and
  Molotovs, Hellfire Molotovs and pipe bombs, the Vinewood Elite a grenade launcher or an RPG), one per squad at most
- Body armour: $750 a guard (guard page, or Vest This Front for everyone at once). In a live fight they wear a full
  100-point vest; when a fight is decided without you they're hurt x0.75 and killed x0.65 as often (and x1.03 stronger).
  Bodyguards wear theirs too. A vest is the guard's for good, and after every fight guards heal back to full with the
  vest topped up (bodyguards ten seconds after they stop fighting)
- Crew rides: each crew has two signature vehicles and convoys seat the whole squad (Hellfire MC on Daemon and Hexer
  bikes two-up, the Dead Kings in Dominators and Mananas, the Vinewood Elite in Schafters and Washingtons)
- Variance pass (200 games per setting, 60 days, p10/p50/p90 and tails): every front holds a little by itself (6 / 10 / 14
  by size) so empty fronts aren't free wins, and the roster holds 70. Normal: staffed fronts hold ~68%, worst losing
  streaks down from 10 to 6, nobody wiped out, holding all 29 is a late-story goal (~day 45-60)
- Story-length pacing (60-day Monte Carlo): attacks wait 60 free-roam minutes on Normal (Low 90, High 35); crews now
  and then sell a front they can't hold so the market keeps about two for sale; raids are a fair fight (crews defend at
  x1.8: ~32% early, ~58% mid, ~73% late); a normal player holds ~15 fronts by day 30 and ~22 by day 40
- Office Overview starts with a Next Step line (defend, buy, post, hire, arm, raid, grow), and selecting it goes there
- More settings: Difficulty (starting), Raids, Bodyguards, PurchaseShot and Tips in the INI
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
- Rival crews: Dead Kings, Hellfire MC and the Vinewood Elite, each with a named boss and three lieutenants
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
