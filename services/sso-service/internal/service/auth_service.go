package service

import (
	"context"
	"errors"
	"time"

	"github.com/google/uuid"
	"golang.org/x/crypto/bcrypt"

	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/internal/repository"
	"github.com/company/sso-service/pkg/jwtutil"
)

var (
	ErrUsernameTaken       = errors.New("service: username already taken")
	ErrEmailTaken          = errors.New("service: email already registered")
	ErrInvalidCredentials  = errors.New("service: invalid username or password")
	ErrUserDisabled        = errors.New("service: user disabled")
	ErrInvalidRefreshToken = errors.New("service: invalid or expired refresh token")
)

type AuthService interface {
	Register(ctx context.Context, req model.RegisterRequest) (model.UserResponse, error)
	Login(ctx context.Context, req model.LoginRequest) (model.TokenPair, error)
	Refresh(ctx context.Context, req model.RefreshRequest) (model.TokenPair, error)
	Logout(ctx context.Context, req model.LogoutRequest) error
	GetProfile(ctx context.Context, userID uint64) (model.UserResponse, error)
}

type authService struct {
	userRepo   repository.UserRepository
	tokenRepo  repository.TokenRepository
	jwtIssuer  *jwtutil.Issuer
	accessTTL  time.Duration
	refreshTTL time.Duration
}

func NewAuthService(
	userRepo repository.UserRepository,
	tokenRepo repository.TokenRepository,
	jwtIssuer *jwtutil.Issuer,
	accessTTL, refreshTTL time.Duration,
) AuthService {
	return &authService{
		userRepo:   userRepo,
		tokenRepo:  tokenRepo,
		jwtIssuer:  jwtIssuer,
		accessTTL:  accessTTL,
		refreshTTL: refreshTTL,
	}
}

func (s *authService) Register(ctx context.Context, req model.RegisterRequest) (model.UserResponse, error) {
	if _, err := s.userRepo.FindByUsername(ctx, req.Username); err == nil {
		return model.UserResponse{}, ErrUsernameTaken
	} else if !errors.Is(err, repository.ErrUserNotFound) {
		return model.UserResponse{}, err
	}

	if _, err := s.userRepo.FindByEmail(ctx, req.Email); err == nil {
		return model.UserResponse{}, ErrEmailTaken
	} else if !errors.Is(err, repository.ErrUserNotFound) {
		return model.UserResponse{}, err
	}

	hash, err := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)
	if err != nil {
		return model.UserResponse{}, err
	}

	u := &model.User{
		Username:     req.Username,
		Email:        req.Email,
		PasswordHash: string(hash),
		Status:       model.UserStatusActive,
	}
	if err := s.userRepo.Create(ctx, u); err != nil {
		return model.UserResponse{}, err
	}

	return model.NewUserResponse(u), nil
}

func (s *authService) Login(ctx context.Context, req model.LoginRequest) (model.TokenPair, error) {
	u, err := s.userRepo.FindByUsername(ctx, req.Username)
	if errors.Is(err, repository.ErrUserNotFound) {
		return model.TokenPair{}, ErrInvalidCredentials
	}
	if err != nil {
		return model.TokenPair{}, err
	}

	if u.Status != model.UserStatusActive {
		return model.TokenPair{}, ErrUserDisabled
	}

	if err := bcrypt.CompareHashAndPassword([]byte(u.PasswordHash), []byte(req.Password)); err != nil {
		return model.TokenPair{}, ErrInvalidCredentials
	}

	return s.issueTokenPair(ctx, u)
}

func (s *authService) Refresh(ctx context.Context, req model.RefreshRequest) (model.TokenPair, error) {
	claims, err := s.jwtIssuer.Parse(req.RefreshToken)
	if err != nil {
		return model.TokenPair{}, ErrInvalidRefreshToken
	}

	ownerID, err := s.tokenRepo.GetRefreshTokenOwner(ctx, claims.ID)
	if errors.Is(err, repository.ErrRefreshTokenNotFound) {
		return model.TokenPair{}, ErrInvalidRefreshToken
	}
	if err != nil {
		return model.TokenPair{}, err
	}
	if ownerID != claims.UserID {
		return model.TokenPair{}, ErrInvalidRefreshToken
	}

	u, err := s.userRepo.FindByID(ctx, claims.UserID)
	if errors.Is(err, repository.ErrUserNotFound) {
		return model.TokenPair{}, ErrInvalidRefreshToken
	}
	if err != nil {
		return model.TokenPair{}, err
	}
	if u.Status != model.UserStatusActive {
		return model.TokenPair{}, ErrUserDisabled
	}

	// 续期采用 refresh token 轮换：旧 token 立即失效，签发新的一对
	if err := s.tokenRepo.DeleteRefreshToken(ctx, claims.ID); err != nil {
		return model.TokenPair{}, err
	}

	return s.issueTokenPair(ctx, u)
}

func (s *authService) Logout(ctx context.Context, req model.LogoutRequest) error {
	claims, err := s.jwtIssuer.Parse(req.RefreshToken)
	if err != nil {
		return ErrInvalidRefreshToken
	}

	if err := s.tokenRepo.DeleteRefreshToken(ctx, claims.ID); err != nil {
		return err
	}

	return nil
}

func (s *authService) GetProfile(ctx context.Context, userID uint64) (model.UserResponse, error) {
	u, err := s.userRepo.FindByID(ctx, userID)
	if errors.Is(err, repository.ErrUserNotFound) {
		return model.UserResponse{}, ErrInvalidCredentials
	}
	if err != nil {
		return model.UserResponse{}, err
	}
	return model.NewUserResponse(u), nil
}

func (s *authService) issueTokenPair(ctx context.Context, u *model.User) (model.TokenPair, error) {
	accessJTI := uuid.NewString()
	refreshJTI := uuid.NewString()

	accessToken, err := s.jwtIssuer.Issue(u.ID, u.Username, s.accessTTL, accessJTI)
	if err != nil {
		return model.TokenPair{}, err
	}

	refreshToken, err := s.jwtIssuer.Issue(u.ID, u.Username, s.refreshTTL, refreshJTI)
	if err != nil {
		return model.TokenPair{}, err
	}

	if err := s.tokenRepo.SaveRefreshToken(ctx, refreshJTI, u.ID, s.refreshTTL); err != nil {
		return model.TokenPair{}, err
	}

	return model.TokenPair{
		AccessToken:  accessToken,
		RefreshToken: refreshToken,
		TokenType:    "Bearer",
		ExpiresIn:    int64(s.accessTTL.Seconds()),
	}, nil
}
