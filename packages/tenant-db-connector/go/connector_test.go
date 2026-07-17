package tenantdbconnector

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNew_NilSsoGetter_ReturnsError(t *testing.T) {
	_, err := New(nil, nil)
	assert.Error(t, err)
}

func TestConnector_GetTenantDbInfo_ReturnsSsoGetterResult(t *testing.T) {
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	})
	require.NoError(t, err)
	defer c.Close()

	info, err := c.GetTenantDbInfo(context.Background(), "acme")
	require.NoError(t, err)
	assert.Equal(t, testInfo("acme"), info)
}

func TestConnector_GetDB_ReturnsUsablePoolHandle(t *testing.T) {
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	})
	require.NoError(t, err)
	defer c.Close()

	db, err := c.GetDB(context.Background(), "acme")
	require.NoError(t, err)
	require.NotNil(t, db)
}

func TestConnector_GetDB_ReusesPoolAcrossCalls(t *testing.T) {
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	})
	require.NoError(t, err)
	defer c.Close()

	db1, err := c.GetDB(context.Background(), "acme")
	require.NoError(t, err)
	db2, err := c.GetDB(context.Background(), "acme")
	require.NoError(t, err)

	assert.Same(t, db1, db2)
}

func TestConnector_GetDB_SsoGetterError_Propagates(t *testing.T) {
	wantErr := errors.New("tenant not found")
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return TenantDbInfo{}, wantErr
	})
	require.NoError(t, err)
	defer c.Close()

	_, err = c.GetDB(context.Background(), "acme")
	require.Error(t, err)
	assert.ErrorIs(t, err, wantErr)
}

func TestConnector_InvalidateTenantDbInfo_ForcesPoolRebuild(t *testing.T) {
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	})
	require.NoError(t, err)
	defer c.Close()

	db1, err := c.GetDB(context.Background(), "acme")
	require.NoError(t, err)

	require.NoError(t, c.InvalidateTenantDbInfo(context.Background(), "acme"))

	db2, err := c.GetDB(context.Background(), "acme")
	require.NoError(t, err)

	assert.NotSame(t, db1, db2)
}

func TestConnector_Close_RejectsFurtherCalls(t *testing.T) {
	c, err := New(nil, func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
		return testInfo(tenantCode), nil
	})
	require.NoError(t, err)

	require.NoError(t, c.Close())
	require.NoError(t, c.Close(), "Close should be idempotent")

	_, err = c.GetTenantDbInfo(context.Background(), "acme")
	assert.ErrorIs(t, err, ErrClosed)

	_, err = c.GetDB(context.Background(), "acme")
	assert.ErrorIs(t, err, ErrClosed)

	err = c.InvalidateTenantDbInfo(context.Background(), "acme")
	assert.ErrorIs(t, err, ErrClosed)
}

func TestConnector_CustomDSNBuilder_IsUsedForPoolOpen(t *testing.T) {
	var gotInfo TenantDbInfo
	c, err := New(nil,
		func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
			return testInfo(tenantCode), nil
		},
		WithDSNBuilder(func(info TenantDbInfo) string {
			gotInfo = info
			return MySQLDSN(info)
		}),
	)
	require.NoError(t, err)
	defer c.Close()

	_, err = c.GetDB(context.Background(), "acme")
	require.NoError(t, err)
	assert.Equal(t, testInfo("acme"), gotInfo)
}

func TestConnector_WithLocalCacheTTL_IsApplied(t *testing.T) {
	c, err := New(nil,
		func(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
			return testInfo(tenantCode), nil
		},
		WithLocalCacheTTL(time.Hour),
	)
	require.NoError(t, err)
	defer c.Close()

	assert.Equal(t, time.Hour, c.info.localTTL)
}
