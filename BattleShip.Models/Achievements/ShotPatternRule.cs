using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// Moteur commun à S-01 (💖 Cœur de tirs) et S-08 (🎀 Nœud papillon) : le motif doit apparaître, par simple
/// translation, dans une fenêtre 5×5 quelconque de la grille. Les cases de la fenêtre hors motif sont
/// ignorées (des tirs en plus n'invalident pas le dessin) ; aucune rotation n'est acceptée — deux arbitrages
/// assumés (voir docs/adr/0017-systeme-de-succes.md). <see cref="AchievementContext.CellsFiredAt"/> est un
/// <see cref="IReadOnlySet{T}"/> (voir <see cref="Board.ShotsReceived"/>) : chaque case testée en O(1).
/// </summary>
public sealed class ShotPatternRule(AchievementId id, IReadOnlyList<Coordinate> pattern) : IAchievementRule
{
    private const int WindowSize = 5;

    public AchievementId Id { get; } = id;

    public bool IsUnlocked(AchievementContext context)
    {
        // Sortie anticipée : sous ce total, aucune fenêtre ne peut contenir le motif entier.
        if (context.CellsFiredAt.Count < pattern.Count)
            return false;

        for (var originRow = 0; originRow <= BoardGrid.Size - WindowSize; originRow++)
        {
            for (var originColumn = 0; originColumn <= BoardGrid.Size - WindowSize; originColumn++)
            {
                if (MatchesWindow(context, originRow, originColumn))
                    return true;
            }
        }

        return false;
    }

    private bool MatchesWindow(AchievementContext context, int originRow, int originColumn) =>
        pattern.All(c => context.CellsFiredAt.Contains(new Coordinate(originRow + c.Row, originColumn + c.Column)));
}
