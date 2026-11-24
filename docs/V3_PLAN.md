# Fronts & Rivals 3.0: plan

Status: draft for Retch's approval (2026-11-23). Scope: everything in this file. Nothing here changes the story, missions
or characters, nothing ships game files, and nothing adds chores.

Build order is by dependency, not by size: each phase ends with tests, the balance sim where it touches balance, and an
in-game check by Retch before the next one starts.

---

## Phase 1: free roam only (the rule everything else sits on)

The mod already pauses and removes its people when the game's mission flag or random-event flag is set, during
cutscenes, character switches, fade-outs and when you can't control your character. That misses things that are
missions in all but name. 3.0 treats only true free roam as free roam.

**New pause reasons** (all checked every tick, all cheap reads; native names confirmed in Script Hook V .NET):

| Situation | How it's detected |
|---|---|
| Minigames (golf, tennis, darts, arm wrestling, yoga, races, flight school, shooting range...) | `IS_MINIGAME_IN_PROGRESS` plus the running-scripts list below |
| Side jobs that don't set the mission flag (taxi, towing, hunting, bail bonds, rampages, Strangers & Freaks steps, heist preps, friend activities) | A list of the game's own script names, built by watching what actually runs (see "Script watch") |
| Director Mode | `IS_IN_DIRECTOR_MODE` |
| Playing as an animal (peyote) or anyone who isn't Michael, Franklin or Trevor | The player's model |
| Standing at a mission's start | No mod spawns, attacks or street crews within about 120 m of a story or Strangers & Freaks start blip, found by scanning the map's blips (`GET_FIRST_BLIP_INFO_ID` / `GET_BLIP_SPRITE`), so a mission always starts on a clean street |

What a pause does stays the same: the clock stops (attack timers, raids, recovery), messages wait in the queue, and the
"world-clearing" reasons delete every mod person and car. Minigames and side jobs clear the world too.

**Script watch (dev tools only):** a logger that writes every game script that starts or stops. Retch plays one of each
activity once with it on; the names that show up become the block list. No guessing script names.

**Done when:** a checklist of every activity type has been started in-game with the mod running, and the log shows the
mod paused (and cleared) for each one and resumed afterwards.

---

## Phase 2: the playthrough check

Nothing new, just proving what exists. A written checklist, run with the dev tools, each item logged:

- Surprise attack on a front you're away from: timer, outcome text, guard deaths
- Live defense: convoy arrival, fight, win and loss, the bonus
- Raid: send, win, loss, called off (front changed hands)
- Taking a front in person: clear, claim, banner, establishing shot, first-front help
- Nightly turf moves between crews, the midnight payout and wages
- Bodyguards following you, on foot and in cars, and giving them back
- Street crews: warning, fight, crime events, police
- Save and load mid-everything; character switch mid-attack; a mission starting mid-attack; dying and being arrested
  mid-fight
- Reset All Fronts and Start a New Empire

Bugs found here are fixed before any new feature lands.

---

## Phase 3: raids that stay worth sending

Today a big front late in the game is a 5-30% raid even with your best people. Fixes, in plain rules:

- **Bigger raiding party as you grow:** 4 guards, 5 at 10 fronts held, 6 at 20
- **Soften them up first:** every crew soldier you kill on the street that day already lowers that crew's manpower;
  the Office now says so on the raid row ("The Hellfire MC are short of people today: better odds")
- **Honest advice:** below 25% the raid row says "Best taken in person" instead of offering a bad bet
- Balance sim over 300 games per setting before and after: raid win rates by front size, and no change to how fast a
  steady player grows

---

## Phase 4: gang wardrobes

Built from the game's own clothing only (no add-on clothing packs: they'd need files installed into the game and break
with updates). Data comes from root-cause/v-clothingnames (tops, legs, shoes, accessories, masks, hats, glasses,
watches, bracelets for both genders), compiled by a new repeatable tool (tools/OutfitBuilder) instead of by hand.

- **Dead Kings (forest green):** street wear, a green cap or bandana, chains, baggy jeans, trainers
- **Hellfire MC (orange):** leather biker vests over hoodies, orange bandanas, boots, sunglasses
- **Vinewood Elite (royal blue):** suits, blue shirts or ties, sunglasses, smart shoes
- **Ranks you can see:** lieutenants and the boss dressed a step up (better jacket, gold chain, watch)
- **Masks for attacks:** a crew hitting your front pulls bandanas or masks up
- **Your crew:** the same depth for your guards in your colour and style
- Every piece and prop is checked in the player's game before anyone wears it (components with
  `IS_PED_COMPONENT_VARIATION_VALID`, props against `GET_NUMBER_OF_PED_PROP_DRAWABLE/TEXTURE_VARIATIONS`); anything
  missing is skipped, as now
- A piece only goes to a gang if it comes in that gang's colour; where it doesn't, the look uses neutral colours
  (black, grey, denim) around one coloured piece

Retch approves each gang's looks from a screenshot sheet before it ships.

---

## Phase 5: the turf map

Each front's block shaded in its owner's colour on the pause map, like San Andreas, so the war reads at a glance.

- One translucent radius blip per front (about 120 m), in the owner's crew colour (yours in your colour), redrawn when
  a front changes hands
- Pause map only by default (the minimap stays clean); an Office setting for minimap too, or off
- Contested fronts (an attack on the way) pulse
- Works with the existing "hide blips" setting

---

## Phase 6: leaders in person

Bosses and lieutenants stop being only names in texts.

- Each crew's boss stands at its biggest front, with a name blip in a fight, more armour and a better gun
- Lieutenants lead some attacks on your fronts (named in the alert: "Marcus is leading the Dead Kings")
- Killing one in person feeds the nemesis loop that already exists (escapes, scars, promotions, revenge texts)
- A crew that loses its boss is shaken for a few days (fewer attacks) until a lieutenant takes over
- Fits the 28-person budget: a leader replaces a soldier, never adds one

---

## Phase 7: no endgame system (Retch, 2026-11-23)

Owning all 29 fronts is the reward: you collect the money. Office > Settings > Reset All Fronts already lets you play
the city again at any time, keeping your crew.

---

## Phase 8: polish

- **Your crew's cars:** bodyguards (and live-defense reinforcements) arrive in cars painted your colour
- **Midnight summary:** one text with income, wages, attacks and anything that changed hands overnight, instead of
  several
- **Front pages:** staff, upgrades, threat level and the last attack, in one place
- **Street life:** crews in their colours a little more often on their own turf
- **Legacy:** one test on the Legacy edition, or the readme and Nexus page say "Enhanced only"

---

## Ships as 3.0 when

- Every phase passes its tests and Retch's in-game check
- The balance sim shows a steady Normal player on the same pace as 2.x (22 fronts around day 38) and raids worth sending
- Saves from 2.x load and carry over (with a backup copy first, as always)
- README.txt (with the version history), Nexus page, GitHub release notes and How It Works page updated
