.PHONY: run test build publish docker-build migrate-up migrate-down

DB_URL ?= mysql://backend_job_service_user:$(MYSQL_PASSWORD)@tcp(127.0.0.1:3306)/backend_job_service_db

run:
	dotnet run --project src/BackendJobService.Api

test:
	dotnet test

build:
	dotnet build -c Release

publish:
	dotnet publish -c Release

docker-build:
	docker build -t backend-job-service .

migrate-up:
	migrate -path migrations -database "$(DB_URL)" up

migrate-down:
	migrate -path migrations -database "$(DB_URL)" down 1
