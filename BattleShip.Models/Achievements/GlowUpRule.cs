using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-11 — 🦋 Glow up (docs/ticket.md TICKET-13) : victoire avec exactement un navire du joueur encore à
/// flot. Un navire coulé ne se relève jamais (<see cref="OwnShipView.IsSunk"/> ne redevient pas false), donc
/// « un seul restant à la fin » équivaut à « être descendue à un seul navire » (voir docs/ticket.md).
/// </summary>
public sealed class GlowUpRule : IAchievementRule
{
    public AchievementId Id => AchievementId.GlowUp;

    public bool IsUnlocked(AchievementContext context) =>
        context.Winner == PlayerId.Human && context.OwnFleet.Count(s => !s.IsSunk) == 1;
}
