package middleware_test

import (
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/assert"

	"github.com/company/sso-service/internal/middleware"
)

func newTestRouter(expectedToken string) *gin.Engine {
	r := gin.New()
	r.GET("/protected", middleware.RequireInternalToken(expectedToken), func(c *gin.Context) {
		c.Status(http.StatusOK)
	})
	return r
}

func TestRequireInternalToken_ValidToken_Passes(t *testing.T) {
	r := newTestRouter("secret-token")

	req := httptest.NewRequest(http.MethodGet, "/protected", nil)
	req.Header.Set(middleware.InternalTokenHeader, "secret-token")
	w := httptest.NewRecorder()
	r.ServeHTTP(w, req)

	assert.Equal(t, http.StatusOK, w.Code)
}

func TestRequireInternalToken_MissingToken_Rejects(t *testing.T) {
	r := newTestRouter("secret-token")

	req := httptest.NewRequest(http.MethodGet, "/protected", nil)
	w := httptest.NewRecorder()
	r.ServeHTTP(w, req)

	assert.Equal(t, http.StatusUnauthorized, w.Code)
}

func TestRequireInternalToken_WrongToken_Rejects(t *testing.T) {
	r := newTestRouter("secret-token")

	req := httptest.NewRequest(http.MethodGet, "/protected", nil)
	req.Header.Set(middleware.InternalTokenHeader, "wrong-token")
	w := httptest.NewRecorder()
	r.ServeHTTP(w, req)

	assert.Equal(t, http.StatusUnauthorized, w.Code)
}
