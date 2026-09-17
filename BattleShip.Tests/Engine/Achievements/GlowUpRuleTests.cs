using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>S-11 — 🦋 Glow up (docs/ticket.md TICKET-13) : gagner avec un seul navire du joueur encore à flot.</summary>
public class GlowUpRuleTests
{
    private static readonly NoScratchRule NoScratch = new();
    private static readonly GlowUpRule GlowUp = new();

    private static OwnShipView[] FleetWithSunkCount(int sunkCount)
    {
        var kinds = new[] { ShipKind.PorteAvions, ShipKind.Croiseur, ShipKind.ContreTorpilleur, ShipKind.SousMarin, ShipKind.Torpilleur };
        return kinds.Select((kind, i) => AchievementContexts.Ship(kind, isSunk: i < sunkCount, (i, 0))).ToArray();
    }

    [Fact]
    public void Victory_WithExactlyOneShipAfloat_Unlocks()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: FleetWithSunkCount(4));

        Assert.True(GlowUp.IsUnlocked(context));
    }

    [Fact]
    public void Victory_WithTwoShipsAfloat_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: FleetWithSunkCount(3));

        Assert.False(GlowUp.IsUnlocked(context));
    }

    [Fact]
    public void Victory_WithNoShipsSunk_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: FleetWithSunkCount(0));

        Assert.False(GlowUp.IsUnlocked(context));
    }

    [Fact]
    public void GlowUpWithoutVictory_DoesNotUnlock()
    {
        var context = AchievementContexts.New(winner: null, status: GameStatus.InProgress, ownFleet: FleetWithSunkCount(4));

        Assert.False(GlowUp.IsUnlocked(context));
    }

    /// <summary>Exigé par docs/ticket.md (S-11) : intacte (0 coulé) et un seul à flot (4 coulés) s'excluent forcément sur une flotte de 5 navires.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NoScratchAndGlowUp_AreMutuallyExclusive(int sunkCount)
    {
        var context = AchievementContexts.New(winner: PlayerId.Human, status: GameStatus.Finished, ownFleet: FleetWithSunkCount(sunkCount));

        Assert.False(NoScratch.IsUnlocked(context) && GlowUp.IsUnlocked(context));
    }
}
