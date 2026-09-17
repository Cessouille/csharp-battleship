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
    /// Index playerId → gameIds (TICKET-14) : jamais un profil séparé, juste de quoi retrouver les parties
    /// d'un joueur (voir docs/adr/0018-profil-joueur-anonyme.md). Le dictionnaire interne sert d'ensemble
    /// thread-safe (valeur ignorée) : deux créations concurrentes pour le même playerId ne s'écrasent pas.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _gameIdsByPlayer = new();

    /// <summary>
    /// Sans placements (null/vide) : flotte humaine aléatoire, comme avant. Avec placements : délègue à
    /// <see cref="Game.TryCreateManual"/> (voir docs/adr/0013-placement-manuel.md) et ne stocke la partie que
    /// si elle a effectivement été créée. <paramref name="playerId"/> absent : la partie n'est associée à
    /// aucun profil (comportement historique inchangé).
    /// </summary>
    public CreateGameResult TryCreate(GameOptions options, IReadOnlyList<(ShipKind, Coordinate, Orientation)>? placements, Guid? playerId = null)
    {
        var result = placements is { Count: > 0 }
            ? Game.TryCreateManual(Guid.NewGuid(), placements, Random.Shared, options)
            : new CreateGameResult.Created(Game.CreateRandom(Guid.NewGuid(), Random.Shared, options));

        if (result is CreateGameResult.Created created)
        {
            _games[created.Game.Id] = created.Game;
            if (playerId is { } id)
                _gameIdsByPlayer.GetOrAdd(id, _ => new ConcurrentDictionary<Guid, byte>())[created.Game.Id] = 0;
        }

        return result;
    }

    public Game? Find(Guid gameId) => _games.GetValueOrDefault(gameId);

    /// <summary>Parties créées avec ce playerId, dans un ordre non garanti — jamais persistées séparément.</summary>
    public IReadOnlyList<Game> FindByPlayer(Guid playerId) =>
        _gameIdsByPlayer.TryGetValue(playerId, out var gameIds)
            ? gameIds.Keys.Select(Find).OfType<Game>().ToList()
            : [];
}
