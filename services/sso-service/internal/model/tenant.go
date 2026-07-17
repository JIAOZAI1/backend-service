package model

// Tenant、UserTenant 是 admin-service 拥有的表（tenants/user_tenants，字段定义以
// admin-service 的迁移脚本为准），sso-service 只读查询，供 /internal/auth/verify 解析
// 当前用户所属租户的 tenant_code，以及 /internal/tenants/:tenantCode/db-info 查询租户
// 数据库连接信息。两个服务共用同一个 MySQL 实例/数据库（sys_db），因此这里直接建表映射
// 做查询，不通过 HTTP 调用 admin-service——与 roles 现有的"每次请求实时查库、不放进 JWT"
// 设计保持一致，租户变更（如开户/取消/过期）对下一次请求立即生效。字段只声明本服务查询
// 用得到的列，不是 admin-service Tenant 实体的完整镜像。
type Tenant struct {
	ID         uint64 `gorm:"column:id;primaryKey"`
	TenantCode string `gorm:"column:tenant_code"`
	Status     string `gorm:"column:status"`
	DbHost     string `gorm:"column:db_host"`
	DbPort     int    `gorm:"column:db_port"`
	DbName     string `gorm:"column:db_name"`
	DbUsername string `gorm:"column:db_username"`
	// DbPassword 是 admin-service 明文写入的数据库密码（见 ReviewService.CreateTenantAsync），
	// 未经 packages/db-credential-crypto 加密。本接口原样透出，调用方需自行保证传输/存储安全。
	DbPassword string `gorm:"column:db_password"`
}

func (Tenant) TableName() string {
	return "tenants"
}

type UserTenant struct {
	ID       uint64 `gorm:"column:id;primaryKey"`
	UserID   uint64 `gorm:"column:user_id"`
	TenantID uint64 `gorm:"column:tenant_id"`
}

func (UserTenant) TableName() string {
	return "user_tenants"
}
