using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>S-10 — ✨ Baguette magique (docs/ticket.md TICKET-13) : couler un navire avec une arme spéciale ; le coup de grâce suffit.</summary>
public class MagicWandRuleTests
{
    private static readonly MagicWandRule Rule = new();

    private static TurnResult WeaponTurn(WeaponKind kind, params ShotOutcome[] outcomes) =>
        new(
            outcomes.Select(o => new ShotResolution(new Coordinate(0, 0), o, null)).ToList(),
            null, kind, [], null, null, GameStatus.InProgress);

    private static TurnResult ClassicShotTurn(params ShotOutcome[] outcomes) =>
        new(
            outcomes.Select(o => new ShotResolution(new Coordinate(0, 0), o, null)).ToList(),
            null, null, [], null, null, GameStatus.InProgress);

    [Fact]
    public void WeaponSinksAShip_Unlocks()
    {
        var context = AchievementContexts.New(history: [WeaponTurn(WeaponKind.Torpedo, ShotOutcome.Miss, ShotOutcome.Sunk)]);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void WeaponHitsWithoutSinking_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [WeaponTurn(WeaponKind.Torpedo, ShotOutcome.Hit)]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void ClassicShotSinking_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [ClassicShotTurn(ShotOutcome.Sunk)]);

        Assert.False(Rule.IsUnlocked(context));
    }

    /// <summary>Le navire peut déjà avoir été endommagé par un tour précédent : seul le coup de grâce doit venir d'une arme.</summary>
    [Fact]
    public void WeaponFinishingAPreviouslyDamagedShip_Unlocks()
    {
        var context = AchievementContexts.New(history:
        [
            ClassicShotTurn(ShotOutcome.Hit),
            WeaponTurn(WeaponKind.AirStrike, ShotOutcome.Miss, ShotOutcome.Sunk)
        ]);

        Assert.True(Rule.IsUnlocked(context));
    }
}
