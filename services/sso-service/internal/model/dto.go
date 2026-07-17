package model

type RegisterRequest struct {
	Username string `json:"username" binding:"required,min=3,max=64"`
	Email    string `json:"email" binding:"required,email"`
	Password string `json:"password" binding:"required,min=8,max=128"`
}

type LoginRequest struct {
	Username string `json:"username" binding:"required"`
	Password string `json:"password" binding:"required"`
}

type RefreshRequest struct {
	RefreshToken string `json:"refreshToken" binding:"required"`
}

type LogoutRequest struct {
	RefreshToken string `json:"refreshToken" binding:"required"`
}

type TokenPair struct {
	AccessToken  string   `json:"accessToken"`
	RefreshToken string   `json:"refreshToken"`
	TokenType    string   `json:"tokenType"`
	ExpiresIn    int64    `json:"expiresIn"`
	Roles        []string `json:"roles"`
}

type UserResponse struct {
	ID           uint64   `json:"id"`
	Username     string   `json:"username"`
	Email        string   `json:"email"`
	Status       int8     `json:"status"`
	ReviewStatus string   `json:"reviewStatus"`
	Roles        []string `json:"roles"`
}

func NewUserResponse(u *User, roles []string) UserResponse {
	return UserResponse{
		ID:           u.ID,
		Username:     u.Username,
		Email:        u.Email,
		Status:       u.Status,
		ReviewStatus: u.ReviewStatus,
		Roles:        roles,
	}
}

// PagedUserResponse 是分页列表接口的响应体，字段名固定为 items/page/pageSize/total（规范第 16.4.3 章）。
type PagedUserResponse struct {
	Items    []UserResponse `json:"items"`
	Page     int            `json:"page"`
	PageSize int            `json:"pageSize"`
	Total    int64          `json:"total"`
}

type RoleResponse struct {
	ID          uint64 `json:"id"`
	Name        string `json:"name"`
	Description string `json:"description"`
}

func NewRoleResponse(r *Role) RoleResponse {
	return RoleResponse{ID: r.ID, Name: r.Name, Description: r.Description}
}

// TenantDbInfoResponse 是租户数据库连接信息，供集群内服务（如未来的租户服务）按
// tenant_code 查询后直连租户库使用。DbPassword 为明文（见 model.Tenant 字段说明）。
type TenantDbInfoResponse struct {
	TenantCode string `json:"tenantCode"`
	DbHost     string `json:"dbHost"`
	DbPort     int    `json:"dbPort"`
	DbName     string `json:"dbName"`
	DbUsername string `json:"dbUsername"`
	DbPassword string `json:"dbPassword"`
}

func NewTenantDbInfoResponse(t *Tenant) TenantDbInfoResponse {
	return TenantDbInfoResponse{
		TenantCode: t.TenantCode,
		DbHost:     t.DbHost,
		DbPort:     t.DbPort,
		DbName:     t.DbName,
		DbUsername: t.DbUsername,
		DbPassword: t.DbPassword,
	}
}

type CreateRoleRequest struct {
	Name        string `json:"name" binding:"required,min=2,max=64"`
	Description string `json:"description" binding:"max=255"`
}

type AssignRoleRequest struct {
	RoleName string `json:"roleName" binding:"required"`
}
