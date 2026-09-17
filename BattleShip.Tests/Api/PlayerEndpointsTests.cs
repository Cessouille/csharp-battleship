using System.Net;
using System.Net.Http.Json;
using BattleShip.Models.Achievements;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

/// <summary>
/// TICKET-14 : profil joueur anonyme, dérivé des parties créées avec un playerId (docs/adr/0018-profil-joueur-anonyme.md).
/// Aucun profil n'est jamais « inconnu » au sens 404 : c'est une projection sur les parties existantes, bien
/// définie (vide) pour tout GUID syntaxiquement valide qui n'a encore joué aucune partie.
/// </summary>
public class PlayerEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostPlayers_Returns201WithEmptyProfile()
    {
        var response = await _client.PostAsync("/api/players", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        Assert.NotNull(profile);
        Assert.NotEqual(Guid.Empty, profile!.PlayerId);
        Assert.Equal(0, profile.HardVictories);
        Assert.Empty(profile.Achievements);
    }

    [Fact]
    public async Task PostPlayers_ReturnsADifferentIdEachTime()
    {
        var first = await (await _client.PostAsync("/api/players", null)).Content.ReadFromJsonAsync<PlayerProfileDto>();
        var second = await (await _client.PostAsync("/api/players", null)).Content.ReadFromJsonAsync<PlayerProfileDto>();

        Assert.NotEqual(first!.PlayerId, second!.PlayerId);
    }

    [Fact]
    public async Task GetPlayer_WithNoGames_ReturnsEmptyProfile()
    {
        var playerId = Guid.NewGuid(); // jamais utilisé pour créer de partie

        var profile = await _client.GetFromJsonAsync<PlayerProfileDto>($"/api/players/{playerId}");

        Assert.Equal(playerId, profile!.PlayerId);
        Assert.Equal(0, profile.HardVictories);
        Assert.Empty(profile.Achievements);
    }

    [Fact]
    public async Task GetPlayer_MalformedGuidInRoute_Returns404()
    {
        var response = await _client.GetAsync("/api/players/pas-un-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_WithMalformedPlayerId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto(PlayerId: "pas-un-guid"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_WithUnknownPlayerId_StillSucceeds()
    {
        // Survit à un "redémarrage" de l'API du point de vue du client : un playerId jamais vu par le serveur
        // (localStorage plus ancien que le processus courant) reste un identifiant valide, pas une erreur.
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto(PlayerId: Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static readonly ShipPlacementDto[] TwoRowPlacements =
    [
        new(nameof(ShipKind.PorteAvions), 0, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Croiseur), 0, 5, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.ContreTorpilleur), 1, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.SousMarin), 1, 3, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Torpilleur), 1, 6, nameof(Orientation.Horizontal)),
    ];

    /// <summary>
    /// Preuve de la chaîne complète (création avec playerId → profil) sans dépendre d'une victoire, donc
    /// déterministe malgré Random.Shared dans InMemoryGameStore (voir docs/adr/0018-profil-joueur-anonyme.md) :
    /// RangeeParfaite (S-06) se débloque dès la création, par un simple placement sur 2 lignes.
    /// </summary>
    [Fact]
    public async Task CreateGame_WithTwoRowFleetAndPlayerId_ShowsRangeeParfaiteInTheProfile()
    {
        var playerId = Guid.NewGuid();

        await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements, PlayerId: playerId.ToString()));

        var profile = await _client.GetFromJsonAsync<PlayerProfileDto>($"/api/players/{playerId}");
        Assert.Contains(nameof(AchievementId.RangeeParfaite), profile!.Achievements);
    }

    [Fact]
    public async Task TwoPlayers_EachSeeOnlyTheirOwnGames()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements, PlayerId: playerA.ToString()));
        // playerB ne joue aucune partie sur deux lignes : son profil ne doit pas hériter du succès de playerA.

        var profileA = await _client.GetFromJsonAsync<PlayerProfileDto>($"/api/players/{playerA}");
        var profileB = await _client.GetFromJsonAsync<PlayerProfileDto>($"/api/players/{playerB}");

        Assert.Contains(nameof(AchievementId.RangeeParfaite), profileA!.Achievements);
        Assert.Empty(profileB!.Achievements);
    }
}
