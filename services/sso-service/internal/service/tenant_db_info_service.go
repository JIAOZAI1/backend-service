package service

import (
	"context"
	"errors"
	"log"
	"time"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
)

// ErrTenantCodeNotFound 表示指定 tenant_code 不存在。
var ErrTenantCodeNotFound = errors.New("service: tenant not found for tenant code")

// tenantDbInfoCacheTTL 短 TTL：租户数据库信息极少变更（开户时生成一次即固定），
// 但密码轮换等运维操作生效延迟需要有上限，60s 内足够收敛，同时能过滤掉高频重复查询。
const tenantDbInfoCacheTTL = 60 * time.Second

// TenantDbInfoService 供集群内其他服务（如未来的租户服务）按 tenant_code 查询
// 租户数据库连接信息，走 Redis cache-aside 降低 MySQL 查询压力。
type TenantDbInfoService interface {
	GetTenantDbInfoInternal(ctx context.Context, tenantCode string) (model.TenantDbInfoResponse, error)
}

type tenantDbInfoService struct {
	tenantRepo repository.TenantRepository
	cacheRepo  repository.TenantCacheRepository
}

func NewTenantDbInfoService(tenantRepo repository.TenantRepository, cacheRepo repository.TenantCacheRepository) TenantDbInfoService {
	return &tenantDbInfoService{tenantRepo: tenantRepo, cacheRepo: cacheRepo}
}

func (s *tenantDbInfoService) GetTenantDbInfoInternal(ctx context.Context, tenantCode string) (model.TenantDbInfoResponse, error) {
	if tenant, err := s.cacheRepo.GetTenantDbInfo(ctx, tenantCode); err == nil {
		return model.NewTenantDbInfoResponse(tenant), nil
	} else if !errors.Is(err, repository.ErrTenantDbInfoCacheMiss) {
		// Redis 故障不阻断主流程，直接回源 MySQL，保证接口可用性优先于缓存命中率。
		log.Printf("tenant db info cache read failed, falling back to mysql: tenantCode=%s err=%v", tenantCode, err)
	}

	tenant, err := s.tenantRepo.GetByTenantCode(ctx, tenantCode)
	if err != nil {
		if errors.Is(err, repository.ErrTenantCodeNotFound) {
			return model.TenantDbInfoResponse{}, ErrTenantCodeNotFound
		}
		return model.TenantDbInfoResponse{}, err
	}

	if err := s.cacheRepo.SetTenantDbInfo(ctx, tenantCode, tenant, tenantDbInfoCacheTTL); err != nil {
		log.Printf("tenant db info cache write failed: tenantCode=%s err=%v", tenantCode, err)
	}

	return model.NewTenantDbInfoResponse(tenant), nil
}
