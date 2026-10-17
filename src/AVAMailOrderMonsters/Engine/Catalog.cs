namespace AVAMailOrderMonsters.Engine;

/// <summary>
/// A monster's controls in the arena: eight ways to go (up the screen is -1), fire the weapon in hand, and
/// change to the next weapon. In the shop the same controls move the cursor, choose, and go back.
/// </summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Fire = false, bool Swap = false);

/// <summary>The screen and the arena.</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    /// <summary>The arena: this big, with its top-left corner here on the screen.</summary>
    public const double ArenaW = 1000, ArenaH = 620, ArenaX = 12, ArenaY = 92;
}

public enum Species { Gnasher, Hydroid, Mothra, Trollock, Stompasaur, Megasaur }

public enum Item
{
    // Weapons.
    Claws, Horns, Flame, Laser, Rockets, Acid,
    // Gear.
    Armour, Turbo, Regenerator,
}

/// <summary>A monster in the catalogue: its life, speed, size (and how many things it can carry), and price.</summary>
public sealed record SpeciesDef(Species Species, string Name, int Life, double Speed, double Radius, int Slots, int Price, bool Flies, double Armour, string About);

/// <summary>A weapon or a piece of gear: what it costs, and (a weapon) how it hurts.</summary>
public sealed record ItemDef(Item Item, string Name, int Price, bool Weapon, double Damage, double Reload, double Range, string About);

public static class Catalog
{
    public static readonly IReadOnlyDictionary<Species, SpeciesDef> Monsters = new Dictionary<Species, SpeciesDef>
    {
        [Species.Gnasher] = new(Species.Gnasher, "Gnasher", 80, 190, 22, 2, 300, false, 0, "A small, quick lizard with a big appetite."),
        [Species.Hydroid] = new(Species.Hydroid, "Hydroid", 110, 155, 26, 3, 450, false, 0, "A sea beast that came ashore. Tentacles everywhere."),
        [Species.Mothra] = new(Species.Mothra, "Mothmoth", 75, 235, 24, 3, 550, true, 0, "A giant moth: fast, frail, and flies over everything."),
        [Species.Trollock] = new(Species.Trollock, "Trollock", 140, 125, 28, 3, 600, false, 0.15, "A troll of living rock. Thick-skinned."),
        [Species.Stompasaur] = new(Species.Stompasaur, "Stompasaur", 165, 115, 32, 4, 750, false, 0.05, "A big, bad dinosaur. Flattens buildings."),
        [Species.Megasaur] = new(Species.Megasaur, "Megasaur", 220, 95, 36, 4, 1050, false, 0.1, "The biggest thing in the catalogue. Slow."),
    };

    public static readonly IReadOnlyDictionary<Item, ItemDef> Items = new Dictionary<Item, ItemDef>
    {
        [Item.Claws] = new(Item.Claws, "Claws", 0, true, 13, 0.55, 30, "Free with every monster: tooth and claw, up close."),
        [Item.Horns] = new(Item.Horns, "Horns", 150, true, 24, 2.6, 0, "A headlong charge: a burst of speed, and a butt."),
        [Item.Flame] = new(Item.Flame, "Flame Breath", 280, true, 2.2, 0.09, 150, "A cone of fire in front. Burns while it's held."),
        [Item.Laser] = new(Item.Laser, "Laser Eyes", 320, true, 9, 0.8, 420, "A beam straight ahead, at once."),
        [Item.Rockets] = new(Item.Rockets, "Rockets", 360, true, 20, 1.5, 700, "A rocket that bursts where it hits. Twelve a bout."),
        [Item.Acid] = new(Item.Acid, "Acid Spit", 200, true, 10, 1.0, 360, "A gob of acid that slows what it hits."),
        [Item.Armour] = new(Item.Armour, "Armour Plates", 250, false, 0, 0, 0, "Takes a quarter off every blow."),
        [Item.Turbo] = new(Item.Turbo, "Turbo Legs", 220, false, 0, 0, 0, "A third faster."),
        [Item.Regenerator] = new(Item.Regenerator, "Regenerator", 300, false, 0, 0, 0, "Heals while it fights."),
    };

    public const int RocketsPerBout = 12;
}

/// <summary>A monster as bought and fitted out: its species, what it carries, and its wounds between bouts.</summary>
public sealed class Loadout
{
    public Species Species;
    /// <summary>What it carries besides its claws, in the order bought (the first weapon is in hand first).</summary>
    public List<Item> Items = [];
    /// <summary>Bouts won: each one toughens it a little.</summary>
    public int Wins;

    public SpeciesDef Def => Catalog.Monsters[Species];
    public int MaxLife => Def.Life + Wins * 5;
    public bool Has(Item i) => i == Item.Claws || Items.Contains(i);
    public int FreeSlots => Def.Slots - Items.Count;
    public IEnumerable<Item> Weapons => new[] { Item.Claws }.Concat(Items.Where(i => Catalog.Items[i].Weapon));
    /// <summary>What it would fetch secondhand: half of everything paid.</summary>
    public int Value => (Def.Price + Items.Sum(i => Catalog.Items[i].Price)) / 2;
    public int Cost => Def.Price + Items.Sum(i => Catalog.Items[i].Price);

    public Loadout Copy() => new() { Species = Species, Items = [.. Items], Wins = Wins };
}

/// <summary>The leagues, from the backyard to the stars: what the other monsters have to spend, and the prize.</summary>
public static class Leagues
{
    public static readonly (string Name, int Budget, int Prize)[] All =
    [
        ("THE BACKYARD LEAGUE", 600, 400),
        ("THE CITY LEAGUE", 950, 600),
        ("THE PLANETARY LEAGUE", 1350, 850),
        ("THE GALACTIC CHAMPIONSHIP", 1700, 1300),
    ];

    public const int BoutsPerLeague = 3;
    public const int StartingCredits = 800;
}
