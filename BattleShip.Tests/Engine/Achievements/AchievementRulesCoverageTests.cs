using BattleShip.Models.Achievements;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// Catalogue intra-partie (TICKET-13) au complet depuis ce commit : vérifie que chaque AchievementId, hormis
/// ReineDuDifficile (S-03, inter-parties — voir PlayerProfile), a exactement une IAchievementRule dans
/// AchievementRules.All. Détecte un succès ajouté à l'enum sans règle, ou deux règles au même Id.
/// </summary>
public class AchievementRulesCoverageTests
{
    [Fact]
    public void EveryIntraGameAchievementId_HasExactlyOneRule()
    {
        var intraGameIds = Enum.GetValues<AchievementId>()
            .Where(id => id != AchievementId.ReineDuDifficile)
            .ToList();
        var ruleIds = AchievementRules.All.Select(r => r.Id).ToList();

        Assert.Equal(ruleIds.Distinct().Count(), ruleIds.Count);
        Assert.Equal(intraGameIds.OrderBy(i => i), ruleIds.OrderBy(i => i));
    }
}
