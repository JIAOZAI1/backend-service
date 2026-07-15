package middleware

import "context"

// TenantLister 抽象了按用户 ID 查当前 active 租户 tenant_code 的能力，供
// handler.VerifyAuthInternal 使用。实现见 repository.TenantRepository。
type TenantLister interface {
	// 找不到 active 租户时返回 error（哨兵值，见 repository.ErrTenantNotFound），
	// 不是异常，调用方据此判断是否设置 X-Tenant-Code 响应头。
	GetActiveTenantCodeByUserID(ctx context.Context, userID uint64) (string, error)
}
