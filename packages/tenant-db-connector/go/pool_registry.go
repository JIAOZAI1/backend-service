package tenantdbconnector

import (
	"database/sql"
	"sync"
	"time"
)

type poolEntry struct {
	db         *sql.DB
	lastUsedAt time.Time
}

// poolRegistry 按 tenantCode 维护常驻的 *sql.DB 连接池，同一租户的并发 GetDB 调用复用
// 同一个池，不会重复建池导致连接数暴涨。长期未被访问的池由 sweep 定期关闭回收，避免
// 租户数量增长后占满下游 MySQL 的 max_connections。
type poolRegistry struct {
	mu         sync.Mutex
	pools      map[string]*poolEntry
	openFunc   func(dsn string) (*sql.DB, error)
	poolConfig PoolConfig
	idleTTL    time.Duration

	stopSweep chan struct{}
	sweepDone chan struct{}
}

func newPoolRegistry(openFunc func(dsn string) (*sql.DB, error), poolConfig PoolConfig, idleTTL, sweepInterval time.Duration) *poolRegistry {
	r := &poolRegistry{
		pools:      make(map[string]*poolEntry),
		openFunc:   openFunc,
		poolConfig: poolConfig,
		idleTTL:    idleTTL,
		stopSweep:  make(chan struct{}),
		sweepDone:  make(chan struct{}),
	}
	go r.sweepLoop(sweepInterval)
	return r
}

// get 返回 tenantCode 对应的连接池，不存在则用 dsn 新建。newDSN 只在需要新建/重建时被
// 调用方求值传入，避免每次 get 都重复拼接 DSN。
func (r *poolRegistry) get(tenantCode, dsn string) (*sql.DB, error) {
	r.mu.Lock()
	defer r.mu.Unlock()

	if entry, ok := r.pools[tenantCode]; ok {
		entry.lastUsedAt = time.Now()
		return entry.db, nil
	}

	db, err := r.openFunc(dsn)
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(r.poolConfig.MaxOpenConns)
	db.SetMaxIdleConns(r.poolConfig.MaxIdleConns)
	db.SetConnMaxLifetime(r.poolConfig.ConnMaxLifetime)
	db.SetConnMaxIdleTime(r.poolConfig.ConnMaxIdleTime)

	r.pools[tenantCode] = &poolEntry{db: db, lastUsedAt: time.Now()}
	return db, nil
}

// evict 关闭并移除 tenantCode 对应的连接池（如果存在），供密码轮换后重建、或空闲回收使用。
func (r *poolRegistry) evict(tenantCode string) error {
	r.mu.Lock()
	entry, ok := r.pools[tenantCode]
	if ok {
		delete(r.pools, tenantCode)
	}
	r.mu.Unlock()

	if !ok {
		return nil
	}
	return entry.db.Close()
}

func (r *poolRegistry) sweepLoop(interval time.Duration) {
	defer close(r.sweepDone)

	ticker := time.NewTicker(interval)
	defer ticker.Stop()

	for {
		select {
		case <-r.stopSweep:
			return
		case <-ticker.C:
			r.sweepIdle()
		}
	}
}

func (r *poolRegistry) sweepIdle() {
	cutoff := time.Now().Add(-r.idleTTL)

	r.mu.Lock()
	var toClose []*sql.DB
	for tenantCode, entry := range r.pools {
		if entry.lastUsedAt.Before(cutoff) {
			toClose = append(toClose, entry.db)
			delete(r.pools, tenantCode)
		}
	}
	r.mu.Unlock()

	for _, db := range toClose {
		_ = db.Close()
	}
}

// closeAll 停止空闲回收巡检并关闭所有连接池，供 Connector.Close 使用。
func (r *poolRegistry) closeAll() error {
	close(r.stopSweep)
	<-r.sweepDone

	r.mu.Lock()
	defer r.mu.Unlock()

	var firstErr error
	for tenantCode, entry := range r.pools {
		if err := entry.db.Close(); err != nil && firstErr == nil {
			firstErr = err
		}
		delete(r.pools, tenantCode)
	}
	return firstErr
}
