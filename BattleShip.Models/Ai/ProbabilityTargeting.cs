using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Ai;

/// <summary>
/// Grille de probabilité (voir docs/adr/0008-ia-grille-probabilite.md) : pour chaque case non jouée, somme des
/// poids de tous les placements encore possibles des navires non coulés qui la recouvrent. Un placement qui
/// recouvre une touche non coulée pèse beaucoup plus lourd, ce qui fait basculer l'IA en mode « cible ».
/// </summary>
public sealed class ProbabilityTargeting : IComputerTargeting
{
    /// <summary>Poids ajouté par touche non coulée recouverte : assez grand pour que tout placement passant par une touche domine les placements « à l'aveugle ».</summary>
    public const int HitCoverageBonus = 100;

    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng)
    {
        var density = ComputeDensity(view);
        var played = PlayedCells(view);

        var unplayed = Enumerable.Range(0, view.Size * view.Size)
            .Select(i => new Coordinate(i / view.Size, i % view.Size))
            .Where(c => !played.Contains(c))
            .ToArray();

        if (unplayed.Length < count)
            throw new InvalidOperationException($"{count} cibles demandées pour {unplayed.Length} cases non jouées.");

        // Mélange puis tri stable par densité décroissante : les égalités sont départagées au hasard. Si aucune
        // densité n'est positive (vue incohérente), cela revient à tirer au hasard parmi les cases non jouées.
        rng.Shuffle(unplayed);
        return unplayed.OrderByDescending(c => density[c.Row, c.Column]).Take(count).ToList();
    }

    /// <summary>
    /// Heuristique volontairement simple (voir docs/adr/0012-armes-speciales.md) : la frappe sert à achever un
    /// navire touché, centrée sur la case la plus probable ; la torpille sert en phase de recherche, sur le
    /// couloir dont les cases non jouées sont les plus probables. Les deux s'appuient sur <see cref="ComputeDensity"/>.
    /// </summary>
    public WeaponAction? PickWeapon(OpponentBoardDto view, bool torpedoAvailable, bool airStrikeAvailable, Random rng)
    {
        var density = ComputeDensity(view);
        var played = PlayedCells(view);
        int Score(IEnumerable<Coordinate> cells) => cells.Where(c => !played.Contains(c)).Sum(c => density[c.Row, c.Column]);

        if (airStrikeAvailable && view.Hits.Count > 0)
        {
            var best = PickTargets(view, 1, rng)[0];
            return AirStrikesCovering(best)
                .Select(strike => (Strike: strike, Score: Score(WeaponRules.Cells(strike)!)))
                .OrderByDescending(s => s.Score)
                .Select(s => s.Strike)
                .FirstOrDefault();
        }

        if (torpedoAvailable && view.Hits.Count == 0)
        {
            var torpedoes = Enum.GetValues<Edge>()
                .SelectMany(edge => Enumerable.Range(0, view.Size).Select(lane => new WeaponAction.Torpedo(edge, lane)))
                .Select(torpedo => (Torpedo: torpedo, Score: Score(WeaponRules.Cells(torpedo)!)))
                .Where(t => t.Score > 0)
                .ToList();

            return torpedoes.Count == 0 ? null : torpedoes.MaxBy(t => t.Score).Torpedo;
        }

        return null;
    }

    private static IEnumerable<WeaponAction.AirStrike> AirStrikesCovering(Coordinate cell)
    {
        for (var offset = 0; offset < WeaponRules.AirStrikeLength; offset++)
        {
            var horizontal = new WeaponAction.AirStrike(cell with { Column = cell.Column - offset }, Orientation.Horizontal);
            if (WeaponRules.Cells(horizontal) is not null)
                yield return horizontal;

            var vertical = new WeaponAction.AirStrike(cell with { Row = cell.Row - offset }, Orientation.Vertical);
            if (WeaponRules.Cells(vertical) is not null)
                yield return vertical;
        }
    }

    public static int[,] ComputeDensity(OpponentBoardDto view)
    {
        var density = new int[view.Size, view.Size];
        var hits = view.Hits.Select(ToCoordinate).ToHashSet();
        var blocked = view.Misses.Select(ToCoordinate)
            .Concat(view.SunkShips.SelectMany(s => s.Cells).Select(ToCoordinate))
            .ToHashSet();

        foreach (var size in RemainingShipSizes(view))
            foreach (var orientation in (Orientation[])[Orientation.Horizontal, Orientation.Vertical])
                for (var row = 0; row < view.Size; row++)
                    for (var column = 0; column < view.Size; column++)
                    {
                        var cells = Segment(new Coordinate(row, column), orientation, size, view.Size);
                        if (cells is null || cells.Any(blocked.Contains))
                            continue;

                        var weight = 1 + HitCoverageBonus * cells.Count(hits.Contains);
                        foreach (var cell in cells.Where(c => !hits.Contains(c)))
                            density[cell.Row, cell.Column] += weight;
                    }

        return density;
    }

    private static IEnumerable<int> RemainingShipSizes(OpponentBoardDto view)
    {
        var sunk = view.SunkShips.Select(s => Enum.Parse<ShipKind>(s.Kind)).ToList();
        foreach (var (kind, size) in Fleet.Standard)
        {
            if (!sunk.Remove(kind))
                yield return size;
        }
    }

    /// <summary>Interne mais visible aux tests (voir InternalsVisibleTo) : les doublures de stratégie de test
    /// réutilisent cette règle plutôt que de la recopier, pour ne pas diverger de celle réellement utilisée en jeu.</summary>
    internal static HashSet<Coordinate> PlayedCells(OpponentBoardDto view) =>
        view.Hits.Concat(view.Misses).Concat(view.SunkShips.SelectMany(s => s.Cells)).Select(ToCoordinate).ToHashSet();

    private static Coordinate[]? Segment(Coordinate origin, Orientation orientation, int length, int gridSize)
    {
        var last = orientation == Orientation.Horizontal ? origin.Column + length - 1 : origin.Row + length - 1;
        if (last >= gridSize)
            return null;

        var cells = new Coordinate[length];
        for (var i = 0; i < length; i++)
            cells[i] = orientation == Orientation.Horizontal
                ? origin with { Column = origin.Column + i }
                : origin with { Row = origin.Row + i };
        return cells;
    }

    private static Coordinate ToCoordinate(CoordinateDto c) => new(c.Row, c.Column);
}
