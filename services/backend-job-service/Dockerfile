FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

COPY BackendJobService.slnx .
COPY src/BackendJobService.Api/BackendJobService.Api.csproj src/BackendJobService.Api/
COPY src/BackendJobService.Application/BackendJobService.Application.csproj src/BackendJobService.Application/
COPY src/BackendJobService.Domain/BackendJobService.Domain.csproj src/BackendJobService.Domain/
COPY src/BackendJobService.Infrastructure/BackendJobService.Infrastructure.csproj src/BackendJobService.Infrastructure/
COPY src/BackendJobService.Contracts/BackendJobService.Contracts.csproj src/BackendJobService.Contracts/
COPY src/BackendJobService.Plugins/BackendJobService.Plugins.csproj src/BackendJobService.Plugins/
RUN dotnet restore src/BackendJobService.Api/BackendJobService.Api.csproj \
    && dotnet restore src/BackendJobService.Plugins/BackendJobService.Plugins.csproj

COPY src src
RUN dotnet publish src/BackendJobService.Api/BackendJobService.Api.csproj -c Release -o /out --no-restore
# 内置插件单独发布到 plugins 目录（Api 不引用 Plugins 项目，插件靠运行时反射加载）
RUN dotnet publish src/BackendJobService.Plugins/BackendJobService.Plugins.csproj -c Release -o /out-plugins --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=builder /out .

# 插件目录：内置插件随镜像发布，额外插件仍可挂载进来，服务启动时扫描加载
COPY --from=builder /out-plugins /app/plugins

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BackendJobService.Api.dll"]
