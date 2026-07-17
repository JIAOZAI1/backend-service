package tenantdbconnector

import "time"

// redisKeyPrefix 与 sso-service 自身的 Redis 缓存前缀（sso:tenant-db-info:）区分开，
// 即使两边共用同一个 Redis 实例也不会 key 冲突或语义混淆。
const redisKeyPrefix = "tenant-db-connector:info:"

const (
	// defaultLocalCacheTTL 是本地进程内缓存的默认 TTL：远短于 Redis TTL，只用来吸收
	// 突发流量（同一 tenantCode 短时间内大量请求）对 Redis 的压力，不作为主缓存层。
	defaultLocalCacheTTL = 5 * time.Second

	// defaultRedisCacheTTL 与 sso-service 侧的 tenantDbInfoCacheTTL 对齐：租户数据库
	// 信息极少变更，但密码轮换等运维操作生效延迟需要有上限。
	defaultRedisCacheTTL = 60 * time.Second

	// defaultIdlePoolTTL 是租户连接池的空闲回收阈值：超过这个时长没有被访问过的连接池
	// 会被自动关闭并从注册表移除，避免长期不活跃的租户占用下游 MySQL 的连接数配额。
	defaultIdlePoolTTL = 30 * time.Minute

	// defaultIdleSweepInterval 是空闲连接池清理的巡检周期。
	defaultIdleSweepInterval = 5 * time.Minute

	defaultMaxOpenConns    = 10
	defaultMaxIdleConns    = 5
	defaultConnMaxLifetime = 30 * time.Minute
	defaultConnMaxIdleTime = 10 * time.Minute
)

// PoolConfig 是每个租户连接池的 *sql.DB 参数，语义与标准库 database/sql 一致。
type PoolConfig struct {
	MaxOpenConns    int
	MaxIdleConns    int
	ConnMaxLifetime time.Duration
	ConnMaxIdleTime time.Duration
}

func defaultPoolConfig() PoolConfig {
	return PoolConfig{
		MaxOpenConns:    defaultMaxOpenConns,
		MaxIdleConns:    defaultMaxIdleConns,
		ConnMaxLifetime: defaultConnMaxLifetime,
		ConnMaxIdleTime: defaultConnMaxIdleTime,
	}
}

type config struct {
	localCacheTTL     time.Duration
	redisCacheTTL     time.Duration
	idlePoolTTL       time.Duration
	idleSweepInterval time.Duration
	poolConfig        PoolConfig
	dsnBuilder        DSNBuilder
}

func defaultConfig() config {
	return config{
		localCacheTTL:     defaultLocalCacheTTL,
		redisCacheTTL:     defaultRedisCacheTTL,
		idlePoolTTL:       defaultIdlePoolTTL,
		idleSweepInterval: defaultIdleSweepInterval,
		poolConfig:        defaultPoolConfig(),
		dsnBuilder:        MySQLDSN,
	}
}

// Option 用于自定义 New 构造出的 Connector 行为。
type Option func(*config)

// WithLocalCacheTTL 覆盖本地进程内缓存 TTL。
func WithLocalCacheTTL(ttl time.Duration) Option {
	return func(c *config) { c.localCacheTTL = ttl }
}

// WithRedisCacheTTL 覆盖 Redis 缓存 TTL。
func WithRedisCacheTTL(ttl time.Duration) Option {
	return func(c *config) { c.redisCacheTTL = ttl }
}

// WithIdlePoolTTL 覆盖租户连接池的空闲回收阈值。
func WithIdlePoolTTL(ttl time.Duration) Option {
	return func(c *config) { c.idlePoolTTL = ttl }
}

// WithIdleSweepInterval 覆盖空闲连接池清理的巡检周期。
func WithIdleSweepInterval(d time.Duration) Option {
	return func(c *config) { c.idleSweepInterval = d }
}

// WithPoolConfig 覆盖每个租户连接池的 *sql.DB 参数。
func WithPoolConfig(pc PoolConfig) Option {
	return func(c *config) { c.poolConfig = pc }
}

// WithDSNBuilder 覆盖 TenantDbInfo → DSN 的拼接方式，默认 MySQLDSN。
// 预留给未来支持其他数据库驱动（如 PostgreSQL）时替换。
func WithDSNBuilder(b DSNBuilder) Option {
	return func(c *config) { c.dsnBuilder = b }
}
