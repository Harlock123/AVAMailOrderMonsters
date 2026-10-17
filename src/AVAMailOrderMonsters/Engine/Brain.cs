namespace AVAMailOrderMonsters.Engine;

/// <summary>
/// The computer's monster handler in the arena. It picks the weapon that suits the distance (and changes to
/// it), keeps the distance that weapon wants, circling a little so it's harder to hit, faces its target and
/// fires when it's lined up. In a wrecking bout it goes for the nearest building, turning to fight only when
/// the other monster comes close. It steers round rocks and buildings in its way.
/// </summary>
public sealed class Brain(int side, double skill = 0.7)
{
    public int Side { get; } = side;
    public double Skill = skill;
    private Dice _dice = new(side * 31 + 5);
    private double _circle = 1, _think;
    private bool _swapPrev;
    private Item _want = Item.Claws;
    private double _lastX, _lastY, _stuck, _unstick, _ux, _uy;

    public Controls Think(Bout b)
    {
        var m = b.Monsters[Side];
        var foe = b.Monsters[1 - Side];
        if (m.Dead || b.Over) return default;
        _think -= Field.Step;
        if (_think <= 0)
        {
            _think = 0.5 + (1 - Skill) * 0.6;
            if (_dice.Next() < 0.3) _circle = -_circle;
        }
        // What to go for.
        double tx = foe.X, ty = foe.Y, tr = foe.R;
        bool building = false;
        Thing? target = null;
        if (b.Mode == Mode.Destructo && Sim.Dist(m, foe) > 200)
        {
            var t = b.Things.Where(t => t.Kind == Prop.Building && !t.Down).OrderBy(t => (t.X - m.X) * (t.X - m.X) + (t.Y - m.Y) * (t.Y - m.Y)).FirstOrDefault();
            if (t != null) { (tx, ty, tr) = (t.X, t.Y, t.R); building = true; target = t; }
        }
        double dx = tx - m.X, dy = ty - m.Y, d = Math.Sqrt(dx * dx + dy * dy), gap = d - m.R - tr;
        // The weapon for this distance.
        _want = Choose(m, gap, building);
        if (m.InHand != _want)
        {
            bool press = !_swapPrev;
            _swapPrev = press;
            if (press) return new Controls(Swap: true);
        }
        _swapPrev = false;
        var def = Catalog.Items[_want];
        double keep = _want switch
        {
            Item.Claws => def.Range * 0.6,
            Item.Horns => 140,
            Item.Flame => def.Range * 0.55,
            Item.Laser => 260,
            Item.Rockets => 320,
            _ => 240,
        };
        if (building) keep = Math.Min(keep, _want is Item.Claws or Item.Flame ? keep : 160);
        double a = Math.Atan2(dy, dx);
        double off = Math.Abs(Sim.Wrap(a - m.Facing));
        // In range and lined up: fire.
        bool inRange = _want switch
        {
            Item.Claws => gap < def.Range,
            Item.Flame => gap < def.Range * 0.9,
            Item.Horns => gap < 260,
            _ => gap < def.Range * 0.9,
        };
        bool fire = inRange && off < (_want is Item.Claws or Item.Flame ? 0.6 : Sim.AimAssist * 0.8) && m.Cool[(int)_want] <= 0;
        if (_want == Item.Rockets && m.Rockets <= 0) fire = false;
        // Move: close in or back off to the distance wanted, with a sidestep; facing the target when it's time to fire.
        double mx, my;
        if (gap > keep + 20) { mx = Math.Cos(a); my = Math.Sin(a); }
        else if (gap < keep - 30 && !building) { mx = -Math.Cos(a) + Math.Cos(a + Math.PI / 2) * _circle; my = -Math.Sin(a) + Math.Sin(a + Math.PI / 2) * _circle; }
        else { mx = Math.Cos(a + Math.PI / 2) * _circle * 0.8 + Math.Cos(a) * 0.4; my = Math.Sin(a + Math.PI / 2) * _circle * 0.8 + Math.Sin(a) * 0.4; }
        if (off > 0.35 && inRange) { mx = Math.Cos(a); my = Math.Sin(a); }
        (mx, my) = Steer(b, m, mx, my, target);
        // Stuck against something (the eight ways a stick can push don't always fit the way round): try another way for a moment.
        bool trying = Math.Abs(mx) + Math.Abs(my) > 0.3;
        if (trying && Math.Abs(m.X - _lastX) + Math.Abs(m.Y - _lastY) < 0.5) _stuck += Field.Step; else _stuck = 0;
        (_lastX, _lastY) = (m.X, m.Y);
        if (_stuck > 0.4) { _unstick = 0.7; _stuck = 0; double r = _dice.Next() * Math.PI * 2; (_ux, _uy) = (Math.Cos(r), Math.Sin(r)); }
        if (_unstick > 0) { _unstick -= Field.Step; (mx, my) = (_ux, _uy); }
        int sx = Math.Abs(mx) > 0.38 ? Math.Sign(mx) : 0, sy = Math.Abs(my) > 0.38 ? Math.Sign(my) : 0;
        // A slower hand fires a little less often.
        if (fire && _dice.Next() > 0.55 + Skill * 0.45) fire = false;
        return new Controls(sx, sy, fire || _want == Item.Flame && inRange && off < 0.6);
    }

