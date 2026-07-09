FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

COPY BackendJobService.slnx .
COPY src/BackendJobService.Api/BackendJobService.Api.csproj src/BackendJobService.Api/
COPY src/BackendJobService.Application/BackendJobService.Application.csproj src/BackendJobService.Application/
COPY src/BackendJobService.Domain/BackendJobService.Domain.csproj src/BackendJobService.Domain/
COPY src/BackendJobService.Infrastructure/BackendJobService.Infrastructure.csproj src/BackendJobService.Infrastructure/
COPY src/BackendJobService.Contracts/BackendJobService.Contracts.csproj src/BackendJobService.Contracts/
RUN dotnet restore src/BackendJobService.Api/BackendJobService.Api.csproj

COPY src src
RUN dotnet publish src/BackendJobService.Api/BackendJobService.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=builder /out .

# 插件 DLL 挂载点：固定目录，服务启动时扫描加载
RUN mkdir -p /app/plugins

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BackendJobService.Api.dll"]
