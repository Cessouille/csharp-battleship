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

    public Game Create(GameOptions options)
    {
        var game = Game.CreateRandom(Guid.NewGuid(), Random.Shared, options);
        _games[game.Id] = game;
        return game;
    }

    public Game? Find(Guid gameId) => _games.GetValueOrDefault(gameId);
}
