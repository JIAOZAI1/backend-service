package service

import (
	"context"
	"errors"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/company/sso-service/internal/model"
)

func newTestInternalUserService() (InternalUserService, *fakeUserRepo) {
	userRepo := newFakeUserRepo()
	return NewInternalUserService(userRepo), userRepo
}

func seedUser(t *testing.T, repo *fakeUserRepo, username string) *model.User {
	t.Helper()
	u := &model.User{Username: username, Email: username + "@example.com", ReviewStatus: model.UserReviewStatusPending}
	require.NoError(t, repo.Create(context.Background(), u))
	return u
}

func TestRejectReviewInternal_SoftDeletesAndIsIrreversible(t *testing.T) {
	svc, repo := newTestInternalUserService()
	u := seedUser(t, repo, "alice")

	err := svc.RejectReviewInternal(context.Background(), u.ID, 1)
	require.NoError(t, err)

	// 拒绝后不可撤销：GetUserInternal/ApproveReviewInternal 都应视为不存在
	_, err = svc.GetUserInternal(context.Background(), u.ID)
	assert.ErrorIs(t, err, ErrUserNotFound)

	err = svc.ApproveReviewInternal(context.Background(), u.ID, 1)
	assert.ErrorIs(t, err, ErrUserNotFound)

	// 重复拒绝同一个已拒绝用户也应返回未找到，而不是静默成功
	err = svc.RejectReviewInternal(context.Background(), u.ID, 1)
	assert.ErrorIs(t, err, ErrUserNotFound)
}

func TestRejectReviewInternal_UnknownUser_ReturnsNotFound(t *testing.T) {
	svc, _ := newTestInternalUserService()

	err := svc.RejectReviewInternal(context.Background(), 999, 1)

	assert.ErrorIs(t, err, ErrUserNotFound)
}

func TestListUsersInternal_DefaultsToPending(t *testing.T) {
	svc, repo := newTestInternalUserService()
	pending := seedUser(t, repo, "pending-user")
	approved := seedUser(t, repo, "approved-user")
	require.NoError(t, repo.ApproveReview(context.Background(), approved.ID, 1))

	result, err := svc.ListUsersInternal(context.Background(), "", 1, 20, "", "")

	require.NoError(t, err)
	require.Len(t, result.Items, 1)
	assert.Equal(t, pending.Username, result.Items[0].Username)
	assert.EqualValues(t, 1, result.Total)
}

func TestListUsersInternal_ExcludesRejectedUsers(t *testing.T) {
	svc, repo := newTestInternalUserService()
	u := seedUser(t, repo, "rejected-user")
	require.NoError(t, repo.RejectReview(context.Background(), u.ID, 1))

	result, err := svc.ListUsersInternal(context.Background(), model.UserReviewStatusPending, 1, 20, "", "")

	require.NoError(t, err)
	assert.Empty(t, result.Items)
}

func TestListUsersInternal_FilterByReviewStatus(t *testing.T) {
	svc, repo := newTestInternalUserService()
	u := seedUser(t, repo, "rejected-user")
	require.NoError(t, repo.RejectReview(context.Background(), u.ID, 1))

	result, err := svc.ListUsersInternal(context.Background(), model.UserReviewStatusRejected, 1, 20, "", "")

	require.NoError(t, err)
	require.Len(t, result.Items, 1)
	assert.Equal(t, u.Username, result.Items[0].Username)
}

func TestListUsersInternal_InvalidReviewStatus_ReturnsError(t *testing.T) {
	svc, _ := newTestInternalUserService()

	_, err := svc.ListUsersInternal(context.Background(), "not-a-real-status", 1, 20, "", "")

	assert.True(t, errors.Is(err, ErrInvalidReviewStatus))
}

func TestListUsersInternal_InvalidSortBy_ReturnsError(t *testing.T) {
	svc, _ := newTestInternalUserService()

	_, err := svc.ListUsersInternal(context.Background(), model.UserReviewStatusPending, 1, 20, "notAField", "")

	assert.True(t, errors.Is(err, ErrInvalidSortBy))
}

func TestListUsersInternal_Pagination(t *testing.T) {
	svc, repo := newTestInternalUserService()
	for i := 0; i < 5; i++ {
		seedUser(t, repo, "user"+string(rune('a'+i)))
	}

	page1, err := svc.ListUsersInternal(context.Background(), model.UserReviewStatusPending, 1, 2, "id", "asc")
	require.NoError(t, err)
	assert.Len(t, page1.Items, 2)
	assert.EqualValues(t, 5, page1.Total)

	page3, err := svc.ListUsersInternal(context.Background(), model.UserReviewStatusPending, 3, 2, "id", "asc")
	require.NoError(t, err)
	assert.Len(t, page3.Items, 1)
}
