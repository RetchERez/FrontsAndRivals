# Balance simulator

Plays hundreds of 30-day games with a simple AI player on the mod's own rules code (the same pure files the unit
tests compile) and prints how each one unfolds: fronts held, income, cash, attacks defended by how well the front
was staffed, raids, guard deaths and spending. Three playstyles (Casual, Normal, Aggressive) at three difficulties.

    dotnet run --project tools/BalanceSim -c Release -- 150 30          # 150 games per setting, 30 in-game days
    dotnet run --project tools/BalanceSim -c Release -- 1 12 trace      # one Normal game, attack by attack

Targets the 1.0 numbers were tuned to (Normal difficulty, Normal player):
- a staffed front holds about 55-60% of attacks (Relaxed ~70%, Brutal ~45-50%)
- 3 fronts by day 4, 8 by day 12, 15 by about day 23 of a 30-day campaign
- income rises every stage; money stays useful into the late game
- guards die sometimes (about one every three in-game days), never in waves
