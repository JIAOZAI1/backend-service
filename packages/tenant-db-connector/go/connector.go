package tenantdbconnector

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"sync"

	_ "github.com/go-sql-driver/mysql" // 注册 "mysql" driver，供 sql.Open 使用
	"github.com/redis/go-redis/v9"
)

// Connector 是本 SDK 的入口：按 tenantCode 查询数据库连接信息（cache-aside）并维护
// 常驻的 *sql.DB 连接池。一个进程内应只构造一个 Connector 并复用，不要每次请求都 New。
type Connector struct {
	info       *infoCache
	pools      *poolRegistry
	dsnBuilder DSNBuilder

	closeMu sync.Mutex
	closed  bool
}

// New 构造 Connector。rdb 可以为 nil（此时跳过 Redis 层，只用本地缓存 + ssoGetter 回源，
// 适合本地开发/测试场景）；ssoGetter 必填。
func New(rdb *redis.Client, ssoGetter SsoGetter, opts ...Option) (*Connector, error) {
	if ssoGetter == nil {
		return nil, errors.New("tenantdbconnector: ssoGetter must not be nil")
	}

	cfg := defaultConfig()
	for _, opt := range opts {
		opt(&cfg)
	}

	c := &Connector{
		info:       newInfoCache(rdb, ssoGetter, cfg.localCacheTTL, cfg.redisCacheTTL),
		dsnBuilder: cfg.dsnBuilder,
	}
	c.pools = newPoolRegistry(func(dsn string) (*sql.DB, error) {
		return sql.Open("mysql", dsn)
	}, cfg.poolConfig, cfg.idlePoolTTL, cfg.idleSweepInterval)

	return c, nil
}

// GetTenantDbInfo 返回租户数据库连接信息：本地缓存 → Redis 缓存 → ssoGetter 回源
// （命中/回源结果按各自 TTL 写回两级缓存）。
func (c *Connector) GetTenantDbInfo(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
	if c.isClosed() {
		return TenantDbInfo{}, ErrClosed
	}
	return c.info.get(ctx, tenantCode)
}

// GetDB 返回该租户的 *sql.DB 连接池句柄。同一 tenantCode 在进程生命周期内复用同一个
// *sql.DB，不会每次调用都新建连接池。首次访问会触发 GetTenantDbInfo 回源（如未命中缓存）
// 并建池；若连接池已存在但底层连接因密码轮换等原因失效，调用方应捕获 ping/query 错误后
// 调用 InvalidateTenantDbInfo 强制重建。
func (c *Connector) GetDB(ctx context.Context, tenantCode string) (*sql.DB, error) {
	if c.isClosed() {
		return nil, ErrClosed
	}

	info, err := c.info.get(ctx, tenantCode)
	if err != nil {
		return nil, err
	}

	db, err := c.pools.get(tenantCode, c.dsnBuilder(info))
	if err != nil {
		return nil, fmt.Errorf("tenantdbconnector: open pool for tenant %q: %w", tenantCode, err)
	}
	return db, nil
}

// InvalidateTenantDbInfo 清除该租户的缓存信息并关闭其现有连接池（如果存在）。
// 用于密码轮换等运维操作后强制下一次 GetDB/GetTenantDbInfo 回源重建，不必等待缓存 TTL
// 自然过期。
func (c *Connector) InvalidateTenantDbInfo(ctx context.Context, tenantCode string) error {
	if c.isClosed() {
		return ErrClosed
	}

	if err := c.info.invalidate(ctx, tenantCode); err != nil {
		return fmt.Errorf("tenantdbconnector: invalidate cache for tenant %q: %w", tenantCode, err)
	}
	if err := c.pools.evict(tenantCode); err != nil {
		return fmt.Errorf("tenantdbconnector: close pool for tenant %q: %w", tenantCode, err)
	}
	return nil
}

// Close 停止后台空闲回收巡检并关闭所有已建立的租户连接池。进程退出前应调用一次。
func (c *Connector) Close() error {
	c.closeMu.Lock()
	defer c.closeMu.Unlock()

	if c.closed {
		return nil
	}
	c.closed = true
	return c.pools.closeAll()
}

func (c *Connector) isClosed() bool {
	c.closeMu.Lock()
	defer c.closeMu.Unlock()
	return c.closed
}
