package handler

import (
	"errors"
	"net/http"

	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/service"
)

// TenantDbInfoHandler 供集群内其他服务直连调用（如未来的租户服务），路由不带网关前缀、
// 不经网关暴露，见 router.go 中 /internal/... 分组的说明。
type TenantDbInfoHandler struct {
	tenantDbInfoService service.TenantDbInfoService
}

func NewTenantDbInfoHandler(tenantDbInfoService service.TenantDbInfoService) *TenantDbInfoHandler {
	return &TenantDbInfoHandler{tenantDbInfoService: tenantDbInfoService}
}

// GetTenantDbInfoInternal 按 tenant_code 返回租户数据库连接信息（含明文 db_password，
// 见 model.Tenant 字段说明）。响应体含敏感凭证，调用方需自行保证传输/存储安全。
func (h *TenantDbInfoHandler) GetTenantDbInfoInternal(c *gin.Context) {
	tenantCode := c.Param("tenantCode")
	if tenantCode == "" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "tenantCode is required"})
		return
	}

	info, err := h.tenantDbInfoService.GetTenantDbInfoInternal(c.Request.Context(), tenantCode)
	if err != nil {
		if errors.Is(err, service.ErrTenantCodeNotFound) {
			c.JSON(http.StatusNotFound, gin.H{"error": "tenant not found"})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.JSON(http.StatusOK, info)
}
