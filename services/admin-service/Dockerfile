FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

COPY AdminService.slnx .
COPY src/AdminService.Api/AdminService.Api.csproj src/AdminService.Api/
COPY src/AdminService.Application/AdminService.Application.csproj src/AdminService.Application/
COPY src/AdminService.Domain/AdminService.Domain.csproj src/AdminService.Domain/
COPY src/AdminService.Infrastructure/AdminService.Infrastructure.csproj src/AdminService.Infrastructure/
RUN dotnet restore src/AdminService.Api/AdminService.Api.csproj

COPY src src
RUN dotnet publish src/AdminService.Api/AdminService.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=builder /out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AdminService.Api.dll"]
