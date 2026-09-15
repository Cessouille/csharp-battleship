using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class WeaponEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<CreateGameResponseDto> CreateWeaponsGameAsync() =>
        _client.CreateGameAsync(new CreateGameRequestDto(SpecialWeapons: true));

    private static async Task<string?> ConflictCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString();
    }

    [Fact]
    public async Task PostGames_WithWeapons_ExposesBothArsenals()
    {
        var game = await CreateWeaponsGameAsync();

        Assert.True(game.Options.SpecialWeapons);
        Assert.Equal(new ArsenalDto(WeaponRules.TorpedoesPerGame, WeaponRules.AirStrikesPerGame), game.Actions.Arsenal);
        Assert.Equal(game.Actions.Arsenal, game.Actions.OpponentArsenal);
    }

    [Fact]
    public async Task PostTorpedoes_Returns200_ThenSecondTorpedo_Returns409NoAmmoLeft()
    {
        var game = await CreateWeaponsGameAsync();

        var first = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/torpedoes", new TorpedoRequestDto("Left", 0));
        var second = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/torpedoes", new TorpedoRequestDto("Top", 9));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var turn = await first.Content.ReadFromJsonAsync<TurnResultDto>();
        Assert.Equal(nameof(WeaponKind.Torpedo), turn!.PlayerWeapon);
        Assert.NotEmpty(turn.PlayerShots);
        Assert.Equal(0, turn.Actions.Arsenal.Torpedoes);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.NoAmmoLeft), await ConflictCodeAsync(second));
    }

    [Theory]
    [InlineData("Diagonal", 0)]
    [InlineData("left", 0)]
    [InlineData("Left", 10)]
    [InlineData("Left", -1)]
    public async Task PostTorpedoes_InvalidRequest_Returns400(string from, int lane)
    {
        var game = await CreateWeaponsGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/torpedoes", new TorpedoRequestDto(from, lane));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAirStrikes_Returns200()
    {
        var game = await CreateWeaponsGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/airstrikes", new AirStrikeRequestDto(4, 4, "Vertical"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turn = await response.Content.ReadFromJsonAsync<TurnResultDto>();
        Assert.Equal(nameof(WeaponKind.AirStrike), turn!.PlayerWeapon);
    }

    [Theory]
    [InlineData(0, 8, "Horizontal")]
    [InlineData(8, 0, "Vertical")]
    [InlineData(0, 0, "Diagonal")]
    [InlineData(-1, 0, "Horizontal")]
    public async Task PostAirStrikes_InvalidRequest_Returns400(int row, int column, string orientation)
    {
        var game = await CreateWeaponsGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/airstrikes", new AirStrikeRequestDto(row, column, orientation));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTorpedoes_OnGameWithoutWeapons_Returns409WeaponsDisabled()
    {
        var game = await _client.CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/torpedoes", new TorpedoRequestDto("Left", 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WeaponsDisabled), await ConflictCodeAsync(response));
    }

    [Fact]
    public async Task PostAirStrikes_OnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/games/{Guid.NewGuid()}/airstrikes", new AirStrikeRequestDto(0, 0, "Horizontal"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
