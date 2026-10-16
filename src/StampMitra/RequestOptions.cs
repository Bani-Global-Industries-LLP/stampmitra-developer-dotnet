using System;
using System.Collections.Generic;

namespace StampMitra;

/// <summary>Per-call overrides accepted by every SDK method. Unset members fall back to the client's settings.</summary>
public sealed record RequestOptions
{
    /// <summary>Timeout for each attempt. Overrides <see cref="StampMitraClientOptions.Timeout"/>.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Maximum automatic retries for this call (0 disables retrying).</summary>
    public int? MaxRetries { get; init; }

    /// <summary>
    /// Your own <c>Idempotency-Key</c> (printable ASCII, at most 255 characters). When omitted the SDK generates a random
    /// UUIDv4 for every POST except webhook replay and reuses it across automatic retries.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Extra headers for this call. <c>Authorization</c> and <c>x-api-key</c> cannot be overridden.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
