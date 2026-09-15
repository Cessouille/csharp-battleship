using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.TestData;

/// <summary>Clients REST et gRPC-Web branchés sur l'API hébergée en mémoire par WebApplicationFactory.</summary>
public static class ApiClients
{
    public static Battleship.BattleshipClient CreateGrpcClient(WebApplicationFactory<Program> factory)
    {
        var httpClient = factory.CreateDefaultClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
        var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions { HttpClient = httpClient });
        return new Battleship.BattleshipClient(channel);
    }

    public static async Task<CreateGameResponseDto> CreateGameAsync(this HttpClient client, CreateGameRequestDto? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/games", request ?? new CreateGameRequestDto());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateGameResponseDto>())!;
    }
}
