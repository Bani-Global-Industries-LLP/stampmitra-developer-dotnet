using System;
using System.Security.Cryptography;
using System.Text;

namespace StampMitra.Webhooks;

/// <summary>Why a webhook delivery failed verification.</summary>
public enum WebhookVerificationResult
{
    /// <summary>The signature matches and the timestamp is within tolerance.</summary>
    Valid,

    /// <summary>The timestamp header is not a unix-seconds integer.</summary>
    InvalidTimestamp,

    /// <summary>The timestamp is further from now than the tolerance (replay protection).</summary>
    TimestampOutsideTolerance,

    /// <summary>The signature does not match the payload, timestamp and secret.</summary>
    SignatureMismatch,
}

/// <summary>
/// Verifies webhook deliveries. Scheme: signature = lower-case hex(HMAC-SHA256(secret, "{timestamp}.{rawBody}")), where
/// the body is exactly as received (do NOT parse and re-serialise it) and the timestamp is unix seconds. Delivered
/// headers: <c>X-Webhook-Signature</c>, <c>X-Webhook-Timestamp</c>, <c>X-Webhook-Event-Id</c>.
/// </summary>
public static partial class WebhookSignature
{
    /// <summary>Header carrying the signature.</summary>
    public const string SignatureHeader = "X-Webhook-Signature";

    /// <summary>Header carrying the unix-seconds timestamp.</summary>
    public const string TimestampHeader = "X-Webhook-Timestamp";

    /// <summary>Header carrying the event id (use it to de-duplicate retried deliveries).</summary>
    public const string EventIdHeader = "X-Webhook-Event-Id";

    /// <summary>Default accepted clock skew, in seconds.</summary>
    public const int DefaultToleranceSeconds = 300;

    /// <summary>Computes the expected signature (useful in tests and local replays).</summary>
    /// <param name="payload">The raw request body.</param>
    /// <param name="timestamp">The timestamp header value.</param>
    /// <param name="secret">The endpoint's signing secret.</param>
    /// <returns>Lower-case hex HMAC-SHA256.</returns>
    public static string Sign(string payload, string timestamp, string secret)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Sign(Encoding.UTF8.GetBytes(payload), timestamp, secret);
    }

    /// <inheritdoc cref="Sign(string, string, string)"/>
    public static string Sign(ReadOnlySpan<byte> payload, string timestamp, string secret)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        RequireSecret(secret);
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var message = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(message, 0);
        payload.CopyTo(message.AsSpan(prefix.Length));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Checks a delivery and reports why it failed, if it did.</summary>
    /// <param name="payload">The raw request body, byte for byte.</param>
    /// <param name="timestamp">The <c>X-Webhook-Timestamp</c> header.</param>
    /// <param name="signature">The <c>X-Webhook-Signature</c> header.</param>
    /// <param name="secret">The endpoint's signing secret (must not be empty).</param>
    /// <param name="toleranceSeconds">Maximum accepted difference from now, in seconds (default 300).</param>
    /// <param name="now">Current time override, for tests.</param>
    /// <returns>The verification outcome.</returns>
    /// <exception cref="SignatureVerificationException">The secret is empty (a configuration error).</exception>
    public static WebhookVerificationResult Check(string payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Check(Encoding.UTF8.GetBytes(payload), timestamp, signature, secret, toleranceSeconds, now);
    }

    /// <inheritdoc cref="Check(string, string, string, string, int, DateTimeOffset?)"/>
    public static WebhookVerificationResult Check(ReadOnlySpan<byte> payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null)
    {
        RequireSecret(secret);
        if (!IsUnixSeconds(timestamp, out var ts))
        {
            return WebhookVerificationResult.InvalidTimestamp;
        }

        var current = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        if (Math.Abs(current - ts) > toleranceSeconds)
        {
            return WebhookVerificationResult.TimestampOutsideTolerance;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(payload, timestamp, secret));
        var provided = Encoding.UTF8.GetBytes(signature ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(expected, provided)
            ? WebhookVerificationResult.Valid
            : WebhookVerificationResult.SignatureMismatch;
    }

    /// <summary>True when the signature matches and the timestamp is within tolerance. Never throws for bad input; throws only for an empty secret.</summary>
    /// <inheritdoc cref="Check(string, string, string, string, int, DateTimeOffset?)"/>
    public static bool Verify(string payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null) =>
        Check(payload, timestamp, signature, secret, toleranceSeconds, now) == WebhookVerificationResult.Valid;

    /// <inheritdoc cref="Verify(string, string, string, string, int, DateTimeOffset?)"/>
    public static bool Verify(ReadOnlySpan<byte> payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null) =>
        Check(payload, timestamp, signature, secret, toleranceSeconds, now) == WebhookVerificationResult.Valid;

    private static void RequireSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new SignatureVerificationException("Webhook secret must not be empty", "MISSING_SECRET");
        }
    }

    private static bool IsUnixSeconds(string? value, out long seconds)
    {
        seconds = 0;
        if (string.IsNullOrEmpty(value) || value.Length > 15)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        seconds = long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }
}
