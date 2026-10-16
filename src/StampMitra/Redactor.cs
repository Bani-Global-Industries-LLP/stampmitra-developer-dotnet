using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StampMitra;

/// <summary>Masks credentials and personal data before anything reaches a log sink.</summary>
public static class Redactor
{
    private const string Redacted = "[REDACTED]";

    private static readonly HashSet<string> SecretHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "x-api-key", "cookie", "set-cookie", "proxy-authorization",
    };

    // Identifiers: the last four characters stay visible.
    private static readonly HashSet<string> IdentifierKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "pan", "gstin", "accountnumber", "ifsc", "cin", "udyamnumber", "vehiclenumber", "regno", "dlnumber", "filenumber",
        "voterid", "phone", "din", "chassis", "engine", "micr", "utr", "email", "customermobile", "customeremail", "partypan",
    };

    // Personal data: removed entirely.
    private static readonly HashSet<string> PersonalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "businessname", "registeredname", "fathername", "ownername", "ownerfathername", "nameatbank",
        "fatherorhusbandname", "companyname", "tradename", "legalname", "dob", "address", "principalplaceofbusiness",
        "enterprisename", "customername", "partyname", "partyaddress",
    };

    /// <summary>Returns a copy of the headers with credentials replaced by <c>[REDACTED]</c>.</summary>
    /// <param name="headers">Header name/value pairs.</param>
    /// <returns>Redacted copy.</returns>
    public static IReadOnlyDictionary<string, string> Headers(IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in headers)
        {
            o[kv.Key] = SecretHeaders.Contains(kv.Key) ? Redacted : kv.Value;
        }

        return o;
    }

    /// <summary>Masks a JSON body: identifiers keep their last four characters, personal data is removed. Non-JSON text becomes a length marker.</summary>
    /// <param name="body">The raw body text.</param>
    /// <returns>Masked JSON text.</returns>
    public static string Body(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is JsonObject or JsonArray)
            {
                return Mask(node)?.ToJsonString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return "[non-JSON body, " + body.Length.ToString(CultureInfo.InvariantCulture) + " chars]";
    }

    private static JsonNode? Mask(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var kv in obj)
                {
                    result[kv.Key] = MaskMember(kv.Key, kv.Value);
                }

                return result;
            case JsonArray arr:
                var list = new JsonArray();
                foreach (var item in arr)
                {
                    list.Add(Mask(item));
                }

                return list;
            default:
                return Clone(node);
        }
    }

    // JsonNode.DeepClone() only exists from .NET 8; this package also targets net6.0.
    private static JsonNode? Clone(JsonNode? node) => node is null ? null : JsonNode.Parse(node.ToJsonString());

    private static JsonNode? MaskMember(string key, JsonNode? value)
    {
        if (value is JsonObject or JsonArray)
        {
            return Mask(value);
        }

        if (PersonalKeys.Contains(key))
        {
            return value is null ? null : JsonValue.Create(Redacted);
        }

        if (IdentifierKeys.Contains(key) && value is JsonValue)
        {
            var s = value.ToString();
            return JsonValue.Create(s.Length > 4 ? new string('*', s.Length - 4) + s[^4..] : "****");
        }

        return Clone(value);
    }
}
