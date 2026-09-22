using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine;

public class TimingEvaluatorTests
{
    // Zone [0.41, 0.59], période 1000 ms (un aller simple).
    private const double ZoneStart = 0.41;
    private const double ZoneWidth = 0.18;
    private const int PeriodMs = 1000;

    [Theory]
    [InlineData(500)]   // milieu de l'aller : position 0.5, en zone
    [InlineData(410)]   // bord bas exact
    [InlineData(590)]   // bord haut exact
    [InlineData(1500)]  // milieu du retour (symétrique) : position 0.5
    [InlineData(2500)]  // un cycle complet plus tard (rebond répété)
    public void Succeeds_WhenCursorPositionFallsInsideZone_ReturnsTrue(int elapsedMs)
    {
        var result = TimingEvaluator.Succeeds(ZoneStart, ZoneWidth, PeriodMs, TimeSpan.FromMilliseconds(elapsedMs));

        Assert.True(result);
    }

    [Theory]
    [InlineData(0)]     // départ, position 0
    [InlineData(409)]   // juste avant le bord bas
    [InlineData(591)]   // juste après le bord haut
    [InlineData(1000)]  // point de rebond, position 1.0
    public void Succeeds_WhenCursorPositionFallsOutsideZone_ReturnsFalse(int elapsedMs)
    {
        var result = TimingEvaluator.Succeeds(ZoneStart, ZoneWidth, PeriodMs, TimeSpan.FromMilliseconds(elapsedMs));

        Assert.False(result);
    }
}
