using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StrategyGame.McpServer;

public static class McpHostSetup
{
    public static void ConfigureLogging(HostApplicationBuilder builder)
    {
        // MCP stdio transport owns stdout; normal console logs corrupt the protocol stream.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.None);
    }
}