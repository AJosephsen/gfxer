using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace StrategyGame.McpServer;

public static class McpHostSetup
{
    public static void ConfigureLogging(HostApplicationBuilder builder)
    {
        // MCP stdio transport owns stdout — redirect all console logs to stderr.
        // This is the pattern recommended by the MCP C# SDK getting-started docs.
        // For in-protocol logging (notifications/message with proper log levels),
        // tool methods can use context.Server.AsClientLoggerProvider() directly.
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }
}