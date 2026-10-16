using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra;

/// <summary>Raw outcome of a successful call.</summary>
internal sealed record ApiResponse(int Status, IReadOnlyDictionary<string, string> Headers, JsonElement Body, string? RequestId, int Attempts)
{
    public ApiResponseInfo ToInfo() => new(Status, Headers, RequestId, Attempts);
}

/// <summary>
/// Builds requests, sends them through <see cref="HttpClient"/>, retries safely and maps failures to typed exceptions.
/// Retry rules: GET is always retry-safe; a POST is retried only when it carries an Idempotency-Key (the SDK adds one
/// to every POST except webhook replay and reuses it across attempts). Retried: 429, 502, 503, 504, connection errors
/// and timeouts. Redirects are never followed.
/// </summary>
internal sealed class Transport
{
    private const int ErrorBodyLimit = 8 * 1024;
    private static readonly TimeSpan RetryAfterCap = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackoffBase = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan BackoffCap = TimeSpan.FromSeconds(8);

    private readonly string _apiKey;
    private readonly Uri _baseUri;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly IReadOnlyDictionary<string, string> _defaultHeaders;
    private readonly string _userAgent;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public Transport(string apiKey, Uri baseUri, HttpClient http, TimeSpan timeout, int maxRetries, IReadOnlyDictionary<string, string> defaultHeaders, string userAgent, Action<string>? log, Func<TimeSpan, CancellationToken, Task>? delay)
    {
        _apiKey = apiKey;
        _baseUri = baseUri;
        _http = http;
        _timeout = timeout;
        _maxRetries = maxRetries;
        _defaultHeaders = defaultHeaders;
        _userAgent = userAgent;
        _log = log;
        _delay = delay ?? ((d, ct) => Task.Delay(d, ct));
    }

    public override string ToString() => "Transport { BaseUri = " + _baseUri + " }";

    private static readonly Regex PlaceholderRx = new(@"\{[A-Za-z]+\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<T> RequestAsync<T>(
        HttpMethod method,
        string route,
        IReadOnlyList<string?>? pathValues,
        IEnumerable<KeyValuePair<string, string?>>? query,
        object? body,
        RequestOptions? options,
        bool autoIdempotency,
        Func<ApiResponse, T>? factory,
        CancellationToken cancellationToken)
        where T : ApiModel
    {
        var response = await SendAsync(method, route, pathValues, query, body, options, autoIdempotency, cancellationToken).ConfigureAwait(false);
        try
        {
            T model;
            if (factory is not null)
            {
                model = factory(response);
            }
            else
            {
                model = response.Body.ValueKind == JsonValueKind.Object
                    ? response.Body.Deserialize<T>(Json.Options) ?? throw new JsonException("empty object")
                    : throw new JsonException("expected a JSON object");
            }

            model.Response = response.ToInfo();
            return model;
        }
        catch (JsonException ex)
        {
            throw new StampMitraException("The API returned a response this SDK could not read: " + ex.Message, response.Status, "INVALID_RESPONSE", response.RequestId, null, null, ex);
        }
    }

