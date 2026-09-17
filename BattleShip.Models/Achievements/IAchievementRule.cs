namespace BattleShip.Models.Achievements;

/// <summary>
/// Une règle de succès, indépendante de toutes les autres (voir docs/adr/0017-systeme-de-succes.md). Pure et
/// sans état : deux appels avec le même contexte renvoient le même résultat. Ajouter un succès, c'est ajouter
/// une classe et une entrée dans <see cref="AchievementRules.All"/>, jamais toucher une règle existante.
/// </summary>
public interface IAchievementRule
{
    AchievementId Id { get; }

    bool IsUnlocked(AchievementContext context);
}
