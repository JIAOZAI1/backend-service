FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

# 构建上下文是仓库根目录（而非 services/admin-service），因为 AdminService.Infrastructure
# 引用了 packages/db-credential-crypto/dotnet 下的共享加解密 SDK（见规范第 10 章，
# 公共代码只放 packages/，服务不允许把它复制进自己目录）。
COPY services/admin-service/AdminService.slnx services/admin-service/
COPY services/admin-service/src/AdminService.Api/AdminService.Api.csproj services/admin-service/src/AdminService.Api/
COPY services/admin-service/src/AdminService.Application/AdminService.Application.csproj services/admin-service/src/AdminService.Application/
COPY services/admin-service/src/AdminService.Domain/AdminService.Domain.csproj services/admin-service/src/AdminService.Domain/
COPY services/admin-service/src/AdminService.Infrastructure/AdminService.Infrastructure.csproj services/admin-service/src/AdminService.Infrastructure/
COPY packages/db-credential-crypto/dotnet/DbCredentialCrypto/DbCredentialCrypto.csproj packages/db-credential-crypto/dotnet/DbCredentialCrypto/
RUN dotnet restore services/admin-service/src/AdminService.Api/AdminService.Api.csproj

COPY services/admin-service/src services/admin-service/src
COPY packages/db-credential-crypto/dotnet/DbCredentialCrypto packages/db-credential-crypto/dotnet/DbCredentialCrypto
RUN dotnet publish services/admin-service/src/AdminService.Api/AdminService.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=builder /out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AdminService.Api.dll"]
