namespace AVAMailOrderMonsters.Engine;

/// <summary>
/// A bout, step by step: two monsters walking (or flying) about the arena among its rocks, trees and
/// buildings; claws, horns, flame, lasers, rockets and acid; armour, turbo legs and regenerators; and how a
/// bout is won.
/// </summary>
public static class Sim
{
    private const double Dt = Field.Step, W = Field.ArenaW, H = Field.ArenaH;
    public const double AimAssist = 0.6, ChargeTime = 0.45, ChargeSpeed = 2.7, FlameCone = 0.45;
    public const int BuildingPoints = 1;

    public static Bout Start(Mode mode, Loadout a, Loadout b, int seed)
    {
        var bout = new Bout { Mode = mode, Dice = new Dice(seed), Seconds = mode == Mode.Duel ? Bout.DuelSeconds : Bout.DestructoSeconds };
        var d = bout.Dice;
        bool Clear(double x, double y, double r) =>
            x > r + 20 && y > r + 20 && x < W - r - 20 && y < H - r - 20
            && Math.Abs(x - 110) + Math.Abs(y - H / 2) > 180 && Math.Abs(x - (W - 110)) + Math.Abs(y - H / 2) > 180
            && bout.Things.All(t => Math.Sqrt((t.X - x) * (t.X - x) + (t.Y - y) * (t.Y - y)) > t.R + r + 40);
        // Buildings (many more for the wrecking), rocks and trees, mirrored so neither side has the better ground.
        int buildings = mode == Mode.Destructo ? 7 : 3, rocks = 3, trees = 5;
        void Pair(Prop kind, double r, double life)
        {
            for (int tries = 0; tries < 200; tries++)
            {
                double x = 80 + d.Next() * (W / 2 - 80), y = 60 + d.Next() * (H - 120);
                if (!Clear(x, y, r) || !Clear(W - x, H - y, r)) continue;
                bout.Things.Add(new Thing { Kind = kind, X = x, Y = y, R = r, Life = life, MaxLife = life });
                bout.Things.Add(new Thing { Kind = kind, X = W - x, Y = H - y, R = r, Life = life, MaxLife = life });
                return;
            }
        }
        for (int i = 0; i < buildings; i++) Pair(Prop.Building, 30, 40);
        for (int i = 0; i < rocks; i++) Pair(Prop.Rock, 26 + d.Next() * 12, 0);
        for (int i = 0; i < trees; i++) Pair(Prop.Tree, 16, 18);
        bout.Dice = d;
        bout.Monsters =
        [
            Make(a, 0, 110, H / 2, 0),
            Make(b, 1, W - 110, H / 2, Math.PI),
        ];
        return bout;
    }

    private static Monster Make(Loadout l, int side, double x, double y, double facing) => new()
    {
        Load = l, Side = side, X = x, Y = y, Facing = facing, Life = l.MaxLife, MaxLife = l.MaxLife,
    };

