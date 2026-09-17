using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-05 — 💎 Quatre coins (docs/ticket.md TICKET-13) : victoire en ayant tiré dans les 4 coins de la grille
/// adverse. Coins dérivés de <see cref="BoardGrid.Size"/> (jamais codés en dur) ; le tir gagnant n'a pas à
/// être lui-même un coin (voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class FourCornersRule : IAchievementRule
{
    private static readonly Coordinate[] Corners =
    [
        new(0, 0),
        new(0, BoardGrid.Size - 1),
        new(BoardGrid.Size - 1, 0),
        new(BoardGrid.Size - 1, BoardGrid.Size - 1)
    ];

    public AchievementId Id => AchievementId.QuatreCoins;

    public bool IsUnlocked(AchievementContext context) =>
        context.Winner == PlayerId.Human && Corners.All(context.CellsFiredAt.Contains);
}
