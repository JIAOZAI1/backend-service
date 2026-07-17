# tenant-db-connector

供需要直连租户数据库的服务（如未来的租户服务）统一使用的 SDK：按 `tenantCode` 查询数据库连接信息并维护常驻连接池，不必自己实现缓存 + 连接池管理。按仓库规范第 10 章，公共代码统一放在 `packages/`，服务间不允许直接互相引用内部代码，只能通过这里的 SDK。

## 目录结构

```bash
tenant-db-connector/
├── go/       # Go 实现（module: github.com/company/tenant-db-connector）
├── dotnet/   # .NET 实现（TenantDbConnector 类库 + 单元测试）
└── README.md
```

按语言分实现而非单一语言，命名仍以能力命名（`tenant-db-connector`），符合第 10.1 节"包名表达能力而非语言实现"的要求。

## 工作原理

```
调用方服务
   │  GetTenantDbInfo(tenantCode) / GetDB(tenantCode)
   ▼
tenant-db-connector SDK
   │ 1. 本地进程内缓存命中？→ 直接返回（TTL 默认 5s，吸收突发流量）
   │ 2. Redis 缓存命中？→ 返回并回填本地缓存（TTL 默认 60s）
   │ 3. 都未命中 → 调用方注入的 SsoGetter 回源查询 sso-service，
   │    结果写回本地 + Redis 缓存
   ▼
返回连接信息 / 常驻连接池句柄（Go: *sql.DB，.NET: MySqlDataSource）
```

- SDK **不内置** sso-service 的 HTTP 调用细节（host、`X-Internal-Token` 等环境相关配置由调用方掌握），只约定 `SsoGetter` 的函数签名，保持与具体网络实现解耦、便于测试替身。调用方需要自己实现一个 `SsoGetter`，调用 sso-service 的 `GET /internal/tenants/{tenantCode}/db-info`（集群内 Service DNS 直连，见规范第 16.5.5 章）。
- 同一 `tenantCode` 并发首次访问时，通过 singleflight（Go）/ 按 key 的信号量（.NET）合并为一次回源，避免缓存击穿打爆 sso-service。
- 同一 `tenantCode` 的连接池在进程生命周期内只建一次、常驻复用，长期空闲（默认 30 分钟无访问）的连接池会被后台巡检自动关闭回收，避免租户数量增长后占满下游 MySQL 的 `max_connections`。
- 密码轮换等场景需要主动调用 `InvalidateTenantDbInfo`（Go）/ `InvalidateTenantDbInfoAsync`（.NET）清缓存 + 重建连接池，不必等待缓存 TTL 自然过期。

## 用法

### Go

```go
import (
    "context"
    "net/http"

    "github.com/redis/go-redis/v9"
    tenantdbconnector "github.com/company/tenant-db-connector"
)

ssoGetter := func(ctx context.Context, tenantCode string) (tenantdbconnector.TenantDbInfo, error) {
    // 调用方自行实现：拼 sso-service 的 Service DNS 地址、带上 X-Internal-Token，
    // 404 时返回 tenantdbconnector.ErrTenantNotFound。
    return callSsoService(ctx, tenantCode)
}

connector, err := tenantdbconnector.New(redisClient, ssoGetter)
defer connector.Close()

db, err := connector.GetDB(ctx, "acme")
row := db.QueryRowContext(ctx, "SELECT 1")

// 密码轮换后：
connector.InvalidateTenantDbInfo(ctx, "acme")
```

### .NET

```csharp
using StackExchange.Redis;
using TenantDbConnector;

services.AddSingleton<ITenantDbConnector>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    SsoGetter ssoGetter = async (tenantCode, ct) =>
        await CallSsoServiceAsync(tenantCode, ct); // 404 时抛 TenantNotFoundException

    return new TenantDbConnectorImpl(redis, ssoGetter);
});

// 使用方
var dataSource = await connector.GetDataSourceAsync("acme");
await using var conn = await dataSource.OpenConnectionAsync(ct);

// 密码轮换后：
await connector.InvalidateTenantDbInfoAsync("acme");
```

## 配置项

| 项 | Go | .NET | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| 本地缓存 TTL | `WithLocalCacheTTL` | `LocalCacheTtl` | 5s | |
| Redis 缓存 TTL | `WithRedisCacheTTL` | `RedisCacheTtl` | 60s | 与 sso-service 侧缓存 TTL 对齐 |
| 连接池空闲回收阈值 | `WithIdlePoolTTL` | `IdlePoolTtl` | 30min | |
| 空闲巡检周期 | `WithIdleSweepInterval` | `IdleSweepInterval` | 5min | |
| 连接池参数 | `WithPoolConfig` | `MaxPoolSize`/`MinPoolSize` | MaxOpen 10 / MaxIdle 5 | |
| DSN/连接串拼接方式 | `WithDSNBuilder` | `ConnectionStringBuilder` | MySQL | 预留给未来扩展其他驱动 |

Redis 客户端参数为 `nil`/`null` 时跳过 Redis 层，只用本地缓存 + `SsoGetter` 回源，适合本地开发/测试场景。

## 注意事项

- `TenantDbInfo.DbPassword` 为明文（与 sso-service 侧透出格式一致），调用方与本 SDK 都不应将其写入日志。
- Redis 缓存 key 使用独立前缀 `tenant-db-connector:info:`，与 sso-service 自身的 `sso:tenant-db-info:` 前缀区分开，即使共用同一个 Redis 实例也不会冲突。
- 一个进程内应只构造一个 Connector 并复用（DI 单例 / 包级单例），不要每次请求都 `New`，否则缓存和连接池都失去意义。
- 目前只支持 MySQL；预留了 DSN/连接串构造器的替换点，未来扩展其他数据库时不需要改核心缓存/连接池逻辑。

## 测试

```bash
# Go
cd go && go test ./... -race -cover

# .NET
cd dotnet && dotnet test
```
