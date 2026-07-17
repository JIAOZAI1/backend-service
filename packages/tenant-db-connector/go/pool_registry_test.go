package tenantdbconnector

import (
	"database/sql"
	"sync/atomic"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// fakeOpen never dials (sql.Open is lazy), so it's safe to use in tests without a live MySQL.
func fakeOpen(openCount *int32) func(dsn string) (*sql.DB, error) {
	return func(dsn string) (*sql.DB, error) {
		atomic.AddInt32(openCount, 1)
		return sql.Open("mysql", dsn)
	}
}

func TestPoolRegistry_Get_ReusesPoolForSameTenant(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Hour, time.Hour)
	defer r.closeAll()

	db1, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)
	db2, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	assert.Same(t, db1, db2)
	assert.EqualValues(t, 1, atomic.LoadInt32(&opens))
}

func TestPoolRegistry_Get_DifferentTenantsGetDifferentPools(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Hour, time.Hour)
	defer r.closeAll()

	db1, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)
	db2, err := r.get("globex", "user:pass@tcp(127.0.0.1:3306)/globex_db")
	require.NoError(t, err)

	assert.NotSame(t, db1, db2)
	assert.EqualValues(t, 2, atomic.LoadInt32(&opens))
}

func TestPoolRegistry_Evict_ClosesAndRemoves(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Hour, time.Hour)
	defer r.closeAll()

	db1, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	require.NoError(t, r.evict("acme"))

	db2, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	assert.NotSame(t, db1, db2, "after evict, a new pool should be opened")
	assert.EqualValues(t, 2, atomic.LoadInt32(&opens))
}

func TestPoolRegistry_Evict_NonExistentTenant_NoError(t *testing.T) {
	r := newPoolRegistry(fakeOpen(new(int32)), defaultPoolConfig(), time.Hour, time.Hour)
	defer r.closeAll()

	assert.NoError(t, r.evict("does-not-exist"))
}

func TestPoolRegistry_SweepIdle_ClosesStaleTenantPools(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Millisecond, time.Hour)
	defer r.closeAll()

	_, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	time.Sleep(5 * time.Millisecond)
	r.sweepIdle()

	r.mu.Lock()
	_, stillTracked := r.pools["acme"]
	r.mu.Unlock()
	assert.False(t, stillTracked, "idle pool should have been swept")

	_, err = r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)
	assert.EqualValues(t, 2, atomic.LoadInt32(&opens), "accessing after sweep should reopen a pool")
}

func TestPoolRegistry_SweepIdle_KeepsRecentlyUsedTenantPools(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Hour, time.Hour)
	defer r.closeAll()

	_, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	r.sweepIdle()

	r.mu.Lock()
	_, stillTracked := r.pools["acme"]
	r.mu.Unlock()
	assert.True(t, stillTracked, "recently used pool should not be swept")
}

func TestPoolRegistry_CloseAll_ClosesEverythingAndStopsSweep(t *testing.T) {
	var opens int32
	r := newPoolRegistry(fakeOpen(&opens), defaultPoolConfig(), time.Hour, time.Millisecond)

	_, err := r.get("acme", "user:pass@tcp(127.0.0.1:3306)/acme_db")
	require.NoError(t, err)

	require.NoError(t, r.closeAll())

	r.mu.Lock()
	assert.Empty(t, r.pools)
	r.mu.Unlock()
}
