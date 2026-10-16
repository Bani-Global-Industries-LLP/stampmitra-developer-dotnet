using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Usage for the organisation that owns the key.</summary>
public sealed class UsageResource
{
    private readonly Transport _transport;

    internal UsageResource(Transport transport)
    {
        _transport = transport;
    }

    /// <summary>Gets the usage summary (GET /v1/usage/summary).</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<UsageSummary> SummaryAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<UsageSummary>(HttpMethod.Get, "/v1/usage/summary", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="SummaryAsync(RequestOptions, CancellationToken)"/>
    public Task<UsageSummary> SummaryAsync(CancellationToken cancellationToken) => SummaryAsync(null, cancellationToken);
}
