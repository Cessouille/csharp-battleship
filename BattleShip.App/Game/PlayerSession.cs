using System.Net.Http.Json;
using BattleShip.Models.Contracts;
using Microsoft.JSInterop;

namespace BattleShip.App.Game;

/// <summary>
/// Identifiant joueur anonyme (TICKET-14, docs/adr/0018-profil-joueur-anonyme.md) : conservé dans le
/// localStorage du navigateur, créé à la volée via <c>POST /api/players</c> au premier besoin. Un GUID plus
/// ancien que le processus API courant (redémarrage) reste valide : dans un profil dérivé, il n'existe pas
/// de « playerId inconnu », seulement un profil vide tant qu'aucune partie n'a été jouée avec lui.
/// </summary>
public sealed class PlayerSession(IJSRuntime js, HttpClient http)
{
    private const string StorageKey = "battleship-player-id";
    private Guid? _cachedPlayerId;

    public async Task<Guid> GetOrCreatePlayerIdAsync()
    {
        if (_cachedPlayerId is { } cached)
            return cached;

        var stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (stored is not null && Guid.TryParse(stored, out var parsed))
        {
            _cachedPlayerId = parsed;
            return parsed;
        }

        var response = await http.PostAsync("api/players", null);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        var playerId = profile!.PlayerId;

        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, playerId.ToString());
        _cachedPlayerId = playerId;
        return playerId;
    }
}
