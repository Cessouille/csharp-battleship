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
            var result = store.TryCreate(
                request.ToGameOptions(),
                request.Placements?.Select(p => p.ToPlacementSpec()).ToList(),
                request.ToPlayerId());
            return result switch
            {
                CreateGameResult.Created created => created.Game.Locked(() =>
                    Results.Created($"/api/games/{created.Game.Id}", created.Game.ToCreateGameResponseDto())),
                CreateGameResult.Rejected rejected => ToConflict(rejected.Reason),
                _ => Results.Problem("Résultat de création de partie inattendu.")
            };
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

        // Voir docs/adr/0019-mini-jeu-de-precision.md : point d'entrée unique pour résoudre un défi de timing,
        // quelle que soit l'action (REST ou gRPC-Web) qui l'a ouvert. Pas de corps JSON : challengeId suffit,
        // le résultat se calcule uniquement à partir de l'horloge serveur (jamais déclaré par le client).
        group.MapPost("/{gameId:guid}/challenges/{challengeId:guid}/resolve", (Guid gameId, Guid challengeId, InMemoryGameStore store) =>
            PlayMove(store, gameId, game => game.ResolveChallenge(challengeId)));
    }

    // PlayHumanShot/Salvo/Weapon/ResolveChallenge verrouillent déjà chacun à eux seuls, mais Locked est réentrant :
    // ce bloc englobe aussi le mapping DTO qui suit, pour qu'aucune autre requête sur ce gameId ne s'intercale
    // entre le coup et la lecture de son résultat.
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
            // Tour terminé (avec ou sans défi de timing entre-temps) : 200, forme inchangée pour tout appelant existant.
            MoveResult.Accepted accepted => Results.Ok(game.ToTurnResultDto(accepted.Turn)),
            // Tour suspendu en attendant la résolution d'un défi (voir docs/adr/0019-mini-jeu-de-precision.md) :
            // 202, ce n'est pas une erreur, juste un résultat pas encore final. État complet (pas seulement le
            // défi) pour que les cases déjà résolues plus tôt dans un même lot (salve, arme) restent visibles
            // côté client sans attendre la fin du tour entier.
            MoveResult.AwaitingChallenge => Results.Accepted(value: game.ToGameStateDto()),
            MoveResult.Rejected rejected => ToConflict(rejected.Reason),
            _ => Results.Problem("Résultat de coup inattendu.")
        };

    private static IResult ToConflict(MoveRejectionReason reason) =>
        // OutOfGrid est déjà rejeté en amont par FluentValidation ; le contrôle refait dans Game.PlayHumanShot
        // est une défense en profondeur, pas un chemin normalement atteint depuis cet endpoint.
        Results.Conflict(new { code = reason.ToString() });

    private static IResult ToConflict(FleetPlacementRejectionReason reason) =>
        Results.Conflict(new { code = reason.ToString() });
}
