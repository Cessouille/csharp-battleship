using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-09 — 👛 Panoplie complète (docs/ticket.md TICKET-13) : victoire avec Radar, Salvo et armes spéciales
/// tous activés, radar et armes en plus réellement utilisés au moins une fois dans la partie. Salvo est un
/// mode figé pour toute la partie (ADR 0009), pas une action ponctuelle : il n'y a rien d'équivalent à
/// « utiliser » pour lui — asymétrie assumée (voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class FullKitRule : IAchievementRule
{
    public AchievementId Id => AchievementId.PanoplieComplete;

    public bool IsUnlocked(AchievementContext context) =>
        context.Winner == PlayerId.Human
        && context.Options.Radar
        && context.Options.ShotMode == ShotMode.Salvo
        && context.Options.SpecialWeapons
        && context.History.Any(t => t.PlayerScan is not null)
        && context.History.Any(t => t.PlayerWeapon is not null);
}
