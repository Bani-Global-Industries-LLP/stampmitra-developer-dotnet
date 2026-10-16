using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StampMitra.Extensions.DependencyInjection;

namespace StampMitra.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddStampMitraRegistersASingletonClient()
    {
        var services = new ServiceCollection();
        services.AddStampMitra(o => o.ApiKey = Tc.Key);
        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<StampMitraClient>();
        var b = sp.GetRequiredService<StampMitraClient>();
        Assert.Same(a, b);
        Assert.Equal(StampMitraEnvironment.Test, a.Environment);
    }

    [Fact]
    public async Task UsesTheFactoryClientAndBridgesDebugLogsToILogger()
    {
        var sink = new ListLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(sink));
        var handler = new FakeHandler((_, _) => Task.FromResult(FakeHandler.Json(200, "{}")));
        services.AddStampMitra(o => o.ApiKey = Tc.Key).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<StampMitraClient>().Usage.SummaryAsync();
        Assert.Single(handler.Requests);
        Assert.Contains(sink.Lines, l => l.Contains("/v1/usage/summary", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Lines, l => l.Contains(Tc.Key, StringComparison.Ordinal));
    }

    [Fact]
    public void MissingKeyFailsOnResolve()
    {
        var old = Environment.GetEnvironmentVariable("STAMPMITRA_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("STAMPMITRA_API_KEY", null);
            var services = new ServiceCollection();
            services.AddStampMitra();
            using var sp = services.BuildServiceProvider();
            Assert.Throws<AuthenticationException>(() => sp.GetRequiredService<StampMitraClient>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("STAMPMITRA_API_KEY", old);
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider, ILogger
    {
        public List<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => categoryName == "StampMitra" ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));

        public void Dispose()
        {
        }
    }
}
