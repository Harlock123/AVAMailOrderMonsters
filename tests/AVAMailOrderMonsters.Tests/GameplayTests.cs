using Avalonia.Input;
using AVAMailOrderMonsters.Engine;

namespace AVAMailOrderMonsters.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avamailordermonsters-tests-" + Guid.NewGuid().ToString("N"));
    private static GameEngine Engine(int seed = 1) => new(HighScoreTable.Load(TempFolder()), seed) { Silent = true };

    /// <summary>An empty arena (no rocks, trees or buildings): two monsters a little way apart, facing each other.</summary>
    private static Bout Bare(Loadout a, Loadout b, double gap = 200, Mode mode = Mode.Duel)
    {
        var bout = Sim.Start(mode, a, b, 1);
        bout.Things.Clear();
        bout.Monsters[0].X = 400;
        bout.Monsters[1].X = 400 + gap;
        bout.Monsters[0].Y = bout.Monsters[1].Y = 300;
        return bout;
    }

    private static Loadout L(Species s, params Item[] items) => new() { Species = s, Items = [.. items] };

    private static void Run(Bout b, int steps, Controls c0 = default, Controls c1 = default)
    {
        for (int i = 0; i < steps && !b.Over; i++) Sim.Step(b, c0, c1);
    }

    // ───────────────────────────── the catalogue ─────────────────────────────

    [Fact]
    public void Buying_a_monster_then_fitting_it_out_and_selling_back_at_half_price()
    {
        var g = Engine();
        g.StartGame();
        Assert.False(g.BuyOrSell(Item.Laser));
        Assert.True(g.BuyMonster(Species.Gnasher));
        Assert.Equal(Leagues.StartingCredits - 300, g.Credits);
        Assert.True(g.BuyOrSell(Item.Laser));
        Assert.Equal(Leagues.StartingCredits - 300 - 320, g.Credits);
        Assert.True(g.BuyOrSell(Item.Horns));
        // A gnasher carries two things: no room for a third.
        Assert.False(g.BuyOrSell(Item.Acid));
        Assert.True(g.BuyOrSell(Item.Laser));
        Assert.Equal(Leagues.StartingCredits - 300 - 320 - 150 + 320 / 2, g.Credits);
        Assert.DoesNotContain(Item.Laser, g.Monster!.Items);
        Assert.False(g.BuyOrSell(Item.Claws));
    }

    [Fact]
    public void Trading_in_for_a_bigger_monster_keeps_its_fittings()
    {
        var g = Engine();
        g.StartGame();
        g.SetCredits(2000);
        g.BuyMonster(Species.Gnasher);
        g.BuyOrSell(Item.Armour);
        g.Monster!.Wins = 2;
        int before = g.Credits;
        Assert.True(g.BuyMonster(Species.Stompasaur));
        Assert.Equal(Species.Stompasaur, g.Monster.Species);
        Assert.Contains(Item.Armour, g.Monster.Items);
        Assert.Equal(2, g.Monster.Wins);
        // The old one at half its cost (fittings and all), the new one at full price, the fittings carried over at half.
        Assert.Equal(before + (300 + 250) / 2 - 750 - 250 / 2, g.Credits);
        Assert.False(g.BuyMonster(Species.Stompasaur));
    }

    [Fact]
    public void The_shop_cursor_moves_between_the_columns_and_fire_buys()
    {
        var g = Engine();
        g.StartGame();
        g.Tick(new Controls(0, 1));
        g.Tick(default);
        Assert.Equal(1, g.ShopRow);
        g.Tick(new Controls(0, 0, true));
        Assert.Equal(Species.Hydroid, g.Monster?.Species);
        g.Tick(default);
        g.Tick(new Controls(1, 0));
        g.Tick(default);
        Assert.Equal(Catalog.Monsters.Count + 1, g.ShopRow);
        g.Tick(new Controls(0, 0, true));
        Assert.Contains(Item.Horns, g.Monster!.Items);
    }

    // ───────────────────────────── the arena ─────────────────────────────

    [Fact]
    public void Claws_strike_only_close_and_in_front()
    {
        var b = Bare(L(Species.Gnasher), L(Species.Hydroid), gap: 46);
        double life = b.Monsters[1].Life;
        Sim.Step(b, new Controls(0, 0, true), default);
        Assert.Equal(life - Catalog.Items[Item.Claws].Damage, b.Monsters[1].Life, 3);
        b.Monsters[1].X = 400 + 200;
        Run(b, 60, new Controls(0, 0, true));
        Assert.Equal(life - Catalog.Items[Item.Claws].Damage, b.Monsters[1].Life, 3);
    }

    [Fact]
    public void A_laser_hits_at_once_down_the_line_and_a_rock_stops_it()
    {
        var b = Bare(L(Species.Hydroid, Item.Laser), L(Species.Hydroid), gap: 300);
        b.Monsters[0].Weapon = 1;
        double life = b.Monsters[1].Life;
        Sim.Step(b, new Controls(0, 0, true), default);
        Assert.Equal(life - 9, b.Monsters[1].Life, 3);
        b.Things.Add(new Thing { Kind = Prop.Rock, X = 550, Y = 300, R = 30 });
        Run(b, 60, new Controls(0, 0, true));
        Assert.Equal(life - 9, b.Monsters[1].Life, 3);
    }

    [Fact]
    public void A_rocket_bursts_and_there_are_only_twelve()
    {
        var b = Bare(L(Species.Megasaur, Item.Rockets), L(Species.Megasaur), gap: 300);
        b.Monsters[0].Weapon = 1;
        Run(b, 60, new Controls(0, 0, true));
        Assert.True(b.Monsters[1].Life < b.Monsters[1].MaxLife);
        Assert.Contains(b.Puffs, p => p.Colour == 0xFFFF9030);
        b.Monsters[1].Life = 9999;
        Run(b, 60 * 30, new Controls(0, 0, true));
        Assert.Equal(0, b.Monsters[0].Rockets);
        Assert.Empty(b.Shots);
    }

    [Fact]
    public void Acid_slows_what_it_hits()
    {
        var b = Bare(L(Species.Hydroid, Item.Acid), L(Species.Hydroid), gap: 250);
        b.Monsters[0].Weapon = 1;
        Run(b, 60, new Controls(0, 0, true));
        Assert.True(b.Monsters[1].Slowed > 0);
        Assert.True(b.Monsters[1].Speed < Catalog.Monsters[Species.Hydroid].Speed);
    }

    [Fact]
    public void Flame_burns_in_a_cone_while_held_and_burns_a_tree_down()
    {
        var b = Bare(L(Species.Trollock, Item.Flame), L(Species.Hydroid), gap: 150);
        b.Monsters[0].Weapon = 1;
        double life = b.Monsters[1].Life;
        Run(b, 30, new Controls(0, 0, true));
        Assert.True(b.Monsters[1].Life < life - 5);
        b.Things.Add(new Thing { Kind = Prop.Tree, X = 400, Y = 420, R = 16, Life = 18, MaxLife = 18 });
        b.Monsters[1].X = 900;
        b.Monsters[0].Facing = Math.PI / 2;
        Run(b, 60, new Controls(0, 0, true));
        Assert.True(b.Things[0].Down);
    }

    [Fact]
    public void Horns_charge_and_butt()
    {
        var b = Bare(L(Species.Stompasaur, Item.Horns), L(Species.Hydroid), gap: 180);
        b.Monsters[0].Weapon = 1;
        double life = b.Monsters[1].Life;
        Sim.Step(b, new Controls(0, 0, true), default);
        Assert.True(b.Monsters[0].Charging > 0);
        Run(b, 40);
        Assert.Equal(life - 24, b.Monsters[1].Life, 3);
    }

    [Fact]
    public void Armour_takes_a_quarter_off_and_a_regenerator_heals()
    {
        var b = Bare(L(Species.Gnasher, Item.Armour, Item.Regenerator), L(Species.Gnasher), gap: 46);
        b.Monsters[1].Facing = Math.PI;
        Sim.Step(b, default, new Controls(0, 0, true));
        Assert.InRange(b.Monsters[0].Life, 80 - 13 * 0.75, 80 - 13 * 0.75 + 0.1);
        b.Monsters[1].X = 900;
        Run(b, 60 * 5);
        Assert.Equal(80, b.Monsters[0].Life, 1);
    }

    [Fact]
    public void A_flier_crosses_rocks_that_stop_a_walker()
    {
        var walker = Bare(L(Species.Gnasher), L(Species.Hydroid), gap: 500);
        walker.Things.Add(new Thing { Kind = Prop.Rock, X = 460, Y = 300, R = 30 });
        Run(walker, 60, new Controls(1, 0));
        Assert.True(walker.Monsters[0].X < 420);
        var flier = Bare(L(Species.Mothra), L(Species.Hydroid), gap: 500);
        flier.Things.Add(new Thing { Kind = Prop.Rock, X = 460, Y = 300, R = 30 });
        Run(flier, 60, new Controls(1, 0));
        Assert.True(flier.Monsters[0].X > 520);
    }

    [Fact]
    public void Turbo_legs_are_a_third_faster()
    {
        var a = Bare(L(Species.Hydroid), L(Species.Hydroid), gap: 600);
        var b = Bare(L(Species.Hydroid, Item.Turbo), L(Species.Hydroid), gap: 600);
        Run(a, 60, new Controls(1, 0));
        Run(b, 60, new Controls(1, 0));
        Assert.Equal((a.Monsters[0].X - 400) * 1.33, b.Monsters[0].X - 400, 0);
    }

    [Fact]
    public void A_knockout_wins_a_duel_and_more_life_wins_on_points()
    {
        var b = Bare(L(Species.Gnasher), L(Species.Gnasher), gap: 46);
        b.Monsters[1].Life = 5;
        Sim.Step(b, new Controls(0, 0, true), default);
        Assert.Equal(0, b.Winner);
        Assert.Equal("A KNOCKOUT", b.How);
        var p = Bare(L(Species.Gnasher), L(Species.Gnasher));
        p.Monsters[0].Life = 50;
        p.Clock = p.Seconds - 0.001;
        Sim.Step(p, default, default);
        Assert.Equal(1, p.Winner);
        Assert.Equal("ON POINTS", p.How);
    }

    [Fact]
    public void Destructo_is_won_by_buildings_flattened()
    {
        var b = Bare(L(Species.Gnasher), L(Species.Gnasher), mode: Mode.Destructo, gap: 500);
        b.Things.Add(new Thing { Kind = Prop.Building, X = 455, Y = 300, R = 25, Life = 40, MaxLife = 40 });
        b.Things.Add(new Thing { Kind = Prop.Building, X = 700, Y = 600, R = 25, Life = 40, MaxLife = 40 });
        Run(b, 60 * 4, new Controls(0, 0, true));
        Assert.Equal(1, b.Monsters[0].Score);
        b.Clock = b.Seconds;
        Sim.Step(b, default, default);
        Assert.Equal(0, b.Winner);
        Assert.Equal("BUILDINGS 1 TO 0", b.How);
    }

    // ───────────────────────────── the career ─────────────────────────────

    [Fact]
    public void A_win_pays_the_prize_and_three_wins_a_promotion()
    {
        var g = Engine();
        g.StartGame();
        g.BuyMonster(Species.Gnasher);
        for (int bout = 0; bout < Leagues.BoutsPerLeague; bout++)
        {
            g.Fight();
            g.Bout!.Monsters[1].Life = 0;
            g.Run(2);
            Assert.True(g.Won);
            g.Run((int)(60 * GameEngine.ResultSeconds) + 2);
            Assert.Equal(GameState.Shop, g.State);
        }
        Assert.Equal(1, g.League);
        Assert.Equal(3 * Leagues.All[0].Prize, g.Score);
        Assert.Equal(3, g.Monster!.Wins);
        Assert.Equal(Catalog.Monsters[Species.Gnasher].Life + 15, g.Monster.MaxLife);
    }

    [Fact]
    public void A_loss_costs_a_licence_and_the_repair_bill_and_the_bout_is_fought_again()
    {
        var g = Engine();
        g.StartGame();
        g.BuyMonster(Species.Hydroid);
        int credits = g.Credits;
        g.Fight();
        var mode = g.Bout!.Mode;
        g.Bout.Monsters[0].Life = 0;
        g.Run(2);
        Assert.False(g.Won);
        g.Run((int)(60 * GameEngine.ResultSeconds) + 2);
        Assert.Equal(GameEngine.Licences0 - 1, g.Licences);
        Assert.Equal(credits - 450 / 5, g.Credits);
        Assert.Equal(0, g.BoutInLeague);
        Assert.Equal(mode, g.NextMode);
    }

    [Fact]
    public void Short_of_money_for_repairs_the_fittings_are_sold_and_bankrupt_is_the_end()
    {
        var g = Engine();
        g.StartGame();
        g.BuyMonster(Species.Hydroid);
        g.BuyOrSell(Item.Laser);
        g.SetCredits(0);
        g.Fight();
        g.Bout!.Monsters[0].Life = 0;
        g.Run(2);
        Assert.Empty(g.Monster!.Items);
        Assert.Equal(320 / 2 - (450 + 320) / 5, g.Credits);
        g.SetCredits(-1);
        g.Run((int)(60 * GameEngine.ResultSeconds) + 2);
        Assert.Equal(GameState.GameOver, g.State);
    }

    [Fact]
    public void Winning_the_galactic_championship_ends_the_game_a_champion()
    {
        var g = Engine();
        g.StartGame();
        g.BuyMonster(Species.Gnasher);
        g.SetLeague(Leagues.All.Length - 1, Leagues.BoutsPerLeague - 1);
        g.SetScore(25000);
        g.Fight();
        g.Bout!.Monsters[1].Life = 0;
        g.Run((int)(60 * GameEngine.ResultSeconds) + 5);
        Assert.True(g.Champion);
        Assert.Equal(GameState.GameOver, g.State);
        g.Run((int)(60 * GameEngine.GameOverSeconds) + 2);
        Assert.Equal(GameState.EnterInitials, g.State);
        foreach (char ch in "REX") g.Initials!.Type(ch);
        g.Run(1);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("REX", g.HighScores.Entries[g.NewEntryIndex].Initials);
    }

    [Fact]
    public void The_second_bout_of_each_league_is_destructo()
    {
        var g = Engine();
        g.StartGame();
        Assert.Equal(Mode.Duel, g.NextMode);
        g.SetLeague(0, 1);
        Assert.Equal(Mode.Destructo, g.NextMode);
        g.SetLeague(0, 2);
        Assert.Equal(Mode.Duel, g.NextMode);
    }

    [Fact]
    public void The_title_gives_way_to_the_demonstration_and_enter_starts_a_game()
    {
        var g = Engine();
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 8);
        Assert.NotNull(g.Monster);
        Assert.NotNull(g.Bout);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Shop, g.State);
        Assert.Null(g.Monster);
        Assert.Equal(Leagues.StartingCredits, g.Credits);
    }

    [Fact]
    public void Paused_nothing_moves()
    {
        var g = Engine();
        g.StartGame();
        g.BuyMonster(Species.Gnasher);
        g.Fight();
        g.Run(30, new Controls(1, 0));
        g.Pause();
        double x = g.Bout!.Monsters[0].X, clock = g.Bout.Clock;
        g.Run(120, new Controls(1, 0));
        Assert.Equal((x, clock), (g.Bout.Monsters[0].X, g.Bout.Clock));
        g.Resume();
        g.Run(10, new Controls(1, 0));
        Assert.True(g.Bout.Monsters[0].X > x);
    }

    [Fact]
    public void The_keys_move_fire_and_change_weapons()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.Up);
        Assert.Equal(-1, input.Player.Dy);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Down);
        Assert.Equal(1, input.Player.Dy);
        input.OnKeyUp(Key.Down);
        input.OnKeyDown(Key.Left);
        Assert.Equal(-1, input.Player.Dx);
        input.OnKeyUp(Key.Left);
        input.OnKeyDown(Key.Space);
        Assert.True(input.Player.Fire);
        input.OnKeyUp(Key.Space);
        input.OnKeyDown(Key.X);
        Assert.True(input.Player.Swap);
        input.OnKeyUp(Key.X);

        // Through to the monster: up the screen, and the next weapon in hand.
        var b = Bare(L(Species.Hydroid, Item.Laser), L(Species.Hydroid), gap: 500);
        input.OnKeyDown(Key.Up);
        Run(b, 20, input.Player);
        Assert.True(b.Monsters[0].Y < 300);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.X);
        Sim.Step(b, input.Player, default);
        Assert.Equal(Item.Laser, b.Monsters[0].InHand);
    }
}
