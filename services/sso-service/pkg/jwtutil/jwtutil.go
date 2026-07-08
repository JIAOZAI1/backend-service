// Package jwtutil 封装 JWT 的签发与解析，不耦合业务逻辑，可被其他服务安全引用。
package jwtutil

import (
	"errors"
	"time"

	"github.com/golang-jwt/jwt/v5"
)

var (
	ErrInvalidToken = errors.New("jwtutil: invalid token")
	ErrExpiredToken = errors.New("jwtutil: expired token")
)

type Claims struct {
	UserID   uint64 `json:"userId"`
	Username string `json:"username"`
	jwt.RegisteredClaims
}

type Issuer struct {
	secret []byte
	issuer string
}

func NewIssuer(secret, issuer string) *Issuer {
	return &Issuer{secret: []byte(secret), issuer: issuer}
}

func (i *Issuer) Issue(userID uint64, username string, ttl time.Duration, jti string) (string, error) {
	now := time.Now()
	claims := Claims{
		UserID:   userID,
		Username: username,
		RegisteredClaims: jwt.RegisteredClaims{
			ID:        jti,
			Issuer:    i.issuer,
			Subject:   username,
			IssuedAt:  jwt.NewNumericDate(now),
			NotBefore: jwt.NewNumericDate(now),
			ExpiresAt: jwt.NewNumericDate(now.Add(ttl)),
		},
	}
	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString(i.secret)
}

func (i *Issuer) Parse(tokenStr string) (*Claims, error) {
	claims := &Claims{}
	token, err := jwt.ParseWithClaims(tokenStr, claims, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, ErrInvalidToken
		}
		return i.secret, nil
	})
	if err != nil {
		if errors.Is(err, jwt.ErrTokenExpired) {
			return nil, ErrExpiredToken
		}
		return nil, ErrInvalidToken
	}
	if !token.Valid {
		return nil, ErrInvalidToken
	}
	return claims, nil
}
