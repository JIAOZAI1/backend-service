package tenantdbconnector

import (
	"context"
	"errors"
	"sync/atomic"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func testInfo(tenantCode string) TenantDbInfo {
	return TenantDbInfo{
		TenantCode: tenantCode,
		DbHost:     "127.0.0.1",
		DbPort:     3306,
		DbName:     tenantCode + "_db",
		DbUsername: "app",
		DbPassword: "secret",
	}
}

func TestInfoCache_LocalCacheHit_DoesNotCallSsoGetter(t *testing.T) {
	var calls int32
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		atomic.AddInt32(&calls, 1)
		return testInfo(tenantCode), nil
	}

	c := newInfoCache(nil, getter, time.Minute, time.Minute)

	info1, err := c.get(context.Background(), "acme")
	require.NoError(t, err)
	info2, err := c.get(context.Background(), "acme")
	require.NoError(t, err)

	assert.Equal(t, testInfo("acme"), info1)
	assert.Equal(t, info1, info2)
	assert.EqualValues(t, 1, atomic.LoadInt32(&calls), "second call should be served from local cache")
}

func TestInfoCache_LocalCacheExpired_CallsSsoGetterAgain(t *testing.T) {
	var calls int32
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		atomic.AddInt32(&calls, 1)
		return testInfo(tenantCode), nil
	}

	c := newInfoCache(nil, getter, time.Millisecond, time.Minute)

	_, err := c.get(context.Background(), "acme")
	require.NoError(t, err)

	time.Sleep(5 * time.Millisecond)

	_, err = c.get(context.Background(), "acme")
	require.NoError(t, err)

	assert.EqualValues(t, 2, atomic.LoadInt32(&calls))
}

func TestInfoCache_SsoGetterError_Propagates(t *testing.T) {
	wantErr := errors.New("boom")
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return TenantDbInfo{}, wantErr
	}

	c := newInfoCache(nil, getter, time.Minute, time.Minute)

	_, err := c.get(context.Background(), "acme")
	require.Error(t, err)
	assert.ErrorIs(t, err, wantErr)
}

func TestInfoCache_ConcurrentMiss_SingleflightsSsoGetter(t *testing.T) {
	var calls int32
	block := make(chan struct{})
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		atomic.AddInt32(&calls, 1)
		<-block
		return testInfo(tenantCode), nil
	}

	c := newInfoCache(nil, getter, time.Minute, time.Minute)

	const n = 20
	results := make(chan TenantDbInfo, n)
	for i := 0; i < n; i++ {
		go func() {
			info, err := c.get(context.Background(), "acme")
			assert.NoError(t, err)
			results <- info
		}()
	}

	time.Sleep(20 * time.Millisecond) // let all goroutines reach the blocking getter
	close(block)

	for i := 0; i < n; i++ {
		info := <-results
		assert.Equal(t, testInfo("acme"), info)
	}
	assert.EqualValues(t, 1, atomic.LoadInt32(&calls), "concurrent misses for same tenant should collapse into one sso call")
}

func TestInfoCache_DifferentTenants_AreIndependent(t *testing.T) {
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	}

	c := newInfoCache(nil, getter, time.Minute, time.Minute)

	acme, err := c.get(context.Background(), "acme")
	require.NoError(t, err)
	globex, err := c.get(context.Background(), "globex")
	require.NoError(t, err)

	assert.Equal(t, "acme", acme.TenantCode)
	assert.Equal(t, "globex", globex.TenantCode)
}

func TestInfoCache_Invalidate_ForcesNextGetToRefetch(t *testing.T) {
	var calls int32
	getter := func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		atomic.AddInt32(&calls, 1)
		return testInfo(tenantCode), nil
	}

	c := newInfoCache(nil, getter, time.Minute, time.Minute)

	_, err := c.get(context.Background(), "acme")
	require.NoError(t, err)

	require.NoError(t, c.invalidate(context.Background(), "acme"))

	_, err = c.get(context.Background(), "acme")
	require.NoError(t, err)

	assert.EqualValues(t, 2, atomic.LoadInt32(&calls))
}
