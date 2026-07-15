using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;

namespace AdminService.Application.Services;

public class TenantInternalService(ITenantRepository tenantRepository) : ITenantInternalService
{
    public async Task ActivateAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException($"tenant {tenantId} not found");

        if (tenant.Status == TenantStatus.Active)
        {
            return;
        }

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        await tenantRepository.SaveChangesAsync(cancellationToken);
    }
}
