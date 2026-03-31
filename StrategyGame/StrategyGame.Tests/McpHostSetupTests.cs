using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using StrategyGame.McpServer;
using Xunit;

namespace StrategyGame.Tests;

public sealed class McpHostSetupTests
{
    [Fact]
    public void ConfigureLogging_RegistersConsoleLoggerProvider()
    {
        var builder = Host.CreateApplicationBuilder();

        McpHostSetup.ConfigureLogging(builder);

        var consoleProviders = builder.Services
            .Where(s => s.ServiceType == typeof(ILoggerProvider)
                     && s.ImplementationType == typeof(ConsoleLoggerProvider))
            .ToList();

        Assert.Single(consoleProviders);
    }
}