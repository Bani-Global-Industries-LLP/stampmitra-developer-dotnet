using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Orders. Orders are addressed by the <c>externalOrderId</c> you supplied (and that the API returns), not by an internal id.</summary>
public sealed class OrdersResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal OrdersResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Creates an order (POST /v1/orders). Requires ExternalOrderId, ExternalCustomerId and ServiceCode. An Idempotency-Key is generated unless you pass one; re-posting the same ExternalOrderId returns the existing order.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<OrderReference> CreateAsync(CreateOrderRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate)
        {
            Require(request.ExternalOrderId, "externalOrderId");
            Require(request.ExternalCustomerId, "externalCustomerId");
            Require(request.ServiceCode, "serviceCode");
        }

        return await _transport.RequestAsync<OrderReference>(HttpMethod.Post, "/v1/orders", null, null, request, options, true, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CreateAsync(CreateOrderRequest, RequestOptions, CancellationToken)"/>
    public Task<OrderReference> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken) => CreateAsync(request, null, cancellationToken);

    /// <summary>Gets an order (GET /v1/orders/{id}).</summary>
    /// <param name="id">The order's <c>externalOrderId</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<OrderDetail> GetAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<OrderDetail>(HttpMethod.Get, "/v1/orders/{id}", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetAsync(string, RequestOptions, CancellationToken)"/>
    public Task<OrderDetail> GetAsync(string id, CancellationToken cancellationToken) => GetAsync(id, null, cancellationToken);

    /// <summary>Gets an order's status (GET /v1/orders/{id}/status).</summary>
    /// <param name="id">The order's <c>externalOrderId</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<OrderStatusResponse> GetStatusAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<OrderStatusResponse>(HttpMethod.Get, "/v1/orders/{id}/status", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetStatusAsync(string, RequestOptions, CancellationToken)"/>
    public Task<OrderStatusResponse> GetStatusAsync(string id, CancellationToken cancellationToken) => GetStatusAsync(id, null, cancellationToken);

    /// <summary>Gets an order's payment (GET /v1/orders/{id}/payment).</summary>
    /// <param name="id">The order's <c>externalOrderId</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<OrderPaymentResponse> GetPaymentAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<OrderPaymentResponse>(HttpMethod.Get, "/v1/orders/{id}/payment", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetPaymentAsync(string, RequestOptions, CancellationToken)"/>
    public Task<OrderPaymentResponse> GetPaymentAsync(string id, CancellationToken cancellationToken) => GetPaymentAsync(id, null, cancellationToken);

    /// <summary>Lists an order's documents (GET /v1/orders/{id}/documents).</summary>
    /// <param name="id">The order's <c>externalOrderId</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<OrderDocumentsResponse> ListDocumentsAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<OrderDocumentsResponse>(HttpMethod.Get, "/v1/orders/{id}/documents", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListDocumentsAsync(string, RequestOptions, CancellationToken)"/>
    public Task<OrderDocumentsResponse> ListDocumentsAsync(string id, CancellationToken cancellationToken) => ListDocumentsAsync(id, null, cancellationToken);

    /// <summary>Gets a short-lived download URL for one document (GET /v1/orders/{id}/documents/{documentId}/download).</summary>
    /// <param name="id">The order's <c>externalOrderId</c>.</param>
    /// <param name="documentId">The document id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<DocumentDownloadUrl> GetDocumentDownloadAsync(string id, string documentId, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<DocumentDownloadUrl>(HttpMethod.Get, "/v1/orders/{id}/documents/{documentId}/download", new string?[] { id, documentId }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetDocumentDownloadAsync(string, string, RequestOptions, CancellationToken)"/>
    public Task<DocumentDownloadUrl> GetDocumentDownloadAsync(string id, string documentId, CancellationToken cancellationToken) => GetDocumentDownloadAsync(id, documentId, null, cancellationToken);

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ValidationException.ForField(field, field + " is required");
        }
    }
}
