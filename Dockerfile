# The FixFlow images for the Compose `full` profile (ARCHITECTURE §16, §20; plan T0.11, P18).
#   docker compose --profile full up -d --build --wait
# Build context: the repository root; .dockerignore lets in only what the build needs.
# Targets: `api` (the default, last stage) serves the API and the built SPA; `migrate` applies EF Core migrations.
# Both run as the base image's non-root `app` user. Tags are pinned exactly; Dependabot proposes updates.

# --- SPA -------------------------------------------------------------------------------------------------------------
# glibc, like the CI runner, so npm ci installs the same native build packages
FROM node:24.22.0-trixie-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json web/.npmrc ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund
COPY web/ ./
# The generated API client is committed, so no gen:api
RUN npm run build

# --- API and migrations bundle ---------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src
# The build-time OpenAPI document starts the app; CI checks the committed one (plan P9)
ENV OpenApiGenerateDocuments=false
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/FixFlow.Domain/FixFlow.Domain.csproj src/FixFlow.Domain/
COPY src/FixFlow.Application/FixFlow.Application.csproj src/FixFlow.Application/
COPY src/FixFlow.Infrastructure/FixFlow.Infrastructure.csproj src/FixFlow.Infrastructure/
COPY src/FixFlow.Hosting/FixFlow.Hosting.csproj src/FixFlow.Hosting/
COPY src/FixFlow.API/FixFlow.API.csproj src/FixFlow.API/
# Packages stay in this layer, not a cache mount: a cached restore layer must never meet an empty package folder.
# NuGet audit fails a vulnerable package here, as in CI.
RUN dotnet restore src/FixFlow.API/FixFlow.API.csproj && dotnet tool restore
COPY src/ src/
RUN dotnet publish src/FixFlow.API/FixFlow.API.csproj -c Release --no-restore -o /out/api

FROM build AS bundle
# dotnet ef runs Program up to Build() as Development to find the DbContext, and Program requires a database password.
# This placeholder only satisfies that check; nothing connects, and the bundle reads the real one when it runs.
RUN Database__Password=design-time-placeholder dotnet ef migrations bundle --configuration Release \
    --project src/FixFlow.Infrastructure --startup-project src/FixFlow.API --output /out/efbundle

# --- Runtime images --------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble AS migrate
WORKDIR /app
COPY --from=bundle /out/efbundle ./
USER $APP_UID
# Reads the API's configuration (connection string, Database__Password) from the environment
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble AS api
# curl only for the Compose healthcheck on /health/ready
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
# Owned by root: the app can read its files but not change them
COPY --from=build /out/api ./
COPY --from=web /web/dist ./wwwroot/
# The Data Protection keys' folder (DataProtection__KeysPath, plan P24). The app user owns it, and a new named volume
# mounted here takes that owner.
RUN mkdir /data-protection-keys && chown $APP_UID /data-protection-keys
USER $APP_UID
ENTRYPOINT ["dotnet", "FixFlow.API.dll"]
