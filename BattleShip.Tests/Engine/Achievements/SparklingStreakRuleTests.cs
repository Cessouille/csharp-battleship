using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// S-04 — 💫 Série étincelante (docs/ticket.md TICKET-13) : 5 résolutions de tir du joueur consécutives
/// (Hit ou Sunk), dans l'ordre du journal, tous types d'action confondus (tir, salve, arme).
/// </summary>
public class SparklingStreakRuleTests
{
    private static readonly SparklingStreakRule Rule = new();

    private static TurnResult ShotTurn(params ShotOutcome[] outcomes) =>
        new(
            outcomes.Select(o => new ShotResolution(new Coordinate(0, 0), o, null)).ToList(),
            null, null, [], null, null, GameStatus.InProgress);

    private static TurnResult ScanTurn() =>
        new([], new ScanResult(new Coordinate(0, 0), false), null, [], null, null, GameStatus.InProgress);

    [Fact]
    public void FiveConsecutiveHits_AcrossTurns_Unlocks()
    {
        var context = AchievementContexts.New(history:
        [
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Hit),
            ShotTurn(ShotOutcome.Hit),
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Sunk)
        ]);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void FourHitsThenMissThenHit_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history:
        [
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Hit, ShotOutcome.Hit, ShotOutcome.Hit),
            ShotTurn(ShotOutcome.Miss),
            ShotTurn(ShotOutcome.Hit)
        ]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void ScanBetweenHits_DoesNotBreakTheStreak()
    {
        var context = AchievementContexts.New(history:
        [
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Hit),
            ScanTurn(),
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Hit, ShotOutcome.Hit)
        ]);

        Assert.True(Rule.IsUnlocked(context));
    }

    /// <summary>
    /// Arbitrage assumé (docs/adr/0017-systeme-de-succes.md) : les cases vides traversées par une torpille sont
    /// résolues en Miss comme n'importe quel tir raté (Board.ReceiveWeapon), donc elles cassent la série.
    /// </summary>
    [Fact]
    public void TorpedoTrailMisses_BreakTheStreak()
    {
        var context = AchievementContexts.New(history:
        [
            ShotTurn(ShotOutcome.Hit, ShotOutcome.Hit, ShotOutcome.Hit, ShotOutcome.Hit),
            ShotTurn(ShotOutcome.Miss, ShotOutcome.Miss, ShotOutcome.Hit)
        ]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void NoShotsYet_DoesNotUnlock()
    {
        var context = AchievementContexts.New(history: [ScanTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }

    /// <summary>
    /// Vérifie via un vrai Game (pas seulement la règle isolée) qu'un coup refusé au milieu de la série ne la
    /// casse pas : un MoveResult.Rejected n'atteint jamais CompleteTurn, donc jamais History ni le contexte.
    /// </summary>
    [Fact]
    public void RejectedDuplicateShotMidStreak_LeavesStreakIntact()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.PorteAvions, new Coordinate(0, 0), Orientation.Horizontal, 5);
        var game = new Game(Guid.NewGuid(), human, computer, GameOptions.Classic, new BottomUpTargeting(), new Random(0));

        game.PlayHumanShot(new Coordinate(0, 0));
        game.PlayHumanShot(new Coordinate(0, 1));
        var rejected = game.PlayHumanShot(new Coordinate(0, 1)); // déjà jouée : refusé
        game.PlayHumanShot(new Coordinate(0, 2));
        game.PlayHumanShot(new Coordinate(0, 3));
        game.PlayHumanShot(new Coordinate(0, 4)); // coule le porte-avions : 5e touche consécutive

        Assert.IsType<MoveResult.Rejected>(rejected);
        Assert.Contains(AchievementId.SerieEtincelante, game.Achievements);
    }
}
