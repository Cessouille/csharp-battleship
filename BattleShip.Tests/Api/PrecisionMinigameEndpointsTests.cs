using System.Net;
using System.Net.Http.Json;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

/// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md. Le détail des règles de déclenchement/résolution est
/// couvert par BattleShip.Tests.Engine.PrecisionMinigameTests (horloge contrôlable) ; ici, uniquement le câblage
/// HTTP (routes, codes de statut, formes de DTO) — le plateau ordinateur n'est jamais contrôlable depuis l'API.</summary>
public class PrecisionMinigameEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostGames_WithPrecisionMinigame_ExposesOptionInResponse()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(PrecisionMinigame: true));

        Assert.True(game.Options.PrecisionMinigame);
    }

    [Fact]
    public async Task PostGames_WithoutPrecisionMinigame_DefaultsToDisabled()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto());

        Assert.False(game.Options.PrecisionMinigame);
    }

    [Fact]
    public async Task PostChallengesResolve_OnUnknownGame_Returns404()
    {
        var response = await _client.PostAsync($"/api/games/{Guid.NewGuid()}/challenges/{Guid.NewGuid()}/resolve", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostChallengesResolve_WithUnknownChallengeId_Returns409_NoChallengePending()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(PrecisionMinigame: true));

        var response = await _client.PostAsync($"/api/games/{game.GameId}/challenges/{Guid.NewGuid()}/resolve", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.NoChallengePending), await response.ConflictCodeAsync());
    }

    /// <summary>
    /// Le plateau ordinateur est toujours placé au hasard côté serveur (ADR 0013) : impossible de viser directement
    /// une case occupée depuis l'API. On tire sur des cases jusqu'à en toucher une (au plus 100, le plateau est
    /// intégralement couvert par la flotte adverse à cette limite) ; toute case déjà résolue en 200 est un raté
    /// classique, une case en 202 est celle qui a ouvert le défi cherché. Les défis de défense croisés en chemin
    /// (l'IA menace un navire humain) sont résolus au passage, quelle qu'en soit l'issue, pour ne pas bloquer la suite.
    /// </summary>
    [Fact]
    public async Task PostShots_WithMinigameEnabled_HittingAShip_Returns202_WithWellFormedTimingChallenge()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(PrecisionMinigame: true, Difficulty: "Easy"));

        for (var row = 0; row < BoardGrid.Size; row++)
        {
            for (var column = 0; column < BoardGrid.Size; column++)
            {
                var shot = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(row, column));
                if (shot.StatusCode == HttpStatusCode.OK)
                    continue; // raté (ou victoire) : rien à résoudre, on continue la recherche

                Assert.Equal(HttpStatusCode.Accepted, shot.StatusCode);
                var state = await shot.Content.ReadFromJsonAsync<GameStateDto>();
                var challenge = state?.PendingChallenge;
                Assert.NotNull(challenge);

                if (challenge!.Kind == nameof(ChallengeKind.Attack))
                {
                    AssertWellFormed(challenge);
                    return; // trouvé : le tir a bien ouvert un défi d'attaque
                }

                // Défi de défense croisé en chemin : le résoudre (résultat indifférent) pour libérer la suite de la recherche.
                var resolved = await _client.PostAsync($"/api/games/{game.GameId}/challenges/{challenge.ChallengeId}/resolve", content: null);
                Assert.True(resolved.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted);
            }
        }

        Assert.Fail("Aucune case du plateau adverse n'a déclenché de défi d'attaque en 100 tirs — la flotte occupe pourtant 17 cases.");
    }

    private static void AssertWellFormed(TimingChallengeDto challenge)
    {
        Assert.NotEqual(Guid.Empty, challenge.ChallengeId);
        Assert.InRange(challenge.ZoneStart, 0, 1);
        Assert.InRange(challenge.ZoneStart + challenge.ZoneWidth, 0, 1);
        Assert.True(challenge.PeriodMs > 0);
    }
}
