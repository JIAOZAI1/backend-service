package middleware

import (
	"crypto/subtle"
	"net/http"

	"github.com/gin-gonic/gin"
)

// InternalTokenHeader 是集群内服务间调用共享密钥的请求头名。
const InternalTokenHeader = "X-Internal-Token"

// RequireInternalToken 校验集群内直连调用（如 admin-service 审核开户流程）携带的共享密钥，
// 用于弥补"不经网关暴露"仅靠网络可达性作为信任边界的不足：即便调用方能连到本服务的
// ClusterIP，没有该密钥也无法调用。密钥通过 K8s Secret 下发（见 deploy/k8s/base/secret-dev.yaml
// 的 internal-api-token），三个服务共享同一份，因此用固定时间比较防止时序侧信道泄露密钥。
func RequireInternalToken(expectedToken string) gin.HandlerFunc {
	return func(c *gin.Context) {
		provided := c.GetHeader(InternalTokenHeader)
		if provided == "" || subtle.ConstantTimeCompare([]byte(provided), []byte(expectedToken)) != 1 {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{"error": "invalid or missing internal token"})
			return
		}
		c.Next()
	}
}
