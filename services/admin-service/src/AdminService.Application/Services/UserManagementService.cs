using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;

namespace AdminService.Application.Services;

public class UserManagementService(IUserManagementRepository repository) : IUserManagementService
{
    // 与 sso-service 的 bcrypt.DefaultCost（Go golang.org/x/crypto/bcrypt 定义为 10）保持一致，
    // 显式指定而非依赖 BCrypt.Net-Next 的库默认值，避免未来两边默认值不同步导致新旧密码
    // 强度不一致。bcrypt 密文自带 cost 参数，sso-service 登录校验时按密文里的 cost 验证，
    // 不受写入方是哪个实现影响。
    private const int BcryptWorkFactor = 10;

    private static readonly IReadOnlySet<string> _sortFields =
        new HashSet<string>(["id", "username", "createdAt"], StringComparer.OrdinalIgnoreCase);

    public async Task<PagedResult<UserWithTenantResponse>> ListUsersAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken)
    {
        var sort = SortSpec.Resolve(sortBy, sortOrder, _sortFields, defaultField: "id");
        var (items, total) = await repository.ListUsersWithTenantAsync(page, pageSize, sort, cancellationToken);

        return new PagedResult<UserWithTenantResponse>
        {
            Items = items.Select(row => new UserWithTenantResponse
            {
                Id = row.Id,
                Username = row.Username,
                Email = row.Email,
                Roles = row.Roles,
                TenantCode = row.TenantCode,
                LicenseExpiresAt = row.LicenseExpiresAt,
                CreatedAt = row.CreatedAt,
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(ulong userId, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"user {userId} not found");

        var newPassword = SecurePasswordGenerator.Generate();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, BcryptWorkFactor);

        await repository.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResponse(newPassword);
    }
}
