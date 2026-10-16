using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampMitra.Webhooks;

/// <summary>
/// A delivered webhook event. Concrete subtypes exist for the eight <c>order.*</c> events; anything else becomes an
/// <see cref="UnknownWebhookEvent"/>. Extra per-event fields are kept in <see cref="AdditionalProperties"/>.
/// </summary>
public abstract record WebhookEvent
{
    /// <summary>The internal order id the event is about.</summary>
    [JsonPropertyName("coreOrderId")]
    public string? CoreOrderId { get; init; }

    /// <summary>The event type as sent, e.g. <c>ORDER_CREATED</c>.</summary>
    [JsonPropertyName("coreEventType")]
    public string? CoreEventType { get; init; }

    /// <summary>ISO 8601 time the event occurred.</summary>
    [JsonPropertyName("occurredAt")]
    public string? OccurredAt { get; init; }

    /// <summary>The <c>X-Webhook-Event-Id</c> header, when supplied to <c>ConstructEvent</c>. Use it to de-duplicate retried deliveries.</summary>
    [JsonIgnore]
    public string? EventId { get; init; }

    /// <summary>The documented webhook name, e.g. <c>order.created</c>, or <see langword="null"/> for an unknown event.</summary>
    [JsonIgnore]
    public abstract string? Type { get; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary><c>order.created</c>: an order was created.</summary>
public sealed record OrderCreatedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.created";
}

/// <summary><c>order.payment_confirmed</c>: payment for the order was confirmed.</summary>
public sealed record OrderPaymentConfirmedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.payment_confirmed";
}

/// <summary><c>order.fulfilment_started</c>: fulfilment began.</summary>
public sealed record OrderFulfilmentStartedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.fulfilment_started";
}

/// <summary><c>order.fulfilment_completed</c>: fulfilment finished.</summary>
public sealed record OrderFulfilmentCompletedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.fulfilment_completed";
}

/// <summary><c>order.failed</c>: the order failed.</summary>
public sealed record OrderFailedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.failed";
}

/// <summary><c>order.cancelled</c>: the order was cancelled.</summary>
public sealed record OrderCancelledEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.cancelled";
}

/// <summary><c>order.refund_completed</c>: a refund finished.</summary>
public sealed record OrderRefundCompletedEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.refund_completed";
}

/// <summary><c>order.document_ready</c>: a document is ready to download.</summary>
public sealed record OrderDocumentReadyEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => "order.document_ready";
}

/// <summary>An event type this SDK version does not know. Inspect <see cref="WebhookEvent.CoreEventType"/> and <see cref="WebhookEvent.AdditionalProperties"/>.</summary>
public sealed record UnknownWebhookEvent : WebhookEvent
{
    /// <inheritdoc />
    public override string? Type => null;
}
