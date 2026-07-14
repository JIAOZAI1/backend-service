namespace AdminService.Application.DTOs;

public class PagedResult<T>
{
    public required List<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }
}
