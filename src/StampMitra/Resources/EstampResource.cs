using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>e-Stamp Paper reference data and helpers. The e-Stamp response shapes are not fixed by the API, so responses are <see cref="GenericResponse"/> (read <see cref="GenericResponse.Data"/>).</summary>
public sealed class EstampResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal EstampResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Gets the order form schema for an e-Stamp service (GET /v1/estamp/schema/{serviceCode}).</summary>
    /// <param name="serviceCode">The service code, e.g. <c>estamp.instrument.rent_agreement</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> GetSchemaAsync(string serviceCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Get, "/v1/estamp/schema/{serviceCode}", new string?[] { serviceCode }, null, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetSchemaAsync(string, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> GetSchemaAsync(string serviceCode, CancellationToken cancellationToken) => GetSchemaAsync(serviceCode, null, cancellationToken);

    /// <summary>Lists the states e-Stamp is available in (GET /v1/estamp/states).</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> ListStatesAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Get, "/v1/estamp/states", null, null, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListStatesAsync(RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> ListStatesAsync(CancellationToken cancellationToken) => ListStatesAsync(null, cancellationToken);

    /// <summary>Lists the instruments of a state (GET /v1/estamp/states/{stateCode}/instruments).</summary>
    /// <param name="stateCode">The state code, e.g. <c>MH</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> ListInstrumentsAsync(string stateCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Get, "/v1/estamp/states/{stateCode}/instruments", new string?[] { stateCode }, null, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListInstrumentsAsync(string, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> ListInstrumentsAsync(string stateCode, CancellationToken cancellationToken) => ListInstrumentsAsync(stateCode, null, cancellationToken);

    /// <summary>Lists the articles of an instrument (GET /v1/estamp/instruments/{instrumentCode}/articles).</summary>
    /// <param name="instrumentCode">The instrument code.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> ListArticlesAsync(string instrumentCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Get, "/v1/estamp/instruments/{instrumentCode}/articles", new string?[] { instrumentCode }, null, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListArticlesAsync(string, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> ListArticlesAsync(string instrumentCode, CancellationToken cancellationToken) => ListArticlesAsync(instrumentCode, null, cancellationToken);

    /// <summary>Calculates stamp duty (POST /v1/estamp/duty/calculate). An Idempotency-Key is generated unless you pass one.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> CalculateDutyAsync(EstampDutyCalculateRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate)
        {
            Require(request.StateCode, "stateCode");
            Require(request.ArticleCode, "articleCode");
            if (request.StampDutyAmount is null)
            {
                throw ValidationException.ForField("stampDutyAmount", "stampDutyAmount is required");
            }
        }

        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Post, "/v1/estamp/duty/calculate", null, null, request, options, true, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CalculateDutyAsync(EstampDutyCalculateRequest, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> CalculateDutyAsync(EstampDutyCalculateRequest request, CancellationToken cancellationToken) => CalculateDutyAsync(request, null, cancellationToken);

    /// <summary>Attaches or corrects party details on an e-Stamp order (POST /v1/estamp/orders/{orderId}/parties). An Idempotency-Key is generated unless you pass one.</summary>
    /// <param name="orderId">The order's <c>externalOrderId</c>.</param>
    /// <param name="request">The request body.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> AddPartiesAsync(string orderId, EstampPartiesRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Post, "/v1/estamp/orders/{orderId}/parties", new string?[] { orderId }, null, request, options, true, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="AddPartiesAsync(string, EstampPartiesRequest, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> AddPartiesAsync(string orderId, EstampPartiesRequest request, CancellationToken cancellationToken) => AddPartiesAsync(orderId, request, null, cancellationToken);

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ValidationException.ForField(field, field + " is required");
        }
    }
}
