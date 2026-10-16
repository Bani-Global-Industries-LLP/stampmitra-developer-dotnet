using System;
using System.Collections.Generic;

namespace StampMitra;

/// <summary>HTTP metadata of the response a model was built from (<see cref="Models.ApiModel.Response"/>).</summary>
public sealed class ApiResponseInfo
{
    internal ApiResponseInfo(int statusCode, IReadOnlyDictionary<string, string> headers, string? requestId, int attempts)
    {
        StatusCode = statusCode;
        Headers = headers;
        RequestId = requestId;
        Attempts = attempts;
    }

    /// <summary>HTTP status code (any 2xx; POSTs return 201).</summary>
    public int StatusCode { get; }

    /// <summary>Response headers (case-insensitive names, multiple values joined with ", ").</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The <c>x-request-id</c> header, when the server sent one.</summary>
    public string? RequestId { get; }

    /// <summary>Number of HTTP attempts made (1 means no retry).</summary>
    public int Attempts { get; }

    /// <summary>Reads a header case-insensitively.</summary>
    /// <param name="name">Header name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? Header(string name) => Headers.TryGetValue(name, out var v) ? v : null;

    /// <inheritdoc />
    public override string ToString() => $"HTTP {StatusCode} (attempts: {Attempts.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
}
