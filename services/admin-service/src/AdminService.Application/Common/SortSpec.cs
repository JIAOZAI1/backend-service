using AdminService.Application.Exceptions;

namespace AdminService.Application.Common;

/// <summary>
/// 分页列表接口的排序规范。所有支持分页的列表接口统一通过 sortBy + sortOrder 两个参数
/// 指定排序字段与方向，可排序字段必须显式声明白名单，防止任意字段名透传到 ORM 排序表达式。
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
