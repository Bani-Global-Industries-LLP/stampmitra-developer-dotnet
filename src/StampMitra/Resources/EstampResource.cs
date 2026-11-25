using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>e-Stamp Paper reference data, duty calculation, parties and the e-Stamp wallets. Older catalogue routes return <see cref="GenericResponse"/> (read <see cref="GenericResponse.Data"/>); the newer routes are typed.</summary>
public sealed class EstampResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal EstampResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
        Orders = new EstampOrdersResource(transport, validate);
        Stamps = new EstampStampsResource(transport, validate);
    }

    /// <summary>Typed e-Stamp orders: validate, create, list, cancel, export, stamps and the PAN card upload.</summary>
    public EstampOrdersResource Orders { get; }

    /// <summary>Single e-Stamps: read, cancel, replace, PAN card link and certificate.</summary>
    public EstampStampsResource Stamps { get; }

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

    /// <summary>Gets live availability of every state (GET /v1/estamp/states/availability). Requires the <c>estamp.read</c> scope.</summary>
    /// <param name="active">Only active states when <see langword="true"/>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampStatesAvailability> GetStatesAvailabilityAsync(bool? active, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampStatesAvailability>(HttpMethod.Get, "/v1/estamp/states/availability", null, new List<KeyValuePair<string, string?>> { new("active", active is null ? null : (active.Value ? "true" : "false")) }, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetStatesAvailabilityAsync(bool?, RequestOptions, CancellationToken)"/>
    public Task<EstampStatesAvailability> GetStatesAvailabilityAsync(bool? active, CancellationToken cancellationToken) => GetStatesAvailabilityAsync(active, null, cancellationToken);

    /// <summary>Gets one state's serviceability and field flags (GET /v1/estamp/states/{stateCode}). Requires the <c>estamp.read</c> scope.</summary>
    /// <param name="stateCode">The state code, e.g. <c>MH</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampState> GetStateAsync(string stateCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampState>(HttpMethod.Get, "/v1/estamp/states/{stateCode}", new string?[] { stateCode }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetStateAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampState> GetStateAsync(string stateCode, CancellationToken cancellationToken) => GetStateAsync(stateCode, null, cancellationToken);

    /// <summary>Gets the required and optional fields for a stamp combination (the response includes a valid <c>ExampleBody</c>) (GET /v1/estamp/states/{stateCode}/requirements). Requires the <c>estamp.read</c> scope.</summary>
    /// <param name="stateCode">The state code, e.g. <c>MH</c>.</param>
    /// <param name="parameters">Optional article, denomination and category; <see langword="null"/> for the state only.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampRequirements> GetStateRequirementsAsync(string stateCode, EstampRequirementsParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampRequirements>(HttpMethod.Get, "/v1/estamp/states/{stateCode}/requirements", new string?[] { stateCode }, RequirementsQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetStateRequirementsAsync(string, EstampRequirementsParams?, RequestOptions, CancellationToken)"/>
    public Task<EstampRequirements> GetStateRequirementsAsync(string stateCode, EstampRequirementsParams? parameters, CancellationToken cancellationToken) => GetStateRequirementsAsync(stateCode, parameters, null, cancellationToken);

    /// <summary>Lists the districts of a state (GET /v1/estamp/states/{stateCode}/districts). Requires the <c>estamp.read</c> scope.</summary>
    /// <param name="stateCode">The state code, e.g. <c>MH</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampDistrictList> ListDistrictsAsync(string stateCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampDistrictList>(HttpMethod.Get, "/v1/estamp/states/{stateCode}/districts", new string?[] { stateCode }, null, null, options, false, EstampDistrictListFactory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListDistrictsAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampDistrictList> ListDistrictsAsync(string stateCode, CancellationToken cancellationToken) => ListDistrictsAsync(stateCode, null, cancellationToken);

    /// <summary>Lists the sub-registrar offices of a district (GET /v1/estamp/states/{stateCode}/districts/{districtCode}/sros). Requires the <c>estamp.read</c> scope.</summary>
    /// <param name="stateCode">The state code, e.g. <c>MH</c>.</param>
    /// <param name="districtCode">The district code.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampSroList> ListSrosAsync(string stateCode, string districtCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampSroList>(HttpMethod.Get, "/v1/estamp/states/{stateCode}/districts/{districtCode}/sros", new string?[] { stateCode, districtCode }, null, null, options, false, EstampSroListFactory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListSrosAsync(string, string, RequestOptions, CancellationToken)"/>
    public Task<EstampSroList> ListSrosAsync(string stateCode, string districtCode, CancellationToken cancellationToken) => ListSrosAsync(stateCode, districtCode, null, cancellationToken);

    /// <summary>Reads both wallets (Stamp duty and, nested under <c>ServiceFee</c>, the Service wallet) in one call (GET /v1/estamp/wallet). Requires the <c>stamp-duty-wallet.read and service-fee-wallet.read</c> scope.</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampWallet> GetWalletAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampWallet>(HttpMethod.Get, "/v1/estamp/wallet", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetWalletAsync(RequestOptions, CancellationToken)"/>
    public Task<EstampWallet> GetWalletAsync(CancellationToken cancellationToken) => GetWalletAsync(null, cancellationToken);

    private static List<KeyValuePair<string, string?>> RequirementsQuery(EstampRequirementsParams? p) => new()
    {
        new("articleCode", p?.ArticleCode),
        new("denomination", p?.Denomination?.ToString(CultureInfo.InvariantCulture)),
        new("category", p?.Category),
    };

    private static EstampDistrictList EstampDistrictListFactory(ApiResponse r)
    {
        if (r.Body.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("expected a JSON array");
        }

        var list = new List<EstampDistrict>();
        foreach (var item in r.Body.EnumerateArray())
        {
            list.Add(item.Deserialize<EstampDistrict>(Json.Options) ?? throw new JsonException("null item"));
        }

        return new EstampDistrictList(list);
    }

    private static EstampSroList EstampSroListFactory(ApiResponse r)
    {
        if (r.Body.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("expected a JSON array");
        }

        var list = new List<EstampSro>();
        foreach (var item in r.Body.EnumerateArray())
        {
            list.Add(item.Deserialize<EstampSro>(Json.Options) ?? throw new JsonException("null item"));
        }

        return new EstampSroList(list);
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ValidationException.ForField(field, field + " is required");
        }
    }
}
