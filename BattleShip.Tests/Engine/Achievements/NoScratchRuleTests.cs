using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>S-02 — 🛡️ Sans une égratignure (docs/ticket.md TICKET-13) : gagner sans qu'aucun navire du joueur ne soit coulé.</summary>
public class NoScratchRuleTests
{
    private static readonly NoScratchRule Rule = new();

    private static readonly OwnShipView[] IntactFleet =
    [
        AchievementContexts.Ship(ShipKind.Torpilleur, isSunk: false, (0, 0), (0, 1)),
        AchievementContexts.Ship(ShipKind.SousMarin, isSunk: false, (2, 0), (2, 1), (2, 2))
    ];

    private static readonly OwnShipView[] OneSunkShip =
    [
        AchievementContexts.Ship(ShipKind.Torpilleur, isSunk: true, (0, 0), (0, 1)),
        AchievementContexts.Ship(ShipKind.SousMarin, isSunk: false, (2, 0), (2, 1), (2, 2))
    ];

    [Fact]
    public void Victory_WithIntactFleet_Unlocks()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: IntactFleet);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void Victory_WithOneSunkShip_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: OneSunkShip);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void IntactFleet_WhileInProgress_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: null, status: GameStatus.InProgress, ownFleet: IntactFleet);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void DefeatWithIntactFleet_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: PlayerId.Computer, status: GameStatus.Finished, ownFleet: IntactFleet);

        Assert.False(Rule.IsUnlocked(context));
    }
}
