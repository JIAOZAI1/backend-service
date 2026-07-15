package handler

import (
	"errors"
	"net/http"
	"strconv"
	"strings"

	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/middleware"
	"github.com/company/sso-service/internal/repository"
)

// VerifyAuthInternal 供网关 ForwardAuth 调用：前置的 RequireAuth 中间件校验通过后，
// 在响应头返回用户身份、角色列表、当前 active 租户的 tenant_code，网关用这些头覆盖
// 原请求同名头后再转发给后端服务。角色/租户每次请求都从数据库实时查询（同 RequireRole），
// 不依赖 JWT 快照，保证权限变更、开户/续期/取消租户对下一次请求立即生效。
// 该路由与 /health 一样不带网关前缀，仅集群内直连本服务 Service 可达，不经网关暴露。
// 方法名带 Internal 后缀，与对外业务接口的方法命名区分（见规范第 16.5 章）。
func VerifyAuthInternal(roleLister middleware.UserRoleLister, tenantLister middleware.TenantLister) gin.HandlerFunc {
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

		// 找不到 active 租户（未开户/审核中/已过期/已取消）是正常情况，不设置该头，
		// 不影响登录本身——很多接口（如管理员账号）本就不属于任何租户。
		tenantCode, err := tenantLister.GetActiveTenantCodeByUserID(c.Request.Context(), userID)
		if err != nil && !errors.Is(err, repository.ErrTenantNotFound) {
			c.AbortWithStatusJSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
			return
		}
		if err == nil {
			c.Header("X-Tenant-Code", tenantCode)
		}

		c.Status(http.StatusNoContent)
	}
}
