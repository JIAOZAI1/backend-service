.PHONY: run test lint build docker-build migrate-up migrate-down

DB_URL ?= mysql://sso_user:$(MYSQL_PASSWORD)@tcp(127.0.0.1:3306)/sso_db

run:
	go run ./cmd/server

test:
	go test ./... -race -cover

lint:
	golangci-lint run

build:
	go build -o bin/server ./cmd/server

docker-build:
	docker build -t sso-service .

migrate-up:
	migrate -path migrations -database "$(DB_URL)" up

migrate-down:
	migrate -path migrations -database "$(DB_URL)" down 1
