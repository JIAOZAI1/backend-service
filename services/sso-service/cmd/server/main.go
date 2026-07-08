package main

import (
	"context"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/redis/go-redis/v9"
	"gorm.io/driver/mysql"
	"gorm.io/gorm"

	"github.com/company/sso-service/internal/config"
	"github.com/company/sso-service/internal/handler"
	"github.com/company/sso-service/internal/repository"
	"github.com/company/sso-service/internal/service"
	"github.com/company/sso-service/pkg/jwtutil"
)

func main() {
	env := os.Getenv("SSO_APP_ENV")
	if env == "" {
		env = "dev"
	}

	cfg, err := config.Load(env)
	if err != nil {
		log.Fatalf("load config: %v", err)
	}

	db, err := gorm.Open(mysql.Open(cfg.MySQL.DSN), &gorm.Config{})
	if err != nil {
		log.Fatalf("connect mysql: %v", err)
	}

	rdb := redis.NewClient(&redis.Options{
		Addr:     cfg.Redis.Addr,
		Password: cfg.Redis.Password,
		DB:       cfg.Redis.DB,
	})
	if err := rdb.Ping(context.Background()).Err(); err != nil {
		log.Fatalf("connect redis: %v", err)
	}

	userRepo := repository.NewUserRepository(db)
	tokenRepo := repository.NewTokenRepository(rdb)

	jwtIssuer := jwtutil.NewIssuer(cfg.JWT.Secret, cfg.JWT.Issuer)

	authService := service.NewAuthService(userRepo, tokenRepo, jwtIssuer, cfg.JWT.AccessTokenTTL, cfg.JWT.RefreshTokenTTL)
	authHandler := handler.NewAuthHandler(authService)

	router := handler.NewRouter(authHandler, jwtIssuer, tokenRepo)

	srv := &http.Server{
		Addr:              fmt.Sprintf(":%d", cfg.App.Port),
		Handler:           router,
		ReadHeaderTimeout: 5 * time.Second,
	}

	go func() {
		log.Printf("sso-service listening on :%d (env=%s)", cfg.App.Port, cfg.App.Env)
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			log.Fatalf("server error: %v", err)
		}
	}()

	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		log.Fatalf("graceful shutdown failed: %v", err)
	}
	log.Println("sso-service stopped")
}
