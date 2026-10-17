using AVAMailOrderMonsters.Engine;

namespace AVAMailOrderMonsters.Tests;

/// <summary>Whole careers: the computer's shopping and handling take it to the top, and doing nothing gets nowhere.</summary>
public class CareerTests(ITestOutputHelper output)
{
    private static GameEngine Engine(int seed) => new(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avamailordermonsters-tests-" + Guid.NewGuid().ToString("N"))), seed) { Silent = true };

    private static void Career(GameEngine g, Func<Bout, Controls> hand)
    {
        g.StartGame();
        for (int t = 0; t < 60 * 60 * 60 && g.State is GameState.Shop or GameState.Playing; t++)
        {
            if (g.State == GameState.Shop) { g.AutoShop(); continue; }
            g.Tick(g.Bout != null && g.Phase == Phase.Fight ? hand(g.Bout) : default);
        }
    }

    [Fact]
    public void The_computer_wins_the_galactic_championship_more_often_than_not()
    {
        int champions = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var g = Engine(seed);
            var brain = new Brain(0, 0.8);
            Career(g, brain.Think);
            output.WriteLine($"seed {seed}: {(g.Champion ? "champion" : $"out in league {g.League + 1}")}, winnings {g.Score}");
            if (g.Champion) champions++;
            Assert.True(g.League >= 2, "it should get past the city league");
        }
        Assert.True(champions >= 5, $"{champions} of 8");
    }

    [Fact]
    public void Standing_still_in_the_arena_loses_everything()
    {
        var g = Engine(1);
        Career(g, _ => default);
        Assert.Equal(GameState.GameOver, g.State);
        Assert.Equal(0, g.League);
        Assert.Equal(0, g.Score);
    }

    [Fact]
    public void The_challengers_get_tougher_league_by_league()
    {
        var d = new Dice(5);
        double Avg(int league)
        {
            double total = 0;
            for (int i = 0; i < 20; i++) total += Shopper.Buy(Leagues.All[league].Budget, ref d).Cost;
            return total / 20;
        }
        for (int l = 1; l < Leagues.All.Length; l++) Assert.True(Avg(l) > Avg(l - 1));
        for (int i = 0; i < 50; i++)
        {
            var l = Shopper.Buy(1000, ref d);
            Assert.True(l.Cost <= 1000);
            Assert.True(l.FreeSlots >= 0);
        }
    }
}
