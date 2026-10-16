using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StampMitra.Extensions.DependencyInjection;

/// <summary>Registers <see cref="StampMitraClient"/> with the dependency injection container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The name of the <see cref="HttpClient"/> registration used by the SDK.</summary>
    public const string HttpClientName = "StampMitra";

    /// <summary>
    /// Registers a singleton <see cref="StampMitraClient"/> backed by an <see cref="IHttpClientFactory"/> client that never
    /// follows redirects. Returns the <see cref="IHttpClientBuilder"/> so you can add handlers (for example a proxy).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the client options (API key, base URL, retries, ...).</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddStampMitra(this IServiceCollection services, Action<StampMitraClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services
            .AddHttpClient(HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });

        services.AddSingleton(sp =>
        {
            var options = new StampMitraClientOptions
            {
                HttpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            };
            configure?.Invoke(options);

            // Bridge the SDK's (already redacted) debug lines to ILogger, unless the caller set their own sink.
            if (options.DebugLog is null && sp.GetService<ILoggerFactory>() is { } loggers)
            {
                var logger = loggers.CreateLogger("StampMitra");
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    options.DebugLog = line => logger.Log(LogLevel.Debug, default, line, null, static (state, _) => state);
                }
            }

            return new StampMitraClient(options);
        });

        return builder;
    }
}