    public async Task<ApiResponse> SendAsync(
        HttpMethod method,
        string route,
        IReadOnlyList<string?>? pathValues,
        IEnumerable<KeyValuePair<string, string?>>? query,
        object? body,
        RequestOptions? options,
        bool autoIdempotency,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(route, pathValues, query);
        var idempotencyKey = options?.IdempotencyKey;
        if (idempotencyKey is not null)
        {
            ValidateHeaderToken("Idempotency-Key", idempotencyKey);
        }
        else if (autoIdempotency && method != HttpMethod.Get)
        {
            idempotencyKey = Idempotency.NewKey();
        }

        var extra = options?.Headers;
        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                ValidateHeaderToken(kv.Key, kv.Value);
            }
        }

        var retrySafe = method == HttpMethod.Get || idempotencyKey is not null;
        var maxRetries = Math.Max(0, options?.MaxRetries ?? _maxRetries);
        var timeout = options?.Timeout ?? _timeout;
        byte[]? payload = method == HttpMethod.Get ? null : JsonSerializer.SerializeToUtf8Bytes(body ?? new object(), body?.GetType() ?? typeof(object), Json.Options);

        for (var attempt = 1; ; attempt++)
        {
            using var request = BuildRequest(method, url, payload, extra, idempotencyKey);
            Log(() => "request " + method.Method + " " + url + " attempt=" + attempt.ToString(CultureInfo.InvariantCulture) + " headers=" + HeadersForLog(request) + " body=" + Redactor.Body(payload is null ? null : Encoding.UTF8.GetString(payload)));

            StampMitraException failure;
            RetryHint hint;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeout != System.Threading.Timeout.InfiniteTimeSpan)
                {
                    cts.CancelAfter(timeout);
                }

                using var http = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
                var text = await http.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var status = (int)http.StatusCode;
                var headers = CollectHeaders(http);
                Log(() => "response status=" + status.ToString(CultureInfo.InvariantCulture) + " attempt=" + attempt.ToString(CultureInfo.InvariantCulture) + " body=" + Redactor.Body(text));

                if (status is >= 200 and < 300)
                {
                    return new ApiResponse(status, headers, ParseSuccess(text, status, headers), Get(headers, "x-request-id"), attempt);
                }

                failure = MapError(status, text, headers, http.Headers.RetryAfter);
                hint = new RetryHint(status is 429 or 502 or 503 or 504, (failure as RateLimitException)?.RetryAfter ?? ParseRetryAfter(http.Headers.RetryAfter));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                failure = new ApiTimeoutException("Request timed out after " + timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s", ex);
                hint = new RetryHint(true, null);
            }
            catch (HttpRequestException ex)
            {
                failure = new ApiConnectionException("Network error: " + ex.Message, ex);
                hint = new RetryHint(true, null);
            }
            catch (IOException ex)
            {
                failure = new ApiConnectionException("Network error: " + ex.Message, ex);
                hint = new RetryHint(true, null);
            }

            if (retrySafe && hint.Retryable && attempt <= maxRetries)
            {
                var delay = hint.RetryAfter is { } ra ? Min(ra, RetryAfterCap) : Backoff(attempt);
                Log(() => "retrying in " + delay.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s after " + failure.GetType().Name);
                await _delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw failure;
        }
    }

    internal static TimeSpan Backoff(int attempt)
    {
        var ceiling = Math.Min(BackoffCap.TotalSeconds, BackoffBase.TotalSeconds * Math.Pow(2, attempt - 1));
        var fraction = RandomNumberGenerator.GetInt32(0, 1_000_001) / 1_000_000d;
        return TimeSpan.FromSeconds(fraction * ceiling); // full jitter
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Delta is { } d)
        {
            return d < TimeSpan.Zero ? TimeSpan.Zero : d;
        }

        if (value.Date is { } at)
        {
            var diff = at - DateTimeOffset.UtcNow;
            return diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
        }

        return null;
    }

    private Uri BuildUrl(string route, IReadOnlyList<string?>? pathValues, IEnumerable<KeyValuePair<string, string?>>? query)
    {
        var i = 0;
        var path = PlaceholderRx.Replace(route, m =>
        {
            var name = m.Value.Trim('{', '}');
            var v = pathValues is not null && i < pathValues.Count ? pathValues[i++] : null;
            return EncodeSegment(v, name);
        });

        var sb = new StringBuilder(_baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
        sb.Append(path);
        var first = true;
        if (query is not null)
        {
            foreach (var kv in query)
            {
                if (kv.Value is null)
                {
                    continue;
                }

                sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
                first = false;
            }
        }

        return new Uri(sb.ToString(), UriKind.Absolute);
    }

    /// <summary>Percent-encodes a path segment, rejecting empty values, dot segments and slashes.</summary>
    internal static string EncodeSegment(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ValidationException.ForField(name, name + " must not be empty");
        }

        if (value is "." or "..")
        {
            throw ValidationException.ForField(name, name + " must not be a dot segment");
        }

        foreach (var c in value)
        {
            if (c is '/' or '\\' || char.IsControl(c))
            {
                throw ValidationException.ForField(name, name + " must not contain slashes or control characters");
            }
        }

        return Uri.EscapeDataString(value);
    }

    private static void ValidateHeaderToken(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
        {
            throw ValidationException.ForField("headers", "invalid header name");
        }

        foreach (var c in name)
        {
            if (c <= ' ' || c >= 0x7F || c == ':')
            {
                throw ValidationException.ForField("headers", "invalid header name");
            }
        }

        if (value.Length > 1024 || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\0', StringComparison.Ordinal))
        {
            throw ValidationException.ForField(name, "invalid header value for " + name);
        }

        if (name.Equals("Idempotency-Key", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Length is 0 or > 255)
            {
                throw ValidationException.ForField("idempotencyKey", "idempotencyKey must be 1-255 characters");
            }

            foreach (var c in value)
            {
                if (c is < ' ' or > '~')
                {
                    throw ValidationException.ForField("idempotencyKey", "idempotencyKey must be printable ASCII");
                }
            }
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, Uri url, byte[]? payload, IReadOnlyDictionary<string, string>? extra, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        foreach (var kv in _defaultHeaders)
        {
            AddHeader(request, kv.Key, kv.Value);
        }

        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                AddHeader(request, kv.Key, kv.Value);
            }
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Remove("Idempotency-Key");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (payload is not null)
        {
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        return request;
    }

    private static void AddHeader(HttpRequestMessage request, string name, string value)
    {
        if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || name.Equals("x-api-key", StringComparison.OrdinalIgnoreCase) || name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage http)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in http.Headers)
        {
            d[h.Key] = string.Join(", ", h.Value);
        }

        foreach (var h in http.Content.Headers)
        {
            d[h.Key] = string.Join(", ", h.Value);
        }

        return d;
    }

    private static string? Get(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var v) ? v : null;

    private static JsonElement ParseSuccess(string text, int status, IReadOnlyDictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new StampMitraException("The API returned a success status with a non-JSON body", status, "INVALID_RESPONSE", Get(headers, "x-request-id"), null, Truncate(text), ex);
        }
    }

    private static string Truncate(string text) => text.Length <= ErrorBodyLimit ? text : text[..ErrorBodyLimit];

    private static StampMitraException MapError(int status, string text, IReadOnlyDictionary<string, string> headers, RetryConditionHeaderValue? retryAfter)
    {
        string? message = null, code = null, field = null, requestId = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                message = Str(err, "message");
                code = Str(err, "code");
                field = Str(err, "field");
                requestId = Str(err, "requestId");
            }
        }
        catch (JsonException)
        {
            // non-JSON error body (proxy HTML etc.): map by status alone
        }

        message ??= "HTTP " + status.ToString(CultureInfo.InvariantCulture);
        requestId ??= Get(headers, "x-request-id");
        var body = text.Length == 0 ? null : Truncate(text);
        return status switch
        {
            400 => new ValidationException(message, status, code, requestId, field, body),
            401 => new AuthenticationException(message, status, code, requestId, body, field),
            402 => new PaymentRequiredException(message, status, code, requestId, body, field),
            403 => new PermissionDeniedException(message, status, code, requestId, body, field),
            404 => new NotFoundException(message, status, code, requestId, body, field),
            409 => new ConflictException(message, status, code, requestId, body, field),
            429 => new RateLimitException(message, status, code, requestId, body, ParseRetryAfter(retryAfter), field),
            >= 500 => new UpstreamException(message, status, code, requestId, body, field),
            _ => new StampMitraException(message, status, code, requestId, field, body),
        };
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string HeadersForLog(HttpRequestMessage request)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var h in request.Headers)
        {
            pairs.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));
        }

        var redacted = Redactor.Headers(pairs);
        return "{" + string.Join(", ", System.Linq.Enumerable.Select(redacted, kv => kv.Key + ": " + kv.Value)) + "}";
    }

    private void Log(Func<string> line)
    {
        if (_log is not null)
        {
            _log(line());
        }
    }

    private readonly record struct RetryHint(bool Retryable, TimeSpan? RetryAfter);
}
