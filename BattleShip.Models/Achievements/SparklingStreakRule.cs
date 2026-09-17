using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-04 — 💫 Série étincelante (docs/ticket.md TICKET-13) : 5 résolutions de tir du joueur consécutives
/// (Hit ou Sunk), toutes actions confondues (tir, salve, arme), dans l'ordre du journal. Un tour sans tir
/// (scan) ne casse rien : il ne contribue aucune résolution à la concaténation. Les cases vides traversées
/// par une torpille sont résolues en Miss (<see cref="Board.ReceiveWeapon"/>) et cassent donc la série,
/// arbitrage assumé (voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class SparklingStreakRule : IAchievementRule
{
    private const int RequiredStreak = 5;

    public AchievementId Id => AchievementId.SerieEtincelante;

    public bool IsUnlocked(AchievementContext context)
    {
        var streak = 0;
        foreach (var shot in context.History.SelectMany(t => t.PlayerShots))
        {
            streak = shot.Outcome == ShotOutcome.Miss ? 0 : streak + 1;
            if (streak >= RequiredStreak)
                return true;
        }

        return false;
    }
}
