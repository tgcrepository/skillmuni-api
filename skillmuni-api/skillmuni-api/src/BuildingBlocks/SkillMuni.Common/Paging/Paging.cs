namespace SkillMuni.Common.Paging;

/// <summary><c>?page=1&amp;pageSize=20</c>, clamped to a page size of at most 100.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Create(int? page, int? pageSize) =>
        new(Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}

/// <summary>The list shape every paged endpoint returns: <c>{ items, page, pageSize, total }</c>.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), Page, PageSize, Total);
}
