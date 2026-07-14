package handler

import (
	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/middleware"
	"github.com/company/sso-service/pkg/jwtutil"
)

// RoutePrefix 是本服务在 K8s 网关上暴露的路由前缀，网关按 host + 前缀转发且不 strip，
// 因此服务内部路由必须以此前缀注册，才能与网关转发路径一致。
const RoutePrefix = "/sso-service"

// AdminRole 是角色管理接口要求的角色名，拥有该角色的用户才能调用 /roles、/users/:userID/roles 相关接口。
const AdminRole = "admin"

// CORS 统一在网关层（Traefik Middleware）配置，服务自身不重复处理，
// 避免网关与服务同时添加 CORS 响应头导致浏览器拒绝（重复 header）。
func NewRouter(
	authHandler *AuthHandler,
	roleHandler *RoleHandler,
	internalUserHandler *InternalUserHandler,
	issuer *jwtutil.Issuer,
	blacklist middleware.BlacklistChecker,
	roleLister middleware.UserRoleLister,
	internalToken string,
) *gin.Engine {
	r := gin.New()
	r.Use(gin.Recovery())

	// 健康检查不带前缀：K8s 探针直接访问 Pod，不经过网关
	r.GET("/health", Health)

	requireAuth := middleware.RequireAuth(issuer, blacklist)
	requireAdmin := middleware.RequireRole(roleLister, AdminRole)

	// 网关 ForwardAuth 校验端点（见 deploy/k8s/gateway/auth-middleware.yaml）：
	// 同 /health 一样不带前缀，网关 Middleware 直连本服务 Service 访问，不经网关暴露
	r.GET("/internal/auth/verify", requireAuth, VerifyAuth(roleLister))

	// 供集群内其他服务直连调用（如 admin-service 审核开户流程），不经网关暴露。
	// 不做用户角色校验，但要求调用方携带集群内共享密钥（RequireInternalToken），
	// 弥补"仅靠网络可达性作为信任边界"的不足：详见 middleware.RequireInternalToken。
	requireInternalToken := middleware.RequireInternalToken(internalToken)
	internalUsers := r.Group("/internal/users", requireInternalToken)
	internalUsers.GET("/:userID", internalUserHandler.GetUser)
	internalUsers.PUT("/:userID/review", internalUserHandler.ApproveReview)

	base := r.Group(RoutePrefix)
	{
		v1 := base.Group("/api/v1")
		{
			auth := v1.Group("/auth")
			auth.POST("/register", authHandler.Register)
			auth.POST("/login", authHandler.Login)
			auth.POST("/refresh", authHandler.Refresh)
			auth.POST("/logout", authHandler.Logout)

			auth.GET("/me", requireAuth, authHandler.Me)

			// 角色管理：需登录 + admin 角色
			roles := v1.Group("/roles", requireAuth, requireAdmin)
			roles.GET("", roleHandler.ListRoles)
			roles.POST("", roleHandler.CreateRole)

			users := v1.Group("/users", requireAuth, requireAdmin)
			users.GET("/:userID/roles", roleHandler.ListUserRoles)
			users.POST("/:userID/roles", roleHandler.AssignRoleToUser)
			users.DELETE("/:userID/roles/:roleName", roleHandler.RemoveRoleFromUser)
		}
	}

	return r
}
