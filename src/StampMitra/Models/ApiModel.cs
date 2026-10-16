using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampMitra.Models;

/// <summary>
/// Base type of every response model. Fields the server sent that this SDK version does not model are preserved in
/// <see cref="AdditionalProperties"/>; unknown fields never cause an error.
/// </summary>
public abstract record ApiModel
{
    /// <summary>Response fields not modelled by this SDK version (name to raw JSON value).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    /// <summary>HTTP metadata (status, headers, request id, attempts) when the model came from a call.</summary>
    [JsonIgnore]
    public ApiResponseInfo? Response { get; internal set; }
}
