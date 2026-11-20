# Fronts & Rivals

Single-player GTA V script mod. Design doc v1.0 (follow it exactly): https://claude.ai/artifact/KAYesFun6qhyBrRT8d5BBr
Verified natives: `docs/fronts_and_rivals_reference.md`.

## TECH STACK (don't change)
- C#, .NET Framework 4.8, ScriptHookVDotNet v3 API (compile against NuGet ScriptHookVDotNet3 3.6.0)
- LemonUI.SHVDN3 2.2.0 for menus/banners, iFruitAddon2 3.1.1 for the phone contact
- Must run on GTA V Legacy (SHVDN nightly) and Enhanced (SHVDN Enhanced)

## HARD RULES
- All code original. Never copy code, structure or names from other mods.
- Follow Rockstar's 10 Sept 2026 mod guidelines: no story/mission/character changes, no voice
  reuse, single-player only (disable if NETWORK_IS_SESSION_STARTED), free, no Rockstar logos.
- No chores: nothing mandatory, no collecting, no upkeep tasks.
- Native look only: phone, feed, banners, blips, help text, LemonUI. No floating markers.
- Stability: max 28 peds / 8 vehicles, every entity tracked and deleted, FR_ relationship
  groups + FR plates for orphan cleanup, no memory patches, no Wait loops on the game thread,
  every system wrapped in try/catch (3 failures in 60 s = system disables itself).
- No internet calls. Pause everything during missions (Game.IsMissionActive etc.).
- Don't guess APIs: check them against the SHVDN/LemonUI/iFruitAddon2 packages and NativeDB.
  If something can only be confirmed in-game, say so and say exactly what to test.

## HOW WE WORK
- One milestone at a time from the doc's Build plan. Each must build with zero errors, pass
  the tests, and stay playable.
- When a milestone builds, commit it with a clear message (e.g. "M1: skeleton, pause gate,
  save, dev tools") and give a short "what to test in-game" checklist.
- The user tests in GTA V and reports back. Fix bugs in small commits ("Fix: guards respawn twice").
- Tag each milestone the user has confirmed in-game (v0.1-m1, v0.2-m2, ...). Never tag unconfirmed work.
- Keep pure logic (rules, resolver, economy, scheduler, save, config parsing) free of game calls
  and covered by unit tests in tests/.
- Ask before pushing to any remote or deleting files.

## Build, test, deploy
- `cd src; dotnet build -c Release` builds `FrontsAndRivals.dll`.
- `cd tests; dotnet run` runs the unit tests; exit code = number of failures.
- Deploy: after every build, the `DeployToGta` target copies FrontsAndRivals.dll/.pdb/.ini into
  `$(GtaVDir)\scripts`. `GtaVDir` lives in the git-ignored `Directory.Build.props.user`
  (template: `Directory.Build.props.user.example`). Never hard-code the path.
- Pure files are compiled into the test project by path (see tests/Tests.csproj). A file listed
  there must not reference GTA, LemonUI or iFruitAddon2.

## Code conventions
- Namespaces `FrontsAndRivals.<Folder>`. Runtime data lives in `scripts/FrontsAndRivals/`.
- No LINQ or per-tick allocations in tick code. No `Script.Wait`/`Script.Yield` loops.
- Models: request, check on later ticks, give up after 5 s. Never call World.Create* on an unloaded model.
- Every created entity goes through `Registry` (budget + cleanup). Peds get an FR_ group, vehicles an FR plate.
- Errors go to the log, never to the screen (one feed message when a system disables itself).
