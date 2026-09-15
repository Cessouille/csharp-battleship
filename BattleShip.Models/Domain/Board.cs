namespace BattleShip.Models.Domain;

public sealed class Board
{
    private readonly List<Ship> _ships = [];
    private readonly HashSet<Coordinate> _shotsReceived = [];

    public IReadOnlyList<Ship> Ships => _ships;
    public IReadOnlyCollection<Coordinate> ShotsReceived => _shotsReceived;
    public bool AllSunk => _ships.Count > 0 && _ships.All(s => s.IsSunk);

    public bool IsOccupied(Coordinate c) => _ships.Any(s => s.Occupies(c));

    /// <summary>Seule source de vérité pour les bornes/le chevauchement : utilisée à la fois par le placement aléatoire et par les tests qui posent une flotte fixe.</summary>
    public bool TryPlaceShip(ShipKind kind, Coordinate origin, Orientation orientation, int length)
    {
        var cells = ComputeCells(origin, orientation, length);
        if (cells is null || cells.Any(IsOccupied))
            return false;

        _ships.Add(new Ship(kind, cells));
        return true;
    }

    public void PlaceFleetRandomly(IReadOnlyList<(ShipKind Kind, int Size)> spec, Random rng, int maxAttemptsPerShip = 200)
    {
        foreach (var (kind, size) in spec)
        {
            var placed = false;
            for (var attempt = 0; attempt < maxAttemptsPerShip && !placed; attempt++)
            {
                var origin = new Coordinate(rng.Next(BoardGrid.Size), rng.Next(BoardGrid.Size));
                var orientation = (Orientation)rng.Next(2);
                placed = TryPlaceShip(kind, origin, orientation, size);
            }

            if (!placed)
                throw new InvalidOperationException($"Impossible de placer {kind} après {maxAttemptsPerShip} tentatives.");
        }
    }

    public bool IsValidTarget(Coordinate c) => BoardGrid.Contains(c) && !_shotsReceived.Contains(c);

    public ShotResolution ReceiveShot(Coordinate target)
    {
        _shotsReceived.Add(target);

        var ship = _ships.FirstOrDefault(s => s.Occupies(target));
        if (ship is null)
            return new ShotResolution(target, ShotOutcome.Miss, null);

        ship.RegisterHit(target);
        return ship.IsSunk
            ? new ShotResolution(target, ShotOutcome.Sunk, ship.Kind)
            : new ShotResolution(target, ShotOutcome.Hit, null);
    }

    private static IReadOnlyList<Coordinate>? ComputeCells(Coordinate origin, Orientation orientation, int length)
    {
        var cells = new Coordinate[length];
        for (var i = 0; i < length; i++)
        {
            var cell = orientation == Orientation.Horizontal
                ? origin with { Column = origin.Column + i }
                : origin with { Row = origin.Row + i };

            if (!BoardGrid.Contains(cell))
                return null;

            cells[i] = cell;
        }

        return cells;
    }
}
