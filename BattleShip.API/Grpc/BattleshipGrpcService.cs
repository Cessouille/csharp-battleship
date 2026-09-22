using BattleShip.API.Storage;
using BattleShip.Grpc;
using BattleShip.Models.Domain;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public sealed class BattleshipGrpcService(
    InMemoryGameStore store,
    IValidator<GetGameStateRequest> getGameStateValidator,
    IValidator<ScanZoneRequest> scanZoneValidator,
    IValidator<PlaySalvoRequest> playSalvoValidator)
    : Battleship.BattleshipBase
{
    public override async Task<GameStateReply> GetGameState(GetGameStateRequest request, ServerCallContext context)
    {
        await ValidateOrThrowAsync(getGameStateValidator, request, context);
        var game = FindOrThrow(request.GameId);

        return game.Locked(() => GameStateMapper.ToGameStateReply(game));
    }

    public override async Task<ScanZoneReply> ScanZone(ScanZoneRequest request, ServerCallContext context)
    {
        await ValidateOrThrowAsync(scanZoneValidator, request, context);
        var game = FindOrThrow(request.GameId);

        return game.Locked(() => game.PlayHumanScan(new Coordinate(request.Row, request.Column)) switch
        {
            MoveResult.Accepted accepted => GameStateMapper.ToScanZoneReply(game, accepted.Turn),
            // Voir docs/adr/0019-mini-jeu-de-precision.md : le scan a déjà eu lieu (jamais gaté, toujours le dernier
            // de ComputerBoard.ScansReceived à ce point) mais la riposte de l'ordinateur attend un défi de défense ;
            // State.PendingChallenge porte l'information, la résolution reste REST quel que soit le transport d'origine.
            MoveResult.AwaitingChallenge => GameStateMapper.ToScanZoneReply(game, game.ComputerBoard.ScansReceived[^1]),
            MoveResult.Rejected rejected => throw ToRpcException(rejected.Reason),
            _ => throw new RpcException(new Status(StatusCode.Internal, "Résultat de coup inattendu."))
        });
    }

    /// <summary>Transport alternatif au POST /salvos REST (docs/adr/0014-salvo-grpc-web.md) : les deux chemins coexistent.</summary>
    public override async Task<PlaySalvoReply> PlaySalvo(PlaySalvoRequest request, ServerCallContext context)
    {
        await ValidateOrThrowAsync(playSalvoValidator, request, context);
        var game = FindOrThrow(request.GameId);

        return game.Locked(() =>
            game.PlayHumanSalvo(request.Shots.Select(s => new Coordinate(s.Row, s.Column)).ToList()) switch
            {
                MoveResult.Accepted accepted => GameStateMapper.ToPlaySalvoReply(game, accepted.Turn),
                // Voir docs/adr/0019-mini-jeu-de-precision.md : un défi (attaque ou défense) est ouvert, la salve
                // n'est pas encore résolue ; State.PendingChallenge porte l'information, résolution toujours REST.
                MoveResult.AwaitingChallenge => GameStateMapper.ToPlaySalvoReply(game),
                MoveResult.Rejected rejected => throw ToRpcException(rejected.Reason),
                _ => throw new RpcException(new Status(StatusCode.Internal, "Résultat de coup inattendu."))
            });
    }

    private static async Task ValidateOrThrowAsync<T>(IValidator<T> validator, T request, ServerCallContext context)
    {
        var check = await validator.ValidateAsync(request, context.CancellationToken);
        if (!check.IsValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, check.ToString()));
    }

    private Game FindOrThrow(string gameId) =>
        store.Find(Guid.Parse(gameId))
        ?? throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable."));

    /// <summary>
    /// Le détail porte le nom du motif (même valeur que le champ <c>code</c> des 409 REST) pour que le client
    /// traduise les refus de la même façon quel que soit le transport. OutOfGrid est normalement intercepté par
    /// la validation en amont : le contrôle du domaine n'est qu'une défense en profondeur.
    /// </summary>
    private static RpcException ToRpcException(MoveRejectionReason reason) =>
        new(new Status(
            reason == MoveRejectionReason.OutOfGrid ? StatusCode.InvalidArgument : StatusCode.FailedPrecondition,
            reason.ToString()));
}

public sealed class GetGameStateRequestValidator : AbstractValidator<GetGameStateRequest>
{
    public GetGameStateRequestValidator() =>
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
}

public sealed class ScanZoneRequestValidator : AbstractValidator<ScanZoneRequest>
{
    public ScanZoneRequestValidator()
    {
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
        RuleFor(r => r.Row).InclusiveBetween(0, BoardGrid.Size - RadarRules.ZoneSize)
            .WithMessage($"row doit être compris entre 0 et {BoardGrid.Size - RadarRules.ZoneSize} (la zone {RadarRules.ZoneSize}x{RadarRules.ZoneSize} doit tenir dans la grille).");
        RuleFor(r => r.Column).InclusiveBetween(0, BoardGrid.Size - RadarRules.ZoneSize)
            .WithMessage($"column doit être compris entre 0 et {BoardGrid.Size - RadarRules.ZoneSize} (la zone {RadarRules.ZoneSize}x{RadarRules.ZoneSize} doit tenir dans la grille).");
    }
}

/// <summary>
/// Contrôles de forme uniquement, comme SalvoRequestDtoValidator côté REST : la taille exacte attendue dépend de
/// l'état de la partie et reste vérifiée par Game.PlayHumanSalvo (FailedPrecondition, pas une erreur de validation).
/// </summary>
public sealed class PlaySalvoRequestValidator : AbstractValidator<PlaySalvoRequest>
{
    public PlaySalvoRequestValidator()
    {
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
        RuleFor(r => r.Shots).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(shots => shots.Count <= Fleet.Standard.Count)
            .WithMessage($"Une salve compte au plus {Fleet.Standard.Count} tirs.")
            .Must(shots => shots.Select(s => (s.Row, s.Column)).Distinct().Count() == shots.Count)
            .WithMessage("Une salve ne peut pas viser deux fois la même case.");
        RuleForEach(r => r.Shots).ChildRules(shot =>
        {
            shot.RuleFor(s => s.Row).InclusiveBetween(0, BoardGrid.Size - 1);
            shot.RuleFor(s => s.Column).InclusiveBetween(0, BoardGrid.Size - 1);
        });
    }
}
