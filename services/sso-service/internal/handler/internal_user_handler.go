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

type rejectReviewRequest struct {
	ReviewedBy uint64 `json:"reviewedBy" binding:"required"`
}

func (h *InternalUserHandler) GetUserInternal(c *gin.Context) {
	userID, err := strconv.ParseUint(c.Param("userID"), 10, 64)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid user id"})
		return
	}

	user, err := h.internalUserService.GetUserInternal(c.Request.Context(), userID)
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

func (h *InternalUserHandler) ApproveReviewInternal(c *gin.Context) {
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

	if err := h.internalUserService.ApproveReviewInternal(c.Request.Context(), userID, req.ReviewedBy); err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			c.JSON(http.StatusNotFound, gin.H{"error": "user not found"})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.Status(http.StatusNoContent)
}

// RejectReviewInternal 拒绝审核并软删除该用户。拒绝不可撤销：软删除后本用户不再被
// GetUserInternal/ApproveReviewInternal/ListUsersInternal 查到，重复调用会返回 404。
func (h *InternalUserHandler) RejectReviewInternal(c *gin.Context) {
	userID, err := strconv.ParseUint(c.Param("userID"), 10, 64)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid user id"})
		return
	}

	var req rejectReviewRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	if err := h.internalUserService.RejectReviewInternal(c.Request.Context(), userID, req.ReviewedBy); err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			c.JSON(http.StatusNotFound, gin.H{"error": "user not found"})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.Status(http.StatusNoContent)
}

// ListUsersInternal 分页查询指定审核状态的用户，供 admin-service 的开户向导展示待审核列表。
// 查询参数遵循规范第 16.4 章：page/pageSize/sortBy/sortOrder，reviewStatus 默认 pending。
func (h *InternalUserHandler) ListUsersInternal(c *gin.Context) {
	page, _ := strconv.Atoi(c.Query("page"))
	if page <= 0 {
		page = 1
	}
	pageSize, _ := strconv.Atoi(c.Query("pageSize"))
	if pageSize <= 0 || pageSize > 200 {
		pageSize = 20
	}

	result, err := h.internalUserService.ListUsersInternal(
		c.Request.Context(),
		c.Query("reviewStatus"),
		page,
		pageSize,
		c.Query("sortBy"),
		c.Query("sortOrder"),
	)
	if err != nil {
		if errors.Is(err, service.ErrInvalidSortBy) || errors.Is(err, service.ErrInvalidReviewStatus) {
			c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
			return
		}
		c.JSON(http.StatusInternalServerError, gin.H{"error": "internal server error"})
		return
	}

	c.JSON(http.StatusOK, result)
}
