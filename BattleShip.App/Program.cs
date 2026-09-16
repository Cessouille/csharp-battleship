using BattleShip.Grpc;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BattleShip.App;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "https://localhost:8080/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

builder.Services.AddSingleton(_ =>
{
    var grpcWebHttpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
    return GrpcChannel.ForAddress(apiBaseAddress, new GrpcChannelOptions { HttpClient = grpcWebHttpClient });
});
builder.Services.AddSingleton(sp => new Battleship.BattleshipClient(sp.GetRequiredService<GrpcChannel>()));

await builder.Build().RunAsync();
