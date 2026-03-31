using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrategyGame.McpServer;
using StrategyGame.Core.Catalog;
using StrategyGame.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

McpHostSetup.ConfigureLogging(builder);

builder.Services.AddSingleton<CardCatalog>();
builder.Services.AddSingleton<IGameRepository, GameRepository>();
builder.Services.AddSingleton<GameService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
