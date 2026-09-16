using System.Collections.Concurrent;
using BattleShip.Models.Domain;

namespace BattleShip.API.Storage;

/// <summary>
/// Stockage en mémoire, sans persistance ni authentification par joueur : décision de périmètre assumée
/// pour ce socle (voir docs/adr/0006-stockage-etat-partie.md), pas un oubli. L'état est perdu au redémarrage
/// de l'API et quiconque détient un gameId peut y jouer.
/// </summary>
public sealed class InMemoryGameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    /// <summary>
    /// Sans placements (null/vide) : flotte humaine aléatoire, comme avant. Avec placements : délègue à
    /// <see cref="Game.TryCreateManual"/> (voir docs/adr/0013-placement-manuel.md) et ne stocke la partie que
    /// si elle a effectivement été créée.
    /// </summary>
    public CreateGameResult TryCreate(GameOptions options, IReadOnlyList<(ShipKind, Coordinate, Orientation)>? placements)
    {
        var result = placements is { Count: > 0 }
            ? Game.TryCreateManual(Guid.NewGuid(), placements, Random.Shared, options)
            : new CreateGameResult.Created(Game.CreateRandom(Guid.NewGuid(), Random.Shared, options));

        if (result is CreateGameResult.Created created)
            _games[created.Game.Id] = created.Game;

        return result;
    }

    public Game? Find(Guid gameId) => _games.GetValueOrDefault(gameId);
}
