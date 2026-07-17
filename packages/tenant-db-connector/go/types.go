// Package tenantdbconnector 为需要直连租户数据库的服务（如未来的租户服务）提供统一的
// "按 tenant_code 拿数据库连接池"能力：本地缓存 → Redis 缓存 → 调用方提供的 SsoGetter
// 回源查询 sso-service，命中/回源后的连接信息按 tenant_code 维护成常驻连接池，避免每次
// 请求都重新握手建连。
//
// SDK 不内置 sso-service 的 HTTP 调用细节（host、X-Internal-Token 等环境相关配置由
// 调用方掌握），只约定 SsoGetter 的函数签名，保持与具体网络实现解耦、便于测试替身。
package tenantdbconnector

import "context"

// TenantDbInfo 是某个租户的数据库连接信息，字段对应 sso-service
// GET /internal/tenants/{tenantCode}/db-info 的响应体。DbPassword 为明文，
// 调用方与本 SDK 都不应将其写入日志。
type TenantDbInfo struct {
	TenantCode string `json:"tenantCode"`
	DbHost     string `json:"dbHost"`
	DbPort     int    `json:"dbPort"`
	DbName     string `json:"dbName"`
	DbUsername string `json:"dbUsername"`
	DbPassword string `json:"dbPassword"`
}

// SsoGetter 由调用方实现：如何从 sso-service 拉取指定 tenantCode 的数据库连接信息
// （host、X-Internal-Token 等由调用方从自己的配置注入，SDK 不关心网络细节）。
// tenantCode 不存在时应返回 ErrTenantNotFound，便于上层区分"查无此租户"与其他故障。
type SsoGetter func(ctx context.Context, tenantCode string) (TenantDbInfo, error)
