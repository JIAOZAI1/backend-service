package service

import (
	"context"
	"errors"
	"fmt"
	"strings"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
)

// ErrInvalidSortBy 表示请求的 sortBy 不在该资源声明的可排序字段白名单内（规范第 16.4.2 章）。
var ErrInvalidSortBy = errors.New("service: invalid sortBy")

// ErrInvalidReviewStatus 表示请求的 reviewStatus 不是已知的审核状态取值。
var ErrInvalidReviewStatus = errors.New("service: invalid reviewStatus")

var validReviewStatuses = map[string]bool{
	model.UserReviewStatusPending:  true,
	model.UserReviewStatusApproved: true,
	model.UserReviewStatusRejected: true,
}

// userListSortFields 是待审核用户列表接口的可排序字段白名单，由本资源自行声明。
var userListSortFields = map[string]repository.UserSortField{
	"id":        repository.UserSortFieldID,
	"createdAt": repository.UserSortFieldCreatedAt,
}

// InternalUserService 供集群内其他服务（如 admin-service 审核开户流程）直连调用，
// 不经网关，不做角色校验，仅供内部可信调用方使用。方法名带 Internal 后缀，
// 与对外业务接口的方法命名区分（见规范第 16.5 章）。
type InternalUserService interface {
	GetUserInternal(ctx context.Context, userID uint64) (model.UserResponse, error)
	ApproveReviewInternal(ctx context.Context, userID uint64, reviewedBy uint64) error
	// RejectReviewInternal 拒绝审核并软删除该用户，允许其后续用同一 username/email 重新注册。
	// 拒绝不可撤销：软删除后 GetUserInternal/ApproveReviewInternal/ListUsersInternal 均查不到该用户。
	RejectReviewInternal(ctx context.Context, userID uint64, reviewedBy uint64) error
	// ListUsersInternal 分页查询指定审核状态的用户，供开户向导展示待审核列表。
	ListUsersInternal(ctx context.Context, reviewStatus string, page, pageSize int, sortBy, sortOrder string) (model.PagedUserResponse, error)
}

type internalUserService struct {
	userRepo repository.UserRepository
}

func NewInternalUserService(userRepo repository.UserRepository) InternalUserService {
	return &internalUserService{userRepo: userRepo}
}

func (s *internalUserService) GetUserInternal(ctx context.Context, userID uint64) (model.UserResponse, error) {
	u, err := s.userRepo.FindByID(ctx, userID)
	if err != nil {
		if errors.Is(err, repository.ErrUserNotFound) {
			return model.UserResponse{}, ErrUserNotFound
		}
		return model.UserResponse{}, err
	}
	return model.NewUserResponse(u, nil), nil
}

// ApproveReviewInternal 幂等：重复调用同一个已审核用户不会报错。
func (s *internalUserService) ApproveReviewInternal(ctx context.Context, userID uint64, reviewedBy uint64) error {
	err := s.userRepo.ApproveReview(ctx, userID, reviewedBy)
	if errors.Is(err, repository.ErrUserNotFound) {
		return ErrUserNotFound
	}
	return err
}

func (s *internalUserService) RejectReviewInternal(ctx context.Context, userID uint64, reviewedBy uint64) error {
	err := s.userRepo.RejectReview(ctx, userID, reviewedBy)
	if errors.Is(err, repository.ErrUserNotFound) {
		return ErrUserNotFound
	}
	return err
}

func (s *internalUserService) ListUsersInternal(
	ctx context.Context, reviewStatus string, page, pageSize int, sortBy, sortOrder string,
) (model.PagedUserResponse, error) {
	if reviewStatus == "" {
		reviewStatus = model.UserReviewStatusPending
	}
	if !validReviewStatuses[reviewStatus] {
		return model.PagedUserResponse{}, fmt.Errorf("%w: %s", ErrInvalidReviewStatus, reviewStatus)
	}

	if sortBy == "" {
		sortBy = "createdAt"
	}
	sortField, ok := userListSortFields[sortBy]
	if !ok {
		return model.PagedUserResponse{}, fmt.Errorf("%w: %s", ErrInvalidSortBy, sortBy)
	}

	sqlSortOrder := "ASC"
	if strings.EqualFold(sortOrder, "desc") {
		sqlSortOrder = "DESC"
	}

	users, total, err := s.userRepo.ListByReviewStatus(ctx, reviewStatus, page, pageSize, sortField, sqlSortOrder)
	if err != nil {
		return model.PagedUserResponse{}, err
	}

	items := make([]model.UserResponse, 0, len(users))
	for i := range users {
		items = append(items, model.NewUserResponse(&users[i], nil))
	}

	return model.PagedUserResponse{
		Items:    items,
		Page:     page,
		PageSize: pageSize,
		Total:    total,
	}, nil
}
