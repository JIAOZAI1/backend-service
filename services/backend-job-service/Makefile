.PHONY: run test build publish docker-build migrate-up migrate-down

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
	dotnet ef database update --project src/BackendJobService.Infrastructure --startup-project src/BackendJobService.Api

migrate-down:
	dotnet ef database update $(TARGET_MIGRATION) --project src/BackendJobService.Infrastructure --startup-project src/BackendJobService.Api
