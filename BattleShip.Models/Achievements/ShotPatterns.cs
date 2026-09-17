using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// Motifs 5×5 pour S-01 (💖 Cœur de tirs) et S-08 (🎀 Nœud papillon), voir docs/ticket.md TICKET-13. Écrits
/// en littéraux lisibles (X = case du motif, . = case ignorée) plutôt qu'en liste de coordonnées, pour que le
/// dessin reste vérifiable à l'œil dans le source.
/// </summary>
public static class ShotPatterns
{
    public static IReadOnlyList<Coordinate> Heart { get; } = Parse(
        ". X . X .",
        "X X X X X",
        "X X X X X",
        ". X X X .",
        ". . X . .");

    public static IReadOnlyList<Coordinate> BowTie { get; } = Parse(
        "X . . . X",
        "X X . X X",
        "X X X X X",
        "X X . X X",
        "X . . . X");

    private static IReadOnlyList<Coordinate> Parse(params string[] rows)
    {
        var cells = new List<Coordinate>();
        for (var row = 0; row < rows.Length; row++)
        {
            var columns = rows[row].Split(' ');
            for (var column = 0; column < columns.Length; column++)
            {
                if (columns[column] == "X")
                    cells.Add(new Coordinate(row, column));
            }
        }

        return cells;
    }
}
