using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// S-06 — 📏 Rangée parfaite (docs/ticket.md TICKET-13, docs/adr/0017-systeme-de-succes.md) : la flotte du
/// joueur n'occupe que deux lignes distinctes au plus. Testé directement sur la règle, isolée de Game.
/// </summary>
public class PerfectRowRuleTests
{
    private static readonly PerfectRowRule Rule = new();

    private static AchievementContext ContextWithFleet(params OwnShipView[] fleet) => new(
        GameOptions.Classic,
        Winner: null,
        Status: GameStatus.InProgress,
        History: [],
        CellsFiredAt: new HashSet<Coordinate>(),
        OwnFleet: fleet);

    private static OwnShipView Ship(params (int Row, int Column)[] cells) =>
        new(ShipKind.Torpilleur, cells.Select(c => new Coordinate(c.Row, c.Column)).ToList(), IsSunk: false);

    [Fact]
    public void FleetOnTwoAdjacentRows_Unlocks()
    {
        var context = ContextWithFleet(
            Ship((4, 0), (4, 1), (4, 2)),
            Ship((5, 0), (5, 1)));

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FleetOnThreeRows_DoesNotUnlock()
    {
        var context = ContextWithFleet(
            Ship((4, 0), (4, 1)),
            Ship((5, 0), (5, 1)),
            Ship((6, 0)));

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FleetOnNonAdjacentRows_Unlocks()
    {
        var context = ContextWithFleet(
            Ship((0, 0), (0, 1)),
            Ship((9, 0), (9, 1)));

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void VerticalShipStraddlingTheTwoRows_Unlocks()
    {
        var context = ContextWithFleet(
            Ship((4, 0), (4, 1), (4, 2)),
            Ship((4, 5), (5, 5))); // torpilleur vertical à cheval sur les deux lignes déjà occupées

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void VerticalShipAddingAThirdRow_DoesNotUnlock()
    {
        var context = ContextWithFleet(
            Ship((4, 0), (4, 1)),
            Ship((5, 0), (5, 1)),
            Ship((5, 5), (6, 5))); // torpilleur vertical ajoute une 3e ligne

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FleetOnASingleRow_Unlocks()
    {
        var context = ContextWithFleet(Ship((7, 0), (7, 1)), Ship((7, 3)));

        Assert.True(Rule.IsUnlocked(context));
    }
}
