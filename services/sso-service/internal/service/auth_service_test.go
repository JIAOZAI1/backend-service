package service

import (
	"context"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
	"github.com/company/sso-service/pkg/jwtutil"
)

type fakeUserRepo struct {
	byUsername map[string]*model.User
	byEmail    map[string]*model.User
	byID       map[uint64]*model.User
	nextID     uint64
}

func newFakeUserRepo() *fakeUserRepo {
	return &fakeUserRepo{
		byUsername: map[string]*model.User{},
		byEmail:    map[string]*model.User{},
		byID:       map[uint64]*model.User{},
	}
}

func (f *fakeUserRepo) Create(_ context.Context, u *model.User) error {
	f.nextID++
	u.ID = f.nextID
	f.byUsername[u.Username] = u
	f.byEmail[u.Email] = u
	f.byID[u.ID] = u
	return nil
}

func (f *fakeUserRepo) FindByUsername(_ context.Context, username string) (*model.User, error) {
	if u, ok := f.byUsername[username]; ok {
		return u, nil
	}
	return nil, repository.ErrUserNotFound
}

func (f *fakeUserRepo) FindByEmail(_ context.Context, email string) (*model.User, error) {
	if u, ok := f.byEmail[email]; ok {
		return u, nil
	}
	return nil, repository.ErrUserNotFound
}

func (f *fakeUserRepo) FindByID(_ context.Context, id uint64) (*model.User, error) {
	if u, ok := f.byID[id]; ok {
		return u, nil
	}
	return nil, repository.ErrUserNotFound
}

type fakeTokenRepo struct {
	refresh   map[string]uint64
	blacklist map[string]bool
}

func newFakeTokenRepo() *fakeTokenRepo {
	return &fakeTokenRepo{
		refresh:   map[string]uint64{},
		blacklist: map[string]bool{},
	}
}

func (f *fakeTokenRepo) SaveRefreshToken(_ context.Context, jti string, userID uint64, _ time.Duration) error {
	f.refresh[jti] = userID
	return nil
}

func (f *fakeTokenRepo) GetRefreshTokenOwner(_ context.Context, jti string) (uint64, error) {
	if id, ok := f.refresh[jti]; ok {
		return id, nil
	}
	return 0, repository.ErrRefreshTokenNotFound
}

func (f *fakeTokenRepo) DeleteRefreshToken(_ context.Context, jti string) error {
	delete(f.refresh, jti)
	return nil
}

func (f *fakeTokenRepo) BlacklistAccessToken(_ context.Context, jti string, _ time.Duration) error {
	f.blacklist[jti] = true
	return nil
}

func (f *fakeTokenRepo) IsAccessTokenBlacklisted(_ context.Context, jti string) (bool, error) {
	return f.blacklist[jti], nil
}

func newTestService() (AuthService, *fakeUserRepo, *fakeTokenRepo) {
	userRepo := newFakeUserRepo()
	tokenRepo := newFakeTokenRepo()
	issuer := jwtutil.NewIssuer("test-secret", "sso-service-test")
	svc := NewAuthService(userRepo, tokenRepo, issuer, time.Minute, time.Hour)
	return svc, userRepo, tokenRepo
}

func TestRegister_Success(t *testing.T) {
	svc, _, _ := newTestService()

	resp, err := svc.Register(context.Background(), model.RegisterRequest{
		Username: "alice",
		Email:    "alice@example.com",
		Password: "password123",
	})

	require.NoError(t, err)
	assert.Equal(t, "alice", resp.Username)
	assert.Equal(t, "alice@example.com", resp.Email)
}

func TestRegister_DuplicateUsername(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "a1@example.com", Password: "password123"})
	require.NoError(t, err)

	_, err = svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "a2@example.com", Password: "password123"})
	assert.ErrorIs(t, err, ErrUsernameTaken)
}

func TestRegister_DuplicateEmail(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "dup@example.com", Password: "password123"})
	require.NoError(t, err)

	_, err = svc.Register(ctx, model.RegisterRequest{Username: "bob", Email: "dup@example.com", Password: "password123"})
	assert.ErrorIs(t, err, ErrEmailTaken)
}

func TestLogin_Success(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "alice@example.com", Password: "password123"})
	require.NoError(t, err)

	tokens, err := svc.Login(ctx, model.LoginRequest{Username: "alice", Password: "password123"})
	require.NoError(t, err)
	assert.NotEmpty(t, tokens.AccessToken)
	assert.NotEmpty(t, tokens.RefreshToken)
	assert.Equal(t, "Bearer", tokens.TokenType)
}

func TestLogin_WrongPassword(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "alice@example.com", Password: "password123"})
	require.NoError(t, err)

	_, err = svc.Login(ctx, model.LoginRequest{Username: "alice", Password: "wrong-password"})
	assert.ErrorIs(t, err, ErrInvalidCredentials)
}

func TestLogin_UnknownUser(t *testing.T) {
	svc, _, _ := newTestService()

	_, err := svc.Login(context.Background(), model.LoginRequest{Username: "ghost", Password: "password123"})
	assert.ErrorIs(t, err, ErrInvalidCredentials)
}

func TestRefresh_Success_RotatesToken(t *testing.T) {
	svc, _, tokenRepo := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "alice@example.com", Password: "password123"})
	require.NoError(t, err)

	tokens, err := svc.Login(ctx, model.LoginRequest{Username: "alice", Password: "password123"})
	require.NoError(t, err)
	assert.Len(t, tokenRepo.refresh, 1)

	newTokens, err := svc.Refresh(ctx, model.RefreshRequest{RefreshToken: tokens.RefreshToken})
	require.NoError(t, err)
	assert.NotEqual(t, tokens.RefreshToken, newTokens.RefreshToken)

	// 旧 refresh token 应已失效
	_, err = svc.Refresh(ctx, model.RefreshRequest{RefreshToken: tokens.RefreshToken})
	assert.ErrorIs(t, err, ErrInvalidRefreshToken)
}

func TestRefresh_InvalidToken(t *testing.T) {
	svc, _, _ := newTestService()

	_, err := svc.Refresh(context.Background(), model.RefreshRequest{RefreshToken: "not-a-jwt"})
	assert.ErrorIs(t, err, ErrInvalidRefreshToken)
}

func TestLogout_RevokesRefreshToken(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	_, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "alice@example.com", Password: "password123"})
	require.NoError(t, err)

	tokens, err := svc.Login(ctx, model.LoginRequest{Username: "alice", Password: "password123"})
	require.NoError(t, err)

	err = svc.Logout(ctx, model.LogoutRequest{RefreshToken: tokens.RefreshToken})
	require.NoError(t, err)

	_, err = svc.Refresh(ctx, model.RefreshRequest{RefreshToken: tokens.RefreshToken})
	assert.ErrorIs(t, err, ErrInvalidRefreshToken)
}

func TestGetProfile_Success(t *testing.T) {
	svc, _, _ := newTestService()
	ctx := context.Background()

	created, err := svc.Register(ctx, model.RegisterRequest{Username: "alice", Email: "alice@example.com", Password: "password123"})
	require.NoError(t, err)

	profile, err := svc.GetProfile(ctx, created.ID)
	require.NoError(t, err)
	assert.Equal(t, "alice", profile.Username)
}
