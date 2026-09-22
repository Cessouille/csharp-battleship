using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine;

public class VolleySequencerTests
{
    [Fact]
    public void Advance_WithNoChallengeNeeded_ResolvesEveryCell_LikeTheOldSynchronousLoop()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2); // 2 cases : (0,0) seule ne coule pas
        var sequencer = new VolleySequencer(board, [new Coordinate(0, 0), new Coordinate(5, 5)], stopOnFirstHit: false, needsChallenge: _ => false);

        var next = sequencer.Advance();

        Assert.Null(next);
        Assert.Equal([ShotOutcome.Hit, ShotOutcome.Miss], sequencer.Resolutions.Select(r => r.Outcome));
    }

    [Fact]
    public void Advance_StopsAtAllSunk_WithoutResolvingRemainingCells()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var sequencer = new VolleySequencer(board, [new Coordinate(0, 0), new Coordinate(5, 5)], stopOnFirstHit: false, needsChallenge: _ => false);

        sequencer.Advance();

        Assert.Single(sequencer.Resolutions);
        Assert.True(board.IsValidTarget(new Coordinate(5, 5))); // jamais atteinte
    }

    [Fact]
    public void Advance_WithStopOnFirstHit_StopsAfterFirstNonMissResolution()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(0, 2), Orientation.Horizontal, 4);
        var sequencer = new VolleySequencer(
            board, [new Coordinate(0, 0), new Coordinate(0, 1), new Coordinate(0, 2), new Coordinate(0, 3)],
            stopOnFirstHit: true, needsChallenge: _ => false);

        sequencer.Advance();

        Assert.Equal([ShotOutcome.Miss, ShotOutcome.Miss, ShotOutcome.Hit], sequencer.Resolutions.Select(r => r.Outcome));
    }

    [Fact]
    public void Advance_WhenCellNeedsChallenge_StopsWithoutMutating_AndKeepsCellPlayable()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var target = new Coordinate(0, 0);
        var sequencer = new VolleySequencer(board, [target], stopOnFirstHit: false, needsChallenge: c => c == target);

        var next = sequencer.Advance();

        Assert.Equal(target, next);
        Assert.Empty(sequencer.Resolutions);
        Assert.True(board.IsValidTarget(target));
    }

    [Fact]
    public void SubmitChallengeResult_AppliesTheGivenResolution_ThenContinues()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        var sequencer = new VolleySequencer(
            board, [new Coordinate(0, 0), new Coordinate(5, 5)], stopOnFirstHit: false, needsChallenge: c => c == new Coordinate(0, 0));
        sequencer.Advance();

        var next = sequencer.SubmitChallengeResult(board.ReceiveShot);

        Assert.Null(next);
        Assert.Equal([ShotOutcome.Hit, ShotOutcome.Miss], sequencer.Resolutions.Select(r => r.Outcome));
    }

    [Fact]
    public void SubmitChallengeResult_WithForcedMissResolution_LeavesShipUnhit_CellStillPlayable()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var target = new Coordinate(0, 0);
        var sequencer = new VolleySequencer(board, [target], stopOnFirstHit: false, needsChallenge: c => c == target);
        sequencer.Advance();

        sequencer.SubmitChallengeResult(board.ReceiveShotForcedMiss);

        Assert.Equal(ShotOutcome.Miss, Assert.Single(sequencer.Resolutions).Outcome);
        Assert.False(board.Ships[0].IsSunk);
        Assert.True(board.IsValidTarget(target)); // sinon ce navire ne pourrait plus jamais couler
    }

    [Fact]
    public void SubmitChallengeResult_WithDodgedResolution_LeavesCellStillPlayable()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var target = new Coordinate(0, 0);
        var sequencer = new VolleySequencer(board, [target], stopOnFirstHit: false, needsChallenge: c => c == target);
        sequencer.Advance();

        sequencer.SubmitChallengeResult(board.ReceiveShotDodged);

        Assert.Equal(ShotOutcome.Miss, Assert.Single(sequencer.Resolutions).Outcome);
        Assert.False(board.Ships[0].IsSunk);
        Assert.True(board.IsValidTarget(target)); // esquivé : rejouable plus tard
    }

    [Fact]
    public void SubmitChallengeResult_WhenNextCellAlsoNeedsChallenge_ReturnsIt()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(0, 0), Orientation.Horizontal, 4);
        var cellA = new Coordinate(0, 0);
        var cellB = new Coordinate(0, 1);
        var sequencer = new VolleySequencer(board, [cellA, cellB], stopOnFirstHit: false, needsChallenge: c => c == cellA || c == cellB);
        sequencer.Advance();

        var next = sequencer.SubmitChallengeResult(board.ReceiveShot);

        Assert.Equal(cellB, next);
        Assert.Single(sequencer.Resolutions);
    }

    [Fact]
    public void Advance_ReEvaluatesNeedsChallenge_OnUpdatedBoardState_NotDecidedUpfront()
    {
        // Navire de 2 cases : (0,0) seule ne le couperait pas (needsChallenge=WouldSink -> false), mais une fois
        // (0,0) touchée, (0,1) devient la dernière case -> WouldSink((0,1)) devient vrai. Un calcul "à l'avance"
        // sur l'état initial du plateau raterait ce changement ; c'est la raison d'être du VolleySequencer.
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        var cellA = new Coordinate(0, 0);
        var cellB = new Coordinate(0, 1);
        var sequencer = new VolleySequencer(board, [cellA, cellB], stopOnFirstHit: false, needsChallenge: board.WouldSink);

        var next = sequencer.Advance();

        Assert.Equal(cellB, next);
        Assert.Equal(ShotOutcome.Hit, Assert.Single(sequencer.Resolutions).Outcome);
    }
}
