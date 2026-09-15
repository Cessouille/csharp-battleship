namespace BattleShip.Models.Domain;

public static class RadarRules
{
    public const int ZoneSize = 2;
    public const int ScansPerGame = 2;

    /// <summary>La zone est désignée par son coin haut-gauche et doit tenir entièrement dans la grille.</summary>
    public static bool ZoneFits(Coordinate origin) =>
        BoardGrid.Contains(origin) && BoardGrid.Contains(new Coordinate(origin.Row + ZoneSize - 1, origin.Column + ZoneSize - 1));

    public static IEnumerable<Coordinate> ZoneCells(Coordinate origin)
    {
        for (var row = 0; row < ZoneSize; row++)
            for (var column = 0; column < ZoneSize; column++)
                yield return new Coordinate(origin.Row + row, origin.Column + column);
    }
}

/// <summary>Résultat d'un scan : uniquement la présence d'un navire dans la zone, jamais les cases concernées.</summary>
public sealed record ScanResult(Coordinate Origin, bool ShipDetected);
