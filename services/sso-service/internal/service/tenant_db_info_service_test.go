package service

import (
	"context"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
)

type fakeTenantRepo struct {
	byCode  map[string]*model.Tenant
	callCnt int
}

func newFakeTenantRepo() *fakeTenantRepo {
	return &fakeTenantRepo{byCode: make(map[string]*model.Tenant)}
}

func (f *fakeTenantRepo) GetActiveTenantCodeByUserID(_ context.Context, _ uint64) (string, error) {
	return "", repository.ErrTenantNotFound
}

func (f *fakeTenantRepo) GetByTenantCode(_ context.Context, tenantCode string) (*model.Tenant, error) {
	f.callCnt++
	t, ok := f.byCode[tenantCode]
	if !ok {
		return nil, repository.ErrTenantCodeNotFound
	}
	return t, nil
}

type fakeTenantCacheRepo struct {
	data map[string]*model.Tenant
}

func newFakeTenantCacheRepo() *fakeTenantCacheRepo {
	return &fakeTenantCacheRepo{data: make(map[string]*model.Tenant)}
}

func (f *fakeTenantCacheRepo) GetTenantDbInfo(_ context.Context, tenantCode string) (*model.Tenant, error) {
	t, ok := f.data[tenantCode]
	if !ok {
		return nil, repository.ErrTenantDbInfoCacheMiss
	}
	return t, nil
}

func (f *fakeTenantCacheRepo) SetTenantDbInfo(_ context.Context, tenantCode string, tenant *model.Tenant, _ time.Duration) error {
	f.data[tenantCode] = tenant
	return nil
}

func (f *fakeTenantCacheRepo) DeleteTenantDbInfo(_ context.Context, tenantCode string) error {
	delete(f.data, tenantCode)
	return nil
}

func TestGetTenantDbInfoInternal_CacheMiss_FallsBackToRepoAndPopulatesCache(t *testing.T) {
	tenantRepo := newFakeTenantRepo()
	tenantRepo.byCode["t-001"] = &model.Tenant{
		TenantCode: "t-001", DbHost: "10.0.0.1", DbPort: 3306,
		DbName: "tenant_t001", DbUsername: "tenant_t001", DbPassword: "s3cr3t",
	}
	cacheRepo := newFakeTenantCacheRepo()
	svc := NewTenantDbInfoService(tenantRepo, cacheRepo)

	resp, err := svc.GetTenantDbInfoInternal(context.Background(), "t-001")

	require.NoError(t, err)
	assert.Equal(t, "10.0.0.1", resp.DbHost)
	assert.Equal(t, "s3cr3t", resp.DbPassword)
	assert.Equal(t, 1, tenantRepo.callCnt)

	// 缓存已回填：下一次查询不应再打 MySQL
	_, err = svc.GetTenantDbInfoInternal(context.Background(), "t-001")
	require.NoError(t, err)
	assert.Equal(t, 1, tenantRepo.callCnt)
}

func TestGetTenantDbInfoInternal_CacheHit_SkipsRepo(t *testing.T) {
	tenantRepo := newFakeTenantRepo()
	cacheRepo := newFakeTenantCacheRepo()
	cacheRepo.data["t-002"] = &model.Tenant{TenantCode: "t-002", DbHost: "cached-host"}
	svc := NewTenantDbInfoService(tenantRepo, cacheRepo)

	resp, err := svc.GetTenantDbInfoInternal(context.Background(), "t-002")

	require.NoError(t, err)
	assert.Equal(t, "cached-host", resp.DbHost)
	assert.Equal(t, 0, tenantRepo.callCnt)
}

func TestGetTenantDbInfoInternal_UnknownTenantCode_ReturnsNotFound(t *testing.T) {
	tenantRepo := newFakeTenantRepo()
	cacheRepo := newFakeTenantCacheRepo()
	svc := NewTenantDbInfoService(tenantRepo, cacheRepo)

	_, err := svc.GetTenantDbInfoInternal(context.Background(), "does-not-exist")

	assert.ErrorIs(t, err, ErrTenantCodeNotFound)
}
