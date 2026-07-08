package service

import (
	"context"
	"errors"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
)

var (
	ErrRoleNotFound     = errors.New("service: role not found")
	ErrRoleNameTaken    = errors.New("service: role name already exists")
	ErrRoleAlreadyOwned = errors.New("service: user already has this role")
	ErrUserNotFound     = errors.New("service: user not found")
)

type RoleService interface {
	ListRoles(ctx context.Context) ([]model.RoleResponse, error)
	CreateRole(ctx context.Context, req model.CreateRoleRequest) (model.RoleResponse, error)
	AssignRoleToUser(ctx context.Context, userID uint64, roleName string) error
	RemoveRoleFromUser(ctx context.Context, userID uint64, roleName string) error
	ListUserRoles(ctx context.Context, userID uint64) ([]model.RoleResponse, error)
}

type roleService struct {
	roleRepo repository.RoleRepository
	userRepo repository.UserRepository
}

func NewRoleService(roleRepo repository.RoleRepository, userRepo repository.UserRepository) RoleService {
	return &roleService{roleRepo: roleRepo, userRepo: userRepo}
}

func (s *roleService) ListRoles(ctx context.Context) ([]model.RoleResponse, error) {
	roles, err := s.roleRepo.ListAll(ctx)
	if err != nil {
		return nil, err
	}
	resp := make([]model.RoleResponse, len(roles))
	for i := range roles {
		resp[i] = model.NewRoleResponse(&roles[i])
	}
	return resp, nil
}

func (s *roleService) CreateRole(ctx context.Context, req model.CreateRoleRequest) (model.RoleResponse, error) {
	role := &model.Role{Name: req.Name, Description: req.Description}
	if err := s.roleRepo.Create(ctx, role); err != nil {
		if errors.Is(err, repository.ErrRoleNameTaken) {
			return model.RoleResponse{}, ErrRoleNameTaken
		}
		return model.RoleResponse{}, err
	}
	return model.NewRoleResponse(role), nil
}

func (s *roleService) AssignRoleToUser(ctx context.Context, userID uint64, roleName string) error {
	if _, err := s.userRepo.FindByID(ctx, userID); err != nil {
		if errors.Is(err, repository.ErrUserNotFound) {
			return ErrUserNotFound
		}
		return err
	}

	role, err := s.roleRepo.FindByName(ctx, roleName)
	if err != nil {
		if errors.Is(err, repository.ErrRoleNotFound) {
			return ErrRoleNotFound
		}
		return err
	}

	if err := s.roleRepo.AssignToUser(ctx, userID, role.ID); err != nil {
		if errors.Is(err, repository.ErrRoleAlreadyOwned) {
			return ErrRoleAlreadyOwned
		}
		return err
	}
	return nil
}

func (s *roleService) RemoveRoleFromUser(ctx context.Context, userID uint64, roleName string) error {
	if _, err := s.userRepo.FindByID(ctx, userID); err != nil {
		if errors.Is(err, repository.ErrUserNotFound) {
			return ErrUserNotFound
		}
		return err
	}

	role, err := s.roleRepo.FindByName(ctx, roleName)
	if err != nil {
		if errors.Is(err, repository.ErrRoleNotFound) {
			return ErrRoleNotFound
		}
		return err
	}

	return s.roleRepo.RemoveFromUser(ctx, userID, role.ID)
}

func (s *roleService) ListUserRoles(ctx context.Context, userID uint64) ([]model.RoleResponse, error) {
	if _, err := s.userRepo.FindByID(ctx, userID); err != nil {
		if errors.Is(err, repository.ErrUserNotFound) {
			return nil, ErrUserNotFound
		}
		return nil, err
	}

	roles, err := s.roleRepo.ListByUserID(ctx, userID)
	if err != nil {
		return nil, err
	}
	resp := make([]model.RoleResponse, len(roles))
	for i := range roles {
		resp[i] = model.NewRoleResponse(&roles[i])
	}
	return resp, nil
}
