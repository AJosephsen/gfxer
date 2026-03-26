using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrategyGame.Core.Catalog;
using StrategyGame.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<CardCatalog>();
builder.Services.AddSingleton<GameRepository>();
builder.Services.AddSingleton<GameService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
