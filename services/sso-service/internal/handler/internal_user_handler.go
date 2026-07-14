package handler

import (
	"errors"
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"

	"github.com/company/sso-service/internal/service"
)

// InternalUserHandler 供集群内其他服务直连调用（如 admin-service 审核开户流程），
// 路由不带网关前缀、不经网关暴露，见 router.go 中 /internal/... 分组的说明。
type InternalUserHandler struct {
	internalUserService service.InternalUserService
}

func NewInternalUserHandler(internalUserService service.InternalUserService) *InternalUserHandler {
	return &InternalUserHandler{internalUserService: internalUserService}
}

type approveReviewRequest struct {
	ReviewedBy uint64 `json:"reviewedBy" binding:"required"`
}

func (h *InternalUserHandler) GetUser(c *gin.Context) {
	userID, err := strconv.ParseUint(c.Param("userID"), 10, 64)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid user id"})
		return
	}

	user, err := h.internalUserService.GetUser(c.Request.Context(), userID)
	if err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			c.JSON(http.StatusNotFound, gin.H{"error": "user not found"})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.JSON(http.StatusOK, user)
}

func (h *InternalUserHandler) ApproveReview(c *gin.Context) {
	userID, err := strconv.ParseUint(c.Param("userID"), 10, 64)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid user id"})
		return
	}

	var req approveReviewRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	if err := h.internalUserService.ApproveReview(c.Request.Context(), userID, req.ReviewedBy); err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			c.JSON(http.StatusNotFound, gin.H{"error": "user not found"})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.Status(http.StatusNoContent)
}
