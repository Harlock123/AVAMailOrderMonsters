using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAMailOrderMonsters.Engine;

namespace AVAMailOrderMonsters.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAMailOrderMonsters/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF8A9AC8, Cyan = 0xFF6AE8FF, Orange = 0xFFFF9A30;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);


    private bool InGame => Engine.State is GameState.Attract or GameState.Playing or GameState.Paused;

    private static readonly uint[] SideColours = [0xFF50C060, 0xFFE05040];

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF000000), new Rect(0, 0, Field.Width, Field.Height));
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            case GameState.GameOver: DrawGameOver(dc); break;
            case GameState.Shop: DrawShop(dc); break;
            default:
                if (g.Bout is { } b) DrawBout(dc, b);
                else DrawShop(dc);
                break;
        }
        if (g.Message is { } msg && g.State is GameState.Shop or GameState.Attract or GameState.Playing)
        {
            double wdt = Measure(msg, 30) + 40;
            dc.FillRectangle(Brush(0xE0101018), new Rect(512 - wdt / 2, 708, wdt, 44), 6);
            Text(dc, msg, new Point(512, 730), 30, Yellow, center: true);
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(512, 300), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(512, 360), 26, White, center: true);
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(512, 14), 22, Yellow, center: true);
    }

    private static double Measure(string text, double size) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(White)).Width;

    private static uint Shade(uint c, double k)
    {
        uint r = (uint)Math.Min(255, ((c >> 16) & 0xFF) * k), gg = (uint)Math.Min(255, ((c >> 8) & 0xFF) * k), b = (uint)Math.Min(255, (c & 0xFF) * k);
        return 0xFF000000 | r << 16 | gg << 8 | b;
    }

    // ───────────────────────────── the catalogue ─────────────────────────────

    private void DrawShop(DrawingContext dc)
    {
        var g = Engine;
        const uint paper = 0xFFF0E6CC, ink = 0xFF2A2018, red = 0xFFB02818, faint = 0xFF8A7A60;
        dc.FillRectangle(Brush(paper), new Rect(0, 0, Field.Width, Field.Height));
        for (int y = 96; y < 700; y += 26) dc.FillRectangle(Brush(0xFFE6DABC), new Rect(0, y, Field.Width, 1));
        Text(dc, "MAIL ORDER MONSTERS", new Point(24, 34), 52, red, shadow: false);
        Text(dc, "THE CATALOGUE", new Point(24, 72), 24, faint, shadow: false);
        var (lname, _, prize) = Leagues.All[g.League];
        Text(dc, $"CREDITS {g.Credits}", new Point(1000, 30), 36, ink, right: true, shadow: false);
        Text(dc, $"{lname}  -  BOUT {g.BoutInLeague + 1} OF {Leagues.BoutsPerLeague}: {(g.NextMode == Mode.Duel ? "DUEL" : "DESTRUCT-O")}  -  PRIZE {prize}", new Point(1000, 62), 20, faint, right: true, shadow: false);
        Text(dc, $"LICENCES {g.Licences}   WINNINGS {g.Score}", new Point(1000, 84), 20, faint, right: true, shadow: false);
        // The monsters.
        Text(dc, "MONSTERS", new Point(24, 116), 26, red, shadow: false);
        int n = Catalog.Monsters.Count;
        for (int i = 0; i < n; i++)
        {
            var s = Catalog.Monsters[(Species)i];
            double y = 140 + i * 62;
            bool on = g.ShopRow == i && g.State == GameState.Shop;
            if (on) dc.FillRectangle(Brush(0xFFFFE890), new Rect(16, y - 4, 480, 58), 4);
            bool mine = g.Monster?.Species == s.Species;
            DrawMonster(dc, s.Species, 52, y + 25, 0.8, -Math.PI / 2, mine ? SideColours[0] : 0xFF9A8A70, _clock);
            Text(dc, s.Name.ToUpperInvariant() + (mine ? "  (YOURS)" : ""), new Point(92, y + 12), 26, ink, shadow: false);
            Text(dc, $"LIFE {s.Life}  SPEED {s.Speed}  CARRIES {s.Slots}{(s.Flies ? "  FLIES" : "")}{(s.Armour > 0 ? "  TOUGH" : "")}", new Point(92, y + 36), 18, faint, shadow: false);
            Text(dc, $"{s.Price}", new Point(486, y + 14), 26, red, right: true, shadow: false);
        }
        // The fittings.
        Text(dc, "WEAPONS AND GEAR", new Point(524, 116), 26, red, shadow: false);
        for (int i = 0; i < Catalog.Items.Count; i++)
        {
            var it = Catalog.Items[(Item)i];
            double y = 140 + i * 41;
            bool on = g.ShopRow == n + i && g.State == GameState.Shop;
            if (on) dc.FillRectangle(Brush(0xFFFFE890), new Rect(516, y - 4, 492, 38), 4);
            bool owned = g.Monster?.Has(it.Item) == true;
            ItemIcon(dc, it.Item, 540, y + 15);
            Text(dc, it.Name.ToUpperInvariant() + (owned ? "  *" : ""), new Point(566, y + 14), 24, owned ? 0xFF2A6A2A : ink, shadow: false);
            Text(dc, it.Price == 0 ? "FREE" : owned ? $"SELL {it.Price / 2}" : $"{it.Price}", new Point(1000, y + 14), 22, owned ? 0xFF2A6A2A : red, right: true, shadow: false);
        }
        // What's highlighted, said.
        var (sp, item) = GameEngine.RowAt(g.ShopRow);
        string about = sp is { } x ? Catalog.Monsters[x].About : item is { } y2 ? Catalog.Items[y2].About : "Off to the arena for the next bout.";
        Text(dc, about, new Point(524, 528), 20, ink, shadow: false);
        // Yours.
        dc.FillRectangle(Brush(0xFFE6DABC), new Rect(16, 520, 480, 170), 6);
        if (g.Monster is { } m)
        {
            DrawMonster(dc, m.Species, 86, 600, 1.6, -Math.PI / 2, SideColours[0], _clock);
            Text(dc, $"YOUR {m.Def.Name.ToUpperInvariant()}", new Point(160, 548), 28, ink, shadow: false);
            Text(dc, $"LIFE {m.MaxLife}   WINS {m.Wins}   WORTH {m.Value}", new Point(160, 576), 20, faint, shadow: false);
            int k = 0;
            foreach (var it in new[] { Item.Claws }.Concat(m.Items))
            {
                ItemIcon(dc, it, 172 + k * 44, 612);
                k++;
            }
            for (int e = 0; e < m.FreeSlots; e++) dc.DrawRectangle(null, Pen(faint, 2), new Rect(152 + (k + e) * 44, 594, 36, 36), 4, 4);
        }
        else Text(dc, "NO MONSTER YET - CHOOSE ONE FROM THE LEFT", new Point(30, 600), 22, red, shadow: false);
        // The way to the arena.
        bool go = g.ShopRow == GameEngine.ShopRows - 1 && g.State == GameState.Shop;
        dc.FillRectangle(Brush(go ? red : 0xFF8A3020), new Rect(524, 600, 484, 80), 8);
        Text(dc, "TO THE ARENA!", new Point(766, 640), 48, go && (int)(_clock * 3) % 2 == 0 ? Yellow : paper, center: true, shadow: false);
        Text(dc, "ARROWS: CHOOSE   SPACE: BUY / SELL   ENTER: TO THE ARENA   ESC: TITLE", new Point(512, 756), 18, faint, center: true, shadow: false);
    }

    public static void ItemIcon(DrawingContext dc, Item i, double x, double y)
    {
        switch (i)
        {
            case Item.Claws:
                for (int k = -1; k <= 1; k++) Poly(dc, 0xFFE8E0D0, 0xFF5A5040, 1, new Point(x + k * 7 - 3, y + 8), new Point(x + k * 7 + 3, y + 8), new Point(x + k * 7 + 4, y - 10));
                break;
            case Item.Horns:
                Poly(dc, 0xFFE8D8B0, 0xFF6A5A30, 1, new Point(x - 12, y + 8), new Point(x - 4, y + 8), new Point(x - 10, y - 12));
                Poly(dc, 0xFFE8D8B0, 0xFF6A5A30, 1, new Point(x + 12, y + 8), new Point(x + 4, y + 8), new Point(x + 10, y - 12));
                break;
            case Item.Flame:
                Poly(dc, 0xFFFF7020, null, 0, new Point(x - 10, y + 10), new Point(x, y - 14), new Point(x + 10, y + 10));
                Poly(dc, 0xFFFFE040, null, 0, new Point(x - 5, y + 10), new Point(x, y - 4), new Point(x + 5, y + 10));
                break;
            case Item.Laser:
                dc.FillEllipse(x - 7, y, 6, 0xFFFF3030);
                dc.FillEllipse(x + 7, y, 6, 0xFFFF3030);
                dc.DrawLine(Pen(0xFFFF8080, 2), new Point(x - 7, y), new Point(x + 14, y - 12));
                break;
            case Item.Rockets:
                dc.FillRectangle(Brush(0xFF8A8A98), new Rect(x - 4, y - 12, 8, 20), 3);
                Poly(dc, 0xFFC03030, null, 0, new Point(x - 4, y - 12), new Point(x + 4, y - 12), new Point(x, y - 18));
                Poly(dc, 0xFFFF9030, null, 0, new Point(x - 3, y + 8), new Point(x + 3, y + 8), new Point(x, y + 15));
                break;
            case Item.Acid:
                dc.FillEllipse(x, y + 2, 9, 0xFF90F040);
                dc.FillEllipse(x - 3, y - 1, 3, 0xFFE0FFC0);
                break;
            case Item.Armour:
                Poly(dc, 0xFF8A94A8, 0xFF3A4458, 1.5, new Point(x - 11, y - 10), new Point(x + 11, y - 10), new Point(x + 9, y + 6), new Point(x, y + 13), new Point(x - 9, y + 6));
                break;
            case Item.Turbo:
                for (int k = 0; k < 3; k++) Poly(dc, 0xFF40A0E0, null, 0, new Point(x - 12 + k * 8, y - 9), new Point(x - 6 + k * 8, y), new Point(x - 12 + k * 8, y + 9));
                break;
            default:
                dc.FillRectangle(Brush(0xFFE03050), new Rect(x - 3, y - 11, 6, 22));
                dc.FillRectangle(Brush(0xFFE03050), new Rect(x - 11, y - 3, 22, 6));
                break;
        }
    }

    /// <summary>A monster seen from above, facing <paramref name="facing"/>: each species its own shape, coloured for its side.</summary>
    private static void DrawMonster(DrawingContext dc, Species s, double x, double y, double k, double facing, uint colour, double clock, bool hurt = false)
    {
        uint body = hurt ? 0xFFFFFFFF : colour, dark = Shade(body, 0.6), light = Shade(body, 1.3);
        using (dc.PushTransform(Matrix.CreateRotation(facing + Math.PI / 2) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(x, y)))
        {
            double step = Math.Sin(clock * 10) * 4;
            switch (s)
            {
                case Species.Gnasher:
                    Poly(dc, dark, null, 0, new Point(-4, 12), new Point(4, 12), new Point(step, 34));
                    foreach (int side in new[] { -1, 1 }) dc.FillEllipse(side * 13, 4 + side * step * 0.5, 5, dark);
                    dc.DrawEllipse(Brush(body), Pen(dark, 2), new Point(0, 4), 12, 16);
                    dc.DrawEllipse(Brush(light), Pen(dark, 2), new Point(0, -16), 9, 9);
                    dc.FillEllipse(-4, -19, 2, 0xFFFFF060);
                    dc.FillEllipse(4, -19, 2, 0xFFFFF060);
                    break;
                case Species.Hydroid:
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * Math.PI / 3 + Math.Sin(clock * 3 + i) * 0.2;
                        dc.DrawLine(Pen(dark, 6), new Point(0, 0), new Point(Math.Cos(a) * 28, Math.Sin(a) * 28 + 4));
                    }
                    dc.DrawEllipse(Brush(body), Pen(dark, 2), new Point(0, 0), 20, 20);
                    dc.FillEllipse(-7, -10, 5, 0xFFF0F0F0);
                    dc.FillEllipse(7, -10, 5, 0xFFF0F0F0);
                    dc.FillEllipse(-7, -11, 2.5, 0xFF101010);
                    dc.FillEllipse(7, -11, 2.5, 0xFF101010);
                    break;
                case Species.Mothra:
                {
                    double flap = 0.75 + Math.Abs(Math.Sin(clock * 12)) * 0.4;
                    foreach (int side in new[] { -1, 1 })
                    {
                        Poly(dc, light, dark, 2, new Point(0, -6), new Point(side * 30 * flap, -20), new Point(side * 34 * flap, 4), new Point(0, 4));
                        Poly(dc, body, dark, 2, new Point(0, 4), new Point(side * 26 * flap, 10), new Point(side * 16 * flap, 24), new Point(0, 12));
                        dc.FillEllipse(side * 18 * flap, -6, 4, dark);
                    }
                    dc.DrawEllipse(Brush(dark), null, new Point(0, 2), 5, 18);
                    dc.DrawLine(Pen(dark, 1.5), new Point(-2, -14), new Point(-8, -26));
                    dc.DrawLine(Pen(dark, 1.5), new Point(2, -14), new Point(8, -26));
                    break;
                }
                case Species.Trollock:
                    foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(Brush(dark), null, new Point(side * 24, -4 + side * step * 0.6), 9, 9);
                    Poly(dc, body, dark, 2, new Point(-20, -10), new Point(-8, -24), new Point(10, -22), new Point(22, -6), new Point(18, 16), new Point(-2, 24), new Point(-20, 14));
                    dc.FillEllipse(-6, -14, 3, 0xFFFFA020);
                    dc.FillEllipse(6, -14, 3, 0xFFFFA020);
                    dc.DrawLine(Pen(dark, 2), new Point(-12, 4), new Point(4, 0));
                    break;
                case Species.Stompasaur:
                    Poly(dc, dark, null, 0, new Point(-8, 16), new Point(8, 16), new Point(step * 1.5, 46));
                    foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(Brush(dark), null, new Point(side * 18, 14 + side * step), 8, 10);
                    dc.DrawEllipse(Brush(body), Pen(dark, 2), new Point(0, 4), 18, 22);
                    dc.DrawEllipse(Brush(light), Pen(dark, 2), new Point(0, -22), 12, 13);
                    dc.FillEllipse(-5, -27, 2.5, 0xFFFFF060);
                    dc.FillEllipse(5, -27, 2.5, 0xFFFFF060);
                    break;
                default:
                    Poly(dc, dark, null, 0, new Point(-10, 18), new Point(10, 18), new Point(step * 1.5, 54));
                    foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(Brush(dark), null, new Point(side * 22, 16 + side * step), 10, 12);
                    dc.DrawEllipse(Brush(body), Pen(dark, 2), new Point(0, 4), 22, 26);
                    for (int i = 0; i < 5; i++) Poly(dc, light, dark, 1, new Point(-5, -14 + i * 9), new Point(5, -14 + i * 9), new Point(0, -22 + i * 9));
                    dc.DrawEllipse(Brush(light), Pen(dark, 2), new Point(0, -26), 14, 14);
                    dc.FillEllipse(-6, -31, 3, 0xFFFF3030);
                    dc.FillEllipse(6, -31, 3, 0xFFFF3030);
                    break;
            }
        }
    }

    // ───────────────────────────── the arena ─────────────────────────────

    private void DrawBout(DrawingContext dc, Bout b)
    {
        var g = Engine;
        // The bar along the top.
        dc.FillRectangle(Brush(0xFF1A1A24), new Rect(0, 0, Field.Width, Field.ArenaY));
        for (int s = 0; s < 2; s++)
        {
            var m = b.Monsters[s];
            double x = s == 0 ? 16 : Field.Width - 16;
            string who = s == 0 ? (g.State == GameState.Attract ? "COMPUTER" : "YOU") : "THE CHALLENGER";
            Text(dc, $"{who}: {m.Load.Def.Name.ToUpperInvariant()}", new Point(x, 30), 26, SideColours[s], right: s == 1);
            double w = 360, bx = s == 0 ? x : x - w;
            dc.FillRectangle(Brush(0xFF3A1010), new Rect(bx, 48, w, 14), 3);
            double f = Math.Clamp(m.Life / m.MaxLife, 0, 1);
            dc.FillRectangle(Brush(SideColours[s]), new Rect(s == 0 ? bx : bx + w * (1 - f), 48, w * f, 14), 3);
            string hand = Catalog.Items[m.InHand].Name.ToUpperInvariant() + (m.InHand == Item.Rockets ? $" ({m.Rockets})" : "");
            Text(dc, hand + (b.Mode == Mode.Destructo ? $"   BUILDINGS {m.Score}" : ""), new Point(x, 76), 20, Dim, right: s == 1);
        }
        int secs = (int)Math.Ceiling(b.Left);
        Text(dc, $"{secs / 60}:{secs % 60:00}", new Point(512, 30), 40, secs < 10 ? Red : White, center: true);
        Text(dc, b.Mode == Mode.Duel ? "DUEL" : "DESTRUCT-O", new Point(512, 68), 22, Yellow, center: true);

        var area = new Rect(Field.ArenaX, Field.ArenaY, Field.ArenaW, Field.ArenaH);
        dc.FillRectangle(Brush(0xFFC8B078), area);
        using (dc.PushClip(area))
        using (dc.PushTransform(Matrix.CreateTranslation(Field.ArenaX, Field.ArenaY)))
        {
            // Sandy ground, scuffed.
            for (int i = 0; i < 90; i++)
            {
                uint h = Hash(i, 3, 7);
                dc.FillEllipse(h % (uint)Field.ArenaW, (h >> 12) % (uint)Field.ArenaH, 3 + (h >> 24) % 6, 0x30806040);
            }
            foreach (var t in b.Things) DrawThing(dc, t);
            foreach (var m in b.Monsters) DrawFire(dc, m);
            foreach (var m in b.Monsters.OrderBy(m => m.Load.Def.Flies))
            {
                if (m.Dead && g.Phase == Phase.Result && (int)(g.PhaseTime * 6) % 2 == 0) continue;
                if (m.Load.Def.Flies) dc.DrawEllipse(Brush(0x40000000), null, new Point(m.X + 10, m.Y + 16), m.R, m.R * 0.6);
                DrawMonster(dc, m.Load.Species, m.X, m.Y, m.R / 28, m.Facing, SideColours[m.Side], _clock + m.Side, m.Hurt > 0.1);
            }
            foreach (var s in b.Shots)
            {
                if (s.Kind == Item.Rockets)
                {
                    double a = Math.Atan2(s.Vy, s.Vx);
                    dc.DrawLine(Pen(0xFF8A8A98, 6), new Point(s.X, s.Y), new Point(s.X - Math.Cos(a) * 14, s.Y - Math.Sin(a) * 14));
                    dc.FillEllipse(s.X - Math.Cos(a) * 17, s.Y - Math.Sin(a) * 17, 4, (int)(_clock * 20) % 2 == 0 ? 0xFFFF9030 : 0xFFFFE060);
                }
                else dc.FillEllipse(s.X, s.Y, 6, 0xFF90F040);
            }
            foreach (var p in b.Puffs)
                using (dc.PushOpacity(Math.Clamp(1 - p.T * 1.7, 0, 1)))
                    dc.FillEllipse(p.X, p.Y, p.R * (0.4 + p.T * 1.2), p.Colour);
        }
        dc.DrawRectangle(null, Pen(0xFF5A4A30, 4), area.Inflate(2));
        if (g.Phase == Phase.Intro)
        {
            dc.FillRectangle(Brush(0xC0000000), area);
            var (a, c) = (b.Monsters[0], b.Monsters[1]);
            DrawMonster(dc, a.Load.Species, 280, 360, 2.4, 0, SideColours[0], _clock);
            DrawMonster(dc, c.Load.Species, 744, 360, 2.4, Math.PI, SideColours[1], _clock);
            Text(dc, "VS", new Point(512, 360), 80, Yellow, center: true);
            Text(dc, a.Load.Def.Name.ToUpperInvariant(), new Point(280, 470), 34, SideColours[0], center: true);
            Text(dc, c.Load.Def.Name.ToUpperInvariant(), new Point(744, 470), 34, SideColours[1], center: true);
            Text(dc, string.Join("  ", a.Load.Items.Select(i => Catalog.Items[i].Name.ToUpperInvariant())), new Point(280, 504), 18, White, center: true);
            Text(dc, string.Join("  ", c.Load.Items.Select(i => Catalog.Items[i].Name.ToUpperInvariant())), new Point(744, 504), 18, White, center: true);
            Text(dc, b.Mode == Mode.Duel ? "DUEL: KNOCK IT OUT" : "DESTRUCT-O: FLATTEN THE MOST BUILDINGS", new Point(512, 200), 34, Yellow, center: true);
        }
        else if (g.Phase == Phase.Result)
        {
            dc.FillRectangle(Brush(0xA0000000), new Rect(212, 280, 600, 180), 10);
            Text(dc, g.Won ? "YOU WIN!" : "YOU LOSE", new Point(512, 330), 64, g.Won ? Yellow : Red, center: true);
            Text(dc, b.How, new Point(512, 382), 26, White, center: true);
            Text(dc, g.Won ? $"PRIZE {g.Paid} CREDITS" : $"REPAIRS {g.Paid} CREDITS  -  LICENCES LEFT {g.Licences}", new Point(512, 420), 26, g.Won ? 0xFF60FF80 : Orange, center: true);
        }
        Text(dc, "ARROWS: MOVE   SPACE: FIRE   X: CHANGE WEAPON   P: PAUSE", new Point(512, 740), 18, Dim, center: true);
    }

    private void DrawThing(DrawingContext dc, Thing t)
    {
        switch (t.Kind)
        {
            case Prop.Rock:
                dc.DrawEllipse(Brush(0xFF7A7A80), Pen(0xFF4A4A50, 2), new Point(t.X, t.Y), t.R, t.R * 0.9);
                dc.FillEllipse(t.X - t.R * 0.3, t.Y - t.R * 0.3, t.R * 0.35, 0xFF9A9AA0);
                break;
            case Prop.Tree:
                if (t.Down) { dc.FillEllipse(t.X, t.Y, 6, 0xFF4A3420); return; }
                dc.FillEllipse(t.X + 5, t.Y + 6, t.R, 0x40000000);
                dc.FillEllipse(t.X, t.Y, t.R, t.Life < t.MaxLife ? 0xFF6A8A30 : 0xFF3A7A2A);
                dc.FillEllipse(t.X - 4, t.Y - 4, t.R * 0.5, 0xFF5A9A3A);
                break;
            default:
                if (t.Down)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        uint h = Hash((int)t.X, i, 4);
                        dc.FillRectangle(Brush(0xFF8A7A6A), new Rect(t.X - 26 + h % 46, t.Y - 22 + (h >> 8) % 40, 8 + (h >> 16) % 10, 6 + (h >> 20) % 8));
                    }
                    return;
                }
                dc.FillRectangle(Brush(0x40000000), new Rect(t.X - t.R + 6, t.Y - t.R + 8, t.R * 2, t.R * 2));
                dc.FillRectangle(Brush(0xFFB8B0A0), new Rect(t.X - t.R, t.Y - t.R, t.R * 2, t.R * 2));
                Poly(dc, 0xFFA04030, 0xFF603020, 1.5, new Point(t.X - t.R - 3, t.Y - t.R + 4), new Point(t.X, t.Y - t.R - 10), new Point(t.X + t.R + 3, t.Y - t.R + 4), new Point(t.X + t.R + 3, t.Y), new Point(t.X - t.R - 3, t.Y));
                for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    dc.FillRectangle(Brush(0xFF3A5A8A), new Rect(t.X - 18 + i * 22, t.Y + 2 + j * 12, 12, 8));
                if (t.Life < t.MaxLife)
                {
                    dc.FillRectangle(Brush(0xFF300808), new Rect(t.X - t.R, t.Y + t.R + 3, t.R * 2, 4));
                    dc.FillRectangle(Brush(0xFFE0A030), new Rect(t.X - t.R, t.Y + t.R + 3, t.R * 2 * t.Life / t.MaxLife, 4));
                }
                break;
        }
    }

    /// <summary>A monster's flame, beam or charge.</summary>
    private void DrawFire(DrawingContext dc, Monster m)
    {
        if (m.Flaming)
        {
            double reach = m.R + Catalog.Items[Item.Flame].Range;
            var pts = new List<Point> { new(m.X + Math.Cos(m.Facing) * m.R, m.Y + Math.Sin(m.Facing) * m.R) };
            for (int i = 0; i <= 6; i++)
            {
                double a = m.Facing - Sim.FlameCone + i * Sim.FlameCone / 3, r = reach * (0.85 + 0.15 * Math.Sin(_clock * 30 + i));
                pts.Add(new Point(m.X + Math.Cos(a) * r, m.Y + Math.Sin(a) * r));
            }
            using (dc.PushOpacity(0.75)) Poly(dc, 0xFFFF7020, null, 0, pts.ToArray());
            using (dc.PushOpacity(0.8)) Poly(dc, 0xFFFFE060, null, 0, pts[0], new Point(m.X + Math.Cos(m.Facing - 0.2) * reach * 0.6, m.Y + Math.Sin(m.Facing - 0.2) * reach * 0.6), new Point(m.X + Math.Cos(m.Facing + 0.2) * reach * 0.6, m.Y + Math.Sin(m.Facing + 0.2) * reach * 0.6));
        }
        if (m.Beam > 0)
        {
            var from = new Point(m.X + Math.Cos(m.BeamAngle) * m.R * 0.6, m.Y + Math.Sin(m.BeamAngle) * m.R * 0.6);
            var to = new Point(m.X + Math.Cos(m.BeamAngle) * m.BeamLength, m.Y + Math.Sin(m.BeamAngle) * m.BeamLength);
            dc.DrawLine(Pen(0x80FF2020, 10), from, to);
            dc.DrawLine(Pen(0xFFFFE0E0, 3), from, to);
        }
        if (m.Charging > 0)
            for (int i = 1; i <= 3; i++)
                dc.DrawLine(Pen(0x80FFFFFF, 3), new Point(m.X - Math.Cos(m.Facing) * (m.R + i * 12) + Math.Sin(m.Facing) * (i * 6 - 12), m.Y - Math.Sin(m.Facing) * (m.R + i * 12) - Math.Cos(m.Facing) * (i * 6 - 12)),
                    new Point(m.X - Math.Cos(m.Facing) * (m.R + i * 12 + 16) + Math.Sin(m.Facing) * (i * 6 - 12), m.Y - Math.Sin(m.Facing) * (m.R + i * 12 + 16) - Math.Cos(m.Facing) * (i * 6 - 12)));
    }

    // ───────────────────────────── the title and the end ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xFFF0E6CC), new Rect(0, 0, Field.Width, Field.Height));
        dc.FillRectangle(Brush(0xFFB02818), new Rect(0, 30, Field.Width, 150));
        Text(dc, "AVA MAIL ORDER MONSTERS", new Point(cx, 92), 74, 0xFFFFE890, center: true, shadow: false);
        Text(dc, "AFTER PAUL REICHE III, EVAN AND NICKY ROBINSON'S 1985 GAME", new Point(cx, 150), 22, 0xFFF0E6CC, center: true, shadow: false);
        var all = Enum.GetValues<Species>();
        for (int i = 0; i < all.Length; i++)
            DrawMonster(dc, all[i], 112 + i * 160, 300 + Math.Sin(_clock * 2 + i) * 8, 1.7, -Math.PI / 2, i % 2 == 0 ? SideColours[0] : SideColours[1], _clock + i);
        const uint ink = 0xFF2A2018;
        Text(dc, "ORDER A MONSTER FROM THE CATALOGUE, FIT IT OUT WITH CLAWS, HORNS, FLAME, LASERS, ROCKETS AND ACID,", new Point(cx, 430), 20, ink, center: true, shadow: false);
        Text(dc, "AND SEND IT INTO THE ARENA. WIN THE PRIZE MONEY, BUY BIGGER, AND CLIMB FROM THE BACKYARD LEAGUE", new Point(cx, 456), 20, ink, center: true, shadow: false);
        Text(dc, "TO THE GALACTIC CHAMPIONSHIP. LOSE, AND THERE'S A REPAIR BILL - AND ONLY THREE LICENCES.", new Point(cx, 482), 20, ink, center: true, shadow: false);
        Text(dc, "PRESS ENTER TO START", new Point(cx, 570), 44, (int)(_clock * 2) % 2 == 0 ? 0xFFB02818 : 0xFF2A2018, center: true, shadow: false);
        Text(dc, "ARROWS: MOVE   SPACE: FIRE / BUY   X: CHANGE WEAPON   P: PAUSE   M: SOUND", new Point(cx, 640), 20, 0xFF8A7A60, center: true, shadow: false);
        Text(dc, $"HIGH SCORE {Engine.HighScores.TopScore:000000}", new Point(cx, 690), 26, 0xFFB02818, center: true, shadow: false);
    }

    private void DrawGameOver(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, g.Champion ? "GALACTIC CHAMPION!" : "GAME OVER", new Point(cx, 220), g.Champion ? 84 : 110, g.Champion ? Yellow : Red, center: true);
        Text(dc, g.Champion ? "YOUR MONSTER IS THE BEST IN THE GALAXY" : g.Licences <= 0 ? "NO LICENCES LEFT" : "BANKRUPT", new Point(cx, 310), 34, White, center: true);
        if (g.Monster is { } m) DrawMonster(dc, m.Species, cx, 440, 2.4, -Math.PI / 2, SideColours[0], _clock);
        Text(dc, $"WINNINGS {g.Score}", new Point(cx, 580), 44, Yellow, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "THE GREAT MONSTER OWNERS", new Point(cx, 120), 64, 0xFFFFE040, center: true);
        Text(dc, "RANK NAME  WINNINGS  LEAGUE", new Point(cx, 190), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,6}", new Point(cx, 236 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 680), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillEllipse(this DrawingContext dc, double x, double y, double r, uint colour) =>
        dc.DrawEllipse(GameCanvas.Brush(colour), null, new Point(x, y), r, r);
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
