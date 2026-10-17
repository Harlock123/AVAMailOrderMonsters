using AVAMailOrderMonsters.Engine;

namespace AVAMailOrderMonsters.Tests;

/// <summary>How the computer gets on with a whole career (set MONSTER_PROBE to run it, for tuning).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_career_goes()
    {
        if (Environment.GetEnvironmentVariable("MONSTER_PROBE") is not { } mode) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int champions = 0;
        for (int seed = 1; seed <= 10; seed++)
        {
            var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "mom-probe-" + Guid.NewGuid().ToString("N"))), seed) { Silent = true };
            g.StartGame();
            var brain = new Brain(0, 0.8);
            int bouts = 0, wins = 0;
            var lostTo = new List<string>();
            for (int t = 0; t < 60 * 60 * 60 && g.State is GameState.Shop or GameState.Playing; t++)
            {
                if (g.State == GameState.Shop) { g.AutoShop(); bouts++; continue; }
                var c = mode == "lazy" ? default : brain.Think(g.Bout!);
                bool was = g.Phase == Phase.Fight;
                g.Tick(c);
                if (was && g.Phase == Phase.Result)
                {
                    if (g.Won) wins++;
                    else lostTo.Add($"L{g.League + 1}B{g.BoutInLeague + 1} {g.Bout!.Mode} {g.Monster!.Def.Name}[{string.Join("+", g.Monster.Items)}] v {g.Opponent!.Def.Name}[{string.Join("+", g.Opponent.Items)}] {g.Bout.How}");
                }
            }
            if (g.Champion) champions++;
            output.WriteLine($"seed {seed}: {(g.Champion ? "CHAMPION" : $"out in league {g.League + 1} bout {g.BoutInLeague + 1}")}, {bouts} bouts, {wins} won, credits {g.Credits}, monster {g.Monster?.Def.Name} [{string.Join("+", g.Monster?.Items ?? [])}]");
            foreach (var l in lostTo) output.WriteLine($"    lost {l}");
        }
        output.WriteLine($"{champions} champions; {sw.ElapsedMilliseconds} ms");
    }
}
