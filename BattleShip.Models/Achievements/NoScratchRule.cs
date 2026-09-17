using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-02 — 🛡️ Sans une égratignure (docs/ticket.md TICKET-13) : victoire sans qu'aucun navire du joueur ne
/// soit coulé. « Perdre un navire » est interprété comme « coulé » — Ship n'expose pas les touches partielles,
/// donc « aucune case touchée » n'est pas observable ici (arbitrage, voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class NoScratchRule : IAchievementRule
{
    public AchievementId Id => AchievementId.SansUneEgratignure;

    public bool IsUnlocked(AchievementContext context) =>
        context.Winner == PlayerId.Human
        && context.OwnFleet.Count > 0
        && context.OwnFleet.All(s => !s.IsSunk);
}
