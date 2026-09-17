using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>S-07 — 💘 Coup de foudre (docs/ticket.md TICKET-13) : le tout premier tir du joueur de la partie touche ou coule.</summary>
public class LoveAtFirstSightRuleTests
{
    private static readonly LoveAtFirstSightRule Rule = new();

    private static TurnResult ShotTurn(params ShotOutcome[] outcomes) =>
        new(
            outcomes.Select(o => new ShotResolution(new Coordinate(0, 0), o, null)).ToList(),
            null, null, [], null, null, GameStatus.InProgress);

    private static TurnResult ScanTurn() =>
        new([], new ScanResult(new Coordinate(0, 0), false), null, [], null, null, GameStatus.InProgress);

    [Fact]
    public void FirstShotHits_Unlocks()
    {
        var context = AchievementContexts.New(history: [ShotTurn(ShotOutcome.Hit)]);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FirstShotMisses_ThenHits_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [ShotTurn(ShotOutcome.Miss), ShotTurn(ShotOutcome.Hit)]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void ScanBeforeFirstShot_DoesNotDisqualify()
    {
        var context = AchievementContexts.New(history: [ScanTurn(), ShotTurn(ShotOutcome.Hit)]);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void NoShotsYet_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [ScanTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void EmptyHistory_DoesNotUnlock()
    {
        var context = AchievementContexts.New();

        Assert.False(Rule.IsUnlocked(context));
    }

    /// <summary>
    /// Arbitrage assumé (docs/adr/0017-systeme-de-succes.md) : une torpille en première action traverse
    /// d'abord ses cases vides (Miss), donc "premier tir" au sens de la résolution est ce sillage, pas
    /// l'impact final — le succès devient quasi inaccessible par ce chemin, en connaissance de cause.
    /// </summary>
    [Fact]
    public void FirstActionIsATorpedoCrossingEmptyCells_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [ShotTurn(ShotOutcome.Miss, ShotOutcome.Miss, ShotOutcome.Hit)]);

        Assert.False(Rule.IsUnlocked(context));
    }
}
