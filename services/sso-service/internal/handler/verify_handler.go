package handler

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"
)

// VerifyAuth 供网关 ForwardAuth 调用：前置的 RequireAuth 中间件校验通过后，
// 在响应头返回用户身份，网关用这两个头覆盖原请求同名头后再转发给后端服务。
// 该路由与 /health 一样不带网关前缀，仅集群内直连本服务 Service 可达，不经网关暴露。
func VerifyAuth(c *gin.Context) {
	c.Header("X-User-Id", strconv.FormatUint(c.GetUint64("userID"), 10))
	c.Header("X-Username", c.GetString("username"))
	c.Status(http.StatusNoContent)
}
