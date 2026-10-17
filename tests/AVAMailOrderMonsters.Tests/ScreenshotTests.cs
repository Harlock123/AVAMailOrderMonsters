using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAMailOrderMonsters.Engine;
using AVAMailOrderMonsters.Views;
using Controls = AVAMailOrderMonsters.Engine.Controls;

namespace AVAMailOrderMonsters.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set MONSTER_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avamailordermonsters-ui-" + Guid.NewGuid().ToString("N"))), seed) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("MONSTER_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAMailOrderMonsters-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("REX", 9200, 4);
        g.HighScores.Insert("AVA", 3100, 2);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 600 && g.State != GameState.HighScores; i++) g.Idle(1);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void The_catalogue()
    {
        var (w, c, g) = Open();
        g.StartGame();
        g.BuyMonster(Species.Hydroid);
        g.BuyOrSell(Item.Laser);
        for (int i = 0; i < 9; i++) { g.Tick(new Controls(0, 1)); g.Tick(default); }
        g.Run(200);
        c.Advance(0.5);
        Shot(w, c, "catalogue");
    }

    /// <summary>A bout between two computer handlers, until the moment's right.</summary>
    private static void Bout(string name, Species mine, Item[] items, int league, int bout, Func<GameEngine, bool> moment, int seed = 11)
    {
        var (w, c, g) = Open(seed);
        g.StartGame();
        g.SetCredits(5000);
        g.SetLeague(league, bout);
        g.BuyMonster(mine);
        foreach (var i in items) g.BuyOrSell(i);
        g.Choose(GameEngine.ShopRows - 1);
        var brain = new Brain(0, 0.9);
        for (int t = 0; t < 60 * 120 && !moment(g); t++)
        {
            g.Tick(g.Bout != null && g.Phase == Phase.Fight ? brain.Think(g.Bout) : default);
            if (t % 3 == 0) c.Advance(0.05);
        }
        Assert.True(moment(g), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void The_introductions() => Bout("intro", Species.Stompasaur, [Item.Flame, Item.Armour], 1, 0, g => g.Phase == Phase.Intro && g.PhaseTime > 1);

    [AvaloniaFact]
    public void Flame_breath() => Bout("flame", Species.Trollock, [Item.Flame, Item.Armour], 0, 0, g => g.Bout is { } b && b.Monsters[0].Flaming && Sim.Dist(b.Monsters[0], b.Monsters[1]) < 180);

    [AvaloniaFact]
    public void Laser_eyes() => Bout("laser", Species.Mothra, [Item.Laser, Item.Turbo], 2, 0, g => g.Bout is { } b && b.Monsters.Any(m => m.Beam > 0.06) && b.Clock > 3);

    [AvaloniaFact]
    public void Rockets() => Bout("rockets", Species.Megasaur, [Item.Rockets, Item.Laser, Item.Armour], 3, 0, g => g.Bout is { } b && b.Shots.Any(s => s.Kind == Item.Rockets) && b.Puffs.Count > 0);

    [AvaloniaFact]
    public void Destructo() => Bout("destructo", Species.Stompasaur, [Item.Horns, Item.Flame], 1, 1, g => g.Bout is { } b && b.Things.Count(t => t.Kind == Prop.Building && t.Down) >= 3);

    [AvaloniaFact]
    public void The_result() => Bout("result", Species.Megasaur, [Item.Flame, Item.Laser, Item.Armour], 0, 0, g => g.Phase == Phase.Result && g.PhaseTime > 0.5);

    [AvaloniaFact]
    public void Champion()
    {
        var (w, c, g) = Open();
        g.StartGame();
        g.SetCredits(9000);
        g.SetLeague(3, 2);
        g.BuyMonster(Species.Megasaur);
        foreach (var i in new[] { Item.Flame, Item.Laser, Item.Armour, Item.Regenerator }) g.BuyOrSell(i);
        g.Fight();
        var brain = new Brain(0, 0.95);
        for (int t = 0; t < 60 * 200 && g.State == GameState.Playing; t++) g.Tick(g.Bout != null ? brain.Think(g.Bout) : default);
        for (int t = 0; t < 60 * 2; t++) { g.Tick(default); if (t % 3 == 0) c.Advance(0.05); }
        Shot(w, c, "end");
    }
}
