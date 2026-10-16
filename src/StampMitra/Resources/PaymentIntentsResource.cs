using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Payment intents.</summary>
public sealed class PaymentIntentsResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal PaymentIntentsResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Creates a payment intent (POST /v1/payments/intents). Requires ExternalOrderId, AmountMinor, Currency and PaymentConnectionId. An Idempotency-Key is generated unless you pass one.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<PaymentIntent> CreateAsync(CreatePaymentIntentRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate)
        {
            Require(request.ExternalOrderId, "externalOrderId");
            Require(request.Currency, "currency");
            Require(request.PaymentConnectionId, "paymentConnectionId");
            if (request.AmountMinor is null or <= 0)
            {
                throw ValidationException.ForField("amountMinor", "amountMinor must be a positive integer (minor units, e.g. paise)");
            }
        }

        return await _transport.RequestAsync<PaymentIntent>(HttpMethod.Post, "/v1/payments/intents", null, null, request, options, true, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CreateAsync(CreatePaymentIntentRequest, RequestOptions, CancellationToken)"/>
    public Task<PaymentIntent> CreateAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken) => CreateAsync(request, null, cancellationToken);

    /// <summary>Gets a payment intent (GET /v1/payments/intents/{id}).</summary>
    /// <param name="id">The payment intent id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<PaymentIntent> GetAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<PaymentIntent>(HttpMethod.Get, "/v1/payments/intents/{id}", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetAsync(string, RequestOptions, CancellationToken)"/>
    public Task<PaymentIntent> GetAsync(string id, CancellationToken cancellationToken) => GetAsync(id, null, cancellationToken);

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ValidationException.ForField(field, field + " is required");
        }
    }
}
