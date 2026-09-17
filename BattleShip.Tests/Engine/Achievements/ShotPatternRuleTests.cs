using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// S-01 — 💖 Cœur de tirs et S-08 — 🎀 Nœud papillon (docs/ticket.md TICKET-13) : même moteur, deux motifs
/// 5×5 différents. Le motif doit apparaître par translation dans une fenêtre 5×5 quelconque de la grille ;
/// les cases hors motif dans la fenêtre sont ignorées, et aucune rotation n'est acceptée (arbitrages, voir
/// docs/adr/0017-systeme-de-succes.md).
/// </summary>
public class ShotPatternRuleTests
{
    private static readonly ShotPatternRule HeartRule = new(AchievementId.CoeurDeTirs, ShotPatterns.Heart);
    private static readonly ShotPatternRule BowTieRule = new(AchievementId.NoeudPapillon, ShotPatterns.BowTie);

    [Fact]
    public void Patterns_HaveTheExpectedCellCount()
    {
        Assert.Equal(16, ShotPatterns.Heart.Count);
        Assert.Equal(17, ShotPatterns.BowTie.Count);
    }

    private static IEnumerable<Coordinate> AtOrigin(IReadOnlyList<Coordinate> pattern, int originRow, int originColumn) =>
        pattern.Select(c => new Coordinate(originRow + c.Row, originColumn + c.Column));

    [Fact]
    public void PatternAtTopLeft_Unlocks()
    {
        var context = AchievementContexts.New(cellsFiredAt: AtOrigin(ShotPatterns.Heart, 0, 0));

        Assert.True(HeartRule.IsUnlocked(context));
    }

    /// <summary>Fenêtre qui tient tout juste dans une grille 10×10 (origine (5,5), dernière case en (9,9)).</summary>
    [Fact]
    public void PatternAtBottomRightCorner_Unlocks()
    {
        var origin = BoardGrid.Size - 5;
        var context = AchievementContexts.New(cellsFiredAt: AtOrigin(ShotPatterns.Heart, origin, origin));

        Assert.True(HeartRule.IsUnlocked(context));
    }

    [Fact]
    public void PatternMissingOneCell_DoesNotUnlock()
    {
        var cells = AtOrigin(ShotPatterns.Heart, 0, 0).Skip(1);
        var context = AchievementContexts.New(cellsFiredAt: cells);

        Assert.False(HeartRule.IsUnlocked(context));
    }

    [Fact]
    public void ExtraShotsInsideTheWindow_DoNotInvalidate()
    {
        // Toute la fenêtre 5×5 est tirée (motif + cases hors motif) : le dessin reste reconnu.
        var wholeWindow = Enumerable.Range(0, 5).SelectMany(r => Enumerable.Range(0, 5).Select(c => new Coordinate(r, c)));
        var context = AchievementContexts.New(cellsFiredAt: wholeWindow);

        Assert.True(HeartRule.IsUnlocked(context));
    }

    [Fact]
    public void RotatedPattern_DoesNotUnlock()
    {
        // Rotation à 90° du cœur : une forme différente, confinée à la même fenêtre 5×5.
        var rotated = ShotPatterns.Heart.Select(c => new Coordinate(c.Column, 4 - c.Row));
        var context = AchievementContexts.New(cellsFiredAt: rotated);

        Assert.False(HeartRule.IsUnlocked(context));
    }

    [Fact]
    public void HeartCells_DoNotUnlockBowTie()
    {
        var context = AchievementContexts.New(cellsFiredAt: AtOrigin(ShotPatterns.Heart, 0, 0));

        Assert.False(BowTieRule.IsUnlocked(context));
    }

    [Fact]
    public void BowTieCells_DoNotUnlockHeart()
    {
        var context = AchievementContexts.New(cellsFiredAt: AtOrigin(ShotPatterns.BowTie, 0, 0));

        Assert.False(HeartRule.IsUnlocked(context));
    }

    [Fact]
    public void FullGridSweep_UnlocksBoth()
    {
        var everyCell = Enumerable.Range(0, BoardGrid.Size)
            .SelectMany(r => Enumerable.Range(0, BoardGrid.Size).Select(c => new Coordinate(r, c)));
        var context = AchievementContexts.New(cellsFiredAt: everyCell);

        Assert.True(HeartRule.IsUnlocked(context));
        Assert.True(BowTieRule.IsUnlocked(context));
    }
}
