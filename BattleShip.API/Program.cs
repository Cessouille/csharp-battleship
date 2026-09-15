using BattleShip.API.Endpoints;
using BattleShip.API.Grpc;
using BattleShip.API.Storage;
using BattleShip.API.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

const string AppCorsPolicy = "AppOrigin";

builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<ShotRequestDtoValidator>();
builder.Services.AddSingleton<InMemoryGameStore>();
builder.Services.AddGrpc();
builder.Services.AddCors(options => options.AddPolicy(AppCorsPolicy, policy => policy
    .WithOrigins("https://localhost:7206", "http://localhost:5209")
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("Grpc-Status", "Grpc-Message")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(AppCorsPolicy);
app.UseGrpcWeb();

app.MapGameEndpoints();
app.MapGrpcService<BattleshipGrpcService>().EnableGrpcWeb().RequireCors(AppCorsPolicy);

app.Run();

public partial class Program;
