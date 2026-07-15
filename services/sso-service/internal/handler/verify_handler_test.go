package handler_test

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/company/sso-service/internal/handler"
	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/pkg/jwtutil"
)

type stubBlacklist struct{ blacklisted bool }

func (s stubBlacklist) IsAccessTokenBlacklisted(_ context.Context, _ string) (bool, error) {
	return s.blacklisted, nil
}

func TestVerifyRoute_MissingToken(t *testing.T) {
	router := handler.NewRouter(nil, nil, nil, jwtutil.NewIssuer("secret", "issuer"), noopBlacklist{}, fakeRoleLister{}, fakeTenantLister{}, "test-internal-token")

	req := httptest.NewRequest(http.MethodGet, "/internal/auth/verify", nil)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusUnauthorized, w.Code)
}

func TestVerifyRoute_ValidToken(t *testing.T) {
	issuer := jwtutil.NewIssuer("secret", "issuer")
	lister := fakeRoleLister{roles: map[uint64][]model.Role{
		1: {{ID: 1, Name: model.DefaultRoleName}, {ID: 2, Name: "admin"}},
	}}
	router := handler.NewRouter(nil, nil, nil, issuer, noopBlacklist{}, lister, fakeTenantLister{}, "test-internal-token")

	token, err := issuer.Issue(1, "alice", time.Minute, "jti-verify-1")
	assert.NoError(t, err)

	req := httptest.NewRequest(http.MethodGet, "/internal/auth/verify", nil)
	// 客户端伪造的身份头不应影响校验端点的输出
	req.Header.Set("X-User-Id", "999")
	req.Header.Set("X-User-Roles", "admin")
	req.Header.Set("Authorization", "Bearer "+token)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusNoContent, w.Code)
	assert.Equal(t, "1", w.Header().Get("X-User-Id"))
	assert.Equal(t, "alice", w.Header().Get("X-Username"))
	assert.Equal(t, "default,admin", w.Header().Get("X-User-Roles"))
	// 没有关联 active 租户时不设置该头
	assert.Empty(t, w.Header().Get("X-Tenant-Code"))
}

func TestVerifyRoute_ValidToken_WithActiveTenant_SetsTenantCodeHeader(t *testing.T) {
	issuer := jwtutil.NewIssuer("secret", "issuer")
	roleLister := fakeRoleLister{roles: map[uint64][]model.Role{1: {{ID: 1, Name: model.DefaultRoleName}}}}
	tenantLister := fakeTenantLister{tenantCodes: map[uint64]string{1: "abcd1234wxyz"}}
	router := handler.NewRouter(nil, nil, nil, issuer, noopBlacklist{}, roleLister, tenantLister, "test-internal-token")

	token, err := issuer.Issue(1, "alice", time.Minute, "jti-verify-3")
	assert.NoError(t, err)

	req := httptest.NewRequest(http.MethodGet, "/internal/auth/verify", nil)
	req.Header.Set("Authorization", "Bearer "+token)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusNoContent, w.Code)
	assert.Equal(t, "abcd1234wxyz", w.Header().Get("X-Tenant-Code"))
}

func TestVerifyRoute_BlacklistedToken(t *testing.T) {
	issuer := jwtutil.NewIssuer("secret", "issuer")
	router := handler.NewRouter(nil, nil, nil, issuer, stubBlacklist{blacklisted: true}, fakeRoleLister{}, fakeTenantLister{}, "test-internal-token")

	token, err := issuer.Issue(1, "alice", time.Minute, "jti-verify-2")
	assert.NoError(t, err)

	req := httptest.NewRequest(http.MethodGet, "/internal/auth/verify", nil)
	req.Header.Set("Authorization", "Bearer "+token)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusUnauthorized, w.Code)
}
