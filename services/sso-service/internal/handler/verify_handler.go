package handler

import (
	"net/http"
	"strconv"
	"strings"

	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/middleware"
)

// VerifyAuthInternal 供网关 ForwardAuth 调用：前置的 RequireAuth 中间件校验通过后，
// 在响应头返回用户身份与角色列表，网关用这些头覆盖原请求同名头后再转发给后端服务。
// 角色每次请求都从数据库实时查询（同 RequireRole），不依赖 JWT 快照，保证权限变更立即生效。
// 该路由与 /health 一样不带网关前缀，仅集群内直连本服务 Service 可达，不经网关暴露。
// 方法名带 Internal 后缀，与对外业务接口的方法命名区分（见规范第 16.5 章）。
func VerifyAuthInternal(roleLister middleware.UserRoleLister) gin.HandlerFunc {
	return func(c *gin.Context) {
		userID := c.GetUint64("userID")

		roles, err := roleLister.ListByUserID(c.Request.Context(), userID)
		if err != nil {
			c.AbortWithStatusJSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
			return
		}

		names := make([]string, len(roles))
		for i, r := range roles {
			names[i] = r.Name
		}

		c.Header("X-User-Id", strconv.FormatUint(userID, 10))
		c.Header("X-Username", c.GetString("username"))
		c.Header("X-User-Roles", strings.Join(names, ","))
		c.Status(http.StatusNoContent)
	}
}
