using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace StampMitra.Webhooks;

public static partial class WebhookSignature
{
    /// <summary>The documented <c>order.*</c> event names.</summary>
    public static IReadOnlyList<string> EventTypes { get; } = new[]
    {
        "order.created", "order.payment_confirmed", "order.fulfilment_started", "order.fulfilment_completed",
        "order.failed", "order.cancelled", "order.refund_completed", "order.document_ready",
    };

    /// <summary>Verifies a delivery, then parses it into a typed event.</summary>
    /// <param name="payload">The raw request body, byte for byte.</param>
    /// <param name="timestamp">The <c>X-Webhook-Timestamp</c> header.</param>
    /// <param name="signature">The <c>X-Webhook-Signature</c> header.</param>
    /// <param name="secret">The endpoint's signing secret (must not be empty).</param>
    /// <param name="toleranceSeconds">Maximum accepted difference from now, in seconds (default 300).</param>
    /// <param name="eventId">The <c>X-Webhook-Event-Id</c> header, stored on <see cref="WebhookEvent.EventId"/>.</param>
    /// <param name="now">Current time override, for tests.</param>
    /// <returns>One of the <c>Order*Event</c> records, or <see cref="UnknownWebhookEvent"/>.</returns>
    /// <exception cref="SignatureVerificationException">Verification failed, the secret is empty, or the payload is not a JSON object.</exception>
    public static WebhookEvent ConstructEvent(string payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, string? eventId = null, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return ConstructEvent(Encoding.UTF8.GetBytes(payload), timestamp, signature, secret, toleranceSeconds, eventId, now);
    }

    /// <inheritdoc cref="ConstructEvent(string, string, string, string, int, string, DateTimeOffset?)"/>
    public static WebhookEvent ConstructEvent(byte[] payload, string timestamp, string signature, string secret, int toleranceSeconds = DefaultToleranceSeconds, string? eventId = null, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var result = Check(payload, timestamp, signature, secret, toleranceSeconds, now);
        if (result != WebhookVerificationResult.Valid)
        {
            var reason = result switch
            {
                WebhookVerificationResult.InvalidTimestamp => "invalid_timestamp",
                WebhookVerificationResult.TimestampOutsideTolerance => "timestamp_outside_tolerance",
                _ => "signature_mismatch",
            };
            throw new SignatureVerificationException("Webhook verification failed: " + reason, reason.ToUpperInvariant());
        }

        return Parse(payload, eventId);
    }

    /// <summary>
    /// Convenience wrapper that reads the three delivery headers through a lookup (for example
    /// <c>name =&gt; request.Headers[name].FirstOrDefault()</c>).
    /// </summary>
    /// <param name="payload">The raw request body, byte for byte.</param>
    /// <param name="header">Returns a header value, or <see langword="null"/>. Names are passed as documented (case-insensitive in HTTP).</param>
    /// <param name="secret">The endpoint's signing secret (must not be empty).</param>
    /// <param name="toleranceSeconds">Maximum accepted difference from now, in seconds (default 300).</param>
    /// <param name="now">Current time override, for tests.</param>
    /// <returns>The typed event.</returns>
    /// <exception cref="SignatureVerificationException">A header is missing, verification failed, or the payload is not a JSON object.</exception>
    public static WebhookEvent ConstructEventFromHeaders(byte[] payload, Func<string, string?> header, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        var signature = header(SignatureHeader);
        var timestamp = header(TimestampHeader);
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            throw new SignatureVerificationException("Missing " + SignatureHeader + " or " + TimestampHeader + " header", "MISSING_HEADERS");
        }

        return ConstructEvent(payload, timestamp, signature, secret, toleranceSeconds, header(EventIdHeader), now);
    }

    private static WebhookEvent Parse(byte[] payload, string? eventId)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SignatureVerificationException("Webhook payload is not a JSON object", "INVALID_PAYLOAD");
            }

            var type = doc.RootElement.TryGetProperty("coreEventType", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            WebhookEvent? evt = type switch
            {
                "ORDER_CREATED" => doc.RootElement.Deserialize<OrderCreatedEvent>(Json.Options),
                "PAYMENT_CONFIRMED" => doc.RootElement.Deserialize<OrderPaymentConfirmedEvent>(Json.Options),
                "FULFILMENT_STARTED" => doc.RootElement.Deserialize<OrderFulfilmentStartedEvent>(Json.Options),
                "FULFILMENT_COMPLETED" => doc.RootElement.Deserialize<OrderFulfilmentCompletedEvent>(Json.Options),
                "ORDER_FAILED" => doc.RootElement.Deserialize<OrderFailedEvent>(Json.Options),
                "ORDER_CANCELLED" => doc.RootElement.Deserialize<OrderCancelledEvent>(Json.Options),
                "REFUND_COMPLETED" => doc.RootElement.Deserialize<OrderRefundCompletedEvent>(Json.Options),
                "DOCUMENT_READY" => doc.RootElement.Deserialize<OrderDocumentReadyEvent>(Json.Options),
                _ => doc.RootElement.Deserialize<UnknownWebhookEvent>(Json.Options),
            };
            return (evt ?? throw new SignatureVerificationException("Webhook payload is empty", "INVALID_PAYLOAD")) with { EventId = eventId };
        }
        catch (JsonException ex)
        {
            throw new SignatureVerificationException("Webhook payload is not valid JSON: " + ex.Message, "INVALID_PAYLOAD");
        }
    }
}
