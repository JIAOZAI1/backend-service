package repository

import (
	"context"
	"errors"
	"time"

	"github.com/redis/go-redis/v9"
)

var ErrRefreshTokenNotFound = errors.New("repository: refresh token not found")

const (
	refreshTokenKeyPrefix = "sso:refresh:"
	blacklistKeyPrefix    = "sso:blacklist:"
)

// TokenRepository 负责 refresh token 的存储（登出/续期校验用）与 access token 黑名单。
type TokenRepository interface {
	SaveRefreshToken(ctx context.Context, jti string, userID uint64, ttl time.Duration) error
	GetRefreshTokenOwner(ctx context.Context, jti string) (uint64, error)
	DeleteRefreshToken(ctx context.Context, jti string) error
	BlacklistAccessToken(ctx context.Context, jti string, ttl time.Duration) error
	IsAccessTokenBlacklisted(ctx context.Context, jti string) (bool, error)
}

type tokenRepository struct {
	rdb *redis.Client
}

func NewTokenRepository(rdb *redis.Client) TokenRepository {
	return &tokenRepository{rdb: rdb}
}

func (r *tokenRepository) SaveRefreshToken(ctx context.Context, jti string, userID uint64, ttl time.Duration) error {
	return r.rdb.Set(ctx, refreshTokenKeyPrefix+jti, userID, ttl).Err()
}

func (r *tokenRepository) GetRefreshTokenOwner(ctx context.Context, jti string) (uint64, error) {
	val, err := r.rdb.Get(ctx, refreshTokenKeyPrefix+jti).Uint64()
	if errors.Is(err, redis.Nil) {
		return 0, ErrRefreshTokenNotFound
	}
	if err != nil {
		return 0, err
	}
	return val, nil
}

func (r *tokenRepository) DeleteRefreshToken(ctx context.Context, jti string) error {
	return r.rdb.Del(ctx, refreshTokenKeyPrefix+jti).Err()
}

func (r *tokenRepository) BlacklistAccessToken(ctx context.Context, jti string, ttl time.Duration) error {
	if ttl <= 0 {
		return nil
	}
	return r.rdb.Set(ctx, blacklistKeyPrefix+jti, 1, ttl).Err()
}

func (r *tokenRepository) IsAccessTokenBlacklisted(ctx context.Context, jti string) (bool, error) {
	n, err := r.rdb.Exists(ctx, blacklistKeyPrefix+jti).Result()
	if err != nil {
		return false, err
	}
	return n > 0, nil
}
