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

        // Corps JSON obligatoire ({} = partie classique) : une requête sans Content-Type JSON n'est pas routée vers
        // cet endpoint par ASP.NET Core, même avec un paramètre nullable (vérifié, voir REVUE-IA.md).
        group.MapPost("", (CreateGameRequestDto request, InMemoryGameStore store) =>
        {
            var game = store.Create(request.ToGameOptions());
            return game.Locked(() => Results.Created($"/api/games/{game.Id}", game.ToCreateGameResponseDto()));
        }).AddEndpointFilter<ValidationFilter<CreateGameRequestDto>>();

        group.MapGet("/{gameId:guid}", (Guid gameId, InMemoryGameStore store) =>
            store.Find(gameId) is { } game
                ? Results.Ok(game.Locked(() => game.ToGameStateDto()))
                : Results.NotFound());

        group.MapPost("/{gameId:guid}/shots", (Guid gameId, ShotRequestDto request, InMemoryGameStore store) =>
            PlayMove(store, gameId, game => game.PlayHumanShot(new Coordinate(request.Row, request.Column))))
            .AddEndpointFilter<ValidationFilter<ShotRequestDto>>();

        group.MapPost("/{gameId:guid}/salvos", (Guid gameId, SalvoRequestDto request, InMemoryGameStore store) =>
            PlayMove(store, gameId, game =>
                game.PlayHumanSalvo(request.Shots.Select(s => new Coordinate(s.Row, s.Column)).ToList())))
            .AddEndpointFilter<ValidationFilter<SalvoRequestDto>>();

        group.MapPost("/{gameId:guid}/torpedoes", (Guid gameId, TorpedoRequestDto request, InMemoryGameStore store) =>
            PlayMove(store, gameId, game => game.PlayHumanWeapon(request.ToWeaponAction())))
            .AddEndpointFilter<ValidationFilter<TorpedoRequestDto>>();

        group.MapPost("/{gameId:guid}/airstrikes", (Guid gameId, AirStrikeRequestDto request, InMemoryGameStore store) =>
            PlayMove(store, gameId, game => game.PlayHumanWeapon(request.ToWeaponAction())))
            .AddEndpointFilter<ValidationFilter<AirStrikeRequestDto>>();
    }

    // PlayHumanShot/Salvo/Weapon verrouillent déjà chacun à eux seuls, mais Locked est réentrant : ce bloc englobe
    // aussi le mapping DTO qui suit, pour qu'aucune autre requête sur ce gameId ne s'intercale entre le coup et
    // la lecture de son résultat.
    private static IResult PlayMove(InMemoryGameStore store, Guid gameId, Func<Game, MoveResult> move)
    {
        var game = store.Find(gameId);
        return game is null
            ? Results.NotFound()
            : game.Locked(() => ToTurnResponse(game, move(game)));
    }

    private static IResult ToTurnResponse(Game game, MoveResult result) =>
        result switch
        {
            MoveResult.Accepted accepted => Results.Ok(game.ToTurnResultDto(accepted.Turn)),
            MoveResult.Rejected rejected => ToConflict(rejected.Reason),
            _ => Results.Problem("Résultat de coup inattendu.")
        };

    private static IResult ToConflict(MoveRejectionReason reason) =>
        // OutOfGrid est déjà rejeté en amont par FluentValidation ; le contrôle refait dans Game.PlayHumanShot
        // est une défense en profondeur, pas un chemin normalement atteint depuis cet endpoint.
        Results.Conflict(new { code = reason.ToString() });
}
