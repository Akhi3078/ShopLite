namespace Catalog.Application.Common;

/// <summary>
/// A page of results. Returned rather than a bare array so that adding pagination
/// metadata later is not a breaking change to the response shape.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
