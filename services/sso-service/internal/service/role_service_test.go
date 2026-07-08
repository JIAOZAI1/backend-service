package service

import (
	"context"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/company/sso-service/internal/model"
)

func newTestRoleService() (RoleService, *fakeUserRepo, *fakeRoleRepo) {
	userRepo := newFakeUserRepo()
	roleRepo := newFakeRoleRepo()
	return NewRoleService(roleRepo, userRepo), userRepo, roleRepo
}

func TestListRoles_IncludesSeeded(t *testing.T) {
	svc, _, _ := newTestRoleService()

	roles, err := svc.ListRoles(context.Background())
	require.NoError(t, err)

	names := make([]string, len(roles))
	for i, r := range roles {
		names[i] = r.Name
	}
	assert.Contains(t, names, model.DefaultRoleName)
	assert.Contains(t, names, "admin")
}

func TestCreateRole_Success(t *testing.T) {
	svc, _, _ := newTestRoleService()

	role, err := svc.CreateRole(context.Background(), model.CreateRoleRequest{Name: "editor", Description: "can edit content"})
	require.NoError(t, err)
	assert.Equal(t, "editor", role.Name)
}

func TestCreateRole_DuplicateName(t *testing.T) {
	svc, _, _ := newTestRoleService()
	ctx := context.Background()

	_, err := svc.CreateRole(ctx, model.CreateRoleRequest{Name: "editor"})
	require.NoError(t, err)

	_, err = svc.CreateRole(ctx, model.CreateRoleRequest{Name: "editor"})
	assert.ErrorIs(t, err, ErrRoleNameTaken)
}

func TestAssignRoleToUser_Success(t *testing.T) {
	svc, userRepo, _ := newTestRoleService()
	ctx := context.Background()

	u := &model.User{Username: "alice", Email: "alice@example.com"}
	require.NoError(t, userRepo.Create(ctx, u))

	err := svc.AssignRoleToUser(ctx, u.ID, "admin")
	require.NoError(t, err)

	roles, err := svc.ListUserRoles(ctx, u.ID)
	require.NoError(t, err)
	require.Len(t, roles, 1)
	assert.Equal(t, "admin", roles[0].Name)
}

func TestAssignRoleToUser_UnknownUser(t *testing.T) {
	svc, _, _ := newTestRoleService()

	err := svc.AssignRoleToUser(context.Background(), 9999, "admin")
	assert.ErrorIs(t, err, ErrUserNotFound)
}

func TestAssignRoleToUser_UnknownRole(t *testing.T) {
	svc, userRepo, _ := newTestRoleService()
	ctx := context.Background()

	u := &model.User{Username: "alice", Email: "alice@example.com"}
	require.NoError(t, userRepo.Create(ctx, u))

	err := svc.AssignRoleToUser(ctx, u.ID, "ghost-role")
	assert.ErrorIs(t, err, ErrRoleNotFound)
}

func TestAssignRoleToUser_AlreadyOwned(t *testing.T) {
	svc, userRepo, _ := newTestRoleService()
	ctx := context.Background()

	u := &model.User{Username: "alice", Email: "alice@example.com"}
	require.NoError(t, userRepo.Create(ctx, u))

	require.NoError(t, svc.AssignRoleToUser(ctx, u.ID, "admin"))
	err := svc.AssignRoleToUser(ctx, u.ID, "admin")
	assert.ErrorIs(t, err, ErrRoleAlreadyOwned)
}

func TestRemoveRoleFromUser_Success(t *testing.T) {
	svc, userRepo, _ := newTestRoleService()
	ctx := context.Background()

	u := &model.User{Username: "alice", Email: "alice@example.com"}
	require.NoError(t, userRepo.Create(ctx, u))
	require.NoError(t, svc.AssignRoleToUser(ctx, u.ID, "admin"))

	err := svc.RemoveRoleFromUser(ctx, u.ID, "admin")
	require.NoError(t, err)

	roles, err := svc.ListUserRoles(ctx, u.ID)
	require.NoError(t, err)
	assert.Empty(t, roles)
}

func TestListUserRoles_UnknownUser(t *testing.T) {
	svc, _, _ := newTestRoleService()

	_, err := svc.ListUserRoles(context.Background(), 9999)
	assert.ErrorIs(t, err, ErrUserNotFound)
}
