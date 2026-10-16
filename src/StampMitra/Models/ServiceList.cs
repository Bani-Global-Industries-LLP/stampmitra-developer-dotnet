using System.Collections;
using System.Collections.Generic;

namespace StampMitra.Models;

/// <summary>The services this key is entitled to (<c>GET /v1/services</c> returns a bare JSON array).</summary>
public sealed record ServiceList : ApiModel, IReadOnlyList<Service>
{
    /// <summary>Creates a list.</summary>
    /// <param name="items">The services.</param>
    public ServiceList(IReadOnlyList<Service> items)
    {
        Items = items;
    }

    /// <summary>The services.</summary>
    public IReadOnlyList<Service> Items { get; }

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public Service this[int index] => Items[index];

    /// <inheritdoc />
    public IEnumerator<Service> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
