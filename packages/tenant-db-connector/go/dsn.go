package tenantdbconnector

import "fmt"

// DSNBuilder 把 TenantDbInfo 拼接成 database/sql 驱动可用的 DSN。可插拔是为了不把
// 具体数据库驱动的连接串格式写死在核心缓存/连接池逻辑里，方便未来扩展其他驱动。
type DSNBuilder func(info TenantDbInfo) string

// MySQLDSN 是默认的 DSNBuilder，产出 go-sql-driver/mysql 可用的 DSN。
// parseTime=true 让 DATETIME/TIMESTAMP 列直接扫描为 time.Time。
func MySQLDSN(info TenantDbInfo) string {
	return fmt.Sprintf(
		"%s:%s@tcp(%s:%d)/%s?parseTime=true&charset=utf8mb4",
		info.DbUsername, info.DbPassword, info.DbHost, info.DbPort, info.DbName,
	)
}
