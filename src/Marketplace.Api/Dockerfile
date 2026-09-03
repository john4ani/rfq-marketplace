# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-alpine3.23 AS build
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .editorconfig Directory.Build.props global.json Marketplace.slnx ./
COPY src/Marketplace.Api/Marketplace.Api.csproj src/Marketplace.Api/
COPY src/Marketplace.Application/Marketplace.Application.csproj src/Marketplace.Application/
COPY src/Marketplace.Domain/Marketplace.Domain.csproj src/Marketplace.Domain/
COPY src/Marketplace.Infrastructure/Marketplace.Infrastructure.csproj src/Marketplace.Infrastructure/

RUN dotnet restore src/Marketplace.Api/Marketplace.Api.csproj \
    && dotnet tool restore

COPY src src

RUN dotnet publish src/Marketplace.Api/Marketplace.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /out/api \
      /p:UseAppHost=false \
    && dotnet ef migrations bundle \
      --project src/Marketplace.Infrastructure \
      --startup-project src/Marketplace.Api \
      --configuration Release \
      --self-contained \
      --target-runtime linux-musl-x64 \
      --output /out/migrations/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-alpine3.23 AS runtime
RUN apk add --no-cache curl icu-libs krb5-libs tzdata

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Europe/Bucharest

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /out/api ./
COPY --from=build --chown=$APP_UID:$APP_UID /out/migrations/efbundle ./efbundle

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=4 \
  CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Marketplace.Api.dll"]
