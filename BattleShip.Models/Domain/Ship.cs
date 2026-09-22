namespace BattleShip.Models.Domain;

public sealed class Ship
{
    private readonly HashSet<Coordinate> _hits = [];

    public Ship(ShipKind kind, IReadOnlyList<Coordinate> cells)
    {
        Kind = kind;
        Cells = cells;
    }

    public ShipKind Kind { get; }
    public IReadOnlyList<Coordinate> Cells { get; }
    public bool IsSunk => _hits.Count == Cells.Count;

    public bool Occupies(Coordinate c) => Cells.Contains(c);

    public bool RegisterHit(Coordinate c) => Occupies(c) && _hits.Add(c);

    /// <summary>Vrai si <paramref name="c"/> est la dernière case non encore touchée de ce navire (le coup qui le couperait).</summary>
    public bool WouldSink(Coordinate c) => Occupies(c) && !_hits.Contains(c) && _hits.Count == Cells.Count - 1;
}
