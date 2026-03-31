using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StrategyGame.McpServer;

public static class McpHostSetup
{
    public static void ConfigureLogging(HostApplicationBuilder builder)
    {
        // MCP stdio transport owns stdout; redirect logs to stderr so VS Code surfaces
        // them in the Output panel without corrupting the JSON-RPC protocol stream.
        // VS Code labels all server stderr as [warning] — this is a VS Code limitation,
        // not a server issue. The alternative (notifications/message) causes a circular
        // dependency between McpServer and ILoggerFactory and prevents startup.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new StderrLoggerProvider());
    }
}

file sealed class StderrLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new StderrLogger(categoryName);
    public void Dispose() { }
}

file sealed class StderrLogger(string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        Console.Error.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [{logLevel,-11}] {category}: {formatter(state, exception)}");
        if (exception is not null)
            Console.Error.WriteLine(exception);
    }
}