using System;

namespace StampMitra;

/// <summary>
/// Base class of every error raised by the SDK. For API errors <see cref="Status"/>, <see cref="Code"/>,
/// <see cref="RequestId"/> and <see cref="Field"/> come from the response; errors raised locally (client-side
/// validation, network failures) have a <see langword="null"/> <see cref="Status"/>. Messages and stack traces never
/// contain the API key.
/// </summary>
public class StampMitraException : Exception
{
    /// <summary>Creates an exception with a message only.</summary>
    public StampMitraException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception wrapping an inner exception.</summary>
    public StampMitraException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception with the full set of API error details.</summary>
    /// <param name="message">Human-readable message (the server message, verbatim, for API errors).</param>
    /// <param name="status">HTTP status code, or <see langword="null"/> when no response was received.</param>
    /// <param name="code">Machine-readable error code from the response body, e.g. <c>VALIDATION_ERROR</c>.</param>
    /// <param name="requestId">Server-side request id from the response body. Quote it when contacting support.</param>
    /// <param name="field">The offending input field for validation errors, when known.</param>
    /// <param name="body">The raw response body (truncated), when one was received.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public StampMitraException(string message, int? status, string? code, string? requestId, string? field, string? body, Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        Code = code;
        RequestId = requestId;
        Field = field;
        Body = body;
    }

    /// <summary>HTTP status code, or <see langword="null"/> when the error did not come from a response.</summary>
    public int? Status { get; }

    /// <summary>Machine-readable error code, e.g. <c>VALIDATION_ERROR</c>.</summary>
    public string? Code { get; }

    /// <summary>Server-side request id.</summary>
    public string? RequestId { get; }

    /// <summary>The offending input field for validation errors, when known.</summary>
    public string? Field { get; }

    /// <summary>The raw response body (truncated to 8 KiB), when one was received.</summary>
    public string? Body { get; }
}

/// <summary>HTTP 400, or input rejected locally by the client-side validators (no network call, nothing billed).</summary>
public class ValidationException : StampMitraException
{
    /// <summary>Creates a validation exception.</summary>
    public ValidationException(string message, int? status = null, string? code = null, string? requestId = null, string? field = null, string? body = null)
        : base(message, status, code ?? "VALIDATION_ERROR", requestId, field, body)
    {
    }

    internal static ValidationException ForField(string field, string message) =>
        new(message, null, "VALIDATION_ERROR", null, field, null);
}

/// <summary>HTTP 401: missing or invalid API key (also raised locally when no key is configured).</summary>
public class AuthenticationException : StampMitraException
{
    /// <summary>Creates an authentication exception.</summary>
    public AuthenticationException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>HTTP 403: the key is not allowed to do this (for example an IP allowlist). The server message is kept verbatim.</summary>
public class PermissionDeniedException : StampMitraException
{
    /// <summary>Creates a permission exception.</summary>
    public PermissionDeniedException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>HTTP 402: insufficient balance or an unpaid plan.</summary>
public class PaymentRequiredException : StampMitraException
{
    /// <summary>Creates a payment-required exception.</summary>
    public PaymentRequiredException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>HTTP 404.</summary>
public class NotFoundException : StampMitraException
{
    /// <summary>Creates a not-found exception.</summary>
    public NotFoundException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>HTTP 409.</summary>
public class ConflictException : StampMitraException
{
    /// <summary>Creates a conflict exception.</summary>
    public ConflictException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>HTTP 429.</summary>
public class RateLimitException : StampMitraException
{
    /// <summary>Creates a rate-limit exception.</summary>
    public RateLimitException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, TimeSpan? retryAfter = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>The <c>Retry-After</c> value when the server sent one (the API itself currently does not).</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>HTTP 5xx.</summary>
public class UpstreamException : StampMitraException
{
    /// <summary>Creates an upstream exception.</summary>
    public UpstreamException(string message, int? status = null, string? code = null, string? requestId = null, string? body = null, string? field = null)
        : base(message, status, code, requestId, field, body)
    {
    }
}

/// <summary>The request never produced a response (DNS failure, connection refused or dropped, TLS failure).</summary>
public class ApiConnectionException : StampMitraException
{
    /// <summary>Creates a connection exception.</summary>
    public ApiConnectionException(string message, Exception? innerException = null)
        : base(message, null, "CONNECTION_ERROR", null, null, null, innerException)
    {
    }
}

/// <summary>The per-attempt timeout elapsed before a response arrived.</summary>
public class ApiTimeoutException : ApiConnectionException
{
    /// <summary>Creates a timeout exception.</summary>
    public ApiTimeoutException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Webhook verification or parsing failed.</summary>
public class SignatureVerificationException : StampMitraException
{
    /// <summary>Creates a signature verification exception.</summary>
    public SignatureVerificationException(string message, string code)
        : base(message, null, code, null, null, null)
    {
    }
}
