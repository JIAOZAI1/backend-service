package service

import (
	"context"
	"errors"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
)

// InternalUserService 供集群内其他服务（如 admin-service 审核开户流程）直连调用，
// 不经网关，不做角色校验，仅供内部可信调用方使用。方法名带 Internal 后缀，
// 与对外业务接口的方法命名区分（见规范第 16.5 章）。
type InternalUserService interface {
	GetUserInternal(ctx context.Context, userID uint64) (model.UserResponse, error)
	ApproveReviewInternal(ctx context.Context, userID uint64, reviewedBy uint64) error
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
