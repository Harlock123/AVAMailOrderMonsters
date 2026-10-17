namespace AVAMailOrderMonsters.Engine;

public enum GameState { Title, Attract, Shop, Playing, Paused, GameOver, EnterInitials, HighScores }

public enum Phase
{
    /// <summary>The two monsters shown off before the bell.</summary>
    Intro,
    Fight,
    /// <summary>Who won, the prize or the repair bill, then back to the shop.</summary>
    Result,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Claw, Charge, Flame, Laser, Rocket, Spit, Boom, Hit, Thud, Crumble, Timber, Swap, Empty, Knockout, Bell,
    Cursor, Buy, Sell, Buzz, Cash, Roar, Promoted,
    Music, MusicStop, TitleTune, GameOver, Victory,
}

/// <summary>
/// The whole game with no UI: the shop, the career through the leagues, and the bouts. Call
/// <see cref="Update"/> with the elapsed time and the controls; it runs in fixed 60 Hz steps.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 16;
    public const double AttractSeconds = 80;
    public const double HighScoreSeconds = 9;
    public const double IntroSeconds = 2.6, ResultSeconds = 3.8, GameOverSeconds = 4;
    public const int Licences0 = 3;
    /// <summary>The shop's rows: the monsters, the fittings, and the way to the arena.</summary>
    public static readonly int ShopRows = Catalog.Monsters.Count + Catalog.Items.Count + 1;

    private double _accumulator, _idle;
    private bool _startPrev, _tunePending = true, _music, _firePrev;
    private int _seed, _boutSeed;
    private Dice _dice;
    private (int Dx, int Dy) _stepPrev;
    private readonly Brain _foe = new(1), _mine = new(0);

    public GameEngine(HighScoreTable? table = null, int seed = 1)
    {
        HighScores = table ?? HighScoreTable.Load();
        _seed = seed;
        _dice = new Dice(seed);
        NewGame();
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Intro;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public bool Silent { get; set; }

    // The career.
    public int Credits { get; private set; }
    public Loadout? Monster { get; private set; }
    public int League { get; private set; }
    public int BoutInLeague { get; private set; }
    public int Licences { get; private set; }
    /// <summary>Prize money won: the score.</summary>
    public int Score { get; private set; }
    public bool Champion { get; private set; }
    public int ShopRow { get; private set; }
    public string? Message { get; private set; }
    public double MessageTime { get; private set; }

    // The bout.
    public Bout? Bout { get; private set; }
    public Loadout? Opponent { get; private set; }
    public Mode NextMode => BoutInLeague == 1 ? Mode.Destructo : Mode.Duel;
    public bool Won { get; private set; }
    public int Paid { get; private set; }

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    private void Say(string text, double seconds = 2.5)
    {
        Message = text;
        MessageTime = seconds;
    }

    public void Update(double dt, Controls c, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(c, start);
        }
    }

    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        bool fire = c.Fire && !_firePrev;
        _firePrev = c.Fire;
        if (MessageTime > 0 && (MessageTime -= dt) <= 0) Message = null;
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                _idle += dt;
                if (startPressed || fire && StateTime > 0.5) StartGame();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                if (startPressed) { StartGame(); break; }
                if (Bout == null) AutoShop();
                else RunBout(dt, _mine.Think(Bout));
                if (State == GameState.Attract && StateTime > AttractSeconds) { Music(false); ShowHighScores(-1); }
                break;
            case GameState.Shop:
                ShopControls(c, fire, startPressed);
                break;
            case GameState.Playing:
                RunBout(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > GameOverSeconds)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, League + 1);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    private void NewGame()
    {
        Credits = Leagues.StartingCredits;
        Monster = null;
        League = BoutInLeague = 0;
        Licences = Licences0;
        Score = 0;
        Champion = false;
        Bout = null;
        ShopRow = 0;
        _boutSeed = _seed * 1000;
    }

    public void StartGame()
    {
        NewGame();
        State = GameState.Shop;
        StateTime = 0;
        NewEntryIndex = -1;
        Say("WELCOME TO THE CATALOGUE - BUY A MONSTER", 3);
    }

    private void StartAttract()
    {
        _seed++;
        NewGame();
        State = GameState.Attract;
        StateTime = 0;
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Music(false);
        Message = null;
        NewGame();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Music(false);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        if (Phase == Phase.Fight) Music(true);
    }

    private void Music(bool on)
    {
        if (on == _music) return;
        _music = on;
        Play(on ? Sfx.Music : Sfx.MusicStop);
    }

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }

    // ───────────────────────────── the shop ─────────────────────────────

    /// <summary>What a shop row is: a monster, a fitting, or (the last) the way to the arena.</summary>
    public static (Species? Monster, Item? Item) RowAt(int row)
    {
        int n = Catalog.Monsters.Count;
        if (row < n) return ((Species)row, null);
        if (row < n + Catalog.Items.Count) return (null, (Item)(row - n));
        return (null, null);
    }

    private void ShopControls(Controls c, bool fire, bool start)
    {
        var step = (c.Dx, c.Dy);
        if (step != _stepPrev && step != (0, 0))
        {
            int n = Catalog.Monsters.Count;
            if (c.Dy != 0) ShopRow = (ShopRow + c.Dy + ShopRows) % ShopRows;
            else if (c.Dx > 0 && ShopRow < n) ShopRow = n + Math.Min(ShopRow, Catalog.Items.Count - 1);
            else if (c.Dx < 0 && ShopRow >= n) ShopRow = Math.Min(ShopRow - n, n - 1);
            Play(Sfx.Cursor);
        }
        _stepPrev = step;
        if (start) { ToArena(); return; }
        if (fire) Choose(ShopRow);
    }

    /// <summary>Fire on a shop row: buy (or sell back) that monster or fitting, or off to the arena.</summary>
    public void Choose(int row)
    {
        var (species, item) = RowAt(row);
        if (species is { } s) { BuyMonster(s); return; }
        if (item is { } i) { BuyOrSell(i); return; }
        ToArena();
    }

    public bool BuyMonster(Species s)
    {
        var def = Catalog.Monsters[s];
        int tradeIn = Monster?.Value ?? 0;
        if (Monster?.Species == s) { Say($"YOU HAVE A {def.Name.ToUpperInvariant()} ALREADY"); Play(Sfx.Buzz); return false; }
        if (Credits + tradeIn < def.Price) { Say("NOT ENOUGH CREDITS"); Play(Sfx.Buzz); return false; }
        // The old one traded in (at half what it cost); what it carried moves over while there's room.
        var kept = Monster?.Items ?? [];
        Credits += tradeIn - def.Price;
        var fresh = new Loadout { Species = s, Wins = Monster?.Wins ?? 0 };
        foreach (var i in kept)
            if (fresh.FreeSlots > 0) { fresh.Items.Add(i); Credits -= Catalog.Items[i].Price / 2; }
        Monster = fresh;
        Say(tradeIn > 0 ? $"TRADED IN FOR A {def.Name.ToUpperInvariant()}" : $"ONE {def.Name.ToUpperInvariant()}, BY RETURN OF POST");
        Play(Sfx.Buy);
        return true;
    }

    public bool BuyOrSell(Item i)
    {
        var def = Catalog.Items[i];
        if (Monster == null) { Say("BUY A MONSTER FIRST"); Play(Sfx.Buzz); return false; }
        if (i == Item.Claws) { Say("CLAWS COME FREE"); Play(Sfx.Buzz); return false; }
        if (Monster.Items.Contains(i))
        {
            Monster.Items.Remove(i);
            Credits += def.Price / 2;
            Say($"SOLD THE {def.Name.ToUpperInvariant()}");
            Play(Sfx.Sell);
            return true;
        }
        if (Monster.FreeSlots <= 0) { Say("NO ROOM - SELL SOMETHING"); Play(Sfx.Buzz); return false; }
        if (Credits < def.Price) { Say("NOT ENOUGH CREDITS"); Play(Sfx.Buzz); return false; }
        Monster.Items.Add(i);
        Credits -= def.Price;
        Say($"FITTED: {def.Name.ToUpperInvariant()}");
        Play(Sfx.Buy);
        return true;
    }

    /// <summary>The computer's shopping (the demonstration, and the tests): the best monster it can afford, filled up.</summary>
    public void AutoShop()
    {
        if (Monster == null || Monster.FreeSlots > 0 && Credits >= 200)
        {
            if (Monster == null)
            {
                var l = Shopper.Buy(Credits, ref _dice);
                Credits -= l.Cost;
                Monster = l;
            }
            else Credits = Shopper.Fit(Monster, Credits, ref _dice);
        }
        // An upgrade, when the money's there for a bigger monster and its fittings.
        else if (Monster.Def.Price < 1050 && Credits > 900)
        {
            var bigger = Catalog.Monsters.Values.Where(s => s.Price > Monster.Def.Price && s.Price <= Credits + Monster.Value - 250).OrderByDescending(s => s.Life).FirstOrDefault();
            if (bigger != null) BuyMonster(bigger.Species);
        }
        ToArena();
    }

    private void ToArena()
    {
        if (Monster == null) { Say("BUY A MONSTER FIRST"); Play(Sfx.Buzz); return; }
        var budget = Leagues.All[League].Budget + BoutInLeague * 120;
        Opponent = Shopper.Buy(budget, ref _dice);
        Opponent.Wins = League + BoutInLeague;
        Bout = Sim.Start(NextMode, Monster.Copy(), Opponent, ++_boutSeed);
        _foe.Skill = Math.Min(0.9, 0.4 + League * 0.12 + BoutInLeague * 0.03);
        if (State == GameState.Shop) State = GameState.Playing;
        StateTime = 0;
        SetPhase(Phase.Intro);
        Play(Sfx.Roar);
    }

    // ───────────────────────────── the bout ─────────────────────────────

    private void RunBout(double dt, Controls c)
    {
        var b = Bout!;
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Intro:
                if (PhaseTime > IntroSeconds) { SetPhase(Phase.Fight); Music(true); Play(Sfx.Bell); }
                break;
            case Phase.Fight:
                Sim.Step(b, c, _foe.Think(b));
                foreach (var s in b.Heard) Play(s);
                b.Heard.Clear();
                if (b.Over) Settle(b);
                break;
            case Phase.Result:
                if (PhaseTime > ResultSeconds) AfterBout();
                break;
        }
    }

    private void Settle(Bout b)
    {
        Music(false);
        SetPhase(Phase.Result);
        Won = b.Winner == 0;
        var prize = Leagues.All[League].Prize;
        if (Won)
        {
            Paid = prize;
            Credits += prize;
            Score += prize;
            Monster!.Wins++;
            Play(Sfx.Cash);
        }
        else
        {
            // The repair bill: a fifth of what the monster cost. Fittings are sold off to pay it if need be.
            int bill = Monster!.Cost / 5;
            Paid = bill;
            while (Credits < bill && Monster.Items.Count > 0)
            {
                var i = Monster.Items[^1];
                Monster.Items.RemoveAt(Monster.Items.Count - 1);
                Credits += Catalog.Items[i].Price / 2;
            }
            Credits -= bill;
            Licences--;
            Play(Sfx.GameOver);
        }
    }

    private void AfterBout()
    {
        Bout = null;
        if (Won)
        {
            BoutInLeague++;
            if (BoutInLeague >= Leagues.BoutsPerLeague)
            {
                BoutInLeague = 0;
                if (League == Leagues.All.Length - 1)
                {
                    Champion = true;
                    Play(Sfx.Victory);
                    End();
                    return;
                }
                League++;
                Play(Sfx.Promoted);
                Say($"PROMOTED TO {Leagues.All[League].Name}", 3.5);
            }
        }
        else if (Licences <= 0 || Credits < 0)
        {
            End();
            return;
        }
        if (State == GameState.Playing) State = GameState.Shop;
        StateTime = 0;
    }

    private void End()
    {
        Music(false);
        if (State == GameState.Attract) { ShowHighScores(-1); return; }
        State = GameState.GameOver;
        StateTime = 0;
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Tick(c);
    }

    public void Idle(int steps) => Run(steps);

    public void SetCredits(int n) => Credits = n;
    public void SetLeague(int league, int bout = 0) { League = league; BoutInLeague = bout; }
    public void SetLicences(int n) => Licences = n;
    public void SetScore(int n) => Score = n;

    /// <summary>For tests: into a bout with the shop's monster, straight past the introductions.</summary>
    public void Fight()
    {
        if (State is not (GameState.Shop or GameState.Playing)) StartGame();
        ToArena();
        SetPhase(Phase.Fight);
    }
}
