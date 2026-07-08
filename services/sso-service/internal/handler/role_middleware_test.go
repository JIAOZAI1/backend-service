package handler_test

import (
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/company/sso-service/internal/handler"
	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/pkg/jwtutil"
)

func TestRoleRoutes_RequireAuth(t *testing.T) {
	router := handler.NewRouter(nil, nil, jwtutil.NewIssuer("secret", "issuer"), noopBlacklist{}, fakeRoleLister{})

	req := httptest.NewRequest(http.MethodGet, "/sso-service/api/v1/roles", nil)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusUnauthorized, w.Code)
}

func TestRoleRoutes_RejectNonAdmin(t *testing.T) {
	issuer := jwtutil.NewIssuer("secret", "issuer")
	lister := fakeRoleLister{roles: map[uint64][]model.Role{
		1: {{ID: 1, Name: model.DefaultRoleName}},
	}}
	router := handler.NewRouter(nil, nil, issuer, noopBlacklist{}, lister)

	token, err := issuer.Issue(1, "alice", time.Minute, "jti-1")
	assert.NoError(t, err)

	req := httptest.NewRequest(http.MethodGet, "/sso-service/api/v1/roles", nil)
	req.Header.Set("Authorization", "Bearer "+token)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusForbidden, w.Code)
}

func TestRoleRoutes_AllowAdmin(t *testing.T) {
	issuer := jwtutil.NewIssuer("secret", "issuer")
	lister := fakeRoleLister{roles: map[uint64][]model.Role{
		1: {{ID: 2, Name: "admin"}},
	}}
	router := handler.NewRouter(nil, nil, issuer, noopBlacklist{}, lister)

	token, err := issuer.Issue(1, "alice", time.Minute, "jti-2")
	assert.NoError(t, err)

	req := httptest.NewRequest(http.MethodGet, "/sso-service/api/v1/roles", nil)
	req.Header.Set("Authorization", "Bearer "+token)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	// roleHandler 是 nil，真正调用 handler 会 panic 并被 gin.Recovery() 转成 500；
	// 只要不是 401/403，就说明中间件放行了，路由确实到达了 handler。
	assert.NotEqual(t, http.StatusUnauthorized, w.Code)
	assert.NotEqual(t, http.StatusForbidden, w.Code)
}
