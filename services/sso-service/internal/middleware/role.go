package middleware

import (
	"context"
	"net/http"

	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/model"
)

type UserRoleLister interface {
	ListByUserID(ctx context.Context, userID uint64) ([]model.Role, error)
}

// RequireRole 要求当前用户拥有指定角色之一，必须放在 RequireAuth 之后使用。
// 角色数据每次请求都从数据库实时查询，不依赖 JWT 中的快照，保证权限变更立即生效。
func RequireRole(roleRepo UserRoleLister, allowed ...string) gin.HandlerFunc {
	allowedSet := make(map[string]struct{}, len(allowed))
	for _, r := range allowed {
		allowedSet[r] = struct{}{}
	}

	return func(c *gin.Context) {
		userID, ok := c.Get("userID")
		if !ok {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{"error": "missing bearer token"})
			return
		}

		roles, err := roleRepo.ListByUserID(c.Request.Context(), userID.(uint64))
		if err != nil {
			c.AbortWithStatusJSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
			return
		}

		for _, r := range roles {
			if _, ok := allowedSet[r.Name]; ok {
				c.Next()
				return
			}
		}

		c.AbortWithStatusJSON(http.StatusForbidden, gin.H{"error": "insufficient role"})
	}
}
