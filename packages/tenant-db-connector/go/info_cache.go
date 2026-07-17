package tenantdbconnector

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"sync"
	"time"

	"github.com/redis/go-redis/v9"
	"golang.org/x/sync/singleflight"
)

type localCacheEntry struct {
	info      TenantDbInfo
	expiresAt time.Time
}

// infoCache 实现 TenantDbInfo 的三级 cache-aside 查询：本地内存 → Redis → ssoGetter 回源。
// 同一 tenantCode 的并发回源请求通过 singleflight 合并为一次，避免缓存击穿时打爆
// sso-service。
type infoCache struct {
	rdb        *redis.Client
	ssoGetter  SsoGetter
	localTTL   time.Duration
	redisTTL   time.Duration
	sf         singleflight.Group
	localMu    sync.RWMutex
	localCache map[string]localCacheEntry
}

func newInfoCache(rdb *redis.Client, ssoGetter SsoGetter, localTTL, redisTTL time.Duration) *infoCache {
	return &infoCache{
		rdb:        rdb,
		ssoGetter:  ssoGetter,
		localTTL:   localTTL,
		redisTTL:   redisTTL,
		localCache: make(map[string]localCacheEntry),
	}
}

func (c *infoCache) get(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
	if info, ok := c.getLocal(tenantCode); ok {
		return info, nil
	}

	// singleflight 保证同一 tenantCode 并发 miss 时只有一个 goroutine 真正查 Redis/回源，
	// 其余等待并复用结果。
	v, err, _ := c.sf.Do(tenantCode, func() (any, error) {
		return c.load(ctx, tenantCode)
	})
	if err != nil {
		return TenantDbInfo{}, err
	}
	return v.(TenantDbInfo), nil
}

func (c *infoCache) load(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
	if info, err := c.getRedis(ctx, tenantCode); err == nil {
		c.setLocal(tenantCode, info)
		return info, nil
	} else if !errors.Is(err, redis.Nil) {
		// Redis 故障不阻断主流程，直接回源 ssoGetter，保证可用性优先于缓存命中率
		// （与 sso-service 侧 TenantDbInfoService 的降级策略一致）。
	}

	info, err := c.ssoGetter(ctx, tenantCode)
	if err != nil {
		return TenantDbInfo{}, fmt.Errorf("tenantdbconnector: sso getter failed for tenant %q: %w", tenantCode, err)
	}

	c.setLocal(tenantCode, info)
	_ = c.setRedis(ctx, tenantCode, info) // Redis 写入失败不影响本次返回，下次请求会再次回源

	return info, nil
}

func (c *infoCache) getLocal(tenantCode string) (TenantDbInfo, bool) {
	c.localMu.RLock()
	defer c.localMu.RUnlock()

	entry, ok := c.localCache[tenantCode]
	if !ok || time.Now().After(entry.expiresAt) {
		return TenantDbInfo{}, false
	}
	return entry.info, true
}

func (c *infoCache) setLocal(tenantCode string, info TenantDbInfo) {
	c.localMu.Lock()
	defer c.localMu.Unlock()

	c.localCache[tenantCode] = localCacheEntry{info: info, expiresAt: time.Now().Add(c.localTTL)}
}

func (c *infoCache) invalidateLocal(tenantCode string) {
	c.localMu.Lock()
	defer c.localMu.Unlock()

	delete(c.localCache, tenantCode)
}

func (c *infoCache) getRedis(ctx context.Context, tenantCode string) (TenantDbInfo, error) {
	if c.rdb == nil {
		return TenantDbInfo{}, redis.Nil
	}

	val, err := c.rdb.Get(ctx, redisKeyPrefix+tenantCode).Bytes()
	if err != nil {
		return TenantDbInfo{}, err
	}

	var info TenantDbInfo
	if err := json.Unmarshal(val, &info); err != nil {
		return TenantDbInfo{}, fmt.Errorf("tenantdbconnector: unmarshal cached tenant db info: %w", err)
	}
	return info, nil
}

func (c *infoCache) setRedis(ctx context.Context, tenantCode string, info TenantDbInfo) error {
	if c.rdb == nil {
		return nil
	}

	val, err := json.Marshal(info)
	if err != nil {
		return fmt.Errorf("tenantdbconnector: marshal tenant db info: %w", err)
	}
	return c.rdb.Set(ctx, redisKeyPrefix+tenantCode, val, c.redisTTL).Err()
}

// invalidate 清除本地 + Redis 中该租户的缓存项，供密码轮换等场景主动失效使用。
func (c *infoCache) invalidate(ctx context.Context, tenantCode string) error {
	c.invalidateLocal(tenantCode)

	if c.rdb == nil {
		return nil
	}
	return c.rdb.Del(ctx, redisKeyPrefix+tenantCode).Err()
}
