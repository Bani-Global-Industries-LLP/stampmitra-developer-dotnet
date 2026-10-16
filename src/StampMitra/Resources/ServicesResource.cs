using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>The services this key is entitled to.</summary>
public sealed class ServicesResource
{
    private readonly Transport _transport;

    internal ServicesResource(Transport transport)
    {
        _transport = transport;
    }

    /// <summary>Lists the services the key is entitled to (GET /v1/services; the API returns a bare array).</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<ServiceList> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<ServiceList>(HttpMethod.Get, "/v1/services", null, null, null, options, false, ServiceListFactory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListAsync(RequestOptions, CancellationToken)"/>
    public Task<ServiceList> ListAsync(CancellationToken cancellationToken) => ListAsync(null, cancellationToken);

    /// <summary>Gets one service (GET /v1/services/{serviceCode}).</summary>
    /// <param name="serviceCode">The service code, e.g. <c>estamp.instrument.rent_agreement</c>.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<Service> GetAsync(string serviceCode, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<Service>(HttpMethod.Get, "/v1/services/{serviceCode}", new string?[] { serviceCode }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetAsync(string, RequestOptions, CancellationToken)"/>
    public Task<Service> GetAsync(string serviceCode, CancellationToken cancellationToken) => GetAsync(serviceCode, null, cancellationToken);

    private static ServiceList ServiceListFactory(ApiResponse r)
    {
        if (r.Body.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("expected a JSON array");
        }

        var list = new List<Service>();
        foreach (var item in r.Body.EnumerateArray())
        {
            list.Add(item.Deserialize<Service>(Json.Options) ?? throw new JsonException("null service"));
        }

        return new ServiceList(list);
    }
}
