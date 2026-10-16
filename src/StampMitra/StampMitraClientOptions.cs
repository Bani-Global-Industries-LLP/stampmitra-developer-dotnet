using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StampMitra;

/// <summary>Application identification appended to the <c>User-Agent</c> header.</summary>
/// <param name="Name">Application name.</param>
/// <param name="Version">Application version.</param>
/// <param name="Url">Application URL.</param>
public sealed record AppInfo(string Name, string? Version = null, string? Url = null);

/// <summary>Settings for <see cref="StampMitraClient"/>.</summary>
public sealed class StampMitraClientOptions
{
    /// <summary>The production API base URL.</summary>
    public const string DefaultBaseUrl = "https://api-b2b.stampmitra.in";

    /// <summary>Name of the environment variable read when <see cref="ApiKey"/> is not set.</summary>
    public const string ApiKeyEnvironmentVariable = "STAMPMITRA_API_KEY";

    /// <summary>
    /// The API key (<c>sk_test_...</c> or <c>sk_live_...</c>). Defaults to the <c>STAMPMITRA_API_KEY</c> environment
    /// variable. Server-side only: never ship <c>sk_</c> keys in apps or browsers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API base URL. Must be <c>https</c> (plain <c>http</c> is accepted only for <c>localhost</c>, <c>127.0.0.1</c> and
    /// <c>::1</c>, e.g. a mock server) and must not contain credentials.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Timeout for each attempt. Default 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum automatic retries (default 2). Per-call <see cref="RequestOptions.MaxRetries"/> overrides it.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Your own <see cref="System.Net.Http.HttpClient"/> (for example from <c>IHttpClientFactory</c>). It is not disposed by the
    /// SDK. Configure it NOT to follow redirects, and give it an infinite <see cref="HttpClient.Timeout"/> (the SDK
    /// applies <see cref="Timeout"/> itself). When omitted the SDK creates one that never follows redirects.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Headers sent with every request. <c>Authorization</c> and <c>x-api-key</c> are ignored.</summary>
    public IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Identifies your application in the <c>User-Agent</c> header.</summary>
    public AppInfo? AppInfo { get; set; }

    /// <summary>
    /// Debug logging sink, off by default. Receives one already-redacted line per request and response (credentials and
    /// identifiers are masked, names removed). Treat the output as sensitive anyway.
    /// </summary>
    public Action<string>? DebugLog { get; set; }

    /// <summary>Run the client-side validators before sending (default <see langword="true"/>).</summary>
    public bool ValidateInput { get; set; } = true;

    /// <summary>Accept an API key that does not look like <c>sk_test_...</c> / <c>sk_live_...</c>.</summary>
    public bool AllowCustomKey { get; set; }

    /// <summary>Replaces the retry delay (for tests). Receives the computed delay.</summary>
    public Func<TimeSpan, CancellationToken, Task>? DelayAsync { get; set; }

    /// <summary>Never includes the API key.</summary>
    public override string ToString() =>
        $"StampMitraClientOptions {{ BaseUrl = {BaseUrl}, ApiKey = {(string.IsNullOrEmpty(ApiKey) ? "(unset)" : "[REDACTED]")} }}";
}
