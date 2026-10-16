using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Filters for <see cref="LogsResource"/>.</summary>
public sealed record LogsListParams
{
    /// <summary>Lower bound (inclusive).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Upper bound (exclusive).</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Filter to one service code, e.g. <c>pan.verify</c>.</summary>
    public string? Service { get; init; }

    /// <summary><c>success</c> or <c>failure</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Page size, 1 to 200 (the API default is 50).</summary>
    public int? Limit { get; init; }

    /// <summary>Opaque cursor from a previous page (<see cref="LogsPage.NextCursor"/>). <see cref="LogsResource.ListAllAsync(LogsListParams?, RequestOptions?, CancellationToken)"/> honours a starting cursor.</summary>
    public string? Cursor { get; init; }
}

/// <summary>API request logs.</summary>
public sealed class LogsResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal LogsResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Fetches one page of logs (GET /v1/logs), newest first.</summary>
    /// <param name="parameters">Filters and paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The page; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<LogsPage> ListAsync(LogsListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        parameters ??= new LogsListParams();
        if (_validate)
        {
            if (parameters.Status is not null && parameters.Status is not ("success" or "failure"))
            {
                throw ValidationException.ForField("status", "status must be 'success' or 'failure'");
            }

            if (parameters.Limit is < 1 or > 200)
            {
                throw ValidationException.ForField("limit", "limit must be between 1 and 200");
            }
        }

        var query = new List<KeyValuePair<string, string?>>
        {
            new("from", Format(parameters.From)),
            new("to", Format(parameters.To)),
            new("service", parameters.Service),
            new("status", parameters.Status),
            new("limit", parameters.Limit?.ToString(CultureInfo.InvariantCulture)),
            new("cursor", parameters.Cursor),
        };
        return await _transport.RequestAsync<LogsPage>(HttpMethod.Get, "/v1/logs", null, query, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListAsync(LogsListParams, RequestOptions, CancellationToken)"/>
    public Task<LogsPage> ListAsync(LogsListParams? parameters, CancellationToken cancellationToken) => ListAsync(parameters, null, cancellationToken);

    /// <summary>
    /// Streams every matching log entry, following <c>nextCursor</c> lazily until the last page. Stop early by breaking out
    /// of the loop or cancelling the token.
    /// </summary>
    /// <param name="parameters">Same filters as <see cref="ListAsync(LogsListParams, RequestOptions, CancellationToken)"/>; a starting cursor is honoured.</param>
    /// <param name="options">Per-call overrides applied to every page request.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>An asynchronous sequence of log entries.</returns>
    public async IAsyncEnumerable<LogEntry> ListAllAsync(LogsListParams? parameters = null, RequestOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = parameters ?? new LogsListParams();
        while (true)
        {
            var page = await ListAsync(current, options, cancellationToken).ConfigureAwait(false);
            if (page.Items is not null)
            {
                foreach (var entry in page.Items)
                {
                    yield return entry;
                }
            }

            var next = page.NextCursor;
            if (string.IsNullOrEmpty(next))
            {
                yield break;
            }

            if (string.Equals(next, current.Cursor, StringComparison.Ordinal))
            {
                throw new StampMitraException("The API returned the same pagination cursor twice; stopping to avoid an infinite loop.", 200, "INVALID_RESPONSE", page.Response?.RequestId, null, null);
            }

            current = current with { Cursor = next };
        }
    }

    private static string? Format(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
