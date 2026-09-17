using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Achievements;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

/// <summary>
/// TICKET-12 (docs/adr/0017-systeme-de-succes.md) : le champ Achievements traverse les 3 réponses REST
/// (création, GET, réponse de tir) et la réponse gRPC, sans jamais rester figé. Porté par une flotte sur
/// deux lignes (S-06, RangeeParfaite) : seul succès obtenable sans tirer, donc déterministe de bout en bout
/// par de simples Placements — pas besoin de contrôler l'IA ni Random.Shared.
/// </summary>
public class AchievementsContractTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Battleship.BattleshipClient _grpcClient = ApiClients.CreateGrpcClient(factory);

    /// <summary>17 cases sur les lignes 0 et 1 seulement.</summary>
    private static readonly ShipPlacementDto[] TwoRowPlacements =
    [
        new(nameof(ShipKind.PorteAvions), 0, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Croiseur), 0, 5, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.ContreTorpilleur), 1, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.SousMarin), 1, 3, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Torpilleur), 1, 6, nameof(Orientation.Horizontal)),
    ];

    /// <summary>Mêmes navires, répartis sur les lignes 0, 2 et 4.</summary>
    private static readonly ShipPlacementDto[] ThreeRowPlacements =
    [
        new(nameof(ShipKind.PorteAvions), 0, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Croiseur), 2, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.ContreTorpilleur), 4, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.SousMarin), 4, 4, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Torpilleur), 4, 8, nameof(Orientation.Horizontal)),
    ];

    [Fact]
    public async Task CreateGame_WithTwoRowFleet_ReturnsRangeeParfaite()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements));

        Assert.Contains(nameof(AchievementId.RangeeParfaite), game.Achievements);
    }

    [Fact]
    public async Task CreateGame_WithThreeRowFleet_ReturnsNoAchievement()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: ThreeRowPlacements));

        Assert.Empty(game.Achievements);
    }

    [Fact]
    public async Task GetGameState_Rest_ReturnsTheSameAchievements()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements));

        var state = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");

        Assert.Equal(game.Achievements, state!.Achievements);
    }

    [Fact]
    public async Task GetGameState_Grpc_ReturnsTheSameAchievements()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements));

        var reply = await _grpcClient.GetGameStateAsync(new GetGameStateRequest { GameId = game.GameId.ToString() });

        Assert.Equal(game.Achievements, reply.Achievements);
    }

    [Fact]
    public async Task AfterAShot_TurnResult_StillCarriesAchievements()
    {
        // Ce test est celui qui détecte un oubli dans le bloc `with` de Game.razor : sans le champ Achievements
        // dans TurnResultDto (ou son mapping), un succès débloqué au placement disparaîtrait de l'affichage dès
        // le premier tir REST, alors qu'il resterait visible après un scan/une salve (chemins gRPC).
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: TwoRowPlacements));

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(9, 9));
        var turn = await response.Content.ReadFromJsonAsync<TurnResultDto>();

        Assert.Contains(nameof(AchievementId.RangeeParfaite), turn!.Achievements);
    }
}
