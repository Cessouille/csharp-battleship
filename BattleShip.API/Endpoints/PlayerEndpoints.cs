using BattleShip.API.Storage;
using BattleShip.Models.Achievements;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Endpoints;

/// <summary>
/// TICKET-14 : profil joueur anonyme, dérivé des parties créées avec un playerId (docs/adr/0018-profil-joueur-anonyme.md).
/// Aucun état de profil n'est stocké : POST ne fait que fabriquer un identifiant, GET recalcule la projection
/// à chaque appel à partir de InMemoryGameStore.FindByPlayer.
/// </summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/players");

        // Aucune dépendance au store : un playerId n'existe nulle part avant d'avoir servi à créer une partie.
        group.MapPost("", () =>
        {
            var playerId = Guid.NewGuid();
            return Results.Created($"/api/players/{playerId}", new PlayerProfileDto(playerId, 0, []));
        });

        group.MapGet("/{playerId:guid}", (Guid playerId, InMemoryGameStore store) =>
        {
            var outcomes = store.FindByPlayer(playerId).Select(game => game.Locked(() => GameOutcome.From(game)));
            var profile = PlayerProfile.Project(outcomes);
            return Results.Ok(new PlayerProfileDto(playerId, profile.HardVictories, profile.Achievements.Select(a => a.ToString()).ToList()));
        });
    }
}
