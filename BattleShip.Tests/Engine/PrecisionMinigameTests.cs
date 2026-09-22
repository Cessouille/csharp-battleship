using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

/// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md.</summary>
public class PrecisionMinigameTests
{
    private static readonly GameOptions WithMinigame = new() { PrecisionMinigame = true };

    // Décale l'horloge à l'intérieur de la zone rose (centre du premier aller), garanti dans [ZoneStart, ZoneStart+ZoneWidth].
    private static void MoveClockInsideZone(FakeClock clock, DateTimeOffset startedAt) =>
        clock.Now = startedAt + TimeSpan.FromMilliseconds(TimingRules.PeriodMs / 2.0);

    // Départ du balayage (position 0) : hors zone puisque ZoneStart > 0.
    private static void MoveClockOutsideZone(FakeClock clock, DateTimeOffset startedAt) =>
        clock.Now = startedAt;

    [Fact]
    public void PlayHumanShot_OnShipCell_WithMinigameDisabled_ResolvesImmediately_NoChallenge()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, GameOptions.Classic, new FixedTargeting(new Coordinate(5, 5)));

        var result = game.PlayHumanShot(new Coordinate(9, 9));

        Assert.IsType<MoveResult.Accepted>(result);
    }

    [Fact]
    public void PlayHumanShot_OnEmptyCell_WithMinigameEnabled_ResolvesImmediately_NoChallenge()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)));

        var result = game.PlayHumanShot(new Coordinate(0, 5)); // case vide

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Miss, Assert.Single(accepted.Turn.PlayerShots).Outcome);
    }

    [Fact]
    public void PlayHumanShot_OnShipCell_WithMinigameEnabled_OpensAttackChallenge_BoardNotYetMutated()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)));

        var result = game.PlayHumanShot(new Coordinate(9, 9));

        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(result);
        Assert.Equal(ChallengeKind.Attack, awaiting.Challenge.Kind);
        Assert.Equal(new Coordinate(9, 9), awaiting.Challenge.Target);
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(9, 9))); // pas encore résolu
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void ResolveChallenge_AttackInsideZone_AppliesNormalResolution()
    {
        var clock = new FakeClock();
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)), clock: clock.Get);
        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 9)));
        MoveClockInsideZone(clock, awaiting.Challenge.StartedAtUtc);

        var result = game.ResolveChallenge(awaiting.Challenge.Id);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Hit, Assert.Single(accepted.Turn.PlayerShots).Outcome);
        Assert.False(game.ComputerBoard.IsValidTarget(new Coordinate(9, 9)));
    }

    [Fact]
    public void ResolveChallenge_AttackOutsideZone_ForcesMiss_ShipUnhit_CellStillPlayable()
    {
        var clock = new FakeClock();
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)), clock: clock.Get);
        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 9)));
        MoveClockOutsideZone(clock, awaiting.Challenge.StartedAtUtc);

        var result = game.ResolveChallenge(awaiting.Challenge.Id);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Miss, Assert.Single(accepted.Turn.PlayerShots).Outcome);
        Assert.False(game.ComputerBoard.Ships[0].IsSunk);
        // Sinon cette case ne serait plus jamais ciblable et le navire ne pourrait plus jamais couler.
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(9, 9)));
    }

    /// <summary>Régression : un raté forcé sur une case de navire ne doit plus jamais empêcher ce navire de couler.</summary>
    [Fact]
    public void ResolveChallenge_AttackOutsideZone_ThenHittingTheSameCellAgain_CanStillSinkTheShip()
    {
        var clock = new FakeClock();
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 9), Orientation.Horizontal, 1);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)), clock: clock.Get);
        var missedChallenge = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 9)));
        MoveClockOutsideZone(clock, missedChallenge.Challenge.StartedAtUtc);
        game.ResolveChallenge(missedChallenge.Challenge.Id);

        var retryChallenge = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 9)));
        MoveClockInsideZone(clock, retryChallenge.Challenge.StartedAtUtc);
        var result = game.ResolveChallenge(retryChallenge.Challenge.Id);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Sunk, Assert.Single(accepted.Turn.PlayerShots).Outcome);
        Assert.True(game.ComputerBoard.Ships[0].IsSunk);
    }

    [Fact]
    public void ResolveChallenge_WithUnknownChallengeId_IsRejected_NoChallengePending()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)));

        var result = game.ResolveChallenge(Guid.NewGuid());

        Assert.Equal(MoveRejectionReason.NoChallengePending, Assert.IsType<MoveResult.Rejected>(result).Reason);
    }

    [Fact]
    public void PlayHumanShot_WhileAChallengeIsPending_IsRejected_ChallengeInProgress()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        var game = new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(5, 5)));
        game.PlayHumanShot(new Coordinate(9, 9)); // ouvre un défi d'attaque

        var result = game.PlayHumanShot(new Coordinate(9, 8));

        Assert.Equal(MoveRejectionReason.ChallengeInProgress, Assert.IsType<MoveResult.Rejected>(result).Reason);
    }

    /// <summary>Navire humain d'une seule case : tout tir dessus couperait la flotte, donc déclenche systématiquement la défense.</summary>
    private static Game CreateGameThreateningHumanShip(FakeClock? clock = null)
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 8), Orientation.Horizontal, 2);
        return new Game(Guid.NewGuid(), human, computer, WithMinigame, new FixedTargeting(new Coordinate(0, 0)), clock: clock is null ? null : clock.Get);
    }

    [Fact]
    public void PlayHumanShot_TriggeringFatalComputerCounterShot_OpensDefenseChallenge()
    {
        var game = CreateGameThreateningHumanShip();

        var result = game.PlayHumanShot(new Coordinate(9, 0)); // case vide côté ordinateur : pas de défi d'attaque

        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(result);
        Assert.Equal(ChallengeKind.Defense, awaiting.Challenge.Kind);
        Assert.Equal(new Coordinate(0, 0), awaiting.Challenge.Target);
    }

    [Fact]
    public void ResolveChallenge_DefenseInsideZone_SparesTheShip_CellStaysPlayable()
    {
        var clock = new FakeClock();
        var game = CreateGameThreateningHumanShip(clock);
        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 0)));
        MoveClockInsideZone(clock, awaiting.Challenge.StartedAtUtc);

        var result = game.ResolveChallenge(awaiting.Challenge.Id);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Miss, Assert.Single(accepted.Turn.ComputerShots).Outcome);
        Assert.False(game.HumanBoard.Ships[0].IsSunk);
        Assert.True(game.HumanBoard.IsValidTarget(new Coordinate(0, 0))); // esquivé : rejouable plus tard
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void ResolveChallenge_DefenseOutsideZone_ShipSinksNormally()
    {
        var clock = new FakeClock();
        var game = CreateGameThreateningHumanShip(clock);
        var awaiting = Assert.IsType<MoveResult.AwaitingChallenge>(game.PlayHumanShot(new Coordinate(9, 0)));
        MoveClockOutsideZone(clock, awaiting.Challenge.StartedAtUtc);

        var result = game.ResolveChallenge(awaiting.Challenge.Id);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Sunk, Assert.Single(accepted.Turn.ComputerShots).Outcome);
        Assert.True(game.HumanBoard.Ships[0].IsSunk);
        Assert.Equal(PlayerId.Computer, game.Winner);
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void PlayHumanSalvo_WithTwoShipCellsAcrossDifferentShips_OpensChallengesSequentially()
    {
        var clock = new FakeClock();
        var human = new Board();
        // 2 navires côté humain : la taille de salve (nombre de navires encore à flot chez le tireur) doit valoir 2.
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        human.TryPlaceShip(ShipKind.SousMarin, new Coordinate(1, 1), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        computer.TryPlaceShip(ShipKind.SousMarin, new Coordinate(5, 5), Orientation.Horizontal, 1);
        var options = new GameOptions { PrecisionMinigame = true, ShotMode = ShotMode.Salvo };
        var game = new Game(Guid.NewGuid(), human, computer, options, new FixedTargeting(new Coordinate(9, 9)), clock: clock.Get);

        var first = Assert.IsType<MoveResult.AwaitingChallenge>(
            game.PlayHumanSalvo([new Coordinate(0, 0), new Coordinate(5, 5)]));
        Assert.Equal(new Coordinate(0, 0), first.Challenge.Target);

        // Raté forcé sur le premier navire : ne coule pas la flotte, la séquence doit continuer vers la seconde case.
        MoveClockOutsideZone(clock, first.Challenge.StartedAtUtc);
        var second = Assert.IsType<MoveResult.AwaitingChallenge>(game.ResolveChallenge(first.Challenge.Id));
        Assert.Equal(new Coordinate(5, 5), second.Challenge.Target);
        Assert.False(game.ComputerBoard.Ships.First(s => s.Kind == ShipKind.Torpilleur).IsSunk);
    }
}
