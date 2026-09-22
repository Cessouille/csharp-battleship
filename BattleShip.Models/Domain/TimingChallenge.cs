namespace BattleShip.Models.Domain;

/// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md.</summary>
public enum ChallengeKind
{
    Attack,
    Defense
}

/// <summary>
/// Paramètres fixes de la barre de timing : zone rose centrée, curseur en va-et-vient continu. Partagés par le
/// serveur (qui seul décide de la réussite) et le client (qui les affiche tels quels, sans les recalculer).
/// </summary>
public static class TimingRules
{
    public const double ZoneWidth = 0.18;
    public const double ZoneStart = (1 - ZoneWidth) / 2;
    public const int PeriodMs = 900;
}

/// <summary>
/// Un défi de timing ouvert sur une case précise, en attente du signal d'arrêt du joueur. Kind détermine ce que
/// signifie « réussir » une fois résolu (voir Game.ResolveChallenge) : pour Attack, réussir applique le tir
/// normalement ; pour Defense, réussir épargne le navire.
/// </summary>
public sealed record PendingChallenge(Guid Id, ChallengeKind Kind, Coordinate Target, DateTimeOffset StartedAtUtc);

/// <summary>
/// Détermine si l'instant d'arrêt choisi par le joueur tombe dans la zone rose, à partir du seul temps écoulé
/// côté serveur (le client n'est jamais consulté sur le résultat, voir docs/adr/0019-mini-jeu-de-precision.md).
/// </summary>
public static class TimingEvaluator
{
    public static bool Succeeds(double zoneStart, double zoneWidth, int periodMs, TimeSpan elapsed)
    {
        var cycle = elapsed.TotalMilliseconds % (periodMs * 2);
        var position = cycle <= periodMs ? cycle / periodMs : (2 * periodMs - cycle) / periodMs;
        return position >= zoneStart && position <= zoneStart + zoneWidth;
    }
}
