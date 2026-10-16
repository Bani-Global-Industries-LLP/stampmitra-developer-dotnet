using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Delivered webhook events (history and replay). Responses are <see cref="GenericResponse"/> because the API does not fix their shape.</summary>
public sealed class WebhookEventsResource
{
    private readonly Transport _transport;

    internal WebhookEventsResource(Transport transport)
    {
        _transport = transport;
    }

    /// <summary>Lists delivered webhook events (GET /v1/webhooks/events).</summary>
    /// <param name="limit">Page size (at least 1).</param>
    /// <param name="cursor">Opaque cursor from a previous response (<see cref="GenericResponse.NextCursor"/>).</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The page; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> ListAsync(int? limit = null, string? cursor = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (limit is < 1)
        {
            throw ValidationException.ForField("limit", "limit must be at least 1");
        }

        var query = new List<KeyValuePair<string, string?>>
        {
            new("limit", limit?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("cursor", cursor),
        };
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Get, "/v1/webhooks/events", null, query, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists delivered webhook events with default paging.</summary>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The first page.</returns>
    public Task<GenericResponse> ListAsync(CancellationToken cancellationToken) => ListAsync(null, null, null, cancellationToken);

    /// <summary>Replays a delivered event to your endpoint (POST /v1/webhooks/events/{id}/replay). Not retried automatically unless you pass <see cref="RequestOptions.IdempotencyKey"/>.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<GenericResponse> ReplayAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<GenericResponse>(HttpMethod.Post, "/v1/webhooks/events/{id}/replay", new string?[] { id }, null, null, options, false, r => new GenericResponse(r.Body), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ReplayAsync(string, RequestOptions, CancellationToken)"/>
    public Task<GenericResponse> ReplayAsync(string id, CancellationToken cancellationToken) => ReplayAsync(id, null, cancellationToken);
}
