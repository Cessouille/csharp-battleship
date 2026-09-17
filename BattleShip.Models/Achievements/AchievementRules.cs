namespace BattleShip.Models.Achievements;

/// <summary>
/// Toutes les règles de succès intra-partie, dans leur ordre d'affichage (voir docs/adr/0017-systeme-de-succes.md).
/// Reine du difficile (S-03, TICKET-14) n'y figure pas : elle se calcule sur plusieurs parties
/// (<see cref="PlayerProfile"/>), pas sur l'état d'une seule <see cref="Game"/>.
/// </summary>
public static class AchievementRules
{
    public static IReadOnlyList<IAchievementRule> All { get; } =
    [
        new PerfectRowRule(),
        new NoScratchRule(),
        new FourCornersRule(),
        new FullKitRule(),
        new GlowUpRule(),
        new SparklingStreakRule(),
        new LoveAtFirstSightRule(),
        new MagicWandRule(),
        new ShotPatternRule(AchievementId.CoeurDeTirs, ShotPatterns.Heart),
        new ShotPatternRule(AchievementId.NoeudPapillon, ShotPatterns.BowTie)
    ];
}