    public static double Wrap(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a < -Math.PI) a += 2 * Math.PI;
        return a;
    }

    public static void Step(Bout b, Controls c0, Controls c1)
    {
        if (b.Over) return;
        b.Clock += Dt;
        foreach (var p in b.Puffs) p.T += Dt;
        b.Puffs.RemoveAll(p => p.T > 0.6);
        Act(b, b.Monsters[0], c0);
        Act(b, b.Monsters[1], c1);
        Shots(b);
        Separate(b);
        Judge(b);
    }

    private static void Act(Bout b, Monster m, Controls c)
    {
        if (m.Dead) return;
        var foe = b.Monsters[1 - m.Side];
        foreach (int i in Enumerable.Range(0, m.Cool.Length)) m.Cool[i] = Math.Max(0, m.Cool[i] - Dt);
        m.Hurt = Math.Max(0, m.Hurt - Dt);
        m.Slowed = Math.Max(0, m.Slowed - Dt);
        m.Beam = Math.Max(0, m.Beam - Dt);
        if (m.Load.Has(Item.Regenerator)) m.Life = Math.Min(m.MaxLife, m.Life + 2.5 * Dt);
        if (c.Swap && !m.SwapPrev)
        {
            m.Weapon = (m.Weapon + 1) % m.Load.Weapons.Count();
            b.Heard.Add(Sfx.Swap);
        }
        m.SwapPrev = c.Swap;
        // On the charge: straight ahead, fast, until it hits something or runs out.
        if (m.Charging > 0)
        {
            m.Charging -= Dt;
            if (!Move(b, m, Math.Cos(m.Facing) * m.Speed * ChargeSpeed * Dt, Math.Sin(m.Facing) * m.Speed * ChargeSpeed * Dt)) m.Charging = 0;
            if (Dist(m, foe) < m.R + foe.R + 6 && !foe.Dead)
            {
                Hit(b, foe, Catalog.Items[Item.Horns].Damage, m);
                double a = Math.Atan2(foe.Y - m.Y, foe.X - m.X);
                Move(b, foe, Math.Cos(a) * 50, Math.Sin(a) * 50);
                m.Charging = 0;
            }
            foreach (var t in b.Things.Where(t => t.Kind != Prop.Rock && !t.Down && Math.Sqrt((t.X - m.X) * (t.X - m.X) + (t.Y - m.Y) * (t.Y - m.Y)) < t.R + m.R + 6))
            {
                Wreck(b, t, Catalog.Items[Item.Horns].Damage, m);
                m.Charging = 0;
            }
            return;
        }
        m.Flaming = false;
        if (c.Dx != 0 || c.Dy != 0)
        {
            double len = Math.Sqrt(c.Dx * c.Dx + c.Dy * c.Dy);
            double want = Math.Atan2(c.Dy, c.Dx);
            m.Facing = Wrap(m.Facing + Math.Clamp(Wrap(want - m.Facing), -9 * Dt, 9 * Dt));
            Move(b, m, c.Dx / len * m.Speed * Dt, c.Dy / len * m.Speed * Dt);
            m.Walk += Dt;
        }
        if (c.Fire) Fire(b, m, foe);
    }

    public static double Dist(Monster a, Monster b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>A step, stopped by the arena's edge, and (for a walker) by rocks and standing buildings. Whether it moved freely.</summary>
    private static bool Move(Bout b, Monster m, double dx, double dy)
    {
        double nx = Math.Clamp(m.X + dx, m.R, W - m.R), ny = Math.Clamp(m.Y + dy, m.R, H - m.R);
        bool free = nx == m.X + dx && ny == m.Y + dy;
        if (!m.Load.Def.Flies)
            foreach (var t in b.Things)
            {
                if (t.Down || t.Kind == Prop.Tree) continue;
                double tx = nx - t.X, ty = ny - t.Y, d = Math.Sqrt(tx * tx + ty * ty), min = t.R + m.R;
                if (d >= min) continue;
                if (d < 0.01) { tx = 1; d = 1; }
                nx = t.X + tx / d * min;
                ny = t.Y + ty / d * min;
                free = false;
            }
        m.X = nx;
        m.Y = ny;
        return free;
    }

    /// <summary>Which way a weapon goes off: at the foe if it's near enough the way the monster faces, else straight on.</summary>
    public static double Aim(Monster m, Monster foe)
    {
        double a = Math.Atan2(foe.Y - m.Y, foe.X - m.X);
        return Math.Abs(Wrap(a - m.Facing)) < AimAssist && !foe.Dead ? a : m.Facing;
    }

    private static void Fire(Bout b, Monster m, Monster foe)
    {
        var item = m.InHand;
        var def = Catalog.Items[item];
        if (m.Cool[(int)item] > 0) return;
        if (item == Item.Rockets && m.Rockets <= 0) { b.Heard.Add(Sfx.Empty); m.Cool[(int)item] = 0.4; return; }
        m.Cool[(int)item] = def.Reload;
        double aim = Aim(m, foe);
        switch (item)
        {
            case Item.Claws:
            {
                b.Heard.Add(Sfx.Claw);
                double reach = m.R + def.Range;
                if (!foe.Dead && Dist(m, foe) < reach + foe.R && Math.Abs(Wrap(Math.Atan2(foe.Y - m.Y, foe.X - m.X) - m.Facing)) < 1.1) Hit(b, foe, def.Damage, m);
                foreach (var t in b.Things.Where(t => t.Kind != Prop.Rock && !t.Down))
                    if (Math.Sqrt((t.X - m.X) * (t.X - m.X) + (t.Y - m.Y) * (t.Y - m.Y)) < reach + t.R && Math.Abs(Wrap(Math.Atan2(t.Y - m.Y, t.X - m.X) - m.Facing)) < 1.1)
                        Wreck(b, t, def.Damage, m);
                break;
            }
            case Item.Horns:
                m.Facing = aim;
                m.Charging = ChargeTime;
                b.Heard.Add(Sfx.Charge);
                break;
            case Item.Flame:
            {
                m.Flaming = true;
                if (b.Clock % 0.3 < Dt) b.Heard.Add(Sfx.Flame);
                bool Burns(double x, double y, double r)
                {
                    double d = Math.Sqrt((x - m.X) * (x - m.X) + (y - m.Y) * (y - m.Y));
                    return d < m.R + def.Range + r && Math.Abs(Wrap(Math.Atan2(y - m.Y, x - m.X) - m.Facing)) < FlameCone + r / Math.Max(d, 1);
                }
                if (!foe.Dead && Burns(foe.X, foe.Y, foe.R)) Hit(b, foe, def.Damage, m, quiet: true);
                foreach (var t in b.Things.Where(t => t.Kind != Prop.Rock && !t.Down))
                    if (Burns(t.X, t.Y, t.R)) Wreck(b, t, def.Damage * (t.Kind == Prop.Tree ? 2 : 1), m, quiet: true);
                break;
            }
            case Item.Laser:
            {
                // Straight along the aim to the first thing in the way.
                b.Heard.Add(Sfx.Laser);
                double ux = Math.Cos(aim), uy = Math.Sin(aim);
                double end = def.Range;
                Thing? blocker = null;
                foreach (var t in b.Things.Where(t => !t.Down))
                {
                    double along = (t.X - m.X) * ux + (t.Y - m.Y) * uy, off = Math.Abs((t.X - m.X) * uy - (t.Y - m.Y) * ux);
                    if (along > 0 && along < end && off < t.R) { end = along; blocker = t; }
                }
                double fAlong = (foe.X - m.X) * ux + (foe.Y - m.Y) * uy, fOff = Math.Abs((foe.X - m.X) * uy - (foe.Y - m.Y) * ux);
                if (!foe.Dead && fAlong > 0 && fAlong < end && fOff < foe.R) { end = fAlong; blocker = null; Hit(b, foe, def.Damage, m); }
                else if (blocker != null) Wreck(b, blocker, def.Damage, m);
                m.Beam = 0.12;
                m.BeamLength = end;
                m.BeamAngle = aim;
                break;
            }
            case Item.Rockets:
            case Item.Acid:
            {
                double v = item == Item.Rockets ? 460 : 380;
                if (item == Item.Rockets) m.Rockets--;
                b.Shots.Add(new Shot { Owner = m.Side, Kind = item, X = m.X + Math.Cos(aim) * (m.R + 6), Y = m.Y + Math.Sin(aim) * (m.R + 6), Vx = Math.Cos(aim) * v, Vy = Math.Sin(aim) * v, Life = def.Range / v });
                b.Heard.Add(item == Item.Rockets ? Sfx.Rocket : Sfx.Spit);
                break;
            }
        }
    }

    private static void Shots(Bout b)
    {
        foreach (var s in b.Shots)
        {
            s.X += s.Vx * Dt;
            s.Y += s.Vy * Dt;
            s.Life -= Dt;
            var foe = b.Monsters[1 - s.Owner];
            bool hit = s.Life <= 0 || s.X < 0 || s.Y < 0 || s.X > W || s.Y > H;
            if (!hit && !foe.Dead && Math.Sqrt((foe.X - s.X) * (foe.X - s.X) + (foe.Y - s.Y) * (foe.Y - s.Y)) < foe.R + 5) hit = true;
            if (!hit) hit = b.Things.Any(t => !t.Down && Math.Sqrt((t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y)) < t.R + 4);
            if (!hit) continue;
            s.Life = -1;
            var owner = b.Monsters[s.Owner];
            var def = Catalog.Items[s.Kind];
            if (s.Kind == Item.Rockets)
            {
                // A burst: everything near it.
                const double blast = 48;
                b.Puffs.Add(new Puff(s.X, s.Y, blast, 0xFFFF9030));
                b.Heard.Add(Sfx.Boom);
                if (!foe.Dead && Math.Sqrt((foe.X - s.X) * (foe.X - s.X) + (foe.Y - s.Y) * (foe.Y - s.Y)) < blast + foe.R) Hit(b, foe, def.Damage, owner);
                foreach (var t in b.Things.Where(t => t.Kind != Prop.Rock && !t.Down && Math.Sqrt((t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y)) < blast + t.R))
                    Wreck(b, t, def.Damage, owner);
            }
            else
            {
                b.Puffs.Add(new Puff(s.X, s.Y, 16, 0xFF90F040));
                if (!foe.Dead && Math.Sqrt((foe.X - s.X) * (foe.X - s.X) + (foe.Y - s.Y) * (foe.Y - s.Y)) < foe.R + 6)
                {
                    Hit(b, foe, def.Damage, owner);
                    foe.Slowed = 2;
                }
                else if (b.Things.FirstOrDefault(t => t.Kind != Prop.Rock && !t.Down && Math.Sqrt((t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y)) < t.R + 6) is { } t)
                    Wreck(b, t, def.Damage, owner);
            }
        }
        b.Shots.RemoveAll(s => s.Life < 0);
    }

    public static void Hit(Bout b, Monster m, double damage, Monster by, bool quiet = false)
    {
        m.Life -= damage * (1 - m.Armour);
        m.Hurt = 0.2;
        if (!quiet || b.Clock % 0.25 < Dt) b.Heard.Add(Sfx.Hit);
    }

    private static void Wreck(Bout b, Thing t, double damage, Monster by, bool quiet = false)
    {
        t.Life -= damage;
        if (!t.Down) { if (!quiet) b.Heard.Add(Sfx.Thud); return; }
        b.Puffs.Add(new Puff(t.X, t.Y, t.R + 20, t.Kind == Prop.Building ? 0xFFB0A090 : 0xFF60A040));
        b.Heard.Add(t.Kind == Prop.Building ? Sfx.Crumble : Sfx.Timber);
        if (t.Kind == Prop.Building) by.Score += BuildingPoints;
    }

    /// <summary>Two monsters can't stand in the same place: each is pushed off the other.</summary>
    private static void Separate(Bout b)
    {
        var (a, c) = (b.Monsters[0], b.Monsters[1]);
        if (a.Dead || c.Dead) return;
        double d = Dist(a, c), min = a.R + c.R;
        if (d >= min || d < 0.01) return;
        double push = (min - d) / 2, ux = (c.X - a.X) / d, uy = (c.Y - a.Y) / d;
        Move(b, a, -ux * push, -uy * push);
        Move(b, c, ux * push, uy * push);
    }

    private static void Judge(Bout b)
    {
        var (a, c) = (b.Monsters[0], b.Monsters[1]);
        if (a.Dead || c.Dead)
        {
            b.Winner = a.Dead && c.Dead ? 2 : a.Dead ? 1 : 0;
            b.How = b.Winner == 2 ? "BOTH KNOCKED OUT" : "A KNOCKOUT";
            b.Heard.Add(Sfx.Knockout);
            return;
        }
        if (b.Mode == Mode.Destructo && b.Things.Where(t => t.Kind == Prop.Building).All(t => t.Down) || b.Clock >= b.Seconds)
        {
            if (b.Mode == Mode.Destructo)
            {
                b.Winner = a.Score > c.Score ? 0 : c.Score > a.Score ? 1 : a.Life / a.MaxLife >= c.Life / c.MaxLife ? 0 : 1;
                b.How = $"BUILDINGS {a.Score} TO {c.Score}";
            }
            else
            {
                b.Winner = a.Life / a.MaxLife >= c.Life / c.MaxLife ? 0 : 1;
                b.How = "ON POINTS";
            }
            b.Heard.Add(Sfx.Bell);
        }
    }
}
