using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>S-05 — 💎 Quatre coins (docs/ticket.md TICKET-13) : gagner en ayant tiré dans les 4 coins de la grille adverse.</summary>
public class FourCornersRuleTests
{
    private static readonly FourCornersRule Rule = new();

    private const int Last = BoardGrid.Size - 1;

    private static readonly Coordinate[] AllFourCorners =
        [new(0, 0), new(0, Last), new(Last, 0), new(Last, Last)];

    [Fact]
    public void Victory_WithFourCornersFiredAt_Unlocks()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, cellsFiredAt: AllFourCorners);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void Victory_WithOnlyThreeCorners_DoesNotUnlock()
    {
        var context = AchievementContexts.New(
            winner: PlayerId.Human,
            status: GameStatus.Finished,
            cellsFiredAt: AllFourCorners.Take(3));

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FourCorners_WithoutVictory_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: null, status: GameStatus.InProgress, cellsFiredAt: AllFourCorners);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void Defeat_WithFourCorners_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: PlayerId.Computer, status: GameStatus.Finished, cellsFiredAt: AllFourCorners);

        Assert.False(Rule.IsUnlocked(context));
    }
}
