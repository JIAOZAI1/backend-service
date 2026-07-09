using BackendJobService.Application.Common;
using BackendJobService.Application.Exceptions;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Common;

public class SortSpecTests
{
    private static readonly HashSet<string> AllowedFields = new(["id", "name"], StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Resolve_SortByNotProvided_UsesDefaultField()
    {
        var sort = SortSpec.Resolve(sortBy: null, SortOrder.Desc, AllowedFields, defaultField: "id");

        sort.SortBy.ShouldBe("id");
        sort.SortOrder.ShouldBe(SortOrder.Desc);
    }

    [Fact]
    public void Resolve_SortByMatchesAllowedField_CaseInsensitive()
    {
        var sort = SortSpec.Resolve(sortBy: "NAME", SortOrder.Asc, AllowedFields, defaultField: "id");

        sort.SortBy.ShouldBe("name");
    }

    [Fact]
    public void Resolve_SortByNotInAllowedFields_ThrowsValidationException()
    {
        Should.Throw<ValidationException>(() => SortSpec.Resolve(sortBy: "unknownField", SortOrder.Asc, AllowedFields, defaultField: "id"));
    }
}
