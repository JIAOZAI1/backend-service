using BackendJobService.Application.Exceptions;

namespace BackendJobService.Application.Common;

/// <summary>
/// 分页列表接口的排序规范。所有支持分页的列表接口（ListXxxAsync）统一通过
/// sortBy + sortOrder 两个参数指定排序字段与方向，可排序字段必须显式声明白名单，
/// 防止任意字段名透传到 ORM 排序表达式。
/// </summary>
public class SortSpec
{
    public string SortBy { get; }
    public SortOrder SortOrder { get; }

    private SortSpec(string sortBy, SortOrder sortOrder)
    {
        SortBy = sortBy;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// 解析并校验请求中的 sortBy，未传时使用 <paramref name="defaultField"/>。
    /// </summary>
    /// <param name="sortBy">请求中的排序字段名（大小写不敏感），为空则使用默认字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <param name="allowedFields">该资源允许排序的字段白名单</param>
    /// <param name="defaultField">未指定 sortBy 时使用的默认字段，必须在白名单内</param>
    public static SortSpec Resolve(string? sortBy, SortOrder sortOrder, IReadOnlySet<string> allowedFields, string defaultField)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return new SortSpec(defaultField, sortOrder);
        }

        var match = allowedFields.FirstOrDefault(f => string.Equals(f, sortBy, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new ValidationException(
                $"unsupported sortBy: {sortBy}. allowed values: {string.Join(", ", allowedFields)}");
        }

        return new SortSpec(match, sortOrder);
    }
}
