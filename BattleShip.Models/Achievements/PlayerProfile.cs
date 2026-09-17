using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>
/// Instantané anonymisé d'une partie, suffisant pour calculer un profil joueur sans jamais passer un
/// <see cref="Game"/> vivant hors de son verrou (voir docs/adr/0018-profil-joueur-anonyme.md). Construit sous
/// <c>Game.Locked</c>, comme tout autre mapping DTO/proto de ce dépôt.
/// </summary>
public sealed record GameOutcome(Guid GameId, GameStatus Status, PlayerId? Winner, AiDifficulty Difficulty, IReadOnlyList<AchievementId> Achievements)
{
    public static GameOutcome From(Game game) => new(game.Id, game.Status, game.Winner, game.Options.Difficulty, game.Achievements);
}

/// <summary>
/// Profil joueur (TICKET-14) : une projection pure sur les parties d'un joueur, jamais un état accumulé.
/// Aucune synchronisation à faire à chaque coup, aucun risque de double-comptage — recalculée à chaque
/// lecture à partir des GameOutcome des parties connues pour ce joueur (voir docs/adr/0018-profil-joueur-anonyme.md).
/// </summary>
public sealed record PlayerProfile(int HardVictories, IReadOnlyList<AchievementId> Achievements)
{
    private const int ReineDuDifficileThreshold = 3;

    public static PlayerProfile Project(IEnumerable<GameOutcome> outcomes)
    {
        var games = outcomes.DistinctBy(o => o.GameId).ToList();

        var hardVictories = games.Count(g => g.Status == GameStatus.Finished && g.Winner == PlayerId.Human && g.Difficulty == AiDifficulty.Hard);

        var achievements = games.SelectMany(g => g.Achievements).Distinct().ToList();
        if (hardVictories >= ReineDuDifficileThreshold)
            achievements.Add(AchievementId.ReineDuDifficile);

        return new PlayerProfile(hardVictories, achievements);
    }
}
