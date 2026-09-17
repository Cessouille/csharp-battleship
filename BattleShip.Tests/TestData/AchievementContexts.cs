using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.TestData;

/// <summary>Construit un AchievementContext isolé pour tester une IAchievementRule sans passer par un Game complet.</summary>
public static class AchievementContexts
{
    public static OwnShipView Ship(ShipKind kind, bool isSunk, params (int Row, int Column)[] cells) =>
        new(kind, cells.Select(c => new Coordinate(c.Row, c.Column)).ToList(), isSunk);

    public static AchievementContext New(
        GameOptions? options = null,
        PlayerId? winner = null,
        GameStatus status = GameStatus.InProgress,
        IReadOnlyList<TurnResult>? history = null,
        IEnumerable<Coordinate>? cellsFiredAt = null,
        IReadOnlyList<OwnShipView>? ownFleet = null) => new(
        options ?? GameOptions.Classic,
        winner,
        status,
        history ?? [],
        (cellsFiredAt ?? []).ToHashSet(),
        ownFleet ?? []);
}
