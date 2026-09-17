using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-10 — ✨ Baguette magique (docs/ticket.md TICKET-13) : un tour où le joueur a utilisé une arme spéciale
/// contient une résolution Sunk. Le coup de grâce suffit — le navire peut avoir déjà été endommagé par un
/// tir classique lors d'un tour précédent (voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class MagicWandRule : IAchievementRule
{
    public AchievementId Id => AchievementId.BaguetteMagique;

    public bool IsUnlocked(AchievementContext context) =>
        context.History.Any(t => t.PlayerWeapon is not null && t.PlayerShots.Any(s => s.Outcome == ShotOutcome.Sunk));
}
