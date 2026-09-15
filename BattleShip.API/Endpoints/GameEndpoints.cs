using BattleShip.API.Storage;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games");

        group.MapPost("", (InMemoryGameStore store) =>
        {
            var game = store.Create();
            return Results.Created($"/api/games/{game.Id}", game.ToCreateGameResponseDto());
        });

        group.MapGet("/{gameId:guid}", (Guid gameId, InMemoryGameStore store) =>
            store.Find(gameId) is { } game
                ? Results.Ok(game.ToGameStateDto())
                : Results.NotFound());

        group.MapPost("/{gameId:guid}/shots", (Guid gameId, ShotRequestDto request, InMemoryGameStore store) =>
        {
            var game = store.Find(gameId);
            if (game is null)
                return Results.NotFound();

            var result = game.PlayHumanShot(new Coordinate(request.Row, request.Column));
            return result switch
            {
                MoveResult.Accepted accepted => Results.Ok(game.ToTurnResultDto(accepted.Turn)),
                MoveResult.Rejected rejected => ToConflict(rejected.Reason),
                _ => Results.Problem("Résultat de coup inattendu.")
            };
        }).AddEndpointFilter<ValidationFilter<ShotRequestDto>>();
    }

    private static IResult ToConflict(MoveRejectionReason reason) =>
        // OutOfGrid est déjà rejeté en amont par FluentValidation ; le contrôle refait dans Game.PlayHumanShot
        // est une défense en profondeur, pas un chemin normalement atteint depuis cet endpoint.
        Results.Conflict(new { code = reason.ToString() });
}
