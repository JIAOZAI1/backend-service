package repository

import (
	"context"
	"encoding/json"
	"errors"
	"time"

	"github.com/redis/go-redis/v9"

	"github.com/company/sso-service/internal/model"
)

// ErrTenantDbInfoCacheMiss 表示 Redis 中没有该 tenant_code 对应的缓存项。
var ErrTenantDbInfoCacheMiss = errors.New("repository: tenant db info cache miss")

const tenantDbInfoKeyPrefix = "sso:tenant-db-info:"

// TenantCacheRepository 缓存租户数据库连接信息（含明文 db_password，见 model.Tenant
// 字段说明），供高频调用的 GetTenantDbInfoInternal 接口降低 MySQL 查询压力。
type TenantCacheRepository interface {
	GetTenantDbInfo(ctx context.Context, tenantCode string) (*model.Tenant, error)
	SetTenantDbInfo(ctx context.Context, tenantCode string, tenant *model.Tenant, ttl time.Duration) error
	DeleteTenantDbInfo(ctx context.Context, tenantCode string) error
}

type tenantCacheRepository struct {
	rdb *redis.Client
}

func NewTenantCacheRepository(rdb *redis.Client) TenantCacheRepository {
	return &tenantCacheRepository{rdb: rdb}
}

func (r *tenantCacheRepository) GetTenantDbInfo(ctx context.Context, tenantCode string) (*model.Tenant, error) {
	val, err := r.rdb.Get(ctx, tenantDbInfoKeyPrefix+tenantCode).Bytes()
	if errors.Is(err, redis.Nil) {
		return nil, ErrTenantDbInfoCacheMiss
	}
	if err != nil {
		return nil, err
	}

	var tenant model.Tenant
	if err := json.Unmarshal(val, &tenant); err != nil {
		return nil, err
	}
	return &tenant, nil
}

func (r *tenantCacheRepository) SetTenantDbInfo(ctx context.Context, tenantCode string, tenant *model.Tenant, ttl time.Duration) error {
	val, err := json.Marshal(tenant)
	if err != nil {
		return err
	}
	return r.rdb.Set(ctx, tenantDbInfoKeyPrefix+tenantCode, val, ttl).Err()
}

// DeleteTenantDbInfo 清除缓存项，供租户数据库信息变更（如密码轮换）时主动失效使用。
func (r *tenantCacheRepository) DeleteTenantDbInfo(ctx context.Context, tenantCode string) error {
	return r.rdb.Del(ctx, tenantDbInfoKeyPrefix+tenantCode).Err()
}
