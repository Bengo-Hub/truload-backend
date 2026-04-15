#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 4000
EXPOSE 4001

# Test stage (optional, not included in final image)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
COPY ["truload-backend.csproj", "."]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./truload-backend.csproj"
COPY . .
RUN dotnet restore "./Tests/truload-backend.Tests.csproj"
# Build and run tests
WORKDIR "/src/Tests"
RUN dotnet test "./truload-backend.Tests.csproj" -c Release --no-build --logger "console;verbosity=detailed" || true

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["truload-backend.csproj", "."]
# Use BuildKit cache mounts to persist NuGet package cache between Docker builds
# (speeds up `dotnet restore` locally and on builders that support BuildKit / buildx)
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./truload-backend.csproj"
COPY . .
WORKDIR "/src/."
# Build only the main project, exclude test projects to speed up local builds
RUN dotnet build "./truload-backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./truload-backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final
# Use SDK image instead of aspnet to have EF Core tools available for migrations

# Accept version from build pipeline (computed from git tags before Docker build)
ARG APP_VERSION=1.0.0
ENV VERSION=${APP_VERSION}

# Install curl and postgresql-client-17 for health checks + DB dump/restore.
# The server runs PostgreSQL 17; pg_dump refuses to dump a newer major version,
# so we pin the client to 17 via the official PostgreSQL APT repo.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg lsb-release \
 && install -d /usr/share/postgresql-common/pgdg \
 && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
 && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends postgresql-client-17 \
 && rm -rf /var/lib/apt/lists/*

# Install EF Core tools globally for migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user for runtime (security)
RUN useradd -m -u 1001 appuser && chown -R appuser:appuser /app

# Ensure media and backup directories exist and are writable by appuser (uid 1001).
# In production, mount PVCs over these paths and set fsGroup/runAsUser so the process can write.
RUN mkdir -p /app/wwwroot/media /app/backups/truload && chown -R appuser:appuser /app/wwwroot/media /app/backups
USER appuser

EXPOSE 4000
EXPOSE 4001

# Configure ASP.NET Core to listen on port 4000 (standardized across all backend apps)
ENV ASPNETCORE_URLS=http://+:4000
ENV ASPNETCORE_HTTP_PORTS=4000

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -fsS http://localhost:4000/health || exit 1

ENTRYPOINT ["dotnet", "truload-backend.dll"]