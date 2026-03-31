using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using McpSdk = ModelContextProtocol.Server;

namespace StrategyGame.McpServer;

public static class McpHostSetup
{
    public static void ConfigureLogging(HostApplicationBuilder builder)
    {
        // MCP stdio transport owns stdout. Route all logs via the MCP notifications/message
        // protocol so VS Code surfaces them at the correct level (info, warning, etc.)
        // in the Output panel. Messages sent before a client connects are silently dropped.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // McpServer.AsClientLoggerProvider() returns an ILoggerProvider that translates
        // ILogger calls into notifications/message notifications with proper log levels.
        builder.Services.AddSingleton<ILoggerProvider>(
            sp => sp.GetRequiredService<McpSdk.McpServer>().AsClientLoggerProvider());

        // Declare the logging capability so connecting clients know we emit log notifications.
        builder.Services.PostConfigure<McpSdk.McpServerOptions>(opts =>
        {
            opts.Capabilities ??= new ServerCapabilities();
            opts.Capabilities.Logging = new LoggingCapability();
        });
    }
}
