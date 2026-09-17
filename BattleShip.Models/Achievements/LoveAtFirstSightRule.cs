using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// S-07 — 💘 Coup de foudre (docs/ticket.md TICKET-13) : le tout premier tir du joueur de la partie (le
/// premier ShotResolution rencontré en parcourant l'historique) touche ou coule. Un scan préalable ne
/// disqualifie pas : il ne contribue aucune résolution. Une torpille en première action traverse d'abord ses
/// cases vides, résolues en Miss : le succès devient alors quasi inaccessible par ce chemin, arbitrage assumé
/// (voir docs/adr/0017-systeme-de-succes.md).
/// </summary>
public sealed class LoveAtFirstSightRule : IAchievementRule
{
    public AchievementId Id => AchievementId.CoupDeFoudre;

    public bool IsUnlocked(AchievementContext context)
    {
        var firstShot = context.History.SelectMany(t => t.PlayerShots).FirstOrDefault();
        return firstShot is not null && firstShot.Outcome != ShotOutcome.Miss;
    }
}