    private static Item Choose(Monster m, double gap, bool building)
    {
        var have = m.Load.Weapons.ToList();
        Item Best(params Item[] order) => order.FirstOrDefault(i => have.Contains(i) && (i != Item.Rockets || m.Rockets > 0), Item.Claws);
        if (building) return Best(Item.Flame, Item.Rockets, Item.Horns, Item.Laser, Item.Claws);
        if (gap < 45) return Best(Item.Flame, Item.Claws);
        if (gap < 150) return Best(Item.Flame, Item.Horns, Item.Laser, Item.Acid, Item.Rockets, Item.Claws);
        return Best(Item.Rockets, Item.Laser, Item.Acid, Item.Horns, Item.Flame, Item.Claws);
    }

    /// <summary>Round whatever's in the way: the first of a fan of headings that's clear for a little way ahead.</summary>
    private static (double, double) Steer(Bout b, Monster m, double mx, double my, Thing? target)
    {
        double len = Math.Sqrt(mx * mx + my * my);
        if (len < 0.01) return (0, 0);
        double a = Math.Atan2(my, mx);
        foreach (double turn in new[] { 0, 0.5, -0.5, 1.0, -1.0, 1.5, -1.5 })
        {
            double ux = Math.Cos(a + turn), uy = Math.Sin(a + turn);
            bool clear = true;
            for (double s = 20; s <= 70 && clear; s += 25)
            {
                double x = m.X + ux * s, y = m.Y + uy * s;
                if (x < m.R || y < m.R || x > Field.ArenaW - m.R || y > Field.ArenaH - m.R) clear = false;
                else if (!m.Load.Def.Flies)
                    clear = !b.Things.Any(t => !t.Down && t.Kind != Prop.Tree && t != target && Math.Sqrt((t.X - x) * (t.X - x) + (t.Y - y) * (t.Y - y)) < t.R + m.R);
            }
            if (clear) return (ux, uy);
        }
        return (mx / len, my / len);
    }
}

/// <summary>
/// The computer's shopper: with a budget, the monster and fittings it buys. It favours the toughest monster
/// that leaves room in the budget for weapons, then the weapons, then gear.
/// </summary>
public static class Shopper
{
    /// <summary>A whole new loadout for a budget.</summary>
    public static Loadout Buy(int budget, ref Dice d)
    {
        var options = Catalog.Monsters.Values.Where(s => s.Price <= budget * 0.62 || s.Species == Species.Gnasher).ToList();
        var dice = d;
        var pick = options.Select(s => (s, Score: s.Life * s.Speed * (1 + s.Armour) + dice.Next() * 6000)).OrderByDescending(x => x.Score).First().s;
        d = dice;
        var l = new Loadout { Species = pick.Species };
        Fit(l, budget - pick.Price, ref d);
        return l;
    }

    /// <summary>Fills a monster's free slots with what money allows: weapons first, then gear.</summary>
    public static int Fit(Loadout l, int money, ref Dice d)
    {
        Item[] weapons = [Item.Laser, Item.Rockets, Item.Flame, Item.Acid, Item.Horns];
        Item[] gear = [Item.Armour, Item.Regenerator, Item.Turbo];
        var dice = d;
        var order = weapons.Select(w => (w, k: dice.Next())).OrderBy(x => x.k).Select(x => x.w).Take(2)
            .Concat(gear.Select(g => (g, k: dice.Next())).OrderBy(x => x.k).Select(x => x.g)).Concat(weapons).Distinct().ToList();
        d = dice;
        foreach (var i in order)
        {
            if (l.FreeSlots <= 0) break;
            var def = Catalog.Items[i];
            if (l.Items.Contains(i) || def.Price > money) continue;
            l.Items.Add(i);
            money -= def.Price;
        }
        return money;
    }
}
