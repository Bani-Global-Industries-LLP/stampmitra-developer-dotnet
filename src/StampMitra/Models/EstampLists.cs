using System.Collections;
using System.Collections.Generic;

namespace StampMitra.Models;

/// <summary>The districts of a state (<c>GET /v1/estamp/states/{stateCode}/districts</c> returns a bare JSON array).</summary>
public sealed record EstampDistrictList : ApiModel, IReadOnlyList<EstampDistrict>
{
    /// <summary>Creates a list.</summary>
    /// <param name="items">The districts.</param>
    public EstampDistrictList(IReadOnlyList<EstampDistrict> items)
    {
        Items = items;
    }

    /// <summary>The districts.</summary>
    public IReadOnlyList<EstampDistrict> Items { get; }

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public EstampDistrict this[int index] => Items[index];

    /// <inheritdoc />
    public IEnumerator<EstampDistrict> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>The sub-registrar offices of a district (a bare JSON array).</summary>
public sealed record EstampSroList : ApiModel, IReadOnlyList<EstampSro>
{
    /// <summary>Creates a list.</summary>
    /// <param name="items">The offices.</param>
    public EstampSroList(IReadOnlyList<EstampSro> items)
    {
        Items = items;
    }

    /// <summary>The offices.</summary>
    public IReadOnlyList<EstampSro> Items { get; }

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public EstampSro this[int index] => Items[index];

    /// <inheritdoc />
    public IEnumerator<EstampSro> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
