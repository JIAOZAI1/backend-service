package repository

import (
	"context"
	"errors"

	"gorm.io/gorm"

	"github.com/company/sso-service/internal/model"
)

// ErrTenantNotFound 表示该用户没有处于 active 状态的租户——可能尚未开户、租户还在
// created/审核中、或已 expired/cancelled。不是异常情况，调用方（VerifyAuthInternal）
// 据此判断是否设置 X-Tenant-Code 响应头，不是错误响应。
var ErrTenantNotFound = errors.New("repository: active tenant not found for user")

// ErrTenantCodeNotFound 表示指定 tenant_code 不存在（不区分 status，
// GetByTenantCode 按 code 精确查找，不做 active 过滤——数据库连接信息在
// 租户处于 created/expired 等非 active 状态时也可能被 backend-job-service 等
// 内部调用方需要，例如首次开户配置阶段）。
var ErrTenantCodeNotFound = errors.New("repository: tenant not found for tenant code")

type TenantRepository interface {
	// GetActiveTenantCodeByUserID 查询用户当前 active 状态租户的 tenant_code。
	// 只认 status=active 的租户：created（未开通完成）/expired（已过期）/cancelled（已取消）
	// 均视为"当前无有效租户"，与 GetActiveTenantCodeByUserID 找不到关联行时的处理一致。
	GetActiveTenantCodeByUserID(ctx context.Context, userID uint64) (string, error)

	// GetByTenantCode 按 tenant_code 查询租户完整信息（含数据库连接信息）。
	GetByTenantCode(ctx context.Context, tenantCode string) (*model.Tenant, error)
}

type tenantRepository struct {
	db *gorm.DB
}

func NewTenantRepository(db *gorm.DB) TenantRepository {
	return &tenantRepository{db: db}
}

func (r *tenantRepository) GetActiveTenantCodeByUserID(ctx context.Context, userID uint64) (string, error) {
	var tenant model.Tenant
	err := r.db.WithContext(ctx).
		Joins("JOIN user_tenants ON user_tenants.tenant_id = tenants.id").
		Where("user_tenants.user_id = ? AND user_tenants.deleted_at IS NULL", userID).
		Where("tenants.status = ? AND tenants.deleted_at IS NULL", "active").
		First(&tenant).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return "", ErrTenantNotFound
	}
	if err != nil {
		return "", err
	}
	return tenant.TenantCode, nil
}

func (r *tenantRepository) GetByTenantCode(ctx context.Context, tenantCode string) (*model.Tenant, error) {
	var tenant model.Tenant
	err := r.db.WithContext(ctx).
		Where("tenant_code = ? AND deleted_at IS NULL", tenantCode).
		First(&tenant).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrTenantCodeNotFound
	}
	if err != nil {
		return nil, err
	}
	return &tenant, nil
}
