# Fronts & Rivals - verified code examples (v1.0 design doc)

These files compile against the real libraries:
- ScriptHookVDotNet3 3.6.0 (NuGet), LemonUI.SHVDN3 2.2.0, iFruitAddon2 3.1.1, .NET Framework 4.8.
- Build: `cd src && dotnet build -c Release`  ->  FrontsAndRivals.dll

`tests/` runs the pure rules (defense rating, win chance, squad size, attack strength,
10,000-fight Monte Carlo) against every number in the design doc: `cd tests && dotnet run`.

Compiling proves every API name, type and signature exists. It does NOT prove in-game
behaviour (AI driving, scenario placement, relationship reactions): test those in GTA V.

Runtime note (SHVDN wiki): SHVDN 3.6.0 stable has a compatibility issue with game
v1.0.3258.0 and later, so players need a SHVDN nightly build (v3.6.0-nightly.89 or later)
or SHVDN Enhanced to run the mod. Build against the stable package, run on nightly.
