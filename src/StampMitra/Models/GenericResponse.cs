using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StampMitra.Models;

/// <summary>
/// A response whose schema the API does not fix (e-Stamp reference data, duty and parties, webhook events). The decoded
/// JSON is available as <see cref="Data"/>; nothing is dropped.
/// </summary>
public sealed record GenericResponse : ApiModel
{
    /// <summary>Creates a response wrapping decoded JSON.</summary>
    /// <param name="data">The decoded body.</param>
    public GenericResponse(JsonElement data)
    {
        Data = data;
    }

    /// <summary>The complete decoded response body (an object, an array or <see cref="JsonValueKind.Undefined"/> for an empty body).</summary>
    public JsonElement Data { get; }

    /// <summary>The raw JSON text of <see cref="Data"/>.</summary>
    public string RawJson => Data.ValueKind == JsonValueKind.Undefined ? string.Empty : Data.GetRawText();

    /// <summary>Reads a top-level property of an object response.</summary>
    /// <param name="name">Property name (case-sensitive).</param>
    /// <returns>The value, or <see langword="null"/> when the body is not an object or has no such property.</returns>
    public JsonElement? Get(string name) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) ? v : null;

    /// <summary>The elements of a list-shaped response: a bare JSON array, or the <c>items</c> array of an object.</summary>
    public IReadOnlyList<JsonElement> Items
    {
        get
        {
            if (Data.ValueKind == JsonValueKind.Array)
            {
                return Data.EnumerateArray().ToList();
            }

            if (Get("items") is { ValueKind: JsonValueKind.Array } items)
            {
                return items.EnumerateArray().ToList();
            }

            return new List<JsonElement>();
        }
    }

    /// <summary>The <c>nextCursor</c> of a paged response, or <see langword="null"/>.</summary>
    public string? NextCursor =>
        Get("nextCursor") is { ValueKind: JsonValueKind.String } c && c.GetString() is { Length: > 0 } s ? s : null;
}
