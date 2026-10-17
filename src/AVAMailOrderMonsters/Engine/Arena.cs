namespace AVAMailOrderMonsters.Engine;

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

/// <summary>What a bout is won by: knocking the other monster out, or knocking more buildings down.</summary>
public enum Mode { Duel, Destructo }

/// <summary>Something in the arena: a rock (stops walkers), a tree (burns, and falls), or a building (worth points).</summary>
public enum Prop { Rock, Tree, Building }

public sealed class Thing
{
    public Prop Kind;
    public double X, Y, R, Life;
    public double MaxLife;
    public bool Down => Kind != Prop.Rock && Life <= 0;
    public Thing Copy() => (Thing)MemberwiseClone();
}

/// <summary>A monster in the arena: where it is, which way it faces (radians), its life and what it's doing.</summary>
public sealed class Monster
{
    public int Side;
    public Loadout Load = null!;
    public double X, Y, Facing, Life, MaxLife, Hurt, Slowed, Charging, Regen;
    public int Weapon, Rockets = Catalog.RocketsPerBout, Score;
    public double[] Cool = new double[9];
    public bool SwapPrev, Flaming;
    public double Beam;
    public double BeamLength, BeamAngle;
    public double Walk;
    public bool Dead => Life <= 0;
    public Item InHand => Load.Weapons.ElementAt(Math.Min(Weapon, Load.Weapons.Count() - 1));
    public double Speed => Load.Def.Speed * (Load.Has(Item.Turbo) ? 1.33 : 1) * (Slowed > 0 ? 0.55 : 1);
    public double Armour => Load.Def.Armour + (Load.Has(Item.Armour) ? 0.25 : 0);
    public double R => Load.Def.Radius;
    public Monster Copy()
    {
        var m = (Monster)MemberwiseClone();
        m.Cool = (double[])Cool.Clone();
        return m;
    }
}

public sealed class Shot
{
    public int Owner;
    public Item Kind;
    public double X, Y, Vx, Vy, Life;
    public Shot Copy() => (Shot)MemberwiseClone();
}

public sealed record Puff(double X, double Y, double R, uint Colour)
{
    public double T;
}

/// <summary>A bout: the arena, the two monsters (the player's is 0), the shots flying, and the clock.</summary>
public sealed class Bout
{
    public const double DuelSeconds = 90, DestructoSeconds = 75;
    public Mode Mode;
    public List<Thing> Things = [];
    public Monster[] Monsters = [];
    public List<Shot> Shots = [];
    public List<Puff> Puffs = [];
    public List<Sfx> Heard = [];
    public double Clock, Seconds;
    public Dice Dice;
    /// <summary>The winner (0 or 1), -1 while it's on, 2 for a draw.</summary>
    public int Winner = -1;
    public string How = "";

    public Bout Copy() => new()
    {
        Mode = Mode, Things = Things.Select(t => t.Copy()).ToList(), Monsters = Monsters.Select(m => m.Copy()).ToArray(),
        Shots = Shots.Select(s => s.Copy()).ToList(), Clock = Clock, Seconds = Seconds, Dice = Dice, Winner = Winner, How = How,
    };

    public bool Over => Winner >= 0;
    public double Left => Math.Max(0, Seconds - Clock);
}
