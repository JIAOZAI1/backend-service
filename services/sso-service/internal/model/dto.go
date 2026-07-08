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
	ID       uint64   `json:"id"`
	Username string   `json:"username"`
	Email    string   `json:"email"`
	Roles    []string `json:"roles"`
}

func NewUserResponse(u *User, roles []string) UserResponse {
	return UserResponse{ID: u.ID, Username: u.Username, Email: u.Email, Roles: roles}
}

type RoleResponse struct {
	ID          uint64 `json:"id"`
	Name        string `json:"name"`
	Description string `json:"description"`
}

func NewRoleResponse(r *Role) RoleResponse {
	return RoleResponse{ID: r.ID, Name: r.Name, Description: r.Description}
}

type CreateRoleRequest struct {
	Name        string `json:"name" binding:"required,min=2,max=64"`
	Description string `json:"description" binding:"max=255"`
}

type AssignRoleRequest struct {
	RoleName string `json:"roleName" binding:"required"`
}
