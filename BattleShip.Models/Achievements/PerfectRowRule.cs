namespace BattleShip.Models.Achievements;

/// <summary>
/// S-06 — 📏 Rangée parfaite (docs/ticket.md TICKET-13) : la flotte du joueur n'occupe que deux lignes
/// distinctes au plus. Les deux lignes n'ont pas besoin d'être adjacentes, et un navire vertical à cheval sur
/// elles est accepté — seul le nombre total de lignes distinctes compte. Évaluée dès la création de la
/// partie (docs/adr/0017-systeme-de-succes.md) : obtenable sans tirer un seul coup, arbitrage assumé.
/// </summary>
public sealed class PerfectRowRule : IAchievementRule
{
    public AchievementId Id => AchievementId.RangeeParfaite;

    public bool IsUnlocked(AchievementContext context) =>
        context.OwnFleet.Count > 0
        && context.OwnFleet.SelectMany(s => s.Cells).Select(c => c.Row).Distinct().Count() <= 2;
}
