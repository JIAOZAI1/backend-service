package handler

import (
	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/middleware"
	"github.com/company/sso-service/pkg/jwtutil"
)

// RoutePrefix 是本服务在 K8s 网关上暴露的路由前缀，网关按 host + 前缀转发且不 strip，
// 因此服务内部路由必须以此前缀注册，才能与网关转发路径一致。
const RoutePrefix = "/sso-service"

func NewRouter(authHandler *AuthHandler, issuer *jwtutil.Issuer, blacklist middleware.BlacklistChecker) *gin.Engine {
	r := gin.New()
	r.Use(gin.Recovery())

	// 健康检查不带前缀：K8s 探针直接访问 Pod，不经过网关
	r.GET("/health", Health)

	base := r.Group(RoutePrefix)
	{
		v1 := base.Group("/api/v1")
		{
			auth := v1.Group("/auth")
			auth.POST("/register", authHandler.Register)
			auth.POST("/login", authHandler.Login)
			auth.POST("/refresh", authHandler.Refresh)
			auth.POST("/logout", authHandler.Logout)

			auth.GET("/me", middleware.RequireAuth(issuer, blacklist), authHandler.Me)
		}
	}

	return r
}
