namespace BattleShip.Models.Domain;

public enum WeaponKind
{
    Torpedo,
    AirStrike
}

/// <summary>Bord de la grille d'où part une torpille.</summary>
public enum Edge
{
    Left,
    Right,
    Top,
    Bottom
}

public abstract record WeaponAction
{
    private WeaponAction()
    {
    }

    public abstract WeaponKind Kind { get; }

    /// <summary>Part de <paramref name="From"/> le long de la ligne (Left/Right) ou de la colonne (Top/Bottom) <paramref name="Lane"/>.</summary>
    public sealed record Torpedo(Edge From, int Lane) : WeaponAction
    {
        public override WeaponKind Kind => WeaponKind.Torpedo;
    }

    /// <summary><see cref="WeaponRules.AirStrikeLength"/> cases alignées à partir de <paramref name="Origin"/>, vers la droite ou vers le bas.</summary>
    public sealed record AirStrike(Coordinate Origin, Orientation Orientation) : WeaponAction
    {
        public override WeaponKind Kind => WeaponKind.AirStrike;
    }
}

public static class WeaponRules
{
    public const int TorpedoesPerGame = 1;
    public const int AirStrikesPerGame = 1;
    public const int AirStrikeLength = 3;

    /// <summary>
    /// Cases couvertes par l'arme, dans l'ordre de résolution (trajectoire de la torpille depuis son bord), ou
    /// <c>null</c> si l'arme sort de la grille (couloir inexistant, frappe qui déborde).
    /// </summary>
    public static IReadOnlyList<Coordinate>? Cells(WeaponAction action)
    {
        switch (action)
        {
            case WeaponAction.Torpedo torpedo:
                if (torpedo.Lane is < 0 or >= BoardGrid.Size)
                    return null;

                var steps = Enumerable.Range(0, BoardGrid.Size);
                return torpedo.From switch
                {
                    Edge.Left => steps.Select(i => new Coordinate(torpedo.Lane, i)).ToList(),
                    Edge.Right => steps.Select(i => new Coordinate(torpedo.Lane, BoardGrid.Size - 1 - i)).ToList(),
                    Edge.Top => steps.Select(i => new Coordinate(i, torpedo.Lane)).ToList(),
                    Edge.Bottom => steps.Select(i => new Coordinate(BoardGrid.Size - 1 - i, torpedo.Lane)).ToList(),
                    _ => null
                };

            case WeaponAction.AirStrike strike:
                var cells = Enumerable.Range(0, AirStrikeLength)
                    .Select(i => strike.Orientation == Orientation.Horizontal
                        ? strike.Origin with { Column = strike.Origin.Column + i }
                        : strike.Origin with { Row = strike.Origin.Row + i })
                    .ToList();
                return cells.All(BoardGrid.Contains) ? cells : null;

            default:
                return null;
        }
    }
}

/// <summary>Munitions restantes d'un joueur. Mutable, donc uniquement modifiée par Game sous son verrou.</summary>
public sealed class Arsenal
{
    public int Torpedoes { get; private set; } = WeaponRules.TorpedoesPerGame;
    public int AirStrikes { get; private set; } = WeaponRules.AirStrikesPerGame;

    public bool Has(WeaponKind kind) => (kind == WeaponKind.Torpedo ? Torpedoes : AirStrikes) > 0;

    internal void Consume(WeaponKind kind)
    {
        if (!Has(kind))
            throw new InvalidOperationException($"Plus de munition pour {kind}.");

        if (kind == WeaponKind.Torpedo)
            Torpedoes--;
        else
            AirStrikes--;
    }
}
