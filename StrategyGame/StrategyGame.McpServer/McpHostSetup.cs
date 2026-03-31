using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StrategyGame.McpServer;

public static class McpHostSetup
{
    public static void ConfigureLogging(HostApplicationBuilder builder)
    {
        // MCP stdio transport owns stdout; redirect all logs to stderr to avoid corrupting the protocol stream.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new StderrLoggerProvider());
    }
}

/// <summary>Writes log output to stderr so it doesn't interfere with the MCP stdio protocol on stdout.</summary>
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
        Console.Error.WriteLine($"[{logLevel,-11}] {category}: {formatter(state, exception)}");
        if (exception is not null)
            Console.Error.WriteLine(exception);
    }
}