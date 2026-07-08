package handler

import (
	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/middleware"
	"github.com/company/sso-service/pkg/jwtutil"
)

func NewRouter(authHandler *AuthHandler, issuer *jwtutil.Issuer, blacklist middleware.BlacklistChecker) *gin.Engine {
	r := gin.New()
	r.Use(gin.Recovery())

	r.GET("/health", Health)

	v1 := r.Group("/api/v1")
	{
		auth := v1.Group("/auth")
		auth.POST("/register", authHandler.Register)
		auth.POST("/login", authHandler.Login)
		auth.POST("/refresh", authHandler.Refresh)
		auth.POST("/logout", authHandler.Logout)

		auth.GET("/me", middleware.RequireAuth(issuer, blacklist), authHandler.Me)
	}

	return r
}
