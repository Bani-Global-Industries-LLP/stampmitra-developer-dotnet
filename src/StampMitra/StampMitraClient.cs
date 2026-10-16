using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using StampMitra.Resources;

namespace StampMitra;

/// <summary>Which environment an API key belongs to.</summary>
public enum StampMitraEnvironment
{
    /// <summary>A free sandbox key (<c>sk_test_...</c>).</summary>
    Test,

    /// <summary>A metered production key (<c>sk_live_...</c>).</summary>
    Live,

    /// <summary>A key that does not follow the standard format (only with <see cref="StampMitraClientOptions.AllowCustomKey"/>).</summary>
    Custom,
}

/// <summary>
/// Client for the StampMitra Developer API. Create one per application (it is thread-safe and reuses connections) and
/// dispose it on shutdown.
/// <code>
/// using var client = new StampMitraClient();   // reads STAMPMITRA_API_KEY
/// var pan = await client.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F" }, cancellationToken);
/// </code>
/// Server-side only: never ship <c>sk_</c> keys in mobile apps, browsers or public repositories.
/// </summary>
public sealed class StampMitraClient : IDisposable
{
    /// <summary>The SDK version.</summary>
    public const string Version = "0.1.0";

    private static readonly Regex KeyRx = new(@"^sk_(test|live)_[A-Za-z0-9_-]{8,}\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly HttpClient? _ownedHttp;

    /// <summary>Creates a client using the <c>STAMPMITRA_API_KEY</c> environment variable and default settings.</summary>
    public StampMitraClient()
        : this(new StampMitraClientOptions())
    {
    }

    /// <summary>Creates a client with an explicit API key and default settings.</summary>
    /// <param name="apiKey">The API key (<c>sk_test_...</c> or <c>sk_live_...</c>).</param>
    public StampMitraClient(string apiKey)
        : this(new StampMitraClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Creates a client.</summary>
    /// <param name="options">Client settings.</param>
    /// <exception cref="AuthenticationException">No key, or a key in an unexpected format.</exception>
    /// <exception cref="ValidationException">An insecure or malformed <see cref="StampMitraClientOptions.BaseUrl"/> or other invalid setting.</exception>
    public StampMitraClient(StampMitraClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var baseUri = ParseBaseUrl(options.BaseUrl);

        var apiKey = options.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = Environment_GetKey();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AuthenticationException("No API key provided. Set StampMitraClientOptions.ApiKey or the STAMPMITRA_API_KEY environment variable.");
        }

        apiKey = apiKey.Trim();
        var m = KeyRx.Match(apiKey);
        if (m.Success)
        {
            Environment = m.Groups[1].Value == "live" ? StampMitraEnvironment.Live : StampMitraEnvironment.Test;
        }
        else if (options.AllowCustomKey && IsHeaderSafe(apiKey))
        {
            Environment = StampMitraEnvironment.Custom;
        }
        else
        {
            throw new AuthenticationException("API key must look like sk_test_... or sk_live_... (set AllowCustomKey to override).");
        }

        if (options.MaxRetries < 0 || options.MaxRetries > 10)
        {
            throw ValidationException.ForField("maxRetries", "MaxRetries must be between 0 and 10");
        }

        if (options.Timeout <= TimeSpan.Zero && options.Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw ValidationException.ForField("timeout", "Timeout must be positive");
        }

        var http = options.HttpClient;
        if (http is null)
        {
            _ownedHttp = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
            })
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
            http = _ownedHttp;
        }

        var headers = new Dictionary<string, string>(options.DefaultHeaders, StringComparer.OrdinalIgnoreCase);
        var transport = new Transport(apiKey, baseUri, http, options.Timeout, options.MaxRetries, headers, BuildUserAgent(options.AppInfo), options.DebugLog, options.DelayAsync);
        Transport = transport;
        BaseUrl = baseUri;
        Verify = new VerifyResource(transport, options.ValidateInput);
        Services = new ServicesResource(transport);
        Estamp = new EstampResource(transport, options.ValidateInput);
        Orders = new OrdersResource(transport, options.ValidateInput);
        Payments = new PaymentsResource(transport, options.ValidateInput);
        Usage = new UsageResource(transport);
        Logs = new LogsResource(transport, options.ValidateInput);
        Webhooks = new WebhooksResource(transport);
    }

    internal Transport Transport { get; }

    /// <summary>Whether the key is a sandbox (<see cref="StampMitraEnvironment.Test"/>) or production (<see cref="StampMitraEnvironment.Live"/>) key.</summary>
    public StampMitraEnvironment Environment { get; }

    /// <summary>The API base URL in use.</summary>
    public Uri BaseUrl { get; }

    /// <summary>Identity verification (PAN, GSTIN, RC, Voter ID, driving licence, passport, CIN, Udyam, bank account).</summary>
    public VerifyResource Verify { get; }

    /// <summary>The services this key may use.</summary>
    public ServicesResource Services { get; }

    /// <summary>e-Stamp Paper reference data, duty calculation and parties.</summary>
    public EstampResource Estamp { get; }

    /// <summary>Orders.</summary>
    public OrdersResource Orders { get; }

    /// <summary>Payment intents.</summary>
    public PaymentsResource Payments { get; }

    /// <summary>Usage summary.</summary>
    public UsageResource Usage { get; }

    /// <summary>Request logs, with an auto-paginating <c>IAsyncEnumerable</c>.</summary>
    public LogsResource Logs { get; }

    /// <summary>Webhook event history and replay.</summary>
    public WebhooksResource Webhooks { get; }

    /// <summary>Releases the connection pool the SDK created. A <see cref="HttpClient"/> you supplied is left alone.</summary>
    public void Dispose() => _ownedHttp?.Dispose();

    /// <summary>Never includes the API key.</summary>
    public override string ToString() => $"StampMitraClient {{ Environment = {Environment}, BaseUrl = {BaseUrl} }}";

    private static string? Environment_GetKey() =>
        System.Environment.GetEnvironmentVariable(StampMitraClientOptions.ApiKeyEnvironmentVariable);

    private static bool IsHeaderSafe(string key)
    {
        foreach (var c in key)
        {
            if (c is < '!' or > '~')
            {
                return false;
            }
        }

        return key.Length > 0;
    }

    private static Uri ParseBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            throw ValidationException.ForField("baseUrl", "baseUrl must be an absolute https URL");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw ValidationException.ForField("baseUrl", "baseUrl must not contain credentials");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw ValidationException.ForField("baseUrl", "baseUrl must not contain a query string or fragment");
        }

        var host = uri.Host.Trim('[', ']');
        var loopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1";
        if (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && loopback))
        {
            return uri;
        }

        throw ValidationException.ForField("baseUrl", "baseUrl must use https (http is only allowed for localhost)");
    }

    private static string BuildUserAgent(AppInfo? app)
    {
        var ua = "stampmitra-dotnet/" + Version + " (" + RuntimeInformation.FrameworkDescription + ")";
        if (app is not null && !string.IsNullOrWhiteSpace(app.Name))
        {
            ua += " " + Clean(app.Name);
            if (!string.IsNullOrWhiteSpace(app.Version))
            {
                ua += "/" + Clean(app.Version);
            }

            if (!string.IsNullOrWhiteSpace(app.Url))
            {
                ua += " (" + Clean(app.Url) + ")";
            }
        }

        return ua;
    }

    private static string Clean(string s) =>
        string.Create(CultureInfo.InvariantCulture, $"{Regex.Replace(s.Trim(), @"[^\x21-\x7E ]", string.Empty)}");
}
