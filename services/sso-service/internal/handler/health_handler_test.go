package handler_test

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/stretchr/testify/assert"

	"github.com/company/sso-service/internal/handler"
	"github.com/company/sso-service/internal/model"
	"github.com/company/sso-service/pkg/jwtutil"
)

type noopBlacklist struct{}

func (noopBlacklist) IsAccessTokenBlacklisted(_ context.Context, _ string) (bool, error) {
	return false, nil
}

type fakeRoleLister struct {
	roles map[uint64][]model.Role
}

func (f fakeRoleLister) ListByUserID(_ context.Context, userID uint64) ([]model.Role, error) {
	return f.roles[userID], nil
}

func TestHealthRoute(t *testing.T) {
	router := handler.NewRouter(nil, nil, nil, jwtutil.NewIssuer("secret", "issuer"), noopBlacklist{}, fakeRoleLister{})

	req := httptest.NewRequest(http.MethodGet, "/health", nil)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	assert.Equal(t, http.StatusOK, w.Code)
	assert.JSONEq(t, `{"status":"ok"}`, w.Body.String())
}
